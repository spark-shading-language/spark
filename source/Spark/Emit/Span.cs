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

namespace Spark.Emit
{
    public interface ISpan
    {
        void Dump(System.IO.TextWriter writer, string prefix, ref bool newLine);
    }

    public class Span : ISpan
    {
        public override string ToString()
        {
            var writer = new System.IO.StringWriter();
            this.Dump(writer);
            return writer.ToString();
        }

        public void Write(string value)
        {
            GetBuilder().Append(value);
        }

        public void Write(string format, params object[] args)
        {
            GetBuilder().Append(string.Format(format, args));
        }

        public void WriteLine()
        {
            GetBuilder().Append("");
            FlushLine();
        }

        public void WriteLine(string value)
        {
            GetBuilder().Append(value);
            FlushLine();
        }

        public void WriteLine(string format, params object[] args)
        {
            GetBuilder().Append(string.Format(format, args));
            FlushLine();
        }

        public void Add(ISpan span)
        {
            FlushBuilder();

            _content.Add(span);
        }

        public void Add(object content)
        {
            if (content is ISpan)
                Add((ISpan)content);
            else
                Write("{0}", content);
        }

        public void Add(IEnumerable<object> content)
        {
            foreach (var c in content)
                Add(c);
        }

        public void Add(IEnumerable<object> content, object separator)
        {
            bool first = true;
            foreach(var c in content)
            {
                if(!first) Add(separator);
                first = false;
                Add(c);
            }
        }

        public Span InsertSpan()
        {
            FlushBuilder();

            var subSpan = new Span();
            _content.Add(subSpan);
            return subSpan;
        }

        public Span IndentSpan()
        {
            return InsertPrefixSpan("\t");
        }

        public Span InsertPrefixSpan(string prefix)
        {
            FlushBuilder();

            var subSpan = new Span();
            _content.Add(new PrefixSpan(subSpan, prefix));
            return subSpan;
        }

        private StringBuilder GetBuilder()
        {
            if (_builder == null)
                _builder = new StringBuilder();
            return _builder;
        }

        private void FlushBuilder()
        {
            if (_builder != null)
            {
                _content.Add(_builder.ToString());
                _builder = null;
            }
        }

        private void FlushLine()
        {
            FlushBuilder();
            _content.Add(_newLineTag);
        }

        public void Dump(System.IO.TextWriter writer, string prefix, ref bool newLine)
        {
            FlushBuilder();

            foreach (var c in _content)
            {
                if (c == _newLineTag)
                {
                    writer.WriteLine();
                    newLine = true;
                }
                else if (c is ISpan)
                {
                    ((ISpan)c).Dump(writer, prefix, ref newLine);
                }
                else
                {
                    if (newLine)
                    {
                        writer.Write(prefix);
                        newLine = false;
                    }
                    writer.Write(c);
                }
            }
        }

        private StringBuilder _builder;
        private List<object> _content = new List<object>();

        private object _newLineTag = new object();
    }

    public class PrefixSpan : ISpan
    {
        public PrefixSpan(
            ISpan inner,
            string prefix )
        {
            _inner = inner;
            _prefix = prefix;
        }

        public void Dump(System.IO.TextWriter writer, string prefix, ref bool newLine)
        {
            _inner.Dump(writer, prefix + _prefix, ref newLine);
        }

        private ISpan _inner;
        private string _prefix;
    }

    public static class SpanMethods
    {
        public static void Dump(
            this ISpan span,
            System.IO.TextWriter writer)
        {
            var newLine = true;
            span.Dump(writer, "", ref newLine);
        }

        public static ISpan Prefix(
            this ISpan span,
            string prefix)
        {
            return new PrefixSpan(span, prefix);
        }
    }
}
