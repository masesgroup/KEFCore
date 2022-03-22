// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

/*
*Copyright 2022 MASES s.r.l.
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

public class KafkaIntegerValueGenerator<TValue> : ValueGenerator<TValue>, IKafkaIntegerValueGenerator
{
    private readonly int _propertyIndex;
    private long _current;

    public KafkaIntegerValueGenerator(int propertyIndex)
    {
        _propertyIndex = propertyIndex;
    }

    public virtual void Bump(object?[] row)
    {
        var newValue = (long)Convert.ChangeType(row[_propertyIndex]!, typeof(long));

        if (_current < newValue)
        {
            Interlocked.Exchange(ref _current, newValue);
        }
    }

    public override TValue Next(EntityEntry entry)
        => (TValue)Convert.ChangeType(Interlocked.Increment(ref _current), typeof(TValue), CultureInfo.InvariantCulture);

    public override bool GeneratesTemporaryValues
        => false;
}
