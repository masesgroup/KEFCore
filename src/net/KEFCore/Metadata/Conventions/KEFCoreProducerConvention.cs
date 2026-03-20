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

using MASES.EntityFrameworkCore.KNet.Infrastructure.Internal;
using MASES.EntityFrameworkCore.KNet.Metadata.Internal;

namespace MASES.EntityFrameworkCore.KNet.Metadata.Conventions;

/// <summary>
/// A convention that resolves per-entity producer configuration overrides at model finalization time,
/// storing the result as a <see cref="KEFCoreProducerAnnotation"/> on each <see cref="IEntityType"/>.
/// </summary>
/// <remarks>
/// Reads <see cref="KEFCoreProducerAttribute"/> from the entity CLR type.
/// Only properties explicitly set on the attribute are stored — omitted properties
/// inherit the global <see cref="IKEFCoreSingletonOptions.ProducerConfig"/> at producer creation time.
/// </remarks>
public class KEFCoreProducerConvention : IModelFinalizingConvention
{
    /// <inheritdoc/>
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            var attr = entityType.ClrType.GetCustomAttribute<KEFCoreProducerAttribute>();
            if (attr == null) continue;

            var ann = new KEFCoreProducerAnnotation
            {
                Acks = attr.Acks,
                LingerMs = attr.LingerMs,
                BatchSize = attr.BatchSize,
                CompressionType = attr.CompressionType,
                Retries = attr.Retries,
                MaxInFlightRequestsPerConnection = attr.MaxInFlightRequestsPerConnection,
                DeliveryTimeoutMs = attr.DeliveryTimeoutMs,
                RequestTimeoutMs = attr.RequestTimeoutMs,
                BufferMemory = attr.BufferMemory,
                MaxBlockMs = attr.MaxBlockMs,
            };

            if (ann.HasAnyValue)
                ((IConventionEntityTypeBuilder)entityType.Builder)
                    .HasAnnotation(KEFCoreAnnotationNames.ProducerConfig, ann);
        }
    }
}
