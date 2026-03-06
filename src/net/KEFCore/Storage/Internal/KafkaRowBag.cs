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
                               IProperty[] properties, IProperty[] flattenedProperties, object?[]? flattenedPropertyValues,
                               IComplexProperty[]? complexProperties, object?[]? complexPropertyValues) : IKafkaRowBag
    where TKey : notnull
{
    readonly TKey _key = key;

    /// <inheritdoc/>
    public IEntityType EntityType { get; } = entry.EntityType;
    /// <inheritdoc/>
    public IProperty[] Properties { get; } = properties;
    /// <inheritdoc/>
    public IProperty[] FlattenedProperties { get; } = flattenedProperties;
    /// <inheritdoc/>
    public IComplexProperty[]? ComplexProperties { get; } = complexProperties;
    /// <inheritdoc/>
    public EntityState EntityState { get; } = entry.EntityState;
    /// <inheritdoc/>
    public string AssociatedTopicName { get; } = topicName;
    /// <inheritdoc/>
    public object?[] PropertyValues { get; } = flattenedPropertyValues!;
    /// <inheritdoc/>
    public object?[]? ComplexPropertyValues { get; } = complexPropertyValues;
    /// <inheritdoc/>
    public TKeyLocal GetKey<TKeyLocal>() where TKeyLocal : notnull => (TKeyLocal)(object)_key;
    /// <inheritdoc/>
    public TValueContainer? GetValue<TKeyLocal, TValueContainer>(Func<IValueContainerData, IComplexTypeConverterFactory?, TValueContainer> creator, IComplexTypeConverterFactory complexTypeConverterFactory)
        where TKeyLocal : notnull
        where TValueContainer : IValueContainer<TKeyLocal>
        => EntityState == EntityState.Deleted ? default : creator(this, complexTypeConverterFactory);
}
