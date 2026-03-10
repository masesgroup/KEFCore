/*
*  Copyright (c) 2022-2026 MASES s.r.l.
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
public interface IKEFCoreSingletonOptions : ISingletonOptions
{
    /// <inheritdoc cref="KEFCoreDbContext.KeySerDesSelectorType"/>
    Type? KeySerDesSelectorType { get; }
    /// <inheritdoc cref="KEFCoreDbContext.ValueSerDesSelectorType"/>
    Type? ValueSerDesSelectorType { get; }
    /// <inheritdoc cref="KEFCoreDbContext.ValueContainerType"/>
    Type? ValueContainerType { get; }
    /// <inheritdoc cref="KEFCoreDbContext.TopicPrefix"/>
    string? TopicPrefix { get; }
    /// <inheritdoc cref="KEFCoreDbContext.ApplicationId"/>
    string? ApplicationId { get; }
    /// <inheritdoc cref="KEFCoreDbContext.BootstrapServers"/>
    string? BootstrapServers { get; }
    /// <inheritdoc cref="KEFCoreDbContext.UseDeletePolicyForTopic"/>
    bool UseDeletePolicyForTopic { get; }
    /// <inheritdoc cref="KEFCoreDbContext.UseCompactedReplicator"/>
    [Obsolete("Option will be removed soon")]
    bool UseCompactedReplicator { get; }
    /// <inheritdoc cref="KEFCoreDbContext.UseKNetStreams"/>
    bool UseKNetStreams { get; }
    /// <inheritdoc cref="KEFCoreDbContext.UseGlobalTable"/>
    bool UseGlobalTable { get; }
    /// <inheritdoc cref="KEFCoreDbContext.UsePersistentStorage"/>
    bool UsePersistentStorage { get; }
    /// <inheritdoc cref="KEFCoreDbContext.UseEnumeratorWithPrefetch"/>
    bool UseEnumeratorWithPrefetch { get; }
    /// <inheritdoc cref="KEFCoreDbContext.UseByteBufferDataTransfer"/>
    bool UseByteBufferDataTransfer { get; }
    /// <inheritdoc cref="KEFCoreDbContext.DefaultNumPartitions"/>
    int DefaultNumPartitions { get; }
    /// <inheritdoc cref="KEFCoreDbContext.DefaultConsumerInstances"/>
    int? DefaultConsumerInstances { get; }
    /// <inheritdoc cref="KEFCoreDbContext.DefaultReplicationFactor"/>
    int DefaultReplicationFactor { get; }
    /// <inheritdoc cref="KEFCoreDbContext.ConsumerConfig"/>
    ConsumerConfigBuilder? ConsumerConfig { get; }
    /// <inheritdoc cref="KEFCoreDbContext.ProducerConfig"/>
    ProducerConfigBuilder? ProducerConfig { get; }
    /// <inheritdoc cref="KEFCoreDbContext.StreamsConfig"/>
    StreamsConfigBuilder? StreamsConfig { get; }
    /// <inheritdoc cref="KEFCoreDbContext.TopicConfig"/>
    TopicConfigBuilder? TopicConfig { get; }
    /// <inheritdoc cref="KEFCoreDbContext.ManageEvents"/>
    bool ManageEvents { get; }
    /// <inheritdoc cref="KEFCoreDbContext.ReadOnlyMode"/>
    bool ReadOnlyMode { get; }
    /// <inheritdoc cref="KEFCoreDbContext.DefaultSynchronizationTimeout"/>
    long DefaultSynchronizationTimeout { get; }
}
