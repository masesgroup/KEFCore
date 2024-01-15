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

using MASES.EntityFrameworkCore.KNet.Serialization;
using MASES.EntityFrameworkCore.KNet.Test.Common;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace MASES.EntityFrameworkCore.KNet.Test.Extractor
{
    partial class Program
    {
        internal static CancellationTokenSource runApplication = new CancellationTokenSource();
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
            try
            {
                if (args.Length > 0)
                {
                    if (!File.Exists(args[0])) { ReportString($"{args[0]} is not a configuration file."); return; }
                    config = JsonSerializer.Deserialize<ProgramConfig>(File.ReadAllText(args[0]));
                }

                if (string.IsNullOrWhiteSpace(config.BootstrapServers)) throw new ArgumentException("BootstrapServers must be set");
                if (string.IsNullOrWhiteSpace(config.TopicToSubscribe)) throw new ArgumentException("TopicToSubscribe must be set");

                KEFCore.CreateGlobalInstance();
                Console.CancelKeyPress += Console_CancelKeyPress;
                EntityExtractor.FromTopic(config.BootstrapServers, config.TopicToSubscribe, ReportData, runApplication.Token);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        static void ReportData(object entity, Exception exception)
        {
            if (exception != null) { Console.Error.WriteLine(exception.Message); }
            if (entity != null) { Console.Out.WriteLine(entity.ToString()); }
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            runApplication.Cancel();
        }
    }
}
