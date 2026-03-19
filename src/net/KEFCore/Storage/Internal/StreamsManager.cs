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
using MASES.EntityFrameworkCore.KNet.Extensions;
using MASES.EntityFrameworkCore.KNet.Infrastructure.Internal;
using MASES.KNet.Streams;
using Org.Apache.Kafka.Streams;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using static Org.Apache.Kafka.Streams.Errors.StreamsUncaughtExceptionHandler;

namespace MASES.EntityFrameworkCore.KNet.Storage.Internal
{
    internal readonly struct FreshEventChange(IStreamsChangeManager manager, string topic, int partition, long offset, object data)
    {
        public readonly IStreamsChangeManager Manager = manager;
        public readonly string Topic = topic;
        public readonly int Partition = partition;
        public readonly long Offset = offset;
        public readonly object Data = data;
    }

    internal readonly struct StoredEventChange(object data)
    {
        public readonly object Data = data;
    }

    internal interface IStreamsChangeManager
    {
        IEntityTypeProducer Producer { get; }
        void ManageChange(IDiagnosticsLogger<DbLoggerCategory.Infrastructure> infrastructureLogger, IValueGeneratorSelector valueGeneratorSelector, IUpdateAdapter adapter, IEntityType entityType, IKey primaryKey, object data);
    }
    /// <summary>
    /// Central interface for stream management
    /// </summary>
    public interface IStreamsManager
    {
        /// <summary>
        /// Register an instance of <see cref="IKEFCoreDatabase"/>
        /// </summary>
        /// <param name="producer">The <see cref="IEntityTypeProducer"/> requesting the operation</param>
        /// <param name="database"><see cref="IKEFCoreDatabase"/></param>
        /// <param name="entity">Associated <see cref="IEntityType"/></param>
        void Register(IEntityTypeProducer producer, IKEFCoreDatabase database, IEntityType entity);
        /// <summary>
        /// Unregister an instance of <see cref="IKEFCoreDatabase"/>
        /// </summary>
        /// <param name="producer">The <see cref="IEntityTypeProducer"/> requesting the operation</param>
        /// <param name="database"><see cref="IKEFCoreDatabase"/></param>
        void Unregister(IEntityTypeProducer producer, IKEFCoreDatabase database);
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
        /// Verify if local instance is synchronized with the <see cref="IKEFCoreCluster"/> instance
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
        readonly struct StreamsAssociatedData(StreamsManager<TStream, TStreamBuilder, TTopology, TStoreSupplier, TTimestampExtractor, TConsumed, TMaterialized, TGlobalKTable, TKTable> streamsManager,
                                              string storageId, object optional, IStreamsChangeManager changeManager,
                                              TStoreSupplier storeSupplier, TTimestampExtractor extractor, TConsumed consumed,
                                              TMaterialized materialized, TGlobalKTable globalTable, TKTable table,
                                              IDictionary<int, long> creationKnownOffset) : IDisposable
        {
            public readonly string StorageId = storageId;
            public readonly object Optional = optional;
            public readonly IStreamsChangeManager ChangeManager = changeManager;

            readonly StreamsManager<TStream, TStreamBuilder, TTopology, TStoreSupplier, TTimestampExtractor, TConsumed, TMaterialized, TGlobalKTable, TKTable> StreamsManager = streamsManager;
            readonly TStoreSupplier StoreSupplier = storeSupplier;
            readonly TConsumed Consumed = consumed;
            readonly TTimestampExtractor TimestampExtractor = extractor;
            readonly TMaterialized Materialized = materialized;
            readonly TGlobalKTable GlobalTable = globalTable;
            readonly TKTable Table = table;
            readonly IDictionary<int, long> CurrentRemoteKnownOffset = creationKnownOffset; // expected to be invariant
            readonly IDictionary<int, long> LatestLocalKnownOffset = new ConcurrentDictionary<int, long>();

            public void UpdateCurrentRemoteKnownPartitionOffset(int partition, long offset)
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

            public void UpdateCurrentRemoteKnownPartitionOffset(IDictionary<int, long> keyValuePairs)
            {
                var lastKnownPartitions = keyValuePairs.Keys.ToList();

                lock (CurrentRemoteKnownOffset)
                {
                    foreach (var kv in keyValuePairs)
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

                    if (lastKnownPartitions.Count != 0) // if old stored partition are no more managed...
                    {
                        foreach (var item in lastKnownPartitions)
                        {
                            CurrentRemoteKnownOffset.Remove(item);  // ...then remove them
                        }
                    }
                }
            }

            public void UpdateLatestLocalKnownOffset(int partition, long offset)
            {
                LatestLocalKnownOffset[partition] = offset;
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

            public void PushLocalStoredData(IValueGeneratorSelector selector, TStream streams, Func<TStream, string, object, IEnumerable<StoredEventChange>> factory)
            {
                foreach (var storedEvent in factory(streams, StorageId, Optional))
                {
                    foreach (var updater in StreamsManager._updaters)
                    {
                        IKEFCoreDatabase database = updater.Key.Database;
                        KEFCoreDatabaseLocalData localData = updater.Value;
                        ChangeManager.ManageChange(database.InfrastructureLogger, selector, localData.UpdateAdapter, localData.EntityTypeForChanges, localData.PrimaryKeyForChanges, storedEvent.Data);
                    }
                }
            }

            public readonly void Dispose()
            {
                TimestampExtractor?.Dispose();
            }
        }
        readonly ConcurrentDictionary<(IEntityTypeProducer Producer, IKEFCoreDatabase Database), KEFCoreDatabaseLocalData> _updaters = new();
        // enqueues changes from cluster when are send
        private readonly ConcurrentQueue<FreshEventChange> _freshDataFromCluster = new();
        // enqueues changes from cluster when are send
        private readonly ConcurrentQueue<FreshEventChange> _locallyStoredData = new();
        // this dictionary controls the entities
        //   private readonly System.Collections.Generic.Dictionary<IEntityType, IEntityType> _managedEntities = new(EntityTypeFullNameComparer.Instance);
        // while this one is used to retain the allocated object to avoid their finalization before the streams is completly finalized
        private readonly ConcurrentDictionary<string, StreamsAssociatedData> _storagesForEntities = new();

        private readonly AutoResetEvent _dataReceived;
        private readonly AutoResetEvent _resetEvent;
        private readonly AutoResetEvent _stateChanged;

        private readonly IKEFCoreCluster _kefcoreCluster;
        private StreamsConfigBuilder _streamsConfig;
        private TStreamBuilder _builder;
        private TTopology _topology;
        private TStream _streams;

        private KEFCoreStreamsUncaughtExceptionHandler<StreamsManager<TStream, TStreamBuilder, TTopology, TStoreSupplier, TTimestampExtractor, TConsumed, TMaterialized, TGlobalKTable, TKTable>> _errorHandler;
        private KEFCoreStreamsStateListener<StreamsManager<TStream, TStreamBuilder, TTopology, TStoreSupplier, TTimestampExtractor, TConsumed, TMaterialized, TGlobalKTable, TKTable>> _stateListener;
        private System.Exception _resultException;
        private Org.Apache.Kafka.Streams.KafkaStreams.State _currentState = Org.Apache.Kafka.Streams.KafkaStreams.State.NOT_RUNNING;

        private readonly bool _usePersistentStorage;
        private readonly bool _useGlobalTable;
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

        public StreamsManager(IKEFCoreCluster kefcoreCluster, KEFCoreOptionsExtension options)
        {
            _kefcoreCluster = kefcoreCluster;
            _streamsConfig ??= options.StreamsOptions();
            _usePersistentStorage = options.UsePersistentStorage;
            _useGlobalTable = options.UseGlobalTable;

            _thread = new System.Threading.Thread(FreshEventChangePusher)
            {
                IsBackground = true
            };
            _thread.Start();

            _dataReceived = new(false);
            _resetEvent = new(false);
            _stateChanged = new(false);

            _errorHandler ??= new(this, (_This, exception) =>
            {
                _kefcoreCluster.InfrastructureLogger.Logger?.LogCritical("StreamsUncaughtExceptionHandler received a new Exception {Exception}", exception);
                if (exception is Org.Apache.Kafka.Streams.Errors.StreamsException streamsException
                    && streamsException.Message.Contains("TimestampExtractor", StringComparison.InvariantCultureIgnoreCase))
                {
                    _kefcoreCluster.InfrastructureLogger.Logger?.LogCritical("StreamsUncaughtExceptionHandler received an exception of type {Exception} try with {Action}",
                                                                           nameof(Org.Apache.Kafka.Streams.Errors.StreamsException), nameof(StreamThreadExceptionResponse.REPLACE_THREAD));
                    return StreamThreadExceptionResponse.REPLACE_THREAD;
                }
                return StreamThreadExceptionResponse.SHUTDOWN_APPLICATION;
            });

            _stateListener ??= new(this, (_This, newState, oldState) =>
            {
                _currentState = newState ?? throw new InvalidOperationException("New state cannot be null.");
                _kefcoreCluster.InfrastructureLogger.Logger?.LogInformation("StateListener reports a state change from {OldState} to {NewState}", oldState, newState);
                if (_stateChanged != null && !_stateChanged.SafeWaitHandle.IsClosed) _stateChanged.Set();
            });
        }

        public TStream Streams => _streams;

        public required Func<StreamsConfigBuilder, TStreamBuilder> CreateStreamBuilder { get; set; }
        public required Func<bool, string, TStoreSupplier> CreateStoreSupplier { get; set; }
        public required Func<Action<FreshEventChange>, IStreamsChangeManager, object, TTimestampExtractor> CreateTimestampExtractor { get; set; }
        public required Func<TTimestampExtractor, TConsumed> CreateConsumed { get; set; }
        public required Func<TStoreSupplier, TMaterialized> CreateMaterialized { get; set; }
        public required Func<TStreamBuilder, string, TMaterialized, TGlobalKTable> CreateGlobalTable { get; set; }
        public required Func<TStreamBuilder, string, TConsumed, TMaterialized, TKTable> CreateTable { get; set; }
        public required Func<TStreamBuilder, TTopology> CreateTopology { get; set; }
        public required Func<TTopology, StreamsConfigBuilder, TStream> CreateStreams { get; set; }
        public required Action<TStream,
                               KEFCoreStreamsUncaughtExceptionHandler<StreamsManager<TStream, TStreamBuilder, TTopology, TStoreSupplier, TTimestampExtractor, TConsumed, TMaterialized, TGlobalKTable, TKTable>>,
                               KEFCoreStreamsStateListener<StreamsManager<TStream, TStreamBuilder, TTopology, TStoreSupplier, TTimestampExtractor, TConsumed, TMaterialized, TGlobalKTable, TKTable>>> SetHandlers
        { get; set; }
        public required Func<TStream, string, object, IEnumerable<StoredEventChange>> GetStoredData { get; set; }
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

            var topicName = entityType.GetKEFCoreTopicName();
            var storage = _storagesForEntities.GetOrAdd(topicName, (tn) =>
            {
                string storageId = entityType.StorageIdForTable();
                storageId = _usePersistentStorage ? storageId : System.Diagnostics.Process.GetCurrentProcess().ProcessName + "-" + storageId;

                var storeSupplier = CreateStoreSupplier(_usePersistentStorage, storageId);
                TTimestampExtractor timestampExtractor = null;
                TConsumed consumed = null;
                timestampExtractor = CreateTimestampExtractor(FreshEventChangeReceiver, changeManager, optional);
                consumed = CreateConsumed(timestampExtractor);
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
                var currentKnownOffsets = _kefcoreCluster.LatestOffsetForEntity(entityType);
                var storage = new StreamsAssociatedData(this, storageId, optional, changeManager, storeSupplier, timestampExtractor, consumed, materialized, globalTable, table, currentKnownOffsets);

                if (CheckState())
                {
                    CreateAndStartTopology();
                }
                return storage;
            });

            return storage.StorageId;
        }

