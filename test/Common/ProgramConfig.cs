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
using MASES.EntityFrameworkCore.KNet.Serialization.Avro;
using MASES.EntityFrameworkCore.KNet.Serialization.Avro.Storage;
using MASES.EntityFrameworkCore.KNet.Serialization.Protobuf;
using MASES.EntityFrameworkCore.KNet.Serialization.Protobuf.Storage;
using MASES.KNet.Streams;
using System.Diagnostics;
using System;
using System.IO;
using System.Text.Json;
using Java.Lang;
using Java.Util.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace MASES.EntityFrameworkCore.KNet.Test.Common
{
    public class ProgramConfig
    {
        public string ApplicationHeapSize { get; set; } = Environment.Is64BitOperatingSystem? "4G" : "2G";
        public string ApplicationInitialHeapSize { get; set; } = Environment.Is64BitOperatingSystem ? "512M" : "256M";
        public bool UseJson { get; set; } = false;
        public bool UseProtobuf { get; set; } = false;
        public bool UseAvro { get; set; } = false;
        public bool UseAvroBinary { get; set; } = true;
        public bool EnableKEFCoreTracing { get; set; } = false;
        public bool UseInMemoryProvider { get; set; } = false;
        public bool UseModelBuilder { get; set; } = false;
        public bool UseCompactedReplicator { get; set; } = false;
        public bool UseKNetStreams { get; set; } = true;
        public bool UseEnumeratorWithPrefetch { get; set; } = true;
        public bool UseByteBufferDataTransfer { get; set; } = true;
        public bool PreserveStreamsAcrossContexts { get; set; } = true;
        public bool UsePersistentStorage { get; set; } = false;
        public string DatabaseName { get; set; } = "TestDB";
        public string DatabaseNameWithModel { get; set; } = "TestDBWithModel";
        public string ApplicationId { get; set; } = "TestApplication";
        public bool DeleteApplicationData { get; set; } = true;
        public bool LoadApplicationData { get; set; } = true;
        public string BootstrapServers { get; set; } = "localhost:9092";
        public string TopicToSubscribe { get; set; }
        public int NumberOfElements { get; set; } = 1000;
        public int NumberOfExecutions { get; set; } = 1;
        public int NumberOfExtraElements { get; set; } = 100;
        public bool WithEvents { get; set; } = false;

        public void ApplyOnContext(KafkaDbContext context)
        {
            var databaseName = UseModelBuilder ? DatabaseNameWithModel : DatabaseName;

            StreamsConfigBuilder streamConfig = null;
            if (!UseInMemoryProvider)
            {
                streamConfig = StreamsConfigBuilder.Create();
                streamConfig = streamConfig.WithAcceptableRecoveryLag(100);
            }

            context.DatabaseName = databaseName;
            context.StreamsConfig = streamConfig;
            context.BootstrapServers = BootstrapServers;
            context.ApplicationId = ApplicationId;
            context.UsePersistentStorage = UsePersistentStorage;
            context.UseCompactedReplicator = UseCompactedReplicator;
            context.UseKNetStreams = UseKNetStreams;
            context.UseEnumeratorWithPrefetch = UseEnumeratorWithPrefetch;
            context.UseByteBufferDataTransfer = UseByteBufferDataTransfer;
            context.EmitEvents = WithEvents;

            if (UseJson)
            { // default
            }
            else if (UseProtobuf)
            {
                context.KeySerDesSelectorType = typeof(ProtobufKEFCoreSerDes.Key<>);
                context.ValueContainerType = typeof(ProtobufValueContainer<>);
                context.ValueSerDesSelectorType = typeof(ProtobufKEFCoreSerDes.ValueContainer<>);
            }
            else if (UseAvro)
            {
                context.KeySerDesSelectorType = UseAvroBinary ? typeof(AvroKEFCoreSerDes.Key.Binary<>)
                                                              : typeof(AvroKEFCoreSerDes.Key.Json<>);
                context.ValueContainerType = typeof(AvroValueContainer<>);
                context.ValueSerDesSelectorType = UseAvroBinary ? typeof(AvroKEFCoreSerDes.ValueContainer.Binary<>)
                                                                : typeof(AvroKEFCoreSerDes.ValueContainer.Json<>);
            }
        }

        public static ProgramConfig Config { get; private set; }

        public static void LoadConfig(string[] args)
        {
            const string FileFormat = "/f:";
            const string PropertyFormat = "/p:";

            Dictionary<PropertyInfo, object> properties = new Dictionary<PropertyInfo, object>();
            var props = typeof(ProgramConfig).GetProperties();
            string file = null;
            foreach (var arg in args)
            {
                if (arg.StartsWith(FileFormat))
                {
                    file = arg[FileFormat.Length..];
                    if (!File.Exists(file)) { throw new FileNotFoundException($"{file} is not a configuration file.", file); }
                }
                else if (arg.StartsWith(PropertyFormat))
                {
                    var argVal = arg[PropertyFormat.Length..];
                    var values = argVal.Split('=');
                    foreach (var prop in props)
                    {
                        if (prop.Name == values[0])
                        {
                            properties.Add(prop, Convert.ChangeType(values[1], prop.PropertyType));
                        }
                    }
                }
                else if (File.Exists(arg)) file = arg;
            }

            if (!string.IsNullOrWhiteSpace(file))
            {
                Config = JsonSerializer.Deserialize<ProgramConfig>(File.ReadAllText(file));
            }
            else Config = new();

            foreach (var property in properties)
            {
                property.Key.SetValue(Config, property.Value);
            }

            //if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX)
            //    && Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != null)
            //{
            //    Config.NumberOfElements = 100; // try reduce number of elements to verify if MacOS goes out-of-memory in GitHub action runner
            //}

            ReportString(JsonSerializer.Serialize(Config, new JsonSerializerOptions() { WriteIndented = true }));

            if (!KafkaDbContext.EnableKEFCoreTracing) KafkaDbContext.EnableKEFCoreTracing = Config.EnableKEFCoreTracing;

            if (!Config.UseInMemoryProvider)
            {
                KEFCore.ApplicationHeapSize = Config.ApplicationHeapSize;
                KEFCore.ApplicationInitialHeapSize = Config.ApplicationInitialHeapSize;
                KEFCore.CreateGlobalInstance();
                KEFCore.PreserveStreamsAcrossContexts = Config.PreserveStreamsAcrossContexts;
            }
        }

        public static void ReportString(string message, bool noDataReturned = false)
        {
            var msg = $"{DateTime.Now:HH::mm::ss:ffff} - {(noDataReturned ? "No data returned for " : " ")}{message}";

            if (Debugger.IsAttached)
            {
                if (noDataReturned) Trace.TraceError(msg);
                else Trace.WriteLine(msg);
            }
            else
            {
                if (noDataReturned) Console.Error.WriteLine(msg);
                else Console.WriteLine(msg);
            }
        }

        public static int ManageException(System.Exception e)
        {
            int retCode = 0;
            if (e is System.Reflection.TargetInvocationException ti)
            {
                return ManageException(ti.InnerException);
            }
            else if (e is ExecutionException ee)
            {
                return ManageException(ee.InnerException);
            }
            else if (e is ClassNotFoundException cnfe)
            {
                ReportString($"Failed with {cnfe}, current ClassPath is {KEFCore.GlobalInstance.ClassPath}");
                retCode = 1;
            }
            else if (e is NoClassDefFoundError ncdfe)
            {
                ReportString($"Failed with {ncdfe}, current ClassPath is {KEFCore.GlobalInstance.ClassPath}");
                retCode = 1;
            }
            else if (e is Org.Apache.Kafka.Common.Errors.TimeoutException toe)
            {
                ReportString(toe.ToString(), true);
            }
            else
            {
                ReportString($"Failed with {e}");
                retCode = 1;
            }
            return retCode;
        }
    }
}
