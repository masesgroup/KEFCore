---
title: How KEFCore works
_description: Describes how works Entity Framework Core provider for Apache Kafka™
---

# KEFCore: migration

The current version of [Entity Framework Core](https://learn.microsoft.com/ef/core/) provider for [Apache Kafka™](https://kafka.apache.org/) does not support [migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations) using an external tool: the schema evolution is intrinsecally available from the serialization structure used.

In real world projects, data models change as features get implemented: new entities or properties are added and removed, and database schemas need to be changed accordingly to be kept in sync with the application.
KEFCore provides a way to incrementally update the schema to keep it in sync with the application's data model while preserving existing data in the [Apache Kafka™](https://kafka.apache.org/) cluster.

## Getting started

Let's assume you've just completed your first EF Core application based on KEFCore, which contains the following simple model:

```csharp
namespace MASES.EntityFrameworkCore.KNet.Test.Common.Model.Base
{
    [PrimaryKey("BlogId")]
    [Table("Blog", Schema = "Simple")]
    public class Blog
    {
        public int BlogId { get; set; }
        public string Url { get; set; }
        public int Rating { get; set; }

        public List<Post> Posts { get; set; }

        public override string ToString()
        {
            return $"BlogId: {BlogId} Url: {Url} Rating: {Rating}";
        }
    }

    [PrimaryKey("PostId")]
    [Table("Post", Schema = "Simple")]
    public class Post
    {
        public int PostId { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }

        public int BlogId { get; set; }
        public Blog Blog { get; set; }

        public override string ToString()
        {
            return $"PostId: {PostId} Title: {Title} Content: {Content} BlogId: {BlogId}";
        }
    }
}
```

> [!TIP]
> The model uses the [TableAttribute](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.schema.tableattribute) or [KEFCoreTopicAttribute](conventions.md#topic-naming-convention) to define the topic to be used — this will be important later due to a change in namespace of the model.
> Without one of these attributes, the topic name is derived from the full CLR type name including namespace: a namespace refactoring will silently change the topic name and break alignment with existing data.
> No topic prefix is configured in this example (see [KEFCoreDbContext](kefcoredbcontext.md#topic-naming-conventions) for the optional `[KEFCoreTopicPrefixAttribute]`/`UseKEFCoreTopicPrefix()` mechanism), so the resulting topic names are just `Simple.Blog` and `Simple.Post`.
> For more info see [Data storage](howitworks.md#data-storage)

### First application run

Considering the applcation is using the JSON serialization just for simplicity, the value stored on [Apache Kafka™](https://kafka.apache.org/) cluster are:

- `Simple.Blog` topic:

```json
{
  "EntityName": "MASES.EntityFrameworkCore.KNet.Test.Common.Model.Base.Blog",
  "ClrType": "MASES.EntityFrameworkCore.KNet.Test.Common.Model.Base.Blog, MASES.EntityFrameworkCore.KNet.Test.Common",
  "Properties": [
    {
      "PropertyName": "BlogId",
      "ManagedType": 11,
      "SupportNull": false,
      "Value": 976
    },
    {
      "PropertyName": "Rating",
      "ManagedType": 11,
      "SupportNull": false,
      "Value": 975
    },
    {
      "PropertyName": "Url",
      "ManagedType": 1,
      "SupportNull": true,
      "Value": "http://blogs.msdn.com/adonet975"
    }
  ]
}
```

- `Simple.Post` topic:

```json
{
  "EntityName": "MASES.EntityFrameworkCore.KNet.Test.Common.Model.Base.Post",
  "ClrType": "MASES.EntityFrameworkCore.KNet.Test.Common.Model.Base.Post, MASES.EntityFrameworkCore.KNet.Test.Common",
  "Properties": [
    {
      "PropertyName": "PostId",
      "ManagedType": 11,
      "SupportNull": false,
      "Value": 976
    },
    {
      "PropertyName": "BlogId",
      "ManagedType": 11,
      "SupportNull": false,
      "Value": 976
    },
    {
      "PropertyName": "Content",
      "ManagedType": 1,
      "SupportNull": true,
      "Value": "975"
    },
    {
      "PropertyName": "Title",
      "ManagedType": 1,
      "SupportNull": true,
      "Value": "title"
    }
  ]
}
```

> [!NOTE]
> If a topic prefix were configured (e.g. via `modelBuilder.UseKEFCoreTopicPrefix("MyPrefix")`), the topic names above would instead be `MyPrefix.Simple.Blog` and `MyPrefix.Simple.Post`. The prefix is always an explicit, opt-in convention — there is no implicit database-name-derived prefix.

## Evolving your model

A few days have passed, and you're asked to add a creation timestamp (**Date**) to your blogs removing the **Url**.
You've done the necessary changes to your application, and your model now looks like this:

```csharp
namespace MASES.EntityFrameworkCore.KNet.Test.Common.Model.Evolved
{
    [PrimaryKey("BlogId")]
    [Table("Blog", Schema = "Simple")]
    public class Blog
    {
        public int BlogId { get; set; }
        public int Rating { get; set; }
        public DateTime? Date { get; set; }

        public List<Post> Posts { get; set; }

        public override string ToString()
        {
            return $"BlogId: {BlogId} Rating: {Rating} Date: {Date}";
        }
    }

    [PrimaryKey("PostId")]
    [Table("Post", Schema = "Simple")]
    public class Post
    {
        public int PostId { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }

        public int BlogId { get; set; }
        public Blog Blog { get; set; }

        public override string ToString()
        {
            return $"PostId: {PostId} Title: {Title} Content: {Content} BlogId: {BlogId}";
        }
    }
}
```

now you think:

- I have to rebuild everything?
- What happens to my stored information?

No problem: KEFCore will automagically manages yuor changes without any interruption!!!

### How old data are retrieved?

When the application try to retrieve the information using something like:

```csharp
var post = context.Posts.Include(o => o.Blog).Single(b => b.BlogId == 20);
```

the result will be:

- Post:

```csharp
post.ToString();
```

```text
PostId: 20 Title: title Content: 19 BlogId: 20
```

- Blog:

```csharp
post.Blog.ToString();
```

```text
BlogId: 20 Rating: 19 Date:
```

The previous stored data was read and serializer identified:

- removed properties: the unmanaged content of the stored data was discarded (**Url** in the example above) and nothing was returned to EF Core
- added properties: the new property was not identified and nothing was done, each new property is returned to EF Core with its default value: in the example above **Date** is returned as *null*

> [!IMPORTANT]
> Current implementation does not support type change, i.e. a property with the same name, but different type

## How new data are managed

The application will continue its execution and new information will be stored.
The new one will use the current model's schema: at write time, KEFCore serializes a record by walking the `IProperty` set EF Core resolves from the *current* model — so a newly-written `Blog` includes `Rating` and `Date`, and no longer includes `Url`, simply because `Url` is no longer one of the model's `IProperty` entries. The record's on-topic schema is a direct reflection of whatever `IProperty` collection the model exposes at the moment of the write, not a fixed or inherited structure.

Existing records already in the topic are **not** rewritten just because the model changed. A given `Blog` record keeps its original, old-schema serialized form until the next time *that specific record* goes through `SaveChanges` again — at that point it is re-serialized from the current model's `IProperty` set like any other write, so it picks up the new schema at that point. There is no separate migration or backfill step: the normal read/update cycle of the application is what gradually carries individual records forward from the old schema to the new one, one record at a time, as they are naturally touched again.