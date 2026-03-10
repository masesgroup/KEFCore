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
using MASES.EntityFrameworkCore.KNet.Infrastructure;
using MASES.KNet.Admin;
using Org.Apache.Kafka.Clients.Admin;
using Org.Apache.Kafka.Common;
using Org.Apache.Kafka.Common.Acl;
using Org.Apache.Kafka.Common.Errors;

namespace MASES.EntityFrameworkCore.KNet.Storage.Internal
{
    internal class KEFCoreClusterAdmin : IDisposable
    {
        static internal bool DisableClusterInvocation = false; // used only to activate an external model builder

        readonly string _clusterId;
        private readonly Admin? _kafkaAdminClient = null;
        private readonly Properties _bootstrapProperties;

        public static KEFCoreClusterAdmin Create(string bootstrapServers)
        {
            return new KEFCoreClusterAdmin(bootstrapServers);
        }

        KEFCoreClusterAdmin(string bootstrapServers)
        {
            if (DisableClusterInvocation) { _clusterId = "FakeClusterId"; return; }

            _bootstrapProperties = AdminClientConfigBuilder.Create().WithBootstrapServers(bootstrapServers).ToProperties();
            try
            {
                _kafkaAdminClient = Admin.Create(_bootstrapProperties);
            }
            catch (ExecutionException ex)
            {
                if (ex.InnerException != null) throw ex.InnerException;
                throw;
            }

            _clusterId = GetClusterId();
        }

        public void Dispose()
        {
            _kafkaAdminClient?.Dispose();
        }

        public string ClusterId => _clusterId;

        public string GetClusterId(IDiagnosticsLogger<DbLoggerCategory.Infrastructure> infrastructureLogger = null)
        {
            DescribeClusterResult result = null;
            KafkaFuture<Java.Lang.String> future = null;
            try
            {
                result = _kafkaAdminClient?.DescribeCluster();
                future = result?.ClusterId();
                return future?.Get();
            }
            catch (ExecutionException ex)
            {
                if (ex.InnerException != null) throw ex.InnerException;
                throw;
            }
            finally { future?.Dispose(); result?.Dispose(); }
        }

        public void CreateTopic(string topicName, int requestedPartitions, short requestedReplicationFactor, Map<Java.Lang.String, Java.Lang.String> options, IDiagnosticsLogger<DbLoggerCategory.Infrastructure> infrastructureLogger = null)
        {
            Set<NewTopic> coll = default;
            CreateTopicsResult result = default;
            KafkaFuture<Java.Lang.Void> future = default;
            NewTopic topic = default;
            try
            {
                topic = new NewTopic((Java.Lang.String)topicName, requestedPartitions, requestedReplicationFactor);
                topic.Configs(options);
                coll = Collections.Singleton(topic);
                result = _kafkaAdminClient?.CreateTopics(coll);
                future = result?.All();
                future?.Get();
            }
            catch (ExecutionException ex)
            {
                if (ex.InnerException != null) throw ex.InnerException;
                throw;
            }
            finally { topic?.Dispose(); future?.Dispose(); result?.Dispose(); coll?.Dispose(); }
        }

        public void DeleteTopics(Collection<Java.Lang.String> coll, IDiagnosticsLogger<DbLoggerCategory.Infrastructure> infrastructureLogger = null)
        {
            try
            {
                DeleteTopicsResult result = default;
                KafkaFuture<Java.Lang.Void> future = default;
                try
                {
                    result = _kafkaAdminClient?.DeleteTopics(coll);
                    future = result?.All();
                    future?.Get();
                }
                catch (ExecutionException ex)
                {
                    if (ex.InnerException != null) throw ex.InnerException;
                    else throw;
                }
                finally { future?.Dispose(); result?.Dispose(); }
            }
            catch (Org.Apache.Kafka.Common.Errors.UnknownTopicOrPartitionException utpe)
            {
                infrastructureLogger?.Logger.LogError("EnsureDeleted reports the following {Error}", utpe.Message);
            }
        }

        public void CheckTopics(Collection<Java.Lang.String> coll, bool readOnlyMode, bool manageEvents, bool useCompactedReplicator, IDiagnosticsLogger<DbLoggerCategory.Infrastructure> infrastructureLogger = null)
        {
            try
            {
                infrastructureLogger?.Logger.LogInformation("Trying to identify information of topics from the cluster.");
                DescribeTopicsResult result = default;
                KafkaFuture<Map<Java.Lang.String, TopicDescription>> future = default;
                DescribeTopicsOptions describeTopicsOptions = new();
                describeTopicsOptions.IncludeAuthorizedOperations(true);
                try
                {
                    result = _kafkaAdminClient?.DescribeTopics(coll, describeTopicsOptions);
                    future = result?.AllTopicNames();
                    foreach (var item in future.Get().EntrySet())
                    {
                        if (item.Value.IsInternal())
                        {
                            infrastructureLogger?.Logger.LogDebug("Topic {Key} is internal", item.Key);
                            continue;
                        }
                        var partitionsData = item.Value.Partitions();
                        var numPartition = partitionsData.Size();
                        if (manageEvents && !useCompactedReplicator && numPartition > 1)
                        {
                            throw new InvalidOperationException($"{nameof(KEFCoreDbContext.ManageEvents)} supports a number of partition higher than 1 only with {nameof(KEFCoreDbContext.UseCompactedReplicator)}=true, in all other cases events are supported only using a single partition.");
                        }

                        bool write = false;
                        bool read = false;

                        foreach (var operation in item.Value.AuthorizedOperations())
                        {
                            if (operation == AclOperation.WRITE) write = true;
                            if (operation == AclOperation.READ) read = true;
                            infrastructureLogger?.Logger.LogDebug("Topic {Key} supports {Name}", item.Key, operation.Name());
                        }
                        if (readOnlyMode)
                        {
                            if (!read) throw new InvalidOperationException($"Topic {item.Key} shall support {AclOperation.READ}");
                        }
                        else if (!(read && write)) { throw new InvalidOperationException($"Topic {item.Key} shall support both {AclOperation.WRITE} and {AclOperation.READ}"); }
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
                finally { future?.Dispose(); result?.Dispose(); }
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
                var coll = Collections.Singleton((Java.Lang.String)topicName);
                DescribeTopicsResult describeTopicsResult = _kafkaAdminClient.DescribeTopics(coll);
                using var future = describeTopicsResult.AllTopicNames();
                var result = future.Get();
                foreach (var item in result.EntrySet())
                {
                    if (item.Key.Equals(topicName))
                    {
                        HashMap<TopicPartition, OffsetSpec> hashMap = new();
                        foreach (var partition in item.Value.Partitions())
                        {
                            var partitionIndex = partition.Partition();
                            TopicPartition topicPartition = new(topicName, partitionIndex);
                            hashMap.Put(topicPartition, OffsetSpec.Latest());
                        }

                        var listOffsetResult = _kafkaAdminClient.ListOffsets(hashMap);
                        using var offsetResultFuture = listOffsetResult.All();
                        var offsetResult = offsetResultFuture.Get();
                        foreach (var offsetResultItem in offsetResult.EntrySet())
                        {
                            if (offsetResultItem.Key.Topic().Equals(topicName))
                            {
                                dictionary.Add(offsetResultItem.Key.Partition(), offsetResultItem.Value.Offset() - 1); // since latest means the latest used offset (a record in kafka) + 1, here we remove 1 to be in sync with received offset from kafka
                            }
                        }
                        break;
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
