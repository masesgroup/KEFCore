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

namespace MASES.EntityFrameworkCore.KNet.Metadata.Conventions;

/// <summary>
///     Convention that converts accesses of <see cref="DbSet{TEntity}" /> inside query filters and defining queries into
///     <see cref="QueryRootExpression" />.
///     This makes them consistent with how DbSet accesses in the actual queries are represented, which allows for easier processing in the
///     query pipeline.
/// </summary>
/// <remarks>
///     See <see href="https://aka.ms/efcore-docs-conventions">Model building conventions</see>, and
///     <see href="https://github.com/masesgroup/KEFCore">The EF Core Kafka database provider</see> for more information and examples.
/// </remarks>
public class DefiningQueryRewritingConvention : QueryFilterRewritingConvention
{
    /// <summary>
    ///     Creates a new instance of <see cref="QueryFilterRewritingConvention" />.
    /// </summary>
    /// <param name="dependencies">Parameter object containing dependencies for this convention.</param>
    public DefiningQueryRewritingConvention(ProviderConventionSetBuilderDependencies dependencies)
        : base(dependencies)
    {
    }

    /// <inheritdoc />
    public override void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            var definingQuery = entityType.GetKafkaQuery();
            if (definingQuery != null)
            {
                entityType.SetKafkaQuery((LambdaExpression)DbSetAccessRewriter.Rewrite(modelBuilder.Metadata, definingQuery));
            }
        }
    }
}
