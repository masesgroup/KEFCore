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
    public interface IKafkaSerdesEntityType
    {
        string Serialize(params object?[]? args);

        string Serialize(Headers headers, params object?[]? args);

        string Serialize<TKey>(TKey key);

        string Serialize<TKey>(Headers headers, TKey key);

        object[] Deserialize(string arg);

        object[] Deserialize(Headers headers, string arg);

        TKey Deserialize<TKey>(string arg);

        TKey Deserialize<TKey>(Headers headers, string arg);

        object[] ConvertData(object[]? input);
    }
}
