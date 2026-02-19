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

using Avro.IO;
using Avro.Specific;
using Java.Nio;
using MASES.EntityFrameworkCore.KNet.Serialization.Avro.Storage;
using MASES.KNet.Serialization;
using Org.Apache.Kafka.Common.Header;
using System.Text;

namespace MASES.EntityFrameworkCore.KNet.Serialization.Avro;

/// <summary>
/// Avro base class to define extensions of <see cref="SerDes{TData, TJVM}"/>, for example <see href="https://masesgroup.github.io/KNet/articles/usageSerDes.html"/>
/// </summary>
public static class AvroKEFCoreSerDes
{
    /// <summary>
    /// Returns the default serializer <see cref="Type"/> for keys
    /// </summary>
    public static readonly Type DefaultKeySerialization = typeof(Key.Binary<>);
    /// <summary>
    /// Returns the default serializer <see cref="Type"/> for value containers
    /// </summary>
    public static readonly Type DefaultValueContainerSerialization = typeof(ValueContainer.Binary<>);
    /// <summary>
    /// Returns the default <see cref="Type"/> for value containers
    /// </summary>
    public static readonly Type DefaultValueContainer = typeof(AvroValueContainer<>);
    /// <summary>
    /// Base class to define key extensions of <see cref="SerDes{TData, TJVM}"/>, for example <see href="https://masesgroup.github.io/KNet/articles/usageSerDes.html"/>
    /// </summary>
    public static class Key
    {
        /// <summary>
        /// Base class to define key extensions of <see cref="ISerDesSelector{T}"/>, for example <see href="https://masesgroup.github.io/KNet/articles/usageSerDes.html"/>
        /// </summary>
        public class Binary<T> : ISerDesSelector<T>
        {
            /// <summary>
            /// Returns a new instance of <see cref="Binary{T}"/>
            /// </summary>
            /// <returns>The <see cref="ISerDesSelector{T}"/> of <see cref="Binary{T}"/></returns>
            public static ISerDesSelector<T> NewInstance() => new Binary<T>();
            /// <inheritdoc cref="ISerDesSelector.SelectorTypeName"/>
            public static string SelectorTypeName => typeof(Binary<>).ToAssemblyQualified();
            /// <inheritdoc cref="ISerDesSelector.ByteArraySerDes"/>
            public static Type ByteArraySerDes => typeof(BinaryRaw<T>);
            /// <inheritdoc cref="ISerDesSelector.ByteBufferSerDes"/>
            public static Type ByteBufferSerDes => typeof(BinaryBuffered<T>);
            /// <inheritdoc cref="ISerDesSelector{T}.NewSerDes{TJVM}"/>
            public static ISerDes<T, TJVM> NewSerDes<TJVM>()
            {
                if (typeof(TJVM) == typeof(Java.Nio.ByteBuffer)) return (ISerDes<T, TJVM>)NewByteBufferSerDes();
                return (ISerDes<T, TJVM>)NewByteArraySerDes();
            }
            /// <inheritdoc cref="ISerDesSelector{T}.NewByteArraySerDes"/>
            public static ISerDesRaw<T> NewByteArraySerDes() { return new BinaryRaw<T>(SelectorTypeName); }
            /// <inheritdoc cref="ISerDesSelector{T}.NewByteBufferSerDes"/>
            public static ISerDesBuffered<T> NewByteBufferSerDes() { return new BinaryBuffered<T>(SelectorTypeName); }

            /// <inheritdoc cref="ISerDesSelector.SelectorTypeName"/>
            string ISerDesSelector.SelectorTypeName => SelectorTypeName;
            /// <inheritdoc cref="ISerDesSelector.ByteArraySerDes"/>
            Type ISerDesSelector.ByteArraySerDes => ByteArraySerDes;
            /// <inheritdoc cref="ISerDesSelector.ByteBufferSerDes"/>
            Type ISerDesSelector.ByteBufferSerDes => ByteBufferSerDes;
            /// <inheritdoc cref="ISerDesSelector{T}.NewSerDes{TJVM}"/>
            ISerDes<T, TJVM> ISerDesSelector<T>.NewSerDes<TJVM>() => NewSerDes<TJVM>();
            /// <inheritdoc cref="ISerDesSelector{T}.NewByteArraySerDes"/>
            ISerDesRaw<T> ISerDesSelector<T>.NewByteArraySerDes() => NewByteArraySerDes();
            /// <inheritdoc cref="ISerDesSelector{T}.NewByteBufferSerDes"/>
            ISerDesBuffered<T> ISerDesSelector<T>.NewByteBufferSerDes() => NewByteBufferSerDes();