        private void FreshEventChangeReceiver(FreshEventChange data)
        {
            _storagesForEntities[data.Topic].UpdateLatestLocalKnownOffset(data.Partition, data.Offset);
            _freshDataFromCluster.Enqueue(data);
        }

        long canExecute = 1;

        private void FreshEventChangePusher()
        {
            while (true)
            {
                try
                {
                    do
                    {
                        if (Interlocked.Read(ref canExecute) == 0) { System.Threading.Thread.Sleep(10); continue; }
                        if (!_freshDataFromCluster.TryDequeue(out var current)) break;
                        foreach (var item in _updaters.Where(item => item.Value.ManageEvents && item.Key.Producer == current.Manager))
                        {
                            IKEFCoreDatabase database = item.Key.Database;
                            KEFCoreDatabaseLocalData localData = item.Value;
                            current.Manager.ManageChange(database.InfrastructureLogger, _kefcoreCluster.ValueGeneratorSelector, localData.UpdateAdapter, localData.EntityTypeForChanges, localData.PrimaryKeyForChanges, current.Data);
                        }
                    }
                    while (!_freshDataFromCluster.IsEmpty);
                    System.Threading.Thread.Sleep(10);
                }
                catch (System.Exception ex) { _kefcoreCluster.InfrastructureLogger.Logger.LogError(ex, "FreshEventChangePusher catched an exception: open an issue on GitHub."); } // final catch
            }
        }

