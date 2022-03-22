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

using MASES.KNet.Clients.Producer;
using MASES.KNet.Common.Config;
using MASES.KNet.Streams;

namespace MASES.EntityFrameworkCore.KNet.Infrastructure.Internal;

public class KafkaSingletonOptions : IKafkaSingletonOptions
{
    public virtual void Initialize(IDbContextOptions options)
    {
        var kafkaOptions = options.FindExtension<KafkaOptionsExtension>();

        if (kafkaOptions != null)
        {
            UseNameMatching = kafkaOptions.UseNameMatching;
            DatabaseName = kafkaOptions.DatabaseName;
            BootstrapServers = kafkaOptions.BootstrapServers;
            ProducerByEntity = kafkaOptions.ProducerByEntity;
            RetrieveWithForEach = kafkaOptions.RetrieveWithForEach;
            ProducerConfigBuilder = ProducerConfigBuilder.CreateFrom(kafkaOptions.ProducerConfigBuilder);
            StreamsConfigBuilder = StreamsConfigBuilder.CreateFrom(kafkaOptions.StreamsConfigBuilder);
            TopicConfigBuilder = TopicConfigBuilder.CreateFrom(kafkaOptions.TopicConfigBuilder);
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
                    nameof(KafkaDbContextOptionsExtensions.UseKafkaDatabase),
                    nameof(DbContextOptionsBuilder.UseInternalServiceProvider)));
        }
    }

    public virtual bool UseNameMatching { get; private set; }

    public virtual string? DatabaseName { get; private set; }

    public virtual string? BootstrapServers { get; private set; }

    public virtual bool ProducerByEntity { get; private set; }

    public virtual bool RetrieveWithForEach { get; private set; }

    public virtual ProducerConfigBuilder? ProducerConfigBuilder { get; private set; }

    public virtual StreamsConfigBuilder? StreamsConfigBuilder { get; private set; }

    public virtual TopicConfigBuilder? TopicConfigBuilder { get; private set; }
}
