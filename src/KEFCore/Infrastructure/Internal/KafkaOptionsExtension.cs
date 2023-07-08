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

using Java.Util;
using MASES.JCOBridge.C2JBridge;
using MASES.KNet.Common;
using MASES.KNet.Producer;
using MASES.KNet.Streams;
using Org.Apache.Kafka.Clients.Consumer;
using Org.Apache.Kafka.Clients.Producer;
using Org.Apache.Kafka.Streams;
using System.Globalization;
using System.Text;

namespace MASES.EntityFrameworkCore.KNet.Infrastructure.Internal;

public class KafkaOptionsExtension : IDbContextOptionsExtension
{
    private bool _useNameMatching = true;
    private string? _databaseName;
    private string? _applicationId;
    private string? _bootstrapServers;
    private bool _producerByEntity = false;
    private bool _usePersistentStorage = false;
    private int _defaultNumPartitions = 1;
    private short _defaultReplicationFactor = 1;
    private ProducerConfigBuilder? _producerConfigBuilder;
    private StreamsConfigBuilder? _streamsConfigBuilder;
    private TopicConfigBuilder? _topicConfigBuilder;
    private DbContextOptionsExtensionInfo? _info;

    public KafkaOptionsExtension()
    {
    }

    protected KafkaOptionsExtension(KafkaOptionsExtension copyFrom)
    {
        _useNameMatching = copyFrom._useNameMatching;
        _databaseName = copyFrom._databaseName;
        _applicationId = copyFrom._applicationId;
        _bootstrapServers = copyFrom._bootstrapServers;
        _producerByEntity = copyFrom._producerByEntity;
        _usePersistentStorage = copyFrom._usePersistentStorage;
        _defaultNumPartitions = copyFrom._defaultNumPartitions;
        _defaultReplicationFactor = copyFrom._defaultReplicationFactor;
        _producerConfigBuilder = ProducerConfigBuilder.CreateFrom(copyFrom._producerConfigBuilder);
        _streamsConfigBuilder = StreamsConfigBuilder.CreateFrom(copyFrom._streamsConfigBuilder);
        _topicConfigBuilder = TopicConfigBuilder.CreateFrom(copyFrom._topicConfigBuilder);
    }

    public virtual DbContextOptionsExtensionInfo Info => _info ??= new ExtensionInfo(this);

    protected virtual KafkaOptionsExtension Clone() => new(this);

    public virtual string ClusterId => _bootstrapServers!;

    public virtual bool UseNameMatching => _useNameMatching;

    public virtual string DatabaseName => _databaseName!;

    public virtual string ApplicationId => _applicationId!;

    public virtual string BootstrapServers => _bootstrapServers!;

    public virtual bool ProducerByEntity => _producerByEntity;

    public virtual bool UsePersistentStorage => _usePersistentStorage;

    public virtual int DefaultNumPartitions => _defaultNumPartitions;

    public virtual short DefaultReplicationFactor => _defaultReplicationFactor;

    public virtual ProducerConfigBuilder ProducerConfigBuilder => _producerConfigBuilder!;

    public virtual StreamsConfigBuilder StreamsConfigBuilder => _streamsConfigBuilder!;

    public virtual TopicConfigBuilder TopicConfigBuilder => _topicConfigBuilder!;

    public virtual KafkaOptionsExtension WithUseNameMatching(bool useNameMatching = true)
    {
        var clone = Clone();

        clone._useNameMatching = useNameMatching;

        return clone;
    }

    public virtual KafkaOptionsExtension WithDatabaseName(string databaseName)
    {
        var clone = Clone();

        clone._databaseName = databaseName;

        return clone;
    }

    public virtual KafkaOptionsExtension WithApplicationId(string applicationId)
    {
        var clone = Clone();

        clone._applicationId = applicationId;

        return clone;
    }

    public virtual KafkaOptionsExtension WithBootstrapServers(string bootstrapServers)
    {
        var clone = Clone();

        clone._bootstrapServers = bootstrapServers;

        return clone;
    }

    public virtual KafkaOptionsExtension WithProducerByEntity(bool producerByEntity = false)
    {
        var clone = Clone();

        clone._producerByEntity = producerByEntity;

        return clone;
    }

    public virtual KafkaOptionsExtension WithUsePersistentStorage(bool usePersistentStorage = false)
    {
        var clone = Clone();

        clone._usePersistentStorage = usePersistentStorage;

        return clone;
    }

    public virtual KafkaOptionsExtension WithDefaultNumPartitions(int defaultNumPartitions = 1)
    {
        var clone = Clone();

        clone._defaultNumPartitions = defaultNumPartitions;

        return clone;
    }

    public virtual KafkaOptionsExtension WithDefaultReplicationFactor(short defaultReplicationFactor = 1)
    {
        var clone = Clone();

        clone._defaultReplicationFactor = defaultReplicationFactor;

        return clone;
    }

