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
using MASES.EntityFrameworkCore.KNet.Storage.Internal;
using MASES.KNet.Common;
using MASES.KNet.Consumer;
using MASES.KNet.Producer;

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
    /// Creates the storage id based on <see cref="GetKEFCoreTopicName"/> and <see cref="IKEFCoreCluster.ClusterId"/>
    /// </summary>
    public static string StorageIdForTable(this IEntityType entityType, IKEFCoreCluster cluster)
    {
        return $"Table_{entityType.GetKEFCoreTopicName()}_{cluster.ClusterId}";
    }
    /// <summary>
    /// Creates the application id
    /// </summary>
    public static string ApplicationIdForTable(this IEntityType entityType, KEFCoreOptionsExtension options)
    {
        return $"{options.ApplicationId}_{entityType.Name}";
    }
    /// <summary>
    /// Returns the number of partitions for the Kafka topic associated with this entity type.
    /// Reads <see cref="KEFCoreAnnotationNames.NumPartitions"/> annotation first,
    /// falling back to <see cref="IKEFCoreSingletonOptions.DefaultNumPartitions"/>.
    /// </summary>
    public static int NumPartitions(this IEntityType entityType, KEFCoreOptionsExtension options)
        => entityType.FindAnnotation(KEFCoreAnnotationNames.NumPartitions)?.Value as int?
           ?? options.DefaultNumPartitions;

    /// <summary>
    /// Returns the replication factor for the Kafka topic associated with this entity type.
    /// Reads <see cref="KEFCoreAnnotationNames.ReplicationFactor"/> annotation first,
    /// falling back to <see cref="IKEFCoreSingletonOptions.DefaultReplicationFactor"/>.
    /// </summary>
    public static short ReplicationFactor(this IEntityType entityType, KEFCoreOptionsExtension options)
        => entityType.FindAnnotation(KEFCoreAnnotationNames.ReplicationFactor)?.Value as short?
           ?? options.DefaultReplicationFactor;

    /// <summary>
    /// Returns the per-entity topic retention in bytes, or <see langword="null"/> if not set.
    /// When <see langword="null"/>, the global <see cref="IKEFCoreSingletonOptions.TopicConfig"/> applies.
    /// </summary>
    public static long? GetTopicRetentionBytes(this IEntityType entityType)
        => entityType.FindAnnotation(KEFCoreAnnotationNames.TopicRetentionBytes)?.Value as long?;

    /// <summary>
    /// Returns the per-entity topic retention in milliseconds, or <see langword="null"/> if not set.
    /// When <see langword="null"/>, the global <see cref="IKEFCoreSingletonOptions.TopicConfig"/> applies.
    /// </summary>
    public static long? GetTopicRetentionMs(this IEntityType entityType)
        => entityType.FindAnnotation(KEFCoreAnnotationNames.TopicRetentionMs)?.Value as long?;

    /// <summary>
    /// Returns whether this entity type is read-only.
    /// Returns <see langword="true"/> if <see cref="KEFCoreReadOnlyAttribute"/> is applied
    /// or if the context-level <see cref="KEFCoreOptionsExtension.ReadOnlyMode"/> is enabled.
    /// </summary>
    public static bool GetReadOnly(this IEntityType entityType, KEFCoreOptionsExtension options)
        => (entityType.FindAnnotation(KEFCoreAnnotationNames.ReadOnly)?.Value as bool? ?? false)
           || options.ReadOnlyMode;

    /// <summary>
    /// Returns whether prefix scan optimization is enabled for this entity type.
    /// Reads <see cref="KEFCoreAnnotationNames.UseStorePrefixScan"/> annotation first,
    /// falling back to <see cref="KEFCoreOptionsExtension.UseStorePrefixScan"/>.
    /// </summary>
    public static bool GetUseStorePrefixScan(this IEntityType entityType, KEFCoreOptionsExtension options)
        => entityType.FindAnnotation(KEFCoreAnnotationNames.UseStorePrefixScan)?.Value as bool?
           ?? options.UseStorePrefixScan;

    /// <summary>
    /// Returns whether single key look-up optimization is enabled for this entity type.
    /// Reads <see cref="KEFCoreAnnotationNames.UseStoreSingleKeyLookup"/> annotation first,
    /// falling back to <see cref="KEFCoreOptionsExtension.UseStoreSingleKeyLookup"/>.
    /// </summary>
    public static bool GetUseStoreSingleKeyLookup(this IEntityType entityType, KEFCoreOptionsExtension options)
        => entityType.FindAnnotation(KEFCoreAnnotationNames.UseStoreSingleKeyLookup)?.Value as bool?
           ?? options.UseStoreSingleKeyLookup;

    /// <summary>
    /// Returns whether key range look-up optimization is enabled for this entity type.
    /// Reads <see cref="KEFCoreAnnotationNames.UseStoreKeyRange"/> annotation first,
    /// falling back to <see cref="KEFCoreOptionsExtension.UseStoreKeyRange"/>.
    /// </summary>
    public static bool GetUseStoreKeyRange(this IEntityType entityType, KEFCoreOptionsExtension options)
        => entityType.FindAnnotation(KEFCoreAnnotationNames.UseStoreKeyRange)?.Value as bool?
           ?? options.UseStoreKeyRange;

    /// <summary>
    /// Returns whether reverse iteration optimization is enabled for this entity type.
    /// Reads <see cref="KEFCoreAnnotationNames.UseStoreReverse"/> annotation first,
    /// falling back to <see cref="KEFCoreOptionsExtension.UseStoreReverse"/>.
    /// </summary>
    public static bool GetUseStoreReverse(this IEntityType entityType, KEFCoreOptionsExtension options)
        => entityType.FindAnnotation(KEFCoreAnnotationNames.UseStoreReverse)?.Value as bool?
           ?? options.UseStoreReverse;

    /// <summary>
    /// Returns whether reverse key range look-up optimization is enabled for this entity type.
    /// Reads <see cref="KEFCoreAnnotationNames.UseStoreReverseKeyRange"/> annotation first,
    /// falling back to <see cref="KEFCoreOptionsExtension.UseStoreReverseKeyRange"/>.
    /// </summary>
    public static bool GetUseStoreReverseKeyRange(this IEntityType entityType, KEFCoreOptionsExtension options)
        => entityType.FindAnnotation(KEFCoreAnnotationNames.UseStoreReverseKeyRange)?.Value as bool?
           ?? options.UseStoreReverseKeyRange;

    /// <summary>
    /// Builds the <see cref="TopicConfigBuilder"/> for this entity type, merging the global
    /// <see cref="IKEFCoreSingletonOptions.TopicConfig"/> with any per-entity retention
    /// overrides set via <see cref="KEFCoreTopicRetentionAttribute"/> or
    /// <see cref="KEFCoreEntityTypeBuilderExtensions.HasKEFCoreTopicRetention"/>.
    /// Per-entity values take precedence over the global config.
    /// </summary>
    /// <returns>
    /// A <see cref="TopicConfigBuilder"/> ready to pass to
    /// <see cref="KEFCoreClusterAdmin.CreateTopic"/> via <see cref="MASES.KNet.GenericConfigBuilder{T}.ToMap"/>.
    /// </returns>
    public static TopicConfigBuilder BuildTopicConfig(
        this IEntityType entityType, KEFCoreOptionsExtension options)
    {
        var builder = TopicConfigBuilder.CreateFrom(options.TopicConfig) ?? new TopicConfigBuilder();

        builder.CleanupPolicy = options.UseDeletePolicyForTopic
            ? TopicConfigBuilder.CleanupPolicyTypes.Compact | TopicConfigBuilder.CleanupPolicyTypes.Delete
            : TopicConfigBuilder.CleanupPolicyTypes.Compact;

        var retentionBytes = entityType.GetTopicRetentionBytes();
        if (retentionBytes.HasValue)
        {
            builder.RetentionBytes = retentionBytes.Value;
        }
        var retentionMs = entityType.GetTopicRetentionMs();
        if (retentionMs.HasValue)
        {
            builder.RetentionMs = retentionMs.Value;
        }
        return builder;
    }
    /// <summary>
    /// Returns the key serializer selector type for this entity type.
    /// Reads <see cref="KEFCoreAnnotationNames.KeySerDesSelectorType"/> annotation first,
    /// falling back to <see cref="IKEFCoreSingletonOptions.KeySerDesSelectorType"/>.
    /// </summary>
    public static Type? GetKeySerDesSelectorType(this IEntityType entityType, IKEFCoreSingletonOptions options)
        => entityType.FindAnnotation(KEFCoreAnnotationNames.KeySerDesSelectorType)?.Value as Type
           ?? options.KeySerDesSelectorType;

    /// <summary>
    /// Returns the value container serializer selector type for this entity type.
    /// Reads <see cref="KEFCoreAnnotationNames.ValueSerDesSelectorType"/> annotation first,
    /// falling back to <see cref="IKEFCoreSingletonOptions.ValueSerDesSelectorType"/>.
    /// </summary>
    public static Type? GetValueSerDesSelectorType(this IEntityType entityType, IKEFCoreSingletonOptions options)
        => entityType.FindAnnotation(KEFCoreAnnotationNames.ValueSerDesSelectorType)?.Value as Type
           ?? options.ValueSerDesSelectorType;

    /// <summary>
    /// Returns the value container type for this entity type.
    /// Reads <see cref="KEFCoreAnnotationNames.ValueContainerType"/> annotation first,
    /// falling back to <see cref="IKEFCoreSingletonOptions.ValueContainerType"/>.
    /// </summary>
    public static Type? GetValueContainerType(this IEntityType entityType, IKEFCoreSingletonOptions options)
        => entityType.FindAnnotation(KEFCoreAnnotationNames.ValueContainerType)?.Value as Type
           ?? options.ValueContainerType;

    /// <summary>
    /// Builds the <see cref="ProducerConfigBuilder"/> for this entity type, merging the global
    /// <see cref="IKEFCoreSingletonOptions.ProducerConfig"/> with any per-entity overrides
    /// stored as a <see cref="KEFCoreProducerAnnotation"/> annotation.
    /// Per-entity values take precedence over the global config.
    /// </summary>
    public static ProducerConfigBuilder BuildProducerConfig(
        this IEntityType entityType, KEFCoreOptionsExtension options)
    {
        var builder = options.ProducerOptionsBuilder();

        var ann = entityType.FindAnnotation(KEFCoreAnnotationNames.ProducerConfig)?.Value
                  as KEFCoreProducerAnnotation;
        if (ann == null) return builder;

        if (ann.Acks.HasValue) builder.Acks = ann.Acks.Value;
        if (ann.LingerMs.HasValue) builder.LingerMs = ann.LingerMs.Value;
        if (ann.BatchSize.HasValue) builder.BatchSize = ann.BatchSize.Value;
        if (ann.CompressionType.HasValue) builder.CompressionType = ann.CompressionType.Value;
        if (ann.Retries.HasValue) builder.Retries = ann.Retries.Value;
        if (ann.MaxInFlightRequestsPerConnection.HasValue) builder.MaxInFlightRequestsPerConnection = ann.MaxInFlightRequestsPerConnection.Value;
        if (ann.DeliveryTimeoutMs.HasValue) builder.DeliveryTimeoutMs = ann.DeliveryTimeoutMs.Value;
        if (ann.RequestTimeoutMs.HasValue) builder.RequestTimeoutMs = ann.RequestTimeoutMs.Value;
        if (ann.BufferMemory.HasValue) builder.BufferMemory = ann.BufferMemory.Value;
        if (ann.MaxBlockMs.HasValue) builder.MaxBlockMs = ann.MaxBlockMs.Value;

        return builder;
    }

    /// <summary>
    /// Returns the Kafka transaction group for this entity type, or <see langword="null"/>
    /// if the entity does not participate in a Kafka transaction.
    /// </summary>
    public static string? GetTransactionGroup(this IEntityType entityType)
        => entityType.FindAnnotation(KEFCoreAnnotationNames.TransactionGroup)?.Value as string;

    /// <summary>
    /// Returns the cache TTL for forward enumeration of this entity type's state store.
    /// Returns <see cref="TimeSpan.Zero"/> if caching is not configured — the proxy
    /// is a transparent pass-through with no allocation overhead.
    /// </summary>
    public static TimeSpan GetValueBufferCacheTtl(this IEntityType entityType)
        => entityType.FindAnnotation(KEFCoreAnnotationNames.ValueBufferCacheTtl)?.Value as TimeSpan?
           ?? TimeSpan.Zero;

    /// <summary>
    /// Returns the cache TTL for reverse enumeration of this entity type's state store.
    /// Returns <see cref="TimeSpan.Zero"/> if caching is not configured.
    /// </summary>
    public static TimeSpan GetValueBufferReverseCacheTtl(this IEntityType entityType)
        => entityType.FindAnnotation(KEFCoreAnnotationNames.ValueBufferReverseCacheTtl)?.Value as TimeSpan?
           ?? TimeSpan.Zero;

    /// <summary>
    /// Builds the <see cref="ConsumerConfigBuilder"/> for this entity type, merging the global
    /// <see cref="IKEFCoreSingletonOptions.ConsumerConfig"/> with any per-entity overrides
    /// stored as a <see cref="KEFCoreProducerAnnotation"/> annotation.
    /// Per-entity values take precedence over the global config.
    /// </summary>
    [Obsolete("Option will be removed soon")]
    public static ConsumerConfigBuilder BuildConsumerConfig(this IEntityType entityType, KEFCoreOptionsExtension options)
    {
        var builder = options.ConsumerOptionsBuilder();

        return builder;
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
