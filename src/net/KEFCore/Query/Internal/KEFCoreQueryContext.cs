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

using MASES.EntityFrameworkCore.KNet.Storage.Internal;
using Org.Apache.Kafka.Common;

namespace MASES.EntityFrameworkCore.KNet.Query.Internal;
/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
/// <remarks>
/// Default initializer
/// </remarks>
public class KEFCoreQueryContext(QueryContextDependencies dependencies, IKEFCoreCluster cluster) : QueryContext(dependencies)
{
    /// <summary>
    /// Retrieve <see cref="ValueBuffer"/> for the specified <see cref="IEntityType"/>
    /// </summary>
    /// <param name="entityType">The <see cref="IEntityType"/> to retrieve</param>
    /// <returns>An <see cref="IEnumerable{T}"/> of <see cref="ValueBuffer"/></returns>
    public virtual IEnumerable<ValueBuffer> GetValueBuffers(IEntityType entityType)
    {
        return cluster.GetValueBuffers(entityType);
    }
    /// <summary>
    /// Retrieve <see cref="ValueBuffer"/> for the specified <see cref="IEntityType"/> with <paramref name="keyValues"/>
    /// </summary>
    /// <param name="entityType">The <see cref="IEntityType"/> to retrieve</param>
    /// <param name="keyValues">The key values</param>
    /// <returns>An <see cref="IEnumerable{T}"/> of <see cref="ValueBuffer"/></returns>
    public virtual IEnumerable<ValueBuffer> GetValueBuffer(IEntityType entityType, object?[] keyValues)
    {
        var result = cluster.GetValueBuffer(entityType, keyValues);
        return result.HasValue ? [result.Value] : [];
    }
    /// <summary>
    /// Retrieve <see cref="ValueBuffer"/> for the specified <see cref="IEntityType"/> in the range between <paramref name="rangeStart"/> and <paramref name="rangeEnd"/>
    /// </summary>
    /// <param name="entityType">The <see cref="IEntityType"/> to retrieve</param>
    /// <param name="rangeStart">The key values start</param>
    /// <param name="rangeEnd">The key values end</param>
    /// <returns>An <see cref="IEnumerable{T}"/> of <see cref="ValueBuffer"/></returns>
    public virtual IEnumerable<ValueBuffer> GetValueBuffersRange(
        IEntityType entityType,
        object?[]? rangeStart,
        object?[]? rangeEnd)
        => cluster.GetValueBuffersRange(entityType, rangeStart, rangeEnd);

    /// <summary>
    /// Retrieve <see cref="ValueBuffer"/> for the specified <see cref="IEntityType"/> in reverse order
    /// </summary>
    /// <param name="entityType">The <see cref="IEntityType"/> to retrieve</param>
    /// <returns>An <see cref="IEnumerable{T}"/> of <see cref="ValueBuffer"/></returns>
    public virtual IEnumerable<ValueBuffer> GetValueBuffersReverse(IEntityType entityType)
        => cluster.GetValueBuffersReverse(entityType);

    /// <summary>
    /// Retrieve <see cref="ValueBuffer"/> for the specified <see cref="IEntityType"/> in the reverse range between <paramref name="rangeStart"/> and <paramref name="rangeEnd"/>
    /// </summary>
    /// <param name="entityType">The <see cref="IEntityType"/> to retrieve</param>
    /// <param name="rangeStart">The key values start</param>
    /// <param name="rangeEnd">The key values end</param>
    /// <returns>An <see cref="IEnumerable{T}"/> of <see cref="ValueBuffer"/></returns>
    public virtual IEnumerable<ValueBuffer> GetValueBuffersReverseRange(
        IEntityType entityType,
        object?[]? rangeStart, 
        object?[]? rangeEnd)
        => cluster.GetValueBuffersReverseRange(entityType, rangeStart, rangeEnd);

    /// <summary>
    /// Retrieve the <see cref="ValueBuffer"/> using prefix scan
    /// </summary>
    /// <param name="entityType">The <see cref="IEntityType"/> to retrieve</param>
    /// <param name="prefixValues">The prefix</param>
    /// <returns>An <see cref="IEnumerable{T}"/> of <see cref="ValueBuffer"/></returns>
    public virtual IEnumerable<ValueBuffer> GetValueBuffersByPrefix(IEntityType entityType, object?[] prefixValues)
        => cluster.GetValueBuffersByPrefix(entityType, prefixValues);
}
