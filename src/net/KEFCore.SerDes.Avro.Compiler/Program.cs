/*
 *  MIT License
 *
 *  Copyright (c) 2022 - 2025 MASES s.r.l.
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

using System.Diagnostics;

namespace MASES.EntityFrameworkCore.KNet.Serialization.Avro.Compiler;

partial class Program
{
    static void ReportString(string message)
    {
        if (Debugger.IsAttached)
        {
            Trace.WriteLine($"{DateTime.Now:HH::mm::ss:ffff} - {message}");
        }
        else
        {
            Console.WriteLine($"{DateTime.Now:HH::mm::ss:ffff} - {message}");
        }
    }

    static void Main(string[] args)
    {
        try
        {
            var outFolder = "Generated";
            if (args.Length != 0 && Directory.Exists( args[0])) outFolder = args[0];
            AvroSerializationHelper.BuildDefaultSchema(outFolder);
        }
        catch (Exception ex)
        {
            ReportString(ex.ToString());
        }
    }
}
