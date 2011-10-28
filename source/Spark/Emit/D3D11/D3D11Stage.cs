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
    public abstract class D3D11Stage
    {
        public PassEmitContext EmitPass { get; set; }
        public SourceRange Range { get; set; }

        public EmitContext EmitContext { get { return EmitPass.EmitContext; } }
        public MidPipelineDecl MidPass { get { return EmitPass.MidPass; } }

        public virtual void EmitInterface() { }
        public abstract void EmitImplSetup();
        public abstract void EmitImplBind();

        protected MidElementDecl GetElement(string name)
        {
            return EmitContext.GetElement(MidPass, name);
        }

        protected MidAttributeWrapperDecl GetAttribute(MidElementDecl element, string name)
        {
            var result = FindAttribute(element, name);
            if (result != null)
                return result;

            throw new NotImplementedException();
        }

        protected MidAttributeWrapperDecl FindAttribute(MidElementDecl element, string name)
        {
            foreach (var a in element.AttributeWrappers)
            {
                if (a.Name == Identifiers.simpleIdentifier(name))
                    return a;
            }

            return null;
        }

        private void EmitShaderBinds(
            IEmitBlock block,
            string prefix,
            EmitContextHLSL hlslContext)
        {
            block.CallCOM(
                SubmitContext,
                "ID3D11DeviceContext",
                string.Format("{0}SetConstantBuffers", prefix),
                block.LiteralU32(0),
                block.LiteralU32(1),
                block.GetArrow(SubmitThis, EmitPass.CBField).GetAddress());

            var resources = hlslContext.ShaderResources.ToArray();
            var resourceCount = resources.Length;
            if (resourceCount != 0)
            {
                var resourceVals = (from r in resources
                                    select EmitContext.EmitExp(r, block, SubmitEnv)).ToArray();

                var resourcesVal = block.Temp(
                    string.Format("{0}Resources", prefix),
                    block.Array(
                        EmitTarget.GetOpaqueType("ID3D11ShaderResourceView*"),
                        resourceVals));

                block.CallCOM(
                    SubmitContext,
                    "ID3D11DeviceContext",
                    string.Format("{0}SetShaderResources", prefix),
                    block.LiteralU32(0),
                    block.LiteralU32((UInt32)resourceCount),
                    resourcesVal.GetAddress());
            }

            var samplers = hlslContext.SamplerStates.ToArray();
            var samplerCount = samplers.Length;
            if (samplerCount != 0)
            {
                var samplerVals = (from s in samplers
                                   select EmitContext.EmitExp(s, block, SubmitEnv)).ToArray();

                var samplersVal = block.Temp(
                    string.Format("{0}Samplers", prefix),
                    block.Array(
                        EmitTarget.GetOpaqueType("ID3D11SamplerState*"),
                        samplerVals));

                block.CallCOM(
                    SubmitContext,
                    "ID3D11DeviceContext",
                    string.Format("{0}SetSamplers", prefix),
                    block.LiteralU32(0),
                    block.LiteralU32((UInt32)samplerCount),
                    samplersVal.GetAddress());
            }
        }

        IEmitField _shaderField = null;

        protected void EmitShaderSetup(
            EmitContextHLSL hlslContext,
            string profile,
            string stageName,
            string prefix)
        {
            var hlslSpan = hlslContext.Span;

            var bytecode = hlslContext.Compile(profile);

            InitBlock.AppendComment(hlslContext.Span);

            if (bytecode != null && bytecode.Length > 0)
                EmitTarget.ShaderBytecodeCallback(prefix, bytecode);

            var bytecodeLengthVal = InitBlock.Temp(
                "bytecodeSize",
                InitBlock.LiteralU32(
                    (UInt32)bytecode.Length));
            var bytecodeVal = InitBlock.Temp(
                "bytecode",
                InitBlock.LiteralData(bytecode));

            // Terrible hack - save off vals in case of vertex shader... :(
            // This is required because creating an Input Layout
            // requires VS bytecode... for some reason...
            if (prefix == "VS")
            {
                EmitPass.VertexShaderBytecodeVal = bytecodeVal;
                EmitPass.VertexShaderBytecodeSizeVal = bytecodeLengthVal;
            }


            var shaderType = EmitTarget.GetOpaqueType(
                string.Format("ID3D11{0}Shader*", stageName));
            var shaderNull = EmitTarget.GetNullPointer(shaderType);
            _shaderField = EmitClass.AddPrivateField(
                shaderType,
                string.Format("_{0}Shader", stageName));

            InitBlock.SetArrow(
                CtorThis,
                _shaderField,
                shaderNull);

            var classLinkageNull = EmitTarget.GetNullPointer(
                EmitTarget.GetOpaqueType("ID3D11ClassLinkage*"));

            InitBlock.CallCOM(
                CtorDevice,
                "ID3D11Device",
                string.Format("Create{0}Shader", stageName),
                bytecodeVal,
                bytecodeLengthVal,
                classLinkageNull,
                InitBlock.GetArrow(CtorThis, _shaderField).GetAddress());

            DtorBlock.CallCOM(
                DtorBlock.GetArrow(DtorThis, _shaderField),
                "IUnknown",
                "Release");
        }

        protected void EmitShaderBind(
            EmitContextHLSL hlslContext,
            string profile,
            string stageName,
            string prefix)
        {
            ExecBlock.CallCOM(
                SubmitContext,
                "ID3D11DeviceContext",
                string.Format("{0}SetShader", prefix),
                ExecBlock.GetArrow(SubmitThis, _shaderField),
                GetNullPointer("ID3D11ClassInstance**"),
                ExecBlock.LiteralU32(0));

            EmitShaderBinds(ExecBlock, prefix, hlslContext);
        }

        protected IEmitVal EmitExp(MidExp midExp, IEmitBlock block, EmitEnv env)
        {
            return EmitContext.EmitExp(midExp, block, env);
        }

        protected UInt32 GetSizeOf(MidType type)
        {
            return GetSizeInfo(type).Size;
        }

        public struct SizeInfo
        {
            public UInt32 Size;
            public UInt32 Align;
        }

        public SizeInfo GetSizeInfo(MidType type)
        {
            return GetSizeInfoImpl((dynamic)type);
        }

        private SizeInfo GetSizeInfoImpl(MidBuiltinType type)
        {
            switch (type.Name)
            {
                case "ubyte4": return new SizeInfo { Size = 1 * 4, Align = 4 };
                case "unorm4": return new SizeInfo { Size = 1 * 4, Align = 4 };
                case "float2": return new SizeInfo { Size = 2 * 4, Align = 4 };
                case "Tangent":
                case "float3": return new SizeInfo { Size = 3 * 4, Align = 4 };
                case "float4": return new SizeInfo { Size = 4 * 4, Align = 4 };
                default:
                    throw new NotImplementedException();
            }
        }

        private SizeInfo GetSizeInfoImpl(MidStructRef type)
        {
            UInt32 totalSize = 0;
            UInt32 maxAlign = 1;
            foreach (var f in type.Fields)
            {
                var fieldInfo = GetSizeInfo(f.Type);

                UInt32 fieldSize = fieldInfo.Size;
                UInt32 fieldAlign = fieldInfo.Align;

                // pad to align
                totalSize = ((totalSize + (fieldAlign - 1)) / fieldAlign) * fieldAlign;

                totalSize += fieldSize;
                maxAlign = Math.Max(maxAlign, fieldAlign);
            }

            return new SizeInfo { Size = totalSize, Align = maxAlign };
        }

        protected UInt32 GetSizeOfImpl(MidType type)
        {
            return EmitContext.EmitType(type, null).Size;
        }


        protected IEmitVal GetNullPointer(string typeName)
        {
            return EmitTarget.GetNullPointer(EmitTarget.GetOpaqueType(typeName));
        }

        protected IdentifierFactory Identifiers { get { return EmitContext.Identifiers; } }
        protected IDiagnosticsCollection Diagnostics { get { return EmitContext.Diagnostics; } }

        protected HLSL.SharedContextHLSL SharedHLSL { get { return EmitPass.SharedHLSL; } }
        protected IEmitBlock InitBlock { get { return EmitPass.InitBlock; } }
        protected IEmitBlock ExecBlock { get { return EmitPass.ExecBlock; } }
        protected IEmitBlock DtorBlock { get { return EmitPass.DtorBlock; } }
        protected IEmitClass EmitClass { get { return EmitPass.EmitClass; } }
        protected IEmitTarget EmitTarget { get { return EmitPass.EmitContext.Target; } }
        protected IEmitVal CtorDevice { get { return EmitPass.CtorDevice; } }
        protected IEmitVal SubmitContext { get { return EmitPass.SubmitContext; } }
        protected IEmitVal CtorThis { get { return EmitPass.CtorThis; } }
        protected IEmitVal SubmitThis { get { return EmitPass.SubmitThis; } }
        protected IEmitVal DtorThis { get { return EmitPass.DtorThis; } }

        protected EmitEnv SubmitEnv { get { return EmitPass.SubmitEnv; } }

        //
        // The following declarations must exactly match
        // the declarations in the D3D11 C/C++ headers:
        //

        public enum D3D11_INPUT_CLASSIFICATION
        {
            D3D11_INPUT_PER_VERTEX_DATA = 0,
            D3D11_INPUT_PER_INSTANCE_DATA = 1,
        }

        public enum DXGI_FORMAT
        {
            DXGI_FORMAT_UNKNOWN = 0,
            DXGI_FORMAT_R32G32B32A32_TYPELESS = 1,
            DXGI_FORMAT_R32G32B32A32_FLOAT = 2,
            DXGI_FORMAT_R32G32B32A32_UINT = 3,
            DXGI_FORMAT_R32G32B32A32_SINT = 4,
            DXGI_FORMAT_R32G32B32_TYPELESS = 5,
            DXGI_FORMAT_R32G32B32_FLOAT = 6,
            DXGI_FORMAT_R32G32B32_UINT = 7,
            DXGI_FORMAT_R32G32B32_SINT = 8,
            DXGI_FORMAT_R16G16B16A16_TYPELESS = 9,
            DXGI_FORMAT_R16G16B16A16_FLOAT = 10,
            DXGI_FORMAT_R16G16B16A16_UNORM = 11,
            DXGI_FORMAT_R16G16B16A16_UINT = 12,
            DXGI_FORMAT_R16G16B16A16_SNORM = 13,
            DXGI_FORMAT_R16G16B16A16_SINT = 14,
            DXGI_FORMAT_R32G32_TYPELESS = 15,
            DXGI_FORMAT_R32G32_FLOAT = 16,
            DXGI_FORMAT_R32G32_UINT = 17,
            DXGI_FORMAT_R32G32_SINT = 18,
            DXGI_FORMAT_R32G8X24_TYPELESS = 19,
            DXGI_FORMAT_D32_FLOAT_S8X24_UINT = 20,
            DXGI_FORMAT_R32_FLOAT_X8X24_TYPELESS = 21,
            DXGI_FORMAT_X32_TYPELESS_G8X24_UINT = 22,
            DXGI_FORMAT_R10G10B10A2_TYPELESS = 23,
            DXGI_FORMAT_R10G10B10A2_UNORM = 24,
            DXGI_FORMAT_R10G10B10A2_UINT = 25,
            DXGI_FORMAT_R11G11B10_FLOAT = 26,
            DXGI_FORMAT_R8G8B8A8_TYPELESS = 27,
            DXGI_FORMAT_R8G8B8A8_UNORM = 28,
            DXGI_FORMAT_R8G8B8A8_UNORM_SRGB = 29,
            DXGI_FORMAT_R8G8B8A8_UINT = 30,
            DXGI_FORMAT_R8G8B8A8_SNORM = 31,
            DXGI_FORMAT_R8G8B8A8_SINT = 32,
            DXGI_FORMAT_R16G16_TYPELESS = 33,
            DXGI_FORMAT_R16G16_FLOAT = 34,
            DXGI_FORMAT_R16G16_UNORM = 35,
            DXGI_FORMAT_R16G16_UINT = 36,
            DXGI_FORMAT_R16G16_SNORM = 37,
            DXGI_FORMAT_R16G16_SINT = 38,
            DXGI_FORMAT_R32_TYPELESS = 39,
            DXGI_FORMAT_D32_FLOAT = 40,
            DXGI_FORMAT_R32_FLOAT = 41,
            DXGI_FORMAT_R32_UINT = 42,
            DXGI_FORMAT_R32_SINT = 43,
            DXGI_FORMAT_R24G8_TYPELESS = 44,
            DXGI_FORMAT_D24_UNORM_S8_UINT = 45,
            DXGI_FORMAT_R24_UNORM_X8_TYPELESS = 46,
            DXGI_FORMAT_X24_TYPELESS_G8_UINT = 47,
            DXGI_FORMAT_R8G8_TYPELESS = 48,
            DXGI_FORMAT_R8G8_UNORM = 49,
            DXGI_FORMAT_R8G8_UINT = 50,
            DXGI_FORMAT_R8G8_SNORM = 51,
            DXGI_FORMAT_R8G8_SINT = 52,
            DXGI_FORMAT_R16_TYPELESS = 53,
            DXGI_FORMAT_R16_FLOAT = 54,
            DXGI_FORMAT_D16_UNORM = 55,
            DXGI_FORMAT_R16_UNORM = 56,
            DXGI_FORMAT_R16_UINT = 57,
            DXGI_FORMAT_R16_SNORM = 58,
            DXGI_FORMAT_R16_SINT = 59,
            DXGI_FORMAT_R8_TYPELESS = 60,
            DXGI_FORMAT_R8_UNORM = 61,
            DXGI_FORMAT_R8_UINT = 62,
            DXGI_FORMAT_R8_SNORM = 63,
            DXGI_FORMAT_R8_SINT = 64,
            DXGI_FORMAT_A8_UNORM = 65,
            DXGI_FORMAT_R1_UNORM = 66,
            DXGI_FORMAT_R9G9B9E5_SHAREDEXP = 67,
            DXGI_FORMAT_R8G8_B8G8_UNORM = 68,
            DXGI_FORMAT_G8R8_G8B8_UNORM = 69,
            DXGI_FORMAT_BC1_TYPELESS = 70,
            DXGI_FORMAT_BC1_UNORM = 71,
            DXGI_FORMAT_BC1_UNORM_SRGB = 72,
            DXGI_FORMAT_BC2_TYPELESS = 73,
            DXGI_FORMAT_BC2_UNORM = 74,
            DXGI_FORMAT_BC2_UNORM_SRGB = 75,
            DXGI_FORMAT_BC3_TYPELESS = 76,
            DXGI_FORMAT_BC3_UNORM = 77,
            DXGI_FORMAT_BC3_UNORM_SRGB = 78,
            DXGI_FORMAT_BC4_TYPELESS = 79,
            DXGI_FORMAT_BC4_UNORM = 80,
            DXGI_FORMAT_BC4_SNORM = 81,
            DXGI_FORMAT_BC5_TYPELESS = 82,
            DXGI_FORMAT_BC5_UNORM = 83,
            DXGI_FORMAT_BC5_SNORM = 84,
            DXGI_FORMAT_B5G6R5_UNORM = 85,
            DXGI_FORMAT_B5G5R5A1_UNORM = 86,
            DXGI_FORMAT_B8G8R8A8_UNORM = 87,
            DXGI_FORMAT_B8G8R8X8_UNORM = 88,
            DXGI_FORMAT_R10G10B10_XR_BIAS_A2_UNORM = 89,
            DXGI_FORMAT_B8G8R8A8_TYPELESS = 90,
            DXGI_FORMAT_B8G8R8A8_UNORM_SRGB = 91,
            DXGI_FORMAT_B8G8R8X8_TYPELESS = 92,
            DXGI_FORMAT_B8G8R8X8_UNORM_SRGB = 93,
            DXGI_FORMAT_BC6H_TYPELESS = 94,
            DXGI_FORMAT_BC6H_UF16 = 95,
            DXGI_FORMAT_BC6H_SF16 = 96,
            DXGI_FORMAT_BC7_TYPELESS = 97,
            DXGI_FORMAT_BC7_UNORM = 98,
            DXGI_FORMAT_BC7_UNORM_SRGB = 99,
        }

        public enum D3D11_BLEND
        {
            D3D11_BLEND_ZERO = 1,
            D3D11_BLEND_ONE = 2,
            D3D11_BLEND_SRC_COLOR = 3,
            D3D11_BLEND_INV_SRC_COLOR = 4,
            D3D11_BLEND_SRC_ALPHA = 5,
            D3D11_BLEND_INV_SRC_ALPHA = 6,
            D3D11_BLEND_DEST_ALPHA = 7,
            D3D11_BLEND_INV_DEST_ALPHA = 8,
            D3D11_BLEND_DEST_COLOR = 9,
            D3D11_BLEND_INV_DEST_COLOR = 10,
            D3D11_BLEND_SRC_ALPHA_SAT = 11,
            D3D11_BLEND_BLEND_FACTOR = 14,
            D3D11_BLEND_INV_BLEND_FACTOR = 15,
            D3D11_BLEND_SRC1_COLOR = 16,
            D3D11_BLEND_INV_SRC1_COLOR = 17,
            D3D11_BLEND_SRC1_ALPHA = 18,
            D3D11_BLEND_INV_SRC1_ALPHA = 19,
        }

        public enum D3D11_BLEND_OP
        {
            D3D11_BLEND_OP_ADD = 1,
            D3D11_BLEND_OP_SUBTRACT = 2,
            D3D11_BLEND_OP_REV_SUBTRACT = 3,
            D3D11_BLEND_OP_MIN = 4,
            D3D11_BLEND_OP_MAX = 5,
        }

        public enum D3D11_COLOR_WRITE_ENABLE
        {
            D3D11_COLOR_WRITE_ENABLE_RED = 1,
            D3D11_COLOR_WRITE_ENABLE_GREEN = 2,
            D3D11_COLOR_WRITE_ENABLE_BLUE = 4,
            D3D11_COLOR_WRITE_ENABLE_ALPHA = 8,
            D3D11_COLOR_WRITE_ENABLE_ALL = 0xf,
        }

        public enum D3D11_USAGE
        {
            D3D11_USAGE_DEFAULT = 0,
            D3D11_USAGE_IMMUTABLE = 1,
            D3D11_USAGE_DYNAMIC = 2,
            D3D11_USAGE_STAGING = 3,
        }

        public enum D3D11_BIND_FLAG
        {
            D3D11_BIND_VERTEX_BUFFER = 0x1,
            D3D11_BIND_INDEX_BUFFER = 0x2,
            D3D11_BIND_CONSTANT_BUFFER = 0x4,
            D3D11_BIND_SHADER_RESOURCE = 0x8,
            D3D11_BIND_STREAM_OUTPUT = 0x10,
            D3D11_BIND_RENDER_TARGET = 0x20,
            D3D11_BIND_DEPTH_STENCIL = 0x40,
            D3D11_BIND_UNORDERED_ACCESS = 0x80,
        }

        public enum D3D11_CPU_ACCESS_FLAG
        {
            D3D11_CPU_ACCESS_WRITE = 0x10000,
            D3D11_CPU_ACCESS_READ = 0x20000,
        }

        public enum D3D11_MAP
        {
            D3D11_MAP_READ = 1,
            D3D11_MAP_WRITE = 2,
            D3D11_MAP_READ_WRITE = 3,
            D3D11_MAP_WRITE_DISCARD = 4,
            D3D11_MAP_WRITE_NO_OVERWRITE = 5,
        }
    }
}
