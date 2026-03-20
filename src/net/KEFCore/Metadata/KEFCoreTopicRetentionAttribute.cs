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
/// Overrides the retention policy for the Kafka topic associated with this entity type.
/// Applied at topic creation time (<see cref="Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade.EnsureCreated"/>).
/// </summary>
/// <remarks>
/// At least one of <see cref="RetentionBytes"/> or <see cref="RetentionMs"/> must be specified.
/// Use <c>-1</c> to leave the respective setting at its cluster default.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class KEFCoreTopicRetentionAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of <see cref="KEFCoreTopicRetentionAttribute"/>.
    /// </summary>
    /// <param name="retentionBytes">
    /// Maximum size in bytes the topic will retain before deleting older segments.
    /// Use <c>-1</c> to leave this setting at its cluster default (unlimited).
    /// </param>
    /// <param name="retentionMs">
    /// Maximum time in milliseconds records are retained before they are eligible for deletion.
    /// Use <c>-1</c> to leave this setting at its cluster default (unlimited).
    /// </param>
    public KEFCoreTopicRetentionAttribute(long retentionBytes = -1, long retentionMs = -1)
    {
        RetentionBytes = retentionBytes;
        RetentionMs = retentionMs;
    }

    /// <summary>
    /// Maximum size in bytes the topic will retain before deleting older segments.
    /// <c>-1</c> means the cluster default applies.
    /// </summary>
    public long RetentionBytes { get; }

    /// <summary>
    /// Maximum time in milliseconds records are retained before they are eligible for deletion.
    /// <c>-1</c> means the cluster default applies.
    /// </summary>
    public long RetentionMs { get; }
}
