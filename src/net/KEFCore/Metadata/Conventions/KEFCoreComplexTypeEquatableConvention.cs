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

namespace MASES.EntityFrameworkCore.KNet.Metadata.Conventions;

/// <summary>
/// A convention that verifies all complex types in the model implement value equality,
/// either via <see cref="IEquatable{T}"/> or by overriding <see cref="object.Equals(object)"/>.
/// </summary>
/// <remarks>
/// <para>
/// KEFCore relies on value equality for complex types to correctly detect changes
/// and serialize data to Kafka topics. Reference equality (the default .NET behavior)
/// would cause incorrect change tracking and data loss.
/// </para>
/// <para>
/// To suppress this check for a specific complex type, apply
/// <see cref="KEFCoreIgnoreEquatableCheckAttribute"/> to the class.
/// </para>
/// </remarks>
public class KEFCoreComplexTypeEquatableConvention : IModelFinalizingConvention
{
    /// <inheritdoc/>
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        foreach (var complexType in modelBuilder.Metadata.GetEntityTypes()
                                                .SelectMany(e => e.GetComplexProperties())
                                                .Select(p => p.ComplexType))
        {
            var clrType = complexType.ClrType;

            // opt-out via attribute
            if (clrType.GetCustomAttribute<KEFCoreIgnoreEquatableCheckAttribute>() != null)
                continue;

            var hasIEquatable = clrType.GetInterfaces()
                .Any(i => i.IsGenericType
                          && i.GetGenericTypeDefinition() == typeof(IEquatable<>)
                          && i.GetGenericArguments()[0] == clrType);

            var hasEqualsOverride = clrType.GetMethod("Equals", [typeof(object)])
                                          ?.DeclaringType == clrType;

            if (!hasIEquatable && !hasEqualsOverride)
            {
                throw new InvalidOperationException(
                    $"Complex type '{clrType.FullName}' must implement " +
                    $"IEquatable<{clrType.Name}> or override Equals(object). " +
                    $"KEFCore requires value equality for complex types to guarantee " +
                    $"correct change tracking and serialization. " +
                    $"Apply {nameof(KEFCoreIgnoreEquatableCheckAttribute)} to suppress this check.");
            }
        }
    }
}