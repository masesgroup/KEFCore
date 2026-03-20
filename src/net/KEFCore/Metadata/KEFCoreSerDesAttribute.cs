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

using MASES.EntityFrameworkCore.KNet.Infrastructure;

namespace MASES.EntityFrameworkCore.KNet.Metadata;

/// <summary>
/// Overrides the serialization types for the Kafka topic associated with this entity type.
/// Takes precedence over the context-level <see cref="KEFCoreDbContext.KeySerDesSelectorType"/>,
/// <see cref="KEFCoreDbContext.ValueSerDesSelectorType"/> and <see cref="KEFCoreDbContext.ValueContainerType"/>.
/// </summary>
/// <remarks>
/// All three types must be open generic type definitions (e.g. <c>typeof(MySerDes&lt;&gt;)</c>).
/// Omitted parameters inherit the context-level default.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class KEFCoreSerDesAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of <see cref="KEFCoreSerDesAttribute"/>.
    /// </summary>
    /// <param name="keySerDesSelectorType">
    /// The open generic type for key serialization, or <see langword="null"/> to inherit the context default.
    /// </param>
    /// <param name="valueSerDesSelectorType">
    /// The open generic type for value container serialization, or <see langword="null"/> to inherit the context default.
    /// </param>
    /// <param name="valueContainerType">
    /// The open generic type for the value container, or <see langword="null"/> to inherit the context default.
    /// </param>
    public KEFCoreSerDesAttribute(
        Type? keySerDesSelectorType = null,
        Type? valueSerDesSelectorType = null,
        Type? valueContainerType = null)
    {
        if (keySerDesSelectorType != null && !keySerDesSelectorType.IsGenericTypeDefinition)
            throw new ArgumentException($"{keySerDesSelectorType.Name} must be an open generic type definition.", nameof(keySerDesSelectorType));
        if (valueSerDesSelectorType != null && !valueSerDesSelectorType.IsGenericTypeDefinition)
            throw new ArgumentException($"{valueSerDesSelectorType.Name} must be an open generic type definition.", nameof(valueSerDesSelectorType));
        if (valueContainerType != null && !valueContainerType.IsGenericTypeDefinition)
            throw new ArgumentException($"{valueContainerType.Name} must be an open generic type definition.", nameof(valueContainerType));

        KeySerDesSelectorType = keySerDesSelectorType;
        ValueSerDesSelectorType = valueSerDesSelectorType;
        ValueContainerType = valueContainerType;
    }

    /// <summary>The open generic key serializer selector type, or <see langword="null"/> to inherit the context default.</summary>
    public Type? KeySerDesSelectorType { get; }
    /// <summary>The open generic value container serializer selector type, or <see langword="null"/> to inherit the context default.</summary>
    public Type? ValueSerDesSelectorType { get; }
    /// <summary>The open generic value container type, or <see langword="null"/> to inherit the context default.</summary>
    public Type? ValueContainerType { get; }
}
