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

namespace MASES.EntityFrameworkCore.KNet.Test.Common
{
    public class ProgramConfig
    {
        public bool UseProtobuf { get; set; } = false;
        public bool UseAvro { get; set; } = false;
        public bool UseAvroBinary { get; set; } = true;
        public bool EnableKEFCoreTracing { get; set; } = false;
        public bool UseInMemoryProvider { get; set; } = false;
        public bool UseModelBuilder { get; set; } = false;
        public bool UseCompactedReplicator { get; set; } = true;
        public bool UsePersistentStorage { get; set; } = false;
        public string DatabaseName { get; set; } = "TestDB";
        public string DatabaseNameWithModel { get; set; } = "TestDBWithModel";
        public string ApplicationId { get; set; } = "TestApplication";
        public bool DeleteApplicationData { get; set; } = true;
        public bool LoadApplicationData { get; set; } = true;
        public string BootstrapServers { get; set; } = "localhost:9092";
        public string TopicToSubscribe { get; set; }
        public int NumberOfElements { get; set; } = 1000;
        public int NumberOfExecutions { get; set; } = 1;
        public int NumberOfExtraElements { get; set; } = 100;
        public bool WithEvents { get; set; } = false;
    }
}
