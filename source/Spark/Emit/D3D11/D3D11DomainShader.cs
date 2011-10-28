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
    public class D3D11DomainShader : D3D11Stage
    {
        EmitContextHLSL hlslContext = null;
        MidElementDecl constantElement;

        public override void EmitImplSetup()
        {
            constantElement = GetElement("Constant");
            var uniformElement = GetElement("Uniform");
            var fineVertexElement = GetElement("FineVertex");
            var rasterVertexElement = GetElement("RasterVertex");

            InitBlock.AppendComment("D3D11 Domain Shader");

            var tessEnabledAttr = FindAttribute( constantElement, "__D3D11TessellationEnabled" );
            if( tessEnabledAttr  == null )
            {
                return;
            }

            var outputPatchElement = GetElement( "OutputPatch" );
            var controlPointElement = GetElement( "ControlPoint" );

            // \todo: Need to check whether GS is enabled.
            var gsEnabledAttr = FindAttribute( constantElement, "__D3D11GeometryShaderEnabled" );
            var outputElement = fineVertexElement;
            if( gsEnabledAttr == null )
            {
                outputElement = rasterVertexElement;
            }

            hlslContext = new EmitContextHLSL(SharedHLSL, Range, this.EmitClass.GetName());
            var entryPointSpan = hlslContext.EntryPointSpan;


            var outputControlPointCount = GetAttribute(constantElement, "HS_OutputControlPointCount");
            var tsDomain = GetAttribute(constantElement, "TS_Domain");

            // Bind a bunch of attributes that really represent constants:
            hlslContext.BindAttrLit(
                GetAttribute(constantElement, "TriangleDomain"),
                "tri");
            hlslContext.BindAttrLit(
                GetAttribute(constantElement, "QuadDomain"),
                "quad");
            hlslContext.BindAttrLit(
                GetAttribute(constantElement, "FractionalOddPartitioning"),
                "fractional_odd");
            hlslContext.BindAttrLit(
                GetAttribute(constantElement, "IntegerPartitioning"),
                "integer");
            hlslContext.BindAttrLit(
                GetAttribute(constantElement, "TriangleCWTopology"),
                "triangle_cw");


            hlslContext.GenerateConnectorType(controlPointElement);
            hlslContext.GenerateConnectorType(outputElement);

            entryPointSpan.WriteLine("[domain(\"{0}\")]",
                hlslContext.EmitAttrLit( tsDomain ));
            entryPointSpan.WriteLine("{0} main(",
                hlslContext.GenerateConnectorType(outputElement));

            bool first = true;
            if (outputPatchElement.Attributes.Any((a) => a.IsOutput))
            {
                hlslContext.DeclareConnectorAndBind(
                    outputPatchElement,
                    GetAttribute(fineVertexElement, "__op2dv"),
                    ref first,
                    entryPointSpan);
            }

            // \todo: These should not be required, but seem related
            // to an fxc bug where it expects the tess-factor inputs
            // to be re-declared in the DS...
            var edgeFactors = GetAttribute(outputPatchElement, "HS_EdgeFactors");
            var insideFactors = GetAttribute(outputPatchElement, "HS_InsideFactors");

            /*
            hlslContext.DeclareParamAndBind(
                edgeFactors,
                "SV_TessFactor",
                ref first,
                entryPointSpan);

            hlslContext.DeclareParamAndBind(
                insideFactors,
                "SV_InsideTessFactor",
                ref first,
                entryPointSpan);
            */

            hlslContext.DeclareParamAndBind(
                GetAttribute(fineVertexElement, "DS_DomainLocation"),
                "SV_DomainLocation",
                ref first,
                entryPointSpan);


            hlslContext.DeclareParamAndBind(
                GetAttribute(fineVertexElement, "DS_InputControlPoints"),
                null,
                ref first,
                entryPointSpan);
/*

            entryPointSpan.WriteLine(",");
            entryPointSpan.WriteLine("\tconst OutputPatch<{0}, {1}> DS_InputControlPoints",
                hlslContext.GenerateConnectorType(outputControlPointElement),
                hlslContext.EmitAttribRef(
                    outputControlPointCount,
                    null));
            */

            entryPointSpan.WriteLine("\t)");
            entryPointSpan.WriteLine("{");

            if (fineVertexElement != outputElement)
            {
                hlslContext.EmitTempRecordCtor(
                    entryPointSpan,
                    fineVertexElement,
                    GetAttribute(rasterVertexElement, "__f2rhelper"));
            }

            var output = hlslContext.EmitConnectorCtor(
                entryPointSpan,
                outputElement );

            entryPointSpan.WriteLine("\treturn {0};", output);
            entryPointSpan.WriteLine("}");

            hlslContext.EmitConstantBufferDecl();

            EmitShaderSetup(
                hlslContext,
                "ds_5_0",
                "Domain",
                "DS");
        }

        public override void EmitImplBind()
        {
            ExecBlock.AppendComment( "D3D11 Domain Shader" );

            var tessEnabledAttr = FindAttribute( constantElement, "__D3D11TessellationEnabled" );
            if( tessEnabledAttr == null )
            {
                ExecBlock.CallCOM(
                    SubmitContext,
                    "ID3D11DeviceContext",
                    "DSSetShader",
                    GetNullPointer( "ID3D11DomainShader*" ),
                    GetNullPointer( "ID3D11ClassInstance*" ),
                    ExecBlock.LiteralU32( 0 ) );
                return;
            }

            EmitShaderBind(
                hlslContext,
                "ds_5_0",
                "Domain",
                "DS" );
        }
    }
}
