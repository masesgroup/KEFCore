/*
*  Copyright (c) 2022-2026 MASES s.r.l.
*
*  Licensed under the Apache License, Version 2.0 (the "License");
*  you may not use this file except in compliance with the License.
*  You may obtain a copy of the License at
*
*  http://www.apache.org/licenses/LICENSE-2.0
*
*  Unless required by applicable law or agreed to in writing, software
*  distributed under the License is distributed on an "AS IS" BASIS,
*  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
*  See the License for the specific language governing permissions and
*  limitations under the License.
*
*  Refer to LICENSE for more information.
*/

//#define DEBUG_PERFORMANCE

#nullable disable

using Java.Lang;
using Java.Util;
using MASES.KNet.Streams;
using Org.Apache.Kafka.Streams;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using static Org.Apache.Kafka.Streams.Errors.StreamsUncaughtExceptionHandler;

namespace MASES.EntityFrameworkCore.KNet.Storage.Internal
{
    internal readonly struct EventChange(IStreamsChangeManager manager, IEntityType entityType, IKey primaryKey, int partition, long offset, object data)
    {
        public readonly IStreamsChangeManager Manager = manager;
        public readonly IEntityType EntityType = entityType;
        public readonly IKey PrimaryKey = primaryKey;
        public readonly int Partition = partition;
        public readonly long Offset = offset;
        public readonly object Data = data;
    }

    internal interface IStreamsChangeManager
    {
        void ManageChange(IValueGeneratorSelector valueGeneratorSelector, IUpdateAdapter adapter, IEntityType entityType, IKey primaryKey, object data);
    }
    /// <summary>
    /// Central interface for stream management
    /// </summary>
    public interface IStreamsManager
    {
        /// <summary>
        /// Creates and start the topology
        /// </summary>
        void CreateAndStartTopology();
        /// <summary>
        /// Invoked when a new partiton/offset is received for <paramref name="entity"/>
        /// </summary>
        /// <param name="entity">The <see cref="IEntityType"/></param>
        /// <param name="partition">The partiton where the data was stored</param>
        /// <param name="offset">The offset received</param>
        void PartitionOffsetWritten(IEntityType entity, int partition, long offset);
        /// <summary>
        /// Verify if local instance is synchronized with the <see cref="IKafkaCluster"/> instance
        /// </summary>
        bool? EnsureSynchronized(IEntityType entity, long timeout);
    }

