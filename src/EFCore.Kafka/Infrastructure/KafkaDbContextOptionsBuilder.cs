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

using MASES.EntityFrameworkCore.Kafka.Infrastructure.Internal;
using MASES.KafkaBridge.Clients.Producer;
using MASES.KafkaBridge.Common.Config;
using MASES.KafkaBridge.Streams;
using System.ComponentModel;

namespace MASES.EntityFrameworkCore.Kafka.Infrastructure;

/// <summary>
///     Allows Kafka specific configuration to be performed on <see cref="DbContextOptions" />.
/// </summary>
/// <remarks>
///     <para>
///         Instances of this class are returned from a call to
///         <see
///             cref="KafkaDbContextOptionsExtensions.UseKafkaDatabase(DbContextOptionsBuilder, string, System.Action{KafkaDbContextOptionsBuilder})" />
///         and it is not designed to be directly constructed in your application code.
///     </para>
///     <para>
///         See <see href="https://aka.ms/efcore-docs-dbcontext-options">Using DbContextOptions</see>, and
///         <see href="https://github.com/masesgroup/EntityFramework4Kafka">The EF Core Kafka database provider</see> for more information and examples.
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
    DbContextOptionsBuilder IKafkaDbContextOptionsBuilderInfrastructure.OptionsBuilder
        => OptionsBuilder;

    public ProducerConfigBuilder EmptyProducerConfigBuilder => ProducerConfigBuilder.Create();

    public StreamsConfigBuilder EmptyStreamsConfigBuilder => StreamsConfigBuilder.Create();

    public TopicConfigBuilder EmptyTopicConfigBuilder => TopicConfigBuilder.Create();

    /// <summary>
    ///     Enables name matching on <see cref="IEntity"/> instead of <see cref="Type"/> matching
    /// </summary>
    /// <remarks>
    ///     See <see href="https://aka.ms/efcore-docs-dbcontext-options">Using DbContextOptions</see>, and
    ///     <see href="https://github.com/masesgroup/EntityFramework4Kafka">The EF Core Kafka database provider</see> for more information and examples.
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

    /// <summary>
    ///     Enables creation of producer for each <see cref="IEntity"/>
    /// </summary>
    /// <remarks>
    ///     See <see href="https://aka.ms/efcore-docs-dbcontext-options">Using DbContextOptions</see>, and
    ///     <see href="https://github.com/masesgroup/EntityFramework4Kafka">The EF Core Kafka database provider</see> for more information and examples.
    /// </remarks>
    /// <param name="producerByEntity">If <see langword="true" />, then each entity will have its own <see cref="KafkaProducer"/>.</param>
    /// <returns>The same builder instance so that multiple calls can be chained.</returns>
    public virtual KafkaDbContextOptionsBuilder WithProducerByEntity(bool producerByEntity = false)
    {
        var extension = OptionsBuilder.Options.FindExtension<KafkaOptionsExtension>()
            ?? new KafkaOptionsExtension();

        extension = extension.WithProducerByEntity(producerByEntity);

        ((IDbContextOptionsBuilderInfrastructure)OptionsBuilder).AddOrUpdateExtension(extension);

        return this;
    }

    /// <summary>
    ///     Enables use of <see cref="ForeachAction"/>, otherwise a <see cref="Materialized"/> store will be used
    /// </summary>
    /// <remarks>
    ///     See <see href="https://aka.ms/efcore-docs-dbcontext-options">Using DbContextOptions</see>, and
    ///     <see href="https://github.com/masesgroup/EntityFramework4Kafka">The EF Core Kafka database provider</see> for more information and examples.
    /// </remarks>
    /// <param name="retrieveWithForEach">If <see langword="true" />, <see cref="ForeachAction"/> will be used.</param>
    /// <returns>The same builder instance so that multiple calls can be chained.</returns>
    public virtual KafkaDbContextOptionsBuilder WithRetrieveWithForEach(bool retrieveWithForEach = true)
    {
        var extension = OptionsBuilder.Options.FindExtension<KafkaOptionsExtension>()
            ?? new KafkaOptionsExtension();

        extension = extension.WithRetrieveWithForEach(retrieveWithForEach);

        ((IDbContextOptionsBuilderInfrastructure)OptionsBuilder).AddOrUpdateExtension(extension);

        return this;
    }

    /// <summary>
    ///     Set properties of <see cref="KafkaProducer"/>.
    /// </summary>
    /// <remarks>
    ///     See <see href="https://aka.ms/efcore-docs-dbcontext-options">Using DbContextOptions</see>, and
    ///     <see href="https://github.com/masesgroup/EntityFramework4Kafka">The EF Core Kafka database provider</see> for more information and examples.
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
    ///     <see href="https://github.com/masesgroup/EntityFramework4Kafka">The EF Core Kafka database provider</see> for more information and examples.
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
    ///     <see href="https://github.com/masesgroup/EntityFramework4Kafka">The EF Core Kafka database provider</see> for more information and examples.
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
