---
title: Conventions in KEFCore
_description: Describes the model conventions available in Entity Framework Core provider for Apache Kafka™
---

# KEFCore: conventions

KEFCore uses EF Core model finalization conventions to resolve configuration declared via attributes or fluent API into model annotations. This approach ensures that topic names, event management, and ComplexType configuration are determined at model build time rather than at runtime.

All conventions are registered automatically in `KEFCoreConventionSetBuilder` and require no additional setup.

## Topic naming convention

`KEFCoreTopicNamingConvention` resolves the Kafka topic name for each entity type and stores it as a model annotation. The resolved name is then used consistently throughout the provider — in topic creation, producer routing, and Streams topology — without requiring options to be passed through the call stack.

### Topic name resolution priority

1. `KEFCoreTopicAttribute` applied directly to the entity class
2. `TableAttribute` applied to the entity class, including schema prefix if specified (e.g. `schema.tablename`)
3. The EF Core entity type name (`IReadOnlyTypeBase.Name`), which includes the model namespace

### Topic prefix resolution priority

1. `KEFCoreTopicPrefixAttribute` applied directly to the entity class — a `null` prefix explicitly disables prefixing for that entity
2. `KEFCoreTopicPrefixAttribute` applied to the `DbContext` class
3. `modelBuilder.UseKEFCoreTopicPrefix()` set in `OnModelCreating`
4. No prefix (default)

### Usage examples

```csharp
// Attribute on entity — explicit topic name
[KEFCoreTopicAttribute("my-orders")]
public class Order { ... }

// Attribute on entity — explicit prefix
[KEFCoreTopicPrefixAttribute("production")]
public class Product { ... }

// Attribute on entity — disable prefix for this entity
[KEFCoreTopicPrefixAttribute(null)]
public class InternalLog { ... }

// Attribute on DbContext — prefix for all entities
[KEFCoreTopicPrefixAttribute("myapp")]
public class MyDbContext : KEFCoreDbContext { ... }

// Fluent API — global prefix
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.UseKEFCoreTopicPrefix("myapp");
}

// Fluent API — per-entity topic name
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Order>().ToKEFCoreTopic("my-orders");
    modelBuilder.Entity<Product>().HasKEFCoreTopicPrefix("catalog");
}
```

> [!IMPORTANT]
> If neither `KEFCoreTopicAttribute` nor `TableAttribute` is applied, the topic name falls back to `IReadOnlyTypeBase.Name` which includes the full model namespace. This means a namespace refactoring will change the topic name and break alignment with existing data in the cluster. It is strongly recommended to always use `[Table]` or `[KEFCoreTopicAttribute]` for stable topic names.

## Event management convention

`KEFCoreManageEventsConvention` resolves whether the `TimestampExtractor` is activated for each entity type, enabling real-time tracking updates from the Apache Kafka™ cluster. Event management is **enabled by default** for all entity types.

### Resolution priority

1. `KEFCoreIgnoreEventsAttribute` applied to the entity class — always disables events for that entity
2. `HasKEFCoreManageEvents(false)` applied via fluent API — disables events for that entity
3. `modelBuilder.UseKEFCoreManageEvents(false)` — disables events globally
4. `true` (default — events enabled)

### Usage examples

```csharp
// Disable events for a specific entity via attribute
[KEFCoreIgnoreEventsAttribute]
public class ReadOnlyLookup { ... }

// Disable events for a specific entity via fluent API
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<ReadOnlyLookup>().HasKEFCoreManageEvents(false);
}

// Disable events globally
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.UseKEFCoreManageEvents(false);
}
```

> [!NOTE]
> When event management is disabled for an entity, post-`SaveChanges` synchronization (`EnsureSynchronized`) is not available for that entity because the `TimestampExtractor` is not active.

