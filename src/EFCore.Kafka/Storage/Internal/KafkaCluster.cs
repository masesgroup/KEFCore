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

using MASES.EntityFrameworkCore.Kafka.ValueGeneration.Internal;
using MASES.EntityFrameworkCore.Kafka.Diagnostics.Internal;
using MASES.EntityFrameworkCore.Kafka.Infrastructure.Internal;
using MASES.KafkaBridge.Clients.Admin;
using MASES.KafkaBridge.Java.Util;
using MASES.JCOBridge.C2JBridge;
using MASES.KafkaBridge.Common.Config;
using MASES.KafkaBridge.Clients.Producer;
using MASES.EntityFrameworkCore.Kafka.Serdes.Internal;
using System.Collections.Concurrent;
using MASES.KafkaBridge.Streams;
using MASES.KafkaBridge.Streams.KStream;
using MASES.KafkaBridge.Streams.Errors;

namespace MASES.EntityFrameworkCore.Kafka.Storage.Internal;

public class KafkaCluster : IKafkaCluster
{
    private readonly KafkaOptionsExtension _options;
    private readonly IKafkaTableFactory _tableFactory;
    private readonly IKafkaSerdesFactory _serdesFactory;
    private readonly bool _useNameMatching;
    private readonly IAdmin _kafkaAdminClient;

    private readonly object _lock = new();

    private Dictionary<object, IKafkaTable>? _tables;

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
        catch (KafkaBridge.Java.Util.Concurrent.ExecutionException ex)
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

    private static Dictionary<object, IKafkaTable> CreateTables()
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

                    return ProcessData(builder, root);
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

    private IReadOnlyList<object[]> ProcessData(StreamsBuilder builder, KStream<string, string> root)
    {
        System.Collections.Generic.List<object[]> list = new();
        AutoResetEvent dataReceived = new(false);
        using (ForeachAction<string, string> act = new((key, value) =>
        {
            list.Add(_serdesFactory.Deserialize(value));
            dataReceived.Set();
            //Trace.WriteLine("ForeachAction DataReceived");
        }))
        {
            root.Foreach(act);
            ExecuteTopology(builder.Build(), dataReceived);
        }

        return list;
    }

    private void ExecuteTopology(Topology topology, WaitHandle dataReceived)
    {
        Exception? resultException = null;
        StateType actualState = StateType.NOT_RUNNING;
        AutoResetEvent resetEvent = new(false);
        AutoResetEvent stateChanged = new(false);
        AutoResetEvent exceptionSet = new(false);
        System.Collections.Generic.List<object[]> list = new();

        string tempApplicationId = Options.DatabaseName + "-" + Guid.NewGuid().ToString();
        using KafkaStreams streams = new(topology, Options.StreamsOptions(tempApplicationId));
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
            ThreadPool.QueueUserWorkItem((_) =>
            {
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
            });
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
    }

    public virtual int ExecuteTransaction(
        IList<IUpdateEntry> entries,
        IDiagnosticsLogger<DbLoggerCategory.Update> updateLogger)
    {
        var rowsAffected = 0;
        Dictionary<IKafkaTable, IList<ProducerRecord<string, string>>> _dataInTransaction = new();

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

                IList<ProducerRecord<string, string>> recordList = null;
                if (!_dataInTransaction.TryGetValue(table, out recordList))
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
            if (!_tables.TryGetValue(key, out var table))
            {
                _tables.Add(key, table = _tableFactory.Create(this, currentEntityType));
            }
        }

        return _tables[_useNameMatching ? entityType.Name : entityType];
    }

    private string TopicFrom(IEntityType entityType)
    {
        return _options.DatabaseName + "." + entityType.Name;
    }
}
