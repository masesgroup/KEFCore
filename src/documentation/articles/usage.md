---
title: Usage of KEFCore
_description: Describes how to use Entity Framework Core provider for Apache Kafka™
---

# KEFCore usage

Read [Getting started](gettingstarted.md) to find out info and tips.

## Backend compatibility

[Entity Framework Core](https://learn.microsoft.com/ef/core/) provider for [Apache Kafka™](https://kafka.apache.org/) uses the official Apache Kafka™ Java client packages directly through [KNet client-side features](https://github.com/masesgroup/KNet).
All examples in this page use standard Producer, Consumer, and Admin Client APIs, which communicate with the broker exclusively through the Kafka wire protocol.

This means the code shown here works with **any broker that implements the Kafka wire protocol** — not only Apache Kafka™ itself. Examples of compatible brokers: [Redpanda](https://redpanda.com/), [Amazon MSK](https://aws.amazon.com/msk/), [Confluent Platform / Cloud](https://www.confluent.io/), [Aiven for Apache Kafka™](https://aiven.io/kafka), [IBM Event Streams](https://www.ibm.com/products/event-streams), [WarpStream](https://www.warpstream.com/), [AutoMQ](https://www.automq.com/), and others.

See [Supported Backends](backends.md) for the full compatibility matrix covering all KNet feature areas.

## Mandatory runtime initialization

Before any interaction with [Entity Framework Core](https://learn.microsoft.com/ef/core/) provider for [Apache Kafka™](https://kafka.apache.org/), the KNet runtime must be initialized. This step starts the JVM™, loads the Kafka libraries, and sets up the JVM↔CLR interop layer.

```csharp
// Must be called once at application startup, before any DbContext is created
KEFCore.CreateGlobalInstance();
```

JVM heap settings can be configured before the call:

```csharp
KEFCore.ApplicationHeapSize = "4G";
KEFCore.ApplicationInitialHeapSize = "512M";
KEFCore.CreateGlobalInstance();
```

> [!IMPORTANT]
> `KEFCore.CreateGlobalInstance()` must be called **before** any `DbContext` is created, before `EnsureCreated()`, and before any LINQ query. Everything after this point follows standard EF Core patterns.

See [Getting started](gettingstarted.md) for JVM identification and environment setup details.

## Basic example

After the runtime is initialized, KEFCore follows standard [Entity Framework Core](https://learn.microsoft.com/ef/core/) patterns. The only difference from other providers is that `KEFCoreDbContext` exposes Kafka-specific properties (`BootstrapServers`, `ApplicationId`, etc.) that can be set like any other property.

```csharp
KEFCore.CreateGlobalInstance();

using var context = new BloggingContext()
{
    BootstrapServers = "MY-KAFKA-BROKER:9092",
    ApplicationId = "MyAppId",  // mandatory — must be unique per process on the cluster
    DbName = "TestDB",
};

// Ensure topics exist (standard EF Core)
context.Database.EnsureCreated();

// Insert
context.Add(new Blog { Url = "http://blogs.msdn.com/adonet", Rating = 5 });
context.SaveChanges();

// Query
var blog = context.Blogs.OrderBy(b => b.BlogId).First();

// Update
blog.Url = "https://devblogs.microsoft.com/dotnet";
blog.Posts.Add(new Post { Title = "Hello World", Content = "I wrote an app using EF Core!" });
context.SaveChanges();

// Delete
context.Remove(blog);
context.SaveChanges();

public class BloggingContext : KEFCoreDbContext
{
    public DbSet<Blog> Blogs { get; set; }
    public DbSet<Post> Posts { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UseKEFCoreTopicPrefix("TestDB");
        base.OnModelCreating(modelBuilder);
    }
}

// [Table] stabilizes the Kafka topic name across namespace refactorings
[Table("Blog")]
public class Blog
{
    public int BlogId { get; set; }
    public string Url { get; set; }
    public long Rating { get; set; }
    public List<Post> Posts { get; set; }
}

[Table("Post")]
public class Post
{
    public int PostId { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }

    public int BlogId { get; set; }
    public Blog Blog { get; set; }
}
```

## Topic naming

Each entity maps to a Kafka topic. The topic name is resolved at model build time by `KEFCoreTopicNamingConvention`. With the example above the topics are:
- `TestDB.Blog` (prefix `TestDB` + `[Table("Blog")]`)
- `TestDB.Post` (prefix `TestDB` + `[Table("Post")]`)

Without `[Table]`, the topic name includes the full .NET namespace — a namespace refactoring would break alignment with existing data. See [conventions](conventions.md#topic-naming-convention) for the full resolution priority.

## Event management

By default, KEFCore enables real-time event management for all entity types. The local state is updated as new records arrive from the cluster, and post-`SaveChanges` synchronization is available.

To disable events for a specific entity:

```csharp
[KEFCoreIgnoreEventsAttribute]
[Table("ReadOnlyLookup")]
public class ReadOnlyLookup { ... }

// or via fluent API
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<ReadOnlyLookup>().HasKEFCoreManageEvents(false);
    base.OnModelCreating(modelBuilder);
}
```

See [conventions](conventions.md#event-management-convention) for full details.

## ComplexType usage

EF Core [ComplexTypes](https://learn.microsoft.com/ef/core/modeling/complex-types) are fully supported. KEFCore requires ComplexTypes to implement value equality. A converter can be registered for types not natively supported by the underlying serializer:

```csharp
[ComplexType]
[KEFCoreComplexTypeConverterAttribute(typeof(AddressConverter))]
public class Address : IEquatable<Address>
{
    public string Street { get; set; }
    public string City { get; set; }

    public bool Equals(Address other)
        => other != null && Street == other.Street && City == other.City;

    public override bool Equals(object obj) => Equals(obj as Address);
    public override int GetHashCode() => HashCode.Combine(Street, City);
}
```

See [serialization](serialization.md#complex-type-serialization) for full details.

## Possible usages

For possible usages of [Entity Framework Core](https://learn.microsoft.com/ef/core/) provider for [Apache Kafka™](https://kafka.apache.org/), see [use cases](usecases.md).