            /// <summary>
            /// Avro Key Binary encoder extension of <see cref="SerDes{TData, TJVM}"/>, for example <see href="https://masesgroup.github.io/KNet/articles/usageSerDes.html"/> based on <see cref="byte"/> array
            /// </summary>
            /// <typeparam name="TData"></typeparam>
            sealed class BinaryRaw<TData> : SerDesRaw<TData>
            {
                readonly byte[] keyTypeName = Encoding.UTF8.GetBytes(typeof(TData).FullName!);
                readonly byte[] keySerDesName;
                readonly SpecificDefaultWriter SpecificWriter = new(AvroKeyContainer._SCHEMA);
                readonly SpecificDefaultReader SpecificReader = new(AvroKeyContainer._SCHEMA, AvroKeyContainer._SCHEMA);
                readonly ISerDesRaw<TData> _defaultSerDes = default!;
                /// <inheritdoc/>
                public override bool UseHeaders => true;
                /// <summary>
                /// Default initializer
                /// </summary>
                public BinaryRaw(string selectorName)
                {
                    keySerDesName = Encoding.UTF8.GetBytes(selectorName);
                    if (KNetSerialization.IsInternalManaged<TData>())
                    {
                        _defaultSerDes = new SerDesRaw<TData>();
                    }
                    else if (!typeof(TData).IsArray)
                    {
                        throw new InvalidOperationException($"{typeof(BinaryRaw<>).ToAssemblyQualified()} cannot manage {typeof(TData).Name}, override or build a new serializaer");
                    }
                }

                /// <inheritdoc cref="SerDes{TData, TJVM}.Serialize(string, TData)"/>
                public override byte[] Serialize(string topic, TData data)
                {
                    return SerializeWithHeaders(topic, null!, data);
                }
                /// <inheritdoc cref="SerDes{TData, TJVM}.SerializeWithHeaders(string, Headers, TData)"/>
                public override byte[] SerializeWithHeaders(string topic, Headers headers, TData data)
                {
                    if (_defaultSerDes != null) return _defaultSerDes.SerializeWithHeaders(topic, headers, data);

                    headers?.Add(KNetSerialization.KeyTypeIdentifierJVM, keyTypeName);
                    headers?.Add(KNetSerialization.KeySerializerIdentifierJVM, keySerDesName);

                    if (data == null) return null!;

                    using MemoryStream memStream = new();
                    BinaryEncoder encoder = new(memStream);
                    var container = new AvroKeyContainer();
                    if (data is object[] dataArray)
                    {
                        container.PrimaryKey = new List<object>(dataArray);
                    }
                    else throw new InvalidDataException($"Cannot manage inputs different from object[], input is {data?.GetType()}");
                    SpecificWriter.Write(container, encoder);
                    return memStream.ToArray();
                }
                /// <inheritdoc cref="SerDes{TData, TJVM}.Deserialize(string, TJVM)"/>
                public override TData Deserialize(string topic, byte[] data)
                {
                    return DeserializeWithHeaders(topic, null!, data);
                }
                /// <inheritdoc cref="SerDes{TData, TJVM}.DeserializeWithHeaders(string, Headers, TJVM)"/>
                public override TData DeserializeWithHeaders(string topic, Headers headers, byte[] data)
                {
                    if (_defaultSerDes != null) return _defaultSerDes.DeserializeWithHeaders(topic, headers, data);
                    if (data == null || data.Length == 0) return default!;

                    using MemoryStream memStream = new(data);
                    BinaryDecoder decoder = new(memStream);
                    AvroKeyContainer t = new AvroKeyContainer();
                    t = SpecificReader.Read(t!, decoder);
                    return (TData)(object)(t.PrimaryKey.ToArray());
                }
            }

            /// <summary>
            /// Avro Key Binary encoder extension of <see cref="SerDes{TData, TJVM}"/>, for example <see href="https://masesgroup.github.io/KNet/articles/usageSerDes.html"/> based on <see cref="ByteBuffer"/>
            /// </summary>
            /// <typeparam name="TData"></typeparam>
            sealed class BinaryBuffered<TData> : SerDesBuffered<TData>
            {
                readonly byte[] keyTypeName = Encoding.UTF8.GetBytes(typeof(TData).FullName!);
                readonly byte[] keySerDesName;
                readonly SpecificDefaultWriter SpecificWriter = new(AvroKeyContainer._SCHEMA);
                readonly SpecificDefaultReader SpecificReader = new(AvroKeyContainer._SCHEMA, AvroKeyContainer._SCHEMA);
                readonly ISerDesBuffered<TData> _defaultSerDes = default!;
                /// <inheritdoc/>
                public override bool UseHeaders => true;
                /// <summary>
                /// Default initializer
                /// </summary>
                public BinaryBuffered(string selectorName)
                {
                    keySerDesName = Encoding.UTF8.GetBytes(selectorName);
                    if (KNetSerialization.IsInternalManaged<TData>())
                    {
                        _defaultSerDes = new SerDesBuffered<TData>();
                    }
                    else if (!typeof(TData).IsArray)
                    {
                        throw new InvalidOperationException($"{typeof(BinaryBuffered<>).ToAssemblyQualified()} cannot manage {typeof(TData).Name}, override or build a new serializaer");
                    }
                }

