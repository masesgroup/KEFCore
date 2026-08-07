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
                else
                {
                    ProgramConfig.ReportString("Process ResetStreams");
                    context.ResetStreams();
                    ProgramConfig.ReportString("ResetStreams completed");
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
                    ProgramConfig.ReportResult("DataLoad", watch.Elapsed, details: $"{ProgramConfig.Config.NumberOfElements} elements");
                    watch.Restart();
                    context.SaveChanges();
                    watch.Stop();
                    ProgramConfig.ReportResult("SaveChanges", watch.Elapsed);
                    watch.Restart();
                    var res = context.WaitForSynchronization();
                    watch.Stop();
                    ProgramConfig.ReportResult("LocalStoreSynchronized", watch.Elapsed, success: res.HasValue && res.Value);
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

                // Projection correctness + timing check. BlogId == 10 is never removed later in this file, so
                // it's a safe, stable record to use here and it also survives across "reload" CI runs
                // (LoadApplicationData=false) that read back data written by a previous process invocation.
                // Expected values are derived from blog.Rating itself (== the loop index used at seed time
                // above) rather than hardcoded, so this stays correct even if the seeding loop or
                // NumberOfElements changes. Runs regardless of LoadApplicationData/cache TTL and always
                // throws on mismatch on ANY iteration - unlike the tolerant try/catch above (meant only for
                // "record doesn't exist yet"), a value mismatch here is always a real bug.
                //
                // Looped NumberOfExecutions times (same knob/config already used by Benchmark.Test, and
                // already set to 100 in the shared CI configuration - previously unused here) to get real
                // Max/Min/Mean/Median statistics instead of a single N=1 sample, which is too noisy to
                // distinguish a real regression from run-to-run jitter (see the projection push-down
                // comparison reports). The data load above is intentionally left as a single pass for now -
                // only these read queries are repeated.
                if (blog != null)
                {
                    int seed = blog.Rating;
                    string expectedUrl = "http://blogs.msdn.com/adonet" + seed;
                    char expectedTaxCode = char.ConvertFromUtf32(seed)[0];
                    int expectedCodeExtended = seed * 3;
                    int iterations = Math.Max(1, ProgramConfig.Config.NumberOfExecutions);

                    var scalarOnlyTimes = new TimeSpan[iterations];
                    var nestedTimes = new TimeSpan[iterations];
                    var iterationTotalTimes = new TimeSpan[iterations];
                    string lastScalarOnly = null;
                    string lastNestedDetails = null;

                    for (int i = 0; i < iterations; i++)
                    {
                        watch.Restart();
                        var scalarOnly = context.Blogs!.Where(b => b.BlogId == 10).Select(b => b.Url).Single();
                        watch.Stop();
                        scalarOnlyTimes[i] = watch.Elapsed;
                        if (scalarOnly != expectedUrl)
                            throw new InvalidOperationException($"Projection mismatch: Select(b => b.Url) for BlogId==10 returned '{scalarOnly}', expected '{expectedUrl}' (iteration {i}).");
                        lastScalarOnly = scalarOnly;

                        watch.Restart();
                        var nested = context.Blogs!.Where(b => b.BlogId == 10)
                            .Select(b => new { b.Url, Code = b.PricingInfo.Tax.Code, CodeExtended = b.PricingInfo.Tax.TaxInfoExtended.CodeExtended })
                            .Single();
                        watch.Stop();
                        nestedTimes[i] = watch.Elapsed;
                        if (nested.Url != expectedUrl || nested.Code != expectedTaxCode || nested.CodeExtended != expectedCodeExtended)
                            throw new InvalidOperationException(
                                $"Projection mismatch for BlogId==10: Url='{nested.Url}' (expected '{expectedUrl}'), " +
                                $"Code='{nested.Code}' (expected '{expectedTaxCode}'), CodeExtended={nested.CodeExtended} (expected {expectedCodeExtended}) (iteration {i}).");
                        lastNestedDetails = $"Url='{nested.Url}' Code='{nested.Code}' CodeExtended={nested.CodeExtended}";

                        iterationTotalTimes[i] = scalarOnlyTimes[i] + nestedTimes[i];
                    }

                    ProgramConfig.ReportTimingStats("ScalarOnlyProjection_BlogId10_Url", scalarOnlyTimes, $"Url='{lastScalarOnly}'");
                    ProgramConfig.ReportTimingStats("NestedComplexTypeProjection_BlogId10_TaxCode", nestedTimes, lastNestedDetails);
                    // Sum of the two projection queries above, per iteration - matches Benchmark.Test's own
                    // "IterationTotal" naming (see its TestNames array) so generate_perf_docs.py's
                    // HEADLINE_PATTERN match ("iterationtotal", case-insensitive substring) picks this up too,
                    // giving Complex.Test its own row in the condensed README.md/index.md summary table
                    // instead of only Benchmark.Test appearing there.
                    ProgramConfig.ReportTimingStats("IterationTotal (sum of the two projection queries measured this run)", iterationTotalTimes);
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
            modelBuilder.Entity<BlogComplex>().HasKEFCoreValueBufferCache(
                TimeSpan.FromSeconds(ProgramConfig.Config.ForwardCacheTimeout),
                TimeSpan.FromSeconds(ProgramConfig.Config.ReverseCacheTimeout));

            if (!ProgramConfig.Config.UseModelBuilder) return;

            modelBuilder.Entity<BlogComplex>().HasKey(c => new { c.BlogId, c.Rating });

            base.OnModelCreating(modelBuilder);
        }
    }
}
