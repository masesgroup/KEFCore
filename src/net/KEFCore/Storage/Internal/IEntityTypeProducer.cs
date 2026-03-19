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

#nullable enable

using Java.Util.Concurrent;
using Org.Apache.Kafka.Clients.Producer;

namespace MASES.EntityFrameworkCore.KNet.Storage.Internal;
/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public interface IEntityTypeProducer : IDisposable
{
    /// <summary>
    /// Associated <see cref="IEntityType"/>
    /// </summary>
    IEntityType EntityType { get; }
    /// <summary>
    /// Register an instance of <see cref="IKEFCoreDatabase"/> in an instance of <see cref="IKEFCoreCluster"/>
    /// </summary>
    /// <param name="database">The instance of <see cref="IKEFCoreDatabase"/> to be registered</param>
    void Register(IKEFCoreDatabase database);
    /// <summary>
    /// Unregister a previously registered instance of <see cref="IKEFCoreDatabase"/> from an instance of <see cref="IKEFCoreCluster"/>
    /// </summary>
    /// <param name="database">The instance of <see cref="IKEFCoreDatabase"/> to be unregistered</param>
    void Unregister(IKEFCoreDatabase database);
    /// <summary>
    /// Stores an <see cref="IEnumerable{IKEFCoreRowBag}"/>
    /// </summary>
    /// <param name="futures">The <see cref="Future{V}"/> with <see cref="RecordMetadata"/> generated from <see cref="Commit(IList{Future{RecordMetadata}}?, IEnumerable{IKEFCoreRowBag})"/></param>
    /// <param name="records">The <see cref="IEnumerable{IKEFCoreRowBag}"/> to be stored</param>
    /// <returns></returns>
    void Commit(IList<Future<RecordMetadata>>? futures, IEnumerable<IKEFCoreRowBag> records);
    /// <summary>
    /// Retrieve an <see cref="IEnumerable{ValueBuffer}"/>
    /// </summary>
    /// <returns>An <see cref="IEnumerable{ValueBuffer}"/></returns>
    IEnumerable<ValueBuffer> GetValueBuffers();
    /// <summary>
    /// Retrieve an<see cref="ValueBuffer"/> associated to <paramref name="keyValues"/>
    /// </summary>
    /// <param name="keyValues">The key</param>
    /// <returns>An <see cref="IEnumerable{ValueBuffer}"/></returns>
    ValueBuffer? GetValueBuffer(object?[]? keyValues);
    /// <summary>
    /// Retrieve an <see cref="IEnumerable{ValueBuffer}"/> in the range <paramref name="rangeStart"/>/<paramref name="rangeEnd"/>
    /// </summary>
    /// <param name="rangeStart">The start key</param>
    /// <param name="rangeEnd">The end key</param>
    /// <returns>An <see cref="IEnumerable{ValueBuffer}"/></returns>
    IEnumerable<ValueBuffer> GetValueBuffersRange(object?[]? rangeStart, object?[]? rangeEnd);
    /// <summary>
    /// Retrieve a reverse order <see cref="IEnumerable{ValueBuffer}"/>
    /// </summary>
    /// <returns>An <see cref="IEnumerable{ValueBuffer}"/></returns>
    IEnumerable<ValueBuffer> GetValueBuffersReverse();
    /// <summary>
    /// Retrieve an <see cref="IEnumerable{ValueBuffer}"/> in the reverse range <paramref name="rangeStart"/>/<paramref name="rangeEnd"/>
    /// </summary>
    /// <param name="rangeStart">The start key</param>
    /// <param name="rangeEnd">The end key</param>
    /// <returns>An <see cref="IEnumerable{ValueBuffer}"/></returns>
    IEnumerable<ValueBuffer> GetValueBuffersReverseRange(object?[]? rangeStart, object?[]? rangeEnd);
    /// <summary>
    /// Retrieve the <see cref="ValueBuffer"/> using prefix scan
    /// </summary>
    /// <param name="prefixValues">The prefix</param>
    /// <returns>An <see cref="IEnumerable{T}"/> of <see cref="ValueBuffer"/></returns>
    IEnumerable<ValueBuffer> GetValueBuffersByPrefix(object?[]? prefixValues);
    /// <summary>
    /// Starts the <see cref="IEntityTypeProducer"/> instance
    /// </summary>
    void Start();
    /// <summary>
    /// Verify if local instance is synchronized with the <see cref="IKEFCoreCluster"/> instance
    /// </summary>
    bool? EnsureSynchronized(long timeout);
}

/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public interface IEntityTypeProducer<TKey> : IEntityTypeProducer where TKey : notnull
{
    /// <summary>
    /// Check if a <paramref name="key"/> exist
    /// </summary>
    /// <param name="key">The key to check for existence</param>
    /// <returns><see langword="true"/> if the <paramref name="key"/> exist, <see langword="false"/> otherwise</returns>
    bool Exist(TKey key);
    /// <summary>
    /// Returns the values associated to the <paramref name="key"/>
    /// </summary>
    /// <param name="key">The key to retrieve</param>
    /// <param name="valueBuffer">A <see cref="ValueBuffer"/> containing the information, or <see langword="null"/> otherwise</param>
    /// <returns><see langword="true"/> if the <paramref name="key"/> exist, <see langword="false"/> otherwise</returns>
    bool TryGetValueBuffer(TKey key, out ValueBuffer valueBuffer);
    /// <summary>
    /// Try add a new item based on the values associated to the <paramref name="keyValues"/>
    /// </summary>
    /// <param name="keyValues">The key values to manage</param>
    void TryAddKey(object[] keyValues);
}
