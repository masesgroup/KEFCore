---
title: Usage of KEFCore
_description: Describes how to use Entity Framework Core provider for Apache Kafka™
---

# KEFCore usage

Read [Getting started](gettingstarted.md) to find out info and tips.

## Basic example

The following code demonstrates basic usage of [Entity Framework Core](https://learn.microsoft.com/ef/core/) provider for [Apache Kafka™](https://kafka.apache.org/).
For a full tutorial to configure the `KafkaDbContext`, define the model, and create the database, see [KEFCoreDbContext](kefcoredbcontext.md) in the docs.

```cs
using (var context = new BloggingContext()
{
    BootstrapServers = serverToUse,
    ApplicationId = "TestApplication",  // mandatory — unique per process on the cluster
    DbName = "TestDB",
})
{
    // Inserting data into the database
    context.Add(new Blog { Url = "http://blogs.msdn.com/adonet" });
    context.SaveChanges();

    // Querying
    var blog = context.Blogs
        .OrderBy(b => b.BlogId)
        .First();

    // Updating
    blog.Url = "https://devblogs.microsoft.com/dotnet";
    blog.Posts.Add(
        new Post
        {
            Title = "Hello World",
            Content = "I wrote an app using EF Core!"
        });
    context.SaveChanges();

    // Deleting
    context.Remove(blog);
    context.SaveChanges();
}

public class BloggingContext : KafkaDbContext
{
    public DbSet<Blog> Blogs { get; set; }
    public DbSet<Post> Posts { get; set; }
}

// [Table] stabilizes the Kafka topic name across namespace refactorings
[Table("Blog", Schema = "Blogging")]
public class Blog
{
    public int BlogId { get; set; }
    public string Url { get; set; }
    public long Rating { get; set; }
    public List<Post> Posts { get; set; }
}

[Table("Post", Schema = "Blogging")]
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

By default, KEFCore resolves the Kafka topic name for each entity at model finalization time.
The resolution priority is: `[KEFCoreTopicAttribute]` → `[TableAttribute]` → entity type name (includes full namespace).

It is strongly recommended to always use `[Table]` or `[KEFCoreTopicAttribute]` to ensure stable topic names across refactorings. A topic prefix can be set globally or per-entity:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // global prefix for all entities
    modelBuilder.UseKEFCoreTopicPrefix("myapp");

    // or per-entity explicit topic name
    modelBuilder.Entity<Blog>().ToKEFCoreTopic("my-blogs");

    base.OnModelCreating(modelBuilder);
}
```

Alternatively use attributes:

```cs
[KEFCoreTopicPrefixAttribute("myapp")]
public class BloggingContext : KafkaDbContext { ... }

[KEFCoreTopicAttribute("my-blogs")]
public class Blog { ... }
```

See [conventions](conventions.md#topic-naming-convention) for the full resolution priority.

## Event management

By default, KEFCore enables real-time event management (via `TimestampExtractor`) for all entity types. This allows the local state to be updated as new records arrive from the Apache Kafka™ cluster, and enables post-`SaveChanges` synchronization.

To disable events for a specific entity:

```cs
// via attribute
[KEFCoreIgnoreEventsAttribute]
public class ReadOnlyLookup { ... }

// or via fluent API
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<ReadOnlyLookup>().HasKEFCoreManageEvents(false);
    base.OnModelCreating(modelBuilder);
}
```

To disable events globally:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.UseKEFCoreManageEvents(false);
    base.OnModelCreating(modelBuilder);
}
```

> [!NOTE]
> When event management is disabled for an entity, post-`SaveChanges` synchronization (`DefaultSynchronizationTimeout`) is not available for that entity.

See [conventions](conventions.md#event-management-convention) for full details.

## ComplexType usage

EF Core [ComplexTypes](https://learn.microsoft.com/ef/core/modeling/complex-types) are fully supported. KEFCore requires ComplexTypes to implement value equality (either `IEquatable<T>` or override `Equals`). A converter can be registered to handle serialization of types that are not natively supported by the underlying serializer:

```cs
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

Or via fluent API in `OnModelCreating`:

```cs
modelBuilder.Entity<Order>()
            .ComplexProperty(o => o.ShippingAddress)
            .HasKEFCoreComplexTypeConverter<AddressConverter>();
```

See [serialization](serialization.md#complex-type-serialization) and [conventions](conventions.md#complextype-converter-convention) for full details.

## Possible usages

For possible usages of [Entity Framework Core](https://learn.microsoft.com/ef/core/) provider for [Apache Kafka™](https://kafka.apache.org/), and this feature, see [use cases](usecases.md).
