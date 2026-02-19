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
using MASES.KNet.Streams.State;

namespace MASES.EntityFrameworkCore.KNet.Storage.Internal;

/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public class KNetStreamsRetriever<TKey, TValue, TJVMKey, TJVMValue> : IKafkaStreamsRetriever<TKey>
    where TKey : notnull
    where TValue : IValueContainer<TKey>
{
    static StreamsManager<KNetStreams, StreamsBuilder, Topology, Org.Apache.Kafka.Streams.State.KeyValueBytesStoreSupplier, Materialized<TKey, TValue, TJVMKey, TJVMValue>, GlobalKTable<TKey, TValue, TJVMKey, TJVMValue>>? _streamsManager;

    private readonly IKafkaCluster _kafkaCluster;
    private readonly IEntityType _entityType;
    private readonly string _storageId;

    /// <summary>
    /// Default initializer
    /// </summary>
    public KNetStreamsRetriever(IKafkaCluster kafkaCluster, IEntityType entityType)
    {
        _kafkaCluster = kafkaCluster;
        _entityType = entityType;
        _streamsManager ??= new(_kafkaCluster, _entityType)
        {
            CreateStreamBuilder = static (streamsConfig) => new StreamsBuilder(streamsConfig),
            CreateStoreSupplier = static (usePersistentStorage, storageId) => usePersistentStorage ? Org.Apache.Kafka.Streams.State.Stores.PersistentKeyValueStore(storageId)
                                                                                                   : Org.Apache.Kafka.Streams.State.Stores.InMemoryKeyValueStore(storageId),
            CreateMaterialized = Materialized<TKey, TValue, TJVMKey, TJVMValue>.As,
            CreateGlobalTable = static (builder, topicName, materialized) => builder.GlobalTable(topicName, materialized),
            CreateTopology = static (builder) => builder.Build(),
            CreateStreams = static (topology, streamsConfig) => new(topology, streamsConfig),
            SetHandlers = static (streams, errorHandler, stateListener) =>
            {
                streams.SetUncaughtExceptionHandler(errorHandler);
                streams.SetStateListener(stateListener);
            },
            Start = static (streams) => streams.Start(),
            Close = static (streams) => streams.Close()
        };

        _storageId = _streamsManager.AddEntity(entityType);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _streamsManager!.Dispose(_entityType);
    }

    /// <inheritdoc/>
    public IEnumerable<ValueBuffer> GetValueBuffers()
    {
        return new KafkaEnumberable(_kafkaCluster, _entityType, _storageId, _streamsManager!.UseEnumeratorWithPrefetch);
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

        object[] array = null!;
        v?.GetData(_entityType, ref array);
        valueBuffer = new ValueBuffer(array);
        return true;
    }
    /// <inheritdoc/>
    public ValueBuffer GetValue(TKey key)
    {
        var v = GetTValue(key);
        object[] array = null!;
        v?.GetData(_entityType, ref array);
        return new ValueBuffer(array);
    }

    class KafkaEnumberable : IEnumerable<ValueBuffer>, IAsyncEnumerable<ValueBuffer>
    {
        private readonly bool _useEnumeratorWithPrefetch;
        private readonly IKafkaCluster _kafkaCluster;
        private readonly IEntityType _entityType;
        private readonly ReadOnlyKeyValueStore<TKey, TValue, TJVMKey, TJVMValue>? _keyValueStore = null;

        public KafkaEnumberable(IKafkaCluster kafkaCluster, IEntityType entityType, string storageId, bool useEnumeratorWithPrefetch)
        {
            _kafkaCluster = kafkaCluster;
            _entityType = entityType;
            _keyValueStore = _streamsManager!.Streams?.Store(storageId, QueryableStoreTypes.KeyValueStore<TKey, TValue, TJVMKey, TJVMValue>());
            _useEnumeratorWithPrefetch = useEnumeratorWithPrefetch;
#if DEBUG_PERFORMANCE
            Infrastructure.KafkaDbContext.ReportString($"KafkaEnumerator for {_entityType.Name} - ApproximateNumEntries {_keyValueStore?.ApproximateNumEntries}");
#endif
        }

        /// <inheritdoc/>
        public IEnumerator<ValueBuffer> GetEnumerator()
        {
            _streamsManager!.ThrowException();
#if DEBUG_PERFORMANCE
            Infrastructure.KafkaDbContext.ReportString($"Requesting KafkaEnumerator for {_entityType.Name} on {DateTime.Now:HH:mm:ss.FFFFFFF}");
#endif
            return new KafkaEnumerator(_kafkaCluster, _entityType, _keyValueStore?.All(), _useEnumeratorWithPrefetch, false);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IAsyncEnumerator<ValueBuffer> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            _streamsManager!.ThrowException();
#if DEBUG_PERFORMANCE
            Infrastructure.KafkaDbContext.ReportString($"Requesting async KafkaEnumerator for {_entityType.Name} on {DateTime.Now:HH:mm:ss.FFFFFFF}");
#endif
            return new KafkaEnumerator(_kafkaCluster, _entityType, _keyValueStore?.All(), _useEnumeratorWithPrefetch, true);
        }
    }

    class KafkaEnumerator : IEnumerator<ValueBuffer>
        , IAsyncEnumerator<ValueBuffer>
    {
        private readonly bool _useEnumeratorWithPrefetch;
        private readonly IKafkaCluster _kafkaCluster;
        private readonly IEntityType _entityType;
        private readonly KeyValueIterator<TKey, TValue, TJVMKey, TJVMValue>? _keyValueIterator = null;
        private readonly IEnumerator<KeyValue<TKey, TValue, TJVMKey, TJVMValue>>? _enumerator = null;
        private readonly IAsyncEnumerator<KeyValue<TKey, TValue, TJVMKey, TJVMValue>>? _asyncEnumerator = null;

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

        public KafkaEnumerator(IKafkaCluster kafkaCluster, IEntityType entityType, KeyValueIterator<TKey, TValue, TJVMKey, TJVMValue>? keyValueIterator, bool useEnumeratorWithPrefetch, bool isAsync)
        {
            _kafkaCluster = kafkaCluster ?? throw new ArgumentNullException(nameof(kafkaCluster));
            _entityType = entityType;
            _keyValueIterator = keyValueIterator ?? throw new ArgumentNullException(nameof(keyValueIterator));
            _useEnumeratorWithPrefetch = useEnumeratorWithPrefetch;
            if (_useEnumeratorWithPrefetch && !isAsync) _enumerator = _keyValueIterator.ToIEnumerator();
            if (isAsync) _asyncEnumerator = _keyValueIterator.GetAsyncEnumerator();
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
            Infrastructure.KafkaDbContext.ReportString($"KafkaEnumerator _moveNextSw: {_moveNextSw.Elapsed} _currentSw: {_currentSw.Elapsed} _valueGetSw: {_valueGetSw.Elapsed} _valueGet2Sw: {_valueGet2Sw.Elapsed} _valueBufferSw: {_valueBufferSw.Elapsed}");
#endif
            _enumerator?.Dispose();
        }

        public ValueTask DisposeAsync()
        {
#if DEBUG_PERFORMANCE
            Infrastructure.KafkaDbContext.ReportString($"KafkaEnumerator _moveNextSw: {_moveNextSw.Elapsed} _currentSw: {_currentSw.Elapsed} _valueGetSw: {_valueGetSw.Elapsed} _valueGet2Sw: {_valueGet2Sw.Elapsed} _valueBufferSw: {_valueBufferSw.Elapsed}");
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
                    object[] array = null!;
                    value?.GetData(_entityType, ref array);
#if DEBUG_PERFORMANCE
                    _valueBufferSw.Stop();
#endif
                    _current = new ValueBuffer(array);
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
                    object[] array = null!;
                    value?.GetData(_entityType, ref array);
#if DEBUG_PERFORMANCE
                    _valueBufferSw.Stop();
#endif
                    _current = new ValueBuffer(array);
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