                /// <inheritdoc cref="SerDes{TData, TJVM}.Serialize(string, TData)"/>
                public override ByteBuffer Serialize(string topic, TData data)
                {
                    return SerializeWithHeaders(topic, null!, data);
                }
                /// <inheritdoc cref="SerDes{TData, TJVM}.SerializeWithHeaders(string, Headers, TData)"/>
                public override ByteBuffer SerializeWithHeaders(string topic, Headers headers, TData data)
                {
                    if (_defaultSerDes != null) return _defaultSerDes.SerializeWithHeaders(topic, headers, data);

                    headers?.Add(KNetSerialization.KeyTypeIdentifierJVM, keyTypeName);
                    headers?.Add(KNetSerialization.KeySerializerIdentifierJVM, keySerDesName);

                    if (data == null) return null!;

                    MemoryStream memStream = new();
                    BinaryEncoder encoder = new(memStream);
                    var container = new AvroKeyContainer();
                    if (data is object[] dataArray)
                    {
                        container.PrimaryKey = new List<object>(dataArray);
                    }
                    else throw new InvalidDataException($"Cannot manage inputs different from object[], input is {data?.GetType()}");
                    SpecificWriter.Write(container, encoder);
                    return ByteBuffer.From(memStream);
                }
                /// <inheritdoc cref="SerDes{TData, TJVM}.Deserialize(string, TJVM)"/>
                public override TData Deserialize(string topic, ByteBuffer data)
                {
                    return DeserializeWithHeaders(topic, null!, data);
                }
                /// <inheritdoc cref="SerDes{TData, TJVM}.DeserializeWithHeaders(string, Headers, TJVM)"/>
                public override TData DeserializeWithHeaders(string topic, Headers headers, ByteBuffer data)
                {
                    if (_defaultSerDes != null) return _defaultSerDes.DeserializeWithHeaders(topic, headers, data);

                    BinaryDecoder decoder = new(data);
                    AvroKeyContainer t = new AvroKeyContainer();
                    t = SpecificReader.Read(t!, decoder);
                    return (TData)(object)(t.PrimaryKey.ToArray());
                }
            }
        }

        /// <summary>
        /// Base class to define key extensions of <see cref="ISerDesSelector{T}"/>, for example <see href="https://masesgroup.github.io/KNet/articles/usageSerDes.html"/>
        /// </summary>
        public class Json<T> : ISerDesSelector<T>
        {
            /// <summary>
            /// Returns a new instance of <see cref="Json{T}"/>
            /// </summary>
            /// <returns>The <see cref="ISerDesSelector{T}"/> of <see cref="Json{T}"/></returns>
            public static ISerDesSelector<T> NewInstance() => new Json<T>();
            /// <inheritdoc cref="ISerDesSelector.SelectorTypeName"/>
            public static string SelectorTypeName => typeof(Json<>).ToAssemblyQualified();
            /// <inheritdoc cref="ISerDesSelector.ByteArraySerDes"/>
            public static Type ByteArraySerDes => typeof(JsonRaw<T>);
            /// <inheritdoc cref="ISerDesSelector.ByteBufferSerDes"/>
            public static Type ByteBufferSerDes => typeof(JsonBuffered<T>);
            /// <inheritdoc cref="ISerDesSelector{T}.NewSerDes{TJVM}"/>
            public static ISerDes<T, TJVM> NewSerDes<TJVM>()
            {
                if (typeof(TJVM) == typeof(Java.Nio.ByteBuffer)) return (ISerDes<T, TJVM>)NewByteBufferSerDes();
                return (ISerDes<T, TJVM>)NewByteArraySerDes();
            }
            /// <inheritdoc cref="ISerDesSelector{T}.NewByteArraySerDes"/>
            public static ISerDesRaw<T> NewByteArraySerDes() { return new JsonRaw<T>(SelectorTypeName); }
            /// <inheritdoc cref="ISerDesSelector{T}.NewByteBufferSerDes"/>
            public static ISerDesBuffered<T> NewByteBufferSerDes() { return new JsonBuffered<T>(SelectorTypeName); }

            /// <inheritdoc cref="ISerDesSelector.SelectorTypeName"/>
            string ISerDesSelector.SelectorTypeName => SelectorTypeName;
            /// <inheritdoc cref="ISerDesSelector.ByteArraySerDes"/>
            Type ISerDesSelector.ByteArraySerDes => ByteArraySerDes;
            /// <inheritdoc cref="ISerDesSelector.ByteBufferSerDes"/>
            Type ISerDesSelector.ByteBufferSerDes => ByteBufferSerDes;
            /// <inheritdoc cref="ISerDesSelector{T}.NewSerDes{TJVM}"/>
            ISerDes<T, TJVM> ISerDesSelector<T>.NewSerDes<TJVM>() => NewSerDes<TJVM>();
            /// <inheritdoc cref="ISerDesSelector{T}.NewByteArraySerDes"/>
            ISerDesRaw<T> ISerDesSelector<T>.NewByteArraySerDes() => NewByteArraySerDes();
            /// <inheritdoc cref="ISerDesSelector{T}.NewByteBufferSerDes"/>
            ISerDesBuffered<T> ISerDesSelector<T>.NewByteBufferSerDes() => NewByteBufferSerDes();

            /// <summary>
            /// Avro Key Json encoder extension of <see cref="SerDes{TData, TJVM}"/>, for example <see href="https://masesgroup.github.io/KNet/articles/usageSerDes.html"/> based on <see cref="byte"/> array
            /// </summary>
            /// <typeparam name="TData"></typeparam>
            sealed class JsonRaw<TData> : SerDesRaw<TData>
            {
                readonly byte[] keyTypeName = Encoding.UTF8.GetBytes(typeof(TData).FullName!);
                readonly byte[] keySerDesName;
                readonly SpecificDefaultWriter SpecificWriter = new(AvroKeyContainer._SCHEMA);
                readonly SpecificDefaultReader SpecificReader = new(AvroKeyContainer._SCHEMA, AvroKeyContainer._SCHEMA);
                readonly ISerDesRaw<TData> _defaultSerDes = default!;
                /// <inheritdoc/>
                public override bool UseHeaders => true;
                /// <summary>
                /// Default initializer
                /// </summary>
                public JsonRaw(string selectorName)
                {
                    keySerDesName = Encoding.UTF8.GetBytes(selectorName);
                    if (KNetSerialization.IsInternalManaged<TData>())
                    {
                        _defaultSerDes = new SerDesRaw<TData>();
                    }
                    else if (!typeof(TData).IsArray)
                    {
                        throw new InvalidOperationException($"{typeof(JsonRaw<>).ToAssemblyQualified()} cannot manage {typeof(TData).Name}, override or build a new serializaer");
                    }
                }

