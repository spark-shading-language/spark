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

using Spark.AbstractSyntax;
using Spark.Emit;
using Spark.Emit.CPlusPlus;

namespace Spark.Compiler
{
    public class Compiler
    {
        private IdentifierFactory _identifiers;
        private IDiagnosticsCollection _diagnostics;
        private string _outputPrefix = "output";

        private IList<AbsSourceRecord> _absSourceRecords;
        private ResolvedSyntax.IResModuleDecl _resModule;
        private Mid.MidModuleDecl _midModule;

        public IdentifierFactory Identifiers
        {
            get
            {
                if (_identifiers == null)
                    _identifiers = new IdentifierFactory();
                return _identifiers;
            }
            set { _identifiers = value; }
        }

        public IDiagnosticsCollection Diagnostics
        {
            get
            {
                if (_diagnostics == null)
                    _diagnostics = new DiagnosticSink();
                return _diagnostics;
            }
            set { _diagnostics = value; }
        }

        public string OutputPrefix
        {
            get { return _outputPrefix; }
            set { _outputPrefix = value; }
        }

        public IEnumerable<AbsSourceRecord> AbsSourceRecords
        {
            get { return _absSourceRecords; }
        }

        public ResolvedSyntax.IResModuleDecl ResModule
        {
            get { return _resModule; }
        }

        public Mid.MidModuleDecl MidModule
        {
            get { return _midModule; }
        }

        public Compiler()
        {
        }

        public void AddReference(
            System.Reflection.Assembly assembly )
        {
            _assemblies.Add( assembly );
        }

        public void AddInput( String input )
        {
            _inputs.Add( input );
        }

        public int Parse()
        {
            _absSourceRecords = new List<AbsSourceRecord>();

            // Parse the "standard library."
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            using (var stdlib = assembly.GetManifestResourceStream("Spark.stdlib.spark"))
            {
                ParseStream(stdlib, "Standard Library");
            }

            // Parse the user code.
            foreach (var input in _inputs)
            {
                var stream = new System.IO.FileStream(
                    input,
                    System.IO.FileMode.Open,
                    System.IO.FileAccess.Read);

                using (stream)
                {
                    ParseStream(stream, input);
                }
            }

            return Diagnostics.Flush(System.Console.Error);
        }

        public int Resolve()
        {
            var resContext = new Resolve.ResolveContext(
                Identifiers,
                Diagnostics);
            _resModule = resContext.Resolve(_absSourceRecords);
            return Diagnostics.Flush(System.Console.Error);
        }

        public int Lower()
        {
            return Lower( new Mid.MidEmitContext( Identifiers ) );
        }

        public int Lower( Mid.MidEmitContext midContext )
        {
            _midModule = midContext.EmitModule( _resModule );
            return 0;
        }

        public int Compile()
        {
            int errorCount = 0;

            errorCount += Parse();
            if (errorCount != 0)
                return errorCount;

            errorCount += Resolve();
            if (errorCount != 0)
                return errorCount;

            errorCount += Lower();
            if (errorCount != 0)
                return errorCount;
            

            var outputHeaderName = string.Format("{0}.h", OutputPrefix);
            var outputSourceName = string.Format("{0}.cpp", OutputPrefix);

            var emitTarget = new EmitTargetCPP();
            var emitContext = new EmitContext {
                OutputName = OutputPrefix,
                Target = emitTarget,
                Identifiers = Identifiers,
                Diagnostics = Diagnostics, };

            var emitModule = (EmitModuleCPP) emitContext.EmitModule(_midModule);

            errorCount += Diagnostics.Flush(System.Console.Error);
            if( errorCount != 0 )
                return errorCount;

            using (var headerWriter = new System.IO.StreamWriter(
                outputHeaderName, false, Encoding.ASCII))
            {
                emitModule.HeaderSpan.Dump(headerWriter);
            }
            using (var sourceWriter = new System.IO.StreamWriter(
                outputSourceName, false, Encoding.ASCII))
            {
                emitModule.SourceSpan.Dump(sourceWriter);
            }

            errorCount += Diagnostics.Flush(System.Console.Error);
            return errorCount;
        }

        private void ParseStream(
            System.IO.Stream stream,
            string name)
        {
            var scanner = new Spark.Parser.Generated.Scanner(
                    stream,
                    Diagnostics,
                    Identifiers,
                    name);
            var parser = new Spark.Parser.Generated.Parser(scanner);

            if (parser.Parse())
                _absSourceRecords.Add(parser.result);
        }

        private List<System.Reflection.Assembly> _assemblies = new List<System.Reflection.Assembly>();
        private List<String> _inputs = new List<String>();
    }
}
