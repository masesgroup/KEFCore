/*
*  Copyright 2022 MASES s.r.l.
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
using MASES.KNet.Producer;
using MASES.KNet.Replicator;
using MASES.KNet.Serialization;
using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using MASES.KNet.Serialization.Json;
using Org.Apache.Kafka.Clients.Producer;
using System.Text.Json;
using System.Collections;

namespace MASES.EntityFrameworkCore.KNet.Storage.Internal;

public class EntityTypeProducers
{
    static IEntityTypeProducer? _globalProducer = null;
    static readonly ConcurrentDictionary<IEntityType, IEntityTypeProducer> _producers = new ConcurrentDictionary<IEntityType, IEntityTypeProducer>();

    public static IEntityTypeProducer Create<TKey>(IEntityType entityType, IKafkaCluster cluster) where TKey : notnull
    {
        //if (!cluster.Options.ProducerByEntity)
        //{
        //    lock (_producers)
        //    {
        //        if (_globalProducer == null) _globalProducer = CreateProducerLocal<TKey>(entityType, cluster);
        //        return _globalProducer;
        //    }
        //}
        //else
        //{
        return _producers.GetOrAdd(entityType, _ => CreateProducerLocal<TKey>(entityType, cluster));
        //}
    }

    static IEntityTypeProducer CreateProducerLocal<TKey>(IEntityType entityType, IKafkaCluster cluster) where TKey : notnull => new EntityTypeProducer<TKey>(entityType, cluster);
}

[JsonSerializable(typeof(ObjectType))]
public class ObjectType : IJsonOnDeserialized
{
    public ObjectType()
    {

    }

    public ObjectType(IProperty typeName, object value)
    {
        TypeName = typeName.ClrType?.FullName;
        Value = value;
    }

    public void OnDeserialized()
    {
        if (Value is JsonElement elem)
        {
            switch (elem.ValueKind)
            {
                case JsonValueKind.Undefined:
                    break;
                case JsonValueKind.Object:
                    break;
                case JsonValueKind.Array:
                    break;
                case JsonValueKind.String:
                    Value = elem.GetString()!;
                    break;
                case JsonValueKind.Number:
                    var tmp = elem.GetInt64();
                    Value = Convert.ChangeType(tmp, Type.GetType(TypeName!)!);
                    break;
                case JsonValueKind.True:
                    Value = true;
                    break;
                case JsonValueKind.False:
                    Value = false;
                    break;
                case JsonValueKind.Null:
                    Value = null;
                    break;
                default:
                    break;
            }
        }
        else
        {
            Value = Convert.ChangeType(Value, Type.GetType(TypeName!)!);
        }
    }

    public string? TypeName { get; set; }

    public object Value { get; set; }
}

public interface IEntityTypeData
{
    void GetData(IEntityType tName, ref object[] array);
}

[JsonSerializable(typeof(KNetEntityTypeData<>))]
public class KNetEntityTypeData<TKey> : IEntityTypeData
{
    public KNetEntityTypeData() { }

    public KNetEntityTypeData(IEntityType tName, IProperty[] properties, object[] rData)
    {
        TypeName = tName.Name;
        Data = new Dictionary<int, ObjectType>();
        for (int i = 0; i < properties.Length; i++)
        {
            Data.Add(properties[i].GetIndex(), new ObjectType(properties[i], rData[i]));
        }
    }

    public string TypeName { get; set; }

    public Dictionary<int, ObjectType> Data { get; set; }

    public void GetData(IEntityType tName, ref object[] array)
    {
#if DEBUG_PERFORMANCE
        Stopwatch fullSw = new Stopwatch();
        Stopwatch newSw = new Stopwatch();
        Stopwatch iterationSw = new Stopwatch();
        try
        {
            fullSw.Start();
#endif
        if (Data == null) { return; }
#if DEBUG_PERFORMANCE
            newSw.Start();
#endif
        array = new object[Data.Count];
#if DEBUG_PERFORMANCE
            newSw.Stop();
            iterationSw.Start();
#endif
        for (int i = 0; i < Data.Count; i++)
        {
            array[i] = Data[i].Value;
        }
#if DEBUG_PERFORMANCE
            iterationSw.Stop();
            fullSw.Stop();
        }
        finally
        {
            Trace.WriteLine($"Time to GetData with length {Data.Count}: {fullSw.Elapsed} - new array took: {newSw.Elapsed} - Iteration took: {iterationSw.Elapsed}");
        }
#endif
    }
}

public class EntityTypeProducer<TKey> : IEntityTypeProducer where TKey : notnull
{
    private readonly bool _useCompactedReplicator;
    private readonly IKafkaCluster _cluster;
    private readonly IEntityType _entityType;
    private readonly IKNetCompactedReplicator<TKey, KNetEntityTypeData<TKey>>? _kafkaCompactedReplicator;
    private readonly IKNetProducer<TKey, KNetEntityTypeData<TKey>>? _kafkaProducer;
    private readonly IKafkaStreamsBaseRetriever _streamData;
    private readonly KNetSerDes<TKey> _keySerdes;
    private readonly KNetSerDes<KNetEntityTypeData<TKey>> _valueSerdes;

    class KNetCompactedReplicatorEnumerable : IEnumerable<ValueBuffer>
    {
        readonly IEntityType _entityType;
        readonly IKNetCompactedReplicator<TKey, KNetEntityTypeData<TKey>>? _kafkaCompactedReplicator;
        class KNetCompactedReplicatorEnumerator : IEnumerator<ValueBuffer>
        {
            readonly IEntityType _entityType;
            readonly IEnumerator<KeyValuePair<TKey, KNetEntityTypeData<TKey>>> _enumerator;
            public KNetCompactedReplicatorEnumerator(IEntityType entityType, IKNetCompactedReplicator<TKey, KNetEntityTypeData<TKey>>? kafkaCompactedReplicator)
            {
                _entityType = entityType;
                kafkaCompactedReplicator?.SyncWait();
                _enumerator = kafkaCompactedReplicator?.GetEnumerator();
            }

            ValueBuffer? _current = null;

            public ValueBuffer Current => _current.HasValue ? _current.Value : default;

            object IEnumerator.Current => Current;

            public void Dispose()
            {
                _enumerator?.Dispose();
            }

            public bool MoveNext()
            {
                if (_enumerator.MoveNext())
                {
                    object[] array = null;
                    _enumerator.Current.Value.GetData(_entityType, ref array);
                    _current = new ValueBuffer(array);
                    return true;
                }
                _current = null;
                return false;
            }

            public void Reset()
            {
                _enumerator?.Reset();
            }
        }

        public KNetCompactedReplicatorEnumerable(IEntityType entityType, IKNetCompactedReplicator<TKey, KNetEntityTypeData<TKey>>? kafkaCompactedReplicator)
        {
            _entityType = entityType;
            _kafkaCompactedReplicator = kafkaCompactedReplicator;
        }

        public IEnumerator<ValueBuffer> GetEnumerator()
        {
            return new KNetCompactedReplicatorEnumerator(_entityType, _kafkaCompactedReplicator);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }


    public EntityTypeProducer(IEntityType entityType, IKafkaCluster cluster)
    {
        _entityType = entityType;
        _cluster = cluster;
        _useCompactedReplicator = _cluster.Options.UseCompactedReplicator;

        if (KNetSerialization.IsInternalManaged<TKey>())
        {
            _keySerdes = new KNetSerDes<TKey>();
        }
        else _keySerdes = new JsonSerDes<TKey>();

        _valueSerdes = new JsonSerDes<KNetEntityTypeData<TKey>>();

        if (_useCompactedReplicator)
        {
            _kafkaCompactedReplicator = new KNetCompactedReplicator<TKey, KNetEntityTypeData<TKey>>()
            {
                UpdateMode = UpdateModeTypes.OnConsume,
                BootstrapServers = _cluster.Options.BootstrapServers,
                StateName = _entityType.TopicName(_cluster.Options),
                Partitions = _entityType.NumPartitions(_cluster.Options),
                ConsumerInstances = _entityType.ConsumerInstances(_cluster.Options),
                ReplicationFactor = _entityType.ReplicationFactor(_cluster.Options),
                TopicConfig = _cluster.Options.TopicConfigBuilder,
                ProducerConfig = _cluster.Options.ProducerConfigBuilder,
                KeySerDes = _keySerdes,
                ValueSerDes = _valueSerdes,
            };
            _kafkaCompactedReplicator.StartAndWait();
        }
        else
        {
            _kafkaProducer = new KNetProducer<TKey, KNetEntityTypeData<TKey>>(_cluster.Options.ProducerOptions(), _keySerdes, _valueSerdes);
            _streamData = new KafkaStreamsTableRetriever<TKey>(cluster, entityType, _keySerdes, _valueSerdes);
        }
    }

    public IEnumerable<Future<RecordMetadata>> Commit(IEnumerable<IKafkaRowBag> records)
    {
        if (_useCompactedReplicator)
        {
            foreach (KafkaRowBag<TKey> record in records)
            {
                var value = record.Value;
                if (_kafkaCompactedReplicator != null) _kafkaCompactedReplicator[record.Key] = value!;
            }

            return null;
        }
        else
        {
            List<Future<RecordMetadata>> futures = new();
            foreach (KafkaRowBag<TKey> record in records)
            {
                var future = _kafkaProducer?.Send(new KNetProducerRecord<TKey, KNetEntityTypeData<TKey>>(record.AssociatedTopicName, 0, record.Key, record.Value!));
                futures.Add(future);
            }

            _kafkaProducer?.Flush();

            return futures;
        }
    }

    public void Dispose()
    {
        if (_useCompactedReplicator)
        {
            _kafkaCompactedReplicator?.Dispose();
        }
        else
        {
            _kafkaProducer?.Dispose();
            _streamData?.Dispose();
        }
    }

    public IEnumerable<ValueBuffer> ValueBuffers
    {
        get
        {
            if (_streamData != null) return _streamData;
            if (_kafkaCompactedReplicator == null) throw new InvalidOperationException("Missing _kafkaCompactedReplicator");
            return new KNetCompactedReplicatorEnumerable(_entityType, _kafkaCompactedReplicator);
        }
    }
}
