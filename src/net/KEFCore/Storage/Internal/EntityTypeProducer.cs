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

#nullable enable

using Java.Util.Concurrent;
using MASES.EntityFrameworkCore.KNet.Serdes.Internal;
using MASES.KNet.Producer;
using MASES.KNet.Replicator;
using MASES.KNet.Serialization;
using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using MASES.KNet.Serialization.Json;
using Org.Apache.Kafka.Clients.Producer;
using System.Text.Json;
using Javax.Xml.Crypto;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

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

//public class ListStringObjectTupleConverter : JsonConverterFactory
//{
//    public override bool CanConvert(Type typeToConvert)
//    {
//        if (typeToConvert != typeof(List<(Type, object)>))
//        {
//            return false;
//        }

//        return true;
//    }

//    public override JsonConverter CreateConverter(Type type, JsonSerializerOptions options)
//    {
//        return new ListStringObjectTupleConverterInner();
//    }

//    private class ListStringObjectTupleConverterInner : JsonConverter<List<(Type, object)>>
//    {
//        public override List<(Type, object)> Read(
//            ref Utf8JsonReader reader,
//            Type typeToConvert,
//            JsonSerializerOptions options)
//        {
//            if (reader.TokenType != JsonTokenType.StartObject)
//            {
//                throw new JsonException();
//            }

//            var dictionary = new List<(Type, object)>();

//            while (reader.Read())
//            {
//                if (reader.TokenType == JsonTokenType.EndObject)
//                {
//                    return dictionary;
//                }

//                // Get the key.
//                if (reader.TokenType != JsonTokenType.PropertyName)
//                {
//                    throw new JsonException();
//                }

//                string? propertyName = reader.GetString();
//                var type = Type.GetType(propertyName!);
//                if (type == null)
//                {
//                    throw new JsonException($"Unable to convert \"{propertyName}\" to known CLR Type.");
//                }

//                var valueConverter = (JsonConverter<object>)options.GetConverter(type);

//                // Get the value.
//                reader.Read();
//                object value = valueConverter.Read(ref reader, type, options)!;

//                // Add to dictionary.
//                dictionary.Add((type, value));
//            }

//            throw new JsonException();
//        }

//        public override void Write(
//            Utf8JsonWriter writer,
//            List<(Type, object)> dictionary,
//            JsonSerializerOptions options)
//        {
//            writer.WriteStartObject();
//            foreach ((Type key, object value) in dictionary)
//            {
//                var propertyName = key.FullName.ToString();
//                writer.WritePropertyName(options.PropertyNamingPolicy?.ConvertName(propertyName) ?? propertyName);
//                JsonConverter valueConverter = options.GetConverter(key);
//                valueConverter.
//                valueConverter.Write(writer, value, options);
//            }

//            writer.WriteEndObject();
//        }
//    }
//}

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

[JsonSerializable(typeof(KNetEntityTypeData<>))]
public class KNetEntityTypeData<TKey>
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
    // [JsonConverter(typeof(ListStringObjectTupleConverter))]
    public Dictionary<int, ObjectType> Data { get; set; }

    public object[] GetData(IEntityType tName)
    {
        if (Data == null) return null;

        var array = Data.Select((o) => o.Value.Value).ToArray();

        return array;

        var _properties = tName.GetProperties().ToArray();
        List<object> data = new List<object>();

        for (int i = 0; i < Data!.Count; i++)
        {
            if (Data[i].Value is JsonElement elem)
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
                        data.Add(elem.GetString());
                        break;
                    case JsonValueKind.Number:
                        var tmp = elem.GetInt64();
                        data.Add(Convert.ChangeType(tmp, _properties[i].ClrType));
                        break;
                    case JsonValueKind.True:
                        data.Add(true);
                        break;
                    case JsonValueKind.False:
                        data.Add(false);
                        break;
                    case JsonValueKind.Null:
                        data.Add(null);
                        break;
                    default:
                        break;
                }

            }
            else
            {
                data.Add(Convert.ChangeType(Data[i], _properties[i].ClrType));
            }
        }
        return data.ToArray();
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
        }
        else
        {
            _kafkaProducer = new KNetProducer<TKey, KNetEntityTypeData<TKey>>(_cluster.Options.ProducerOptions(), _keySerdes, _valueSerdes);
            _streamData = new KafkaStreamsTableRetriever<TKey>(cluster, entityType, _keySerdes, _valueSerdes);
        }
    }

    public IEnumerable<Java.Util.Concurrent.Future<RecordMetadata>> Commit(IEnumerable<IKafkaRowBag> records)
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
            System.Collections.Generic.List<Future<RecordMetadata>> futures = new();
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

    public IEnumerable<ValueBuffer> GetValueBuffer()
    {
        if (_streamData != null) return _streamData;
        _kafkaCompactedReplicator?.SyncWait();
        if (_kafkaCompactedReplicator == null) throw new InvalidOperationException("Missing _kafkaCompactedReplicator");
        return _kafkaCompactedReplicator.Values.Select((item) => new ValueBuffer(item.GetData(_entityType)));
    }
}
