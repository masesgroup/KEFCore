---
title: Entity Framework Core provider for Apache Kafka™
_description: Main page of Entity Framework Core provider for Apache Kafka™
---

# KEFCore: [Entity Framework Core](https://learn.microsoft.com/it-it/ef/core/) provider for [Apache Kafka™](https://kafka.apache.org/)

KEFCore is the [Entity Framework Core](https://learn.microsoft.com/it-it/ef/core/) provider for [Apache Kafka™](https://kafka.apache.org/).
Based on [KNet](https://github.com/masesgroup/KNet) it allows to use [Apache Kafka™](https://kafka.apache.org/) as a distributed database and more.

### Libraries and Tools

|Core | Templates | Json Serialization | Avro Serialization | Protobuf Serialization |
|:---: |:---: |:---: |:---: |:---: |
|[![Core](https://img.shields.io/nuget/v/MASES.EntityFrameworkCore.KNet)](https://www.nuget.org/packages/MASES.EntityFrameworkCore.KNet) [![downloads](https://img.shields.io/nuget/dt/MASES.EntityFrameworkCore.KNet)](https://www.nuget.org/packages/MASES.EntityFrameworkCore.KNet) | [![Templates](https://img.shields.io/nuget/v/MASES.EntityFrameworkCore.KNet.Templates)](https://www.nuget.org/packages/MASES.EntityFrameworkCore.KNet.Templates) [![downloads](https://img.shields.io/nuget/dt/MASES.EntityFrameworkCore.KNet.Templates)](https://www.nuget.org/packages/MASES.EntityFrameworkCore.KNet.Templates) | [![Serialization](https://img.shields.io/nuget/v/MASES.EntityFrameworkCore.KNet.Serialization)](https://www.nuget.org/packages/MASES.EntityFrameworkCore.KNet.Serialization) [![downloads](https://img.shields.io/nuget/dt/MASES.EntityFrameworkCore.KNet.Serialization)](https://www.nuget.org/packages/MASES.EntityFrameworkCore.KNet.Serialization) | [![Serialization Avro](https://img.shields.io/nuget/v/MASES.EntityFrameworkCore.KNet.Serialization.Avro)](https://www.nuget.org/packages/MASES.EntityFrameworkCore.KNet.Serialization.Avro) [![downloads](https://img.shields.io/nuget/dt/MASES.EntityFrameworkCore.KNet.Serialization.Avro)](https://www.nuget.org/packages/MASES.EntityFrameworkCore.KNet.Serialization.Avro) | [![Serialization Protobuf](https://img.shields.io/nuget/v/MASES.EntityFrameworkCore.KNet.Serialization.Protobuf)](https://www.nuget.org/packages/MASES.EntityFrameworkCore.KNet.Serialization.Protobuf) [![downloads](https://img.shields.io/nuget/dt/MASES.EntityFrameworkCore.KNet.Serialization.Protobuf)](https://www.nuget.org/packages/MASES.EntityFrameworkCore.KNet.Serialization.Protobuf) |

### Pipelines

[![CI_BUILD](https://github.com/masesgroup/KEFCore/actions/workflows/build.yaml/badge.svg)](https://github.com/masesgroup/KEFCore/actions/workflows/build.yaml) 
[![CI_RELEASE](https://github.com/masesgroup/KEFCore/actions/workflows/release.yaml/badge.svg)](https://github.com/masesgroup/KEFCore/actions/workflows/release.yaml) 

### Project disclaimer

KEFCore is a project, curated by MASES Group, can be supported by the open-source community.

Its primary scope is to support other, public or internal, MASES Group projects: open-source community and commercial entities can use it for their needs and support this project, moreover there are dedicated community and commercial subscription plans.

The repository code and releases may contain bugs, the release cycle depends from critical discovered issues and/or enhancement requested from this or other projects.

Looking for the help of [Entity Framework Core](https://learn.microsoft.com/it-it/ef/core/) and [Apache Kafka™](https://kafka.apache.org/) experts? MASES Group can help you design, build, deploy, and manage [Entity Framework Core](https://learn.microsoft.com/it-it/ef/core/) and [Apache Kafka™](https://kafka.apache.org/) applications.

---

## Scope of the project

This project aims to create a provider to access the information stored within an Apache Kafka™ cluster using the paradigm behind Entity Framework.
The project is based on available information within the official [EntityFrameworkCore repository](https://github.com/dotnet/efcore), many classes was copied from there as reported in the official documentation within the Microsoft website at https://docs.microsoft.com/en-us/ef/core/providers/writing-a-provider.

Currently the project tries to support, at our best, the [supported Apache Kafka™ binary distribution](https://kafka.apache.org/downloads):

| KEFCore | State | Apache Kafka™ | .NET | JVM™ |
|:---:	|:---:	|:---:	|:---:	|:---:	|
| 2.6.* | Active | 4.1.* | 8+ | 17+ |
| 2.5.* | Active | 3.9.* | 8+ | 11+ |

### Community and Contribution

Do you like the project? 
- Request your free [community subscription](https://www.jcobridge.com/pricing-25/).

Do you want to help us?
- put a :star: on this project
- open [issues](https://github.com/masesgroup/KEFCore/issues) to request features or report bugs :bug:
- improves the project with Pull Requests

This project adheres to the Contributor [Covenant code of conduct](https://github.com/masesgroup/KEFCore/blob/master/CODE_OF_CONDUCT.md). By participating, you are expected to uphold this code. Please report unacceptable behavior to coc_reporting@masesgroup.com.
