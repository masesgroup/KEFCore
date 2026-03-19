/*
*  Copyright (c) 2022-2026 MASES s.r.l.
*
*  Licensed under the Apache License, Version 2.0 (the "License");
*  you may not use this file except in compliance with the License.
*  You may obtain a copy of the License at
*
*  http://www.apache.org/licenses/LICENSE-2.0
*
*  Unless required by applicable law or agreed to in writing, software
*  distributed under the License is distributed on an "AS IS" BASIS,
*  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
*  See the License for the specific language governing permissions and
*  limitations under the License.
*
*  Refer to LICENSE for more information.
*/

// #define DEBUG_PERFORMANCE

using MASES.EntityFrameworkCore.KNet.Extensions;
using MASES.EntityFrameworkCore.KNet.Serialization;
using MASES.EntityFrameworkCore.KNet.Serialization.Json;
using MASES.EntityFrameworkCore.KNet.Serialization.Json.Storage;
using MASES.EntityFrameworkCore.KNet.Storage;
using MASES.EntityFrameworkCore.KNet.Storage.Internal;
using MASES.KNet.Common;
using MASES.KNet.Consumer;
using MASES.KNet.Producer;
using MASES.KNet.Serialization;
using MASES.KNet.Streams;

namespace MASES.EntityFrameworkCore.KNet.Infrastructure;

/// <summary>
///     A <see cref="KEFCoreDbContext"/> instance represents a session with the Apache Kafka� cluster and can be used to query and save
///     instances of your entities. <see cref="KEFCoreDbContext"/> extends <see cref="DbContext"/> and it is a combination of the Unit Of Work and Repository patterns.
/// </summary>
/// <remarks>
///     <para>
///         Entity Framework Core does not support multiple parallel operations being run on the same <see cref="KEFCoreDbContext"/> instance. This
///         includes both parallel execution of async queries and any explicit concurrent use from multiple threads.
///         Therefore, always await async calls immediately, or use separate DbContext instances for operations that execute
///         in parallel. See <see href="https://aka.ms/efcore-docs-threading">Avoiding DbContext threading issues</see> for more information
///         and examples.
///     </para>
///     <para>
///         Typically you create a class that derives from DbContext and contains <see cref="DbSet{TEntity}" />
///         properties for each entity in the model. If the <see cref="DbSet{TEntity}" /> properties have a public setter,
///         they are automatically initialized when the instance of the derived context is created.
///     </para>
///     <para>
///         Typically you don't need to override the <see cref="OnConfiguring(DbContextOptionsBuilder)" /> method to configure the database (and
///         other options) to be used for the context, just set the properties associated to <see cref="KEFCoreDbContext"/> class.
///         Alternatively, if you would rather perform configuration externally instead of inline in your context, you can use <see cref="DbContextOptionsBuilder{TContext}" />
///         (or <see cref="DbContextOptionsBuilder" />) to externally create an instance of <see cref="DbContextOptions{TContext}" />
///         (or <see cref="DbContextOptions" />) and pass it to a base constructor of <see cref="DbContext" />.
///     </para>
///     <para>
///         The model is discovered by running a set of conventions over the entity classes found in the
///         <see cref="DbSet{TEntity}" /> properties on the derived context. To further configure the model that
///         is discovered by convention, you can override the <see cref="DbContext.OnModelCreating(ModelBuilder)" /> method.
///     </para>
///     <para>
///         See <see href="https://masesgroup.github.io/KEFCore/articles/kefcoredbcontext.html">KEFCoreDbContext configuration and initialization</see>,
///         <see href="https://aka.ms/efcore-docs-dbcontext">DbContext lifetime, configuration, and initialization</see>,
///         <see href="https://aka.ms/efcore-docs-query">Querying data with EF Core</see>,
///         <see href="https://aka.ms/efcore-docs-change-tracking">Changing tracking</see>, and
///         <see href="https://aka.ms/efcore-docs-saving-data">Saving data with EF Core</see> for more information and examples.
///     </para>
/// </remarks>
public class KEFCoreDbContext : DbContext
{
    /// <summary>
    ///     The default <see cref="ConsumerConfig"/> configuration
    /// </summary>
    /// <returns>The default <see cref="ConsumerConfig"/> configuration.</returns>
    [Obsolete("Option will be removed soon")]
    public static ConsumerConfigBuilder DefaultConsumerConfig => ConsumerConfigBuilder.Create().WithEnableAutoCommit(true)
                                                                                               .WithAutoOffsetReset(ConsumerConfigBuilder.AutoOffsetResetTypes.EARLIEST)
                                                                                               .WithAllowAutoCreateTopics(false);
    /// <summary>
    ///     The default <see cref="ProducerConfig"/> configuration
    /// </summary>
    /// <returns>The default <see cref="ProducerConfig"/> configuration.</returns>
    public static ProducerConfigBuilder DefaultProducerConfig => ProducerConfigBuilder.Create();
    /// <summary>
    ///     The default <see cref="StreamsConfig"/> configuration
    /// </summary>
    /// <returns>The default <see cref="StreamsConfig"/> configuration.</returns>
    public static StreamsConfigBuilder DefaultStreamsConfig => StreamsConfigBuilder.Create();
    /// <summary>
    ///     The default <see cref="TopicConfig"/> configuration
    /// </summary>
    /// <returns>The default <see cref="TopicConfig"/> configuration.</returns>
    public static TopicConfigBuilder DefaultTopicConfig => TopicConfigBuilder.Create().WithDeleteRetentionMs(100)
                                                                                      .WithMinCleanableDirtyRatio(0.01)
                                                                                      .WithSegmentMs(100)
                                                                                      .WithRetentionBytes(1073741824);

