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

using MASES.EntityFrameworkCore.KNet.Internal;

namespace MASES.EntityFrameworkCore.KNet.Diagnostics.Internal;

public static class KafkaLoggerExtensions
{
    public static void TransactionIgnoredWarning(
        this IDiagnosticsLogger<DbLoggerCategory.Database.Transaction> diagnostics)
    {
        var definition = KafkaResources.LogTransactionsNotSupported(diagnostics);

        if (diagnostics.ShouldLog(definition))
        {
            definition.Log(diagnostics);
        }

        if (diagnostics.NeedsEventData(definition, out var diagnosticSourceEnabled, out var simpleLogEnabled))
        {
            var eventData = new EventData(
                definition,
                (d, _) => ((EventDefinition)d).GenerateMessage());

            diagnostics.DispatchEventData(definition, eventData, diagnosticSourceEnabled, simpleLogEnabled);
        }
    }

    public static void ChangesSaved(
        this IDiagnosticsLogger<DbLoggerCategory.Update> diagnostics,
        IEnumerable<IUpdateEntry> entries,
        int rowsAffected)
    {
        var definition = KafkaResources.LogSavedChanges(diagnostics);

        if (diagnostics.ShouldLog(definition))
        {
            definition.Log(diagnostics, rowsAffected);
        }

        if (diagnostics.NeedsEventData(definition, out var diagnosticSourceEnabled, out var simpleLogEnabled))
        {
            var eventData = new SaveChangesEventData(
                definition,
                ChangesSaved,
                entries,
                rowsAffected);

            diagnostics.DispatchEventData(definition, eventData, diagnosticSourceEnabled, simpleLogEnabled);
        }
    }

    private static string ChangesSaved(EventDefinitionBase definition, EventData payload)
    {
        var d = (EventDefinition<int>)definition;
        var p = (SaveChangesEventData)payload;
        return d.GenerateMessage(p.RowsAffected);
    }
}
