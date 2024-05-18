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
using MASES.EntityFrameworkCore.KNet.Serialization.Avro;
using MASES.EntityFrameworkCore.KNet.Serialization.Avro.Storage;
using MASES.EntityFrameworkCore.KNet.Serialization.Protobuf;
using MASES.EntityFrameworkCore.KNet.Serialization.Protobuf.Storage;
using MASES.EntityFrameworkCore.KNet.Test.Common;
using MASES.EntityFrameworkCore.KNet.Test.Model;
using MASES.KNet.Streams;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MASES.EntityFrameworkCore.KNet.Complex.Test
{
    partial class Program
    {
        internal static ProgramConfig config = new();

        static void ReportString(string message)
        {
            if (Debugger.IsAttached)
            {
                Trace.WriteLine(message);
            }
            else
            {
                Console.WriteLine(message);
            }
        }

        static void Main(string[] args)
        {
            BloggingContext context = null;
            var testWatcher = new Stopwatch();
            var globalWatcher = new Stopwatch();

            if (args.Length > 0)
            {
                if (!File.Exists(args[0])) { ReportString($"{args[0]} is not a configuration file."); return; }
                config = JsonSerializer.Deserialize<ProgramConfig>(File.ReadAllText(args[0]));
            }

            if (!config.UseInMemoryProvider)
            {
                KEFCore.CreateGlobalInstance();
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

                context = new BloggingContext()
                {
                    BootstrapServers = config.BootstrapServers,
                    ApplicationId = config.ApplicationId,
                    DatabaseName = databaseName,
                    StreamsConfig = streamConfig,
                };

                if (config.UseProtobuf)
                {
                    context.KeySerializationType = typeof(ProtobufKEFCoreSerDes.Key.BinaryRaw<>);
                    context.ValueContainerType = typeof(ProtobufValueContainer<>);
                    context.ValueSerializationType = typeof(ProtobufKEFCoreSerDes.ValueContainer.BinaryRaw<>);
                }
                else if (config.UseAvro)
                {
                    context.KeySerializationType = config.UseAvroBinary ? typeof(AvroKEFCoreSerDes.Key.BinaryRaw<>) : typeof(AvroKEFCoreSerDes.Key.JsonRaw<>);
                    context.ValueContainerType = typeof(AvroValueContainer<>);
                    context.ValueSerializationType = config.UseAvroBinary ? typeof(AvroKEFCoreSerDes.ValueContainer.BinaryRaw<>) : typeof(AvroKEFCoreSerDes.ValueContainer.JsonRaw<>);
                }

                if (config.DeleteApplicationData)
                {
                    context.Database.EnsureDeleted();
                    context.Database.EnsureCreated();
                }

                testWatcher.Start();
                Stopwatch watch = new Stopwatch();
                if (config.LoadApplicationData)
                {
                    watch.Start();
                    for (int i = 0; i < config.NumberOfElements; i++)
                    {
                        context.Add(new BlogComplex
                        {
                            Url = "http://blogs.msdn.com/adonet" + i.ToString(),
                            BooleanValue = i % 2 == 0,
                            PostComplexs = new List<PostComplex>()
                            {
                                new PostComplex()
                                {
                                    Title = "title",
                                    Content = i.ToString(),
                                    CreationTime = DateTime.Now,
                                    Identifier = Guid.NewGuid()
                                }
                            },
                            Rating = i,
                        });
                    }
                    watch.Stop();
                    ReportString($"Elapsed data load {watch.ElapsedMilliseconds} ms");
                    watch.Restart();
                    context.SaveChanges();
                    watch.Stop();
                    ReportString($"Elapsed SaveChanges {watch.ElapsedMilliseconds} ms");
                }

                if (config.UseModelBuilder)
                {
                    watch.Restart();
                    var selector = (from op in context.Blogs
                                    join pg in context.Posts on op.BlogId equals pg.BlogId
                                    where pg.BlogId == op.BlogId
                                    select new { pg, op });
                    var pageObject = selector.SingleOrDefault();
                    watch.Stop();
                    ReportString($"Elapsed UseModelBuilder {watch.ElapsedMilliseconds} ms");
                }

                watch.Restart();
                var post = context.Posts.Single(b => b.BlogId == 2);
                watch.Stop();
                ReportString($"Elapsed context.Posts.Single(b => b.BlogId == 2) {watch.ElapsedMilliseconds} ms. Result is {post}");

                try
                {
                    watch.Restart();
                    post = context.Posts.Single(b => b.BlogId == 1);
                    watch.Stop();
                    ReportString($"Elapsed context.Posts.Single(b => b.BlogId == 1) {watch.ElapsedMilliseconds} ms. Result is {post}");
                }
                catch
                {
                    if (config.LoadApplicationData) throw; // throw only if the test is loading data otherwise it was removed in a previous run
                }

                watch.Restart();
                var all = context.Posts.All((o) => true);
                watch.Stop();
                ReportString($"Elapsed context.Posts.All((o) => true) {watch.ElapsedMilliseconds} ms. Result is {all}");

                Blog blog = null;
                try
                {
                    watch.Restart();
                    blog = context.Blogs!.Single(b => b.BlogId == 1);
                    watch.Stop();
                    ReportString($"Elapsed context.Blogs!.Single(b => b.BlogId == 1) {watch.ElapsedMilliseconds} ms. Result is {blog}");
                }
                catch
                {
                    if (config.LoadApplicationData) throw; // throw only if the test is loading data otherwise it was removed in a previous run
                }

                if (config.LoadApplicationData)
                {
                    watch.Restart();
                    context.Remove(post);
                    context.Remove(blog);
                    watch.Stop();
                    ReportString($"Elapsed data remove {watch.ElapsedMilliseconds} ms");

                    watch.Restart();
                    context.SaveChanges();
                    watch.Stop();
                    ReportString($"Elapsed SaveChanges {watch.ElapsedMilliseconds} ms");

                    watch.Restart();
                    for (int i = config.NumberOfElements; i < config.NumberOfElements + config.NumberOfExtraElements; i++)
                    {
                        context.Add(new BlogComplex
                        {
                            Url = "http://blogs.msdn.com/adonet" + i.ToString(),
                            BooleanValue = i % 2 == 0,
                            PostComplexs = new List<PostComplex>()
                            {
                                new PostComplex()
                                {
                                    Title = "title",
                                    Content = i.ToString(),
                                    CreationTime = DateTime.Now,
                                    Identifier = Guid.NewGuid()
                                }
                            },
                            Rating = i,
                        });
                    }
                    watch.Stop();
                    ReportString($"Elapsed data load {watch.ElapsedMilliseconds} ms");
                    watch.Restart();
                    context.SaveChanges();
                    watch.Stop();
                    ReportString($"Elapsed SaveChanges {watch.ElapsedMilliseconds} ms");
                }

                watch.Restart();
                post = context.Posts.Single(b => b.BlogId == config.NumberOfElements + (config.NumberOfExtraElements != 0 ? 1 : 0));
                watch.Stop();
                ReportString($"Elapsed context.Posts.Single(b => b.BlogId == config.NumberOfElements + (config.NumberOfExtraElements != 0 ? 1 : 0)) {watch.ElapsedMilliseconds} ms. Result is {post}");

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
    }

    public class BloggingContext : KafkaDbContext
    {
        public override bool UsePersistentStorage { get; set; } = Program.config.UsePersistentStorage;
        public override bool UseCompactedReplicator { get; set; } = Program.config.UseCompactedReplicator;
        public override bool UseKNetStreams { get; set; } = Program.config.UseKNetStreams;

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

    public class BlogComplex : Blog
    {
        public bool BooleanValue { get; set; }

        public List<PostComplex> PostComplexs { get; set; }

        public override string ToString()
        {
            return $"BlogId: {BlogId} Url: {Url} Rating: {Rating} BooleanValue: {BooleanValue}";
        }
    }

    public class PostComplex : Post
    {
        public DateTime CreationTime { get; set; }
        public Guid Identifier { get; set; }

        public override string ToString()
        {
            return $"PostId: {PostId} Title: {Title} Content: {Content} BlogId: {BlogId} CreationTime: {CreationTime} Identifier: {Identifier}";
        }
    }
}
