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

namespace Spark.Mid
{
    public class MidMarkOutputs
    {
        public static void MarkOutputs(MidModuleDecl module)
        {
            foreach (var p in module.Pipelines)
                MarkOutputs(p);
        }

        public static void MarkOutputs(MidPipelineDecl pipeline)
        {
            foreach (var e in pipeline.Elements)
                MarkOutputs(e);
        }

        public static void MarkOutputs(MidElementDecl element)
        {
            foreach (var a in element.Attributes)
                MarkOutputs(a);
        }

        public static void MarkOutputs(MidAttributeDecl attribute)
        {
            if (attribute.Exp != null)
                MarkOutputs(attribute.Exp);
        }

        public static void MarkOutputs(MidExp exp)
        {
            MidTransform transform = new MidTransform(
                (e) =>
                {
                    if (e is MidAttributeFetch)
                        ((MidAttributeFetch)e).Attribute.IsOutput = true;
                    return e;
                });

            transform.Transform(exp);
        }

        public static void UnmarkOutputs( MidModuleDecl module )
        {
            foreach( var p in module.Pipelines )
                UnmarkOutputs( p );
        }

        public static void UnmarkOutputs( MidPipelineDecl pipeline )
        {
            foreach( var e in pipeline.Elements )
                UnmarkOutputs( e );
        }

        public static void UnmarkOutputs( MidElementDecl element )
        {
            foreach( var a in element.Attributes )
                UnmarkOutputs( a );
        }

        public static void UnmarkOutputs( MidAttributeDecl attribute )
        {
            attribute.IsOutput = false;
        }
    }
}