    internal class StreamsManager<TStream, TStreamBuilder, TTopology, TStoreSupplier, TTimestampExtractor, TConsumed, TMaterialized, TGlobalKTable, TKTable> : IStreamsManager
        where TStream : class
        where TStreamBuilder : class
        where TTopology : class
        where TStoreSupplier : class
        where TTimestampExtractor : class, IDisposable
        where TConsumed : class
        where TMaterialized : class
        where TGlobalKTable : class
        where TKTable : class
    {
        readonly struct StreamsAssociatedData(TStoreSupplier storeSupplier, TTimestampExtractor extractor, TConsumed consumed,
                                              TMaterialized materialized, TGlobalKTable globalTable, TKTable table,
                                              IDictionary<int, long> creationKnownOffset) : IDisposable
        {
            public readonly TStoreSupplier StoreSupplier = storeSupplier;
            public readonly TConsumed Consumed = consumed;
            public readonly TTimestampExtractor TimestampExtractor = extractor;
            public readonly TMaterialized Materialized = materialized;
            public readonly TGlobalKTable GlobalTable = globalTable;
            public readonly TKTable Table = table;
            public readonly IDictionary<int, long> CurrentRemoteKnownOffset = creationKnownOffset; // expected to be invariant
            public readonly IDictionary<int, long> LatestLocalKnownOffset = new ConcurrentDictionary<int, long>();

            public void UpdateCurrentRemoteKnownOffset(int partition, long offset)
            {
                lock (CurrentRemoteKnownOffset)
                {
                    if (CurrentRemoteKnownOffset.ContainsKey(partition))
                    {
                        CurrentRemoteKnownOffset[partition] = offset;
                    }
                    else
                    {
                        CurrentRemoteKnownOffset.Add(partition, offset);
                    }
                }
            }

            public void UpdateCurrentRemoteKnownOffset(IDictionary<int, long> keyValuePairs)
            {
                var lastKnownPartitions = keyValuePairs.Keys.ToList();

                foreach (var kv in keyValuePairs)
                {
                    lock (CurrentRemoteKnownOffset)
                    {
                        if (CurrentRemoteKnownOffset.ContainsKey(kv.Key))
                        {
                            CurrentRemoteKnownOffset[kv.Key] = kv.Value;
                        }
                        else
                        {
                            CurrentRemoteKnownOffset.Add(kv);
                        }
                        lastKnownPartitions.Remove(kv.Key);
                    }
                }
                if (lastKnownPartitions.Count != 0) // if old stored partition are no more managed...
                {
                    foreach (var item in lastKnownPartitions)
                    {
                        CurrentRemoteKnownOffset.Remove(item);  // ...then remove them
                    }
                }
            }

            public bool IsSynchronized()
            {
                bool[] bools;
                lock (CurrentRemoteKnownOffset)
                {
                    bools = new bool[CurrentRemoteKnownOffset.Count];
                    int index = 0;
                    foreach (var item in CurrentRemoteKnownOffset)
                    {
                        if (item.Value < 0) bools[index] = true; // the topic is empty
                        else if (LatestLocalKnownOffset.TryGetValue(item.Key, out var lastOffset))
                        {
                            bools[index] = lastOffset >= item.Value;
                        }
                        index++;
                    }
                }
                return bools.All((o) => o == true);
            }

            public readonly void Dispose()
            {
                TimestampExtractor?.Dispose();
            }
        }

        // enqueues changes
        private readonly ConcurrentQueue<EventChange> _dataFromStream = new();
        // this dictionary controls the entities
        private readonly System.Collections.Generic.Dictionary<IEntityType, IEntityType> _managedEntities = new(EntityTypeFullNameComparer.Instance);
        // while this one is used to retain the allocated object to avoid their finalization before the streams is completly finalized
        private readonly System.Collections.Generic.Dictionary<IEntityType, StreamsAssociatedData> _storagesForEntities = new(EntityTypeFullNameComparer.Instance);

        private readonly AutoResetEvent _dataReceived;
        private readonly AutoResetEvent _resetEvent;
        private readonly AutoResetEvent _stateChanged;

        private readonly IKafkaCluster _kafkaCluster;
        private StreamsConfigBuilder _streamsConfig;
        private TStreamBuilder _builder;
        private TTopology _topology;
        private TStream _streams;

        readonly IUpdateAdapter _updateAdapter;

        private KEFCoreStreamsUncaughtExceptionHandler<StreamsManager<TStream, TStreamBuilder, TTopology, TStoreSupplier, TTimestampExtractor, TConsumed, TMaterialized, TGlobalKTable, TKTable>> _errorHandler;
        private KEFCoreStreamsStateListener<StreamsManager<TStream, TStreamBuilder, TTopology, TStoreSupplier, TTimestampExtractor, TConsumed, TMaterialized, TGlobalKTable, TKTable>> _stateListener;
        private System.Exception _resultException;
        private Org.Apache.Kafka.Streams.KafkaStreams.State _currentState = Org.Apache.Kafka.Streams.KafkaStreams.State.NOT_RUNNING;

        private readonly bool _useEnumeratorWithPrefetch;
        private readonly bool _usePersistentStorage;
        private readonly bool _useGlobalTable;
        private readonly bool _manageEvents;
        private readonly System.Threading.Thread _thread;

        static Java.Util.Properties PropertyUpdate(StreamsConfigBuilder builder)
        {
            Java.Util.Properties props = builder;
            if (props.ContainsKey(Org.Apache.Kafka.Clients.Consumer.ConsumerConfig.AUTO_OFFSET_RESET_CONFIG))
            {
                props.Remove(Org.Apache.Kafka.Clients.Consumer.ConsumerConfig.AUTO_OFFSET_RESET_CONFIG);
            }
            props.Put(Org.Apache.Kafka.Clients.Consumer.ConsumerConfig.AUTO_OFFSET_RESET_CONFIG, "earliest");

            return props;
        }

        static StreamsManager()
        {
            KNetStreams.OverrideProperties = PropertyUpdate;
            MASES.KNet.Streams.StreamsBuilder.OverrideProperties = PropertyUpdate;
        }

        public StreamsManager(IKafkaCluster kafkaCluster, IEntityType entityType)
        {
            _kafkaCluster = kafkaCluster;
            _updateAdapter = kafkaCluster.UpdateAdapterFactory.Create();
            _streamsConfig ??= kafkaCluster.Options.StreamsOptions(entityType);
            _usePersistentStorage = _kafkaCluster.Options.UsePersistentStorage;
            _useEnumeratorWithPrefetch = _kafkaCluster.Options.UseEnumeratorWithPrefetch;
            _useGlobalTable = _kafkaCluster.Options.UseGlobalTable;
            _manageEvents = _kafkaCluster.Options.ManageEvents;

            if (_manageEvents)
            {
                _thread = new System.Threading.Thread(EventChangePusher)
                {
                    IsBackground = true
                };
                _thread.Start();
            }

            _dataReceived = new(false);
            _resetEvent = new(false);
            _stateChanged = new(false);

            _errorHandler ??= new(this, (_This, exception) =>
            {
                Console.WriteLine($"StreamsUncaughtExceptionHandler reports Exception: {exception}"); // <- added to understand what is raised really
                Console.WriteLine($"StreamsUncaughtExceptionHandler reports InnerException: {exception?.InnerException}"); // <- added to understand what is raised really

                _kafkaCluster.InfrastructureLogger.Logger?.LogCritical("StreamsUncaughtExceptionHandler received a new Exception {Exception}", exception);
                if (exception is Org.Apache.Kafka.Streams.Errors.StreamsException streamsException 
                    && streamsException.Message.Contains("TimestampExtractor", StringComparison.InvariantCultureIgnoreCase))
                {
                    _kafkaCluster.InfrastructureLogger.Logger?.LogCritical("StreamsUncaughtExceptionHandler received an exception of type {Exception} try with {Action}", 
                                                                           nameof(Org.Apache.Kafka.Streams.Errors.StreamsException), nameof(StreamThreadExceptionResponse.REPLACE_THREAD));

                    Console.WriteLine($"StreamsUncaughtExceptionHandler request: {StreamThreadExceptionResponse.REPLACE_THREAD}"); // <- added to understand what is raised really
                    return StreamThreadExceptionResponse.REPLACE_THREAD;
                }
                Console.WriteLine($"StreamsUncaughtExceptionHandler reached end trying for debug: {StreamThreadExceptionResponse.REPLACE_THREAD}");
                return StreamThreadExceptionResponse.REPLACE_THREAD; // StreamThreadExceptionResponse.SHUTDOWN_APPLICATION; <- JUST FOR TEST
            });

            _stateListener ??= new(this, (_This, newState, oldState) =>
            {
                _currentState = newState ?? throw new InvalidOperationException("New state cannot be null.");
                _kafkaCluster.InfrastructureLogger.Logger?.LogInformation("StateListener reports a state change from {OldState} to {NewState}", oldState, newState);
#if DEBUG_PERFORMANCE
                KNet.Internal.DebugPerformanceHelper.ReportString($"StateListener oldState: {oldState} newState: {newState} on {DateTime.Now:HH:mm:ss.FFFFFFF}");
#endif
                if (_stateChanged != null && !_stateChanged.SafeWaitHandle.IsClosed) _stateChanged.Set();
            });
        }

        public bool UseEnumeratorWithPrefetch => _useEnumeratorWithPrefetch;
        public TStream Streams => _streams;

        public required Func<StreamsConfigBuilder, TStreamBuilder> CreateStreamBuilder { get; set; }
        public required Func<bool, string, TStoreSupplier> CreateStoreSupplier { get; set; }
        public required Func<Action<EventChange>, IStreamsChangeManager, IEntityType, IKey, object, TTimestampExtractor> CreateTimestampExtractor { get; set; }
        public required Func<TTimestampExtractor, TConsumed> CreateConsumed { get; set; }
        public required Func<TStoreSupplier, TMaterialized> CreateMaterialized { get; set; }
        public required Func<TStreamBuilder, string, TMaterialized, TGlobalKTable> CreateGlobalTable { get; set; }
        public required Func<TStreamBuilder, string, TConsumed, TMaterialized, TKTable> CreateTable { get; set; }
        public required Func<TStreamBuilder, TTopology> CreateTopology { get; set; }
        public required Func<TTopology, StreamsConfigBuilder, TStream> CreateStreams { get; set; }
        public required Action<TStream,
                               KEFCoreStreamsUncaughtExceptionHandler<StreamsManager<TStream, TStreamBuilder, TTopology, TStoreSupplier, TTimestampExtractor, TConsumed, TMaterialized, TGlobalKTable, TKTable>>,
                               KEFCoreStreamsStateListener<StreamsManager<TStream, TStreamBuilder, TTopology, TStoreSupplier, TTimestampExtractor, TConsumed, TMaterialized, TGlobalKTable, TKTable>>> SetHandlers { get; set; }
        public required Action<TStream> Start { get; set; }
        public required Action<TStream> Pause { get; set; }
        public required Action<TStream> Resume { get; set; }
        public required Action<TStream> Close { get; set; }
        public required Func<TStream, Org.Apache.Kafka.Streams.KafkaStreams.State> GetState { get; set; }
        public required Func<TStream, Map<Java.Lang.String, Map<Integer, LagInfo>>> GetLags { get; set; }
        public required Func<TStream, bool> GetIsPaused { get; set; }

        public string AddEntity(IStreamsChangeManager changeManager, IEntityType entityType, object optional)
        {
            _builder ??= CreateStreamBuilder(_streamsConfig!);

            var topicName = entityType.TopicName(_kafkaCluster.Options);

            string storageId = entityType.StorageIdForTable(_kafkaCluster.Options);
            storageId = _usePersistentStorage ? storageId : System.Diagnostics.Process.GetCurrentProcess().ProcessName + "-" + storageId;

            lock (_managedEntities)
            {
                if (!_managedEntities.ContainsKey(entityType))
                {
                    var storeSupplier = CreateStoreSupplier(_usePersistentStorage, storageId);
                    TTimestampExtractor timestampExtractor = null;
                    TConsumed consumed = null;
                    IEntityType entityType1 = _updateAdapter.Model.FindEntityType(entityType.ClrType);
                    if (_manageEvents)
                    {
                        IKey key1 = entityType1.FindPrimaryKey();
                        timestampExtractor = CreateTimestampExtractor(EventChangeReceiver, changeManager, entityType1, key1, optional);
                        consumed = CreateConsumed(timestampExtractor);
                    }
                    var materialized = CreateMaterialized(storeSupplier);
                    TGlobalKTable globalTable = null;
                    TKTable table = null;
                    if (_useGlobalTable)
                    {
                        globalTable = CreateGlobalTable(_builder!, topicName, materialized);
                    }
                    else
                    {
                        table = CreateTable(_builder!, topicName, consumed, materialized);
                    }
                    var currentKnownOffsets = _kafkaCluster.LatestOffsetForEntity(entityType);
                    _managedEntities.Add(entityType, entityType1);
                    _storagesForEntities.Add(entityType1, new StreamsAssociatedData(storeSupplier, timestampExtractor, consumed, materialized, globalTable, table, currentKnownOffsets));

                    if (CheckState())
                    {
                        CreateAndStartTopology();
                    }
                }
            }

            return storageId;
        }

        private void EventChangeReceiver(EventChange data)
        {
            _storagesForEntities[data.EntityType].LatestLocalKnownOffset[data.Partition] = data.Offset;
            _dataFromStream.Enqueue(data);
        }

        private void EventChangePusher()
        {
            while (true)
            {
                do
                {
                    if (!_dataFromStream.TryDequeue(out var current)) break;
                    current.Manager.ManageChange(_kafkaCluster.ValueGeneratorSelector, _updateAdapter, current.EntityType, current.PrimaryKey, current.Data);
                }
                while (!_dataFromStream.IsEmpty);
                System.Threading.Thread.Sleep(10);
            }
        }

        public void Dispose(IEntityType entityType)
        {
            lock (_managedEntities)
            {
                if (!KEFCore.PreserveStreamsAcrossContexts)
                {
                    if (_managedEntities.TryGetAndRemove(entityType, out StreamsAssociatedData data))
                    {
                        data.Dispose();
                    }
                    _managedEntities.Remove(entityType);
                    if (_managedEntities.Count == 0)
                    {
                        Close(_streams!);
                        _streams = null;
                        _storagesForEntities.Clear();
                    }
                }
            }
        }

        bool CheckState()
        {
            lock (this)
            {
                bool wasRunning = false;
                if (_streams != null)
                {
                    var state = GetState(_streams);
                    if (state != null)
                    {
                        if (state == Org.Apache.Kafka.Streams.KafkaStreams.State.NOT_RUNNING) { _started = false; }
                        else if (state == Org.Apache.Kafka.Streams.KafkaStreams.State.RUNNING)
                        {
                            Close(_streams!);
                            _streams = null;
                            wasRunning = true;
                            _started = false;
                        }
                        else if (state == Org.Apache.Kafka.Streams.KafkaStreams.State.PENDING_SHUTDOWN ||
                                 state == Org.Apache.Kafka.Streams.KafkaStreams.State.REBALANCING)
                        {
                            System.Threading.Thread.Sleep(1000);
                            CheckState();
                        }
                        else if (state == Org.Apache.Kafka.Streams.KafkaStreams.State.PENDING_ERROR ||
                                 state == Org.Apache.Kafka.Streams.KafkaStreams.State.ERROR)
                        {
                            Close(_streams!);
                            _streams = null;
                            _started = false;
                        }
                    }
                }
                return wasRunning;
            }
        }

        bool _started = false;
        public void CreateAndStartTopology()
        {
            lock (this)
            {
                if (!_started)
                {
                    _topology = CreateTopology(_builder!);
                    _streams = CreateStreams(_topology, _streamsConfig!);
                    SetHandlers(_streams, _errorHandler!, _stateListener!);
                    StartTopology(_streams);
                    _started = true;
                }
            }
        }

        private void StartTopology(TStream streams)
        {
#if DEBUG_PERFORMANCE
            Stopwatch watch = Stopwatch.StartNew();
#endif
            _dataReceived?.Reset();
            _resetEvent?.Reset();
            _stateChanged?.Reset();

            ThreadPool.QueueUserWorkItem((o) =>
            {
                int waitingTime = Timeout.Infinite;
                Stopwatch watcher = new();
                try
                {
                    _resetEvent?.Set();
                    var index = WaitHandle.WaitAny([_stateChanged!]);
                    if (index == 1) return;
                    while (true)
                    {
                        index = WaitHandle.WaitAny([_stateChanged!, _dataReceived!], waitingTime);
                        if (index == 2) return;
                        // the states are defined as 
                        // CREATED
                        // ERROR
                        // NOT_RUNNING
                        // PENDING_ERROR
                        // PENDING_SHUTDOWN
                        // REBALANCING
                        // RUNNING
                        //
                        // with following transitions
                        //
                        //                +--------------+
                        //        +<----- | Created (0)  |
                        //        |       +-----+--------+
                        //        |             |
                        //        |             v
                        //        |       +----+--+------+
                        //        |       | Re-          |
                        //        +<----- | Balancing (1)| -------->+
                        //        |       +-----+-+------+          |
                        //        |             | ^                 |
                        //        |             v |                 |
                        //        |       +--------------+          v
                        //        |       | Running (2)  | -------->+
                        //        |       +------+-------+          |
                        //        |              |                  |
                        //        |              v                  |
                        //        |       +------+-------+     +----+-------+
                        //        +-----> | Pending      |     | Pending    |
                        //                | Shutdown (3) |     | Error (5)  |
                        //                +------+-------+     +-----+------+
                        //                       |                   |
                        //                       v                   v
                        //                +------+-------+     +-----+--------+
                        //                | Not          |     | Error (6)    |
                        //                | Running (4)  |     +--------------+
                        //                +--------------+
                        //

                        if (_currentState == Org.Apache.Kafka.Streams.KafkaStreams.State.CREATED
                            || _currentState == Org.Apache.Kafka.Streams.KafkaStreams.State.REBALANCING)
                        {
                            if (index == WaitHandle.WaitTimeout)
                            {
#if DEBUG_PERFORMANCE
                                KNet.Internal.DebugPerformanceHelper.ReportString($"State: {_currentState} No handle set within {waitingTime} ms");
#endif
                                continue;
                            }
                        }
                        else if (_currentState == Org.Apache.Kafka.Streams.KafkaStreams.State.PENDING_ERROR
                                 || _currentState == Org.Apache.Kafka.Streams.KafkaStreams.State.PENDING_SHUTDOWN
                                 || _currentState == Org.Apache.Kafka.Streams.KafkaStreams.State.ERROR)
                        {
                            throw new InvalidOperationException($"Cannot continue since streams is in {_currentState} state");
                        }
                        else if (_currentState == Org.Apache.Kafka.Streams.KafkaStreams.State.NOT_RUNNING)
                        {
                            throw new InvalidOperationException($"It is impossible that after start the streams is in {_currentState} state");
                        }
                        else if (_currentState == Org.Apache.Kafka.Streams.KafkaStreams.State.RUNNING)
                        {
                            return;
                        }
                        else // exit external wait thread 
                        {
                            throw new InvalidOperationException($"Impossible condition since every possible state was checked except {_currentState} state");
                        }
                    }
                }
                catch (System.Exception e)
                {
                    _resultException = e;
                }
                finally
                {
                    _resetEvent?.Set();
                }
            });
            _resetEvent?.WaitOne();
            Start(streams);
#if DEBUG_PERFORMANCE
            KNet.Internal.DebugPerformanceHelper.ReportString($"StreamsManager started on {DateTime.Now:HH:mm:ss.FFFFFFF} after {watch.Elapsed}");
#endif
            _resetEvent?.WaitOne(); // wait running state
            ThrowException();
#if DEBUG_PERFORMANCE
            watch.Stop();
            KNet.Internal.DebugPerformanceHelper.ReportString($"StreamsManager in running state started after {watch.Elapsed}");
#endif
        }

        /// <inheritdoc/>
        public void PartitionOffsetWritten(IEntityType entity, int partition, long offset)
        {
            if (_managedEntities.TryGetValue(entity, out var innerEntity) &&
                _storagesForEntities.TryGetValue(innerEntity, out var storage))
            {
                storage.UpdateCurrentRemoteKnownOffset(partition, offset);
            }
            else throw new InvalidOperationException($"{entity} not found in managed entities.");
        }

        /// <inheritdoc/>
        public bool? EnsureSynchronized(IEntityType entity, long timeout)
        {
            if (!_manageEvents || _useGlobalTable) return null; // cannot deduct synchronization without events or using GlobalKTable
            Stopwatch watch = null;
            if (timeout != Timeout.Infinite)
            {
                watch = Stopwatch.StartNew();
            }
            if (_managedEntities.TryGetValue(entity, out var innerEntity) &&
                _storagesForEntities.TryGetValue(innerEntity, out var storage))
            {
                var currentKnownOffsets = _kafkaCluster.LatestOffsetForEntity(entity);
                storage.UpdateCurrentRemoteKnownOffset(currentKnownOffsets);
                return EnsureSynchronized(storage, timeout, watch) // received data are aligned
                       && _dataFromStream.IsEmpty; // and all data are processed
            }
            else throw new InvalidOperationException($"{entity} not found in managed entities.");
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool EnsureSynchronized(StreamsAssociatedData storage, long timeout, Stopwatch watcher = null)
        {
            do
            {
                if (storage.IsSynchronized()) return true;
                System.Threading.Thread.Sleep(1000);
            }
            while (timeout == Timeout.Infinite || watcher.ElapsedMilliseconds < timeout);
            return false;
        }

        public void ThrowException()
        {
            if (_resultException != null) throw _resultException;
        }

        private void FinalCleanup()
        {
            _dataReceived?.Dispose();
            _resetEvent?.Dispose();
            _stateChanged?.Dispose();

            _errorHandler?.Dispose();
            _stateListener?.Dispose();
            _errorHandler = null;
            _stateListener = null;
            _streamsConfig = null;
            _builder = null;
            _topology = null;
        }
    }
}
