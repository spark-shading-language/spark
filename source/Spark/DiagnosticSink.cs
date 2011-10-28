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

namespace Spark
{
    // For lack of a smarter place right now:

    public enum TokenType
    {
        Comment,
        Frequency,
        Keyword,
        Literal,
    }

    public interface ITagSink
    {
        void Tag(SourceRange range, TokenType tokenType);
    }


    //


    public enum Severity
    {
        Info = 0,
        Warning = 1,
        Error = 2,
        Fatal = 3,
    }

    public interface IDiagnosticsSource
    {
        IEnumerable<Diagnostic> Diagnostics { get; }
    }

    public interface IDiagnosticsSink
    {
        void Add(IDiagnosticsSource source);
    }

    public interface IDiagnosticsCollection : IDiagnosticsSource, IDiagnosticsSink
    {
        void Clear();
    }

    public class Diagnostic : IDiagnosticsSource
    {
        public Diagnostic(
            Severity severity,
            SourceRange range,
            string message )
        {
            _severity = severity;
            _range = range;
            _message = message;
        }

        public Severity Severity { get { return _severity; } }
        public SourceRange Range { get { return _range; } }
        public string Message { get { return _message; } }

        IEnumerable<Diagnostic> IDiagnosticsSource.Diagnostics
        {
            get { yield return this; }
        }

        private Severity _severity;
        private SourceRange _range;
        private string _message;
    }

    public class DiagnosticSink : IDiagnosticsCollection
    {
        public void Add(IDiagnosticsSource source)
        {
            _diagnostics.Add( source );
        }

        public IEnumerable<Diagnostic> Diagnostics
        {
            get
            {
                foreach (var s in _diagnostics)
                    foreach (var d in s.Diagnostics)
                        yield return d;
            }
        }

        public void Clear()
        {
            _diagnostics.Clear();
        }
        
        private List<IDiagnosticsSource> _diagnostics = new List<IDiagnosticsSource>();
    }

    public interface IDiagnosticsWriter
    {
        void Write( string value );
    }

    public static class DiagnosticsExtensions
    {
        public static void Add(
            this IDiagnosticsSink sink,
            Severity severity,
            SourceRange range,
            string message)
        {
            sink.Add(new Diagnostic(severity, range, message));
        }

        public static void Add(
            this IDiagnosticsSink sink,
            Severity severity,
            SourceRange range,
            string format,
            params object[] args)
        {
            sink.Add(new Diagnostic(severity, range, string.Format(format, args)));
        }

        public static IDiagnosticsSink InsertSink(
            this IDiagnosticsSink sink)
        {
            var subSink = new DiagnosticSink();
            sink.Add(subSink);
            return subSink;
        }

        public static int Flush(
            this IDiagnosticsCollection collection,
            System.IO.TextWriter writer)
        {
            int errorCount = collection.Dump(writer);
            collection.Clear();
            return errorCount;
        }

        public static int Dump(
            this IDiagnosticsSource source,
            System.IO.TextWriter writer )
        {
            int errorCount = 0;
            foreach (var d in source.Diagnostics)
            {
                if (d.Severity >= Severity.Error)
                    errorCount++;
                d.Dump(writer);
            }
            return errorCount;
        }

        public static void Dump(
            this Diagnostic diagnostic,
            System.IO.TextWriter writer)
        {
            writer.WriteLine("{0}: {1}: {2}",
                diagnostic.Range,
                diagnostic.Severity,
                diagnostic.Message);
        }

        public static int Dump(
            this IDiagnosticsSource source,
            IDiagnosticsWriter writer )
        {
            int errorCount = 0;
            foreach( var d in source.Diagnostics )
            {
                if( d.Severity >= Severity.Error )
                    errorCount++;
                d.Dump( writer );
            }
            return errorCount;
        }

        public static void Dump(
            this Diagnostic diagnostic,
            IDiagnosticsWriter writer )
        {
            writer.Write( string.Format("{0}: {1}: {2}\n",
                diagnostic.Range,
                diagnostic.Severity,
                diagnostic.Message));
        }
    }
}
