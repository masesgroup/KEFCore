/*
*  Copyright 2022 MASES s.r.l.
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

using Org.Apache.Kafka.Common.Header;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace MASES.EntityFrameworkCore.KNet.Serdes.Internal
{
    [JsonSerializable(typeof(KafkaSerdesEntityTypeData))]
    public class KafkaSerdesEntityTypeData
    {
        public KafkaSerdesEntityTypeData() { }

        public KafkaSerdesEntityTypeData(string tName, object[] rData)
        {
            typeName = tName;
            data = rData;
        }
        [JsonInclude()]
        public string typeName;
        [JsonInclude()]
        public object[] data;
    }

    public class KafkaSerdesEntityType : IKafkaSerdesEntityType
    {
        private readonly IEntityType _type;
        private readonly IProperty[] _properties;

        public KafkaSerdesEntityType(IEntityType type)
        {
            _type = type;
            _properties = _type.GetProperties().ToArray();
        }

        public object[] Deserialize(string arg)
        {
            var des = GetFullType(arg);
            return ConvertData(des!.data);
        }

        public object[] Deserialize(Headers headers, string arg)
        {
            var des = GetFullType(arg);
            return ConvertData(des!.data);
        }

        public TKey Deserialize<TKey>(string arg) => System.Text.Json.JsonSerializer.Deserialize<TKey>(arg)!;

        public TKey Deserialize<TKey>(Headers headers, string arg) => System.Text.Json.JsonSerializer.Deserialize<TKey>(arg)!;

        public string Serialize(params object?[]? args) => System.Text.Json.JsonSerializer.Serialize(new KafkaSerdesEntityTypeData(_type.Name, args!));

        public string Serialize(Headers headers, params object?[]? args) => System.Text.Json.JsonSerializer.Serialize(new KafkaSerdesEntityTypeData(_type.Name, args!));

        public string Serialize<TKey>(TKey key) => System.Text.Json.JsonSerializer.Serialize(key);

        public string Serialize<TKey>(Headers headers, TKey key) => System.Text.Json.JsonSerializer.Serialize(key);

        public static KafkaSerdesEntityTypeData? GetFullType(string arg) => System.Text.Json.JsonSerializer.Deserialize<KafkaSerdesEntityTypeData>(arg);

        public object[] ConvertData(object[]? input)
        {
            if (input == null) return null;
            List<object> data = new List<object>();

            for (int i = 0; i < input!.Length; i++)
            {
                if (input[i] is JsonElement elem)
                {
                    switch (elem.ValueKind)
                    {
                        case JsonValueKind.Undefined:
                            break;
                        case JsonValueKind.Object:
                            break;
                        case JsonValueKind.Array:
                            break;
                        case JsonValueKind.String:
                            data.Add(elem.GetString());
                            break;
                        case JsonValueKind.Number:
                            var tmp = elem.GetInt64();
                            data.Add(Convert.ChangeType(tmp, _properties[i].ClrType));
                            break;
                        case JsonValueKind.True:
                            data.Add(true);
                            break;
                        case JsonValueKind.False:
                            data.Add(false);
                            break;
                        case JsonValueKind.Null:
                            data.Add(null);
                            break;
                        default:
                            break;
                    }

                }
                else
                {
                    data.Add(Convert.ChangeType(input[i], _properties[i].ClrType));
                }
            }
            return data.ToArray();
        }
    }
}
