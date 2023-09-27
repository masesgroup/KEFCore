/*
 *  MIT License
 *
 *  Copyright (c) 2022 MASES s.r.l.
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
using MASES.KNet.Streams;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace MASES.EntityFrameworkCore.KNet.Test
{
    partial class Program
    {
        const string theServer = "localhost:9092";
        static string serverToUse = theServer;
        static string databaseName = "TestDB";
        static string databaseNameWithModel = "TestDBWithModel";
        static string applicationId = "TestApplication";

        static void Main(string[] args)
        {
            if (!UseInMemoryProvider)
            {
                KEFCore.CreateGlobalInstance();
                var appArgs = KEFCore.FilteredArgs;

                if (appArgs.Length > 0)
                {
                    serverToUse = args[0];
                }

                if (appArgs.Length > 1)
                {
                    deleteApplication = args[1].ToLowerInvariant() == "true";
                }

                if (appArgs.Length > 2)
                {
                    applicationId = args[2];
                }
            }

            DatabaseName = UseModelBuilder ? databaseNameWithModel : databaseName;

            var globalWatcher = Stopwatch.StartNew();
            StreamsConfigBuilder streamConfig = null;
            if (!UseInMemoryProvider)
            {
                streamConfig = StreamsConfigBuilder.Create();
                streamConfig = streamConfig.WithAcceptableRecoveryLag(100);
            }

            var context = new BloggingContext()
            {
                BootstrapServers = serverToUse,
                ApplicationId = applicationId,
                DbName = DatabaseName,
                StreamsConfigBuilder = streamConfig,
            };

            if (deleteApplication)
            {
                context.Database.EnsureDeleted();
                context.Database.EnsureCreated();
            }

            var testWatcher = Stopwatch.StartNew();
            Stopwatch watch = Stopwatch.StartNew();
            for (int i = 0; i < 1000; i++)
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
            Trace.WriteLine($"Elapsed data load {watch.ElapsedMilliseconds} ms");

            watch.Restart();
            context.SaveChanges();
            watch.Stop();
            Trace.WriteLine($"Elapsed SaveChanges {watch.ElapsedMilliseconds} ms");

            if (UseModelBuilder)
            {
                watch.Restart();
                var pageObject = (from op in context.Blogs
                                  join pg in context.Posts on op.BlogId equals pg.BlogId
                                  where pg.BlogId == op.BlogId
                                  select new { pg, op }).SingleOrDefault();
                watch.Stop();
                Trace.WriteLine($"Elapsed UseModelBuilder {watch.ElapsedMilliseconds} ms");
            }

            watch.Restart();
            var post = context.Posts.Single(b => b.BlogId == 2);
            watch.Stop();
            Trace.WriteLine($"Elapsed context.Posts.Single(b => b.BlogId == 2) {watch.ElapsedMilliseconds} ms. Result is {post}");

            watch.Restart();
            post = context.Posts.Single(b => b.BlogId == 1);
            watch.Stop();
            Trace.WriteLine($"Elapsed context.Posts.Single(b => b.BlogId == 1) {watch.ElapsedMilliseconds} ms. Result is {post}");

            watch.Restart();
            var all = context.Posts.All((o) => true);
            watch.Stop();
            Trace.WriteLine($"Elapsed context.Posts.All((o) => true) {watch.ElapsedMilliseconds} ms. Result is {all}");

            watch.Restart();
            var blog = context.Blogs!.Single(b => b.BlogId == 1);
            watch.Stop();
            Trace.WriteLine($"Elapsed context.Blogs!.Single(b => b.BlogId == 1) {watch.ElapsedMilliseconds} ms. Result is {blog}");

            watch.Restart();
            context.Remove(post);
            context.Remove(blog);
            watch.Stop();
            Trace.WriteLine($"Elapsed data remove {watch.ElapsedMilliseconds} ms");

            watch.Restart();
            context.SaveChanges();
            watch.Stop();
            Trace.WriteLine($"Elapsed SaveChanges {watch.ElapsedMilliseconds} ms");

            watch.Restart();
            for (int i = 1000; i < 1100; i++)
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
            Trace.WriteLine($"Elapsed data load {watch.ElapsedMilliseconds} ms");

            watch.Restart();
            context.SaveChanges();
            watch.Stop();
            Trace.WriteLine($"Elapsed SaveChanges {watch.ElapsedMilliseconds} ms");

            watch.Restart();
            post = context.Posts.Single(b => b.BlogId == 1009);
            watch.Stop();
            Trace.WriteLine($"Elapsed context.Posts.Single(b => b.BlogId == 1009) {watch.ElapsedMilliseconds} ms. Result is {post}");

            var value = context.Blogs.AsQueryable().ToQueryString();

            context?.Dispose();
            testWatcher.Stop();
            globalWatcher.Stop();
            Console.WriteLine($"Full test completed in {globalWatcher.Elapsed}, only tests completed in {testWatcher.Elapsed}");
        }
    }

    public class BloggingContext : KafkaDbContext
    {
        public override bool UseCompactedReplicator { get; set; } = Program.UseCompactedReplicator;

        public DbSet<Blog> Blogs { get; set; }
        public DbSet<Post> Posts { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (Program.UseInMemoryProvider)
            {
                optionsBuilder.UseInMemoryDatabase(Program.DatabaseName);
            }
            else
            {
                base.OnConfiguring(optionsBuilder);
            }
            //optionsBuilder.UseKafkaDatabase(ApplicationId, DbName, BootstrapServers, (o) =>
            //{
            //    o.StreamsConfig(o.EmptyStreamsConfigBuilder.WithAcceptableRecoveryLag(100)).WithDefaultNumPartitions(10);
            //});
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            if (!Program.UseModelBuilder) return;

            modelBuilder.Entity<Blog>().HasKey(c => new { c.BlogId, c.Rating });
        }
    }

    public class Blog
    {
        public int BlogId { get; set; }
        public string Url { get; set; }
        public long Rating { get; set; }
        public List<Post> Posts { get; set; }

        public override string ToString()
        {
            return $"BlogId: {BlogId} Url: {Url} Rating: {Rating}";
        }
    }

    public class Post
    {
        public int PostId { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }

        public int BlogId { get; set; }
        public Blog Blog { get; set; }

        public override string ToString()
        {
            return $"PostId: {PostId} Title: {Title} Content: {Content} BlogId: {BlogId}";
        }
    }
}
