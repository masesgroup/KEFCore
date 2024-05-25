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

using Java.Nio;
using MASES.EntityFrameworkCore.KNet.Serialization.Json.Storage;
using MASES.KNet.Serialization;
using Org.Apache.Kafka.Common.Header;
using System.Text;
using System.Text.Json;

namespace MASES.EntityFrameworkCore.KNet.Serialization.Json;
/// <summary>
/// Default base class to define extensions of <see cref="SerDes{TData, TJVM}"/>, for example <see href="https://masesgroup.github.io/KNet/articles/usageSerDes.html"/>
/// </summary>
public static class DefaultKEFCoreSerDes
{
    /// <summary>
    /// Returns the default serializer <see cref="Type"/> for keys
    /// </summary>
    public static readonly Type DefaultKeySerialization = typeof(Key<>);
    /// <summary>
    /// Returns the default serializer <see cref="Type"/> for value containers
    /// </summary>
    public static readonly Type DefaultValueContainerSerialization = typeof(ValueContainer<>);
    /// <summary>
    /// Returns the default <see cref="Type"/> for value containers
    /// </summary>
    public static readonly Type DefaultValueContainer = typeof(DefaultValueContainer<>);
    /// <summary>
    /// Base class to define key extensions of <see cref="ISerDesSelector{T}"/>, for example <see href="https://masesgroup.github.io/KNet/articles/usageSerDes.html"/>
    /// </summary>
    public class Key<T> : ISerDesSelector<T>
    {
        /// <summary>
        /// Returns a new instance of <see cref="Key{T}"/>
        /// </summary>
        /// <returns>The <see cref="ISerDesSelector{T}"/> of <see cref="Key{T}"/></returns>
        public static ISerDesSelector<T> NewInstance() => new Key<T>();
        /// <inheritdoc cref="ISerDesSelector.SelectorTypeName"/>
        public static string SelectorTypeName => typeof(Key<>).ToAssemblyQualified();
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
        /// Json extension of <see cref="SerDes{TData, TJVM}"/>, for example <see href="https://masesgroup.github.io/KNet/articles/usageSerDes.html"/> based on <see cref="byte"/> array
        /// </summary>
        /// <typeparam name="TData">The type to be serialized or deserialized. It can be a Primary Key or a ValueContainer like <see cref="DefaultValueContainer{TKey}"/></typeparam>
        sealed class JsonRaw<TData> : SerDesRaw<TData>
        {
            readonly byte[] keySerDesName;
            readonly byte[] keyTypeName = Encoding.UTF8.GetBytes(typeof(TData).FullName!);
            readonly ISerDesRaw<TData> _defaultSerDes = default!;
            readonly JsonSerializerOptions? _options = null;
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
                else
                {
                    _options = new JsonSerializerOptions()
                    {
                        WriteIndented = false,
                    };
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
                headers?.Add(KNetSerialization.KeyTypeIdentifier, keyTypeName);
                headers?.Add(KNetSerialization.KeySerializerIdentifier, keySerDesName);

                if (_defaultSerDes != null) return _defaultSerDes.SerializeWithHeaders(topic, headers, data);
                var jsonStr = System.Text.Json.JsonSerializer.Serialize<TData>(data);
                return Encoding.UTF8.GetBytes(jsonStr);
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

                if (data == null) return default!;
                return System.Text.Json.JsonSerializer.Deserialize<TData>(data, _options)!;
            }
        }

        /// <summary>
        /// Json extension of <see cref="SerDes{TData, TJVM}"/>, for example <see href="https://masesgroup.github.io/KNet/articles/usageSerDes.html"/> based on <see cref="ByteBuffer"/>
        /// </summary>
        /// <typeparam name="TData">The type to be serialized or deserialized. It can be a Primary Key or a ValueContainer like <see cref="DefaultValueContainer{TKey}"/></typeparam>
        sealed class JsonBuffered<TData> : SerDesBuffered<TData>
        {
            readonly byte[] keySerDesName;
            readonly byte[] keyTypeName = Encoding.UTF8.GetBytes(typeof(TData).FullName!);
            readonly ISerDesBuffered<TData> _defaultSerDes = default!;
            readonly JsonSerializerOptions? _options = null;
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
                else
                {
                    _options = new JsonSerializerOptions()
                    {
                        WriteIndented = false,
                    };
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
                headers?.Add(KNetSerialization.KeyTypeIdentifier, keyTypeName);
                headers?.Add(KNetSerialization.KeySerializerIdentifier, keySerDesName);

                if (_defaultSerDes != null) return _defaultSerDes.SerializeWithHeaders(topic, headers, data);

                var ms = new MemoryStream();
                System.Text.Json.JsonSerializer.Serialize<TData>(ms, data, _options);
                return ByteBuffer.From(ms);
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
                return System.Text.Json.JsonSerializer.Deserialize<TData>(data, _options)!;
            }
        }
    } 

