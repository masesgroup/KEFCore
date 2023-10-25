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

using Google.Protobuf;
using MASES.EntityFrameworkCore.KNet.Serialization.Protobuf.Storage;
using MASES.KNet.Serialization;
using Org.Apache.Kafka.Common.Header;
using System.Text;

namespace MASES.EntityFrameworkCore.KNet.Serialization.Protobuf;

/// <summary>
/// Protobuf base class to define extensions of <see cref="KNetSerDes{T}"/>, for example <see href="https://masesgroup.github.io/KNet/articles/usageSerDes.html"/>
/// </summary>
public static class ProtobufKEFCoreSerDes
{
    /// <summary>
    /// Avro Key Binary encoder extension of <see cref="KNetSerDes{T}"/>, for example <see href="https://masesgroup.github.io/KNet/articles/usageSerDes.html"/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class Key<T> : KNetSerDes<T>
    {
        readonly byte[] keyTypeName = Encoding.UTF8.GetBytes(typeof(T).FullName!);
        readonly byte[] keySerDesName = Encoding.UTF8.GetBytes(typeof(Key<>).ToAssemblyQualified());
        readonly IKNetSerDes<T> _defaultSerDes = default!;
        /// <inheritdoc/>
        public override bool UseHeaders => true;
        /// <summary>
        /// Default initializer
        /// </summary>
        public Key()
        {
            if (KNetSerialization.IsInternalManaged<T>())
            {
                _defaultSerDes = new KNetSerDes<T>();
            }
            else if (!typeof(T).IsArray)
            {
                throw new InvalidOperationException($"{typeof(Key<>).ToAssemblyQualified()} cannot manage {typeof(T).Name}, override or build a new serializaer");
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
            ProtobufKeyContainer keyContainer = null!;
            if (data is object[] dataArray)
            {
                keyContainer = new ProtobufKeyContainer(dataArray);
            }

            using (MemoryStream stream = new())
            {
                keyContainer.WriteTo(stream);
                return stream.ToArray();
            }
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

            if (data == null) return default!;

            ProtobufKeyContainer container = ProtobufKeyContainer.Parser.ParseFrom(data);

            return (T)container.GetContent();
        }
    }

    /// <summary>
    /// Avro ValueContainer Binary encoder extension of <see cref="KNetSerDes{T}"/>, for example <see href="https://masesgroup.github.io/KNet/articles/usageSerDes.html"/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ValueContainer<T> : KNetSerDes<T> where T : class, IMessage<T>
    {
        readonly byte[] valueContainerSerDesName = Encoding.UTF8.GetBytes(typeof(ValueContainer<>).ToAssemblyQualified());
        readonly byte[] valueContainerName = null!;
        /// <inheritdoc/>
        public override bool UseHeaders => true;
        /// <summary>
        /// Default initializer
        /// </summary>
        public ValueContainer()
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

            using (MemoryStream stream = new())
            {
                data.WriteTo(stream);
                return stream.ToArray();
            }
        }
        /// <inheritdoc cref="KNetSerDes{T}.Deserialize(string, byte[])"/>
        public override T Deserialize(string topic, byte[] data)
        {
            return DeserializeWithHeaders(topic, null!, data);
        }
        /// <inheritdoc cref="KNetSerDes{T}.DeserializeWithHeaders(string, Headers, byte[])"/>
        public override T DeserializeWithHeaders(string topic, Headers headers, byte[] data)
        {
            if (data == null) return default!;
            var container = ProtobufValueContainer.Parser.ParseFrom(data);
            return (Activator.CreateInstance(typeof(T), container) as T)!;
        }
    }
}