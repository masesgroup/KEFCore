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

using MASES.EntityFrameworkCore.KNet.Infrastructure.Internal;
using MASES.EntityFrameworkCore.KNet.Metadata;
using MASES.EntityFrameworkCore.KNet.Metadata.Internal;
using System.ComponentModel.DataAnnotations.Schema;

namespace MASES.EntityFrameworkCore.KNet.Extensions;

/// <summary>
///     Extension methods for <see cref="IReadOnlyEntityType" /> for the Kafka provider.
/// </summary>
/// <remarks>
///     See <see href="https://aka.ms/efcore-docs-modeling">Modeling entity types and relationships</see>, and
///     <see href="https://github.com/masesgroup/KEFCore">The EF Core Kafka database provider</see> for more information and examples.
/// </remarks>
public static class KEFCoreEntityTypeExtensions
{
    /// <summary>
    /// Creates the topic name
    /// </summary>
    public static string GetKEFCoreTopicName(this IEntityType entityType)
        => entityType.FindAnnotation(KEFCoreAnnotationNames.TopicName)?.Value as string
           ?? entityType.Name;

    /// <summary>
    /// Returns whether KEFCore event management is enabled for this entity type.
    /// Reads the <see cref="KEFCoreAnnotationNames.ManageEvents"/> annotation
    /// set by <see cref="MASES.EntityFrameworkCore.KNet.Metadata.Conventions.KEFCoreManageEventsConvention"/>.
    /// </summary>
    /// <param name="entityType">The <see cref="IEntityType"/> to query.</param>
    /// <returns>
    /// <see langword="true"/> if event management is enabled (default);
    /// <see langword="false"/> if disabled via <see cref="KEFCoreIgnoreEventsAttribute"/>
    /// or <c>HasKEFCoreManageEvents(false)</c>.
    /// </returns>
    public static bool GetManageEvents(this IEntityType entityType)
        => entityType.FindAnnotation(KEFCoreAnnotationNames.ManageEvents)?.Value as bool? ?? true;

    /// <summary>
    /// Creates the topic name
    /// </summary>
    public static string TopicName(this IEntityType entityType)
        => entityType.FindAnnotation(KEFCoreAnnotationNames.TopicName)?.Value as string
           ?? entityType.Name;

    /// <summary>
    /// Creates the storage id
    /// </summary>
    public static string StorageIdForTable(this IEntityType entityType)
    {
        return $"Table_{entityType.TopicName()}";
    }
    /// <summary>
    /// Creates the application id
    /// </summary>
    public static string ApplicationIdForTable(this IEntityType entityType, KEFCoreOptionsExtension options)
    {
        return $"{options.ApplicationId}_{entityType.Name}";
    }
    /// <summary>
    /// Gets replication factor
    /// </summary>
    public static short ReplicationFactor(this IEntityType entityType, KEFCoreOptionsExtension options)
    {
        var replicationFactor = options.DefaultReplicationFactor;
        return replicationFactor;
    }
    /// <summary>
    /// Gets number of partitions
    /// </summary>
    public static int NumPartitions(this IEntityType entityType, KEFCoreOptionsExtension options)
    {
        var numPartitions = options.DefaultNumPartitions;
        return numPartitions;
    }
    /// <summary>
    /// Gets consumer instances
    /// </summary>
    [Obsolete("Option will be removed soon")]
    public static int? ConsumerInstances(this IEntityType entityType, KEFCoreOptionsExtension options)
    {
        var consumerInstances = options.DefaultConsumerInstances;
        return consumerInstances;
    }
}
