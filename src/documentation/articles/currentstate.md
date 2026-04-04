---
title: Current development state of KEFCore
_description: Describes the current development state of Entity Framework Core provider for Apache Kafka™
---

# KEFCore: development state

The latest release implements these features:

* [x] A working provider based on Apache Kafka™ Streams
* [x] A base package for serialization based on .NET Json serializers
* [x] An external package for serialization based on Apache Avro serializers
* [x] An external package for serialization based on Protobuf serializers
* [x] A package with some templates
* [x] Separation of singleton-scoped and context-scoped options with correct EF Core Service Provider caching
* [x] Topic naming via `KEFCoreTopicNamingConvention` — supports `KEFCoreTopicAttribute`, `KEFCoreTopicPrefixAttribute`, `TableAttribute`, and fluent API (`ToKEFCoreTopic`, `HasKEFCoreTopicPrefix`, `UseKEFCoreTopicPrefix`)
* [x] Event management via `KEFCoreManageEventsConvention` — enabled by default per entity, opt-out via `KEFCoreIgnoreEventsAttribute` or fluent API
* [x] ComplexType equality enforcement via `KEFCoreComplexTypeEquatableConvention` with opt-out via `KEFCoreIgnoreEquatableCheckAttribute`
* [x] ComplexType converter registration via `KEFCoreComplexTypeConverterConvention` — supports `KEFCoreComplexTypeConverterAttribute` and fluent API (`HasKEFCoreComplexTypeConverter`)
* [x] `StreamsManager` lifecycle managed per `ApplicationId` via `IKEFCoreCluster.GetOrCreateStreamsManager`
* [x] Per-entity topic partitions, replication factor, and retention — `KEFCoreTopicPartitionsConvention`, `KEFCoreTopicRetentionConvention`
* [x] Per-entity read-only mode — `KEFCoreReadOnlyConvention`, `KEFCoreReadOnlyAttribute`
* [x] Per-entity store lookup optimization flags — `KEFCoreStoreLookupConvention`, `KEFCoreStoreLookupAttribute`
* [x] Per-entity serialization override — `KEFCoreSerDesConvention`, `KEFCoreSerDesAttribute`
* [x] Per-entity producer configuration — `KEFCoreProducerConvention`, `KEFCoreProducerAttribute`
* [x] Kafka transactional producer support — `KEFCoreTransactionalConvention`, `ITransactionalEntityTypeProducer`, `KEFCoreTransactionManager`
* [x] Per-entity RocksDB lifecycle handler — `KEFCoreRocksDbLifecycleAttributeConvention`, `KEFCoreRocksDbLifecycleAttribute` (requires KNet with `IRocksDbLifecycleHandler`)
* [x] SSL/SASL broker authentication — `SslConfig`, `SaslConfig`, `SecurityProtocol` singleton options applied to all internal Kafka clients
* [x] Value buffer result cache — `KEFCoreValueBufferCacheConvention`, `KEFCoreValueBufferCacheAttribute`, TTL-based `CachedValueBufferEnumerable` with automatic invalidation on writes
