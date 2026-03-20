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

using MASES.EntityFrameworkCore.KNet.Infrastructure;
using MASES.KNet.Producer;

namespace MASES.EntityFrameworkCore.KNet.Metadata;

/// <summary>
/// Overrides producer configuration for the Kafka topic associated with this entity type.
/// Only explicitly set properties override the global <see cref="KEFCoreDbContext.ProducerConfig"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class KEFCoreProducerAttribute : Attribute
{
    // ── Throughput / latency ──────────────────────────────────────────────

    /// <summary>
    /// Number of acknowledgments required before considering a write successful.
    /// <c>0</c> = no ack, <c>1</c> = leader only, <c>-1</c>/<c>all</c> = all replicas (default).
    /// </summary>
    public ProducerConfigBuilder.AcksTypes? Acks { get; set; }

    /// <summary>
    /// Delay in milliseconds to wait before sending a batch, allowing more records to accumulate.
    /// Higher values improve throughput at the cost of latency. Default is <c>1</c>.
    /// </summary>
    public int? LingerMs { get; set; }

    /// <summary>
    /// Maximum size in bytes of a single batch sent to a partition.
    /// Default is <c>16384</c> (16 KB).
    /// </summary>
    public int? BatchSize { get; set; }

    /// <summary>
    /// Compression type applied to record batches.
    /// </summary>
    public ProducerConfigBuilder.CompressionTypes? CompressionType { get; set; }

    // ── Reliability ───────────────────────────────────────────────────────

    /// <summary>
    /// Number of retries on transient failures. Default is <c>0</c>.
    /// </summary>
    public int? Retries { get; set; }

    /// <summary>
    /// Maximum number of unacknowledged requests per connection before blocking.
    /// Set to <c>1</c> to guarantee ordering when <see cref="Retries"/> > 0.
    /// </summary>
    public int? MaxInFlightRequestsPerConnection { get; set; }

    /// <summary>
    /// Upper bound in milliseconds for the total time a record may await delivery,
    /// including retries. Default is <c>120000</c> (2 minutes).
    /// </summary>
    public int? DeliveryTimeoutMs { get; set; }

    /// <summary>
    /// Timeout in milliseconds for a single produce request to the broker.
    /// Default is <c>30000</c> (30 seconds).
    /// </summary>
    public int? RequestTimeoutMs { get; set; }

    // ── Buffer ────────────────────────────────────────────────────────────

    /// <summary>
    /// Total memory in bytes the producer can use to buffer records.
    /// Default is <c>33554432</c> (32 MB).
    /// </summary>
    public long? BufferMemory { get; set; }

    /// <summary>
    /// Maximum time in milliseconds to block when the buffer is full.
    /// Default is <c>60000</c> (60 seconds).
    /// </summary>
    public long? MaxBlockMs { get; set; }
}
