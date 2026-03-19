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

using MASES.EntityFrameworkCore.KNet.Serialization;

namespace MASES.EntityFrameworkCore.KNet.Metadata;

/// <summary>
/// Associates a <see cref="IComplexTypeConverter"/> implementation with this complex type,
/// registering it in <see cref="IComplexTypeConverterFactory"/> at model finalization time.
/// </summary>
/// <remarks>
/// Equivalent to calling <c>HasKEFCoreComplexTypeConverter()</c> via fluent API.
/// The converter type must implement <see cref="IComplexTypeConverter"/>.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
public sealed class KEFCoreComplexTypeConverterAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of <see cref="KEFCoreComplexTypeConverterAttribute"/>.
    /// </summary>
    /// <param name="converterType">
    /// The <see cref="Type"/> implementing <see cref="IComplexTypeConverter"/>.
    /// </param>
    public KEFCoreComplexTypeConverterAttribute(Type converterType)
    {
        if (!typeof(IComplexTypeConverter).IsAssignableFrom(converterType))
            throw new ArgumentException(
                $"{converterType.Name} must implement {nameof(IComplexTypeConverter)}.",
                nameof(converterType));
        ConverterType = converterType;
    }

    /// <summary>The converter type to register.</summary>
    public Type ConverterType { get; }
}