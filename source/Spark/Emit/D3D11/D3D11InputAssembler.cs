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
    public class D3D11InputAssembler : D3D11Stage
    {
//        private MidElementDecl _uniformElement;
//        private MidElementDecl _iaElement;

//        private MidAttributeDecl _primitiveTopology;
//        private MidAttributeDecl _vertexID;
//        private MidAttributeDecl _instanceID;
        private List<MidExp> _vertexBuffers = new List<MidExp>();
        private List<MidExp> _vertexBufferStrides = new List<MidExp>();
        private List<MidExp> _vertexBufferOffsets = new List<MidExp>();
        private int _inputElementCount = 0;

        IEmitField inputLayoutField;

        public override void EmitImplSetup()
        {
            var uniformElement = GetElement("Uniform");
            var iaElement = GetElement("AssembledVertex");

            var vertexID = GetAttribute(iaElement, "IA_VertexID");
            var instanceID = GetAttribute(iaElement, "IA_InstanceID");

            _info[vertexID.Attribute] = new IndexSourceInfo
            {
                InputSlotClass = InitBlock.Enum32("D3D11_INPUT_CLASSIFICATION", "D3D11_INPUT_PER_VERTEX_DATA", D3D11_INPUT_CLASSIFICATION.D3D11_INPUT_PER_VERTEX_DATA),
                StepRate = 0
            };

            _info[ instanceID.Attribute ] = new IndexSourceInfo
            {
                InputSlotClass = InitBlock.Enum32("D3D11_INPUT_CLASSIFICATION", "D3D11_INPUT_PER_INSTANCE_DATA", D3D11_INPUT_CLASSIFICATION.D3D11_INPUT_PER_INSTANCE_DATA),
                StepRate = 1
            };

            InitBlock.AppendComment("D3D11 Input Assembler");

            var inputElementInits = (from a in iaElement.Attributes
                     where a.IsOutput
                     where a != vertexID.Attribute
                     where a != instanceID.Attribute
                     let name = SharedHLSL.MapName(a)
                     let attrInfo = DecomposeAttr(a)
                     from e in DeclareInputElements(InitBlock, name, attrInfo)
                     select e).ToArray();

            if (_inputElementCount != 0)
            {
                var inputElementDescsVal = InitBlock.Temp(
                    "inputElementDescs",
                    InitBlock.Array(
                        EmitTarget.GetBuiltinType("D3D11_INPUT_ELEMENT_DESC"),
                        inputElementInits));

                var inputLayoutPointerType = EmitTarget.GetOpaqueType("ID3D11InputLayout*");
                inputLayoutField = EmitClass.AddPrivateField(
                    inputLayoutPointerType,
                    "_inputLayout");
                InitBlock.SetArrow(
                    CtorThis,
                    inputLayoutField,
                    EmitTarget.GetNullPointer(inputLayoutPointerType));

                InitBlock.CallCOM(
                    CtorDevice,
                    "ID3D11Device",
                    "CreateInputLayout",
                    inputElementDescsVal.GetAddress(),
                    InitBlock.LiteralU32((UInt32)_inputElementCount),
                    EmitPass.VertexShaderBytecodeVal,
                    EmitPass.VertexShaderBytecodeSizeVal,
                    InitBlock.GetArrow(CtorThis, inputLayoutField).GetAddress());

                DtorBlock.CallCOM(
                    DtorBlock.GetArrow(DtorThis, inputLayoutField),
                    "IUnknown",
                    "Release");
            }
        }

        public override void EmitImplBind()
        {
            ExecBlock.AppendComment( "D3D11 Input Assembler" );


            var uniformElement = GetElement( "Uniform" );
            var drawSpanAttr = GetAttribute( uniformElement, "IA_DrawSpan" );
            ExecBlock.BuiltinApp( EmitTarget.VoidType, "{0}.Bind({1})",
                new[] {
                    EmitContext.EmitAttributeRef(drawSpanAttr, ExecBlock, SubmitEnv),
                    SubmitContext, } );

            var inputLayoutPointerType = EmitTarget.GetOpaqueType("ID3D11InputLayout*");
            var inputLayoutToBind = _inputElementCount == 0
                ? EmitTarget.GetNullPointer(inputLayoutPointerType)
                : ExecBlock.GetArrow(SubmitThis, inputLayoutField);
            ExecBlock.CallCOM(
                SubmitContext,
                "ID3D11DeviceContext",
                "IASetInputLayout",
                inputLayoutToBind);

            if (_vertexBuffers.Count != 0)
            {
                var vertexBuffersVal = ExecBlock.Temp(
                    "inputVertexBuffers",
                    ExecBlock.Array(
                        EmitTarget.GetOpaqueType("ID3D11Buffer*"),
                        (from b in _vertexBuffers
                         select EmitExp(b, ExecBlock, SubmitEnv))));

                var vertexBuffersStridesVal = ExecBlock.Temp(
                    "inputVertexBufferStrides",
                    ExecBlock.Array(
                        EmitTarget.GetBuiltinType("UINT"),
                        (from b in _vertexBufferStrides
                         select EmitExp(b, ExecBlock, SubmitEnv))));

                var vertexBuffersOffsetsVal = ExecBlock.Temp(
                    "inputVertexBufferOffsets",
                    ExecBlock.Array(
                        EmitTarget.GetBuiltinType("UINT"),
                        (from b in _vertexBufferOffsets
                         select EmitExp(b, ExecBlock, SubmitEnv))));

                ExecBlock.CallCOM(
                    SubmitContext,
                    "ID3D11DeviceContext",
                    "IASetVertexBuffers",
                    ExecBlock.LiteralU32(0),
                    ExecBlock.LiteralU32((UInt32)_vertexBuffers.Count),
                    vertexBuffersVal.GetAddress(),
                    vertexBuffersStridesVal.GetAddress(),
                    vertexBuffersOffsetsVal.GetAddress());
            }

            /*
            ExecBlock.CallCOM(
                SubmitContext,
                "ID3D11DeviceContext",
                "IASetPrimitiveTopology",
                EmitContext.EmitAttributeRef( primitiveTopology, ExecBlock, SubmitEnv ) );
            */

        }

        public void EmitImplDraw()
        {
            var uniformElement = GetElement( "Uniform" );
            var drawSpanAttr = GetAttribute( uniformElement, "IA_DrawSpan" );
            ExecBlock.BuiltinApp( EmitTarget.VoidType, "{0}.Submit({1})",
                new[] {
                    EmitContext.EmitAttributeRef(drawSpanAttr, ExecBlock, SubmitEnv),
                    SubmitContext, } );
        }

        private class AttributeInfo
        {
        }

        private class InputElementInfo : AttributeInfo
        {
            public string Name { get; set; }
            public IEmitVal Format { get; set; }
            public int InputSlotIndex { get; set; }
            public IndexSourceInfo Index { get; set; }
            public UInt32 ByteOffset { get; set; }
        }

        private class IndexSourceInfo : AttributeInfo
        {
            public IEmitVal InputSlotClass { get; set; }
            public int StepRate { get; set; }
        }

        private struct FieldInfo
        {
            public MidFieldDecl Field { get; set; }
            public AttributeInfo Info { get; set; }
        }

        private class StructInfo : AttributeInfo
        {
            public FieldInfo[] Fields { get; set; }
        }

        private Dictionary<MidAttributeDecl, AttributeInfo> _info = new Dictionary<MidAttributeDecl, AttributeInfo>();

        private AttributeInfo DecomposeAttr(
            MidAttributeDecl midAttrDecl)
        {
            return _info.Cache(midAttrDecl,
                () => DecomposeAttr(midAttrDecl.Exp));
        }

        private AttributeInfo DecomposeAttr(
            MidExp exp )
        {
            return DecomposeAttrImpl((dynamic)exp);
        }

        private AttributeInfo DecomposeAttrImpl(
            MidAttributeRef midAttrRef)
        {
            return DecomposeAttr(midAttrRef.Decl);
        }

        private AttributeInfo DecomposeAttrImpl(
            MidFieldRef midFieldRef)
        {
            var structAttrInfo = (StructInfo)DecomposeAttr(midFieldRef.Obj);

            var fieldInfo = (from f in structAttrInfo.Fields
                             where f.Field == midFieldRef.Decl
                             select f.Info).First();

            return fieldInfo;
        }

        private AttributeInfo DecomposeAttrImpl(
            MidBuiltinApp midApp)
        {
            var name = midApp.Decl.GetTemplate("hlsl");
            var args = midApp.Args.ToArray();
            if (name == "__VertexFetch")
            {
                var buffer = args[0];
                var offset = args[1];
                var stride = args[2];
                var index = args[3];

                var inputVertexStream = DecomposeVertexStream(buffer, offset, stride);
                var inputIndex = DecomposeAttr(index);

                return GenerateInputElements(
                    "",
                    midApp.Type,
                    inputVertexStream,
                    inputIndex,
                    0);

                /*
                var inputSlotClass = "";
                var stepRate = 0;

                if (index is MidAttributeRef)
                {
                    var indexAttribRef = (MidAttributeRef)index;
                    if (indexAttribRef.Decl == _vertexID)
                    {
                        inputSlotClass = "D3D11_INPUT_PER_VERTEX_DATA";
                    }
                    else if (indexAttribRef.Decl == _instanceID)
                    {
                        inputSlotClass = "D3D11_INPUT_PER_INSTANCE_DATA";
                        stepRate = 1;
                    }

                    DecomposeInputElements(
                        span: span,
                        name: name,
                        type: exp.Type,
                        inputSlotIndex: inputSlotIndex,
                        inputSlotClass: inputSlotClass,
                        stepRate: stepRate);
                }*/
            }

            throw new NotImplementedException();
        }

        private AttributeInfo GenerateInputElements(
            string name,
            MidType type,
            int vertexStream,
            AttributeInfo index,
            UInt32 baseOffset)
        {
            return GenerateInputElementsImpl(
                name,
                (dynamic)type,
                vertexStream,
                (IndexSourceInfo)index,
                baseOffset,
                vertexStream);
        }

        private AttributeInfo GenerateInputElementsImpl(
            string name,
            MidStructRef type,
            int vertexStream,
            IndexSourceInfo index,
            UInt32 baseOffset,
            int inputSlotIndex)
        {
            var fields = new List<FieldInfo>();
            UInt32 totalOffset = baseOffset;
            foreach (var f in type.Fields)
            {
                // \todo: Align

                fields.Add(new FieldInfo
                {
                    Field = f,
                    Info = GenerateInputElements(
                      string.Format("{0}_{1}", name, f.Name.ToString()),
                      f.Type,
                      vertexStream,
                      index,
                      totalOffset)
                });

                totalOffset += GetSizeOf(f.Type);
            }

            return new StructInfo { Fields = fields.ToArray() };
        }

        private AttributeInfo GenerateInputElementsImpl(
            string name,
            MidBuiltinType type,
            int vertexStream,
            IndexSourceInfo index,
            UInt32 baseOffset,
            int inputSlotIndex)
        {
            var format = MapBuiltinTypeFormat(type);

            return new InputElementInfo
            {
                Name = name,
                Format = format,
                ByteOffset = baseOffset,
                Index = index,
                InputSlotIndex = inputSlotIndex,
            };
        }

        private IEmitVal MapBuiltinTypeFormat(MidBuiltinType type)
        {
            switch (type.Name)
            {
                case "int":
                    return InitBlock.Enum32("DXGI_FORMAT", "DXGI_FORMAT_R32_SINT", DXGI_FORMAT.DXGI_FORMAT_R32_SINT);
                case "float2":
                    return InitBlock.Enum32("DXGI_FORMAT", "DXGI_FORMAT_R32G32_FLOAT", DXGI_FORMAT.DXGI_FORMAT_R32G32_FLOAT);
                case "float3":
                case "Tangent":
                    return InitBlock.Enum32("DXGI_FORMAT", "DXGI_FORMAT_R32G32B32_FLOAT", DXGI_FORMAT.DXGI_FORMAT_R32G32B32_FLOAT);
                case "float4":
                    return InitBlock.Enum32("DXGI_FORMAT", "DXGI_FORMAT_R32G32B32A32_FLOAT", DXGI_FORMAT.DXGI_FORMAT_R32G32B32A32_FLOAT);

                case "ubyte4":
                    return InitBlock.Enum32("DXGI_FORMAT", "DXGI_FORMAT_R8G8B8A8_UINT", DXGI_FORMAT.DXGI_FORMAT_R8G8B8A8_UINT);
                case "unorm4":
                    return InitBlock.Enum32("DXGI_FORMAT", "DXGI_FORMAT_R8G8B8A8_UNORM", DXGI_FORMAT.DXGI_FORMAT_R8G8B8A8_UNORM);
                
                default:
                    throw new NotImplementedException();
            }
        }

        private int DecomposeVertexStream(MidVal bufferVal, MidVal offsetVal, MidVal strideVal )
        {
            int result = _vertexBuffers.Count;

            _vertexBuffers.Add(bufferVal);
            _vertexBufferOffsets.Add(offsetVal);
            _vertexBufferStrides.Add(strideVal);

            return result;
        }

        private IEnumerable<IEmitVal> DeclareInputElements(
            IEmitBlock block,
            string name,
            AttributeInfo info)
        {
            return DeclareInputElementsImpl(
                block,
                name,
                (dynamic)info);
        }

        private IEnumerable<IEmitVal> DeclareInputElementsImpl(
            IEmitBlock block,
            string name,
            StructInfo info)
        {
            foreach (var f in info.Fields)
            {
                var fieldElements = (DeclareInputElements(
                    block,
                    name,
                    f.Info)).ToArray();
                foreach (var fe in fieldElements)
                    yield return fe;
            }
        }

        private IEnumerable<IEmitVal> DeclareInputElementsImpl(
            IEmitBlock block,
            string name,
            InputElementInfo info)
        {
            _inputElementCount++;

            yield return block.Struct(
                "D3D11_INPUT_ELEMENT_DESC",
                block.LiteralString(string.Format("USER_{0}", name)),
                block.LiteralU32(0),
                info.Format,
                block.LiteralU32((UInt32) info.InputSlotIndex),
                block.LiteralU32((UInt32) info.ByteOffset),
                info.Index.InputSlotClass,
                block.LiteralU32((UInt32) info.Index.StepRate));
        }
    }
}
