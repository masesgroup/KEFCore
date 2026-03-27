---
title: Professional support for KEFCore
_description: Professional support and implementation services for KEFCore by MASES Group
---

# KEFCore: professional support

Looking for Entity Framework Core and Apache Kafka™ expertise? MASES Group can help you design, build, deploy, and manage Entity Framework Core and Apache Kafka™ applications.

---

## What MASES Group can help with

### Architecture and design

- Data model design for Kafka-backed EF Core applications: entity structure, topic naming conventions, partition and retention strategy, and ComplexType mapping
- SerDes selection and configuration (JSON, Avro, Protobuf) — including mixed-format models and custom serializers
- Kafka Streams topology alignment with EF Core query patterns: state store selection, RocksDB vs. in-memory, and interactive query design

### Implementation

- Integration with existing EF Core codebases: migration from InMemory or relational providers, model convention setup, and DbContext lifecycle management
- LINQ query optimization against KEFCore: store lookup flag selection (`UseStorePrefixScan`, `UseStoreKeyRange`, etc.) and RocksDB state store tuning
- Exactly-once semantics with KEFCore transactional producers: transaction group design, `SaveChanges` patterns, and consumer `isolation.level` configuration
- SignalR integration for real-time .NET applications consuming live Kafka data via KEFCore

### Operations and performance

- RocksDB lifecycle handler configuration and state store parameter tuning for production workloads
- Per-entity producer configuration: batching, compression, acks, and retry strategy via `KEFCoreProducerAttribute` or fluent API
- Durability and restart behavior: persistent storage configuration, catchup strategy, and state store sizing

### Training and knowledge transfer

- Hands-on workshops covering the KEFCore programming model: DbContext setup, entity conventions, SerDes, and Kafka Streams integration
- Code reviews and architectural walkthroughs for teams building EF Core applications on Apache Kafka™
- Guidance on JCOBridge HPA Edition for teams requiring the highest reliability at the JVM↔CLR boundary

---

## Contact

To discuss your project or request a quote, contact MASES Group at:

- **Website:** [https://www.masesgroup.com/](https://www.masesgroup.com/)
- **GitHub:** [https://github.com/masesgroup](https://github.com/masesgroup)
- **Email:** <span>&#115;&#97;&#108;&#101;&#115;&#64;&#109;&#97;&#115;&#101;&#115;&#103;&#114;&#111;&#117;&#112;&#46;&#99;&#111;&#109;</span>

---

> [!NOTE]
> KAFKA is a registered trademark of The Apache Software Foundation. MASES Group has no affiliation with and is not endorsed by The Apache Software Foundation.
