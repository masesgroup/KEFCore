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
/// <param name="EntityType"><see cref="IValueContainerMetadata.EntityType"/></param>
/// <param name="Properties"><see cref="IValueContainerMetadata.Properties"/></param>
/// <param name="FlattenedProperties"><see cref="IValueContainerMetadata.FlattenedProperties"/></param>
/// <param name="ComplexProperties"><see cref="IValueContainerMetadata.ComplexProperties"/></param>
public record ValueContainerMetadata(IEntityType EntityType, IProperty[]? Properties = null, IProperty[]? FlattenedProperties = null, IComplexProperty[]? ComplexProperties = null) 
    : IValueContainerMetadata
{
    /// <inheritdoc/>
    public IEntityType EntityType { get; init; } = EntityType;
    /// <inheritdoc/>
    public IProperty[] Properties { get; init; } = Properties ?? [.. EntityType.GetProperties()];
    /// <inheritdoc/>
    public IProperty[] FlattenedProperties { get; init; } = FlattenedProperties ?? [.. EntityType.GetFlattenedProperties()];
    /// <inheritdoc/>
    public IComplexProperty[]? ComplexProperties { get; init; } = ComplexProperties ?? [.. EntityType.GetComplexProperties()];
}
