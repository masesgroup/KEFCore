# EntityFramework4Kafka: the EntityFrameworkCore provider for Apache Kafka

[![CI_BUILD](https://github.com/masesgroup/EntityFramework4Kafka/actions/workflows/build.yaml/badge.svg)](https://github.com/masesgroup/EntityFramework4Kafka/actions/workflows/build.yaml) [![CI_RELEASE](https://github.com/masesgroup/EntityFramework4Kafka/actions/workflows/release.yaml/badge.svg)](https://github.com/masesgroup/EntityFramework4Kafka/actions/workflows/release.yaml) 

EntityFramework4Kafka is the EntityFrameworkCore provider for Apache Kafka.
Based on [KafkaBridge](https://github.com/masesgroup/KafkaBridge) it allows to use Apache Kafka as a distributed database.

This project adheres to the Contributor [Covenant code of conduct](CODE_OF_CONDUCT.md). By participating, you are expected to uphold this code. Please report unacceptable behavior to coc_reporting@masesgroup.com.

## Scope of the project

This project aims to create a provider to access the information stored within an Apache Kafka cluster using the paradigm behind Entity Framework.
The project is based on available information within the official [EntityFrameworkCore repository](https://github.com/dotnet/efcore), many classes was copied from there as reported in the official documentation within the Microsoft website at https://docs.microsoft.com/en-us/ef/core/providers/writing-a-provider.

---
## Summary

* [Roadmap](src/net/Documentation/articles/roadmap.md)
* [Actual state](src/net/Documentation/articles/actualstate.md)
* [EntityFramework4Kafka usage](articles/usage.md)

---

KAFKA is a registered trademark of The Apache Software Foundation. EntityFramework4Kafka has no affiliation with and is not endorsed by The Apache Software Foundation.
Microsoft is a registered trademark of Microsoft Corporation.
EntityFramework is a registered trademark of Microsoft Corporation.
