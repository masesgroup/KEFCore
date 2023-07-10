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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace MASES.EntityFrameworkCore.KNet.Test
{
    class Program
    {
        const string theServer = "localhost:9092";
        static string serverToUse = theServer;

        static void Main(string[] args)
        {
            KEFCore.CreateGlobalInstance();
            var appArgs = KEFCore.FilteredArgs;

            if (appArgs.Length != 0)
            {
                serverToUse = args[0];
            }

            var streamConfig = StreamsConfigBuilder.Create();
            streamConfig = streamConfig.WithAcceptableRecoveryLag(100);

            using (var context = new BloggingContext()
            {
                BootstrapServers = serverToUse,
                ApplicationId = "TestApplication",
                DbName = "TestDB",
                StreamsConfigBuilder = streamConfig,
            })
            {
                context.Database.EnsureDeleted();
                context.Database.EnsureCreated();

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
                context.SaveChanges();

            }

            using (var context = new BloggingContext()
            {
                BootstrapServers = serverToUse,
                ApplicationId = "TestApplication",
                DbName = "TestDB",
                StreamsConfigBuilder = streamConfig,
            })
            {

                //var pageObject = (from op in context.Blogs
                //                  join pg in context.Posts on op.BlogId equals pg.BlogId
                //                  where pg.BlogId == op.BlogId
                //                  select new { pg, op }).SingleOrDefault();

                Stopwatch watch = Stopwatch.StartNew();
                var post = context.Posts.Single(b => b.BlogId == 2);
                watch.Stop();
                Trace.WriteLine($"Elapsed {watch.ElapsedMilliseconds} ms");

                watch.Restart();
                post = context.Posts.Single(b => b.BlogId == 1);
                watch.Stop();
                Trace.WriteLine($"Elapsed {watch.ElapsedMilliseconds} ms");

                watch.Restart();
                var all = context.Posts.All((o) => true);
                watch.Stop();
                Trace.WriteLine($"Elapsed {watch.ElapsedMilliseconds} ms");

                watch.Restart();
                var blog = context.Blogs!.Single(b => b.BlogId == 1);
                watch.Stop();
                Trace.WriteLine($"Elapsed {watch.ElapsedMilliseconds} ms");

                var value = context.Blogs.AsQueryable().ToQueryString();
            }
        }
    }

    public class BloggingContext : KafkaDbContext
    {
        public DbSet<Blog> Blogs { get; set; }
        public DbSet<Post> Posts { get; set; }

        //protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        //{
        //    optionsBuilder.UseKafkaDatabase(ApplicationId, DbName, BootstrapServers, (o) =>
        //    {
        //        o.StreamsConfig(o.EmptyStreamsConfigBuilder.WithAcceptableRecoveryLag(100)).WithDefaultNumPartitions(10);
        //    });
        //}

        //protected override void OnModelCreating(ModelBuilder modelBuilder)
        //{
        //    modelBuilder.Entity<Blog>().HasKey(c => new { c.BlogId, c.Rating });
        //}
    }

    public class Blog
    {
        public int BlogId { get; set; }
        public string Url { get; set; }
        public long Rating { get; set; }
        public List<Post> Posts { get; set; }
    }

    public class Post
    {
        public int PostId { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }

        public int BlogId { get; set; }
        public Blog Blog { get; set; }
    }
}
