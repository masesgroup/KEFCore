---
title: Options in KEFCore
_description: Describes the options available in Entity Framework Core provider for Apache Kafka™
---

# KEFCore: options

KEFCore separates its options into two categories: **singleton-scoped** and **context-scoped**. Understanding this separation is important to avoid unexpected Service Provider cache misses or configuration errors at startup.

## Why the separation matters

EF Core caches the internal Service Provider (the DI container that holds provider services) and reuses it across `DbContext` instances that share the same configuration. KEFCore keys this cache on the physical Kafka cluster identity (`ClusterId`, resolved from `BootstrapServers`) plus the singleton options that affect which services are registered.

Two `DbContext` instances that share all singleton options will reuse the same Service Provider — and the same `StreamsManager`. Two instances with different singleton options will create separate Service Providers.

Context-scoped options do not affect the Service Provider cache. They are read per-`DbContext` instance at runtime.

## Singleton options

These options must be consistent across all `DbContext` instances pointing to the same physical Apache Kafka™ cluster. Changing any of these between two contexts on the same cluster will cause a new Service Provider to be created, or a `SingletonOptionChanged` exception if `UseInternalServiceProvider` is in use.

### In hash (affect Service Provider cache key)

| Option | Default | Notes |
|---|---|---|
| `BootstrapServers` | — | Mandatory. Resolved to `ClusterId` for caching |
| `ApplicationId` | — | Mandatory when not using `UseCompactedReplicator`. Unique per process on the cluster |
| `KeySerDesSelectorType` | `DefaultKEFCoreSerDes.DefaultKeySerialization` | Generic type definition — overridable per entity via `KEFCoreSerDesAttribute` or `HasKEFCoreSerDes()` |
| `ValueSerDesSelectorType` | `DefaultKEFCoreSerDes.DefaultValueContainerSerialization` | Generic type definition — overridable per entity |
| `ValueContainerType` | `DefaultKEFCoreSerDes.DefaultValueContainer` | Generic type definition — overridable per entity |
| `UseKeyByteBufferDataTransfer` | `false` | Determines key transport type in Streams topology |
| `UseValueContainerByteBufferDataTransfer` | `false` | Determines value transport type in Streams topology |
| `UseKNetStreams` | `true` | KNet vs plain Kafka Streams backend |
| `UsePersistentStorage` | `false` | RocksDB vs in-memory store supplier |
| `UseCompactedReplicator` | `false` | ⚠️ Deprecated |

### First-wins (singleton but not in hash)

These are cluster-level but do not affect the Service Provider cache key. The values set by the first `DbContext` to initialize the cluster are used for all subsequent contexts.

| Option | Default | Notes |
|---|---|---|
| `UseDeletePolicyForTopic` | `false` | Applied at topic creation time |
| `DefaultNumPartitions` | `1` | Applied at topic creation time — overridable per entity via `KEFCoreTopicPartitionsAttribute` or `HasKEFCoreTopicPartitions()` |
| `DefaultReplicationFactor` | `1` | Applied at topic creation time — overridable per entity via `KEFCoreTopicReplicationFactorAttribute` or `HasKEFCoreTopicReplicationFactor()` |
| `TopicConfig` | `null` | Cluster-level topic configuration — retention can be overridden per entity via `KEFCoreTopicRetentionAttribute` or `HasKEFCoreTopicRetention()` |
| `StreamsConfig` | `null` | Kafka Streams application configuration |
| `ProducerConfig` | `null` | Cluster-level producer configuration (first-wins) — individual settings can be overridden per entity via `KEFCoreProducerAttribute` or `HasKEFCoreProducer()` |
| `SecurityProtocol` | `null` | Security protocol for broker connections — set to `SSL`, `SASL_PLAINTEXT`, or `SASL_SSL` to enable encrypted or authenticated connections; must be consistent with `SslConfig` and `SaslConfig` |
| `SslConfig` | `null` | SSL/TLS configuration built via `SslConfigsBuilder` — required when `SecurityProtocol` is `SSL` or `SASL_SSL`; applied automatically to all internal Kafka clients via `WithSslConfigs()` |
| `SaslConfig` | `null` | SASL authentication configuration built via `SaslConfigsBuilder` — required when `SecurityProtocol` is `SASL_PLAINTEXT` or `SASL_SSL`; applied automatically to all internal Kafka clients via `WithSaslConfigs()` |

## Secure broker connections

`SecurityProtocol`, `SslConfig`, and `SaslConfig` are cluster-level first-wins options that configure authentication and encryption for all internal Kafka clients. When set, `SslConfig` and `SaslConfig` are merged automatically into `ProducerOptionsBuilder()` and `StreamsOptions()` via `WithSslConfigs()` and `WithSaslConfigs()` — no additional per-client configuration is needed.

The example below shows `SASL_SSL` — TLS encryption combined with SASL/PLAIN authentication, the most common configuration for managed cloud brokers (Amazon MSK, Confluent Cloud, Aiven, etc.):

