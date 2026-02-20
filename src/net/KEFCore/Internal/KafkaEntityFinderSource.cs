// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
using MASES.EntityFrameworkCore.KNet.Storage.Internal;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Internal;

namespace MASES.EntityFrameworkCore.KNet.Internal;

/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public class KafkaEntityFinderSource(IKafkaTableFactory tableFactory) : IEntityFinderSource, IKafkaEntityFinderSource
{
    private readonly IKafkaTableFactory _tableFactory = tableFactory;
    private readonly ConcurrentDictionary<Type, Func<IStateManager, IDbSetSource, IDbSetCache, IEntityType, IEntityFinder>> _cache = new();

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "EF1001:Internal EF Core API usage.", Justification = "No other way to override the find behavior from a provider.")]
    public virtual IEntityFinder Create(
        IStateManager stateManager,
        IDbSetSource setSource,
        IDbSetCache setCache,
        IEntityType type)
        => _cache.GetOrAdd(
            type.ClrType,
            t => (Func<IStateManager, IDbSetSource, IDbSetCache, IEntityType, IEntityFinder>)
                typeof(KafkaEntityFinderSource).GetMethod(nameof(CreateConstructor), BindingFlags.NonPublic | BindingFlags.Static)!
                    .MakeGenericMethod(t).Invoke(null, null)!)(stateManager, setSource, setCache, type);

    private static Func<IStateManager, IDbSetSource, IDbSetCache, IEntityType, IEntityFinder> CreateConstructor<TEntity>()
        where TEntity : class
        => (s, src, c, t) => new KafkaEntityFinder<TEntity>(s, src, c, t);
}