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

using Spark.Emit;
using Spark.Resolve; // For Separate()

namespace Spark.Mid
{
    public static class MidDump
    {
        public static void Dump(
            this MidModuleDecl module,
            System.IO.TextWriter writer)
        {
            var span = new Span();
            module.Dump( span );
            span.Dump( writer );
        }

        public static void Dump(
            this MidModuleDecl module,
            Span span )
        {
            span.WriteLine("module {");
            var inner = span.IndentSpan();
            foreach (var p in module.Pipelines)
                p.Dump(inner);
            span.WriteLine("}");
        }

        public static void Dump(
            this MidPipelineDecl pipeline,
            Span span)
        {
            span.WriteLine("pipeline {0} {{", pipeline.Name);
            var inner = span.IndentSpan();
            foreach (var e in pipeline.Elements)
                e.Dump(inner);
            span.WriteLine("}");
        }

        public static void Dump(
            this MidElementDecl element,
                Span span )
        {
            span.WriteLine("element {0} {{", element.Name);
            var inner = span.IndentSpan();
            foreach (var a in element.Attributes)
                a.Dump(inner);
            span.WriteLine("}");
        }

        public static void Dump(
            this MidAttributeDecl attribute,
                Span span)
        {
            if (attribute.IsOutput)
                span.Write("output ");
            span.Write("{0} {1}",
                attribute.Type.Dump(),
                attribute.Name);
            if (attribute.Exp != null)
            {
                span.Write(" = ");
                attribute.Exp.Dump(span.IndentSpan());
            }
            span.WriteLine(";");
        }

        public static ISpan Dump(
            this MidType type )
        {
            var span = new Span();
            type.Dump(span);
            return span;
        }

        public static void Dump(
            this MidType type,
            Span span)
        {
            DumpTypeImpl((dynamic)type, span);
        }

        private static void DumpTypeImpl(
            MidBuiltinType builtin,
            Span span)
        {
            span.Write("{0}", builtin.Name);
            if (builtin.Args.Length != 0)
            {
                span.Write("[");
                span.Add(from a in builtin.Args select ((MidType) a).Dump(), ", ");
                span.Write("]");
            }
        }

        private static void DumpTypeImpl(
            MidStructRef structRef,
            Span span)
        {
            span.Write("{0}", structRef.Decl.Name);
        }

        private static void DumpTypeImpl(
            MidElementType elemType,
            Span span)
        {
            span.Write("{0}", elemType.Decl.Name);
        }

        public static ISpan Dump(
            this MidExp exp)
        {
            var span = new Span();
            exp.Dump(span);
            return span;
        }

        public static void Dump(
            this MidExp exp,
            Span span)
        {
            DumpExpImpl((dynamic)exp, span);
        }

        private static void DumpExpImpl<T>(
            MidLit<T> lit,
            Span span)
        {
            span.Write("{0}", lit.Value);
        }

        private static void DumpExpImpl(
            MidAttributeRef attrRef,
            Span span)
        {
            span.Write("{0}", attrRef.Decl.Name);
        }

        private static void DumpExpImpl(
            MidAttributeFetch attrFetch,
            Span span)
        {
            span.Write("{0}.{1}", attrFetch.Obj.Dump(), attrFetch.Attribute.Name);
        }

        private static void DumpExpImpl(
            MidFieldRef fieldRef,
            Span span)
        {
            span.Write("{0}.{1}", fieldRef.Obj.Dump(), fieldRef.Decl.Name);
        }

        private static void DumpExpImpl(
            MidVarRef varRef,
            Span span)
        {
            span.Write("{0}", varRef.Var.Name);
        }

        private static void DumpExpImpl(
            MidBuiltinApp app,
            Span span)
        {
            span.Write("{0}(", app.Decl.Name);
            span.Add(from a in app.Args select a.Dump(), ", ");
            span.Write(")");
        }

        private static void DumpExpImpl(
            MidMethodApp app,
            Span span)
        {
            span.Write("{0}(", app.MethodDecl.Name);
            span.Add(from a in app.Args select a.Dump(), ", ");
            span.Write(")");
        }

        private static void DumpExpImpl(
            MidLetExp let,
            Span span)
        {
            span.Write("let {0} = ", let.Var.Name);
            let.Exp.Dump(span.IndentSpan());
            span.WriteLine(";");
            let.Body.Dump(span);
        }

    }
}
