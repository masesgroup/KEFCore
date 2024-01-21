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

using MASES.KNet.Serialization;

namespace MASES.EntityFrameworkCore.KNet.Serialization;

interface ILocalEntityExtractor
{
    object GetEntity(string topic, byte[] recordValue, byte[] recordKey, bool throwUnmatch);
}

class LocalEntityExtractor<TKey, TValueContainer, TKeySerializer, TValueSerializer> : ILocalEntityExtractor
    where TKey : notnull
    where TValueContainer : class, IValueContainer<TKey>
    where TKeySerializer : class, new()
    where TValueSerializer : class, new()
{
    private readonly IKNetSerDes<TKey>? _keySerdes;
    private readonly IKNetSerDes<TValueContainer>? _valueSerdes;

    public LocalEntityExtractor()
    {
        _keySerdes = new TKeySerializer() as IKNetSerDes<TKey>;
        _valueSerdes = new TValueSerializer() as IKNetSerDes<TValueContainer>;
    }

    public object GetEntity(string topic, byte[] recordValue, byte[] recordKey, bool throwUnmatch)
    {
        if (recordValue == null) throw new ArgumentNullException(nameof(recordValue), "Record value shall be available");

        TValueContainer valueContainer = _valueSerdes?.DeserializeWithHeaders(topic, null, recordValue)!;
        var entityType = Type.GetType(valueContainer.ClrType, true);
        if (entityType != null)
        {
            var newEntity = Activator.CreateInstance(entityType!);
            object[] data = null!;
            valueContainer.GetData(null!, ref data);
            foreach (var property in valueContainer.GetProperties())
            {
                var propInfo = entityType.GetProperty(property.Value);
                if (propInfo != null)
                {
                    if (propInfo.CanWrite)
                    {
                        propInfo.SetValue(newEntity, data[property.Key]);
                    }
                    else if (throwUnmatch) throw new InvalidOperationException($"Unable to write property {property.Value} at index {property.Key} with {data[property.Key]}");
                }
                else if (throwUnmatch) throw new InvalidOperationException($"Property {property.Value} not found in {valueContainer.ClrType}");
            }

            return newEntity!;

        }
        throw new ArgumentException($"Cannot create an instance of {valueContainer.ClrType}");
    }
}

