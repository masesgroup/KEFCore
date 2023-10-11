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
  - **ProducerConfigBuilder**: parameters to use for Producer
  - **StreamsConfigBuilder**: parameters to use for Apche Kafka Streams application
  - **TopicConfigBuilder**: parameters to use on topic creation for each entity

## How to use `KafkaDbContext` class

The most simple example of usage can be found in [KEFCore usage](usage.md). By default, `KafkaDbContext` automatically manages `OnConfiguring` method of `DbContext`:
- checks for mandatory opions like **BootstrapServers** and **DbName**
- setup the options to use an Apache Kafka cluster




