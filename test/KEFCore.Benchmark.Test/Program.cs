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
using MASES.EntityFrameworkCore.KNet.Test.Common;
using MASES.EntityFrameworkCore.KNet.Test.Model;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace MASES.EntityFrameworkCore.KNet.Test.Benchmark
{
    partial class Program
    {
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

        static void Main(string[] args)
        {
            const int maxTests = 10;
            Dictionary<int, ExecutionData> _tests = new();
            BloggingContext context = null;
            var testWatcher = new Stopwatch();
            var globalWatcher = new Stopwatch();

            ProgramConfig.LoadConfig(args);

            if (!ProgramConfig.Config.UseInMemoryProvider)
            {
                KEFCore.CreateGlobalInstance();
                KEFCore.PreserveInformationAcrossContexts = ProgramConfig.Config.PreserveInformationAcrossContexts;
            }

            try
            {
                globalWatcher.Start();
                using (context = new BloggingContext())
                {
                    ProgramConfig.Config.ApplyOnContext(context);

                    if (ProgramConfig.Config.DeleteApplicationData)
                    {
                        context.Database.EnsureDeleted();
                        if (context.Database.EnsureCreated())
                        {
                            ProgramConfig.ReportString("EnsureCreated created database");
                        }
                        else
                        {
                            ProgramConfig.ReportString("EnsureCreated does not created database");
                        }
                    }

                    testWatcher.Start();
                    Stopwatch watch = new Stopwatch();
                    if (ProgramConfig.Config.LoadApplicationData)
                    {
                        watch.Start();
                        for (int i = 0; i < ProgramConfig.Config.NumberOfElements; i++)
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
                        ProgramConfig.ReportString($"Elapsed data load: {watch.Elapsed}");
                        watch.Restart();
                        context.SaveChanges();
                        watch.Stop();
                        ProgramConfig.ReportString($"Elapsed SaveChanges: {watch.Elapsed}");
                    }
                }

                for (int execution = 0; execution < ProgramConfig.Config.NumberOfExecutions; execution++)
                {
                    _tests.Add(execution, new ExecutionData(execution, maxTests));
                    ProgramConfig.ReportString($"Starting cycle number {execution}");
                    Stopwatch singleTestWatch = Stopwatch.StartNew();
                    using (context = new BloggingContext())
                    {
                        ProgramConfig.Config.ApplyOnContext(context);

                        Stopwatch watch = new();
                        watch.Restart();
                        var post = context.Posts.Single(b => b.BlogId == 2);
                        watch.Stop();
                        _tests[execution].QueryTimes[0] = watch.Elapsed;
                        ProgramConfig.ReportString($"First execution of context.Posts.Single(b => b.BlogId == 2) takes {watch.Elapsed}. Result is {post}");

                        watch.Restart();
                        post = context.Posts.Single(b => b.BlogId == 2);
                        watch.Stop();
                        _tests[execution].QueryTimes[1] = watch.Elapsed;
                        ProgramConfig.ReportString($"Second execution of context.Posts.Single(b => b.BlogId == 2) takes {watch.Elapsed}. Result is {post}");

                        watch.Restart();
                        post = context.Posts.Single(b => b.BlogId == ProgramConfig.Config.NumberOfElements - 1);
                        watch.Stop();
                        _tests[execution].QueryTimes[2] = watch.Elapsed;
                        ProgramConfig.ReportString($"Execution of context.Posts.Single(b => b.BlogId == {ProgramConfig.Config.NumberOfElements - 1}) takes {watch.Elapsed}. Result is {post}");

                        watch.Restart();
                        var all = context.Posts.All((o) => true);
                        watch.Stop();
                        _tests[execution].QueryTimes[3] = watch.Elapsed;
                        ProgramConfig.ReportString($"Execution of context.Posts.All((o) => true) takes {watch.Elapsed}. Result is {all}");

                        Blog blog = null;
                        watch.Restart();
                        blog = context.Blogs.Single(b => b.BlogId == 1);
                        watch.Stop();
                        _tests[execution].QueryTimes[4] = watch.Elapsed;

                        ProgramConfig.ReportString($"First execution of context.Blogs.Single(b => b.BlogId == 1) takes {watch.Elapsed}. Result is {blog}");
                        watch.Restart();
                        blog = context.Blogs.Single(b => b.BlogId == 1);

                        watch.Stop();
                        _tests[execution].QueryTimes[5] = watch.Elapsed;
                        ProgramConfig.ReportString($"Second execution of context.Blogs.Single(b => b.BlogId == 1) takes {watch.Elapsed}. Result is {blog}");

                        watch.Restart();
                        var selector = (from op in context.Blogs
                                        join pg in context.Posts on op.BlogId equals pg.BlogId
                                        where pg.BlogId == op.BlogId
                                        select new { pg, op });
                        watch.Stop();
                        _tests[execution].QueryTimes[6] = watch.Elapsed;
                        var result = selector.ToList();
                        ProgramConfig.ReportString($"Execution of first complex query takes {watch.Elapsed}. Result is {result.Count} element{(result.Count == 1 ? string.Empty : "s")}");

                        watch.Restart();
                        var selector2 = (from op in context.Blogs
                                         join pg in context.Posts on op.BlogId equals pg.BlogId
                                         where op.Rating >= 100
                                         select new { pg, op });
                        watch.Stop();
                        _tests[execution].QueryTimes[7] = watch.Elapsed;
                        var result2 = selector.ToList();
                        ProgramConfig.ReportString($"Execution of second complex query takes {watch.Elapsed}. Result is {result2.Count} element{(result2.Count == 1 ? string.Empty : "s")}");
                        singleTestWatch.Stop();
                        _tests[execution].QueryTimes[8] = singleTestWatch.Elapsed;
                        ProgramConfig.ReportString($"Test {execution} takes {singleTestWatch.Elapsed}.");
                    }
                }
            }
            catch (Exception ex)
            {
                ProgramConfig.ReportString(ex.ToString());
            }
            finally
            {
                testWatcher.Stop();
                globalWatcher.Stop();
                context?.Dispose();
                ProgramConfig.ReportString(string.Empty);
                ProgramConfig.ReportString($"Full test completed in {globalWatcher.Elapsed}, only tests completed in {testWatcher.Elapsed}");

                TimeSpan[] max = new TimeSpan[maxTests];
                for (int i = 0; i < max.Length; i++) { max[i] = TimeSpan.Zero; }
                TimeSpan[] min = new TimeSpan[maxTests];
                for (int i = 0; i < min.Length; i++) { min[i] = TimeSpan.MaxValue; }
                TimeSpan[] total = new TimeSpan[maxTests];
                for (int i = 0; i < total.Length; i++) { total[i] = TimeSpan.Zero; }
                for (int i = 0; i < ProgramConfig.Config.NumberOfExecutions; i++)
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
                    ProgramConfig.ReportString($"Test {testId} -> Max {max[testId]} Min {min[testId]} Mean {total[testId] / ProgramConfig.Config.NumberOfExecutions}");
                }
            }
        }
    }

    public class BloggingContext : KafkaDbContext
    {
        public override bool UsePersistentStorage { get; set; } = ProgramConfig.Config.UsePersistentStorage;
        public override bool UseCompactedReplicator { get; set; } = ProgramConfig.Config.UseCompactedReplicator;
        public override bool UseKNetStreams { get; set; } = ProgramConfig.Config.UseKNetStreams;
        public override bool UseEnumeratorWithPrefetch { get; set; } = ProgramConfig.Config.UseEnumeratorWithPrefetch;

        public DbSet<Blog> Blogs { get; set; }
        public DbSet<Post> Posts { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (ProgramConfig.Config.UseInMemoryProvider)
            {
                optionsBuilder.UseInMemoryDatabase(ProgramConfig.Config.DatabaseName);
            }
            else
            {
                base.OnConfiguring(optionsBuilder);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            if (!ProgramConfig.Config.UseModelBuilder) return;

            modelBuilder.Entity<Blog>().HasKey(c => new { c.BlogId, c.Rating });
        }
    }
}
