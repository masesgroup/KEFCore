﻿/*
*  Copyright 2023 MASES s.r.l.
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

// #define DEBUG_PERFORMANCE

#nullable enable

using MASES.EntityFrameworkCore.KNet.Serialization;
using MASES.KNet.Streams;
using MASES.KNet.Streams.Kstream;
using MASES.KNet.Streams.State;
using static Org.Apache.Kafka.Streams.Errors.StreamsUncaughtExceptionHandler;

namespace MASES.EntityFrameworkCore.KNet.Storage.Internal;

/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public class KNetStreamsRetriever<TKey, TValue> : IKafkaStreamsRetriever
    where TKey : notnull
    where TValue : IValueContainer<TKey>
{
    struct StreamsAssociatedData(Org.Apache.Kafka.Streams.State.KeyValueBytesStoreSupplier storeSupplier, KNetMaterialized<TKey, TValue> materialized, KNetGlobalKTable<TKey, TValue> globalTable)
    {
        public Org.Apache.Kafka.Streams.State.KeyValueBytesStoreSupplier StoreSupplier = storeSupplier;
        public KNetMaterialized<TKey, TValue> Materialized = materialized;
        public KNetGlobalKTable<TKey, TValue> GlobalTable = globalTable;
    }

    private static bool _preserveStreamsAcrossContext = KEFCore.PreserveInformationAcrossContexts;
    // this dictionary controls the entities
    private static readonly System.Collections.Generic.Dictionary<IEntityType, IEntityType> _managedEntities = new(EntityTypeFullNameComparer.Instance);
    // while this one is used to retain the allocated object to avoid thier finalization before the streams is completly finalized
    private static readonly System.Collections.Generic.Dictionary<IEntityType, StreamsAssociatedData> _storagesForEntities = new(EntityTypeFullNameComparer.Instance);

    private static StreamsConfigBuilder? _streamsConfig = null;
    private static KNetStreamsBuilder? _builder = null;
    private static KNetTopology? _topology = null;
    private static KNetStreams? _streams = null;

    private static AutoResetEvent? _dataReceived;
    private static AutoResetEvent? _resetEvent;
    private static AutoResetEvent? _stateChanged;
    private static AutoResetEvent? _exceptionSet;
    private static Org.Apache.Kafka.Streams.Errors.StreamsUncaughtExceptionHandler? _errorHandler;
    private static Org.Apache.Kafka.Streams.KafkaStreams.StateListener? _stateListener;
    private static Exception? _resultException = null;
    private static Org.Apache.Kafka.Streams.KafkaStreams.State _currentState = Org.Apache.Kafka.Streams.KafkaStreams.State.NOT_RUNNING;

    private readonly IKafkaCluster _kafkaCluster;
    private readonly IEntityType _entityType;

    private readonly bool _useEnumeratorWithPrefetch;
    private readonly bool _usePersistentStorage;
    private readonly string _topicName;
    private readonly string _storageId;

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

    static KNetStreamsRetriever()
    {
        KNetStreams.OverrideProperties = PropertyUpdate;
        KNetStreamsBuilder.OverrideProperties = PropertyUpdate;
    }

    /// <summary>
    /// Default initializer
    /// </summary>
    public KNetStreamsRetriever(IKafkaCluster kafkaCluster, IEntityType entityType)
    {
        _kafkaCluster = kafkaCluster;
        _entityType = entityType;
        _streamsConfig ??= _kafkaCluster.Options.StreamsOptions();
        _builder ??= new KNetStreamsBuilder(_streamsConfig);
        _topicName = _entityType.TopicName(kafkaCluster.Options);
        _usePersistentStorage = _kafkaCluster.Options.UsePersistentStorage;
        _useEnumeratorWithPrefetch = _kafkaCluster.Options.UseEnumeratorWithPrefetch;

        string storageId = _entityType.StorageIdForTable(_kafkaCluster.Options);
        _storageId = _usePersistentStorage ? storageId : Process.GetCurrentProcess().ProcessName + "-" + storageId;

        lock (_managedEntities)
        {
            if (!_managedEntities.ContainsKey(_entityType))
            {
                var storeSupplier = _usePersistentStorage ? Org.Apache.Kafka.Streams.State.Stores.PersistentKeyValueStore(_storageId) 
                                                          : Org.Apache.Kafka.Streams.State.Stores.InMemoryKeyValueStore(_storageId);
                var materialized = KNetMaterialized<TKey, TValue>.As(storeSupplier);
                var globalTable = _builder.GlobalTable(_topicName, materialized);
                _managedEntities.Add(_entityType, _entityType);
                _storagesForEntities.Add(_entityType, new StreamsAssociatedData(storeSupplier, materialized, globalTable));

                if (_streams != null)
                {
                    StopTopology();
                }
                _topology = _builder.Build();
                _streams = new(_topology, _streamsConfig);
                StartTopology(_streams);
            }
        }
    }

    private static void StartTopology(KNetStreams streams)
    {
#if DEBUG_PERFORMANCE
        Stopwatch watch = Stopwatch.StartNew(); 
#endif
        _dataReceived = new(false);
        _resetEvent = new(false);
        _stateChanged = new(false);
        _exceptionSet = new(false);

        _errorHandler ??= new()
        {
            OnHandle = (exception) =>
            {
                _resultException = exception;
                _exceptionSet.Set();
                return StreamThreadExceptionResponse.SHUTDOWN_APPLICATION;
            }
        };

        _stateListener ??= new()
        {
            OnOnChange = (newState, oldState) =>
            {
                _currentState = newState;
                if (_currentState == null) { throw new InvalidOperationException("New state cannot be null."); }
#if DEBUG_PERFORMANCE
                Infrastructure.KafkaDbContext.ReportString($"StateListener oldState: {oldState} newState: {newState} on {DateTime.Now:HH:mm:ss.FFFFFFF}");
#endif
                if (_stateChanged != null && !_stateChanged.SafeWaitHandle.IsClosed) _stateChanged.Set();
            }
        };

        streams.SetUncaughtExceptionHandler(_errorHandler);
        streams.SetStateListener(_stateListener);

        ThreadPool.QueueUserWorkItem((o) =>
        {
            int waitingTime = Timeout.Infinite;
            Stopwatch watcher = new();
            try
            {
                _resetEvent.Set();
                var index = WaitHandle.WaitAny([_stateChanged, _exceptionSet]);
                if (index == 1) return;
                while (true)
                {
                    index = WaitHandle.WaitAny([_stateChanged, _dataReceived, _exceptionSet], waitingTime);
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
                _resetEvent.Set();
            }
        });
        _resetEvent.WaitOne();
        streams.Start();
#if DEBUG_PERFORMANCE
        Infrastructure.KafkaDbContext.ReportString($"KNetStreamsRetriever started on {DateTime.Now:HH:mm:ss.FFFFFFF} after {watch.Elapsed}");
#endif
        _resetEvent.WaitOne(); // wait running state
        if (_resultException != null) throw _resultException;
#if DEBUG_PERFORMANCE
        watch.Stop();
        Infrastructure.KafkaDbContext.ReportString($"KNetStreamsRetriever in running state started after {watch.Elapsed}");
#endif
    }

    private static void StopTopology()
    {
        _streams?.Close();

        _dataReceived?.Dispose();
        _resetEvent?.Dispose();
        _exceptionSet?.Dispose();
        _stateChanged?.Dispose();

        _streams = null;
    }

    private static void FinalCleanup()
    {
        _errorHandler?.Dispose();
        _stateListener?.Dispose();
        _errorHandler = null;
        _stateListener = null;
        _streamsConfig = null;
        _builder = null;
        _topology = null;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_managedEntities)
        {
            if (!_preserveStreamsAcrossContext)
            {
                _managedEntities.Remove(_entityType);
                if (_managedEntities.Count == 0)
                {
                    StopTopology();
                    _storagesForEntities.Clear();
                    FinalCleanup();
                }
            }
        }
    }

    /// <inheritdoc/>
    public IEnumerable<ValueBuffer> GetValueBuffers()
    {
        return new KafkaEnumberable(_kafkaCluster, _entityType, _storageId, _useEnumeratorWithPrefetch);
    }

    class KafkaEnumberable : IEnumerable<ValueBuffer>
    {
        private readonly bool _useEnumeratorWithPrefetch;
        private readonly IKafkaCluster _kafkaCluster;
        private readonly IEntityType _entityType;
        private readonly KNetReadOnlyKeyValueStore<TKey, TValue>? _keyValueStore = null;

        public KafkaEnumberable(IKafkaCluster kafkaCluster, IEntityType entityType, string storageId, bool useEnumerator)
        {
            _kafkaCluster = kafkaCluster;
            _entityType = entityType;
            _keyValueStore = _streams?.Store(storageId, KNetQueryableStoreTypes.KeyValueStore<TKey, TValue>());
            _useEnumeratorWithPrefetch = useEnumerator;
#if DEBUG_PERFORMANCE
            Infrastructure.KafkaDbContext.ReportString($"KafkaEnumerator for {_entityType.Name} - ApproximateNumEntries {_keyValueStore?.ApproximateNumEntries}");
#endif
        }

        /// <inheritdoc/>
        public IEnumerator<ValueBuffer> GetEnumerator()
        {
            if (_resultException != null) throw _resultException;
#if DEBUG_PERFORMANCE
            Infrastructure.KafkaDbContext.ReportString($"Requesting KafkaEnumerator for {_entityType.Name} on {DateTime.Now:HH:mm:ss.FFFFFFF}");
#endif
            return new KafkaEnumerator(_kafkaCluster, _entityType, _keyValueStore?.All, _useEnumeratorWithPrefetch);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    class KafkaEnumerator : IEnumerator<ValueBuffer>
    {
        private readonly bool _useEnumeratorWithPrefetch;
        private readonly IKafkaCluster _kafkaCluster;
        private readonly IEntityType _entityType;
        private readonly KNetKeyValueIterator<TKey, TValue>? _keyValueIterator = null;
        private readonly IEnumerator<KNetKeyValue<TKey, TValue>>? _enumerator = null;

#if DEBUG_PERFORMANCE
        Stopwatch _moveNextSw = new Stopwatch();
        Stopwatch _currentSw = new Stopwatch();
        Stopwatch _valueGetSw = new Stopwatch();
        Stopwatch _valueGet2Sw = new Stopwatch();
        Stopwatch _valueBufferSw = new Stopwatch();
#endif

        public KafkaEnumerator(IKafkaCluster kafkaCluster, IEntityType entityType, KNetKeyValueIterator<TKey, TValue>? keyValueIterator, bool useEnumerator)
        {
            _kafkaCluster = kafkaCluster ?? throw new ArgumentNullException(nameof(kafkaCluster));
            _entityType = entityType;
            _keyValueIterator = keyValueIterator ?? throw new ArgumentNullException(nameof(keyValueIterator));
            _useEnumeratorWithPrefetch = useEnumerator;
            if (_useEnumeratorWithPrefetch) _enumerator = _keyValueIterator.ToIEnumerator();
        }

        ValueBuffer? _current = null;

        public ValueBuffer Current
        {
            get
            {
#if DEBUG_PERFORMANCE
                    try
                    {
                        _currentSw.Start();
#endif
                return _current ?? default;
#if DEBUG_PERFORMANCE
                    }
                    finally
                    {
                        _currentSw.Stop();
                    }
#endif
            }
        }

        object System.Collections.IEnumerator.Current => Current;

        public void Dispose()
        {
#if DEBUG_PERFORMANCE
            Infrastructure.KafkaDbContext.ReportString($"KafkaEnumerator _moveNextSw: {_moveNextSw.Elapsed} _currentSw: {_currentSw.Elapsed} _valueGetSw: {_valueGetSw.Elapsed} _valueGet2Sw: {_valueGet2Sw.Elapsed} _valueBufferSw: {_valueBufferSw.Elapsed}");
#endif
            _enumerator?.Dispose();
        }

#if DEBUG_PERFORMANCE
        int _cycles = 0;
#endif

        public bool MoveNext()
        {
#if DEBUG_PERFORMANCE
                try
                {
                    _moveNextSw.Start();
#endif
            if (_useEnumeratorWithPrefetch ? _enumerator != null && _enumerator.MoveNext() : _keyValueIterator != null && _keyValueIterator.HasNext)
            {
#if DEBUG_PERFORMANCE
                _cycles++;
                _valueGetSw.Start();
#endif
                KNetKeyValue<TKey, TValue> kv = _useEnumeratorWithPrefetch ? _enumerator.Current : _keyValueIterator.Next;
#if DEBUG_PERFORMANCE
                _valueGetSw.Stop();
                _valueGet2Sw.Start();
#endif
                TValue value = kv.Value;
#if DEBUG_PERFORMANCE
                _valueGet2Sw.Stop();
                _valueBufferSw.Start();
#endif
                object[] array = null!;
                value?.GetData(_entityType, ref array);
#if DEBUG_PERFORMANCE
                _valueBufferSw.Stop();
#endif
                _current = new ValueBuffer(array);

                return true;
            }
            _current = null;
            return false;
#if DEBUG_PERFORMANCE
                }
                finally
                {
                    _moveNextSw.Stop();
                    if (_cycles == 0)
                    {
                        throw new InvalidOperationException($"KafkaEnumerator - No data returned from {_keyValueIterator}");
                    }
                }
#endif
        }

        public void Reset()
        {
            throw new InvalidOperationException($"Cannot apply {nameof(IEnumerator<ValueBuffer>.Reset)} operation over {typeof(KNetKeyValueIterator<TKey, TValue>)}");
        }
    }
}
