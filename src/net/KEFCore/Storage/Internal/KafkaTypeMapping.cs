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
#if NET8_0
using Microsoft.EntityFrameworkCore.Storage.Json;
#endif
namespace MASES.EntityFrameworkCore.KNet.Storage.Internal;
/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public class KafkaTypeMapping : CoreTypeMapping
{
    /// <summary>
    /// Default initializer
    /// </summary>
    public KafkaTypeMapping(
        Type clrType,
        ValueComparer? comparer = null,
        ValueComparer? keyComparer = null)
        : base(
            new CoreTypeMappingParameters(
                clrType,
                converter: null,
                comparer,
                keyComparer))
    {
    }

    private KafkaTypeMapping(CoreTypeMappingParameters parameters)
        : base(parameters)
    {
    }
#if !NET8_0
    /// <inheritdoc/>
    public override CoreTypeMapping Clone(ValueConverter? converter)
        => new KafkaTypeMapping(Parameters.WithComposedConverter(converter));
#else
    /// <inheritdoc/>
    protected override CoreTypeMapping Clone(CoreTypeMappingParameters parameters)
        => new KafkaTypeMapping(parameters);
    /// <inheritdoc/>
    public override CoreTypeMapping WithComposedConverter(ValueConverter? converter, ValueComparer? comparer = null, ValueComparer? keyComparer = null, CoreTypeMapping? elementMapping = null, JsonValueReaderWriter? jsonValueReaderWriter = null)
        => new KafkaTypeMapping(Parameters.WithComposedConverter(converter, comparer, keyComparer, elementMapping, jsonValueReaderWriter));
#endif
}
