# EFCoreKafka usage

> NOTE: you need a working Apache Kafka cluster to use this provider.

### Installation

EF Core for Apache Kafka is available on [NuGet](https://www.nuget.org/packages/MASES.EntityFrameworkCore.Kafka):

```sh
dotnet add package MASES.EntityFrameworkCore.Kafka
```

### Basic usage

The following code demonstrates basic usage of EF Core for Apache Kafka. 
For a full tutorial configuring the `DbContext`, defining the model, and creating the database, see [getting started](https://docs.microsoft.com/ef/core/get-started/) in the docs.

```cs
using (var db = new BloggingContext())
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

public class BloggingContext : DbContext
{
	readonly string _serverToUse;
	public BloggingContext(string serverToUse)
	{
		_serverToUse = serverToUse;
	}

	public DbSet<Blog> Blogs { get; set; }
	public DbSet<Post> Posts { get; set; }

	protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
	{
		optionsBuilder.UseKafkaDatabase("TestDB", _serverToUse, (o) =>
		{
			o.AutoOffsetReset();
		});
	}

}
```