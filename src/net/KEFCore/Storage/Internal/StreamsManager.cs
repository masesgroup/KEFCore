/*
*  Copyright 2022 - 2025 MASES s.r.l.
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

using MASES.KNet.Serialization;
using MASES.KNet.Streams;
using MASES.KNet.Streams.Kstream;
using Org.Apache.Kafka.Streams.State;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Org.Apache.Kafka.Streams.Errors.StreamsUncaughtExceptionHandler;

namespace MASES.EntityFrameworkCore.KNet.Storage.Internal
{
    internal class StreamsManager<TStream, TStreamBuilder, TTopology, TStoreSupplier, TMaterialized, TGlobalKTable> 
        where TStream: class
        where TStreamBuilder : class
        where TTopology : class
    {
        struct StreamsAssociatedData(TStoreSupplier storeSupplier, TMaterialized materialized, TGlobalKTable globalTable)
        {
            public TStoreSupplier StoreSupplier = storeSupplier;
            public TMaterialized Materialized = materialized;
            public TGlobalKTable GlobalTable = globalTable;
        }

        // this dictionary controls the entities
        private readonly System.Collections.Generic.Dictionary<IEntityType, IEntityType> _managedEntities = new(EntityTypeFullNameComparer.Instance);
        // while this one is used to retain the allocated object to avoid thier finalization before the streams is completly finalized
        private readonly System.Collections.Generic.Dictionary<IEntityType, StreamsAssociatedData> _storagesForEntities = new(EntityTypeFullNameComparer.Instance);

        private readonly AutoResetEvent? _dataReceived;
        private readonly AutoResetEvent? _resetEvent;
        private readonly AutoResetEvent? _stateChanged;
        private readonly AutoResetEvent? _exceptionSet;

        private readonly IKafkaCluster _kafkaCluster;
        private StreamsConfigBuilder? _streamsConfig = null;
        private TStreamBuilder? _builder = null;
        private TTopology? _topology = null;
        private TStream? _streams = null;

        private KEFCoreStreamsUncaughtExceptionHandler? _errorHandler;
        private KEFCoreStreamsStateListener? _stateListener;
        private Exception? _resultException = null;
        private Org.Apache.Kafka.Streams.KafkaStreams.State _currentState = Org.Apache.Kafka.Streams.KafkaStreams.State.NOT_RUNNING;

        private readonly bool _useEnumeratorWithPrefetch;
        private readonly bool _usePersistentStorage;

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
            StreamsBuilder.OverrideProperties = PropertyUpdate;
        }

        public StreamsManager(IKafkaCluster kafkaCluster, IEntityType entityType)
        {
            _kafkaCluster = kafkaCluster;
            _streamsConfig ??= kafkaCluster.Options.StreamsOptions(entityType);
            _usePersistentStorage = _kafkaCluster.Options.UsePersistentStorage;
            _useEnumeratorWithPrefetch = _kafkaCluster.Options.UseEnumeratorWithPrefetch;

            _dataReceived = new(false);
            _resetEvent = new(false);
            _stateChanged = new(false);
            _exceptionSet = new(false);

            _errorHandler ??= new((exception) =>
            {
                _resultException = exception;
                _exceptionSet.Set();
                return StreamThreadExceptionResponse.SHUTDOWN_APPLICATION;
            });

            _stateListener ??= new((newState, oldState) =>
            {
                _currentState = newState;
                if (_currentState == null) { throw new InvalidOperationException("New state cannot be null."); }
#if DEBUG_PERFORMANCE
                Infrastructure.KafkaDbContext.ReportString($"StateListener oldState: {oldState} newState: {newState} on {DateTime.Now:HH:mm:ss.FFFFFFF}");
#endif
                if (_stateChanged != null && !_stateChanged.SafeWaitHandle.IsClosed) _stateChanged.Set();
                if (_streams == null && newState.Equals(Org.Apache.Kafka.Streams.KafkaStreams.State.NOT_RUNNING))
                {
                    //FinalCleanup();
                }
            });
        }

        public bool UseEnumeratorWithPrefetch => _useEnumeratorWithPrefetch;
        public TStream? Streams => _streams;

        public Func<StreamsConfigBuilder, TStreamBuilder>? CreateStreamBuilder;
        public Func<bool, string, TStoreSupplier>? CreateStoreSupplier;
        public Func<TStoreSupplier, TMaterialized>? CreateMaterialized;
        public Func<TStreamBuilder, string, TMaterialized, TGlobalKTable>? CreateGlobalKTable;
        public Func<TStreamBuilder, TTopology>? CreateTopology;
        public Func<TTopology, StreamsConfigBuilder, TStream>? CreateStreams;
        public Action<TStream, KEFCoreStreamsUncaughtExceptionHandler, KEFCoreStreamsStateListener>? SetHandlers;
        public Action<TStream>? Start;
        public Action<TStream>? Close;

        public string AddEntity(IEntityType entityType)
        {
            if (CreateStreamBuilder == null || 
                CreateStoreSupplier == null ||
                CreateMaterialized == null ||
                CreateGlobalKTable == null ||
                CreateTopology == null ||
                CreateStreams == null)
            {
                throw new InvalidOperationException("All handlers shall be set");
            }

            _builder ??= CreateStreamBuilder?.Invoke(_streamsConfig!);

             var topicName = entityType.TopicName(_kafkaCluster.Options);

            string storageId = entityType.StorageIdForTable(_kafkaCluster.Options);
            storageId = _usePersistentStorage ? storageId : Process.GetCurrentProcess().ProcessName + "-" + storageId;

            lock (_managedEntities)
            {
                if (!_managedEntities.ContainsKey(entityType))
                {
                    var storeSupplier = CreateStoreSupplier(_usePersistentStorage, storageId);
                    var materialized = CreateMaterialized(storeSupplier);
                    var globalTable = CreateGlobalKTable(_builder!, topicName, materialized);
                    _managedEntities.Add(entityType, entityType);
                    _storagesForEntities.Add(entityType, new StreamsAssociatedData(storeSupplier, materialized, globalTable));

                    if (_streams != null)
                    {
                        StopTopology();
                    }
                    _topology = CreateTopology(_builder!);
                    _streams = CreateStreams(_topology, _streamsConfig!);
                    StartTopology(_streams);
                }
            }

            return storageId;
        }

        public void Dispose(IEntityType entityType)
        {
            lock (_managedEntities)
            {
                if (!KEFCore.PreserveInformationAcrossContexts)
                {
                    _managedEntities.Remove(entityType);
                    if (_managedEntities.Count == 0)
                    {
                        StopTopology();
                        _storagesForEntities.Clear();
                    }
                }
            }
        }

        private void StartTopology(TStream streams)
        {
            if (SetHandlers == null || Start == null)
            {
                throw new InvalidOperationException("SetHandlers and Start handlers shall be set");
            }

#if DEBUG_PERFORMANCE
            Stopwatch watch = Stopwatch.StartNew(); 
#endif
            _dataReceived?.Reset();
            _resetEvent?.Reset();
            _stateChanged?.Reset();
            _exceptionSet?.Reset();

            SetHandlers(streams, _errorHandler!, _stateListener!);

            ThreadPool.QueueUserWorkItem((o) =>
            {
                int waitingTime = Timeout.Infinite;
                Stopwatch watcher = new();
                try
                {
                    _resetEvent?.Set();
                    var index = WaitHandle.WaitAny([_stateChanged!, _exceptionSet!]);
                    if (index == 1) return;
                    while (true)
                    {
                        index = WaitHandle.WaitAny([_stateChanged!, _dataReceived!, _exceptionSet!], waitingTime);
                        if (index == 2) return;
                        if (_currentState.Equals(Org.Apache.Kafka.Streams.KafkaStreams.State.CREATED) || _currentState.Equals(Org.Apache.Kafka.Streams.KafkaStreams.State.REBALANCING))
                        {
                            if (index == WaitHandle.WaitTimeout)
                            {
#if DEBUG_PERFORMANCE
                            Infrastructure.KafkaDbContext.ReportString($"State: {_currentState} No handle set within {waitingTime} ms");
#endif
                                continue;
                            }
                        }
                        else // exit external wait thread 
                        {
                            return;
                        }
                    }
                }
                catch (Exception e)
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
        Infrastructure.KafkaDbContext.ReportString($"StreamsManager started on {DateTime.Now:HH:mm:ss.FFFFFFF} after {watch.Elapsed}");
#endif
            _resetEvent?.WaitOne(); // wait running state
            ThrowException();
#if DEBUG_PERFORMANCE
        watch.Stop();
        Infrastructure.KafkaDbContext.ReportString($"StreamsManager in running state started after {watch.Elapsed}");
#endif
        }

        public void ThrowException()
        {
            if (_resultException != null) throw _resultException;
        }

        private void StopTopology()
        {
            if (Close == null)
            {
                throw new InvalidOperationException("Close handler shall be set");
            }

            Close(_streams!);
            _streams = null;
        }

        private void FinalCleanup()
        {
            _dataReceived?.Dispose();
            _resetEvent?.Dispose();
            _exceptionSet?.Dispose();
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
