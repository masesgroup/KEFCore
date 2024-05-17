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

using MASES.EntityFrameworkCore.KNet.Serialization;
using System.Collections.Concurrent;

namespace MASES.EntityFrameworkCore.KNet.Storage.Internal;
/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public class EntityTypeProducers
{
    static readonly ConcurrentDictionary<IEntityType, IEntityTypeProducer> _producers = new();
    /// <summary>
    /// Allocates a new <see cref="IEntityTypeProducer"/>
    /// </summary>
    public static IEntityTypeProducer Create<TKey, TValueContainer, TJVMKey, TJVMValueContainer, TKeySerializer, TValueSerializer>(IEntityType entityType, IKafkaCluster cluster)
        where TKey : notnull
        where TValueContainer : class, IValueContainer<TKey>
        where TKeySerializer : class, new()
        where TValueSerializer : class, new()
    {
        return _producers.GetOrAdd(entityType, _ => CreateProducerLocal<TKey, TValueContainer, TJVMKey, TJVMValueContainer, TKeySerializer, TValueSerializer>(entityType, cluster));
    }
    /// <summary>
    /// Dispose a previously allocated <see cref="IEntityTypeProducer"/>
    /// </summary>
    public static void Dispose(IEntityTypeProducer producer)
    {
        if (!_producers.TryRemove(new KeyValuePair<IEntityType, IEntityTypeProducer>(producer.EntityType, producer)))
        {
            throw new InvalidOperationException($"Failed to remove IEntityTypeProducer for {producer.EntityType.Name}");
        }
        producer.Dispose();
    }

    static IEntityTypeProducer CreateProducerLocal<TKey, TValueContainer, TJVMKey, TJVMValueContainer, TKeySerializer, TValueSerializer>(IEntityType entityType, IKafkaCluster cluster)
        where TKey : notnull
        where TValueContainer : class, IValueContainer<TKey>
        where TKeySerializer : class, new()
        where TValueSerializer : class, new()
        => new EntityTypeProducer<TKey, TValueContainer, TJVMKey, TJVMValueContainer, TKeySerializer, TValueSerializer>(entityType, cluster);
}
