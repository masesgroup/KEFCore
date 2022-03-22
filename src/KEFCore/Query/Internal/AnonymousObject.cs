// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

/*
*  Copyright 2022 MASES s.r.l.
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

using JetBrains.Annotations;

namespace MASES.EntityFrameworkCore.KNet.Query.Internal;

public readonly struct AnonymousObject
{
    private readonly object[] _values;

    public static readonly ConstructorInfo AnonymousObjectCtor
        = typeof(AnonymousObject).GetTypeInfo()
            .DeclaredConstructors
            .Single(c => c.GetParameters().Length == 1);

    public AnonymousObject(object[] values)
    {
        _values = values;
    }

    public static bool operator ==(AnonymousObject x, AnonymousObject y)
        => x.Equals(y);

    public static bool operator !=(AnonymousObject x, AnonymousObject y)
        => !x.Equals(y);

    public override bool Equals(object? obj)
        => obj is not null && (obj is AnonymousObject anonymousObject
            && _values.SequenceEqual(anonymousObject._values));

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var value in _values)
        {
            hash.Add(value);
        }

        return hash.ToHashCode();
    }
}
