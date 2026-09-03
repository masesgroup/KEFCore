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

using Java.Lang;
using Java.Util.Concurrent;
using MASES.EntityFrameworkCore.KNet.Extensions;
using MASES.EntityFrameworkCore.KNet.Infrastructure;
using MASES.EntityFrameworkCore.KNet.Internal;
using MASES.EntityFrameworkCore.KNet.Serialization.Avro;
using MASES.EntityFrameworkCore.KNet.Serialization.Avro.Storage;
using MASES.EntityFrameworkCore.KNet.Serialization.Protobuf;
using MASES.EntityFrameworkCore.KNet.Serialization.Protobuf.Storage;
using MASES.KNet.Streams;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;

namespace MASES.EntityFrameworkCore.KNet.Test.Common
{
    public class DebugOutputLoggerProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new DebugOutputLogger(categoryName);
        public void Dispose() { }
    }

    public class DebugOutputLogger(string category) : ILogger
    {
        private readonly string _category = category;
        private readonly LogLevel _minLogLevel = !ProgramConfig.Config.ForceDebugLog 
                                                 && Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != null ? LogLevel.Information 
                                                                                                                 : LogLevel.Debug; 
 
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) =>
            logLevel >= _minLogLevel &&
            _category == DbLoggerCategory.Infrastructure.Name;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, System.Exception exception, Func<TState, System.Exception, string> formatter)
        {
            Console.WriteLine($"[{logLevel}] {DateTime.Now:HH::mm::ss:ffff} {_category}: {formatter(state, exception)}");
        }
    }

    public class TestContext : KEFCoreDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.UseKEFCoreTopicPrefix(ProgramConfig.Config.UseModelBuilder ? ProgramConfig.Config.TopicPrefixWithModel
                                                                                    : ProgramConfig.Config.TopicPrefix);

            modelBuilder.UseKEFCoreManageEvents(ProgramConfig.Config.ManageEvents);

            base.OnModelCreating(modelBuilder);
        }

        private static readonly ILoggerFactory SharedLoggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel((!ProgramConfig.Config.ForceDebugLog
                                    && Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != null)
                                    || !ProgramConfig.Config.EnableIntermediateOutput ? LogLevel.Information
                                                                                      : LogLevel.Debug)
                   .AddProvider(new DebugOutputLoggerProvider());
        });

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseLoggerFactory(SharedLoggerFactory);

            if (ProgramConfig.Config.UseInMemoryProvider)
            {
                optionsBuilder.UseInMemoryDatabase("InMemory");
            }
            else
            {
                base.OnConfiguring(optionsBuilder);
            }
        }
    }

    public class ProgramConfig
    {
        public string ApplicationHeapSize { get; set; } = Environment.Is64BitOperatingSystem ? "1G" : "512G";
        public string ApplicationInitialHeapSize { get; set; } = Environment.Is64BitOperatingSystem ? "256M" : "128M";
        public bool UseJson { get; set; } = false;
        public bool UseProtobuf { get; set; } = false;
        public bool UseAvro { get; set; } = false;
        public bool UseAvroBinary { get; set; } = true;
        public bool UseAvroBinaryLegacy { get; set; } = false;
        public bool EnableKEFCoreTracing { get; set; } = false;
        public bool UseInMemoryProvider { get; set; } = false;
        public bool UseModelBuilder { get; set; } = false;
        public bool UseCompactedReplicator { get; set; } = false;
        public bool UseKNetStreams { get; set; } = true;
        public bool UseEnumeratorWithPrefetch { get; set; } = true;
        public bool UseValueContainerByteBufferDataTransfer { get; set; } = true;
        public bool UsePersistentStorage { get; set; } = false;
        public string TopicPrefix { get; set; } = "TestDB";
        public string TopicPrefixWithModel { get; set; } = "TestDBWithModel";
        public string ApplicationId { get; set; } = "TestApplication";
        public bool DeleteApplicationData { get; set; } = true;
        public bool LoadApplicationData { get; set; } = true;
        public string BootstrapServers { get; set; } = "localhost:9092";
        public string TopicToSubscribe { get; set; }
        public int NumberOfElements { get; set; } = 1000;
        public int NumberOfExecutions { get; set; } = 1;
        public int NumberOfExtraElements { get; set; } = 100;
        public bool ManageEvents { get; set; } = true;
        public long DefaultSynchronizationTimeout { get; set; } = Timeout.Infinite;
        public bool ForceDebugLog { get; set; } = false;
        public bool EnableIntermediateOutput { get; set; } = false;
        public int ForwardCacheTimeout { get; set; } = -1;
        public int ReverseCacheTimeout { get; set; } = -1;
        /// <summary>
        /// Optional path to a JSON Lines file where <see cref="ReportResult"/> appends one structured record
        /// per call, in addition to the existing console output. <see langword="null"/>/empty (the default)
        /// disables structured output entirely — existing behavior and CI commands are unaffected unless this
        /// is explicitly set via the existing <c>/f:</c>/<c>/p:ResultsOutputPath=...</c> config mechanism.
        /// </summary>
        public string ResultsOutputPath { get; set; } = null;

        /// <summary>
        /// The backend/topology label this run is exercising - the file name (without extension) of the
        /// <c>/f:</c> config file passed on the command line, e.g. <c>"KafkaStreams.Raw"</c>,
        /// <c>"KNetStreams.Buffered.Prefetch"</c>, <c>"KNetReplicator"</c>. Populated by <see cref="LoadConfig"/>,
        /// <see langword="null"/> if no config file was given (e.g. defaults-only invocation).
        /// <para>
        /// Exists because CI's Linux leg (see <c>build_common.yaml</c>) invokes <c>Benchmark.Test.dll</c> up to
        /// 7 times per matrix cell - once per backend config file - all appending to the <em>same</em>
        /// <see cref="ResultsOutputPath"/> file via the same <c>/p:ResultsOutputPath=...</c> value. Without a
        /// field identifying which invocation produced which record, those genuinely different backends (native
        /// Kafka Streams vs KNetStreams, Raw vs Buffered persistence, with/without prefetch) get silently
        /// averaged together by any report that groups on <c>(project, framework, testId, cache, scenario)</c>
        /// alone - e.g. a non-cached <c>IterationTotal</c> spread of 123ms-197ms across backends collapsing into
        /// one uninformative median. This field lets reports add "backend" as its own group-by key instead.
        /// </para>
        /// </summary>
        public string BackendLabel { get; private set; } = null;

        public void ApplyOnContext(KEFCoreDbContext context)
        {
            var databaseName = UseModelBuilder ? TopicPrefixWithModel : TopicPrefix;

            StreamsConfigBuilder streamConfig = null;
            if (!UseInMemoryProvider)
            {
                streamConfig = StreamsConfigBuilder.Create();
                streamConfig = streamConfig.WithAcceptableRecoveryLag(100);
                context.TopicConfig = KEFCoreDbContext.DefaultTopicConfig;
                context.TopicConfig.RetentionBytes = 1024 * 1024 * 1024;
            }

            context.StreamsConfig = streamConfig;
            context.BootstrapServers = BootstrapServers;
            context.ApplicationId = ApplicationId;
            context.UsePersistentStorage = UsePersistentStorage;
            context.UseCompactedReplicator = UseCompactedReplicator;
            context.UseKNetStreams = UseKNetStreams;
            context.UseEnumeratorWithPrefetch = UseEnumeratorWithPrefetch;
            context.UseValueContainerByteBufferDataTransfer = UseValueContainerByteBufferDataTransfer;
            context.DefaultSynchronizationTimeout = DefaultSynchronizationTimeout;

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
            else if (UseAvroBinaryLegacy)
            {
                context.KeySerDesSelectorType = typeof(AvroKEFCoreSerDes.Key.Binary<>);
                context.ValueContainerType = typeof(AvroValueContainer<>);
                context.ValueSerDesSelectorType = typeof(AvroKEFCoreSerDes.ValueContainer.Binary<>);
                AvroKEFCoreSerDes.UseLegacyCodec = true;
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
            // Deliberately set after deserialization (not sourced from the config file's own JSON content):
            // this is the file's identity, not a tunable setting, and must survive regardless of what the
            // config file itself contains.
            Config.BackendLabel = file == null ? null : Path.GetFileNameWithoutExtension(file);
#if DEBUG
            Config.EnableIntermediateOutput = true;
#endif

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

            if (!DebugPerformanceHelper.EnableKEFCoreTracing) DebugPerformanceHelper.EnableKEFCoreTracing = Config.EnableKEFCoreTracing;

            if (!Config.UseInMemoryProvider)
            {
                if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != null)  // for GitHub problem with Linux container
                {
                    KEFCore.ApplicationIgnoreUnrecognized = true; // add this condition if the JVM does not support the UseContainerSupport
                    KEFCore.AddJVMOption("-XX:-UseContainerSupport");
                }
                KEFCore.ApplicationHeapSize = Config.ApplicationHeapSize;
                KEFCore.ApplicationInitialHeapSize = Config.ApplicationInitialHeapSize;
                KEFCore.CreateGlobalInstance();
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

        private static readonly object _resultsOutputLock = new();

        /// <summary>
        /// Reports a single measured/verified test outcome. Always logs via <see cref="ReportString"/> for console
        /// output (unchanged behavior). Additionally, when <see cref="ProgramConfig.ResultsOutputPath"/> is
        /// configured, appends one JSON Lines record to that file: <c>{ timestamp, project, framework, testId,
        /// elapsedMs, success, details, forwardCacheTimeout, reverseCacheTimeout, loadApplicationData, backendLabel }</c>.
        /// <c>framework</c> (e.g. "net9.0") and the TTL fields make it possible to group/compare records across CI
        /// matrix legs (different .NET/EF Core versions, cached vs non-cached) without needing to parse that
        /// information back out of a file name. <c>loadApplicationData</c> mirrors <see cref="LoadApplicationData"/>
        /// at the time of the call, so a "reload" CI leg (data already written by a previous process invocation,
        /// <c>/p:LoadApplicationData=false</c>) can be told apart from the initial "load" leg that produced it -
        /// without this, headline/aggregate reports silently merge measurements taken against a warm local store
        /// (reload) with measurements taken right after the initial seed (load), which skews any cached-vs-non-cached
        /// comparison toward "similar" numbers regardless of the actual cache setting. <c>backendLabel</c> mirrors
        /// <see cref="BackendLabel"/> - see its docstring for why CI's Linux Benchmark.Test leg needs this to keep
        /// its up-to-7 backend configs (native Kafka Streams vs KNetStreams, Raw vs Buffered, with/without
        /// prefetch) from being silently averaged into one meaningless median per matrix cell. Safe to call
        /// repeatedly within
        /// a single process (e.g. inside <see cref="ProgramConfig.NumberOfExecutions"/> loops) and safe if
        /// multiple test processes happen to append to the same configured path (lock-guarded per-process;
        /// <see cref="File.AppendAllText(string, string)"/> append-mode writes are also safe across processes
        /// on the platforms this test suite targets).
        /// </summary>
        /// <param name="testId">A short, stable identifier for the specific test/measurement (e.g. the query or scenario name).</param>
        /// <param name="elapsed">The measured elapsed time for this test.</param>
        /// <param name="success">Whether the test passed its correctness check, if any. Defaults to <see langword="true"/> for pure timing measurements with no correctness assertion.</param>
        /// <param name="details">Optional free-form details (e.g. the verified value, or a mismatch description).</param>
        public static void ReportResult(string testId, TimeSpan elapsed, bool success = true, string details = null)
        {
            ReportString($"{testId} -> {elapsed}{(details == null ? string.Empty : $" ({details})")}", !success);

            if (string.IsNullOrWhiteSpace(Config?.ResultsOutputPath)) return;

            var record = new
            {
                timestamp = DateTime.UtcNow.ToString("o"),
                project = Assembly.GetEntryAssembly()?.GetName().Name,
                // Self-describing: derived from the actual running .NET runtime rather than relying on
                // external labeling or parsing it back out of a CI-generated file name. Formatted to match
                // the "net8.0"/"net9.0"/"net10.0" target-framework monikers used elsewhere (CI matrix,
                // artifact/cache naming) so records can be grouped/compared by framework (== EF Core major
                // version) directly, which is exactly what a cross-run aggregate report needs.
                framework = $"net{Environment.Version.Major}.{Environment.Version.Minor}",
                testId,
                elapsedMs = elapsed.TotalMilliseconds,
                success,
                details,
                forwardCacheTimeout = Config.ForwardCacheTimeout,
                reverseCacheTimeout = Config.ReverseCacheTimeout,
                loadApplicationData = Config.LoadApplicationData,
                backendLabel = Config.BackendLabel,
            };
            var line = JsonSerializer.Serialize(record) + Environment.NewLine;

            lock (_resultsOutputLock)
            {
                File.AppendAllText(Config.ResultsOutputPath, line);
            }
        }

        /// <summary>
        /// Computes Max/Min/Mean/Median across a set of repeated timing samples for the same measurement and
        /// reports them via <see cref="ReportResult"/>, using the median as the reported elapsed value (more
        /// robust to occasional outliers - a cold first iteration, a GC pause - than the mean). Shared by every
        /// test project that repeats a measurement across <see cref="NumberOfExecutions"/> iterations (e.g.
        /// KEFCore.Benchmark.Test's per-query loop, KEFCore.Complex.Test's projection-correctness checks), so
        /// the statistics/format are computed in exactly one place rather than duplicated per project.
        /// </summary>
        /// <param name="testId">A short, stable identifier for the specific test/measurement.</param>
        /// <param name="samples">The repeated elapsed-time samples for this measurement. A no-op if empty.</param>
        /// <param name="extraDetails">Optional free-form details (e.g. a verified value) prepended to the computed statistics in the reported details string.</param>
        public static void ReportTimingStats(string testId, TimeSpan[] samples, string extraDetails = null)
        {
            if (samples == null || samples.Length == 0) return;
            var sorted = (TimeSpan[])samples.Clone();
            Array.Sort(sorted);
            var max = sorted[^1];
            var min = sorted[0];
            var mean = TimeSpan.FromTicks((long)sorted.Select(t => t.Ticks).Average());
            int mid = sorted.Length / 2;
            var median = sorted.Length % 2 == 0
                ? TimeSpan.FromTicks((sorted[mid - 1].Ticks + sorted[mid].Ticks) / 2)
                : sorted[mid];
            var stats = $"N={samples.Length} Max {max} Min {min} Mean {mean} Median {median}";
            ReportResult(testId, median, details: extraDetails == null ? stats : $"{extraDetails} | {stats}");
        }

        public static int ManageException(System.Exception e, int iteration = -1)
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
                ReportString($"Failed{(iteration == -1 ? string.Empty : $" at iteration {iteration}")} with {e}");
                retCode = 1;
            }
            return retCode;
        }
    }
}
