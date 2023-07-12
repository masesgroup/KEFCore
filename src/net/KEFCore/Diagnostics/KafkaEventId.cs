// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

/*
*  Copyright 2022 MASES s.r.l.
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

namespace MASES.EntityFrameworkCore.KNet.Diagnostics;

/// <summary>
///     Event IDs for Kafka events that correspond to messages logged to an <see cref="ILogger" />
///     and events sent to a <see cref="DiagnosticSource" />.
/// </summary>
/// <remarks>
///     <para>
///         These IDs are also used with <see cref="WarningsConfigurationBuilder" /> to configure the
///         behavior of warnings.
///     </para>
///     <para>
///         See <see href="https://aka.ms/efcore-docs-diagnostics">Logging, events, and diagnostics</see>, and
///         <see href="https://github.com/masesgroup/KEFCore">The EF Core Kafka database provider</see> for more information and examples.
///     </para>
/// </remarks>
public static class KafkaEventId
{
    // Warning: These values must not change between releases.
    // Only add new values to the end of sections, never in the middle.
    // Try to use <Noun><Verb> naming and be consistent with existing names.
    private enum Id
    {
        // Transaction events
        TransactionIgnoredWarning = CoreEventId.ProviderBaseId,

        // Update events
        ChangesSaved = CoreEventId.ProviderBaseId + 100
    }

    private static readonly string TransactionPrefix = DbLoggerCategory.Database.Transaction.Name + ".";

    private static EventId MakeTransactionId(Id id)
        => new((int)id, TransactionPrefix + id);

    /// <summary>
    ///     A transaction operation was requested, but ignored because Kafka does not support transactions.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This event is in the <see cref="DbLoggerCategory.Database.Transaction" /> category.
    ///     </para>
    ///     <para>
    ///         This event uses the <see cref="EventData" /> payload when used with a <see cref="DiagnosticSource" />.
    ///     </para>
    /// </remarks>
    public static readonly EventId TransactionIgnoredWarning = MakeTransactionId(Id.TransactionIgnoredWarning);

    private static readonly string UpdatePrefix = DbLoggerCategory.Update.Name + ".";

    private static EventId MakeUpdateId(Id id)
        => new((int)id, UpdatePrefix + id);

    /// <summary>
    ///     Changes were saved to the database.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This event is in the <see cref="DbLoggerCategory.Update" /> category.
    ///     </para>
    ///     <para>
    ///         This event uses the <see cref="SaveChangesEventData" /> payload when used with a <see cref="DiagnosticSource" />.
    ///     </para>
    /// </remarks>
    public static readonly EventId ChangesSaved = MakeUpdateId(Id.ChangesSaved);
}
