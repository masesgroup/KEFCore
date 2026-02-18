---
title: KafkaDbContext
_description: Describe what is and how use KafkaDbContext class from Entity Framework Core provider for Apache Kafka™
---

# KEFCore: KafkaDbContext

`KafkaDbContext` is a special class which helps to define the `DbContext` and use [Entity Framework Core](https://learn.microsoft.com/ef/core/) provider for [Apache Kafka™](https://kafka.apache.org/):
- `KafkaDbContext` inherits from `DbContext`: to define the model, and/or creating the database, see [getting started](https://docs.microsoft.com/ef/core/get-started/) in the docs and [KEFCore usage](usage.md)
- `KafkaDbContext` defines the following properties:
  - **KeySerDesSelectorType**: the .NET type to be used to allocate an external serializer for Apache Kafka™ record key
  - **ValueSerDesSelectorType**: the .NET type to be used to allocate an external serializer for Apache Kafka™ record value
  - **ValueContainerType**: the .NET type to be used to allocate an external container class for Apache Kafka™ record value
  - **UseNameMatching**: set to **false** to avoid Entity matching based on Name
  - **BootstrapServers**: the server hosting the broker of Apache Kafka™
  - **ApplicationId**: the application identifier used to identify the context
  - **DatabaseName**: the user defined name which declares the database name, it is used to prepend every Topic which belongs to this database
  - **DefaultNumPartitions**: the default number of partitions used when topics are created for each entity
  - **DefaultReplicationFactor**: the replication factor to use when data are stored in Apache Kafka™
  - **DefaultConsumerInstances**: the consumer instances to be allocated when UseCompactedReplicator is **true**
  - **UsePersistentStorage**: set to **true** to use a persistent storage between multiple application startup
  - **UseEnumeratorWithPrefetch**: set to **true** to prefer enumerator instances able to do a prefetch on data speeding up execution, used if **UseKNetStreams** is **true** and **UseCompactedReplicator** is **false**
  - **UseByteBufferDataTransfer**: set to **true** to prefer <see cref="Java.Nio.ByteBuffer"/> data exchange in serializer instances
  - **UseDeletePolicyForTopic**: set to **true** to enable [delete cleanup policy](https://kafka.apache.org/documentation/#topicconfigs_cleanup.policy)
  - ~~**UseCompactedReplicator**: Use `KNetCompactedReplicator` instead of Apache Kafka™ Streams to manage data to or from topics~~
  - **UseKNetStreams**: Use KNet version of Apache Kafka™ Streams instead of standard Apache Kafka™ Streams, used if **UseCompactedReplicator** is **false**
  - **ConsumerConfig**: parameters to use for Producer
  - **ProducerConfig**: parameters to use for Producer
  - **StreamsConfig**: parameters to use for Apche Kafka™ Streams application
  - **TopicConfig**: parameters to use on topic creation for each entity
  - **OnChangeEvent**: handler to receive change events from back-end

## How to use `KafkaDbContext` class

The most simple example of usage can be found in [KEFCore usage](usage.md). By default, `KafkaDbContext` automatically manages `OnConfiguring` method of `DbContext`:
- `KafkaDbContext` checks the mandatory options like **BootstrapServers** and **DbName**
- `KafkaDbContext` setup the options needed to use an Apache Kafka™ cluster:
  - ~~default `ConsumerConfig` can be overridden using **ConsumerConfig** property of `KafkaDbContext`~~
  - default `ProducerConfig` can be overridden using **ProducerConfig** property of `KafkaDbContext`
  - default `StreamsConfig` can be overridden using **StreamsConfig** property of `KafkaDbContext`
  - default `TopicConfig` can be overridden using **TopicConfig** property of `KafkaDbContext`

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
