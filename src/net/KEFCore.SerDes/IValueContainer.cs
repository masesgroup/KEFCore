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
/// <summary>
/// Implements <see cref="IValueContainerMetadata"/>
/// </summary>
/// <param name="EntityType"><see cref="IValueContainerMetadata.EntityType"/></param>
/// <param name="Properties"><see cref="IValueContainerMetadata.Properties"/></param>
/// <param name="FlattenedProperties"><see cref="IValueContainerMetadata.FlattenedProperties"/></param>
/// <param name="ComplexProperties"><see cref="IValueContainerMetadata.ComplexProperties"/></param>
public record ValueContainerMetadata(IEntityType EntityType, IProperty[] Properties, IProperty[] FlattenedProperties, IComplexProperty[]? ComplexProperties) 
    : IValueContainerMetadata
{
    /// <inheritdoc/>
    public IEntityType EntityType { get; init; } = EntityType;
    /// <inheritdoc/>
    public IProperty[] Properties { get; init; } = Properties;
    /// <inheritdoc/>
    public IProperty[] FlattenedProperties { get; init; } = FlattenedProperties;
    /// <inheritdoc/>
    public IComplexProperty[]? ComplexProperties { get; init; } = ComplexProperties;
}

/// <summary>
/// Contains the data associated to a single data change
/// </summary>
public interface IValueContainerData : IValueContainerMetadata
{
    /// <summary>
    /// The <see cref="Microsoft.EntityFrameworkCore.EntityState"/> associated to <see cref="IValueContainerMetadata.EntityType"/>
    /// </summary>
    EntityState EntityState { get; }
    /// <summary>
    /// The <see cref="ValueBuffer"/> containing all indexed values of the <see cref="IValueContainerMetadata.FlattenedProperties"/>
    /// </summary>
    /// <remarks>Values are indexed and the first values are associated to <see cref="IValueContainerMetadata.Properties"/> too</remarks>
    object?[] PropertyValues { get; }
    /// <summary>
    /// The <see cref="ValueBuffer"/> containing all indexed values of the <see cref="IValueContainerMetadata.ComplexProperties"/>
    /// </summary>
    object?[]? ComplexPropertyValues { get; }
}

/// <summary>
/// This is the main interface a class must implement to be a ValueContainer. More info <see href="https://masesgroup.github.io/KEFCore/articles/serialization.html">here</see>
/// </summary>
/// <typeparam name="T">It is the key <see cref="Type"/> passed from Entity Framework associated to the Entity data will be stored in the ValueContainer</typeparam>
public interface IValueContainer<in T> where T : notnull
{
    /// <summary>
    /// The Entity name of <see cref="IEntityType"/>
    /// </summary>
    string EntityName { get; }
    /// <summary>
    /// The CLR <see cref="Type"/> of <see cref="IEntityType"/>
    /// </summary>
    string ClrType { get; }
    /// <summary>
    /// Returns back the raw data associated to the Entity contained in <see cref="IValueContainer{T}"/> instance
    /// </summary>
    /// <param name="metadata">The requesting <see cref="IValueContainerMetadata"/> to get the data back, can <see langword="null"/> if not available</param>
    /// <param name="allPropertyValues">The array of object to be filled in with the data stored in the <see cref="IValueContainer{T}"/> instance for <paramref name="metadata"/></param>
    /// <param name="complexTypeFactory">The optional <see cref="IComplexTypeConverterFactory"/> instance to manage conversion of <see cref="IComplexType"/></param>
    void GetData(IValueContainerMetadata metadata, ref object[] allPropertyValues, IComplexTypeConverterFactory? complexTypeFactory);
    /// <summary>
    /// Returns back a dictionary of properties (PropertyName, Value) associated to the Entity
    /// </summary>
    /// <param name="complexTypeHook">The optional <see cref="IComplexTypeConverterFactory"/> instance to manage conversion of <see cref="IComplexType"/></param>
    /// <returns>A dictionary of properties (PropertyName, Value) filled in with the data stored in the <see cref="IValueContainer{T}"/> instance</returns>
    IDictionary<string, object?> GetProperties(IComplexTypeConverterFactory? complexTypeHook);
}
