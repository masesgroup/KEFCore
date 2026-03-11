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
/// Implementation of <see cref="IValueContainerData"/>
/// </summary>
/// <param name="Metadata">The <see cref="IValueContainerMetadata"/> containing the metadata information</param>
/// <param name="Values">The values associated to <see cref="IValueContainerMetadata.FlattenedProperties"/></param>
/// <param name="ComplexValues">The values associated to <see cref="IValueContainerMetadata.ComplexProperties"/></param>
public record ValueContainerData(IValueContainerMetadata Metadata, object[] Values, object[]? ComplexValues) : IValueContainerData
{
    /// <inheritdoc/>
    public IEntityType EntityType => Metadata.EntityType;
    /// <inheritdoc/>
    public IProperty[] Properties => Metadata.Properties;
    /// <inheritdoc/>
    public IProperty[] FlattenedProperties => Metadata.FlattenedProperties;
    /// <inheritdoc/>
    public IComplexProperty[]? ComplexProperties => Metadata.ComplexProperties;
    /// <inheritdoc/>
    public object?[] PropertyValues => Values;
    /// <inheritdoc/>
    public object?[]? ComplexPropertyValues => ComplexValues;
}
