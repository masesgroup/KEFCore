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
        // Optional: layer a topic prefix on top of [Table] resolution.
        // Without this call, topics would simply be named "Blog" / "Post".
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

## Secure broker connections

For brokers that require TLS encryption or SASL authentication (common in production and managed cloud environments), use the `WithSecurityProtocol()`, `WithSslConfig()`, and `WithSaslConfig()` fluent options:

```csharp
optionsBuilder.UseKEFCore(opt => opt
    .WithBootstrapServers("KAFKA-SERVER:9093")
    .WithApplicationId("MyApp")
    .WithSecurityProtocol(SecurityProtocol.SASL_SSL)
    .WithSslConfig(new SslConfigsBuilder()
        .WithSslTruststoreLocation("/path/to/truststore.jks")
        .WithSslTruststorePassword(new Password("truststore-password")))
    .WithSaslConfig(new SaslConfigsBuilder()
        .WithSaslMechanism("PLAIN")
        .WithSaslJaasConfig(new Password(
            "org.apache.kafka.common.security.plain.PlainLoginModule required " +
            "username=\"myuser\" password=\"mypassword\";")))
);
```

See [options — secure broker connections](options.md#secure-broker-connections) for the full protocol matrix and configuration reference.

## Topic naming

Each entity maps to a Kafka topic. The topic name is resolved at model build time by `KEFCoreTopicNamingConvention`. With the example above the topics are:

- `TestDB.Blog` (prefix `TestDB` + `[Table("Blog")]`)
- `TestDB.Post` (prefix `TestDB` + `[Table("Post")]`)

The `TestDB` prefix comes entirely from `modelBuilder.UseKEFCoreTopicPrefix("TestDB")` above — it is not a `DbName`/`DatabaseName` property on the context. If no prefix is configured, the topics are simply `Blog` and `Post`.

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

## Resetting local and cluster-side state

KEFCore's Kafka Streams topology (identified by `ApplicationId`) keeps state both on the cluster (consumer-group membership and offsets) and, when `UsePersistentStorage = true`, in a local RocksDB store. Restarting an application — especially soon after a crash, a forced shutdown, or a heavy load test — can leave that state stale: Kafka may not have expired the previous session yet, and/or the local RocksDB store may be out of sync with what the topology expects on the next startup.

`ResetStreams` on `KEFCoreDbContext` clears **both** sides in one call:

- cluster-side consumer-group/Streams application state associated with the context's `ApplicationId`
- the local RocksDB-backed state store, if `UsePersistentStorage = true`

```csharp
using var context = new BloggingContext()
{
    BootstrapServers = "MY-KAFKA-BROKER:9092",
    ApplicationId = "MyAppId",
    UsePersistentStorage = true,
};

context.ResetStreams();       // clear stale cluster + local state before (re)starting the topology
context.Database.EnsureCreated();
```

> [!IMPORTANT]
> `ResetStreams` is **not** the same as `context.Database.EnsureDeleted()`. `EnsureDeleted()` deletes the underlying Kafka topics themselves — all data, for every application sharing them. `ResetStreams` only clears this application's own Streams/consumer-group state and local cache; it never touches topic data.

Typical situations where `ResetStreams` is needed:

- **Same-application restarts**, not just switching between different applications: e.g. a test-reload run immediately after a load test can hit consumer-group state Kafka hasn't cleaned up yet for that `ApplicationId`. Calling `ResetStreams` before re-initializing clears it explicitly instead of waiting on Kafka's own session-timeout expiry.
- **Local development/testing** where you want a clean local RocksDB cache without wiping the shared topic data other applications rely on.

See [KEFCoreDbContext](kefcoredbcontext.md#resetting-local-and-cluster-side-state) for the full API reference, and the note on `ApplicationId` uniqueness below.

### `ApplicationId` must be unique per application process

`ApplicationId` identifies the Kafka Streams application/topology and doubles as the underlying consumer-group id. **It must be distinct per application process**, even when multiple applications intentionally read and write the same entities/topics on the same cluster (the "distributed cache" pattern — see [use cases](usecases.md)).

Giving two application processes the *same* `ApplicationId` does not make them cooperate on one shared, fully-replicated view. Instead, Kafka treats them as members of a single consumer group and divides partitions between them — each process only sees a subset of the data, and a process can appear to receive no data at all if Kafka still considers a previous instance an active group member. This is the scenario `ResetStreams` is designed to recover from (see above).

## ComplexType usage

EF Core [ComplexTypes](https://learn.microsoft.com/ef/core/modeling/complex-types) are fully supported. KEFCore requires ComplexTypes to implement value equality. A converter can be registered for types that need explicit control over their serializer-native representation — with JSON serialization this is optional (an unconverted ComplexType falls back to being handled as a plain POCO); with Avro/Protobuf a converter is recommended for anything beyond a trivial POCO, though even there an unconverted ComplexType still round-trips via an internal JSON-string fallback rather than failing:

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