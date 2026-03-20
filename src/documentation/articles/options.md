---
title: Options in KEFCore
_description: Describes the options available in Entity Framework Core provider for Apache Kafka™
---

# KEFCore: options

KEFCore separates its options into two categories: **singleton-scoped** and **context-scoped**. Understanding this separation is important to avoid unexpected Service Provider cache misses or configuration errors at startup.

## Why the separation matters

EF Core caches the internal Service Provider (the DI container that holds provider services) and reuses it across `DbContext` instances that share the same configuration. KEFCore keys this cache on the physical Kafka cluster identity (`ClusterId`, resolved from `BootstrapServers`) plus the singleton options that affect which services are registered.

Two `DbContext` instances that share all singleton options will reuse the same Service Provider — and the same `StreamsManager`. Two instances with different singleton options will create separate Service Providers.

Context-scoped options do not affect the Service Provider cache. They are read per-`DbContext` instance at runtime.

## Singleton options

These options must be consistent across all `DbContext` instances pointing to the same physical Apache Kafka™ cluster. Changing any of these between two contexts on the same cluster will cause a new Service Provider to be created, or a `SingletonOptionChanged` exception if `UseInternalServiceProvider` is in use.

### In hash (affect Service Provider cache key)

| Option | Default | Notes |
|---|---|---|
| `BootstrapServers` | — | Mandatory. Resolved to `ClusterId` for caching |
| `ApplicationId` | — | Mandatory when not using `UseCompactedReplicator`. Unique per process on the cluster |
| `KeySerDesSelectorType` | `DefaultKEFCoreSerDes.DefaultKeySerialization` | Generic type definition |
| `ValueSerDesSelectorType` | `DefaultKEFCoreSerDes.DefaultValueContainerSerialization` | Generic type definition |
| `ValueContainerType` | `DefaultKEFCoreSerDes.DefaultValueContainer` | Generic type definition |
| `UseKeyByteBufferDataTransfer` | `false` | Determines key transport type in Streams topology |
| `UseValueContainerByteBufferDataTransfer` | `false` | Determines value transport type in Streams topology |
| `UseKNetStreams` | `true` | KNet vs plain Kafka Streams backend |
| `UsePersistentStorage` | `false` | RocksDB vs in-memory store supplier |
| `UseCompactedReplicator` | `false` | ⚠️ Deprecated |

### First-wins (singleton but not in hash)

These are cluster-level but do not affect the Service Provider cache key. The values set by the first `DbContext` to initialize the cluster are used for all subsequent contexts.

| Option | Default | Notes |
|---|---|---|
| `UseDeletePolicyForTopic` | `false` | Applied at topic creation time |
| `DefaultNumPartitions` | `1` | Applied at topic creation time — overridable per entity via `KEFCoreTopicPartitionsAttribute` or `HasKEFCoreTopicPartitions()` |
| `DefaultReplicationFactor` | `1` | Applied at topic creation time — overridable per entity via `KEFCoreTopicReplicationFactorAttribute` or `HasKEFCoreTopicReplicationFactor()` |
| `TopicConfig` | `null` | Cluster-level topic configuration — retention can be overridden per entity via `KEFCoreTopicRetentionAttribute` or `HasKEFCoreTopicRetention()` |
| `StreamsConfig` | `null` | Kafka Streams application configuration |
| `ProducerConfig` | `null` | Kafka producer configuration |

## Context-scoped options

These options are specific to each `DbContext` instance and do not affect the Service Provider cache.

| Option | Default | Notes |
|---|---|---|
| `ReadOnlyMode` | `false` | Rejects all write operations for the entire context — individual entities can be marked read-only via `KEFCoreReadOnlyAttribute` or `IsKEFCoreReadOnly()` |
| `DefaultSynchronizationTimeout` | `Timeout.Infinite` | Milliseconds to wait after `SaveChanges` for backend sync; `0` disables sync |
| `UseEnumeratorWithPrefetch` | `true` | Prefetch enumerator for faster enumeration with Streams |
| `UseStorePrefixScan` | `false` | Enables prefix scan globally — overridable per entity via `KEFCoreStoreLookupAttribute` or `HasKEFCoreStoreLookup()` |
| `UseStoreSingleKeyLookup` | `true` | Enables single key look-up globally — overridable per entity |
| `UseStoreKeyRange` | `true` | Enables key range look-up globally — overridable per entity |
| `UseStoreReverse` | `true` | Enables reverse look-up globally — overridable per entity |
| `UseStoreReverseKeyRange` | `true` | Enables reverse key range look-up globally — overridable per entity |

### Deprecated context-scoped options

| Option | Replacement |
|---|---|
| `ManageEvents` | `KEFCoreIgnoreEventsAttribute` / `HasKEFCoreManageEvents()` |
| `UseGlobalTable` | Removed — `ApplicationId` uniqueness per process makes it unnecessary |
| `ConsumerConfig` | No replacement — deprecated with `UseCompactedReplicator` |
| `DefaultConsumerInstances` | No replacement — deprecated with `UseCompactedReplicator` |

## Topic naming and event management

`TopicPrefix`, `ManageEvents`, and topic name configuration are no longer options — they are model conventions resolved at model finalization time. See [conventions](conventions.md) for details.
