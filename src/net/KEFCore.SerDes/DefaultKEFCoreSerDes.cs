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
/// Default base class to define extensions of <see cref="SerDes{T, TJVM}"/>, for example <see href="https://masesgroup.github.io/KNet/articles/usageSerDes.html"/>
/// </summary>
public static class DefaultKEFCoreSerDes
{
    /// <summary>
    /// Returns the default serializer <see cref="Type"/> for keys
    /// </summary>
    public static readonly Type DefaultKeySerialization = typeof(Key.JsonRaw<>);
    /// <summary>
    /// Returns the default serializer <see cref="Type"/> for value containers
    /// </summary>
    public static readonly Type DefaultValueContainerSerialization = typeof(ValueContainer.JsonRaw<>);
    /// <summary>
    /// Returns the default <see cref="Type"/> for value containers
    /// </summary>
    public static readonly Type DefaultValueContainer = typeof(DefaultValueContainer<>);
    /// <summary>
    /// Base class to define key extensions of <see cref="SerDes{T, TJVM}"/>, for example <see href="https://masesgroup.github.io/KNet/articles/usageSerDes.html"/>
    /// </summary>
    public static class Key
    {
        /// <summary>
        /// Json extension of <see cref="SerDes{T, TJVM}"/>, for example <see href="https://masesgroup.github.io/KNet/articles/usageSerDes.html"/> based on <see cref="byte"/> array
        /// </summary>
        /// <typeparam name="T">The type to be serialized or deserialized. It can be a Primary Key or a ValueContainer like <see cref="DefaultValueContainer{TKey}"/></typeparam>
        public class JsonRaw<T> : SerDesRaw<T>
        {
            readonly byte[] keySerDesName = Encoding.UTF8.GetBytes(typeof(JsonRaw<>).ToAssemblyQualified());
            readonly byte[] keyTypeName = Encoding.UTF8.GetBytes(typeof(T).FullName!);
            readonly ISerDesRaw<T> _defaultSerDes = default!;
            readonly JsonSerializerOptions? _options = null;
            /// <inheritdoc/>
            public override bool UseHeaders => true;
            /// <summary>
            /// Default initializer
            /// </summary>
            public JsonRaw()
            {
                if (KNetSerialization.IsInternalManaged<T>())
                {
                    _defaultSerDes = new SerDesRaw<T>();
                }
                else if (!typeof(T).IsArray)
                {
                    throw new InvalidOperationException($"{typeof(JsonRaw<>).ToAssemblyQualified()} cannot manage {typeof(T).Name}, override or build a new serializaer");
                }
                else
                {
                    _options = new JsonSerializerOptions()
                    {
                        WriteIndented = false,
                    };
                }
            }

            /// <inheritdoc cref="SerDes{T, TJVM}.Serialize(string, T)"/>
            public override byte[] Serialize(string topic, T data)
            {
                return SerializeWithHeaders(topic, null!, data);
            }
            /// <inheritdoc cref="SerDes{T, TJVM}.SerializeWithHeaders(string, Headers, T)"/>
            public override byte[] SerializeWithHeaders(string topic, Headers headers, T data)
            {
                headers?.Add(KNetSerialization.KeyTypeIdentifier, keyTypeName);
                headers?.Add(KNetSerialization.KeySerializerIdentifier, keySerDesName);

                if (_defaultSerDes != null) return _defaultSerDes.SerializeWithHeaders(topic, headers, data);
                var jsonStr = System.Text.Json.JsonSerializer.Serialize<T>(data);
                return Encoding.UTF8.GetBytes(jsonStr);
            }
            /// <inheritdoc cref="SerDes{T, TJVM}.Deserialize(string, TJVM)"/>
            public override T Deserialize(string topic, byte[] data)
            {
                return DeserializeWithHeaders(topic, null!, data);
            }
            /// <inheritdoc cref="SerDes{T, TJVM}.DeserializeWithHeaders(string, Headers, TJVM)"/>
            public override T DeserializeWithHeaders(string topic, Headers headers, byte[] data)
            {
                if (_defaultSerDes != null) return _defaultSerDes.DeserializeWithHeaders(topic, headers, data);

                if (data == null) return default!;
                return System.Text.Json.JsonSerializer.Deserialize<T>(data, _options)!;
            }
        }

