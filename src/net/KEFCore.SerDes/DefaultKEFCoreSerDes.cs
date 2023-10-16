﻿/*
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

using MASES.EntityFrameworkCore.KNet.Serialization.Json.Storage;
using MASES.KNet.Serialization;
using Org.Apache.Kafka.Common.Header;
using System.Text;

namespace MASES.EntityFrameworkCore.KNet.Serialization.Json;
/// <summary>
/// Default base class to define extensions of <see cref="KNetSerDes{T}"/>, for example <see href="https://masesgroup.github.io/KNet/articles/usageSerDes.html"/>
/// </summary>
public static class DefaultKEFCoreSerDes
{
    /// <summary>
    /// Base class to define key extensions of <see cref="KNetSerDes{T}"/>, for example <see href="https://masesgroup.github.io/KNet/articles/usageSerDes.html"/>
    /// </summary>
    public static class Key
    {
        /// <summary>
        /// Json extension of <see cref="KNetSerDes{T}"/>, for example <see href="https://masesgroup.github.io/KNet/articles/usageSerDes.html"/>
        /// </summary>
        /// <typeparam name="T">The type to be serialized or deserialized. It can be a Primary Key or a ValueContainer like <see cref="DefaultValueContainer{TKey}"/></typeparam>
        public class Json<T> : KNetSerDes<T>
        {
            readonly IKNetSerDes<T> _defaultSerDes = default;
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
                    throw new InvalidOperationException($"KEFCoreSerDes cannot manage {typeof(T).Name}, override or build a new serializaer");
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
                if (_defaultSerDes != null) return _defaultSerDes.SerializeWithHeaders(topic, headers, data);

                var jsonStr = System.Text.Json.JsonSerializer.Serialize<T>(data);
                return Encoding.UTF8.GetBytes(jsonStr);
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
                return System.Text.Json.JsonSerializer.Deserialize<T>(data)!;
            }
        }
    }

    /// <summary>
    /// Base class to define ValueContainer extensions of <see cref="KNetSerDes{T}"/>, for example <see href="https://masesgroup.github.io/KNet/articles/usageSerDes.html"/>
    /// </summary>
    public static class ValueContainer
    {
        /// <summary>
        /// Json extension of <see cref="KNetSerDes{T}"/>, for example <see href="https://masesgroup.github.io/KNet/articles/usageSerDes.html"/>
        /// </summary>
        /// <typeparam name="T">The type to be serialized or deserialized. It can be a Primary Key or a ValueContainer like <see cref="DefaultValueContainer{TKey}"/></typeparam>
        public class Json<T> : KNetSerDes<T>
        {
            /// <summary>
            /// Default initializer
            /// </summary>
            public Json()
            {
                var tt = typeof(T);
                if (tt.IsGenericType)
                {
                    var t = tt.GetGenericTypeDefinition();
                    if (t.GetInterface(typeof(IValueContainer<>).Name) != null)
                    {
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
                var jsonStr = System.Text.Json.JsonSerializer.Serialize<T>(data);
                return Encoding.UTF8.GetBytes(jsonStr);
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
                return System.Text.Json.JsonSerializer.Deserialize<T>(data)!;
            }
        }
    }
}