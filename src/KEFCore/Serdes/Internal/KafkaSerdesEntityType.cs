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

namespace MASES.EntityFrameworkCore.KNet.Serdes.Internal
{
    public class KafkaSerdesEntityTypeData
    {
        public KafkaSerdesEntityTypeData() { }

        public KafkaSerdesEntityTypeData(string tName, object[] rData)
        {
            typeName = tName;
            data = rData;
        }

        public string typeName;
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

        public object[] Deserialize(Headers headers, string arg)
        {
            var des = GetFullType(arg);
            return ConvertData(des!.data);
        }

        public TKey Deserialize<TKey>(Headers headers, string arg) => System.Text.Json.JsonSerializer.Deserialize<TKey>(arg)!;

        public string Serialize(Headers headers, params object?[]? args) => System.Text.Json.JsonSerializer.Serialize(new KafkaSerdesEntityTypeData(_type.Name, args!));

        public string Serialize<TKey>(Headers headers, TKey key) => System.Text.Json.JsonSerializer.Serialize(key);

        public static KafkaSerdesEntityTypeData? GetFullType(string arg) => System.Text.Json.JsonSerializer.Deserialize<KafkaSerdesEntityTypeData>(arg);

        public object[] ConvertData(object[]? input)
        {
            for (int i = 0; i < input!.Length; i++)
            {
                input[i] = Convert.ChangeType(input[i], _properties[i].ClrType);
            }
            return input;
        }
    }
}
