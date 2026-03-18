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
using MASES.EntityFrameworkCore.KNet.Metadata.Internal;
using System.ComponentModel.DataAnnotations.Schema;

namespace MASES.EntityFrameworkCore.KNet.Metadata.Conventions;

/// <summary>
/// A convention that resolves and stores the Kafka topic name for each entity type
/// as a model annotation (<see cref="KEFCoreAnnotationNames.TopicName"/>).
/// </summary>
/// <remarks>
/// <para>
/// The topic name is resolved using the following priority order:
/// <list type="number">
///   <item><description><see cref="KafkaTopicAttribute"/> applied directly to the entity class.</description></item>
///   <item><description><see cref="System.ComponentModel.DataAnnotations.Schema.TableAttribute"/> applied to the entity class,
///   including schema prefix if specified (e.g. <c>schema.tablename</c>).</description></item>
///   <item><description>The EF Core entity type name (<see cref="IReadOnlyTypeBase.Name"/>), which includes the model namespace.</description></item>
/// </list>
/// </para>
/// <para>
/// The topic prefix is resolved using the following priority order:
/// <list type="number">
///   <item><description><see cref="KafkaTopicPrefixAttribute"/> applied directly to the entity class
///   (a <see langword="null"/> prefix explicitly disables prefixing for that entity).</description></item>
///   <item><description>The context-level prefix provided at convention registration time,
///   which itself comes from <see cref="KafkaTopicPrefixAttribute"/> on the <see cref="DbContext"/>
///   or from <see cref="KEFCoreModelBuilderExtensions.UseKafkaTopicPrefix"/>.</description></item>
/// </list>
/// </para>
/// </remarks>
/// <remarks>
/// Initializes a new instance of <see cref="KafkaTopicNamingConvention"/>.
/// </remarks>
/// <param name="contextPrefix">
/// The topic prefix defined at context level, or <see langword="null"/> if no prefix applies.
/// </param>
public class KafkaTopicNamingConvention(string? contextPrefix) : IEntityTypeAddedConvention
{
    /// <inheritdoc/>
    public void ProcessEntityTypeAdded(
        IConventionEntityTypeBuilder entityTypeBuilder,
        IConventionContext<IConventionEntityTypeBuilder> context)
    {
        var entityType = entityTypeBuilder.Metadata;
        var clrType = entityType.ClrType;
        var tableAttr = clrType.GetCustomAttribute<TableAttribute>();

        // 1. Topic base name — KafkaTopicAttribute > TableAttribute (with schema) > entityType.Name
        var baseName = clrType.GetCustomAttribute<KafkaTopicAttribute>()?.TopicName
                       ?? (tableAttr != null
                           ? (tableAttr.Schema != null
                               ? $"{tableAttr.Schema}.{tableAttr.Name}"
                               : tableAttr.Name)
                           : entityType.Name);

        // 2. Prefix — KafkaTopicPrefixAttribute on entity > context-level prefix
        var entityPrefixAttr = clrType.GetCustomAttribute<KafkaTopicPrefixAttribute>();
        string? prefix = entityPrefixAttr != null ? entityPrefixAttr.Prefix : contextPrefix;

        // 3. Final composition
        var fullTopicName = string.IsNullOrEmpty(prefix) ? baseName : $"{prefix}.{baseName}";

        entityTypeBuilder.HasAnnotation(KEFCoreAnnotationNames.TopicName, fullTopicName);
    }
}
