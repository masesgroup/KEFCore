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
using MASES.EntityFrameworkCore.KNet.Extensions;
using MASES.EntityFrameworkCore.KNet.Serialization;
using MASES.JCOBridge.C2JBridge;
using MASES.KNet.Producer;
using MASES.KNet.Replicator;
using MASES.KNet.Serialization;
using Org.Apache.Kafka.Clients.Producer;
using System.Collections;
using System.Collections.Concurrent;

namespace MASES.EntityFrameworkCore.KNet.Storage.Internal;

readonly struct KEFCoreDatabaseLocalData
{
    public KEFCoreDatabaseLocalData(IKEFCoreDatabase database, IEntityType entityType)
    {
        Database = database;
        ManageEvents = entityType.GetManageEvents();
        if (ManageEvents)
        {
            UpdateAdapter = database.UpdateAdapterFactory.Create();
            var entityType1 = UpdateAdapter.Model.FindEntityType(entityType.ClrType)!;
            MetadataForChanges = new ValueContainerMetadata(entityType1);
            PrimaryKeyForChanges = entityType1.FindPrimaryKey()!;
        }
    }
    public readonly IKEFCoreDatabase Database;
    public readonly bool ManageEvents;
    public readonly IUpdateAdapter? UpdateAdapter;
    public readonly IValueContainerMetadata? MetadataForChanges;
    public readonly IKey? PrimaryKeyForChanges;
}

