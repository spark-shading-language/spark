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

using Spark.Emit.HLSL;
using Spark.Mid;

namespace Spark.Emit.D3D11
{
    public class D3D11VertexShader : D3D11Stage
    {
        EmitContextHLSL hlslContext;

        public override void EmitImplSetup()
        {
            var uniformElement = GetElement("Uniform");
            var vertexElement = GetElement("CoarseVertex");
            var assembledVertex = GetElement("AssembledVertex");

            var constantElement = GetElement("Constant");
            var tessEnabledAttr = FindAttribute( constantElement, "__D3D11TessellationEnabled" );
            var gsEnabledAttr = FindAttribute( constantElement, "__D3D11GeometryShaderEnabled" );

            // \todo: We eventually need to support both tessellation enabled/disabled...

            var fineVertexElement = GetElement("FineVertex");
            var rasterVertexElement = GetElement("RasterVertex");

            var outputElement = vertexElement;
            if( tessEnabledAttr == null )
            {
                if( gsEnabledAttr == null )
                {
                    outputElement = rasterVertexElement;
                }
                else
                {
                    outputElement = fineVertexElement;
                }
            }


            InitBlock.AppendComment("D3D11 Vertex Shader");

            var outputAttributes = new List<MidAttributeDecl>();
            foreach (var a in outputElement.Attributes)
            {
                if (a.IsOutput) outputAttributes.Add(a);
            }

            hlslContext = new EmitContextHLSL(SharedHLSL, Range, this.EmitClass.GetName());

            var entryPointSpan = hlslContext.EntryPointSpan;

            entryPointSpan.WriteLine("{0} main(",
                hlslContext.GenerateConnectorType(outputElement));
            
            bool first = true;

            hlslContext.DeclareConnectorAndBind(
                assembledVertex,
                GetAttribute(vertexElement, "__ia2vs"),
                ref first,
                entryPointSpan);

            hlslContext.DeclareParamAndBind(
                GetAttribute(vertexElement, "VS_VertexID"),
                "SV_VertexID",
                ref first,
                entryPointSpan);
            hlslContext.DeclareParamAndBind(
                GetAttribute(vertexElement, "VS_InstanceID"),
                "SV_InstanceID",
                ref first,
                entryPointSpan);

            entryPointSpan.WriteLine("\t)");
            entryPointSpan.WriteLine("{");

            if( tessEnabledAttr == null )
            {
                hlslContext.EmitTempRecordCtor(
                    entryPointSpan,
                    vertexElement,
                    GetAttribute(fineVertexElement, "__c2fhelper"));

                if( gsEnabledAttr == null )
                {
                    hlslContext.EmitTempRecordCtor(
                        entryPointSpan,
                        fineVertexElement,
                        GetAttribute(rasterVertexElement, "__f2rhelper"));
                }
            }

            var resultVal = hlslContext.EmitConnectorCtor(
                entryPointSpan,
                outputElement);
            entryPointSpan.WriteLine("\treturn {0};", resultVal);
            entryPointSpan.WriteLine("}");

            hlslContext.EmitConstantBufferDecl();

            //

            EmitShaderSetup(
                hlslContext,
                "vs_5_0",
                "Vertex",
                "VS");
        }

        public override void EmitImplBind()
        {
            ExecBlock.AppendComment( "D3D11 Vertex Shader" );

            EmitShaderBind(
                hlslContext,
                "vs_5_0",
                "Vertex",
                "VS" );
        }
    }
}
