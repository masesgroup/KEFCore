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

using MASES.EntityFrameworkCore.KNet.Infrastructure.Internal;
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
public class KNetStreamsRetriever<TKey, TValue, TJVMKey, TJVMValue> : IKEFCoreStreamsRetriever<TKey>, IStreamsChangeManager
    where TKey : notnull
    where TValue : IValueContainer<TKey>
{
    class KNetStreamsRetrieverTimestampExtractor(Action<FreshEventChange> pushChanges, IStreamsChangeManager manager)
        : TimestampExtractor<TKey, TValue, TJVMKey, TJVMValue>
    {
        private readonly Action<FreshEventChange> _pushChanges = pushChanges;
        private readonly IStreamsChangeManager _manager = manager;

        public override DateTime Extract()
        {
            _pushChanges.Invoke(new FreshEventChange(_manager, Record.Topic, Record.Partition, Record.Offset, new Tuple<TKey, TValue>(Record.Key, Record.Value)));
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
    /// <param name="options"></param>
    /// <returns></returns>
    public static IStreamsManager Create(IKEFCoreCluster cluster, KEFCoreOptionsExtension options)
    {
        _streamsManager ??= new(cluster, options)
        {
            CreateStreamBuilder = static (streamsConfig) => new StreamsBuilder(streamsConfig),
            CreateStoreSupplier = static (usePersistentStorage, storageId) => usePersistentStorage ? Org.Apache.Kafka.Streams.State.Stores.PersistentKeyValueStore(storageId)
                                                                                                   : Org.Apache.Kafka.Streams.State.Stores.InMemoryKeyValueStore(storageId),
            CreateTimestampExtractor = static (enqueuer, manager, _) => new KNetStreamsRetrieverTimestampExtractor(enqueuer, manager),
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

    private readonly IEntityTypeProducer _producer;
    private readonly IValueContainerMetadata _metadata;
    private readonly IComplexTypeConverterFactory _complexTypeConverterFactory;
    private readonly string _storageId;

    /// <summary>
    /// Default initializer
    /// </summary>
    public KNetStreamsRetriever(IEntityTypeProducer producer, IValueContainerMetadata metadata, IComplexTypeConverterFactory complexTypeConverterFactory)
    {
        _producer = producer;
        _metadata = metadata;
        _complexTypeConverterFactory = complexTypeConverterFactory;
        _storageId = _streamsManager!.AddEntity(this, _metadata.EntityType, null);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _streamsManager!.Dispose(_metadata.EntityType);
    }

    /// <inheritdoc/>
    public IEnumerable<ValueBuffer> GetValueBuffers(IKEFCoreDatabase database)
    {
        return new KafkaEnumberable(_metadata, _complexTypeConverterFactory, _storageId, database.Options, false);
    }

    /// <inheritdoc/>
    public IEnumerable<ValueBuffer> GetValueBuffersRange(IKEFCoreDatabase database, IPrincipalKeyValueFactory<TKey> keyValueFactory, object?[]? rangeStart, object?[]? rangeEnd)
    {
        TKey? start = (TKey)keyValueFactory.CreateFromKeyValues(rangeStart!)!;
        TKey? end = (TKey)keyValueFactory.CreateFromKeyValues(rangeEnd!)!;
        return new KafkaEnumberable(_metadata, _complexTypeConverterFactory, _storageId, database.Options, false, start, end);
    }

    /// <inheritdoc/>
    public IEnumerable<ValueBuffer> GetValueBuffersReverse(IKEFCoreDatabase database)
    {
        return new KafkaEnumberable(_metadata, _complexTypeConverterFactory, _storageId, database.Options, true);
    }

    /// <inheritdoc/>
    public IEnumerable<ValueBuffer> GetValueBuffersReverseRange(IKEFCoreDatabase database, IPrincipalKeyValueFactory<TKey> keyValueFactory, object?[]? rangeStart, object?[]? rangeEnd)
    {
        TKey? start = (TKey)keyValueFactory.CreateFromKeyValues(rangeStart!)!;
        TKey? end = (TKey)keyValueFactory.CreateFromKeyValues(rangeEnd!)!;
        return new KafkaEnumberable(_metadata, _complexTypeConverterFactory, _storageId, database.Options, true, start, end);
    }

    /// <inheritdoc/>
    public IEnumerable<ValueBuffer> GetValueBuffersByPrefix(IKEFCoreDatabase database, IPrincipalKeyValueFactory<TKey> keyValueFactory, object?[]? prefixValues)
    {
        TKey? prefix = (TKey)keyValueFactory.CreateFromKeyValues(prefixValues!)!;
        return new KafkaEnumberable(_metadata, _complexTypeConverterFactory, _storageId, database.Options, prefix);
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
        v?.GetData(_metadata, ref propertyValues, _complexTypeConverterFactory);
        valueBuffer = new ValueBuffer(propertyValues);
        return true;
    }

    /// <inheritdoc/>
    public bool TryGetProperties(TKey key, out IDictionary<string, object?> properties, out IDictionary<string, object?> complexProperties)
    {
        var v = GetTValue(key);
        if (v == null)
        {
            properties = default!;
            complexProperties = default!;
            return false;
        }

        properties = v?.GetProperties(_metadata.EntityType)!;
        complexProperties = v?.GetComplexProperties(_metadata.EntityType, _complexTypeConverterFactory)!;
        return true;
    }

    IEntityTypeProducer IStreamsChangeManager.Producer => _producer;

    void IStreamsChangeManager.ManageChange(IDiagnosticsLogger<DbLoggerCategory.Infrastructure> infrastructureLogger, IValueGeneratorSelector valueGeneratorSelector, IUpdateAdapter adapter, IEntityType entityType, IKey primaryKey, object data)
    {
        var input = (Tuple<TKey, TValue>)data;
        KEFCoreStateHelper.ManageAdded(infrastructureLogger, valueGeneratorSelector, _complexTypeConverterFactory, adapter, entityType, primaryKey, input.Item1, input.Item2);
    }

    static IEnumerable<StoredEventChange> GetStoredData(KNetStreams streams, string storageId)
    {
        ReadOnlyKeyValueStore<TKey, TValue, TJVMKey, TJVMValue>? keyValueStore = streams?.Store(storageId, QueryableStoreTypes.KeyValueStore<TKey, TValue, TJVMKey, TJVMValue>());

        foreach (var item in keyValueStore!.All())
        {
            yield return new StoredEventChange(new Tuple<TKey, TValue>(item.Key, item.Value));
        }
    }

    class KafkaEnumberable : IEnumerable<ValueBuffer>, IAsyncEnumerable<ValueBuffer>
    {
        private readonly KEFCoreOptionsExtension _options;
        private readonly bool _withPrefix;
        private readonly bool _isReverse;
        private readonly bool _useRange = false;
        private readonly TKey? _rangeStart;
        private readonly TKey? _rangeEnd;
        private readonly TKey? _prefix;
        private readonly IValueContainerMetadata _metadata;
        private readonly IComplexTypeConverterFactory _complexTypeConverterFactory;
        private readonly ReadOnlyKeyValueStore<TKey, TValue, TJVMKey, TJVMValue>? _keyValueStore = null;

        public KafkaEnumberable(IValueContainerMetadata metadata, IComplexTypeConverterFactory complexTypeConverterFactory, string storageId, KEFCoreOptionsExtension options, bool isReverse)
        {
            _metadata = metadata;
            _complexTypeConverterFactory = complexTypeConverterFactory;
            _keyValueStore = _streamsManager!.Streams?.Store(storageId, QueryableStoreTypes.KeyValueStore<TKey, TValue, TJVMKey, TJVMValue>());
            _options = options;
            _isReverse = isReverse;
#if DEBUG_PERFORMANCE
            KNet.Internal.DebugPerformanceHelper.ReportString($"KafkaEnumerator for {_metadata.EntityType.Name} - ApproximateNumEntries {_keyValueStore?.ApproximateNumEntries()}");
#endif
        }

        public KafkaEnumberable(IValueContainerMetadata metadata, IComplexTypeConverterFactory complexTypeConverterFactory, string storageId, KEFCoreOptionsExtension options, bool isReverse, TKey? rangeStart, TKey? rangeEnd)
        {
            _metadata = metadata;
            _complexTypeConverterFactory = complexTypeConverterFactory;
            _keyValueStore = _streamsManager!.Streams?.Store(storageId, QueryableStoreTypes.KeyValueStore<TKey, TValue, TJVMKey, TJVMValue>());
            _options = options;
            _isReverse = isReverse;
            _useRange = true;
            _rangeStart = rangeStart;
            _rangeEnd = rangeEnd;
#if DEBUG_PERFORMANCE
            KNet.Internal.DebugPerformanceHelper.ReportString($"KafkaEnumerator for {_metadata.EntityType.Name} - ApproximateNumEntries {_keyValueStore?.ApproximateNumEntries()}");
#endif
        }

        public KafkaEnumberable(IValueContainerMetadata metadata, IComplexTypeConverterFactory complexTypeConverterFactory, string storageId, KEFCoreOptionsExtension options, TKey? prefix)
        {
            _metadata = metadata;
            _complexTypeConverterFactory = complexTypeConverterFactory;
            _keyValueStore = _streamsManager!.Streams?.Store(storageId, QueryableStoreTypes.KeyValueStore<TKey, TValue, TJVMKey, TJVMValue>());
            _options = options;
            _withPrefix = true;
            _prefix = prefix;
#if DEBUG_PERFORMANCE
            KNet.Internal.DebugPerformanceHelper.ReportString($"KafkaEnumerator for {_metadata.EntityType.Name} - ApproximateNumEntries {_keyValueStore?.ApproximateNumEntries()}");
#endif
        }

        static KeyValueIterator<TKey, TValue, TJVMKey, TJVMValue>? GetIterator(ReadOnlyKeyValueStore<TKey, TValue, TJVMKey, TJVMValue>? keyValueStore,
                                                                               bool isReverse,
                                                                               bool useRange,
                                                                               bool withPrefix,
                                                                               TKey? rangeStart,
                                                                               TKey? rangeEnd,
                                                                               TKey? prefix,
                                                                               CancellationToken cancellationToken = default)
        {
            const int maxCycle = 100;
            const int waitTime = 100;
            int cycle = 0;
#if DEBUG_PERFORMANCE
            KNet.Internal.DebugPerformanceHelper.ReportString($"GetIterator isReverse={isReverse} - useRange={useRange}" + (useRange ? $" rangeStart={rangeStart} - rangeEnd={rangeEnd}" : string.Empty));
#endif
            try
            {
                return GetIterator(keyValueStore, isReverse, useRange, withPrefix, rangeStart, rangeEnd, prefix, waitTime, maxCycle, ref cycle, cancellationToken);
            }
            catch (Org.Apache.Kafka.Streams.Errors.InvalidStateStoreException isse)
            {
                throw new InvalidOperationException($"Failed to retrieve {nameof(KeyValueIterator<,,,>)} after {cycle * waitTime} ms", isse);
            }
        }

        static KeyValueIterator<TKey, TValue, TJVMKey, TJVMValue>? GetIterator(ReadOnlyKeyValueStore<TKey, TValue, TJVMKey, TJVMValue>? keyValueStore, bool isReverse, bool useRange, bool withPrefix, TKey? rangeStart, TKey? rangeEnd, TKey? prefix, int waitTime, int maxCycle, ref int currentCycle, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (withPrefix)
                {
                    throw new NotImplementedException("PrefixScan is not implemented yer");
                }
                else
                {
                    return !useRange ? (isReverse ? keyValueStore?.ReverseAll()
                                                  : keyValueStore?.All())
                                     : (isReverse ? keyValueStore?.ReverseRange(rangeStart!, rangeEnd!)
                                                  : keyValueStore?.Range(rangeStart!, rangeEnd!));
                }
            }
            catch (Org.Apache.Kafka.Streams.Errors.InvalidStateStoreException isse)
            {
                if (isse.Message.Contains(", not RUNNING") && currentCycle < maxCycle)
                {
                    Thread.Sleep(waitTime);
                    currentCycle++;
                    return GetIterator(keyValueStore, isReverse, useRange, withPrefix, rangeStart, rangeEnd, prefix, waitTime, maxCycle, ref currentCycle, cancellationToken);
                }
                throw;
            }
        }

        /// <inheritdoc/>
        public IEnumerator<ValueBuffer> GetEnumerator()
        {
            _streamsManager!.ThrowException();
#if DEBUG_PERFORMANCE
            KNet.Internal.DebugPerformanceHelper.ReportString($"Requesting KafkaEnumerator for {_metadata.EntityType.Name} on {DateTime.Now:HH:mm:ss.FFFFFFF}");
#endif
            return new KafkaEnumerator(_metadata, _complexTypeConverterFactory, GetIterator(_keyValueStore, _isReverse, _useRange, _withPrefix, _rangeStart, _rangeEnd, _prefix), _options.UseEnumeratorWithPrefetch, false);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IAsyncEnumerator<ValueBuffer> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            _streamsManager!.ThrowException();
#if DEBUG_PERFORMANCE
            KNet.Internal.DebugPerformanceHelper.ReportString($"Requesting async KafkaEnumerator for {_metadata.EntityType.Name} on {DateTime.Now:HH:mm:ss.FFFFFFF}");
#endif
            return new KafkaEnumerator(_metadata, _complexTypeConverterFactory, GetIterator(_keyValueStore, _isReverse, _useRange, _withPrefix, _rangeStart, _rangeEnd, _prefix, cancellationToken), _options.UseEnumeratorWithPrefetch, true, cancellationToken);
        }
    }

    class KafkaEnumerator : IEnumerator<ValueBuffer>
        , IAsyncEnumerator<ValueBuffer>
    {
        private readonly bool _useEnumeratorWithPrefetch;
        private readonly IValueContainerMetadata _metadata;
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

        public KafkaEnumerator(IValueContainerMetadata metadata, IComplexTypeConverterFactory complexTypeConverterFactory, KeyValueIterator<TKey, TValue, TJVMKey, TJVMValue>? keyValueIterator, bool useEnumeratorWithPrefetch, bool isAsync, CancellationToken cancellationToken = default)
        {
            _metadata = metadata;
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
                    value?.GetData(_metadata, ref propertyValues, _complexTypeConverterFactory);
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
                    KNet.Internal.DebugPerformanceHelper.ReportString($"KafkaEnumerator - No data returned from {_keyValueIterator}");
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
                    value?.GetData(_metadata, ref propertyValues, _complexTypeConverterFactory);
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
                    KNet.Internal.DebugPerformanceHelper.ReportString($"KafkaEnumerator - No data returned from {_keyValueIterator}");
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