        /// <summary>
        /// Json extension of <see cref="SerDes{T, TJVM}"/>, for example <see href="https://masesgroup.github.io/KNet/articles/usageSerDes.html"/> based on <see cref="ByteBuffer"/>
        /// </summary>
        /// <typeparam name="T">The type to be serialized or deserialized. It can be a Primary Key or a ValueContainer like <see cref="DefaultValueContainer{TKey}"/></typeparam>
        public class JsonBuffered<T> : SerDesBuffered<T>
        {
            readonly byte[] keySerDesName = Encoding.UTF8.GetBytes(typeof(JsonBuffered<>).ToAssemblyQualified());
            readonly byte[] keyTypeName = Encoding.UTF8.GetBytes(typeof(T).FullName!);
            readonly ISerDesBuffered<T> _defaultSerDes = default!;
            readonly JsonSerializerOptions? _options = null;
            /// <inheritdoc/>
            public override bool UseHeaders => true;
            /// <summary>
            /// Default initializer
            /// </summary>
            public JsonBuffered()
            {
                if (KNetSerialization.IsInternalManaged<T>())
                {
                    _defaultSerDes = new SerDesBuffered<T>();
                }
                else if (!typeof(T).IsArray)
                {
                    throw new InvalidOperationException($"{typeof(JsonBuffered<>).ToAssemblyQualified()} cannot manage {typeof(T).Name}, override or build a new serializaer");
                }
                else
                {
                    _options = new JsonSerializerOptions()
                    {
                        WriteIndented = false,
                    };
                }
            }

            /// <inheritdoc cref="SerDes{T, TJVM}.Serialize(string, T)"/>
            public override ByteBuffer Serialize(string topic, T data)
            {
                return SerializeWithHeaders(topic, null!, data);
            }
            /// <inheritdoc cref="SerDes{T, TJVM}.SerializeWithHeaders(string, Headers, T)"/>
            public override ByteBuffer SerializeWithHeaders(string topic, Headers headers, T data)
            {
                headers?.Add(KNetSerialization.KeyTypeIdentifier, keyTypeName);
                headers?.Add(KNetSerialization.KeySerializerIdentifier, keySerDesName);

                if (_defaultSerDes != null) return _defaultSerDes.SerializeWithHeaders(topic, headers, data);

                var ms = new MemoryStream();
                System.Text.Json.JsonSerializer.Serialize<T>(ms, data, _options);
                return ByteBuffer.From(ms);
            }
            /// <inheritdoc cref="SerDes{T, TJVM}.Deserialize(string, TJVM)"/>
            public override T Deserialize(string topic, ByteBuffer data)
            {
                return DeserializeWithHeaders(topic, null!, data);
            }
            /// <inheritdoc cref="SerDes{T, TJVM}.DeserializeWithHeaders(string, Headers, TJVM)"/>
            public override T DeserializeWithHeaders(string topic, Headers headers, ByteBuffer data)
            {
                if (_defaultSerDes != null) return _defaultSerDes.DeserializeWithHeaders(topic, headers, data);

                if (data == null) return default!;
                return System.Text.Json.JsonSerializer.Deserialize<T>(data, _options)!;
            }
        }
    }

    /// <summary>
    /// Base class to define ValueContainer extensions of <see cref="SerDes{T, TJVM}"/>, for example <see href="https://masesgroup.github.io/KNet/articles/usageSerDes.html"/>
    /// </summary>
    public static class ValueContainer
    {
        /// <summary>
        /// Json extension of <see cref="SerDes{T, TJVM}"/>, for example <see href="https://masesgroup.github.io/KNet/articles/usageSerDes.html"/> based on <see cref="byte"/> array
        /// </summary>
        /// <typeparam name="T">The type to be serialized or deserialized. It can be a Primary Key or a ValueContainer like <see cref="DefaultValueContainer{TKey}"/></typeparam>
        public class JsonRaw<T> : SerDesRaw<T>
        {
            readonly byte[] valueContainerSerDesName = Encoding.UTF8.GetBytes(typeof(JsonRaw<>).ToAssemblyQualified());
            readonly byte[] valueContainerName = null!;
            readonly System.Text.Json.JsonSerializerOptions _options;
            /// <inheritdoc/>
            public override bool UseHeaders => true;
            /// <summary>
            /// Default initializer
            /// </summary>
            public JsonRaw()
            {
                var tt = typeof(T);
                if (tt.IsGenericType)
                {
                    var keyT = tt.GetGenericArguments();
                    if (keyT.Length != 1) { throw new ArgumentException($"{typeof(T).Name} does not contains a single generic argument and cannot be used because it is not a valid ValueContainer type"); }
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
                    else throw new ArgumentException($"{typeof(T).Name} does not implement IValueContainer<> and cannot be used because it is not a valid ValueContainer type");
                }
                throw new ArgumentException($"{typeof(T).Name} is not a generic type and cannot be used as a valid ValueContainer type");
            }

