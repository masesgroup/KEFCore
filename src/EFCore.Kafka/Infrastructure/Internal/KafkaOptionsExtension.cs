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

using MASES.JCOBridge.C2JBridge;
using MASES.KafkaBridge.Clients.Consumer;
using MASES.KafkaBridge.Clients.Producer;
using MASES.KafkaBridge.Java.Util;
using MASES.KafkaBridge.Streams;
using System.Globalization;
using System.Text;

namespace MASES.EntityFrameworkCore.Kafka.Infrastructure.Internal;

public class KafkaOptionsExtension : IDbContextOptionsExtension
{
    private bool _useNameMatching = true;
    private string? _applicationId = Guid.NewGuid().ToString();
    private string? _databaseName;
    private string? _bootstrapServers;
    private bool _producerByEntity = false;
    private string? _autoOffsetReset;
    private DbContextOptionsExtensionInfo? _info;

    public KafkaOptionsExtension()
    {
    }

    protected KafkaOptionsExtension(KafkaOptionsExtension copyFrom)
    {
        _useNameMatching = copyFrom._useNameMatching;
        _applicationId = copyFrom._applicationId;
        _databaseName = copyFrom._databaseName;
        _bootstrapServers = copyFrom._bootstrapServers;
        _producerByEntity = copyFrom._producerByEntity;
        _autoOffsetReset = copyFrom._autoOffsetReset;
    }

    public virtual DbContextOptionsExtensionInfo Info => _info ??= new ExtensionInfo(this);

    protected virtual KafkaOptionsExtension Clone() => new(this);

    public virtual string ClusterId => _bootstrapServers!;

    public virtual bool UseNameMatching => _useNameMatching;

    public virtual string ApplicationId => _applicationId!;

    public virtual string DatabaseName => _databaseName!;

    public virtual string BootstrapServers => _bootstrapServers!;

    public virtual bool ProducerByEntity => _producerByEntity;

    public virtual string AutoOffsetReset => _autoOffsetReset!;

    public virtual KafkaOptionsExtension WithUseNameMatching(bool useNameMatching = true)
    {
        var clone = Clone();

        clone._useNameMatching = useNameMatching;

        return clone;
    }

    public virtual KafkaOptionsExtension WithApplicationId(string applicationId)
    {
        var clone = Clone();

        clone._applicationId = applicationId;

        return clone;
    }

    public virtual KafkaOptionsExtension WithDatabaseName(string databaseName)
    {
        var clone = Clone();

        clone._databaseName = databaseName;

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

    public virtual KafkaOptionsExtension WithAutoOffsetReset(string autoOffsetReset)
    {
        var clone = Clone();

        clone._autoOffsetReset = autoOffsetReset;

        return clone;
    }

    public virtual Properties StreamsOptions(string applicationId)
    {
        var props = new Properties();

        props.Put(StreamsConfig.APPLICATION_ID_CONFIG, applicationId);
        props.Put(StreamsConfig.BOOTSTRAP_SERVERS_CONFIG, BootstrapServers);
        props.Put(StreamsConfig.CACHE_MAX_BYTES_BUFFERING_CONFIG, 0);
        props.Put(StreamsConfig.DEFAULT_KEY_SERDE_CLASS_CONFIG, KafkaBridge.Common.Serialization.Serdes.String.Dyn().getClass());
        props.Put(StreamsConfig.DEFAULT_VALUE_SERDE_CLASS_CONFIG, KafkaBridge.Common.Serialization.Serdes.String.Dyn().getClass());

        // setting offset reset to earliest so that we can re-run the demo code with the same pre-loaded data
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

    public virtual int InitialDataWaitingTime => 10000;

    public virtual int MinDataWaitingTime => 100;

    public virtual void ApplyServices(IServiceCollection services) => services.AddEntityFrameworkKafkaDatabase();

    public virtual void Validate(IDbContextOptions options)
    {
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

                    builder.Append("StoreName=").Append(Extension._applicationId).Append(' ');

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