                /// <inheritdoc cref="SerDes{TData, TJVM}.Serialize(string, TData)"/>
                public override byte[] Serialize(string topic, TData data)
                {
                    return SerializeWithHeaders(topic, null!, data);
                }
                /// <inheritdoc cref="SerDes{TData, TJVM}.SerializeWithHeaders(string, Headers, TData)"/>
                public override byte[] SerializeWithHeaders(string topic, Headers headers, TData data)
                {
                    if (_defaultSerDes != null) return _defaultSerDes.SerializeWithHeaders(topic, headers, data);

                    headers?.Add(KNetSerialization.KeyTypeIdentifierJVM, keyTypeName);
                    headers?.Add(KNetSerialization.KeySerializerIdentifierJVM, keySerDesName);

                    if (data == null) return null!;

                    using MemoryStream memStream = new();
                    JsonEncoder encoder = new(AvroKeyContainer._SCHEMA, memStream);
                    SpecificWriter.Write(data, encoder);
                    encoder.Flush();
                    return memStream.ToArray();
                }
                /// <inheritdoc cref="SerDes{TData, TJVM}.Deserialize(string, TJVM)"/>
                public override TData Deserialize(string topic, byte[] data)
                {
                    return DeserializeWithHeaders(topic, null!, data);
                }
                /// <inheritdoc cref="SerDes{TData, TJVM}.DeserializeWithHeaders(string, Headers, TJVM)"/>
                public override TData DeserializeWithHeaders(string topic, Headers headers, byte[] data)
                {
                    if (_defaultSerDes != null) return _defaultSerDes.DeserializeWithHeaders(topic, headers, data);
                    if (data == null || data.Length == 0) return default!;

                    using MemoryStream memStream = new(data);
                    JsonDecoder decoder = new(AvroKeyContainer._SCHEMA, memStream);
                    TData t = Activator.CreateInstance<TData>()!;
                    t = SpecificReader.Read(t!, decoder);
                    return t;
                }
            }

            /// <summary>
            /// Avro Key Json encoder extension of <see cref="SerDes{TData, TJVM}"/>, for example <see href="https://masesgroup.github.io/KNet/articles/usageSerDes.html"/> based on <see cref="ByteBuffer"/>
            /// </summary>
            /// <typeparam name="TData"></typeparam>
            sealed class JsonBuffered<TData> : SerDesBuffered<TData>
            {
                readonly byte[] keyTypeName = Encoding.UTF8.GetBytes(typeof(TData).FullName!);
                readonly byte[] keySerDesName;
                readonly SpecificDefaultWriter SpecificWriter = new(AvroKeyContainer._SCHEMA);
                readonly SpecificDefaultReader SpecificReader = new(AvroKeyContainer._SCHEMA, AvroKeyContainer._SCHEMA);
                readonly ISerDesBuffered<TData> _defaultSerDes = default!;
                /// <inheritdoc/>
                public override bool UseHeaders => true;
                /// <summary>
                /// Default initializer
                /// </summary>
                public JsonBuffered(string selectorName)
                {
                    keySerDesName = Encoding.UTF8.GetBytes(selectorName);
                    if (KNetSerialization.IsInternalManaged<TData>())
                    {
                        _defaultSerDes = new SerDesBuffered<TData>();
                    }
                    else if (!typeof(TData).IsArray)
                    {
                        throw new InvalidOperationException($"{typeof(JsonBuffered<>).ToAssemblyQualified()} cannot manage {typeof(TData).Name}, override or build a new serializaer");
                    }
                }

                /// <inheritdoc cref="SerDes{TData, TJVM}.Serialize(string, TData)"/>
                public override ByteBuffer Serialize(string topic, TData data)
                {
                    return SerializeWithHeaders(topic, null!, data);
                }
                /// <inheritdoc cref="SerDes{TData, TJVM}.SerializeWithHeaders(string, Headers, TData)"/>
                public override ByteBuffer SerializeWithHeaders(string topic, Headers headers, TData data)
                {
                    if (_defaultSerDes != null) return _defaultSerDes.SerializeWithHeaders(topic, headers, data);

                    headers?.Add(KNetSerialization.KeyTypeIdentifierJVM, keyTypeName);
                    headers?.Add(KNetSerialization.KeySerializerIdentifierJVM, keySerDesName);

                    if (data == null) return null!;

                    MemoryStream memStream = new();
                    JsonEncoder encoder = new(AvroKeyContainer._SCHEMA, memStream);
                    SpecificWriter.Write(data, encoder);
                    encoder.Flush();
                    return ByteBuffer.From(memStream);
                }
                /// <inheritdoc cref="SerDes{TData, TJVM}.Deserialize(string, TJVM)"/>
                public override TData Deserialize(string topic, ByteBuffer data)
                {
                    return DeserializeWithHeaders(topic, null!, data);
                }
                /// <inheritdoc cref="SerDes{TData, TJVM}.DeserializeWithHeaders(string, Headers, TJVM)"/>
                public override TData DeserializeWithHeaders(string topic, Headers headers, ByteBuffer data)
                {
                    if (_defaultSerDes != null) return _defaultSerDes.DeserializeWithHeaders(topic, headers, data);

                    if (data == null) return default!;

                    JsonDecoder decoder = new(AvroKeyContainer._SCHEMA, data);
                    TData t = Activator.CreateInstance<TData>()!;
                    t = SpecificReader.Read(t!, decoder);
                    return t;
                }
            }
        }
    }

