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
using MASES.EntityFrameworkCore.KNet.ValueGeneration.Internal;

namespace MASES.EntityFrameworkCore.KNet.Storage.Internal;
/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public interface IKafkaCluster : IDisposable
{
    /// <summary>
    /// Execute the <see cref="IKafkaDatabase.EnsureDatabaseDeleted"/>
    /// </summary>
    bool EnsureDeleted(IUpdateAdapterFactory updateAdapterFactory, IModel designModel, IDiagnosticsLogger<DbLoggerCategory.Update> updateLogger);
    /// <summary>
    /// Execute the <see cref="IKafkaDatabase.EnsureDatabaseCreated"/>
    /// </summary>
    bool EnsureCreated(IUpdateAdapterFactory updateAdapterFactory, IModel designModel, IDiagnosticsLogger<DbLoggerCategory.Update> updateLogger);
    /// <summary>
    /// Execute the <see cref="IKafkaDatabase.EnsureDatabaseConnected"/>
    /// </summary>
    bool EnsureConnected(IModel designModel, IDiagnosticsLogger<DbLoggerCategory.Update> updateLogger);
    /// <summary>
    /// Creates a topic for <see cref="IEntityType"/> on Apache Kafka cluster
    /// </summary>
    string CreateTopicForEntity(IEntityType entityType);
    /// <summary>
    /// Retrieve the <see cref="ValueBuffer"/>
    /// </summary>
    IEnumerable<ValueBuffer> GetValueBuffers(IEntityType entityType);
    /// <summary>
    /// Gets the <see cref="KafkaIntegerValueGenerator{TValue}"/>
    /// </summary>
    KafkaIntegerValueGenerator<TProperty> GetIntegerValueGenerator<TProperty>(IProperty property);
    /// <summary>
    /// Executes a transaction
    /// </summary>
    int ExecuteTransaction(IList<IUpdateEntry> entries, IDiagnosticsLogger<DbLoggerCategory.Update> updateLogger);
    /// <summary>
    /// The Apche Kafka cluster identifier
    /// </summary>
    string ClusterId { get; }
    /// <summary>
    /// The <see cref="KafkaOptionsExtension"/>
    /// </summary>
    KafkaOptionsExtension Options { get; }
}
