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

#nullable enable

using Java.Util;
using MASES.EntityFrameworkCore.KNet.Serialization.Json.Storage;
using MASES.JCOBridge.C2JBridge;
using MASES.KNet.Consumer;
using MASES.KNet.Serialization;
using Org.Apache.Kafka.Clients.Consumer;
using Org.Apache.Kafka.Common.Serialization;
using System.Collections.Concurrent;
using System.Text;

namespace MASES.EntityFrameworkCore.KNet.Serialization;

/// <summary>
/// An helper class used to manage native and special types in serialization
/// </summary>
public static class NativeTypeMapper
{
    static readonly ConcurrentDictionary<Type, (ManagedTypes, bool)> dict = new();
    static readonly ConcurrentDictionary<(ManagedTypes, bool), Type> reverseDict = new();
    readonly static Type StringType = typeof(string);
    readonly static Type GuidType = typeof(Guid);
    readonly static Type NullableGuidType = typeof(Guid?);
    readonly static Type DateTimeType = typeof(DateTime);
    readonly static Type NullableDateTimeType = typeof(DateTime?);
    readonly static Type DateTimeOffsetType = typeof(DateTimeOffset);
    readonly static Type NullableDateTimeOffsetType = typeof(DateTimeOffset?);
    readonly static Type BoolType = typeof(bool);
    readonly static Type NullableBoolType = typeof(bool?);
    readonly static Type CharType = typeof(char);
    readonly static Type NullableCharType = typeof(char?);
    readonly static Type SByteType = typeof(sbyte);
    readonly static Type NullableSByteType = typeof(sbyte?);
    readonly static Type ByteType = typeof(byte);
    readonly static Type NullableByteType = typeof(byte?);
    readonly static Type ShortType = typeof(short);
    readonly static Type NullableShortType = typeof(short?);
    readonly static Type UShortType = typeof(ushort);
    readonly static Type NullableUShortType = typeof(ushort?);
    readonly static Type IntType = typeof(int);
    readonly static Type NullableIntType = typeof(int?);
    readonly static Type UIntType = typeof(uint);
    readonly static Type NullableUIntType = typeof(uint?);
    readonly static Type LongType = typeof(long);
    readonly static Type NullableLongType = typeof(long?);
    readonly static Type ULongType = typeof(ulong);
    readonly static Type NullableULongType = typeof(ulong?);
    readonly static Type DoubleType = typeof(double);
    readonly static Type NullableDoubleType = typeof(double?);
    readonly static Type FloatType = typeof(float);
    readonly static Type NullableFloatType = typeof(float?);
    readonly static Type DecimalType = typeof(decimal);
    readonly static Type NullableDecimalType = typeof(decimal?);

    /// <summary>
    /// List of <see cref="Type"/> managed from <see cref="PropertyData"/>
    /// </summary>
    public enum ManagedTypes
    {
        /// <summary>
        /// Not defined or not found
        /// </summary>
        Undefined,
        /// <summary>
        /// <see cref="string"/>
        /// </summary>
        String,
        /// <summary>
        /// <see cref="Guid"/>
        /// </summary>
        Guid,
        /// <summary>
        /// <see cref="DateTime"/>
        /// </summary>
        DateTime,
        /// <summary>
        /// <see cref="DateTimeOffset"/>
        /// </summary>
        DateTimeOffset,
        /// <summary>
        /// <see cref="bool"/>
        /// </summary>
        Bool,
        /// <summary>
        /// <see cref="char"/>
        /// </summary>
        Char,
        /// <summary>
        /// <see cref="sbyte"/>
        /// </summary>
        SByte,
        /// <summary>
        /// <see cref="byte"/>
        /// </summary>
        Byte,
        /// <summary>
        /// <see cref="short"/>
        /// </summary>
        Short,
        /// <summary>
        /// <see cref="ushort"/>
        /// </summary>
        UShort,
        /// <summary>
        /// <see cref="int"/>
        /// </summary>
        Int,
        /// <summary>
        /// <see cref="uint"/>
        /// </summary>
        UInt,
        /// <summary>
        /// <see cref="long"/>
        /// </summary>
        Long,
        /// <summary>
        /// <see cref="ulong"/>
        /// </summary>
        ULong,
        /// <summary>
        /// <see cref="double"/>
        /// </summary>
        Double,
        /// <summary>
        /// <see cref="float"/>
        /// </summary>
        Float,
        /// <summary>
        /// <see cref="decimal"/>
        /// </summary>
        Decimal,
    }

