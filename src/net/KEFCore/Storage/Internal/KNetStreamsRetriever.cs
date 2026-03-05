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
//#define VERIFY_WHERE_ENUMERATOR_STOPS

#nullable enable

using MASES.EntityFrameworkCore.KNet.Serialization;
using MASES.KNet.Streams;
using MASES.KNet.Streams.Kstream;
using MASES.KNet.Streams.Processor;
using MASES.KNet.Streams.State;

namespace MASES.EntityFrameworkCore.KNet.Storage.Internal;

/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public class KNetStreamsRetriever<TKey, TValue, TJVMKey, TJVMValue> : IKafkaStreamsRetriever<TKey>, IStreamsChangeManager
    where TKey : notnull
    where TValue : IValueContainer<TKey>
{
    class KNetStreamsRetrieverTimestampExtractor(Action<FreshEventChange> pushChanges, IStreamsChangeManager manager, IEntityType entityType, IKey primaryKey)
        : TimestampExtractor<TKey, TValue, TJVMKey, TJVMValue>
    {
        private readonly Action<FreshEventChange> _pushChanges = pushChanges;
        private readonly IStreamsChangeManager _manager = manager;
        private readonly IEntityType _entityType = entityType;
        private readonly IKey _primaryKey = primaryKey;

        public override DateTime Extract()
        {
            _pushChanges.Invoke(new FreshEventChange(_manager, _entityType, _primaryKey, Record.Partition, Record.Offset, new Tuple<TKey, TValue>(Record.Key, Record.Value)));
            return Record.DateTime;
        }
    }

    static StreamsManager<KNetStreams,
                          StreamsBuilder,
                          Topology,
                          Org.Apache.Kafka.Streams.State.KeyValueBytesStoreSupplier,
                          TimestampExtractor<TKey, TValue, TJVMKey, TJVMValue>,
                          Consumed<TKey, TValue, TJVMKey, TJVMValue>,
                          Materialized<TKey, TValue, TJVMKey, TJVMValue>,
                          GlobalKTable<TKey, TValue, TJVMKey, TJVMValue>,
                          KTable<TKey, TValue, TJVMKey, TJVMValue>>? _streamsManager;

    /// <summary>
    /// Creates an instance of <see cref="IStreamsManager"/>
    /// </summary>
    /// <param name="cluster"></param>
    /// <param name="entity"></param>
    /// <returns></returns>
    public static IStreamsManager Create(IKafkaCluster cluster, IEntityType entity)
    {
        _streamsManager ??= new(cluster, entity)
        {
            CreateStreamBuilder = static (streamsConfig) => new StreamsBuilder(streamsConfig),
            CreateStoreSupplier = static (usePersistentStorage, storageId) => usePersistentStorage ? Org.Apache.Kafka.Streams.State.Stores.PersistentKeyValueStore(storageId)
                                                                                                   : Org.Apache.Kafka.Streams.State.Stores.InMemoryKeyValueStore(storageId),
            CreateTimestampExtractor = static (enqueuer, manager, entity, key, _) => new KNetStreamsRetrieverTimestampExtractor(enqueuer, manager, entity, key),
            CreateConsumed = static (extractor) => Consumed<TKey, TValue, TJVMKey, TJVMValue>.With(extractor),
            CreateMaterialized = static (supplier) => Materialized<TKey, TValue, TJVMKey, TJVMValue>.As(supplier),
            CreateGlobalTable = static (builder, topicName, materialized) => builder.GlobalTable(topicName, materialized),
            CreateTable = static (builder, topicName, consumed, materialized) => consumed != null ? builder.Table(topicName, consumed, materialized)
                                                                                                  : builder.Table(topicName, materialized),
            CreateTopology = static (builder) => builder.Build(),
            CreateStreams = static (topology, streamsConfig) => new(topology, streamsConfig),
            SetHandlers = static (streams, errorHandler, stateListener) =>
            {
                streams.SetUncaughtExceptionHandler(errorHandler);
                streams.SetStateListener(stateListener);
            },
            GetStoredData = static (streams, storageId, _) => GetStoredData(streams, storageId),
            Start = static (streams) => streams.Start(),
            Pause = static (streams) => streams.Pause(),
            Resume = static (streams) => streams.Resume(),
            Close = static (streams) => streams.Close(),
            GetState = static (streams) => streams.State,
            GetLags = static (streams) => streams.AllLocalStorePartitionLags,
            GetIsPaused = static (streams) => streams.IsPaused,
        };

        return _streamsManager;
    }

    private readonly IKafkaCluster _cluster;
    private readonly IEntityType _entityType;
    private readonly IKey _primaryKey;
    private readonly IProperty[] _properties;
    private readonly IComplexProperty[]? _complexProperties;
    private readonly IComplexTypeConverterFactory _complexTypeConverterFactory;
    private readonly string _storageId;

    /// <summary>
    /// Default initializer
    /// </summary>
    public KNetStreamsRetriever(IKafkaCluster cluster, IEntityType entityType, IKey primaryKey, IProperty[] properties, IComplexProperty[]? complexProperties, IComplexTypeConverterFactory complexTypeConverterFactory)
    {
        _cluster = cluster;
        _entityType = entityType;
        _primaryKey = primaryKey;
        _properties = properties;
        _complexProperties = complexProperties;
        _complexTypeConverterFactory = complexTypeConverterFactory;
        _storageId = _streamsManager!.AddEntity(this, entityType, null);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _streamsManager!.Dispose(_entityType);
    }

    /// <inheritdoc/>
    public IEnumerable<ValueBuffer> GetValueBuffers()
    {
        return new KafkaEnumberable(_entityType, _properties, _complexProperties, _complexTypeConverterFactory, _storageId, _streamsManager!.UseEnumeratorWithPrefetch);
    }

    TValue GetTValue(TKey key)
    {
        ReadOnlyKeyValueStore<TKey, TValue, TJVMKey, TJVMValue>? keyValueStore = _streamsManager!.Streams?.Store(_storageId, QueryableStoreTypes.KeyValueStore<TKey, TValue, TJVMKey, TJVMValue>());
        if (keyValueStore == null) return default!;
        var v = keyValueStore.Get(key);
        return v;
    }

    /// <inheritdoc/>
    public bool Exist(TKey key)
    {
        var v = GetTValue(key);
        return v != null;
    }
    /// <inheritdoc/>
    public bool TryGetValue(TKey key, out ValueBuffer valueBuffer)
    {
        var v = GetTValue(key);
        if (v == null)
        {
            valueBuffer = ValueBuffer.Empty;
            return false;
        }

        object[] propertyValues = null!;
        v?.GetData(_entityType, _properties, _complexProperties, ref propertyValues, _complexTypeConverterFactory);
        valueBuffer = new ValueBuffer(propertyValues);
        return true;
    }

    /// <inheritdoc/>
    public bool TryGetProperties(TKey key, out IDictionary<string, object?> properties)
    {
        var v = GetTValue(key);
        if (v == null)
        {
            properties = default!;
            return false;
        }

        properties = v?.GetProperties(_complexTypeConverterFactory)!;
        return true;
    }

    void IStreamsChangeManager.ManageChange(IValueGeneratorSelector valueGeneratorSelector, IUpdateAdapter adapter, IEntityType entityType, IKey primaryKey, object data)
    {
        var input = (Tuple<TKey, TValue>)data;
        KafkaStateHelper.ManageAdded(_cluster.InfrastructureLogger, valueGeneratorSelector, _complexTypeConverterFactory, adapter, entityType, primaryKey, input.Item1, input.Item2);
    }

    static IEnumerable<StoredEventChange> GetStoredData(KNetStreams streams, string storageId)
    {
        ReadOnlyKeyValueStore<TKey, TValue, TJVMKey, TJVMValue>? keyValueStore = streams?.Store(storageId, QueryableStoreTypes.KeyValueStore<TKey, TValue, TJVMKey, TJVMValue>());

        foreach (var item in keyValueStore?.All())
        {
            yield return new StoredEventChange(new Tuple<TKey, TValue>(item.Key, item.Value));
        }
    }

    class KafkaEnumberable : IEnumerable<ValueBuffer>, IAsyncEnumerable<ValueBuffer>
    {
        private readonly bool _useEnumeratorWithPrefetch;
        private readonly IEntityType _entityType;
        private readonly IProperty[] _properties;
        private readonly IComplexProperty[]? _complexProperties;
        private readonly IComplexTypeConverterFactory _complexTypeConverterFactory;
        private readonly ReadOnlyKeyValueStore<TKey, TValue, TJVMKey, TJVMValue>? _keyValueStore = null;

        public KafkaEnumberable(IEntityType entityType, IProperty[] properties, IComplexProperty[]? complexProperties, IComplexTypeConverterFactory complexTypeConverterFactory, string storageId, bool useEnumeratorWithPrefetch)
        {
            _entityType = entityType;
            _properties = properties;
            _complexProperties = complexProperties;
            _complexTypeConverterFactory = complexTypeConverterFactory;
            _keyValueStore = _streamsManager!.Streams?.Store(storageId, QueryableStoreTypes.KeyValueStore<TKey, TValue, TJVMKey, TJVMValue>());
            _useEnumeratorWithPrefetch = useEnumeratorWithPrefetch;
#if DEBUG_PERFORMANCE
            KNet.Internal.DebugPerformanceHelper.ReportString($"KafkaEnumerator for {_entityType.Name} - ApproximateNumEntries {_keyValueStore?.ApproximateNumEntries}");
#endif
        }

        static KeyValueIterator<TKey, TValue, TJVMKey, TJVMValue>? GetIterator(ReadOnlyKeyValueStore<TKey, TValue, TJVMKey, TJVMValue>? keyValueStore, CancellationToken cancellationToken = default)
        {
            const int maxCycle = 100;
            const int waitTime = 100;
            int cycle = 0;
            try
            {
                return GetIterator(keyValueStore, waitTime, maxCycle, ref cycle, cancellationToken);
            }
            catch (Org.Apache.Kafka.Streams.Errors.InvalidStateStoreException isse)
            {
                throw new InvalidOperationException($"Failed to retrieve {nameof(KeyValueIterator<,,,>)} after {cycle * waitTime} ms", isse);
            }
        }

        static KeyValueIterator<TKey, TValue, TJVMKey, TJVMValue>? GetIterator(ReadOnlyKeyValueStore<TKey, TValue, TJVMKey, TJVMValue>? keyValueStore, int waitTime, int maxCycle, ref int currentCycle, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return keyValueStore?.All();
            }
            catch (Org.Apache.Kafka.Streams.Errors.InvalidStateStoreException isse)
            {
                if (isse.Message.Contains(", not RUNNING") && currentCycle < maxCycle)
                {
                    Thread.Sleep(waitTime);
                    currentCycle++;
                    return GetIterator(keyValueStore, waitTime, maxCycle, ref currentCycle, cancellationToken);
                }
                throw;
            }
        }

        /// <inheritdoc/>
        public IEnumerator<ValueBuffer> GetEnumerator()
        {
            _streamsManager!.ThrowException();
#if DEBUG_PERFORMANCE
            KNet.Internal.DebugPerformanceHelper.ReportString($"Requesting KafkaEnumerator for {_entityType.Name} on {DateTime.Now:HH:mm:ss.FFFFFFF}");
#endif
            return new KafkaEnumerator(_entityType, _properties, _complexProperties, _complexTypeConverterFactory, GetIterator(_keyValueStore), _useEnumeratorWithPrefetch, false);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IAsyncEnumerator<ValueBuffer> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            _streamsManager!.ThrowException();
#if DEBUG_PERFORMANCE
            KNet.Internal.DebugPerformanceHelper.ReportString($"Requesting async KafkaEnumerator for {_entityType.Name} on {DateTime.Now:HH:mm:ss.FFFFFFF}");
#endif
            return new KafkaEnumerator(_entityType, _properties, _complexProperties, _complexTypeConverterFactory, GetIterator(_keyValueStore, cancellationToken), _useEnumeratorWithPrefetch, true, cancellationToken);
        }
    }

    class KafkaEnumerator : IEnumerator<ValueBuffer>
        , IAsyncEnumerator<ValueBuffer>
    {
        private readonly bool _useEnumeratorWithPrefetch;
        private readonly IEntityType _entityType;
        private readonly IProperty[] _properties;
        private readonly IComplexProperty[]? _complexProperties;
        private readonly IComplexTypeConverterFactory _complexTypeConverterFactory;
        private readonly KeyValueIterator<TKey, TValue, TJVMKey, TJVMValue>? _keyValueIterator = null;
        private readonly IEnumerator<KeyValue<TKey, TValue, TJVMKey, TJVMValue>>? _enumerator = null;
        private readonly IAsyncEnumerator<KeyValue<TKey, TValue, TJVMKey, TJVMValue>>? _asyncEnumerator = null;
        private readonly CancellationToken _cancellationToken = default;

#if DEBUG_PERFORMANCE
        Stopwatch _moveNextSw = new Stopwatch();
        Stopwatch _currentSw = new Stopwatch();
        Stopwatch _valueGetSw = new Stopwatch();
        Stopwatch _valueGet2Sw = new Stopwatch();
        Stopwatch _valueBufferSw = new Stopwatch();
#endif
#if DEBUG_PERFORMANCE || VERIFY_WHERE_ENUMERATOR_STOPS
        int _cycles = 0;
#endif

        public KafkaEnumerator(IEntityType entityType, IProperty[] properties, IComplexProperty[]? complexProperties, IComplexTypeConverterFactory complexTypeConverterFactory, KeyValueIterator<TKey, TValue, TJVMKey, TJVMValue>? keyValueIterator, bool useEnumeratorWithPrefetch, bool isAsync, CancellationToken cancellationToken = default)
        {
            _entityType = entityType;
            _properties = properties;
            _complexProperties = complexProperties;
            _complexTypeConverterFactory = complexTypeConverterFactory;
            _keyValueIterator = keyValueIterator ?? throw new ArgumentNullException(nameof(keyValueIterator));
            _useEnumeratorWithPrefetch = useEnumeratorWithPrefetch;
            if (_useEnumeratorWithPrefetch && !isAsync) _enumerator = _keyValueIterator.ToIEnumerator();
            if (isAsync) _asyncEnumerator = _keyValueIterator.GetAsyncEnumerator();
            _cancellationToken = cancellationToken;
        }

        ValueBuffer _current = ValueBuffer.Empty;

        public ValueBuffer Current
        {
            get
            {
#if DEBUG_PERFORMANCE
                try
                {
                    _currentSw.Start();
#endif
                    return _current;
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
            KNet.Internal.DebugPerformanceHelper.ReportString($"KafkaEnumerator _moveNextSw: {_moveNextSw.Elapsed} _currentSw: {_currentSw.Elapsed} _valueGetSw: {_valueGetSw.Elapsed} _valueGet2Sw: {_valueGet2Sw.Elapsed} _valueBufferSw: {_valueBufferSw.Elapsed}");
#endif
            _enumerator?.Dispose();
        }

        public ValueTask DisposeAsync()
        {
#if DEBUG_PERFORMANCE
            KNet.Internal.DebugPerformanceHelper.ReportString($"KafkaEnumerator _moveNextSw: {_moveNextSw.Elapsed} _currentSw: {_currentSw.Elapsed} _valueGetSw: {_valueGetSw.Elapsed} _valueGet2Sw: {_valueGet2Sw.Elapsed} _valueBufferSw: {_valueBufferSw.Elapsed}");
#endif
            return _asyncEnumerator != null ? _asyncEnumerator.DisposeAsync() : new ValueTask();
        }

        public bool MoveNext()
        {
#if DEBUG_PERFORMANCE || VERIFY_WHERE_ENUMERATOR_STOPS
            try
#endif
            {
#if DEBUG_PERFORMANCE
                _moveNextSw.Start();
#endif
                bool hasNext = false;
                TValue? value = default;
                do
                {
                    if (_useEnumeratorWithPrefetch ? _enumerator != null && _enumerator.MoveNext() : _keyValueIterator != null && _keyValueIterator.HasNext())
                    {
#if DEBUG_PERFORMANCE || VERIFY_WHERE_ENUMERATOR_STOPS
                        _cycles++;
#if DEBUG_PERFORMANCE
                        _valueGetSw.Start();
#endif
#endif
                        KeyValue<TKey, TValue, TJVMKey, TJVMValue>? kv = _useEnumeratorWithPrefetch ? _enumerator?.Current : _keyValueIterator?.Next();
#if DEBUG_PERFORMANCE
                        _valueGetSw.Stop();
                        _valueGet2Sw.Start();
#endif
                        value = kv != null ? kv.Value : default;
#if DEBUG_PERFORMANCE
                        _valueGet2Sw.Stop();
#endif
                        if (value == null) continue;
                        hasNext = true;
                    }
                    break;
                }
                while (true);

                if (hasNext)
                {
#if DEBUG_PERFORMANCE
                    _valueBufferSw.Start();
#endif
                    object[] propertyValues = null!;
                    value?.GetData(_entityType, _properties, _complexProperties, ref propertyValues, _complexTypeConverterFactory);
#if DEBUG_PERFORMANCE
                    _valueBufferSw.Stop();
#endif
                    _current = new ValueBuffer(propertyValues);
                }
                else
                {
                    _current = ValueBuffer.Empty;
                }
                return hasNext;
            }
#if VERIFY_WHERE_ENUMERATOR_STOPS
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Enumerator stops at cycle {_cycles} with InnerException", ex);
            }
#endif
#if DEBUG_PERFORMANCE
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

        public async ValueTask<bool> MoveNextAsync()
        {
            _cancellationToken.ThrowIfCancellationRequested();
#if DEBUG_PERFORMANCE
            try
            {
                _moveNextSw.Start();
#endif
                bool hasNext = false;
                TValue? value = default;

                do
                {
                    hasNext = await (_asyncEnumerator == null ? new ValueTask<bool>(false) : _asyncEnumerator.MoveNextAsync());
                    if (hasNext)
                    {
#if DEBUG_PERFORMANCE || VERIFY_WHERE_ENUMERATOR_STOPS
                        _cycles++;
#if DEBUG_PERFORMANCE
                        _valueGetSw.Start();
#endif
#endif
                        KeyValue<TKey, TValue, TJVMKey, TJVMValue>? kv = _asyncEnumerator?.Current;
#if DEBUG_PERFORMANCE
                        _valueGetSw.Stop();
                        _valueGet2Sw.Start();
#endif
                        value = kv != null ? kv.Value : default;
#if DEBUG_PERFORMANCE
                        _valueGet2Sw.Stop();
#endif
                        if (value == null) continue;
                        hasNext = true;
                    }
                    break;
                }
                while (true);

                if (hasNext)
                {
#if DEBUG_PERFORMANCE
                    _valueBufferSw.Start();
#endif
                    object[] propertyValues = null!;
                    value?.GetData(_entityType, _properties, _complexProperties, ref propertyValues, _complexTypeConverterFactory);
#if DEBUG_PERFORMANCE
                    _valueBufferSw.Stop();
#endif
                    _current = new ValueBuffer(propertyValues);
                }
                else
                {
                    _current = ValueBuffer.Empty;
                }
                return await ValueTask.FromResult(hasNext);
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
            throw new NotSupportedException(CoreStrings.EnumerableResetNotSupported);
        }
    }
}
