/*
*  Copyright 2023 MASES s.r.l.
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

#nullable enable

using Avro;

namespace MASES.EntityFrameworkCore.KNet.Serialization.Avro.Compiler;

public static class AvroSerializationHelper
{
    public static void BuildSchemaClasses( string outputFolder, params string[] schemas)
    {
        var codegen = new CodeGen();
        foreach (var schema in schemas)
        {
            codegen.AddSchema(schema);
        }
        codegen.GenerateCode();
        codegen.WriteTypes(outputFolder, true);
    }

    public static void BuildSchemaClassesFromFiles( string outputFolder, params string[] schemaFiles)
    {
        var codegen = new CodeGen();
        foreach (var schemaFile in schemaFiles)
        {
            var schema = File.ReadAllText(schemaFile);
            codegen.AddSchema(schema);
        }
        codegen.GenerateCode();
        codegen.WriteTypes(outputFolder, true);
    }

    public static void BuildDefaultSchema(string outputFolder)
    {
        BuildSchemaClassesFromFiles(outputFolder, "AvroValueContainer.avsc");
        BuildSchemaClassesFromFiles(outputFolder, "AvroKeyContainer.avsc");
    }
}

