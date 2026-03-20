---
title: KEFCoreDbContext
_description: Describe what is and how use KEFCoreDbContext class from Entity Framework Core provider for Apache Kafka™
---

# KEFCore: KEFCoreDbContext

`KEFCoreDbContext` is a special class which helps to define the `DbContext` and use [Entity Framework Core](https://learn.microsoft.com/ef/core/) provider for [Apache Kafka™](https://kafka.apache.org/):
- `KEFCoreDbContext` inherits from `DbContext`: to define the model, and/or creating the database, see [getting started](https://docs.microsoft.com/ef/core/get-started/) in the docs and [KEFCore usage](usage.md)

## Singleton vs context-scoped options

KEFCore separates options into two categories:

**Singleton options** are shared across all `DbContext` instances pointing to the same physical Apache Kafka™ cluster (identified by `ClusterId`). They contribute to the EF Core Service Provider cache key — two contexts with different singleton options will use separate Service Providers. These include serialization types, backend architecture choices, and cluster-level configuration.

**Context-scoped options** are specific to each `DbContext` instance and do not affect Service Provider caching. They control runtime behavior such as synchronization timeouts and query optimization hints.

## Singleton options

The following options are singleton-scoped and must be consistent across all `DbContext` instances sharing the same cluster:

- **KeySerDesSelectorType**: the .NET type to be used to allocate an external serializer for Apache Kafka™ record key — overridable per entity via `KEFCoreSerDesAttribute` or `HasKEFCoreSerDes()`
- **ValueSerDesSelectorType**: the .NET type to be used to allocate an external serializer for Apache Kafka™ record value — overridable per entity
- **ValueContainerType**: the .NET type to be used to allocate an external container class for Apache Kafka™ record value — overridable per entity
- **UseKeyByteBufferDataTransfer**: set to **true** to prefer `Java.Nio.ByteBuffer` data exchange in serializer instances for keys
- **UseValueContainerByteBufferDataTransfer**: set to **true** to prefer `Java.Nio.ByteBuffer` data exchange in serializer instances for value containers
- **BootstrapServers**: the server hosting the broker of Apache Kafka™ — used to resolve the `ClusterId` which is the actual Service Provider cache key
- **ApplicationId**: the application identifier used for the Apache Kafka™ Streams topology — must be unique per process on the same cluster
- **UseKNetStreams**: set to **true** (default) to use the KNet version of Apache Kafka™ Streams instead of standard Apache Kafka™ Streams
- **UsePersistentStorage**: set to **true** to use persistent storage (RocksDB) between multiple application startups; set to **false** (default) for in-memory storage
- **UseDeletePolicyForTopic**: set to **true** to enable [delete cleanup policy](https://kafka.apache.org/documentation/#topicconfigs_cleanup.policy) on topic creation
- **DefaultNumPartitions**: the default number of partitions used when topics are created for each entity (first-wins per cluster) — overridable per entity via `KEFCoreTopicPartitionsAttribute` or `HasKEFCoreTopicPartitions()`
- **DefaultReplicationFactor**: the replication factor to use when topics are created (first-wins per cluster) — overridable per entity via `KEFCoreTopicReplicationFactorAttribute` or `HasKEFCoreTopicReplicationFactor()`
- **ProducerConfig**: parameters to use for the Apache Kafka™ producer (cluster-level, first-wins) — individual producer settings can be overridden per entity via `KEFCoreProducerAttribute` or `HasKEFCoreProducer()`
- **StreamsConfig**: parameters to use for the Apache Kafka™ Streams application (cluster-level, first-wins)
- **TopicConfig**: parameters to use on topic creation for each entity (cluster-level, first-wins) — retention can be overridden per entity via `KEFCoreTopicRetentionAttribute` or `HasKEFCoreTopicRetention()`
- ~~**UseCompactedReplicator**~~: deprecated, will be removed in a future release
- ~~**DefaultConsumerInstances**~~: deprecated, will be removed in a future release
- ~~**ConsumerConfig**~~: deprecated, will be removed in a future release

> [!NOTE]
> Options marked as **first-wins** are singleton but do not affect the Service Provider cache key — the values set by the first `DbContext` to initialize the cluster are used for all subsequent contexts on the same cluster.

## Context-scoped options

The following options are scoped to each `DbContext` instance:

- **ReadOnlyMode**: set to **true** (default is **false**) to reject any write operation for the entire context; the engine will also verify that topics have proper `AclOperation.READ` rights — individual entities can be marked read-only via `KEFCoreReadOnlyAttribute` or `IsKEFCoreReadOnly()`
- **DefaultSynchronizationTimeout**: the default timeout in milliseconds KEFCore waits for the backend to be in-sync with the Apache Kafka™ cluster after a `SaveChanges`; set to `Timeout.Infinite` (default) to wait indefinitely, or `0` to disable synchronization
- **UseEnumeratorWithPrefetch**: set to **true** (default) to prefer enumerator instances that prefetch data, speeding up enumeration when using Apache Kafka™ Streams
- **UseStorePrefixScan**: set to **true** to enable prefix scan in the engine (default is **false**) — overridable per entity via `KEFCoreStoreLookupAttribute` or `HasKEFCoreStoreLookup()`
- **UseStoreSingleKeyLookup**: set to **true** (default) to enable single key look-up in the engine — overridable per entity
- **UseStoreKeyRange**: set to **true** (default) to enable key range look-up in the engine — overridable per entity
- **UseStoreReverse**: set to **true** (default) to enable reverse look-up in the engine — overridable per entity
- **UseStoreReverseKeyRange**: set to **true** (default) to enable reverse key range look-up in the engine — overridable per entity
- ~~**UseGlobalTable**~~: deprecated — see [topic naming and event management conventions](conventions.md) for the recommended approach
- ~~**ManageEvents**~~: deprecated — event management is now enabled by default for all entity types; use `KEFCoreIgnoreEventsAttribute` or `HasKEFCoreManageEvents(false)` to disable it per entity

## Topic naming conventions

Topic names are no longer configured via a `TopicPrefix` option. Instead, KEFCore resolves the topic name for each entity at model finalization time using `KEFCoreTopicNamingConvention`. See [conventions](conventions.md) for the full resolution priority.

In short:
- Apply `[KEFCoreTopicAttribute("my-topic")]` on the entity class to set the topic name explicitly
- Apply `[KEFCoreTopicPrefixAttribute("myprefix")]` on the entity class or the `DbContext` class to set a prefix
- Call `modelBuilder.UseKEFCoreTopicPrefix("myprefix")` in `OnModelCreating` for a global prefix
- Call `modelBuilder.Entity<T>().ToKEFCoreTopic("my-topic")` for a per-entity override

## Event management conventions

By default, KEFCore enables event management (i.e. `TimestampExtractor` activation for real-time change tracking) for all entity types via `KEFCoreManageEventsConvention`. To opt out:

- Apply `[KEFCoreIgnoreEventsAttribute]` on the entity class to disable events for that entity
- Call `modelBuilder.Entity<T>().HasKEFCoreManageEvents(false)` for a per-entity override
- Call `modelBuilder.UseKEFCoreManageEvents(false)` to disable events globally

## Per-entity topic and store conventions

Several context-level options can be overridden per entity type via conventions, allowing fine-grained control without affecting the global configuration:

- **Kafka transactions** — `KEFCoreTransactionalAttribute` or `HasKEFCoreTransactionGroup()` to assign entity types to a transaction group, enabling exactly-once semantics for atomic writes across multiple entity types
- **Producer configuration** — `KEFCoreProducerAttribute` or `HasKEFCoreProducer()` to override individual producer settings (`Acks`, `LingerMs`, `BatchSize`, `CompressionType`, `Retries`, etc.) per entity, enabling mixed throughput/reliability profiles on the same cluster
- **Serialization types** — `KEFCoreSerDesAttribute` or `HasKEFCoreSerDes()` to override `KeySerDesSelectorType`, `ValueSerDesSelectorType` and `ValueContainerType` per entity, enabling mixed serialization formats on the same cluster
- **Topic partitions and replication factor** — `KEFCoreTopicPartitionsAttribute`, `KEFCoreTopicReplicationFactorAttribute`, or fluent API `HasKEFCoreTopicPartitions()` / `HasKEFCoreTopicReplicationFactor()`
- **Topic retention** — `KEFCoreTopicRetentionAttribute` or `HasKEFCoreTopicRetention()` to override `RetentionBytes` and `RetentionMs` per entity
- **Read-only** — `KEFCoreReadOnlyAttribute` or `IsKEFCoreReadOnly()` to prevent writes for a specific entity type while allowing writes for others in the same `SaveChanges` call
- **Store lookup optimizations** — `KEFCoreStoreLookupAttribute` or `HasKEFCoreStoreLookup()` to enable or disable specific query optimization paths per entity

See [conventions](conventions.md) for full documentation and examples.

## How to use `KEFCoreDbContext` class

The most simple example of usage can be found in [KEFCore usage](usage.md). By default, `KEFCoreDbContext` automatically manages the `OnConfiguring` method of `DbContext`:
- `KEFCoreDbContext` checks the mandatory options like **BootstrapServers** and **ApplicationId**
- `KEFCoreDbContext` sets up the options needed to use an Apache Kafka™ cluster:
  - default `ProducerConfig` can be overridden using **ProducerConfig** property
  - default `StreamsConfig` can be overridden using **StreamsConfig** property
  - default `TopicConfig` can be overridden using **TopicConfig** property

Kafka transactional producers are supported via `Database.BeginTransaction()` when entity types are assigned to a transaction group via `KEFCoreTransactionalAttribute` or `HasKEFCoreTransactionGroup()`. The standard EF Core transaction pattern applies — `tx.Commit()` or `tx.Rollback()` maps to `CommitTransaction()`/`AbortTransaction()` on the Kafka transactional producer. See [conventions](conventions.md#transactional-producer-convention) for full details.

### Default **ProducerConfig**

Does not change anything over the [Apache Kafka™ defaults](https://kafka.apache.org/documentation/#producerconfigs)

### Default **StreamsConfig**

Does not change anything over the [Apache Kafka™ defaults](https://kafka.apache.org/documentation/#streamsconfigs)

### Default **TopicConfig**

Over the [Apache Kafka™ defaults](https://kafka.apache.org/documentation/#topicconfigs) it applies:

- DeleteRetentionMs set to 100 ms
- MinCleanableDirtyRatio set to 0.01
- SegmentMs set to 100 ms
- RetentionBytes set to 1073741824 bytes (1 Gb)
