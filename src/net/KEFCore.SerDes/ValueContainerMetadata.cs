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

#nullable enable

namespace MASES.EntityFrameworkCore.KNet.Serialization;

/// <summary>
/// Implements <see cref="IValueContainerMetadata"/>
/// </summary>
public record ValueContainerMetadata 
    : IValueContainerMetadata
{
    /// <summary>
    /// Initialize <see cref="ValueContainerMetadata"/>
    /// </summary>
    /// <param name="entityType"><see cref="IValueContainerMetadata.EntityType"/></param>
    /// <param name="properties"><see cref="IValueContainerMetadata.Properties"/></param>
    /// <param name="flattenedProperties"><see cref="IValueContainerMetadata.FlattenedProperties"/></param>
    /// <param name="complexProperties"><see cref="IValueContainerMetadata.ComplexProperties"/></param>
    public ValueContainerMetadata(
        IEntityType entityType,
        IProperty[]? properties = null,
        IProperty[]? flattenedProperties = null,
        IComplexProperty[]? complexProperties = null)
    {
        EntityType = entityType;
        Properties = properties ?? [.. entityType.GetProperties()];
        FullFlattenedProperties = [.. entityType.GetFlattenedProperties()];
        FlattenedProperties = flattenedProperties ?? FullFlattenedProperties;
        ComplexProperties = complexProperties ?? [.. entityType.GetComplexProperties()];
    }

    /// <inheritdoc/>
    public IEntityType EntityType { get; init; }
    /// <inheritdoc/>
    public IProperty[] Properties { get; init; }
    /// <inheritdoc/>
    /// <remarks>
    /// Always derived from <see cref="EntityType"/> directly, computed once here at construction — deliberately NOT
    /// derived from the (possibly projected) <see cref="FlattenedProperties"/> constructor argument, which may be a
    /// narrower subset when this metadata was built to restrict deserialization to a query's projection (see the
    /// projection push-down design). This must always be the entity's true, full flattened property set. Declared
    /// before <see cref="FlattenedProperties"/> so the latter's initializer can reuse this same array instance
    /// (rather than re-enumerating) whenever no projected subset was supplied.
    /// </remarks>
    public IProperty[] FullFlattenedProperties { get; init; }
    /// <inheritdoc/>
    public IProperty[] FlattenedProperties { get; init; }
    /// <inheritdoc/>
    public IComplexProperty[]? ComplexProperties { get; init; }
}