            /// <inheritdoc cref="SerDes{T, TJVM}.Serialize(string, T)"/>
            public override byte[] Serialize(string topic, T data)
            {
                return SerializeWithHeaders(topic, null!, data);
            }
            /// <inheritdoc cref="SerDes{T, TJVM}.SerializeWithHeaders(string, Headers, T)"/>
            public override byte[] SerializeWithHeaders(string topic, Headers headers, T data)
            {
                headers?.Add(KNetSerialization.ValueSerializerIdentifier, valueContainerSerDesName);
                headers?.Add(KNetSerialization.ValueTypeIdentifier, valueContainerName);

                var jsonStr = System.Text.Json.JsonSerializer.Serialize<T>(data, _options);
                return Encoding.UTF8.GetBytes(jsonStr);
            }
            /// <inheritdoc cref="SerDes{T, TJVM}.Deserialize(string, TJVM)"/>
            public override T Deserialize(string topic, byte[] data)
            {
                return DeserializeWithHeaders(topic, null!, data);
            }
            /// <inheritdoc cref="SerDes{T, TJVM}.DeserializeWithHeaders(string, Headers, TJVM)"/>
            public override T DeserializeWithHeaders(string topic, Headers headers, byte[] data)
            {
                if (data == null) return default!;
                return System.Text.Json.JsonSerializer.Deserialize<T>(data, _options)!;
            }
        }

        /// <summary>
        /// Json extension of <see cref="SerDes{T, TJVM}"/>, for example <see href="https://masesgroup.github.io/KNet/articles/usageSerDes.html"/> based on <see cref="ByteBuffer"/>
        /// </summary>
        /// <typeparam name="T">The type to be serialized or deserialized. It can be a Primary Key or a ValueContainer like <see cref="DefaultValueContainer{TKey}"/></typeparam>
        public class JsonBuffered<T> : SerDesBuffered<T>
        {
            readonly byte[] valueContainerSerDesName = Encoding.UTF8.GetBytes(typeof(JsonBuffered<>).ToAssemblyQualified());
            readonly byte[] valueContainerName = null!;
            readonly System.Text.Json.JsonSerializerOptions _options;
            /// <inheritdoc/>
            public override bool UseHeaders => true;
            /// <summary>
            /// Default initializer
            /// </summary>
            public JsonBuffered()
            {
                var tt = typeof(T);
                if (tt.IsGenericType)
                {
                    var keyT = tt.GetGenericArguments();
                    if (keyT.Length != 1) { throw new ArgumentException($"{typeof(T).Name} does not contains a single generic argument and cannot be used because it is not a valid ValueContainer type"); }
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
                    else throw new ArgumentException($"{typeof(T).Name} does not implement IValueContainer<> and cannot be used because it is not a valid ValueContainer type");
                }
                throw new ArgumentException($"{typeof(T).Name} is not a generic type and cannot be used as a valid ValueContainer type");
            }

            /// <inheritdoc cref="SerDes{T, TJVM}.Serialize(string, T)"/>
            public override ByteBuffer Serialize(string topic, T data)
            {
                return SerializeWithHeaders(topic, null!, data);
            }
            /// <inheritdoc cref="SerDes{T, TJVM}.SerializeWithHeaders(string, Headers, T)"/>
            public override ByteBuffer SerializeWithHeaders(string topic, Headers headers, T data)
            {
                headers?.Add(KNetSerialization.ValueSerializerIdentifier, valueContainerSerDesName);
                headers?.Add(KNetSerialization.ValueTypeIdentifier, valueContainerName);

                var ms = new MemoryStream();
                System.Text.Json.JsonSerializer.Serialize<T>(ms, data, _options);
                return ByteBuffer.From(ms);
            }
            /// <inheritdoc cref="SerDes{T, TJVM}.Deserialize(string, TJVM)"/>
            public override T Deserialize(string topic, ByteBuffer data)
            {
                return DeserializeWithHeaders(topic, null!, data);
            }
            /// <inheritdoc cref="SerDes{T, TJVM}.DeserializeWithHeaders(string, Headers, TJVM)"/>
            public override T DeserializeWithHeaders(string topic, Headers headers, ByteBuffer data)
            {
                if (data == null) return default!;
                return System.Text.Json.JsonSerializer.Deserialize<T>(data, _options)!;
            }
        }
    }
}