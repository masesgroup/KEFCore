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
/// Overrides the number of partitions for the Kafka topic associated with this entity type.
/// Takes precedence over <see cref="KEFCoreDbContext.DefaultNumPartitions"/>.
/// Applied at topic creation time (<see cref="Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade.EnsureCreated"/>).
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class KEFCoreTopicPartitionsAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of <see cref="KEFCoreTopicPartitionsAttribute"/>.
    /// </summary>
    /// <param name="numPartitions">The number of partitions for the topic. Must be greater than zero.</param>
    public KEFCoreTopicPartitionsAttribute(int numPartitions)
    {
        if (numPartitions <= 0) throw new ArgumentOutOfRangeException(nameof(numPartitions), "Must be greater than zero.");
        NumPartitions = numPartitions;
    }

    /// <summary>
    /// The number of partitions for the Kafka topic associated with this entity type.
    /// </summary>
    public int NumPartitions { get; }
}
