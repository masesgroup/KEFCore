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

using Java.Util;
using MASES.EntityFrameworkCore.KNet.Serialization;
using MASES.JCOBridge.C2JBridge;
using MASES.KNet.Consumer;
using Org.Apache.Kafka.Clients.Consumer;
using Org.Apache.Kafka.Common.Serialization;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace MASES.EntityFrameworkCore.KNet.Test
{
    partial class Program
    {
        internal static bool runApplication = true;
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
            if (args.Length > 0)
            {
                config = JsonSerializer.Deserialize<ProgramConfig>(File.ReadAllText(args[0]));
            }

            if (string.IsNullOrWhiteSpace(config.TopicToSubscribe)) throw new ArgumentException("TopicToSubscribe must be set");

            KEFCore.CreateGlobalInstance();

            try
            {
                Console.CancelKeyPress += Console_CancelKeyPress;
                ConsumerConfigBuilder consumerBuilder = ConsumerConfigBuilder.Create()
                                                                             .WithBootstrapServers(config.BootstrapServers)
                                                                             .WithGroupId(Guid.NewGuid().ToString())
                                                                             .WithAutoOffsetReset(ConsumerConfigBuilder.AutoOffsetResetTypes.EARLIEST)
                                                                             .WithKeyDeserializerClass(JVMBridgeBase.ClassNameOf<ByteArrayDeserializer>())
                                                                             .WithValueDeserializerClass(JVMBridgeBase.ClassNameOf<ByteArrayDeserializer>());

                KafkaConsumer<byte[], byte[]> kafkaConsumer = new KafkaConsumer<byte[], byte[]>(consumerBuilder);
                using var collection = Collections.Singleton(config.TopicToSubscribe);
                kafkaConsumer.Subscribe(collection);

                while (runApplication)
                {
                    var records = kafkaConsumer.Poll(100);
                    foreach (var record in records)
                    {
                        var entity = EntityExtractor.FromRecord(record);
                        Console.WriteLine(entity.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            runApplication = false;
        }
    }
}
