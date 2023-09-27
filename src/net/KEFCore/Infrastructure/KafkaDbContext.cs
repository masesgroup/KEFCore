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

using MASES.KNet;
using MASES.KNet.Common;
using MASES.KNet.Producer;
using MASES.KNet.Streams;

namespace MASES.EntityFrameworkCore.KNet.Infrastructure;

/// <summary>
///     Allows Kafka specific configuration to be performed on <see cref="DbContext" />.
/// </summary>
public class KafkaDbContext : DbContext
{
    /// <inheritdoc cref="DbContext.DbContext()"/>
    public KafkaDbContext()
    {

    }
    /// <inheritdoc cref="DbContext.DbContext(DbContextOptions)"/>
    public KafkaDbContext(DbContextOptions options) : base(options)
    {

    }

    /// <summary>
    /// The bootstrap servers of the Apache Kafka cluster
    /// </summary>
    public virtual string? BootstrapServers { get; set; }
    /// <summary>
    /// The application id
    /// </summary>
    public virtual string ApplicationId { get; set; } = Guid.NewGuid().ToString();
    /// <summary>
    /// Database name
    /// </summary>
    public virtual string? DbName { get; set; }
    /// <summary>
    /// Database number of partitions
    /// </summary>
    public virtual int DefaultNumPartitions { get; set; } = 10;
    /// <summary>
    /// Database replication factor
    /// </summary>
    public virtual short DefaultReplicationFactor { get; set; } = 1;
    /// <summary>
    /// Database consumr instances used in conjunction with <see cref="UseCompactedReplicator"/>
    /// </summary>
    public virtual int? DefaultConsumerInstances { get; set; } = null;
    /// <summary>
    /// Use persistent storage
    /// </summary>
    public virtual bool UsePersistentStorage { get; set; } = false;
    /// <summary>
    /// Use a producer for each Entity
    /// </summary>
    public bool UseProducerByEntity { get; set; } = false;
    /// <summary>
    /// Use <see cref="MASES.KNet.Replicator.KNetCompactedReplicator{TKey, TValue}"/> instead of Apache Kafka Streams
    /// </summary>
    public virtual bool UseCompactedReplicator { get; set; } = false;
    /// <summary>
    /// The optional <see cref="ProducerConfigBuilder"/>
    /// </summary>
    public virtual ProducerConfigBuilder? ProducerConfigBuilder { get; set; }
    /// <summary>
    /// The optional <see cref="StreamsConfigBuilder"/>
    /// </summary>
    public virtual StreamsConfigBuilder? StreamsConfigBuilder { get; set; }
    /// <summary>
    /// The optional <see cref="TopicConfigBuilder"/>
    /// </summary>
    public virtual TopicConfigBuilder? TopicConfigBuilder { get; set; }
    /// <inheritdoc cref="DbContext.OnConfiguring(DbContextOptionsBuilder)"/>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (BootstrapServers == null)
        {
            throw new ArgumentNullException(nameof(BootstrapServers));
        }

        if (DbName == null) throw new ArgumentNullException(nameof(DbName));

        optionsBuilder.UseKafkaDatabase(ApplicationId, DbName, BootstrapServers, (o) =>
        {
            o.StreamsConfig(StreamsConfigBuilder ?? o.EmptyStreamsConfigBuilder).WithDefaultNumPartitions(DefaultNumPartitions);
            o.WithUsePersistentStorage(UsePersistentStorage);
            o.WithProducerByEntity(UseProducerByEntity);
            o.WithCompactedReplicator(UseCompactedReplicator);
            o.WithDefaultReplicationFactor(DefaultReplicationFactor);
        });
    }
}
