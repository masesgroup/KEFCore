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

// #define DEBUG_PERFORMANCE

#nullable enable

using Java.Util.Concurrent;
using MASES.EntityFrameworkCore.KNet.Serialization;
using MASES.JCOBridge.C2JBridge;
using MASES.KNet.Producer;
using MASES.KNet.Replicator;
using MASES.KNet.Serialization;
using Org.Apache.Kafka.Clients.Producer;
using System.Collections;

namespace MASES.EntityFrameworkCore.KNet.Storage.Internal;
/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public class EntityTypeProducer<TKey, TValueContainer, TJVMKey, TJVMValueContainer> : IEntityTypeProducer<TKey>
    where TKey : notnull
    where TValueContainer : class, IValueContainer<TKey>
{
    class EntityTypeProducerCallback(EntityTypeProducer<TKey, TValueContainer, TJVMKey, TJVMValueContainer> entityTypeProducer, Action<EntityTypeProducer<TKey, TValueContainer, TJVMKey, TJVMValueContainer>, int, long?, DateTime?, JVMBridgeException> result) : Callback
    {
        readonly EntityTypeProducer<TKey, TValueContainer, TJVMKey, TJVMValueContainer> _entityTypeProducer = entityTypeProducer;
        readonly Action<EntityTypeProducer<TKey, TValueContainer, TJVMKey, TJVMValueContainer>, int, long?, DateTime?, JVMBridgeException> _result = result;

        public override void OnCompletion(RecordMetadata arg0, JVMBridgeException arg1)
        {
            _result?.Invoke(_entityTypeProducer, arg0.Partition(), arg0.HasOffset() ? arg0.Offset() : null, arg0.HasTimestamp() ? System.DateTimeOffset.FromUnixTimeMilliseconds((long)arg0.Timestamp()).DateTime : null, arg1);
        }
    }

    private static IStreamsManager? _streamsManager;

    private readonly Func<IValueContainerData, IComplexTypeConverterFactory?, TValueContainer> _createValueContainer;
    private readonly bool _useCompactedReplicator;
    private readonly IKafkaCluster _cluster;
    private readonly IEntityType _entityType;
    private readonly IValueContainerMetadata _entityMetadata;
    private readonly IComplexTypeConverterFactory _complexTypeConverterFactory;
    private readonly IKey? _primaryKey;
    private readonly IPrincipalKeyValueFactory<TKey> _keyValueFactory;
    private readonly IKNetCompactedReplicator<TKey, TValueContainer, TJVMKey, TJVMValueContainer>? _kafkaCompactedReplicator;
    private readonly IProducer<TKey, TValueContainer, TJVMKey, TJVMValueContainer>? _kafkaProducer;
    private readonly IKafkaStreamsRetriever<TKey>? _streamData;
    private readonly ISerDes<TKey, TJVMKey>? _keySerdes;
    private readonly ISerDes<TValueContainer, TJVMValueContainer>? _valueSerdes;

    private readonly IUpdateAdapter? _updateAdapter;
    private readonly IEntityType? _entityTypeForChanges;
    private readonly IKey? _primaryKeyForChanges;

    private readonly EntityTypeProducerCallback? _producerCallback;

    #region KNetCompactedReplicatorEnumerable
    class KNetCompactedReplicatorEnumerable(IValueContainerMetadata entityMetadata, IComplexTypeConverterFactory complexTypeConverterFactory, IKNetCompactedReplicator<TKey, TValueContainer, TJVMKey, TJVMValueContainer>? kafkaCompactedReplicator) : IEnumerable<ValueBuffer>
    {
        readonly IValueContainerMetadata _entityMetadata = entityMetadata;
        readonly IComplexTypeConverterFactory _complexTypeConverterFactory = complexTypeConverterFactory;
        readonly IKNetCompactedReplicator<TKey, TValueContainer, TJVMKey, TJVMValueContainer>? _kafkaCompactedReplicator = kafkaCompactedReplicator;

        #region KNetCompactedReplicatorEnumerator
        class KNetCompactedReplicatorEnumerator : IEnumerator<ValueBuffer>
        {
#if DEBUG_PERFORMANCE
            Stopwatch _moveNextSw = new Stopwatch();
            Stopwatch _currentSw = new Stopwatch();
            Stopwatch _valueBufferSw = new Stopwatch();
#endif
            readonly IValueContainerMetadata _entityMetadata;
            readonly IComplexTypeConverterFactory _complexTypeConverterFactory;
            readonly IKNetCompactedReplicator<TKey, TValueContainer, TJVMKey, TJVMValueContainer>? _kafkaCompactedReplicator;
            readonly IEnumerator<KeyValuePair<TKey, TValueContainer>>? _enumerator;
            public KNetCompactedReplicatorEnumerator(IValueContainerMetadata entityMetadata, IComplexTypeConverterFactory complexTypeConverterFactory, IKNetCompactedReplicator<TKey, TValueContainer, TJVMKey, TJVMValueContainer>? kafkaCompactedReplicator)
            {
                _entityMetadata = entityMetadata;
                _complexTypeConverterFactory = complexTypeConverterFactory;
                _kafkaCompactedReplicator = kafkaCompactedReplicator;
#if DEBUG_PERFORMANCE
                Stopwatch sw = Stopwatch.StartNew();
#endif
                if (!_kafkaCompactedReplicator!.SyncWait()) throw new InvalidOperationException($"Failed to synchronize with {_kafkaCompactedReplicator.StateName}");
#if DEBUG_PERFORMANCE
                sw.Stop();
                KNet.Internal.DebugPerformanceHelper.ReportString($"KNetCompactedReplicatorEnumerator SyncWait for {_entityMetadata.EntityType.Name} tooks {sw.Elapsed}");
#endif
                _enumerator = _kafkaCompactedReplicator?.GetEnumerator();
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

            object IEnumerator.Current => Current;

            public void Dispose()
            {
#if DEBUG_PERFORMANCE
                KNet.Internal.DebugPerformanceHelper.ReportString($"KNetCompactedReplicatorEnumerator _moveNextSw: {_moveNextSw.Elapsed} _currentSw: {_currentSw.Elapsed} _valueBufferSw: {_valueBufferSw.Elapsed}");
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
                    if (_enumerator != null && _enumerator.MoveNext())
                    {
#if DEBUG_PERFORMANCE
                        _cycles++;
                        _valueBufferSw.Start();
#endif
                        object[] propertyValues = null!;
                        _enumerator.Current.Value.GetData(_entityMetadata, ref propertyValues, _complexTypeConverterFactory);
#if DEBUG_PERFORMANCE
                        _valueBufferSw.Stop();
#endif
                        _current = new ValueBuffer(propertyValues);
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
                        throw new InvalidOperationException($"KNetCompactedReplicatorEnumerator - No data returned from {_kafkaCompactedReplicator}");
                    }
                }
#endif
            }

            public void Reset()
            {
                _enumerator?.Reset();
            }
        }

        #endregion

        public IEnumerator<ValueBuffer> GetEnumerator()
        {
            return new KNetCompactedReplicatorEnumerator(_entityMetadata, _complexTypeConverterFactory, _kafkaCompactedReplicator);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
    #endregion
    /// <summary>
    /// Default initializer
    /// </summary>
    public EntityTypeProducer(IEntityType entityType, IKafkaCluster cluster)
    {
#if DEBUG_PERFORMANCE
        KNet.Internal.DebugPerformanceHelper.ReportString($"Creating new EntityTypeProducer for {entityType.Name}");
#endif
        _entityType = entityType;
        _primaryKey = entityType.FindPrimaryKey();
        _keyValueFactory = _primaryKey!.GetPrincipalKeyValueFactory<TKey>();
        _entityMetadata = new ValueContainerMetadata(_entityType,
                                                     [.. _entityType.GetProperties()],
                                                     [.. _entityType.GetFlattenedProperties()],
                                                     [.. _entityType.GetComplexProperties()]);
        _complexTypeConverterFactory = cluster.ComplexTypeConverterFactory;
        _cluster = cluster;
        _useCompactedReplicator = _cluster.Options.UseCompactedReplicator;

        var tTValueContainer = typeof(TValueContainer);
        var ctor = tTValueContainer.GetConstructors().Single(ci => ci.GetParameters().Length == 2);
        var param1 = Expression.Parameter(typeof(IValueContainerData));
        var param2 = Expression.Parameter(typeof(IComplexTypeConverterFactory));

        _createValueContainer = Expression.Lambda<Func<IValueContainerData, IComplexTypeConverterFactory?, TValueContainer>>(
                                           Expression.New(ctor, param1, param2),
                                           param1, param2)
                                .Compile();

        var keySelector = _cluster.Options.SerDesSelectorForKey(_entityType) as ISerDesSelector<TKey>;
        var valueSelector = _cluster.Options.SerDesSelectorForValue(_entityType) as ISerDesSelector<TValueContainer>;

        _keySerdes = keySelector?.NewSerDes<TJVMKey>();
        _valueSerdes = valueSelector?.NewSerDes<TJVMValueContainer>();

        if (_keySerdes == null) throw new InvalidOperationException($"SerDsSelector for key does not returned a {typeof(ISerDes<TKey, TJVMKey>)}");
        if (_valueSerdes == null) throw new InvalidOperationException($"SerDsSelector for value does not returned a {typeof(ISerDes<TValueContainer, TJVMValueContainer>)}");

        if (_useCompactedReplicator)
        {
            _kafkaCompactedReplicator = new KNetCompactedReplicator<TKey, TValueContainer, TJVMKey, TJVMValueContainer>()
            {
                UpdateMode = UpdateModeTypes.OnConsume,
                BootstrapServers = _cluster.Options.BootstrapServers,
                StateName = _entityType.TopicName(_cluster.Options),
                Partitions = _entityType.NumPartitions(_cluster.Options),
                ConsumerInstances = _entityType.ConsumerInstances(_cluster.Options),
                ReplicationFactor = _entityType.ReplicationFactor(_cluster.Options),
                ConsumerConfig = _cluster.Options.ConsumerConfig,
                TopicConfig = _cluster.Options.TopicConfig,
                ProducerConfig = _cluster.Options.ProducerConfig,
                KeySerDes = _keySerdes,
                ValueSerDes = _valueSerdes,
            };
            if (_cluster.Options.ManageEvents)
            {
                _updateAdapter = _cluster.UpdateAdapterFactory.Create();
                _entityTypeForChanges = _updateAdapter.Model.FindEntityType(_entityType.ClrType)!;
                _primaryKeyForChanges = _entityTypeForChanges.FindPrimaryKey()!;
                _kafkaCompactedReplicator.OnRemoteAdd += KafkaCompactedReplicator_OnRemoteAdd;
                _kafkaCompactedReplicator.OnRemoteUpdate += KafkaCompactedReplicator_OnRemoteUpdate;
                _kafkaCompactedReplicator.OnRemoteRemove += KafkaCompactedReplicator_OnRemoteRemove;
            }
        }
        else
        {
            _streamsManager ??= (_cluster.Options.UseKNetStreams ? KNetStreamsRetriever<TKey, TValueContainer, TJVMKey, TJVMValueContainer>.Create(cluster, entityType)
                                                                 : KafkaStreamsTableRetriever<TKey, TValueContainer, TJVMKey, TJVMValueContainer>.Create(cluster, entityType));

            _producerCallback = new EntityTypeProducerCallback(this, UpdateFromCommit);
            _kafkaProducer = new KNetProducer<TKey, TValueContainer, TJVMKey, TJVMValueContainer>(_cluster.Options.ProducerOptionsBuilder(), _keySerdes, _valueSerdes);
            _kafkaProducer.SetCallback(_producerCallback);
            _streamData = _cluster.Options.UseKNetStreams ? new KNetStreamsRetriever<TKey, TValueContainer, TJVMKey, TJVMValueContainer>(_cluster, _entityMetadata, _primaryKey, _complexTypeConverterFactory)
                                                          : new KafkaStreamsTableRetriever<TKey, TValueContainer, TJVMKey, TJVMValueContainer>(_cluster, _entityMetadata, _primaryKey, _complexTypeConverterFactory, _keySerdes!, _valueSerdes!);
        }
    }

    /// <inheritdoc/>
    public bool Exist(TKey key)
    {
        if (_streamData != null) return _streamData.Exist(key);
        else if (_kafkaCompactedReplicator != null) return _kafkaCompactedReplicator.ContainsKey(key);
        else throw new InvalidOperationException("Missing _kafkaCompactedReplicator or _streamData");
    }
    /// <inheritdoc/>
    public bool TryGetValueBuffer(TKey key, out ValueBuffer valueBuffer)
    {
        if (_streamData != null)
        {
            return _streamData.TryGetValue(key, out valueBuffer);
        }
        else if (_kafkaCompactedReplicator != null)
        {
            if (_kafkaCompactedReplicator.TryGetValue(key, out var valueContainer))
            {
                object[] propertyValues = null!;
                valueContainer?.GetData(_entityMetadata, ref propertyValues, _complexTypeConverterFactory);
                valueBuffer = new ValueBuffer(propertyValues);
                return true;
            }

            valueBuffer = default;
            return false;
        }
        else throw new InvalidOperationException("Missing _kafkaCompactedReplicator or _streamData");
    }
    /// <inheritdoc/>
    public void TryAddKey(object[] keyValues)
    {
        if (keyValues == null) return;
        TKey? key = (TKey)_keyValueFactory.CreateFromKeyValues(keyValues)!;
        if (key == null) return;

        if (_streamData != null)
        {
            if (_streamData.TryGetProperties(key, out var properties, out var complexProperties))
            {
                KafkaStateHelper.ManageFind(_cluster.InfrastructureLogger, _cluster.UpdateAdapterFactory, _entityType, _primaryKey!, keyValues, properties, complexProperties);
            }
            return;
        }
        else if (_kafkaCompactedReplicator != null)
        {
            if (_kafkaCompactedReplicator.TryGetValue(key, out var valueContainer))
            {
                IDictionary<string, object?>? properties = valueContainer?.GetProperties(_complexTypeConverterFactory)!;
                IDictionary<string, object?>? complexProperties = valueContainer?.GetComplexProperties(_complexTypeConverterFactory)!;
                KafkaStateHelper.ManageFind(_cluster.InfrastructureLogger, _cluster.UpdateAdapterFactory, _entityType, _primaryKey!, keyValues, properties, complexProperties);
            }
            return;
        }
        else throw new InvalidOperationException("Missing _kafkaCompactedReplicator or _streamData");
    }

    /// <inheritdoc/>
    public bool TryGetProperties(TKey key, out IDictionary<string, object?> properties, out IDictionary<string, object?> complexProperties)
    {
        if (_streamData != null)
        {
            return _streamData.TryGetProperties(key, out properties, out complexProperties);
        }
        else if (_kafkaCompactedReplicator != null)
        {
            if (_kafkaCompactedReplicator.TryGetValue(key, out var valueContainer))
            {
                properties = valueContainer?.GetProperties(_complexTypeConverterFactory)!;
                complexProperties = valueContainer?.GetComplexProperties(_complexTypeConverterFactory)!;
                return true;
            }

            properties = default!;
            complexProperties = default!;
            return false;
        }

        throw new InvalidOperationException("Missing _kafkaCompactedReplicator or _streamData");
    }

    /// <inheritdoc/>
    public virtual IEntityType EntityType => _entityType;
    /// <inheritdoc/>
    public void Commit(IList<Future<RecordMetadata>>? futures, IEnumerable<IKafkaRowBag> records)
    {
        if (_useCompactedReplicator)
        {
            foreach (var record in records)
            {
                var value = record.GetValue<TKey, TValueContainer>(_createValueContainer, _complexTypeConverterFactory);
                _kafkaCompactedReplicator?[record.GetKey<TKey>()] = value!;
            }
        }
        else
        {
            foreach (var record in records)
            {
                Future<RecordMetadata> future;
#if OLD_WAY
                var newRecord = _kafkaProducer?.NewRecord(record.AssociatedTopicName, 0, record.Key, record.Value(TValueContainerConstructor)!);
                future = _kafkaProducer?.Send(newRecord);
                futures.Add(future!);
#else
                if (record.EntityState == EntityState.Deleted)
                {
                    future = _kafkaProducer?.Send(record.AssociatedTopicName, record.GetKey<TKey>(), null!)!;
                    futures?.Add(future);
                }
                else
                {
                    Org.Apache.Kafka.Common.Header.Headers headers = null!;
                    if (_keySerdes!.UseHeaders || _valueSerdes!.UseHeaders)
                    {
                        headers = Org.Apache.Kafka.Common.Header.Headers.Create();
                    }
                    future = _kafkaProducer?.Send(record.AssociatedTopicName, null, record.GetKey<TKey>(), record.GetValue<TKey, TValueContainer>(_createValueContainer, _complexTypeConverterFactory)!, headers)!;
                    futures?.Add(future);
                }
#endif
            }

            _kafkaProducer?.Flush();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_kafkaCompactedReplicator != null)
        {
            if (_cluster.Options.ManageEvents)
            {
                _kafkaCompactedReplicator.OnRemoteAdd -= KafkaCompactedReplicator_OnRemoteAdd;
                _kafkaCompactedReplicator.OnRemoteUpdate -= KafkaCompactedReplicator_OnRemoteUpdate;
                _kafkaCompactedReplicator.OnRemoteRemove -= KafkaCompactedReplicator_OnRemoteRemove;
            }
            _kafkaCompactedReplicator?.Dispose();
        }
        else
        {
            _kafkaProducer?.SetCallback(null);
            _producerCallback?.Dispose();
            _kafkaProducer?.Dispose();
            _streamData?.Dispose();
        }
        _keySerdes?.Dispose();
        _valueSerdes?.Dispose();
    }
    /// <inheritdoc/>
    public IEnumerable<ValueBuffer> ValueBuffers
    {
        get
        {
            if (_streamData != null) return _streamData.GetValueBuffers();
            else if (_kafkaCompactedReplicator != null) return new KNetCompactedReplicatorEnumerable(_entityMetadata, _complexTypeConverterFactory, _kafkaCompactedReplicator);
            else throw new InvalidOperationException("Missing _kafkaCompactedReplicator or _streamData");
        }
    }
    /// <inheritdoc/>
    public void Start()
    {
        if (_streamData != null) _streamsManager!.CreateAndStartTopology();
        else if (_kafkaCompactedReplicator != null)
        {
#if DEBUG_PERFORMANCE
            Stopwatch sw = Stopwatch.StartNew();
#endif
            _kafkaCompactedReplicator.Start();
#if DEBUG_PERFORMANCE
            sw.Stop();
            KNet.Internal.DebugPerformanceHelper.ReportString($"EntityTypeProducer - KNetCompactedReplicator::StartAndWait for {_entityType.Name} in {sw.Elapsed}");
#endif
        }
        else throw new InvalidOperationException("Missing _kafkaCompactedReplicator or _streamData");
    }

    private static void UpdateFromCommit(EntityTypeProducer<TKey, TValueContainer, TJVMKey, TJVMValueContainer> producer, int partiton, long? offset, DateTime? timestamp, JVMBridgeException error)
    {
        if (offset.HasValue) _streamsManager!.PartitionOffsetWritten(producer.EntityType, partiton, offset.Value);
    }

    /// <summary>
    /// Verify if local instance is synchronized with the <see cref="IKafkaCluster"/> instance
    /// </summary>
    public bool? EnsureSynchronized(long timeout)
    {
        if (_streamData != null) return _streamsManager!.EnsureSynchronized(_entityType, timeout);
        else if (_kafkaCompactedReplicator != null)
        {
#if DEBUG_PERFORMANCE
            Stopwatch sw = null!;
            try
            {
                sw = Stopwatch.StartNew();
#endif
                return _kafkaCompactedReplicator.SyncWait((int)timeout);
#if DEBUG_PERFORMANCE
            }
            finally
            {
                sw?.Stop();
                KNet.Internal.DebugPerformanceHelper.ReportString($"EntityTypeProducer - KNetCompactedReplicator::SyncWait for {_entityType.Name} in {sw?.Elapsed}");
            }
#endif
        }
        else throw new InvalidOperationException("Missing _kafkaCompactedReplicator or _streamData");
    }

    private void KafkaCompactedReplicator_OnRemoteAdd(IKNetCompactedReplicator<TKey, TValueContainer, TJVMKey, TJVMValueContainer> arg1, KeyValuePair<TKey, TValueContainer> arg2)
    {
        KafkaStateHelper.ManageAdded(_cluster.InfrastructureLogger, _cluster.ValueGeneratorSelector, _cluster.ComplexTypeConverterFactory, _updateAdapter!, _entityTypeForChanges!, _primaryKeyForChanges!, arg2.Key, arg2.Value);
    }

    private void KafkaCompactedReplicator_OnRemoteUpdate(IKNetCompactedReplicator<TKey, TValueContainer, TJVMKey, TJVMValueContainer> arg1, KeyValuePair<TKey, TValueContainer> arg2)
    {
        KafkaStateHelper.ManageUpdate(_cluster.InfrastructureLogger, _cluster.ValueGeneratorSelector, _cluster.ComplexTypeConverterFactory, _updateAdapter!, _entityTypeForChanges!, _primaryKeyForChanges!, arg2.Key, arg2.Value);
    }

    private void KafkaCompactedReplicator_OnRemoteRemove(IKNetCompactedReplicator<TKey, TValueContainer, TJVMKey, TJVMValueContainer> arg1, KeyValuePair<TKey, TValueContainer> arg2)
    {
        KafkaStateHelper.ManageDelete(_cluster.InfrastructureLogger, _updateAdapter!, _primaryKeyForChanges!, arg2.Key);
    }
}
