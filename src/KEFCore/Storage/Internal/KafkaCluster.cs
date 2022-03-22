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
using MASES.KNet.Streams;
using MASES.KNet.Streams.KStream;
using MASES.KNet.Streams.Errors;
using MASES.KNet.Streams.State;
using MASES.KNet.Common.Utils;

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
                    coll.Add(TopicFrom(entityType));
                }
                var result = _kafkaAdminClient.DeleteTopics(coll.Cast<Collection<string>>());
                result.Dyn().all().get();
            }
            catch (Exception ex)
            {

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

    public virtual bool CreateTable(IEntityType entityType, out string tableName)
    {
        tableName = string.Empty;
        try
        {
            tableName = TopicFrom(entityType);
            var topic = new NewTopic(tableName);
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

    public virtual IKafkaSerdesEntityType CreateSerdes(IEntityType entityType)
        => _serdesFactory.GetOrCreate(entityType);

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

    private IProducer<string, string> CreateProducer()
        => new KafkaProducer<string, string>(Options.ProducerOptions());

    private static System.Collections.Generic.Dictionary<object, IKafkaTable> CreateTables()
        => new();

    public virtual IReadOnlyList<object?[]> GetTables(IEntityType entityType)
    {
        Stopwatch watcher = new();
        StreamsBuilder builder = new();
        lock (_lock)
        {
            try
            {
                watcher.Start();
                if (_tables != null)
                {
                    var key = _useNameMatching ? (object)entityType.Name : entityType;
                    _tables.TryGetValue(key, out var table);
                    KStream<string, string> root = builder.Stream<string, string>(TopicFrom(entityType));
                    foreach (var et in entityType.GetDerivedTypes().Where(et => !et.IsAbstract()))
                    {
                        key = _useNameMatching ? (object)et.Name : et;
                        if (_tables.TryGetValue(key, out table))
                        {
                            root = root.Merge(builder.Stream<string, string>(TopicFrom(entityType)));
                        }
                    }

                    var list = new System.Collections.Generic.List<object?[]>();

                    foreach (var item in ExecuteTopology<string, string>(builder, root))
                    {
                        list.Add(_serdesFactory.Deserialize(item.Item2));
                    }

                    return list;
                }
            }
            finally
            {
                watcher.Stop();
                Trace.WriteLine("Execution time was " + watcher.ElapsedMilliseconds + " ms");
            }
        }

        return new System.Collections.Generic.List<object?[]>();

        //var data = new System.Collections.Generic.List<KafkaTableSnapshot>();
        //lock (_lock)
        //{
        //    if (_tables != null)
        //    {
        //        foreach (var et in entityType.GetDerivedTypesInclusive().Where(et => !et.IsAbstract()))
        //        {
        //            var key = _useNameMatching ? (object)et.Name : et;
        //            if (_tables.TryGetValue(key, out var table))
        //            {
        //                data.Add(new KafkaTableSnapshot(et, table.SnapshotRows()));
        //            }
        //        }
        //    }
        //}

        //return data;
    }

    private IReadOnlyList<(K, V)> ExecuteTopology<K,V>(StreamsBuilder builder, KStream<K, V> root)
    {
        System.Collections.Generic.List<(K, V)> list = new();
        AutoResetEvent dataReceived = new(false);
        AutoResetEvent resetEvent = new(false);
        AutoResetEvent stateChanged = new(false);
        AutoResetEvent exceptionSet = new(false);
        ForeachAction<K, V>? foreachAction = null;

        try
        {
            string tempApplicationId = Options.DatabaseName + "-" + Guid.NewGuid().ToString();

            if (Options.RetrieveWithForEach)
            {
                foreachAction = new((key, value) =>
                {
                    list.Add((key, value));
                    dataReceived.Set();
                });
                root.Foreach(foreachAction);
            }
            else
            {
                var materialized = Materialized<K, V, KeyValueStore<Bytes, byte[]>>.As(tempApplicationId);
                root.ToTable(materialized);
            }

            Exception? resultException = null;
            StateType actualState = StateType.NOT_RUNNING;

            using KafkaStreams streams = new(builder.Build(), Options.StreamsOptions(tempApplicationId));
            using StreamsUncaughtExceptionHandler errorHandler = new((exception) =>
            {
                resultException = exception;
                exceptionSet.Set();
                return StreamThreadExceptionResponse.SHUTDOWN_APPLICATION;
            });
            using StateListener stateListener = new((newState, oldState) =>
            {
                actualState = newState;
                Trace.WriteLine("StateListener oldState: " + oldState + " newState: " + newState + " on " + DateTime.Now.ToString("HH:mm:ss.FFFFFFF"));
                stateChanged.Set();
            });
            streams.SetUncaughtExceptionHandler(errorHandler);
            streams.SetStateListener(stateListener);
            try
            {
                ThreadPool.QueueUserWorkItem((o) =>
                {
                    IList<(K, V)>? innerResult = o as IList<(K, V)>;
                    bool firstTimeRunning = true;
                    int waitingTime = Timeout.Infinite;
                    Stopwatch watcher = new();
                    try
                    {
                        resetEvent.Set();
                        var index = WaitHandle.WaitAny(new WaitHandle[] { stateChanged, exceptionSet });
                        if (index == 1) return;
                        while (true)
                        {
                            index = WaitHandle.WaitAny(new WaitHandle[] { stateChanged, dataReceived, exceptionSet }, waitingTime);
                            if (index == 2) return;
                            switch (actualState)
                            {
                                case StateType.CREATED:
                                case StateType.REBALANCING:
                                    if (index == WaitHandle.WaitTimeout)
                                    {
                                        Trace.WriteLine("State: " + actualState + " No handle set within " + waitingTime + " ms");
                                        continue;
                                    }
                                    break;
                                case StateType.RUNNING:
                                    if (Options.RetrieveWithForEach)
                                    {
                                        if (firstTimeRunning)
                                        {
                                            //Trace.WriteLine("State: " + actualState + " FirstTimeRunning");
                                            firstTimeRunning = false;
                                            waitingTime = Options.InitialDataWaitingTime;
                                            watcher.Start();
                                        }
                                        else
                                        {
                                            if (index == 1)
                                            {
                                                watcher.Stop();
                                                //Trace.WriteLine("State: " + actualState + " dataReceived Set after " + watcher.ElapsedMilliseconds + " ms");
                                                waitingTime = Options.MinDataWaitingTime + (int)watcher.ElapsedMilliseconds;
                                                watcher.Restart();
                                            }
                                            if (index == WaitHandle.WaitTimeout)
                                            {
                                                Trace.WriteLine("State: " + actualState + " No handle set within " + waitingTime + " ms");
                                                return;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        // Get the key-value store CountsKeyValueStore
                                        ReadOnlyKeyValueStore<K, V> keyValueStore =
                                                streams.Store(StoreQueryParameters<ReadOnlyKeyValueStore<K, V>>.FromNameAndType(tempApplicationId, QueryableStoreTypes.KeyValueStore<K, V>()));

                                        foreach (var item in keyValueStore.All)
                                        {
                                            innerResult?.Add((item.Key, item.Value));
                                        }
                                        return;
                                    }
                                    break;
                                case StateType.NOT_RUNNING:
                                case StateType.PENDING_ERROR:
                                case StateType.PENDING_SHUTDOWN:
                                case StateType.ERROR:
                                default:
                                    return;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        resultException = e;
                    }
                    finally
                    {
                        resetEvent.Set();
                    }
                }, list);
                resetEvent.WaitOne();
                streams.Start();
                Trace.WriteLine("Started on " + DateTime.Now.ToString("HH:mm:ss.FFFFFFF"));
                resetEvent.WaitOne();
            }
            finally
            {
                streams.Close();
            }
            if (resultException != null) throw resultException;
            return list;
        }
        finally
        {
            dataReceived?.Dispose();
            resetEvent?.Dispose();
            stateChanged?.Dispose();
            exceptionSet?.Dispose();
            foreachAction?.Dispose();
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

    private string TopicFrom(IEntityType entityType)
    {
        return _options.DatabaseName + "." + entityType.Name;
    }
}
