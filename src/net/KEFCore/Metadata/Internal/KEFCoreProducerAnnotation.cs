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

using MASES.KNet.Producer;

namespace MASES.EntityFrameworkCore.KNet.Metadata.Internal;

/// <summary>
/// Internal DTO stored as a model annotation to carry per-entity producer configuration overrides.
/// Populated by <see cref="Conventions.KEFCoreProducerConvention"/> from
/// <see cref="KEFCoreProducerAttribute"/> or <see cref="Extensions.KEFCoreEntityTypeBuilderExtensions.HasKEFCoreProducer"/>.
/// </summary>
public sealed class KEFCoreProducerAnnotation
{
    /// <inheritdoc cref="KEFCoreProducerAttribute.Acks"/>
    public ProducerConfigBuilder.AcksTypes? Acks { get; set; }
    /// <inheritdoc cref="KEFCoreProducerAttribute.LingerMs"/>
    public int? LingerMs { get; set; }
    /// <inheritdoc cref="KEFCoreProducerAttribute.BatchSize"/>
    public int? BatchSize { get; set; }
    /// <inheritdoc cref="KEFCoreProducerAttribute.CompressionType"/>
    public ProducerConfigBuilder.CompressionTypes? CompressionType { get; set; }
    /// <inheritdoc cref="KEFCoreProducerAttribute.Retries"/>
    public int? Retries { get; set; }
    /// <inheritdoc cref="KEFCoreProducerAttribute.MaxInFlightRequestsPerConnection"/>
    public int? MaxInFlightRequestsPerConnection { get; set; }
    /// <inheritdoc cref="KEFCoreProducerAttribute.DeliveryTimeoutMs"/>
    public int? DeliveryTimeoutMs { get; set; }
    /// <inheritdoc cref="KEFCoreProducerAttribute.RequestTimeoutMs"/>
    public int? RequestTimeoutMs { get; set; }
    /// <inheritdoc cref="KEFCoreProducerAttribute.BufferMemory"/>
    public long? BufferMemory { get; set; }
    /// <inheritdoc cref="KEFCoreProducerAttribute.MaxBlockMs"/>
    public long? MaxBlockMs { get; set; }

    /// <summary>
    /// Returns <see langword="true"/> if at least one property is set.
    /// </summary>
    public bool HasAnyValue =>
        Acks.HasValue || LingerMs.HasValue || BatchSize.HasValue ||
        CompressionType.HasValue || Retries.HasValue ||
        MaxInFlightRequestsPerConnection.HasValue || DeliveryTimeoutMs.HasValue ||
        RequestTimeoutMs.HasValue || BufferMemory.HasValue || MaxBlockMs.HasValue;
}
