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

using MASES.EntityFrameworkCore.KNet.Infrastructure;
using MASES.EntityFrameworkCore.KNet.Metadata;
using MASES.EntityFrameworkCore.KNet.Metadata.Internal;

namespace MASES.EntityFrameworkCore.KNet.Extensions;

/// <summary>
///     Extension methods for <see cref="EntityTypeBuilder" /> for the Kafka provider.
/// </summary>
/// <remarks>
///     See <see href="https://aka.ms/efcore-docs-modeling">Modeling entity types and relationships</see>, and
///     <see href="https://github.com/masesgroup/KEFCore">The EF Core Kafka database provider</see> for more information and examples.
/// </remarks>
public static class KEFCoreEntityTypeBuilderExtensions
{
    /// <summary>
    /// Sets the KEFCore event management behavior for this entity type,
    /// overriding the context-level default.
    /// </summary>
    /// <remarks>
    /// Passing <see langword="false"/> is equivalent to applying
    /// <see cref="KEFCoreIgnoreEventsAttribute"/> on the entity class.
    /// </remarks>
    /// <param name="entityTypeBuilder">The <see cref="EntityTypeBuilder"/> to configure.</param>
    /// <param name="manageEvents">
    /// <see langword="true"/> to enable event management (default);
    /// <see langword="false"/> to disable it for this entity.
    /// </param>
    /// <returns>The same <see cref="EntityTypeBuilder"/> for chaining.</returns>
    public static EntityTypeBuilder HasKEFCoreManageEvents(
        this EntityTypeBuilder entityTypeBuilder, bool manageEvents = true)
    {
        entityTypeBuilder.HasAnnotation(KEFCoreAnnotationNames.ManageEvents, manageEvents);
        return entityTypeBuilder;
    }

    /// <summary>
    /// Sets the number of partitions for the Kafka topic associated with this entity type,
    /// overriding <see cref="KEFCoreDbContext.DefaultNumPartitions"/>.
    /// Equivalent to applying <see cref="KEFCoreTopicPartitionsAttribute"/> on the entity class.
    /// Applied at topic creation time.
    /// </summary>
    /// <param name="builder">The <see cref="EntityTypeBuilder"/> to configure.</param>
    /// <param name="numPartitions">The number of partitions. Must be greater than zero.</param>
    /// <returns>The same <see cref="EntityTypeBuilder"/> for chaining.</returns>
    public static EntityTypeBuilder HasKEFCoreTopicPartitions(
        this EntityTypeBuilder builder, int numPartitions)
    {
        if (numPartitions <= 0) throw new ArgumentOutOfRangeException(nameof(numPartitions), "Must be greater than zero.");
        builder.Metadata.SetAnnotation(KEFCoreAnnotationNames.NumPartitions, numPartitions);
        return builder;
    }

    /// <summary>
    /// Sets the replication factor for the Kafka topic associated with this entity type,
    /// overriding <see cref="KEFCoreDbContext.DefaultReplicationFactor"/>.
    /// Equivalent to applying <see cref="KEFCoreTopicReplicationFactorAttribute"/> on the entity class.
    /// Applied at topic creation time.
    /// </summary>
    /// <param name="builder">The <see cref="EntityTypeBuilder"/> to configure.</param>
    /// <param name="replicationFactor">The replication factor. Must be greater than zero.</param>
    /// <returns>The same <see cref="EntityTypeBuilder"/> for chaining.</returns>
    public static EntityTypeBuilder HasKEFCoreTopicReplicationFactor(
        this EntityTypeBuilder builder, short replicationFactor)
    {
        if (replicationFactor <= 0) throw new ArgumentOutOfRangeException(nameof(replicationFactor), "Must be greater than zero.");
        builder.Metadata.SetAnnotation(KEFCoreAnnotationNames.ReplicationFactor, replicationFactor);
        return builder;
    }

    /// <summary>
    /// Sets the retention policy for the Kafka topic associated with this entity type,
    /// overriding the values in <see cref="KEFCoreDbContext.TopicConfig"/>.
    /// Equivalent to applying <see cref="KEFCoreTopicRetentionAttribute"/> on the entity class.
    /// Applied at topic creation time.
    /// </summary>
    /// <param name="builder">The <see cref="EntityTypeBuilder"/> to configure.</param>
    /// <param name="retentionBytes">
    /// Maximum size in bytes before older segments are deleted. Use <c>-1</c> to leave at cluster default.
    /// </param>
    /// <param name="retentionMs">
    /// Maximum retention time in milliseconds. Use <c>-1</c> to leave at cluster default.
    /// </param>
    /// <returns>The same <see cref="EntityTypeBuilder"/> for chaining.</returns>
    public static EntityTypeBuilder HasKEFCoreTopicRetention(
        this EntityTypeBuilder builder, long retentionBytes = -1, long retentionMs = -1)
    {
        if (retentionBytes >= 0)
            builder.Metadata.SetAnnotation(KEFCoreAnnotationNames.TopicRetentionBytes, retentionBytes);
        if (retentionMs >= 0)
            builder.Metadata.SetAnnotation(KEFCoreAnnotationNames.TopicRetentionMs, retentionMs);
        return builder;
    }

