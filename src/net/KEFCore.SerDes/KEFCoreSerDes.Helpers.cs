/*
*  Copyright 2024 MASES s.r.l.
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

using Java.Util;
using MASES.JCOBridge.C2JBridge;
using MASES.KNet.Consumer;
using MASES.KNet.Serialization;
using Org.Apache.Kafka.Clients.Consumer;
using Org.Apache.Kafka.Common.Serialization;
using System.Text;

namespace MASES.EntityFrameworkCore.KNet.Serialization;
/// <summary>
/// This is an helper class to extract data information from Kafka Records stored in topics
/// </summary>
public class EntityExtractor
{
    /// <summary>
    /// Extract information for Entity using <paramref name="consumerConfig"/> configurtion within a <paramref name="topicName"/> and send them to <paramref name="cb"/>
    /// </summary>
    /// <param name="consumerConfig">The <see cref="ConsumerConfigBuilder"/> with configuration</param>
    /// <param name="topicName">The topic containing the data</param>
    /// <param name="cb">The <see cref="Action{T1, T2}"/> where data will be available</param>
    /// <param name="token">The <see cref="CancellationToken"/> to use to stop execution</param>
    /// <param name="onlyLatest">Start execution only for newest messages and does not execute for oldest, default is from beginning</param>
    public static void FromTopic(ConsumerConfigBuilder consumerConfig, string topicName, Action<object?, Exception?> cb, CancellationToken token, bool onlyLatest = false)
    {
        FromTopic<object>(consumerConfig, topicName, cb, token, onlyLatest);
    }

    /// <summary>
    /// Extract information for Entity from <paramref name="bootstrapServer"/> within a <paramref name="topicName"/> and send them to <paramref name="cb"/>
    /// </summary>
    /// <param name="bootstrapServer">The Apache Kafka <see href="https://kafka.apache.org/documentation/#consumerconfigs_bootstrap.servers">bootstrap.servers</see></param>
    /// <param name="topicName">The topic containing the data</param>
    /// <param name="cb">The <see cref="Action{T1, T2}"/> where data will be available</param>
    /// <param name="token">The <see cref="CancellationToken"/> to use to stop execution</param>
    /// <param name="onlyLatest">Start execution only for newest messages and does not execute for oldest, default is from beginning</param>
    public static void FromTopic(string bootstrapServer, string topicName, Action<object?, Exception?> cb, CancellationToken token, bool onlyLatest = false)
    {
        FromTopic<object>(bootstrapServer, topicName, cb, token, onlyLatest);
    }
    /// <summary>
    /// Extract information for Entity from <paramref name="bootstrapServer"/> within a <paramref name="topicName"/> and send them to <paramref name="cb"/>
    /// </summary>
    /// <typeparam name="TEntity">The Entity type if it is known</typeparam>
    /// <param name="bootstrapServer">The Apache Kafka <see href="https://kafka.apache.org/documentation/#consumerconfigs_bootstrap.servers">bootstrap.servers</see></param>
    /// <param name="topicName">The topic containing the data</param>
    /// <param name="cb">The <see cref="Action{T1, T2}"/> where data will be available</param>
    /// <param name="token">The <see cref="CancellationToken"/> to use to stop execution</param>
    /// <param name="onlyLatest">Start execution only for newest messages and does not execute for oldest, default is from beginning</param>
    public static void FromTopic<TEntity>(string bootstrapServer, string topicName, Action<TEntity?, Exception?> cb, CancellationToken token, bool onlyLatest = false)
        where TEntity : class
    {
        ConsumerConfigBuilder consumerBuilder = ConsumerConfigBuilder.Create().WithBootstrapServers(bootstrapServer);
        FromTopic<TEntity>(consumerBuilder, topicName, cb, token, onlyLatest);
    }

    /// <summary>
    /// Extract information for Entity using <paramref name="consumerConfig"/> configurtion within a <paramref name="topicName"/> and send them to <paramref name="cb"/>
    /// </summary>
    /// <typeparam name="TEntity">The Entity type if it is known</typeparam>
    /// <param name="consumerConfig">The <see cref="ConsumerConfigBuilder"/> with configuration</param>
    /// <param name="topicName">The topic containing the data</param>
    /// <param name="cb">The <see cref="Action{T1, T2}"/> where data will be available</param>
    /// <param name="token">The <see cref="CancellationToken"/> to use to stop execution</param>
    /// <param name="onlyLatest">Start execution only for newest messages and does not execute for oldest, default is from beginning</param>
    public static void FromTopic<TEntity>(ConsumerConfigBuilder consumerConfig, string topicName, Action<TEntity?, Exception?> cb, CancellationToken token, bool onlyLatest = false)
        where TEntity : class
    {
        try
        {
            ConsumerConfigBuilder consumerBuilder = ConsumerConfigBuilder.CreateFrom(consumerConfig)
                                                                         .WithGroupId(Guid.NewGuid().ToString())
                                                                         .WithAutoOffsetReset(onlyLatest ? ConsumerConfigBuilder.AutoOffsetResetTypes.LATEST : ConsumerConfigBuilder.AutoOffsetResetTypes.EARLIEST)
                                                                         .WithKeyDeserializerClass(JVMBridgeBase.ClassNameOf<ByteArrayDeserializer>())
                                                                         .WithValueDeserializerClass(JVMBridgeBase.ClassNameOf<ByteArrayDeserializer>());

            KafkaConsumer<byte[], byte[]> kafkaConsumer = new KafkaConsumer<byte[], byte[]>(consumerBuilder);
            using var collection = Collections.Singleton((Java.Lang.String)topicName);
            kafkaConsumer.Subscribe(collection);

            while (!token.IsCancellationRequested)
            {
                var records = kafkaConsumer.Poll(100);
                foreach (var record in records)
                {
                    TEntity? entity = null;
                    Exception? exception = null;
                    try
                    {
                        entity = FromRecord<TEntity>(record, true);
                    }
                    catch (Exception ex)
                    {
                        entity = null;
                        exception = ex;
                    }
                    cb.Invoke(entity, exception);
                }
            }
        }
        catch (OperationCanceledException) { return; }
    }

    /// <summary>
    /// Extract information for Entity from <paramref name="record"/> in input
    /// </summary>
    /// <param name="record">The Apache Kafka record containing the information</param>
    /// <param name="throwUnmatch">Throws exceptions if there is unmatch in data retrieve, e.g. a property not available or a not settable</param>
    /// <returns>The extracted entity</returns>
    public static TEntity FromRecord<TEntity>(Org.Apache.Kafka.Clients.Consumer.ConsumerRecord<byte[], byte[]> record, bool throwUnmatch = false)
        where TEntity : class
    {
        return (TEntity)FromRecord(record, throwUnmatch);
    }

    /// <summary>
    /// Extract information for Entity from <paramref name="record"/> in input
    /// </summary>
    /// <param name="record">The Apache Kafka record containing the information</param>
    /// <param name="throwUnmatch">Throws exceptions if there is unmatch in data retrieve, e.g. a property not available or a not settable</param>
    /// <returns>The extracted entity</returns>
    public static object FromRecord(Org.Apache.Kafka.Clients.Consumer.ConsumerRecord<byte[], byte[]> record, bool throwUnmatch = false)
    {
        Type? keySerializerSelectorType = null;
        Type? valueSerializerSelectorType = null;
        Type? keyType = null;
        Type? valueType = null;

        var headers = record.Headers();
        if (headers != null)
        {
            foreach (var header in headers.ToArray())
            {
                var key = header.Key();
                if (key == KNetSerialization.KeyTypeIdentifier)
                {
                    var strType = Encoding.UTF8.GetString(header.Value());
                    keyType = Type.GetType(strType, true)!;
                }
                if (key == KNetSerialization.KeySerializerIdentifier)
                {
                    var strType = Encoding.UTF8.GetString(header.Value());
                    keySerializerSelectorType = Type.GetType(strType, true)!;
                }
                if (key == KNetSerialization.ValueTypeIdentifier)
                {
                    var strType = Encoding.UTF8.GetString(header.Value());
                    valueType = Type.GetType(strType, true)!;
                }
                if (key == KNetSerialization.ValueSerializerIdentifier)
                {
                    var strType = Encoding.UTF8.GetString(header.Value());
                    valueSerializerSelectorType = Type.GetType(strType, true)!;
                }
            }
        }

        if (keyType == null || keySerializerSelectorType == null || valueType == null || valueSerializerSelectorType == null)
        {
            throw new InvalidOperationException($"Missing one, or more, mandatory information in record: keyType: {keyType} - keySerializerType: {keySerializerSelectorType} - valueType: {valueType} - valueSerializerType: {valueSerializerSelectorType}");
        }

        return FromRawValueData(keyType!, valueType!, keySerializerSelectorType!, valueSerializerSelectorType!, record.Topic(), record.Value(), record.Key(), throwUnmatch);
    }

    /// <summary>
    /// Extract information for Entity from <paramref name="recordValue"/> in input
    /// </summary>
    /// <param name="keyType">Expected key <see cref="Type"/></param>
    /// <param name="valueContainer">Expected ValueContainer <see cref="Type"/></param>
    /// <param name="keySerializerSelectorType">Key serializer to be used</param>
    /// <param name="valueSerializerSelectorType">ValueContainer serializer to be used</param>
    /// <param name="topic">The Apache Kafka topic the data is coming from</param>
    /// <param name="recordValue">The Apache Kafka record value containing the information</param>
    /// <param name="recordKey">The Apache Kafka record key containing the information</param>
    /// <param name="throwUnmatch">Throws exceptions if there is unmatch in data retrieve, e.g. a property not available or a not settable</param>
    /// <returns>The extracted entity</returns>
    public static object FromRawValueData(Type keyType, Type valueContainer, Type keySerializerSelectorType, Type valueSerializerSelectorType, string topic, byte[] recordValue, byte[] recordKey, bool throwUnmatch = false)
    {
        var fullKeySerializer = keySerializerSelectorType.MakeGenericType(keyType);

        var fullValueContainer = valueContainer.MakeGenericType(keyType);
        var fullValueContainerSerializer = valueSerializerSelectorType.MakeGenericType(fullValueContainer);

        var ccType = typeof(LocalEntityExtractor<,,,,,>);
        var extractorType = ccType.MakeGenericType(keyType, fullValueContainer, typeof(byte[]), typeof(byte[]), fullKeySerializer, fullValueContainerSerializer);
        var methodInfo = extractorType.GetMethod("GetEntity");
        var extractor = Activator.CreateInstance(extractorType);
        return methodInfo?.Invoke(extractor, new object[] { topic, recordValue, recordKey, throwUnmatch })!;
    }
}