    /// <summary>
    /// Base class to define ValueContainer extensions of <see cref="ISerDesSelector{T}"/>, for example <see href="https://masesgroup.github.io/KNet/articles/usageSerDes.html"/>
    /// </summary>
    public class ValueContainer<T> : ISerDesSelector<T>
    {
        /// <summary>
        /// Returns a new instance of <see cref="ValueContainer{T}"/>
        /// </summary>
        /// <returns>The <see cref="ISerDesSelector{T}"/> of <see cref="ValueContainer{T}"/></returns>
        public static ISerDesSelector<T> NewInstance() => new ValueContainer<T>();
        /// <inheritdoc cref="ISerDesSelector.SelectorTypeName"/>
        public static string SelectorTypeName => typeof(ValueContainer<>).ToAssemblyQualified();
        /// <inheritdoc cref="ISerDesSelector.ByteArraySerDes"/>
        public static Type ByteArraySerDes => typeof(JsonRaw<T>);
        /// <inheritdoc cref="ISerDesSelector{T}.NewSerDes{TJVM}"/>
        public static ISerDes<T, TJVM> NewSerDes<TJVM>()
        {
            if (typeof(TJVM) == typeof(Java.Nio.ByteBuffer)) return (ISerDes<T, TJVM>)NewByteBufferSerDes();
            return (ISerDes<T, TJVM>)NewByteArraySerDes();
        }
        /// <inheritdoc cref="ISerDesSelector.ByteBufferSerDes"/>
        public static Type ByteBufferSerDes => typeof(JsonBuffered<T>);
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
        /// Json extension of <see cref="SerDes{TData, TJVM}"/>, for example <see href="https://masesgroup.github.io/KNet/articles/usageSerDes.html"/> based on <see cref="byte"/> array
        /// </summary>
        /// <typeparam name="TData">The type to be serialized or deserialized. It can be a Primary Key or a ValueContainer like <see cref="DefaultValueContainer{TKey}"/></typeparam>
        sealed class JsonRaw<TData> : SerDesRaw<TData>
        {
            readonly byte[] valueContainerSerDesName;
            readonly byte[] valueContainerName = null!;
            readonly System.Text.Json.JsonSerializerOptions _options;
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
                        _options = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.General)
                        {
                            WriteIndented = false,
                        };
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
                headers?.Add(KNetSerialization.ValueSerializerIdentifier, valueContainerSerDesName);
                headers?.Add(KNetSerialization.ValueTypeIdentifier, valueContainerName);

                var jsonStr = System.Text.Json.JsonSerializer.Serialize<TData>(data, _options);
                return Encoding.UTF8.GetBytes(jsonStr);
            }
            /// <inheritdoc cref="SerDes{TData, TJVM}.Deserialize(string, TJVM)"/>
            public override TData Deserialize(string topic, byte[] data)
            {
                return DeserializeWithHeaders(topic, null!, data);
            }
            /// <inheritdoc cref="SerDes{TData, TJVM}.DeserializeWithHeaders(string, Headers, TJVM)"/>
            public override TData DeserializeWithHeaders(string topic, Headers headers, byte[] data)
            {
                if (data == null) return default!;
                return System.Text.Json.JsonSerializer.Deserialize<TData>(data, _options)!;
            }
        }

        /// <summary>
        /// Json extension of <see cref="SerDes{TData, TJVM}"/>, for example <see href="https://masesgroup.github.io/KNet/articles/usageSerDes.html"/> based on <see cref="ByteBuffer"/>
        /// </summary>
        /// <typeparam name="TData">The type to be serialized or deserialized. It can be a Primary Key or a ValueContainer like <see cref="DefaultValueContainer{TKey}"/></typeparam>
        sealed class JsonBuffered<TData> : SerDesBuffered<TData>
        {
            readonly byte[] valueContainerSerDesName;
            readonly byte[] valueContainerName = null!;
            readonly System.Text.Json.JsonSerializerOptions _options;
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
                        _options = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.General)
                        {
                            WriteIndented = false,
                        };
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
                headers?.Add(KNetSerialization.ValueSerializerIdentifier, valueContainerSerDesName);
                headers?.Add(KNetSerialization.ValueTypeIdentifier, valueContainerName);

                var ms = new MemoryStream();
                System.Text.Json.JsonSerializer.Serialize<TData>(ms, data, _options);
                return ByteBuffer.From(ms);
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
                return System.Text.Json.JsonSerializer.Deserialize<TData>(data, _options)!;
            }
        }
    }
}