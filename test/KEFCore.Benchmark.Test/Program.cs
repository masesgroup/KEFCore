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

using MASES.EntityFrameworkCore.KNet.Test.Common;
using MASES.EntityFrameworkCore.KNet.Test.Common.Model.Base;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace MASES.EntityFrameworkCore.KNet.Test.Benchmark
{
    partial class Program
    {
        readonly struct ExecutionData(int executionIndex)
        {
            public readonly int ExecutionIndex = executionIndex;
            public readonly List<TimeSpan> QueryTimes = [];
        }

        static void Main(string[] args)
        {
            ProgramConfig.LoadConfig(args);
            ExecuteTests();
        }

        static void ExecuteTests()
        {
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
                    }

                    Stopwatch watch = new();
                    watch.Start();
                    if (context.Database.EnsureCreated()) // call always for initialization
                    {
                        watch.Stop();
                        ProgramConfig.ReportString($"EnsureCreated created database in {watch.ElapsedMilliseconds} ms");
                    }
                    else
                    {
                        watch.Stop();
                        ProgramConfig.ReportString($"EnsureCreated does not created database in {watch.ElapsedMilliseconds} ms");
                    }
                    watch.Start();

                    testWatcher.Start();
                    if (ProgramConfig.Config.LoadApplicationData)
                    {
                        watch.Start();
                        for (int i = 0; i < ProgramConfig.Config.NumberOfElements; i++)
                        {
                            context.Add(new Blog
                            {
                                Url = "http://blogs.msdn.com/adonet" + i.ToString(),
                                Posts =
                                [
                                    new Post()
                                    {
                                        Title = "title",
                                        Content = i.ToString()
                                    }
                                ],
                                Rating = i,
                            });
                        }
                        watch.Stop();
                        ProgramConfig.ReportString($"Elapsed data load: {watch.Elapsed}");
                        watch.Restart();
                        context.SaveChanges();
                        watch.Stop();
                        ProgramConfig.ReportString($"Elapsed SaveChanges: {watch.Elapsed}");
                        watch.Restart();
                        var res = context.WaitForSynchronization();
                        watch.Stop();
                        if (res.HasValue && res.Value)
                        {
                            ProgramConfig.ReportString($"Local store synchronized in {watch.ElapsedMilliseconds} ms.");
                        }
                        else
                        {
                            ProgramConfig.ReportString($"Local store is not synchronized.");
                        }
                    }
                }

                for (int execution = 0; execution < ProgramConfig.Config.NumberOfExecutions; execution++)
                {
                    _tests.Add(execution, new ExecutionData(execution));
                    ProgramConfig.ReportString($"Starting cycle number {execution}");
                    Stopwatch singleTestWatch = Stopwatch.StartNew();
                    using (context = new BloggingContext())
                    {
                        ProgramConfig.Config.ApplyOnContext(context);

                        Stopwatch watch = new();
                        watch.Restart();
                        var post = context.Posts.SingleOrDefault(b => b.BlogId == 2);
                        watch.Stop();
                        _tests[execution].QueryTimes.Add(watch.Elapsed);
                        ProgramConfig.ReportString($"First execution of context.Posts.Single(b => b.BlogId == 2) takes {watch.Elapsed}. Result is {post}", post == default);

                        watch.Restart();
                        post = context.Posts.SingleOrDefault(b => b.BlogId == 2);
                        watch.Stop();
                        _tests[execution].QueryTimes.Add(watch.Elapsed);
                        ProgramConfig.ReportString($"Second execution of context.Posts.Single(b => b.BlogId == 2) takes {watch.Elapsed}. Result is {post}", post == default);

                        watch.Restart();
                        post = context.Posts.SingleOrDefault(b => b.BlogId == ProgramConfig.Config.NumberOfElements - 1);
                        watch.Stop();
                        _tests[execution].QueryTimes.Add(watch.Elapsed);
                        ProgramConfig.ReportString($"Execution of context.Posts.Single(b => b.BlogId == {ProgramConfig.Config.NumberOfElements - 1}) takes {watch.Elapsed}. Result is {post}", post == default);

                        watch.Restart();
                        var all = context.Posts.All((o) => true);
                        watch.Stop();
                        _tests[execution].QueryTimes.Add(watch.Elapsed);
                        ProgramConfig.ReportString($"Execution of context.Posts.All((o) => true) takes {watch.Elapsed}. Result is {all}");

                        Blog blog = null;
                        watch.Restart();
                        blog = context.Blogs.SingleOrDefault(b => b.BlogId == 1);
                        watch.Stop();
                        _tests[execution].QueryTimes.Add(watch.Elapsed);

                        ProgramConfig.ReportString($"First execution of context.Blogs.Single(b => b.BlogId == 1) takes {watch.Elapsed}. Result is {blog}", blog == default);
                        watch.Restart();
                        blog = context.Blogs.SingleOrDefault(b => b.BlogId == 1);

                        watch.Stop();
                        _tests[execution].QueryTimes.Add(watch.Elapsed);
                        ProgramConfig.ReportString($"Second execution of context.Blogs.Single(b => b.BlogId == 1) takes {watch.Elapsed}. Result is {blog}", blog == default);

                        watch.Restart();
                        blog = context.Blogs.SingleOrDefault(b => b.BlogId == ProgramConfig.Config.NumberOfElements - 1);

                        watch.Stop();
                        _tests[execution].QueryTimes.Add(watch.Elapsed);
                        ProgramConfig.ReportString($"First execution of context.Blogs.Single(b => b.BlogId == {ProgramConfig.Config.NumberOfElements - 1}) takes {watch.Elapsed}. Result is {blog}", blog == default);

                        watch.Restart();
                        blog = context.Blogs.SingleOrDefault(b => b.BlogId == ProgramConfig.Config.NumberOfElements - 1);

                        watch.Stop();
                        _tests[execution].QueryTimes.Add(watch.Elapsed);
                        ProgramConfig.ReportString($"Second execution of context.Blogs.Single(b => b.BlogId == {ProgramConfig.Config.NumberOfElements - 1}) takes {watch.Elapsed}. Result is {blog}", blog == default);

                        watch.Restart();
                        int count = context.Blogs.Where(b => b.BlogId > 1 && b.BlogId < ProgramConfig.Config.NumberOfElements - 10).Count();

                        watch.Stop();
                        _tests[execution].QueryTimes.Add(watch.Elapsed);
                        ProgramConfig.ReportString($"First execution of context.Blogs.Where(b => b.BlogId > 1 && b.BlogId < {ProgramConfig.Config.NumberOfElements - 10}).Count() takes {watch.Elapsed}. Result is {count}", count == 0);

                        watch.Restart();
                        count = context.Blogs.Where(b => b.BlogId > 1 && b.BlogId < ProgramConfig.Config.NumberOfElements - 10).Count();

                        watch.Stop();
                        _tests[execution].QueryTimes.Add(watch.Elapsed);
                        ProgramConfig.ReportString($"Second execution of context.Blogs.Where(b => b.BlogId > 1 && b.BlogId < {ProgramConfig.Config.NumberOfElements - 10}).Count() takes {watch.Elapsed}. Result is {count}", count == 0);

                        watch.Restart();
                        var selector = (from op in context.Blogs
                                        join pg in context.Posts on op.BlogId equals pg.BlogId
                                        where pg.BlogId == op.BlogId
                                        select new { pg, op });
                        watch.Stop();
                        _tests[execution].QueryTimes.Add(watch.Elapsed);
                        var result = selector.ToList();
                        ProgramConfig.ReportString($"Execution of first complex query takes {watch.Elapsed}. Result is {result.Count} element{(result.Count == 1 ? string.Empty : "s")}");

                        watch.Restart();
                        var selector2 = (from op in context.Blogs
                                         join pg in context.Posts on op.BlogId equals pg.BlogId
                                         where op.Rating >= 100
                                         select new { pg, op });
                        watch.Stop();
                        _tests[execution].QueryTimes.Add(watch.Elapsed);
                        var result2 = selector2.ToList();
                        ProgramConfig.ReportString($"Execution of second complex query takes {watch.Elapsed}. Result is {result2.Count} element{(result2.Count == 1 ? string.Empty : "s")}");
                        singleTestWatch.Stop();
                        _tests[execution].QueryTimes.Add(singleTestWatch.Elapsed);

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

                    int testDone = _tests[0].QueryTimes.Count;

                    TimeSpan[] max = new TimeSpan[testDone];
                    for (int i = 0; i < max.Length; i++) { max[i] = TimeSpan.Zero; }
                    TimeSpan[] min = new TimeSpan[testDone];
                    for (int i = 0; i < min.Length; i++) { min[i] = TimeSpan.MaxValue; }
                    TimeSpan[] total = new TimeSpan[testDone];
                    for (int i = 0; i < total.Length; i++) { total[i] = TimeSpan.Zero; }
                    for (int i = 0; i < ProgramConfig.Config.NumberOfExecutions; i++)
                    {
                        var item = _tests[i].QueryTimes;

                        for (int testId = 0; testId < testDone; testId++)
                        {
                            max[testId] = item[testId] > max[testId] ? item[testId] : max[testId];
                            min[testId] = item[testId] < min[testId] ? item[testId] : min[testId];
                            total[testId] += item[testId];
                        }
                    }

                    for (int testId = 0; testId < testDone; testId++)
                    {
                        ProgramConfig.ReportString($"Test {testId} -> Max {max[testId]} Min {min[testId]} Mean {total[testId] / ProgramConfig.Config.NumberOfExecutions}");
                    }
                }
                catch { ProgramConfig.ReportString($"Failed to report test execution"); }
            }
        }
    }


    public class BloggingContext : TestContext
    {
        public DbSet<Blog> Blogs { get; set; }
        public DbSet<Post> Posts { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            if (!ProgramConfig.Config.UseModelBuilder) return;

            modelBuilder.Entity<Blog>().HasKey(c => new { c.BlogId, c.Rating });
        }
    }
}
