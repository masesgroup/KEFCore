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

using Java.Nio;
using Java.Util;
using MASES.EntityFrameworkCore.KNet.Serialization.Json.Storage;
using MASES.JCOBridge.C2JBridge;
using MASES.KNet.Consumer;
using MASES.KNet.Serialization;
using Org.Apache.Kafka.Clients.Consumer;
using Org.Apache.Kafka.Common.Serialization;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using static MASES.EntityFrameworkCore.KNet.Serialization.Json.DefaultKEFCoreSerDes;

namespace MASES.EntityFrameworkCore.KNet.Internal
{
    /// <summary>
    /// Internal debug class
    /// </summary>
    public class DebugPerformanceHelper
    {
#if DEBUG_PERFORMANCE
        const bool perf = true;
        /// <summary>
        /// Enable tracing of <see cref="MASES.EntityFrameworkCore.KNet.Serialization.IValueContainer{T}.GetData(Serialization.IValueContainerMetadata, ref object[], Serialization.IComplexTypeConverterFactory?)"/>
        /// </summary>
        public static bool TraceEntityTypeDataStorageGetData = false;
        /// <summary>
        /// Prints a message in console or trace if debugger is attached
        /// </summary>
        /// <param name="message"></param>
        public static void ReportString(string message)
        {
            if (!_enableKEFCoreTracing) return;

            if (false && Debugger.IsAttached)
            {
                Trace.WriteLine($"{DateTime.Now:HH::mm::ss:ffff} - {message}");
            }
            else
            {
                Console.WriteLine($"{DateTime.Now:HH::mm::ss:ffff} - {message}");
            }
        }
#else
const bool perf = false;
#endif
        /// <summary>
        /// Reports if the library was compiled to reports performance information
        /// </summary>
        public const bool IsPerformanceVersion = perf;
#if DEBUG_PERFORMANCE
        static bool _enableKEFCoreTracing = true;
#else
    static bool _enableKEFCoreTracing = false;
#endif
        /// <summary>
        /// Set to <see langword="true"/> to enable tracing of KEFCore
        /// </summary>
        /// <remarks>Can be set only if the project is compiled with DEBUG_PERFORMANCE preprocessor directive, otherwise an <see cref="InvalidOperationException"/> is raised</remarks>
        public static bool EnableKEFCoreTracing
        {
            get { return _enableKEFCoreTracing; }
            set
            {
                _enableKEFCoreTracing = value;
#if DEBUG_PERFORMANCE
                if (_enableKEFCoreTracing) throw new InvalidOperationException("Compile KEFCore using DEBUG_PERFORMANCE preprocessor directive");
#endif
            }
        }
    }
}