    static NativeTypeMapper()
    {
        dict.TryAdd(StringType, (ManagedTypes.String, true)); reverseDict.TryAdd((ManagedTypes.String, true), StringType);

        dict.TryAdd(GuidType, (ManagedTypes.Guid, false)); reverseDict.TryAdd((ManagedTypes.Guid, false), GuidType);
        dict.TryAdd(NullableGuidType, (ManagedTypes.Guid, true)); reverseDict.TryAdd((ManagedTypes.Guid, true), NullableGuidType);

        dict.TryAdd(DateTimeType, (ManagedTypes.DateTime, false)); reverseDict.TryAdd((ManagedTypes.DateTime, false), DateTimeType);
        dict.TryAdd(NullableDateTimeType, (ManagedTypes.DateTime, true)); reverseDict.TryAdd((ManagedTypes.DateTime, true), NullableDateTimeType);

        dict.TryAdd(DateTimeOffsetType, (ManagedTypes.DateTimeOffset, false)); reverseDict.TryAdd((ManagedTypes.DateTimeOffset, false), DateTimeOffsetType);
        dict.TryAdd(NullableDateTimeOffsetType, (ManagedTypes.DateTimeOffset, true)); reverseDict.TryAdd((ManagedTypes.DateTimeOffset, true), NullableDateTimeOffsetType);

        dict.TryAdd(BoolType, (ManagedTypes.Bool, false)); reverseDict.TryAdd((ManagedTypes.Bool, false), BoolType);
        dict.TryAdd(NullableBoolType, (ManagedTypes.Bool, true)); reverseDict.TryAdd((ManagedTypes.Bool, true), NullableBoolType);

        dict.TryAdd(CharType, (ManagedTypes.Char, false)); reverseDict.TryAdd((ManagedTypes.Char, false), CharType);
        dict.TryAdd(NullableCharType, (ManagedTypes.Char, true)); reverseDict.TryAdd((ManagedTypes.Char, true), NullableCharType);

        dict.TryAdd(SByteType, (ManagedTypes.SByte, false)); reverseDict.TryAdd((ManagedTypes.SByte, false), SByteType);
        dict.TryAdd(NullableSByteType, (ManagedTypes.SByte, true)); reverseDict.TryAdd((ManagedTypes.SByte, true), NullableSByteType);

        dict.TryAdd(ByteType, (ManagedTypes.Byte, false)); reverseDict.TryAdd((ManagedTypes.Byte, false), ByteType);
        dict.TryAdd(NullableByteType, (ManagedTypes.Byte, true)); reverseDict.TryAdd((ManagedTypes.Byte, true), NullableByteType);

        dict.TryAdd(ShortType, (ManagedTypes.Short, false)); reverseDict.TryAdd((ManagedTypes.Short, false), ShortType);
        dict.TryAdd(NullableShortType, (ManagedTypes.Short, true)); reverseDict.TryAdd((ManagedTypes.Short, true), NullableShortType);

        dict.TryAdd(UShortType, (ManagedTypes.UShort, false)); reverseDict.TryAdd((ManagedTypes.UShort, false), UShortType);
        dict.TryAdd(NullableUShortType, (ManagedTypes.UShort, true)); reverseDict.TryAdd((ManagedTypes.UShort, true), NullableUShortType);

        dict.TryAdd(IntType, (ManagedTypes.Int, false)); reverseDict.TryAdd((ManagedTypes.Int, false), IntType);
        dict.TryAdd(NullableIntType, (ManagedTypes.Int, true)); reverseDict.TryAdd((ManagedTypes.Int, true), NullableIntType);

        dict.TryAdd(UIntType, (ManagedTypes.UInt, false)); reverseDict.TryAdd((ManagedTypes.UInt, false), UIntType);
        dict.TryAdd(NullableUIntType, (ManagedTypes.UInt, true)); reverseDict.TryAdd((ManagedTypes.UInt, true), NullableUIntType);

        dict.TryAdd(LongType, (ManagedTypes.Long, false)); reverseDict.TryAdd((ManagedTypes.Long, false), LongType);
        dict.TryAdd(NullableLongType, (ManagedTypes.Long, true)); reverseDict.TryAdd((ManagedTypes.Long, true), NullableLongType);

        dict.TryAdd(ULongType, (ManagedTypes.ULong, false)); reverseDict.TryAdd((ManagedTypes.ULong, false), ULongType);
        dict.TryAdd(NullableULongType, (ManagedTypes.ULong, true)); reverseDict.TryAdd((ManagedTypes.ULong, true), NullableULongType);

        dict.TryAdd(DoubleType, (ManagedTypes.Double, false)); reverseDict.TryAdd((ManagedTypes.Double, false), DoubleType);
        dict.TryAdd(NullableDoubleType, (ManagedTypes.Double, true)); reverseDict.TryAdd((ManagedTypes.Double, true), NullableDoubleType);

        dict.TryAdd(FloatType, (ManagedTypes.Float, false)); reverseDict.TryAdd((ManagedTypes.Float, false), FloatType);
        dict.TryAdd(NullableFloatType, (ManagedTypes.Float, true)); reverseDict.TryAdd((ManagedTypes.Float, true), NullableFloatType);

        dict.TryAdd(DecimalType, (ManagedTypes.Decimal, false)); reverseDict.TryAdd((ManagedTypes.Decimal, false), DecimalType);
        dict.TryAdd(NullableDecimalType, (ManagedTypes.Decimal, true)); reverseDict.TryAdd((ManagedTypes.Decimal, true), NullableDecimalType);
    }

