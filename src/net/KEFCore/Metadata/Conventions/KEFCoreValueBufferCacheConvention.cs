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

using MASES.EntityFrameworkCore.KNet.Metadata.Internal;

namespace MASES.EntityFrameworkCore.KNet.Metadata.Conventions;

/// <summary>
/// Resolves per-entity value buffer cache TTL configuration at model finalization time.
/// Reads <see cref="KEFCoreValueBufferCacheAttribute"/> from the entity CLR type and
/// stores forward and reverse TTLs as model annotations.
/// </summary>
public sealed class KEFCoreValueBufferCacheConvention : IModelFinalizingConvention
{
    /// <inheritdoc/>
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            if (entityType.ClrType.GetCustomAttributes(typeof(KEFCoreValueBufferCacheAttribute), inherit: false)
                                  .FirstOrDefault() is not KEFCoreValueBufferCacheAttribute attr) continue;

            var builder = (IConventionEntityTypeBuilder)entityType.Builder;

            if (attr.Ttl > TimeSpan.Zero)
                builder.HasAnnotation(KEFCoreAnnotationNames.ValueBufferCacheTtl, attr.Ttl);

            if (attr.ReverseTtl > TimeSpan.Zero)
                builder.HasAnnotation(KEFCoreAnnotationNames.ValueBufferReverseCacheTtl, attr.ReverseTtl);
        }
    }
}