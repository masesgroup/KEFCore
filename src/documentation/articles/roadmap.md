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
* [ ] Remove deprecated options: `UseCompactedReplicator`, `UseGlobalTable`, `ManageEvents`, `ConsumerConfig`, `DefaultConsumerInstances`
* [ ] Full documentation update for new conventions and options architecture
