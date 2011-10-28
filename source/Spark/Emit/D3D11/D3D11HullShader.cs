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
    public class D3D11HullShader : D3D11Stage
    {
        EmitContextHLSL hlslContext;

        public override void EmitImplSetup()
        {
            var constantElement = GetElement("Constant");
            var uniformElement = GetElement("Uniform");
            var coarseVertexElement = GetElement("CoarseVertex");

            InitBlock.AppendComment("D3D11 Hull Shader");

            var tessEnabledAttr = FindAttribute( constantElement, "__D3D11TessellationEnabled" );
            if( tessEnabledAttr == null )
            {
                return;
            }

            var superPatchElement = GetElement( "__InputPatch" );
            var inputPatchElement = GetElement( "InputPatch" );
            var outputPatchElement = GetElement( "OutputPatch" );
            var controlPointElement = GetElement( "ControlPoint" );

            var patchEdgeElement = GetElement( "PatchEdge" );
            var patchCornerElement = GetElement( "PatchCorner" );
            var patchInteriorElement = GetElement( "PatchInterior" );

            hlslContext = new EmitContextHLSL(SharedHLSL, Range, this.EmitClass.GetName());
            var entryPointSpan = hlslContext.EntryPointSpan;
            entryPointSpan = hlslContext.PushErrorMask(entryPointSpan, "X3550");

            var inputCoarseVertexCount = GetAttribute(constantElement, "HS_InputCoarseVertexCount");
            var outputControlPointCount = GetAttribute(constantElement, "HS_OutputControlPointCount");

            var tsDomain = GetAttribute(constantElement, "TS_Domain");
            var tsPartitioning = GetAttribute(constantElement, "TS_Partitioning");
            var tsOutputTopology = GetAttribute(constantElement, "TS_OutputTopology");
            var tsMaxTessFactor = GetAttribute(constantElement, "TS_MaxTessFactor");

            var hsPatchEdgeCount = GetAttribute(constantElement, "HS_PatchEdgeCount");
            var hsPatchInsideCount = GetAttribute(constantElement, "HS_PatchInsideCount");
            var hsPatchCornerCount = GetAttribute(constantElement, "HS_PatchCornerCount");

            var hsCullPatchAttr = GetAttribute(outputPatchElement, "HS_CullPatch");

            hlslContext.GenerateConnectorType(coarseVertexElement);
            hlslContext.GenerateConnectorType(controlPointElement);
            hlslContext.GenerateConnectorType(outputPatchElement);


            // OutputPatch entry point

            var outputPatchConnector = hlslContext.GenerateConnectorType(outputPatchElement);
            entryPointSpan.WriteLine("{0} __patchMain(",
                outputPatchConnector);



            bool first = true;
            hlslContext.DeclareParamAndBind(
                GetAttribute(superPatchElement, "HS_InputCoarseVertices"),
                null,
                ref first,
                entryPointSpan);

            // \todo:
            // @OutputPatch Array[ControlPoint, HS_OutputControlPointCount] HS_OutputControlPoints

            hlslContext.DeclareParamAndBind(
                GetAttribute(superPatchElement, "HS_PatchID"),
                "SV_PrimitiveID",
                ref first,
                entryPointSpan);

            entryPointSpan.WriteLine("\t)");
            entryPointSpan.WriteLine("{");

            // Declare the output patch variable, but don't initialize it
            var outputPatch = outputPatchConnector.CreateVal( "HS_OutputPatch" );
            hlslContext.DeclareLocal( outputPatch, entryPointSpan );

            // Do any input-patch initialization stuff
            var ip2op = hlslContext.EmitTempRecordCtor(
                entryPointSpan,
                inputPatchElement,
                GetAttribute(outputPatchElement, "__ip2op"));

            // Iterate over the corners of the patch, and initialize each
            entryPointSpan.WriteLine( "for( uint HS_CornerID = 0; HS_CornerID < {0}; HS_CornerID++ )",
                hlslContext.EmitAttribRef( hsPatchCornerCount, null ) );
            entryPointSpan.WriteLine("{" );

            hlslContext.BindAttr(
                GetAttribute( patchCornerElement, "__ip2pc" ),
                ip2op );

            var patchCornerIDAttr = GetAttribute( patchCornerElement, "HS_PatchCornerID" );
            var hsCornerIDVal = new SimpleValHLSL( "HS_CornerID",
                    (RealTypeHLSL) hlslContext.EmitType( patchCornerIDAttr.Type ) );
            hlslContext.BindAttr(
                patchCornerIDAttr,
                hsCornerIDVal );


            var hsPatchCornersAttr = GetAttribute( outputPatchElement, "HS_PatchCorners" );
            var hsPatchCornersVal = hlslContext.FetchAttr(
                outputPatch,
                hsPatchCornersAttr.Attribute,
                entryPointSpan );
            hlslContext.BindAttr(hsPatchCornersAttr, hsPatchCornersVal);
            var hsPatchCornerVal = hlslContext.GetElem(
                hsPatchCornersVal,
                hsCornerIDVal);


            hlslContext.InitRecord(
                entryPointSpan,
                patchCornerElement,
                hsPatchCornerVal );

            entryPointSpan.WriteLine( "}" );

            entryPointSpan.WriteLine( "for( uint HS_EdgeID = 0; HS_EdgeID < {0}; HS_EdgeID++ )",
                hlslContext.EmitAttribRef(hsPatchEdgeCount, null));
            entryPointSpan.WriteLine( "{" );

            hlslContext.BindAttr(
                GetAttribute(patchEdgeElement, "__ip2pe"),
                ip2op);

            hlslContext.BindAttr(
                GetAttribute(patchEdgeElement, "__op2pe"),
                outputPatch);

            var patchEdgeIDAttr = GetAttribute(patchEdgeElement, "HS_PatchEdgeID");
            var hsEdgeIDVal = new SimpleValHLSL("HS_EdgeID",
                (RealTypeHLSL)hlslContext.EmitType(patchEdgeIDAttr.Type));
            hlslContext.BindAttr(
                patchEdgeIDAttr,
                hsEdgeIDVal );


            var hsPatchEdgesAttr = GetAttribute(outputPatchElement, "HS_PatchEdges");
            var hsPatchEdgesVal = hlslContext.FetchAttr(
                outputPatch,
                hsPatchEdgesAttr.Attribute,
                entryPointSpan );
            hlslContext.BindAttr(hsPatchEdgesAttr, hsPatchEdgesVal);
            var hsPatchEdgeVal = hlslContext.GetElem(
                hsPatchEdgesVal,
                hsEdgeIDVal);
            hlslContext.InitRecord(
                entryPointSpan,
                patchEdgeElement,
                hsPatchEdgeVal);

            var hsEdgeFactorSrcVal = hlslContext.FetchAttr(
                hsPatchEdgeVal,
                GetAttribute(patchEdgeElement, "HS_EdgeFactor").Attribute,
                entryPointSpan);

            var hsEdgeFactorsAttr = GetAttribute(outputPatchElement, "HS_EdgeFactors").Attribute;
            var hsEdgeFactorsVal = hlslContext.FetchAttr(
                outputPatch,
                hsEdgeFactorsAttr,
                entryPointSpan);
            hlslContext.BindAttr(hsEdgeFactorsAttr, hsEdgeFactorsVal);
            var hsEdgeFactorDstVal = hlslContext.GetElem(
                hsEdgeFactorsVal,
                hsEdgeIDVal);
            hlslContext.Assign(
                hsEdgeFactorDstVal,
                hsEdgeFactorSrcVal,
                entryPointSpan);
            entryPointSpan.WriteLine("}");

            var hsPatchInsideCountStr = hlslContext.EmitAttribRef(hsPatchInsideCount, null).ToString();
            var onlyOneInside = hsPatchInsideCountStr == "1";

            if (!onlyOneInside)
            {
                entryPointSpan.WriteLine("for( uint HS_InsideID = 0; HS_InsideID < {0}; HS_InsideID++ )",
                    hlslContext.EmitAttribRef(hsPatchInsideCount, null));
                entryPointSpan.WriteLine("{");
            }
            else
            {
                entryPointSpan.WriteLine("uint HS_InsideID = 0;");
            }

            hlslContext.BindAttr(
                GetAttribute(patchInteriorElement, "__ip2pi"),
                ip2op);

            hlslContext.BindAttr(
                GetAttribute(patchInteriorElement, "__op2pi"),
                outputPatch);

            var hsPatchInsideIDAttr = GetAttribute(patchInteriorElement, "HS_PatchInteriorID");
            var hsInsideIDVal = new SimpleValHLSL("HS_InsideID",
                (RealTypeHLSL)hlslContext.EmitType(hsPatchInsideIDAttr.Type));
            hlslContext.BindAttr(
                hsPatchInsideIDAttr,
                hsInsideIDVal);

            var hsPatchInteriorsAttr = GetAttribute(outputPatchElement, "HS_PatchInteriors");
            var hsPatchInteriorsVal = hlslContext.FetchAttr(
                outputPatch,
                hsPatchInteriorsAttr.Attribute,
                entryPointSpan);
            hlslContext.BindAttr(hsPatchInteriorsAttr, hsPatchInteriorsVal);

            var hsPatchInteriorVal = hlslContext.GetElem(
                hsPatchInteriorsVal,
                hsInsideIDVal);
            hlslContext.InitRecord(
                entryPointSpan,
                patchInteriorElement,
                hsPatchInteriorVal);

            var hsInsideFactorSrcVal = hlslContext.FetchAttr(
                hsPatchInteriorVal,
                GetAttribute(patchInteriorElement, "HS_InsideFactor").Attribute,
                entryPointSpan);

            var hsInsideFactorsAttr = GetAttribute(outputPatchElement, "HS_InsideFactors").Attribute;
            var hsInsideFactorsVal = hlslContext.FetchAttr(
                outputPatch,
                hsInsideFactorsAttr,
                entryPointSpan);
            hlslContext.BindAttr(hsInsideFactorsAttr, hsInsideFactorsVal);
            var hsInsideFactorDstVal = hlslContext.GetElem(
                hsInsideFactorsVal,
                hsInsideIDVal);
            hlslContext.Assign(
                hsInsideFactorDstVal,
                hsInsideFactorSrcVal,
                entryPointSpan);

            if (!onlyOneInside)
            {
                entryPointSpan.WriteLine("}");
            }

            hlslContext.InitRecord(
                entryPointSpan,
                outputPatchElement,
                outputPatch);

            var hsCullPatchVal = hlslContext.EmitAttribRef(hsCullPatchAttr, entryPointSpan);

            entryPointSpan.WriteLine("if( {0} )", hsCullPatchVal);
            entryPointSpan.WriteLine("{");

            var hsEdgeFactor0Val = hlslContext.GetElem(
                hsEdgeFactorsVal,
                new SimpleValHLSL("0", new ScalarTypeHLSL("int")));
            hlslContext.Assign(
                hsEdgeFactor0Val,
                new SimpleValHLSL("0.0f", new ScalarTypeHLSL("float")),
                entryPointSpan);

            entryPointSpan.WriteLine("}");


            entryPointSpan.WriteLine( "\treturn {0};", outputPatch );

            entryPointSpan.WriteLine("}");

            // ControlPoint entry point

            foreach (var a in inputPatchElement.Attributes)
                hlslContext.UnbindAttribute(a);

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


            entryPointSpan.WriteLine("[domain(\"{0}\")]",
                hlslContext.EmitAttrLit( tsDomain ));
            entryPointSpan.WriteLine("[partitioning(\"{0}\")]",
                hlslContext.EmitAttrLit( tsPartitioning ));
            entryPointSpan.WriteLine("[outputtopology(\"{0}\")]",
                hlslContext.EmitAttrLit( tsOutputTopology ));
            entryPointSpan.WriteLine("[outputcontrolpoints({0})]",
                hlslContext.EmitAttrLit( outputControlPointCount ));
            entryPointSpan.WriteLine("[patchconstantfunc(\"__patchMain\")]");
//            entryPointSpan.WriteLine("[maxtessfactor({0:f})]",
//                hlslContext.EmitAttribRef(
//                    tsMaxTessFactor,
//                    null));
            entryPointSpan.WriteLine("{0} main(",
                hlslContext.GenerateConnectorType(controlPointElement));

            first = true;
            hlslContext.DeclareParamAndBind(
                GetAttribute(superPatchElement, "HS_InputCoarseVertices"),
                null,
                ref first,
                entryPointSpan);
            hlslContext.DeclareParamAndBind(
                GetAttribute(superPatchElement, "HS_PatchID"),
                "SV_PrimitiveID",
                ref first,
                entryPointSpan);

            hlslContext.DeclareParamAndBind(
                GetAttribute(controlPointElement, "HS_ControlPointID"),
                "SV_OutputControlPointID",
                ref first,
                entryPointSpan);


            entryPointSpan.WriteLine("\t)");
            entryPointSpan.WriteLine("{");

            hlslContext.EmitTempRecordCtor(
                entryPointSpan,
                inputPatchElement,
                GetAttribute(controlPointElement, "__ip2ocp"));

            var cpOutput = hlslContext.EmitConnectorCtor(
                entryPointSpan,
                controlPointElement);

            entryPointSpan.WriteLine("\treturn {0};", cpOutput);

            entryPointSpan.WriteLine("}");

            hlslContext.EmitConstantBufferDecl();

            EmitShaderSetup(
                hlslContext,
                "hs_5_0",
                "Hull",
                "HS");
        }

        public override void EmitImplBind()
        {
            ExecBlock.AppendComment( "D3D11 Hull Shader" );

            var constantElement = GetElement( "Constant" );

            var tessEnabledAttr = FindAttribute( constantElement, "__D3D11TessellationEnabled" );
            if( tessEnabledAttr == null )
            {
                ExecBlock.CallCOM(
                    SubmitContext,
                    "ID3D11DeviceContext",
                    "HSSetShader",
                    GetNullPointer( "ID3D11HullShader*" ),
                    GetNullPointer( "ID3D11ClassInstance*" ),
                    ExecBlock.LiteralU32( 0 ) );
                return;
            }

            EmitShaderBind(
                hlslContext,
                "hs_5_0",
                "Hull",
                "HS" );
        }
    }
}
