/*
*  Copyright 2022 - 2025 MASES s.r.l.
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
using MASES.KNet.Serialization;
using System.Collections.Concurrent;

namespace MASES.EntityFrameworkCore.KNet;

/// <summary>
///     Kafka specific extension methods for <see cref="DbContextOptionsBuilder" />.
/// </summary>
public static class KafkaDbContextOptionsExtensions
{
    /// <summary>
    ///     Configures the context to connect to an Apache Kafka cluster.
    ///     The Apache Kafka cluster is shared anywhere the same name is used, but only for a given
    ///     service provider.
    /// </summary>
    /// <remarks>
    ///     See <see href="https://aka.ms/efcore-docs-dbcontext-options">Using DbContextOptions</see>, and
    ///     <see href="https://github.com/masesgroup/KEFCore">The EF Core Apache Kafka cluster provider</see> for more information and examples.
    /// </remarks>
    /// <typeparam name="TContext">The type of context being configured.</typeparam>
    /// <param name="optionsBuilder">The builder being used to configure the context.</param>
    /// <param name="applicationId">
    ///     The name of the application will use <paramref name="databaseName"/>. This allows the scope of the Apache Kafka cluster to be controlled
    ///     independently of the context. The Apache Kafka cluster is shared anywhere the same name is used.
    /// </param>
    /// <param name="databaseName">
    ///     The name of the Apache Kafka cluster. This allows the scope of the Apache Kafka cluster to be controlled
    ///     independently of the context. The Apache Kafka cluster is shared anywhere the same name is used.
    /// </param>
    /// <param name="bootstrapServers">
    ///     The bootstrap servers of the Kafka cluster.
    /// </param>
    /// <param name="kafkaOptionsAction">An optional action to allow additional Apache Kafka cluster specific configuration.</param>
    /// <returns>The options builder so that further configuration can be chained.</returns>
    public static DbContextOptionsBuilder<TContext> UseKafkaCluster<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        string applicationId,
        string databaseName,
        string bootstrapServers,
        Action<KafkaDbContextOptionsBuilder>? kafkaOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)UseKafkaCluster(
            (DbContextOptionsBuilder)optionsBuilder, applicationId, databaseName, bootstrapServers, kafkaOptionsAction);

    /// <summary>
    ///     Configures the context to connect to a named Apache Kafka cluster.
    ///     The Apache Kafka cluster is shared anywhere the same name is used, but only for a given
    ///     service provider.
    /// </summary>
    /// <remarks>
    ///     See <see href="https://aka.ms/efcore-docs-dbcontext-options">Using DbContextOptions</see>, and
    ///     <see href="https://github.com/masesgroup/KEFCore">The EF Core Apache Kafka cluster provider</see> for more information and examples.
    /// </remarks>
    /// <param name="optionsBuilder">The builder being used to configure the context.</param>
    /// <param name="applicationId">
    ///     The name of the application will use <paramref name="databaseName"/>. This allows the scope of the Apache Kafka cluster to be controlled
    ///     independently of the context. The Apache Kafka cluster is shared anywhere the same name is used.
    /// </param>
    /// <param name="databaseName">
    ///     The name of the Apache Kafka cluster. This allows the scope of the Apache Kafka cluster to be controlled
    ///     independently of the context. The Apache Kafka cluster is shared anywhere the same name is used.
    /// </param>
    /// <param name="bootstrapServers">
    ///     The bootstrap servers of the Kafka cluster.
    /// </param>
    /// <param name="kafkaOptionsAction">An optional action to allow additional Kafka specific configuration.</param>
    /// <returns>The options builder so that further configuration can be chained.</returns>
    public static DbContextOptionsBuilder UseKafkaCluster(
        this DbContextOptionsBuilder optionsBuilder,
        string applicationId,
        string databaseName,
        string bootstrapServers,
        Action<KafkaDbContextOptionsBuilder>? kafkaOptionsAction = null)
    {
        Check.NotNull(optionsBuilder, nameof(optionsBuilder));
        Check.NotEmpty(databaseName, nameof(databaseName));
        Check.NotEmpty(bootstrapServers, nameof(bootstrapServers));

        var extension = optionsBuilder.Options.FindExtension<KafkaOptionsExtension>()
            ?? new KafkaOptionsExtension();

        extension = extension.WithApplicationId(applicationId).WithDatabaseName(databaseName).WithBootstrapServers(bootstrapServers);

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
    /// <summary>
    /// Creates a serializer <see cref="Type"/> for keys
    /// </summary>
    public static Type KeyType(this IKafkaSingletonOptions options, IEntityType entityType)
    {
        var primaryKey = entityType.FindPrimaryKey()!.GetKeyType();
        return primaryKey;
    }
    /// <summary>
    /// Create the ValueContainer <see cref="Type"/>
    /// </summary>
    public static Type ValueContainerType(this IKafkaSingletonOptions options, IEntityType entityType)
    {
        return options.ValueContainerType?.MakeGenericType(KeyType(options, entityType))!;
    }
    /// <summary>
    /// Create the ValueContainer <see cref="Type"/>
    /// </summary>
    public static Type JVMKeyType(this IKafkaSingletonOptions options, IEntityType entityType)
    {
        return typeof(byte[]);
    }
    /// <summary>
    /// Create the ValueContainer <see cref="Type"/>
    /// </summary>
    public static Type JVMValueContainerType(this IKafkaSingletonOptions options, IEntityType entityType)
    {
        var selector = SerDesSelectorForValue(options, entityType);
        if (options.UseByteBufferDataTransfer && selector != null && selector.ByteBufferSerDes != null) return typeof(Java.Nio.ByteBuffer);
        return typeof(byte[]);
    }

    private static readonly ConcurrentDictionary<(Type?, IEntityType), ISerDesSelector?> _keySerDesSelctors = new();

    /// <summary>
    /// Creates a serializer <see cref="Type"/> for keys
    /// </summary>
    public static ISerDesSelector? SerDesSelectorForKey(this IKafkaSingletonOptions options, IEntityType entityType)
    {
        return _keySerDesSelctors.GetOrAdd((options.KeySerDesSelectorType, entityType), (o) =>
        {
            var selector = o.Item1?.MakeGenericType(KeyType(options, o.Item2))!;
            return Activator.CreateInstance(selector) as ISerDesSelector;
        });
    }

    private static readonly ConcurrentDictionary<(Type?, IEntityType), ISerDesSelector?> _valueContainerSerDesSelctors = new();

    /// <summary>
    /// Creates a serialzier <see cref="Type"/> for values
    /// </summary>
    public static ISerDesSelector? SerDesSelectorForValue(this IKafkaSingletonOptions options, IEntityType entityType)
    {
        return _valueContainerSerDesSelctors.GetOrAdd((options.ValueSerDesSelectorType, entityType), (o) =>
        {
            var selector = o.Item1?.MakeGenericType(ValueContainerType(options, o.Item2))!;
            return Activator.CreateInstance(selector) as ISerDesSelector;
        });
    }
}
