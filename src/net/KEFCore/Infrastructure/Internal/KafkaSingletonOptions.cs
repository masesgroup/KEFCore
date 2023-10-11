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

using MASES.KNet.Common;
using MASES.KNet.Consumer;
using MASES.KNet.Producer;
using MASES.KNet.Streams;

namespace MASES.EntityFrameworkCore.KNet.Infrastructure.Internal;
/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public class KafkaSingletonOptions : IKafkaSingletonOptions
{
    public virtual void Initialize(IDbContextOptions options)
    {
        var kafkaOptions = options.FindExtension<KafkaOptionsExtension>();

        if (kafkaOptions != null)
        {
            UseNameMatching = kafkaOptions.UseNameMatching;
            DatabaseName = kafkaOptions.DatabaseName;
            ApplicationId = kafkaOptions.ApplicationId;
            BootstrapServers = kafkaOptions.BootstrapServers;
            //ProducerByEntity = kafkaOptions.ProducerByEntity;
            UseCompactedReplicator = kafkaOptions.UseCompactedReplicator;
            UsePersistentStorage = kafkaOptions.UsePersistentStorage;
            DefaultNumPartitions = kafkaOptions.DefaultNumPartitions;
            DefaultConsumerInstances = kafkaOptions.DefaultConsumerInstances;
            DefaultReplicationFactor = kafkaOptions.DefaultReplicationFactor;
            ConsumerConfig = ConsumerConfigBuilder.CreateFrom(kafkaOptions.ConsumerConfig);
            ProducerConfig = ProducerConfigBuilder.CreateFrom(kafkaOptions.ProducerConfig);
            StreamsConfig = StreamsConfigBuilder.CreateFrom(kafkaOptions.StreamsConfig);
            TopicConfig = TopicConfigBuilder.CreateFrom(kafkaOptions.TopicConfig);
        }
    }

    public virtual void Validate(IDbContextOptions options)
    {
        var kafkaOptions = options.FindExtension<KafkaOptionsExtension>();

        if (kafkaOptions != null
            && BootstrapServers != kafkaOptions.BootstrapServers)
        {
            throw new InvalidOperationException(
                CoreStrings.SingletonOptionChanged(
                    nameof(KafkaDbContextOptionsExtensions.UseKafkaCluster),
                    nameof(DbContextOptionsBuilder.UseInternalServiceProvider)));
        }
    }

    public virtual bool UseNameMatching { get; private set; }

    public virtual string? DatabaseName { get; private set; }

    public virtual string? ApplicationId { get; private set; }

    public virtual string? BootstrapServers { get; private set; }

    public virtual bool ProducerByEntity { get; private set; }

    public virtual bool UseCompactedReplicator { get; private set; }

    public virtual bool UsePersistentStorage { get; private set; }

    public virtual int DefaultNumPartitions { get; private set; }

    public virtual int? DefaultConsumerInstances { get; private set; }

    public virtual int DefaultReplicationFactor { get; private set; }

    public virtual ConsumerConfigBuilder? ConsumerConfig { get; private set; }

    public virtual ProducerConfigBuilder? ProducerConfig { get; private set; }

    public virtual StreamsConfigBuilder? StreamsConfig { get; private set; }

    public virtual TopicConfigBuilder? TopicConfig { get; private set; }
}
