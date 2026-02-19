---
title: Entity Framework Core provider for Apache Kafka™
_description: Main page of Entity Framework Core provider for Apache Kafka™
---

# KEFCore: [Entity Framework Core](https://learn.microsoft.com/ef/core/) provider for [Apache Kafka™](https://kafka.apache.org/)

KEFCore is the [Entity Framework Core](https://learn.microsoft.com/ef/core/) provider for [Apache Kafka™](https://kafka.apache.org/).
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

Looking for the help of [Entity Framework Core](https://learn.microsoft.com/ef/core/) and [Apache Kafka™](https://kafka.apache.org/) experts? MASES Group can help you design, build, deploy, and manage [Entity Framework Core](https://learn.microsoft.com/ef/core/) and [Apache Kafka™](https://kafka.apache.org/) applications.

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

The project is based on available information within the official [EntityFrameworkCore repository](https://github.com/dotnet/efcore), many classes was copied from there as reported in the official documentation within the Microsoft website at https://docs.microsoft.com/ef/core/providers/writing-a-provider.

Currently the project tries to support, at our best, the [official supported Apache Kafka™ binary distribution](https://kafka.apache.org/downloads):

| KEFCore | State | KNet | Apache Kafka™ | .NET | JVM™ |
|:---:	|:---:	|:---:	|:---:	|:---:	|:---:	|
| 2.6.*+ | Active | 3.1.x | 4.1.x | [![.NET 8+](https://img.shields.io/badge/.NET-8%2B-purple)](https://dotnet.microsoft.com/) | [![Java 17+](https://img.shields.io/badge/Java-17%2B-blue)](https://www.oracle.com/java/) |
| 2.5.* | Deprecated | 2.9.x | 3.9.x | [![.NET 8+](https://img.shields.io/badge/.NET-8%2B-purple)](https://dotnet.microsoft.com/) | [![Java 11+](https://img.shields.io/badge/Java-11%2B-blue)](https://www.oracle.com/java/) |

### Community and Contribution

Do you like the project? 
- Request your free [community subscription](https://www.jcobridge.com/pricing-25/).

Do you want to help us?
- put a :star: on this project
- open [issues](https://github.com/masesgroup/KEFCore/issues) to request features or report bugs :bug:
- improves the project with Pull Requests

This project adheres to the Contributor [Covenant code of conduct](https://github.com/masesgroup/KEFCore/blob/master/CODE_OF_CONDUCT.md). By participating, you are expected to uphold this code. Please report unacceptable behavior to coc_reporting@masesgroup.com.
