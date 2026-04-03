// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

using MASES.EntityFrameworkCore.KNet.Extensions;
using MASES.EntityFrameworkCore.KNet.Infrastructure.Internal;
using MASES.EntityFrameworkCore.KNet.Serialization;
using MASES.EntityFrameworkCore.KNet.Storage.Internal;

namespace MASES.EntityFrameworkCore.KNet.Metadata.Conventions;

/// <summary>
///     A builder for building conventions for the KEFCore provider.
/// </summary>
/// <remarks>
///     <para>
///         The service lifetime is <see cref="ServiceLifetime.Scoped" /> and multiple registrations
///         are allowed. This means that each <see cref="DbContext" /> instance will use its own
///         set of instances of this service.
///         The implementations may depend on other services registered with any lifetime.
///         The implementations do not need to be thread-safe.
///     </para>
///     <para>
///         See <see href="https://aka.ms/efcore-docs-conventions">Model building conventions</see>, and
///         <see href="https://github.com/masesgroup/KEFCore">The EF Core KNet database provider</see> for more information and examples.
///     </para>
/// </remarks>
/// <remarks>
///     Creates a new <see cref="KEFCoreConventionSetBuilder" /> instance.
/// </remarks>
/// <param name="dependencies">The core dependencies for this service.</param>
/// <param name="converterFactory">The <see cref="IComplexTypeConverterFactory"/> instance</param>
public class KEFCoreConventionSetBuilder(
ProviderConventionSetBuilderDependencies dependencies,
IComplexTypeConverterFactory converterFactory) : ProviderConventionSetBuilder(dependencies)
{
    /// <inheritdoc />
    public override ConventionSet CreateConventionSet()
    {
        var conventionSet = base.CreateConventionSet();

        conventionSet.ModelFinalizingConventions.Add(new KEFCoreTopicNamingConvention());
        conventionSet.ModelFinalizingConventions.Add(new KEFCoreManageEventsConvention());
        conventionSet.ModelFinalizingConventions.Add(new KEFCoreComplexTypeConverterConvention(converterFactory));
        conventionSet.ModelFinalizingConventions.Add(new KEFCoreComplexTypeEquatableConvention());
        conventionSet.ModelFinalizingConventions.Add(new KEFCoreTopicPartitionsConvention());
        conventionSet.ModelFinalizingConventions.Add(new KEFCoreTopicRetentionConvention());
        conventionSet.ModelFinalizingConventions.Add(new KEFCoreReadOnlyConvention());
        conventionSet.ModelFinalizingConventions.Add(new KEFCoreStoreLookupConvention());
        conventionSet.ModelFinalizingConventions.Add(new KEFCoreProducerConvention());
        conventionSet.ModelFinalizingConventions.Add(new KEFCoreTransactionalConvention());
        conventionSet.ModelFinalizingConventions.Add(new KEFCoreRocksDbLifecycleAttributeConvention());
        conventionSet.ModelFinalizingConventions.Add(new KEFCoreValueBufferCacheConvention());

        return conventionSet;
    }

    /// <summary>
    ///     Call this method to build a <see cref="ConventionSet" /> for the KEFCore provider when using
    ///     the <see cref="ModelBuilder" /> outside of <see cref="DbContext.OnModelCreating" />.
    /// </summary>
    /// <remarks>
    ///     Note that it is unusual to use this method.
    ///     Consider using <see cref="DbContext" /> in the normal way instead.
    /// </remarks>
    /// <returns>The convention set.</returns>
    public static ConventionSet Build()
    {
        try
        {
            KEFCoreClusterAdmin.DisableClusterInvocation = true;

            using var serviceScope = CreateServiceScope();
            using var context = serviceScope.ServiceProvider.GetRequiredService<DbContext>();
            return ConventionSet.CreateConventionSet(context);
        }
        finally { KEFCoreClusterAdmin.DisableClusterInvocation = false; }
    }

    /// <summary>
    /// Builds a <see cref="ConventionSet"/> for KEFCore outside of <see cref="DbContext.OnModelCreating"/>,
    /// exposing the <see cref="IComplexTypeConverterFactory"/> populated during model finalization.
    /// </summary>
    /// <param name="converterFactory">
    /// The <see cref="IComplexTypeConverterFactory"/> populated by <see cref="KEFCoreComplexTypeConverterConvention"/>
    /// during model finalization.
    /// </param>
    public static ConventionSet Build(out IComplexTypeConverterFactory converterFactory)
    {
        try
        {
            KEFCoreClusterAdmin.DisableClusterInvocation = true;
            using var serviceScope = CreateServiceScope();
            using var context = serviceScope.ServiceProvider.GetRequiredService<DbContext>();
            converterFactory = serviceScope.ServiceProvider.GetRequiredService<IComplexTypeConverterFactory>();
            return ConventionSet.CreateConventionSet(context);
        }
        finally { KEFCoreClusterAdmin.DisableClusterInvocation = false; }
    }

    /// <summary>
    ///     Call this method to build a <see cref="ModelBuilder" /> for KEFCore outside of <see cref="DbContext.OnModelCreating" />.
    /// </summary>
    /// <remarks>
    ///     Note that it is unusual to use this method. Consider using <see cref="DbContext" /> in the normal way instead.
    /// </remarks>
    /// <returns>The convention set.</returns>
    public static ModelBuilder CreateModelBuilder()
    {
        try
        {
            KEFCoreClusterAdmin.DisableClusterInvocation = true;

            using var serviceScope = CreateServiceScope();
            using var context = serviceScope.ServiceProvider.GetRequiredService<DbContext>();
            return new ModelBuilder(ConventionSet.CreateConventionSet(context), context.GetService<ModelDependencies>());
        }
        finally { KEFCoreClusterAdmin.DisableClusterInvocation = false; }
    }

    /// <summary>
    /// Builds a <see cref="ModelBuilder"/> for KEFCore outside of <see cref="DbContext.OnModelCreating"/>,
    /// exposing the <see cref="IComplexTypeConverterFactory"/> populated during model finalization.
    /// </summary>
    /// <param name="converterFactory">
    /// The <see cref="IComplexTypeConverterFactory"/> populated by <see cref="KEFCoreComplexTypeConverterConvention"/>
    /// during model finalization.
    /// </param>
    public static ModelBuilder CreateModelBuilder(out IComplexTypeConverterFactory converterFactory)
    {
        try
        {
            KEFCoreClusterAdmin.DisableClusterInvocation = true;
            using var serviceScope = CreateServiceScope();
            using var context = serviceScope.ServiceProvider.GetRequiredService<DbContext>();
            converterFactory = serviceScope.ServiceProvider.GetRequiredService<IComplexTypeConverterFactory>();
            return new ModelBuilder(ConventionSet.CreateConventionSet(context),
                                    context.GetService<ModelDependencies>());
        }
        finally { KEFCoreClusterAdmin.DisableClusterInvocation = false; }
    }

    private static IServiceScope CreateServiceScope()
    {
        var serviceProvider = new ServiceCollection()
            .AddEntityFrameworkKNetDatabase()
            .AddDbContext<DbContext>((p, o) =>
                o.UseKEFCore(Guid.NewGuid().ToString(), Guid.NewGuid().ToString())
                    .UseInternalServiceProvider(p))
            .BuildServiceProvider();

        return serviceProvider.GetRequiredService<IServiceScopeFactory>().CreateScope();
    }
}
