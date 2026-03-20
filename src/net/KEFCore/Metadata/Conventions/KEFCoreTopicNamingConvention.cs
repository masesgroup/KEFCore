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
///   <item><description><see cref="KEFCoreTopicAttribute"/> applied directly to the entity class.</description></item>
///   <item><description><see cref="System.ComponentModel.DataAnnotations.Schema.TableAttribute"/> applied to the entity class,
///   including schema prefix if specified (e.g. <c>schema.tablename</c>).</description></item>
///   <item><description>The EF Core entity type name (<see cref="IReadOnlyTypeBase.Name"/>), which includes the model namespace.</description></item>
/// </list>
/// </para>
/// <para>
/// The topic prefix is resolved using the following priority order:
/// <list type="number">
///   <item><description><see cref="KEFCoreTopicPrefixAttribute"/> applied directly to the entity class
///   (a <see langword="null"/> prefix explicitly disables prefixing for that entity).</description></item>
///   <item><description>The context-level prefix set via
///   <see cref="Extensions.KEFCoreModelBuilderExtensions.UseKEFCoreTopicPrefix"/>,
///   read from the <see cref="KEFCoreAnnotationNames.TopicPrefix"/> model annotation
///   at model finalization time.</description></item>
/// </list>
/// </para>
/// </remarks>
public class KEFCoreTopicNamingConvention : IModelFinalizingConvention
{
    /// <inheritdoc/>
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        var contextPrefix = modelBuilder.Metadata
            .FindAnnotation(KEFCoreAnnotationNames.TopicPrefix)?.Value as string;

        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            var clrType = entityType.ClrType;
            var tableAttr = clrType.GetCustomAttribute<TableAttribute>();

            var baseName = clrType.GetCustomAttribute<KEFCoreTopicAttribute>()?.TopicName
                           ?? (tableAttr != null
                               ? (tableAttr.Schema != null
                                   ? $"{tableAttr.Schema}.{tableAttr.Name}"
                                   : tableAttr.Name)
                               : entityType.Name);

            var entityPrefixAttr = clrType.GetCustomAttribute<KEFCoreTopicPrefixAttribute>();
            string? prefix = entityPrefixAttr != null ? entityPrefixAttr.Prefix : contextPrefix;

            var fullTopicName = string.IsNullOrEmpty(prefix) ? baseName : $"{prefix}.{baseName}";

            ((IConventionEntityTypeBuilder)entityType.Builder)
                .HasAnnotation(KEFCoreAnnotationNames.TopicName, fullTopicName);
        }
    }
}