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

using MASES.EntityFrameworkCore.KNet.Serialization;
using Org.Apache.Kafka.Clients.Producer;

namespace MASES.EntityFrameworkCore.KNet.Storage.Internal;
/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public interface IKEFCoreCluster : IDisposable
{
    /// <summary>
    /// The Apche Kafka cluster identifier
    /// </summary>
    string ClusterId { get; }
    /// <summary>
    /// Reference to <see cref="IDiagnosticsLogger{TLoggerCategory}"/> received
    /// </summary>
    IDiagnosticsLogger<DbLoggerCategory.Infrastructure> InfrastructureLogger { get; }
    /// <summary>
    /// The global <see cref="IComplexTypeConverterFactory"/>
    /// </summary>
    IComplexTypeConverterFactory ComplexTypeConverterFactory { get; }
    /// <summary>
    /// The global <see cref="IValueGeneratorSelector"/>
    /// </summary>
    IValueGeneratorSelector ValueGeneratorSelector { get; }
    /// <summary>
    /// Register an instance of <see cref="IKEFCoreDatabase"/> in an instance of <see cref="IKEFCoreCluster"/>
    /// </summary>
    /// <param name="database">The instance of <see cref="IKEFCoreDatabase"/> to be registered</param>
    void Register(IKEFCoreDatabase database);
    /// <summary>
    /// Unregister a previously registered instance of <see cref="IKEFCoreDatabase"/> from an instance of <see cref="IKEFCoreCluster"/>
    /// </summary>
    /// <param name="database">The instance of <see cref="IKEFCoreDatabase"/> to be unregistered</param>
    void Unregister(IKEFCoreDatabase database);
    /// <summary>
    /// Resets the Apache Kafka streams application associated to this <see cref="IKEFCoreCluster"/> instance
    /// </summary>
    void ResetStreams(IKEFCoreDatabase database);
    /// <summary>
    /// Execute the <see cref="IKEFCoreDatabase.EnsureDatabaseDeleted"/>
    /// </summary>
    bool EnsureDeleted(IKEFCoreDatabase database, IDiagnosticsLogger<DbLoggerCategory.Update> updateLogger);
    /// <summary>
    /// Execute the <see cref="IKEFCoreDatabase.EnsureDatabaseCreated"/>
    /// </summary>
    bool EnsureCreated(IKEFCoreDatabase database, IDiagnosticsLogger<DbLoggerCategory.Update> updateLogger);
    /// <summary>
    /// Execute the <see cref="IKEFCoreDatabase.EnsureDatabaseConnected"/>
    /// </summary>
    bool EnsureConnected(IKEFCoreDatabase database, IDiagnosticsLogger<DbLoggerCategory.Update> updateLogger);
    /// <summary>
    /// Verify if local instance is synchronized with the <see cref="IKEFCoreCluster"/> instance
    /// </summary>
    bool? EnsureSynchronized(IKEFCoreDatabase database, long timeout);
    /// <summary>
    /// Retrieves the <see cref="IStreamsManager"/> associated to <see cref="IKEFCoreDatabase"/> in the instance of <see cref="IKEFCoreCluster"/>
    /// </summary>
    IStreamsManager GetStreamsManager(IKEFCoreDatabase database, Func<IKEFCoreDatabase, IStreamsManager> createFunc);
    /// <summary>
    /// Retrieves the <see cref="IKEFCoreTable"/> associated to <see cref="IEntityType"/> in the instance of <see cref="IKEFCoreCluster"/>
    /// </summary>
    IKEFCoreTable GetTable(IEntityType entityType);
    /// <summary>
    /// Creates a topic for <see cref="IEntityType"/> on Apache Kafka cluster
    /// </summary>
    string CreateTopicForEntity(IKEFCoreDatabase database, IEntityType entityType);
    /// <summary>
    /// Returns the latest offset for each partition associated to <paramref name="entityType"/>
    /// </summary>
    /// <param name="entityType">The <see cref="IEntityType"/> to check</param>
    /// <returns>A <see cref="IDictionary{TKey, TValue}"/> containing the values</returns>
    IDictionary<int, long> LatestOffsetForEntity(IEntityType entityType);
    /// <summary>
    /// Retrieve the <see cref="ValueBuffer"/>
    /// </summary>
    IEnumerable<ValueBuffer> GetValueBuffers(IKEFCoreDatabase database, IEntityType entityType);
    /// <summary>
    /// Retrieve the <see cref="ValueBuffer"/> for a specified key
    /// </summary>
    ValueBuffer? GetValueBuffer(IKEFCoreDatabase database, IEntityType entityType, object?[] keyValues);
    /// <summary>
    /// Retrieve the <see cref="ValueBuffer"/> in a range of keys
    /// </summary>
    IEnumerable<ValueBuffer> GetValueBuffersRange(IKEFCoreDatabase database, IEntityType entityType, object?[]? rangeStart, object?[]? rangeEnd);
    /// <summary>
    /// Retrieve the <see cref="ValueBuffer"/> in reverse order
    /// </summary>
    IEnumerable<ValueBuffer> GetValueBuffersReverse(IKEFCoreDatabase database, IEntityType entityType);
    /// <summary>
    /// Retrieve the <see cref="ValueBuffer"/> in a range of keys
    /// </summary>
    IEnumerable<ValueBuffer> GetValueBuffersReverseRange(IKEFCoreDatabase database, IEntityType entityType, object?[]? rangeStart, object?[]? rangeEnd);
    /// <summary>
    /// Retrieve the <see cref="ValueBuffer"/> using prefix scan
    /// </summary>
    IEnumerable<ValueBuffer> GetValueBuffersByPrefix(IKEFCoreDatabase database, IEntityType entityType, object?[] prefixValues);
    /// <summary>
    /// Executes a transaction
    /// </summary>
    int ExecuteTransaction(IKEFCoreDatabase database, IList<IUpdateEntry> entries, IDiagnosticsLogger<DbLoggerCategory.Update> updateLogger);
    /// <summary>
    /// Executes a transaction in async
    /// </summary>
    Task<int> ExecuteTransactionAsync(IKEFCoreDatabase database, IList<IUpdateEntry> entries, IDiagnosticsLogger<DbLoggerCategory.Update> updateLogger, CancellationToken cancellationToken = default);
    /// <summary>
    /// Returns or creates the transactional <see cref="Org.Apache.Kafka.Clients.Producer.KafkaProducer{K,V}"/>
    /// for the given <paramref name="transactionGroup"/>, calling <c>InitTransactions()</c> on first creation.
    /// Registers <paramref name="entityTypeProducer"/> in the group in the same call.
    /// </summary>
    /// <param name="transactionGroup">The transaction group name.</param>
    /// <param name="entityTypeProducer">The <see cref="ITransactionalEntityTypeProducer"/> joining the group.</param>
    /// <returns>The shared transactional producer for the group.</returns>
    IProducer GetOrCreateTransactionalProducer(string transactionGroup, ITransactionalEntityTypeProducer entityTypeProducer);

    /// <summary>
    /// Calls <c>BeginTransaction()</c> on the transactional producer for <paramref name="transactionGroup"/>.
    /// </summary>
    void BeginTransactions(string transactionGroup);

    /// <summary>
    /// Calls <c>CommitTransaction()</c> on the transactional producer for <paramref name="transactionGroup"/>,
    /// then calls <c>CommitPendingOffsets()</c> on all <see cref="IEntityTypeProducer"/> registered in the group.
    /// </summary>
    void CommitTransactions(string transactionGroup);

    /// <summary>
    /// Calls <c>AbortTransaction()</c> on the transactional producer for <paramref name="transactionGroup"/>,
    /// then calls <c>AbortPendingOffsets()</c> on all <see cref="IEntityTypeProducer"/> registered in the group.
    /// </summary>
    void AbortTransactions(string transactionGroup);
}
