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

using MASES.EntityFrameworkCore.KNet.Metadata;
using MASES.EntityFrameworkCore.KNet.Metadata.Internal;
using System.ComponentModel.DataAnnotations.Schema;

namespace MASES.EntityFrameworkCore.KNet.Extensions;

/// <summary>
/// Extension methods on <see cref="ModelBuilder"/> for Kafka-specific model configuration.
/// </summary>
public static class KEFCoreModelBuilderExtensions
{
    /// <summary>
    /// Sets a global Kafka topic prefix for all entity types in the model.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The prefix is stored as a model annotation (<see cref="KEFCoreAnnotationNames.TopicPrefix"/>)
    /// and consumed by <see cref="MASES.EntityFrameworkCore.KNet.Metadata.Conventions.KafkaTopicNamingConvention"/> during model building.
    /// </para>
    /// <para>
    /// Per-entity overrides are still possible via <see cref="KafkaTopicPrefixAttribute"/>
    /// applied directly to the entity class.
    /// </para>
    /// <para>
    /// Passing <see langword="null"/> explicitly disables prefixing for all entities
    /// that do not have a <see cref="KafkaTopicPrefixAttribute"/>.
    /// </para>
    /// </remarks>
    /// <param name="modelBuilder">The <see cref="ModelBuilder"/> to configure.</param>
    /// <param name="prefix">The topic prefix, or <see langword="null"/> to disable prefixing.</param>
    /// <returns>The same <see cref="ModelBuilder"/> for chaining.</returns>
    public static ModelBuilder UseKafkaTopicPrefix(
        this ModelBuilder modelBuilder, string? prefix)
    {
        modelBuilder.HasAnnotation(KEFCoreAnnotationNames.TopicPrefix, prefix);
        return modelBuilder;
    }
    /// <summary>
    /// Sets the Kafka topic name for this entity type, overriding the default naming convention.
    /// </summary>
    /// <remarks>
    /// Equivalent to applying <see cref="KafkaTopicAttribute"/> on the entity class.
    /// Takes precedence over <see cref="TableAttribute"/> and the entity type name.
    /// </remarks>
    /// <param name="entityTypeBuilder">The <see cref="EntityTypeBuilder"/> to configure.</param>
    /// <param name="topicName">The Kafka topic name.</param>
    /// <returns>The same <see cref="EntityTypeBuilder"/> for chaining.</returns>
    public static EntityTypeBuilder ToKafkaTopic(
        this EntityTypeBuilder entityTypeBuilder, string topicName)
    {
        entityTypeBuilder.HasAnnotation(KEFCoreAnnotationNames.TopicName, topicName);
        return entityTypeBuilder;
    }

    /// <summary>
    /// Sets the Kafka topic prefix for this entity type, overriding the context-level prefix.
    /// </summary>
    /// <param name="entityTypeBuilder">The <see cref="EntityTypeBuilder"/> to configure.</param>
    /// <param name="prefix">The topic prefix, or <see langword="null"/> to disable prefixing for this entity.</param>
    /// <returns>The same <see cref="EntityTypeBuilder"/> for chaining.</returns>
    public static EntityTypeBuilder HasKafkaTopicPrefix(
        this EntityTypeBuilder entityTypeBuilder, string? prefix)
    {
        entityTypeBuilder.HasAnnotation(KEFCoreAnnotationNames.TopicPrefix, prefix);
        return entityTypeBuilder;
    }
}
