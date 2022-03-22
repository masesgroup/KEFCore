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

using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace MASES.EntityFrameworkCore.KNet;

/// <summary>
///     Extension methods for <see cref="IReadOnlyEntityType" /> for the Kafka provider.
/// </summary>
/// <remarks>
///     See <see href="https://aka.ms/efcore-docs-modeling">Modeling entity types and relationships</see>, and
///     <see href="https://github.com/masesgroup/EntityFramework4Kafka">The EF Core Kafka database provider</see> for more information and examples.
/// </remarks>
public static class KafkaEntityTypeExtensions
{
    /// <summary>
    ///     Gets the LINQ query used as the default source for queries of this type.
    /// </summary>
    /// <param name="entityType">The entity type to get the Kafka query for.</param>
    /// <returns>The LINQ query used as the default source.</returns>
    public static LambdaExpression? GetKafkaQuery(this IReadOnlyEntityType entityType)
#pragma warning disable EF1001 // Internal EF Core API usage.
#pragma warning disable CS0612 // Il tipo o il membro è obsoleto
        => (LambdaExpression?)entityType[CoreAnnotationNames.DefiningQuery];
#pragma warning restore CS0612 // Il tipo o il membro è obsoleto
#pragma warning restore EF1001 // Internal EF Core API usage.

    /// <summary>
    ///     Sets the LINQ query used as the default source for queries of this type.
    /// </summary>
    /// <param name="entityType">The entity type.</param>
    /// <param name="kafkaQuery">The LINQ query used as the default source.</param>
    public static void SetKafkaQuery(
        this IMutableEntityType entityType,
        LambdaExpression? kafkaQuery)
        => entityType
#pragma warning disable EF1001 // Internal EF Core API usage.
#pragma warning disable CS0612 // Il tipo o il membro è obsoleto
            .SetOrRemoveAnnotation(CoreAnnotationNames.DefiningQuery, kafkaQuery);
#pragma warning restore CS0612 // Il tipo o il membro è obsoleto
#pragma warning restore EF1001 // Internal EF Core API usage.

    /// <summary>
    ///     Sets the LINQ query used as the default source for queries of this type.
    /// </summary>
    /// <param name="entityType">The entity type.</param>
    /// <param name="kafkaQuery">The LINQ query used as the default source.</param>
    /// <param name="fromDataAnnotation">Indicates whether the configuration was specified using a data annotation.</param>
    /// <returns>The configured entity type.</returns>
    public static LambdaExpression? SetKafkaQuery(
        this IConventionEntityType entityType,
        LambdaExpression? kafkaQuery,
        bool fromDataAnnotation = false)
        => (LambdaExpression?)entityType
#pragma warning disable EF1001 // Internal EF Core API usage.
#pragma warning disable CS0612 // Il tipo o il membro è obsoleto
            .SetOrRemoveAnnotation(CoreAnnotationNames.DefiningQuery, kafkaQuery, fromDataAnnotation)
#pragma warning restore CS0612 // Il tipo o il membro è obsoleto
#pragma warning restore EF1001 // Internal EF Core API usage.
            ?.Value;

    /// <summary>
    ///     Returns the configuration source for <see cref="GetKafkaQuery" />.
    /// </summary>
    /// <param name="entityType">The entity type.</param>
    /// <returns>The configuration source for <see cref="GetKafkaQuery" />.</returns>
    public static ConfigurationSource? GetDefiningQueryConfigurationSource(this IConventionEntityType entityType)
#pragma warning disable EF1001 // Internal EF Core API usage.
#pragma warning disable CS0612 // Il tipo o il membro è obsoleto
        => entityType.FindAnnotation(CoreAnnotationNames.DefiningQuery)?.GetConfigurationSource();
#pragma warning restore CS0612 // Il tipo o il membro è obsoleto
#pragma warning restore EF1001 // Internal EF Core API usage.
}
