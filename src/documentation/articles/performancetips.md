---
title: Performance tips for KEFCore
_description: Describes performance tips for Entity Framework Core provider for Apache Kafka™
---

# KEFCore: performance tips

## Query optimization with store access hints

KEFCore can optimize LINQ queries by using targeted Kafka Streams state store access methods instead of full scans. These are context-scoped options — they can differ between `DbContext` instances on the same cluster.

### `UseStoreSingleKeyLookup` (default: `true`)

Enables direct single-key look-up in the state store for queries like:

```csharp
var blog = context.Blogs.Single(b => b.BlogId == 42);
var blog = context.Blogs.Find(42);
```

Without this, the engine falls back to a full store scan. Keep this enabled unless you have a specific reason to disable it.

### `UseStoreKeyRange` (default: `true`)

Enables range queries on the primary key:

```csharp
var blogs = context.Blogs.Where(b => b.BlogId >= 10 && b.BlogId <= 20).ToList();
```

### `UseStorePrefixScan` (default: `false`)

Enables prefix scan for composite keys where the leading key component is known. Useful when the primary key is a composite type and you want to retrieve all records with a given prefix. Disabled by default because it requires the key serializer to support prefix comparison.

### `UseStoreReverse` and `UseStoreReverseKeyRange` (default: `true`)

Enable reverse iteration and reverse range queries:

```csharp
var latest = context.Blogs.OrderByDescending(b => b.BlogId).First();
```

---

## Data transfer: `byte[]` vs `ByteBuffer`

KEFCore supports two data transfer modes between .NET and the JVM for serialization:

- **`byte[]` (default)**: standard managed array, copied across the JVM boundary on each operation
- **`ByteBuffer`**: direct JVM buffer, avoids array copying — lower memory pressure and better throughput under high load

These are singleton options (affect all `DbContext` instances on the cluster):

```csharp
optionsBuilder.UseKEFCore(opt => opt
    .WithBootstrapServers("KAFKA-SERVER:9092")
    .WithKeyByteBufferDataTransfer()           // default: false
    .WithValueContainerByteBufferDataTransfer() // default: false
);
```

Enable `ByteBuffer` when throughput is a priority and your serializer supports it. The default JSON serializer supports both modes. Avro and Protobuf serializers also support `ByteBuffer` via their `Buffered` variants.

> [!NOTE]
> `UseKeyByteBufferDataTransfer` and `UseValueContainerByteBufferDataTransfer` are singleton options — they determine the `DefaultKeySerdeClass` and `DefaultValueSerdeClass` in the Streams topology and must be consistent across all contexts on the same cluster.

---

## Persistent vs in-memory storage

`UsePersistentStorage` (singleton, default: `false`) controls whether the Streams state store uses RocksDB (persistent) or in-memory storage.

| | In-memory (default) | Persistent (RocksDB) |
|---|---|---|
| Startup time | Instant — empty store | Fast — resumes from last checkpoint |
| Memory usage | Higher — all data in RAM | Lower — RocksDB spills to disk |
| Restart behavior | Full rebuild from topics on every restart | Incremental — only new records consumed |
| Best for | Development, short-lived processes | Long-running production services |

Enable persistent storage for production services that restart frequently:

```csharp
using var context = new BloggingContext()
{
    BootstrapServers = "MY-KAFKA-BROKER:9092",
    ApplicationId = "MyApp",
    UsePersistentStorage = true,
};
```

### Restart behavior with persistent storage

When `UsePersistentStorage = true`, the Kafka Streams state store is backed by RocksDB and persisted to disk under a path derived from `ApplicationId` and the topic storage id. On restart:

1. The Streams topology starts and reads the RocksDB checkpoint — the local store already contains all data received in previous runs
2. Kafka Streams resumes consumption from the last committed offset — only records produced after the previous shutdown are consumed and applied
3. The local state is immediately consistent from the application's point of view — no full replay from the beginning of the topic is needed

