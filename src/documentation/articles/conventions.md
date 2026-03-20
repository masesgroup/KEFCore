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
