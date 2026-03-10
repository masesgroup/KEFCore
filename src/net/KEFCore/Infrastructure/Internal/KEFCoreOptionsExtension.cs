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
    private Type _keySerDesSelectorType = DefaultKEFCoreSerDes.DefaultKeySerialization;
    private Type _valueSerDesSelectorType = DefaultKEFCoreSerDes.DefaultValueContainerSerialization;
    private Type _valueContainerType = DefaultKEFCoreSerDes.DefaultValueContainer;
    private string? _topicPrefix;
    private string? _applicationId;
    private string? _bootstrapServers;
    private bool _useDeletePolicyForTopic = false;
    private bool _useCompactedReplicator = false;
    private bool _useKNetStreams = true;
    private bool _useGlobalTable = false;
    private bool _usePersistentStorage = false;
    private bool _useEnumeratorWithPrefetch = true;
    private bool _useByteBufferDataTransfer = false;
    private int _defaultNumPartitions = 1;
    private int? _defaultConsumerInstances = null;
    private short _defaultReplicationFactor = 1;
    private ConsumerConfigBuilder? _consumerConfigBuilder;
    private ProducerConfigBuilder? _producerConfigBuilder;
    private StreamsConfigBuilder? _streamsConfigBuilder;
    private TopicConfigBuilder? _topicConfigBuilder;
    private bool _manageEvents = false;
    private bool _readOnlyMode = false;
    private long _defaultSynchronizationTimeout = Timeout.Infinite;
    private DbContextOptionsExtensionInfo? _info;

    /// <summary>
    /// Initializer
    /// </summary>
    public KEFCoreOptionsExtension()
    {
    }
    /// <summary>
    /// Initializer
    /// </summary>
    protected KEFCoreOptionsExtension(KEFCoreOptionsExtension copyFrom)
    {
        _keySerDesSelectorType = copyFrom._keySerDesSelectorType;
        _valueSerDesSelectorType = copyFrom._valueSerDesSelectorType;
        _valueContainerType = copyFrom._valueContainerType;
        _topicPrefix = copyFrom._topicPrefix;
        _applicationId = copyFrom._applicationId;
        _bootstrapServers = copyFrom._bootstrapServers;
        _useDeletePolicyForTopic = copyFrom._useDeletePolicyForTopic;
        _useCompactedReplicator = copyFrom._useCompactedReplicator;
        _useKNetStreams = copyFrom._useKNetStreams;
        _useGlobalTable = copyFrom._useGlobalTable;
        _usePersistentStorage = copyFrom._usePersistentStorage;
        _useEnumeratorWithPrefetch = copyFrom._useEnumeratorWithPrefetch;
        _useByteBufferDataTransfer = copyFrom._useByteBufferDataTransfer;
        _defaultNumPartitions = copyFrom._defaultNumPartitions;
        _defaultConsumerInstances = copyFrom._defaultConsumerInstances;
        _defaultReplicationFactor = copyFrom._defaultReplicationFactor;
        _consumerConfigBuilder = ConsumerConfigBuilder.CreateFrom(copyFrom._consumerConfigBuilder);
        _producerConfigBuilder = ProducerConfigBuilder.CreateFrom(copyFrom._producerConfigBuilder);
        _streamsConfigBuilder = StreamsConfigBuilder.CreateFrom(copyFrom._streamsConfigBuilder);
        _topicConfigBuilder = TopicConfigBuilder.CreateFrom(copyFrom._topicConfigBuilder);
        _manageEvents = copyFrom._manageEvents;
        _readOnlyMode = copyFrom._readOnlyMode;
        _defaultSynchronizationTimeout = copyFrom._defaultSynchronizationTimeout;
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
                _clusterId ??= KEFCoreClusterAdmin.Create(_bootstrapServers).ClusterId;
                if (_clusterId == null) throw new InvalidOperationException($"ClusterId currently not available from {_bootstrapServers}");
                return _clusterId;
            }
        }
    }
    /// <inheritdoc cref="KEFCoreDbContext.KeySerDesSelectorType"/>
    public virtual Type KeySerDesSelectorType => _keySerDesSelectorType;
    /// <inheritdoc cref="KEFCoreDbContext.ValueSerDesSelectorType"/>
    public virtual Type ValueSerDesSelectorType => _valueSerDesSelectorType;
    /// <inheritdoc cref="KEFCoreDbContext.ValueContainerType"/>
    public virtual Type ValueContainerType => _valueContainerType;
    /// <inheritdoc cref="KEFCoreDbContext.TopicPrefix"/>
    public virtual string TopicPrefix => _topicPrefix!;
    /// <inheritdoc cref="KEFCoreDbContext.ApplicationId"/>
    public virtual string ApplicationId => _applicationId!;
    /// <inheritdoc cref="KEFCoreDbContext.BootstrapServers"/>
    public virtual string BootstrapServers => _bootstrapServers!;
    /// <inheritdoc cref="KEFCoreDbContext.UseDeletePolicyForTopic"/>
    public virtual bool UseDeletePolicyForTopic => _useDeletePolicyForTopic;
    /// <inheritdoc cref="KEFCoreDbContext.UseCompactedReplicator"/>
    public virtual bool UseCompactedReplicator => _useCompactedReplicator;
    /// <inheritdoc cref="KEFCoreDbContext.UseKNetStreams"/>
    public virtual bool UseKNetStreams => _useKNetStreams;
    /// <inheritdoc cref="KEFCoreDbContext.UseGlobalTable"/>
    public virtual bool UseGlobalTable => _useGlobalTable;
    /// <inheritdoc cref="KEFCoreDbContext.UsePersistentStorage"/>
    public virtual bool UsePersistentStorage => _usePersistentStorage;
    /// <inheritdoc cref="KEFCoreDbContext.UseByteBufferDataTransfer"/>
    public virtual bool UseByteBufferDataTransfer => _useByteBufferDataTransfer;
    /// <inheritdoc cref="KEFCoreDbContext.UseEnumeratorWithPrefetch"/>
    public virtual bool UseEnumeratorWithPrefetch => _useEnumeratorWithPrefetch;
    /// <inheritdoc cref="KEFCoreDbContext.DefaultNumPartitions"/>
    public virtual int DefaultNumPartitions => _defaultNumPartitions;
    /// <inheritdoc cref="KEFCoreDbContext.DefaultConsumerInstances"/>
    public virtual int? DefaultConsumerInstances => _defaultConsumerInstances;
    /// <inheritdoc cref="KEFCoreDbContext.DefaultReplicationFactor"/>
    public virtual short DefaultReplicationFactor => _defaultReplicationFactor;
    /// <inheritdoc cref="KEFCoreDbContext.ConsumerConfig"/>
    public virtual ConsumerConfigBuilder ConsumerConfig => _consumerConfigBuilder!;
    /// <inheritdoc cref="KEFCoreDbContext.ProducerConfig"/>
    public virtual ProducerConfigBuilder ProducerConfig => _producerConfigBuilder!;
    /// <inheritdoc cref="KEFCoreDbContext.StreamsConfig"/>
    public virtual StreamsConfigBuilder StreamsConfig => _streamsConfigBuilder!;
    /// <inheritdoc cref="KEFCoreDbContext.TopicConfig"/>
    public virtual TopicConfigBuilder TopicConfig => _topicConfigBuilder!;
    /// <inheritdoc cref="KEFCoreDbContext.ManageEvents"/>
    public virtual bool ManageEvents => _manageEvents!;
    /// <inheritdoc cref="KEFCoreDbContext.ReadOnlyMode"/>
    public virtual bool ReadOnlyMode => _readOnlyMode!;
    /// <inheritdoc cref="KEFCoreDbContext.DefaultSynchronizationTimeout"/>
    public virtual long DefaultSynchronizationTimeout => _defaultSynchronizationTimeout!;

    int IKEFCoreSingletonOptions.DefaultReplicationFactor => _defaultReplicationFactor;

    /// <inheritdoc cref="KEFCoreDbContext.KeySerDesSelectorType"/>
    public virtual KEFCoreOptionsExtension WithKeySerDesSelectorType(Type serializationType)
    {
        if (!serializationType.IsGenericTypeDefinition) throw new InvalidOperationException($"{serializationType.Name} shall be a generic type and shall be defined using \"<>\"");

        var clone = Clone();

        clone._keySerDesSelectorType = serializationType;

        return clone;
    }
    /// <inheritdoc cref="KEFCoreDbContext.ValueSerDesSelectorType"/>
    public virtual KEFCoreOptionsExtension WithValueSerDesSelectorType(Type serializationType)
    {
        if (!serializationType.IsGenericTypeDefinition) throw new InvalidOperationException($"{serializationType.Name} shall be a generic type and shall be defined using \"<>\"");

        var clone = Clone();

        clone._valueSerDesSelectorType = serializationType;

        return clone;
    }
    /// <inheritdoc cref="KEFCoreDbContext.ValueContainerType"/>
    public virtual KEFCoreOptionsExtension WithValueContainerType(Type serializationType)
    {
        if (!serializationType.IsGenericTypeDefinition) throw new InvalidOperationException($"{serializationType.Name} shall be a generic type and shall be defined using \"<>\"");

        var clone = Clone();

        clone._valueContainerType = serializationType;

        return clone;
    }
    /// <inheritdoc cref="KEFCoreDbContext.TopicPrefix"/>
    public virtual KEFCoreOptionsExtension WithTopicPrefix(string topicPrefix)
    {
        var clone = Clone();

        clone._topicPrefix = topicPrefix;

        return clone;
    }
    /// <inheritdoc cref="KEFCoreDbContext.ApplicationId"/>
    public virtual KEFCoreOptionsExtension WithApplicationId(string? applicationId)
    {
        var clone = Clone();

        clone._applicationId = applicationId;

        return clone;
    }
    /// <inheritdoc cref="KEFCoreDbContext.BootstrapServers"/>
    public virtual KEFCoreOptionsExtension WithBootstrapServers(string bootstrapServers)
    {
        var clone = Clone();

        clone._bootstrapServers = bootstrapServers;

        return clone;
    }
    /// <inheritdoc cref="KEFCoreDbContext.UseDeletePolicyForTopic"/>
    public virtual KEFCoreOptionsExtension WithUseDeletePolicyForTopic(bool useDeletePolicyForTopic = true)
    {
        var clone = Clone();

        clone._useDeletePolicyForTopic = useDeletePolicyForTopic;

        return clone;
    }
    /// <inheritdoc cref="KEFCoreDbContext.UseCompactedReplicator"/>
    [Obsolete("Option will be removed soon")]
    public virtual KEFCoreOptionsExtension WithCompactedReplicator(bool useCompactedReplicator = true)
    {
        var clone = Clone();

        clone._useCompactedReplicator = useCompactedReplicator;

        return clone;
    }
    /// <inheritdoc cref="KEFCoreDbContext.UseKNetStreams"/>
    public virtual KEFCoreOptionsExtension WithUseKNetStreams(bool useKNetStreams = true)
    {
        var clone = Clone();

        clone._useKNetStreams = useKNetStreams;

        return clone;
    }
    /// <inheritdoc cref="KEFCoreDbContext.UseGlobalTable"/>
    public virtual KEFCoreOptionsExtension WithUseGlobalTable(bool useGlobalTable = true)
    {
        var clone = Clone();

        clone._useGlobalTable = useGlobalTable;

        return clone;
    }
    /// <inheritdoc cref="KEFCoreDbContext.UsePersistentStorage"/>
    public virtual KEFCoreOptionsExtension WithUsePersistentStorage(bool usePersistentStorage = true)
    {
        var clone = Clone();

        clone._usePersistentStorage = usePersistentStorage;

        return clone;
    }
    /// <inheritdoc cref="KEFCoreDbContext.UseEnumeratorWithPrefetch"/>
    public virtual KEFCoreOptionsExtension WithUseEnumeratorWithPrefetch(bool useEnumeratorWithPrefetch = true)
    {
        var clone = Clone();

        clone._useEnumeratorWithPrefetch = useEnumeratorWithPrefetch;

        return clone;
    }
    /// <inheritdoc cref="KEFCoreDbContext.UseByteBufferDataTransfer"/>
    public virtual KEFCoreOptionsExtension WithUseByteBufferDataTransfer(bool useByteBufferDataTransfer = true)
    {
        var clone = Clone();

        clone._useByteBufferDataTransfer = useByteBufferDataTransfer;

        return clone;
    }
    /// <inheritdoc cref="KEFCoreDbContext.DefaultNumPartitions"/>
    public virtual KEFCoreOptionsExtension WithDefaultNumPartitions(int defaultNumPartitions = 1)
    {
        var clone = Clone();

        clone._defaultNumPartitions = defaultNumPartitions;

        return clone;
    }
    /// <inheritdoc cref="KEFCoreDbContext.DefaultConsumerInstances"/>
    public virtual KEFCoreOptionsExtension WithDefaultConsumerInstances(int? defaultConsumerInstances = null)
    {
        var clone = Clone();

        clone._defaultConsumerInstances = defaultConsumerInstances;

        return clone;
    }
    /// <inheritdoc cref="KEFCoreDbContext.DefaultReplicationFactor"/>
    public virtual KEFCoreOptionsExtension WithDefaultReplicationFactor(short defaultReplicationFactor = 3)
    {
        var clone = Clone();

        clone._defaultReplicationFactor = defaultReplicationFactor;

        return clone;
    }
    /// <inheritdoc cref="KEFCoreDbContext.ConsumerConfig"/>
    public virtual KEFCoreOptionsExtension WithConsumerConfig(ConsumerConfigBuilder consumerConfigBuilder)
    {
        var clone = Clone();

        clone._consumerConfigBuilder = ConsumerConfigBuilder.CreateFrom(consumerConfigBuilder);

        return clone;
    }
    /// <inheritdoc cref="KEFCoreDbContext.ProducerConfig"/>
    public virtual KEFCoreOptionsExtension WithProducerConfig(ProducerConfigBuilder producerConfigBuilder)
    {
        var clone = Clone();

        clone._producerConfigBuilder = ProducerConfigBuilder.CreateFrom(producerConfigBuilder);

        return clone;
    }
    /// <inheritdoc cref="KEFCoreDbContext.StreamsConfig"/>
    public virtual KEFCoreOptionsExtension WithStreamsConfig(StreamsConfigBuilder streamsConfigBuilder)
    {
        var clone = Clone();

        clone._streamsConfigBuilder = StreamsConfigBuilder.CreateFrom(streamsConfigBuilder);

        return clone;
    }
    /// <inheritdoc cref="KEFCoreDbContext.TopicConfig"/>
    public virtual KEFCoreOptionsExtension WithTopicConfig(TopicConfigBuilder topicConfigBuilder)
    {
        var clone = Clone();

        clone._topicConfigBuilder = TopicConfigBuilder.CreateFrom(topicConfigBuilder);

        return clone;
    }

    /// <inheritdoc cref="KEFCoreDbContext.ManageEvents"/>
    public virtual KEFCoreOptionsExtension WithManageEvents(bool manageEvents)
    {
        var clone = Clone();

        clone._manageEvents = manageEvents;

        return clone;
    }

    /// <inheritdoc cref="KEFCoreDbContext.ReadOnlyMode"/>
    public virtual KEFCoreOptionsExtension WithReadOnlyMode(bool readOnlyMode)
    {
        var clone = Clone();

        clone._readOnlyMode = readOnlyMode;

        return clone;
    }

    /// <inheritdoc cref="KEFCoreDbContext.DefaultSynchronizationTimeout"/>
    public virtual KEFCoreOptionsExtension WithDefaultSynchronizationTimeout(long defaultSynchronizationTimeout)
    {
        var clone = Clone();

        clone._defaultSynchronizationTimeout = defaultSynchronizationTimeout;

        return clone;
    }

    /// <summary>
    /// Build <see cref="StreamsConfigBuilder"/> from options
    /// </summary>
    public virtual StreamsConfigBuilder StreamsOptions(IEntityType entityType)
    {
        _streamsConfigBuilder ??= new();
        StreamsConfigBuilder builder = StreamsConfigBuilder.CreateFrom(_streamsConfigBuilder);

        builder.KeySerDesSelector = KeySerDesSelectorType;
        builder.ValueSerDesSelector = ValueSerDesSelectorType;

        builder.ApplicationId = ApplicationId;
        builder.BootstrapServers = BootstrapServers;

        string baSerdesName = Class.ClassNameOf<Org.Apache.Kafka.Common.Serialization.Serdes.ByteArraySerde>();
        string bbSerdesName = Class.ClassNameOf<MASES.KNet.Serialization.Serdes.ByteBufferSerde>();

        builder.DefaultKeySerdeClass = this.JVMKeyType(entityType) == typeof(byte[]) ? Class.ForName(baSerdesName, true, Class.SystemClassLoader)
                                                                                     : Class.ForName(bbSerdesName, true, Class.SystemClassLoader);
        builder.DefaultValueSerdeClass = this.JVMValueContainerType(entityType) == typeof(byte[]) ? Class.ForName(baSerdesName, true, Class.SystemClassLoader)
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
        Properties props = _streamsConfigBuilder ?? new();
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
        ProducerConfigBuilder props = _producerConfigBuilder ?? new();
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
        Properties props = _producerConfigBuilder ?? new();
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
    /// <inheritdoc/>
    public virtual void ApplyServices(IServiceCollection services) => services.AddEntityFrameworkKNetDatabase();
    /// <inheritdoc/>
    public virtual void Validate(IDbContextOptions options)
    {
        var kefcoreOptions = options.FindExtension<KEFCoreOptionsExtension>() ?? throw new InvalidOperationException($"Cannot find an instance of {nameof(KEFCoreOptionsExtension)}");
        if (!UseCompactedReplicator && string.IsNullOrEmpty(ApplicationId))
        {
            throw new ArgumentException("Cannot be null or empty when Streams based backend is in use.", nameof(ApplicationId));
        }
        if (string.IsNullOrEmpty(kefcoreOptions.BootstrapServers)) throw new ArgumentException("It is manadatory", "BootstrapServers");
    }
    /// <inheritdoc/>
    public void Initialize(IDbContextOptions options)
    {
        var kefcoreOptions = options.FindExtension<KEFCoreOptionsExtension>() ?? throw new InvalidOperationException($"Cannot find an instance of {nameof(KEFCoreOptionsExtension)}");

        if (kefcoreOptions?.ClusterId != ClusterId)
        {
            throw new InvalidOperationException(
                $"Cannot reuse internal service provider: ClusterId mismatch " +
                $"(expected '{ClusterId}', got '{kefcoreOptions?.ClusterId}')");
        }
    }

    private sealed class ExtensionInfo(IDbContextOptionsExtension extension) : DbContextOptionsExtensionInfo(extension)
    {
        private string? _logFragment;

        private new KEFCoreOptionsExtension Extension
            => (KEFCoreOptionsExtension)base.Extension;

        public override bool IsDatabaseProvider
            => true;

        public override string LogFragment
        {
            get
            {
                if (_logFragment == null)
                {
                    var builder = new System.Text.StringBuilder();

                    builder.Append("KeySerDesSelectorType=").Append(Extension._keySerDesSelectorType).Append(' ');
                    builder.Append("ValueSerDesSelectorType=").Append(Extension._valueSerDesSelectorType).Append(' ');
                    builder.Append("ValueContainerType=").Append(Extension._valueContainerType).Append(' ');
                    builder.Append("TopicPrefix=").Append(Extension._topicPrefix).Append(' ');
                    builder.Append("ApplicationId=").Append(Extension._applicationId).Append(' ');
                    builder.Append("BootstrapServers=").Append(Extension._bootstrapServers).Append(' ');
                    builder.Append("UseDeletePolicyForTopic=").Append(Extension._useDeletePolicyForTopic).Append(' ');
                    builder.Append("UseCompactedReplicator=").Append(Extension._useCompactedReplicator).Append(' ');
                    builder.Append("UseKNetStreams=").Append(Extension._useKNetStreams).Append(' ');
                    builder.Append("UseGlobalTable=").Append(Extension._useGlobalTable).Append(' ');
                    builder.Append("UsePersistentStorage=").Append(Extension._usePersistentStorage).Append(' ');
                    builder.Append("UseEnumeratorWithPrefetch=").Append(Extension._useEnumeratorWithPrefetch).Append(' ');
                    builder.Append("UseByteBufferDataTransfer=").Append(Extension._useByteBufferDataTransfer).Append(' ');
                    builder.Append("DefaultNumPartitions=").Append(Extension._defaultNumPartitions).Append(' ');
                    builder.Append("DefaultReplicationFactor=").Append(Extension._defaultReplicationFactor).Append(' ');
                    builder.Append("DefaultConsumerInstances=").Append(Extension._defaultConsumerInstances).Append(' ');
                    builder.Append("ConsumerConfigBuilder=").Append(Extension._consumerConfigBuilder?.ToString()).Append(' ');
                    builder.Append("ProducerConfigBuilder=").Append(Extension._producerConfigBuilder?.ToString()).Append(' ');
                    builder.Append("StreamsConfigBuilder=").Append(Extension._streamsConfigBuilder?.ToString()).Append(' ');
                    builder.Append("TopicConfigBuilder=").Append(Extension._topicConfigBuilder?.ToString()).Append(' ');
                    builder.Append("ManageEvents=").Append(Extension._manageEvents).Append(' ');
                    builder.Append("ReadOnlyMode=").Append(Extension._readOnlyMode).Append(' ');
                    builder.Append("DefaultSynchronizationTimeout=").Append(Extension._defaultSynchronizationTimeout).Append(' ');
                    _logFragment = builder.ToString();
                }

                return _logFragment;
            }
        }

        public override int GetServiceProviderHashCode()
            => Extension.ClusterId?.GetHashCode() ?? 0;

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other)
            => other is ExtensionInfo otherInfo
                && Extension.ClusterId == otherInfo.Extension.ClusterId;

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
            => debugInfo["KEFCore:ClusterId"]
                = (Extension.ClusterId?.GetHashCode() ?? 0).ToString(CultureInfo.InvariantCulture);
    }
}