This means that for long-running services or large datasets, restart time is proportional to the number of new records since the last run, not the total topic size.

> [!IMPORTANT]
> The RocksDB state directory is identified by `ApplicationId` and the storage id derived from the entity topic name. If you change `ApplicationId` or the topic name between restarts, the existing state directory is not found and a full rebuild occurs.

> [!NOTE]
> With `UsePersistentStorage = false` (default), the state store is in-memory and always rebuilt from the beginning of the topic on every startup. For small datasets or development this is fine — for large topics or frequent restarts this can cause significant startup latency.

> [!TIP]
> If you need to force a full rebuild even with persistent storage (e.g. after a schema change), call `context.Database.EnsureDeleted()` before `EnsureCreated()` — this deletes the topics and the local state directory, triggering a clean rebuild on next startup.

## RocksDB configuration

When `UsePersistentStorage = true`, KEFCore uses RocksDB as the Streams state store backend. Per-entity RocksDB configuration is exposed via `KEFCoreRocksDbLifecycleAttributeConvention` — the convention reads a lifecycle handler from attributes or fluent API and registers it with `KNetRocksDBConfigSetter` so that RocksDB options can be tuned per store instance at runtime.

> [!NOTE]
> This feature requires the KNet package version that includes `IRocksDbLifecycleHandler` and `KNetRocksDBConfigSetter` (KNet PR [#1480](https://github.com/masesgroup/KNet/pull/1480)). All new types are under the `Org.Apache.Kafka.Streams.State` namespace.

### Configuration models

Two models are supported:

**Handler type** — implement `IRocksDbLifecycleHandler` (from `Org.Apache.Kafka.Streams.State`) and associate it via attribute or fluent API:

```csharp
using Org.Apache.Kafka.Streams.State;

public class MyRocksDbHandler : IRocksDbLifecycleHandler
{
    public void OnSetConfig(Org.Rocksdb.Options options, IKNetConfigurationFromMap config,
                            IDictionary<string, object> data)
    {
        // configure RocksDB options
        // store managed objects that must outlive the JVM call in data
        var blockCache = new Org.Rocksdb.LRUCache(256 * 1024 * 1024L);
        options.SetBlockBasedTableFactory(new Org.Rocksdb.BlockBasedTableConfig().SetBlockCache(blockCache));
        data["blockCache"] = blockCache; // keep alive — released in OnClose
    }

    public void OnClose(Org.Rocksdb.Options options, IDictionary<string, object> data)
    {
        // dispose resources retained in data
        if (data.TryGetValue("blockCache", out var bc))
            (bc as IDisposable)?.Dispose();
    }
}

// via attribute
[KEFCoreRocksDbLifecycleAttribute(typeof(MyRocksDbHandler))]
[Table("SensorReading")]
public class SensorReading { ... }

// or via fluent API
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<SensorReading>()
                .HasKEFCoreRocksDbLifecycleHandler<MyRocksDbHandler>();
}
```

**Inline callbacks** — pass `onSetConfig` and `onClose` directly without implementing an interface:

```csharp
using Org.Apache.Kafka.Streams.State;

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<SensorReading>()
                .HasKEFCoreRocksDbLifecycle(
                    onSetConfig: (options, config, data) =>
                    {
                        var blockCache = new Org.Rocksdb.LRUCache(256 * 1024 * 1024L);
                        options.SetBlockBasedTableFactory(
                            new Org.Rocksdb.BlockBasedTableConfig().SetBlockCache(blockCache));
                        data["blockCache"] = blockCache;
                    },
                    onClose: (options, data) =>
                    {
                        if (data.TryGetValue("blockCache", out var bc))
                            (bc as IDisposable)?.Dispose();
                    });
}
```

### Lifetime and object retention

The `data` dictionary passed to `onSetConfig` is the per-store lifetime container managed by `KNetRocksDBConfigSetter`. Any managed .NET object that is still referenced by a native RocksDB component after `onSetConfig` returns **must** be stored in `data` to prevent premature GC collection. The same dictionary is passed back to `onClose` when the store is closed — retrieve and dispose those objects there.

> [!IMPORTANT]
> `UsePersistentStorage = true` is required for RocksDB lifecycle callbacks to be invoked. With `UsePersistentStorage = false` the in-memory store is used and callbacks are never called.

> [!NOTE]
> The storage directory is keyed on `StorageIdForTable` which includes the entity topic name and the cluster ID. Changing `ApplicationId`, the topic name, or the cluster will produce a different storage ID and trigger a full rebuild.

---

## Value buffer cache

For entities queried with full scans but written infrequently, KEFCore supports an in-memory result cache that eliminates deserialization and JNI boundary cost on repeated queries. Two independent caches are maintained per entity type — one for forward enumeration and one for reverse — each a `SortedList<TKey, ValueBuffer>` keyed on the entity primary key.

### How it works

Each cache is populated lazily on the first **complete** enumeration in its direction. The `PopulatingEnumerator` accumulates `(key, ValueBuffer)` pairs as the caller iterates. On `Dispose`, if the enumeration was complete (`MoveNext` returned `false`), the `SortedList` is committed to the cache with the configured expiry. If the caller stopped early, the accumulated data is discarded — no partial population.

Once warm, the `SortedList` serves all access paths in that direction:

| Access path | Forward cache | Reverse cache |
|---|---|---|
| `GetValueBuffers()` — full scan | Populates on first complete enumeration; hits on subsequent | — |
| `GetValueBuffersReverse()` — full reverse scan | — | Populates on first complete enumeration; hits on subsequent; cold → native RocksDB reverse iterator |
| Single key lookup | `O(log n)` binary search on `Keys` array, zero JNI | — |
| Key range | Index scan lower→upper bound, zero JNI | — |
| Reverse range | — | Index scan upper→lower bound, zero JNI |
| Prefix scan | Scan from first matching key, zero JNI | — |

The `SortedList` is backed by two parallel arrays (`keys[]` and `values[]`). Reverse iteration is direct index traversal — `O(n)`, zero allocation, no buffering.

### Invalidation

Both caches are invalidated together when new data from the cluster arrives via `FreshEventChange`, which fires after any `SaveChanges` reaches the Streams state store. `IsWarm` is checked as a guard before constructing key objects — key construction has a non-trivial allocation cost and is skipped when the cache is definitely cold.

### Memory considerations

Enabling both forward and reverse caches doubles memory consumption for that entity — each holds an independent copy of all `ValueBuffer` objects. For large entities, enable only the direction that is actually queried frequently.

### Configuration

```csharp
// Attribute — same TTL for forward and reverse (default: reverseTtlSeconds = ttlSeconds)
[KEFCoreValueBufferCacheAttribute(ttlSeconds: 30)]
[Table("Country")]
public class Country { ... }

// Attribute — different TTLs
[KEFCoreValueBufferCacheAttribute(ttlSeconds: 60, reverseTtlSeconds: 15)]
[Table("Product")]
public class Product { ... }

// Attribute — only forward cache
[KEFCoreValueBufferCacheAttribute(ttlSeconds: 60, reverseTtlSeconds: 0)]
[Table("PriceList")]
public class PriceList { ... }

// Attribute — only reverse cache (entity always queried OrderByDescending)
[KEFCoreValueBufferCacheAttribute(ttlSeconds: 0, reverseTtlSeconds: 30)]
[Table("EventLog")]
public class EventLog { ... }

// Fluent API — same TTL for both (reverseTtl defaults to ttl)
modelBuilder.Entity<Country>()
            .HasKEFCoreValueBufferCache(ttl: TimeSpan.FromSeconds(30));

// Fluent API — different TTLs
modelBuilder.Entity<Product>()
            .HasKEFCoreValueBufferCache(
                ttl: TimeSpan.FromSeconds(60),
                reverseTtl: TimeSpan.FromSeconds(15));
```

> [!TIP]
> This cache is most effective for reference data and lookup tables — entities queried frequently but written rarely. For entities with high write frequency the cache is invalidated often and the benefit diminishes.

> [!NOTE]
> A TTL of zero or negative disables that cache direction. The internal `CachedValueBufferStore<TKey>` proxy is always created but acts as a transparent pass-through with no allocation overhead.

> [!IMPORTANT]
> Both caches are shared across all `DbContext` instances on the same cluster and `ApplicationId`. A write from any context invalidates both caches for all contexts sharing the same producer.

---

## Enumerator prefetch

`UseEnumeratorWithPrefetch` (context-scoped, default: `true`) activates enumerator instances that prefetch data from the state store in batches, reducing JVM boundary crossings during enumeration.

This is beneficial when iterating large result sets. Disable it only if you observe ordering or consistency issues during enumeration (which should be rare).

---

## Synchronization timeout

`DefaultSynchronizationTimeout` (context-scoped, default: `Timeout.Infinite`) controls how long KEFCore waits after `SaveChanges` for the Streams state store to acknowledge the new offsets.

- `Timeout.Infinite` — wait indefinitely (safe, but may block under load)
- A positive value (milliseconds) — fail fast if the store does not catch up in time
- `0` — disable synchronization entirely (useful for write-only producers or read-only consumers)

For high-throughput write scenarios where you do not need to immediately read back what you wrote, set `DefaultSynchronizationTimeout = 0`:

```csharp
optionsBuilder.UseKEFCore(opt => opt
    .WithBootstrapServers("KAFKA-SERVER:9092")
    .WithDefaultSynchronizationTimeout(0)
);
```

> [!NOTE]
> Synchronization is only available when event management is enabled for the entity. If `[KEFCoreIgnoreEventsAttribute]` is applied or `HasKEFCoreManageEvents(false)` is set, the synchronization wait is skipped regardless of `DefaultSynchronizationTimeout`.

---

## Event management per entity

By default, KEFCore activates the `TimestampExtractor` for all entities, enabling real-time local state updates and post-`SaveChanges` synchronization. This has a small overhead per consumed record.

For entities that are never written by this process (reference data, lookup tables), disable event management to reduce CPU overhead:

```csharp
[KEFCoreIgnoreEventsAttribute]
public class Country { ... }
```

Or globally, if the application is a pure consumer:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.UseKEFCoreManageEvents(false);
    base.OnModelCreating(modelBuilder);
}
```

See [conventions](conventions.md#event-management-convention) for full details.

## JCOBridge HPA edition

Several intermittent errors in KEFCore under sustained load — including `TimestampExtractor` callback failures ([KEFCore#448](https://github.com/masesgroup/KEFCore/issues/448)) and related JVM↔CLR boundary exceptions — share the same root cause documented in [JCOBridgePublic#24](https://github.com/masesgroup/JCOBridgePublic/issues/24): non-deterministic GC interactions at the JNI boundary under sustained call pressure.

Workarounds at the application level (disabling event management, `REPLACE_THREAD` recovery) are palliatives — they reduce frequency or recover gracefully, but do not eliminate the root cause.

The **JCOBridge HPA (High Performance Application) edition** addresses these failures at the interop layer, providing stable behavior under sustained JVM↔CLR call pressure. If your application:

- runs with event management enabled on high-throughput entities
- uses `ByteBuffer` data transfer (`UseKeyByteBufferDataTransfer`, `UseValueContainerByteBufferDataTransfer`)
- experiences intermittent stream thread failures under load

then the HPA edition is the recommended path. See [jcobridge.com](https://www.jcobridge.com) for licensing and availability.
