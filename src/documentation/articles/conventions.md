---
title: Conventions in KEFCore
_description: Describes the model conventions available in Entity Framework Core provider for Apache Kafka™
---

# KEFCore: conventions

KEFCore uses EF Core model finalization conventions to resolve configuration declared via attributes or fluent API into model annotations. This approach ensures that topic names, event management, and ComplexType configuration are determined at model build time rather than at runtime.

All conventions are registered automatically in `KEFCoreConventionSetBuilder` and require no additional setup.

## Topic naming convention

`KEFCoreTopicNamingConvention` resolves the Kafka topic name for each entity type and stores it as a model annotation. The resolved name is then used consistently throughout the provider — in topic creation, producer routing, and Streams topology — without requiring options to be passed through the call stack.

### Topic name resolution priority

1. `KEFCoreTopicAttribute` applied directly to the entity class
2. `TableAttribute` applied to the entity class, including schema prefix if specified (e.g. `schema.tablename`)
3. The EF Core entity type name (`IReadOnlyTypeBase.Name`), which includes the model namespace

### Topic prefix resolution priority

1. `KEFCoreTopicPrefixAttribute` applied directly to the entity class — a `null` prefix explicitly disables prefixing for that entity
2. `KEFCoreTopicPrefixAttribute` applied to the `DbContext` class
3. `modelBuilder.UseKEFCoreTopicPrefix()` set in `OnModelCreating`
4. No prefix (default)

### Usage examples

```csharp
// Attribute on entity — explicit topic name
[KEFCoreTopicAttribute("my-orders")]
public class Order { ... }

// Attribute on entity — explicit prefix
[KEFCoreTopicPrefixAttribute("production")]
public class Product { ... }

// Attribute on entity — disable prefix for this entity
[KEFCoreTopicPrefixAttribute(null)]
public class InternalLog { ... }

// Attribute on DbContext — prefix for all entities
[KEFCoreTopicPrefixAttribute("myapp")]
public class MyDbContext : KEFCoreDbContext { ... }

// Fluent API — global prefix
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.UseKEFCoreTopicPrefix("myapp");
}

// Fluent API — per-entity topic name
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Order>().ToKEFCoreTopic("my-orders");
    modelBuilder.Entity<Product>().HasKEFCoreTopicPrefix("catalog");
}
```

> [!IMPORTANT]
> If neither `KEFCoreTopicAttribute` nor `TableAttribute` is applied, the topic name falls back to `IReadOnlyTypeBase.Name` which includes the full model namespace. This means a namespace refactoring will change the topic name and break alignment with existing data in the cluster. It is strongly recommended to always use `[Table]` or `[KEFCoreTopicAttribute]` for stable topic names.

## Event management convention

`KEFCoreManageEventsConvention` resolves whether the `TimestampExtractor` is activated for each entity type, enabling real-time tracking updates from the Apache Kafka™ cluster. Event management is **enabled by default** for all entity types.

### Resolution priority

1. `KEFCoreIgnoreEventsAttribute` applied to the entity class — always disables events for that entity
2. `HasKEFCoreManageEvents(false)` applied via fluent API — disables events for that entity
3. `modelBuilder.UseKEFCoreManageEvents(false)` — disables events globally
4. `true` (default — events enabled)

### Usage examples

```csharp
// Disable events for a specific entity via attribute
[KEFCoreIgnoreEventsAttribute]
public class ReadOnlyLookup { ... }

// Disable events for a specific entity via fluent API
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<ReadOnlyLookup>().HasKEFCoreManageEvents(false);
}

// Disable events globally
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.UseKEFCoreManageEvents(false);
}
```

> [!NOTE]
> When event management is disabled for an entity, post-`SaveChanges` synchronization (`EnsureSynchronized`) is not available for that entity because the `TimestampExtractor` is not active.

## ComplexType equality convention

`KEFCoreComplexTypeEquatableConvention` verifies at model finalization time that all ComplexTypes in the model implement `IEquatable<T>` or override `Equals(object)`. This is required because KEFCore relies on value equality to detect changes in ComplexType properties — without it, reference equality would cause incorrect change detection and unnecessary Kafka writes.

If a ComplexType does not satisfy the requirement, an `InvalidOperationException` is thrown at startup with an actionable message.

### Opt-out

Apply `KEFCoreIgnoreEquatableCheckAttribute` to a ComplexType class to suppress the check when value equality is guaranteed by other means:

```csharp
[KEFCoreIgnoreEquatableCheckAttribute]
public class MySpecialAddress { ... }
```

## ComplexType converter convention

`KEFCoreComplexTypeConverterConvention` registers `IComplexTypeConverter` implementations declared via attribute or fluent API into `IComplexTypeConverterFactory` at model finalization time, making the converter available to the serialization subsystem without any additional startup code.

### Usage examples

```csharp
// Attribute on ComplexType class
[KEFCoreComplexTypeConverterAttribute(typeof(AddressConverter))]
public class Address { ... }

// Fluent API on ComplexPropertyBuilder
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Invoice>()
                .ComplexProperty(i => i.ShippingAddress)
                .HasKEFCoreComplexTypeConverter<AddressConverter>();
}
```

The converter type must implement `IComplexTypeConverter`. If declared via fluent API, it takes precedence over the attribute.
