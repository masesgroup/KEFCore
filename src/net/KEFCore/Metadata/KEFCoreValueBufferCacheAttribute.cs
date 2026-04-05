/*
 * Copyright (c) 2022-2026 MASES s.r.l.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *  http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
 * Refer to LICENSE for more information.
 */

#nullable enable

namespace MASES.EntityFrameworkCore.KNet.Metadata;

/// <summary>
/// Configures an in-memory result cache for this entity type's Kafka Streams state store.
/// <para>
/// Two independent caches are maintained: one for forward enumeration
/// (<c>GetValueBuffers</c>, <c>GetValueBuffersRange</c>, single key lookup)
/// and one for reverse enumeration (<c>GetValueBuffersReverse</c>,
/// <c>GetValueBuffersReverseRange</c>). Each is backed by its own
/// <see cref="SortedList{TKey,TValue}"/> and populated independently on the first
/// complete enumeration in the respective direction.
/// </para>
/// <para>
/// Partial enumerations (e.g. <c>First()</c>, <c>Take(n)</c>) never populate the cache.
/// Both caches are invalidated automatically when new data arrives from the cluster via
/// <c>FreshEventChange</c>.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class KEFCoreValueBufferCacheAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of <see cref="KEFCoreValueBufferCacheAttribute"/>.
    /// </summary>
    /// <param name="ttlSeconds">
    /// Cache TTL in seconds for forward enumeration (<c>GetValueBuffers</c>, range, single key).
    /// Zero or negative disables the forward cache — the proxy acts as a transparent
    /// pass-through.
    /// </param>
    /// <param name="reverseTtlSeconds">
    /// Cache TTL in seconds for reverse enumeration (<c>GetValueBuffersReverse</c>,
    /// reverse range). Zero or negative disables the reverse cache.
    /// Defaults to <c>-1</c> which means: use the same value as
    /// <paramref name="ttlSeconds"/>.
    /// </param>
    public KEFCoreValueBufferCacheAttribute(double ttlSeconds, double reverseTtlSeconds = -1)
    {
        Ttl = ttlSeconds > 0 ? TimeSpan.FromSeconds(ttlSeconds) : TimeSpan.Zero;
        ReverseTtl = reverseTtlSeconds >= 0
            ? TimeSpan.FromSeconds(reverseTtlSeconds)
            : Ttl;
    }

    /// <summary>TTL for forward cache. <see cref="TimeSpan.Zero"/> disables it.</summary>
    public TimeSpan Ttl { get; }

    /// <summary>TTL for reverse cache. <see cref="TimeSpan.Zero"/> disables it.</summary>
    public TimeSpan ReverseTtl { get; }
}