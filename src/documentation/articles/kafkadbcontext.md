# KEFCore: KafkaDbContext

`KafkaDbContext` is a special class to define the `DbContext` to use with EntityFrameworkCore:
- it inherits from `DbContext`: to define the model, and/or creating the database, see [getting started](https://docs.microsoft.com/ef/core/get-started/) in the docs
- it defines the following properties:
  - **BootstrapServers**: the server hosting the broker of Apache Kafka
  - **ApplicationId**: the application identifier used to identify the context
  - **DbName**: the user defined name which declares the database name, it is used to prepend every Topic which belongs to this database
  - **DefaultNumPartitions**: the default number of partitions used when topics are created for each entity
  - **DefaultReplicationFactor**: the replication factor to use when data are stored in Apache Kafka
  - **UsePersistentStorage**: set to **true** to use a persintent storage between multiple application startup
  - **UseProducerByEntity**: use a different producer for each enity of EntityFrameworkCore model
  - **ProducerConfigBuilder**: parameters to use for Producer
  - **StreamsConfigBuilder**: parameters to use for Stream application
  - **TopicConfigBuilder**: parameters to use on topic creation for each entity

