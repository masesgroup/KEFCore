# EFCoreKafka: the EntityFrameworkCore provider for Apache Kafka

[![CI_BUILD](https://github.com/masesgroup/EFCoreKafka/actions/workflows/build.yaml/badge.svg)](https://github.com/masesgroup/EFCoreKafka/actions/workflows/build.yaml) [![CI_RELEASE](https://github.com/masesgroup/EFCoreKafka/actions/workflows/release.yaml/badge.svg)](https://github.com/masesgroup/EFCoreKafka/actions/workflows/release.yaml) 

[![latest version](https://img.shields.io/nuget/v/MASES.EntityFrameworkCore.Kafka)](https://www.nuget.org/packages/MASES.EntityFrameworkCore.Kafka) [![downloads](https://img.shields.io/nuget/dt/MASES.EntityFrameworkCore.Kafka)](https://www.nuget.org/packages/MASES.EntityFrameworkCore.Kafka)

EFCoreKafka is the EntityFrameworkCore provider for Apache Kafka.
Based on [KafkaBridge](https://github.com/masesgroup/KafkaBridge) it allows to use Apache Kafka as a distributed database.

This project adheres to the Contributor [Covenant code of conduct](CODE_OF_CONDUCT.md). By participating, you are expected to uphold this code. Please report unacceptable behavior to coc_reporting@masesgroup.com.

## Scope of the project

This project aims to create a provider to access the information stored within an Apache Kafka cluster using the paradigm behind Entity Framework.
The project is based on available information within the official [EntityFrameworkCore repository](https://github.com/dotnet/efcore), many classes was copied from there as reported in the official documentation within the Microsoft website at https://docs.microsoft.com/en-us/ef/core/providers/writing-a-provider.

## Runtime engine

EFCoreKafka uses [KafkaBridge](https://github.com/masesgroup/KafkaBridge), and indeed [JCOBridge](https://www.jcobridge.com) with its [features](https://www.jcobridge.com/features/), to obtain many benefits:
* **Cyber-security**:
  * [JVM](https://en.wikipedia.org/wiki/Java_virtual_machine) and [CLR, or CoreCLR,](https://en.wikipedia.org/wiki/Common_Language_Runtime) runs in the same process, but are insulated from each other;
  * JCOBridge does not make any code injection into JVM;
  * JCOBridge does not use any other communication mechanism than JNI;
  * .NET (CLR) inherently inherits the cyber-security levels of running JVM and Apache Kafka; 
* **Direct access the JVM from any .NET application**: 
  * Any Java/Scala class behind Apache Kafka can be directly managed: Consumer, Producer, Administration, Streams, Server-side, and so on;
  * No need to learn new APIs: we try to expose the same APIs in C# style;
  * No extra validation cycle on protocol and functionality: bug fix, improvements, new features are immediately available;
  * Documentation is shared.

Have a look at the following resources:
- [Release notes](https://www.jcobridge.com/release-notes/)
- [Commercial info](https://www.jcobridge.com/pricing/)
- [![JCOBridge nuget](https://img.shields.io/nuget/v/MASES.JCOBridge)](https://www.nuget.org/packages/MASES.JCOBridge)

---
## Summary

* [Roadmap](src/Documentation/articles/roadmap.md)
* [Actual state](src/Documentation/articles/actualstate.md)
* [EFCoreKafka usage](src/Documentation/articles/usage.md)

---

KAFKA is a registered trademark of The Apache Software Foundation. EFCoreKafka has no affiliation with and is not endorsed by The Apache Software Foundation.
Microsoft is a registered trademark of Microsoft Corporation.
EntityFramework is a registered trademark of Microsoft Corporation.
