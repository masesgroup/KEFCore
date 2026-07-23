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

using MASES.EntityFrameworkCore.KNet.Extensions;
using MASES.EntityFrameworkCore.KNet.Test.Common;
using MASES.EntityFrameworkCore.KNet.Test.Common.Model.Complex;
using Microsoft.EntityFrameworkCore;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

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
                }
                Stopwatch watch = new Stopwatch();
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
                                    Percentage = i / 2,
                                    TaxInfoExtended = new TaxInfoExtended()
                                    {
                                        CodeExtended = (int)i * 3,
                                        PercentageExtended = i / 3
                                    },
                                    TaxInfoExtended2 = new TaxInfoExtended()
                                    {
                                        CodeExtended = (int)i * 5,
                                        PercentageExtended = i / 5
                                    }
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

                if (ProgramConfig.Config.UseModelBuilder)
                {
                    watch.Restart();
                    var selector = (from op in context.Blogs
                                    join pg in context.Posts on op.BlogId equals pg.BlogId
                                    where pg.BlogId == op.BlogId
                                    select new { pg, op });
                    var pageObject = selector.SingleOrDefault();
                    watch.Stop();
                    if (ProgramConfig.Config.EnableIntermediateOutput) ProgramConfig.ReportString($"Elapsed UseModelBuilder {watch.ElapsedMilliseconds} ms");
                }

                BlogComplex blog = null;
                try
                {
                    watch.Restart();
                    blog = context.Blogs!.Single(b => b.BlogId == 10);
                    watch.Stop();
                    if (ProgramConfig.Config.EnableIntermediateOutput) ProgramConfig.ReportString($"Elapsed context.Blogs!.Single(b => b.BlogId == 1) {watch.ElapsedMilliseconds} ms. Result is {blog}");
                }
                catch
                {
                    if (ProgramConfig.Config.LoadApplicationData) throw; // throw only if the test is loading data otherwise it was removed in a previous run
                }

                // Projection correctness check. BlogId == 10 is never removed later in this file, so it's a safe,
                // stable record to use here and it also survives across "reload" CI runs (LoadApplicationData=false)
                // that read back data written by a previous process invocation. Expected values are derived from
                // blog.Rating itself (== the loop index used at seed time above, e.g. Url = "..." + i, Tax.Code =
                // char.ConvertFromUtf32(i)[0], TaxInfoExtended.CodeExtended = i * 3) rather than hardcoded, so this
                // stays correct even if the seeding loop or NumberOfElements changes. Runs regardless of
                // LoadApplicationData/cache TTL and always throws on mismatch — unlike the tolerant try/catch above
                // (meant only for "record doesn't exist yet"), a value mismatch here is always a real bug.
                if (blog != null)
                {
                    int seed = blog.Rating;
                    string expectedUrl = "http://blogs.msdn.com/adonet" + seed;
                    char expectedTaxCode = char.ConvertFromUtf32(seed)[0];
                    int expectedCodeExtended = seed * 3;

                    // Scalar-only projection: no property of PricingInfo (a complex type spanning Tax/
                    // TaxInfoExtended/TaxInfoExtended2/Discounts) is touched, so the projection push-down should
                    // skip its deserialization entirely. This checks the externally observable result — it doesn't
                    // directly prove deserialization was skipped, but an indexing/skip-logic bug in that code path
                    // would surface here as a wrong value or a KeyNotFoundException/IndexOutOfRangeException.
                    watch.Restart();
                    var scalarOnly = context.Blogs!.Where(b => b.BlogId == 10).Select(b => b.Url).Single();
                    watch.Stop();
                    if (scalarOnly != expectedUrl)
                        throw new InvalidOperationException($"Projection mismatch: Select(b => b.Url) for BlogId==10 returned '{scalarOnly}', expected '{expectedUrl}'.");
                    if (ProgramConfig.Config.EnableIntermediateOutput) ProgramConfig.ReportString($"Elapsed scalar-only projection {watch.ElapsedMilliseconds} ms. Verified Url == '{scalarOnly}'.");

                    // Nested projection: touches only ONE sub-property (Tax.Code) of a multi-level complex type,
                    // plus a sub-property nested one level deeper (Tax.TaxInfoExtended.CodeExtended). This is the
                    // critical regression case for "include the whole complex property if any sub-property is
                    // requested" — getting this wrong throws KeyNotFoundException inside FillFlattened instead of
                    // producing a wrong value.
                    watch.Restart();
                    var nested = context.Blogs!.Where(b => b.BlogId == 10)
                        .Select(b => new { b.Url, Code = b.PricingInfo.Tax.Code, CodeExtended = b.PricingInfo.Tax.TaxInfoExtended.CodeExtended })
                        .Single();
                    watch.Stop();
                    if (nested.Url != expectedUrl || nested.Code != expectedTaxCode || nested.CodeExtended != expectedCodeExtended)
                        throw new InvalidOperationException(
                            $"Projection mismatch for BlogId==10: Url='{nested.Url}' (expected '{expectedUrl}'), " +
                            $"Code='{nested.Code}' (expected '{expectedTaxCode}'), CodeExtended={nested.CodeExtended} (expected {expectedCodeExtended}).");
                    if (ProgramConfig.Config.EnableIntermediateOutput) ProgramConfig.ReportString($"Elapsed nested complex-type projection {watch.ElapsedMilliseconds} ms. Verified Url/Code/CodeExtended.");
                }

                watch.Restart();
                var post = context.Posts.Single(b => b.BlogId == 2);
                watch.Stop();
                if (ProgramConfig.Config.EnableIntermediateOutput) ProgramConfig.ReportString($"Elapsed context.Posts.Single(b => b.BlogId == 2) {watch.ElapsedMilliseconds} ms. Result is {post}");

                try
                {
                    watch.Restart();
                    post = context.Posts.Single(b => b.BlogId == 100);
                    watch.Stop();
                    if (ProgramConfig.Config.EnableIntermediateOutput) ProgramConfig.ReportString($"Elapsed context.Posts.Single(b => b.BlogId == 100) {watch.ElapsedMilliseconds} ms. Result is {post}");
                }
                catch
                {
                    if (ProgramConfig.Config.LoadApplicationData) throw; // throw only if the test is loading data otherwise it was removed in a previous run
                }

                watch.Restart();
                var all = context.Posts.All(static (o) => true);
                watch.Stop();
                if (ProgramConfig.Config.EnableIntermediateOutput) ProgramConfig.ReportString($"Elapsed context.Posts.All((o) => true) {watch.ElapsedMilliseconds} ms. Result is {all}");

                try
                {
                    watch.Restart();
                    blog = context.Blogs!.Single(b => b.BlogId == 1);
                    watch.Stop();
                    var code = blog.PricingInfo.Tax.TaxInfoExtended.CodeExtended;
                    if (ProgramConfig.Config.EnableIntermediateOutput) ProgramConfig.ReportString($"Elapsed context.Blogs!.Single(b => b.BlogId == 1) {watch.ElapsedMilliseconds} ms. Result is {blog}");
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
                            PricingInfo = new Pricing()
                            {
                                Tax = new TaxInfo()
                                {
                                    TaxInfoExtended = new TaxInfoExtended()
                                    {
                                        CodeExtended = (int)i * 3,
                                        PercentageExtended = i / 3
                                    },
                                    TaxInfoExtended2 = new TaxInfoExtended()
                                    {
                                        CodeExtended = (int)i * 5,
                                        PercentageExtended = i / 5
                                    }
                                }
                            },
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
                    if (ProgramConfig.Config.EnableIntermediateOutput) ProgramConfig.ReportString($"Elapsed context.Blogs!.Single(b => b.BlogId == 101) {watch.ElapsedMilliseconds} ms. Result is {blog}");
                }
                catch
                {
                    if (ProgramConfig.Config.LoadApplicationData) throw; // throw only if the test is loading data otherwise it was removed in a previous run
                }
                if (ProgramConfig.Config.LoadApplicationData)
                {
                    watch.Restart();
                    var res = context.WaitForSynchronization();
                    watch.Stop();
                    if (res.HasValue && res.Value)
                    {
                        ProgramConfig.ReportString($"Local store synchronized in {watch.ElapsedMilliseconds} ms.");
                        watch.Restart();
                        post = context.Posts.Single(b => b.BlogId == ProgramConfig.Config.NumberOfElements + ProgramConfig.Config.NumberOfExtraElements - 1);
                        watch.Stop();
                        if (ProgramConfig.Config.EnableIntermediateOutput) ProgramConfig.ReportString($"Elapsed context.Posts.Single(b => b.BlogId == config.NumberOfElements + (config.NumberOfExtraElements != 0 ? 1 : 0)) {watch.ElapsedMilliseconds} ms. Result is {post}");
                    }
                    else
                    {
                        ProgramConfig.ReportString($"Local store is not synchronized. Test skipped.");
                    }
                }
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

    public class BloggingContext : TestContext
    {
        public DbSet<BlogComplex> Blogs { get; set; }
        public DbSet<PostComplex> Posts { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Activates the per-entity ValueBuffer cache using the same /p:ForwardCacheTimeout /p:ReverseCacheTimeout
            // CLI overrides already wired into ProgramConfig and already passed to this exact test executable by the
            // CI matrix (use_cache: [true, false] in build_common.yaml) — previously a no-op here since nothing
            // consumed them. Placed before the UseModelBuilder early-return below so it applies in both configurations.
            // Default is -1 seconds (cache disabled, TimeSpan negative) when the CI leg doesn't override it; see
            // KEFCoreCachedValueBufferStore.IsEnabled (TTL > TimeSpan.Zero) for the exact enabled/disabled semantics.
            modelBuilder.Entity<BlogComplex>().HasKEFCoreValueBufferCache(
                TimeSpan.FromSeconds(ProgramConfig.Config.ForwardCacheTimeout),
                TimeSpan.FromSeconds(ProgramConfig.Config.ReverseCacheTimeout));

            if (!ProgramConfig.Config.UseModelBuilder) return;

            modelBuilder.Entity<BlogComplex>().HasKey(c => new { c.BlogId, c.Rating });

            base.OnModelCreating(modelBuilder);
        }
    }
}
