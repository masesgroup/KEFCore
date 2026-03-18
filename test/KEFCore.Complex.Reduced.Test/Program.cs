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
using MASES.EntityFrameworkCore.KNet.Test.Common.Model.ReducedComplex;
using Microsoft.EntityFrameworkCore;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace MASES.EntityFrameworkCore.KNet.Test.ReducedComplex
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

                context.RegisterComplexTypeConverter(typeof(TaxInfoExtendedConverter));

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
                            TaxInfoExtended = new TaxInfoExtended()
                            {
                                CodeExtended = (int)i * 3,
                                PercentageExtended = i / 3,
                                NestedTaxInfoExtended = new NestedTaxInfoExtended()
                                {
                                    CodeExtended = (int)i * 5,
                                    PercentageExtended = i / 5,
                                }
                            },                 
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

                if (!context.ManageEvents)
                {
                    Thread.Sleep(5000);
                }

                BlogComplex blog = null;
                try
                {
                    watch.Restart();
                    blog = context.Blogs!.Single(b => b.BlogId == 10);
                    watch.Stop();
                    var code = blog.TaxInfoExtended.CodeExtended;
                    ProgramConfig.ReportString($"Elapsed context.Blogs!.Single(b => b.BlogId == 1) {watch.ElapsedMilliseconds} ms. Result is {blog}");
                }
                catch
                {
                    if (ProgramConfig.Config.LoadApplicationData) throw; // throw only if the test is loading data otherwise it was removed in a previous run
                }

                try
                {
                    watch.Restart();
                    int count = context.Blogs!.Where(b => b.TaxInfoExtended.PercentageExtended >= 1).Count();
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
                            TaxInfoExtended = new TaxInfoExtended()
                            {
                                CodeExtended = (int)i * 3,
                                PercentageExtended = i / 3
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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            if (!ProgramConfig.Config.UseModelBuilder) return;

            modelBuilder.Entity<BlogComplex>().HasKey(c => new { c.BlogId, c.Rating });

            base.OnModelCreating(modelBuilder);
        }
    }
}
