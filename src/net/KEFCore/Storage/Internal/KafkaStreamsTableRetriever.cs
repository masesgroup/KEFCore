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

using MASES.EntityFrameworkCore.KNet.Serialization;
using MASES.KNet.Serialization;
using Org.Apache.Kafka.Streams;

namespace MASES.EntityFrameworkCore.KNet.Storage.Internal;

/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public sealed class KafkaStreamsTableRetriever<TKey, TValueContainer> : KafkaStreamsBaseRetriever<TKey, TValueContainer, byte[], byte[]>
    where TKey :notnull
    where TValueContainer : IValueContainer<TKey>
{
    /// <summary>
    /// Initializer
    /// </summary>
    public KafkaStreamsTableRetriever(IKafkaCluster kafkaCluster, IEntityType entityType, IKNetSerDes<TKey> keySerdes, IKNetSerDes<TValueContainer> valueSerdes)
        : this(kafkaCluster, entityType, keySerdes, valueSerdes, new StreamsBuilder())
    {
    }
    /// <summary>
    /// Initializer
    /// </summary>
    public KafkaStreamsTableRetriever(IKafkaCluster kafkaCluster, IEntityType entityType, IKNetSerDes<TKey> keySerdes, IKNetSerDes<TValueContainer> valueSerdes, StreamsBuilder builder)
        : base(kafkaCluster, entityType, keySerdes, valueSerdes, entityType.StorageIdForTable(kafkaCluster.Options), builder, builder.Stream<byte[], byte[]>(entityType.TopicName(kafkaCluster.Options)))
    {
    }
}

