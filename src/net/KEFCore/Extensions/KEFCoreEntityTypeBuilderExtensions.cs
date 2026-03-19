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
}
