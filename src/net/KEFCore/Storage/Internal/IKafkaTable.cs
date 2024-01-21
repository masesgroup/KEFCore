/*
*  Copyright 2024 MASES s.r.l.
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

using MASES.EntityFrameworkCore.KNet.ValueGeneration.Internal;

namespace MASES.EntityFrameworkCore.KNet.Storage.Internal;
/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public interface IKafkaTable : IEntityTypeProducer
{
    /// <summary>
    /// Create snapshot
    /// </summary>
    IReadOnlyList<object?[]> SnapshotRows();
    /// <summary>
    /// Current rows
    /// </summary>
    IEnumerable<object?[]> Rows { get; }
    /// <summary>
    /// Creates a new row
    /// </summary>
    IKafkaRowBag Create(IUpdateEntry entry);
    /// <summary>
    /// Deletes a row
    /// </summary>
    IKafkaRowBag Delete(IUpdateEntry entry);
    /// <summary>
    /// Updates a row
    /// </summary>
    IKafkaRowBag Update(IUpdateEntry entry);
    /// <summary>
    /// Get an <see cref="KafkaIntegerValueGenerator{TValue}"/>
    /// </summary>
    KafkaIntegerValueGenerator<TProperty> GetIntegerValueGenerator<TProperty>(IProperty property, IReadOnlyList<IKafkaTable> tables);
    /// <summary>
    /// Bumps values
    /// </summary>
    void BumpValueGenerators(object?[] row);
    /// <summary>
    /// The referring <see cref="IKafkaCluster"/>
    /// </summary>
    IKafkaCluster Cluster { get; }
}