    /// <summary>
    /// Base class to define ValueContainer extensions of <see cref="ISerDesSelector{T}"/>, for example <see href="https://masesgroup.github.io/KNet/articles/usageSerDes.html"/>
    /// </summary>
    public static class ValueContainer
    {
        /// <summary>
        /// Base class to define key extensions of <see cref="ISerDesSelector{T}"/>, for example <see href="https://masesgroup.github.io/KNet/articles/usageSerDes.html"/>
        /// </summary>
        public class Binary<T> : ISerDesSelector<T>
        {
            /// <summary>
            /// Returns a new instance of <see cref="Binary{T}"/>
            /// </summary>
            /// <returns>The <see cref="ISerDesSelector{T}"/> of <see cref="Binary{T}"/></returns>
            public static ISerDesSelector<T> NewInstance() => new Binary<T>();
            /// <inheritdoc cref="ISerDesSelector.SelectorTypeName"/>
            public static string SelectorTypeName => typeof(Binary<>).ToAssemblyQualified();
            /// <inheritdoc cref="ISerDesSelector.ByteArraySerDes"/>
            public static Type ByteArraySerDes => typeof(BinaryRaw<T>);
            /// <inheritdoc cref="ISerDesSelector.ByteBufferSerDes"/>
            public static Type ByteBufferSerDes => typeof(BinaryBuffered<T>);
            /// <inheritdoc cref="ISerDesSelector{T}.NewSerDes{TJVM}"/>
            public static ISerDes<T, TJVM> NewSerDes<TJVM>()
            {
                if (typeof(TJVM) == typeof(Java.Nio.ByteBuffer)) return (ISerDes<T, TJVM>)NewByteBufferSerDes();
                return (ISerDes<T, TJVM>)NewByteArraySerDes();
            }
            /// <inheritdoc cref="ISerDesSelector{T}.NewByteArraySerDes"/>
            public static ISerDesRaw<T> NewByteArraySerDes() { return new BinaryRaw<T>(SelectorTypeName); }
            /// <inheritdoc cref="ISerDesSelector{T}.NewByteBufferSerDes"/>
            public static ISerDesBuffered<T> NewByteBufferSerDes() { return new BinaryBuffered<T>(SelectorTypeName); }

            /// <inheritdoc cref="ISerDesSelector.SelectorTypeName"/>
            string ISerDesSelector.SelectorTypeName => SelectorTypeName;
            /// <inheritdoc cref="ISerDesSelector.ByteArraySerDes"/>
            Type ISerDesSelector.ByteArraySerDes => ByteArraySerDes;
            /// <inheritdoc cref="ISerDesSelector.ByteBufferSerDes"/>
            Type ISerDesSelector.ByteBufferSerDes => ByteBufferSerDes;
            /// <inheritdoc cref="ISerDesSelector{T}.NewSerDes{TJVM}"/>
            ISerDes<T, TJVM> ISerDesSelector<T>.NewSerDes<TJVM>() => NewSerDes<TJVM>();
            /// <inheritdoc cref="ISerDesSelector{T}.NewByteArraySerDes"/>
            ISerDesRaw<T> ISerDesSelector<T>.NewByteArraySerDes() => NewByteArraySerDes();
            /// <inheritdoc cref="ISerDesSelector{T}.NewByteBufferSerDes"/>
            ISerDesBuffered<T> ISerDesSelector<T>.NewByteBufferSerDes() => NewByteBufferSerDes();

            /// <summary>
            /// Avro ValueContainer Binary encoder extension of <see cref="SerDes{TData, TJVM}"/>, for example <see href="https://masesgroup.github.io/KNet/articles/usageSerDes.html"/> based on <see cref="byte"/> array
            /// </summary>
            /// <typeparam name="TData"></typeparam>
            sealed class BinaryRaw<TData> : SerDesRaw<TData>
            {
                readonly byte[] valueContainerSerDesName;
                readonly byte[] valueContainerName = null!;
                readonly SpecificDefaultWriter SpecificWriter = new(AvroValueContainer._SCHEMA);
                readonly SpecificDefaultReader SpecificReader = new(AvroValueContainer._SCHEMA, AvroValueContainer._SCHEMA);
                /// <inheritdoc/>
                public override bool UseHeaders => true;
                /// <summary>
                /// Default initializer
                /// </summary>
                public BinaryRaw(string selectorName)
                {
                    valueContainerSerDesName = Encoding.UTF8.GetBytes(selectorName);
                    var tt = typeof(TData);
                    if (tt.IsGenericType)
                    {
                        var keyT = tt.GetGenericArguments();
                        if (keyT.Length != 1) { throw new ArgumentException($"{typeof(TData).Name} does not contains a single generic argument and cannot be used because it is not a valid ValueContainer type"); }
                        var t = tt.GetGenericTypeDefinition();
                        if (t.GetInterface(typeof(IValueContainer<>).Name) != null)
                        {
                            valueContainerName = Encoding.UTF8.GetBytes(t.ToAssemblyQualified());
                            return;
                        }
                        else throw new ArgumentException($"{typeof(TData).Name} does not implement IValueContainer<> and cannot be used because it is not a valid ValueContainer type");
                    }
                    throw new ArgumentException($"{typeof(TData).Name} is not a generic type and cannot be used as a valid ValueContainer type");
                }

