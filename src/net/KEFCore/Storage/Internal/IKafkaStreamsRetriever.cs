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

namespace MASES.EntityFrameworkCore.KNet.Storage.Internal;
/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public interface IKafkaStreamsRetriever<TKey> : IDisposable where TKey : notnull
{
    /// <summary>
    /// Retrieve an <see cref="IEnumerable{ValueBuffer}"/> from the <see cref="IKafkaStreamsRetriever{TKey}"/> instance
    /// </summary>
    /// <returns>An <see cref="IEnumerable{ValueBuffer}"/></returns>
    IEnumerable<ValueBuffer> GetValueBuffers();
    /// <summary>
    /// Che if a <paramref name="key"/> exist
    /// </summary>
    /// <param name="key">The key to check for existance</param>
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
    /// <param name="properties">A <see cref="IDictionary{TKey, TValue}"/> containing the property name and associated value, or <see langword="null"/> otherwise</param>
    /// <returns><see langword="true"/> if the <paramref name="key"/> exist, <see langword="false"/> otherwise</returns>
    bool TryGetProperties(TKey key, out IDictionary<string, object?> properties);
}