    /// <inheritdoc cref="DbContext.DbContext()"/>
    public KEFCoreDbContext()
    {

    }
    /// <inheritdoc cref="DbContext.DbContext(DbContextOptions)"/>
    public KEFCoreDbContext(DbContextOptions options) : base(options)
    {

    }
    /// <summary>
    /// The optional <see cref="Type"/> to use for key serialization selection
    /// <para>
    /// Default value is <see cref="DefaultKEFCoreSerDes.Key{T}"/>, any custom <see cref="Type"/> shall implement <see cref="ISerDesSelector{T}"/>
    /// </para>
    /// </summary>
    public virtual Type? KeySerDesSelectorType { get; set; } = null;
    /// <summary>
    /// The optional <see cref="Type"/> to use for value serialization selection
    /// <para>
    /// Default value is <see cref="DefaultKEFCoreSerDes.ValueContainer{T}"/>, any custom <see cref="Type"/> shall implement <see cref="ISerDesSelector{T}"/>
    /// </para>
    /// </summary>
    public virtual Type? ValueSerDesSelectorType { get; set; } = null;
    /// <summary>
    /// The optional <see cref="Type"/> to use as value container
    /// <para>
    /// Default value is <see cref="DefaultValueContainer{T}"/>, any custom <see cref="Type"/> shall implement <see cref="IValueContainer{T}"/>
    /// </para>
    /// </summary>
    public virtual Type? ValueContainerType { get; set; } = null;
    /// <summary>
    /// Set to <see langword="false"/> to avoid match of <see cref="IEntityType"/>s using <see cref="IReadOnlyTypeBase.Name"/>
    /// </summary>
    public virtual bool UseNameMatching { get; set; } = true;
    /// <summary>
    /// The bootstrap servers of the Apache Kafka� cluster
    /// </summary>
    public virtual string? BootstrapServers { get; set; }
    /// <summary>
    /// The application id associated to the application in use, shall be set if <see cref="UseCompactedReplicator"/> is <see langword="false"/>
    /// </summary>
    /// <remarks>Choose the value carefully: upon restart its value is used to identify information on Apache Kafka� cluster of previous running application.
    /// If <see cref="UseGlobalTable"/> is <see langword="false"/> the partitions associated to each topic are shared across all instances with the same <see cref="ApplicationId"/> so be carefull to avoid other application consumes the data from Apache Kafka� cluster and local stores does not contains the expected information.</remarks>
    public virtual string? ApplicationId { get; set; }
    /// <summary>
    /// Default number of partitions associated to each topic
    /// </summary>
    public virtual int DefaultNumPartitions { get; set; } = 1;
    /// <summary>
    /// Default replication factor associated to each topic
    /// </summary>
    public virtual short DefaultReplicationFactor { get; set; } = 1;
    /// <summary>
    /// Default consumr instances used in conjunction with <see cref="UseCompactedReplicator"/>
    /// </summary>
    [Obsolete("Option will be removed soon")] 
    public virtual int? DefaultConsumerInstances { get; set; } = null;
    /// <summary>
    /// Use persistent storage when Apache Kafka� Streams is in use
    /// </summary>
    public virtual bool UsePersistentStorage { get; set; } = false;
    /// <summary>
    /// Setting this property to <see langword="true"/> the engine prefers to use enumerator instances able to do a prefetch on data speeding up execution
    /// </summary>
    /// <remarks>Used only if <see cref="UseCompactedReplicator"/> is <see langword="false"/> and <see cref="UseKNetStreams"/> is <see langword="true"/>, not available in EFCore 6.</remarks>
    public virtual bool UseEnumeratorWithPrefetch { get; set; } = false;
    /// <summary>
    /// Setting this property to <see langword="true"/> the engine prefers to use <see cref="Java.Nio.ByteBuffer"/> data exchange in serializer instances for the key
    /// </summary>
    public virtual bool UseKeyByteBufferDataTransfer { get; set; } = false;
    /// <summary>
    /// Setting this property to <see langword="true"/> the engine prefers to use <see cref="Java.Nio.ByteBuffer"/> data exchange in serializer instances for value buffer
    /// </summary>
    public virtual bool UseValueContainerByteBufferDataTransfer { get; set; } = false;
    /// <summary>
    /// Use <see href="https://kafka.apache.org/documentation/#topicconfigs_cleanup.policy">delete cleanup policy</see> when a topic is created
    /// </summary>
    public bool UseDeletePolicyForTopic { get; set; } = false;
    /// <summary>
    /// Use <see cref="MASES.KNet.Replicator.KNetCompactedReplicator{TKey, TValue}"/> instead of Apache Kafka� Streams
    /// </summary>
    [Obsolete("Option will be removed soon")]
    public virtual bool UseCompactedReplicator { get; set; } = false;
    /// <summary>
    /// Use KNet version of Apache Kafka� Streams instead of standard Apache Kafka� Streams
    /// </summary>
    public virtual bool UseKNetStreams { get; set; } = true;
    /// <summary>
    /// Setting this property to <see langword="true"/> the engine based on Streams (i.e. <see cref="UseCompactedReplicator"/> is <see langword="false"/>) 
    /// will use <see cref="Org.Apache.Kafka.Streams.Kstream.GlobalKTable{K, V}"/> (<see cref="MASES.KNet.Streams.Kstream.GlobalKTable{K, V, TJVMK, TJVMV}"/> if <see cref="UseKNetStreams"/> is <see langword="true"/>)
    /// instead of <see cref="Org.Apache.Kafka.Streams.Kstream.KTable{K, V}"/> (<see cref="MASES.KNet.Streams.Kstream.KTable{K, V, TJVMK, TJVMV}"/> if <see cref="UseKNetStreams"/> is <see langword="true"/>) to manage local storage of information.
    /// </summary>
    /// <remarks>Setting <see cref="UseGlobalTable"/> to <see langword="true"/> is in contrast with <see cref="ManageEvents"/> that needs <see cref="UseCompactedReplicator"/>=<see langword="true"/> or a backend based on <see cref="Org.Apache.Kafka.Streams.Kstream.KTable{K, V}"/> (<see cref="MASES.KNet.Streams.Kstream.KTable{K, V, TJVMK, TJVMV}"/> if <see cref="UseKNetStreams"/> is <see langword="true"/>)
    /// The behavior can be changed if the application based on KEFCore does not needs events (<see cref="ManageEvents"/>=<see langword="true"/>) and/or there is a limitation using <see cref="Org.Apache.Kafka.Streams.Kstream.KTable{K, V}"/> (<see cref="MASES.KNet.Streams.Kstream.KTable{K, V, TJVMK, TJVMV}"/> if <see cref="UseKNetStreams"/> is <see langword="true"/>) 
    /// coming from other configuration parameters like <see cref="ApplicationId"/>: using the same <see cref="ApplicationId"/> across the same cluster, the partitions of <see cref="Org.Apache.Kafka.Streams.Kstream.KTable{K, V}"/> (<see cref="MASES.KNet.Streams.Kstream.KTable{K, V, TJVMK, TJVMV}"/> if <see cref="UseKNetStreams"/> is <see langword="true"/>)
    /// are managed from multiple instances.
    /// </remarks>
    [Obsolete("ApplicationId must be unique per process � UseGlobalTable is no longer needed.")] 
    public virtual bool UseGlobalTable { get; set; } = false;
    /// <summary>
    /// The optional <see cref="ConsumerConfigBuilder"/> used when <see cref="UseCompactedReplicator"/> is <see langword="true"/>
    /// </summary>
    [Obsolete("Option will be removed soon")] 
    public virtual ConsumerConfigBuilder? ConsumerConfig { get; set; }
    /// <summary>
    /// The optional <see cref="ProducerConfigBuilder"/>
    /// </summary>
    public virtual ProducerConfigBuilder? ProducerConfig { get; set; }
    /// <summary>
    /// The optional <see cref="StreamsConfig"/> used when <see cref="UseCompactedReplicator"/> is <see langword="false"/>
    /// </summary>
    public virtual StreamsConfigBuilder? StreamsConfig { get; set; }
    /// <summary>
    /// The optional <see cref="TopicConfigBuilder"/> used when topics shall be created
    /// </summary>
    public virtual TopicConfigBuilder? TopicConfig { get; set; }
    /// <summary>
    ///  Setting this property to <see langword="true"/> if the engine shall reject any write operation, its value will be used to verify if topics has the proper rights <see cref="Org.Apache.Kafka.Common.Acl.AclOperation.WRITE"/> and <see cref="Org.Apache.Kafka.Common.Acl.AclOperation.READ"/>
    /// </summary>
    public virtual bool ReadOnlyMode { get; set; } = false;
    /// <summary>
    /// The default timeout, expressed in milliseconds, KEFCore will wait for backend to be in-sync with Apache Kafka� cluster.
    /// Setting <see cref="DefaultSynchronizationTimeout"/> to <see langword="0"/> the synchronization will be disabled
    /// </summary>
    /// <remarks>The KEFCore provider try to synchronize with Apache Kafka� cluster waiting at least <see cref="DefaultSynchronizationTimeout"/> when <see cref="DatabaseFacade.EnsureCreated"/> of <see cref="DbContext.Database"/> is invoked.</remarks>
    public virtual long DefaultSynchronizationTimeout { get; set; } = Timeout.Infinite;
    /// <summary>
    ///  Setting this property to <see langword="true"/> to enable prefix scan in engine
    /// </summary>
    public virtual bool UseStorePrefixScan { get; set; } = false;
    /// <summary>
    ///  Setting this property to <see langword="true"/> to enable single key look-up in engine
    /// </summary>
    public virtual bool UseStoreSingleKeyLookup { get; set; } = true;
    /// <summary>
    ///  Setting this property to <see langword="true"/> to enable key range look-up in engine
    /// </summary>
    public virtual bool UseStoreKeyRange { get; set; } = true;
    /// <summary>
    ///  Setting this property to <see langword="true"/> to enable reverse look-up in engine
    /// </summary>
    public virtual bool UseStoreReverse { get; set; } = true;
    /// <summary>
    ///  Setting this property to <see langword="true"/> to enable reverse key range look-up in engine
    /// </summary>
    public virtual bool UseStoreReverseKeyRange { get; set; } = true;

