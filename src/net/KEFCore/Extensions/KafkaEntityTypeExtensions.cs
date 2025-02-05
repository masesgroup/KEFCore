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

using MASES.EntityFrameworkCore.KNet.Infrastructure.Internal;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace MASES.EntityFrameworkCore.KNet;

/// <summary>
///     Extension methods for <see cref="IReadOnlyEntityType" /> for the Kafka provider.
/// </summary>
/// <remarks>
///     See <see href="https://aka.ms/efcore-docs-modeling">Modeling entity types and relationships</see>, and
///     <see href="https://github.com/masesgroup/KEFCore">The EF Core Kafka database provider</see> for more information and examples.
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
    /// <summary>
    /// Creates the topic name
    /// </summary>
    public static string TopicName(this IEntityType entityType, KafkaOptionsExtension options)
    {
        return $"{options.DatabaseName}.{entityType.Name}";
    }
    /// <summary>
    /// Creates the storage id
    /// </summary>
    public static string StorageIdForTable(this IEntityType entityType, KafkaOptionsExtension options)
    {
        return $"Table_{entityType.TopicName(options)}";
    }
    /// <summary>
    /// Creates the application id
    /// </summary>
    public static string ApplicationIdForTable(this IEntityType entityType, KafkaOptionsExtension options)
    {
        return $"{options.ApplicationId}_{entityType.Name}";
    }
    /// <summary>
    /// Gets replication factor
    /// </summary>
    public static short ReplicationFactor(this IEntityType entityType, KafkaOptionsExtension options)
    {
        var replicationFactor = options.DefaultReplicationFactor;
        return replicationFactor;
    }
    /// <summary>
    /// Gets number of partitions
    /// </summary>
    public static int NumPartitions(this IEntityType entityType, KafkaOptionsExtension options)
    {
        var numPartitions = options.DefaultNumPartitions;
        return numPartitions;
    }
    /// <summary>
    /// Gets consumer instances
    /// </summary>
    public static int? ConsumerInstances(this IEntityType entityType, KafkaOptionsExtension options)
    {
        var consumerInstances = options.DefaultConsumerInstances;
        return consumerInstances;
    }
}
