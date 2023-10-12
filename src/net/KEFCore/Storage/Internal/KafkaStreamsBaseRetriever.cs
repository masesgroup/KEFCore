/*
*  Copyright 2023 MASES s.r.l.
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

using MASES.EntityFrameworkCore.KNet.Serialization.Storage;
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
public interface IKafkaStreamsBaseRetriever : IEnumerable<ValueBuffer>, IDisposable
{
}
/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public class KafkaStreamsBaseRetriever<TKey, TValue, K, V> : IKafkaStreamsBaseRetriever
    where TValue : IEntityTypeData
{
    private readonly IKafkaCluster _kafkaCluster;
    private readonly IEntityType _entityType;
    private readonly IKNetSerDes<TKey> _keySerdes;
    private readonly IKNetSerDes<TValue> _valueSerdes;
    private readonly StreamsBuilder _builder;
    private readonly KStream<K, V> _root;

    private readonly AutoResetEvent _dataReceived = new(false);
    private readonly AutoResetEvent _resetEvent = new(false);
    private readonly AutoResetEvent _stateChanged = new(false);
    private readonly AutoResetEvent _exceptionSet = new(false);

    private KafkaStreams? _streams = null;
    private StreamsUncaughtExceptionHandler? _errorHandler;
    private KafkaStreams.StateListener? _stateListener;

    private readonly string _storageId;
    private Exception? _resultException = null;
    private KafkaStreams.State _currentState = KafkaStreams.State.NOT_RUNNING;
    private ReadOnlyKeyValueStore<K, V>? keyValueStore;

    public KafkaStreamsBaseRetriever(IKafkaCluster kafkaCluster, IEntityType entityType, IKNetSerDes<TKey> keySerdes, IKNetSerDes<TValue> valueSerdes, string storageId, StreamsBuilder builder, KStream<K, V> root)
    {
        _kafkaCluster = kafkaCluster;
        _entityType = entityType;
        _keySerdes = keySerdes;
        _valueSerdes = valueSerdes;
        _builder = builder;
        _root = root;
        _storageId = _kafkaCluster.Options.UsePersistentStorage ? storageId : Process.GetCurrentProcess().ProcessName + "-" + storageId;

        StartTopology(_builder, _root);
    }

    private void StartTopology(StreamsBuilder builder, KStream<K, V> root)
    {
        var storeSupplier = _kafkaCluster.Options.UsePersistentStorage ? Stores.PersistentKeyValueStore(_storageId) : Stores.InMemoryKeyValueStore(_storageId);
        var materialized = Materialized<K, V, KeyValueStore<Bytes, byte[]>>.As(storeSupplier);
        root.ToTable(materialized);

        _streams = new(builder.Build(), _kafkaCluster.Options.StreamsOptions(_entityType));

        _errorHandler = new()
        {
            OnHandle = (exception) =>
            {
                _resultException = exception;
                _exceptionSet.Set();
                return StreamThreadExceptionResponse.SHUTDOWN_APPLICATION;
            }
        };

        _stateListener = new()
        {
            OnOnChange = (newState, oldState) =>
            {
                _currentState = newState;
#if DEBUG_PERFORMANCE
                    Infrastructure.KafkaDbContext.ReportString($"StateListener of {_entityType.Name} oldState: {oldState} newState: {newState} on {DateTime.Now:HH:mm:ss.FFFFFFF}");
#endif
                if (_stateChanged != null && !_stateChanged.SafeWaitHandle.IsClosed) _stateChanged.Set();
            }
        };

        _streams.SetUncaughtExceptionHandler(_errorHandler);
        _streams.SetStateListener(_stateListener);

        ThreadPool.QueueUserWorkItem((o) =>
        {
            int waitingTime = Timeout.Infinite;
            Stopwatch watcher = new();
            try
            {
                _resetEvent.Set();
                var index = WaitHandle.WaitAny(new WaitHandle[] { _stateChanged, _exceptionSet });
                if (index == 1) return;
                while (true)
                {
                    index = WaitHandle.WaitAny(new WaitHandle[] { _stateChanged, _dataReceived, _exceptionSet }, waitingTime);
                    if (index == 2) return;
                    if (_currentState.Equals(KafkaStreams.State.CREATED) || _currentState.Equals(KafkaStreams.State.REBALANCING))
                    {
                        if (index == WaitHandle.WaitTimeout)
                        {
#if DEBUG_PERFORMANCE
                                Infrastructure.KafkaDbContext.ReportString($"State of {_entityType.Name}: {_currentState} No handle set within {waitingTime} ms");
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
        _streams.Start();
#if DEBUG_PERFORMANCE
            Infrastructure.KafkaDbContext.ReportString($"KafkaStreamsBaseRetriever on {_entityType.Name} started on {DateTime.Now:HH:mm:ss.FFFFFFF}");
#endif
        _resetEvent.WaitOne(); // wait running state
        if (_resultException != null) throw _resultException;

        keyValueStore ??= _streams?.Store(StoreQueryParameters<ReadOnlyKeyValueStore<K, V>>.FromNameAndType(_storageId, QueryableStoreTypes.KeyValueStore<K, V>()));
    }

    public IEnumerator<ValueBuffer> GetEnumerator()
    {
        if (_resultException != null) throw _resultException;
#if DEBUG_PERFORMANCE
            Infrastructure.KafkaDbContext.ReportString($"Requested KafkaEnumerator for {_entityType.Name} on {DateTime.Now:HH:mm:ss.FFFFFFF}");
#endif
        return new KafkaEnumerator(_kafkaCluster, _entityType, _keySerdes, _valueSerdes, keyValueStore);
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void Dispose()
    {
        _streams?.Close();
        _dataReceived?.Dispose();
        _resetEvent?.Dispose();
        _exceptionSet?.Dispose();
        _errorHandler?.Dispose();
        _stateListener?.Dispose();
        _stateChanged?.Dispose();

        _streams = null;
        _errorHandler = null;
        _stateListener = null;
    }

    class KafkaEnumerator : IEnumerator<ValueBuffer>
    {
        private readonly IKafkaCluster _kafkaCluster;
        private readonly IEntityType _entityType;
        private readonly IKNetSerDes<TKey> _keySerdes;
        private readonly IKNetSerDes<TValue> _valueSerdes;
        private readonly ReadOnlyKeyValueStore<K, V>? _keyValueStore;
        private KeyValueIterator<K, V>? keyValueIterator = null;
        private IEnumerator<KeyValue<K, V>>? keyValueEnumerator = null;

#if DEBUG_PERFORMANCE
            Stopwatch _moveNextSw = new Stopwatch();
            Stopwatch _currentSw = new Stopwatch();
            Stopwatch _valueSerdesSw = new Stopwatch();
            Stopwatch _valueBufferSw = new Stopwatch();
#endif

        public KafkaEnumerator(IKafkaCluster kafkaCluster, IEntityType entityType, IKNetSerDes<TKey> keySerdes, IKNetSerDes<TValue> valueSerdes, ReadOnlyKeyValueStore<K, V>? keyValueStore)
        {
            if (keyValueStore == null) throw new ArgumentNullException(nameof(keyValueStore));
            _kafkaCluster = kafkaCluster ?? throw new ArgumentNullException(nameof(kafkaCluster));
            _entityType = entityType;
            _keySerdes = keySerdes ?? throw new ArgumentNullException(nameof(keySerdes));
            _valueSerdes = valueSerdes ?? throw new ArgumentNullException(nameof(valueSerdes));
            _keyValueStore = keyValueStore;
#if DEBUG_PERFORMANCE
                Infrastructure.KafkaDbContext.ReportString($"KafkaEnumerator for {_entityType.Name} - ApproximateNumEntries {_keyValueStore?.ApproximateNumEntries()}");
#endif
            keyValueIterator = _keyValueStore?.All();
            keyValueEnumerator = keyValueIterator?.ToIEnumerator();
        }

        public ValueBuffer Current
        {
            get
            {
#if DEBUG_PERFORMANCE
                    try
                    {
                        _currentSw.Start();
#endif
                if (keyValueEnumerator != null)
                {
                    var kv = keyValueEnumerator.Current;
                    object? v = kv.value;
#if DEBUG_PERFORMANCE
                        _valueSerdesSw.Start();
#endif
                    TValue entityTypeData = _valueSerdes.DeserializeWithHeaders(null, null, v as byte[]);
#if DEBUG_PERFORMANCE
                        _valueSerdesSw.Stop();
                        _valueBufferSw.Start();
#endif
                    object[] array = null;
                    entityTypeData.GetData(_entityType, ref array);
#if DEBUG_PERFORMANCE
                        _valueBufferSw.Stop();
#endif
                    var data = new ValueBuffer(array);
                    if (data.IsEmpty)
                    {
                        throw new InvalidOperationException("Data is Empty");
                    }
                    return data;
                }
                throw new InvalidOperationException("InvalidEnumerator");
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
                Infrastructure.KafkaDbContext.ReportString($"KafkaEnumerator _moveNextSw: {_moveNextSw.Elapsed} _currentSw: {_currentSw.Elapsed} _valueSerdesSw: {_valueSerdesSw.Elapsed} _valueBufferSw: {_valueBufferSw.Elapsed}");
#endif
            keyValueIterator?.Dispose();
        }

        public bool MoveNext()
        {
#if DEBUG_PERFORMANCE
                try
                {
                    _moveNextSw.Start();
#endif
            return (keyValueEnumerator != null) && keyValueEnumerator.MoveNext();
#if DEBUG_PERFORMANCE
                }
                finally { _moveNextSw.Stop(); }
#endif
        }

        public void Reset()
        {
            keyValueIterator?.Dispose();
            keyValueIterator = _keyValueStore?.All();
            keyValueEnumerator = keyValueIterator?.ToIEnumerator();
        }
    }
}
