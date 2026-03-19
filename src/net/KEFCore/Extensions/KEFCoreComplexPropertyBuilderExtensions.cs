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

using MASES.EntityFrameworkCore.KNet.Metadata.Internal;
using MASES.EntityFrameworkCore.KNet.Serialization;

namespace MASES.EntityFrameworkCore.KNet.Extensions;
/// <summary>
/// Extension methods on <see cref="ComplexPropertyBuilder"/> for KEFCore-specific
/// complex type configuration.
/// </summary>
/// <remarks>
/// These methods allow associating an <see cref="IComplexTypeConverter"/> with a complex
/// property via the fluent API, as an alternative to <see cref="MASES.EntityFrameworkCore.KNet.Metadata.KEFCoreComplexTypeConverterAttribute"/>.
/// The converter is registered in <see cref="IComplexTypeConverterFactory"/> at model
/// finalization time by <see cref="MASES.EntityFrameworkCore.KNet.Metadata.Conventions.KEFCoreComplexTypeConverterConvention"/>.
/// </remarks>
public static partial class KEFCoreComplexPropertyBuilderExtensions
{
    /// <summary>
    /// Associates a <see cref="IComplexTypeConverter"/> with this complex property,
    /// registering it in <see cref="IComplexTypeConverterFactory"/> at model finalization time.
    /// </summary>
    /// <typeparam name="TConverter">
    /// The type implementing <see cref="IComplexTypeConverter"/>.
    /// </typeparam>
    /// <param name="builder">The <see cref="ComplexPropertyBuilder"/> to configure.</param>
    /// <returns>The same <see cref="ComplexPropertyBuilder"/> for chaining.</returns>
    public static ComplexPropertyBuilder HasKEFCoreComplexTypeConverter<TConverter>(
        this ComplexPropertyBuilder builder)
        where TConverter : IComplexTypeConverter
    {
        builder.Metadata.ComplexType.SetAnnotation(KEFCoreAnnotationNames.ComplexTypeConverter, typeof(TConverter));
        return builder;
    }

    /// <summary>
    /// Associates a <see cref="IComplexTypeConverter"/> with this complex property,
    /// registering it in <see cref="IComplexTypeConverterFactory"/> at model finalization time.
    /// </summary>
    /// <param name="builder">The <see cref="ComplexPropertyBuilder"/> to configure.</param>
    /// <param name="converterType">
    /// The <see cref="Type"/> implementing <see cref="IComplexTypeConverter"/>.
    /// </param>
    /// <returns>The same <see cref="ComplexPropertyBuilder"/> for chaining.</returns>
    public static ComplexPropertyBuilder HasKEFCoreComplexTypeConverter(
        this ComplexPropertyBuilder builder, Type converterType)
    {
        if (!typeof(IComplexTypeConverter).IsAssignableFrom(converterType))
            throw new ArgumentException(
                $"{converterType.Name} must implement {nameof(IComplexTypeConverter)}.",
                nameof(converterType));

        builder.Metadata.ComplexType.SetAnnotation(
            KEFCoreAnnotationNames.ComplexTypeConverter, converterType);
        return builder;
    }
}