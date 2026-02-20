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

using MASES.EntityFrameworkCore.KNet.Diagnostics.Internal;
using MASES.EntityFrameworkCore.KNet.Infrastructure.Internal;
using MASES.EntityFrameworkCore.KNet.Internal;
using MASES.EntityFrameworkCore.KNet.Metadata.Conventions;
using MASES.EntityFrameworkCore.KNet.Query.Internal;
using MASES.EntityFrameworkCore.KNet.Storage.Internal;
using MASES.EntityFrameworkCore.KNet.ValueGeneration.Internal;
using System.ComponentModel;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
///     Kafka specific extension methods for <see cref="IServiceCollection" />.
/// </summary>
public static class KafkaServiceCollectionExtensions
{
    /// <summary>
    ///     Adds the services required by the Kafka database provider for Entity Framework
    ///     to an <see cref="IServiceCollection" />.
    /// </summary>
    /// <remarks>
    ///     Calling this method is no longer necessary when building most applications, including those that
    ///     use dependency injection in ASP.NET or elsewhere.
    ///     It is only needed when building the internal service provider for use with
    ///     the <see cref="DbContextOptionsBuilder.UseInternalServiceProvider" /> method.
    ///     This is not recommend other than for some advanced scenarios.
    /// </remarks>
    /// <param name="serviceCollection">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <returns>
    ///     The same service collection so that multiple calls can be chained.
    /// </returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static IServiceCollection AddEntityFrameworkKafkaDatabase(this IServiceCollection serviceCollection)
    {
#pragma warning disable EF1001 // Internal EF Core API usage.
        var builder = new EntityFrameworkServicesBuilder(serviceCollection)
            .TryAdd<LoggingDefinitions, KafkaLoggingDefinitions>()
            .TryAdd<EntityFrameworkCore.Internal.IEntityFinderSource, KafkaEntityFinderSource>()
            .TryAdd<IDatabaseProvider, DatabaseProvider<KafkaOptionsExtension>>()
            .TryAdd<IValueGeneratorSelector, KafkaValueGeneratorSelector>()
            .TryAdd<IDatabase>(p => p.GetRequiredService<IKafkaDatabase>())
            .TryAdd<IDbContextTransactionManager, KafkaTransactionManager>()
            .TryAdd<IDatabaseCreator, KafkaDatabaseCreator>()
            .TryAdd<IQueryContextFactory, KafkaQueryContextFactory>()
            .TryAdd<IProviderConventionSetBuilder, KafkaConventionSetBuilder>()
            .TryAdd<IModelValidator, KafkaModelValidator>()
            .TryAdd<ITypeMappingSource, KafkaTypeMappingSource>()
            .TryAdd<IShapedQueryCompilingExpressionVisitorFactory, KafkaShapedQueryCompilingExpressionVisitorFactory>()
            .TryAdd<IQueryableMethodTranslatingExpressionVisitorFactory, KafkaQueryableMethodTranslatingExpressionVisitorFactory>()    
            .TryAdd<IQueryTranslationPreprocessorFactory, KafkaQueryTranslationPreprocessorFactory>()
            .TryAdd<ISingletonOptions, IKafkaSingletonOptions>(p => p.GetRequiredService<IKafkaSingletonOptions>())
            .TryAddProviderSpecificServices(
                b => b
                    .TryAddSingleton<IKafkaSingletonOptions, KafkaSingletonOptions>()
                    .TryAddSingleton<IKafkaClusterCache, KafkaClusterCache>()
                    .TryAddSingleton<IKafkaTableFactory, KafkaTableFactory>()
                    .TryAddScoped<IKafkaDatabase, KafkaDatabase>());
#pragma warning restore EF1001 // Internal EF Core API usage.

        builder.TryAddCoreServices();

        return serviceCollection;
    }
}
