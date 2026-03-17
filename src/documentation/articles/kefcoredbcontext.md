---
title: KEFCoreDbContext
_description: Describe what is and how use KEFCoreDbContext class from Entity Framework Core provider for Apache Kafka™
---

# KEFCore: KEFCoreDbContext

`KEFCoreDbContext` is a special class which helps to define the `DbContext` and use [Entity Framework Core](https://learn.microsoft.com/ef/core/) provider for [Apache Kafka™](https://kafka.apache.org/):
- `KEFCoreDbContext` inherits from `DbContext`: to define the model, and/or creating the database, see [getting started](https://docs.microsoft.com/ef/core/get-started/) in the docs and [KEFCore usage](usage.md)
- `KEFCoreDbContext` defines the following properties:
  - **KeySerDesSelectorType**: the .NET type to be used to allocate an external serializer for Apache Kafka™ record key
  - **ValueSerDesSelectorType**: the .NET type to be used to allocate an external serializer for Apache Kafka™ record value
  - **ValueContainerType**: the .NET type to be used to allocate an external container class for Apache Kafka™ record value
  - **BootstrapServers**: the server hosting the broker of Apache Kafka™
  - **ApplicationId**: the application identifier used to identify the context
  - **TopicPrefix**: the optional user defined name which is used to prepend every Topic which belongs to the application
  - ~~**DefaultNumPartitions**: the default number of partitions used when topics are created for each entity~~
  - **DefaultReplicationFactor**: the replication factor to use when data are stored in Apache Kafka™
  - **DefaultConsumerInstances**: the consumer instances to be allocated when UseCompactedReplicator is **true**
  - **UsePersistentStorage**: set to **true** to use a persistent storage between multiple application startup
  - **UseEnumeratorWithPrefetch**: set to **true** to prefer enumerator instances able to do a prefetch on data speeding up execution, used if **UseKNetStreams** is **true** and **UseCompactedReplicator** is **false**
  - **UseKeyByteBufferDataTransfer**: set to **true** to prefer <see cref="Java.Nio.ByteBuffer"/> data exchange in serializer instances for keys
  - **UseValueContainerByteBufferDataTransfer**: set to **true** to prefer <see cref="Java.Nio.ByteBuffer"/> data exchange in serializer instances for value container
  - **UseDeletePolicyForTopic**: set to **true** to enable [delete cleanup policy](https://kafka.apache.org/documentation/#topicconfigs_cleanup.policy)
  - ~~**UseCompactedReplicator**: Use `KNetCompactedReplicator` instead of Apache Kafka™ Streams to manage data to or from topics~~
  - **UseKNetStreams**: Setting this property to true the KNet version of Apache Kafka™ Streams is used instead of standard Apache Kafka™ Streams, the property is used if **UseCompactedReplicator** is **false**
  - **UseGlobalTable**: Setting this property to true the engine based on Streams (i.e. **UseCompactedReplicator** is false will use `Org.Apache.Kafka.Streams.Kstream.GlobalKTable` (`MASES.KNet.Streams.Kstream.GlobalKTable` if **UseKNetStreams** is true) instead of `Org.Apache.Kafka.Streams.Kstream.KTable` (`MASES.KNet.Streams.Kstream.KTable` if **UseKNetStreams** is true) to manage local storage of information.
  - ~~**ConsumerConfig**: parameters to use for Producer~~
  - **ProducerConfig**: parameters to use for Producer
  - **StreamsConfig**: parameters to use for Apche Kafka™ Streams application
  - **TopicConfig**: parameters to use on topic creation for each entity
  - **ManageEvents**: Setting this property to true (default) the backend emits events on ChangeTracker
  - **ReadOnlyMode**: Setting this property to true (default is false) if the engine shall reject any write operation, its value will be used to verify if topics has the proper rights AclOperation.WRITE and AclOperation.READ
  - **DefaultSynchronizationTimeout**: The default timeout, expressed in milliseconds, KEFCore will wait for backend to be in-sync with Apache Kafka™ cluster. Setting this property to 0 the synchronization will be disabled
  - **UseStorePrefixScan**: Set this property to true to enable prefix scan in engine, default is false
  - **UseStoreSingleKeyLookup**: Set this property to true to enable single key look-up in engine, default is true
  - **UseStoreKeyRange**: Set this property to true to enable key range look-up in engine, default is true
  - **UseStoreReverse**: Set this property to true to enable reverse look-up in engine, default is true
  - **UseStoreReverseKeyRange**: Set this property to true to reverse key range look-up in engine, default is true

## How to use `KEFCoreDbContext` class

The most simple example of usage can be found in [KEFCore usage](usage.md). By default, `KEFCoreDbContext` automatically manages `OnConfiguring` method of `DbContext`:
- `KEFCoreDbContext` checks the mandatory options like **BootstrapServers** and **DbName**
- `KEFCoreDbContext` setup the options needed to use an Apache Kafka™ cluster:
  - ~~default `ConsumerConfig` can be overridden using **ConsumerConfig** property of `KEFCoreDbContext`~~
  - default `ProducerConfig` can be overridden using **ProducerConfig** property of `KEFCoreDbContext`
  - default `StreamsConfig` can be overridden using **StreamsConfig** property of `KEFCoreDbContext`
  - default `TopicConfig` can be overridden using **TopicConfig** property of `KEFCoreDbContext`

### Default **ConsumerConfig**

Over the [Apache Kafka™ defaults](https://kafka.apache.org/documentation/#consumerconfigs) it applies:

- EnableAutoCommit is **true**
- AutoOffsetReset set to **EARLIEST**
- AllowAutoCreateTopics set to **false**

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