    /// <summary>
    /// Invoke the method to wait a timeout defined from <paramref name="waitTime"/> for synchonization with Apache Kafka� backend
    /// </summary>
    /// <param name="waitTime">The time expressed as <see cref="TimeSpan"/> to wait for synchonization with Apache Kafka� backend</param>
    /// <returns>An optional <see cref="bool"/>, <see langword="null"/> means an uncertain result (e.g. <see cref="UseGlobalTable"/> is <see langword="true"/>), <see langword="true"/> if the store is in-sync, <see langword="false"/> otherwise</returns>
    /// <exception cref="TimeoutException">Raised if the <paramref name="waitTime"/> has expired without receive an information</exception>
    public bool? WaitForSynchronization(TimeSpan waitTime)
    {
        return WaitForSynchronization((long)waitTime.TotalMilliseconds);
    }
    /// <summary>
    /// Invoke the method to wait a timeout defined from <paramref name="waitTimeMs"/> for synchonization with Apache Kafka� backend
    /// </summary>
    /// <param name="waitTimeMs">The time expressed as milliseconds to wait for synchonization with Apache Kafka� backend</param>
    /// <returns>An optional <see cref="bool"/>, <see langword="null"/> means an uncertain result (e.g. <see cref="UseGlobalTable"/> is <see langword="true"/>), <see langword="true"/> if the store is in-sync, <see langword="false"/> otherwise</returns>
    /// <exception cref="TimeoutException">Raised if the <paramref name="waitTimeMs"/> has expired without receive an information</exception>
    public bool? WaitForSynchronization(long waitTimeMs = Timeout.Infinite)
    {
        var serviceProvider = ((IInfrastructure<IServiceProvider>)this).Instance;
        var database = serviceProvider.GetService<IKEFCoreDatabase>();

        if (database == null) return true;

        if (waitTimeMs != Timeout.Infinite && waitTimeMs <= 0) throw new ArgumentException($"Timeout can be the default or shall be greater than 0", nameof(waitTimeMs));
        return database.EnsureDatabaseSynchronized(waitTimeMs);
    }