namespace MASES.EntityFrameworkCore.KNet.Serialization
{
    /// <summary>
    /// An helper class to centralize management of every Json conversion for <see cref="MASES.EntityFrameworkCore.KNet.Serialization.Json.DefaultKEFCoreSerDes"/> 
    /// or any other serialization sub-system which needs Jon conversion (typically when there isn't any <see cref="IComplexTypeConverter"/> available in <see cref="IComplexTypeConverterFactory"/> instance)
    /// </summary>
    public class JsonSupport
    {
        /// <summary>
        /// The <see cref="JsonSerializerOptions"/> used for serialization/deserialization of <see cref="Key{T}"/>
        /// </summary>
        public static readonly JsonSerializerOptions? DefautJsonOptions = new();
        /// <inheritdoc cref="JsonSerializer.Serialize(object, Type, JsonSerializerOptions?)"/>
        public string Serialize(Type type, object data)
        {
            return JsonSerializer.Serialize(data, type, DefautJsonOptions);
        }
        /// <inheritdoc cref="JsonSerializer.Serialize{TValue}(TValue, JsonSerializerOptions?)"/>
        public string Serialize<TData>(TData data)
        {
            return JsonSerializer.Serialize(data, DefautJsonOptions);
        }
        /// <inheritdoc cref="JsonSerializer.Serialize{TValue}(TValue, JsonSerializerOptions?)"/>
        public byte[] SerializeAsBytes<TData>(TData data)
        {
            var jsonStr = JsonSerializer.Serialize(data, DefautJsonOptions);
            return Encoding.UTF8.GetBytes(jsonStr);
        }
        /// <inheritdoc cref="JsonSerializer.Serialize(object, Type, JsonSerializerOptions?)"/>
        public byte[] SerializeAsBytes(Type type, object data)
        {
            var jsonStr = JsonSerializer.Serialize(data, type, DefautJsonOptions);
            return Encoding.UTF8.GetBytes(jsonStr);
        }
        /// <inheritdoc cref="JsonSerializer.Deserialize{TValue}(ReadOnlySpan{byte}, JsonSerializerOptions?)"/>
        public TData Deserialize<TData>(byte[] data)
        {
            return JsonSerializer.Deserialize<TData>(data, DefautJsonOptions)!;
        }
        /// <inheritdoc cref="JsonSerializer.Deserialize{TValue}(string, JsonSerializerOptions?)"/>
        public TData Deserialize<TData>(string data)
        {
            return JsonSerializer.Deserialize<TData>(data, DefautJsonOptions)!;
        }
        /// <inheritdoc cref="JsonSerializer.Deserialize(ReadOnlySpan{byte}, Type, JsonSerializerOptions?)"/>
        public object Deserialize(Type type, byte[] data)
        {
            return JsonSerializer.Deserialize(data, type, DefautJsonOptions)!;
        }
        /// <inheritdoc cref="JsonSerializer.Deserialize(string, Type, JsonSerializerOptions?)"/>
        public object Deserialize(Type type, string data)
        {
            return JsonSerializer.Deserialize(data, type, DefautJsonOptions)!;
        }
        /// <inheritdoc cref="JsonSerializer.Serialize{TValue}(Stream, TValue, JsonSerializerOptions?)"/>
        public byte[] SerializeAsByteBuffer<TData>(TData data)
        {
            var ms = new MemoryStream();
            JsonSerializer.Serialize(ms, data, DefautJsonOptions);
            return ByteBuffer.From(ms);
        }
        /// <inheritdoc cref="JsonSerializer.Serialize(Stream, object?, Type, JsonSerializerOptions?)"/>
        public byte[] SerializeAsByteBuffer(Type type, object data)
        {
            var ms = new MemoryStream();
            JsonSerializer.Serialize(ms, data, type, DefautJsonOptions);
            return ByteBuffer.From(ms);
        }
        /// <inheritdoc cref="JsonSerializer.Deserialize(Stream, JsonSerializerOptions?)"/>
        public TData Deserialize<TData>(ByteBuffer data)
        {
            return JsonSerializer.Deserialize<TData>(data.ToStream(), DefautJsonOptions)!;
        }
        /// <inheritdoc cref="JsonSerializer.Deserialize(Stream, Type, JsonSerializerOptions?)"/>
        public object Deserialize(Type type, ByteBuffer data)
        {
            return JsonSerializer.Deserialize(data.ToStream(), type, DefautJsonOptions)!;
        }
        static readonly JsonSupport _key = new();
        /// <summary>
        /// <see cref="JsonSupport"/> istance for keys
        /// </summary>
        public static JsonSupport Key => _key;

        static readonly JsonSupport _valueContainer = new();
        /// <summary>
        /// <see cref="JsonSupport"/> istance for <see cref="IValueContainer{T}"/> instances
        /// </summary>
        public static JsonSupport ValueContainer => _valueContainer;
    }