```csharp
optionsBuilder.UseKEFCore(opt => opt
    .WithBootstrapServers("KAFKA-SERVER:9093")
    .WithApplicationId("MyApp")
    .WithSecurityProtocol(SecurityProtocol.SASL_SSL)
    .WithSslConfig(SslConfigsBuilder.Create()
        .WithSslTruststoreLocation("/path/to/truststore.jks")
        .WithSslTruststorePassword(new Password("truststore-password"))
        .WithSslKeystoreLocation("/path/to/keystore.jks")
        .WithSslKeystorePassword(new Password("keystore-password")))
    .WithSaslConfig(SaslConfigsBuilder.Create()
        .WithSaslMechanism("PLAIN")
        .WithSaslJaasConfig(new Password(
            "org.apache.kafka.common.security.plain.PlainLoginModule required " +
            "username=\"myuser\" password=\"mypassword\";")))
);
```

Or via `KEFCoreDbContext` properties:

```csharp
using var context = new BloggingContext()
{
    BootstrapServers = "KAFKA-SERVER:9093",
    ApplicationId = "MyApp",
    SecurityProtocol = SecurityProtocol.SASL_SSL,
    SslConfig = SslConfigsBuilder.Create()
        .WithSslTruststoreLocation("/path/to/truststore.jks")
        .WithSslTruststorePassword(new Password("truststore-password")),
    SaslConfig = SaslConfigsBuilder.Create()
        .WithSaslMechanism("PLAIN")
        .WithSaslJaasConfig(new Password(
            "org.apache.kafka.common.security.plain.PlainLoginModule required " +
            "username=\"myuser\" password=\"mypassword\";")),
};
```

For brokers that only require TLS (no SASL), use `SecurityProtocol.SSL` and omit `WithSaslConfig`. For SCRAM-based authentication, replace `"PLAIN"` with `"SCRAM-SHA-256"` or `"SCRAM-SHA-512"` and the corresponding JAAS login module (`ScramLoginModule`). The default SASL mechanism when `SaslConfig` is set without an explicit mechanism is `GSSAPI` (Kerberos).

| `SecurityProtocol` value | When to use |
|---|---|
| `PLAINTEXT` (default) | Unencrypted, unauthenticated — development and trusted networks only |
| `SSL` | TLS encryption, no SASL — requires `SslConfig`; omit `SaslConfig` |
| `SASL_PLAINTEXT` | SASL authentication, no TLS — requires `SaslConfig`; omit `SslConfig` |
| `SASL_SSL` | TLS + SASL — requires both `SslConfig` and `SaslConfig` |

> [!IMPORTANT]
> These options are **first-wins singleton** — the values set by the first `DbContext` to initialize the cluster are used for all subsequent contexts on the same cluster. Ensure all `DbContext` instances that share the same cluster use the same security configuration.

> [!NOTE]
> `SslConfig` and `SaslConfig` do **not** participate in the Service Provider cache key hash — they are connection-level parameters, not service registration parameters. Two contexts with the same cluster identity and different SSL configurations will see the first-wins values applied without creating a separate Service Provider.

> [!NOTE]
> `SslConfigsBuilder` and `SaslConfigsBuilder` expose typed properties for every Kafka SSL and SASL configuration key. Refer to the [Apache Kafka™ security documentation](https://kafka.apache.org/documentation/#security) and the KNet API documentation for the full list of available properties.

## Context-scoped options

These options are specific to each `DbContext` instance and do not affect the Service Provider cache.

| Option | Default | Notes |
|---|---|---|
| `ReadOnlyMode` | `false` | Rejects all write operations for the entire context — individual entities can be marked read-only via `KEFCoreReadOnlyAttribute` or `IsKEFCoreReadOnly()` |
| `DefaultSynchronizationTimeout` | `Timeout.Infinite` | Milliseconds to wait after `SaveChanges` for backend sync; `0` disables sync |
| `UseEnumeratorWithPrefetch` | `true` | Prefetch enumerator for faster enumeration with Streams |
| `UseStorePrefixScan` | `false` | Enables prefix scan globally — overridable per entity via `KEFCoreStoreLookupAttribute` or `HasKEFCoreStoreLookup()` |
| `UseStoreSingleKeyLookup` | `true` | Enables single key look-up globally — overridable per entity |
| `UseStoreKeyRange` | `true` | Enables key range look-up globally — overridable per entity |
| `UseStoreReverse` | `true` | Enables reverse look-up globally — overridable per entity |
| `UseStoreReverseKeyRange` | `true` | Enables reverse key range look-up globally — overridable per entity |

### Deprecated context-scoped options

| Option | Replacement |
|---|---|
| `ManageEvents` | `KEFCoreIgnoreEventsAttribute` / `HasKEFCoreManageEvents()` |
| `UseGlobalTable` | Removed — `ApplicationId` uniqueness per process makes it unnecessary |
| `ConsumerConfig` | No replacement — deprecated with `UseCompactedReplicator` |
| `DefaultConsumerInstances` | No replacement — deprecated with `UseCompactedReplicator` |

## Topic naming and event management

`TopicPrefix`, `ManageEvents`, and topic name configuration are no longer options — they are model conventions resolved at model finalization time. See [conventions](conventions.md) for details.
