---
title: Supported backends for Entity Framework Core provider for Apache Kafka™
_description: Supported backends for Entity Framework Core provider for Apache Kafka™
---

# KEFCore: Supported Backends

KEFCore is the [Entity Framework Core](https://learn.microsoft.com/ef/core/) provider for [Apache Kafka™](https://kafka.apache.org/),
based on [KNet client-side features](https://github.com/masesgroup/KNet) it allows to use [Apache Kafka™](https://kafka.apache.org/) as a distributed database and more: 
KNet client-side features are also compatible with any broker that implements the Kafka wire protocol.

## Kafka wire-protocol compatible brokers

[Entity Framework Core](https://learn.microsoft.com/ef/core/) provider for [Apache Kafka™](https://kafka.apache.org/) use only the standard Apache Kafka™ client APIs: Producer, Consumer, Admin Client, and Kafka Streams. These APIs communicate with the broker exclusively through the Kafka wire protocol.

Any broker that fully implements the Kafka wire protocol is therefore compatible with the following [KNet client-side features](https://github.com/masesgroup/KNet) used from [Entity Framework Core](https://learn.microsoft.com/ef/core/) provider for [Apache Kafka™](https://kafka.apache.org/):

- **Producer and Consumer APIs** (`KafkaProducer`, `KafkaConsumer`, `KNetProducer`, `KNetConsumer`, `KNetCompactedReplicator`)
- **Admin Client API** (`KafkaAdminClient`)
- **Kafka Streams / KNet Streams SDK** (runs entirely embedded in the .NET process; the broker only sees standard topics)

> **Note on Kafka Streams**: Kafka Streams runs entirely embedded within the application process. The broker is not aware of Streams: it only stores the standard topics that Streams creates and manages (state store changelog topics, repartition topics). No server-side Streams support is required from the broker.

## Compatible brokers for client-side features

The following brokers declare full or substantial Kafka wire-protocol compatibility and are therefore compatible with all KNet client-side features:

| Broker | Type | Compatibility notes |
|---|---|---|
| [Apache Kafka™](https://kafka.apache.org/) | Self-hosted | Reference implementation, full compatibility |
| [Redpanda](https://redpanda.com/) | Self-hosted / Cloud | Declares full Kafka wire-protocol compatibility |
| [Amazon MSK](https://aws.amazon.com/msk/) | Managed Cloud (AWS) | Kafka-native managed service, full compatibility |
| [Confluent Platform / Cloud](https://www.confluent.io/) | Self-hosted / Cloud | Commercial Kafka distribution, full compatibility |
| [Aiven for Apache Kafka™](https://aiven.io/kafka) | Managed Cloud (multi-cloud) | Kafka-native managed service, full compatibility |
| [IBM Event Streams](https://www.ibm.com/products/event-streams) | Self-hosted / Cloud | Based on Apache Kafka™, full compatibility |
| [WarpStream](https://www.warpstream.com/) | Cloud (S3-backed) | Kafka-compatible, zero inter-broker network |
| [AutoMQ](https://www.automq.com/) | Self-hosted / Cloud | Kafka-compatible, S3-backed cloud-native |
| [Azure Event Hubs](https://azure.microsoft.com/en-us/products/event-hubs/) | Managed Cloud (Azure) | Kafka endpoint available; partial compatibility — some Admin API features may not be supported |
| [Apache Pulsar](https://pulsar.apache.org/) (KoP) | Self-hosted | Kafka-on-Pulsar translation layer; partial compatibility — verify Admin API and topic configuration support |

> **Important**: [Entity Framework Core](https://learn.microsoft.com/ef/core/) provider for [Apache Kafka™](https://kafka.apache.org/) uses the official Apache Kafka™ Java client packages directly through [KNet client-side features](https://github.com/masesgroup/KNet). Compatibility with Kafka-protocol-compatible brokers is therefore **inherited from the Apache Kafka™ client libraries themselves**, not from a custom implementation. Any broker compatible with the Apache Kafka™ client is automatically compatible with KNet client-side features.

