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

// #define DEBUG_PERFORMANCE

#nullable enable

using Java.Util;
using MASES.EntityFrameworkCore.KNet.Serialization;
using MASES.KNet.Serialization;
using Org.Apache.Kafka.Common.Utils;
using Org.Apache.Kafka.Streams;
using Org.Apache.Kafka.Streams.Errors;
using Org.Apache.Kafka.Streams.Kstream;
using Org.Apache.Kafka.Streams.State;
using static Org.Apache.Kafka.Streams.Errors.StreamsUncaughtExceptionHandler;

namespace MASES.EntityFrameworkCore.KNet.Storage.Internal;

/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public class KafkaStreamsBaseRetriever<TKey, TValue, K, V> : IKafkaStreamsRetriever
    where TKey : notnull
    where TValue : IValueContainer<TKey>
{
    struct StreamsAssociatedData(KeyValueBytesStoreSupplier storeSupplier, Materialized<K, V, KeyValueStore<Bytes, byte[]>> materialized, GlobalKTable<K, V> globalTable)
    {
        public KeyValueBytesStoreSupplier StoreSupplier = storeSupplier;
        public Materialized<K, V, KeyValueStore<Bytes, byte[]>> Materialized = materialized;
        public GlobalKTable<K, V> GlobalTable = globalTable;
    }

    private static bool _preserveStreamsAcrossContext = KEFCore.PreserveInformationAcrossContexts;
    // this dictionary controls the entities
    private static readonly System.Collections.Generic.Dictionary<IEntityType, IEntityType> _managedEntities = new(EntityTypeFullNameComparer.Instance);
    // while this one is used to retain the allocated object to avoid thier finalization before the streams is completly finalized
    private static readonly System.Collections.Generic.Dictionary<IEntityType, StreamsAssociatedData> _storagesForEntities = new(EntityTypeFullNameComparer.Instance);

    private static StreamsBuilder? _builder = null;
    private static Topology? _topology = null;
    private static Properties? _properties = null;
    private static KafkaStreams? _streams = null;

    private static AutoResetEvent? _dataReceived;
    private static AutoResetEvent? _resetEvent;
    private static AutoResetEvent? _stateChanged;
    private static AutoResetEvent? _exceptionSet;
    private static StreamsUncaughtExceptionHandler? _errorHandler;
    private static KafkaStreams.StateListener? _stateListener;
    private static Exception? _resultException = null;
    private static KafkaStreams.State _currentState = KafkaStreams.State.NOT_RUNNING;

    private readonly IKafkaCluster _kafkaCluster;
    private readonly IEntityType _entityType;
    private readonly ISerDes<TKey> _keySerdes;
    private readonly ISerDes<TValue> _valueSerdes;

    private readonly bool _usePersistentStorage;
    private readonly string _topicName;
    private readonly string _storageId;

    /// <summary>
    /// Default initializer
    /// </summary>
    public KafkaStreamsBaseRetriever(IKafkaCluster kafkaCluster, IEntityType entityType, ISerDes<TKey> keySerdes, ISerDes<TValue> valueSerdes, StreamsBuilder builder)
    {
        _kafkaCluster = kafkaCluster;
        _entityType = entityType;
        _keySerdes = keySerdes;
        _valueSerdes = valueSerdes;
        _builder ??= builder;
        _topicName = _entityType.TopicName(kafkaCluster.Options);
        _usePersistentStorage = _kafkaCluster.Options.UsePersistentStorage;
        _properties ??= _kafkaCluster.Options.StreamsOptions(_kafkaCluster.Options.ApplicationId);

        string storageId = _entityType.StorageIdForTable(_kafkaCluster.Options);
        _storageId = _usePersistentStorage ? storageId : Process.GetCurrentProcess().ProcessName + "-" + storageId;

        lock (_managedEntities)
        {
            if (!_managedEntities.ContainsKey(_entityType))
            {
                var storeSupplier = _usePersistentStorage ? Stores.PersistentKeyValueStore(_storageId) : Stores.InMemoryKeyValueStore(_storageId);
                var materialized = Materialized<K, V, KeyValueStore<Bytes, byte[]>>.As(storeSupplier);
                var globalTable = _builder.GlobalTable(_topicName, materialized);
                _managedEntities.Add(_entityType, _entityType);
                _storagesForEntities.Add(_entityType, new StreamsAssociatedData(storeSupplier, materialized, globalTable));
                
                if (_streams != null)
                {
                    StopTopology();
                }
                _topology = _builder.Build();
                _streams = new(_topology, _properties);
                StartTopology(_streams);
            }
        }
    }

    private static void StartTopology(KafkaStreams streams)
    {
        _dataReceived = new(false);
        _resetEvent = new(false);
        _stateChanged = new(false);
        _exceptionSet = new(false);

        _errorHandler ??= new()
        {
            OnHandle = (exception) =>
            {
                _resultException = exception;
                _exceptionSet.Set();
                return StreamThreadExceptionResponse.SHUTDOWN_APPLICATION;
            }
        };

        _stateListener ??= new()
        {
            OnOnChange = (newState, oldState) =>
            {
                _currentState = newState;
#if DEBUG_PERFORMANCE
                Infrastructure.KafkaDbContext.ReportString($"StateListener oldState: {oldState} newState: {newState} on {DateTime.Now:HH:mm:ss.FFFFFFF}");
#endif
                if (_stateChanged != null && !_stateChanged.SafeWaitHandle.IsClosed) _stateChanged.Set();
            }
        };

        streams.SetUncaughtExceptionHandler(_errorHandler);
        streams.SetStateListener(_stateListener);

        ThreadPool.QueueUserWorkItem((o) =>
        {
            int waitingTime = Timeout.Infinite;
            Stopwatch watcher = new();
            try
            {
                _resetEvent.Set();
                var index = WaitHandle.WaitAny([_stateChanged, _exceptionSet]);
                if (index == 1) return;
                while (true)
                {
                    index = WaitHandle.WaitAny([_stateChanged, _dataReceived, _exceptionSet], waitingTime);
                    if (index == 2) return;
                    if (_currentState.Equals(KafkaStreams.State.CREATED) || _currentState.Equals(KafkaStreams.State.REBALANCING))
                    {
                        if (index == WaitHandle.WaitTimeout)
                        {
#if DEBUG_PERFORMANCE
                            Infrastructure.KafkaDbContext.ReportString($"State: {_currentState} No handle set within {waitingTime} ms");
#endif
                            continue;
                        }
                    }
                    else // exit external wait thread 
                    {
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                _resultException = e;
            }
            finally
            {
                _resetEvent.Set();
            }
        });
        _resetEvent.WaitOne();
        streams.Start();
#if DEBUG_PERFORMANCE
        Infrastructure.KafkaDbContext.ReportString($"KafkaStreamsBaseRetriever started on {DateTime.Now:HH:mm:ss.FFFFFFF}");
#endif
        _resetEvent.WaitOne(); // wait running state
        if (_resultException != null) throw _resultException;
    }

    private static void StopTopology()
    {
        _streams?.Close();

        _dataReceived?.Dispose();
        _resetEvent?.Dispose();
        _exceptionSet?.Dispose();
        _stateChanged?.Dispose();

        _streams = null;
    }

    private static void FinalCleanup()
    {
        _errorHandler?.Dispose();
        _stateListener?.Dispose();
        _errorHandler = null;
        _stateListener = null;
        _builder = null;
        _topology = null;
        _properties = null;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_managedEntities)
        {
            if (!_preserveStreamsAcrossContext)
            {
                _managedEntities.Remove(_entityType);
                if (_managedEntities.Count == 0)
                {
                    StopTopology();
                    _storagesForEntities.Clear();
                    FinalCleanup();
                }
            }
        }
    }

    /// <inheritdoc/>
    public IEnumerable<ValueBuffer> GetValueBuffers()
    {
        return new KafkaEnumberable(_kafkaCluster, _entityType, _keySerdes, _valueSerdes, _storageId);
    }

    class KafkaEnumberable : IEnumerable<ValueBuffer>
    {
        private readonly IKafkaCluster _kafkaCluster;
        private readonly IEntityType _entityType;
        private readonly ISerDes<TKey> _keySerdes;
        private readonly ISerDes<TValue> _valueSerdes;
        private readonly Org.Apache.Kafka.Streams.State.ReadOnlyKeyValueStore<K, V>? _keyValueStore = null;

        public KafkaEnumberable(IKafkaCluster kafkaCluster, IEntityType entityType, ISerDes<TKey> keySerdes, ISerDes<TValue> valueSerdes, string storageId)
        {
            _kafkaCluster = kafkaCluster;
            _entityType = entityType;
            _keySerdes = keySerdes;
            _valueSerdes = valueSerdes;
            _keyValueStore = _streams?.Store(StoreQueryParameters<Org.Apache.Kafka.Streams.State.ReadOnlyKeyValueStore<K, V>>.FromNameAndType(storageId, Org.Apache.Kafka.Streams.State.QueryableStoreTypes.KeyValueStore<K, V>()));
#if DEBUG_PERFORMANCE
            Infrastructure.KafkaDbContext.ReportString($"KafkaEnumerator for {_entityType.Name} - ApproximateNumEntries {_keyValueStore?.ApproximateNumEntries()}");
#endif
        }

        /// <inheritdoc/>
        public IEnumerator<ValueBuffer> GetEnumerator()
        {
            if (_resultException != null) throw _resultException;
#if DEBUG_PERFORMANCE
            Infrastructure.KafkaDbContext.ReportString($"Requesting KafkaEnumerator for {_entityType.Name} on {DateTime.Now:HH:mm:ss.FFFFFFF}");
#endif
            return new KafkaEnumerator(_kafkaCluster, _entityType, _keySerdes, _valueSerdes, _keyValueStore?.All());
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    class KafkaEnumerator : IEnumerator<ValueBuffer>
    {
        private readonly IKafkaCluster _kafkaCluster;
        private readonly IEntityType _entityType;
        private readonly ISerDes<TKey> _keySerdes;
        private readonly ISerDes<TValue> _valueSerdes;
        private readonly Org.Apache.Kafka.Streams.State.KeyValueIterator<K, V>? _keyValueIterator = null;

        Stopwatch _valueGet = new Stopwatch();

#if DEBUG_PERFORMANCE
        Stopwatch _moveNextSw = new Stopwatch();
        Stopwatch _currentSw = new Stopwatch();
        Stopwatch _valueGetSw = new Stopwatch();
        Stopwatch _valueSerdesSw = new Stopwatch();
        Stopwatch _valueBufferSw = new Stopwatch();
#endif

        public KafkaEnumerator(IKafkaCluster kafkaCluster, IEntityType entityType, ISerDes<TKey> keySerdes, ISerDes<TValue> valueSerdes, Org.Apache.Kafka.Streams.State.KeyValueIterator<K, V>? keyValueIterator)
        {
            _kafkaCluster = kafkaCluster ?? throw new ArgumentNullException(nameof(kafkaCluster));
            _entityType = entityType;
            _keySerdes = keySerdes ?? throw new ArgumentNullException(nameof(keySerdes));
            _valueSerdes = valueSerdes ?? throw new ArgumentNullException(nameof(valueSerdes));
            _keyValueIterator = keyValueIterator ?? throw new ArgumentNullException(nameof(keyValueIterator));
#if DEBUG_PERFORMANCE
            Infrastructure.KafkaDbContext.ReportString($"Requested KafkaEnumerator for {_entityType.Name} on {DateTime.Now:HH:mm:ss.FFFFFFF}");
#endif
        }

        ValueBuffer? _current = null;

        public ValueBuffer Current
        {
            get
            {
#if DEBUG_PERFORMANCE
                    try
                    {
                        _currentSw.Start();
#endif
                return _current ?? default;
#if DEBUG_PERFORMANCE
                    }
                    finally
                    {
                        _currentSw.Stop();
                    }
#endif
            }
        }

        object System.Collections.IEnumerator.Current => Current;

        public void Dispose()
        {
#if DEBUG_PERFORMANCE
            Infrastructure.KafkaDbContext.ReportString($"KafkaEnumerator _moveNextSw: {_moveNextSw.Elapsed} _currentSw: {_currentSw.Elapsed} _valueGetSw: {_valueGetSw.Elapsed} _valueSerdesSw: {_valueSerdesSw.Elapsed} _valueBufferSw: {_valueBufferSw.Elapsed}");
#endif
            _keyValueIterator?.Dispose();
        }

#if DEBUG_PERFORMANCE
        int _cycles = 0;
#endif

        public bool MoveNext()
        {
#if DEBUG_PERFORMANCE
                try
                {
                    _moveNextSw.Start();
#endif
            if (_keyValueIterator != null && _keyValueIterator.HasNext)
            {
#if DEBUG_PERFORMANCE
                _cycles++;
                _valueGetSw.Start();
#endif
                byte[]? data;
                using (KeyValue<K, V> kv = _keyValueIterator.Next)
                {
                    data = kv.value as byte[];
                }
#if DEBUG_PERFORMANCE
                _valueGetSw.Stop();
                _valueSerdesSw.Start();
#endif
                TValue entityTypeData = _valueSerdes.DeserializeWithHeaders(null, null, data);
#if DEBUG_PERFORMANCE
                _valueSerdesSw.Stop();
                _valueBufferSw.Start();
#endif
                object[] array = null!;
                entityTypeData.GetData(_entityType, ref array);
#if DEBUG_PERFORMANCE
                _valueBufferSw.Stop();
#endif
                _current = new ValueBuffer(array);

                return true;
            }
            _current = null;
            return false;
#if DEBUG_PERFORMANCE
                }
                finally
                {
                    _moveNextSw.Stop();
                    if (_cycles == 0)
                    {
                        throw new InvalidOperationException($"KafkaEnumerator - No data returned from {_keyValueIterator}");
                    }
                }
#endif
        }

        public void Reset()
        {
            throw new NotSupportedException(CoreStrings.EnumerableResetNotSupported);
        }
    }
}
