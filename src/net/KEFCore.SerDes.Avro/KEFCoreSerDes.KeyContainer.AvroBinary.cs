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

namespace MASES.EntityFrameworkCore.KNet.Serialization.Avro;

/// <summary>
/// Avro Key Binary encoder extension of <see cref="KNetSerDes{T}"/>, for example <see href="https://masesgroup.github.io/KNet/articles/usageSerDes.html"/>
/// </summary>
/// <typeparam name="T"></typeparam>
public class KEFCoreSerDesKeyAvroBinary<T> : KNetSerDes<T>
{
    static readonly SpecificDefaultWriter SpecificWriter = new(AvroKeyContainer._SCHEMA);
    static readonly SpecificDefaultReader SpecificReader = new(AvroKeyContainer._SCHEMA, AvroKeyContainer._SCHEMA);
    readonly IKNetSerDes<T> _defaultSerDes = default;
    /// <summary>
    /// Default initializer
    /// </summary>
    public KEFCoreSerDesKeyAvroBinary()
    {
        if (KNetSerialization.IsInternalManaged<T>())
        {
            _defaultSerDes = new KNetSerDes<T>();
        }
        else if (!typeof(T).IsArray)
        {
            throw new InvalidOperationException($"KEFCoreSerDesKeyAvroBinary cannot manage {typeof(T).Name}, override or build a new serializaer");
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
        if (_defaultSerDes != null) return _defaultSerDes.DeserializeWithHeaders(topic, headers, data);

        using MemoryStream memStream = new(data);
        BinaryDecoder decoder = new(memStream);
        T t = (T)Activator.CreateInstance(typeof(T))!;
        t = SpecificReader.Read(t!, decoder);
        return t;
    }
}
