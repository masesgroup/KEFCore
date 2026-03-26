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

//#define DEBUG_PERFORMANCE

#nullable disable

using Java.Util;
using Java.Util.Concurrent;
using MASES.EntityFrameworkCore.KNet.Diagnostics.Internal;
using MASES.EntityFrameworkCore.KNet.Extensions;
using MASES.EntityFrameworkCore.KNet.Infrastructure.Internal;
using MASES.EntityFrameworkCore.KNet.Serialization;
using MASES.KNet.Producer;
using Org.Apache.Kafka.Clients.Producer;
using Org.Apache.Kafka.Common.Errors;
using Org.Apache.Kafka.Tools;
using System.Collections.Concurrent;

namespace MASES.EntityFrameworkCore.KNet.Storage.Internal;
/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
/// <remarks>
/// Default initializer
/// </remarks>
public class KEFCoreCluster(KEFCoreOptionsExtension options,
    IDiagnosticsLogger<DbLoggerCategory.Infrastructure> infrastructureLogger,
    IKEFCoreTableFactory tableFactory,
    IValueGeneratorSelector valueGeneratorSelector,
    IComplexTypeConverterFactory complexTypeConverterFactory) : IKEFCoreCluster
{
    private readonly KEFCoreClusterAdmin _kefcoreAdminClient = KEFCoreClusterAdmin.Create(options.BootstrapServers);

    private readonly System.Collections.Concurrent.ConcurrentDictionary<IKEFCoreDatabase, IKEFCoreDatabase> _registeredDatabases = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, IStreamsManager> _streamsForApplications = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<IEntityType, string> _topicForEntity = new();

    private readonly ConcurrentDictionary<string, (Properties, IProducer)> _transactionalProducers = new();
    private readonly ConcurrentDictionary<string, System.Collections.Generic.List<ITransactionalEntityTypeProducer>> _producersByGroup = new();

    /// <inheritdoc/>
    public virtual void Dispose()
    {
        tableFactory?.Dispose();
    }
    /// <inheritdoc/>
    public virtual string ClusterId => _kefcoreAdminClient.ClusterId;
    /// <inheritdoc/>
    public virtual IDiagnosticsLogger<DbLoggerCategory.Infrastructure> InfrastructureLogger => infrastructureLogger;
    /// <inheritdoc/>
    public virtual IComplexTypeConverterFactory ComplexTypeConverterFactory => complexTypeConverterFactory;
    /// <inheritdoc/>
    public virtual IValueGeneratorSelector ValueGeneratorSelector => valueGeneratorSelector;
    /// <inheritdoc/>
    public virtual void Register(IKEFCoreDatabase db)
    {
        _registeredDatabases.GetOrAdd(db, (database) =>
        {
            database.InfrastructureLogger.Logger.LogDebug("Invoking Register");
            var coll = TopicsFromModel(database, out var topics);
            _kefcoreAdminClient.CheckTopics(coll, database.Options.ReadOnlyMode, database.InfrastructureLogger);

            var adapter = database.UpdateAdapterFactory.CreateStandalone();
            var entities = adapter.Model.GetEntityTypes();
            bool needsNewTables = tableFactory.NeedsNewTables(this, entities);
            if (!db.Options.UseCompactedReplicator && needsNewTables)
            {
                database.InfrastructureLogger.Logger.LogInformation("KEFCore is using Streams, needs to stop topology due to new items to be added.");
                if (_streamsForApplications.TryGetValue(db.Options.ApplicationId, out var streamsManager))
                {
                    streamsManager.StopTopology();
                }
                else database.InfrastructureLogger.Logger.LogInformation("Not found a Streams Manager for {AppId}, maybe it is the first start-up.", db.Options.ApplicationId);
            }

            var tables = entities.Select((et) => tableFactory.GetOrCreate(database, et));
            database.RegisterTables(tables);
            tableFactory.Start(database);

            return database;
        });
    }

    /// <inheritdoc/>
    public virtual void Unregister(IKEFCoreDatabase database)
    {
        if (!_registeredDatabases.TryRemove(database, out _))
        {
            InfrastructureLogger.Logger.LogError("KEFCoreCluster: failed to unregister a database never registered before.");
        }
    }

    ArrayList<Java.Lang.String> TopicsFromModel(IKEFCoreDatabase database, out System.Collections.Generic.IList<string> topics)
    {
        database.InfrastructureLogger.Logger.LogDebug("Invoking TopicsFromModel");

        var coll = new ArrayList<Java.Lang.String>();
        topics = new System.Collections.Generic.List<string>();
        foreach (var entityType in database.DesignTimeModel.Model.GetEntityTypes())
        {
            string topic = entityType.GetKEFCoreTopicName();
            if (_topicForEntity.TryRemove(entityType, out string topicName))
            {
                if (topicName != topic)
                {
                    topics.Add(topicName);
                    coll.Add(topicName);
                }
            }
            topics.Add(topic);
            coll.Add(topic);
        }

        database.InfrastructureLogger.Logger.LogDebug("Identified from model {Model} the following topics {Topics}", database.DesignTimeModel.Model, string.Join(", ", topics));

        return coll;
    }

    static void ResetStream(IKEFCoreDatabase database, System.Collections.Generic.IList<string> topics)
    {
        database.InfrastructureLogger.Logger.LogDebug("Invoking ResetStream");

        if (!database.Options.UseCompactedReplicator)
        {
            database.InfrastructureLogger.Logger.LogInformation("Requesting Streams reset for {Application} with topics {Topics}", database.Options.ApplicationId, string.Join(", ", topics));
            try
            {
                StreamsResetter.ResetApplicationForced(database.Options.BootstrapServers, database.Options.ApplicationId, topics);
            }
            catch (ExecutionException ex)
            {
                if (ex.InnerException != null) throw ex.InnerException;
                throw;
            }
        }
    }

    /// <inheritdoc/>
    public void ResetStreams(IKEFCoreDatabase database)
    {
        _ = TopicsFromModel(database, out var topics);
        ResetStream(database, topics);
    }

    /// <inheritdoc/>
    public virtual bool EnsureDeleted(IKEFCoreDatabase database, IDiagnosticsLogger<DbLoggerCategory.Update> updateLogger)
    {
        database.InfrastructureLogger.Logger.LogDebug("Invoking EnsureDeleted");
        var coll = TopicsFromModel(database, out var topics);
        ResetStream(database, topics);

        _kefcoreAdminClient.DeleteTopics(coll, database.InfrastructureLogger);

        return true;
    }
    /// <inheritdoc/>
    public virtual bool EnsureCreated(IKEFCoreDatabase database, IDiagnosticsLogger<DbLoggerCategory.Update> updateLogger)
    {
        database.InfrastructureLogger.Logger.LogDebug("Invoking EnsureCreated");

        Register(database);

        var updateAdapter = database.UpdateAdapterFactory.CreateStandalone();
        var entries = new System.Collections.Generic.List<IUpdateEntry>();
        foreach (var entityType in database.DesignTimeModel.Model.GetEntityTypes())
        {
            tableFactory.Get(this, entityType); // test existence
            IEntityType targetEntityType = null;
            foreach (var targetSeed in entityType.GetSeedData())
            {
                targetEntityType ??= updateAdapter.Model.FindEntityType(entityType.Name)!;
                var entry = updateAdapter.CreateEntry(targetSeed, targetEntityType);
                entry.EntityState = EntityState.Added;
                entries.Add(entry);
            }
        }

        ExecuteTransaction(database, entries, updateLogger);

        if (database.Options.DefaultSynchronizationTimeout != 0)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                EnsureSynchronized(database, database.Options.DefaultSynchronizationTimeout);
            }
            catch
            {
                database.InfrastructureLogger.Logger.LogError("Failed to execute synchronization within a timeout of {Timeout} for cluster id {ClusterId}", database.Options.DefaultSynchronizationTimeout, database.Options.ClusterId);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                database.InfrastructureLogger.Logger.LogInformation("Synchronization of {ApplicationId} with cluster id {ClusterId} done in {Elapsed}", database.Options.ApplicationId, database.Options.ClusterId, stopwatch.Elapsed);
            }
        }

        return true;
    }
    /// <inheritdoc/>
    public virtual bool EnsureConnected(IKEFCoreDatabase database, IDiagnosticsLogger<DbLoggerCategory.Update> updateLogger)
    {
        database.InfrastructureLogger.Logger.LogDebug("Invoking EnsureConnected");
        return true;
    }
    /// <inheritdoc/>
    public virtual bool? EnsureSynchronized(IKEFCoreDatabase database, long timeout)
    {
        database.InfrastructureLogger.Logger.LogDebug("Invoking EnsureSynchronized with {Timeout}", timeout);

        Register(database);

        var remainingTimeout = timeout;
        bool?[] bools = new bool?[database.Tables.Count];
        Stopwatch stopwatch = new();
        do
        {
            int index = 0;
            foreach (var item in database.Tables)
            {
                stopwatch.Restart();
                bools[index] = item.EnsureSynchronized(remainingTimeout);
                stopwatch.Stop();
                if (timeout != Timeout.Infinite)
                {
                    remainingTimeout -= stopwatch.ElapsedMilliseconds;
                    if (remainingTimeout < 0)
                    {
                        throw new System.TimeoutException($"Timeout of {timeout} ms expired evaluating {item.AssociatedTopicName}");
                    }
                }
                index++;
            }
            bool? result = true;
            foreach (var item in bools)
            {
                if (!item.HasValue) return null; // if one item is uncertain return uncertain
                result &= item;
            }
            if (result.HasValue && result.Value) return true;
        }
        while (timeout == Timeout.Infinite || remainingTimeout > 0);
        return false;
    }

    /// <summary>
    /// Retrieves the <see cref="IStreamsManager"/> associated to <see cref="IKEFCoreDatabase"/> in the instance of <see cref="IKEFCoreCluster"/>
    /// </summary>
    public IStreamsManager GetStreamsManager(IKEFCoreDatabase database, Func<IKEFCoreDatabase, IStreamsManager> createFunc)
    {
        return _streamsForApplications.GetOrAdd(database.Options.ApplicationId, createFunc(database));
    }

    /// <inheritdoc/>
    public IKEFCoreTable GetTable(IEntityType entityType)
    {
        return tableFactory.Get(this, entityType);
    }

    /// <inheritdoc/>
    public virtual string CreateTopicForEntity(IKEFCoreDatabase database, IEntityType entityType)
    {
        return _topicForEntity.GetOrAdd(entityType, (et) =>
        {
            database.InfrastructureLogger.Logger.LogInformation("Invoking CreateTopicForEntity for {Entity}", entityType.Name);
            var topicName = entityType.GetKEFCoreTopicName();
            var requestedPartitions = entityType.NumPartitions(database.Options);
            var requestedReplicationFactor = entityType.ReplicationFactor(database.Options);
            var topicConfig = entityType.BuildTopicConfig(database.Options);

            return CreateTopicForEntity(database, et, topicName, requestedPartitions, requestedReplicationFactor, topicConfig.ToMap(), 100, 100, 0);
        });
    }

    private string CreateTopicForEntity(IKEFCoreDatabase database, IEntityType entityType, string topicName, int requestedPartitions, short requestedReplicationFactor, Map<Java.Lang.String, Java.Lang.String> map, int waitTime, int maxCycles, int cycle)
    {
        database.InfrastructureLogger.Logger.LogDebug("Invoking CreateTopicForEntity for {Entity} attempt {Cycle}", entityType.Name, cycle);
        try
        {
            _kefcoreAdminClient.CreateTopic(topicName, requestedPartitions, requestedReplicationFactor, map, database.InfrastructureLogger);
        }
        catch (TopicExistsException ex)
        {
            if (cycle >= maxCycles) throw new System.TimeoutException($"Timeout occurred executing CreateTopicForEntity on {entityType.Name} after {cycle * waitTime}");
            if (ex.Message.Contains("deletion"))
            {
                database.InfrastructureLogger.Logger.LogInformation("Invoke again CreateTopicForEntity for {Entity} at attempt {Cycle} since server reported {Error}", entityType.Name, cycle, ex.Message);
                Thread.Sleep(waitTime); // wait a while before the server completes topic deletion and try again
                return CreateTopicForEntity(database, entityType, topicName, requestedPartitions, requestedReplicationFactor, map, waitTime, maxCycles, cycle++);
            }
        }

        return topicName;
    }

    /// <inheritdoc/>
    IDictionary<int, long> LatestOffsetForEntity(IEntityType entityType, int waitTime, int maxCycles, int cycle)
    {
        InfrastructureLogger.Logger.LogDebug("Invoking LatestOffsetForEntity {Entity} attempt {Cycle}", entityType.Name, cycle);
        System.Collections.Generic.Dictionary<int, long> dictionary = new();

        try
        {
            return _kefcoreAdminClient.LastPartitionOffsetForTopic(entityType.GetKEFCoreTopicName());
        }
        catch (UnknownTopicOrPartitionException ex)
        {
            if (cycle >= maxCycles) throw new System.TimeoutException($"Timeout occurred executing LatestOffsetForEntity on {entityType.Name} after {cycle * waitTime}");
            InfrastructureLogger.Logger.LogInformation("Invoke again LatestOffsetForEntity for {Entity} at attempt {Cycle} since server reported {Error}. This can be a normal condition on clean start-up.", entityType.Name, cycle, ex.Message);
            Thread.Sleep(waitTime); // wait a while before the server completes topic creation and try again
            return LatestOffsetForEntity(entityType, waitTime, maxCycles, cycle++);
        }
    }

    /// <inheritdoc/>
    public IDictionary<int, long> LatestOffsetForEntity(IEntityType entityType)
    {
        return LatestOffsetForEntity(entityType, 100, 10, 0);
    }

    /// <inheritdoc/>
    public virtual IEnumerable<ValueBuffer> GetValueBuffers(IKEFCoreDatabase database, IEntityType entityType)
    {
        database.InfrastructureLogger.Logger.LogDebug("Invoking GetValueBuffers for {Entity}", entityType.Name);
#if DEBUG_PERFORMANCE
        Stopwatch tableSw = new();
        Stopwatch valueBufferSw = new();
        try
        {
            tableSw.Start();
#endif
            var table = tableFactory.GetOrCreate(database, entityType);
#if DEBUG_PERFORMANCE
            valueBufferSw.Start();
#endif
            if (table != null)
            {
                return table.GetValueBuffers(database);
            }
            throw new InvalidOperationException("No table available");
#if DEBUG_PERFORMANCE
        }
        finally
        {
            valueBufferSw.Stop();
            database.InfrastructureLogger.Logger.LogInformation($"KEFCoreCluster::GetValueBuffers for {entityType.Name} - EnsureTable: {tableSw.Elapsed} ValueBuffer: {valueBufferSw.Elapsed}");
        }
#endif
    }
    /// <inheritdoc/>
    public ValueBuffer? GetValueBuffer(IKEFCoreDatabase database, IEntityType entityType, object[] keyValues)
    {
        database.InfrastructureLogger.Logger.LogDebug("Invoking GetValueBuffer for {Entity}", entityType.Name);
#if DEBUG_PERFORMANCE
        Stopwatch tableSw = new();
        Stopwatch valueBufferSw = new();
        try
        {
            tableSw.Start();
#endif
            var table = tableFactory.GetOrCreate(database, entityType);
#if DEBUG_PERFORMANCE
            valueBufferSw.Start();
#endif
            if (table != null)
            {
                return table.GetValueBuffer(database, keyValues);
            }
            throw new InvalidOperationException("No table available");
#if DEBUG_PERFORMANCE
        }
        finally
        {
            valueBufferSw.Stop();
            database.InfrastructureLogger.Logger.LogInformation($"KEFCoreCluster::GetValueBuffer for {entityType.Name} - EnsureTable: {tableSw.Elapsed} ValueBuffer: {valueBufferSw.Elapsed}");
        }
#endif
    }
    /// <inheritdoc/>
    public IEnumerable<ValueBuffer> GetValueBuffersRange(IKEFCoreDatabase database, IEntityType entityType, object[] rangeStart, object[] rangeEnd)
    {
        database.InfrastructureLogger.Logger.LogDebug("Invoking GetValueBuffersRange for {Entity}", entityType.Name);
#if DEBUG_PERFORMANCE
        Stopwatch tableSw = new();
        Stopwatch valueBufferSw = new();
        try
        {
            tableSw.Start();
#endif
            var table = tableFactory.GetOrCreate(database, entityType);
#if DEBUG_PERFORMANCE
            valueBufferSw.Start();
#endif
            if (table != null)
            {
                return table.GetValueBuffersRange(database, rangeStart, rangeEnd);
            }
            throw new InvalidOperationException("No table available");
#if DEBUG_PERFORMANCE
        }
        finally
        {
            valueBufferSw.Stop();
            database.InfrastructureLogger.Logger.LogInformation($"KEFCoreCluster::GetValueBuffersRange for {entityType.Name} - EnsureTable: {tableSw.Elapsed} ValueBuffer: {valueBufferSw.Elapsed}");
        }
#endif
    }
    /// <inheritdoc/>
    public IEnumerable<ValueBuffer> GetValueBuffersReverse(IKEFCoreDatabase database, IEntityType entityType)
    {
        database.InfrastructureLogger.Logger.LogDebug("Invoking GetValueBuffersReverse for {Entity}", entityType.Name);
#if DEBUG_PERFORMANCE
        Stopwatch tableSw = new();
        Stopwatch valueBufferSw = new();
        try
        {
            tableSw.Start();
#endif
            var table = tableFactory.GetOrCreate(database, entityType);
#if DEBUG_PERFORMANCE
            valueBufferSw.Start();
#endif
            if (table != null)
            {
                return table.GetValueBuffersReverse(database);
            }
            throw new InvalidOperationException("No table available");
#if DEBUG_PERFORMANCE
        }
        finally
        {
            valueBufferSw.Stop();
            database.InfrastructureLogger.Logger.LogInformation($"KEFCoreCluster::GetValueBuffersReverse for {entityType.Name} - EnsureTable: {tableSw.Elapsed} ValueBuffer: {valueBufferSw.Elapsed}");
        }
#endif
    }

    /// <inheritdoc/>
    public IEnumerable<ValueBuffer> GetValueBuffersReverseRange(IKEFCoreDatabase database, IEntityType entityType, object[] rangeStart, object[] rangeEnd)
    {
        database.InfrastructureLogger.Logger.LogDebug("Invoking GetValueBuffersReverseRange for {Entity}", entityType.Name);
#if DEBUG_PERFORMANCE
        Stopwatch tableSw = new();
        Stopwatch valueBufferSw = new();
        try
        {
            tableSw.Start();
#endif
            var table = tableFactory.GetOrCreate(database, entityType);
#if DEBUG_PERFORMANCE
            valueBufferSw.Start();
#endif
            if (table != null)
            {
                return table.GetValueBuffersReverseRange(database, rangeStart, rangeEnd);
            }
            throw new InvalidOperationException("No table available");
#if DEBUG_PERFORMANCE
        }
        finally
        {
            valueBufferSw.Stop();
            database.InfrastructureLogger.Logger.LogInformation($"KEFCoreCluster::GetValueBuffersReverseRange for {entityType.Name} - EnsureTable: {tableSw.Elapsed} ValueBuffer: {valueBufferSw.Elapsed}");
        }
#endif
    }

    /// <summary>
    /// Retrieve the <see cref="ValueBuffer"/> using prefix scan
    /// </summary>
    public IEnumerable<ValueBuffer> GetValueBuffersByPrefix(IKEFCoreDatabase database, IEntityType entityType, object[] prefixValues)
    {
        database.InfrastructureLogger.Logger.LogDebug("Invoking GetValueBuffersByPrefix for {Entity}", entityType.Name);
#if DEBUG_PERFORMANCE
        Stopwatch tableSw = new();
        Stopwatch valueBufferSw = new();
        try
        {
            tableSw.Start();
#endif
            var table = tableFactory.GetOrCreate(database, entityType);
#if DEBUG_PERFORMANCE
            valueBufferSw.Start();
#endif
            if (table != null)
            {
                return table.GetValueBuffersByPrefix(database, prefixValues);
            }
            throw new InvalidOperationException("No table available");
#if DEBUG_PERFORMANCE
        }
        finally
        {
            valueBufferSw.Stop();
            database.InfrastructureLogger.Logger.LogInformation($"KEFCoreCluster::GetValueBuffersByPrefix for {entityType.Name} - EnsureTable: {tableSw.Elapsed} ValueBuffer: {valueBufferSw.Elapsed}");
        }
#endif
    }

    int PrepareTransaction(IKEFCoreDatabase database, IDictionary<IKEFCoreTable, System.Collections.Generic.IList<IKEFCoreRowBag>> dataInTransaction,
                           System.Collections.Generic.IList<IUpdateEntry> entries,
                           IDiagnosticsLogger<DbLoggerCategory.Update> updateLogger,
                           out System.Collections.Generic.List<string> readOnlyViolations)
    {
        var rowsAffected = 0;
        readOnlyViolations = null;
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var entityType = entry.EntityType;

            if (entityType.GetReadOnly(database.Options))
            {
                throw new InvalidOperationException($"Entity type '{entityType.DisplayName()}' is read-only.");
            }

            Check.DebugAssert(!entityType.IsAbstract(), "entityType is abstract");

            if (entityType.GetReadOnly(database.Options))
            {
                readOnlyViolations ??= [];
                readOnlyViolations.Add(entityType.DisplayName());
                database.InfrastructureLogger.Logger?.LogWarning(
                    "Entity type '{EntityType}' is read-only — entry with state {State} skipped.",
                    entityType.DisplayName(), entry.EntityState);
                continue;
            }

            var table = tableFactory.GetOrCreate(database, entityType);

            IKEFCoreRowBag record;

            if (entry.SharedIdentityEntry != null)
            {
                if (entry.EntityState == EntityState.Deleted)
                {
                    continue;
                }

                _ = table.Delete(entry);
            }

            switch (entry.EntityState)
            {
                case EntityState.Added:
                    record = table.Create(entry);
                    break;
                case EntityState.Deleted:
                    record = table.Delete(entry);
                    break;
                case EntityState.Modified:
                    record = table.Update(entry);
                    break;
                default:
                    continue;
            }

            if (!dataInTransaction.TryGetValue(table, out System.Collections.Generic.IList<IKEFCoreRowBag> recordList))
            {
                recordList = [];
                dataInTransaction[table] = recordList;
            }
            recordList?.Add(record);

            rowsAffected++;
        }

        return rowsAffected;
    }

    IEnumerable<Task<RecordMetadata>> ExecuteTransaction(IKEFCoreDatabase database, System.Collections.Generic.IList<IUpdateEntry> entries, IDiagnosticsLogger<DbLoggerCategory.Update> updateLogger, out int rowsAffected, CancellationToken cancellationToken = default)
    {
        if (database.Options.ReadOnlyMode)
        {
            throw new InvalidOperationException($"Cannot execute any operation since the instance is in read-only mode.");
        }

        System.Collections.Generic.Dictionary<IKEFCoreTable, System.Collections.Generic.IList<IKEFCoreRowBag>> dataInTransaction = [];

        rowsAffected = PrepareTransaction(database, dataInTransaction, entries, updateLogger, out var readOnlyViolations);

        var currentTx = database.TransactionManager?.CurrentTransaction as KEFCoreTransaction;
        if (currentTx != null)
        {
            var groups = entries
                .Select(e => e.EntityType.GetTransactionGroup())
                .OfType<string>()
                .Distinct()
                .ToList();
            if (groups.Count > 0)
            {
                currentTx.Begin(groups, database.Cluster);
            }
        }

        System.Collections.Generic.List<Future<RecordMetadata>> futures = [];
        foreach (var tableData in dataInTransaction)
        {
            tableData.Key.Commit(futures, tableData.Value);
        }

        if (readOnlyViolations?.Count > 0)
        {
            updateLogger.Logger?.LogWarning("SaveChanges completed but {Count} entries were skipped because their entity types are read-only: {Types}.",
                                            readOnlyViolations.Count, string.Join(", ", readOnlyViolations));
        }

        return futures.Select(obj => Task.Run(() =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                return obj.Get();
            }
            catch (ExecutionException ex) { throw ex.InnerException; }
            catch (OperationCanceledException) { return null; }
        }, cancellationToken));
    }

    /// <inheritdoc/>
    public virtual int ExecuteTransaction(IKEFCoreDatabase database, System.Collections.Generic.IList<IUpdateEntry> entries, IDiagnosticsLogger<DbLoggerCategory.Update> updateLogger)
    {
        database.InfrastructureLogger.Logger.LogInformation("Invoking ExecuteTransaction for {number} entries", entries.Count);

        Register(database);

        using var ctSource = new CancellationTokenSource();
        var tasks = ExecuteTransaction(database, entries, updateLogger, out var rowsAffected, ctSource.Token);
        var result = Task.WhenAll(tasks);
        result.Wait();
        if (result.IsFaulted)
        {
            updateLogger.ChangesSaved(entries, rowsAffected); // check entries not saved and update rowsAffected

            throw result.Exception;
        }

        if (result.IsCompleted)
        {
            updateLogger.ChangesSaved(entries, rowsAffected);
        }

        return rowsAffected;
    }

    /// <inheritdoc/>
    public async Task<int> ExecuteTransactionAsync(IKEFCoreDatabase database, System.Collections.Generic.IList<IUpdateEntry> entries, IDiagnosticsLogger<DbLoggerCategory.Update> updateLogger, CancellationToken cancellationToken = default)
    {
        database.InfrastructureLogger.Logger.LogInformation("Invoking ExecuteTransactionAsync for {number} entries", entries.Count);

        Register(database);

        var tasks = ExecuteTransaction(database, entries, updateLogger, out var rowsAffected, cancellationToken);

        try
        {
            await Task.WhenAll(tasks);
        }
        catch
        {
            updateLogger.ChangesSaved(entries, rowsAffected); // check for unsaved entries and update rowsAffected!
        }

        return rowsAffected;
    }

    /// <inheritdoc/>
    public IProducer GetOrCreateTransactionalProducer(string transactionGroup, ITransactionalEntityTypeProducer entityTypeProducer)
    {
        _producersByGroup.GetOrAdd(transactionGroup, _ => new()).Add(entityTypeProducer);

        var tmp = _transactionalProducers.GetOrAdd(transactionGroup, group =>
        {
            var config = options.ProducerOptionsBuilder();
            config.TransactionalId = $"{options.ApplicationId}.{group}";
            config.Acks = ProducerConfigBuilder.AcksTypes.All;
            config.EnableIdempotence = true;
            config.Retries = int.MaxValue;
            config.MaxInFlightRequestsPerConnection = 1;

            string baSerdesName = Java.Lang.Class.ClassNameOf<Org.Apache.Kafka.Common.Serialization.ByteArraySerializer>();
            string bbSerdesName = Java.Lang.Class.ClassNameOf<Org.Apache.Kafka.Common.Serialization.ByteBufferSerializer>();

            config.KeySerializerClass = options.UseKeyByteBufferDataTransfer ? bbSerdesName : baSerdesName;
            config.ValueSerializerClass = options.UseValueContainerByteBufferDataTransfer ? bbSerdesName : baSerdesName;

            var jvmKeyType = options.UseKeyByteBufferDataTransfer ? typeof(Java.Nio.ByteBuffer) : typeof(byte[]);
            var jvmValueType = options.UseValueContainerByteBufferDataTransfer ? typeof(Java.Nio.ByteBuffer) : typeof(byte[]);

            var properties = config.ToProperties();
            var producerType = typeof(KafkaProducer<,>).MakeGenericType(jvmKeyType, jvmValueType);
            var producer = (IProducer)Activator.CreateInstance(producerType, properties)!;

            producer.InitTransactions();
            InfrastructureLogger.Logger?.LogInformation("Created transactional producer for group '{Group}' with transactional.id '{TransactionalId}'", group, config.TransactionalId);
            return (properties, producer);
        });
        return tmp.Item2;
    }

    /// <inheritdoc/>
    public void BeginTransactions(string transactionGroup)
    {
        if (_transactionalProducers.TryGetValue(transactionGroup, out var producer))
        {
            producer.Item2.BeginTransaction();
        }
        else throw new InvalidOperationException($"No transactional producer found for group '{transactionGroup}'.");
    }

    /// <inheritdoc/>
    public void CommitTransactions(string transactionGroup)
    {
        if (!_transactionalProducers.TryGetValue(transactionGroup, out var producer))
        {
            throw new InvalidOperationException($"No transactional producer found for group '{transactionGroup}'.");
        }

        producer.Item2.CommitTransaction();

        if (_producersByGroup.TryGetValue(transactionGroup, out var producers))
        {
            foreach (var p in producers)
            {
                p.CommitPendingOffsets();
            }
        }
    }

    /// <inheritdoc/>
    public void AbortTransactions(string transactionGroup)
    {
        if (!_transactionalProducers.TryGetValue(transactionGroup, out var producer))
        {
            throw new InvalidOperationException($"No transactional producer found for group '{transactionGroup}'.");
        }

        producer.Item2.AbortTransaction();

        if (_producersByGroup.TryGetValue(transactionGroup, out var producers))
        {
            foreach (var p in producers)
            {
                p.AbortPendingOffsets();
            }
        }
    }
}
