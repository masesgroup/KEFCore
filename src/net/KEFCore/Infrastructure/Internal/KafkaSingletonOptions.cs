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

using MASES.EntityFrameworkCore.KNet.Storage;
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
    /// <inheritdoc/>
    public virtual void Initialize(IDbContextOptions options)
    {
        var kafkaOptions = options.FindExtension<KafkaOptionsExtension>();

        if (kafkaOptions != null)
        {
            KeySerializationType = kafkaOptions.KeySerializationType;
            ValueSerializationType = kafkaOptions.ValueSerializationType;
            ValueContainerType = kafkaOptions.ValueContainerType;
            UseNameMatching = kafkaOptions.UseNameMatching;
            DatabaseName = kafkaOptions.DatabaseName;
            ApplicationId = kafkaOptions.ApplicationId;
            BootstrapServers = kafkaOptions.BootstrapServers;
            UseDeletePolicyForTopic = kafkaOptions.UseDeletePolicyForTopic;
            UseCompactedReplicator = kafkaOptions.UseCompactedReplicator;
            UseKNetStreams = kafkaOptions.UseKNetStreams;
            UsePersistentStorage = kafkaOptions.UsePersistentStorage;
            UseEnumeratorWithPrefetch = kafkaOptions.UseEnumeratorWithPrefetch;
            DefaultNumPartitions = kafkaOptions.DefaultNumPartitions;
            DefaultConsumerInstances = kafkaOptions.DefaultConsumerInstances;
            DefaultReplicationFactor = kafkaOptions.DefaultReplicationFactor;
            ConsumerConfig = ConsumerConfigBuilder.CreateFrom(kafkaOptions.ConsumerConfig);
            ProducerConfig = ProducerConfigBuilder.CreateFrom(kafkaOptions.ProducerConfig);
            StreamsConfig = StreamsConfigBuilder.CreateFrom(kafkaOptions.StreamsConfig);
            TopicConfig = TopicConfigBuilder.CreateFrom(kafkaOptions.TopicConfig);
            OnChangeEvent = kafkaOptions.OnChangeEvent;
        }
    }
    /// <inheritdoc/>
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
    /// <inheritdoc/>
    public virtual Type? KeySerializationType { get; private set; }
    /// <inheritdoc/>
    public virtual Type? ValueSerializationType { get; private set; }
    /// <inheritdoc/>
    public virtual Type? ValueContainerType { get; private set; }
    /// <inheritdoc/>
    public virtual bool UseNameMatching { get; private set; }
    /// <inheritdoc/>
    public virtual string? DatabaseName { get; private set; }
    /// <inheritdoc/>
    public virtual string? ApplicationId { get; private set; }
    /// <inheritdoc/>
    public virtual string? BootstrapServers { get; private set; }
    /// <inheritdoc/>
    public virtual bool UseDeletePolicyForTopic { get; private set; }
    /// <inheritdoc/>
    public virtual bool UseCompactedReplicator { get; private set; }
    /// <inheritdoc/>
    public virtual bool UseKNetStreams { get; private set; }
    /// <inheritdoc/>
    public virtual bool UsePersistentStorage { get; private set; }
    /// <inheritdoc/>
    public virtual bool UseEnumeratorWithPrefetch { get; private set; }
    /// <inheritdoc/>
    public virtual int DefaultNumPartitions { get; private set; }
    /// <inheritdoc/>
    public virtual int? DefaultConsumerInstances { get; private set; }
    /// <inheritdoc/>
    public virtual int DefaultReplicationFactor { get; private set; }
    /// <inheritdoc/>
    public virtual ConsumerConfigBuilder? ConsumerConfig { get; private set; }
    /// <inheritdoc/>
    public virtual ProducerConfigBuilder? ProducerConfig { get; private set; }
    /// <inheritdoc/>
    public virtual StreamsConfigBuilder? StreamsConfig { get; private set; }
    /// <inheritdoc/>
    public virtual TopicConfigBuilder? TopicConfig { get; private set; }
    /// <inheritdoc/>
    public virtual Action<EntityTypeChanged>? OnChangeEvent { get; private set; }
}
