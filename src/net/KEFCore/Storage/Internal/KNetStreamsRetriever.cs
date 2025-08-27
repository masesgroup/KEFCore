﻿/*
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

// #define DEBUG_PERFORMANCE

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
public class KNetStreamsRetriever<TKey, TValue, TJVMKey, TJVMValue> : IKafkaStreamsRetriever
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
            CreateStreamBuilder = (streamsConfig) =>  new StreamsBuilder(streamsConfig),
            CreateStoreSupplier = (usePersistentStorage, storageId) => usePersistentStorage ? Org.Apache.Kafka.Streams.State.Stores.PersistentKeyValueStore(storageId)
                                                                                            : Org.Apache.Kafka.Streams.State.Stores.InMemoryKeyValueStore(storageId),
            CreateMaterialized = (storeSupplier) => Materialized<TKey, TValue, TJVMKey, TJVMValue>.As(storeSupplier),
            CreateGlobalKTable = (builder, topicName, materialized) => builder.GlobalTable(topicName, materialized),
            CreateTopology = (builder) => builder.Build(),
            CreateStreams = (topology, streamsConfig) => new(topology, streamsConfig),
            SetHandlers = (streams, errorHandler, stateListener) =>
            {
                streams.SetUncaughtExceptionHandler(errorHandler);
                streams.SetStateListener(stateListener);
            },
            Start = (streams) => streams.Start(),
            Close = (streams) => streams.Close()
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
            if (_useEnumeratorWithPrefetch ? _enumerator != null && _enumerator.MoveNext() : _keyValueIterator != null && _keyValueIterator.HasNext())
            {
#if DEBUG_PERFORMANCE
                _cycles++;
                _valueGetSw.Start();
#endif
                KeyValue<TKey, TValue, TJVMKey, TJVMValue>? kv = _useEnumeratorWithPrefetch ? _enumerator?.Current : _keyValueIterator?.Next();
#if DEBUG_PERFORMANCE
                _valueGetSw.Stop();
                _valueGet2Sw.Start();
#endif
                TValue? value = kv != null ? kv.Value : default;
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
            _current = ValueBuffer.Empty;
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

        public ValueTask<bool> MoveNextAsync()
        {
#if DEBUG_PERFORMANCE
                try
                {
                    _moveNextSw.Start();
#endif
            ValueTask<bool> hasNext = _asyncEnumerator == null ? new ValueTask<bool>(false) : _asyncEnumerator.MoveNextAsync();
            hasNext.AsTask().Wait();
            if (hasNext.Result)
            {
#if DEBUG_PERFORMANCE
                _cycles++;
                _valueGetSw.Start();
#endif
                KeyValue<TKey, TValue, TJVMKey, TJVMValue>? kv = _asyncEnumerator?.Current;
#if DEBUG_PERFORMANCE
                _valueGetSw.Stop();
                _valueGet2Sw.Start();
#endif
                TValue? value = kv != null ? kv.Value : default;
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

                return ValueTask.FromResult(true);
            }
            _current = ValueBuffer.Empty;
            return ValueTask.FromResult(false);
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
