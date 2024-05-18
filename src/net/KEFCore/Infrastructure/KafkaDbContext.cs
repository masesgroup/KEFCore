/*
*  Copyright 2024 MASES s.r.l.
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

using MASES.EntityFrameworkCore.KNet.Serialization;
using MASES.EntityFrameworkCore.KNet.Serialization.Json;
using MASES.EntityFrameworkCore.KNet.Serialization.Json.Storage;
using MASES.EntityFrameworkCore.KNet.Storage;
using MASES.KNet.Common;
using MASES.KNet.Consumer;
using MASES.KNet.Producer;
using MASES.KNet.Serialization;
using MASES.KNet.Streams;

namespace MASES.EntityFrameworkCore.KNet.Infrastructure;

/// <summary>
///     A <see cref="KafkaDbContext"/> instance represents a session with the Apache Kafka cluster and can be used to query and save
///     instances of your entities. <see cref="KafkaDbContext"/> extends <see cref="DbContext"/> and it is a combination of the Unit Of Work and Repository patterns.
/// </summary>
/// <remarks>
///     <para>
///         Entity Framework Core does not support multiple parallel operations being run on the same <see cref="KafkaDbContext"/> instance. This
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
///         other options) to be used for the context, just set the properties associated to <see cref="KafkaDbContext"/> class.
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
///         See <see href="https://masesgroup.github.io/KEFCore/articles/kafkadbcontext.html">KafkaDbContext configuration and initialization</see>,
///         <see href="https://aka.ms/efcore-docs-dbcontext">DbContext lifetime, configuration, and initialization</see>,
///         <see href="https://aka.ms/efcore-docs-query">Querying data with EF Core</see>,
///         <see href="https://aka.ms/efcore-docs-change-tracking">Changing tracking</see>, and
///         <see href="https://aka.ms/efcore-docs-saving-data">Saving data with EF Core</see> for more information and examples.
///     </para>
/// </remarks>
public class KafkaDbContext : DbContext
{
#if DEBUG_PERFORMANCE
    const bool perf = true;
    /// <summary>
    /// Enable tracing of <see cref="MASES.EntityFrameworkCore.KNet.Storage.Internal.EntityTypeProducer{TKey, TValueContainer, TKeySerializer, TValueSerializer}"/>
    /// </summary>
    public static bool TraceEntityTypeDataStorageGetData = false;

    public static void ReportString(string message)
    {
        if (!_enableKEFCoreTracing) return;

        if (Debugger.IsAttached)
        {
            Trace.WriteLine($"{DateTime.Now:HH::mm::ss:ffff} - {message}");
        }
        else
        {
            Console.WriteLine($"{DateTime.Now:HH::mm::ss:ffff} - {message}");
        }
    }
#else
const bool perf = false;
#endif
    /// <summary>
    /// Reports if the library was compiled to reports performance information
    /// </summary>
    public const bool IsPerformanceVersion = perf;
#if DEBUG_PERFORMANCE
    static bool _enableKEFCoreTracing = true;
#else
    static bool _enableKEFCoreTracing = false;
#endif
    /// <summary>
    /// Set to <see langword="true"/> to enable tracing of KEFCore
    /// </summary>
    /// <remarks>Can be set only if the project is compiled with DEBUG_PERFORMANCE preprocessor directive, otherwise an <see cref="InvalidOperationException"/> is raised</remarks>
    public static bool EnableKEFCoreTracing
    {
        get { return _enableKEFCoreTracing; }
        set 
        {
            _enableKEFCoreTracing = value;
#if DEBUG_PERFORMANCE
            if (_enableKEFCoreTracing) throw new InvalidOperationException("Compile KEFCore using DEBUG_PERFORMANCE preprocessor directive");
#endif
        }
    }

    /// <summary>
    ///     The default <see cref="ConsumerConfig"/> configuration
    /// </summary>
    /// <returns>The default <see cref="ConsumerConfig"/> configuration.</returns>
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
    public KafkaDbContext()
    {

    }
    /// <inheritdoc cref="DbContext.DbContext(DbContextOptions)"/>
    public KafkaDbContext(DbContextOptions options) : base(options)
    {

    }
    /// <summary>
    /// The optional <see cref="Type"/> to use for key serialization
    /// <para>
    /// Default value is <see cref="DefaultKEFCoreSerDes.Key.JsonRaw{T}"/>, any custom <see cref="Type"/> shall implement <see cref="ISerDes{T, TJVM}"/>
    /// </para>
    /// </summary>
    public virtual Type? KeySerializationType { get; set; } = null;
    /// <summary>
    /// The optional <see cref="Type"/> to use for value serialization
    /// <para>
    /// Default value is <see cref="DefaultKEFCoreSerDes.ValueContainer.JsonRaw{T}"/>, any custom <see cref="Type"/> shall implement <see cref="ISerDes{T, TJVM}"/>
    /// </para>
    /// </summary>
    public virtual Type? ValueSerializationType { get; set; } = null;
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
    /// The bootstrap servers of the Apache Kafka cluster
    /// </summary>
    public virtual string? BootstrapServers { get; set; }
    /// <summary>
    /// The application id
    /// </summary>
    public virtual string ApplicationId { get; set; } = Guid.NewGuid().ToString();
    /// <summary>
    /// Database name means whe prefix of the topics associated to the instance of <see cref="KafkaDbContext"/>
    /// </summary>
    public virtual string? DatabaseName { get; set; }
    /// <summary>
    /// Default number of partitions associated to each topic
    /// </summary>
    public virtual int DefaultNumPartitions { get; set; } = 10;
    /// <summary>
    /// Default replication factor associated to each topic
    /// </summary>
    public virtual short DefaultReplicationFactor { get; set; } = 1;
    /// <summary>
    /// Default consumr instances used in conjunction with <see cref="UseCompactedReplicator"/>
    /// </summary>
    public virtual int? DefaultConsumerInstances { get; set; } = null;
    /// <summary>
    /// Use persistent storage when Apache Kafka Streams is in use
    /// </summary>
    public virtual bool UsePersistentStorage { get; set; } = false;
    /// <summary>
    /// Use <see href="https://kafka.apache.org/documentation/#topicconfigs_cleanup.policy">delete cleanup policy</see> when a topic is created
    /// </summary>
    public bool UseDeletePolicyForTopic { get; set; } = false;
    /// <summary>
    /// Use <see cref="MASES.KNet.Replicator.KNetCompactedReplicator{TKey, TValue}"/> instead of Apache Kafka Streams
    /// </summary>
    public virtual bool UseCompactedReplicator { get; set; } = true;
    /// <summary>
    /// Use KNet version of Apache Kafka Streams instead of standard Apache Kafka Streams
    /// </summary>
    public virtual bool UseKNetStreams { get; set; } = true;
    /// <summary>
    /// Setting this property to <see langword="true"/> the engine prefers to use enumerator instances able to do a prefetch on data speeding up execution
    /// </summary>
    /// <remarks>Used only if <see cref="UseCompactedReplicator"/> is <see langword="false"/> and <see cref="UseKNetStreams"/> is <see langword="true"/>, not available in EFCore 6.</remarks>
    public virtual bool UseEnumeratorWithPrefetch { get; set; } = false;
    /// <summary>
    /// The optional <see cref="ConsumerConfigBuilder"/> used when <see cref="UseCompactedReplicator"/> is <see langword="true"/>
    /// </summary>
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
    /// The optional handler to be used to receive notification when the back-end triggers a data change.
    /// </summary>
    /// <remarks>Works if <see cref="UseCompactedReplicator"/> is <see langword="true"/></remarks>
    public virtual Action<EntityTypeChanged>? OnChangeEvent { get; set; } = null;

    /// <inheritdoc cref="DbContext.OnConfiguring(DbContextOptionsBuilder)"/>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (BootstrapServers == null) throw new ArgumentNullException(nameof(BootstrapServers));
        if (DatabaseName == null) throw new ArgumentNullException(nameof(DatabaseName));

        optionsBuilder.UseKafkaCluster(ApplicationId, DatabaseName, BootstrapServers, (o) =>
        {
            o.WithUseNameMatching(UseNameMatching);
            o.WithConsumerConfig(ConsumerConfig ?? DefaultConsumerConfig);
            o.WithProducerConfig(ProducerConfig ?? DefaultProducerConfig);
            o.WithStreamsConfig(StreamsConfig ?? DefaultStreamsConfig).WithDefaultNumPartitions(DefaultNumPartitions);
            o.WithTopicConfig(TopicConfig ?? DefaultTopicConfig);
            o.WithUsePersistentStorage(UsePersistentStorage);
        o.WithUseEnumeratorWithPrefetch(UseEnumeratorWithPrefetch);
            o.WithUseDeletePolicyForTopic(UseDeletePolicyForTopic);
            o.WithCompactedReplicator(UseCompactedReplicator);
            o.WithUseKNetStreams(UseKNetStreams);
            o.WithDefaultReplicationFactor(DefaultReplicationFactor);
            if (KeySerializationType != null) o.WithKeySerializationType(KeySerializationType);
            if (ValueSerializationType != null) o.WithValueSerializationType(ValueSerializationType);
            if (ValueContainerType != null) o.WithValueContainerType(ValueContainerType);
            if (OnChangeEvent != null) o.WithOnChangeEvent(OnChangeEvent);
        });
    }
}
