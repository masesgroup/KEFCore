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

using Java.Util.Concurrent;
using MASES.EntityFrameworkCore.KNet.Serialization;
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
public class EntityTypeProducer<TKey, TValueContainer, TJVMKey, TJVMValueContainer> : IEntityTypeProducer
    where TKey : notnull
    where TValueContainer : class, IValueContainer<TKey>
{
    private readonly ConstructorInfo TValueContainerConstructor;
    private readonly bool _useCompactedReplicator;
    private readonly IKafkaCluster _cluster;
    private readonly IEntityType _entityType;
    private readonly IKNetCompactedReplicator<TKey, TValueContainer, TJVMKey, TJVMValueContainer>? _kafkaCompactedReplicator;
    private readonly MASES.KNet.Producer.IProducer<TKey, TValueContainer, TJVMKey, TJVMValueContainer>? _kafkaProducer;
    private readonly IKafkaStreamsRetriever? _streamData;
    private readonly ISerDes<TKey, TJVMKey>? _keySerdes;
    private readonly ISerDes<TValueContainer, TJVMValueContainer>? _valueSerdes;
    private readonly Action<EntityTypeChanged>? _onChangeEvent;

    #region KNetCompactedReplicatorEnumerable
    class KNetCompactedReplicatorEnumerable(IEntityType entityType, IKNetCompactedReplicator<TKey, TValueContainer, TJVMKey, TJVMValueContainer>? kafkaCompactedReplicator) : IEnumerable<ValueBuffer>
    {
        readonly IEntityType _entityType = entityType;
        readonly IKNetCompactedReplicator<TKey, TValueContainer, TJVMKey, TJVMValueContainer>? _kafkaCompactedReplicator = kafkaCompactedReplicator;

        #region KNetCompactedReplicatorEnumerator
        class KNetCompactedReplicatorEnumerator : IEnumerator<ValueBuffer>
        {
#if DEBUG_PERFORMANCE
            Stopwatch _moveNextSw = new Stopwatch();
            Stopwatch _currentSw = new Stopwatch();
            Stopwatch _valueBufferSw = new Stopwatch();
#endif
            readonly IEntityType _entityType;
            readonly IKNetCompactedReplicator<TKey, TValueContainer, TJVMKey, TJVMValueContainer>? _kafkaCompactedReplicator;
            readonly IEnumerator<KeyValuePair<TKey, TValueContainer>>? _enumerator;
            public KNetCompactedReplicatorEnumerator(IEntityType entityType, IKNetCompactedReplicator<TKey, TValueContainer, TJVMKey, TJVMValueContainer>? kafkaCompactedReplicator)
            {
                _entityType = entityType;
                _kafkaCompactedReplicator = kafkaCompactedReplicator;
#if DEBUG_PERFORMANCE
                Stopwatch sw = Stopwatch.StartNew();
#endif
                if (!_kafkaCompactedReplicator!.SyncWait()) throw new InvalidOperationException($"Failed to synchronize with {_kafkaCompactedReplicator.StateName}");
#if DEBUG_PERFORMANCE
                sw.Stop();
                Infrastructure.KafkaDbContext.ReportString($"KNetCompactedReplicatorEnumerator SyncWait for {_entityType.Name} tooks {sw.Elapsed}");
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
                Infrastructure.KafkaDbContext.ReportString($"KNetCompactedReplicatorEnumerator _moveNextSw: {_moveNextSw.Elapsed} _currentSw: {_currentSw.Elapsed} _valueBufferSw: {_valueBufferSw.Elapsed}");
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
                    object[] array = null!;
                    _enumerator.Current.Value.GetData(_entityType, ref array);
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
            return new KNetCompactedReplicatorEnumerator(_entityType, _kafkaCompactedReplicator);
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
        Infrastructure.KafkaDbContext.ReportString($"Creating new EntityTypeProducer for {entityType.Name}");
#endif
        _entityType = entityType;
        _cluster = cluster;
        _useCompactedReplicator = _cluster.Options.UseCompactedReplicator;
        _onChangeEvent = _cluster.Options.OnChangeEvent;

        var tTValueContainer = typeof(TValueContainer);
        TValueContainerConstructor = tTValueContainer.GetConstructors().Single(ci => ci.GetParameters().Length == 2);

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
            if (_onChangeEvent != null)
            {
                _kafkaCompactedReplicator.OnRemoteAdd += KafkaCompactedReplicator_OnRemoteAdd;
                _kafkaCompactedReplicator.OnRemoteUpdate += KafkaCompactedReplicator_OnRemoteUpdate;
                _kafkaCompactedReplicator.OnRemoteRemove += KafkaCompactedReplicator_OnRemoteRemove;
            }
#if DEBUG_PERFORMANCE
            Stopwatch sw = Stopwatch.StartNew();
#endif
            if (!_kafkaCompactedReplicator.StartAndWait()) throw new InvalidOperationException($"Failed to synchronize with {_kafkaCompactedReplicator.StateName}");
#if DEBUG_PERFORMANCE
            sw.Stop();
            Infrastructure.KafkaDbContext.ReportString($"EntityTypeProducer - KNetCompactedReplicator::StartAndWait for {entityType.Name} in {sw.Elapsed}");
#endif
        }
        else
        {
            _kafkaProducer = new KNetProducer<TKey, TValueContainer, TJVMKey, TJVMValueContainer>(_cluster.Options.ProducerOptionsBuilder(), _keySerdes, _valueSerdes);
            _streamData = _cluster.Options.UseKNetStreams ? new KNetStreamsRetriever<TKey, TValueContainer, TJVMKey, TJVMValueContainer>(cluster, entityType)
                                                          : new KafkaStreamsTableRetriever<TKey, TValueContainer, TJVMKey, TJVMValueContainer>(cluster, entityType, _keySerdes!, _valueSerdes!);
        }
    }

    /// <inheritdoc/>
    public virtual IEntityType EntityType => _entityType;
    /// <inheritdoc/>
    public IEnumerable<Future<RecordMetadata>> Commit(IEnumerable<IKafkaRowBag> records)
    {
        if (_useCompactedReplicator)
        {
            foreach (KafkaRowBag<TKey, TValueContainer> record in records.Cast<KafkaRowBag<TKey, TValueContainer>>())
            {
                var value = record.Value(TValueContainerConstructor);
                if (_kafkaCompactedReplicator != null) _kafkaCompactedReplicator[record.Key] = value!;
            }

            return null!;
        }
        else
        {
            List<Future<RecordMetadata>> futures = new();
            foreach (KafkaRowBag<TKey, TValueContainer> record in records.Cast<KafkaRowBag<TKey, TValueContainer>>())
            {
                Future<RecordMetadata> future;
#if OLD_WAY
                var newRecord = _kafkaProducer?.NewRecord(record.AssociatedTopicName, 0, record.Key, record.Value(TValueContainerConstructor)!);
                var future = _kafkaProducer?.Send(newRecord);
                futures.Add(future!);
#else
                Org.Apache.Kafka.Common.Header.Headers headers = null!;
                if (_keySerdes.UseHeaders || _valueSerdes.UseHeaders)
                {
                    headers = Org.Apache.Kafka.Common.Header.Headers.Create();
                }

                _kafkaProducer?.Send(record.AssociatedTopicName, 0, record.Key, record.Value(TValueContainerConstructor), headers);
#endif
            }

            _kafkaProducer?.Flush();

            return futures;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_kafkaCompactedReplicator != null)
        {
            if (_onChangeEvent != null)
            {
                _kafkaCompactedReplicator.OnRemoteAdd -= KafkaCompactedReplicator_OnRemoteAdd;
                _kafkaCompactedReplicator.OnRemoteUpdate -= KafkaCompactedReplicator_OnRemoteUpdate;
                _kafkaCompactedReplicator.OnRemoteRemove -= KafkaCompactedReplicator_OnRemoteRemove;
            }
            _kafkaCompactedReplicator?.Dispose();
        }
        else
        {
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
            if (_kafkaCompactedReplicator != null) return new KNetCompactedReplicatorEnumerable(_entityType, _kafkaCompactedReplicator);
            if (_streamData != null) return _streamData.GetValueBuffers();
            throw new InvalidOperationException("Missing _kafkaCompactedReplicator or _streamData");
        }
    }

    private void KafkaCompactedReplicator_OnRemoteAdd(IKNetCompactedReplicator<TKey, TValueContainer, TJVMKey, TJVMValueContainer> arg1, KeyValuePair<TKey, TValueContainer> arg2)
    {
        Task.Factory.StartNew(() =>
        {
            _onChangeEvent?.Invoke(new EntityTypeChanged(_entityType, EntityTypeChanged.ChangeKindType.Added, arg2.Key));
        });
    }

    private void KafkaCompactedReplicator_OnRemoteUpdate(IKNetCompactedReplicator<TKey, TValueContainer, TJVMKey, TJVMValueContainer> arg1, KeyValuePair<TKey, TValueContainer> arg2)
    {
        Task.Factory.StartNew(() =>
        {
            _onChangeEvent?.Invoke(new EntityTypeChanged(_entityType, EntityTypeChanged.ChangeKindType.Updated, arg2.Key));
        });
    }

    private void KafkaCompactedReplicator_OnRemoteRemove(IKNetCompactedReplicator<TKey, TValueContainer, TJVMKey, TJVMValueContainer> arg1, KeyValuePair<TKey, TValueContainer> arg2)
    {
        Task.Factory.StartNew(() =>
        {
            _onChangeEvent?.Invoke(new EntityTypeChanged(_entityType, EntityTypeChanged.ChangeKindType.Removed, arg2.Key));
        });
    }
}
