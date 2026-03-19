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

namespace MASES.EntityFrameworkCore.KNet.Metadata.Conventions;

/// <summary>
/// A convention that registers <see cref="IComplexTypeConverter"/> implementations
/// declared via <see cref="KEFCoreComplexTypeConverterAttribute"/> or
/// <c>HasKEFCoreComplexTypeConverter()</c> into <see cref="IComplexTypeConverterFactory"/>
/// at model finalization time.
/// </summary>
public class KEFCoreComplexTypeConverterConvention : IModelFinalizingConvention
{
    private readonly IComplexTypeConverterFactory _factory;

    /// <summary>
    /// Initializes a new instance of <see cref="KEFCoreComplexTypeConverterConvention"/>.
    /// </summary>
    /// <param name="factory">The singleton <see cref="IComplexTypeConverterFactory"/>.</param>
    public KEFCoreComplexTypeConverterConvention(IComplexTypeConverterFactory factory)
        => _factory = factory;

    /// <inheritdoc/>
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        foreach (var complexType in modelBuilder.Metadata.GetEntityTypes()
                                                .SelectMany(e => e.GetComplexProperties())
                                                .Select(p => p.ComplexType)
                                                .DistinctBy(ct => ct.ClrType))
        {
            var clrType = complexType.ClrType;

            // 1. annotation from HasKEFCoreComplexTypeConverter() — takes precedence
            var converterType =
                complexType.FindAnnotation(KEFCoreAnnotationNames.ComplexTypeConverter)
                           ?.Value as Type
                // 2. KEFCoreComplexTypeConverterAttribute on the CLR type
                ?? clrType.GetCustomAttribute<KEFCoreComplexTypeConverterAttribute>()
                          ?.ConverterType;

            if (converterType != null)
                _factory.Register(converterType);
        }
    }
}