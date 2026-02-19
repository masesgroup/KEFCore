---
title: How KEFCore works
_description: Describes how works Entity Framework Core provider for Apache Kafka™
---

# KEFCore: how it works

[Entity Framework Core](https://learn.microsoft.com/ef/core/) provider for [Apache Kafka™](https://kafka.apache.org/) can be used in some [operative conditions](usecases.md).

It is important to start with a simple description on how it works.
In the following chapters sometime it is used the term back-end and sometime Apache Kafka™ cluster: they shall be considered the same thing int the [Entity Framework Core](https://learn.microsoft.com/ef/core/) provider for [Apache Kafka™](https://kafka.apache.org/) context.

## Basic concepts

Here below an image from Wikipedia describing simple concepts:

![Alt text](https://upload.wikimedia.org/wikipedia/commons/6/64/Overview_of_Apache_Kafka.svg "Kafka™ basic concepts")

Simplifying there are three active elements:
- **Topics**: storage of the records (the data), they are hosted in the Apache Kafka™ cluster and can be partitioned
- **Producers**: entities producing records to be stored in one or more topics
- **Consumers**: entities receiving records from the topics

When a producer send a record to Apache Kafka™ cluster, the record will be sent to the consumers subscribed to the topics the producer is producing on: this is a classic pub-sub pattern.
Apache Kafka™ cluster adds the ability to store this information within the topic the producer has produced on, this feature guarantee that:
- an application consuming from the Apache Kafka™ cluster can hear only latest changes or position to a specific position in the past and start from that point to receive data
- the standard way to consume from Apache Kafka™ cluster is to start from the end (latest available record) or start from the beginning (first available record)

## How [Entity Framework Core](https://learn.microsoft.com/ef/core/) provider for [Apache Kafka™](https://kafka.apache.org/) works

An application based on [Entity Framework Core](https://learn.microsoft.com/ef/core/) provider for [Apache Kafka™](https://kafka.apache.org/) is both a producer and a consumer at the same time:
- when an entity is created/updated/deleted (e.g. calling [SaveChanges](https://learn.microsoft.com/ef/core/saving/basic)) the provider will invoke the right producer to store a new record in the right topic of the Apache Kafka™ cluster
- then the consumer subscribed will be informed about this new record and will store it back: this seems not useful till now, but it will be more clear later

Apache Kafka™ cluster becomes:
1. a central routing for data changes in [Entity Framework Core](https://learn.microsoft.com/ef/core/) based applications.
2. a reliable storage because, when the application restarts, the data stored in the topics will be read back from the consumers so the state will be aligned to the latest available.

Apache Kafka™ comes with [topic compaction](https://kafka.apache.org/documentation/#compaction) feature, thanks to it the point 2 is optimized.
[Entity Framework Core](https://learn.microsoft.com/ef/core/) provider for [Apache Kafka™](https://kafka.apache.org/) is interested to store only the latest state of the entity and not the changes.
Using the [topic compaction](https://kafka.apache.org/documentation/#compaction), the combination of producer, consumer and Apache Kafka™ cluster can apply the CRUD operations on data:
- Create: a producer stores a new record with a unique key
- Read: a consumer retrieves records from topic
- Update: a producer storing a new record with a previously stored unique key will discard the old records
- Delete: a producer storing a new record with a previously stored unique key, and value set to null, will delete all records with that unique key

All CRUD operations are helped, behind the scene, from ~~[`KNetCompactedReplicator`](https://github.com/masesgroup/KNet/blob/master/src/net/KNet/Specific/Replicator/KNetCompactedReplicator.cs) or~~ [`KNetProducer`](https://github.com/masesgroup/KNet/blob/master/src/net/KNet/Specific/Producer/KNetProducer.cs)/[Apache Kafka™ Streams](https://kafka.apache.org/documentation/streams/).

### First-level cache

~~[`KNetCompactedReplicator`](https://github.com/masesgroup/KNet/blob/master/src/net/KNet/Specific/Replicator/KNetCompactedReplicator.cs) or~~ [Apache Kafka™ Streams](https://kafka.apache.org/documentation/streams/) act as first-level cache of [Entity Framework Core](https://learn.microsoft.com/ef/core/): **data coming from the Apache Kafka™ cluster updates their content while the system is running**.
The behavior is intrinsic and does not need any extra call to the back-end.

### Data storage

Apache Kafka™ stores the information using records. It is important to convert entities in something usable from Apache Kafka™.

At first glance each Entity is translated into a topic in Apache Kafka™; consider the following simple model extracted from [Model.cs](https://github.com/masesgroup/KEFCore/blob/53baf6aa6a875a4eee7291d9b16c3495fda07f1f/test/Common/Model.cs):

```C#
namespace MASES.EntityFrameworkCore.KNet.Test.Model
{
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
used in:
```C#
namespace MASES.EntityFrameworkCore.KNet.Test
{
    public class BloggingContext : KafkaDbContext
    {
        public override string DatabaseName => "Test";

        public DbSet<Blog> Blogs { get; set; }
        public DbSet<Post> Posts { get; set; }
    }
}
```

each entity maps to a specific topic using the feature added with https://github.com/masesgroup/KEFCore/issues/417:
- Blog belongs to the topic named [DatabaseName].Simple.Blog
- Post belongs to the topic named [DatabaseName].Simple.Post

where DatabaseName is the property defined from `BloggingContext` see [KafkaDbContext](kafkadbcontext.md).

> [!IMPORTANT]
> The entities Blog and Post are decorated with [TableAttribute](https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations.schema.tableattribute), if the attribute is missing the topic name becomes different and use the [**Name**](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.metadata.ireadonlytypebase.name) property of the Entity which belongs to the namespace defining it:
> - Blog belongs to the topic named [DatabaseName].MASES.EntityFrameworkCore.KNet.Test.Model.Blog
> - Post belongs to the topic named [DatabaseName].MASES.EntityFrameworkCore.KNet.Test.Model.Post

Established how [Apache Kafka™](https://kafka.apache.org/) stores the data it is important to understand how the storage is filled with the data in the model.
Each element in the [DbSet](https://learn.microsoft.com/dotnet/api/system.data.entity.dbset-1), in the above example there are **Blogs** and **Posts**, is converted in something can be used from [Apache Kafka™](https://kafka.apache.org/).
The conversion is done using serializers that converts the Entities (data in the model) into [Apache Kafka™](https://kafka.apache.org/) records and viceversa: see [serialization chapter](serialization.md) for more info.
The serializers are the glue between [Entity Framework Core](https://learn.microsoft.com/ef/core/) and [Apache Kafka™](https://kafka.apache.org/).

## [Entity Framework Core](https://learn.microsoft.com/ef/core/) provider for [Apache Kafka™](https://kafka.apache.org/) compared to other providers

In the previous chapter was described how [Entity Framework Core](https://learn.microsoft.com/ef/core/) provider for [Apache Kafka™](https://kafka.apache.org/) permits to reproduce the CRUD operations.
Starting from the model defined in the code, the data are stored in the topics and each topic can be seen as a table of a database filled in with the same data.
From the point of view of an application, the use of [Entity Framework Core](https://learn.microsoft.com/ef/core/) provider for [Apache Kafka™](https://kafka.apache.org/) is similar to the use of the InMemory provider.

### A note on [migrations](https://learn.microsoft.com/ef/core/managing-schemas/migrations)

The current version of [Entity Framework Core](https://learn.microsoft.com/ef/core/) provider for [Apache Kafka™](https://kafka.apache.org/) does not support [migrations](https://learn.microsoft.com/ef/core/managing-schemas/migrations) using an external tool: the schema evolution is intrinsecally available from the serialization structure used.

Read more about migration in [KEFCore migration](migration.md).

## [Entity Framework Core](https://learn.microsoft.com/ef/core/) provider for [Apache Kafka™](https://kafka.apache.org/) features not available in other providers

Here a list of features [Entity Framework Core](https://learn.microsoft.com/ef/core/) provider for [Apache Kafka™](https://kafka.apache.org/) gives to its user and useful in some use cases.
The features below are strictly correlated with the consumers receiving back the record from Apache Kafka™ cluster described above.

### Distributed cache

In the previous chapter was stated that consumers align the application data to the last topics information.
The alignment is managed from ~~[`KNetCompactedReplicator`](https://github.com/masesgroup/KNet/blob/master/src/net/KNet/Specific/Replicator/KNetCompactedReplicator.cs) and/or~~ [Apache Kafka™ Streams](https://kafka.apache.org/documentation/streams/), everything is driven from the Apache Kafka™ back-end.
Considering two, or more, applications, sharing the same model and configuration, they always align to the latest state of the topics involved.
This implies that, virtually, there is a distributed cache between the applications and the Apache Kafka™ back-end:
- Apache Kafka™ stores physically the cache (shared state) within the topics and routes changes to the subscribed applications
- Applications use latest cache version (local state) received from Apache Kafka™ back-end

If an application restarts it will be able to retrieve latest data (latest cache) and aligns to the shared state.

![Alt text](../images/cache.gif "Distributed cache")

### Events

Generally, an application based on [Entity Framework Core](https://learn.microsoft.com/ef/core/), executes queries to the back-end to store, or retrieve, information on demand.
The alignment (record consumed) can be considered a change event: so any change in the backend produces an event used in different mode:
- Mainly these change events are used from ~~[`KNetCompactedReplicator`](https://github.com/masesgroup/KNet/blob/master/src/net/KNet/Specific/Replicator/KNetCompactedReplicator.cs) and/or~~ [Apache Kafka™ Streams](https://kafka.apache.org/documentation/streams/) to align the local state;
- Moreover [Entity Framework Core](https://learn.microsoft.com/ef/core/) provider for [Apache Kafka™](https://kafka.apache.org/) can inform, using callbacks and at zero cost, the registered application about these events.

Then the application can use the reported events in many modes:
- execute a query
- write something to disk
- execute a REST call
- and so on

![Alt text](../images/events.gif "Distributed cache")

> **IMPORTANT NOTE**: the events are raised from external threads and this can lead to [concurrent exceptions](https://learn.microsoft.com/ef/core/dbcontext-configuration/#avoiding-dbcontext-threading-issues) if the `KafkaDbContext` is used to retrieve information.

### Applications not based on [Entity Framework Core](https://learn.microsoft.com/ef/core/)

Till now was spoken about applications based on [Entity Framework Core](https://learn.microsoft.com/ef/core/), however this provider can be used to feed applications not based on [Entity Framework Core](https://learn.microsoft.com/ef/core/).
[Entity Framework Core](https://learn.microsoft.com/ef/core/) provider for [Apache Kafka™](https://kafka.apache.org/) comes with ready-made helping classes to subscribe to any topic of the Apache Kafka™ cluster to retrieve the data stored from an application based on [Entity Framework Core](https://learn.microsoft.com/ef/core/).
Any application can use this feature to:
- read latest data stored in the topics from the application based on [Entity Framework Core](https://learn.microsoft.com/ef/core/) 
- attach to the topics involved from the application based on [Entity Framework Core](https://learn.microsoft.com/ef/core/) and receive change events upon something was produced 

The ready-made helping classes upon a record is received, deserialize it and returns back the filled Entity.