    /// <summary>
    /// Resets the Apache Kafka streams application in use
    /// </summary>
    /// <remarks>Use this method with cautions</remarks>
    public void ResetStreams()
    {
        var serviceProvider = ((IInfrastructure<IServiceProvider>)this).Instance;
        var clusterCache = serviceProvider.GetService<IKEFCoreClusterCache>();
        var options = serviceProvider.GetService<IDbContextOptions>();
        var database = serviceProvider.GetService<IKEFCoreDatabase>();

        if (clusterCache == null) throw new InvalidOperationException($"Unable to retrieve {nameof(IKEFCoreClusterCache)} service");
        if (options == null) throw new InvalidOperationException($"Unable to retrieve {nameof(IDbContextOptions)} service");
        if (database == null) throw new InvalidOperationException($"Unable to retrieve {nameof(IKEFCoreDatabase)} service");

        var cluster = clusterCache.GetCluster(options);

        cluster?.ResetStreams(database);
    }

    /// <inheritdoc cref="DbContext.OnConfiguring(DbContextOptionsBuilder)"/>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        if (!UseCompactedReplicator && string.IsNullOrEmpty(ApplicationId))
        {
            throw new ArgumentException("Cannot be null or empty when Streams based backend is in use.", nameof(ApplicationId));
        }

