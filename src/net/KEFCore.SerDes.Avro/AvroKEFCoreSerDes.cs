/*
*  Copyright 2023 MASES s.r.l.
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
using MASES.EntityFrameworkCore.KNet.Serialization.Avro.Storage;
using MASES.KNet.Serialization;
using Org.Apache.Kafka.Common.Header;
using System.Text;

namespace MASES.EntityFrameworkCore.KNet.Serialization.Avro;

/// <summary>
/// Avro base class to define extensions of <see cref="KNetSerDes{T}"/>, for example <see href="https://masesgroup.github.io/KNet/articles/usageSerDes.html"/>
/// </summary>
public static class AvroKEFCoreSerDes
{
    /// <summary>
    /// Base class to define key extensions of <see cref="KNetSerDes{T}"/>, for example <see href="https://masesgroup.github.io/KNet/articles/usageSerDes.html"/>
    /// </summary>
    public static class Key
    {
        /// <summary>
        /// Avro Key Binary encoder extension of <see cref="KNetSerDes{T}"/>, for example <see href="https://masesgroup.github.io/KNet/articles/usageSerDes.html"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public class Binary<T> : KNetSerDes<T>
        {
            readonly byte[] keyTypeName = Encoding.UTF8.GetBytes(typeof(T).FullName!);
            readonly byte[] keySerDesName = Encoding.UTF8.GetBytes(typeof(Binary<>).ToAssemblyQualified());
            readonly SpecificDefaultWriter SpecificWriter = new(AvroKeyContainer._SCHEMA);
            readonly SpecificDefaultReader SpecificReader = new(AvroKeyContainer._SCHEMA, AvroKeyContainer._SCHEMA);
            readonly IKNetSerDes<T> _defaultSerDes = default!;
            /// <inheritdoc/>
            public override bool UseHeaders => true;
            /// <summary>
            /// Default initializer
            /// </summary>
            public Binary()
            {
                if (KNetSerialization.IsInternalManaged<T>())
                {
                    _defaultSerDes = new KNetSerDes<T>();
                }
                else if (!typeof(T).IsArray)
                {
                    throw new InvalidOperationException($"{typeof(Binary<>).ToAssemblyQualified()} cannot manage {typeof(T).Name}, override or build a new serializaer");
                }
            }

            /// <inheritdoc cref="KNetSerDes{T}.Serialize(string, T)"/>
            public override byte[] Serialize(string topic, T data)
            {
                return SerializeWithHeaders(topic, null!, data);
            }
            /// <inheritdoc cref="KNetSerDes{T}.SerializeWithHeaders(string, Headers, T)"/>
            public override byte[] SerializeWithHeaders(string topic, Headers headers, T data)
            {
                headers?.Add(KNetSerialization.KeyTypeIdentifier, keyTypeName);
                headers?.Add(KNetSerialization.KeySerializerIdentifier, keySerDesName);

                if (_defaultSerDes != null) return _defaultSerDes.SerializeWithHeaders(topic, headers, data);

                using MemoryStream memStream = new();
                BinaryEncoder encoder = new(memStream);
                var container = new AvroKeyContainer();
                container.PrimaryKey = new List<object>(data as object[]);
                SpecificWriter.Write(container, encoder);
                return memStream.ToArray();
            }
            /// <inheritdoc cref="KNetSerDes{T}.Deserialize(string, byte[])"/>
            public override T Deserialize(string topic, byte[] data)
            {
                return DeserializeWithHeaders(topic, null!, data);
            }
            /// <inheritdoc cref="KNetSerDes{T}.DeserializeWithHeaders(string, Headers, byte[])"/>
            public override T DeserializeWithHeaders(string topic, Headers headers, byte[] data)
            {
                if (_defaultSerDes != null) return _defaultSerDes.DeserializeWithHeaders(topic, headers, data);

                using MemoryStream memStream = new(data);
                BinaryDecoder decoder = new(memStream);
                AvroKeyContainer t = new AvroKeyContainer();
                t = SpecificReader.Read(t!, decoder);
                return (T)(object)(t.PrimaryKey.ToArray());
            }
        }

        /// <summary>
        /// Avro Key Json encoder extension of <see cref="KNetSerDes{T}"/>, for example <see href="https://masesgroup.github.io/KNet/articles/usageSerDes.html"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public class Json<T> : KNetSerDes<T>
        {
            readonly byte[] keyTypeName = Encoding.UTF8.GetBytes(typeof(T).FullName!);
            readonly byte[] keySerDesName = Encoding.UTF8.GetBytes(typeof(Json<>).ToAssemblyQualified());
            readonly SpecificDefaultWriter SpecificWriter = new(AvroKeyContainer._SCHEMA);
            readonly SpecificDefaultReader SpecificReader = new(AvroKeyContainer._SCHEMA, AvroKeyContainer._SCHEMA);
            readonly IKNetSerDes<T> _defaultSerDes = default!;
            /// <inheritdoc/>
            public override bool UseHeaders => true;
            /// <summary>
            /// Default initializer
            /// </summary>
            public Json()
            {
                if (KNetSerialization.IsInternalManaged<T>())
                {
                    _defaultSerDes = new KNetSerDes<T>();
                }
                else if (!typeof(T).IsArray)
                {
                    throw new InvalidOperationException($"{typeof(Json<>).ToAssemblyQualified()} cannot manage {typeof(T).Name}, override or build a new serializaer");
                }
            }

            /// <inheritdoc cref="KNetSerDes{T}.Serialize(string, T)"/>
            public override byte[] Serialize(string topic, T data)
            {
                return SerializeWithHeaders(topic, null!, data);
            }
            /// <inheritdoc cref="KNetSerDes{T}.SerializeWithHeaders(string, Headers, T)"/>
            public override byte[] SerializeWithHeaders(string topic, Headers headers, T data)
            {
                headers?.Add(KNetSerialization.KeyTypeIdentifier, keyTypeName);
                headers?.Add(KNetSerialization.KeySerializerIdentifier, keySerDesName);

                if (_defaultSerDes != null) return _defaultSerDes.SerializeWithHeaders(topic, headers, data);

                using MemoryStream memStream = new();
                JsonEncoder encoder = new(AvroKeyContainer._SCHEMA, memStream);
                SpecificWriter.Write(data, encoder);
                encoder.Flush();
                return memStream.ToArray();
            }
            /// <inheritdoc cref="KNetSerDes{T}.Deserialize(string, byte[])"/>
            public override T Deserialize(string topic, byte[] data)
            {
                return DeserializeWithHeaders(topic, null!, data);
            }
            /// <inheritdoc cref="KNetSerDes{T}.DeserializeWithHeaders(string, Headers, byte[])"/>
            public override T DeserializeWithHeaders(string topic, Headers headers, byte[] data)
            {
                if (_defaultSerDes != null) return _defaultSerDes.DeserializeWithHeaders(topic, headers, data);

                using MemoryStream memStream = new(data);
                JsonDecoder decoder = new(AvroKeyContainer._SCHEMA, memStream);
                T t = (T)Activator.CreateInstance(typeof(T))!;
                t = SpecificReader.Read(t!, decoder);
                return t;
            }
        }
    }

    /// <summary>
    /// Base class to define ValueContainer extensions of <see cref="KNetSerDes{T}"/>, for example <see href="https://masesgroup.github.io/KNet/articles/usageSerDes.html"/>
    /// </summary>
    public static class ValueContainer
    {
        /// <summary>
        /// Avro ValueContainer Binary encoder extension of <see cref="KNetSerDes{T}"/>, for example <see href="https://masesgroup.github.io/KNet/articles/usageSerDes.html"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public class Binary<T> : KNetSerDes<T>
        {
            readonly byte[] valueContainerSerDesName = Encoding.UTF8.GetBytes(typeof(Binary<>).ToAssemblyQualified());
            readonly byte[] valueContainerName = null!;
            readonly SpecificDefaultWriter SpecificWriter = new(AvroValueContainer._SCHEMA);
            readonly SpecificDefaultReader SpecificReader = new(AvroValueContainer._SCHEMA, AvroValueContainer._SCHEMA);
            /// <inheritdoc/>
            public override bool UseHeaders => true;
            /// <summary>
            /// Default initializer
            /// </summary>
            public Binary()
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
                        return;
                    }
                    else throw new ArgumentException($"{typeof(T).Name} does not implement IValueContainer<> and cannot be used because it is not a valid ValueContainer type");
                }
                throw new ArgumentException($"{typeof(T).Name} is not a generic type and cannot be used as a valid ValueContainer type");
            }

            /// <inheritdoc cref="KNetSerDes{T}.Serialize(string, T)"/>
            public override byte[] Serialize(string topic, T data)
            {
                return SerializeWithHeaders(topic, null!, data);
            }
            /// <inheritdoc cref="KNetSerDes{T}.SerializeWithHeaders(string, Headers, T)"/>
            public override byte[] SerializeWithHeaders(string topic, Headers headers, T data)
            {
                headers?.Add(KNetSerialization.ValueSerializerIdentifier, valueContainerSerDesName);
                headers?.Add(KNetSerialization.ValueTypeIdentifier, valueContainerName);

                using MemoryStream memStream = new();
                BinaryEncoder encoder = new(memStream);
                SpecificWriter.Write(data, encoder);
                return memStream.ToArray();
            }
            /// <inheritdoc cref="KNetSerDes{T}.Deserialize(string, byte[])"/>
            public override T Deserialize(string topic, byte[] data)
            {
                return DeserializeWithHeaders(topic, null!, data);
            }
            /// <inheritdoc cref="KNetSerDes{T}.DeserializeWithHeaders(string, Headers, byte[])"/>
            public override T DeserializeWithHeaders(string topic, Headers headers, byte[] data)
            {
                using MemoryStream memStream = new(data);
                BinaryDecoder decoder = new(memStream);
                T t = (T)Activator.CreateInstance(typeof(T))!;
                t = SpecificReader.Read(t!, decoder);
                return t;
            }
        }

        /// <summary>
        /// Avro ValueContainer Json encoder extension of <see cref="KNetSerDes{T}"/>, for example <see href="https://masesgroup.github.io/KNet/articles/usageSerDes.html"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public class Json<T> : KNetSerDes<T>
        {
            readonly byte[] valueContainerSerDesName = Encoding.UTF8.GetBytes(typeof(Json<>).ToAssemblyQualified());
            readonly byte[] valueContainerName = null!;
            readonly SpecificDefaultWriter SpecificWriter = new(AvroValueContainer._SCHEMA);
            readonly SpecificDefaultReader SpecificReader = new(AvroValueContainer._SCHEMA, AvroValueContainer._SCHEMA);
            /// <inheritdoc/>
            public override bool UseHeaders => true;
            /// <summary>
            /// Default initializer
            /// </summary>
            public Json()
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
                        return;
                    }
                    else throw new ArgumentException($"{typeof(T).Name} does not implement IValueContainer<> and cannot be used because it is not a valid ValueContainer type");
                }
                throw new ArgumentException($"{typeof(T).Name} is not a generic type and cannot be used as a valid ValueContainer type");
            }

            /// <inheritdoc cref="KNetSerDes{T}.Serialize(string, T)"/>
            public override byte[] Serialize(string topic, T data)
            {
                return SerializeWithHeaders(topic, null!, data);
            }
            /// <inheritdoc cref="KNetSerDes{T}.SerializeWithHeaders(string, Headers, T)"/>
            public override byte[] SerializeWithHeaders(string topic, Headers headers, T data)
            {
                headers?.Add(KNetSerialization.ValueSerializerIdentifier, valueContainerSerDesName);
                headers?.Add(KNetSerialization.ValueTypeIdentifier, valueContainerName);

                using MemoryStream memStream = new();
                JsonEncoder encoder = new(AvroValueContainer._SCHEMA, memStream);
                SpecificWriter.Write(data, encoder);
                encoder.Flush();
                return memStream.ToArray();
            }
            /// <inheritdoc cref="KNetSerDes{T}.Deserialize(string, byte[])"/>
            public override T Deserialize(string topic, byte[] data)
            {
                return DeserializeWithHeaders(topic, null!, data);
            }
            /// <inheritdoc cref="KNetSerDes{T}.DeserializeWithHeaders(string, Headers, byte[])"/>
            public override T DeserializeWithHeaders(string topic, Headers headers, byte[] data)
            {
                using MemoryStream memStream = new(data);
                JsonDecoder decoder = new(AvroValueContainer._SCHEMA, memStream);
                T t = (T)Activator.CreateInstance(typeof(T))!;
                t = SpecificReader.Read(t!, decoder);
                return t;
            }
        }
    }
}