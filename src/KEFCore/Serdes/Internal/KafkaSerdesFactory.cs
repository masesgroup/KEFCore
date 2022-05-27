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

using System.Collections.Concurrent;
using System.Text;

namespace MASES.EntityFrameworkCore.KNet.Serdes.Internal;

public class KafkaSerdesFactory : IKafkaSerdesFactory
{
    private readonly ConcurrentDictionary<string, IKafkaSerdesEntityType> _serdes;

    public KafkaSerdesFactory(
        ILoggingOptions loggingOptions)
    {
        _serdes = new ConcurrentDictionary<string, IKafkaSerdesEntityType>();
        LoggingOptions = loggingOptions;
    }

    public ILoggingOptions LoggingOptions { get; }

    public virtual IKafkaSerdesEntityType GetOrCreate(IEntityType type)
        => _serdes.GetOrAdd(type.Name, _ => new KafkaSerdesEntityType(type));

    public virtual IKafkaSerdesEntityType Get(string typeName)
        => _serdes[typeName];

    public virtual object[] Deserialize(byte[] data)
    {
        var str = Encoding.UTF8.GetString(data);
        var fulltype = KafkaSerdesEntityType.GetFullType(str);
        return Get(fulltype!.typeName!).ConvertData(fulltype.data);
    }

    public virtual object[] Deserialize(string data)
    {
        var fulltype = KafkaSerdesEntityType.GetFullType(data);
        return Get(fulltype!.typeName!).ConvertData(fulltype.data);
    }
}
