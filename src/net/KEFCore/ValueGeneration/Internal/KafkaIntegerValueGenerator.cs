/*
* Copyright 2023 MASES s.r.l.
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

using System.Globalization;

namespace MASES.EntityFrameworkCore.KNet.ValueGeneration.Internal;
/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public class KafkaIntegerValueGenerator<TValue> : ValueGenerator<TValue>, IKafkaIntegerValueGenerator
{
    private readonly int _propertyIndex;
    private long _current;
    /// <summary>
    /// Default initializer
    /// </summary>
    public KafkaIntegerValueGenerator(int propertyIndex)
    {
        _propertyIndex = propertyIndex;
    }
    /// <inheritdoc/>
    public virtual void Bump(object?[] row)
    {
        var newValue = (long)Convert.ChangeType(row[_propertyIndex]!, typeof(long));

        if (_current < newValue)
        {
            Interlocked.Exchange(ref _current, newValue);
        }
    }
    /// <inheritdoc/>
    public override TValue Next(EntityEntry entry)
        => (TValue)Convert.ChangeType(Interlocked.Increment(ref _current), typeof(TValue), CultureInfo.InvariantCulture);
    /// <inheritdoc/>
    public override bool GeneratesTemporaryValues
        => false;
}
