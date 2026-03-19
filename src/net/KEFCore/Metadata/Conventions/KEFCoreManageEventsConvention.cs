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
/// A convention that resolves and stores the KEFCore event management flag for each entity type
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
///   <item><description><c>HasKEFCoreManageEvents(false)</c> applied to the entity via <c>ModelBuilder</c>.</description></item>
///   <item><description>Context-level default set via <c>UseKEFCoreManageEvents()</c>.</description></item>
///   <item><description><see langword="true"/> (default — events managed).</description></item>
/// </list>
/// </para>
/// </remarks>
public class KEFCoreManageEventsConvention : IEntityTypeAddedConvention
{
    private readonly bool _contextDefault;

    /// <summary>
    /// Initializes a new instance of <see cref="KEFCoreManageEventsConvention"/>.
    /// </summary>
    /// <param name="contextDefault">
    /// The context-level default for event management.
    /// Defaults to <see langword="true"/> — all entities manage events unless overridden.
    /// </param>
    public KEFCoreManageEventsConvention(bool contextDefault = true)
        => _contextDefault = contextDefault;

    /// <inheritdoc/>
    public void ProcessEntityTypeAdded(
        IConventionEntityTypeBuilder entityTypeBuilder,
        IConventionContext<IConventionEntityTypeBuilder> context)
    {
        var clrType = entityTypeBuilder.Metadata.ClrType;

        var manageEvents = clrType.GetCustomAttribute<KEFCoreIgnoreEventsAttribute>() != null
            ? false
            : _contextDefault;

        entityTypeBuilder.HasAnnotation(KEFCoreAnnotationNames.ManageEvents, manageEvents);
    }
}
