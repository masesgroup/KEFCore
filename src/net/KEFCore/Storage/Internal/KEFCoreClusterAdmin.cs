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

#nullable disable

using Java.Util;
using Java.Util.Concurrent;
using MASES.EntityFrameworkCore.KNet.Infrastructure.Internal;
using MASES.KNet.Admin;
using Org.Apache.Kafka.Clients.Admin;
using Org.Apache.Kafka.Common;
using Org.Apache.Kafka.Common.Acl;
using Org.Apache.Kafka.Common.Errors;
using System.Collections.Concurrent;

namespace MASES.EntityFrameworkCore.KNet.Storage.Internal
{
    internal class KEFCoreClusterAdmin : IDisposable
    {
        static internal bool DisableClusterInvocation = false; // used only to activate an external model builder
        static readonly ConcurrentDictionary<string, Admin> _configuredAdminClient = new();

        readonly string _clusterId;
        private readonly Admin _kafkaAdminClient = null;

        public static KEFCoreClusterAdmin Create(IKEFCoreSingletonOptions configuration)
        {
            return new KEFCoreClusterAdmin(configuration);
        }

        KEFCoreClusterAdmin(IKEFCoreSingletonOptions configuration)
        {
            if (DisableClusterInvocation) { _clusterId = "FakeClusterId"; return; }

            var builder = AdminClientConfigBuilder.Create();
            builder = builder.WithBootstrapServers(configuration.BootstrapServers);
            if (configuration.SecurityProtocol != null) builder = builder.WithSecurityProtocol(configuration.SecurityProtocol);
            if (configuration.SslConfig != null) builder = builder.WithSslConfigs(configuration.SslConfig);
            if (configuration.SaslConfig != null) builder = builder.WithSaslConfigs(configuration.SaslConfig);

            var bootstrapProperties = builder.ToProperties();
            var key = bootstrapProperties.ToString();
            if (!_configuredAdminClient.TryGetValue(key, out var adminClient))
            {
                try
                {
                    adminClient = Admin.Create(bootstrapProperties);
                }
                catch (ExecutionException ex)
                {
                    if (ex.InnerException != null) throw ex.InnerException;
                    throw;
                }
                _configuredAdminClient.TryAdd(key, adminClient);
            }

            _kafkaAdminClient = adminClient;
            _clusterId = GetClusterId();
        }

        public void Dispose()
        {
            _kafkaAdminClient?.Dispose();
        }

        public string ClusterId => _clusterId;

        public string GetClusterId(IDiagnosticsLogger<DbLoggerCategory.Infrastructure> infrastructureLogger = null)
        {
            try
            {
                using var result = _kafkaAdminClient?.DescribeCluster();
                using var future = result?.ClusterId();
                using var res = future?.Get();
                return res;
            }
            catch (ExecutionException ex)
            {
                if (ex.InnerException != null) throw ex.InnerException;
                throw;
            }
        }

        public void CreateTopic(string topicName, int requestedPartitions, short requestedReplicationFactor, Map<Java.Lang.String, Java.Lang.String> options, IDiagnosticsLogger<DbLoggerCategory.Infrastructure> infrastructureLogger = null)
        {
            try
            {
                using var topic1 = new NewTopic((Java.Lang.String)topicName, requestedPartitions, requestedReplicationFactor);
                using var topic = topic1.Configs(options);
                using var coll = Collections.Singleton(topic);
                using var result = _kafkaAdminClient?.CreateTopics(coll);
                using var future = result?.All();
                using var res = future?.Get();
            }
            catch (ExecutionException ex)
            {
                if (ex.InnerException != null) throw ex.InnerException;
                throw;
            }
        }

        public void DeleteTopics(Collection<Java.Lang.String> coll, IDiagnosticsLogger<DbLoggerCategory.Infrastructure> infrastructureLogger = null)
        {
            try
            {
                try
                {
                    using var result = _kafkaAdminClient?.DeleteTopics(coll);
                    using var future = result?.All();
                    using var res = future?.Get();
                }
                catch (ExecutionException ex)
                {
                    if (ex.InnerException != null) throw ex.InnerException;
                    else throw;
                }
            }
            catch (Org.Apache.Kafka.Common.Errors.UnknownTopicOrPartitionException utpe)
            {
                infrastructureLogger?.Logger.LogError(utpe, "EnsureDeleted reports the following: {Error}", utpe.Message);
            }
        }

