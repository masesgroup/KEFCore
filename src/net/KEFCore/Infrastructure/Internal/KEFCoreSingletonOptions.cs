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
public class KEFCoreSingletonOptions : IKEFCoreSingletonOptions
{
    /// <inheritdoc/>
    public virtual void Initialize(IDbContextOptions options)
    {
        var kefcoreOptions = options.FindExtension<KEFCoreOptionsExtension>();

        if (kefcoreOptions != null)
        {
            KeySerDesSelectorType = kefcoreOptions.KeySerDesSelectorType;
            ValueSerDesSelectorType = kefcoreOptions.ValueSerDesSelectorType;
            ValueContainerType = kefcoreOptions.ValueContainerType;
            TopicPrefix = kefcoreOptions.TopicPrefix;
            ApplicationId = kefcoreOptions.ApplicationId;
            BootstrapServers = kefcoreOptions.BootstrapServers;
            UseDeletePolicyForTopic = kefcoreOptions.UseDeletePolicyForTopic;
            UseCompactedReplicator = kefcoreOptions.UseCompactedReplicator;
            UseKNetStreams = kefcoreOptions.UseKNetStreams;
            UseGlobalTable = kefcoreOptions.UseGlobalTable;
            UsePersistentStorage = kefcoreOptions.UsePersistentStorage;
            UseEnumeratorWithPrefetch = kefcoreOptions.UseEnumeratorWithPrefetch;
            UseByteBufferDataTransfer = kefcoreOptions.UseByteBufferDataTransfer;
            DefaultNumPartitions = kefcoreOptions.DefaultNumPartitions;
            DefaultConsumerInstances = kefcoreOptions.DefaultConsumerInstances;
            DefaultReplicationFactor = kefcoreOptions.DefaultReplicationFactor;
            ConsumerConfig = ConsumerConfigBuilder.CreateFrom(kefcoreOptions.ConsumerConfig);
            ProducerConfig = ProducerConfigBuilder.CreateFrom(kefcoreOptions.ProducerConfig);
            StreamsConfig = StreamsConfigBuilder.CreateFrom(kefcoreOptions.StreamsConfig);
            TopicConfig = TopicConfigBuilder.CreateFrom(kefcoreOptions.TopicConfig);
            ManageEvents = kefcoreOptions.ManageEvents;
            ReadOnlyMode = kefcoreOptions.ReadOnlyMode;
            DefaultSynchronizationTimeout = kefcoreOptions.DefaultSynchronizationTimeout;
        }
    }
    /// <inheritdoc/>
    public virtual void Validate(IDbContextOptions options)
    {
        var kefcoreOptions = options.FindExtension<KEFCoreOptionsExtension>();

        if (kefcoreOptions != null
            && BootstrapServers != kefcoreOptions.BootstrapServers)
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
    public virtual string? TopicPrefix { get; private set; }
    /// <inheritdoc/>
    public virtual string? ApplicationId { get; private set; }
    /// <inheritdoc/>
    public virtual string? BootstrapServers { get; private set; }
    /// <inheritdoc/>
    public virtual bool UseDeletePolicyForTopic { get; private set; }
    /// <inheritdoc/>
    [Obsolete("Option will be removed soon")] 
    public virtual bool UseCompactedReplicator { get; private set; }
    /// <inheritdoc/>
    public virtual bool UseKNetStreams { get; private set; }
    /// <inheritdoc/>
    public virtual bool UseGlobalTable { get; private set; }
    /// <inheritdoc/>
    public virtual bool UsePersistentStorage { get; private set; }
    /// <inheritdoc/>
    public virtual bool UseEnumeratorWithPrefetch { get; private set; }
    /// <inheritdoc/>
    public virtual bool UseByteBufferDataTransfer { get; private set; }
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
    public virtual bool ManageEvents { get; private set; }
    /// <inheritdoc/>
    public virtual bool ReadOnlyMode { get; private set; }
    /// <inheritdoc/>
    public virtual long DefaultSynchronizationTimeout { get; private set; }
}