> [!TIP]
> If you experience intermittent `StreamsException: Fatal user code error in TimestampExtractor callback` errors ([KEFCore#448](https://github.com/masesgroup/KEFCore/issues/448)), this is part of a broader class of non-deterministic JVM↔CLR boundary failures documented in [JCOBridgePublic#24](https://github.com/masesgroup/JCOBridgePublic/issues/24). The `StreamsManager` attempts automatic recovery via `REPLACE_THREAD`, but the definitive solution is the **JCOBridge HPA edition**. As a workaround, disabling event management for the affected entities via `KEFCoreIgnoreEventsAttribute` removes the `TimestampExtractor` and eliminates the error for those entities at the cost of real-time tracking.

## ComplexType equality convention

`KEFCoreComplexTypeEquatableConvention` verifies at model finalization time that all ComplexTypes in the model implement `IEquatable<T>` or override `Equals(object)`. This is required because KEFCore relies on value equality to detect changes in ComplexType properties — without it, reference equality would cause incorrect change detection and unnecessary Kafka writes.

If a ComplexType does not satisfy the requirement, an `InvalidOperationException` is thrown at startup with an actionable message.

### Opt-out

Apply `KEFCoreIgnoreEquatableCheckAttribute` to a ComplexType class to suppress the check when value equality is guaranteed by other means:

```csharp
[KEFCoreIgnoreEquatableCheckAttribute]
public class MySpecialAddress { ... }
```

## ComplexType converter convention

`KEFCoreComplexTypeConverterConvention` registers `IComplexTypeConverter` implementations declared via attribute or fluent API into `IComplexTypeConverterFactory` at model finalization time, making the converter available to the serialization subsystem without any additional startup code.

### Usage examples

```csharp
// Attribute on ComplexType class
[KEFCoreComplexTypeConverterAttribute(typeof(AddressConverter))]
public class Address { ... }

// Fluent API on ComplexPropertyBuilder
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Invoice>()
                .ComplexProperty(i => i.ShippingAddress)
                .HasKEFCoreComplexTypeConverter<AddressConverter>();
}
```

The converter type must implement `IComplexTypeConverter`. If declared via fluent API, it takes precedence over the attribute.

## Topic partitions and replication factor convention

`KEFCoreTopicPartitionsConvention` resolves per-entity partition and replication factor configuration at model finalization time. The resolved values are used by `EnsureCreated` when creating the Kafka topic for each entity, overriding the global `DefaultNumPartitions` and `DefaultReplicationFactor` singleton options.

### Usage examples

```csharp
// Attribute — high-throughput entity needs more partitions
[KEFCoreTopicPartitionsAttribute(numPartitions: 12)]
[KEFCoreTopicReplicationFactorAttribute(replicationFactor: 3)]
[Table("SensorReading")]
public class SensorReading { ... }

// Low-traffic reference data — single partition is enough
[KEFCoreTopicPartitionsAttribute(numPartitions: 1)]
[Table("Country")]
public class Country { ... }

// Fluent API
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<SensorReading>()
                .HasKEFCoreTopicPartitions(12)
                .HasKEFCoreTopicReplicationFactor(3);
}
```

> [!NOTE]
> Partition and replication factor are applied only at topic creation time (`EnsureCreated`). Changing these values after the topic exists has no effect — use Kafka admin tools to alter existing topics.

## Topic retention convention

`KEFCoreTopicRetentionConvention` resolves per-entity topic retention configuration at model finalization time. The resolved values override the global `TopicConfig` retention settings for that entity's topic.

### Usage examples

```csharp
// Attribute — keep only last 7 days of sensor data
[KEFCoreTopicRetentionAttribute(retentionMs: 7 * 24 * 60 * 60 * 1000L)]
[Table("SensorReading")]
public class SensorReading { ... }

// Attribute — limit topic size to 500 MB
[KEFCoreTopicRetentionAttribute(retentionBytes: 500 * 1024 * 1024L)]
[Table("EventLog")]
public class EventLog { ... }

// Fluent API
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<SensorReading>()
                .HasKEFCoreTopicRetention(retentionMs: 7 * 24 * 60 * 60 * 1000L);
}
```

> [!NOTE]
> Retention is applied only at topic creation time. Use `-1` for either parameter to leave that setting at the cluster default.

## Read-only convention

`KEFCoreReadOnlyConvention` resolves per-entity read-only configuration at model finalization time. Entities marked as read-only will have their `SaveChanges` entries skipped and logged as warnings — valid entries from other entity types in the same `SaveChanges` call are still committed.

This differs from `ReadOnlyMode` on the context, which blocks all writes for the entire context. Per-entity read-only is useful for mixed models where some entities are immutable reference data consumed from external topics.

### Resolution priority

1. `KEFCoreReadOnlyAttribute` applied to the entity class
2. `IsKEFCoreReadOnly()` applied via fluent API
3. Context-level `ReadOnlyMode = true` — applies to all entities

### Usage examples

```csharp
// Attribute — reference data that should never be written by this process
[KEFCoreReadOnlyAttribute]
[Table("Country")]
public class Country { ... }

// Fluent API
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Country>().IsKEFCoreReadOnly();
}
```

> [!NOTE]
> When a read-only entity is included in a `SaveChanges` call, the entry is skipped and a warning is logged via both `InfrastructureLogger` (per-entry) and `updateLogger` (aggregated summary). No exception is thrown — the remaining entries are committed normally.

## Store lookup convention

`KEFCoreStoreLookupConvention` resolves per-entity Kafka Streams state store query optimization flags at model finalization time. The resolved values override the context-level `UseStore*` options for queries on that entity type.

This allows enabling expensive optimizations (e.g. prefix scan) only for the specific entities that benefit from them, rather than globally.

### Usage examples

```csharp
// Attribute — enable prefix scan for this entity, disable unused range lookups
[KEFCoreStoreLookupAttribute(UseStorePrefixScan = true, UseStoreKeyRange = false, UseStoreReverseKeyRange = false)]
[Table("SensorReading")]
public class SensorReading { ... }

// Fluent API — only override the flags you need
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<SensorReading>()
                .HasKEFCoreStoreLookup(prefixScan: true, keyRange: false);
}
```

> [!TIP]
> Only override the flags relevant to the actual query patterns used for that entity. Enabling unused optimizations adds overhead to query planning without benefit. See [performance tips](performancetips.md#store-query-optimizations) for guidance on which flags to enable.

## Serialization convention

`KEFCoreSerDesConvention` resolves per-entity serialization type overrides at model finalization time. The resolved types are used when creating serializer instances for that entity, overriding the global `KeySerDesSelectorType`, `ValueSerDesSelectorType` and `ValueContainerType` singleton options.

This is useful when a model contains entities with heterogeneous serialization needs — for example mixing JSON and Avro entities on the same cluster, or using a custom serializer for a specific high-throughput entity.

### Usage examples

```csharp
// Attribute — use Avro for this entity, JSON for everything else
[KEFCoreSerDesAttribute(
    keySerDesSelectorType: typeof(AvroKEFCoreSerDes.Key.BinaryRaw<>),
    valueSerDesSelectorType: typeof(AvroKEFCoreSerDes.ValueContainer.BinaryRaw<>),
    valueContainerType: typeof(AvroValueContainer<>))]
[Table("SensorReading")]
public class SensorReading { ... }

// Attribute — override only the value container, inherit key serializer from context
[KEFCoreSerDesAttribute(valueContainerType: typeof(MyCustomValueContainer<>))]
[Table("Order")]
public class Order { ... }

// Fluent API
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<SensorReading>()
                .HasKEFCoreSerDes(
                    keySerDesSelectorType: typeof(AvroKEFCoreSerDes.Key.BinaryRaw<>),
                    valueSerDesSelectorType: typeof(AvroKEFCoreSerDes.ValueContainer.BinaryRaw<>),
                    valueContainerType: typeof(AvroValueContainer<>));
}
```

> [!IMPORTANT]
> All serialization types must be open generic type definitions (e.g. `typeof(MySerDes<>)`). Closed generic types will throw `ArgumentException` at model build time.

> [!NOTE]
> `UseKeyByteBufferDataTransfer` and `UseValueContainerByteBufferDataTransfer` are singleton options that control the JVM transport layer and apply globally to the Streams topology — they cannot be overridden per entity. Only the serializer selector types and value container type can be overridden.

## Producer configuration convention

`KEFCoreProducerConvention` resolves per-entity producer configuration overrides at model finalization time. The resolved configuration is used when creating the `KNetProducer` instance for that entity's topic, merging the global `ProducerConfig` singleton option with per-entity overrides. Per-entity values take precedence.

This allows tuning producer behavior per entity — for example using higher throughput settings for high-volume entities and stronger reliability guarantees for critical ones.

### Usage examples

```csharp
// High-throughput entity — batch more aggressively, compress, relax acks
[KEFCoreProducerAttribute(
    Acks = ProducerConfigBuilder.AcksTypes.One,
    LingerMs = 50,
    BatchSize = 65536,
    CompressionType = ProducerConfigBuilder.CompressionTypes.Lz4)]
[Table("SensorReading")]
public class SensorReading { ... }

// Critical entity — strongest delivery guarantees
[KEFCoreProducerAttribute(
    Acks = ProducerConfigBuilder.AcksTypes.All,
    Retries = 10,
    MaxInFlightRequestsPerConnection = 1,
    DeliveryTimeoutMs = 300000)]
[Table("FinancialTransaction")]
public class FinancialTransaction { ... }

// Fluent API — only override what you need
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<SensorReading>()
                .HasKEFCoreProducer(lingerMs: 50, compressionType: ProducerConfigBuilder.CompressionTypes.Lz4);

    modelBuilder.Entity<FinancialTransaction>()
                .HasKEFCoreProducer(acks: ProducerConfigBuilder.AcksTypes.All, retries: 10);
}
```

> [!NOTE]
> Only explicitly set properties override the global `ProducerConfig`. Omitted properties inherit the global defaults set via `KEFCoreDbContext.ProducerConfig` or `ProducerOptionsBuilder()`.

> [!TIP]
> When setting `Retries` > 0, also set `MaxInFlightRequestsPerConnection = 1` to preserve message ordering on retry. See [Kafka producer documentation](https://kafka.apache.org/documentation/#producerconfigs) for the full list of producer configuration options.

## Transactional producer convention

`KEFCoreTransactionalConvention` resolves per-entity Kafka transaction group configuration at model finalization time. Entity types assigned to the same group share a single transactional `KafkaProducer` with exactly-once semantics — writes from a `SaveChanges` call involving those entities are committed or aborted atomically at the Kafka level.

Entity types without this attribute use the standard non-transactional producer path unchanged.

### How it works

- Each transaction group gets a dedicated `KafkaProducer` with `transactional.id = "{ApplicationId}.{transactionGroup}"`, stable across application restarts
- `BeginTransaction()` is lazy — the Kafka transaction starts only when `SaveChanges` identifies which groups are actually involved in the current operation
- After `SaveChanges`, the user calls `tx.Commit()` or `tx.Rollback()` to commit or abort all groups atomically
- `EnsureSynchronized` works correctly — `PartitionOffsetWritten` is called only after `CommitTransaction()`, when records become visible to the Streams consumer

### Usage examples

```csharp
// Attribute — Order and OrderLine participate in the same Kafka transaction
[KEFCoreTransactionalAttribute(transactionGroup: "OrderGroup")]
[Table("Order")]
public class Order { ... }

[KEFCoreTransactionalAttribute(transactionGroup: "OrderGroup")]
[Table("OrderLine")]
public class OrderLine { ... }

// Fluent API
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Order>().HasKEFCoreTransactionGroup("OrderGroup");
    modelBuilder.Entity<OrderLine>().HasKEFCoreTransactionGroup("OrderGroup");
}

// Usage — standard EF Core transaction pattern
KEFCore.CreateGlobalInstance();

using var context = new OrderContext()
{
    BootstrapServers = "MY-KAFKA-BROKER:9092",
    ApplicationId = "MyApp",
};
context.Database.EnsureCreated();

using var tx = context.Database.BeginTransaction();
try
{
    context.Add(new Order { ... });
    context.Add(new OrderLine { ... });
    context.SaveChanges();
    tx.Commit();   // → CommitTransaction() on the Kafka transactional producer
}
catch
{
    tx.Rollback(); // → AbortTransaction() — records not visible to consumers
    throw;
}
```

> [!IMPORTANT]
> All entity types in a transaction group must be part of the same `SaveChanges` call. Kafka transactions do not span multiple `SaveChanges` calls.

> [!NOTE]
> The transactional producer is automatically configured with the settings required by Kafka for exactly-once semantics:
> - `transactional.id = "{ApplicationId}.{transactionGroup}"`
> - `acks = all`
> - `enable.idempotence = true`
> - `retries = Int32.MaxValue`
> - `max.in.flight.requests.per.connection = 1`
>
> These settings cannot be overridden via `KEFCoreProducerAttribute` or `HasKEFCoreProducer()` for transactional producers.

> [!NOTE]
> `UseKeyByteBufferDataTransfer` and `UseValueContainerByteBufferDataTransfer` are singleton options — the transactional producer type (`byte[]` vs `ByteBuffer`) follows the global setting and cannot be overridden per group.

> [!NOTE]
> Consumers reading from topics written by transactional producers must set `isolation.level = read_committed` to correctly filter aborted transactions. Verify the `StreamsConfig` for the consuming application.

## RocksDB lifecycle convention

`KEFCoreRocksDbLifecycleAttributeConvention` resolves per-entity RocksDB lifecycle handler configuration at model finalization time. The resolved handler is registered with `KNetRocksDBConfigSetter` and invoked when Kafka Streams configures and closes the RocksDB state store for that entity's topic.

This convention only applies when `UsePersistentStorage = true`.

### Resolution priority

1. `KEFCoreRocksDbLifecycleAttribute` applied to the entity class — specifies a handler type
2. `HasKEFCoreRocksDbLifecycleHandler()` or `HasKEFCoreRocksDbLifecycle()` via fluent API

### Usage examples

```csharp
// Attribute — handler type
[KEFCoreRocksDbLifecycleAttribute(typeof(MyRocksDbHandler))]
[Table("SensorReading")]
public class SensorReading { ... }

// Fluent API — handler type
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<SensorReading>()
                .HasKEFCoreRocksDbLifecycleHandler<MyRocksDbHandler>();
}

// Fluent API — handler instance
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<SensorReading>()
                .HasKEFCoreRocksDbLifecycleHandler(new MyRocksDbHandler());
}

// Fluent API — inline callbacks
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<SensorReading>()
                .HasKEFCoreRocksDbLifecycle(
                    onSetConfig: (options, config, data) => { ... },
                    onClose: (options, data) => { ... });
}
```

See [performance tips](performancetips.md#rocksdb-configuration) for full details on the lifecycle model and object retention rules.

## Value buffer cache convention

`KEFCoreValueBufferCacheConvention` resolves per-entity in-memory result cache configuration at model finalization time. When active, the first **complete** enumeration of `GetValueBuffers()` or `GetValueBuffersReverse()` populates an internal list with the deserialized `ValueBuffer` objects. Subsequent full scans within the TTL window are served from that list — zero JNI cost, zero deserialization overhead.

The cache is stored in `EntityTypeProducer` (singleton per cluster and `ApplicationId`) and is therefore shared across all `DbContext` instances using the same cluster.

### When the cache is populated vs bypassed

| Access path | Cache behavior |
|---|---|
| `GetValueBuffers()` — full scan | Populates cache on first complete enumeration; hits cache on subsequent full scans within TTL |
| `GetValueBuffersReverse()` — full reverse scan | Independent cache with its own TTL; same population/hit rules |
| Single key lookup (`UseStoreSingleKeyLookup`) | Always bypasses cache — goes directly to state store |
| Key range scan (`UseStoreKeyRange`, `UseStoreReverseKeyRange`) | Always bypasses cache — result depends on range parameters |
| Prefix scan (`UseStorePrefixScan`) | Always bypasses cache |

**Partial enumeration** (e.g. `First()`, `Take(3)`, any LINQ operator that stops early) **never populates the cache**. If the enumerator is disposed before `MoveNext()` returns `false`, the accumulated list is discarded — the next call will re-fetch from the state store. This is intentional: a partial list would produce incorrect results for subsequent full-scan queries.

### Invalidation

The cache is invalidated automatically when new data arrives from the Kafka cluster via `FreshEventChange` — i.e. after any `SaveChanges` that writes to that entity's topic reaches the Streams state store. `EnsureSynchronized` can be used to wait for the state store to be in sync before querying; the cache is invalidated before the wait completes.

Both forward and reverse caches are invalidated together on any write.

### Resolution priority

1. `KEFCoreValueBufferCacheAttribute` applied to the entity class
2. `HasKEFCoreValueBufferCache()` applied via fluent API

### Usage examples

```csharp
// Attribute — cache forward and reverse scans for 30 seconds
[KEFCoreValueBufferCacheAttribute(ttlSeconds: 30)]
[Table("Country")]
public class Country { ... }

// Attribute — different TTL for forward and reverse
[KEFCoreValueBufferCacheAttribute(ttlSeconds: 60, reverseTtlSeconds: 30)]
[Table("Product")]
public class Product { ... }

// Attribute — cache only forward scan, disable reverse cache
[KEFCoreValueBufferCacheAttribute(ttlSeconds: 60, reverseTtlSeconds: 0)]
[Table("PriceList")]
public class PriceList { ... }

// Fluent API — same TTL for both directions
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Country>()
                .HasKEFCoreValueBufferCache(ttl: TimeSpan.FromSeconds(30));
}

// Fluent API — different TTL for forward and reverse
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Product>()
                .HasKEFCoreValueBufferCache(
                    ttl: TimeSpan.FromSeconds(60),
                    reverseTtl: TimeSpan.FromSeconds(30));
}
```

> [!TIP]
> This cache is most effective for entities that are queried frequently with full scans but written infrequently — reference data, lookup tables, configuration entities. For entities with high write frequency the cache will be invalidated often and the benefit diminishes.

> [!NOTE]
> A `TTL` of zero or negative disables caching for that direction — the proxy still exists but acts as a transparent pass-through with no allocation overhead. When neither attribute nor fluent API is applied, TTL defaults to zero and the proxy is always a pass-through.

> [!IMPORTANT]
> The cache operates at the `EntityTypeProducer` level — it is shared across all `DbContext` instances on the same cluster and `ApplicationId`. This means a write from one context will invalidate the cache for all contexts sharing the same producer. This is the correct behavior for consistency.
