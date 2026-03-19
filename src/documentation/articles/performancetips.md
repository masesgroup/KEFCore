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
| Startup time | Instant — empty store | Slower — reads from topics to rebuild |
| Memory usage | Higher — all data in RAM | Lower — RocksDB spills to disk |
| Restart behavior | Full rebuild from topics | Incremental — resumes from last checkpoint |
| Best for | Development, short-lived processes | Long-running production services |

Enable persistent storage for production services that restart frequently:

```csharp
optionsBuilder.UseKEFCore(opt => opt
    .WithBootstrapServers("KAFKA-SERVER:9092")
    .WithPersistentStorage()
);
```

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