        public void CheckTopics(Collection<Java.Lang.String> coll, bool readOnlyMode, IDiagnosticsLogger<DbLoggerCategory.Infrastructure> infrastructureLogger = null)
        {
            try
            {
                infrastructureLogger?.Logger.LogInformation("Trying to identify information of topics from the cluster.");

                try
                {
                    using DescribeTopicsOptions describeTopicsOptions = new();
                    describeTopicsOptions.IncludeAuthorizedOperations(true);

                    using var result = _kafkaAdminClient?.DescribeTopics(coll, describeTopicsOptions);
                    using var future = result?.AllTopicNames();
                    using var map = future.Get();
                    using var entrySet = map.EntrySet();
                    foreach (var item in entrySet)
                    {
                        using (item)
                        {
                            using var key = item.Key;
                            using var value = item.Value;
                            if (value.IsInternal())
                            {
                                infrastructureLogger?.Logger.LogDebug("Topic {Key} is internal", key);
                                continue;
                            }
                            using var partitionsData = value.Partitions();
                            var numPartition = partitionsData.Size();

                            bool write = false;
                            bool read = false;
                            using var authOperations = value.AuthorizedOperations();
                            foreach (var operation in authOperations)
                            {
                                using (operation)
                                {
                                    if (operation == AclOperation.WRITE) write = true;
                                    if (operation == AclOperation.READ) read = true;
                                    using var operationName = operation.Name();
                                    infrastructureLogger?.Logger.LogDebug("Topic {Key} supports {Name}", key, operationName);
                                }
                            }
                            if (readOnlyMode)
                            {
                                if (!read) throw new InvalidOperationException($"Topic {item.Key} shall support {AclOperation.READ}");
                            }
                            else if (!(read && write)) { throw new InvalidOperationException($"Topic {item.Key} shall support both {AclOperation.WRITE} and {AclOperation.READ}"); }
                        }
                    }
                }
                catch (ExecutionException ex)
                {
                    if (ex.InnerException is UnknownTopicOrPartitionException)
                    {
                        throw ex.InnerException;
                    }
                    else if (ex.InnerException != null) throw ex.InnerException;
                    else throw;
                }
            }
            catch (UnknownTopicOrPartitionException ex)
            {
                infrastructureLogger?.Logger.LogDebug(ex.Message);
            }
        }

        public IDictionary<int, long> LastPartitionOffsetForTopic(string topicName, IDiagnosticsLogger<DbLoggerCategory.Infrastructure> infrastructureLogger = null)
        {
            System.Collections.Generic.Dictionary<int, long> dictionary = new();
            try
            {
                using Java.Lang.String jTopic = topicName;
                using var coll = Collections.Singleton(jTopic);
                using DescribeTopicsResult describeTopicsResult = _kafkaAdminClient.DescribeTopics(coll);
                using var future = describeTopicsResult.AllTopicNames();
                using var result = future.Get();
                using var entrySet = result.EntrySet();
                foreach (var item in entrySet)
                {
                    using (item)
                    {
                        using var key = item.Key;
                        using var value = item.Value;
                        if (key.Equals(jTopic))
                        {
                            using HashMap<TopicPartition, OffsetSpec> hashMap = new();
                            using var partitions = value.Partitions();
                            foreach (var partition in partitions)
                            {
                                using (partition)
                                {
                                    var partitionIndex = partition.Partition();
                                    using TopicPartition topicPartition = new(topicName, partitionIndex);
                                    using var offsetSpec = OffsetSpec.Latest();
                                    hashMap.Put(topicPartition, offsetSpec);
                                }
                            }

                            using var listOffsetResult = _kafkaAdminClient.ListOffsets(hashMap);
                            using var offsetResultFuture = listOffsetResult.All();
                            using var offsetResult = offsetResultFuture.Get();
                            using var offsetResultEntrySet = offsetResult.EntrySet();
                            foreach (var offsetResultItem in offsetResultEntrySet)
                            {
                                using var offsetResultItemKey = offsetResultItem.Key; 
                                using var offsetResultItemValue = offsetResultItem.Value;
                                using var offsetResultItemTopic = offsetResultItemKey.Topic();
                                if (offsetResultItemTopic.Equals(jTopic))
                                {
                                    dictionary.Add(offsetResultItemKey.Partition(), offsetResultItemValue.Offset() - 1); // since latest means the latest used offset (a record in kafka) + 1, here we remove 1 to be in sync with received offset from kafka
                                }
                            }
                            break;
                        }
                    }
                }
                return dictionary;
            }
            catch (ExecutionException ex)
            {
                if (ex.InnerException != null) throw ex.InnerException;
                else throw;
            }
        }
    }
}
