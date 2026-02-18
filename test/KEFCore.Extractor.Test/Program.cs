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

using MASES.EntityFrameworkCore.KNet.Serialization;
using MASES.EntityFrameworkCore.KNet.Test.Common;
using System;
using System.Threading;

namespace MASES.EntityFrameworkCore.KNet.Test.Extractor
{
    partial class Program
    {
        internal static CancellationTokenSource runApplication = new CancellationTokenSource();

        static void Main(string[] args)
        {
            try
            {
                ProgramConfig.LoadConfig(args);

                if (string.IsNullOrWhiteSpace(ProgramConfig.Config.BootstrapServers)) throw new ArgumentException("BootstrapServers must be set");
                if (string.IsNullOrWhiteSpace(ProgramConfig.Config.TopicToSubscribe)) throw new ArgumentException("TopicToSubscribe must be set");

                KEFCore.CreateGlobalInstance();
                Console.CancelKeyPress += Console_CancelKeyPress;
                EntityExtractor.FromTopic(ProgramConfig.Config.BootstrapServers, ProgramConfig.Config.TopicToSubscribe, ReportData, runApplication.Token);
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
