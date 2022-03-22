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

using MASES.EntityFrameworkCore.KNet.Diagnostics;
using MASES.EntityFrameworkCore.KNet.Infrastructure;
using MASES.EntityFrameworkCore.KNet.Infrastructure.Internal;

namespace MASES.EntityFrameworkCore.KNet;

/// <summary>
///     Kafka specific extension methods for <see cref="DbContextOptionsBuilder" />.
/// </summary>
public static class KafkaDbContextOptionsExtensions
{
    /// <summary>
    ///     Configures the context to connect to an Kafka database.
    ///     The Kafka database is shared anywhere the same name is used, but only for a given
    ///     service provider.
    /// </summary>
    /// <remarks>
    ///     See <see href="https://aka.ms/efcore-docs-dbcontext-options">Using DbContextOptions</see>, and
    ///     <see href="https://github.com/masesgroup/EntityFramework4Kafka">The EF Core Kafka database provider</see> for more information and examples.
    /// </remarks>
    /// <typeparam name="TContext">The type of context being configured.</typeparam>
    /// <param name="optionsBuilder">The builder being used to configure the context.</param>
    /// <param name="databaseName">
    ///     The name of the Kafka database. This allows the scope of the Kafka database to be controlled
    ///     independently of the context. The Kafka database is shared anywhere the same name is used.
    /// </param>
    /// <param name="bootstrapServers">
    ///     The bootstrap servers of the Kafka cluster.
    /// </param>
    /// <param name="kafkaOptionsAction">An optional action to allow additional Kafka specific configuration.</param>
    /// <returns>The options builder so that further configuration can be chained.</returns>
    public static DbContextOptionsBuilder<TContext> UseKafkaDatabase<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        string databaseName,
        string bootstrapServers,
        Action<KafkaDbContextOptionsBuilder>? kafkaOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)UseKafkaDatabase(
            (DbContextOptionsBuilder)optionsBuilder, databaseName, bootstrapServers, kafkaOptionsAction);

    /// <summary>
    ///     Configures the context to connect to a named Kafka database.
    ///     The Kafka database is shared anywhere the same name is used, but only for a given
    ///     service provider.
    /// </summary>
    /// <remarks>
    ///     See <see href="https://aka.ms/efcore-docs-dbcontext-options">Using DbContextOptions</see>, and
    ///     <see href="https://github.com/masesgroup/EntityFramework4Kafka">The EF Core Kafka database provider</see> for more information and examples.
    /// </remarks>
    /// <param name="optionsBuilder">The builder being used to configure the context.</param>
    /// <param name="databaseName">
    ///     The name of the Kafka database. This allows the scope of the Kafka database to be controlled
    ///     independently of the context. The Kafka database is shared anywhere the same name is used.
    /// </param>
    /// <param name="bootstrapServers">
    ///     The bootstrap servers of the Kafka cluster.
    /// </param>
    /// <param name="kafkaOptionsAction">An optional action to allow additional Kafka specific configuration.</param>
    /// <returns>The options builder so that further configuration can be chained.</returns>
    public static DbContextOptionsBuilder UseKafkaDatabase(
        this DbContextOptionsBuilder optionsBuilder,
        string databaseName,
        string bootstrapServers,
        Action<KafkaDbContextOptionsBuilder>? kafkaOptionsAction = null)
    {
        Check.NotNull(optionsBuilder, nameof(optionsBuilder));
        Check.NotEmpty(databaseName, nameof(databaseName));
        Check.NotEmpty(bootstrapServers, nameof(bootstrapServers));

        var extension = optionsBuilder.Options.FindExtension<KafkaOptionsExtension>()
            ?? new KafkaOptionsExtension();

        extension = extension.WithDatabaseName(databaseName).WithBootstrapServers(bootstrapServers);

        ConfigureWarnings(optionsBuilder);

        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);

        kafkaOptionsAction?.Invoke(new KafkaDbContextOptionsBuilder(optionsBuilder));

        return optionsBuilder;
    }

    private static void ConfigureWarnings(DbContextOptionsBuilder optionsBuilder)
    {
        // Set warnings defaults
        var coreOptionsExtension
            = optionsBuilder.Options.FindExtension<CoreOptionsExtension>()
            ?? new CoreOptionsExtension();

        coreOptionsExtension = coreOptionsExtension.WithWarningsConfiguration(
            coreOptionsExtension.WarningsConfiguration.TryWithExplicit(
                KafkaEventId.TransactionIgnoredWarning, WarningBehavior.Throw));

        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(coreOptionsExtension);
    }
}
