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

namespace MASES.EntityFrameworkCore.KNet.Metadata;

/// <summary>
/// Configures in-memory result caching for full enumeration of this entity type's
/// state store. When active, the first complete enumeration of <c>GetValueBuffers</c>
/// or <c>GetValueBuffersReverse</c> populates an internal list; subsequent queries
/// within the TTL window are served directly from that list — zero JNI cost.
/// Partial enumerations (e.g. <c>First</c>, <c>Take</c>) never populate the cache.
/// The cache is automatically invalidated when new data arrives from the cluster
/// via <c>FreshEventChange</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class KEFCoreValueBufferCacheAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of <see cref="KEFCoreValueBufferCacheAttribute"/>.
    /// </summary>
    /// <param name="ttlSeconds">
    /// Cache TTL in seconds for forward enumeration (<c>GetValueBuffers</c>).
    /// Zero or negative disables caching for forward enumeration.
    /// </param>
    /// <param name="reverseTtlSeconds">
    /// Cache TTL in seconds for reverse enumeration (<c>GetValueBuffersReverse</c>).
    /// Zero or negative disables caching for reverse enumeration.
    /// Defaults to the same value as <paramref name="ttlSeconds"/>.
    /// </param>
    public KEFCoreValueBufferCacheAttribute(double ttlSeconds, double reverseTtlSeconds = -1)
    {
        Ttl = ttlSeconds > 0 ? TimeSpan.FromSeconds(ttlSeconds) : TimeSpan.Zero;
        ReverseTtl = reverseTtlSeconds >= 0
            ? TimeSpan.FromSeconds(reverseTtlSeconds)
            : Ttl; // defaults to same as forward
    }

    /// <summary>Cache TTL for forward enumeration. <see cref="TimeSpan.Zero"/> disables caching.</summary>
    public TimeSpan Ttl { get; }
    /// <summary>Cache TTL for reverse enumeration. <see cref="TimeSpan.Zero"/> disables caching.</summary>
    public TimeSpan ReverseTtl { get; }
}
