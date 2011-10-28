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
    public class D3D11GeometryShader : D3D11Stage
    {
        MidElementDecl constantElement;
        EmitContextHLSL hlslContext = null;

        public override void EmitImplSetup()
        {
            constantElement = GetElement( "Constant" );

            InitBlock.AppendComment( "D3D11 Geometry Shader" );

            var gsEnabledAttr = FindAttribute( constantElement, "__D3D11GeometryShaderEnabled" );
            if( gsEnabledAttr == null )
            {
                return;
            }

            var uniformElement = GetElement( "Uniform" );
            var fineVertexElement = GetElement( "FineVertex" );
            var rasterVertexElement = GetElement( "RasterVertex" );
            var geometryInputElement = GetElement( "GeometryInput" );
            var geometryOutputElement = GetElement( "GeometryOutput" );

            hlslContext = new EmitContextHLSL( SharedHLSL, Range, this.EmitClass.GetName() );
            var entryPointSpan = hlslContext.EntryPointSpan;

            var gsInstanceCount = GetAttribute( constantElement, "GS_InstanceCount" );
            var gsInputVertexCount = GetAttribute( constantElement, "GS_InputVertexCount" );
            var gsMaxOutputVertexCount = GetAttribute( constantElement, "GS_MaxOutputVertexCount" );
            var gsInstanceID = GetAttribute( geometryInputElement, "GS_InstanceID" );
            var gsInputVertices = GetAttribute( geometryInputElement, "GS_InputVertices" );
            var gsOutputStream = GetAttribute( geometryOutputElement, "GS_OutputStream" );

            hlslContext.GenerateConnectorType(fineVertexElement);
            hlslContext.GenerateConnectorType(rasterVertexElement);

            entryPointSpan.WriteLine( "[instance({0})]",
                hlslContext.EmitAttrLit( gsInstanceCount ) );
            entryPointSpan.WriteLine( "[maxvertexcount({0})]",
                hlslContext.EmitAttrLit( gsMaxOutputVertexCount ) );
            entryPointSpan.WriteLine( "void main(" );

            bool first = true;

            // \todo: "triangle" or appropriate prefix...
            hlslContext.DeclareParamAndBind(
                gsInputVertices,
                hlslContext.MakeArrayType(
                    hlslContext.GenerateConnectorType(fineVertexElement),
                    hlslContext.EmitAttribRef(gsInputVertexCount, null)),
                null,
                ref first,
                entryPointSpan,
                prefix: "triangle ");

            hlslContext.DeclareParamAndBind(
                gsOutputStream,
                null,
                ref first,
                entryPointSpan,
                prefix: "inout ");

            hlslContext.DeclareParamAndBind(
                gsInstanceID,
                "SV_GSInstanceID",
                ref first,
                entryPointSpan );

            entryPointSpan.WriteLine( "\t)" );
            entryPointSpan.WriteLine( "{" );

            var gi2go = hlslContext.EmitTempRecordCtor(
                entryPointSpan,
                geometryInputElement,
                GetAttribute(geometryOutputElement, "__gi2go"));

            hlslContext.BindAttr(
                GetAttribute(rasterVertexElement, "__gi2rv"),
                gi2go);

            var output = hlslContext.EmitConnectorCtor(
                entryPointSpan,
                geometryOutputElement );

            entryPointSpan.WriteLine( "}" );

            hlslContext.EmitConstantBufferDecl();

            EmitShaderSetup(
                hlslContext,
                "gs_5_0",
                "Geometry",
                "GS" );
        }

        public override void EmitImplBind()
        {
            ExecBlock.AppendComment( "D3D11 Geometry Shader" );

            var gsEnabledAttr = FindAttribute( constantElement, "__D3D11GeometryShaderEnabled" );
            if( gsEnabledAttr == null )
            {
                ExecBlock.CallCOM(
                    SubmitContext,
                    "ID3D11DeviceContext",
                    "GSSetShader",
                    GetNullPointer( "ID3D11GeometryShader*" ),
                    GetNullPointer( "ID3D11ClassInstance*" ),
                    ExecBlock.LiteralU32( 0 ) );
                return;
            }

            EmitShaderBind(
                hlslContext,
                "gs_5_0",
                "Geometry",
                "GS" );
        }
    }
}
