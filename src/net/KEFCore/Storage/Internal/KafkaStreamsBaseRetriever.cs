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

// #define DEBUG_PERFORMANCE

#nullable enable

using Java.Util;
using MASES.EntityFrameworkCore.KNet.Serialization;
using MASES.KNet.Serialization;
using Org.Apache.Kafka.Common.Utils;
using Org.Apache.Kafka.Streams;
using Org.Apache.Kafka.Streams.Errors;
using Org.Apache.Kafka.Streams.Kstream;
using Org.Apache.Kafka.Streams.State;
using static Org.Apache.Kafka.Streams.Errors.StreamsUncaughtExceptionHandler;

namespace MASES.EntityFrameworkCore.KNet.Storage.Internal;

/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public class KafkaStreamsBaseRetriever<TKey, TValue, K, V> : IKafkaStreamsRetriever
    where TKey : notnull
    where TValue : IValueContainer<TKey>
{
    static StreamsManager<KafkaStreams, StreamsBuilder, Topology, KeyValueBytesStoreSupplier, Materialized<K, V, KeyValueStore<Bytes, byte[]>>, GlobalKTable<K, V>>? _streamsManager;

    private static StreamsBuilder? _builder = null;
    private static Properties? _properties = null;

    private readonly IKafkaCluster _kafkaCluster;
    private readonly IEntityType _entityType;
    private readonly ISerDes<TKey, K> _keySerdes;
    private readonly ISerDes<TValue, V> _valueSerdes;
    private readonly string _storageId;

    /// <summary>
    /// Default initializer
    /// </summary>
    public KafkaStreamsBaseRetriever(IKafkaCluster kafkaCluster, IEntityType entityType, ISerDes<TKey, K> keySerdes, ISerDes<TValue, V> valueSerdes, StreamsBuilder builder)
    {
        _kafkaCluster = kafkaCluster;
        _entityType = entityType;
        _keySerdes = keySerdes;
        _valueSerdes = valueSerdes;
        _builder ??= builder;
        _streamsManager ??= new(_kafkaCluster, _entityType)
        {
            CreateStreamBuilder = static (streamsConfig) => _builder,
            CreateStoreSupplier = static (usePersistentStorage, storageId) => usePersistentStorage ? Stores.PersistentKeyValueStore(storageId)
                                                                                                   : Stores.InMemoryKeyValueStore(storageId),
            CreateMaterialized = Materialized<K, V, KeyValueStore<Bytes, byte[]>>.As,
            CreateGlobalTable = static (builder, topicName, materialized) => builder.GlobalTable(topicName, materialized),
            CreateTopology = static (builder) => builder.Build(),
            CreateStreams = static (topology, streamsConfig) =>
            {
                _properties ??= streamsConfig.ToProperties();
                return new(topology, streamsConfig);
            },
            SetHandlers = static (streams, errorHandler, stateListener) =>
            {
                streams.SetUncaughtExceptionHandler(errorHandler);
                streams.SetStateListener(stateListener);
            },
            Start = static (streams) => streams.Start(),
            Close = static (streams) => streams.Close()
        };

        _storageId = _streamsManager.AddEntity(_entityType);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _streamsManager!.Dispose(_entityType);
    }

    /// <inheritdoc/>
    public IEnumerable<ValueBuffer> GetValueBuffers()
    {
        return new KafkaEnumberable(_kafkaCluster, _entityType, _keySerdes, _valueSerdes, _storageId);
    }

    class KafkaEnumberable : IEnumerable<ValueBuffer>
    {
        private readonly IKafkaCluster _kafkaCluster;
        private readonly IEntityType _entityType;
        private readonly ISerDes<TKey, K> _keySerdes;
        private readonly ISerDes<TValue, V> _valueSerdes;
        private readonly ReadOnlyKeyValueStore<K, V>? _keyValueStore = null;

        public KafkaEnumberable(IKafkaCluster kafkaCluster, IEntityType entityType, ISerDes<TKey, K> keySerdes, ISerDes<TValue, V> valueSerdes, string storageId)
        {
            _kafkaCluster = kafkaCluster;
            _entityType = entityType;
            _keySerdes = keySerdes;
            _valueSerdes = valueSerdes;
            _keyValueStore = _streamsManager!.Streams?.Store(StoreQueryParameters<Org.Apache.Kafka.Streams.State.ReadOnlyKeyValueStore<K, V>>.FromNameAndType(storageId, Org.Apache.Kafka.Streams.State.QueryableStoreTypes.KeyValueStore<K, V>()));
#if DEBUG_PERFORMANCE
            Infrastructure.KafkaDbContext.ReportString($"KafkaEnumerator for {_entityType.Name} - ApproximateNumEntries {_keyValueStore?.ApproximateNumEntries()}");
#endif
        }

        /// <inheritdoc/>
        public IEnumerator<ValueBuffer> GetEnumerator()
        {
            _streamsManager!.ThrowException();
#if DEBUG_PERFORMANCE
            Infrastructure.KafkaDbContext.ReportString($"Requesting KafkaEnumerator for {_entityType.Name} on {DateTime.Now:HH:mm:ss.FFFFFFF}");
#endif
            return new KafkaEnumerator(_kafkaCluster, _entityType, _keySerdes, _valueSerdes, _keyValueStore?.All());
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    class KafkaEnumerator : IEnumerator<ValueBuffer>
    {
        private readonly IKafkaCluster _kafkaCluster;
        private readonly IEntityType _entityType;
        private readonly ISerDes<TKey, K> _keySerdes;
        private readonly ISerDes<TValue, V> _valueSerdes;
        private readonly Org.Apache.Kafka.Streams.State.KeyValueIterator<K, V>? _keyValueIterator = null;

        Stopwatch _valueGet = new Stopwatch();

#if DEBUG_PERFORMANCE
        Stopwatch _moveNextSw = new Stopwatch();
        Stopwatch _currentSw = new Stopwatch();
        Stopwatch _valueGetSw = new Stopwatch();
        Stopwatch _valueSerdesSw = new Stopwatch();
        Stopwatch _valueBufferSw = new Stopwatch();
#endif

        public KafkaEnumerator(IKafkaCluster kafkaCluster, IEntityType entityType, ISerDes<TKey, K> keySerdes, ISerDes<TValue, V> valueSerdes, Org.Apache.Kafka.Streams.State.KeyValueIterator<K, V>? keyValueIterator)
        {
            _kafkaCluster = kafkaCluster ?? throw new ArgumentNullException(nameof(kafkaCluster));
            _entityType = entityType;
            _keySerdes = keySerdes ?? throw new ArgumentNullException(nameof(keySerdes));
            _valueSerdes = valueSerdes ?? throw new ArgumentNullException(nameof(valueSerdes));
            _keyValueIterator = keyValueIterator ?? throw new ArgumentNullException(nameof(keyValueIterator));
#if DEBUG_PERFORMANCE
            Infrastructure.KafkaDbContext.ReportString($"Requested KafkaEnumerator for {_entityType.Name} on {DateTime.Now:HH:mm:ss.FFFFFFF}");
#endif
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
            Infrastructure.KafkaDbContext.ReportString($"KafkaEnumerator _moveNextSw: {_moveNextSw.Elapsed} _currentSw: {_currentSw.Elapsed} _valueGetSw: {_valueGetSw.Elapsed} _valueSerdesSw: {_valueSerdesSw.Elapsed} _valueBufferSw: {_valueBufferSw.Elapsed}");
#endif
            _keyValueIterator?.Dispose();
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
            if (_keyValueIterator != null && _keyValueIterator.HasNext())
            {
#if DEBUG_PERFORMANCE
                _cycles++;
                _valueGetSw.Start();
#endif
                V? data;
                using (KeyValue<K, V> kv = _keyValueIterator.Next())
                {
                    var kvSupport = new MASES.KNet.Streams.KeyValueSupport<K, V>(kv);
                    data = kvSupport.Value != null ? (V)(object)kvSupport.Value! : default;
                }
#if DEBUG_PERFORMANCE
                _valueGetSw.Stop();
                _valueSerdesSw.Start();
#endif
                TValue entityTypeData = _valueSerdes.DeserializeWithHeaders(null, null, data!);
#if DEBUG_PERFORMANCE
                _valueSerdesSw.Stop();
                _valueBufferSw.Start();
#endif
                object[] array = null!;
                entityTypeData?.GetData(_entityType, ref array);
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

        public void Reset()
        {
            throw new NotSupportedException(CoreStrings.EnumerableResetNotSupported);
        }
    }
}
