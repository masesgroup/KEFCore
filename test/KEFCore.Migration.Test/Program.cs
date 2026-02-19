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
using MASES.EntityFrameworkCore.KNet.Storage;
using MASES.EntityFrameworkCore.KNet.Test.Common;
using MASES.EntityFrameworkCore.KNet.Test.Common.Model.Evolved;
using Microsoft.EntityFrameworkCore;
using System;
using System.Diagnostics;
using System.Linq;

namespace MASES.EntityFrameworkCore.KNet.Test
{
    partial class Program
    {
        static BloggingContext context = null;

        static void Main(string[] args)
        {
            ProgramConfig.LoadConfig(args);
            ExecuteTests();
        }

        static void ExecuteTests()
        {
            var testWatcher = new Stopwatch();
            var globalWatcher = new Stopwatch();

            try
            {
                globalWatcher.Start();
                context = new BloggingContext()
                {
                    OnChangeEvent = ProgramConfig.Config.WithEvents ? OnEvent : null,
                };

                ProgramConfig.Config.ApplyOnContext(context);

                if (context.Database.EnsureCreated())
                {
                    ProgramConfig.ReportString("EnsureCreated created database");
                }
                else
                {
                    ProgramConfig.ReportString("EnsureCreated does not created database");
                }

                testWatcher.Start();
                Stopwatch watch = new Stopwatch();

                if (ProgramConfig.Config.UseModelBuilder)
                {
                    watch.Restart();
                    var selector = (from op in context.Blogs
                                    join pg in context.Posts on op.BlogId equals pg.BlogId
                                    where pg.BlogId == op.BlogId
                                    select new { pg, op });
                    var pageObject = selector.FirstOrDefault();
                    watch.Stop();
                    ProgramConfig.ReportString($"Elapsed UseModelBuilder {watch.ElapsedMilliseconds} ms");
                }

                watch.Restart();
                var post = context.Posts.Include(o => o.Blog).Single(b => b.BlogId == 20);
                watch.Stop();
                ProgramConfig.ReportString($"Elapsed context.Posts.Single(b => b.BlogId == 2) {watch.ElapsedMilliseconds} ms. Result is {post}");

                try
                {
                    watch.Restart();
                    post = context.Posts.Include(o => o.Blog).Single(b => b.BlogId == 10);
                    watch.Stop();
                    ProgramConfig.ReportString($"Elapsed context.Posts.Single(b => b.BlogId == 1) {watch.ElapsedMilliseconds} ms. Result is {post}");
                }
                catch
                {
                    if (ProgramConfig.Config.LoadApplicationData) throw; // throw only if the test is loading data otherwise it was removed in a previous run
                }

                watch.Restart();
                var all = context.Posts.All((o) => true);
                watch.Stop();
                ProgramConfig.ReportString($"Elapsed context.Posts.All((o) => true) {watch.ElapsedMilliseconds} ms. Result is {all}");

                Blog blog = null;
                try
                {
                    watch.Restart();
                    blog = context.Blogs!.Single(b => b.BlogId == 100);
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
                        context.Add(new Blog
                        {
                            Date = DateTime.Now,
                            Posts =
                            [
                                new()
                                {
                                    Title = "title",
                                    Content = i.ToString()
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

                var postion = ProgramConfig.Config.NumberOfElements + ProgramConfig.Config.NumberOfExtraElements - 1;
                watch.Restart();
                post = context.Posts.Single(b => b.BlogId == postion);
                watch.Stop();
                ProgramConfig.ReportString($"Elapsed context.Posts.Single(b => b.BlogId == {postion}) {watch.ElapsedMilliseconds} ms. Result is {post}");

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

        static void OnEvent(EntityTypeChanged change)
        {
            object value = null;
            try
            {
                value = context.Find(change.EntityType.ClrType, change.Key);
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }

            ProgramConfig.ReportString($"{change.EntityType.Name} -> {(change.KeyRemoved ? "removed" : "updated/added")}: {change.Key} - {value}");
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
