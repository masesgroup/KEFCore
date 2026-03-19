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

namespace MASES.EntityFrameworkCore.KNet.Metadata.Conventions;

/// <summary>
/// A convention that resolves and stores the Kafka event management flag for each entity type
/// as a model annotation (<see cref="KEFCoreAnnotationNames.ManageEvents"/>).
/// </summary>
/// <remarks>
/// <para>
/// When enabled for an entity, the <c>TimestampExtractor</c> is activated in
/// <c>StreamsManager.AddEntity()</c>, allowing real-time tracking updates from the cluster.
/// Event management is enabled by default for all entity types.
/// </para>
/// <para>
/// Resolution priority:
/// <list type="number">
///   <item><description><see cref="KEFCoreIgnoreEventsAttribute"/> applied to the entity class — always disables events.</description></item>
///   <item><description><c>HasKEFCoreManageEvents(false)</c> applied to the entity via <c>ModelBuilder</c>,
///   read from the <see cref="KEFCoreAnnotationNames.ManageEvents"/> entity annotation.</description></item>
///   <item><description>Context-level default set via
///   <see cref="Extensions.KEFCoreModelBuilderExtensions.UseKEFCoreManageEvents"/>,
///   read from the <see cref="KEFCoreAnnotationNames.ManageEvents"/> model annotation
///   at model finalization time.</description></item>
///   <item><description><see langword="true"/> (default — events managed).</description></item>
/// </list>
/// </para>
/// </remarks>
public class KEFCoreManageEventsConvention : IModelFinalizingConvention
{
    /// <inheritdoc/>
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        // context-level default — set via UseKEFCoreManageEvents() or true if not set
        var contextDefault = modelBuilder.Metadata
            .FindAnnotation(KEFCoreAnnotationNames.ManageEvents)?.Value as bool? ?? true;

        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            var clrType = entityType.ClrType;

            // entity-level annotation set via HasKEFCoreManageEvents() takes precedence
            // over context default, but KEFCoreIgnoreEventsAttribute always wins
            var manageEvents = clrType.GetCustomAttribute<KEFCoreIgnoreEventsAttribute>() != null
                ? false
                : entityType.FindAnnotation(KEFCoreAnnotationNames.ManageEvents)?.Value as bool?
                  ?? contextDefault;

            ((IConventionEntityTypeBuilder)entityType.Builder)
                .HasAnnotation(KEFCoreAnnotationNames.ManageEvents, manageEvents);
        }
    }
}