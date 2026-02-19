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

```C#
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
> The model use the [TableAttribute](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.schema.tableattribute) to define the topic to be used, this will be important later due to a change in namespace of the model.
> For more info see [Data storage](howitworks.md#^data-storage)

### First application run

Considering the applcation is using the JSON serialization just for simplicity, the value stored on [Apache Kafka™](https://kafka.apache.org/) cluster are:

- [DBName].Simple.Blog topic:
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

- [DBName].Simple.Post topic:
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

## Evolving your model

A few days have passed, and you're asked to add a creation timestamp (**Date**) to your blogs removing the **Url**. 
You've done the necessary changes to your application, and your model now looks like this:

```C#
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
```C#
var post = context.Posts.Include(o => o.Blog).Single(b => b.BlogId == 20);
```
the result will be:
- Post:
  ```C#
  post.ToString();
  ```
  
  ```sh
  PostId: 20 Title: title Content: 19 BlogId: 20
  ```
- Blog:
  ```C#
  post.Blog.ToString();
  ```
  
  ```sh
  BlogId: 20 Rating: 19 Date: 
  ```


The previous stored data was read and serializer identified:
- removed properties: the unmanaged content of the stored data was discarded (**Url** in the example above) and nothing was returned to EF Core
- added properties: the new property was not identified and nothing was done, each new property is returned to EF Core with its default value: in the example above **Date** is returned as _null_

> [!IMPORTANT]
> Current implementation does not support type change, i.e. a property with the same name, but different type 

## How new data are managed

The application will continue its execution and new information will be stored.
The new one will use 
