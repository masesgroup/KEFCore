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

using Java.Sql;
using MASES.EntityFrameworkCore.KNet.Infrastructure;
using MASES.EntityFrameworkCore.KNet.Serialization.Avro;
using MASES.EntityFrameworkCore.KNet.Serialization.Avro.Storage;
using MASES.EntityFrameworkCore.KNet.Serialization.Json.Storage;
using MASES.EntityFrameworkCore.KNet.Serialization.Protobuf;
using MASES.EntityFrameworkCore.KNet.Serialization.Protobuf.Storage;
using MASES.EntityFrameworkCore.KNet.Test.Common;
using MASES.EntityFrameworkCore.KNet.Test.Model;
using MASES.KNet.Streams;
using Microsoft.EntityFrameworkCore;
using Org.Apache.Kafka.Common.Metrics.Stats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MASES.EntityFrameworkCore.KNet.Test.Benchmark
{
    partial class Program
    {
        internal static ProgramConfig config = new();

        struct ExecutionData
        {
            public ExecutionData(int executionIndex, int maxTests)
            {
                ExecutionIndex = executionIndex;
                QueryTimes = new TimeSpan[maxTests];
            }
            public int ExecutionIndex;
            public TimeSpan[] QueryTimes;
        }

        static void ReportString(string message)
        {
            if (Debugger.IsAttached)
            {
                Trace.WriteLine($"{DateTime.Now:HH::mm::ss:ffff} - {message}");
            }
            else
            {
                Console.WriteLine($"{DateTime.Now:HH::mm::ss:ffff} - {message}");
            }
        }

        static void Main(string[] args)
        {
            const int maxTests = 10;
            Dictionary<int, ExecutionData> _tests = new();
            BloggingContext context = null;
            var testWatcher = new Stopwatch();
            var globalWatcher = new Stopwatch();

            if (args.Length > 0)
            {
                if (!File.Exists(args[0])) { ReportString($"{args[0]} is not a configuration file."); return; }
                config = JsonSerializer.Deserialize<ProgramConfig>(File.ReadAllText(args[0]));
            }

            if (!KafkaDbContext.EnableKEFCoreTracing) KafkaDbContext.EnableKEFCoreTracing = config.EnableKEFCoreTracing;

            if (!config.UseInMemoryProvider)
            {
                KEFCore.CreateGlobalInstance();
                KEFCore.PreserveInformationAcrossContexts = config.PreserveInformationAcrossContexts;
            }

            var databaseName = config.UseModelBuilder ? config.DatabaseNameWithModel : config.DatabaseName;

            try
            {
                globalWatcher.Start();
                StreamsConfigBuilder streamConfig = null;
                if (!config.UseInMemoryProvider)
                {
                    streamConfig = StreamsConfigBuilder.Create();
                    streamConfig = streamConfig.WithAcceptableRecoveryLag(100);
                }

                using (context = new BloggingContext()
                {
                    BootstrapServers = config.BootstrapServers,
                    ApplicationId = config.ApplicationId,
                    DatabaseName = databaseName,
                    StreamsConfig = streamConfig,
                })
                {
                    if (config.UseProtobuf)
                    {
                        context.KeySerializationType = typeof(ProtobufKEFCoreSerDes.Key.Binary<>);
                        context.ValueContainerType = typeof(ProtobufValueContainer<>);
                        context.ValueSerializationType = typeof(ProtobufKEFCoreSerDes.ValueContainer.Binary<>);
                    }
                    else if (config.UseAvro)
                    {
                        context.KeySerializationType = config.UseAvroBinary ? typeof(AvroKEFCoreSerDes.Key.Binary<>) : typeof(AvroKEFCoreSerDes.Key.Json<>);
                        context.ValueContainerType = typeof(AvroValueContainer<>);
                        context.ValueSerializationType = config.UseAvroBinary ? typeof(AvroKEFCoreSerDes.ValueContainer.Binary<>) : typeof(AvroKEFCoreSerDes.ValueContainer.Json<>);
                    }

                    if (config.DeleteApplicationData)
                    {
                        context.Database.EnsureDeleted();
                        if (context.Database.EnsureCreated())
                        {
                            ReportString("EnsureCreated created database");
                        }
                        else
                        {
                            ReportString("EnsureCreated does not created database");
                        }
                    }

                    testWatcher.Start();
                    Stopwatch watch = new Stopwatch();
                    if (config.LoadApplicationData)
                    {
                        watch.Start();
                        for (int i = 0; i < config.NumberOfElements; i++)
                        {
                            context.Add(new Blog
                            {
                                Url = "http://blogs.msdn.com/adonet" + i.ToString(),
                                Posts = new List<Post>()
                                {
                                    new Post()
                                    {
                                        Title = "title",
                                        Content = i.ToString()
                                    }
                                },
                                Rating = i,
                            });
                        }
                        watch.Stop();
                        ReportString($"Elapsed data load: {watch.Elapsed}");
                        watch.Restart();
                        context.SaveChanges();
                        watch.Stop();
                        ReportString($"Elapsed SaveChanges: {watch.Elapsed}");
                    }
                }

                for (int execution = 0; execution < config.NumberOfExecutions; execution++)
                {
                    _tests.Add(execution, new ExecutionData(execution, maxTests));
                    ReportString($"Starting cycle number {execution}");
                    Stopwatch singleTestWatch = Stopwatch.StartNew();
                    using (context = new BloggingContext()
                    {
                        BootstrapServers = config.BootstrapServers,
                        ApplicationId = config.ApplicationId,
                        DatabaseName = databaseName,
                        StreamsConfig = streamConfig,
                    })
                    {
                        if (config.UseProtobuf)
                        {
                            context.KeySerializationType = typeof(ProtobufKEFCoreSerDes.Key.Binary<>);
                            context.ValueContainerType = typeof(ProtobufValueContainer<>);
                            context.ValueSerializationType = typeof(ProtobufKEFCoreSerDes.ValueContainer.Binary<>);
                        }
                        else if (config.UseAvro)
                        {
                            context.KeySerializationType = config.UseAvroBinary ? typeof(AvroKEFCoreSerDes.Key.Binary<>) : typeof(AvroKEFCoreSerDes.Key.Json<>);
                            context.ValueContainerType = typeof(AvroValueContainer<>);
                            context.ValueSerializationType = config.UseAvroBinary ? typeof(AvroKEFCoreSerDes.ValueContainer.Binary<>) : typeof(AvroKEFCoreSerDes.ValueContainer.Json<>);
                        }

                        Stopwatch watch = new();
                        watch.Restart();
                        var post = context.Posts.Single(b => b.BlogId == 2);
                        watch.Stop();
                        _tests[execution].QueryTimes[0] = watch.Elapsed;
                        ReportString($"First execution of context.Posts.Single(b => b.BlogId == 2) takes {watch.Elapsed}. Result is {post}");

                        watch.Restart();
                        post = context.Posts.Single(b => b.BlogId == 2);
                        watch.Stop();
                        _tests[execution].QueryTimes[1] = watch.Elapsed;
                        ReportString($"Second execution of context.Posts.Single(b => b.BlogId == 2) takes {watch.Elapsed}. Result is {post}");

                        watch.Restart();
                        post = context.Posts.Single(b => b.BlogId == config.NumberOfElements - 1);
                        watch.Stop();
                        _tests[execution].QueryTimes[2] = watch.Elapsed;
                        ReportString($"Execution of context.Posts.Single(b => b.BlogId == {config.NumberOfElements - 1}) takes {watch.Elapsed}. Result is {post}");

                        watch.Restart();
                        var all = context.Posts.All((o) => true);
                        watch.Stop();
                        _tests[execution].QueryTimes[3] = watch.Elapsed;
                        ReportString($"Execution of context.Posts.All((o) => true) takes {watch.Elapsed}. Result is {all}");

                        Blog blog = null;
                        watch.Restart();
                        blog = context.Blogs.Single(b => b.BlogId == 1);
                        watch.Stop();
                        _tests[execution].QueryTimes[4] = watch.Elapsed;

                        ReportString($"First execution of context.Blogs.Single(b => b.BlogId == 1) takes {watch.Elapsed}. Result is {blog}");
                        watch.Restart();
                        blog = context.Blogs.Single(b => b.BlogId == 1);

                        watch.Stop();
                        _tests[execution].QueryTimes[5] = watch.Elapsed;
                        ReportString($"Second execution of context.Blogs.Single(b => b.BlogId == 1) takes {watch.Elapsed}. Result is {blog}");

                        watch.Restart();
                        var selector = (from op in context.Blogs
                                        join pg in context.Posts on op.BlogId equals pg.BlogId
                                        where pg.BlogId == op.BlogId
                                        select new { pg, op });
                        watch.Stop();
                        _tests[execution].QueryTimes[6] = watch.Elapsed;
                        var result = selector.ToList();
                        ReportString($"Execution of first complex query takes {watch.Elapsed}. Result is {result.Count} element{(result.Count == 1 ? string.Empty : "s")}");

                        watch.Restart();
                        var selector2 = (from op in context.Blogs
                                         join pg in context.Posts on op.BlogId equals pg.BlogId
                                         where op.Rating >= 100
                                         select new { pg, op });
                        watch.Stop();
                        _tests[execution].QueryTimes[7] = watch.Elapsed;
                        var result2 = selector.ToList();
                        ReportString($"Execution of second complex query takes {watch.Elapsed}. Result is {result2.Count} element{(result2.Count == 1 ? string.Empty : "s")}");
                        singleTestWatch.Stop();
                        _tests[execution].QueryTimes[8] = singleTestWatch.Elapsed;
                        ReportString($"Test {execution} takes {singleTestWatch.Elapsed}.");
                    }
                }
            }
            catch (Exception ex)
            {
                ReportString(ex.ToString());
            }
            finally
            {
                testWatcher.Stop();
                globalWatcher.Stop();
                context?.Dispose();
                ReportString(string.Empty);
                ReportString($"Full test completed in {globalWatcher.Elapsed}, only tests completed in {testWatcher.Elapsed}");

                TimeSpan[] max = new TimeSpan[maxTests];
                for (int i = 0; i < max.Length; i++) { max[i] = TimeSpan.Zero; }
                TimeSpan[] min = new TimeSpan[maxTests];
                for (int i = 0; i < min.Length; i++) { min[i] = TimeSpan.MaxValue; }
                TimeSpan[] total = new TimeSpan[maxTests];
                for (int i = 0; i < total.Length; i++) { total[i] = TimeSpan.Zero; }
                for (int i = 0; i < config.NumberOfExecutions; i++)
                {
                    var item = _tests[i].QueryTimes;

                    for (int testId = 0; testId < maxTests; testId++)
                    {
                        max[testId] = item[testId] > max[testId] ? item[testId] : max[testId];
                        min[testId] = item[testId] < min[testId] ? item[testId] : min[testId];
                        total[testId] += item[testId];
                    }
                }

                for (int testId = 0; testId < maxTests; testId++)
                {
                    ReportString($"Test {testId} -> Max {max[testId]} Min {min[testId]} Mean {total[testId] / config.NumberOfExecutions}");
                }
            }
        }
    }

    public class BloggingContext : KafkaDbContext
    {
        public override bool UsePersistentStorage { get; set; } = Program.config.UsePersistentStorage;
        public override bool UseCompactedReplicator { get; set; } = Program.config.UseCompactedReplicator;
        public override bool UseKNetStreams { get; set; } = Program.config.UseKNetStreams;
        public override bool UseEnumeratorWithPrefetch { get; set; } = Program.config.UseEnumeratorWithPrefetch;

        public DbSet<Blog> Blogs { get; set; }
        public DbSet<Post> Posts { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (Program.config.UseInMemoryProvider)
            {
                optionsBuilder.UseInMemoryDatabase(Program.config.DatabaseName);
            }
            else
            {
                base.OnConfiguring(optionsBuilder);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            if (!Program.config.UseModelBuilder) return;

            modelBuilder.Entity<Blog>().HasKey(c => new { c.BlogId, c.Rating });
        }
    }
}
