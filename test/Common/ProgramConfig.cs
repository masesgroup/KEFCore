/*
 *  MIT License
 *
 *  Copyright (c) 2024 MASES s.r.l.
 *
 *  Permission is hereby granted, free of charge, to any person obtaining a copy
 *  of this software and associated documentation files (the "Software"), to deal
 *  in the Software without restriction, including without limitation the rights
 *  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 *  copies of the Software, and to permit persons to whom the Software is
 *  furnished to do so, subject to the following conditions:
 *
 *  The above copyright notice and this permission notice shall be included in all
 *  copies or substantial portions of the Software.
 *
 *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 *  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 *  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 *  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 *  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 *  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 *  SOFTWARE.
 */

using MASES.EntityFrameworkCore.KNet.Infrastructure;
using MASES.EntityFrameworkCore.KNet.Serialization.Avro;
using MASES.EntityFrameworkCore.KNet.Serialization.Avro.Storage;
using MASES.EntityFrameworkCore.KNet.Serialization.Json;
using MASES.EntityFrameworkCore.KNet.Serialization.Protobuf;
using MASES.EntityFrameworkCore.KNet.Serialization.Protobuf.Storage;
using MASES.KNet.Streams;
using System.Diagnostics;
using System;
using System.IO;
using System.Text.Json;

namespace MASES.EntityFrameworkCore.KNet.Test.Common
{
    public class ProgramConfig
    {
        public bool UseProtobuf { get; set; } = false;
        public bool UseAvro { get; set; } = false;
        public bool UseAvroBinary { get; set; } = false;
        public bool EnableKEFCoreTracing { get; set; } = false;
        public bool UseInMemoryProvider { get; set; } = false;
        public bool UseModelBuilder { get; set; } = false;
        public bool UseCompactedReplicator { get; set; } = true;
        public bool UseKNetStreams { get; set; } = true;
        public bool UseEnumeratorWithPrefetch { get; set; } = true;
        public bool UseByteBufferDataTransfer { get; set; } = true;
        public bool PreserveInformationAcrossContexts { get; set; } = true;
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

            if (UseProtobuf)
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
            if (args.Length > 0)
            {
                if (!File.Exists(args[0])) { throw new FileNotFoundException($"{args[0]} is not a configuration file.", args[0]); }
                Config = JsonSerializer.Deserialize<ProgramConfig>(File.ReadAllText(args[0]));
            }
            else Config = new();

            if (args.Length > 1)
            {
                Config.BootstrapServers = args[1];
            }

            ReportString(JsonSerializer.Serialize<ProgramConfig>(Config, new JsonSerializerOptions() { WriteIndented = true }));

            if (!KafkaDbContext.EnableKEFCoreTracing) KafkaDbContext.EnableKEFCoreTracing = Config.EnableKEFCoreTracing;
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
    }
}
