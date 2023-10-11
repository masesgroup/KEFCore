# KEFCore: KafkaDbContext

`KafkaDbContext` is a special class which helps to define the `DbContext` and use Entity Framework Core with Apache Kafka:
- `KafkaDbContext` inherits from `DbContext`: to define the model, and/or creating the database, see [getting started](https://docs.microsoft.com/ef/core/get-started/) in the docs and [KEFCore usage](usage.md)
- `KafkaDbContext` defines the following properties:
  - **BootstrapServers**: the server hosting the broker of Apache Kafka
  - **ApplicationId**: the application identifier used to identify the context
  - **DbName**: the user defined name which declares the database name, it is used to prepend every Topic which belongs to this database
  - **DefaultNumPartitions**: the default number of partitions used when topics are created for each entity
  - **DefaultReplicationFactor**: the replication factor to use when data are stored in Apache Kafka
  - **DefaultConsumerInstances**: the consumer instances to be allocated when UseCompactedReplicator is **true**
  - **UsePersistentStorage**: set to **true** to use a persintent storage between multiple application startup
  - **UseCompactedReplicator**: Use `KNetCompactedReplicator` instead of Apache Kafka Streams to manage data to or from topics
  - **ConsumerConfigBuilder**: parameters to use for Producer
  - **ProducerConfigBuilder**: parameters to use for Producer
  - **StreamsConfigBuilder**: parameters to use for Apche Kafka Streams application
  - **TopicConfigBuilder**: parameters to use on topic creation for each entity

## How to use `KafkaDbContext` class

The most simple example of usage can be found in [KEFCore usage](usage.md). By default, `KafkaDbContext` automatically manages `OnConfiguring` method of `DbContext`:
- `KafkaDbContext` checks the mandatory options like **BootstrapServers** and **DbName**
- `KafkaDbContext` setup the options needed to use an Apache Kafka cluster:
  - default `ConsumerConfig` can be overridden using **ConsumerConfigBuilder** property of `KafkaDbContext`
  - default `ProducerConfig` can be overridden using **ProducerConfigBuilder** property of `KafkaDbContext`
  - default `StreamsConfig` can be overridden using **StreamsConfigBuilder** property of `KafkaDbContext`
  - default `TopicConfig` can be overridden using **TopicConfigBuilder** property of `KafkaDbContext`


### Default **ConsumerConfigBuilder**

Over the Apache Kafka defaults it applies:

- EnableAutoCommit is **true**
- AutoOffsetReset set to **EARLIEST**
- AllowAutoCreateTopics set to **false**

### Default **ProducerConfigBuilder**

Does not change anything than the Apache Kafka defaults

### Default **ConsumerConfigBuilder**

Does not change anything than the Apache Kafka defaults

### Default **TopicConfigBuilder**

Over the Apache Kafka defaults it applies:

- DeleteRetentionMs set to 100 ms
- MinCleanableDirtyRatio set to 0.01
- SegmentMs set to 100 ms
- RetentionBytes set to 1073741824 bytes (1 Gb)
