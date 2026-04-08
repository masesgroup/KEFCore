---
title: Roadmap of KEFCore
_description: Describes current roadmap of Entity Framework Core provider for Apache Kafka™
---

# KEFCore: roadmap

The roadmap can be synthetized in the following points:

* [x] Create a first working provider starting from the code of InMemory provider
* [x] Extends the first provider with new features able to create Apache Kafka™ Streams topology to retrieve information
* [x] Add external package to manage data serialization
* [x] Add Avro external package to manage data serialization
* [x] Add Protobuf external package to manage data serialization
* [x] Add Templates package
* [x] Separate singleton-scoped from context-scoped options — correct EF Core Service Provider cache behavior (#438)
* [x] Topic naming convention and attributes — `KEFCoreTopicAttribute`, `KEFCoreTopicPrefixAttribute`, fluent API (#417)
* [x] Event management convention — per-entity opt-out via `KEFCoreIgnoreEventsAttribute` (#501)
* [x] ComplexType equality enforcement convention (#494)
* [x] ComplexType converter registration convention (#417)
* [x] `StreamsManager` factory — lifecycle managed per `ApplicationId` via `IKEFCoreCluster` (#501)
* [x] ComplexType LINQ binding — `TryBindMember` extension, `KEFCoreComplexTypeProjectionExpression` (#491)
* [x] `TranslateRightJoin` implementation — EF Core 10 requirement (#514)
* [x] Per-entity topic partitions and replication factor — `KEFCoreTopicPartitionsConvention`, `KEFCoreTopicReplicationFactorConvention` (#417)
* [x] Per-entity topic retention — `KEFCoreTopicRetentionConvention`, `KEFCoreTopicRetentionAttribute` (#417)
* [x] Per-entity read-only mode — `KEFCoreReadOnlyConvention`, `KEFCoreReadOnlyAttribute` (#417)
* [x] Per-entity store lookup optimization flags — `KEFCoreStoreLookupConvention`, `KEFCoreStoreLookupAttribute` (#417)
* [x] Per-entity serialization override — `KEFCoreSerDesConvention`, `KEFCoreSerDesAttribute` (#417)
* [x] Per-entity producer configuration — `KEFCoreProducerConvention`, `KEFCoreProducerAttribute` (#417)
* [x] Kafka transactional producer support — `KEFCoreTransactionalConvention`, `KEFCoreTransactionalAttribute`, `ITransactionalEntityTypeProducer` (#544, #545)
* [x] Per-entity RocksDB lifecycle handler — `KEFCoreRocksDbLifecycleAttributeConvention`, `KEFCoreRocksDbLifecycleAttribute` (#551, #552)
* [x] SSL/SASL broker authentication — `SslConfig`, `SaslConfig`, `SecurityProtocol` singleton options; `SslConfigsBuilder`, `SaslConfigsBuilder` from KNet (#1465)
* [x] Value buffer result cache — `KEFCoreValueBufferCacheConvention`, `KEFCoreValueBufferCacheAttribute`, `CachedValueBufferEnumerable` proxy with TTL and auto-invalidation on `FreshEventChange`
* [ ] Remove deprecated options: `UseCompactedReplicator`, `UseGlobalTable`, `ManageEvents`, `ConsumerConfig`, `DefaultConsumerInstances`
* [ ] Full documentation update for new conventions and options architecture

## Query engine improvements

### Store-level query optimizations (#496, #497)
These optimizations bypass full table scans by delegating work directly to the underlying
Kafka Streams state store (RocksDB). Each optimization is independently controlled via a
`KafkaOptionsExtension` flag.

* [x] **Single key lookup** (`UseStoreSingleKeyLookup`, default: `true`) — PK equality predicate → direct `store.get(key)` instead of full scan + `Where` (#497)
* [x] **Key range scan** (`UseStoreKeyRange`, default: `true`) — PK range predicate (`>=`, `<=`) → `store.range(from, to)` iterator (#497)
* [x] **Reverse iteration** (`UseStoreReverse`, default: `true`) — `Last()`/`LastOrDefault()` without range → native `store.reverseAll()` iterator (#497)
* [x] **Reverse range scan** (`UseStoreReverseKeyRange`, default: `true`) — `Last()`/`LastOrDefault()` with PK range → `store.reverseRange(from, to)` with inverted bounds (#497)
* [ ] **Prefix scan** (`UseStorePrefixScan`, default: `false`, experimental) — `LIKE 'prefix%'` on PK string or partial composite PK equality → `store.prefixScan(prefix, serializer)`. Requires string-serialized keys for correct lexicographic behavior. (stub introduced in #524, implementation tracked in #541)

### KTable-level query optimizations (#496)
These optimizations require Kafka Streams topology awareness. KTable joins operate only
on record keys (PK), require co-partitioned topics, and must be explicitly enabled.

* [ ] **KTable inner/left/right join** (`UseStoreKTableJoin`, default: `false`) — EF Core `Join`/`LeftJoin`/`RightJoin` on PK-to-PK conditions → `KTable.join()` / `KTable.leftJoin()` native API (#537)

### Future / topology-dependent
These features require pre-configured Kafka Streams topology elements (declared before
Streams startup) and are not implementable on-the-fly:

* [ ] Secondary index support via `KGroupedTable` — opt-in per property via fluent API / attribute; enables optimized `Where` on non-PK properties (#538)
* [ ] Aggregate push-down (`Count`, `Sum`, `Min`, `Max`, `Average`) via `KGroupedTable.aggregate()` / `reduce()` (#539)
* [ ] `GroupBy` push-down via `KTable.groupBy()` → `KGroupedTable` (#540)
* [ ] Subquery-to-KTable join — use pre-built KTable results as subquery sources in LINQ
