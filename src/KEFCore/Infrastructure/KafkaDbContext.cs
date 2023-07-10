// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

using MASES.KNet.Common;
using MASES.KNet.Producer;
using MASES.KNet.Streams;

namespace MASES.EntityFrameworkCore.KNet.Infrastructure;

/// <summary>
///     Allows Kafka specific configuration to be performed on <see cref="DbContext" />.
/// </summary>
public class KafkaDbContext : DbContext
{
    /// <summary>
    /// The bootstrap servers of the Apache Kafka cluster
    /// </summary>
    public string? BootstrapServers { get; set; }
    /// <summary>
    /// The application id
    /// </summary>
    public string ApplicationId { get; set; } = Guid.NewGuid().ToString();
    /// <summary>
    /// Database name
    /// </summary>
    public string? DbName { get; set; }
    /// <summary>
    /// Database name
    /// </summary>
    public int DefaultNumPartitions { get; set; } = 10;


    public ProducerConfigBuilder? ProducerConfigBuilder { get; set; }

    public StreamsConfigBuilder? StreamsConfigBuilder { get; set; }

    public TopicConfigBuilder? TopicConfigBuilder { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (BootstrapServers == null) throw new ArgumentNullException(nameof(BootstrapServers));
        if (DbName == null) throw new ArgumentNullException(nameof(DbName));

        optionsBuilder.UseKafkaDatabase(ApplicationId, DbName, BootstrapServers, (o) =>
        {
            o.StreamsConfig(StreamsConfigBuilder??o.EmptyStreamsConfigBuilder).WithDefaultNumPartitions(DefaultNumPartitions);
        });
    }
}
