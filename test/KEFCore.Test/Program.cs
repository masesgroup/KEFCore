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
using MASES.EntityFrameworkCore.KNet.Storage;
using MASES.EntityFrameworkCore.KNet.Test.Common;
using MASES.EntityFrameworkCore.KNet.Test.Model;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace MASES.EntityFrameworkCore.KNet.Test
{
    partial class Program
    {
        static BloggingContext context = null;

        static void Main(string[] args)
        {
            var testWatcher = new Stopwatch();
            var globalWatcher = new Stopwatch();

            ProgramConfig.LoadConfig(args);

            if (!ProgramConfig.Config.UseInMemoryProvider)
            {
                KEFCore.CreateGlobalInstance();
            }

            try
            {
                globalWatcher.Start();
                context = new BloggingContext()
                {
                    OnChangeEvent = ProgramConfig.Config.WithEvents ? OnEvent : null,
                };

                ProgramConfig.Config.ApplyOnContext(context);
                
                if (ProgramConfig.Config.DeleteApplicationData)
                {
                    context.Database.EnsureDeleted();
                    context.Database.EnsureCreated();
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
                    var pageObject = selector.FirstOrDefault();
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
                    post = context.Posts.Single(b => b.BlogId == 1);
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
                Console.WriteLine(ex.ToString());
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