    /// <summary>
    /// Extension methods for flattened properties preparation
    /// </summary>
    public static class ComplexTypeExtension
    {
        /// <summary>
        /// Fills <paramref name="allPropertyValues"/> using information in <paramref name="propertiesInfo"/> and <paramref name="complexPropertiesInfo"/> base on <paramref name="flattenedProperties"/>
        /// </summary>
        /// <param name="flattenedProperties">The <see cref="IProperty"/> from the <see cref="ITypeBase.GetFlattenedProperties"/></param>
        /// <param name="rootType">The root <see cref="IEntityType"/></param>
        /// <param name="propertiesInfo">The data associated to the base properties</param>
        /// <param name="complexPropertiesInfo">The data associated to the first level complex properties</param>
        /// <param name="allPropertyValues">All data to be returned</param>
        public static void FillFlattened(this IProperty[] flattenedProperties, IEntityType rootType, IDictionary<IPropertyBase, object> propertiesInfo, IDictionary<IComplexProperty, object> complexPropertiesInfo, ref object[] allPropertyValues)
        {
            for (int i = 0; i < flattenedProperties.Length; i++)
            {
                var property = flattenedProperties[i];
                if (property.DeclaringType is not IEntityType entityType)
                {
                    if (property.DeclaringType is IComplexType complexType)
                    {
                        if (complexType.ComplexProperty.DeclaringType == rootType) // <- first level
                        {
                            var obj = complexPropertiesInfo[complexType.ComplexProperty];
                            var propAccessor = complexType.ClrType.GetProperty(property.Name);
                            if (propAccessor != null)
                            {
                                allPropertyValues[property.GetIndex()] = propAccessor.GetValue(obj)!;
                            }
                        }
                        else
                        {
                            System.Collections.Generic.List<IComplexProperty> traversedProperties = [];
                            var complexRoot = FindRootProperty(rootType, complexType, traversedProperties);
                            var obj = complexPropertiesInfo[complexRoot];
                            allPropertyValues[property.GetIndex()] = FindValueRecursive(obj, complexRoot.ComplexType, traversedProperties, property);
                        }
                    }
                }
                else
                {
                    allPropertyValues[property.GetIndex()] = propertiesInfo[property];
                }
            }
        }

        static IComplexProperty FindRootProperty(ITypeBase root, IComplexType complexType, System.Collections.Generic.IList<IComplexProperty> traversedProperties)
        {
            if (complexType.ComplexProperty.DeclaringType == root)
            {
                return complexType.ComplexProperty;
            }
            else
            {
                traversedProperties.Add(complexType.ComplexProperty);
                return FindRootProperty(root, complexType.ComplexProperty.DeclaringType as IComplexType, traversedProperties);
            }
        }

        static object FindValueRecursive(object complexRootValue, IComplexType complexRootType, System.Collections.Generic.IList<IComplexProperty> traversedProperties, IProperty destination)
        {
            object value = complexRootValue;
            Type type = complexRootType.ClrType;
            foreach (var item in traversedProperties)
            {
                var propAccessor = type.GetProperty(item.Name);
                if (propAccessor != null)
                {
                    value = propAccessor.GetValue(value)!;
                    if (item.ComplexType == destination.DeclaringType)
                    {
                        propAccessor = item.ClrType.GetProperty(destination.Name);
                        if (propAccessor != null)
                        {
                            return propAccessor.GetValue(value)!;
                        }
                        return null!;
                    }
                    type = item.ClrType;
                }
                else throw new InvalidOperationException($"Cannot find PropertyInfo on {item}");
            }
            return null!;
        }
    }

