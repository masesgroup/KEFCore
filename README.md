# KEFCore: [Entity Framework Core](https://learn.microsoft.com/ef/core/) provider for [Apache Kafka™](https://kafka.apache.org/)

KEFCore is the [Entity Framework Core](https://learn.microsoft.com/ef/core/) provider for [Apache Kafka™](https://kafka.apache.org/).
Based on [KNet client-side features](https://github.com/masesgroup/KNet) it allows to use [Apache Kafka™](https://kafka.apache.org/) as a distributed database and more: KNet client-side features are also compatible with any broker that implements the Kafka wire protocol — see [Backend compatibility](#backend-compatibility) below.

### Libraries and Tools

|Core | Templates | Json Serialization | Avro Serialization | Protobuf Serialization |
|:---: |:---: |:---: |:---: |:---: |
|[![Core](https://img.shields.io/nuget/v/MASES.EntityFrameworkCore.KNet)](https://www.nuget.org/packages/MASES.EntityFrameworkCore.KNet) [![downloads](https://img.shields.io/nuget/dt/MASES.EntityFrameworkCore.KNet)](https://www.nuget.org/packages/MASES.EntityFrameworkCore.KNet) | [![Templates](https://img.shields.io/nuget/v/MASES.EntityFrameworkCore.KNet.Templates)](https://www.nuget.org/packages/MASES.EntityFrameworkCore.KNet.Templates) [![downloads](https://img.shields.io/nuget/dt/MASES.EntityFrameworkCore.KNet.Templates)](https://www.nuget.org/packages/MASES.EntityFrameworkCore.KNet.Templates) | [![Serialization](https://img.shields.io/nuget/v/MASES.EntityFrameworkCore.KNet.Serialization)](https://www.nuget.org/packages/MASES.EntityFrameworkCore.KNet.Serialization) [![downloads](https://img.shields.io/nuget/dt/MASES.EntityFrameworkCore.KNet.Serialization)](https://www.nuget.org/packages/MASES.EntityFrameworkCore.KNet.Serialization) | [![Serialization Avro](https://img.shields.io/nuget/v/MASES.EntityFrameworkCore.KNet.Serialization.Avro)](https://www.nuget.org/packages/MASES.EntityFrameworkCore.KNet.Serialization.Avro) [![downloads](https://img.shields.io/nuget/dt/MASES.EntityFrameworkCore.KNet.Serialization.Avro)](https://www.nuget.org/packages/MASES.EntityFrameworkCore.KNet.Serialization.Avro) | [![Serialization Protobuf](https://img.shields.io/nuget/v/MASES.EntityFrameworkCore.KNet.Serialization.Protobuf)](https://www.nuget.org/packages/MASES.EntityFrameworkCore.KNet.Serialization.Protobuf) [![downloads](https://img.shields.io/nuget/dt/MASES.EntityFrameworkCore.KNet.Serialization.Protobuf)](https://www.nuget.org/packages/MASES.EntityFrameworkCore.KNet.Serialization.Protobuf) |

### Pipelines

[![CI_BUILD](https://github.com/masesgroup/KEFCore/actions/workflows/build.yaml/badge.svg)](https://github.com/masesgroup/KEFCore/actions/workflows/build.yaml) 
[![CI_RELEASE](https://github.com/masesgroup/KEFCore/actions/workflows/release.yaml/badge.svg)](https://github.com/masesgroup/KEFCore/actions/workflows/release.yaml) 

### Project disclaimer

KEFCore is a project curated by MASES Group and supported by the open-source community.
Its primary scope is to support other MASES Group projects — both open-source and commercial — though it is freely available for any use. Dedicated community and commercial subscription plans are available.
The repository and releases may contain bugs. The release cycle depends on critical issues discovered and/or enhancement requests from this or other dependent projects.

Looking for [Entity Framework Core](https://learn.microsoft.com/ef/core/) and [Apache Kafka™](https://kafka.apache.org/) expertise? MASES Group can help you design, build, deploy, and manage [Entity Framework Core](https://learn.microsoft.com/ef/core/) and [Apache Kafka™](https://kafka.apache.org/) applications. [Find out more.](src/documentation/articles/support.md)

---

## Scope of the project

This project aims to create a provider able to save data to, and read data from, an Apache Kafka™ cluster using the paradigm behind Entity Framework.

Have you ever reached the [Entity Framework Core introduction page](https://learn.microsoft.com/ef/core/)? 
The first example proposed is:

```c#
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Intro;

public class BloggingContext : DbContext
{
    public DbSet<Blog> Blogs { get; set; }
    public DbSet<Post> Posts { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlServer(
            @"Server=(localdb)\mssqllocaldb;Database=Blogging;Trusted_Connection=True;ConnectRetryCount=0");
    }
}

public class Blog
{
    public int BlogId { get; set; }
    public string Url { get; set; }
    public int Rating { get; set; }
    public List<Post> Posts { get; set; }
}

public class Post
{
    public int PostId { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }

    public int BlogId { get; set; }
    public Blog Blog { get; set; }
}
```
which uses a SQL Server instance to store and retrieve the data.

With [Entity Framework Core](https://learn.microsoft.com/ef/core/) provider for [Apache Kafka™](https://kafka.apache.org/) any user can replace a database (the available providers always use a database) with an [Apache Kafka™](https://kafka.apache.org/) cluster.
So the following query:
```c#
using (var db = new BloggingContext())
{
    var blogs = await db.Blogs
        .Where(b => b.Rating > 3)
        .OrderBy(b => b.Url)
        .ToListAsync();
}
```
or the following operation:
```c#
using (var db = new BloggingContext())
{
    var blog = new Blog { Url = "http://sample.com" };
    db.Blogs.Add(blog);
    await db.SaveChangesAsync();
}
```

can be done on a set of topics in an [Apache Kafka™](https://kafka.apache.org/) cluster.

The project is based on available information within the official [EntityFrameworkCore repository](https://github.com/dotnet/efcore), many classes was copied from there as reported in the official documentation within the Microsoft website at https://docs.microsoft.com/en-us/ef/core/providers/writing-a-provider.

Currently the project tries to support, at our best, the [official supported Apache Kafka™ binary distribution](https://kafka.apache.org/downloads):

| KEFCore | State | KNet | Apache Kafka™ | .NET | JVM™ |
|:---:	|:---:	|:---:	|:---:	|:---:	|:---:	|
| 2.6.*+ | Active | 3.2.x | 4.2.x | [![.NET 8+](https://img.shields.io/badge/.NET-8%2B-purple)](https://dotnet.microsoft.com/) | [![Java 17+](https://img.shields.io/badge/Java-17%2B-blue)](https://www.oracle.com/java/) |
| 2.5.* | Deprecated | 2.9.x | 3.9.x | [![.NET 8+](https://img.shields.io/badge/.NET-8%2B-purple)](https://dotnet.microsoft.com/) | [![Java 11+](https://img.shields.io/badge/Java-11%2B-blue)](https://www.oracle.com/java/) |

---

## Backend compatibility

[Entity Framework Core](https://learn.microsoft.com/ef/core/) provider for [Apache Kafka™](https://kafka.apache.org/) uses the official Apache Kafka™ Java client packages directly through [KNet client-side features](https://github.com/masesgroup/KNet). This architecture has a direct impact on backend compatibility.

**Client-side features** — Producer, Consumer, Admin Client, Kafka Streams, KNet Streams SDK, KNet Connect SDK, KNetPS scriptable cmdlets — communicate with the broker exclusively through the Kafka wire protocol and are therefore compatible with **any broker that implements it**, not only Apache Kafka™ itself.

Examples of compatible brokers: [Redpanda](https://redpanda.com/), [Amazon MSK](https://aws.amazon.com/msk/), [Confluent Platform / Cloud](https://www.confluent.io/), [Aiven for Apache Kafka™](https://aiven.io/kafka), [IBM Event Streams](https://www.ibm.com/products/event-streams), [WarpStream](https://www.warpstream.com/), [AutoMQ](https://www.automq.com/), and others.

See [Supported Backends](src/documentation/articles/backends.md) for the full compatibility matrix covering all KNet feature areas.

---

## Community and Contribution

If you find [Entity Framework Core](https://learn.microsoft.com/ef/core/) provider for [Apache Kafka™](https://kafka.apache.org/) useful:

* Leave a ⭐ on the repository
* Open [issues](https://github.com/masesgroup/KEFCore/issues) to report bugs 🐛 or request features
* Submit Pull Requests to improve the project

This project adheres to the Contributor [Covenant code of conduct](CODE_OF_CONDUCT.md). By participating, you are expected to uphold this code. Please report unacceptable behavior to coc_reporting@masesgroup.com.

---

## Summary

* [Getting started](src/documentation/articles/gettingstarted.md)
* [How it works](src/documentation/articles/howitworks.md)
* [Supported backends](src/documentation/articles/backends.md)
* [Usage](src/documentation/articles/usage.md)
* [Use cases](src/documentation/articles/usecases.md)
* [Templates usage](src/documentation/articles/usageTemplates.md)
* [Options](src/documentation/articles/options.md)
* [Conventions](src/documentation/articles/conventions.md)
* [Serialization](src/documentation/articles/serialization.md)
* [Schema migration](src/documentation/articles/migration.md)
* [Performance tips](src/documentation/articles/performancetips.md)
* [Troubleshooting](src/documentation/articles/troubleshooting.md)
* [External application](src/documentation/articles/externalapplication.md)
* [Roadmap](src/documentation/articles/roadmap.md)
* [Current state](src/documentation/articles/currentstate.md)
* [KEFCoreDbContext](src/documentation/articles/kefcoredbcontext.md)

---

## Runtime engine

KEFCore uses [KNet](https://github.com/masesgroup/KNet), and indeed [JCOBridge](https://www.jcobridge.com) with its [features](https://www.jcobridge.com/features/), to obtain many benefits:
* **Cyber-security**:
  * [JVM™](https://en.wikipedia.org/wiki/Java_virtual_machine) and [CLR, or CoreCLR,](https://en.wikipedia.org/wiki/Common_Language_Runtime) runs in the same process, but are insulated from each other;
  * JCOBridge does not make any code injection into JVM™;
  * JCOBridge does not use any other communication mechanism than JNI;
  * .NET (CLR) inherently inherits the cyber-security levels of running JVM™ and Apache Kafka™; 
* **Direct access the JVM™ from any .NET application**: 
  * Any Java/Scala class behind Apache Kafka™ can be directly managed: Consumer, Producer, Administration, Streams, Server-side, and so on;
  * No need to learn new APIs: we try to expose the same APIs in C# style;
  * No extra validation cycle on protocol and functionality: bug fix, improvements, new features are immediately available;
  * Documentation is shared.

[JCOBridge 2.6.*](https://www.jcobridge.com) can be used for free without any obligations. A commercial license must be purchased — or the software uninstalled — if you derive direct or indirect income from its usage.

### JCOBridge resources

Have a look at the following JCOBridge resources:

|JCOBridge | 2.5.* series | 2.6.* series |
|:---:	|:---:	|:---:	|
|KEFCore | > 1.0.* series | > 2.6.1 series |
|Release notes|[Link](https://www.jcobridge.com/release-notes/)| [Link](https://www.jcobridge.com/release-notes/)|
|Community Edition|[Conditions](https://www.jcobridge.com/pricing-25/)|[Conditions](https://www.jcobridge.com/pricing-26/)|
|Commercial Edition|[Information](https://www.jcobridge.com/pricing-25/)|[Information](https://www.jcobridge.com/pricing-26/)|

Latest release: [![JCOBridge nuget](https://img.shields.io/nuget/v/MASES.JCOBridge)](https://www.nuget.org/packages/MASES.JCOBridge)

---

KAFKA is a registered trademark of The Apache Software Foundation. KEFCore has no affiliation with and is not endorsed by The Apache Software Foundation.
Microsoft is a registered trademark of Microsoft Corporation.
EntityFramework is a registered trademark of Microsoft Corporation.
