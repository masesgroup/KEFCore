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
public interface IKafkaSingletonOptions : ISingletonOptions
{
    /// <inheritdoc cref="KafkaDbContext.KeySerializationType"/>
    Type? KeySerializationType { get; }
    /// <inheritdoc cref="KafkaDbContext.ValueSerializationType"/>
    Type? ValueSerializationType { get; }
    /// <inheritdoc cref="KafkaDbContext.ValueContainerType"/>
    Type? ValueContainerType { get; }
    /// <inheritdoc cref="KafkaDbContext.UseNameMatching"/>
    bool UseNameMatching { get; }
    /// <inheritdoc cref="KafkaDbContext.DatabaseName"/>
    string? DatabaseName { get; }
    /// <inheritdoc cref="KafkaDbContext.ApplicationId"/>
    string? ApplicationId { get; }
    /// <inheritdoc cref="KafkaDbContext.BootstrapServers"/>
    string? BootstrapServers { get; }
    /// <inheritdoc cref="KafkaDbContext.UseDeletePolicyForTopic"/>
    bool UseDeletePolicyForTopic { get; }
    /// <inheritdoc cref="KafkaDbContext.UseCompactedReplicator"/>
    bool UseCompactedReplicator { get; }
    /// <inheritdoc cref="KafkaDbContext.UseKNetStreams"/>
    bool UseKNetStreams { get; }
    /// <inheritdoc cref="KafkaDbContext.UsePersistentStorage"/>
    bool UsePersistentStorage { get; }
    /// <inheritdoc cref="KafkaDbContext.UseEnumeratorWithPrefetch"/>
    bool UseEnumeratorWithPrefetch { get; }
    /// <inheritdoc cref="KafkaDbContext.DefaultNumPartitions"/>
    int DefaultNumPartitions { get; }
    /// <inheritdoc cref="KafkaDbContext.DefaultConsumerInstances"/>
    int? DefaultConsumerInstances { get; }
    /// <inheritdoc cref="KafkaDbContext.DefaultReplicationFactor"/>
    int DefaultReplicationFactor { get; }
    /// <inheritdoc cref="KafkaDbContext.ConsumerConfig"/>
    ConsumerConfigBuilder? ConsumerConfig { get; }
    /// <inheritdoc cref="KafkaDbContext.ProducerConfig"/>
    ProducerConfigBuilder? ProducerConfig { get; }
    /// <inheritdoc cref="KafkaDbContext.StreamsConfig"/>
    StreamsConfigBuilder? StreamsConfig { get; }
    /// <inheritdoc cref="KafkaDbContext.TopicConfig"/>
    TopicConfigBuilder? TopicConfig { get; }
    /// <inheritdoc cref="KafkaDbContext.OnChangeEvent"/>
    Action<EntityTypeChanged>? OnChangeEvent { get; }
}