    /// <summary>
    /// Returns a <see cref="Tuple{T1, T2}"/> of <see cref="ManagedTypes"/> with a <see cref="bool"/> indicating if the types supports <see cref="Nullable"/>
    /// </summary>
    /// <param name="type">The <see cref="Type"/> to check</param>
    /// <returns>A <see cref="Tuple{T1, T2}"/> of <see cref="ManagedTypes"/> with a <see cref="bool"/> if <paramref name="type"/> is mapped otherwise returns <see cref="ManagedTypes.Undefined"/> and <see langword="false"/> for <see cref="Nullable"/> support</returns>
    public static (ManagedTypes, bool) GetValue(Type? type)
    {
        if (type == null || !dict.TryGetValue(type, out (ManagedTypes, bool) result)) { result = (ManagedTypes.Undefined, false); }
        return result;
    }
    /// <summary>
    /// Returns a <see cref="Type"/> mapped from the <paramref name="type"/> in input
    /// </summary>
    /// <param name="type"><see cref="Tuple{T1, T2}"/> of <see cref="ManagedTypes"/> with a <see cref="bool"/> indicating if the type needs support for <see cref="Nullable"/></param>
    /// <returns>A <see cref="Type"/> associated to the <paramref name="type"/> in input, otherwise <see langword="null"/></returns>
    public static Type GetValue((ManagedTypes, bool) type)
    {
        if (!reverseDict.TryGetValue(type, out Type result)) { result = null!; }
        return result;
    }
}

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
                if (key == KNetSerialization.KeyTypeIdentifierJVM)
                {
                    var strType = Encoding.UTF8.GetString(header.Value());
                    keyType = Type.GetType(strType, true)!;
                }
                if (key == KNetSerialization.KeySerializerIdentifierJVM)
                {
                    var strType = Encoding.UTF8.GetString(header.Value());
                    keySerializerSelectorType = Type.GetType(strType, true)!;
                }
                if (key == KNetSerialization.ValueTypeIdentifierJVM)
                {
                    var strType = Encoding.UTF8.GetString(header.Value());
                    valueType = Type.GetType(strType, true)!;
                }
                if (key == KNetSerialization.ValueSerializerIdentifierJVM)
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
        return methodInfo?.Invoke(extractor, new object[] { topic, recordKey, recordValue, throwUnmatch })!;
    }
}
