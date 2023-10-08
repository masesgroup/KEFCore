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

#nullable enable

namespace MASES.EntityFrameworkCore.KNet.Storage.Internal;

public class KafkaRowBag<TKey> : IKafkaRowBag
{
    public KafkaRowBag(IUpdateEntry entry, string topicName, TKey key, IProperty[] properties, object?[]? row)
    {
        UpdateEntry = entry;
        AssociatedTopicName = topicName;
        Key = key;
        Properties = properties;
        ValueBuffer = row;
    }

    public IUpdateEntry UpdateEntry { get; private set; }

    public string AssociatedTopicName { get; private set; }

    public TKey Key { get; private set; }

    public IProperty[] Properties { get; private set; }

    public EntityTypeDataStorage<TKey>? Value => UpdateEntry.EntityState == EntityState.Deleted ? null : new EntityTypeDataStorage<TKey>(UpdateEntry.EntityType, Properties, ValueBuffer!);

    public object?[]? ValueBuffer { get; private set; }
}
