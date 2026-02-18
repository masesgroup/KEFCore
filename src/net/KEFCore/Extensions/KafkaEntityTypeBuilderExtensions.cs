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

using MASES.EntityFrameworkCore.KNet.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace MASES.EntityFrameworkCore.KNet;

/// <summary>
///     Extension methods for <see cref="EntityTypeBuilder" /> for the Kafka provider.
/// </summary>
/// <remarks>
///     See <see href="https://aka.ms/efcore-docs-modeling">Modeling entity types and relationships</see>, and
///     <see href="https://github.com/masesgroup/KEFCore">The EF Core Kafka database provider</see> for more information and examples.
/// </remarks>
public static class KafkaEntityTypeBuilderExtensions
{
    /// <summary>
    ///     Configures a query used to provide data for an entity type.
    /// </summary>
    /// <remarks>
    ///     See <see href="https://aka.ms/efcore-docs-modeling">Modeling entity types and relationships</see>, and
    ///     <see href="https://github.com/masesgroup/KEFCore">The EF Core Kafka database provider</see> for more information and examples.
    /// </remarks>
    /// <param name="entityTypeBuilder">The builder for the entity type being configured.</param>
    /// <param name="query">The query that will provide the underlying data for the entity type.</param>
    /// <returns>The same builder instance so that multiple calls can be chained.</returns>
    public static EntityTypeBuilder ToKafkaQuery(
        this EntityTypeBuilder entityTypeBuilder,
        LambdaExpression? query)
    {
        Check.NotNull(query, nameof(query));

        entityTypeBuilder.Metadata.SetKafkaQuery(query);

        return entityTypeBuilder;
    }

    /// <summary>
    ///     Configures a query used to provide data for an entity type.
    /// </summary>
    /// <remarks>
    ///     See <see href="https://aka.ms/efcore-docs-modeling">Modeling entity types and relationships</see>, and
    ///     <see href="https://github.com/masesgroup/KEFCore">The EF Core Kafka database provider</see> for more information and examples.
    /// </remarks>
    /// <param name="entityTypeBuilder">The builder for the entity type being configured.</param>
    /// <param name="query">The query that will provide the underlying data for the entity type.</param>
    /// <returns>The same builder instance so that multiple calls can be chained.</returns>
    public static EntityTypeBuilder<TEntity> ToKafkaQuery<TEntity>(
        this EntityTypeBuilder<TEntity> entityTypeBuilder,
        Expression<Func<IQueryable<TEntity>>> query)
        where TEntity : class
    {
        Check.NotNull(query, nameof(query));

        entityTypeBuilder.Metadata.SetKafkaQuery(query);

        return entityTypeBuilder;
    }

    /// <summary>
    ///     Configures a query used to provide data for an entity type.
    /// </summary>
    /// <remarks>
    ///     See <see href="https://aka.ms/efcore-docs-modeling">Modeling entity types and relationships</see>, and
    ///     <see href="https://github.com/masesgroup/KEFCore">The EF Core Kafka database provider</see> for more information and examples.
    /// </remarks>
    /// <param name="entityTypeBuilder">The builder for the entity type being configured.</param>
    /// <param name="query">The query that will provide the underlying data for the entity type.</param>
    /// <param name="fromDataAnnotation">Indicates whether the configuration was specified using a data annotation.</param>
    /// <returns>
    ///     The same builder instance if the query was set, <see langword="null" /> otherwise.
    /// </returns>
    public static IConventionEntityTypeBuilder? ToKafkaQuery(
        this IConventionEntityTypeBuilder entityTypeBuilder,
        LambdaExpression? query,
        bool fromDataAnnotation = false)
    {
        if (CanSetKafkaQuery(entityTypeBuilder, query, fromDataAnnotation))
        {
            entityTypeBuilder.Metadata.SetKafkaQuery(query, fromDataAnnotation);

            return entityTypeBuilder;
        }

        return null;
    }

    /// <summary>
    ///     Returns a value indicating whether the given Kafka query can be set from the current configuration source.
    /// </summary>
    /// <remarks>
    ///     See <see href="https://aka.ms/efcore-docs-modeling">Modeling entity types and relationships</see>, and
    ///     <see href="https://github.com/masesgroup/KEFCore">The EF Core Kafka database provider</see> for more information and examples.
    /// </remarks>
    /// <param name="entityTypeBuilder">The builder for the entity type being configured.</param>
    /// <param name="query">The query that will provide the underlying data for the keyless entity type.</param>
    /// <param name="fromDataAnnotation">Indicates whether the configuration was specified using a data annotation.</param>
    /// <returns><see langword="true" /> if the given Kafka query can be set.</returns>
    public static bool CanSetKafkaQuery(
        this IConventionEntityTypeBuilder entityTypeBuilder,
        LambdaExpression? query,
        bool fromDataAnnotation = false)
        => entityTypeBuilder.CanSetAnnotation(KafkaAnnotationNames.DefiningQuery, query, fromDataAnnotation);
}
