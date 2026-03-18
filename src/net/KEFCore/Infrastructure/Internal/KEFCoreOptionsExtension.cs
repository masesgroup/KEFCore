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

using Java.Lang;
using Java.Util;
using MASES.EntityFrameworkCore.KNet.Extensions;
using MASES.EntityFrameworkCore.KNet.Serialization.Json;
using MASES.EntityFrameworkCore.KNet.Storage.Internal;
using MASES.KNet.Common;
using MASES.KNet.Consumer;
using MASES.KNet.Producer;
using MASES.KNet.Streams;
using Org.Apache.Kafka.Streams.State;
using System.Globalization;

namespace MASES.EntityFrameworkCore.KNet.Infrastructure.Internal;
/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public class KEFCoreOptionsExtension : IDbContextOptionsExtension, IKEFCoreSingletonOptions
{
    private readonly object _clusterIdLock = new();
    private string _clusterId = null!;
    private DbContextOptionsExtensionInfo? _info;
    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public KEFCoreOptionsExtension() { }
    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected KEFCoreOptionsExtension(KEFCoreOptionsExtension copyFrom)
    {
        // ── Singleton options ─────────────────────────────────────────────
        KeySerDesSelectorType = copyFrom.KeySerDesSelectorType;
        ValueSerDesSelectorType = copyFrom.ValueSerDesSelectorType;
        ValueContainerType = copyFrom.ValueContainerType;
        UseKeyByteBufferDataTransfer = copyFrom.UseKeyByteBufferDataTransfer;
        UseValueContainerByteBufferDataTransfer = copyFrom.UseValueContainerByteBufferDataTransfer;
        BootstrapServers = copyFrom.BootstrapServers;
        UseKNetStreams = copyFrom.UseKNetStreams;
        UsePersistentStorage = copyFrom.UsePersistentStorage;
        UseCompactedReplicator = copyFrom.UseCompactedReplicator;
        UseDeletePolicyForTopic = copyFrom.UseDeletePolicyForTopic;
        DefaultNumPartitions = copyFrom.DefaultNumPartitions;
        DefaultReplicationFactor = copyFrom.DefaultReplicationFactor;
        TopicConfig = TopicConfigBuilder.CreateFrom(copyFrom.TopicConfig);
        // obsolete
        DefaultConsumerInstances = copyFrom.DefaultConsumerInstances;
        ConsumerConfig = ConsumerConfigBuilder.CreateFrom(copyFrom.ConsumerConfig);

        // ── Context-only options ──────────────────────────────────────────
        ApplicationId = copyFrom.ApplicationId;
        ProducerConfig = ProducerConfigBuilder.CreateFrom(copyFrom.ProducerConfig);
        StreamsConfig = StreamsConfigBuilder.CreateFrom(copyFrom.StreamsConfig);
        ManageEvents = copyFrom.ManageEvents;
        ReadOnlyMode = copyFrom.ReadOnlyMode;
        DefaultSynchronizationTimeout = copyFrom.DefaultSynchronizationTimeout;
        UseEnumeratorWithPrefetch = copyFrom.UseEnumeratorWithPrefetch;
        UseStorePrefixScan = copyFrom.UseStorePrefixScan;
        UseStoreSingleKeyLookup = copyFrom.UseStoreSingleKeyLookup;
        UseStoreKeyRange = copyFrom.UseStoreKeyRange;
        UseStoreReverse = copyFrom.UseStoreReverse;
        UseStoreReverseKeyRange = copyFrom.UseStoreReverseKeyRange;
        UseGlobalTable = copyFrom.UseGlobalTable;
    }
    /// <inheritdoc/>
    public virtual DbContextOptionsExtensionInfo Info => _info ??= new ExtensionInfo(this);
    /// <inheritdoc/>
    protected virtual KEFCoreOptionsExtension Clone() => new(this);
    /// <summary>
    /// Internal property
    /// </summary>
    public virtual string ClusterId
    {
        get
        {
            lock (_clusterIdLock)
            {
                _clusterId ??= KEFCoreClusterAdmin.Create(BootstrapServers).ClusterId;
                if (_clusterId == null) throw new InvalidOperationException($"ClusterId currently not available from {BootstrapServers}");
                return _clusterId;
            }
        }
    }

    // ── Singleton options ─────────────────────────────────────────────────

    /// <inheritdoc cref="KEFCoreDbContext.KeySerDesSelectorType"/>
    public virtual Type KeySerDesSelectorType { get; set; } = DefaultKEFCoreSerDes.DefaultKeySerialization;
    /// <inheritdoc cref="KEFCoreDbContext.ValueSerDesSelectorType"/>
    public virtual Type ValueSerDesSelectorType { get; set; } = DefaultKEFCoreSerDes.DefaultValueContainerSerialization;
    /// <inheritdoc cref="KEFCoreDbContext.ValueContainerType"/>
    public virtual Type ValueContainerType { get; set; } = DefaultKEFCoreSerDes.DefaultValueContainer;
    /// <inheritdoc cref="KEFCoreDbContext.UseKeyByteBufferDataTransfer"/>
    public virtual bool UseKeyByteBufferDataTransfer { get; set; } = false;
    /// <inheritdoc cref="KEFCoreDbContext.UseValueContainerByteBufferDataTransfer"/>
    public virtual bool UseValueContainerByteBufferDataTransfer { get; set; } = false;
    /// <inheritdoc cref="KEFCoreDbContext.BootstrapServers"/>
    public virtual string? BootstrapServers { get; set; }
    /// <inheritdoc cref="KEFCoreDbContext.UseKNetStreams"/>
    public virtual bool UseKNetStreams { get; set; } = true;
    /// <inheritdoc cref="KEFCoreDbContext.UsePersistentStorage"/>
    public virtual bool UsePersistentStorage { get; set; } = false;
    /// <inheritdoc cref="KEFCoreDbContext.UseCompactedReplicator"/>
    [Obsolete("Option will be removed soon")]
    public virtual bool UseCompactedReplicator { get; set; } = false;
    /// <inheritdoc cref="KEFCoreDbContext.UseDeletePolicyForTopic"/>
    public virtual bool UseDeletePolicyForTopic { get; set; } = false;
    /// <inheritdoc cref="KEFCoreDbContext.DefaultNumPartitions"/>
    public virtual int DefaultNumPartitions { get; set; } = 1;
    /// <inheritdoc cref="KEFCoreDbContext.DefaultReplicationFactor"/>
    public virtual short DefaultReplicationFactor { get; set; } = 1;
    /// <inheritdoc cref="KEFCoreDbContext.TopicConfig"/>
    public virtual TopicConfigBuilder? TopicConfig { get; set; }
    // explicit for mismatch short/int in the interface
    int IKEFCoreSingletonOptions.DefaultReplicationFactor => DefaultReplicationFactor;

    // ── Context-only options ──────────────────────────────────────────────
    /// <inheritdoc cref="KEFCoreDbContext.DefaultConsumerInstances"/>
    [Obsolete("Option will be removed soon")]
    public virtual int? DefaultConsumerInstances { get; set; } = null;
    /// <inheritdoc cref="KEFCoreDbContext.ConsumerConfig"/>
    [Obsolete("Option will be removed soon")]
    public virtual ConsumerConfigBuilder? ConsumerConfig { get; set; }
    /// <inheritdoc cref="KEFCoreDbContext.ApplicationId"/>
    public virtual string? ApplicationId { get; set; }
    /// <inheritdoc cref="KEFCoreDbContext.ProducerConfig"/>
    public virtual ProducerConfigBuilder? ProducerConfig { get; set; }
    /// <inheritdoc cref="KEFCoreDbContext.StreamsConfig"/>
    public virtual StreamsConfigBuilder? StreamsConfig { get; set; }
    /// <inheritdoc cref="KEFCoreDbContext.ManageEvents"/>
    public virtual bool ManageEvents { get; set; } = false;
    /// <inheritdoc cref="KEFCoreDbContext.ReadOnlyMode"/>
    public virtual bool ReadOnlyMode { get; set; } = false;
    /// <inheritdoc cref="KEFCoreDbContext.DefaultSynchronizationTimeout"/>
    public virtual long DefaultSynchronizationTimeout { get; set; } = Timeout.Infinite;
    /// <inheritdoc cref="KEFCoreDbContext.UseEnumeratorWithPrefetch"/>
    public virtual bool UseEnumeratorWithPrefetch { get; set; } = true;
    /// <inheritdoc cref="KEFCoreDbContext.UseStorePrefixScan"/>
    public virtual bool UseStorePrefixScan { get; set; } = false;
    /// <inheritdoc cref="KEFCoreDbContext.UseStoreSingleKeyLookup"/>
    public virtual bool UseStoreSingleKeyLookup { get; private set; } = true;
    /// <inheritdoc cref="KEFCoreDbContext.UseStoreKeyRange"/>
    public virtual bool UseStoreKeyRange { get; private set; } = true;
    /// <inheritdoc cref="KEFCoreDbContext.UseStoreReverse"/>
    public virtual bool UseStoreReverse { get; private set; } = true;
    /// <inheritdoc cref="KEFCoreDbContext.UseStoreReverseKeyRange"/>
    public virtual bool UseStoreReverseKeyRange { get; private set; } = true;
    /// <inheritdoc cref="KEFCoreDbContext.UseGlobalTable"/>
    [Obsolete("ApplicationId must be unique per process — UseGlobalTable is no longer needed.")]
    public virtual bool UseGlobalTable { get; set; } = false;

    // ── With* methods — singleton ─────────────────────────────────────────
    /// <inheritdoc cref="KEFCoreDbContext.KeySerDesSelectorType"/>
    public virtual KEFCoreOptionsExtension WithKeySerDesSelectorType(Type serializationType)
    {
        if (!serializationType.IsGenericTypeDefinition)
            throw new InvalidOperationException($"{serializationType.Name} shall be a generic type and shall be defined using \"<>\"");
        var clone = Clone();
        clone.KeySerDesSelectorType = serializationType;
        return clone;
    }
    /// <inheritdoc cref="KEFCoreDbContext.ValueSerDesSelectorType"/>
    public virtual KEFCoreOptionsExtension WithValueSerDesSelectorType(Type serializationType)
    {
        if (!serializationType.IsGenericTypeDefinition)
            throw new InvalidOperationException($"{serializationType.Name} shall be a generic type and shall be defined using \"<>\"");
        var clone = Clone();
        clone.ValueSerDesSelectorType = serializationType;
        return clone;
    }
    /// <inheritdoc cref="KEFCoreDbContext.ValueContainerType"/>
    public virtual KEFCoreOptionsExtension WithValueContainerType(Type serializationType)
    {
        if (!serializationType.IsGenericTypeDefinition)
            throw new InvalidOperationException($"{serializationType.Name} shall be a generic type and shall be defined using \"<>\"");
        var clone = Clone();
        clone.ValueContainerType = serializationType;
        return clone;
    }
    /// <inheritdoc cref="KEFCoreDbContext.UseKeyByteBufferDataTransfer"/>
    public virtual KEFCoreOptionsExtension WithKeyByteBufferDataTransfer(bool useKeyByteBufferDataTransfer = true)
    {
        var clone = Clone();
        clone.UseKeyByteBufferDataTransfer = useKeyByteBufferDataTransfer;
        return clone;
    }
    /// <inheritdoc cref="KEFCoreDbContext.UseValueContainerByteBufferDataTransfer"/>
    public virtual KEFCoreOptionsExtension WithValueContainerByteBufferDataTransfer(bool useValueContainerByteBufferDataTransfer = true)
    {
        var clone = Clone();
        clone.UseValueContainerByteBufferDataTransfer = useValueContainerByteBufferDataTransfer;
        return clone;
    }
    /// <inheritdoc cref="KEFCoreDbContext.BootstrapServers"/>
    public virtual KEFCoreOptionsExtension WithBootstrapServers(string bootstrapServers)
    {
        var clone = Clone();
        clone.BootstrapServers = bootstrapServers;
        return clone;
    }
    /// <inheritdoc cref="KEFCoreDbContext.UseKNetStreams"/>
    public virtual KEFCoreOptionsExtension WithKNetStreams(bool useKNetStreams = true)
    {
        var clone = Clone();
        clone.UseKNetStreams = useKNetStreams;
        return clone;
    }
    /// <inheritdoc cref="KEFCoreDbContext.UsePersistentStorage"/>
    public virtual KEFCoreOptionsExtension WithPersistentStorage(bool usePersistentStorage = true)
    {
        var clone = Clone();
        clone.UsePersistentStorage = usePersistentStorage;
        return clone;
    }
    /// <inheritdoc cref="KEFCoreDbContext.UseCompactedReplicator"/>
    [Obsolete("Option will be removed soon")]
    public virtual KEFCoreOptionsExtension WithCompactedReplicator(bool useCompactedReplicator = true)
    {
        var clone = Clone();
        clone.UseCompactedReplicator = useCompactedReplicator;
        return clone;
    }
    /// <inheritdoc cref="KEFCoreDbContext.UseDeletePolicyForTopic"/>
    public virtual KEFCoreOptionsExtension WithDeletePolicyForTopic(bool useDeletePolicyForTopic = true)
    {
        var clone = Clone();
        clone.UseDeletePolicyForTopic = useDeletePolicyForTopic;
        return clone;
    }
    /// <inheritdoc cref="KEFCoreDbContext.DefaultNumPartitions"/>
    public virtual KEFCoreOptionsExtension WithDefaultNumPartitions(int defaultNumPartitions = 1)
    {
        var clone = Clone();
        clone.DefaultNumPartitions = defaultNumPartitions;
        return clone;
    }
    /// <inheritdoc cref="KEFCoreDbContext.DefaultReplicationFactor"/>
    public virtual KEFCoreOptionsExtension WithDefaultReplicationFactor(short defaultReplicationFactor = 3)
    {
        var clone = Clone();
        clone.DefaultReplicationFactor = defaultReplicationFactor;
        return clone;
    }
    /// <inheritdoc cref="KEFCoreDbContext.TopicConfig"/>
    public virtual KEFCoreOptionsExtension WithTopicConfig(TopicConfigBuilder topicConfigBuilder)
    {
        var clone = Clone();
        clone.TopicConfig = TopicConfigBuilder.CreateFrom(topicConfigBuilder);
        return clone;
    }

    // ── With* methods — context ───────────────────────────────────────────
    /// <inheritdoc cref="KEFCoreDbContext.DefaultConsumerInstances"/>
    [Obsolete("Option will be removed soon")]
    public virtual KEFCoreOptionsExtension WithDefaultConsumerInstances(int? defaultConsumerInstances = null)
    {
        var clone = Clone();
        clone.DefaultConsumerInstances = defaultConsumerInstances;
        return clone;
    }
    /// <inheritdoc cref="KEFCoreDbContext.ConsumerConfig"/>
    [Obsolete("Option will be removed soon")]
    public virtual KEFCoreOptionsExtension WithConsumerConfig(ConsumerConfigBuilder consumerConfigBuilder)
    {
        var clone = Clone();
        clone.ConsumerConfig = ConsumerConfigBuilder.CreateFrom(consumerConfigBuilder);
        return clone;
    }

    /// <inheritdoc cref="KEFCoreDbContext.ApplicationId"/>
    public virtual KEFCoreOptionsExtension WithApplicationId(string? applicationId)
    {
        var clone = Clone();
        clone.ApplicationId = applicationId;
        return clone;
    }
    /// <inheritdoc cref="KEFCoreDbContext.ProducerConfig"/>
    public virtual KEFCoreOptionsExtension WithProducerConfig(ProducerConfigBuilder producerConfigBuilder)
    {
        var clone = Clone();
        clone.ProducerConfig = ProducerConfigBuilder.CreateFrom(producerConfigBuilder);
        return clone;
    }
    /// <inheritdoc cref="KEFCoreDbContext.StreamsConfig"/>
    public virtual KEFCoreOptionsExtension WithStreamsConfig(StreamsConfigBuilder streamsConfigBuilder)
    {
        var clone = Clone();
        clone.StreamsConfig = StreamsConfigBuilder.CreateFrom(streamsConfigBuilder);
        return clone;
    }
    /// <inheritdoc cref="KEFCoreDbContext.ManageEvents"/>
    public virtual KEFCoreOptionsExtension WithManageEvents(bool manageEvents)
    {
        var clone = Clone();
        clone.ManageEvents = manageEvents;
        return clone;
    }
    /// <inheritdoc cref="KEFCoreDbContext.ReadOnlyMode"/>
    public virtual KEFCoreOptionsExtension WithReadOnlyMode(bool readOnlyMode)
    {
        var clone = Clone();
        clone.ReadOnlyMode = readOnlyMode;
        return clone;
    }
    /// <inheritdoc cref="KEFCoreDbContext.DefaultSynchronizationTimeout"/>
    public virtual KEFCoreOptionsExtension WithDefaultSynchronizationTimeout(long defaultSynchronizationTimeout)
    {
        var clone = Clone();
        clone.DefaultSynchronizationTimeout = defaultSynchronizationTimeout;
        return clone;
    }
    /// <inheritdoc cref="KEFCoreDbContext.UseEnumeratorWithPrefetch"/>
    public virtual KEFCoreOptionsExtension WithEnumeratorWithPrefetch(bool useEnumeratorWithPrefetch = true)
    {
        var clone = Clone();
        clone.UseEnumeratorWithPrefetch = useEnumeratorWithPrefetch;
        return clone;
    }
    /// <inheritdoc cref="KEFCoreDbContext.UseStorePrefixScan"/>
    public virtual KEFCoreOptionsExtension WithStorePrefixScan(bool enabled = true)
    {
        var clone = Clone();
        clone.UseStorePrefixScan = enabled;
        return clone;
    }
    /// <inheritdoc cref="KEFCoreDbContext.UseStoreSingleKeyLookup"/>
    public virtual KEFCoreOptionsExtension WithStoreSingleKeyLookup(bool enabled = true)
    {
        var clone = Clone();
        clone.UseStoreSingleKeyLookup = enabled;
        return clone;
    }
    /// <inheritdoc cref="KEFCoreDbContext.UseStoreKeyRange"/>
    public virtual KEFCoreOptionsExtension WithStoreKeyRange(bool enabled = true)
    {
        var clone = Clone();
        clone.UseStoreKeyRange = enabled;
        return clone;
    }
    /// <inheritdoc cref="KEFCoreDbContext.UseStoreReverse"/>
    public virtual KEFCoreOptionsExtension WithStoreReverse(bool enabled = true)
    {
        var clone = Clone();
        clone.UseStoreReverse = enabled;
        return clone;
    }
    /// <inheritdoc cref="KEFCoreDbContext.UseStoreReverseKeyRange"/>
    public virtual KEFCoreOptionsExtension WithStoreReverseKeyRange(bool enabled = true)
    {
        var clone = Clone();
        clone.UseStoreReverseKeyRange = enabled;
        return clone;
    }
    /// <inheritdoc cref="KEFCoreDbContext.UseGlobalTable"/>
    [Obsolete("ApplicationId must be unique per process — UseGlobalTable is no longer needed.")]
    public virtual KEFCoreOptionsExtension WithGlobalTable(bool useGlobalTable = true)
    {
        var clone = Clone();
        clone.UseGlobalTable = useGlobalTable;
        return clone;
    }

    /// <summary>
    /// Build <see cref="StreamsConfigBuilder"/> from options
    /// </summary>
    public virtual StreamsConfigBuilder StreamsOptions()
    {
        StreamsConfig ??= new();
        StreamsConfigBuilder builder = StreamsConfigBuilder.CreateFrom(StreamsConfig);

        builder.KeySerDesSelector = KeySerDesSelectorType;
        builder.ValueSerDesSelector = ValueSerDesSelectorType;

        builder.ApplicationId = ApplicationId;
        builder.BootstrapServers = BootstrapServers;

        string baSerdesName = Class.ClassNameOf<Org.Apache.Kafka.Common.Serialization.Serdes.ByteArraySerde>();
        string bbSerdesName = Class.ClassNameOf<MASES.KNet.Serialization.Serdes.ByteBufferSerde>();

        builder.DefaultKeySerdeClass = !UseKeyByteBufferDataTransfer ? Class.ForName(baSerdesName, true, Class.SystemClassLoader)
                                                                     : Class.ForName(bbSerdesName, true, Class.SystemClassLoader);
        builder.DefaultValueSerdeClass = !UseValueContainerByteBufferDataTransfer ? Class.ForName(baSerdesName, true, Class.SystemClassLoader)
                                                                                  : Class.ForName(bbSerdesName, true, Class.SystemClassLoader);
        builder.DSLStoreSuppliersClass = UsePersistentStorage ? Class.ForName(Class.ClassNameOf<BuiltInDslStoreSuppliers.RocksDBDslStoreSuppliers>(), true, Class.SystemClassLoader)
                                                              : Class.ForName(Class.ClassNameOf<BuiltInDslStoreSuppliers.InMemoryDslStoreSuppliers>(), true, Class.SystemClassLoader);

        //Properties props = builder.ToProperties();
        //if (props.ContainsKey(Org.Apache.Kafka.Streams.StreamsConfig.APPLICATION_ID_CONFIG))
        //{
        //    props.Remove(Org.Apache.Kafka.Streams.StreamsConfig.APPLICATION_ID_CONFIG);
        //}
        //props.Put(Org.Apache.Kafka.Streams.StreamsConfig.APPLICATION_ID_CONFIG, ApplicationId);
        //if (props.ContainsKey(Org.Apache.Kafka.Streams.StreamsConfig.BOOTSTRAP_SERVERS_CONFIG))
        //{
        //    props.Remove(Org.Apache.Kafka.Streams.StreamsConfig.BOOTSTRAP_SERVERS_CONFIG);
        //}
        //props.Put(Org.Apache.Kafka.Streams.StreamsConfig.BOOTSTRAP_SERVERS_CONFIG, BootstrapServers);
        //if (props.ContainsKey(Org.Apache.Kafka.Streams.StreamsConfig.DEFAULT_KEY_SERDE_CLASS_CONFIG))
        //{
        //    props.Remove(Org.Apache.Kafka.Streams.StreamsConfig.DEFAULT_KEY_SERDE_CLASS_CONFIG);
        //}
        //props.Put(Org.Apache.Kafka.Streams.StreamsConfig.DEFAULT_KEY_SERDE_CLASS_CONFIG, Class.ForName("org.apache.kafka.common.serialization.Serdes$ByteArraySerde", true, SystemClassLoader));
        //if (props.ContainsKey(Org.Apache.Kafka.Streams.StreamsConfig.DEFAULT_VALUE_SERDE_CLASS_CONFIG))
        //{
        //    props.Remove(Org.Apache.Kafka.Streams.StreamsConfig.DEFAULT_VALUE_SERDE_CLASS_CONFIG);
        //}
        //props.Put(Org.Apache.Kafka.Streams.StreamsConfig.DEFAULT_VALUE_SERDE_CLASS_CONFIG, Class.ForName("org.apache.kafka.common.serialization.Serdes$ByteArraySerde", true, SystemClassLoader));
        //if (props.ContainsKey(Org.Apache.Kafka.Clients.Consumer.ConsumerConfig.AUTO_OFFSET_RESET_CONFIG))
        //{
        //    props.Remove(Org.Apache.Kafka.Clients.Consumer.ConsumerConfig.AUTO_OFFSET_RESET_CONFIG);
        //}
        //props.Put(Org.Apache.Kafka.Clients.Consumer.ConsumerConfig.AUTO_OFFSET_RESET_CONFIG, "earliest");

        return builder;
    }

    /// <summary>
    /// Build <see cref="Properties"/> for applicationId
    /// </summary>
    public virtual Properties StreamsOptions(string applicationId)
    {
        Properties props = StreamsConfig ?? new();
        if (props.ContainsKey(Org.Apache.Kafka.Streams.StreamsConfig.APPLICATION_ID_CONFIG))
        {
            props.Remove(Org.Apache.Kafka.Streams.StreamsConfig.APPLICATION_ID_CONFIG);
        }
        props.Put(Org.Apache.Kafka.Streams.StreamsConfig.APPLICATION_ID_CONFIG, applicationId);
        if (props.ContainsKey(Org.Apache.Kafka.Streams.StreamsConfig.BOOTSTRAP_SERVERS_CONFIG))
        {
            props.Remove(Org.Apache.Kafka.Streams.StreamsConfig.BOOTSTRAP_SERVERS_CONFIG);
        }
        props.Put(Org.Apache.Kafka.Streams.StreamsConfig.BOOTSTRAP_SERVERS_CONFIG, BootstrapServers);
        if (props.ContainsKey(Org.Apache.Kafka.Streams.StreamsConfig.DEFAULT_KEY_SERDE_CLASS_CONFIG))
        {
            props.Remove(Org.Apache.Kafka.Streams.StreamsConfig.DEFAULT_KEY_SERDE_CLASS_CONFIG);
        }
        props.Put(Org.Apache.Kafka.Streams.StreamsConfig.DEFAULT_KEY_SERDE_CLASS_CONFIG, Class.ForName("org.apache.kafka.common.serialization.Serdes$ByteArraySerde", true, Class.SystemClassLoader));
        if (props.ContainsKey(Org.Apache.Kafka.Streams.StreamsConfig.DEFAULT_VALUE_SERDE_CLASS_CONFIG))
        {
            props.Remove(Org.Apache.Kafka.Streams.StreamsConfig.DEFAULT_VALUE_SERDE_CLASS_CONFIG);
        }
        props.Put(Org.Apache.Kafka.Streams.StreamsConfig.DEFAULT_VALUE_SERDE_CLASS_CONFIG, Class.ForName("org.apache.kafka.common.serialization.Serdes$ByteArraySerde", true, Class.SystemClassLoader));
        if (props.ContainsKey(Org.Apache.Kafka.Clients.Consumer.ConsumerConfig.AUTO_OFFSET_RESET_CONFIG))
        {
            props.Remove(Org.Apache.Kafka.Clients.Consumer.ConsumerConfig.AUTO_OFFSET_RESET_CONFIG);
        }
        props.Put(Org.Apache.Kafka.Clients.Consumer.ConsumerConfig.AUTO_OFFSET_RESET_CONFIG, "earliest");

        return props;
    }
    /// <summary>
    /// Build <see cref="ProducerConfigBuilder"/> for producers
    /// </summary>
    public virtual ProducerConfigBuilder ProducerOptionsBuilder()
    {
        ProducerConfigBuilder props = ProducerConfig ?? new();
        props.BootstrapServers = BootstrapServers;
        props.Acks = ProducerConfigBuilder.AcksTypes.All;
        props.Retries = 0;
        props.LingerMs = 1;
        return props;
    }
    /// <summary>
    /// Build <see cref="Properties"/> for producers
    /// </summary>
    public virtual Properties ProducerOptions()
    {
        Properties props = ProducerConfig ?? new();
        if (props.ContainsKey(Org.Apache.Kafka.Clients.Producer.ProducerConfig.BOOTSTRAP_SERVERS_CONFIG))
        {
            props.Remove(Org.Apache.Kafka.Clients.Producer.ProducerConfig.BOOTSTRAP_SERVERS_CONFIG);
        }
        props.Put(Org.Apache.Kafka.Clients.Producer.ProducerConfig.BOOTSTRAP_SERVERS_CONFIG, BootstrapServers);
        if (!props.ContainsKey(Org.Apache.Kafka.Clients.Producer.ProducerConfig.ACKS_CONFIG))
        {
            props.Put(Org.Apache.Kafka.Clients.Producer.ProducerConfig.ACKS_CONFIG, "all");
        }
        if (!props.ContainsKey(Org.Apache.Kafka.Clients.Producer.ProducerConfig.RETRIES_CONFIG))
        {
            props.Put(Org.Apache.Kafka.Clients.Producer.ProducerConfig.RETRIES_CONFIG, 0);
        }
        if (!props.ContainsKey(Org.Apache.Kafka.Clients.Producer.ProducerConfig.LINGER_MS_CONFIG))
        {
            props.Put(Org.Apache.Kafka.Clients.Producer.ProducerConfig.LINGER_MS_CONFIG, 1);
        }
        //if (props.ContainsKey(ProducerConfig.KEY_SERIALIZER_CLASS_CONFIG))
        //{
        //    props.Remove(ProducerConfig.KEY_SERIALIZER_CLASS_CONFIG);
        //}
        //props.Put(ProducerConfig.KEY_SERIALIZER_CLASS_CONFIG, Class.ForName("org.apache.kafka.common.serialization.StringSerializer", true, SystemClassLoader));
        //if (props.ContainsKey(ProducerConfig.VALUE_SERIALIZER_CLASS_CONFIG))
        //{
        //    props.Remove(ProducerConfig.VALUE_SERIALIZER_CLASS_CONFIG);
        //}
        //props.Put(ProducerConfig.VALUE_SERIALIZER_CLASS_CONFIG, Class.ForName("org.apache.kafka.common.serialization.StringSerializer", true, SystemClassLoader));

        return props;
    }

    #region IDbContextOptionsExtension

    /// <inheritdoc/>
    public virtual void ApplyServices(IServiceCollection services)
        => services.AddEntityFrameworkKNetDatabase();
    /// <inheritdoc/>
    public virtual void Validate(IDbContextOptions options)
    {
        var kefcoreOptions = options.FindExtension<KEFCoreOptionsExtension>()
            ?? throw new InvalidOperationException($"Cannot find an instance of {nameof(KEFCoreOptionsExtension)}");
        if (!UseCompactedReplicator && string.IsNullOrEmpty(ApplicationId))
            throw new ArgumentException("Cannot be null or empty when Streams based backend is in use.", nameof(ApplicationId));
        if (string.IsNullOrEmpty(kefcoreOptions.BootstrapServers))
            throw new ArgumentException("It is mandatory", "BootstrapServers");
    }

    /// <inheritdoc/>
    public void Initialize(IDbContextOptions options)
    {
        var other = options.FindExtension<KEFCoreOptionsExtension>()
            ?? throw new InvalidOperationException($"Cannot find an instance of {nameof(KEFCoreOptionsExtension)}");

        if (other.ClusterId != ClusterId
            || other.KeySerDesSelectorType != KeySerDesSelectorType
            || other.ValueSerDesSelectorType != ValueSerDesSelectorType
            || other.ValueContainerType != ValueContainerType
            || other.UseKeyByteBufferDataTransfer != UseKeyByteBufferDataTransfer
            || other.UseValueContainerByteBufferDataTransfer != UseValueContainerByteBufferDataTransfer
            || other.UseKNetStreams != UseKNetStreams
            || other.UsePersistentStorage != UsePersistentStorage
            || other.UseCompactedReplicator != UseCompactedReplicator)
        {
            throw new InvalidOperationException(
                "Cannot reuse internal service provider: singleton options mismatch.");
        }
    }

    #endregion

    #region ExtensionInfo

    private sealed class ExtensionInfo(IDbContextOptionsExtension extension)
        : DbContextOptionsExtensionInfo(extension)
    {
        private string? _logFragment;
        private new KEFCoreOptionsExtension Extension => (KEFCoreOptionsExtension)base.Extension;

        public override bool IsDatabaseProvider => true;

        public override string LogFragment
        {
            get
            {
                if (_logFragment == null)
                {
                    var builder = new System.Text.StringBuilder();
                    // singleton
                    builder.Append("KeySerDesSelectorType=").Append(Extension.KeySerDesSelectorType).Append(' ');
                    builder.Append("ValueSerDesSelectorType=").Append(Extension.ValueSerDesSelectorType).Append(' ');
                    builder.Append("ValueContainerType=").Append(Extension.ValueContainerType).Append(' ');
                    builder.Append("UseKeyByteBufferDataTransfer=").Append(Extension.UseKeyByteBufferDataTransfer).Append(' ');
                    builder.Append("UseValueContainerByteBufferDataTransfer=").Append(Extension.UseValueContainerByteBufferDataTransfer).Append(' ');
                    builder.Append("BootstrapServers=").Append(Extension.BootstrapServers).Append(' ');
                    builder.Append("UseKNetStreams=").Append(Extension.UseKNetStreams).Append(' ');
                    builder.Append("UsePersistentStorage=").Append(Extension.UsePersistentStorage).Append(' ');
                    builder.Append("UseCompactedReplicator=").Append(Extension.UseCompactedReplicator).Append(' ');
                    builder.Append("UseDeletePolicyForTopic=").Append(Extension.UseDeletePolicyForTopic).Append(' ');
                    builder.Append("DefaultNumPartitions=").Append(Extension.DefaultNumPartitions).Append(' ');
                    builder.Append("DefaultReplicationFactor=").Append(Extension.DefaultReplicationFactor).Append(' ');
                    // context
                    builder.Append("DefaultConsumerInstances=").Append(Extension.DefaultConsumerInstances).Append(' ');
                    builder.Append("ConsumerConfig=").Append(Extension.ConsumerConfig?.ToString()).Append(' ');
                    builder.Append("ApplicationId=").Append(Extension.ApplicationId).Append(' ');
                    builder.Append("ManageEvents=").Append(Extension.ManageEvents).Append(' ');
                    builder.Append("ReadOnlyMode=").Append(Extension.ReadOnlyMode).Append(' ');
                    builder.Append("DefaultSynchronizationTimeout=").Append(Extension.DefaultSynchronizationTimeout).Append(' ');
                    builder.Append("UseEnumeratorWithPrefetch=").Append(Extension.UseEnumeratorWithPrefetch).Append(' ');
                    builder.Append("UseStorePrefixScan=").Append(Extension.UseStorePrefixScan).Append(' ');
                    builder.Append("UseStoreSingleKeyLookup=").Append(Extension.UseStoreSingleKeyLookup).Append(' ');
                    builder.Append("UseStoreKeyRange=").Append(Extension.UseStoreKeyRange).Append(' ');
                    builder.Append("UseStoreReverse=").Append(Extension.UseStoreReverse).Append(' ');
                    builder.Append("UseStoreReverseKeyRange=").Append(Extension.UseStoreReverseKeyRange).Append(' ');
                    builder.Append("UseGlobalTable=").Append(Extension.UseGlobalTable).Append(' ');
                    _logFragment = builder.ToString();
                }
                return _logFragment;
            }
        }

        // ClusterId + singleton options che cambiano i servizi nel SP
        public override int GetServiceProviderHashCode()
        {
            var hash = new HashCode();
            hash.Add(Extension.ClusterId);
            hash.Add(Extension.KeySerDesSelectorType);
            hash.Add(Extension.ValueSerDesSelectorType);
            hash.Add(Extension.ValueContainerType);
            hash.Add(Extension.UseKeyByteBufferDataTransfer);
            hash.Add(Extension.UseValueContainerByteBufferDataTransfer);
            hash.Add(Extension.UseKNetStreams);
            hash.Add(Extension.UsePersistentStorage);
            hash.Add(Extension.UseCompactedReplicator);
            return hash.ToHashCode();
        }

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other)
            => other is ExtensionInfo o
            && Extension.ClusterId == o.Extension.ClusterId
            && Extension.KeySerDesSelectorType == o.Extension.KeySerDesSelectorType
            && Extension.ValueSerDesSelectorType == o.Extension.ValueSerDesSelectorType
            && Extension.ValueContainerType == o.Extension.ValueContainerType
            && Extension.UseKeyByteBufferDataTransfer == o.Extension.UseKeyByteBufferDataTransfer
            && Extension.UseValueContainerByteBufferDataTransfer == o.Extension.UseValueContainerByteBufferDataTransfer
            && Extension.UseKNetStreams == o.Extension.UseKNetStreams
            && Extension.UsePersistentStorage == o.Extension.UsePersistentStorage
            && Extension.UseCompactedReplicator == o.Extension.UseCompactedReplicator;

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
        {
            debugInfo["KEFCore:ClusterId"] = (Extension.ClusterId?.GetHashCode() ?? 0).ToString(CultureInfo.InvariantCulture);
            debugInfo["KEFCore:UseKNetStreams"] = Extension.UseKNetStreams.ToString();
            debugInfo["KEFCore:UsePersistentStorage"] = Extension.UsePersistentStorage.ToString();
            debugInfo["KEFCore:ApplicationId"] = Extension.ApplicationId ?? "(none)";
        }
    }

    #endregion
}
