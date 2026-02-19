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
using MASES.EntityFrameworkCore.KNet.Test.Common.Model.Complex;
using Microsoft.EntityFrameworkCore;
using System;
using System.Diagnostics;
using System.Linq;

namespace MASES.EntityFrameworkCore.KNet.Test.Complex
{
    partial class Program
    {
        static void Main(string[] args)
        {
            ProgramConfig.LoadConfig(args);
            ExecuteTests();
        }

        static void ExecuteTests()
        {
            BloggingContext context = null;
            var testWatcher = new Stopwatch();
            var globalWatcher = new Stopwatch();

            try
            {
                globalWatcher.Start();
                context = new BloggingContext();
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
                    for (uint i = 0; i < ProgramConfig.Config.NumberOfElements; i++)
                    {
                        context.Add(new BlogComplex
                        {
                            Url = "http://blogs.msdn.com/adonet" + i.ToString(),
                            BooleanValue = i % 2 == 0,
                            NullableBooleanValue = i % 3 == 0 ? null : i % 2 == 0,
                            PricingInfo = new Pricing()
                            {
                                Discounts =
                                [
                                    new()
                                    {
                                        Validity = new DateRange()
                                        {
                                            CurrentDiff = i,
                                            Min = DateTime.UtcNow.Subtract(TimeSpan.FromHours(i)),
                                            Max = DateTime.Now.AddHours(i),
                                        }
                                    }
                                ],
                                Tax = new TaxInfo()
                                {
                                    Code = char.ConvertFromUtf32((int)i)[0],
                                    Percentage = i / 2
                                }
                            },
                            ComplexPosts =
                            [
                                new()
                                {
                                    Title = "title",
                                    Content = i.ToString(),
                                    CreationTime = DateTimeOffset.Now,
                                    Identifier = Guid.NewGuid()
                                }
                            ],
                            Rating = (int)i,
                        });
                    }
                    watch.Stop();
                    ProgramConfig.ReportString($"Elapsed data load {watch.ElapsedMilliseconds} ms");
                    watch.Restart();
                    context.SaveChanges();
                    watch.Stop();
                    ProgramConfig.ReportString($"Elapsed SaveChanges {watch.ElapsedMilliseconds} ms");
                }

                if (ProgramConfig.Config.UseModelBuilder)
                {
                    watch.Restart();
                    var selector = (from op in context.Blogs
                                    join pg in context.Posts on op.BlogId equals pg.BlogId
                                    where pg.BlogId == op.BlogId
                                    select new { pg, op });
                    var pageObject = selector.SingleOrDefault();
                    watch.Stop();
                    ProgramConfig.ReportString($"Elapsed UseModelBuilder {watch.ElapsedMilliseconds} ms");
                }

                watch.Restart();
                var post = context.Posts.Single(b => b.BlogId == 2);
                watch.Stop();
                ProgramConfig.ReportString($"Elapsed context.Posts.Single(b => b.BlogId == 2) {watch.ElapsedMilliseconds} ms. Result is {post}");

                try
                {
                    watch.Restart();
                    post = context.Posts.Single(b => b.BlogId == 100);
                    watch.Stop();
                    ProgramConfig.ReportString($"Elapsed context.Posts.Single(b => b.BlogId == 100) {watch.ElapsedMilliseconds} ms. Result is {post}");
                }
                catch
                {
                    if (ProgramConfig.Config.LoadApplicationData) throw; // throw only if the test is loading data otherwise it was removed in a previous run
                }

                watch.Restart();
                var all = context.Posts.All((o) => true);
                watch.Stop();
                ProgramConfig.ReportString($"Elapsed context.Posts.All((o) => true) {watch.ElapsedMilliseconds} ms. Result is {all}");

                BlogComplex blog = null;
                try
                {
                    watch.Restart();
                    blog = context.Blogs!.Single(b => b.BlogId == 1);
                    watch.Stop();
                    ProgramConfig.ReportString($"Elapsed context.Blogs!.Single(b => b.BlogId == 1) {watch.ElapsedMilliseconds} ms. Result is {blog}");
                }
                catch
                {
                    if (ProgramConfig.Config.LoadApplicationData) throw; // throw only if the test is loading data otherwise it was removed in a previous run
                }

                if (ProgramConfig.Config.LoadApplicationData)
                {
                    watch.Restart();
                    context.Remove(post);
                    context.Remove(blog);
                    watch.Stop();
                    ProgramConfig.ReportString($"Elapsed data remove {watch.ElapsedMilliseconds} ms");

                    watch.Restart();
                    context.SaveChanges();
                    watch.Stop();
                    ProgramConfig.ReportString($"Elapsed SaveChanges {watch.ElapsedMilliseconds} ms");

                    watch.Restart();
                    for (int i = ProgramConfig.Config.NumberOfElements; i < ProgramConfig.Config.NumberOfElements + ProgramConfig.Config.NumberOfExtraElements; i++)
                    {
                        context.Add(new BlogComplex
                        {
                            Url = "http://blogs.msdn.com/adonet" + i.ToString(),
                            BooleanValue = i % 2 == 0,
                            ComplexPosts =
                            [
                                new()
                                {
                                    Title = "title",
                                    Content = i.ToString(),
                                    CreationTime = DateTime.UtcNow,
                                    Identifier = Guid.NewGuid()
                                }
                            ],
                            Rating = i,
                        });
                    }
                    watch.Stop();
                    ProgramConfig.ReportString($"Elapsed data load {watch.ElapsedMilliseconds} ms");
                    watch.Restart();
                    context.SaveChanges();
                    watch.Stop();
                    ProgramConfig.ReportString($"Elapsed SaveChanges {watch.ElapsedMilliseconds} ms");
                }

                try
                {
                    watch.Restart();
                    blog = context.Blogs!.Single(b => b.BlogId == 101);
                    watch.Stop();
                    ProgramConfig.ReportString($"Elapsed context.Blogs!.Single(b => b.BlogId == 101) {watch.ElapsedMilliseconds} ms. Result is {blog}");
                }
                catch
                {
                    if (ProgramConfig.Config.LoadApplicationData) throw; // throw only if the test is loading data otherwise it was removed in a previous run
                }

                watch.Restart();
                post = context.Posts.Single(b => b.BlogId == ProgramConfig.Config.NumberOfElements + (ProgramConfig.Config.NumberOfExtraElements != 0 ? 1 : 0));
                watch.Stop();
                ProgramConfig.ReportString($"Elapsed context.Posts.Single(b => b.BlogId == config.NumberOfElements + (config.NumberOfExtraElements != 0 ? 1 : 0)) {watch.ElapsedMilliseconds} ms. Result is {post}");

                var value = context.Blogs.AsQueryable().ToQueryString();
            }
            catch (Exception ex)
            {
                Environment.ExitCode = ProgramConfig.ManageException(ex);
            }
            finally
            {
                context?.Dispose();
                testWatcher.Stop();
                globalWatcher.Stop();
                Console.WriteLine($"Full test completed in {globalWatcher.Elapsed}, only tests completed in {testWatcher.Elapsed}");
            }
        }
    }

    public class BloggingContext : KafkaDbContext
    {
        public DbSet<BlogComplex> Blogs { get; set; }
        public DbSet<PostComplex> Posts { get; set; }

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

            modelBuilder.Entity<BlogComplex>().HasKey(c => new { c.BlogId, c.Rating });
        }
    }
}
