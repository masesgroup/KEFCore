/*
*  Copyright (c) 2022-2026 MASES s.r.l.
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

namespace MASES.EntityFrameworkCore.KNet.Extensions;

/// <summary>
///     KEFCore specific extension methods for <see cref="DbContextOptionsBuilder" />.
/// </summary>
public static class KEFCoreDbContextOptionsExtensions
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
    ///     The name of the application will use <paramref name="bootstrapServers"/>. This allows the scope of the Apache Kafka cluster to be controlled
    ///     independently of the context. The Apache Kafka cluster is shared anywhere the same name is used.
    /// </param>
    /// <param name="bootstrapServers">
    ///     The bootstrap servers of the Kafka cluster.
    /// </param>
    /// <param name="kefcoreOptionsAction">An optional action to allow additional Apache Kafka cluster specific configuration.</param>
    /// <returns>The options builder so that further configuration can be chained.</returns>
    public static DbContextOptionsBuilder<TContext> UseKEFCore<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        string applicationId,
        string bootstrapServers,
        Action<KEFCoreDbContextOptionsBuilder>? kefcoreOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)UseKEFCore(
            (DbContextOptionsBuilder)optionsBuilder, applicationId, bootstrapServers, kefcoreOptionsAction);

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
    ///     The name of the application will use <paramref name="bootstrapServers"/>. This allows the scope of the Apache Kafka cluster to be controlled
    ///     independently of the context. The Apache Kafka cluster is shared anywhere the same name is used.
    /// </param>
    /// <param name="bootstrapServers">
    ///     The bootstrap servers of the Kafka cluster.
    /// </param>
    /// <param name="kefcoreOptionsAction">An optional action to allow additional Kafka specific configuration.</param>
    /// <returns>The options builder so that further configuration can be chained.</returns>
    public static DbContextOptionsBuilder UseKEFCore(
        this DbContextOptionsBuilder optionsBuilder,
        string? applicationId,
        string bootstrapServers,
        Action<KEFCoreDbContextOptionsBuilder>? kefcoreOptionsAction = null)
    {
        Check.NotNull(optionsBuilder, nameof(optionsBuilder));
        Check.NotEmpty(bootstrapServers, nameof(bootstrapServers));

        var extension = optionsBuilder.Options.FindExtension<KEFCoreOptionsExtension>()
            ?? new KEFCoreOptionsExtension();

        extension = extension.WithApplicationId(applicationId).WithBootstrapServers(bootstrapServers);

        ConfigureWarnings(optionsBuilder);

        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);

        kefcoreOptionsAction?.Invoke(new KEFCoreDbContextOptionsBuilder(optionsBuilder));

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
                KEFCoreEventId.TransactionIgnoredWarning, WarningBehavior.Throw));

        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(coreOptionsExtension);
    }
    /// <summary>
    /// Creates a serializer <see cref="Type"/> for keys
    /// </summary>
    public static Type KeyType(this IKEFCoreSingletonOptions options, IEntityType entityType)
    {
        var primaryKey = entityType.FindPrimaryKey()!.GetKeyType();
        return primaryKey;
    }
    /// <summary>
    /// Create the ValueContainer <see cref="Type"/>
    /// </summary>
    public static Type ValueContainerType(this IKEFCoreSingletonOptions options, IEntityType entityType)
    {
        return options.ValueContainerType?.MakeGenericType(options.KeyType(entityType))!;
    }
    /// <summary>
    /// Create the ValueContainer <see cref="Type"/>
    /// </summary>
    public static Type JVMKeyType(this IKEFCoreSingletonOptions options, IEntityType entityType)
    {
        return typeof(byte[]);
    }
    /// <summary>
    /// Create the ValueContainer <see cref="Type"/>
    /// </summary>
    public static Type JVMValueContainerType(this IKEFCoreSingletonOptions options, IEntityType entityType)
    {
        var selector = SerDesSelectorForValue(options, entityType);
        if (options.UseByteBufferDataTransfer && selector != null && selector.ByteBufferSerDes != null) return typeof(Java.Nio.ByteBuffer);
        return typeof(byte[]);
    }

    private static readonly ConcurrentDictionary<(Type?, IEntityType), ISerDesSelector?> _keySerDesSelctors = new();

    /// <summary>
    /// Creates a serializer <see cref="Type"/> for keys
    /// </summary>
    public static ISerDesSelector? SerDesSelectorForKey(this IKEFCoreSingletonOptions options, IEntityType entityType)
    {
        return _keySerDesSelctors.GetOrAdd((options.KeySerDesSelectorType, entityType), (o) =>
        {
            var selector = o.Item1?.MakeGenericType(options.KeyType(o.Item2))!;
            return Activator.CreateInstance(selector) as ISerDesSelector;
        });
    }

    private static readonly ConcurrentDictionary<(Type?, IEntityType), ISerDesSelector?> _valueContainerSerDesSelctors = new();

    /// <summary>
    /// Creates a serialzier <see cref="Type"/> for values
    /// </summary>
    public static ISerDesSelector? SerDesSelectorForValue(this IKEFCoreSingletonOptions options, IEntityType entityType)
    {
        return _valueContainerSerDesSelctors.GetOrAdd((options.ValueSerDesSelectorType, entityType), (o) =>
        {
            var selector = o.Item1?.MakeGenericType(options.ValueContainerType(o.Item2))!;
            return Activator.CreateInstance(selector) as ISerDesSelector;
        });
    }
}