    /// <summary>
    /// List of <see cref="Type"/> managed from <see cref="PropertyData"/>
    /// </summary>
    public enum WellKnownManagedTypes
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
        /// <summary>
        /// Represent a generic array of <see cref="byte"/>
        /// </summary>
        Bytes,
        /// <summary>
        /// Defines a property as <see cref="IComplexType"/>
        /// </summary>
        ComplexType = 0x10000000,
        /// <summary>
        /// Defines a property as <see cref="IComplexType"/> converted into Json since it is missing its <see cref="IComplexTypeConverter"/>
        /// </summary>
        ComplexTypeAsJson = 0x20000000,
    }

    /// <summary>
    /// An helper class used to manage native and special types in serialization
    /// </summary>
    public static class NativeTypeMapper
    {
        static readonly ConcurrentDictionary<Type, (WellKnownManagedTypes, bool)> dict = new();
        static readonly ConcurrentDictionary<(WellKnownManagedTypes, bool), Type> reverseDict = new();
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
        readonly static Type BytesType = typeof(byte[]);

        static NativeTypeMapper()
        {
            dict.TryAdd(StringType, (WellKnownManagedTypes.String, true)); reverseDict.TryAdd((WellKnownManagedTypes.String, true), StringType);

            dict.TryAdd(GuidType, (WellKnownManagedTypes.Guid, false)); reverseDict.TryAdd((WellKnownManagedTypes.Guid, false), GuidType);
            dict.TryAdd(NullableGuidType, (WellKnownManagedTypes.Guid, true)); reverseDict.TryAdd((WellKnownManagedTypes.Guid, true), NullableGuidType);

            dict.TryAdd(DateTimeType, (WellKnownManagedTypes.DateTime, false)); reverseDict.TryAdd((WellKnownManagedTypes.DateTime, false), DateTimeType);
            dict.TryAdd(NullableDateTimeType, (WellKnownManagedTypes.DateTime, true)); reverseDict.TryAdd((WellKnownManagedTypes.DateTime, true), NullableDateTimeType);

            dict.TryAdd(DateTimeOffsetType, (WellKnownManagedTypes.DateTimeOffset, false)); reverseDict.TryAdd((WellKnownManagedTypes.DateTimeOffset, false), DateTimeOffsetType);
            dict.TryAdd(NullableDateTimeOffsetType, (WellKnownManagedTypes.DateTimeOffset, true)); reverseDict.TryAdd((WellKnownManagedTypes.DateTimeOffset, true), NullableDateTimeOffsetType);

            dict.TryAdd(BoolType, (WellKnownManagedTypes.Bool, false)); reverseDict.TryAdd((WellKnownManagedTypes.Bool, false), BoolType);
            dict.TryAdd(NullableBoolType, (WellKnownManagedTypes.Bool, true)); reverseDict.TryAdd((WellKnownManagedTypes.Bool, true), NullableBoolType);

            dict.TryAdd(CharType, (WellKnownManagedTypes.Char, false)); reverseDict.TryAdd((WellKnownManagedTypes.Char, false), CharType);
            dict.TryAdd(NullableCharType, (WellKnownManagedTypes.Char, true)); reverseDict.TryAdd((WellKnownManagedTypes.Char, true), NullableCharType);

            dict.TryAdd(SByteType, (WellKnownManagedTypes.SByte, false)); reverseDict.TryAdd((WellKnownManagedTypes.SByte, false), SByteType);
            dict.TryAdd(NullableSByteType, (WellKnownManagedTypes.SByte, true)); reverseDict.TryAdd((WellKnownManagedTypes.SByte, true), NullableSByteType);

            dict.TryAdd(ByteType, (WellKnownManagedTypes.Byte, false)); reverseDict.TryAdd((WellKnownManagedTypes.Byte, false), ByteType);
            dict.TryAdd(NullableByteType, (WellKnownManagedTypes.Byte, true)); reverseDict.TryAdd((WellKnownManagedTypes.Byte, true), NullableByteType);

            dict.TryAdd(ShortType, (WellKnownManagedTypes.Short, false)); reverseDict.TryAdd((WellKnownManagedTypes.Short, false), ShortType);
            dict.TryAdd(NullableShortType, (WellKnownManagedTypes.Short, true)); reverseDict.TryAdd((WellKnownManagedTypes.Short, true), NullableShortType);

            dict.TryAdd(UShortType, (WellKnownManagedTypes.UShort, false)); reverseDict.TryAdd((WellKnownManagedTypes.UShort, false), UShortType);
            dict.TryAdd(NullableUShortType, (WellKnownManagedTypes.UShort, true)); reverseDict.TryAdd((WellKnownManagedTypes.UShort, true), NullableUShortType);

            dict.TryAdd(IntType, (WellKnownManagedTypes.Int, false)); reverseDict.TryAdd((WellKnownManagedTypes.Int, false), IntType);
            dict.TryAdd(NullableIntType, (WellKnownManagedTypes.Int, true)); reverseDict.TryAdd((WellKnownManagedTypes.Int, true), NullableIntType);

            dict.TryAdd(UIntType, (WellKnownManagedTypes.UInt, false)); reverseDict.TryAdd((WellKnownManagedTypes.UInt, false), UIntType);
            dict.TryAdd(NullableUIntType, (WellKnownManagedTypes.UInt, true)); reverseDict.TryAdd((WellKnownManagedTypes.UInt, true), NullableUIntType);

            dict.TryAdd(LongType, (WellKnownManagedTypes.Long, false)); reverseDict.TryAdd((WellKnownManagedTypes.Long, false), LongType);
            dict.TryAdd(NullableLongType, (WellKnownManagedTypes.Long, true)); reverseDict.TryAdd((WellKnownManagedTypes.Long, true), NullableLongType);

            dict.TryAdd(ULongType, (WellKnownManagedTypes.ULong, false)); reverseDict.TryAdd((WellKnownManagedTypes.ULong, false), ULongType);
            dict.TryAdd(NullableULongType, (WellKnownManagedTypes.ULong, true)); reverseDict.TryAdd((WellKnownManagedTypes.ULong, true), NullableULongType);

            dict.TryAdd(DoubleType, (WellKnownManagedTypes.Double, false)); reverseDict.TryAdd((WellKnownManagedTypes.Double, false), DoubleType);
            dict.TryAdd(NullableDoubleType, (WellKnownManagedTypes.Double, true)); reverseDict.TryAdd((WellKnownManagedTypes.Double, true), NullableDoubleType);

            dict.TryAdd(FloatType, (WellKnownManagedTypes.Float, false)); reverseDict.TryAdd((WellKnownManagedTypes.Float, false), FloatType);
            dict.TryAdd(NullableFloatType, (WellKnownManagedTypes.Float, true)); reverseDict.TryAdd((WellKnownManagedTypes.Float, true), NullableFloatType);

            dict.TryAdd(DecimalType, (WellKnownManagedTypes.Decimal, false)); reverseDict.TryAdd((WellKnownManagedTypes.Decimal, false), DecimalType);
            dict.TryAdd(NullableDecimalType, (WellKnownManagedTypes.Decimal, true)); reverseDict.TryAdd((WellKnownManagedTypes.Decimal, true), NullableDecimalType);

            dict.TryAdd(BytesType, (WellKnownManagedTypes.Bytes, true)); reverseDict.TryAdd((WellKnownManagedTypes.Bytes, true), BytesType);
        }

        /// <summary>
        /// Returns a <see cref="Tuple{T1, T2}"/> of <see cref="WellKnownManagedTypes"/> with a <see cref="bool"/> indicating if the types supports <see cref="Nullable"/>
        /// </summary>
        /// <param name="type">The <see cref="Type"/> to check</param>
        /// <returns>A <see cref="Tuple{T1, T2}"/> of <see cref="WellKnownManagedTypes"/> with a <see cref="bool"/> if <paramref name="type"/> is mapped otherwise returns <see cref="WellKnownManagedTypes.Undefined"/> and <see langword="false"/> for <see cref="Nullable"/> support</returns>
        public static (WellKnownManagedTypes, bool) GetValue(Type? type)
        {
            if (type == null || !dict.TryGetValue(type, out (WellKnownManagedTypes, bool) result)) { result = (WellKnownManagedTypes.Undefined, false); }
            return result;
        }
        /// <summary>
        /// Returns a <see cref="Type"/> mapped from the <paramref name="type"/> in input
        /// </summary>
        /// <param name="type"><see cref="Tuple{T1, T2}"/> of <see cref="WellKnownManagedTypes"/> with a <see cref="bool"/> indicating if the type needs support for <see cref="Nullable"/></param>
        /// <returns>A <see cref="Type"/> associated to the <paramref name="type"/> in input, otherwise <see langword="null"/></returns>
        public static Type GetValue((WellKnownManagedTypes, bool) type)
        {
            if (!reverseDict.TryGetValue(type, out Type? result)) { result = null!; }
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
            var methodInfo = extractorType.GetMethod(nameof(LocalEntityExtractor<,,,,,>.GetEntity));
            var extractor = Activator.CreateInstance(extractorType);
            return methodInfo?.Invoke(extractor, new object[] { topic, recordKey, recordValue, throwUnmatch })!;
        }
    }
}