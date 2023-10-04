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

using MASES.KNet.Serialization;
using Org.Apache.Kafka.Streams;

namespace MASES.EntityFrameworkCore.KNet.Storage.Internal
{
    public sealed class KafkaStreamsTableRetriever<TKey> : KafkaStreamsBaseRetriever<TKey, KNetEntityTypeData<TKey>, byte[], byte[]>
    {
        public KafkaStreamsTableRetriever(IKafkaCluster kafkaCluster, IEntityType entityType, IKNetSerDes<TKey> keySerdes, IKNetSerDes<KNetEntityTypeData<TKey>> valueSerdes)
            : this(kafkaCluster, entityType, keySerdes, valueSerdes, new StreamsBuilder())
        {
        }

        public KafkaStreamsTableRetriever(IKafkaCluster kafkaCluster, IEntityType entityType, IKNetSerDes<TKey> keySerdes, IKNetSerDes<KNetEntityTypeData<TKey>> valueSerdes, StreamsBuilder builder)
            : base(kafkaCluster, entityType, keySerdes, valueSerdes, entityType.StorageIdForTable(kafkaCluster.Options), builder, builder.Stream<byte[], byte[]>(entityType.TopicName(kafkaCluster.Options)))
        {
        }
    }
}