/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public class EntityTypeProducer<TKey, TValueContainer, TJVMKey, TJVMValueContainer> : IEntityTypeProducer<TKey>, ITransactionalEntityTypeProducer
    where TKey : notnull
    where TValueContainer : class, IValueContainer<TKey>
{
    #region EntityTypeProducerCallback

    class EntityTypeProducerCallback(EntityTypeProducer<TKey, TValueContainer, TJVMKey, TJVMValueContainer> entityTypeProducer, Action<EntityTypeProducer<TKey, TValueContainer, TJVMKey, TJVMValueContainer>, int, long?, DateTime?, JVMBridgeException> result) : Callback
    {
        readonly EntityTypeProducer<TKey, TValueContainer, TJVMKey, TJVMValueContainer> _entityTypeProducer = entityTypeProducer;
        readonly Action<EntityTypeProducer<TKey, TValueContainer, TJVMKey, TJVMValueContainer>, int, long?, DateTime?, JVMBridgeException> _result = result;

        public override void OnCompletion(RecordMetadata arg0, JVMBridgeException arg1)
        {
            _result?.Invoke(_entityTypeProducer, arg0.Partition(), arg0.HasOffset() ? arg0.Offset() : null, arg0.HasTimestamp() ? System.DateTimeOffset.FromUnixTimeMilliseconds((long)arg0.Timestamp()).DateTime : null, arg1);
        }
    }

    #endregion

    #region TransactionalCallback

    class TransactionalCallback(ConcurrentQueue<(string topicName, int partition, long offset, JVMBridgeException? exception)> pendingOffsets) : Callback
    {
        public override void OnCompletion(RecordMetadata metadata, JVMBridgeException error)
        {
            pendingOffsets.Enqueue((metadata.Topic(), metadata.Partition(), metadata.Offset(), error));
        }
    }

    #endregion

    // KEFCoreCachedValueBufferStore<TKey> lives in its own file:
    // Storage/Internal/KEFCoreCachedValueBufferStore.cs

    private readonly IStreamsManager? _streamsManager;

    private readonly Func<IValueContainerData, IComplexTypeConverterFactory?, TValueContainer> _createValueContainer;
    private readonly bool _useCompactedReplicator;
    private readonly IKEFCoreDatabase _database;
    private readonly IEntityType _entityType;
    private readonly IValueContainerMetadata _entityMetadata;
    private readonly IComplexTypeConverterFactory _complexTypeConverterFactory;
    private readonly IKey? _primaryKey;
    private readonly IPrincipalKeyValueFactory<TKey> _keyValueFactory;
    private readonly IKNetCompactedReplicator<TKey, TValueContainer, TJVMKey, TJVMValueContainer>? _knetCompactedReplicator;
    private readonly IProducer<TKey, TValueContainer, TJVMKey, TJVMValueContainer>? _kafkaProducer;
    private readonly IKEFCoreStreamsRetriever<TKey, TValueContainer>? _streamData;
    private readonly ISerDes<TKey, TJVMKey>? _keySerdes;
    private readonly ISerDes<TValueContainer, TJVMValueContainer>? _valueSerdes;
    /// <summary>Forward cache — populated by GetValueBuffers(), serves range and single key.</summary>
    private readonly KEFCoreCachedValueBufferStore<TKey> _forwardCache;
    /// <summary>Reverse cache — populated by GetValueBuffersReverse(), serves reverse range.</summary>
    private readonly KEFCoreCachedValueBufferStore<TKey> _reverseCache;

    readonly ConcurrentDictionary<IKEFCoreDatabase, KEFCoreDatabaseLocalData> _updaters = new();
    private readonly EntityTypeProducerCallback? _producerCallback;

    private readonly IProducer? _transactionalProducer;
    private readonly string? _transactionGroup;
    private readonly ConcurrentQueue<(string topicName, int partition, long offset, JVMBridgeException? exception)> _pendingOffsets = new();
    private readonly TransactionalCallback? _transactionalCallback;

    #region KNetCompactedReplicatorEnumerable
    class KNetCompactedReplicatorEnumerable(IValueContainerMetadata entityMetadata, IComplexTypeConverterFactory complexTypeConverterFactory, IKNetCompactedReplicator<TKey, TValueContainer, TJVMKey, TJVMValueContainer>? knetCompactedReplicator) : IEnumerable<ValueBuffer>
    {
        readonly IValueContainerMetadata _entityMetadata = entityMetadata;
        readonly IComplexTypeConverterFactory _complexTypeConverterFactory = complexTypeConverterFactory;
        readonly IKNetCompactedReplicator<TKey, TValueContainer, TJVMKey, TJVMValueContainer>? _knetCompactedReplicator = knetCompactedReplicator;

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
            readonly IKNetCompactedReplicator<TKey, TValueContainer, TJVMKey, TJVMValueContainer>? _knetCompactedReplicator;
            readonly IEnumerator<KeyValuePair<TKey, TValueContainer>>? _enumerator;
            public KNetCompactedReplicatorEnumerator(IValueContainerMetadata entityMetadata, IComplexTypeConverterFactory complexTypeConverterFactory, IKNetCompactedReplicator<TKey, TValueContainer, TJVMKey, TJVMValueContainer>? knetCompactedReplicator)
            {
                _entityMetadata = entityMetadata;
                _complexTypeConverterFactory = complexTypeConverterFactory;
                _knetCompactedReplicator = knetCompactedReplicator;
#if DEBUG_PERFORMANCE
                Stopwatch sw = Stopwatch.StartNew();
#endif
                if (!_knetCompactedReplicator!.SyncWait()) throw new InvalidOperationException($"Failed to synchronize with {_knetCompactedReplicator.StateName}");
#if DEBUG_PERFORMANCE
                sw.Stop();
                KNet.Internal.DebugPerformanceHelper.ReportString($"KNetCompactedReplicatorEnumerator SyncWait for {_entityMetadata.EntityType.Name} tooks {sw.Elapsed}");
#endif
                _enumerator = _knetCompactedReplicator?.GetEnumerator();
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
                        throw new InvalidOperationException($"KNetCompactedReplicatorEnumerator - No data returned from {_knetCompactedReplicator}");
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
            return new KNetCompactedReplicatorEnumerator(_entityMetadata, _complexTypeConverterFactory, _knetCompactedReplicator);
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
    public EntityTypeProducer(IKEFCoreDatabase database, IEntityType entityType)
    {
#if DEBUG_PERFORMANCE
        KNet.Internal.DebugPerformanceHelper.ReportString($"Creating new EntityTypeProducer for {entityType.Name}");
#endif
        _database = database;
        _entityType = entityType;
        _primaryKey = entityType.FindPrimaryKey();
        _keyValueFactory = _primaryKey!.GetPrincipalKeyValueFactory<TKey>();
        _entityMetadata = new ValueContainerMetadata(_entityType,
                                                     [.. _entityType.GetProperties()],
                                                     [.. _entityType.GetFlattenedProperties()],
                                                     [.. _entityType.GetComplexProperties()]);
        _complexTypeConverterFactory = _database.Cluster.ComplexTypeConverterFactory;
        _useCompactedReplicator = _database.Options.UseCompactedReplicator;

        var tTValueContainer = typeof(TValueContainer);
        var ctor = tTValueContainer.GetConstructors().Single(ci => ci.GetParameters().Length == 2);
        var param1 = Expression.Parameter(typeof(IValueContainerData));
        var param2 = Expression.Parameter(typeof(IComplexTypeConverterFactory));

        _createValueContainer = Expression.Lambda<Func<IValueContainerData, IComplexTypeConverterFactory?, TValueContainer>>(
                                           Expression.New(ctor, param1, param2),
                                           param1, param2)
                                .Compile();

        var keySelector = _database.Options.SerDesSelectorForKey(_entityType) as ISerDesSelector<TKey>;
        var valueSelector = _database.Options.SerDesSelectorForValue(_entityType) as ISerDesSelector<TValueContainer>;

        _keySerdes = keySelector?.NewSerDes<TJVMKey>();
        _valueSerdes = valueSelector?.NewSerDes<TJVMValueContainer>();

        if (_keySerdes == null) throw new InvalidOperationException($"SerDsSelector for key does not returned a {typeof(ISerDes<TKey, TJVMKey>)}");
        if (_valueSerdes == null) throw new InvalidOperationException($"SerDsSelector for value does not returned a {typeof(ISerDes<TValueContainer, TJVMValueContainer>)}");

        if (_useCompactedReplicator)
        {
            _knetCompactedReplicator = new KNetCompactedReplicator<TKey, TValueContainer, TJVMKey, TJVMValueContainer>()
            {
                UpdateMode = UpdateModeTypes.OnConsume,
                BootstrapServers = _database.Options.BootstrapServers,
                StateName = _entityType.GetKEFCoreTopicName(),
                Partitions = _entityType.NumPartitions(_database.Options),
                ConsumerInstances = _entityType.ConsumerInstances(_database.Options),
                ReplicationFactor = _entityType.ReplicationFactor(_database.Options),
                ConsumerConfig = entityType.BuildConsumerConfig(_database.Options),
                TopicConfig = _entityType.BuildTopicConfig(_database.Options),
                ProducerConfig = entityType.BuildProducerConfig(_database.Options),
                KeySerDes = _keySerdes,
                ValueSerDes = _valueSerdes,
            };
            _knetCompactedReplicator.OnRemoteAdd += KNetCompactedReplicator_OnRemoteAdd;
            _knetCompactedReplicator.OnRemoteUpdate += KNetCompactedReplicator_OnRemoteUpdate;
            _knetCompactedReplicator.OnRemoteRemove += KNetCompactedReplicator_OnRemoteRemove;
        }
        else
        {
            _streamsManager = database.Cluster.GetStreamsManager(database, (db) =>
            {
                return db.Options.UseKNetStreams ? KNetStreamsRetriever<TKey, TValueContainer, TJVMKey, TJVMValueContainer>.Create(db.Cluster, db.Options)
                                                 : KafkaStreamsRetriever<TKey, TValueContainer, TJVMKey, TJVMValueContainer>.Create(db.Cluster, db.Options);
            });

            _transactionGroup = _entityType.GetTransactionGroup();
            if (_transactionGroup != null)
            {
                _transactionalProducer = _database.Cluster.GetOrCreateTransactionalProducer(_transactionGroup, this);
                _transactionalCallback = new TransactionalCallback(_pendingOffsets);
            }
            else
            {
                _producerCallback = new EntityTypeProducerCallback(this, UpdateFromCommit);
                _kafkaProducer = new KNetProducer<TKey, TValueContainer, TJVMKey, TJVMValueContainer>(entityType.BuildProducerConfig(_database.Options), _keySerdes, _valueSerdes);
                _kafkaProducer.SetCallback(_producerCallback);
            }
            _streamData = _database.Options.UseKNetStreams ? new KNetStreamsRetriever<TKey, TValueContainer, TJVMKey, TJVMValueContainer>(this, _entityMetadata, _complexTypeConverterFactory)
                                                           : new KafkaStreamsRetriever<TKey, TValueContainer, TJVMKey, TJVMValueContainer>(this, _entityMetadata, _complexTypeConverterFactory, _keySerdes!, _valueSerdes!);

            _forwardCache = new KEFCoreCachedValueBufferStore<TKey>(_primaryKey!, _keyValueFactory, _entityType.GetValueBufferCacheTtl());
            _reverseCache = new KEFCoreCachedValueBufferStore<TKey>(_primaryKey!, _keyValueFactory, _entityType.GetValueBufferReverseCacheTtl());
        }
    }

    /// <inheritdoc/>
    public bool Exist(TKey key)
    {
        if (_streamData != null) return _streamData.Exist(key);
        else if (_knetCompactedReplicator != null) return _knetCompactedReplicator.ContainsKey(key);
        else throw new InvalidOperationException("Missing _knetCompactedReplicator or _streamData");
    }
    /// <inheritdoc/>
    public bool TryGetValueBuffer(TKey key, out ValueBuffer valueBuffer)
    {
        if (_streamData != null)
        {
            return _streamData.TryGetValue(key, out valueBuffer);
        }
        else if (_knetCompactedReplicator != null)
        {
            if (_knetCompactedReplicator.TryGetValue(key, out var valueContainer))
            {
                object[] propertyValues = null!;
                valueContainer?.GetData(_entityMetadata, ref propertyValues, _complexTypeConverterFactory);
                valueBuffer = new ValueBuffer(propertyValues);
                return true;
            }

            valueBuffer = default;
            return false;
        }
        else throw new InvalidOperationException("Missing _knetCompactedReplicator or _streamData");
    }
    /// <inheritdoc/>
    public void TryAddKey(object[] keyValues)
    {
        if (keyValues == null) return;
        TKey? key = (TKey)_keyValueFactory.CreateFromKeyValues(keyValues)!;
        if (key == null) return;

        if (_streamData != null)
        {
            if (_streamData.TryGetProperties(key, out var valueContainer))
            {
                KEFCoreStateHelper.ManageFind(_database.InfrastructureLogger, _database.UpdateAdapterFactory, _entityMetadata, _primaryKey!, keyValues, valueContainer.GetProperties(_entityMetadata), valueContainer.GetComplexProperties(_entityMetadata, _complexTypeConverterFactory));
            }
            return;
        }
        else if (_knetCompactedReplicator != null)
        {
            if (_knetCompactedReplicator.TryGetValue(key, out var valueContainer))
            {
                IDictionary<string, object?>? properties = valueContainer?.GetProperties(_entityMetadata)!;
                IDictionary<string, object?>? complexProperties = valueContainer?.GetComplexProperties(_entityMetadata, _complexTypeConverterFactory)!;
                KEFCoreStateHelper.ManageFind(_database.InfrastructureLogger, _database.UpdateAdapterFactory, _entityMetadata, _primaryKey!, keyValues, properties, complexProperties);
            }
            return;
        }
        else throw new InvalidOperationException("Missing _knetCompactedReplicator or _streamData");
    }

    /// <inheritdoc/>
    public bool TryGetProperties(TKey key, out TValueContainer valueContainer)
    {
        if (_streamData != null)
        {
            return _streamData.TryGetProperties(key, out valueContainer);
        }
        else if (_knetCompactedReplicator != null)
        {
            return _knetCompactedReplicator.TryGetValue(key, out valueContainer!);
        }

        throw new InvalidOperationException("Missing _knetCompactedReplicator or _streamData");
    }

    /// <inheritdoc/>
    public virtual IEntityType EntityType => _entityType;
    /// <inheritdoc/>
    public virtual void Register(IKEFCoreDatabase database)
    {
        if (_useCompactedReplicator)
        {
            if (!_updaters.TryAdd(database, new KEFCoreDatabaseLocalData(database, _entityType)))
            {
                database.InfrastructureLogger.Logger.LogError("EntityTypeProducer: Failed to register database twice");
            }
        }
        else
        {
            _streamsManager?.Register(this, database, _entityType);
        }
    }
    /// <inheritdoc/>
    public virtual void Unregister(IKEFCoreDatabase database)
    {
        if (_useCompactedReplicator)
        {
            if (!_updaters.TryRemove(database, out _))
            {
                database.InfrastructureLogger.Logger.LogError("EntityTypeProducer: Failed to unregister database");
            }
        }
        else if (_streamsManager != null)
        {
            _streamsManager.Unregister(this, database);
        }
    }
    /// <inheritdoc/>
    public void Commit(IList<Future<RecordMetadata>>? futures, IEnumerable<IKEFCoreRowBag> records)
    {
        if (_useCompactedReplicator)
        {
            foreach (var record in records)
            {
                var value = record.GetValue<TKey, TValueContainer>(_createValueContainer, _complexTypeConverterFactory);
                _knetCompactedReplicator?[record.GetKey<TKey>()] = value!;
            }
        }
        else if (_transactionalProducer != null)
        {
            var txProducer = (IProducer<TJVMKey, TJVMValueContainer>)_transactionalProducer;
            foreach (var record in records)
            {
                var topicName = record.AssociatedTopicName;
                if (record.EntityState == EntityState.Deleted)
                {
                    Org.Apache.Kafka.Common.Header.Headers headers = null!;
                    if (_keySerdes!.UseHeaders)
                    {
                        headers = Org.Apache.Kafka.Common.Header.Headers.Create();
                    }
                    var jvmKey = _keySerdes!.SerializeWithHeaders(topicName, headers, record.GetKey<TKey>());
                    var kRecord = new Org.Apache.Kafka.Clients.Producer.ProducerRecord<TJVMKey, TJVMValueContainer>(topicName, null, jvmKey, default!, headers);
                    var future = txProducer.Send(kRecord, _transactionalCallback);
                    futures?.Add(future);
                }
                else
                {
                    Org.Apache.Kafka.Common.Header.Headers headers = null!;
                    if (_keySerdes!.UseHeaders || _valueSerdes!.UseHeaders)
                    {
                        headers = Org.Apache.Kafka.Common.Header.Headers.Create();
                    }
                    var jvmKey = _keySerdes!.SerializeWithHeaders(topicName, headers, record.GetKey<TKey>());
                    var jvmValue = _valueSerdes!.SerializeWithHeaders(topicName, headers, record.GetValue<TKey, TValueContainer>(_createValueContainer, _complexTypeConverterFactory)!);
                    var kRecord = new Org.Apache.Kafka.Clients.Producer.ProducerRecord<TJVMKey, TJVMValueContainer>(topicName, null, jvmKey, jvmValue, headers);
                    var future = txProducer.Send(kRecord, _transactionalCallback);
                    futures?.Add(future);
                }
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
#else
                if (record.EntityState == EntityState.Deleted)
                {
                    Org.Apache.Kafka.Common.Header.Headers headers = null!;
                    if (_keySerdes!.UseHeaders)
                    {
                        headers = Org.Apache.Kafka.Common.Header.Headers.Create();
                    }
                    future = _kafkaProducer?.Send(record.AssociatedTopicName, null, record.GetKey<TKey>(), null!, headers)!;
                }
                else
                {
                    Org.Apache.Kafka.Common.Header.Headers headers = null!;
                    if (_keySerdes!.UseHeaders || _valueSerdes!.UseHeaders)
                    {
                        headers = Org.Apache.Kafka.Common.Header.Headers.Create();
                    }
                    future = _kafkaProducer?.Send(record.AssociatedTopicName, null, record.GetKey<TKey>(), record.GetValue<TKey, TValueContainer>(_createValueContainer, _complexTypeConverterFactory)!, headers)!;
                }
#endif
                futures?.Add(future);
            }

            _kafkaProducer?.Flush();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_knetCompactedReplicator != null)
        {
            _knetCompactedReplicator.OnRemoteAdd -= KNetCompactedReplicator_OnRemoteAdd;
            _knetCompactedReplicator.OnRemoteUpdate -= KNetCompactedReplicator_OnRemoteUpdate;
            _knetCompactedReplicator.OnRemoteRemove -= KNetCompactedReplicator_OnRemoteRemove;
            _knetCompactedReplicator?.Dispose();
        }
        else if (_transactionalProducer != null)
        {
            _transactionalCallback?.Dispose();
            _pendingOffsets.Clear();
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
    public IEnumerable<ValueBuffer> GetValueBuffers(IKEFCoreDatabase database)
    {
        if (_streamData != null)
            return _forwardCache.GetAllPopulating(() => _streamData.GetValueBuffers(_database));
        else if (_knetCompactedReplicator != null) return new KNetCompactedReplicatorEnumerable(_entityMetadata, _complexTypeConverterFactory, _knetCompactedReplicator);
        else throw new InvalidOperationException("Missing _knetCompactedReplicator or _streamData");
    }
    /// <inheritdoc/>
    public ValueBuffer? GetValueBuffer(IKEFCoreDatabase database, object?[]? keyValues)
    {
        if (keyValues == null) return null;
        if (_streamData != null)
        {
            if (_forwardCache.IsWarm)
            {
                // construct key only when cache is actually warm — avoids non-trivial allocation
                TKey key = (TKey)_keyValueFactory.CreateFromKeyValues(keyValues)!;
                if (_forwardCache.TryGetValue(key, out var cached)) return cached;
                // cache warm but miss — fall through to store with the already-constructed key
                return _streamData.TryGetValue(key, out var vbMiss) ? vbMiss : null;
            }
            // cache cold or disabled — go directly to store
            TKey k = (TKey)_keyValueFactory.CreateFromKeyValues(keyValues)!;
            return _streamData.TryGetValue(k, out var vb) ? vb : null;
        }
        else if (_knetCompactedReplicator != null)
        {
            return TryGetValueBuffer((TKey)_keyValueFactory.CreateFromKeyValues(keyValues)!, out var buffer) ? buffer : null;
        }
        else throw new InvalidOperationException("Missing _knetCompactedReplicator or _streamData");
    }
    /// <inheritdoc/>
    public IEnumerable<ValueBuffer> GetValueBuffersRange(IKEFCoreDatabase database, object?[]? rangeStart, object?[]? rangeEnd)
    {
        if (_streamData != null)
        {
            // guard: build keys only if forward cache is warm — avoids non-trivial allocation
            if (rangeStart != null && rangeEnd != null && _forwardCache.IsWarm)
            {
                TKey from = (TKey)_keyValueFactory.CreateFromKeyValues(rangeStart)!;
                TKey to = (TKey)_keyValueFactory.CreateFromKeyValues(rangeEnd)!;
                var cached = _forwardCache.TryGetRange(from, to);
                if (cached != null) return cached;
            }
            return _streamData.GetValueBuffersRange(database, _keyValueFactory, rangeStart, rangeEnd);
        }
        else if (_knetCompactedReplicator != null) throw new InvalidOperationException($"KNetCompactedReplicator does not support range iteration");
        else throw new InvalidOperationException("Missing _knetCompactedReplicator or _streamData");
    }
    /// <inheritdoc/>
    public IEnumerable<ValueBuffer> GetValueBuffersReverse(IKEFCoreDatabase database)
    {
        if (_streamData != null)
            // reverse cache: populated by this method, serves reverse range when warm.
            // cold → native RocksDB reverse iterator (more efficient than any .NET alternative)
            return _reverseCache.GetAllPopulating(() => _streamData.GetValueBuffersReverse(_database));
        else if (_knetCompactedReplicator != null) throw new InvalidOperationException($"KNetCompactedReplicator does not support reverse iteration");
        else throw new InvalidOperationException("Missing _knetCompactedReplicator or _streamData");
    }
    /// <inheritdoc/>
    public IEnumerable<ValueBuffer> GetValueBuffersReverseRange(IKEFCoreDatabase database, object?[]? rangeStart, object?[]? rangeEnd)
    {
        if (_streamData != null)
        {
            // guard: build keys only if reverse cache is warm
            if (rangeStart != null && rangeEnd != null && _reverseCache.IsWarm)
            {
                TKey from = (TKey)_keyValueFactory.CreateFromKeyValues(rangeStart)!;
                TKey to = (TKey)_keyValueFactory.CreateFromKeyValues(rangeEnd)!;
                var cached = _reverseCache.TryGetReverseRange(from, to);
                if (cached != null) return cached;
            }
            return _streamData.GetValueBuffersReverseRange(database, _keyValueFactory, rangeStart, rangeEnd);
        }
        else if (_knetCompactedReplicator != null) throw new InvalidOperationException($"KNetCompactedReplicator does not support reverse range iteration");
        else throw new InvalidOperationException("Missing _knetCompactedReplicator or _streamData");
    }
    /// <inheritdoc/>
    public IEnumerable<ValueBuffer> GetValueBuffersByPrefix(IKEFCoreDatabase database, object?[]? prefixValues)
    {
        if (_streamData != null)
        {
            // try forward cache first if warm
            if (prefixValues != null && _forwardCache.IsWarm)
            {
                TKey prefix = (TKey)_keyValueFactory.CreateFromKeyValues(prefixValues)!;
                var cached = _forwardCache.TryGetPrefix(prefix, prefixValues.Length);
                if (cached != null) return cached;
            }
            return _streamData.GetValueBuffersByPrefix(database, _keyValueFactory, prefixValues);
        }
        else if (_knetCompactedReplicator != null) throw new InvalidOperationException($"KNetCompactedReplicator does not support prefix iteration");
        else throw new InvalidOperationException("Missing _knetCompactedReplicator or _streamData");
    }

    /// <inheritdoc/>
    public void Start(IKEFCoreDatabase database)
    {
        if (_streamData != null) _streamsManager!.CreateAndStartTopology(database);
        else if (_knetCompactedReplicator != null)
        {
#if DEBUG_PERFORMANCE
            Stopwatch sw = Stopwatch.StartNew();
#endif
            if (!_knetCompactedReplicator.IsStarted)
            {
                _knetCompactedReplicator.Start();
            }
            else
            {
                // instance ready, try to fill IKEFCoreDatabase
                KEFCoreDatabaseLocalData localData = new(database, _entityType);
                foreach (var item in _knetCompactedReplicator)
                {
                    KEFCoreStateHelper.ManageAdded(database.InfrastructureLogger, database.Cluster.ValueGeneratorSelector, database.Cluster.ComplexTypeConverterFactory, localData.UpdateAdapter!, localData.MetadataForChanges!, localData.PrimaryKeyForChanges!, item.Key, item.Value);
                }
            }
#if DEBUG_PERFORMANCE
            sw.Stop();
            KNet.Internal.DebugPerformanceHelper.ReportString($"EntityTypeProducer - KNetCompactedReplicator::StartAndWait for {_entityType.Name} in {sw.Elapsed}");
#endif
        }
        else throw new InvalidOperationException("Missing _knetCompactedReplicator or _streamData");
    }

    private static void UpdateFromCommit(EntityTypeProducer<TKey, TValueContainer, TJVMKey, TJVMValueContainer> producer, int partiton, long? offset, DateTime? timestamp, JVMBridgeException error)
    {
        if (offset.HasValue)
        {
            producer._streamsManager!.PartitionOffsetWritten(producer.EntityType.GetKEFCoreTopicName(), partiton, offset.Value);
            producer._forwardCache.Invalidate();
            producer._reverseCache.Invalidate();
        }
    }

    /// <inheritdoc/>
    string ITransactionalEntityTypeProducer.TransactionGroup => _transactionGroup!;

    /// <inheritdoc/>
    void ITransactionalEntityTypeProducer.CommitPendingOffsets()
    {
        while (_pendingOffsets.TryDequeue(out var item))
        {
            _streamsManager!.PartitionOffsetWritten(item.topicName, item.partition, item.offset);
        }
    }
    /// <inheritdoc/>
    void ITransactionalEntityTypeProducer.AbortPendingOffsets() => _pendingOffsets.Clear();

    /// <inheritdoc/>
    public bool? EnsureSynchronized(long timeout)
    {
        if (_streamData != null) return _streamsManager!.EnsureSynchronized(_entityType, timeout);
        else if (_knetCompactedReplicator != null)
        {
#if DEBUG_PERFORMANCE
            Stopwatch sw = null!;
            try
            {
                sw = Stopwatch.StartNew();
#endif
                return _knetCompactedReplicator.SyncWait((int)timeout);
#if DEBUG_PERFORMANCE
            }
            finally
            {
                sw?.Stop();
                KNet.Internal.DebugPerformanceHelper.ReportString($"EntityTypeProducer - KNetCompactedReplicator::SyncWait for {_entityType.Name} in {sw?.Elapsed}");
            }
#endif
        }
        else throw new InvalidOperationException("Missing _knetCompactedReplicator or _streamData");
    }

    private void KNetCompactedReplicator_OnRemoteAdd(IKNetCompactedReplicator<TKey, TValueContainer, TJVMKey, TJVMValueContainer> arg1, KeyValuePair<TKey, TValueContainer> arg2)
    {
        foreach (var item in _updaters.Where(u => u.Value.ManageEvents))
        {
            IKEFCoreDatabase database = item.Key;
            KEFCoreDatabaseLocalData localData = item.Value;
            KEFCoreStateHelper.ManageAdded(database.InfrastructureLogger, database.Cluster.ValueGeneratorSelector, database.Cluster.ComplexTypeConverterFactory, localData.UpdateAdapter!, localData.MetadataForChanges!, localData.PrimaryKeyForChanges!, arg2.Key, arg2.Value);
        }
    }

    private void KNetCompactedReplicator_OnRemoteUpdate(IKNetCompactedReplicator<TKey, TValueContainer, TJVMKey, TJVMValueContainer> arg1, KeyValuePair<TKey, TValueContainer> arg2)
    {
        foreach (var item in _updaters.Where(u => u.Value.ManageEvents))
        {
            IKEFCoreDatabase database = item.Key;
            KEFCoreDatabaseLocalData localData = item.Value;
            KEFCoreStateHelper.ManageUpdate(database.InfrastructureLogger, database.Cluster.ValueGeneratorSelector, database.Cluster.ComplexTypeConverterFactory, localData.UpdateAdapter!, localData.MetadataForChanges!, localData.PrimaryKeyForChanges!, arg2.Key, arg2.Value);
        }
    }

    private void KNetCompactedReplicator_OnRemoteRemove(IKNetCompactedReplicator<TKey, TValueContainer, TJVMKey, TJVMValueContainer> arg1, KeyValuePair<TKey, TValueContainer> arg2)
    {
        foreach (var item in _updaters.Where(u => u.Value.ManageEvents))
        {
            IKEFCoreDatabase database = item.Key;
            KEFCoreDatabaseLocalData localData = item.Value;
            KEFCoreStateHelper.ManageDelete(database.InfrastructureLogger, localData.UpdateAdapter!, localData.MetadataForChanges!, localData.PrimaryKeyForChanges!, arg2.Key);
        }
    }
}
