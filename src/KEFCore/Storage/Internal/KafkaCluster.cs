/*
*  Copyright 2022 MASES s.r.l.
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
using MASES.EntityFrameworkCore.KNet.Diagnostics.Internal;
using MASES.EntityFrameworkCore.KNet.Infrastructure.Internal;
using MASES.KNet.Clients.Admin;
using Java.Util;
using MASES.JCOBridge.C2JBridge;
using MASES.KNet.Common.Config;
using MASES.KNet.Clients.Producer;
using MASES.EntityFrameworkCore.KNet.Serdes.Internal;
using System.Collections.Concurrent;
using MASES.KNet.Common.Errors;
using Java.Util.Concurrent;

namespace MASES.EntityFrameworkCore.KNet.Storage.Internal;

public class KafkaCluster : IKafkaCluster
{
    private readonly KafkaOptionsExtension _options;
    private readonly IKafkaTableFactory _tableFactory;
    private readonly IKafkaSerdesFactory _serdesFactory;
    private readonly bool _useNameMatching;
    private readonly IAdmin _kafkaAdminClient;

    private readonly object _lock = new();

    private System.Collections.Generic.Dictionary<object, IKafkaTable>? _tables;

    private IProducer<string, string>? _globalProducer = null;
    private readonly ConcurrentDictionary<IEntityType, IProducer<string, string>> _producers;

    public KafkaCluster(
        KafkaOptionsExtension options,
        IKafkaTableFactory tableFactory,
        IKafkaSerdesFactory serdesFactory)
    {
        _options = options;
        _tableFactory = tableFactory;
        _serdesFactory = serdesFactory;
        _useNameMatching = options.UseNameMatching;
        _producers = new();
        Properties props = new();
        props.Put(AdminClientConfig.BOOTSTRAP_SERVERS_CONFIG, _options.BootstrapServers);
        _kafkaAdminClient = KafkaAdminClient.Create(props);
    }

    public virtual IKafkaSerdesFactory SerdesFactory => _serdesFactory;

    public virtual KafkaOptionsExtension Options => _options;

    public virtual KafkaIntegerValueGenerator<TProperty> GetIntegerValueGenerator<TProperty>(
        IProperty property)
    {
        lock (_lock)
        {
            var entityType = property.DeclaringEntityType;

            return EnsureTable(entityType).GetIntegerValueGenerator<TProperty>(
                property,
                entityType.GetDerivedTypesInclusive().Select(type => EnsureTable(type)).ToArray());
        }
    }

    public virtual bool EnsureDeleted(
        IUpdateAdapterFactory updateAdapterFactory,
        IModel designModel,
        IDiagnosticsLogger<DbLoggerCategory.Update> updateLogger)
    {
        lock (_lock)
        {
            try
            {
                var coll = new ArrayList<string>();
                foreach (var entityType in designModel.GetEntityTypes())
                {
                    coll.Add(entityType.TopicName(_options));
                }
                var result = _kafkaAdminClient.DeleteTopics(coll);
                result.All.Get();
            }
            catch (ExecutionException ex)
            {
                if (ex.InnerException is UnknownTopicOrPartitionException) { Trace.WriteLine(ex.InnerException.Message); }
                else throw ex.InnerException;
            }
        }

        if (_tables == null)
        {
            return false;
        }

        _tables = null;

        return true;
    }

    public virtual bool EnsureCreated(
        IUpdateAdapterFactory updateAdapterFactory,
        IModel designModel,
        IDiagnosticsLogger<DbLoggerCategory.Update> updateLogger)
    {
        lock (_lock)
        {
            var valuesSeeded = _tables == null;
            if (valuesSeeded)
            {
                _tables = CreateTables();

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
    }

    public virtual bool EnsureConnected(
        IModel designModel,
        IDiagnosticsLogger<DbLoggerCategory.Update> updateLogger)
    {
        return true;
    }

    public virtual bool CreateTable(IEntityType entityType)
    {
        try
        {
            var topic = new NewTopic(entityType.TopicName(_options), entityType.NumPartitions(_options), entityType.ReplicationFactor(_options));
            var map = Collections.SingletonMap(TopicConfig.CLEANUP_POLICY_CONFIG, TopicConfig.CLEANUP_POLICY_COMPACT);
            topic.Configs(map);
            var coll = Collections.Singleton(topic);
            var result = _kafkaAdminClient.CreateTopics(coll);
            result.All.Get();
        }
        catch (Java.Util.Concurrent.ExecutionException ex)
        {
            return false;
        }

        return true;
    }

    public virtual IKafkaSerdesEntityType CreateSerdes(IEntityType entityType) => _serdesFactory.GetOrCreate(entityType);

    public virtual IProducer<string, string> CreateProducer(IEntityType entityType)
    {
        if (!Options.ProducerByEntity)
        {
            lock (_lock)
            {
                if (_globalProducer == null) _globalProducer = CreateProducer();
                return _globalProducer;
            }
        }
        else
        {
            return _producers.GetOrAdd(entityType, _ => CreateProducer());
        }
    }

    private IProducer<string, string> CreateProducer() => new KNetProducer<string, string>(Options.ProducerOptions());

    private static System.Collections.Generic.Dictionary<object, IKafkaTable> CreateTables() => new();

    public virtual IEnumerable<ValueBuffer> GetData(IEntityType entityType)
    {
        Stopwatch watcher = new();
        lock (_lock)
        {
            try
            {
                watcher.Start();
                EnsureTable(entityType);
                var key = _useNameMatching ? (object)entityType.Name : entityType;
                if (_tables != null && _tables.TryGetValue(key, out var table))
                {
                    return table.ValueBuffers;
                }
                throw new InvalidOperationException("No table available");
            }
            finally
            {
                watcher.Stop();
                Trace.WriteLine("GetData - Execution time was " + watcher.ElapsedMilliseconds + " ms");
            }
        }
    }

    public virtual int ExecuteTransaction(
        IList<IUpdateEntry> entries,
        IDiagnosticsLogger<DbLoggerCategory.Update> updateLogger)
    {
        var rowsAffected = 0;
        System.Collections.Generic.Dictionary<IKafkaTable, IList<ProducerRecord<string, string>>> _dataInTransaction = new();

        lock (_lock)
        {
            // ReSharper disable once ForCanBeConvertedToForeach
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var entityType = entry.EntityType;

                Check.DebugAssert(!entityType.IsAbstract(), "entityType is abstract");

                var table = EnsureTable(entityType);

                ProducerRecord<string, string> record;

                if (entry.SharedIdentityEntry != null)
                {
                    if (entry.EntityState == EntityState.Deleted)
                    {
                        continue;
                    }

                    record = table.Delete(entry);
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

                if (!_dataInTransaction.TryGetValue(table, out IList<ProducerRecord<string, string>>? recordList))
                {
                    recordList = new System.Collections.Generic.List<ProducerRecord<string, string>>();
                    _dataInTransaction[table] = recordList;
                }
                recordList?.Add(record);

                rowsAffected++;
            }
        }

        updateLogger.ChangesSaved(entries, rowsAffected);

        foreach (var tableData in _dataInTransaction)
        {
            tableData.Key.Commit(tableData.Value);
        }

        return rowsAffected;
    }

    // Must be called from inside the lock
    private IKafkaTable EnsureTable(IEntityType entityType)
    {
        _tables ??= CreateTables();

        var entityTypes = entityType.GetAllBaseTypesInclusive();
        foreach (var currentEntityType in entityTypes)
        {
            var key = _useNameMatching ? (object)currentEntityType.Name : currentEntityType;
            if (!_tables.TryGetValue(key, out _))
            {
                _tables.Add(key, _ = _tableFactory.Create(this, currentEntityType));
            }
        }

        return _tables[_useNameMatching ? entityType.Name : entityType];
    }
}
