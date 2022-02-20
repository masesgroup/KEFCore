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

namespace MASES.EntityFrameworkCore.Kafka.Infrastructure.Internal;

public class KafkaSingletonOptions : IKafkaSingletonOptions
{
    public virtual void Initialize(IDbContextOptions options)
    {
        var kafkaOptions = options.FindExtension<KafkaOptionsExtension>();

        if (kafkaOptions != null)
        {
            ApplicationId = kafkaOptions.ApplicationId;
            BootstrapServers = kafkaOptions.BootstrapServers;
            AutoOffsetReset = kafkaOptions.AutoOffsetReset;
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

    public virtual string? ApplicationId { get; private set; }

    public virtual string? BootstrapServers { get; private set; }

    public virtual string? AutoOffsetReset { get; private set; }
}
