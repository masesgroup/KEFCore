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

using MASES.EntityFrameworkCore.KNet.Serialization;

namespace MASES.EntityFrameworkCore.KNet.Storage.Internal;
/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
/// <remarks>
/// Default initializer
/// </remarks>
public class KafkaRowBag<TKey>(IUpdateEntry entry, string topicName, TKey key,
                               IProperty[] properties, object?[]? propertyValues,
                               IComplexProperty[]? complexProperties, object?[]? complexPropertyValues) : IKafkaRowBag
    where TKey : notnull
{
    readonly TKey _key = key;

    /// <summary>
    /// The <see cref="IEntityType"/> with changes
    /// </summary>
    public IEntityType EntityType { get; } = entry.EntityType;
    /// <summary>
    /// The <see cref="IProperty"/> associated to <see cref="EntityType"/>
    /// </summary>
    public IProperty[] Properties { get; } = properties;
    /// <summary>
    /// The <see cref="IComplexProperty"/> associated to <see cref="EntityType"/>
    /// </summary>
    public IComplexProperty[]? ComplexProperties { get; } = complexProperties;
    /// <summary>
    /// The <see cref="EntityState"/> associated to <see cref="EntityType"/>
    /// </summary>
    public EntityState EntityState { get; } = entry.EntityState;
    /// <inheritdoc/>
    public string AssociatedTopicName { get; } = topicName;
    /// <summary>
    /// The Key
    /// </summary>
    public TKeyLocal GetKey<TKeyLocal>() where TKeyLocal : notnull => (TKeyLocal)(object)_key;
    /// <summary>
    /// The Value
    /// </summary>
    public TValueContainer? GetValue<TKeyLocal, TValueContainer>(Func<IEntityType, IProperty[]?, object?[], IComplexProperty[]?, object?[]?, IComplexTypeConverterFactory?, TValueContainer> creator, IComplexTypeConverterFactory complexTypeConverterFactory)
        where TKeyLocal : notnull
        where TValueContainer : IValueContainer<TKeyLocal>
        => EntityState == EntityState.Deleted ? default : (TValueContainer)creator(EntityType, Properties, PropertyValues, ComplexProperties, ComplexPropertyValues, complexTypeConverterFactory);
    /// <summary>
    /// The <see cref="ValueBuffer"/> containing all indexed values of the <see cref="Properties"/>
    /// </summary>
    public object?[] PropertyValues { get; } = propertyValues!;
    /// <summary>
    /// The <see cref="ValueBuffer"/> containing all indexed values of the <see cref="ComplexProperties"/>
    /// </summary>
    public object?[]? ComplexPropertyValues { get; } = complexPropertyValues;
}
