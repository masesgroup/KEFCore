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

using MASES.EntityFrameworkCore.KNet.Extensions;
using MASES.KNet.Common;
using MASES.KNet.Consumer;
using MASES.KNet.Producer;
using MASES.KNet.Streams;
using Org.Apache.Kafka.Clients.Consumer;
using Org.Apache.Kafka.Clients.Producer;
using Org.Apache.Kafka.Common.Security.Auth;
using Org.Apache.Kafka.Streams;

namespace MASES.EntityFrameworkCore.KNet.Infrastructure.Internal;
/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public class KEFCoreSingletonOptions : IKEFCoreSingletonOptions
{
    private string? _clusterId;

    /// <inheritdoc/>
    public virtual void Initialize(IDbContextOptions options)
    {
        var kefcoreOptions = options.FindExtension<KEFCoreOptionsExtension>();
        if (kefcoreOptions == null) return;

        _clusterId = kefcoreOptions.ClusterId;

        KeySerDesSelectorType = kefcoreOptions.KeySerDesSelectorType;
        ValueSerDesSelectorType = kefcoreOptions.ValueSerDesSelectorType;
        ValueContainerType = kefcoreOptions.ValueContainerType;
        BootstrapServers = kefcoreOptions.BootstrapServers;
        UseKeyByteBufferDataTransfer = kefcoreOptions.UseKeyByteBufferDataTransfer;
        UseValueContainerByteBufferDataTransfer = kefcoreOptions.UseValueContainerByteBufferDataTransfer;
        UseCompactedReplicator = kefcoreOptions.UseCompactedReplicator;
        ConsumerConfig = ConsumerConfigBuilder.CreateFrom(kefcoreOptions.ConsumerConfig);
        UseKNetStreams = kefcoreOptions.UseKNetStreams;
        UsePersistentStorage = kefcoreOptions.UsePersistentStorage;
        ApplicationId = kefcoreOptions.ApplicationId;
        StreamsConfig = StreamsConfigBuilder.CreateFrom(kefcoreOptions.StreamsConfig);
        ProducerConfig = ProducerConfigBuilder.CreateFrom(kefcoreOptions.ProducerConfig);
        SecurityProtocol = kefcoreOptions.SecurityProtocol;
        SslConfig = SslConfigsBuilder.CreateFrom(kefcoreOptions.SslConfig);
        SaslConfig = SaslConfigsBuilder.CreateFrom(kefcoreOptions.SaslConfig);
        // non-hash singleton (first-wins)
        UseDeletePolicyForTopic = kefcoreOptions.UseDeletePolicyForTopic;
        DefaultNumPartitions = kefcoreOptions.DefaultNumPartitions;
        DefaultReplicationFactor = kefcoreOptions.DefaultReplicationFactor;
        TopicConfig = TopicConfigBuilder.CreateFrom(kefcoreOptions.TopicConfig);
    }
    /// <inheritdoc/>
    public virtual void Validate(IDbContextOptions options)
    {
        var kefcoreOptions = options.FindExtension<KEFCoreOptionsExtension>();
        if (kefcoreOptions == null) return;

        if (kefcoreOptions.ClusterId != _clusterId
            || kefcoreOptions.KeySerDesSelectorType != KeySerDesSelectorType
            || kefcoreOptions.ValueSerDesSelectorType != ValueSerDesSelectorType
            || kefcoreOptions.ValueContainerType != ValueContainerType
            || kefcoreOptions.UseKeyByteBufferDataTransfer != UseKeyByteBufferDataTransfer
            || kefcoreOptions.UseValueContainerByteBufferDataTransfer != UseValueContainerByteBufferDataTransfer
            || kefcoreOptions.UseCompactedReplicator != UseCompactedReplicator
            || kefcoreOptions.UseKNetStreams != UseKNetStreams
            || kefcoreOptions.UsePersistentStorage != UsePersistentStorage
            || kefcoreOptions.ApplicationId != ApplicationId)
        {
            throw new InvalidOperationException(
                CoreStrings.SingletonOptionChanged(
                    nameof(KEFCoreDbContextOptionsExtensions.UseKEFCore),
                    nameof(DbContextOptionsBuilder.UseInternalServiceProvider)));
        }
    }
    /// <inheritdoc/>
    public virtual Type? KeySerDesSelectorType { get; private set; }
    /// <inheritdoc/>
    public virtual Type? ValueSerDesSelectorType { get; private set; }
    /// <inheritdoc/>
    public virtual Type? ValueContainerType { get; private set; }
    /// <inheritdoc/>
    public virtual bool UseKeyByteBufferDataTransfer { get; private set; }
    /// <inheritdoc/>
    public virtual bool UseValueContainerByteBufferDataTransfer { get; private set; }
    /// <inheritdoc/>
    public virtual string? BootstrapServers { get; private set; }
    /// <inheritdoc/>
    public virtual bool UseDeletePolicyForTopic { get; private set; }
    /// <inheritdoc cref="KEFCoreDbContext.UseCompactedReplicator"/>
    [Obsolete("Option will be removed soon")]
    public virtual bool UseCompactedReplicator { get; private set; }
    /// <inheritdoc cref="KEFCoreDbContext.ConsumerConfig"/>
    [Obsolete("Option will be removed soon")]
    public virtual ConsumerConfigBuilder? ConsumerConfig { get; private set; }
    /// <inheritdoc cref="KEFCoreDbContext.UseKNetStreams"/>
    public virtual bool UseKNetStreams { get; private set; }
    /// <inheritdoc cref="KEFCoreDbContext.UsePersistentStorage"/>
    public virtual bool UsePersistentStorage { get; private set; }
    /// <inheritdoc cref="KEFCoreDbContext.ApplicationId"/>
    public virtual string? ApplicationId { get; private set; }
    /// <inheritdoc cref="KEFCoreDbContext.StreamsConfig"/>
    public virtual StreamsConfigBuilder? StreamsConfig { get; private set; }
    /// <inheritdoc cref="KEFCoreDbContext.ProducerConfig"/>
    public virtual ProducerConfigBuilder? ProducerConfig { get; private set; }
    /// <inheritdoc cref="KEFCoreDbContext.SecurityProtocol"/>
    public virtual SecurityProtocol? SecurityProtocol { get; private set; }
    /// <inheritdoc cref="KEFCoreDbContext.SslConfig"/>
    public virtual SslConfigsBuilder? SslConfig { get; private set; }
    /// <inheritdoc cref="KEFCoreDbContext.SaslConfig"/>
    public virtual SaslConfigsBuilder? SaslConfig { get; private set; }
    /// <inheritdoc/>
    public virtual int DefaultNumPartitions { get; private set; }
    /// <inheritdoc/>
    public virtual int DefaultReplicationFactor { get; private set; }
    /// <inheritdoc/>
    public virtual TopicConfigBuilder? TopicConfig { get; private set; }
}
