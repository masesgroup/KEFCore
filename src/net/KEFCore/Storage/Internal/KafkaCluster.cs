/*
*  Copyright 2022 - 2025 MASES s.r.l.
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

// #define DEBUG_PERFORMANCE

#nullable disable

using Java.Util;
using Java.Util.Concurrent;
using MASES.EntityFrameworkCore.KNet.Diagnostics.Internal;
using MASES.EntityFrameworkCore.KNet.Infrastructure.Internal;
using MASES.EntityFrameworkCore.KNet.ValueGeneration.Internal;
using MASES.KNet.Admin;
using Org.Apache.Kafka.Clients.Admin;
using Org.Apache.Kafka.Common;
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
    private readonly IKafkaTableFactory _tableFactory;
    private readonly bool _useNameMatching;
    private readonly Admin? _kafkaAdminClient = null;
    private readonly Properties _bootstrapProperties;

    private System.Collections.Concurrent.ConcurrentDictionary<object, IKafkaTable>? _tables;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<IEntityType, string>? _topicForEntity = new();
    /// <summary>
    /// Dfault initializer
    /// </summary>
    public KafkaCluster(KafkaOptionsExtension options, IKafkaTableFactory tableFactory)
    {
        _options = options;
        _tableFactory = tableFactory;
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
#if DEBUG_PERFORMANCE
        Infrastructure.KafkaDbContext.ReportString($"Disposing KafkaCluster");
#endif
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
    public virtual KafkaOptionsExtension Options => _options;
    /// <inheritdoc/>
    public virtual KafkaIntegerValueGenerator<TProperty> GetIntegerValueGenerator<TProperty>(
        IProperty property)
    {
#if NET9_0 || NET10_0
            var entityType = property.DeclaringType;

            return EnsureTable(entityType.ContainingEntityType).GetIntegerValueGenerator<TProperty>(
                property,
                entityType.ContainingEntityType.GetDerivedTypesInclusive().Select(type => EnsureTable(type)).ToArray());
#elif NET8_0
            var entityType = property.DeclaringType;

            return EnsureTable(entityType.ContainingEntityType).GetIntegerValueGenerator<TProperty>(
                property,
                entityType.ContainingEntityType.GetDerivedTypesInclusive().Select(type => EnsureTable(type)).ToArray());
#else
            var entityType = property.DeclaringEntityType;

            return EnsureTable(entityType).GetIntegerValueGenerator<TProperty>(
                property,
                entityType.GetDerivedTypesInclusive().Select(type => EnsureTable(type)).ToArray());
#endif
    }
    /// <inheritdoc/>
    public virtual bool EnsureDeleted(
        IUpdateAdapterFactory updateAdapterFactory,
        IModel designModel,
        IDiagnosticsLogger<DbLoggerCategory.Update> updateLogger)
    {
        var coll = new ArrayList<Java.Lang.String>();
        var topics = new System.Collections.Generic.List<string>();
        foreach (var entityType in designModel.GetEntityTypes())
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

        if (!Options.UseCompactedReplicator)
        {
            try
            {
                StreamsResetter.ResetApplicationForced(Options.BootstrapServers, Options.ApplicationId, topics.ToArray());
            }
            catch (ExecutionException ex)
            {
                if (ex.InnerException != null) throw ex.InnerException;
                throw;
            }
        }

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
            if (ex.InnerException is UnknownTopicOrPartitionException)
            {
#if DEBUG_PERFORMANCE
                Infrastructure.KafkaDbContext.ReportString(ex.InnerException.Message); 
#endif
            }
            else if (ex.InnerException != null) throw ex.InnerException;
            else throw;
        }
        finally { future?.Dispose(); result?.Dispose(); }
        
        if (_tables == null)
        {
            return false;
        }

        _tables = null;

        return true;
    }
    /// <inheritdoc/>
    public virtual bool EnsureCreated(
        IUpdateAdapterFactory updateAdapterFactory,
        IModel designModel,
        IDiagnosticsLogger<DbLoggerCategory.Update> updateLogger)
    {
        var valuesSeeded = _tables == null;
        if (valuesSeeded)
        {
            _tables = new System.Collections.Concurrent.ConcurrentDictionary<object, IKafkaTable>();

            var updateAdapter = updateAdapterFactory.CreateStandalone();
            var entries = new System.Collections.Generic.List<IUpdateEntry>();
            foreach (var entityType in designModel.GetEntityTypes())
            {
                EnsureTable(entityType);

                IEntityType? targetEntityType = null;
                foreach (var targetSeed in entityType.GetSeedData())
                {
                    targetEntityType ??= updateAdapter.Model.FindEntityType(entityType.Name)!;
                    var entry = updateAdapter.CreateEntry(targetSeed, targetEntityType);
                    entry.EntityState = EntityState.Added;
                    entries.Add(entry);
                }
            }

            ExecuteTransaction(entries, updateLogger);
        }

        return valuesSeeded;
    }
    /// <inheritdoc/>
    public virtual bool EnsureConnected(
        IModel designModel,
        IDiagnosticsLogger<DbLoggerCategory.Update> updateLogger)
    {
        return true;
    }
    /// <inheritdoc/>
    public virtual string CreateTopicForEntity(IEntityType entityType)
    {
        return _topicForEntity.GetOrAdd(entityType, (et) =>
        {
            return CreateTopicForEntity(et, 0);
        });
    }

    private string CreateTopicForEntity(IEntityType entityType, int cycle)
    {
        if (cycle >= 10) throw new System.TimeoutException($"Timeout occurred executing CreateTable on {entityType.Name}");

        var topicName = entityType.TopicName(Options);
        Set<NewTopic>? coll = default;
        CreateTopicsResult? result = default;
        KafkaFuture<Java.Lang.Void>? future = default;
        try
        {
            NewTopic? topic = default;
            Map<Java.Lang.String, Java.Lang.String>? map = default;
            try
            {
                topic = new NewTopic(topicName, entityType.NumPartitions(Options), entityType.ReplicationFactor(Options));
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
            finally { map?.Dispose(); topic?.Dispose(); }
        }
        catch (TopicExistsException ex)
        {
            if (ex.Message.Contains("deletion"))
            {
                Thread.Sleep(1000); // wait a while to complete topic deletion and try again
                return CreateTopicForEntity(entityType, cycle++);
            }
        }
        finally { future?.Dispose(); result?.Dispose(); coll?.Dispose(); }

        return topicName;
    }

    /// <inheritdoc/>
    public virtual IEnumerable<ValueBuffer> GetValueBuffers(IEntityType entityType)
    {
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
            Infrastructure.KafkaDbContext.ReportString($"KafkaCluster::GetValueBuffers for {entityType.Name} - EnsureTable: {tableSw.Elapsed} ValueBuffer: {valueBufferSw.Elapsed}");
        }
#endif
    }
    /// <inheritdoc/>
    public virtual int ExecuteTransaction(
        System.Collections.Generic.IList<IUpdateEntry> entries,
        IDiagnosticsLogger<DbLoggerCategory.Update> updateLogger)
    {
        var rowsAffected = 0;
        System.Collections.Generic.Dictionary<IKafkaTable, System.Collections.Generic.IList<IKafkaRowBag>> dataInTransaction = new();

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

            if (!dataInTransaction.TryGetValue(table, out System.Collections.Generic.IList<IKafkaRowBag>? recordList))
            {
                recordList = [];
                dataInTransaction[table] = recordList;
            }
            recordList?.Add(record);

            rowsAffected++;
        }

        updateLogger.ChangesSaved(entries, rowsAffected);

        foreach (var tableData in dataInTransaction)
        {
            tableData.Key.Commit(tableData.Value);
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
#if DEBUG_PERFORMANCE
                Infrastructure.KafkaDbContext.ReportString($"KafkaCluster::EnsureTable creating table for {entityType.Name}");
#endif
                return _tableFactory.Create(this, currentEntityType);
            });
        }

        return _tables[_useNameMatching ? entityType.Name : entityType];
    }
}
