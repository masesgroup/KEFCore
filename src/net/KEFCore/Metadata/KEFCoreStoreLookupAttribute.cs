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

using MASES.EntityFrameworkCore.KNet.Infrastructure;

namespace MASES.EntityFrameworkCore.KNet.Metadata;

/// <summary>
/// Configures the Kafka Streams store query optimization flags for this entity type,
/// overriding the context-level defaults set via <see cref="KEFCoreDbContext"/> options.
/// </summary>
/// <remarks>
/// Each flag enables a specific query optimization path in the Streams state store.
/// Only set the flags relevant to the query patterns used for this entity —
/// unused optimizations add overhead to query planning without benefit.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class KEFCoreStoreLookupAttribute : Attribute
{
    /// <summary>
    /// Enables prefix scan optimization for queries using <c>StartsWith</c>-style predicates.
    /// Default is <see langword="false"/>.
    /// </summary>
    public bool UseStorePrefixScan { get; set; } = false;

    /// <summary>
    /// Enables single key look-up optimization for equality predicates on the primary key.
    /// Default is <see langword="true"/>.
    /// </summary>
    public bool UseStoreSingleKeyLookup { get; set; } = true;

    /// <summary>
    /// Enables key range look-up optimization for range predicates on the primary key.
    /// Default is <see langword="true"/>.
    /// </summary>
    public bool UseStoreKeyRange { get; set; } = true;

    /// <summary>
    /// Enables reverse iteration optimization for <c>OrderByDescending</c> queries.
    /// Default is <see langword="true"/>.
    /// </summary>
    public bool UseStoreReverse { get; set; } = true;

    /// <summary>
    /// Enables reverse key range look-up optimization for descending range queries.
    /// Default is <see langword="true"/>.
    /// </summary>
    public bool UseStoreReverseKeyRange { get; set; } = true;
}