                /// <inheritdoc cref="SerDes{TData, TJVM}.Serialize(string, TData)"/>
                public override byte[] Serialize(string topic, TData data)
                {
                    return SerializeWithHeaders(topic, null!, data);
                }
                /// <inheritdoc cref="SerDes{TData, TJVM}.SerializeWithHeaders(string, Headers, TData)"/>
                public override byte[] SerializeWithHeaders(string topic, Headers headers, TData data)
                {
                    headers?.Add(KNetSerialization.ValueSerializerIdentifierJVM, valueContainerSerDesName);
                    headers?.Add(KNetSerialization.ValueTypeIdentifierJVM, valueContainerName);

                    if (data == null) return null!;

                    using MemoryStream memStream = new();
                    BinaryEncoder encoder = new(memStream);
                    SpecificWriter.Write(data, encoder);
                    return memStream.ToArray();
                }
                /// <inheritdoc cref="SerDes{TData, TJVM}.Deserialize(string, TJVM)"/>
                public override TData Deserialize(string topic, byte[] data)
                {
                    return DeserializeWithHeaders(topic, null!, data);
                }
                /// <inheritdoc cref="SerDes{TData, TJVM}.DeserializeWithHeaders(string, Headers, TJVM)"/>
                public override TData DeserializeWithHeaders(string topic, Headers headers, byte[] data)
                {
                    if (data == null || data.Length == 0) return default!;

                    using MemoryStream memStream = new(data);
                    BinaryDecoder decoder = new(memStream);
                    TData t = Activator.CreateInstance<TData>()!;
                    t = SpecificReader.Read(t!, decoder);
                    return t;
                }
            }

            /// <summary>
            /// Avro ValueContainer Binary encoder extension of <see cref="SerDes{TData, TJVM}"/>, for example <see href="https://masesgroup.github.io/KNet/articles/usageSerDes.html"/> based on <see cref="ByteBuffer"/>
            /// </summary>
            /// <typeparam name="TData"></typeparam>
            sealed class BinaryBuffered<TData> : SerDesBuffered<TData>
            {
                readonly byte[] valueContainerSerDesName;
                readonly byte[] valueContainerName = null!;
                readonly SpecificDefaultWriter SpecificWriter = new(AvroValueContainer._SCHEMA);
                readonly SpecificDefaultReader SpecificReader = new(AvroValueContainer._SCHEMA, AvroValueContainer._SCHEMA);
                /// <inheritdoc/>
                public override bool UseHeaders => true;
                /// <summary>
                /// Default initializer
                /// </summary>
                public BinaryBuffered(string selectorName)
                {
                    valueContainerSerDesName = Encoding.UTF8.GetBytes(selectorName);
                    var tt = typeof(TData);
                    if (tt.IsGenericType)
                    {
                        var keyT = tt.GetGenericArguments();
                        if (keyT.Length != 1) { throw new ArgumentException($"{typeof(TData).Name} does not contains a single generic argument and cannot be used because it is not a valid ValueContainer type"); }
                        var t = tt.GetGenericTypeDefinition();
                        if (t.GetInterface(typeof(IValueContainer<>).Name) != null)
                        {
                            valueContainerName = Encoding.UTF8.GetBytes(t.ToAssemblyQualified());
                            return;
                        }
                        else throw new ArgumentException($"{typeof(TData).Name} does not implement IValueContainer<> and cannot be used because it is not a valid ValueContainer type");
                    }
                    throw new ArgumentException($"{typeof(TData).Name} is not a generic type and cannot be used as a valid ValueContainer type");
                }

                /// <inheritdoc cref="SerDes{TData, TJVM}.Serialize(string, TData)"/>
                public override ByteBuffer Serialize(string topic, TData data)
                {
                    return SerializeWithHeaders(topic, null!, data);
                }
                /// <inheritdoc cref="SerDes{TData, TJVM}.SerializeWithHeaders(string, Headers, TData)"/>
                public override ByteBuffer SerializeWithHeaders(string topic, Headers headers, TData data)
                {
                    headers?.Add(KNetSerialization.ValueSerializerIdentifierJVM, valueContainerSerDesName);
                    headers?.Add(KNetSerialization.ValueTypeIdentifierJVM, valueContainerName);

                    if (data == null) return null!;

                    MemoryStream memStream = new();
                    BinaryEncoder encoder = new(memStream);
                    SpecificWriter.Write(data, encoder);
                    return ByteBuffer.From(memStream);
                }
                /// <inheritdoc cref="SerDes{TData, TJVM}.Deserialize(string, TJVM)"/>
                public override TData Deserialize(string topic, ByteBuffer data)
                {
                    return DeserializeWithHeaders(topic, null!, data);
                }
                /// <inheritdoc cref="SerDes{TData, TJVM}.DeserializeWithHeaders(string, Headers, TJVM)"/>
                public override TData DeserializeWithHeaders(string topic, Headers headers, ByteBuffer data)
                {
                    if (data == null) return default!;

                    BinaryDecoder decoder = new(data);
                    TData t = Activator.CreateInstance<TData>()!;
                    t = SpecificReader.Read(t!, decoder);
                    return t;
                }
            }
        }

