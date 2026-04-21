/*
 * Copyright (c) 2022-2026 MASES s.r.l.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *  http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
 * Refer to LICENSE for more information.
 */

#nullable enable

using System.Collections;

namespace MASES.EntityFrameworkCore.KNet.Storage.Internal;

/// <summary>
/// In-memory result cache for a Kafka Streams state store entity type.
/// Backed by a <see cref="SortedList{TKey,TValue}"/> keyed on the entity primary key,
/// using an <see cref="IComparer{T}"/> derived from <see cref="IKey"/> at construction
/// time so that ordering matches the store's internal ordering.
/// <para>
/// The cache is populated lazily on the first <em>complete</em> forward enumeration
/// (obtained via <see cref="GetAllPopulating"/>). Once warm, all access paths —
/// forward, reverse, single key, range, reverse range, prefix — are served from the
/// same sorted structure with zero JNI cost.
/// </para>
/// <para>
/// Partial enumerations (caller disposes the enumerator before <c>MoveNext</c> returns
/// <see langword="false"/>) transparently hand off the upstream enumerator to a background
/// task that completes the scan and commits the result to the cache. The caller sees no
/// delay; the cache becomes warm once the background task finishes. A generation counter
/// ensures that data accumulated before an <see cref="Invalidate"/> call is never committed
/// after the invalidation.
/// </para>
/// <para>
/// When <paramref name="ttl"/> is <see cref="TimeSpan.Zero"/> or negative, caching is
/// disabled: <see cref="GetAllPopulating"/> acts as a transparent pass-through and all
/// <c>TryGet*</c> methods return <see langword="null"/> immediately.
/// </para>
/// </summary>
internal sealed class KEFCoreCachedValueBufferStore<TKey>(
    IKey primaryKey,
    IPrincipalKeyValueFactory<TKey> keyValueFactory,
    TimeSpan ttl)
    where TKey : notnull
{
    // ── comparer for composite keys (object?[]) ──────────────────────────────
    private sealed class CompositeKeyComparer(IReadOnlyList<IProperty> properties) : IComparer<TKey>
    {
        private readonly IReadOnlyList<IProperty> _properties = properties;

        public int Compare(TKey? x, TKey? y)
        {
            if (x is object?[] xa && y is object?[] ya)
            {
                for (int i = 0; i < _properties.Count; i++)
                {
                    var c = Comparer<object?>.Default.Compare(xa[i], ya[i]);
                    if (c != 0) return c;
                }
                return 0;
            }
            // fallback for non-array composite keys
            return Comparer<TKey>.Default.Compare(x, y);
        }
    }

    // ── populating enumerable returned from GetAllPopulating ─────────────────
    private sealed class PopulatingEnumerable(
        KEFCoreCachedValueBufferStore<TKey> store,
        IEnumerable<ValueBuffer> source) : IEnumerable<ValueBuffer>
    {
        public IEnumerator<ValueBuffer> GetEnumerator()
            => new PopulatingEnumerator(store, source);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class PopulatingEnumerator : IEnumerator<ValueBuffer>
    {
        private readonly KEFCoreCachedValueBufferStore<TKey> _store;
        private readonly IEnumerator<ValueBuffer> _inner;
        private readonly SortedList<TKey, ValueBuffer> _accumulator;
        private readonly int _capturedGeneration;
        private bool _completed;
        private bool _disposed;

        public PopulatingEnumerator(KEFCoreCachedValueBufferStore<TKey> store, IEnumerable<ValueBuffer> source)
        {
            _store = store;
            _inner = source.GetEnumerator();
            _accumulator = new SortedList<TKey, ValueBuffer>(store._comparer);
            // capture generation before any MoveNext so we can detect intervening Invalidate()
            lock (store._lock) _capturedGeneration = store._generation;
        }

        public ValueBuffer Current => _inner.Current;
        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            if (!_inner.MoveNext())
            {
                _completed = true;
                return false;
            }
            // accumulate: extract key and copy ValueBuffer for independent ownership
            var vb = _inner.Current;
            var key = _store.ExtractKey(vb);
            _accumulator[key] = CopyValueBuffer(vb);
            return true;
        }

        public void Reset() => throw new NotSupportedException();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_completed)
            {
                // full enumeration — commit synchronously and dispose inner
                _inner.Dispose();
                TryCommit();
                return;
            }

            // partial enumeration — hand off _inner to a background task that
            // completes the scan and then commits. _inner must NOT be disposed here.
            var inner = _inner;
            var accumulator = _accumulator;
            var store = _store;
            var capturedGeneration = _capturedGeneration;

            Task.Run(() =>
            {
                try
                {
                    while (inner.MoveNext())
                    {
                        var vb = inner.Current;
                        var key = store.ExtractKey(vb);
                        accumulator[key] = CopyValueBuffer(vb);
                    }
                    // full scan completed in background — try to commit
                    lock (store._lock)
                    {
                        // only commit if no Invalidate() arrived since we started
                        if (store._generation == capturedGeneration)
                        {
                            store._cache = accumulator;
                            store._expiry = DateTime.UtcNow.Add(store._ttl);
                        }
                    }
                }
                catch
                {
                    // background population failure — cache stays cold, no action needed
                }
                finally
                {
                    inner.Dispose();
                }
            });
        }

        private void TryCommit()
        {
            lock (_store._lock)
            {
                if (_store._generation == _capturedGeneration)
                {
                    _store._cache = _accumulator;
                    _store._expiry = DateTime.UtcNow.Add(_store._ttl);
                }
            }
        }

        private static ValueBuffer CopyValueBuffer(in ValueBuffer vb)
        {
            var arr = new object?[vb.Count];
            for (int i = 0; i < arr.Length; i++) arr[i] = vb[i];
            return new ValueBuffer(arr);
        }
    }

    // ── state ─────────────────────────────────────────────────────────────────
    private readonly TimeSpan _ttl = ttl;
    private readonly bool _enabled = ttl > TimeSpan.Zero;
    private readonly IComparer<TKey> _comparer = primaryKey.Properties.Count == 1
            ? Comparer<TKey>.Default
            : new CompositeKeyComparer(primaryKey.Properties);
    private readonly IKey _primaryKey = primaryKey;
    private readonly IPrincipalKeyValueFactory<TKey> _keyValueFactory = keyValueFactory;

    private SortedList<TKey, ValueBuffer>? _cache;
    private DateTime _expiry = DateTime.MinValue;
    private readonly object _lock = new();
    // incremented by Invalidate() — background tasks compare against their captured value
    // to avoid committing data that became stale after a write arrived mid-population
    private int _generation = 0;

    /// <summary><see langword="true"/> when caching is active (TTL &gt; zero).</summary>
    public bool IsEnabled => _enabled;

    // ── invalidation ──────────────────────────────────────────────────────────
    /// <summary>
    /// Marks the cache as expired. The next enumeration will re-fetch from the upstream
    /// source. No-op when caching is disabled.
    /// </summary>
    public void Invalidate()
    {
        if (!_enabled) return;
        lock (_lock)
        {
            _cache = null;
            _expiry = DateTime.MinValue;
            _generation++;
        }
    }

    // ── key extraction ────────────────────────────────────────────────────────
    private TKey ExtractKey(in ValueBuffer vb)
    {
        var props = _primaryKey.Properties;
        var keyValues = new object?[props.Count];
        for (int i = 0; i < props.Count; i++)
            keyValues[i] = vb[props[i].GetIndex()];
        return (TKey)_keyValueFactory.CreateFromKeyValues(keyValues)!;
    }

    // ── cache warm check ──────────────────────────────────────────────────────
    /// <summary>
    /// <see langword="true"/> when the cache is populated and the TTL has not expired.
    /// Use this as a fast guard before constructing key objects — key construction has
    /// a non-trivial cost and should be skipped when the cache is definitely cold.
    /// </summary>
    public bool IsWarm
    {
        get
        {
            if (!_enabled) return false;
            lock (_lock) return _cache != null && DateTime.UtcNow < _expiry;
        }
    }

    // private overload used inside methods that already hold _lock
    private bool IsWarmUnsafe => _cache != null && DateTime.UtcNow < _expiry;

    // ── public access API ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns an <see cref="IEnumerable{ValueBuffer}"/> for a full forward scan.
    /// <list type="bullet">
    ///   <item>Cache disabled — returns <paramref name="sourceFactory"/> directly (pass-through).</item>
    ///   <item>Cache warm — returns <see cref="SortedList{TKey,TValue}.Values"/> directly.</item>
    ///   <item>Cache cold — returns a <see cref="PopulatingEnumerable"/> that populates
    ///         the cache when fully enumerated.</item>
    /// </list>
    /// </summary>
    public IEnumerable<ValueBuffer> GetAllPopulating(Func<IEnumerable<ValueBuffer>> sourceFactory)
    {
        if (!_enabled) return sourceFactory();

        lock (_lock)
        {
            if (IsWarmUnsafe) return _cache!.Values;
            _cache = null;
            _expiry = DateTime.MinValue;
        }
        return new PopulatingEnumerable(this, sourceFactory());
    }

    /// <summary>
    /// Returns all values in reverse order.
    /// Returns <see langword="null"/> when the cache is disabled or cold — caller should
    /// fall back to the upstream source.
    /// </summary>
    public IEnumerable<ValueBuffer>? TryGetAllReverse()
    {
        if (!_enabled) return null;
        lock (_lock)
        {
            if (!IsWarmUnsafe) return null;
            // SortedList.Values is an IList<TValue> backed by an array —
            // reverse by index: O(n), zero allocation
            return ReverseValues(_cache!.Values);
        }
    }

    private static IEnumerable<ValueBuffer> ReverseValues(IList<ValueBuffer> values)
    {
        for (int i = values.Count - 1; i >= 0; i--)
            yield return values[i];
    }

    /// <summary>
    /// Tries to retrieve a single entry by primary key from the cache.
    /// Returns <see langword="false"/> when cache is disabled or cold — caller falls back.
    /// </summary>
    public bool TryGetValue(TKey key, out ValueBuffer valueBuffer)
    {
        if (_enabled)
        {
            lock (_lock)
            {
                if (IsWarmUnsafe)
                    return _cache!.TryGetValue(key, out valueBuffer);
            }
        }
        valueBuffer = default;
        return false;
    }

    /// <summary>
    /// Returns all values whose key falls within [<paramref name="from"/>, <paramref name="to"/>].
    /// Returns <see langword="null"/> when cache is disabled or cold — caller falls back.
    /// </summary>
    public IEnumerable<ValueBuffer>? TryGetRange(TKey from, TKey to)
    {
        if (!_enabled) return null;
        lock (_lock)
        {
            if (!IsWarmUnsafe) return null;
            return RangeValues(_cache!, from, to, _comparer);
        }
    }

    private static List<ValueBuffer> RangeValues(
        SortedList<TKey, ValueBuffer> cache, TKey from, TKey to, IComparer<TKey> comparer)
    {
        var result = new List<ValueBuffer>();
        // SortedList keys are sorted — find start index via binary search
        var keys = cache.Keys;
        int start = LowerBound(keys, from, comparer);
        for (int i = start; i < keys.Count; i++)
        {
            if (comparer.Compare(keys[i], to) > 0) break;
            result.Add(cache.Values[i]);
        }
        return result;
    }

    /// <summary>
    /// Returns all values in reverse order whose key falls within
    /// [<paramref name="from"/>, <paramref name="to"/>].
    /// Returns <see langword="null"/> when cache is disabled or cold — caller falls back.
    /// </summary>
    public IEnumerable<ValueBuffer>? TryGetReverseRange(TKey from, TKey to)
    {
        if (!_enabled) return null;
        lock (_lock)
        {
            if (!IsWarmUnsafe) return null;
            return ReverseRangeValues(_cache!, from, to, _comparer);
        }
    }

    private static List<ValueBuffer> ReverseRangeValues(
        SortedList<TKey, ValueBuffer> cache, TKey from, TKey to, IComparer<TKey> comparer)
    {
        var result = new List<ValueBuffer>();
        var keys = cache.Keys;
        // find the last key <= to
        int end = UpperBound(keys, to, comparer);
        for (int i = end; i >= 0; i--)
        {
            if (comparer.Compare(keys[i], from) < 0) break;
            result.Add(cache.Values[i]);
        }
        return result;
    }

    /// <summary>
    /// Returns all values whose key starts with the given prefix components.
    /// Returns <see langword="null"/> when cache is disabled or cold — caller falls back.
    /// </summary>
    public IEnumerable<ValueBuffer>? TryGetPrefix(TKey prefixKey, int prefixLength)
    {
        if (!_enabled) return null;
        lock (_lock)
        {
            if (!IsWarmUnsafe) return null;
            return PrefixValues(_cache!, prefixKey, prefixLength, _primaryKey.Properties);
        }
    }

    private static List<ValueBuffer> PrefixValues(
        SortedList<TKey, ValueBuffer> cache,
        TKey prefixKey,
        int prefixLength,
        IReadOnlyList<IProperty> properties)
    {
        var result = new List<ValueBuffer>();
        // Extract prefix components for matching
        object?[]? prefixComponents = prefixKey is object?[] arr ? arr : null;

        foreach (var kv in cache)
        {
            if (MatchesPrefix(kv.Key, prefixComponents, prefixLength, properties))
                result.Add(kv.Value);
        }
        return result;
    }

    private static bool MatchesPrefix(
        TKey key, object?[]? prefixComponents, int prefixLength,
        IReadOnlyList<IProperty> properties)
    {
        if (prefixComponents == null)
        {
            // simple key — treat as string prefix if applicable
            return key is string s && prefixComponents != null; // no-op for simple keys
        }

        if (key is not object?[] keyArr) return false;
        for (int i = 0; i < prefixLength && i < keyArr.Length; i++)
        {
            if (!Equals(keyArr[i], prefixComponents[i])) return false;
        }
        return true;
    }

    // ── binary search helpers ─────────────────────────────────────────────────

    /// <summary>Returns the index of the first element >= <paramref name="target"/>.</summary>
    private static int LowerBound(IList<TKey> keys, TKey target, IComparer<TKey> comparer)
    {
        int lo = 0, hi = keys.Count;
        while (lo < hi)
        {
            int mid = (lo + hi) >>> 1;
            if (comparer.Compare(keys[mid], target) < 0) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }

    /// <summary>Returns the index of the last element &lt;= <paramref name="target"/>, or -1.</summary>
    private static int UpperBound(IList<TKey> keys, TKey target, IComparer<TKey> comparer)
    {
        int lo = 0, hi = keys.Count - 1, result = -1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >>> 1;
            if (comparer.Compare(keys[mid], target) <= 0) { result = mid; lo = mid + 1; }
            else hi = mid - 1;
        }
        return result;
    }
}