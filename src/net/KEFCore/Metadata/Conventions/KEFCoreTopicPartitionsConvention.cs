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
/// A convention that resolves per-entity topic partition and replication factor configuration
/// at model finalization time, storing the results as model annotations on each <see cref="IEntityType"/>.
/// </summary>
/// <remarks>
/// Reads <see cref="KEFCoreTopicPartitionsAttribute"/> and <see cref="KEFCoreTopicReplicationFactorAttribute"/>
/// from the entity CLR type. If neither is present, the global defaults from
/// <see cref="IKEFCoreSingletonOptions.DefaultNumPartitions"/> and
/// <see cref="IKEFCoreSingletonOptions.DefaultReplicationFactor"/> apply at topic creation time.
/// </remarks>
public class KEFCoreTopicPartitionsConvention : IModelFinalizingConvention
{
    /// <inheritdoc/>
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            var clrType = entityType.ClrType;

            var partitionsAttr = clrType.GetCustomAttribute<KEFCoreTopicPartitionsAttribute>();
            if (partitionsAttr != null)
                ((IConventionEntityTypeBuilder)entityType.Builder)
                    .HasAnnotation(KEFCoreAnnotationNames.NumPartitions, partitionsAttr.NumPartitions);

            var replicationAttr = clrType.GetCustomAttribute<KEFCoreTopicReplicationFactorAttribute>();
            if (replicationAttr != null)
                ((IConventionEntityTypeBuilder)entityType.Builder)
                    .HasAnnotation(KEFCoreAnnotationNames.ReplicationFactor, replicationAttr.ReplicationFactor);
        }
    }
}
