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
using MASES.EntityFrameworkCore.KNet.Infrastructure;
using MASES.EntityFrameworkCore.KNet.Infrastructure.Internal;
using MASES.EntityFrameworkCore.KNet.Serialization;
using Org.Apache.Kafka.Clients.Producer;
using Org.Apache.Kafka.Common.Errors;
using Org.Apache.Kafka.Tools;

namespace MASES.EntityFrameworkCore.KNet.Storage.Internal;
/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public class KEFCoreCluster : IKEFCoreCluster
{
    private readonly KEFCoreOptionsExtension _options;
    private readonly IDiagnosticsLogger<DbLoggerCategory.Infrastructure> _infrastructureLogger;
    private readonly IKEFCoreTableFactory _tableFactory;
    private readonly IComplexTypeConverterFactory _complexTypeConverterFactory;
    private readonly IValueGeneratorSelector _valueGeneratorSelector;
    private readonly IUpdateAdapterFactory _updateAdapterFactory;
    private readonly IModel _designModel;
    private readonly KEFCoreClusterAdmin _kefcoreAdminClient = null;

    private System.Collections.Concurrent.ConcurrentDictionary<string, IKEFCoreTable> _tables;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<IEntityType, string> _topicForEntity = new();
    /// <summary>
    /// Dfault initializer
    /// </summary>
    public KEFCoreCluster(KEFCoreOptionsExtension options,
        IDiagnosticsLogger<DbLoggerCategory.Infrastructure> infrastructureLogger,
        IKEFCoreTableFactory tableFactory,
        IComplexTypeConverterFactory complexTypeConverterFactory,
        IValueGeneratorSelector valueGeneratorSelector,
        IUpdateAdapterFactory updateAdapterFactory,
        IModel designModel)
    {
        _options = options;
        _infrastructureLogger = infrastructureLogger;
        _tableFactory = tableFactory;
        _complexTypeConverterFactory = complexTypeConverterFactory;
        _valueGeneratorSelector = valueGeneratorSelector;
        _updateAdapterFactory = updateAdapterFactory;
        _designModel = designModel;

        _kefcoreAdminClient = KEFCoreClusterAdmin.Create(Options.BootstrapServers);
    }
    /// <inheritdoc/>
    public virtual void Dispose()
    {
        _infrastructureLogger.Logger.LogDebug("Disposing KafkaCluster");
        if (_tables != null)
        {
            foreach (var item in _tables.Values)
            {
                _tableFactory.Dispose(item);
            }
        }
        _tables?.Clear();
    }
    /// <inheritdoc/>
    public virtual string ClusterId => _options.ClusterId;
    /// <inheritdoc/>
    public virtual IDiagnosticsLogger<DbLoggerCategory.Infrastructure> InfrastructureLogger => _infrastructureLogger;
    /// <inheritdoc/>
    public virtual KEFCoreOptionsExtension Options => _options;
    /// <inheritdoc/>
    public IValueGeneratorSelector ValueGeneratorSelector => _valueGeneratorSelector;
    /// <inheritdoc/>
    public virtual IModel Model => _designModel;
    /// <inheritdoc/>
    public virtual IUpdateAdapterFactory UpdateAdapterFactory => _updateAdapterFactory;
    /// <inheritdoc/>
    public virtual IComplexTypeConverterFactory ComplexTypeConverterFactory => _complexTypeConverterFactory;

    ArrayList<Java.Lang.String> TopicsFromModel(out System.Collections.Generic.IList<string> topics)
    {
        _infrastructureLogger.Logger.LogDebug("Invoking TopicsFromModel");

        var coll = new ArrayList<Java.Lang.String>();
        topics = new System.Collections.Generic.List<string>();
        foreach (var entityType in _designModel.GetEntityTypes())
        {
            string topic = entityType.TopicName(Options);
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

        _infrastructureLogger.Logger.LogDebug("Identified from model {Model} the following topics {Topics}", _designModel, string.Join(", ", topics));

        return coll;
    }

    void ResetStream(System.Collections.Generic.IList<string> topics)
    {
        _infrastructureLogger.Logger.LogDebug("Invoking ResetStream");

        if (!Options.UseCompactedReplicator)
        {
            _infrastructureLogger.Logger.LogInformation("Requesting Streams reset for {Application} with topics {Topics}", Options.ApplicationId, string.Join(", ", topics));
            try
            {
                StreamsResetter.ResetApplicationForced(Options.BootstrapServers, Options.ApplicationId, topics);
            }
            catch (ExecutionException ex)
            {
                if (ex.InnerException != null) throw ex.InnerException;
                throw;
            }
        }
    }

    /// <inheritdoc/>
    public void ResetStreams()
    {
        _ = TopicsFromModel(out var topics);
        ResetStream(topics);
    }

    /// <inheritdoc/>
    public virtual bool EnsureDeleted(IDiagnosticsLogger<DbLoggerCategory.Update> updateLogger)
    {
        _infrastructureLogger.Logger.LogDebug("Invoking EnsureDeleted");
        var coll = TopicsFromModel(out var topics);
        ResetStream(topics);

        _kefcoreAdminClient.DeleteTopics(coll, _infrastructureLogger);

        if (_tables == null)
        {
            return false;
        }

        _tables = null;

        return true;
    }
    /// <inheritdoc/>
    public virtual bool EnsureCreated(IDiagnosticsLogger<DbLoggerCategory.Update> updateLogger)
    {
        _infrastructureLogger.Logger.LogDebug("Invoking EnsureCreated");
        var coll = TopicsFromModel(out var topics);
        if (!Options.UsePersistentStorage)
        {
            ResetStream(topics);
        }

        _kefcoreAdminClient.CheckTopics(coll, Options.ReadOnlyMode, Options.ManageEvents, Options.UseCompactedReplicator, _infrastructureLogger);

        var valuesSeeded = _tables == null;
        if (valuesSeeded)
        {
            System.Collections.Generic.List<IKEFCoreTable> tables = new();
            _tables = new System.Collections.Concurrent.ConcurrentDictionary<string, IKEFCoreTable>();

            var updateAdapter = _updateAdapterFactory.CreateStandalone();
            var entries = new System.Collections.Generic.List<IUpdateEntry>();
            foreach (var entityType in _designModel.GetEntityTypes())
            {
                tables.Add(EnsureTable(entityType));

                IEntityType targetEntityType = null;
                foreach (var targetSeed in entityType.GetSeedData())
                {
                    targetEntityType ??= updateAdapter.Model.FindEntityType(entityType.Name)!;
                    var entry = updateAdapter.CreateEntry(targetSeed, targetEntityType);
                    entry.EntityState = EntityState.Added;
                    entries.Add(entry);
                }
            }

            _tableFactory.Start(tables);

            if (Options.DefaultSynchronizationTimeout != 0)
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                try
                {
                    EnsureSynchronized(Options.DefaultSynchronizationTimeout);
                }
                catch
                {
                    _infrastructureLogger.Logger.LogError("Failed to execute synchronization within a timeout of {Timeout} for cluster id {ClusterId}", Options.DefaultSynchronizationTimeout, Options.ClusterId);
                    throw;
                }
                finally
                {
                    stopwatch.Stop();
                    _infrastructureLogger.Logger.LogInformation("Synchronization of {ApplicationId} with cluster id {ClusterId} done in {Elapsed}", Options.ApplicationId, Options.ClusterId, stopwatch.Elapsed);
                }
            }

            ExecuteTransaction(entries, updateLogger);
        }

        return valuesSeeded;
    }
    /// <inheritdoc/>
    public virtual bool EnsureConnected(IDiagnosticsLogger<DbLoggerCategory.Update> updateLogger)
    {
        _infrastructureLogger.Logger.LogDebug("Invoking EnsureConnected");
        return true;
    }
    /// <inheritdoc/>
    public virtual bool? EnsureSynchronized(long timeout)
    {
        _infrastructureLogger.Logger.LogDebug("Invoking EnsureSynchronized with {Timeout}", timeout);
        var remainingTimeout = timeout;
        bool?[] bools = new bool?[_tables.Count];
        Stopwatch stopwatch = new();
        do
        {
            int index = 0;
            foreach (var item in _tables)
            {
                stopwatch.Restart();
                bools[index] = item.Value.EnsureSynchronized(remainingTimeout);
                stopwatch.Stop();
                if (timeout != Timeout.Infinite)
                {
                    remainingTimeout -= stopwatch.ElapsedMilliseconds;
                    if (remainingTimeout < 0)
                    {
                        throw new System.TimeoutException($"Timeout of {timeout} ms expired evaluating {item.Key}");
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

    /// <inheritdoc/>
    public IKEFCoreTable GetTable(IEntityType entityType)
    {
        return _tableFactory.Get(this, entityType.TopicName(Options));
    }

    /// <inheritdoc/>
    public virtual string CreateTopicForEntity(IEntityType entityType)
    {
        return _topicForEntity.GetOrAdd(entityType, (et) =>
        {
            _infrastructureLogger.Logger.LogInformation("Invoking CreateTopicForEntity for {Entity}", entityType.Name);
            var topicName = entityType.TopicName(Options);
            var requestedPartitions = entityType.NumPartitions(Options);
            var requestedReplicationFactor = entityType.ReplicationFactor(Options);

            if (Options.ManageEvents
                && !Options.UseCompactedReplicator
                && requestedPartitions > 1)
            {
                throw new InvalidOperationException($"{nameof(KEFCoreDbContext.ManageEvents)} supports a number of partition higher than 1 only with {nameof(KEFCoreDbContext.UseCompactedReplicator)}=true, in all other cases events are supported only using a single partition.");
            }

            Options.TopicConfig.CleanupPolicy = Options.UseDeletePolicyForTopic
                        ? MASES.KNet.Common.TopicConfigBuilder.CleanupPolicyTypes.Compact | MASES.KNet.Common.TopicConfigBuilder.CleanupPolicyTypes.Delete
                        : MASES.KNet.Common.TopicConfigBuilder.CleanupPolicyTypes.Compact;

            return CreateTopicForEntity(et, topicName, requestedPartitions, requestedReplicationFactor, Options.TopicConfig.ToMap(), 100, 100, 0);
        });
    }

    private string CreateTopicForEntity(IEntityType entityType, string topicName, int requestedPartitions, short requestedReplicationFactor, Map<Java.Lang.String, Java.Lang.String> map, int waitTime, int maxCycles, int cycle)
    {
        _infrastructureLogger.Logger.LogDebug("Invoking CreateTopicForEntity for {Entity} attempt {Cycle}", entityType.Name, cycle);
        try
        {
            _kefcoreAdminClient.CreateTopic(topicName, requestedPartitions, requestedReplicationFactor, map, _infrastructureLogger);
        }
        catch (TopicExistsException ex)
        {
            if (cycle >= maxCycles) throw new System.TimeoutException($"Timeout occurred executing CreateTopicForEntity on {entityType.Name} after {cycle * waitTime}");
            if (ex.Message.Contains("deletion"))
            {
                _infrastructureLogger.Logger.LogInformation("Invoke again CreateTopicForEntity for {Entity} at attempt {Cycle} since server reported {Error}", entityType.Name, cycle, ex.Message);
                Thread.Sleep(waitTime); // wait a while before the server completes topic deletion and try again
                return CreateTopicForEntity(entityType, topicName, requestedPartitions, requestedReplicationFactor, map, waitTime, maxCycles, cycle++);
            }
        }

        return topicName;
    }

    /// <inheritdoc/>
    IDictionary<int, long> LatestOffsetForEntity(IEntityType entityType, int waitTime, int maxCycles, int cycle)
    {
        _infrastructureLogger.Logger.LogDebug("Invoking LatestOffsetForEntity {Entity} attempt {Cycle}", entityType.Name, cycle);
        System.Collections.Generic.Dictionary<int, long> dictionary = new();

        try
        {
            return _kefcoreAdminClient.LastPartitionOffsetForTopic(entityType.TopicName(Options));
        }
        catch (UnknownTopicOrPartitionException ex)
        {
            if (cycle >= maxCycles) throw new System.TimeoutException($"Timeout occurred executing LatestOffsetForEntity on {entityType.Name} after {cycle * waitTime}");
            _infrastructureLogger.Logger.LogInformation("Invoke again LatestOffsetForEntity for {Entity} at attempt {Cycle} since server reported {Error}. This can be a normal condition on clean start-up.", entityType.Name, cycle, ex.Message);
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
    public virtual IEnumerable<ValueBuffer> GetValueBuffers(IEntityType entityType)
    {
        _infrastructureLogger.Logger.LogDebug("Invoking GetValueBuffers for {Entity}", entityType.Name);
#if DEBUG_PERFORMANCE
        Stopwatch tableSw = new();
        Stopwatch valueBufferSw = new();
        try
        {
            tableSw.Start();
#endif
            EnsureTable(entityType);
#if DEBUG_PERFORMANCE
            valueBufferSw.Start();
#endif
            var key = entityType.TopicName(Options);
            if (_tables != null && _tables.TryGetValue(key, out var table))
            {
                return table.GetValueBuffers();
            }
            throw new InvalidOperationException("No table available");
#if DEBUG_PERFORMANCE
        }
        finally
        {
            valueBufferSw.Stop();
            _infrastructureLogger.Logger.LogInformation($"KafkaCluster::GetValueBuffers for {entityType.Name} - EnsureTable: {tableSw.Elapsed} ValueBuffer: {valueBufferSw.Elapsed}");
        }
#endif
    }
    /// <inheritdoc/>
    public ValueBuffer? GetValueBuffer(IEntityType entityType, object[] keyValues)
    {
        _infrastructureLogger.Logger.LogDebug("Invoking GetValueBuffer for {Entity}", entityType.Name);
#if DEBUG_PERFORMANCE
        Stopwatch tableSw = new();
        Stopwatch valueBufferSw = new();
        try
        {
            tableSw.Start();
#endif
            EnsureTable(entityType);
#if DEBUG_PERFORMANCE
            valueBufferSw.Start();
#endif
            var key = entityType.TopicName(Options);
            if (_tables != null && _tables.TryGetValue(key, out var table))
            {
                return table.GetValueBuffer(keyValues);
            }
            throw new InvalidOperationException("No table available");
#if DEBUG_PERFORMANCE
        }
        finally
        {
            valueBufferSw.Stop();
            _infrastructureLogger.Logger.LogInformation($"KafkaCluster::GetValueBuffer for {entityType.Name} - EnsureTable: {tableSw.Elapsed} ValueBuffer: {valueBufferSw.Elapsed}");
        }
#endif
    }
    /// <inheritdoc/>
    public IEnumerable<ValueBuffer> GetValueBuffersRange(IEntityType entityType, object[] rangeStart, object[] rangeEnd)
    {
        _infrastructureLogger.Logger.LogDebug("Invoking GetValueBuffersRange for {Entity}", entityType.Name);
#if DEBUG_PERFORMANCE
        Stopwatch tableSw = new();
        Stopwatch valueBufferSw = new();
        try
        {
            tableSw.Start();
#endif
            EnsureTable(entityType);
#if DEBUG_PERFORMANCE
            valueBufferSw.Start();
#endif
            var key = entityType.TopicName(Options);
            if (_tables != null && _tables.TryGetValue(key, out var table))
            {
                return table.GetValueBuffersRange(rangeStart, rangeEnd);
            }
            throw new InvalidOperationException("No table available");
#if DEBUG_PERFORMANCE
        }
        finally
        {
            valueBufferSw.Stop();
            _infrastructureLogger.Logger.LogInformation($"KafkaCluster::GetValueBuffersRange for {entityType.Name} - EnsureTable: {tableSw.Elapsed} ValueBuffer: {valueBufferSw.Elapsed}");
        }
#endif
    }
    /// <inheritdoc/>
    public IEnumerable<ValueBuffer> GetValueBuffersReverse(IEntityType entityType)
    {
        _infrastructureLogger.Logger.LogDebug("Invoking GetValueBuffersReverse for {Entity}", entityType.Name);
#if DEBUG_PERFORMANCE
        Stopwatch tableSw = new();
        Stopwatch valueBufferSw = new();
        try
        {
            tableSw.Start();
#endif
            EnsureTable(entityType);
#if DEBUG_PERFORMANCE
            valueBufferSw.Start();
#endif
            var key = entityType.TopicName(Options);
            if (_tables != null && _tables.TryGetValue(key, out var table))
            {
                return table.GetValueBuffersReverse();
            }
            throw new InvalidOperationException("No table available");
#if DEBUG_PERFORMANCE
        }
        finally
        {
            valueBufferSw.Stop();
            _infrastructureLogger.Logger.LogInformation($"KafkaCluster::GetValueBuffersReverse for {entityType.Name} - EnsureTable: {tableSw.Elapsed} ValueBuffer: {valueBufferSw.Elapsed}");
        }
#endif
    }

    /// <inheritdoc/>
    public IEnumerable<ValueBuffer> GetValueBuffersReverseRange(IEntityType entityType, object[] rangeStart, object[] rangeEnd)
    {
        _infrastructureLogger.Logger.LogDebug("Invoking GetValueBuffersReverseRange for {Entity}", entityType.Name);
#if DEBUG_PERFORMANCE
        Stopwatch tableSw = new();
        Stopwatch valueBufferSw = new();
        try
        {
            tableSw.Start();
#endif
            EnsureTable(entityType);
#if DEBUG_PERFORMANCE
            valueBufferSw.Start();
#endif
            var key = entityType.TopicName(Options);
            if (_tables != null && _tables.TryGetValue(key, out var table))
            {
                return table.GetValueBuffersReverseRange(rangeStart, rangeEnd);
            }
            throw new InvalidOperationException("No table available");
#if DEBUG_PERFORMANCE
        }
        finally
        {
            valueBufferSw.Stop();
            _infrastructureLogger.Logger.LogInformation($"KafkaCluster::GetValueBuffersReverseRange for {entityType.Name} - EnsureTable: {tableSw.Elapsed} ValueBuffer: {valueBufferSw.Elapsed}");
        }
#endif
    }

    int PrepareTransaction(IDictionary<IKEFCoreTable, System.Collections.Generic.IList<IKEFCoreRowBag>> dataInTransaction,
                           System.Collections.Generic.IList<IUpdateEntry> entries,
                           IDiagnosticsLogger<DbLoggerCategory.Update> updateLogger)
    {
        var rowsAffected = 0;
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var entityType = entry.EntityType;

            Check.DebugAssert(!entityType.IsAbstract(), "entityType is abstract");

            var table = EnsureTable(entityType);

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

    IEnumerable<Task<RecordMetadata>> ExecuteTransaction(System.Collections.Generic.IList<IUpdateEntry> entries, IDiagnosticsLogger<DbLoggerCategory.Update> updateLogger, out int rowsAffected, CancellationToken cancellationToken = default)
    {
        if (Options.ReadOnlyMode)
        {
            throw new InvalidOperationException($"Cannot execute any operation since the instance is in read-only mode.");
        }

        System.Collections.Generic.Dictionary<IKEFCoreTable, System.Collections.Generic.IList<IKEFCoreRowBag>> dataInTransaction = [];

        rowsAffected = PrepareTransaction(dataInTransaction, entries, updateLogger);

        System.Collections.Generic.List<Future<RecordMetadata>> futures = [];
        foreach (var tableData in dataInTransaction)
        {
            tableData.Key.Commit(futures, tableData.Value);
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
    public virtual int ExecuteTransaction(System.Collections.Generic.IList<IUpdateEntry> entries, IDiagnosticsLogger<DbLoggerCategory.Update> updateLogger)
    {
        _infrastructureLogger.Logger.LogInformation("Invoking ExecuteTransaction for {number} entries", entries.Count);

        using var ctSource = new CancellationTokenSource();
        var tasks = ExecuteTransaction(entries, updateLogger, out var rowsAffected, ctSource.Token);
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

    /// <summary>
    /// Executes a transaction in async
    /// </summary>
    public async Task<int> ExecuteTransactionAsync(System.Collections.Generic.IList<IUpdateEntry> entries, IDiagnosticsLogger<DbLoggerCategory.Update> updateLogger, CancellationToken cancellationToken = default)
    {
        _infrastructureLogger.Logger.LogInformation("Invoking ExecuteTransactionAsync for {number} entries", entries.Count);

        var tasks = ExecuteTransaction(entries, updateLogger, out var rowsAffected, cancellationToken);

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

    // Must be called from inside the lock
    private IKEFCoreTable EnsureTable(IEntityType entityType)
    {
        _tables ??= new System.Collections.Concurrent.ConcurrentDictionary<string, IKEFCoreTable>();

        var entityTypes = entityType.GetAllBaseTypesInclusive();
        foreach (var currentEntityType in entityTypes)
        {
            var key = currentEntityType.TopicName(Options);
            _ = _tables.GetOrAdd(key, (k) =>
            {
                _infrastructureLogger.Logger.LogInformation("KafkaCluster::EnsureTable creating table for {Name}", entityType.Name);
                return _tableFactory.Create(this, k, currentEntityType);
            });
        }

        return _tables[entityType.TopicName(Options)];
    }
}
