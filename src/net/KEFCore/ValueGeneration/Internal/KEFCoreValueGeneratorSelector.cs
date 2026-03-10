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

using System.Collections.Concurrent;

namespace MASES.EntityFrameworkCore.KNet.ValueGeneration.Internal;
/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
/// <remarks>
/// Default initializer
/// </remarks>
public class KEFCoreValueGeneratorSelector(ValueGeneratorSelectorDependencies dependencies) : ValueGeneratorSelector(dependencies)
{
    private ConcurrentDictionary<IProperty, IKEFCoreValueGenerator>? _generators;

#if NET9_0 || NET10_0
    /// <inheritdoc/>
    public override bool TrySelect(IProperty property, ITypeBase typeBase, out ValueGenerator? valueGenerator)
    {
        if (property.GetValueGeneratorFactory() == null)
        {
            valueGenerator = GetOrCreate(property, typeBase);
            if (valueGenerator != null) return true;
        }
        return base.TrySelect(property, typeBase, out valueGenerator);
    }
#elif NET8_0
    /// <inheritdoc/>
    public override ValueGenerator Select(IProperty property, ITypeBase typeBase)
        => property.GetValueGeneratorFactory() == null ? GetOrCreate(property, typeBase) ?? base.Select(property, typeBase) 
                                                       : base.Select(property, typeBase);
#else
    /// <inheritdoc/>
    public override ValueGenerator Select(IProperty property, IEntityType entityType)
        => property.GetValueGeneratorFactory() == null
            && property.ClrType.IsInteger()
            && property.ClrType.UnwrapNullableType() != typeof(char)
                ? GetOrCreate(property)
                : base.Select(property, entityType);
#endif

    private ValueGenerator GetOrCreate(IProperty property, ITypeBase typeBase)
    {
        if (property.ClrType.IsInteger()
            && property.ClrType.UnwrapNullableType() != typeof(char))
        {
            var type = property.ClrType.UnwrapNullableType().UnwrapEnumType();

            if (type == typeof(long))
            {
                return GetIntegerValueGenerator<long>(property);
            }

            if (type == typeof(int))
            {
                return GetIntegerValueGenerator<int>(property);
            }

            if (type == typeof(short))
            {
                return GetIntegerValueGenerator<short>(property);
            }

            if (type == typeof(byte))
            {
                return GetIntegerValueGenerator<byte>(property);
            }

            if (type == typeof(ulong))
            {
                return GetIntegerValueGenerator<ulong>(property);
            }

            if (type == typeof(uint))
            {
                return GetIntegerValueGenerator<uint>(property);
            }

            if (type == typeof(ushort))
            {
                return GetIntegerValueGenerator<ushort>(property);
            }

            if (type == typeof(sbyte))
            {
                return GetIntegerValueGenerator<sbyte>(property);
            }
#if NET8_0 || NET9_0 || NET10_0
            throw new ArgumentException(
                CoreStrings.InvalidValueGeneratorFactoryProperty(
                    "KEFCoreIntegerValueGeneratorFactory", property.Name, property.DeclaringType.DisplayName()));
#else
            throw new ArgumentException(
                CoreStrings.InvalidValueGeneratorFactoryProperty(
                    "KEFCoreIntegerValueGeneratorFactory", property.Name, property.DeclaringEntityType.DisplayName()));
#endif
        }
        else if (property.ClrType == typeof(DateTime)
                 || property.ClrType.UnwrapNullableType() == typeof(DateTime))
        {
            return GetDateTimeValueGenerator(property);
        }
        else if (property.ClrType == typeof(DateTimeOffset)
                 || property.ClrType.UnwrapNullableType() == typeof(DateTimeOffset))
        {
            return GetDateTimeOffsetValueGenerator(property);
        }

        return null!;
    }

    /// <inheritdoc/>
    ValueGenerator GetIntegerValueGenerator<TProperty>(IProperty property)
    {
        _generators ??= new ConcurrentDictionary<IProperty, IKEFCoreValueGenerator>();

        var propertyIndex = property.GetIndex();
        if (!_generators.TryGetValue(property, out var generator))
        {
            generator = new KEFCoreIntegerValueGenerator<TProperty>(propertyIndex);
            _generators[property] = generator;
        }

        return (ValueGenerator)generator;
    }

    /// <inheritdoc/>
    ValueGenerator GetDateTimeValueGenerator(IProperty property)
    {
        _generators ??= new ConcurrentDictionary<IProperty, IKEFCoreValueGenerator>();

        var propertyIndex = property.GetIndex();
        if (!_generators.TryGetValue(property, out var generator))
        {
            generator = new KEFCoreDateTimeValueGenerator(propertyIndex);
            _generators[property] = generator;
        }

        return (ValueGenerator)generator;
    }

    /// <inheritdoc/>
    ValueGenerator GetDateTimeOffsetValueGenerator(IProperty property)
    {
        _generators ??= new ConcurrentDictionary<IProperty, IKEFCoreValueGenerator>();

        var propertyIndex = property.GetIndex();
        if (!_generators.TryGetValue(property, out var generator))
        {
            generator = new KEFCoreDateTimeOffsetValueGenerator(propertyIndex);
            _generators[property] = generator;
        }

        return (ValueGenerator)generator;
    }
}
