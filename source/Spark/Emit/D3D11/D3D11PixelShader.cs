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
    public class D3D11PixelShader : D3D11Stage
    {
        public override void EmitInterface()
        {
            base.EmitInterface();

            var pixelElement = GetElement("Pixel");

            var shaderClass = this.MidPass;

            var directFacet = shaderClass.DirectFacet;

            foreach (var a in directFacet.Attributes)
            {
                if (a.Element != pixelElement)
                    continue;

                if (!a.IsForcedOutput)
                    continue;

                var attrField = EmitClass.AddFieldAndAccessors(
                    EmitTarget.GetOpaqueType("ID3D11RenderTargetView*"),
                    a.Name.ToString());

                // \todo: Associate that field with the attribute...
                EmitPass.ShaderClassEnv.Insert(
                    a,
                    (b) => b.GetArrow(
                            b.Method.ThisParameter,
                            attrField));

                EmitPass.ShaderClassInfo.DirectFacet.BindAttribute(
                    directFacet,
                    a,
                    (b, shaderObj) => b.GetArrow(
                            shaderObj,
                            attrField));
            }
        }

        EmitContextHLSL hlslContext;
        IEmitField blendStateField;
        MidAttributeDecl[] renderTargetAttributes;
        int renderTargetCount;
        MidAttributeWrapperDecl depthStencilViewAttribute;

        enum AttrCase
        {
            Color,
            Alpha,
            Combined,
        }

        struct DecomposeAttrContext
        {
            public AttrCase flavor;
            public int index;

            public DecomposeAttrContext SwitchToAlpha()
            {
                DecomposeAttrContext result = this;
                result.flavor = AttrCase.Alpha;
                return result;
            }
        }

        abstract class AttrInfo
        {
            public virtual bool IsFactor(
                TermFlavor flavor,
                AttrCase channel)
            {
                return false;
            }
        }

        class SrcInfo : AttrInfo
        {
            public override bool IsFactor(
                TermFlavor flavor,
                AttrCase channel)
            {
                if (flavor == TermFlavor.Src)
                    return true;
                return false;
            }
        }

        class DestInfo : AttrInfo
        {
            public override bool IsFactor(
                TermFlavor flavor,
                AttrCase channel)
            {
                if (flavor == TermFlavor.Dest)
                    return true;
                return false;
            }
        }

        class FactorInfo : AttrInfo
        {
            public FactorInfo(
                D3D11_BLEND color,
                D3D11_BLEND alpha)
            {
                _color = color;
                _alpha = alpha;
            }

            public FactorInfo(
                D3D11_BLEND opnd)
            {
                _color = opnd;
                _alpha = opnd;
            }

            public override bool IsFactor(
                TermFlavor flavor,
                AttrCase channel)
            {
                if (flavor == TermFlavor.Dest)
                {
                    if (channel == AttrCase.Color)
                        return _color == D3D11_BLEND.D3D11_BLEND_DEST_COLOR;
                    else if (channel == AttrCase.Alpha)
                        return _alpha == D3D11_BLEND.D3D11_BLEND_DEST_ALPHA;
                }
                else if (flavor == TermFlavor.Src)
                {
                    if (channel == AttrCase.Color)
                        return _color == D3D11_BLEND.D3D11_BLEND_SRC_COLOR;
                    else if (channel == AttrCase.Alpha)
                        return _alpha == D3D11_BLEND.D3D11_BLEND_SRC_ALPHA;
                }
                return false;
            }

            public D3D11_BLEND _color;
            public D3D11_BLEND _alpha;
        }

        class TermInfo : AttrInfo
        {
            public TermInfo(
                AttrInfo left,
                AttrInfo right )
            {
                _left = left;
                _right = right;
            }

            public AttrInfo _left;
            public AttrInfo _right;
        }

        class OpInfo : AttrInfo
        {
            public OpInfo(
                D3D11_BLEND_OP op,
                AttrInfo left,
                AttrInfo right)
            {
                _op = op;
                _left = left;
                _right = right;
            }

            public D3D11_BLEND_OP _op;
            public AttrInfo _left;
            public AttrInfo _right;
        }

        class SubInfo : AttrInfo
        {
            public SubInfo(
                AttrInfo left,
                AttrInfo right)
            {
                _left = left;
                _right = right;
            }

            public AttrInfo _left;
            public AttrInfo _right;
        }

        class LitInfo : AttrInfo
        {
            public LitInfo(
                float value)
            {
                _value = value;
            }

            public float _value;
        }

        struct SourceInfo
        {
            public MidExp colorExp;
            public MidExp alphaExp;
            public MidExp combinedExp;
        };

        struct ChannelBlendDesc
        {
            public D3D11_BLEND srcBlend;
            public D3D11_BLEND destBlend;
            public D3D11_BLEND_OP op;
        }

        struct TargetBlendDesc
        {
            public bool blendEnable;
            public ChannelBlendDesc color;
            public ChannelBlendDesc alpha;
            public UInt32 writeMask;
        }

        TargetBlendDesc[] _renderTargetBlendDescs;
        SourceInfo[] _renderTargetSources;

        private void DecomposeAttr(
            MidAttributeDecl midAttrDecl,
            int index)
        {
            DecomposeAttrContext context;
            context.index = index;
            context.flavor = AttrCase.Combined;

            AttrInfo info = DecomposeAttr(midAttrDecl, context);

            _renderTargetBlendDescs[index] = GetTargetBlendDesc(info);

            // Do validation stuff - basically, we need to
            // be sure that whatever expressions get put into
            // the _renderTargetSources[index] entry are more
            // or less the "same" source...
        }

        private AttrInfo DecomposeAttr(
            MidAttributeDecl midAttrDecl,
            DecomposeAttrContext context )
        {
            return DecomposeAttr(midAttrDecl.Exp, context);
        }

        private AttrInfo DecomposeAttr(
            MidExp exp,
            DecomposeAttrContext context)
        {
            return DecomposeAttrImpl((dynamic)exp, context);
        }

        private AttrInfo DecomposeAttrImpl(
            MidAttributeRef midAttrRef,
            DecomposeAttrContext context)
        {
            return DecomposeAttr(midAttrRef.Decl, context);
        }

        private AttrInfo DecomposeAttrImpl(
            MidLit<Int32> exp,
            DecomposeAttrContext context)
        {
            return new LitInfo((float) exp.Value);
        }

        private void SetSource(
            ref MidExp srcExp,
            MidAttributeDecl attr)
        {
            if( srcExp != null )
            {
                if (srcExp is MidAttributeRef)
                {
                    var srcAttr = ((MidAttributeRef)srcExp).Decl;
                    if (srcAttr != attr)
                    {
                        throw new NotImplementedException();
                    }
                }
                else
                {
                    throw new NotImplementedException();
                }
            }

            srcExp = new MidAttributeRef(attr, new LazyFactory());
        }

        private AttrInfo DecomposeAttrImpl(
            MidAttributeFetch exp,
            DecomposeAttrContext context)
        {
            string obj = exp.Obj.ToString();

            if (obj == "__ps2om")
            {
                // The operand is then a source to the
                // blending operation

                var srcAttr = exp.Attribute;

                switch (context.flavor)
                {
                case AttrCase.Color:
                    SetSource(ref _renderTargetSources[context.index].colorExp, srcAttr);
                    return new SrcInfo();
                case AttrCase.Alpha:
                    SetSource(ref _renderTargetSources[context.index].alphaExp, srcAttr);
                    return new FactorInfo(D3D11_BLEND.D3D11_BLEND_SRC_ALPHA);
                case AttrCase.Combined:
                    SetSource(ref _renderTargetSources[context.index].combinedExp, srcAttr);
                    return new SrcInfo();
                default:
                    throw new NotImplementedException();
                }
            }
            else if (obj == "OM_Dest")
            {
                switch (context.flavor)
                {
                case AttrCase.Color:
                    return new FactorInfo(D3D11_BLEND.D3D11_BLEND_DEST_COLOR);
                case AttrCase.Alpha:
                    return new FactorInfo(D3D11_BLEND.D3D11_BLEND_DEST_ALPHA);
                case AttrCase.Combined:
                    return new FactorInfo(
                        D3D11_BLEND.D3D11_BLEND_DEST_COLOR,
                        D3D11_BLEND.D3D11_BLEND_DEST_ALPHA);
                default:
                    throw new NotImplementedException();
                }
            }
            else
            {
                throw new NotImplementedException();
            }

        }

        private AttrInfo DecomposeAttrImpl(
            MidBuiltinApp app,
            DecomposeAttrContext context)
        {
            string template = app.Decl.GetTemplate("blend");
            if (template == null)
            {
                throw new NotImplementedException();
            }

            var args = app.Args.ToArray();
            switch (template)
            {
            case "AddFloat4":
                {
                    var left = DecomposeAttr(args[0], context);
                    var right = DecomposeAttr(args[1], context);
                    return new OpInfo(
                        D3D11_BLEND_OP.D3D11_BLEND_OP_ADD,
                        left,
                        right);
                }

            case "MulFloat4": {
                var left = DecomposeAttr(args[0], context);
                var right = DecomposeAttr(args[1], context);
                return new TermInfo(left, right);
                }
            case "MulFloat_Float4": {
                var left = DecomposeAttr(args[0], context.SwitchToAlpha());
                var right = DecomposeAttr(args[1], context);
                return new TermInfo(left, right);
                }
            case "MulFloat4_Float": {
                var left = DecomposeAttr(args[0], context);
                var right = DecomposeAttr(args[1], context.SwitchToAlpha());
                return new TermInfo(left, right);
                }
            case "GetAlpha": {
                var arg = DecomposeAttr(args[0], context.SwitchToAlpha());
                return arg;
                }

            case "SubFloat":
                {
                    var left = DecomposeAttr(args[0], context);
                    var right = DecomposeAttr(args[1], context);
                    return new SubInfo( left, right );
                }

            case "IntToFloat":
                {
                    var arg = DecomposeAttr(args[0], context);
                    return arg;
                }
            default:
                throw new NotImplementedException();
            }
        }

        //

        TargetBlendDesc GetTargetBlendDesc(AttrInfo info)
        {
            return GetTargetBlendDescImpl((dynamic)info);
        }

        TargetBlendDesc GetTargetBlendDescImpl(SrcInfo info)
        {
            TargetBlendDesc result;
            result.blendEnable = false;
            result.color.srcBlend = D3D11_BLEND.D3D11_BLEND_ONE;
            result.color.destBlend = D3D11_BLEND.D3D11_BLEND_ZERO;
            result.color.op = D3D11_BLEND_OP.D3D11_BLEND_OP_ADD;
            result.alpha.srcBlend = D3D11_BLEND.D3D11_BLEND_ONE;
            result.alpha.destBlend = D3D11_BLEND.D3D11_BLEND_ZERO;
            result.alpha.op = D3D11_BLEND_OP.D3D11_BLEND_OP_ADD;
            result.writeMask = (UInt32)D3D11_COLOR_WRITE_ENABLE.D3D11_COLOR_WRITE_ENABLE_ALL;
            return result;
        }

        TargetBlendDesc GetTargetBlendDescImpl(OpInfo info)
        {
            TargetBlendDesc result;
            result.blendEnable = true;
            result.color = GetChannelBlendDesc(info, AttrCase.Color);
            result.alpha = GetChannelBlendDesc(info, AttrCase.Alpha);
            result.writeMask = (UInt32)D3D11_COLOR_WRITE_ENABLE.D3D11_COLOR_WRITE_ENABLE_ALL;
            return result;
        }

        ChannelBlendDesc GetChannelBlendDesc(
            AttrInfo info,
            AttrCase channel)
        {
            return GetChannelBlendDescImpl((dynamic)info, channel);
        }

        enum TermFlavor
        {
            Src,
            Dest,
            Either,
            None,
        }


        ChannelBlendDesc GetChannelBlendDescImpl(
            OpInfo info,
            AttrCase channel)
        {
            // \todo: Need to pick whether left or right is src/dest
            ChannelBlendDesc result;

            var left = info._left;
            var right = info._right;
            if (CanUseAs(left, channel, TermFlavor.Src) && CanUseAs(right, channel, TermFlavor.Dest))
            {
                result.srcBlend = GetTermBlendDesc(info._left, channel, TermFlavor.Src);
                result.destBlend = GetTermBlendDesc(info._right, channel, TermFlavor.Dest);
            }
            else if (CanUseAs(left, channel, TermFlavor.Dest) && CanUseAs(right, channel, TermFlavor.Src))
            {
                result.destBlend = GetTermBlendDesc(info._left, channel, TermFlavor.Dest);
                result.srcBlend = GetTermBlendDesc(info._right, channel, TermFlavor.Src);
            }
            else
            {
                throw new NotImplementedException();
            }
            result.op = info._op;
            return result;
        }

        bool CanUseAs(
            AttrInfo info,
            AttrCase channel,
            TermFlavor flavor)
        {
            if (info.IsFactor(flavor, channel))
                return true;
            return CanUseAsImpl((dynamic)info, channel, flavor);
        }

        bool CanUseAsImpl(
            TermInfo info,
            AttrCase channel,
            TermFlavor flavor)
        {
            var left = info._left;
            var right = info._right;
            bool canUseLeft = CanUseAs(left, channel, flavor);
            bool canUseRight = CanUseAs(right, channel, flavor);
            return canUseLeft || canUseRight;
        }

        bool CanUseAsImpl(
            AttrInfo info,
            AttrCase channel,
            TermFlavor flavor)
        {
            return info.IsFactor(flavor, channel);
        }

        D3D11_BLEND GetTermBlendDesc(
            AttrInfo info,
            AttrCase channel,
            TermFlavor flavor)
        {
            return GetTermBlendDescImpl((dynamic)info, channel, flavor);
        }

        D3D11_BLEND GetTermBlendDescImpl(
            TermInfo info,
            AttrCase channel,
            TermFlavor flavor)
        {
            // Need to look for a match on our target type
            var left = info._left;
            var right = info._right;

            AttrInfo factor = null;
            if (left.IsFactor(flavor, channel))
            {
                factor = right;
            }
            else if (right.IsFactor(flavor, channel))
            {
                factor = left;
            }
            else
            {
                throw new NotImplementedException();
            }

            return GetFactorBlendDesc(
                factor,
                channel );
        }

        D3D11_BLEND GetFactorBlendDesc(
            AttrInfo info,
            AttrCase channel )
        {
            return GetFactorBlendDescImpl((dynamic)info, channel);
        }

        D3D11_BLEND GetFactorBlendDescImpl(
            FactorInfo info,
            AttrCase channel)
        {
            switch (channel)
            {
                case AttrCase.Color:
                    return info._color;
                case AttrCase.Alpha:
                    return info._alpha;
                default:
                    throw new NotImplementedException();
            }
        }

        D3D11_BLEND GetFactorBlendDescImpl(
            SubInfo info,
            AttrCase channel)
        {
            return GetSubFactorBlendDescImpl(
                (dynamic)info._left,
                (dynamic)info._right,
                channel);
        }

        D3D11_BLEND GetSubFactorBlendDescImpl(
            LitInfo left,
            AttrInfo right,
            AttrCase channel)
        {
            var value = left._value;
            if (value != 1.0f)
            {
                throw new NotImplementedException();
            }

            var factor = GetFactorBlendDesc(right, channel);
            switch( factor )
            {
            case D3D11_BLEND.D3D11_BLEND_SRC_COLOR:
                    return D3D11_BLEND.D3D11_BLEND_INV_SRC_COLOR;
            case D3D11_BLEND.D3D11_BLEND_SRC_ALPHA:
                    return D3D11_BLEND.D3D11_BLEND_INV_SRC_ALPHA;
            case D3D11_BLEND.D3D11_BLEND_DEST_ALPHA:
                    return D3D11_BLEND.D3D11_BLEND_INV_DEST_ALPHA;
            case D3D11_BLEND.D3D11_BLEND_DEST_COLOR:
                    return D3D11_BLEND.D3D11_BLEND_INV_DEST_COLOR;
            case D3D11_BLEND.D3D11_BLEND_BLEND_FACTOR:
                    return D3D11_BLEND.D3D11_BLEND_INV_BLEND_FACTOR;
            case D3D11_BLEND.D3D11_BLEND_SRC1_COLOR:
                    return D3D11_BLEND.D3D11_BLEND_INV_SRC1_COLOR;
            case D3D11_BLEND.D3D11_BLEND_SRC1_ALPHA:
                    return D3D11_BLEND.D3D11_BLEND_INV_SRC1_ALPHA;
            default:
                throw new NotImplementedException();
            }
        }

        D3D11_BLEND GetSubFactorBlendDescImpl(
            AttrInfo left,
            AttrInfo right,
            AttrCase channel)
        {
            throw new NotImplementedException();
        }

        //



        public override void EmitImplSetup()
        {
            var uniformElement = GetElement("Uniform");
            var rasterVertex = GetElement("RasterVertex");
            var fragmentElement = GetElement("Fragment");
            var pixelElement = GetElement("Pixel");

            // Find all render targets:

            renderTargetAttributes = (from a in pixelElement.Attributes
                                      where a.Exp != null
                                      where a.IsOutput
                                      select a).ToArray();
            renderTargetCount = renderTargetAttributes.Length;

            // Depth-stencil view

            depthStencilViewAttribute = GetAttribute(uniformElement, "depthStencilView");


            // Compute the setup required by the OM


            // Blending stuff
            var blendStateType = EmitTarget.GetOpaqueType("ID3D11BlendState*");

            blendStateField = EmitClass.AddPrivateField(
                blendStateType,
                "_blendState");


            _renderTargetBlendDescs = new TargetBlendDesc[renderTargetCount];
            _renderTargetSources = new SourceInfo[renderTargetCount];
            for (int ii = 0; ii < renderTargetCount; ++ii)
            {
                DecomposeAttr(renderTargetAttributes[ii], ii);
            }

            var rtBlendDescType = EmitTarget.GetBuiltinType("D3D11_RENDER_TARGET_BLEND_DESC");
            var blendSpecVals = (from desc in _renderTargetBlendDescs
                                 select InitBlock.Struct(
                                    "D3D11_RENDER_TARGET_BLEND_DESC",
                                    InitBlock.LiteralBool(desc.blendEnable),
                                    InitBlock.Enum32(desc.color.srcBlend),
                                    InitBlock.Enum32(desc.color.destBlend),
                                    InitBlock.Enum32(desc.color.op),
                                    InitBlock.Enum32(desc.alpha.srcBlend),
                                    InitBlock.Enum32(desc.alpha.destBlend),
                                    InitBlock.Enum32(desc.alpha.op),
                                    InitBlock.LiteralU32(desc.writeMask))).ToList();
            while (blendSpecVals.Count < 8) // \todo: get the limits from somwhere!!!
            {
                blendSpecVals.Add(
                    InitBlock.Struct(
                        "D3D11_RENDER_TARGET_BLEND_DESC",
                        InitBlock.LiteralBool(false),
                        InitBlock.Enum32("D3D11_BLEND", "D3D11_BLEND_ONE", D3D11_BLEND.D3D11_BLEND_ONE),
                        InitBlock.Enum32("D3D11_BLEND", "D3D11_BLEND_ZERO", D3D11_BLEND.D3D11_BLEND_ZERO),
                        InitBlock.Enum32("D3D11_BLEND_OP", "D3D11_BLEND_OP_ADD", D3D11_BLEND_OP.D3D11_BLEND_OP_ADD),
                        InitBlock.Enum32("D3D11_BLEND", "D3D11_BLEND_ONE", D3D11_BLEND.D3D11_BLEND_ONE),
                        InitBlock.Enum32("D3D11_BLEND", "D3D11_BLEND_ZERO", D3D11_BLEND.D3D11_BLEND_ZERO),
                        InitBlock.Enum32("D3D11_BLEND_OP", "D3D11_BLEND_OP_ADD", D3D11_BLEND_OP.D3D11_BLEND_OP_ADD),
                        InitBlock.LiteralU32((UInt32)D3D11_COLOR_WRITE_ENABLE.D3D11_COLOR_WRITE_ENABLE_ALL)));
            }

            var blendSpecsVal = InitBlock.Array(
                rtBlendDescType,
                blendSpecVals);


            InitBlock.AppendComment("D3D11 Output Merger");
            var blendDescVal =
                InitBlock.Temp("blendDesc",
                    InitBlock.Struct(
                        "D3D11_BLEND_DESC",
                        InitBlock.LiteralBool(false),
                        InitBlock.LiteralBool(true),
                        blendSpecsVal));

            InitBlock.SetArrow(
                CtorThis,
                blendStateField,
                EmitTarget.GetNullPointer(blendStateType));

            InitBlock.CallCOM(
                CtorDevice,
                "ID3D11Device",
                "CreateBlendState",
                blendDescVal.GetAddress(),
                InitBlock.GetArrow(CtorThis, blendStateField).GetAddress());

            DtorBlock.CallCOM(
                DtorBlock.GetArrow(DtorThis, blendStateField),
                "IUnknown",
                "Release");






            // Emit HLSL code for PS

            InitBlock.AppendComment("D3D11 Pixel Shader");

            hlslContext = new EmitContextHLSL(SharedHLSL, Range, this.EmitClass.GetName());

            var entryPointSpan = hlslContext.EntryPointSpan;

            entryPointSpan.WriteLine("void main(");

            bool firstParam = true;
            hlslContext.DeclareConnectorAndBind(
                rasterVertex,
                GetAttribute(fragmentElement, "__rv2f"),
                ref firstParam,
                entryPointSpan);

            for (int ii = 0; ii < renderTargetCount; ++ii)
            {
                if( !firstParam ) entryPointSpan.WriteLine(",");
                firstParam = false;

                var sourceInfo = _renderTargetSources[ii];
                MidExp exp = null;
                if (sourceInfo.combinedExp != null)
                {
                    // \todo: Validate other bits and bobs!!!
                    exp = sourceInfo.combinedExp;
                }
                else
                {
                    throw new NotImplementedException();
                }

                entryPointSpan.Write("\tout {1} target{0} : SV_Target{0}", ii,
                    hlslContext.EmitType(exp.Type));
            }
            entryPointSpan.WriteLine(" )");
            entryPointSpan.WriteLine("{");


            hlslContext.EmitTempRecordCtor(
                entryPointSpan,
                fragmentElement,
                GetAttribute(pixelElement, "__ps2om"));

            for (int ii = 0; ii < renderTargetCount; ++ii)
            {
                var sourceInfo = _renderTargetSources[ii];
                MidExp exp = null;
                if (sourceInfo.combinedExp != null)
                {
                    // \todo: Validate other bits and bobs!!!
                    exp = sourceInfo.combinedExp;
                }
                else
                {
                    throw new NotImplementedException();
                }

                entryPointSpan.WriteLine("\ttarget{0} = {1};",
                    ii,
                    hlslContext.EmitExp(exp, entryPointSpan));
            }

            entryPointSpan.WriteLine("}");

            hlslContext.EmitConstantBufferDecl();

            //

            EmitShaderSetup(
                hlslContext,
                "ps_5_0",
                "Pixel",
                "PS");
        }

        public void EmitImplBindOM()
        {
            ExecBlock.AppendComment( "D3D11 Output Merger" );

            var renderTargetViewVals = (from a in renderTargetAttributes
                                        select EmitContext.EmitAttributeRef( a, ExecBlock, SubmitEnv )).ToArray();
            var renderTargetViewsVal = ExecBlock.Temp(
                "renderTargetViews",
                ExecBlock.Array(
                    EmitTarget.GetOpaqueType( "ID3D11RenderTargetView*" ),
                    renderTargetViewVals ) );

            ExecBlock.CallCOM(
                SubmitContext,
                "ID3D11DeviceContext",
                "OMSetRenderTargets",
                ExecBlock.LiteralU32( (UInt32) renderTargetCount ),
                renderTargetViewsVal.GetAddress(),
                EmitContext.EmitAttributeRef( depthStencilViewAttribute, ExecBlock, SubmitEnv ) );

            var floatOne = ExecBlock.LiteralF32( 1.0f );
            var blendFactorVal = ExecBlock.Temp(
                "blendFactor",
                ExecBlock.Array(
                    EmitTarget.GetBuiltinType( "float" ),
                    new IEmitVal[] { floatOne, floatOne, floatOne, floatOne } ) );

            ExecBlock.CallCOM(
                SubmitContext,
                "ID3D11DeviceContext",
                "OMSetBlendState",
                ExecBlock.GetArrow( SubmitThis, blendStateField ),
                blendFactorVal.GetAddress(),
                ExecBlock.LiteralU32( 0xFFFFFFFF ) );
        }

        public override void EmitImplBind()
        {
            ExecBlock.AppendComment( "D3D11 Pixel Shader" );

            EmitShaderBind(
                hlslContext,
                "ps_5_0",
                "Pixel",
                "PS" );
        }
    }
}
