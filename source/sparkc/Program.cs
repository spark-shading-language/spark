// Copyright 2011 Intel Corporation
// All Rights Reserved
//
// Permission is granted to use, copy, distribute and prepare derivative works of this
// software for any purpose and without fee, provided, that the above copyright notice
// and this statement appear in all copies.  Intel makes no representations about the
// suitability of this software for any purpose.  THIS SOFTWARE IS PROVIDED "AS IS."
// INTEL SPECIFICALLY DISCLAIMS ALL WARRANTIES, EXPRESS OR IMPLIED, AND ALL LIABILITY,
// INCLUDING CONSEQUENTIAL AND OTHER INDIRECT DAMAGES, FOR THE USE OF THIS SOFTWARE,
// INCLUDING LIABILITY FOR INFRINGEMENT OF ANY PROPRIETARY RIGHTS, AND INCLUDING THE
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE.  Intel does not
// assume any responsibility for any errors which may appear in this software nor any
// responsibility to update it.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Spark;

namespace sparkc
{
    class Program
    {
        class Options
        {
            public static Options Parse(string[] args)
            {
                var diagnostics = new DiagnosticSink();
                var range = new SourceRange("<command line>");

                Options result = new Options();

                int argCount = args.Length;
                int argIdx = 0;

                while (argIdx < argCount)
                {
                    var argStr = args[argIdx++];
                    if (argStr.StartsWith("-"))
                    {
                        if (argStr.StartsWith("-o"))
                        {
                            var option = argStr.Substring(2);
                            if( string.IsNullOrWhiteSpace(option) )
                            {
                                option = args[argIdx++];
                            }

                            result.outputPrefix = option;
                        }
                        else
                        {
                            diagnostics.Add(
                                Severity.Error,
                                range,
                                "Unknown option '{0}'",
                                argStr);
                        }
                    }
                    else
                    {
                        result.fileNames.Add(argStr);
                    }
                }

                int fileCount = result.fileNames.Count;
                if (fileCount == 1)
                {
                    if (result.outputPrefix == null)
                    {
                        result.outputPrefix = result.fileNames[0];
                    }
                }
                else if (fileCount == 0)
                {
                    diagnostics.Add(
                        Severity.Error,
                        range,
                        "No input files given");
                }
                else
                {
                    if (result.outputPrefix == null)
                    {
                        diagnostics.Add(
                            Severity.Error,
                            range,
                            "When multiple input files are given, an output prefix must be selected with -o");
                    }
                }

                int errorCount = diagnostics.Flush(System.Console.Error);
                if (errorCount != 0)
                {
                    System.Console.Error.WriteLine(
                        "Usage: sparkc [-o outputPrefix] file.spark file2.spark");
                    return null;
                }

                return result;
            }

            public string outputPrefix = null;
            public List<string> fileNames = new List<string>();
        }


        static void Main(string[] args)
        {
            try
            {
                var options = Options.Parse(args);
                if (options == null)
                    return;

                var prefix = options.outputPrefix;

                var compiler = new Spark.Compiler.Compiler
                {
                    OutputPrefix = prefix,
                };
                foreach( var fileName in options.fileNames )
                    compiler.AddInput(fileName);

                int result = compiler.Compile();
            }
            catch (StackOverflowException e)
            {
                System.Console.Error.WriteLine("Exception: {0}", e);
            }

        }
    }
}