    public virtual KafkaOptionsExtension WithProducerConfig(ProducerConfigBuilder producerConfigBuilder)
    {
        var clone = Clone();

        clone._producerConfigBuilder = ProducerConfigBuilder.CreateFrom(producerConfigBuilder);

        return clone;
    }

    public virtual KafkaOptionsExtension WithStreamsConfig(StreamsConfigBuilder streamsConfigBuilder)
    {
        var clone = Clone();

        clone._streamsConfigBuilder = StreamsConfigBuilder.CreateFrom(streamsConfigBuilder);

        return clone;
    }

    public virtual KafkaOptionsExtension WithTopicConfig(TopicConfigBuilder topicConfigBuilder)
    {
        var clone = Clone();

        clone._topicConfigBuilder = TopicConfigBuilder.CreateFrom(topicConfigBuilder);

        return clone;
    }

    public virtual Properties StreamsOptions(IEntityType entityType)
    {
        return StreamsOptions(entityType.ApplicationIdForTable(this));
    }

    public virtual Properties StreamsOptions(string applicationId)
    {
        var props = new Properties();
        var localCfg = StreamsConfigBuilder.CreateFrom(StreamsConfigBuilder).WithApplicationId(applicationId)
                                                                            .WithBootstrapServers(BootstrapServers)
                                                                            .WithDefaultKeySerdeClass(Org.Apache.Kafka.Common.Serialization.Serdes.String().Dyn().getClass())
                                                                            .WithDefaultValueSerdeClass(Org.Apache.Kafka.Common.Serialization.Serdes.String().Dyn().getClass());


        props.Put(StreamsConfig.APPLICATION_ID_CONFIG, applicationId);
        props.Put(StreamsConfig.BOOTSTRAP_SERVERS_CONFIG, BootstrapServers);
        props.Put(StreamsConfig.DEFAULT_KEY_SERDE_CLASS_CONFIG, Org.Apache.Kafka.Common.Serialization.Serdes.String().Dyn().getClass());
        props.Put(StreamsConfig.DEFAULT_VALUE_SERDE_CLASS_CONFIG, Org.Apache.Kafka.Common.Serialization.Serdes.String().Dyn().getClass());

        props.Put(ConsumerConfig.AUTO_OFFSET_RESET_CONFIG, "earliest");

        return props;
    }

    public virtual Properties ProducerOptions()
    {
        Properties props = new();
        props.Put(ProducerConfig.BOOTSTRAP_SERVERS_CONFIG, BootstrapServers);
        props.Put(ProducerConfig.ACKS_CONFIG, "all");
        props.Put(ProducerConfig.RETRIES_CONFIG, 0);
        props.Put(ProducerConfig.LINGER_MS_CONFIG, 1);
        props.Put(ProducerConfig.KEY_SERIALIZER_CLASS_CONFIG, "org.apache.kafka.common.serialization.StringSerializer");
        props.Put(ProducerConfig.VALUE_SERIALIZER_CLASS_CONFIG, "org.apache.kafka.common.serialization.StringSerializer");

        return props;
    }

    public virtual void ApplyServices(IServiceCollection services) => services.AddEntityFrameworkKafkaDatabase();

    public virtual void Validate(IDbContextOptions options)
    {
        var kafkaOptions = options.FindExtension<KafkaOptionsExtension>();

        if (kafkaOptions == null) throw new InvalidOperationException("Cannot find an instance of KafkaOptionsExtension");

        if (string.IsNullOrEmpty(kafkaOptions.DatabaseName)) throw new ArgumentException("It is manadatory", "DatabaseName");
        if (string.IsNullOrEmpty(kafkaOptions.ApplicationId)) throw new ArgumentException("It is manadatory", "ApplicationId");
        if (string.IsNullOrEmpty(kafkaOptions.BootstrapServers)) throw new ArgumentException("It is manadatory", "BootstrapServers");
    }

    private sealed class ExtensionInfo : DbContextOptionsExtensionInfo
    {
        private string? _logFragment;

        public ExtensionInfo(IDbContextOptionsExtension extension)
            : base(extension)
        {
        }

        private new KafkaOptionsExtension Extension
            => (KafkaOptionsExtension)base.Extension;

        public override bool IsDatabaseProvider
            => true;

        public override string LogFragment
        {
            get
            {
                if (_logFragment == null)
                {
                    var builder = new StringBuilder();

                    builder.Append("DataBaseName=").Append(Extension._databaseName).Append(' ');

                    _logFragment = builder.ToString();
                }

                return _logFragment;
            }
        }

        public override int GetServiceProviderHashCode()
            => Extension._bootstrapServers?.GetHashCode() ?? 0;

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other)
            => other is ExtensionInfo otherInfo
                && Extension._bootstrapServers == otherInfo.Extension._bootstrapServers;

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
            => debugInfo["KafkaDatabase:BootstrapServers"]
                = (Extension._bootstrapServers?.GetHashCode() ?? 0).ToString(CultureInfo.InvariantCulture);
    }
}
