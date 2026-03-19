---
title: Troubleshooting KEFCore
_description: Common errors and solutions for Entity Framework Core provider for Apache Kafka™
---

# KEFCore: troubleshooting

## `SingletonOptionChanged` at startup

**Symptom**: An `InvalidOperationException` with message containing `SingletonOptionChanged` is thrown when creating a `DbContext`.

**Cause**: You are using `UseInternalServiceProvider` and two `DbContext` instances have been configured with different singleton options (e.g. different `ApplicationId`, `BootstrapServers`, or serialization types) on the same shared Service Provider.

**Solution**: Ensure all singleton options are identical across all `DbContext` instances that share the same Service Provider. See [options](options.md#singleton-options) for the full list. If you need different singleton options, use separate Service Providers.

---

## `ClusterId currently not available`

**Symptom**: `InvalidOperationException: ClusterId currently not available from <BootstrapServers>`.

**Cause**: KEFCore resolves `ClusterId` by connecting to the Kafka broker when the Service Provider is first initialized. If the broker is not reachable at that point, the resolution fails.

**Solution**:
- Verify the broker address in `BootstrapServers` is correct and reachable from the application host.
- If you are running integration tests or building the model outside a live cluster, use `KEFCoreClusterAdmin.DisableClusterInvocation = true` before calling `KEFCoreConventionSetBuilder.Build()` or `CreateModelBuilder()`.

---

## Topic name changed after namespace refactoring

**Symptom**: After moving entity classes to a different namespace, the application no longer reads existing data from the cluster, or creates new topics instead of reusing existing ones.

**Cause**: Without `[Table]` or `[KEFCoreTopicAttribute]`, the topic name is derived from the EF Core entity type `Name` property which includes the full CLR namespace. Renaming the namespace changes the topic name.

**Solution**: Always decorate entity classes with `[Table("name", Schema = "schema")]` or `[KEFCoreTopicAttribute("name")]` to make the topic name independent of the CLR namespace. See [conventions](conventions.md#topic-naming-convention) and [migration](migration.md) for details.

---

## `ComplexType must implement IEquatable or override Equals`

**Symptom**: `InvalidOperationException` at startup identifying a specific ComplexType class.

**Cause**: `KEFCoreComplexTypeEquatableConvention` detected a ComplexType that uses reference equality (the .NET default). KEFCore relies on value equality to detect changes — without it, two logically identical instances are treated as different, causing unnecessary Kafka writes.

**Solution**: Implement `IEquatable<T>` or override `Equals(object)` on the ComplexType:

```csharp
[ComplexType]
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

If equality is guaranteed by other means, apply `[KEFCoreIgnoreEquatableCheckAttribute]` to suppress the check. See [conventions](conventions.md#complextype-equality-convention).

---

## `ApplicationId` conflict across processes

**Symptom**: Two processes sharing the same `ApplicationId` and cluster do not each have a complete view of all entity data — queries return partial results.

**Cause**: Apache Kafka™ Streams assigns partitions across all consumers sharing the same `ApplicationId`. Each process receives only a subset of partitions and therefore has an incomplete local state store.

**Solution**: Use a distinct `ApplicationId` for each process. The `ApplicationId` is a singleton option — all `DbContext` instances within the same process share it, but different processes must use different values. See [options](options.md#singleton-options).

---

## Post-`SaveChanges` synchronization timeout

**Symptom**: Operations after `SaveChanges` read stale data, or `DefaultSynchronizationTimeout` expires.

**Cause**: KEFCore waits for the Streams state store to catch up with the latest produced offset after each `SaveChanges`. If the store is under load or the timeout is too short, this wait expires.

**Solutions**:
- Increase `DefaultSynchronizationTimeout` (in milliseconds) or set it to `Timeout.Infinite` to wait indefinitely.
- Verify that event management is enabled for the affected entity (`KEFCoreIgnoreEventsAttribute` disables synchronization for that entity).
- If synchronization is not needed (read-only consumers), set `DefaultSynchronizationTimeout = 0` to disable it.

---

## `StreamsManager` not starting / state errors

**Symptom**: `InvalidOperationException` mentioning Streams state (`PENDING_ERROR`, `ERROR`, `NOT_RUNNING`).

**Cause**: The Kafka Streams topology failed to start, often due to broker connectivity issues, incompatible `StreamsConfig`, or a previous unclean shutdown leaving corrupt RocksDB state.

**Solutions**:
- Check broker connectivity and `StreamsConfig.BootstrapServers`.
- If using `UsePersistentStorage = true`, the RocksDB state directory may be corrupt — delete it and let the store rebuild from the topics.
- Check the application logs for the `StreamsUncaughtExceptionHandler` message which identifies the root cause.
