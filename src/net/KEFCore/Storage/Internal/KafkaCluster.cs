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
using MASES.KNet.Admin;
using Org.Apache.Kafka.Clients.Admin;
using Org.Apache.Kafka.Clients.Producer;
using Org.Apache.Kafka.Common;
using Org.Apache.Kafka.Common.Acl;
using Org.Apache.Kafka.Common.Errors;
using Org.Apache.Kafka.Tools;

namespace MASES.EntityFrameworkCore.KNet.Storage.Internal;
/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public class KafkaCluster : IKafkaCluster
{
    private readonly KafkaOptionsExtension _options;
    private readonly IDiagnosticsLogger<DbLoggerCategory.Infrastructure> _infrastructureLogger;
    private readonly IKafkaTableFactory _tableFactory;
    private readonly IComplexTypeConverterFactory _complexTypeConverterFactory;
    private readonly IValueGeneratorSelector _valueGeneratorSelector;
    private readonly IUpdateAdapterFactory _updateAdapterFactory;
    private readonly IModel _designModel;
    private readonly bool _useNameMatching;
    private readonly Admin _kafkaAdminClient = null;
    private readonly Properties _bootstrapProperties;

    private System.Collections.Concurrent.ConcurrentDictionary<object, IKafkaTable> _tables;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<IEntityType, string> _topicForEntity = new();
    /// <summary>
    /// Dfault initializer
    /// </summary>
    public KafkaCluster(KafkaOptionsExtension options,
        IDiagnosticsLogger<DbLoggerCategory.Infrastructure> infrastructureLogger,
        IKafkaTableFactory tableFactory,
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
        _useNameMatching = options.UseNameMatching;

        _bootstrapProperties = AdminClientConfigBuilder.Create().WithBootstrapServers(Options.BootstrapServers).ToProperties();
        try
        {
            _kafkaAdminClient = Admin.Create(_bootstrapProperties);
        }
        catch (ExecutionException ex)
        {
            if (ex.InnerException != null) throw ex.InnerException;
            throw;
        }
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
    public virtual KafkaOptionsExtension Options => _options;
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

        try
        {
            DeleteTopicsResult result = default;
            KafkaFuture<Java.Lang.Void> future = default;
            try
            {
                result = _kafkaAdminClient?.DeleteTopics(coll);
                future = result?.All();
                future?.Get();
            }
            catch (ExecutionException ex)
            {
                if (ex.InnerException != null) throw ex.InnerException;
                else throw;
            }
            finally { future?.Dispose(); result?.Dispose(); }
        }
        catch (Org.Apache.Kafka.Common.Errors.UnknownTopicOrPartitionException utpe)
        {
            _infrastructureLogger.Logger.LogError("EnsureDeleted reports the following {Error}", utpe.Message);
        }

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

        try
        {
            _infrastructureLogger.Logger.LogInformation("Trying to identify information of topics from the cluster.");
            DescribeTopicsResult result = default;
            KafkaFuture<Map<Java.Lang.String, TopicDescription>> future = default;
            DescribeTopicsOptions describeTopicsOptions = new();
            describeTopicsOptions.IncludeAuthorizedOperations(true);
            try
            {
                result = _kafkaAdminClient?.DescribeTopics(coll, describeTopicsOptions);
                future = result?.AllTopicNames();
                foreach (var item in future.Get().EntrySet())
                {
                    if (item.Value.IsInternal())
                    {
                        _infrastructureLogger.Logger.LogDebug("Topic {Key} is internal", item.Key);
                        continue;
                    }
                    var partitionsData = item.Value.Partitions();
                    var numPartition = partitionsData.Size();
                    if (Options.ManageEvents && !Options.UseCompactedReplicator && numPartition > 1)
                    {
                        throw new InvalidOperationException($"{nameof(KafkaDbContext.ManageEvents)} supports a number of partition higher than 1 only with {nameof(KafkaDbContext.UseCompactedReplicator)}=true, in all other cases events are supported only using a single partition.");
                    }

                    bool write = false;
                    bool read = false;

                    foreach (var operation in item.Value.AuthorizedOperations())
                    {
                        if (operation == AclOperation.WRITE) write = true;
                        if (operation == AclOperation.READ) read = true;
                        _infrastructureLogger.Logger.LogDebug("Topic {Key} supports {Name}", item.Key, operation.Name());
                    }
                    if (Options.ReadOnlyMode)
                    {
                        if (!read) throw new InvalidOperationException($"Topic {item.Key} shall support {AclOperation.READ}");
                    }
                    else if (!(read && write)) { throw new InvalidOperationException($"Topic {item.Key} shall support both {AclOperation.WRITE} and {AclOperation.READ}"); }
                }
            }
            catch (ExecutionException ex)
            {
                if (ex.InnerException is UnknownTopicOrPartitionException)
                {
                    throw ex.InnerException;
                }
                else if (ex.InnerException != null) throw ex.InnerException;
                else throw;
            }
            finally { future?.Dispose(); result?.Dispose(); }
        }
        catch (UnknownTopicOrPartitionException ex)
        {
            _infrastructureLogger.Logger.LogDebug(ex.Message);
        }

        var valuesSeeded = _tables == null;
        if (valuesSeeded)
        {
            System.Collections.Generic.List<IKafkaTable> tables = new();
            _tables = new System.Collections.Concurrent.ConcurrentDictionary<object, IKafkaTable>();

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
                    _infrastructureLogger.Logger.LogDebug("Synchronization of {ApplicationId} with cluster id {ClusterId} done in {Elapsed}", Options.ApplicationId, Options.ClusterId, stopwatch.Elapsed);
                }
            }

            ExecuteTransaction(entries, updateLogger);
        }

        return valuesSeeded;
    }
    /// <inheritdoc/>
    public virtual bool EnsureConnected(IDiagnosticsLogger<DbLoggerCategory.Update> updateLogger)
    {
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
    public IKafkaTable GetTable(IEntityType entityType)
    {
        return _tableFactory.Get(this, entityType);
    }

    /// <inheritdoc/>
    public virtual string CreateTopicForEntity(IEntityType entityType)
    {
        _infrastructureLogger.Logger.LogDebug("Invoking CreateTopicForEntity for {Entity}", entityType.Name);
        return _topicForEntity.GetOrAdd(entityType, (et) =>
        {
            return CreateTopicForEntity(et, 100, 100, 0);
        });
    }

    private string CreateTopicForEntity(IEntityType entityType, int waitTime, int maxCycles, int cycle)
    {
        _infrastructureLogger.Logger.LogDebug("Invoking CreateTopicForEntity for {Entity} attempt {Cycle}", entityType.Name, cycle);

        var topicName = entityType.TopicName(Options);

        try
        {
            var requestedPartitions = entityType.NumPartitions(Options);
            var requestedReplicationFactor = entityType.ReplicationFactor(Options);
            if (Options.ManageEvents
                && !Options.UseCompactedReplicator
                && requestedPartitions > 1)
            {
                throw new InvalidOperationException($"{nameof(KafkaDbContext.ManageEvents)} supports a number of partition higher than 1 only with {nameof(KafkaDbContext.UseCompactedReplicator)}=true, in all other cases events are supported only using a single partition.");
            }

            Set<NewTopic> coll = default;
            CreateTopicsResult result = default;
            KafkaFuture<Java.Lang.Void> future = default;
            NewTopic topic = default;
            Map<Java.Lang.String, Java.Lang.String> map = default;
            try
            {
                topic = new NewTopic(topicName, requestedPartitions, requestedReplicationFactor);
                Options.TopicConfig.CleanupPolicy = Options.UseDeletePolicyForTopic
                                                    ? MASES.KNet.Common.TopicConfigBuilder.CleanupPolicyTypes.Compact | MASES.KNet.Common.TopicConfigBuilder.CleanupPolicyTypes.Delete
                                                    : MASES.KNet.Common.TopicConfigBuilder.CleanupPolicyTypes.Compact;
                Options.TopicConfig.RetentionBytes = 1024 * 1024 * 1024;
                map = Options.TopicConfig.ToMap();
                topic.Configs(map);
                coll = Collections.Singleton(topic);
                result = _kafkaAdminClient?.CreateTopics(coll);
                future = result?.All();
                future?.Get();
            }
            catch (ExecutionException ex)
            {
                if (ex.InnerException != null) throw ex.InnerException;
                throw;
            }
            finally { map?.Dispose(); topic?.Dispose(); future?.Dispose(); result?.Dispose(); coll?.Dispose(); }
        }
        catch (TopicExistsException ex)
        {
            if (cycle >= maxCycles) throw new System.TimeoutException($"Timeout occurred executing CreateTopicForEntity on {entityType.Name} after {cycle * waitTime}");
            if (ex.Message.Contains("deletion"))
            {
                _infrastructureLogger.Logger.LogInformation("Invoke again CreateTopicForEntity for {Entity} at attempt {Cycle} since server reported {Error}", entityType.Name, cycle, ex.Message);
                Thread.Sleep(waitTime); // wait a while before the server completes topic deletion and try again
                return CreateTopicForEntity(entityType, waitTime, maxCycles, cycle++);
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
            try
            {
                Java.Lang.String topicName = entityType.TopicName(Options);
                var coll = Collections.Singleton(topicName);
                DescribeTopicsResult describeTopicsResult = _kafkaAdminClient.DescribeTopics(coll);
                using var future = describeTopicsResult.AllTopicNames();
                var result = future.Get();
                foreach (var item in result.EntrySet())
                {
                    if (item.Key.Equals(topicName))
                    {
                        HashMap<TopicPartition, OffsetSpec> hashMap = new HashMap<TopicPartition, OffsetSpec>();
                        foreach (var partition in item.Value.Partitions())
                        {
                            var partitionIndex = partition.Partition();
                            TopicPartition topicPartition = new(topicName, partitionIndex);
                            hashMap.Put(topicPartition, OffsetSpec.Latest());
                        }

                        var listOffsetResult = _kafkaAdminClient.ListOffsets(hashMap);
                        using var offsetResultFuture = listOffsetResult.All();
                        var offsetResult = offsetResultFuture.Get();
                        foreach (var offsetResultItem in offsetResult.EntrySet())
                        {
                            if (offsetResultItem.Key.Topic().Equals(topicName))
                            {
                                dictionary.Add(offsetResultItem.Key.Partition(), offsetResultItem.Value.Offset() - 1); // since latest means the latest used offset (a record in kafka) + 1, here we remove 1 to be in sync with received offset from kafka
                            }
                        }
                        break;
                    }
                }
            }
            catch (ExecutionException ex)
            {
                if (ex.InnerException != null) throw ex.InnerException;
                else throw;
            }
        }
        catch (UnknownTopicOrPartitionException ex)
        {
            if (cycle >= maxCycles) throw new System.TimeoutException($"Timeout occurred executing LatestOffsetForEntity on {entityType.Name} after {cycle * waitTime}");
            _infrastructureLogger.Logger.LogInformation("Invoke again LatestOffsetForEntity for {Entity} at attempt {Cycle} since server reported {Error}. This can be a normal condition on clean start-up.", entityType.Name, cycle, ex.Message);
            Thread.Sleep(waitTime); // wait a while before the server completes topic creation and try again
            return LatestOffsetForEntity(entityType, waitTime, maxCycles, cycle++);
        }
        return dictionary;
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
        var key = _useNameMatching ? (object)entityType.Name : entityType;
        if (_tables != null && _tables.TryGetValue(key, out var table))
        {
            return table.ValueBuffers;
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

    int PrepareTransaction(IDictionary<IKafkaTable, System.Collections.Generic.IList<IKafkaRowBag>> dataInTransaction,
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

            IKafkaRowBag record;

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

            if (!dataInTransaction.TryGetValue(table, out System.Collections.Generic.IList<IKafkaRowBag> recordList))
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

        System.Collections.Generic.Dictionary<IKafkaTable, System.Collections.Generic.IList<IKafkaRowBag>> dataInTransaction = [];

        rowsAffected = PrepareTransaction(dataInTransaction, entries, updateLogger);

        System.Collections.Generic.List<Future<RecordMetadata>> futures = [];
        foreach (var tableData in dataInTransaction)
        {
            tableData.Key.Commit(null, tableData.Value);
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
        _infrastructureLogger.Logger.LogDebug("Invoking ExecuteTransaction");

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
        _infrastructureLogger.Logger.LogDebug("Invoking ExecuteTransactionAsync");

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
    private IKafkaTable EnsureTable(IEntityType entityType)
    {
        _tables ??= new System.Collections.Concurrent.ConcurrentDictionary<object, IKafkaTable>();

        var entityTypes = entityType.GetAllBaseTypesInclusive();
        foreach (var currentEntityType in entityTypes)
        {
            var key = _useNameMatching ? (object)currentEntityType.Name : currentEntityType;
            _ = _tables.GetOrAdd(key, (k) =>
            {
                _infrastructureLogger.Logger.LogInformation("KafkaCluster::EnsureTable creating table for {Name}", entityType.Name);
                return _tableFactory.Create(this, currentEntityType);
            });
        }

        return _tables[_useNameMatching ? entityType.Name : entityType];
    }
}
