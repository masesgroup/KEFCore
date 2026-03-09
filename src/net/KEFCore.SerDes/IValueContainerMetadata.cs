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
/// Contains the metadata associated to a single data change
/// </summary>
public interface IValueContainerMetadata
{
    /// <summary>
    /// The <see cref="IEntityType"/> with changes
    /// </summary>
    IEntityType EntityType { get; }
    /// <summary>
    /// The <see cref="IProperty"/> associated to <see cref="EntityType"/>
    /// </summary>
    IProperty[] Properties { get; }
    /// <summary>
    /// The flattened <see cref="IProperty"/> associated to <see cref="EntityType"/>
    /// </summary>
    IProperty[] FlattenedProperties { get; }
    /// <summary>
    /// The <see cref="IComplexProperty"/> associated to <see cref="EntityType"/>
    /// </summary>
    IComplexProperty[]? ComplexProperties { get; }
}