        public void Dispose(IEntityType entityType)
        {
            if (_storagesForEntities.TryGetAndRemove(entityType.GetKEFCoreTopicName(), out StreamsAssociatedData data))
            {
                data.Dispose();
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

        public void Register(IEntityTypeProducer producer, IKEFCoreDatabase database, IEntityType entity)
        {
            if (!_updaters.TryAdd((producer, database), new KEFCoreDatabaseLocalData(database, entity)))
            {
                database.InfrastructureLogger.Logger.LogError($"StreamsManager: Failed to register database for {entity}");
            }
        }

        public void Unregister(IEntityTypeProducer producer, IKEFCoreDatabase database)
        {
            if (!_updaters.TryRemove((producer, database), out _))
            {
                database.InfrastructureLogger.Logger.LogError("StreamsManager: Failed to unregister database");
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
                    if (_usePersistentStorage)
                    {
                        try
                        {
                            Interlocked.Increment(ref canExecute);
                            foreach (var item in _storagesForEntities)
                            {
                                item.Value.PushLocalStoredData(_kefcoreCluster.ValueGeneratorSelector, _streams, GetStoredData);
                            }
                        }
                        finally
                        {
                            Interlocked.Decrement(ref canExecute);
                        }
                    }
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
                                _kefcoreCluster.InfrastructureLogger.Logger.LogInformation("State: {CurrentState} No handle set within {WaitingTime} ms", _currentState, waitingTime);
                                continue;
                            }
                        }
                        else if (_currentState == Org.Apache.Kafka.Streams.KafkaStreams.State.PENDING_ERROR
                                 || _currentState == Org.Apache.Kafka.Streams.KafkaStreams.State.PENDING_SHUTDOWN
                                 || _currentState == Org.Apache.Kafka.Streams.KafkaStreams.State.ERROR)
                        {
                            throw new InvalidOperationException($"Cannot continue since streams is in {_currentState} state.");
                        }
                        else if (_currentState == Org.Apache.Kafka.Streams.KafkaStreams.State.NOT_RUNNING)
                        {
                            throw new InvalidOperationException($"It is impossible that the streams is in {_currentState} state after the start was requested.");
                        }
                        else if (_currentState == Org.Apache.Kafka.Streams.KafkaStreams.State.RUNNING)
                        {
                            // exit gracefully external wait thread 
                            return;
                        }
                        else
                        {
                            throw new InvalidOperationException($"Impossible condition since every possible state was checked except {_currentState} state.");
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
            if (_storagesForEntities.TryGetValue(entity.GetKEFCoreTopicName(), out var storage))
            {
                storage.UpdateCurrentRemoteKnownPartitionOffset(partition, offset);
            }
            else throw new InvalidOperationException($"{entity} not found in managed entities.");
        }

        /// <inheritdoc/>
        public bool? EnsureSynchronized(IEntityType entity, long timeout)
        {
            if (!entity.GetManageEvents() || _useGlobalTable) return null; // cannot deduct synchronization without events or using GlobalKTable
            Stopwatch watch = null;
            if (timeout != Timeout.Infinite)
            {
                watch = Stopwatch.StartNew();
            }
            if (_storagesForEntities.TryGetValue(entity.GetKEFCoreTopicName(), out var storage))
            {
                var currentKnownOffsets = _kefcoreCluster.LatestOffsetForEntity(entity);
                storage.UpdateCurrentRemoteKnownPartitionOffset(currentKnownOffsets);
                return EnsureSynchronized(storage, timeout, watch) // received data are aligned
                       && _freshDataFromCluster.IsEmpty; // and all data are processed
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