        /// <summary>
        /// Base class to define key extensions of <see cref="ISerDesSelector{T}"/>, for example <see href="https://masesgroup.github.io/KNet/articles/usageSerDes.html"/>
        /// </summary>
        public class Json<T> : ISerDesSelector<T>
        {
            /// <summary>
            /// Returns a new instance of <see cref="Json{T}"/>
            /// </summary>
            /// <returns>The <see cref="ISerDesSelector{T}"/> of <see cref="Json{T}"/></returns>
            public static ISerDesSelector<T> NewInstance() => new Json<T>();
            /// <inheritdoc cref="ISerDesSelector.SelectorTypeName"/>
            public static string SelectorTypeName => typeof(Json<>).ToAssemblyQualified();
            /// <inheritdoc cref="ISerDesSelector.ByteArraySerDes"/>
            public static Type ByteArraySerDes => typeof(JsonRaw<T>);
            /// <inheritdoc cref="ISerDesSelector.ByteBufferSerDes"/>
            public static Type ByteBufferSerDes => typeof(JsonBuffered<T>);
            /// <inheritdoc cref="ISerDesSelector{T}.NewSerDes{TJVM}"/>
            public static ISerDes<T, TJVM> NewSerDes<TJVM>()
            {
                if (typeof(TJVM) == typeof(Java.Nio.ByteBuffer)) return (ISerDes<T, TJVM>)NewByteBufferSerDes();
                return (ISerDes<T, TJVM>)NewByteArraySerDes();
            }
            /// <inheritdoc cref="ISerDesSelector{T}.NewByteArraySerDes"/>
            public static ISerDesRaw<T> NewByteArraySerDes() { return new JsonRaw<T>(SelectorTypeName); }
            /// <inheritdoc cref="ISerDesSelector{T}.NewByteBufferSerDes"/>
            public static ISerDesBuffered<T> NewByteBufferSerDes() { return new JsonBuffered<T>(SelectorTypeName); }

            /// <inheritdoc cref="ISerDesSelector.SelectorTypeName"/>
            string ISerDesSelector.SelectorTypeName => SelectorTypeName;
            /// <inheritdoc cref="ISerDesSelector.ByteArraySerDes"/>
            Type ISerDesSelector.ByteArraySerDes => ByteArraySerDes;
            /// <inheritdoc cref="ISerDesSelector.ByteBufferSerDes"/>
            Type ISerDesSelector.ByteBufferSerDes => ByteBufferSerDes;
            /// <inheritdoc cref="ISerDesSelector{T}.NewSerDes{TJVM}"/>
            ISerDes<T, TJVM> ISerDesSelector<T>.NewSerDes<TJVM>() => NewSerDes<TJVM>();
            /// <inheritdoc cref="ISerDesSelector{T}.NewByteArraySerDes"/>
            ISerDesRaw<T> ISerDesSelector<T>.NewByteArraySerDes() => NewByteArraySerDes();
            /// <inheritdoc cref="ISerDesSelector{T}.NewByteBufferSerDes"/>
            ISerDesBuffered<T> ISerDesSelector<T>.NewByteBufferSerDes() => NewByteBufferSerDes();

            /// <summary>
            /// Avro ValueContainer Json encoder extension of <see cref="SerDes{TData, TJVM}"/>, for example <see href="https://masesgroup.github.io/KNet/articles/usageSerDes.html"/> based on <see cref="byte"/> array
            /// </summary>
            /// <typeparam name="TData"></typeparam>
            sealed class JsonRaw<TData> : SerDesRaw<TData>
            {
                readonly byte[] valueContainerSerDesName;
                readonly byte[] valueContainerName = null!;
                readonly SpecificDefaultWriter SpecificWriter = new(AvroValueContainer._SCHEMA);
                readonly SpecificDefaultReader SpecificReader = new(AvroValueContainer._SCHEMA, AvroValueContainer._SCHEMA);
                /// <inheritdoc/>
                public override bool UseHeaders => true;
                /// <summary>
                /// Default initializer
                /// </summary>
                public JsonRaw(string selectorName)
                {
                    valueContainerSerDesName = Encoding.UTF8.GetBytes(selectorName);
                    var tt = typeof(TData);
                    if (tt.IsGenericType)
                    {
                        var keyT = tt.GetGenericArguments();
                        if (keyT.Length != 1) { throw new ArgumentException($"{typeof(TData).Name} does not contains a single generic argument and cannot be used because it is not a valid ValueContainer type"); }
                        var t = tt.GetGenericTypeDefinition();
                        if (t.GetInterface(typeof(IValueContainer<>).Name) != null)
                        {
                            valueContainerName = Encoding.UTF8.GetBytes(t.ToAssemblyQualified());
                            return;
                        }
                        else throw new ArgumentException($"{typeof(TData).Name} does not implement IValueContainer<> and cannot be used because it is not a valid ValueContainer type");
                    }
                    throw new ArgumentException($"{typeof(TData).Name} is not a generic type and cannot be used as a valid ValueContainer type");
                }

