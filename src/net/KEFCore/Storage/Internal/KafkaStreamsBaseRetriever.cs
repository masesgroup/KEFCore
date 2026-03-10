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

using Java.Util;
using MASES.EntityFrameworkCore.KNet.Serialization;
using MASES.KNet.Serialization;
using Org.Apache.Kafka.Clients.Consumer;
using Org.Apache.Kafka.Common.Utils;
using Org.Apache.Kafka.Streams;
using Org.Apache.Kafka.Streams.Kstream;
using Org.Apache.Kafka.Streams.Processor;
using Org.Apache.Kafka.Streams.State;

namespace MASES.EntityFrameworkCore.KNet.Storage.Internal;

/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public class KafkaStreamsBaseRetriever<TKey, TValue, K, V> : IKafkaStreamsRetriever<TKey>, IStreamsChangeManager
    where TKey : notnull
    where TValue : IValueContainer<TKey>
{
    class KafkaStreamsBaseRetrieverTimestampExtractor : TimestampExtractor
    {
        private readonly Action<FreshEventChange> _pushChanges;
        private readonly IStreamsChangeManager _manager;
        private readonly IEntityType _entityType;
        private readonly IKey _primaryKey;
        private readonly ISerDes<TKey, K> _keySerdes;
        private readonly ISerDes<TValue, V> _valueSerdes;

        public KafkaStreamsBaseRetrieverTimestampExtractor(Action<FreshEventChange> pushChanges, IStreamsChangeManager manager, IEntityType entityType, IKey primaryKey, object optional)
        {
            _pushChanges = pushChanges;
            _manager = manager;
            _entityType = entityType;
            _primaryKey = primaryKey;
            var serdes = (Tuple<ISerDes<TKey, K>, ISerDes<TValue, V>>)optional;
            _keySerdes = serdes.Item1;
            _valueSerdes = serdes.Item2;
        }

        public override long Extract(ConsumerRecord<object, object> record, long timestamp)
        {
            var record2 = record.CastTo<ConsumerRecord<K, V>>();
            var topic = record2.Topic();
            var headers = record2.Headers();

            var key = _keySerdes.DeserializeWithHeaders(topic, headers, record2.Key());
            var value = _valueSerdes.DeserializeWithHeaders(topic, headers, record2.Value());

            _pushChanges.Invoke(new FreshEventChange(_manager, _entityType, _primaryKey, record.Partition(), record.Offset(), new Tuple<TKey, TValue>(key, value)));

            return record.Timestamp();
        }
    }

    static StreamsManager<KafkaStreams,
                          StreamsBuilder,
                          Topology,
                          KeyValueBytesStoreSupplier,
                          TimestampExtractor,
                          Consumed<K, V>,
                          Materialized<K, V, KeyValueStore<Bytes, byte[]>>,
                          GlobalKTable<K, V>,
                          KTable<K, V>>? _streamsManager;

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
            CreateStreamBuilder = static (streamsConfig) => new StreamsBuilder(),
            CreateStoreSupplier = static (usePersistentStorage, storageId) => usePersistentStorage ? Stores.PersistentKeyValueStore(storageId)
                                                                                                   : Stores.InMemoryKeyValueStore(storageId),
            CreateTimestampExtractor = static (enqueuer, manager, entity, key, optional) => new KafkaStreamsBaseRetrieverTimestampExtractor(enqueuer, manager, entity, key, optional),
            CreateConsumed = static (extractor) => Consumed<K, V>.With(extractor),
            CreateMaterialized = static (supplier) => Materialized<K, V, KeyValueStore<Bytes, byte[]>>.As(supplier),
            CreateGlobalTable = static (builder, topicName, materialized) => builder.GlobalTable(topicName, materialized),
            CreateTable = static (builder, topicName, consumed, materialized) => consumed != null ? builder.Table(topicName, consumed, materialized)
                                                                                                  : builder.Table(topicName, materialized),
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
            GetStoredData = static (streams, storageId, optional) => GetStoredData(streams, storageId, optional),
            Start = static (streams) => streams.Start(),
            Pause = static (streams) => streams.Pause(),
            Resume = static (streams) => streams.Resume(),
            Close = static (streams) => streams.Close(),
            GetState = static (streams) => streams.StateMethod(),
            GetLags = static (streams) => streams.AllLocalStorePartitionLags(),
            GetIsPaused = static (streams) => streams.IsPaused(),
        };

        return _streamsManager;
    }

    private static Properties? _properties = null;

    private readonly IKafkaCluster _cluster;
    private readonly IValueContainerMetadata _metadata;
    private readonly IKey _primaryKey;
    private readonly IComplexTypeConverterFactory _complexTypeConverterFactory;
    private readonly ISerDes<TKey, K> _keySerdes;
    private readonly ISerDes<TValue, V> _valueSerdes;
    private readonly string _storageId;

    /// <summary>
    /// Default initializer
    /// </summary>
    public KafkaStreamsBaseRetriever(IKafkaCluster cluster, IValueContainerMetadata metadata, IKey primaryKey, IComplexTypeConverterFactory complexTypeConverterFactory, ISerDes<TKey, K> keySerdes, ISerDes<TValue, V> valueSerdes)
    {
        _cluster = cluster;
        _metadata = metadata;
        _primaryKey = primaryKey;
        _complexTypeConverterFactory = complexTypeConverterFactory;
        _keySerdes = keySerdes;
        _valueSerdes = valueSerdes;

        _storageId = _streamsManager!.AddEntity(this, _metadata.EntityType, new Tuple<ISerDes<TKey, K>, ISerDes<TValue, V>>(_keySerdes, _valueSerdes));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _streamsManager!.Dispose(_metadata.EntityType);
    }

    /// <inheritdoc/>
    public IEnumerable<ValueBuffer> GetValueBuffers()
    {
        return new KafkaEnumberable(_metadata, _complexTypeConverterFactory, _keySerdes, _valueSerdes, _storageId, false);
    }

    /// <inheritdoc/>
    public IEnumerable<ValueBuffer> GetValueBuffersRange(TKey rangeStart, TKey rangeEnd)
    {
        return new KafkaEnumberable(_metadata, _complexTypeConverterFactory, _keySerdes, _valueSerdes, _storageId, false, rangeStart, rangeEnd);
    }

    /// <inheritdoc/>
    public IEnumerable<ValueBuffer> GetValueBuffersReverse()
    {
        return new KafkaEnumberable(_metadata, _complexTypeConverterFactory, _keySerdes, _valueSerdes, _storageId, true);
    }

    /// <inheritdoc/>
    public IEnumerable<ValueBuffer> GetValueBuffersReverseRange(TKey rangeStart, TKey rangeEnd)
    {
        return new KafkaEnumberable(_metadata, _complexTypeConverterFactory, _keySerdes, _valueSerdes, _storageId, true, rangeStart, rangeEnd);
    }

    V GetV(TKey key)
    {
        ReadOnlyKeyValueStore<K, V>? keyValueStore = _streamsManager!.Streams?.Store(StoreQueryParameters<ReadOnlyKeyValueStore<K, V>>.FromNameAndType(_storageId, QueryableStoreTypes.KeyValueStore<K, V>()));
        if (keyValueStore == null) return default!;
        var k = _keySerdes.Serialize(null, key);
        var v = keyValueStore.Get(k);
        return v;
    }

    /// <inheritdoc/>
    public bool Exist(TKey key)
    {
        var v = GetV(key);
        return v != null;
    }
    /// <inheritdoc/>
    public bool TryGetValue(TKey key, out ValueBuffer valueBuffer)
    {
        var v = GetV(key);
        if (v == null)
        {
            valueBuffer = ValueBuffer.Empty;
            return false;
        }
        var entityTypeData = _valueSerdes.DeserializeWithHeaders(null, null, v!);

        object[] propertyValues = null!;
        entityTypeData?.GetData(_metadata, ref propertyValues, _complexTypeConverterFactory);
        valueBuffer = new ValueBuffer(propertyValues);
        return true;
    }
    /// <inheritdoc/>
    public bool TryGetProperties(TKey key, out IDictionary<string, object?> properties, out IDictionary<string, object?> complexProperties)
    {
        var v = GetV(key);
        if (v == null)
        {
            properties = default!;
            complexProperties = default!;
            return false;
        }
        var entityTypeData = _valueSerdes.DeserializeWithHeaders(null, null, v!);
        properties = entityTypeData?.GetProperties(_metadata.EntityType)!;
        complexProperties = entityTypeData?.GetComplexProperties(_metadata.EntityType, _complexTypeConverterFactory)!;
        return true;
    }

    void IStreamsChangeManager.ManageChange(IValueGeneratorSelector valueGeneratorSelector, IUpdateAdapter adapter, IEntityType entityType, IKey primaryKey, object data)
    {
        var input = (Tuple<TKey, TValue>)data;
        KafkaStateHelper.ManageAdded(_cluster.InfrastructureLogger, valueGeneratorSelector, _complexTypeConverterFactory, adapter, entityType, primaryKey, input.Item1, input.Item2);
    }

    static IEnumerable<StoredEventChange> GetStoredData(KafkaStreams streams, string storageId, object optional)
    {
        var serdes = (Tuple<ISerDes<TKey, K>, ISerDes<TValue, V>>)optional;
        ISerDes<TKey, K> keySerdes = serdes.Item1;
        ISerDes<TValue, V> valueSerdes = serdes.Item2;

        ReadOnlyKeyValueStore<K, V>? keyValueStore = streams?.Store(StoreQueryParameters<ReadOnlyKeyValueStore<K, V>>.FromNameAndType(storageId, QueryableStoreTypes.KeyValueStore<K, V>()));
        var iterator = keyValueStore?.All();
        while (iterator!.HasNext())
        {
            using KeyValue<K, V> kv = iterator.Next();
            var kvSupport = new MASES.KNet.Streams.KeyValueSupport<K, V>(kv);
            yield return new StoredEventChange(new Tuple<TKey, TValue>( keySerdes.Deserialize(null, kvSupport.Key), valueSerdes.Deserialize(null, kvSupport.Value)));
        }
    }

    class KafkaEnumberable : IEnumerable<ValueBuffer>
    {
        private readonly bool _isReverse;
        private readonly bool _useRange = false;
        private readonly K? _rangeStart;
        private readonly K? _rangeEnd;
        private readonly IValueContainerMetadata _metadata;
        private readonly IComplexTypeConverterFactory _complexTypeConverterFactory;
        private readonly ISerDes<TKey, K> _keySerdes;
        private readonly ISerDes<TValue, V> _valueSerdes;
        private readonly ReadOnlyKeyValueStore<K, V>? _keyValueStore = null;

        public KafkaEnumberable(IValueContainerMetadata metadata, IComplexTypeConverterFactory complexTypeConverterFactory, ISerDes<TKey, K> keySerdes, ISerDes<TValue, V> valueSerdes, string storageId, bool isReverse)
        {
            _metadata = metadata;
            _complexTypeConverterFactory = complexTypeConverterFactory;
            _keySerdes = keySerdes;
            _valueSerdes = valueSerdes;
            _isReverse = isReverse;
            _keyValueStore = _streamsManager!.Streams?.Store(StoreQueryParameters<ReadOnlyKeyValueStore<K, V>>.FromNameAndType(storageId, QueryableStoreTypes.KeyValueStore<K, V>()));
#if DEBUG_PERFORMANCE
            KNet.Internal.DebugPerformanceHelper.ReportString($"KafkaEnumerator for {_metadata.EntityType.Name} - ApproximateNumEntries {_keyValueStore?.ApproximateNumEntries()}");
#endif
        }

        public KafkaEnumberable(IValueContainerMetadata metadata, IComplexTypeConverterFactory complexTypeConverterFactory, ISerDes<TKey, K> keySerdes, ISerDes<TValue, V> valueSerdes, string storageId, bool isReverse, TKey? rangeStart, TKey? rangeEnd)
        {
            _metadata = metadata;
            _complexTypeConverterFactory = complexTypeConverterFactory;
            _keySerdes = keySerdes;
            _valueSerdes = valueSerdes;
            _isReverse = isReverse;
            _useRange = true;
            _rangeStart = _keySerdes.Serialize(null, rangeStart!);
            _rangeEnd = _keySerdes.Serialize(null, rangeEnd!);
            _keyValueStore = _streamsManager!.Streams?.Store(StoreQueryParameters<ReadOnlyKeyValueStore<K, V>>.FromNameAndType(storageId, QueryableStoreTypes.KeyValueStore<K, V>()));
#if DEBUG_PERFORMANCE
            KNet.Internal.DebugPerformanceHelper.ReportString($"KafkaEnumerator for {_metadata.EntityType.Name} - ApproximateNumEntries {_keyValueStore?.ApproximateNumEntries()}");
#endif
        }

        static Org.Apache.Kafka.Streams.State.KeyValueIterator<K, V>? GetIterator(ReadOnlyKeyValueStore<K, V>? keyValueStore, bool isReverse, bool useRange, K? rangeStart, K? rangeEnd)
        {
            const int maxCycle = 100;
            const int waitTime = 100;
            int cycle = 0;
#if DEBUG_PERFORMANCE
            KNet.Internal.DebugPerformanceHelper.ReportString($"GetIterator isReverse={isReverse} - useRange={useRange}" + (useRange ? $" rangeStart={rangeStart} - rangeEnd={rangeEnd}" : string.Empty));
#endif
            try
            {
                return GetIterator(keyValueStore, isReverse, useRange, rangeStart, rangeEnd, waitTime, maxCycle, ref cycle);
            }
            catch (Org.Apache.Kafka.Streams.Errors.InvalidStateStoreException isse)
            {
                throw new InvalidOperationException($"Failed to retrieve {nameof(Org.Apache.Kafka.Streams.State.KeyValueIterator<,>)} after {cycle * waitTime} ms", isse);
            }
        }

        static Org.Apache.Kafka.Streams.State.KeyValueIterator<K, V>? GetIterator(ReadOnlyKeyValueStore<K, V>? keyValueStore, bool isReverse, bool useRange, K? rangeStart, K? rangeEnd, int waitTime, int maxCycle, ref int currentCycle)
        {
            try
            {
                return !useRange ? (isReverse ? keyValueStore?.ReverseAll()
                                              : keyValueStore?.All())
                                 : (isReverse ? keyValueStore?.ReverseRange(rangeStart!, rangeEnd!)
                                              : keyValueStore?.Range(rangeStart!, rangeEnd!));
            }
            catch (Org.Apache.Kafka.Streams.Errors.InvalidStateStoreException isse)
            {
                if (isse.Message.Contains(", not RUNNING") && currentCycle < maxCycle)
                {
                    Thread.Sleep(waitTime);
                    currentCycle++;
                    return GetIterator(keyValueStore, isReverse, useRange, rangeStart, rangeEnd, waitTime, maxCycle, ref currentCycle);
                }
                throw;
            }
        }

        /// <inheritdoc/>
        public IEnumerator<ValueBuffer> GetEnumerator()
        {
            _streamsManager!.ThrowException();
#if DEBUG_PERFORMANCE
            KNet.Internal.DebugPerformanceHelper.ReportString($"Requesting KafkaEnumerator for { _metadata.EntityType.Name} on {DateTime.Now:HH:mm:ss.FFFFFFF}");
#endif
            return new KafkaEnumerator(_metadata, _complexTypeConverterFactory, _keySerdes, _valueSerdes, GetIterator(_keyValueStore, _isReverse, _useRange, _rangeStart, _rangeEnd));
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    class KafkaEnumerator : IEnumerator<ValueBuffer>
    {
        private readonly IValueContainerMetadata _metadata;
        private readonly IComplexTypeConverterFactory _complexTypeConverterFactory;
        private readonly ISerDes<TKey, K> _keySerdes;
        private readonly ISerDes<TValue, V> _valueSerdes;
        private readonly Org.Apache.Kafka.Streams.State.KeyValueIterator<K, V>? _keyValueIterator = null;

#if DEBUG_PERFORMANCE
        Stopwatch _moveNextSw = new Stopwatch();
        Stopwatch _currentSw = new Stopwatch();
        Stopwatch _valueGetSw = new Stopwatch();
        Stopwatch _valueSerdesSw = new Stopwatch();
        Stopwatch _valueBufferSw = new Stopwatch();
#endif
#if DEBUG_PERFORMANCE || VERIFY_WHERE_ENUMERATOR_STOPS
        int _cycles = 0;
#endif
        public KafkaEnumerator(IValueContainerMetadata metadata, IComplexTypeConverterFactory complexTypeConverterFactory, ISerDes<TKey, K> keySerdes, ISerDes<TValue, V> valueSerdes, Org.Apache.Kafka.Streams.State.KeyValueIterator<K, V>? keyValueIterator)
        {
            _metadata = metadata;
            _complexTypeConverterFactory = complexTypeConverterFactory;
            _keySerdes = keySerdes ?? throw new ArgumentNullException(nameof(keySerdes));
            _valueSerdes = valueSerdes ?? throw new ArgumentNullException(nameof(valueSerdes));
            _keyValueIterator = keyValueIterator ?? throw new ArgumentNullException(nameof(keyValueIterator));
#if DEBUG_PERFORMANCE
            KNet.Internal.DebugPerformanceHelper.ReportString($"Requested KafkaEnumerator for {_metadata.EntityType.Name} on {DateTime.Now:HH:mm:ss.FFFFFFF}");
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
            KNet.Internal.DebugPerformanceHelper.ReportString($"KafkaEnumerator _moveNextSw: {_moveNextSw.Elapsed} _currentSw: {_currentSw.Elapsed} _valueGetSw: {_valueGetSw.Elapsed} _valueSerdesSw: {_valueSerdesSw.Elapsed} _valueBufferSw: {_valueBufferSw.Elapsed}");
#endif
            _keyValueIterator?.Dispose();
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
                TValue? entityTypeData = default;
                do
                {
                    if (_keyValueIterator != null && _keyValueIterator.HasNext())
                    {
#if DEBUG_PERFORMANCE || VERIFY_WHERE_ENUMERATOR_STOPS
                        _cycles++;
#if DEBUG_PERFORMANCE
                        _valueGetSw.Start();
#endif
#endif
                        V? data;
                        using (KeyValue<K, V> kv = _keyValueIterator.Next())
                        {
                            var kvSupport = new MASES.KNet.Streams.KeyValueSupport<K, V>(kv);
                            data = kvSupport.Value != null ? (V)(object)kvSupport.Value! : default;
                        }
#if DEBUG_PERFORMANCE
                        _valueGetSw.Stop();
#endif
                        if (data == null) continue;
#if DEBUG_PERFORMANCE
                        _valueSerdesSw.Start();
#endif
                        entityTypeData = _valueSerdes.DeserializeWithHeaders(null, null, data!);
#if DEBUG_PERFORMANCE
                        _valueSerdesSw.Stop();
#endif
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
                    entityTypeData?.GetData(_metadata, ref propertyValues, _complexTypeConverterFactory);
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

        public void Reset()
        {
            throw new NotSupportedException(CoreStrings.EnumerableResetNotSupported);
        }
    }
}
