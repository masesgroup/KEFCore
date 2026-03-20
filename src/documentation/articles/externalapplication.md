---
title: External applications of KEFCore
_description: Describes how to use data managed by Entity Framework Core provider for Apache Kafka™ from external applications
---

# KEFCore: external application

[Entity Framework Core](https://learn.microsoft.com/ef/core/) provider for [Apache Kafka™](https://kafka.apache.org/) converts the entities used within the model into a format suitable for the backend. Continuing from the concepts introduced in [serialization](serialization.md), an external application can consume data stored in the topics and get back the original CLR entity objects using the `EntityExtractor` helper class.

> [!IMPORTANT]
> Until the first major version, all releases are considered not stable: public and internal APIs may change without notice.

## Basic concepts

An external application may want to be informed about data changes in topics and reconstruct the entity objects that were previously managed by the EF Core application. `EntityExtractor` provides methods to consume records from Apache Kafka™ topics and return the entity objects with all properties populated.

## Identifying the correct topic name

The topic name to subscribe to is the same name resolved by `KEFCoreTopicNamingConvention` at model finalization time. The resolution priority is:

1. `KEFCoreTopicAttribute` on the entity class
2. `TableAttribute` on the entity class (including schema prefix, e.g. `schema.tablename`)
3. The EF Core entity type name including full namespace

The topic name is then optionally prefixed via `KEFCoreTopicPrefixAttribute` or `UseKEFCoreTopicPrefix()`. The full resolved name always follows the pattern `[prefix].[resolved-topic-name]`.

For example, given:
```csharp
[Table("Blog", Schema = "Simple")]
public class Blog { ... }
```
with `TopicPrefix = "TestDB"`, the topic name is `TestDB.Simple.Blog`.

See [conventions](conventions.md#topic-naming-convention) for full details.

## Basic usage

The simplest case — no ComplexType properties, topic name provided explicitly:

```csharp
KEFCore.CreateGlobalInstance();

EntityExtractor.FromTopic(
    "MY-KAFKA-BROKER:9092",
    "TestDB.Simple.Blog",
    (entity, exception) =>
    {
        if (exception != null) Console.Error.WriteLine(exception.Message);
        if (entity != null) Console.WriteLine(entity);
    },
    cancellationToken);
```

## ComplexType support and IModel integration

When entity types contain ComplexType properties, `EntityExtractor` needs an `IComplexTypeConverterFactory` populated with the converters declared in the model. It also optionally accepts an `IModel` for accurate property mapping via `IEntityType`.

Both are obtained by building the KEFCore model standalone outside of `DbContext.OnModelCreating`:

```csharp
KEFCore.CreateGlobalInstance();

// build the model standalone — populates the converter factory
var modelBuilder = KEFCoreConventionSetBuilder.CreateModelBuilder(out var converterFactory);

// register the same entity types as in the DbContext
modelBuilder.Entity<Blog>();
modelBuilder.Entity<Post>();

// finalize the model — applies all KEFCore conventions including topic name resolution
IModel model = modelBuilder.FinalizeModel();

// pass factory and model to EntityExtractor
EntityExtractor.FromTopic<Blog>(
    "MY-KAFKA-BROKER:9092",
    "TestDB.Simple.Blog",
    (entity, exception) =>
    {
        if (exception != null) Console.Error.WriteLine(exception.Message);
        if (entity != null) Console.WriteLine(entity);
    },
    cancellationToken,
    converterFactory: converterFactory,
    model: model);
```

## Automatic topic name resolution

When `IModel` is provided, the topic name can be omitted — `EntityExtractor` resolves it automatically from the model annotation set by `KEFCoreTopicNamingConvention`:

```csharp
// topicName not required — resolved from model
EntityExtractor.FromTopic<Blog>(
    "MY-KAFKA-BROKER:9092",
    topicName: null,
    (entity, exception) => { ... },
    cancellationToken,
    converterFactory: converterFactory,
    model: model);
```

> [!NOTE]
> `topicName` null or whitespace triggers automatic resolution. An explicit non-empty value always takes precedence.

## Processing individual records

```csharp
var record = ...; // ConsumerRecord<byte[], byte[]> from KafkaConsumer

// without model — basic deserialization
var entity = EntityExtractor.FromRecord(record);

// with model and factory — full ComplexType support
var entity = EntityExtractor.FromRecord(record,
    converterFactory: converterFactory,
    model: model);

// typed
var blog = EntityExtractor.FromRecord<Blog>(record,
    converterFactory: converterFactory,
    model: model);
```

## Mandatory runtime dependencies

`EntityExtractor.FromTopic` and `FromRecord` use reflection to recover the serializer and entity types from the record headers. The following must be loaded in memory:

- The assembly containing the serializer — if using the default JSON serializer this is automatically available
- The assembly containing the model entity types (`Blog`, `Post`, etc.)

## Performance

The internal extractor cache ensures that `MakeGenericType`, `Activator.CreateInstance` and delegate compilation happen only once per unique combination of `(keyType, valueContainerType, keySerDesType, valueSerDesType, converterFactory, model)`. Subsequent records of the same type invoke only the compiled delegate — zero reflection overhead at runtime per record.

## Possible usages

For possible usages of [Entity Framework Core](https://learn.microsoft.com/ef/core/) provider for [Apache Kafka™](https://kafka.apache.org/), and this feature, see [use cases](usecases.md).
