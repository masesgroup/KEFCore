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

KEFCore provides an [Entity Framework Core](https://learn.microsoft.com/ef/core/) provider for [Apache Kafka™](https://kafka.apache.org/), enabling .NET applications to use Kafka topics as a data store through the standard EF Core programming model — `DbContext`, LINQ queries, and strongly-typed entities — with no Kafka-specific consumer or producer code.

The [EF Core introduction page](https://learn.microsoft.com/ef/core/) opens with this example:
```c#
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
```

With KEFCore, replacing the SQL Server backend with an Apache Kafka™ cluster requires changing a single line:
```c#
optionsBuilder.UseKEFCore("my-application", "localhost:9092");
```

From that point on, standard EF Core code works unchanged against Kafka topics:
```c#
// Query
var blogs = await db.Blogs
    .Where(b => b.Rating > 3)
    .OrderBy(b => b.Url)
    .ToListAsync();

// Write
db.Blogs.Add(new Blog { Url = "http://sample.com" });
await db.SaveChangesAsync();
```

KEFCore is developed following the guidelines in the official [EntityFrameworkCore repository](https://github.com/dotnet/efcore) and the [Writing a provider](https://docs.microsoft.com/ef/core/providers/writing-a-provider) documentation published by Microsoft.

Currently the project tries to support, at our best, the [official supported Apache Kafka™ binary distribution](https://kafka.apache.org/downloads):

| KEFCore | State | KNet | Apache Kafka™ | .NET | JVM™ |
|:---:	|:---:	|:---:	|:---:	|:---:	|:---:	|
| 4.* | Active | 3.* | 4.* | [![.NET 8+](https://img.shields.io/badge/.NET-8%2B-purple)](https://dotnet.microsoft.com/) | [![Java 17+](https://img.shields.io/badge/Java-17%2B-blue)](https://www.oracle.com/java/) |
| 3.* | Active | 2.9.* | 3.9.* | [![.NET 8+](https://img.shields.io/badge/.NET-8%2B-purple)](https://dotnet.microsoft.com/) | [![Java 11+](https://img.shields.io/badge/Java-11%2B-blue)](https://www.oracle.com/java/) |
| 2.6.*+ | Deprecated | 3.2.x | 4.2.x | [![.NET 8+](https://img.shields.io/badge/.NET-8%2B-purple)](https://dotnet.microsoft.com/) | [![Java 17+](https://img.shields.io/badge/Java-17%2B-blue)](https://www.oracle.com/java/) |
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

<!-- PERFORMANCE-SUMMARY:START (auto-generated by generate_perf_docs.py, do not edit by hand) -->
## Performance

| Project | Framework | Test | Cache | Scenario | Backend | Median iteration time (ms) |
|---|---|---|---|---|---|---|
| Benchmark | net10.0 | IterationTotal | non-cached | load | KNetStreams.Buffered | 129.106 |
| Benchmark | net10.0 | IterationTotal | non-cached | load | KNetStreams.Buffered.Prefetch | 155.959 |
| Benchmark | net10.0 | IterationTotal | non-cached | load | KNetStreams.Raw | 94.552 |
| Benchmark | net10.0 | IterationTotal | non-cached | load | KNetStreams.Raw.Prefetch | 117.294 |
| Benchmark | net10.0 | IterationTotal | non-cached | load | KafkaStreams.Buffered | 126.415 |
| Benchmark | net10.0 | IterationTotal | non-cached | load | KafkaStreams.Raw | 94.594 |
| Benchmark | net10.0 | IterationTotal | cached | load | KNetStreams.Buffered | 22.018 |
| Benchmark | net10.0 | IterationTotal | cached | load | KNetStreams.Buffered.Prefetch | 22.621 |
| Benchmark | net10.0 | IterationTotal | cached | load | KNetStreams.Raw | 22.840 |
| Benchmark | net10.0 | IterationTotal | cached | load | KNetStreams.Raw.Prefetch | 21.743 |
| Benchmark | net10.0 | IterationTotal | cached | load | KafkaStreams.Buffered | 21.676 |
| Benchmark | net10.0 | IterationTotal | cached | load | KafkaStreams.Raw | 22.091 |
| Benchmark | net8.0 | IterationTotal | non-cached | load | KNetStreams.Buffered | 151.751 |
| Benchmark | net8.0 | IterationTotal | non-cached | load | KNetStreams.Buffered.Prefetch | 180.669 |
| Benchmark | net8.0 | IterationTotal | non-cached | load | KNetStreams.Raw | 119.497 |
| Benchmark | net8.0 | IterationTotal | non-cached | load | KNetStreams.Raw.Prefetch | 141.356 |
| Benchmark | net8.0 | IterationTotal | non-cached | load | KafkaStreams.Buffered | 149.607 |
| Benchmark | net8.0 | IterationTotal | non-cached | load | KafkaStreams.Raw | 118.614 |
| Benchmark | net8.0 | IterationTotal | cached | load | KNetStreams.Buffered | 16.349 |
| Benchmark | net8.0 | IterationTotal | cached | load | KNetStreams.Buffered.Prefetch | 16.677 |
| Benchmark | net8.0 | IterationTotal | cached | load | KNetStreams.Raw | 16.901 |
| Benchmark | net8.0 | IterationTotal | cached | load | KNetStreams.Raw.Prefetch | 16.366 |
| Benchmark | net8.0 | IterationTotal | cached | load | KafkaStreams.Buffered | 16.346 |
| Benchmark | net8.0 | IterationTotal | cached | load | KafkaStreams.Raw | 16.347 |
| Benchmark | net9.0 | IterationTotal | non-cached | load | KNetStreams.Buffered | 133.006 |
| Benchmark | net9.0 | IterationTotal | non-cached | load | KNetStreams.Buffered.Prefetch | 157.205 |
| Benchmark | net9.0 | IterationTotal | non-cached | load | KNetStreams.Raw | 92.192 |
| Benchmark | net9.0 | IterationTotal | non-cached | load | KNetStreams.Raw.Prefetch | 118.593 |
| Benchmark | net9.0 | IterationTotal | non-cached | load | KafkaStreams.Buffered | 132.957 |
| Benchmark | net9.0 | IterationTotal | non-cached | load | KafkaStreams.Raw | 91.713 |
| Benchmark | net9.0 | IterationTotal | cached | load | KNetStreams.Buffered | 17.174 |
| Benchmark | net9.0 | IterationTotal | cached | load | KNetStreams.Buffered.Prefetch | 16.248 |
| Benchmark | net9.0 | IterationTotal | cached | load | KNetStreams.Raw | 17.235 |
| Benchmark | net9.0 | IterationTotal | cached | load | KNetStreams.Raw.Prefetch | 18.219 |
| Benchmark | net9.0 | IterationTotal | cached | load | KafkaStreams.Buffered | 17.293 |
| Benchmark | net9.0 | IterationTotal | cached | load | KafkaStreams.Raw | 16.744 |
| Complex | net10.0 | IterationTotal | non-cached | load | KNetStreams.Raw | 247.949 |
| Complex | net10.0 | IterationTotal | non-cached | reload | KNetStreams.Raw | 226.194 |
| Complex | net10.0 | IterationTotal | cached | load | KNetStreams.Raw | 218.278 |
| Complex | net10.0 | IterationTotal | cached | reload | KNetStreams.Raw | 201.298 |
| Complex | net10.0 | ScalarOnlyProjection | non-cached | load | KNetStreams.Raw | 0.249 |
| Complex | net10.0 | ScalarOnlyProjection | non-cached | reload | KNetStreams.Raw | 0.342 |
| Complex | net10.0 | ScalarOnlyProjection | cached | load | KNetStreams.Raw | 0.254 |
| Complex | net10.0 | ScalarOnlyProjection | cached | reload | KNetStreams.Raw | 0.330 |
| Complex | net8.0 | IterationTotal | non-cached | load | KNetStreams.Raw | 290.206 |
| Complex | net8.0 | IterationTotal | non-cached | reload | KNetStreams.Raw | 277.198 |
| Complex | net8.0 | IterationTotal | cached | load | KNetStreams.Raw | 262.687 |
| Complex | net8.0 | IterationTotal | cached | reload | KNetStreams.Raw | 256.419 |
| Complex | net8.0 | ScalarOnlyProjection | non-cached | load | KNetStreams.Raw | 0.303 |
| Complex | net8.0 | ScalarOnlyProjection | non-cached | reload | KNetStreams.Raw | 0.383 |
| Complex | net8.0 | ScalarOnlyProjection | cached | load | KNetStreams.Raw | 0.286 |
| Complex | net8.0 | ScalarOnlyProjection | cached | reload | KNetStreams.Raw | 0.368 |
| Complex | net9.0 | IterationTotal | non-cached | load | KNetStreams.Raw | 239.744 |
| Complex | net9.0 | IterationTotal | non-cached | reload | KNetStreams.Raw | 221.893 |
| Complex | net9.0 | IterationTotal | cached | load | KNetStreams.Raw | 252.182 |
| Complex | net9.0 | IterationTotal | cached | reload | KNetStreams.Raw | 224.035 |
| Complex | net9.0 | ScalarOnlyProjection | non-cached | load | KNetStreams.Raw | 0.283 |
| Complex | net9.0 | ScalarOnlyProjection | non-cached | reload | KNetStreams.Raw | 0.344 |
| Complex | net9.0 | ScalarOnlyProjection | cached | load | KNetStreams.Raw | 0.279 |
| Complex | net9.0 | ScalarOnlyProjection | cached | reload | KNetStreams.Raw | 0.357 |

Full breakdown: [performance benchmarks](src/documentation/articles/benchmarks.md).
<!-- PERFORMANCE-SUMMARY:END -->

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

> [!NOTE]
> [JCOBridge 2.6.\*](https://www.jcobridge.com) can be used for free without any obligations. A commercial license must be purchased — or the software uninstalled — if you derive direct or indirect income from its usage.

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
