// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

/*
*  Copyright 2023 MASES s.r.l.
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

using MASES.EntityFrameworkCore.KNet.Infrastructure.Internal;
using MASES.KNet.Common;
using MASES.KNet.Producer;
using MASES.KNet.Streams;
using Org.Apache.Kafka.Clients.Producer;
using Org.Apache.Kafka.Streams;
using Org.Apache.Kafka.Streams.Kstream;
using System.ComponentModel;

namespace MASES.EntityFrameworkCore.KNet.Infrastructure;

/// <summary>
///     Allows Kafka specific configuration to be performed on <see cref="DbContextOptions" />.
/// </summary>
/// <remarks>
///     <para>
///         Instances of this class are returned from a call to
///         <see
///             cref="KafkaDbContextOptionsExtensions.UseKafkaDatabase(DbContextOptionsBuilder, string, Action{KafkaDbContextOptionsBuilder})" />
///         and it is not designed to be directly constructed in your application code.
///     </para>
///     <para>
///         See <see href="https://aka.ms/efcore-docs-dbcontext-options">Using DbContextOptions</see>, and
///         <see href="https://github.com/masesgroup/KEFCore">The EF Core Kafka database provider</see> for more information and examples.
///     </para>
/// </remarks>
public class KafkaDbContextOptionsBuilder : IKafkaDbContextOptionsBuilderInfrastructure
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="KafkaDbContextOptionsBuilder" /> class.
    /// </summary>
    /// <param name="optionsBuilder">The options builder.</param>
    public KafkaDbContextOptionsBuilder(DbContextOptionsBuilder optionsBuilder)
    {
        OptionsBuilder = optionsBuilder;
    }

    /// <summary>
    ///     Clones the configuration in this builder.
    /// </summary>
    /// <returns>The cloned configuration.</returns>
    protected virtual DbContextOptionsBuilder OptionsBuilder { get; }

    /// <inheritdoc />
    DbContextOptionsBuilder IKafkaDbContextOptionsBuilderInfrastructure.OptionsBuilder => OptionsBuilder;
    /// <summary>
    ///     The default <see cref="ProducerConfigBuilder"/> configuration
    /// </summary>
    /// <returns>The default <see cref="ProducerConfigBuilder"/> configuration.</returns>
    public ProducerConfigBuilder EmptyProducerConfigBuilder => ProducerConfigBuilder.Create();
    /// <summary>
    ///     The default <see cref="StreamsConfigBuilder"/> configuration
    /// </summary>
    /// <returns>The default <see cref="StreamsConfigBuilder"/> configuration.</returns>
    public StreamsConfigBuilder EmptyStreamsConfigBuilder => StreamsConfigBuilder.Create();
    /// <summary>
    ///     The default <see cref="TopicConfigBuilder"/> configuration
    /// </summary>
    /// <returns>The default <see cref="TopicConfigBuilder"/> configuration.</returns>
    public TopicConfigBuilder EmptyTopicConfigBuilder => TopicConfigBuilder.Create();

    /// <summary>
    ///     Enables name matching on <see cref="IEntityType"/> instead of <see cref="Type"/> matching
    /// </summary>
    /// <remarks>
    ///     See <see href="https://aka.ms/efcore-docs-dbcontext-options">Using DbContextOptions</see>, and
    ///     <see href="https://github.com/masesgroup/KEFCore">The EF Core Kafka database provider</see> for more information and examples.
    /// </remarks>
    /// <param name="useNameMatching">If <see langword="true" />, it is used name matching.</param>
    /// <returns>The same builder instance so that multiple calls can be chained.</returns>
    public virtual KafkaDbContextOptionsBuilder WithUseNameMatching(bool useNameMatching = true)
    {
        var extension = OptionsBuilder.Options.FindExtension<KafkaOptionsExtension>()
            ?? new KafkaOptionsExtension();

        extension = extension.WithUseNameMatching(useNameMatching);

        ((IDbContextOptionsBuilderInfrastructure)OptionsBuilder).AddOrUpdateExtension(extension);

        return this;
    }

    ///// <summary>
    /////     Enables creation of producer for each <see cref="IEntity"/>
    ///// </summary>
    ///// <remarks>
    /////     See <see href="https://aka.ms/efcore-docs-dbcontext-options">Using DbContextOptions</see>, and
    /////     <see href="https://github.com/masesgroup/KEFCore">The EF Core Kafka database provider</see> for more information and examples.
    ///// </remarks>
    ///// <param name="producerByEntity">If <see langword="true" />, then each entity will have its own <see cref="KafkaProducer"/>.</param>
    ///// <returns>The same builder instance so that multiple calls can be chained.</returns>
    //public virtual KafkaDbContextOptionsBuilder WithProducerByEntity(bool producerByEntity = false)
    //{
    //    var extension = OptionsBuilder.Options.FindExtension<KafkaOptionsExtension>()
    //        ?? new KafkaOptionsExtension();

    //    extension = extension.WithProducerByEntity(producerByEntity);

    //    ((IDbContextOptionsBuilderInfrastructure)OptionsBuilder).AddOrUpdateExtension(extension);

    //    return this;
    //}

    /// <summary>
    ///     Enables use of <see cref="MASES.KNet.Replicator.KNetCompactedReplicator{TKey, TValue}"/>
    /// </summary>
    /// <remarks>
    ///     See <see href="https://aka.ms/efcore-docs-dbcontext-options">Using DbContextOptions</see>, and
    ///     <see href="https://github.com/masesgroup/KEFCore">The EF Core Kafka database provider</see> for more information and examples.
    /// </remarks>
    /// <param name="useCompactedReplicator">If <see langword="true" /> then <see cref="MASES.KNet.Replicator.KNetCompactedReplicator{TKey, TValue}"/> will be used instead of Apache Kafka Streams.</param>
    /// <returns>The same builder instance so that multiple calls can be chained.</returns>
    public virtual KafkaDbContextOptionsBuilder WithCompactedReplicator(bool useCompactedReplicator = false)
    {
        var extension = OptionsBuilder.Options.FindExtension<KafkaOptionsExtension>()
            ?? new KafkaOptionsExtension();

        extension = extension.WithCompactedReplicator(useCompactedReplicator);

        ((IDbContextOptionsBuilderInfrastructure)OptionsBuilder).AddOrUpdateExtension(extension);

        return this;
    }

    /// <summary>
    ///     Enables use of persistent storage, otherwise a <see cref="Materialized"/> storage will be in-memory
    /// </summary>
    /// <remarks>
    ///     See <see href="https://aka.ms/efcore-docs-dbcontext-options">Using DbContextOptions</see>, and
    ///     <see href="https://github.com/masesgroup/KEFCore">The EF Core Kafka database provider</see> for more information and examples.
    /// </remarks>
    /// <param name="usePersistentStorage">If <see langword="true" />, persistent storage will be used.</param>
    /// <returns>The same builder instance so that multiple calls can be chained.</returns>
    public virtual KafkaDbContextOptionsBuilder WithUsePersistentStorage(bool usePersistentStorage = false)
    {
        var extension = OptionsBuilder.Options.FindExtension<KafkaOptionsExtension>()
            ?? new KafkaOptionsExtension();

        extension = extension.WithUsePersistentStorage(usePersistentStorage);

        ((IDbContextOptionsBuilderInfrastructure)OptionsBuilder).AddOrUpdateExtension(extension);

        return this;
    }

    /// <summary>
    ///     Defines the default number of partitions to use when a new topic is created
    /// </summary>
    /// <remarks>
    ///     See <see href="https://aka.ms/efcore-docs-dbcontext-options">Using DbContextOptions</see>, and
    ///     <see href="https://github.com/masesgroup/KEFCore">The EF Core Kafka database provider</see> for more information and examples.
    /// </remarks>
    /// <param name="defaultNumPartitions">The default number of partitions to use when a new topic is created.</param>
    /// <returns>The same builder instance so that multiple calls can be chained.</returns>
    public virtual KafkaDbContextOptionsBuilder WithDefaultNumPartitions(int defaultNumPartitions = 1)
    {
        var extension = OptionsBuilder.Options.FindExtension<KafkaOptionsExtension>()
            ?? new KafkaOptionsExtension();

        extension = extension.WithDefaultNumPartitions(defaultNumPartitions);

        ((IDbContextOptionsBuilderInfrastructure)OptionsBuilder).AddOrUpdateExtension(extension);

        return this;
    }

    /// <summary>
    ///     Defines the default number of consumer instances to be used in conjunction with <see cref="WithCompactedReplicator(bool)"/>
    /// </summary>
    /// <remarks>
    ///     See <see href="https://aka.ms/efcore-docs-dbcontext-options">Using DbContextOptions</see>, and
    ///     <see href="https://github.com/masesgroup/KEFCore">The EF Core Kafka database provider</see> for more information and examples.
    /// </remarks>
    /// <param name="defaultConsumerInstances">The default number of consumer instances to be used in conjunction with <see cref="WithCompactedReplicator(bool)"/></param>
    /// <returns>The same builder instance so that multiple calls can be chained.</returns>
    public virtual KafkaDbContextOptionsBuilder WithDefaultConsumerInstances(int? defaultConsumerInstances = null)
    {
        var extension = OptionsBuilder.Options.FindExtension<KafkaOptionsExtension>()
            ?? new KafkaOptionsExtension();

        extension = extension.WithDefaultConsumerInstances(defaultConsumerInstances);

        ((IDbContextOptionsBuilderInfrastructure)OptionsBuilder).AddOrUpdateExtension(extension);

        return this;
    }

    /// <summary>
    ///     Defines the default replication factor to use when a new topic is created
    /// </summary>
    /// <remarks>
    ///     See <see href="https://aka.ms/efcore-docs-dbcontext-options">Using DbContextOptions</see>, and
    ///     <see href="https://github.com/masesgroup/KEFCore">The EF Core Kafka database provider</see> for more information and examples.
    /// </remarks>
    /// <param name="defaultReplicationFactor">The default replication factor to use when a new topic is created.</param>
    /// <returns>The same builder instance so that multiple calls can be chained.</returns>
    public virtual KafkaDbContextOptionsBuilder WithDefaultReplicationFactor(short defaultReplicationFactor = 1)
    {
        var extension = OptionsBuilder.Options.FindExtension<KafkaOptionsExtension>()
            ?? new KafkaOptionsExtension();

        extension = extension.WithDefaultReplicationFactor(defaultReplicationFactor);

        ((IDbContextOptionsBuilderInfrastructure)OptionsBuilder).AddOrUpdateExtension(extension);

        return this;
    }

    /// <summary>
    ///     Set properties of <see cref="KafkaProducer"/>.
    /// </summary>
    /// <remarks>
    ///     See <see href="https://aka.ms/efcore-docs-dbcontext-options">Using DbContextOptions</see>, and
    ///     <see href="https://github.com/masesgroup/KEFCore">The EF Core Kafka database provider</see> for more information and examples.
    /// </remarks>
    /// <param name="producerConfigBuilder">The <see cref="ProducerConfigBuilder"/> where options are stored.</param>
    /// <returns>The same builder instance so that multiple calls can be chained.</returns>
    public virtual KafkaDbContextOptionsBuilder ProducerConfig(ProducerConfigBuilder producerConfigBuilder)
    {
        var extension = OptionsBuilder.Options.FindExtension<KafkaOptionsExtension>()
            ?? new KafkaOptionsExtension();

        extension = extension.WithProducerConfig(producerConfigBuilder);

        ((IDbContextOptionsBuilderInfrastructure)OptionsBuilder).AddOrUpdateExtension(extension);

        return this;
    }

    /// <summary>
    ///     Set properties of <see cref="KafkaStreams"/>.
    /// </summary>
    /// <remarks>
    ///     See <see href="https://aka.ms/efcore-docs-dbcontext-options">Using DbContextOptions</see>, and
    ///     <see href="https://github.com/masesgroup/KEFCore">The EF Core Kafka database provider</see> for more information and examples.
    /// </remarks>
    /// <param name="streamsConfigBuilder">The <see cref="StreamsConfigBuilder"/> where options are stored.</param>
    /// <returns>The same builder instance so that multiple calls can be chained.</returns>
    public virtual KafkaDbContextOptionsBuilder StreamsConfig(StreamsConfigBuilder streamsConfigBuilder)
    {
        var extension = OptionsBuilder.Options.FindExtension<KafkaOptionsExtension>()
            ?? new KafkaOptionsExtension();

        extension = extension.WithStreamsConfig(streamsConfigBuilder);

        ((IDbContextOptionsBuilderInfrastructure)OptionsBuilder).AddOrUpdateExtension(extension);

        return this;
    }

    /// <summary>
    ///      Set properties of <see cref="TopicConfig"/>.
    /// </summary>
    /// <remarks>
    ///     See <see href="https://aka.ms/efcore-docs-dbcontext-options">Using DbContextOptions</see>, and
    ///     <see href="https://github.com/masesgroup/KEFCore">The EF Core Kafka database provider</see> for more information and examples.
    /// </remarks>
    /// <param name="topicConfig">The <see cref="TopicConfigBuilder"/> where options are stored.</param>
    /// <returns>The same builder instance so that multiple calls can be chained.</returns>
    public virtual KafkaDbContextOptionsBuilder TopicConfig(TopicConfigBuilder topicConfig)
    {
        var extension = OptionsBuilder.Options.FindExtension<KafkaOptionsExtension>()
            ?? new KafkaOptionsExtension();

        extension = extension.WithTopicConfig(topicConfig);

        ((IDbContextOptionsBuilderInfrastructure)OptionsBuilder).AddOrUpdateExtension(extension);

        return this;
    }

    #region Hidden System.Object members

    /// <summary>
    ///     Returns a string that represents the current object.
    /// </summary>
    /// <returns>A string that represents the current object.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override string? ToString()
        => base.ToString();

    /// <summary>
    ///     Determines whether the specified object is equal to the current object.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns><see langword="true" /> if the specified object is equal to the current object; otherwise, <see langword="false" />.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override bool Equals(object? obj)
        => base.Equals(obj);

    /// <summary>
    ///     Serves as the default hash function.
    /// </summary>
    /// <returns>A hash code for the current object.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override int GetHashCode()
        => base.GetHashCode();

    #endregion
}