    /// <summary>
    /// Marks this entity type as read-only, preventing any write via
    /// <see cref="Microsoft.EntityFrameworkCore.DbContext.SaveChanges()"/>.
    /// Equivalent to applying <see cref="KEFCoreReadOnlyAttribute"/> on the entity class.
    /// Unlike <see cref="KEFCoreDbContext.ReadOnlyMode"/> which applies to the entire context,
    /// this applies only to this entity type.
    /// </summary>
    /// <param name="builder">The <see cref="EntityTypeBuilder"/> to configure.</param>
    /// <returns>The same <see cref="EntityTypeBuilder"/> for chaining.</returns>
    public static EntityTypeBuilder IsKEFCoreReadOnly(this EntityTypeBuilder builder)
    {
        builder.Metadata.SetAnnotation(KEFCoreAnnotationNames.ReadOnly, true);
        return builder;
    }

    /// <summary>
    /// Configures the Kafka Streams store query optimization flags for this entity type,
    /// overriding the context-level defaults from <see cref="KEFCoreDbContext"/>.
    /// Equivalent to applying <see cref="KEFCoreStoreLookupAttribute"/> on the entity class.
    /// Only the flags explicitly provided are stored — omitted parameters retain the context-level default.
    /// </summary>
    /// <param name="builder">The <see cref="EntityTypeBuilder"/> to configure.</param>
    /// <param name="prefixScan">Enables prefix scan optimization. Default is <see langword="null"/> (inherit).</param>
    /// <param name="singleKeyLookup">Enables single key look-up optimization. Default is <see langword="null"/> (inherit).</param>
    /// <param name="keyRange">Enables key range look-up optimization. Default is <see langword="null"/> (inherit).</param>
    /// <param name="reverse">Enables reverse iteration optimization. Default is <see langword="null"/> (inherit).</param>
    /// <param name="reverseKeyRange">Enables reverse key range look-up optimization. Default is <see langword="null"/> (inherit).</param>
    /// <returns>The same <see cref="EntityTypeBuilder"/> for chaining.</returns>
    public static EntityTypeBuilder HasKEFCoreStoreLookup(
        this EntityTypeBuilder builder,
        bool? prefixScan = null,
        bool? singleKeyLookup = null,
        bool? keyRange = null,
        bool? reverse = null,
        bool? reverseKeyRange = null)
    {
        if (prefixScan.HasValue)
            builder.Metadata.SetAnnotation(KEFCoreAnnotationNames.UseStorePrefixScan, prefixScan.Value);
        if (singleKeyLookup.HasValue)
            builder.Metadata.SetAnnotation(KEFCoreAnnotationNames.UseStoreSingleKeyLookup, singleKeyLookup.Value);
        if (keyRange.HasValue)
            builder.Metadata.SetAnnotation(KEFCoreAnnotationNames.UseStoreKeyRange, keyRange.Value);
        if (reverse.HasValue)
            builder.Metadata.SetAnnotation(KEFCoreAnnotationNames.UseStoreReverse, reverse.Value);
        if (reverseKeyRange.HasValue)
            builder.Metadata.SetAnnotation(KEFCoreAnnotationNames.UseStoreReverseKeyRange, reverseKeyRange.Value);
        return builder;
    }

    /// <summary>
    /// Overrides the serialization types for the Kafka topic associated with this entity type.
    /// Equivalent to applying <see cref="KEFCoreSerDesAttribute"/> on the entity class.
    /// Only the types explicitly provided are stored — omitted parameters inherit the context-level default.
    /// All types must be open generic type definitions (e.g. <c>typeof(MySerDes&lt;&gt;)</c>).
    /// </summary>
    /// <param name="builder">The <see cref="EntityTypeBuilder"/> to configure.</param>
    /// <param name="keySerDesSelectorType">Open generic key serializer selector type, or <see langword="null"/> to inherit.</param>
    /// <param name="valueSerDesSelectorType">Open generic value container serializer selector type, or <see langword="null"/> to inherit.</param>
    /// <param name="valueContainerType">Open generic value container type, or <see langword="null"/> to inherit.</param>
    /// <returns>The same <see cref="EntityTypeBuilder"/> for chaining.</returns>
    public static EntityTypeBuilder HasKEFCoreSerDes(
        this EntityTypeBuilder builder,
        Type? keySerDesSelectorType = null,
        Type? valueSerDesSelectorType = null,
        Type? valueContainerType = null)
    {
        if (keySerDesSelectorType != null)
        {
            if (!keySerDesSelectorType.IsGenericTypeDefinition)
                throw new ArgumentException($"{keySerDesSelectorType.Name} must be an open generic type definition.", nameof(keySerDesSelectorType));
            builder.Metadata.SetAnnotation(KEFCoreAnnotationNames.KeySerDesSelectorType, keySerDesSelectorType);
        }
        if (valueSerDesSelectorType != null)
        {
            if (!valueSerDesSelectorType.IsGenericTypeDefinition)
                throw new ArgumentException($"{valueSerDesSelectorType.Name} must be an open generic type definition.", nameof(valueSerDesSelectorType));
            builder.Metadata.SetAnnotation(KEFCoreAnnotationNames.ValueSerDesSelectorType, valueSerDesSelectorType);
        }
        if (valueContainerType != null)
        {
            if (!valueContainerType.IsGenericTypeDefinition)
                throw new ArgumentException($"{valueContainerType.Name} must be an open generic type definition.", nameof(valueContainerType));
            builder.Metadata.SetAnnotation(KEFCoreAnnotationNames.ValueContainerType, valueContainerType);
        }
        return builder;
    }
}
