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

using MASES.EntityFrameworkCore.KNet.Serialization;

namespace MASES.EntityFrameworkCore.KNet.Storage.Internal;
/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public interface IKEFCoreStreamsRetriever<TKey, TValueContainer> : IDisposable 
    where TKey : notnull
    where TValueContainer : IValueContainer<TKey>
{
    /// <summary>
    /// Retrieve an <see cref="IEnumerable{ValueBuffer}"/> from the <see cref="IKEFCoreStreamsRetriever{TKey, TValueContainer}"/> instance
    /// </summary>
    /// <param name="database">The <see cref="IKEFCoreDatabase"/> requesting the data</param>
    /// <returns>An <see cref="IEnumerable{ValueBuffer}"/></returns>
    IEnumerable<ValueBuffer> GetValueBuffers(IKEFCoreDatabase database);
    /// <summary>
    /// Retrieve an <see cref="IEnumerable{ValueBuffer}"/> in the range <paramref name="rangeStart"/>/<paramref name="rangeEnd"/> from the <see cref="IKEFCoreStreamsRetriever{TKey, TValueContainer}"/> instance
    /// </summary>
    /// <param name="database">The <see cref="IKEFCoreDatabase"/> requesting the data</param>
    /// <param name="keyValueFactory">The key converter</param>
    /// <param name="rangeStart">The start key</param>
    /// <param name="rangeEnd">The end key</param>
    /// <returns>An <see cref="IEnumerable{ValueBuffer}"/></returns>
    IEnumerable<ValueBuffer> GetValueBuffersRange(IKEFCoreDatabase database, IPrincipalKeyValueFactory<TKey> keyValueFactory, object?[]? rangeStart, object?[]? rangeEnd);
    /// <summary>
    /// Retrieve a reverse order <see cref="IEnumerable{ValueBuffer}"/> from the <see cref="IKEFCoreStreamsRetriever{TKey, TValueContainer}"/> instance
    /// </summary>
    /// <param name="database">The <see cref="IKEFCoreDatabase"/> requesting the data</param>
    /// <returns>An <see cref="IEnumerable{ValueBuffer}"/></returns>
    IEnumerable<ValueBuffer> GetValueBuffersReverse(IKEFCoreDatabase database);
    /// <summary>
    /// Retrieve an <see cref="IEnumerable{ValueBuffer}"/> in the reverse range <paramref name="rangeStart"/>/<paramref name="rangeEnd"/> from the <see cref="IKEFCoreStreamsRetriever{TKey, TValueContainer}"/> instance
    /// </summary>
    /// <param name="database">The <see cref="IKEFCoreDatabase"/> requesting the data</param>
    /// <param name="keyValueFactory">The key converter</param>
    /// <param name="rangeStart">The start key</param>
    /// <param name="rangeEnd">The end key</param>
    /// <returns>An <see cref="IEnumerable{ValueBuffer}"/></returns>
    IEnumerable<ValueBuffer> GetValueBuffersReverseRange(IKEFCoreDatabase database, IPrincipalKeyValueFactory<TKey> keyValueFactory, object?[]? rangeStart, object?[]? rangeEnd);
    /// <summary>
    /// Retrieve the <see cref="ValueBuffer"/> using prefix scan
    /// </summary>
    /// <param name="database">The <see cref="IKEFCoreDatabase"/> requesting the data</param>
    /// <param name="keyValueFactory">The key converter</param>
    /// <param name="prefixValues">The prefix</param>
    /// <returns>An <see cref="IEnumerable{T}"/> of <see cref="ValueBuffer"/></returns>
    IEnumerable<ValueBuffer> GetValueBuffersByPrefix(IKEFCoreDatabase database, IPrincipalKeyValueFactory<TKey> keyValueFactory, object?[]? prefixValues);
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
    bool TryGetValue(TKey key, out ValueBuffer valueBuffer);
    /// <summary>
    /// Returns the values associated to the <paramref name="key"/>
    /// </summary>
    /// <param name="key">The key to retrieve</param>
    /// <param name="valueContainer">The <see cref="IValueContainer{T}"/> containing the properties associated to <paramref name="key"/>, or <see langword="null"/> otherwise</param>
    /// <returns><see langword="true"/> if the <paramref name="key"/> exist, <see langword="false"/> otherwise</returns>
    bool TryGetProperties(TKey key, out TValueContainer valueContainer);

    // --- Projection-aware overloads -------------------------------------------------------
    // Additive, non-breaking: each has a default implementation that falls back to the
    // existing full-metadata overload, so any pre-existing implementer of this interface
    // (e.g. KNetStreamsRetriever) keeps compiling and working exactly as before without any
    // change, simply ignoring `projectedProperties`. KafkaStreamsRetriever overrides these to
    // build a filtered IValueContainerMetadata and skip deserialization of properties that are
    // not part of `projectedProperties`. `projectedProperties` should be null (no projection
    // narrowing — use the full entity metadata) or the set of IProperty actually required by
    // the query's shaper.

    /// <inheritdoc cref="GetValueBuffers(IKEFCoreDatabase)"/>
    /// <param name="database">The <see cref="IKEFCoreDatabase"/> requesting the data</param>
    /// <param name="projectedProperties">The properties actually required by the query, or <see langword="null"/> for the full entity.</param>
    IEnumerable<ValueBuffer> GetValueBuffers(IKEFCoreDatabase database, IReadOnlyList<IProperty>? projectedProperties)
        => GetValueBuffers(database);

    /// <inheritdoc cref="GetValueBuffersRange(IKEFCoreDatabase, IPrincipalKeyValueFactory{TKey}, object[], object[])"/>
    /// <param name="database">The <see cref="IKEFCoreDatabase"/> requesting the data</param>
    /// <param name="keyValueFactory">The key converter</param>
    /// <param name="rangeStart">The start key</param>
    /// <param name="rangeEnd">The end key</param>
    /// <param name="projectedProperties">The properties actually required by the query, or <see langword="null"/> for the full entity.</param>
    IEnumerable<ValueBuffer> GetValueBuffersRange(IKEFCoreDatabase database, IPrincipalKeyValueFactory<TKey> keyValueFactory, object?[]? rangeStart, object?[]? rangeEnd, IReadOnlyList<IProperty>? projectedProperties)
        => GetValueBuffersRange(database, keyValueFactory, rangeStart, rangeEnd);

    /// <inheritdoc cref="GetValueBuffersReverse(IKEFCoreDatabase)"/>
    /// <param name="database">The <see cref="IKEFCoreDatabase"/> requesting the data</param>
    /// <param name="projectedProperties">The properties actually required by the query, or <see langword="null"/> for the full entity.</param>
    IEnumerable<ValueBuffer> GetValueBuffersReverse(IKEFCoreDatabase database, IReadOnlyList<IProperty>? projectedProperties)
        => GetValueBuffersReverse(database);

    /// <inheritdoc cref="GetValueBuffersReverseRange(IKEFCoreDatabase, IPrincipalKeyValueFactory{TKey}, object[], object[])"/>
    /// <param name="database">The <see cref="IKEFCoreDatabase"/> requesting the data</param>
    /// <param name="keyValueFactory">The key converter</param>
    /// <param name="rangeStart">The start key</param>
    /// <param name="rangeEnd">The end key</param>
    /// <param name="projectedProperties">The properties actually required by the query, or <see langword="null"/> for the full entity.</param>
    IEnumerable<ValueBuffer> GetValueBuffersReverseRange(IKEFCoreDatabase database, IPrincipalKeyValueFactory<TKey> keyValueFactory, object?[]? rangeStart, object?[]? rangeEnd, IReadOnlyList<IProperty>? projectedProperties)
        => GetValueBuffersReverseRange(database, keyValueFactory, rangeStart, rangeEnd);

    /// <inheritdoc cref="GetValueBuffersByPrefix(IKEFCoreDatabase, IPrincipalKeyValueFactory{TKey}, object[])"/>
    /// <param name="database">The <see cref="IKEFCoreDatabase"/> requesting the data</param>
    /// <param name="keyValueFactory">The key converter</param>
    /// <param name="prefixValues">The prefix</param>
    /// <param name="projectedProperties">The properties actually required by the query, or <see langword="null"/> for the full entity.</param>
    IEnumerable<ValueBuffer> GetValueBuffersByPrefix(IKEFCoreDatabase database, IPrincipalKeyValueFactory<TKey> keyValueFactory, object?[]? prefixValues, IReadOnlyList<IProperty>? projectedProperties)
        => GetValueBuffersByPrefix(database, keyValueFactory, prefixValues);

    /// <inheritdoc cref="TryGetValue(TKey, out ValueBuffer)"/>
    /// <param name="key">The key to retrieve</param>
    /// <param name="projectedProperties">The properties actually required by the query, or <see langword="null"/> for the full entity.</param>
    /// <param name="valueBuffer">A <see cref="ValueBuffer"/> containing the information, or <see langword="null"/> otherwise</param>
    bool TryGetValue(TKey key, IReadOnlyList<IProperty>? projectedProperties, out ValueBuffer valueBuffer)
        => TryGetValue(key, out valueBuffer);
}
