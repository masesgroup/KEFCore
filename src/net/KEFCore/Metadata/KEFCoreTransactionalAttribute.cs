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

namespace MASES.EntityFrameworkCore.KNet.Metadata;

/// <summary>
/// Assigns this entity type to a Kafka transaction group, enabling exactly-once semantics
/// for all entities sharing the same <see cref="TransactionGroup"/>.
/// </summary>
/// <remarks>
/// All entity types in the same group share a single <see cref="Org.Apache.Kafka.Clients.Producer.KafkaProducer"/>
/// with a stable <c>transactional.id</c> equal to <c>{ApplicationId}.{TransactionGroup}</c>.
/// Entity types without this attribute use the standard non-transactional producer path.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class KEFCoreTransactionalAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of <see cref="KEFCoreTransactionalAttribute"/>.
    /// </summary>
    /// <param name="transactionGroup">
    /// The transaction group name. All entity types sharing this name will participate
    /// in the same Kafka transaction. Must be non-empty.
    /// </param>
    public KEFCoreTransactionalAttribute(string transactionGroup)
    {
        if (string.IsNullOrWhiteSpace(transactionGroup))
            throw new ArgumentException("Transaction group name must be non-empty.", nameof(transactionGroup));
        TransactionGroup = transactionGroup;
    }

    /// <summary>
    /// The transaction group name shared by all entity types participating in the same Kafka transaction.
    /// </summary>
    public string TransactionGroup { get; }
}