        if (string.IsNullOrWhiteSpace(BootstrapServers)) throw new ArgumentException("Cannot be null or empty", nameof(BootstrapServers));

        optionsBuilder.UseKEFCore(ApplicationId, BootstrapServers, (o) =>
        {
            if (ManageEvents 
                && !(UseCompactedReplicator || UseGlobalTable) 
                && DefaultNumPartitions > 1)
            {
                throw new InvalidOperationException($"{nameof(ManageEvents)} supports a number of partition higher than 1 only with {nameof(UseCompactedReplicator)}=true, in all other cases events are supported only using a single partition.");
            }

            o.WithConsumerConfig(ConsumerConfig ?? DefaultConsumerConfig);
            o.WithProducerConfig(ProducerConfig ?? DefaultProducerConfig);
            o.WithStreamsConfig(StreamsConfig ?? DefaultStreamsConfig).WithDefaultNumPartitions(DefaultNumPartitions);
            o.WithTopicConfig(TopicConfig ?? DefaultTopicConfig);
            o.WithPersistentStorage(UsePersistentStorage);
            o.WithEnumeratorWithPrefetch(UseEnumeratorWithPrefetch);
            o.WithKeyByteBufferDataTransfer(UseKeyByteBufferDataTransfer);
            o.WithValueContainerByteBufferDataTransfer(UseValueContainerByteBufferDataTransfer);
            o.WithDeletePolicyForTopic(UseDeletePolicyForTopic);
            o.WithCompactedReplicator(UseCompactedReplicator);
            o.WithKNetStreams(UseKNetStreams);
            o.WithGlobalTable(UseGlobalTable);
            o.WithDefaultReplicationFactor(DefaultReplicationFactor);
            o.WithDefaultSynchronizationTimeout(DefaultSynchronizationTimeout);
            o.WithStorePrefixScan(UseStorePrefixScan);
            o.WithStoreSingleKeyLookup(UseStoreSingleKeyLookup);
            o.WithStoreKeyRange(UseStoreKeyRange);
            o.WithStoreReverse(UseStoreReverse);
            o.WithStoreReverseKeyRange(UseStoreReverseKeyRange);
            if (KeySerDesSelectorType != null) o.WithKeySerDesSelectorType(KeySerDesSelectorType);
            if (ValueSerDesSelectorType != null) o.WithValueSerDesSelectorType(ValueSerDesSelectorType);
            if (ValueContainerType != null) o.WithValueContainerType(ValueContainerType);
        });
    }
    /// <inheritdoc cref="IComplexTypeConverterFactory.Register(Assembly)"/>
    public void RegisterComplexTypeConverter(Assembly assembly)
    {
        var serviceProvider = ((IInfrastructure<IServiceProvider>)this).Instance;
        var factory = serviceProvider.GetService<IComplexTypeConverterFactory>();
        factory?.Register(assembly);
    }
    /// <inheritdoc cref="IComplexTypeConverterFactory.Register(Type)"/>
    public void RegisterComplexTypeConverter(Type type)
    {
        var serviceProvider = ((IInfrastructure<IServiceProvider>)this).Instance;
        var factory = serviceProvider.GetService<IComplexTypeConverterFactory>();
        factory?.Register(type);
    }
    /// <inheritdoc cref="IComplexTypeConverterFactory.Register(IComplexTypeConverter)"/>
    public void RegisterComplexTypeConverter(IComplexTypeConverter converter)
    {
        var serviceProvider = ((IInfrastructure<IServiceProvider>)this).Instance;
        var factory = serviceProvider.GetService<IComplexTypeConverterFactory>();
        factory?.Register(converter);
    }
}