                /// <inheritdoc cref="SerDes{TData, TJVM}.Serialize(string, TData)"/>
                public override byte[] Serialize(string topic, TData data)
                {
                    return SerializeWithHeaders(topic, null!, data);
                }
                /// <inheritdoc cref="SerDes{TData, TJVM}.SerializeWithHeaders(string, Headers, TData)"/>
                public override byte[] SerializeWithHeaders(string topic, Headers headers, TData data)
                {
                    headers?.Add(KNetSerialization.ValueSerializerIdentifierJVM, valueContainerSerDesName);
                    headers?.Add(KNetSerialization.ValueTypeIdentifierJVM, valueContainerName);

                    if (data == null) return null!;

                    using MemoryStream memStream = new();
                    JsonEncoder encoder = new(AvroValueContainer._SCHEMA, memStream);
                    SpecificWriter.Write(data, encoder);
                    encoder.Flush();
                    return memStream.ToArray();
                }
                /// <inheritdoc cref="SerDes{TData, TJVM}.Deserialize(string, TJVM)"/>
                public override TData Deserialize(string topic, byte[] data)
                {
                    return DeserializeWithHeaders(topic, null!, data);
                }
                /// <inheritdoc cref="SerDes{TData, TJVM}.DeserializeWithHeaders(string, Headers, TJVM)"/>
                public override TData DeserializeWithHeaders(string topic, Headers headers, byte[] data)
                {
                    if (data == null || data.Length == 0) return default!;

                    using MemoryStream memStream = new(data);
                    JsonDecoder decoder = new(AvroValueContainer._SCHEMA, memStream);
                    TData t = Activator.CreateInstance<TData>()!;
                    t = SpecificReader.Read(t!, decoder);
                    return t;
                }
            }

            /// <summary>
            /// Avro ValueContainer Json encoder extension of <see cref="SerDes{TData, TJVM}"/>, for example <see href="https://masesgroup.github.io/KNet/articles/usageSerDes.html"/> based on <see cref="ByteBuffer"/>
            /// </summary>
            /// <typeparam name="TData"></typeparam>
            sealed class JsonBuffered<TData> : SerDesBuffered<TData>
            {
                readonly byte[] valueContainerSerDesName;
                readonly byte[] valueContainerName = null!;
                readonly SpecificDefaultWriter SpecificWriter = new(AvroValueContainer._SCHEMA);
                readonly SpecificDefaultReader SpecificReader = new(AvroValueContainer._SCHEMA, AvroValueContainer._SCHEMA);
                /// <inheritdoc/>
                public override bool UseHeaders => true;
                /// <summary>
                /// Default initializer
                /// </summary>
                public JsonBuffered(string selectorName)
                {
                    valueContainerSerDesName = Encoding.UTF8.GetBytes(selectorName);
                    var tt = typeof(TData);
                    if (tt.IsGenericType)
                    {
                        var keyT = tt.GetGenericArguments();
                        if (keyT.Length != 1) { throw new ArgumentException($"{typeof(TData).Name} does not contains a single generic argument and cannot be used because it is not a valid ValueContainer type"); }
                        var t = tt.GetGenericTypeDefinition();
                        if (t.GetInterface(typeof(IValueContainer<>).Name) != null)
                        {
                            valueContainerName = Encoding.UTF8.GetBytes(t.ToAssemblyQualified());
                            return;
                        }
                        else throw new ArgumentException($"{typeof(TData).Name} does not implement IValueContainer<> and cannot be used because it is not a valid ValueContainer type");
                    }
                    throw new ArgumentException($"{typeof(TData).Name} is not a generic type and cannot be used as a valid ValueContainer type");
                }

                /// <inheritdoc cref="SerDes{TData, TJVM}.Serialize(string, TData)"/>
                public override ByteBuffer Serialize(string topic, TData data)
                {
                    return SerializeWithHeaders(topic, null!, data);
                }
                /// <inheritdoc cref="SerDes{TData, TJVM}.SerializeWithHeaders(string, Headers, TData)"/>
                public override ByteBuffer SerializeWithHeaders(string topic, Headers headers, TData data)
                {
                    headers?.Add(KNetSerialization.ValueSerializerIdentifierJVM, valueContainerSerDesName);
                    headers?.Add(KNetSerialization.ValueTypeIdentifierJVM, valueContainerName);

                    if (data == null) return null!;

                    MemoryStream memStream = new();
                    JsonEncoder encoder = new(AvroValueContainer._SCHEMA, memStream);
                    SpecificWriter.Write(data, encoder);
                    encoder.Flush();
                    return ByteBuffer.From(memStream);
                }
                /// <inheritdoc cref="SerDes{TData, TJVM}.Deserialize(string, TJVM)"/>
                public override TData Deserialize(string topic, ByteBuffer data)
                {
                    return DeserializeWithHeaders(topic, null!, data);
                }
                /// <inheritdoc cref="SerDes{TData, TJVM}.DeserializeWithHeaders(string, Headers, TJVM)"/>
                public override TData DeserializeWithHeaders(string topic, Headers headers, ByteBuffer data)
                {
                    if (data == null) return default!;

                    JsonDecoder decoder = new(AvroValueContainer._SCHEMA, data);
                    TData t = Activator.CreateInstance<TData>()!;
                    t = SpecificReader.Read(t!, decoder);
                    return t;
                }
            }
        }
    }
}