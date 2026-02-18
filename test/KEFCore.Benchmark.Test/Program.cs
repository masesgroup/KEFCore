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
            ProgramConfig.LoadConfig(args);
            ExecuteTests();
        }

        static void ExecuteTests()
        {
            int maxTests = ProgramConfig.Config.NumberOfExecutions;
            Dictionary<int, ExecutionData> _tests = new();
            BloggingContext context = null;
            var testWatcher = new Stopwatch();
            var globalWatcher = new Stopwatch();

            try
            {
                globalWatcher.Start();
                using (context = new BloggingContext())
                {
                    ProgramConfig.Config.ApplyOnContext(context);

                    if (ProgramConfig.Config.DeleteApplicationData)
                    {
                        ProgramConfig.ReportString("Process EnsureDeleted");
                        context.Database.EnsureDeleted();
                        ProgramConfig.ReportString("EnsureDeleted deleted database");
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

                for (int execution = 0; execution < maxTests; execution++)
                {
                    _tests.Add(execution, new ExecutionData(execution, maxTests));
                    ProgramConfig.ReportString($"Starting cycle number {execution}");
                    Stopwatch singleTestWatch = Stopwatch.StartNew();
                    using (context = new BloggingContext())
                    {
                        ProgramConfig.Config.ApplyOnContext(context);

                        Stopwatch watch = new();
                        watch.Restart();
                        var post = context.Posts.SingleOrDefault(b => b.BlogId == 2);
                        watch.Stop();
                        _tests[execution].QueryTimes[0] = watch.Elapsed;
                        ProgramConfig.ReportString($"First execution of context.Posts.Single(b => b.BlogId == 2) takes {watch.Elapsed}. Result is {post}", post == default);

                        watch.Restart();
                        post = context.Posts.SingleOrDefault(b => b.BlogId == 2);
                        watch.Stop();
                        _tests[execution].QueryTimes[1] = watch.Elapsed;
                        ProgramConfig.ReportString($"Second execution of context.Posts.Single(b => b.BlogId == 2) takes {watch.Elapsed}. Result is {post}", post == default);

                        watch.Restart();
                        post = context.Posts.SingleOrDefault(b => b.BlogId == ProgramConfig.Config.NumberOfElements - 1);
                        watch.Stop();
                        _tests[execution].QueryTimes[2] = watch.Elapsed;
                        ProgramConfig.ReportString($"Execution of context.Posts.Single(b => b.BlogId == {ProgramConfig.Config.NumberOfElements - 1}) takes {watch.Elapsed}. Result is {post}", post == default);

                        watch.Restart();
                        var all = context.Posts.All((o) => true);
                        watch.Stop();
                        _tests[execution].QueryTimes[3] = watch.Elapsed;
                        ProgramConfig.ReportString($"Execution of context.Posts.All((o) => true) takes {watch.Elapsed}. Result is {all}");

                        Blog blog = null;
                        watch.Restart();
                        blog = context.Blogs.SingleOrDefault(b => b.BlogId == 1);
                        watch.Stop();
                        _tests[execution].QueryTimes[4] = watch.Elapsed;

                        ProgramConfig.ReportString($"First execution of context.Blogs.Single(b => b.BlogId == 1) takes {watch.Elapsed}. Result is {blog}", blog == default);
                        watch.Restart();
                        blog = context.Blogs.SingleOrDefault(b => b.BlogId == 1);

                        watch.Stop();
                        _tests[execution].QueryTimes[5] = watch.Elapsed;
                        ProgramConfig.ReportString($"Second execution of context.Blogs.Single(b => b.BlogId == 1) takes {watch.Elapsed}. Result is {blog}", blog == default);

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
                Environment.ExitCode = ProgramConfig.ManageException(ex);
            }
            finally
            {
                try
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
                    for (int i = 0; i < _tests.Count; i++)
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
                        ProgramConfig.ReportString($"Test {testId} -> Max {max[testId]} Min {min[testId]} Mean {total[testId] / maxTests}");
                    }
                }
                catch { ProgramConfig.ReportString($"Failed to report test execution"); }
            }
        }
    }


    public class BloggingContext : KafkaDbContext
    {
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
