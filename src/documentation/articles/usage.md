---
title: Usage of KEFCore
_description: Describes how to use Entity Framework Core provider for Apache Kafka™
---

# KEFCore usage

Read [Getting started](gettingstarted.md) to find out info and tips.

## Basic example

The following code demonstrates basic usage of [Entity Framework Core](https://learn.microsoft.com/ef/core/) provider for [Apache Kafka™](https://kafka.apache.org/).. 
For a full tutorial to configure the `KafkaDbContext`, defines the model, and creates the database, see [KafkaDbContext](kafkadbcontext.md) in the docs.

```cs
using (var context = new BloggingContext()
{
    BootstrapServers = serverToUse,
    ApplicationId = "TestApplication",
    DbName = "TestDB",
})
{
    // Inserting data into the database
    db.Add(new Blog { Url = "http://blogs.msdn.com/adonet" });
    db.SaveChanges();

    // Querying
    var blog = db.Blogs
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
    db.SaveChanges();

    // Deleting
    db.Remove(blog);
    db.SaveChanges();
}

public class BloggingContext : KafkaDbContext
{
    public DbSet<Blog> Blogs { get; set; }
    public DbSet<Post> Posts { get; set; }
}

public class Blog
{
    public int BlogId { get; set; }
    public string Url { get; set; }
    public long Rating { get; set; }
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

## Possible usages

For possible usages of [Entity Framework Core](https://learn.microsoft.com/ef/core/) provider for [Apache Kafka™](https://kafka.apache.org/), and this feature, see [use cases](usecases.md)
