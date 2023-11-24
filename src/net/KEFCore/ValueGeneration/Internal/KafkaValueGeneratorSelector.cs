/*
*  Copyright 2023 MASES s.r.l.
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

using MASES.EntityFrameworkCore.KNet.Storage.Internal;

namespace MASES.EntityFrameworkCore.KNet.ValueGeneration.Internal;
/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public class KafkaValueGeneratorSelector : ValueGeneratorSelector
{
    private readonly IKafkaCluster _kafkaCluster;
    /// <summary>
    /// Default initializer
    /// </summary>
    public KafkaValueGeneratorSelector(
        ValueGeneratorSelectorDependencies dependencies,
        IKafkaDatabase kafkaDatabase)
        : base(dependencies)
    {
        _kafkaCluster = kafkaDatabase.Cluster;
    }
#if !NET8_0
    /// <inheritdoc/>
    public override ValueGenerator Select(IProperty property, IEntityType entityType)
        => property.GetValueGeneratorFactory() == null
            && property.ClrType.IsInteger()
            && property.ClrType.UnwrapNullableType() != typeof(char)
                ? GetOrCreate(property)
                : base.Select(property, entityType);
#endif
    private ValueGenerator GetOrCreate(IProperty property)
    {
        var type = property.ClrType.UnwrapNullableType().UnwrapEnumType();

        if (type == typeof(long))
        {
            return _kafkaCluster.GetIntegerValueGenerator<long>(property);
        }

        if (type == typeof(int))
        {
            return _kafkaCluster.GetIntegerValueGenerator<int>(property);
        }

        if (type == typeof(short))
        {
            return _kafkaCluster.GetIntegerValueGenerator<short>(property);
        }

        if (type == typeof(byte))
        {
            return _kafkaCluster.GetIntegerValueGenerator<byte>(property);
        }

        if (type == typeof(ulong))
        {
            return _kafkaCluster.GetIntegerValueGenerator<ulong>(property);
        }

        if (type == typeof(uint))
        {
            return _kafkaCluster.GetIntegerValueGenerator<uint>(property);
        }

        if (type == typeof(ushort))
        {
            return _kafkaCluster.GetIntegerValueGenerator<ushort>(property);
        }

        if (type == typeof(sbyte))
        {
            return _kafkaCluster.GetIntegerValueGenerator<sbyte>(property);
        }

        throw new ArgumentException(
            CoreStrings.InvalidValueGeneratorFactoryProperty(
                "KafkaIntegerValueGeneratorFactory", property.Name, property.DeclaringEntityType.DisplayName()));
    }
}
