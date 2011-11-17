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

using Spark.Mid;
using Spark.Emit.D3D11;

namespace Spark.Emit
{
    public class PassEmitContext
    {
        public EmitContext EmitContext { get; set; }
        public MidPipelineDecl MidPass { get; set; }

        public HLSL.SharedContextHLSL SharedHLSL { get; set; }
        public IEmitBlock InitBlock { get; set; }
        public IEmitBlock ExecBlock { get; set; }
        public IEmitBlock DtorBlock { get; set; }
        public IEmitClass EmitClass { get; set; }
        public IEmitVal CtorDevice { get; set; }
        public IEmitVal SubmitContext { get; set; }
        public IEmitVal CtorThis { get; set; }
        public IEmitVal SubmitThis { get; set; }
        public IEmitVal DtorThis { get; set; }
        public IEmitField CBField { get; set; }
        public EmitEnv SubmitEnv { get; set; }

        public EmitEnv ShaderClassEnv { get; set; }
        public EmitContext.ShaderClassInfo ShaderClassInfo { get; set; }

        public IEmitVal VertexShaderBytecodeVal { get; set; }
        public IEmitVal VertexShaderBytecodeSizeVal { get; set; }
    }

    public class EmitEnv
    {
        public EmitEnv(
            EmitEnv outer)
        {
            _outer = outer;
        }

        public void Insert(
            Mid.MidAttributeDecl attribute,
            Func<IEmitBlock, IEmitVal> generator)
        {
            _attributes[attribute] = generator;
        }

        public bool ContainsKey(
            Mid.MidAttributeDecl attribute )
        {
            if( _attributes.ContainsKey(attribute) )
                return true;

            if( _outer != null )
                return _outer.ContainsKey(attribute);

            return false;
        }

        public IEmitVal Lookup(
            Mid.MidAttributeDecl attribute,
            IEmitBlock block )
        {
            Func<IEmitBlock, IEmitVal> generator;
            if (_attributes.TryGetValue(attribute, out generator))
            {
                return generator(block);
            }

            if (_outer != null)
                return _outer.Lookup(attribute, block);

            throw new KeyNotFoundException();
        }

        public void Insert(
            MidVar var,
            Func<IEmitBlock, IEmitVal> generator)
        {
            _vars[var] = generator;
        }

        public bool ContainsKey(
            MidVar var)
        {
            if (_vars.ContainsKey(var))
                return true;

            if (_outer != null)
                return _outer.ContainsKey(var);

            return false;
        }

        public IEmitVal Lookup(
            MidVar var,
            IEmitBlock block)
        {
            Func<IEmitBlock, IEmitVal> generator;
            if (_vars.TryGetValue(var, out generator))
            {
                return generator(block);
            }

            if (_outer != null)
                return _outer.Lookup(var, block);

            throw new KeyNotFoundException();
        }

        private EmitEnv _outer;
        private Dictionary<Mid.MidAttributeDecl, Func<IEmitBlock, IEmitVal>> _attributes =
            new Dictionary<MidAttributeDecl, Func<IEmitBlock, IEmitVal>>();
        private Dictionary<MidVar, Func<IEmitBlock, IEmitVal>> _vars =
            new Dictionary<MidVar, Func<IEmitBlock, IEmitVal>>();
    }

    public class EmitContext
    {
        public EmitContext()
        {
        }

        public string OutputName
        {
            get
            {
                if (_outputName == null)
                    _outputName = "Output";
                return _outputName;
            }
            set { _outputName = value; }
        }

        public IEmitTarget Target
        {
            get { return _target; }
            set { _target = value; }
        }

        public IdentifierFactory Identifiers { get; set; }
        public IDiagnosticsCollection Diagnostics { get; set; }

        private IEmitModule _module;
        private EmitEnv _moduleEnv;

        public IEmitModule EmitModule(
            MidModuleDecl midModule )
        {
            IEmitModule emitModule = _target.CreateModule(OutputName);

            _module = emitModule;
            _moduleEnv = new EmitEnv(null);

            foreach (var p in midModule.Pipelines)
                EmitPipeline(emitModule, p);

            return emitModule;
        }

        public class ShaderAttributeInfo
        {
            public MidAttributeDecl AttributeDecl { get; set; }
            public Func<IEmitBlock, IEmitVal, IEmitVal> Accessor { get; set; }
        }

        public class ShaderFacetMixinInfo
        {
            public ShaderFacetMixinInfo(
                MidPipelineDecl originalClass,
                IEmitField mixinField)
            {
                this.OriginalClass = originalClass;
                this.MixinField = mixinField;
            }

            public MidPipelineDecl OriginalClass { get; set; }
            public IEmitField MixinField { get; set; }
        }

        public class ShaderFacetInfo
        {
            public MidPipelineDecl OriginalClass { get; set; }
            public Func<IEmitBlock, IEmitVal, IEmitVal> FacetAccessor { get; set; }
            public ShaderAttributeInfo[] Attributes { get; set; }

            public readonly List<ShaderFacetMixinInfo> Mixins = new List<ShaderFacetMixinInfo>();

            public void BindAttribute(
                MidFacetDecl midFacet,
                MidAttributeDecl midAttr,
                Func<IEmitBlock, IEmitVal, IEmitVal> accessor)
            {
                int ii = 0;
                foreach (var a in midFacet.Attributes)
                {
                    if (a == midAttr)
                    {
                        Attributes[ii] = new ShaderAttributeInfo
                        {
                            AttributeDecl = midAttr,
                            Accessor = accessor,
                        };
                        return;
                    }

                    ++ii;
                }
                throw new NotImplementedException();
            }
        }


        public class ShaderClassInfo
        {
            public MidPipelineDecl MidClassDecl { get; set; }
            public IEmitClass InterfaceClass { get; set; }
            public IEmitClass ImplementationClass { get; set; }


            public ShaderFacetInfo DirectFacet { get; set; }
            public ShaderFacetInfo[] InheritedFacets { get; set; }

            public IEnumerable<ShaderFacetInfo> AllFacets
            {
                get
                {
                    yield return DirectFacet;
                    foreach (var f in InheritedFacets)
                        yield return f;
                }
            }
        };

        private Dictionary<MidPipelineDecl, ShaderClassInfo> _mapShaderClassToInfo = new Dictionary<MidPipelineDecl, ShaderClassInfo>();

        private ShaderFacetInfo CreateDirectFacet(
            MidFacetDecl midFacet,
            MidElementDecl constantElement,
            MidElementDecl uniformElement,
            IEmitClass ifaceClass,
            EmitEnv env)
        {
            var midAttributes = midFacet.Attributes.ToArray();
            var attributeCount = midAttributes.Length;
            var attributeInfos = new ShaderAttributeInfo[attributeCount];

            var result = new ShaderFacetInfo
            {
                OriginalClass = midFacet.OriginalShaderClass.Decl,
                FacetAccessor = (b,shaderObj) => shaderObj,
                Attributes = attributeInfos
            };

            for( int ii = 0; ii < attributeCount; ++ii )
            {
                var midAttribute = midAttributes[ii];
                var midElement = midAttribute.Element;

                if (midAttribute.IsAbstract)
                    continue;

                if (midElement == constantElement)
                {
                    if (midAttribute.Exp == null)
                        continue;

                    env.Insert(midAttribute,
                        (b) => EmitExp(midAttribute.Exp, b, env));
                }
                else if (midElement == uniformElement)
                {
                    if (midAttribute.Exp != null)
                        continue;

                    if (!midAttribute.IsInput)
                        continue;

                    var attrType = EmitType(midAttribute.Type, env);
                    var attrName = midAttribute.Name.ToString();

                    var attrField = ifaceClass.AddFieldAndAccessors(
                        attrType,
                        attrName);

                    var attrInfo = new ShaderAttributeInfo();
                    attrInfo.AttributeDecl = midAttribute;
                    attrInfo.Accessor =
                        (b, shaderObj) => b.GetArrow(
                            shaderObj,
                            attrField);

                    attributeInfos[ii] = attrInfo;
                }
            }

            return result;
        }

        private MidFacetDecl GetFacetForBase(
            MidPipelineDecl classDecl,
            MidPipelineDecl baseDecl)
        {
            foreach (var f in classDecl.Facets)
            {
                if (f.OriginalShaderClass.Decl == baseDecl)
                    return f;
            }

            throw new NotImplementedException();
        }

        private void SetFacetForBase(
            ShaderClassInfo classInfo,
            ShaderFacetInfo facetInfo)
        {
            int ii = -1; // start at -1 since first facet is direct, not inherited
            foreach (var f in classInfo.MidClassDecl.Facets)
            {
                if (f.OriginalShaderClass.Decl == facetInfo.OriginalClass)
                {
                    classInfo.InheritedFacets[ii] = facetInfo;
                    return;
                }
                ++ii;
            }
        }

        private void AddBaseFacet(
            ShaderClassInfo classInfo,
            ShaderFacetInfo baseFacet,
            Func<IEmitBlock, IEmitVal, IEmitVal> baseFacetAccessor)
        {
            var derivedFacetDecl = GetFacetForBase(
                classInfo.MidClassDecl,
                baseFacet.OriginalClass);

            var baseAttrs = baseFacet.Attributes;
            var baseAttrCount = baseAttrs.Length;

            var derivedAttrs = new ShaderAttributeInfo[baseAttrCount];
            var derivedFacetInfo = new ShaderFacetInfo
            {
                OriginalClass = baseFacet.OriginalClass,
                FacetAccessor = baseFacetAccessor,
                Attributes = derivedAttrs,
            };

            var derivedFacetAttrDecls = derivedFacetDecl.Attributes.ToArray();

            for (int ii = 0; ii < baseAttrCount; ++ii)
            {
                var baseAttrInfo = baseAttrs[ii];
                if (baseAttrInfo == null)
                    continue;

                var baseAttrAccessor = baseAttrInfo.Accessor;

                var derivedAttrDecl = derivedFacetAttrDecls[ ii ];

                var attrInfo = new ShaderAttributeInfo
                {
                    AttributeDecl = derivedAttrDecl,
                    Accessor = (b, shaderObj) =>
                        baseAttrAccessor(b,
                            derivedFacetInfo.FacetAccessor(b, shaderObj)),
                };

                derivedFacetInfo.Attributes[ii] = attrInfo;
            }

            derivedFacetInfo.Mixins.AddRange(baseFacet.Mixins);

            SetFacetForBase(
                classInfo,
                derivedFacetInfo);
        }

        private void AddPrimaryBaseFacet(
            ShaderClassInfo classInfo,
            ShaderClassInfo primaryBaseInfo,
            Func<IEmitBlock, IEmitVal, IEmitVal> baseAccessor )
        {
            foreach (var bf in primaryBaseInfo.AllFacets)
            {
                var baseFacet = bf; // avoid capture

                AddBaseFacet(
                    classInfo,
                    baseFacet,
                    (b, shaderObj) =>
                        baseFacet.FacetAccessor(b,
                            baseAccessor(b, shaderObj)));
            }
        }

        static IEnumerable<MidAttributeDecl> FindAttribute(
            MidPipelineDecl midShaderClass,
            string elementName,
            string attributeName )
        {
            foreach( var a in
                (from e in midShaderClass.Elements
                 where e.Name.ToString() == elementName
                 from a in e.Attributes
                 where a.Name.ToString() == attributeName
                 select a) )
            {
                yield return a;
            }
        }

        private void EmitPipeline(
            IEmitModule emitModule,
            MidPipelineDecl midPipeline)
        {
            var range = midPipeline.Range;

            // Create the class to represent the pipeline

            EmitClassFlags ifaceFlags = EmitClassFlags.None;
            EmitClassFlags implFlags = EmitClassFlags.Hidden | EmitClassFlags.Implementation;

            if( !midPipeline.IsPrimary )
            {
                ifaceFlags |= EmitClassFlags.Mixin;
                // The "impl" class is treated as always primary
            }

            // this is a terrible horrible hack to detect a stdlib clas
            // (which means we *don't* want it in the generated header...
            var className = midPipeline.Name.ToString();
            if (className.StartsWith("D3D11"))
            {
                ifaceFlags |= EmitClassFlags.Internal;
                implFlags |= EmitClassFlags.Internal;
                className = string.Format("spark::d3d11::{0}", className);
            }

            var baseFacets = (from f in midPipeline.Facets
                               where f != midPipeline.DirectFacet
                              select f).ToArray();

            var primaryBaseFacet = (from f in baseFacets
                                    where f.OriginalShaderClass.Decl.IsPrimary
                                    select f).FirstOrDefault();

            ShaderClassInfo primaryBaseInfo = null;
            IEmitClass primaryBaseClass = null;
            if (primaryBaseFacet != null)
            {
                primaryBaseInfo = _mapShaderClassToInfo[primaryBaseFacet.OriginalShaderClass.Decl];
                primaryBaseClass = primaryBaseInfo.InterfaceClass;
            }

            var pipelineEnv = new EmitEnv(_moduleEnv);
            IEmitClass ifaceClass = emitModule.CreateClass(
                className,
                midPipeline.IsPrimary ? primaryBaseClass : null,
                ifaceFlags);

            var info = new ShaderClassInfo
            {
                MidClassDecl = midPipeline,
                InheritedFacets = new ShaderFacetInfo[ midPipeline.Facets.Count() - 1 ],
            };
            info.InterfaceClass = ifaceClass;

            _mapShaderClassToInfo[midPipeline] = info;

            // Janky RTTI-like system: use teh name of the calss
            ifaceClass.WrapperWriteLine(
                "static const char* StaticGetShaderClassName() {{ return \"{0}\"; }}",
                className );


            // Before we create fields declared in Spark, we
            // first create the "hidden" fields that represent
            // an instance at run-time:
            if( primaryBaseInfo == null && midPipeline.IsPrimary)
            {
                // We need two fields here for any root-of-the-hierarchy
                // class (which should actually be declared off in a header somewhere).
                // The first is a "class info" pointer:
                ifaceClass.AddPrivateField(
                    Target.GetOpaqueType( "void*" ),
                    "__classInfo" );

                // The second is the reference-count field:
                ifaceClass.AddPrivateField(
                    Target.GetBuiltinType( "UINT" ),
                    "__referenceCount" );
            }

            // Create fields to represent the various attributes
            var constantElement = GetElement(midPipeline, "Constant");
            var uniformElement = GetElement(midPipeline, "Uniform");

            // Direct facet:
            var directFacetDecl = midPipeline.DirectFacet;
            var directFacetInfo = CreateDirectFacet(
                directFacetDecl,
                constantElement,
                uniformElement,
                ifaceClass,
                pipelineEnv);

            info.DirectFacet = directFacetInfo;

            // Base-class facet, if any
            if( primaryBaseInfo != null )
            {
                if( midPipeline.IsPrimary )
                {
                    AddPrimaryBaseFacet(
                        info,
                        primaryBaseInfo,
                        ( b, shaderObj ) => shaderObj );
                }
                else
                {
                    var fieldName = "_Base_" + primaryBaseInfo.MidClassDecl.Name.ToString();
                    var fieldType = primaryBaseInfo.InterfaceClass.Pointer();
                    var facetField = ifaceClass.AddPrivateField(
                        fieldType,
                        fieldName );

                    ifaceClass.WrapperWriteLine(
                        "{0} _StaticCastImpl( {0} ) {{ return {1}; }}",
                        fieldType.ToString(),
                        fieldName );

                    info.DirectFacet.Mixins.Add(
                        new ShaderFacetMixinInfo(
                            primaryBaseInfo.MidClassDecl,
                            facetField ) );

                    AddPrimaryBaseFacet(
                        info,
                        primaryBaseInfo,
                        ( b, shaderObj ) =>
                            b.GetArrow( shaderObj, facetField ) );
                }
            }


            // Inherited facets:
            foreach (var f in midPipeline.Facets)
            {
                // Clearly don't want to embed a facet for ourself...
                if (f == midPipeline.DirectFacet)
                    continue;

                // Primary classes will already be covered by
                // the inheritance chain...
                if (f.OriginalShaderClass.Decl.IsPrimary)
                {
                    continue;
                }

                // We may have inherited an interface
                // to this facet through our base...
                if (primaryBaseFacet != null)
                {
                    if (primaryBaseFacet.OriginalShaderClass.Decl.Facets.Any(
                        (otherF) => otherF.OriginalShaderClass.Decl == f.OriginalShaderClass.Decl))
                    {
                        continue;
                    }
                }

                // Otherwise we need a public field
                // to expose the facet:

                var facetClassDecl = f.OriginalShaderClass.Decl;
                var facetClassInfo = _mapShaderClassToInfo[facetClassDecl];

                // \todo: get pointer type... :(
                var fieldType = facetClassInfo.InterfaceClass.Pointer();

                var fieldName = string.Format(
                    "_Mixin_{0}",
                    f.OriginalShaderClass.Decl.Name.ToString());

                var field = ifaceClass.AddPrivateField(fieldType, fieldName);

                info.DirectFacet.Mixins.Add(
                    new ShaderFacetMixinInfo(
                        facetClassDecl,
                        field));

                ifaceClass.WrapperWriteLine(
                    "{0} _StaticCastImpl( {0} ) {{ return {1}; }}",
                    fieldType.ToString(),
                    fieldName);

                AddBaseFacet(
                    info,
                    facetClassInfo.DirectFacet,
                    (b, shaderObj) => b.GetArrow(shaderObj, field));
            }

            ifaceClass.WrapperWriteLine(
                "template<typename TBase>");
            ifaceClass.WrapperWriteLine(
                "TBase* StaticCast() { return _StaticCastImpl(static_cast<TBase*>(nullptr)); }");

            /*
            
            
            
            foreach (var a in constantElement.Attributes)
            {
                var attr = a; // Avoid capture.

                if (attr.Exp == null) continue;

                pipelineEnv.Insert(attr,
                    (b) => EmitExp(attr.Exp, b, pipelineEnv));
            }
            foreach (var a in uniformElement.Attributes)
            {
                var attr = a; // Avoid capture.

                if (attr.Exp != null) continue;

                var attrType = EmitType(attr.Type, pipelineEnv);
                var attrName = attr.Name.ToString();

                var attrField = emitClass.AddPublicField(attrType, attrName);

                pipelineEnv.Insert(attr,
                    (b) => b.GetArrow(
                        b.Method.ThisParameter,
                        attrField));
            }
            */

            var sharedHLSL = new HLSL.SharedContextHLSL(Identifiers, Diagnostics);
            var emitPass = new PassEmitContext()
            {
                EmitContext = this,
                MidPass = midPipeline,
                SharedHLSL = sharedHLSL,
                EmitClass = ifaceClass,
                ShaderClassEnv = pipelineEnv,
                ShaderClassInfo = info,
            };

            // Now emit stage-specific code.

            EmitStageInterface<D3D11VertexShader>(emitPass);
            EmitStageInterface<D3D11HullShader>(emitPass);
            EmitStageInterface<D3D11DomainShader>(emitPass);
            EmitStageInterface<D3D11GeometryShader>( emitPass );
            EmitStageInterface<D3D11PixelShader>( emitPass );
            // IA last since it generates the call to Draw*()
            EmitStageInterface<D3D11InputAssembler>(emitPass);


            // Do this *after* emitting the per-stage interface,
            // so that stages of the pipeline can bind attributes too.

            foreach (var f in info.AllFacets)
            {
                foreach (var a in f.Attributes)
                {
                    if (a == null)
                        continue;

                    var attr = a.AttributeDecl;
                    var accessor = a.Accessor;
                    pipelineEnv.Insert(
                        attr,
                        (b) => accessor(b, b.Method.ThisParameter));
                }
            }

            ifaceClass.Seal();

            if (midPipeline.IsAbstract)
            {
                return;
            }

            var implBase = ifaceClass;
            if (!midPipeline.IsPrimary)
            {
                implBase = primaryBaseClass;

                if (implBase == null)
                    throw new NotImplementedException();
            }


            var implClass = emitModule.CreateClass(
                className,
                implBase,
                implFlags);


            ifaceClass.WrapperWriteLine(
                "static const spark::ShaderClassDesc* GetShaderClassDesc();" );

            // 

            // The impl class needs a field to hold each of its mixin bases...
            Dictionary<MidPipelineDecl, Func<IEmitBlock, IEmitVal>> getFacetPointerForBase = new Dictionary<MidPipelineDecl, Func<IEmitBlock, IEmitVal>>();

            var facetInfoData = new List<IEmitVal>();
            var facetInfoCount = 0;

            foreach (var f in midPipeline.Facets)
            {
                // Clearly don't want to embed a facet for ourself...
                var facetClassDecl = f.OriginalShaderClass.Decl;

                if (facetClassDecl.IsPrimary)
                {
                    // Primary classes will already be covered by
                    // the inheritance chain...
                    getFacetPointerForBase[facetClassDecl] = (b) => b.Method.ThisParameter;

                    facetInfoData.Add( emitModule.LiteralString( _mapShaderClassToInfo[ facetClassDecl ].InterfaceClass.GetName() ));
                    facetInfoData.Add( Target.LiteralU32(0) );
                    facetInfoCount++;
                }
            }

            UInt32 facetOffset = ifaceClass.Size;
            foreach (var f in midPipeline.Facets)
            {
                // Clearly don't want to embed a facet for ourself...
                var facetClassDecl = f.OriginalShaderClass.Decl;

                if (!facetClassDecl.IsPrimary)
                {
                    // Mixin classes will be reflected as fields.
                    var facetClassInfo = _mapShaderClassToInfo[facetClassDecl];

                    var fieldType = facetClassInfo.InterfaceClass;

                    var fieldName = string.Format(
                        "_MixinImpl_{0}",
                        f.OriginalShaderClass.Decl.Name.ToString());

                    var field = implClass.AddPrivateField(fieldType, fieldName);
                    getFacetPointerForBase[facetClassDecl] = (b) => b.GetArrow(b.Method.ThisParameter, field).GetAddress();

                    facetInfoData.Add( emitModule.LiteralString(_mapShaderClassToInfo[ facetClassDecl ].InterfaceClass.GetName()) );
                    facetInfoData.Add( Target.LiteralU32(facetOffset) );
                    facetInfoCount++;

                    facetOffset += fieldType.Size;
                }
            }

            var facetInfoVal = emitModule.EmitGlobalStruct(
                null,
                facetInfoData.ToArray() ).GetAddress();

            //

            var deviceType = Target.GetOpaqueType("ID3D11Device*");
            var contextType = Target.GetOpaqueType("ID3D11DeviceContext*");

            // Create constructor

            var ctor = implClass.CreateCtor();
            var ctorDevice = ctor.AddParameter(
                deviceType,
                "device");

            var facetInitBlock = ctor.EntryBlock.InsertBlock();
            var cbInit = ctor.EntryBlock.InsertBlock();

            // First things first: wire up all the various facets to
            // the appropriate pointers:

            Dictionary<MidPipelineDecl, IEmitVal> initFacetPointers = new Dictionary<MidPipelineDecl, IEmitVal>();
            foreach (var p in getFacetPointerForBase)
            {
                var facet = p.Key;
                var accessor = p.Value;

                var val = accessor(facetInitBlock);

                initFacetPointers[facet] = val;
            }

            foreach (var f in info.AllFacets)
            {
                var facetPointer = initFacetPointers[f.OriginalClass];

                foreach (var m in f.Mixins)
                {
                    var mixinPointer = initFacetPointers[m.OriginalClass];

                    facetInitBlock.SetArrow(
                        facetPointer,
                        m.MixinField,
                        facetInitBlock.CastRawPointer(
                            mixinPointer,
                            m.MixinField.Type));
                }
            }


            // Create destructor

            var dtor = implClass.CreateDtor();

            var cbFinit = dtor.EntryBlock.InsertBlock();

            // Create Submit() method

            var submit = implClass.CreateMethod(
                Target.VoidType,
                "Submit");
            var submitDevice = submit.AddParameter(
                deviceType,
                "device");
            var submitContext = submit.AddParameter(
                contextType,
                "context");
            var submitEnv = new EmitEnv(pipelineEnv);

            var cbSubmit = submit.EntryBlock.InsertBlock();


            var cbPointerType = Target.GetOpaqueType("ID3D11Buffer*");
            var cbField = implClass.AddPrivateField(
                cbPointerType,
                "_cb");

            emitPass = new PassEmitContext()
            {
                EmitContext = this,
                MidPass = midPipeline,
                SharedHLSL = sharedHLSL,
                InitBlock = ctor.EntryBlock,
                ExecBlock = submit.EntryBlock,
                DtorBlock = dtor.EntryBlock,
                EmitClass = implClass,
                CtorDevice = ctorDevice,
                SubmitContext = submitContext,
                CtorThis = ctor.ThisParameter,
                SubmitThis = submit.ThisParameter,
                DtorThis = dtor.ThisParameter,
                CBField = cbField,
                SubmitEnv = submitEnv,
            };

            // Now emit stage-specific code.

            var iaStage = new D3D11InputAssembler() { EmitPass = emitPass, Range = range };
            var vsStage = new D3D11VertexShader()   { EmitPass = emitPass, Range = range };
            var hsStage = new D3D11HullShader()     { EmitPass = emitPass, Range = range };
            var dsStage = new D3D11DomainShader()   { EmitPass = emitPass, Range = range };
            var gsStage = new D3D11GeometryShader() { EmitPass = emitPass, Range = range };
            var psStage = new D3D11PixelShader()    { EmitPass = emitPass, Range = range };

            vsStage.EmitImplSetup();
            iaStage.EmitImplSetup(); // IA after VS for bytecode dependency
            hsStage.EmitImplSetup();
            dsStage.EmitImplSetup();
            gsStage.EmitImplSetup();
            psStage.EmitImplSetup();

            psStage.EmitImplBindOM(); // OM first
            iaStage.EmitImplBind(); // IA as early as possible
            vsStage.EmitImplBind();
            hsStage.EmitImplBind();
            dsStage.EmitImplBind();
            gsStage.EmitImplBind();
            psStage.EmitImplBind();

            iaStage.EmitImplDraw();

            // Generate code to fill out CB after all the
            // stage-specific shader stuff, since these are
            // what compute the required @Uniform values.

            var block = cbInit;
            var cbDescVal = block.Temp(
                "cbDesc",
                block.Struct(
                    "D3D11_BUFFER_DESC",
                    block.LiteralU32((UInt32) sharedHLSL.ConstantBufferSize),
                    block.Enum32("D3D11_USAGE", "D3D11_USAGE_DYNAMIC", D3D11Stage.D3D11_USAGE.D3D11_USAGE_DYNAMIC),
                    block.LiteralU32((UInt32) D3D11Stage.D3D11_BIND_FLAG.D3D11_BIND_CONSTANT_BUFFER),
                    block.LiteralU32((UInt32) D3D11Stage.D3D11_CPU_ACCESS_FLAG.D3D11_CPU_ACCESS_WRITE),
                    block.LiteralU32(0),
                    block.LiteralU32(0)));

            block.SetArrow(
                ctor.ThisParameter,
                cbField,
                cbPointerType.Null());
            block.CallCOM(
                ctorDevice,
                "ID3D11Device",
                "CreateBuffer",
                cbDescVal.GetAddress(),
                Target.GetBuiltinType("D3D11_SUBRESOURCE_DATA").Pointer().Null(),
                block.GetArrow(ctor.ThisParameter, cbField).GetAddress());

            block = cbSubmit;
            var mappedType = Target.GetBuiltinType("D3D11_MAPPED_SUBRESOURCE");
            var cbMappedVal = block.Local("_cbMapped", mappedType);

            block.CallCOM(
                submitContext,
                "ID3D11DeviceContext",
                "Map",
                block.GetArrow(submit.ThisParameter, cbField),
                block.LiteralU32(0),
                block.Enum32("D3D11_MAP", "D3D11_MAP_WRITE_DISCARD", D3D11Stage.D3D11_MAP.D3D11_MAP_WRITE_DISCARD),
                block.LiteralU32(0),
                cbMappedVal.GetAddress());


            IEmitVal mappedData = block.Temp(
                "cbMappedData",
                block.CastRawPointer(
                    block.GetBuiltinField(cbMappedVal, "pData", Target.GetOpaqueType("void*"))));

            foreach (var u in sharedHLSL.Uniforms)
            {
                var val = EmitExp(u.Val, block, pipelineEnv);
                block.StoreRaw(
                    mappedData,
                    (UInt32) u.ByteOffset,
                    val);
            }

            block.CallCOM(
                submitContext,
                "ID3D11DeviceContext",
                "Unmap",
                block.GetArrow(submit.ThisParameter, cbField),
                block.LiteralU32(0));

            cbFinit.CallCOM(
                cbFinit.GetArrow(dtor.ThisParameter, cbField),
                "ID3D11Buffer",
                "Release");

            implClass.Seal();

            // Now we need to constrct the class-info
            // structure, that will be used as a kind of
            // virtual function table at runtime.

            var classInfoVal = emitModule.EmitGlobalStruct(
                className,
                new IEmitVal[]{
                    Target.LiteralU32(implClass.Size),
                    Target.LiteralU32((UInt32) facetInfoCount),
                    facetInfoVal,
                    emitModule.GetMethodPointer( ctor ),
                    emitModule.GetMethodPointer( dtor ),
                    emitModule.GetMethodPointer( submit ),
                } );

            if (emitModule is Emit.CPlusPlus.EmitModuleCPP)
            {
                var moduleCPP = (Emit.CPlusPlus.EmitModuleCPP)emitModule;

                moduleCPP.SourceSpan.WriteLine(
                "const spark::ShaderClassDesc* {0}::GetShaderClassDesc() {{ return reinterpret_cast<const spark::ShaderClassDesc*>(&({1})); }}",
                ifaceClass,
                classInfoVal);
            }
        }

        private void EmitStageInterface<T>(
            PassEmitContext emitPass)
            where T : D3D11Stage, new()
        {
            var stage = new T() { EmitPass = emitPass };
            stage.EmitInterface();
        }

        public IEmitType EmitType(
            MidType midType,
            EmitEnv env )
        {
            return EmitTypeImpl((dynamic) midType, env);
        }

        private IEmitType EmitTypeImpl(
            MidBuiltinType midType,
            EmitEnv env)
        {
            IEmitTerm[] args = null;
            if (midType.Args != null)
            {
                args = (from a in midType.Args
                        select EmitGenericArg(a, env)).ToArray();
            }
            return Target.GetBuiltinType(
                midType.GetTemplate( Target.TargetName ),
                args );
        }

        private IEmitTerm EmitGenericArg(
            object arg,
            EmitEnv env )
        {
            if (arg is MidType)
                return EmitType((MidType)arg, env);
            else if (arg is MidExp)
                return EmitExp((MidExp)arg, null, env);
            else
                throw new NotImplementedException();
        }

        private IEmitType EmitTypeImpl(
            MidStructRef midType,
            EmitEnv env)
        {
            var midDecl = midType.Decl;

            return _structTypes.Cache(
                midDecl,
                () => EmitStructType(_module, midDecl));
        }

        private IEmitStruct EmitStructType(
            IEmitModule emitModule,
            MidStructDecl midDecl)
        {
            var result = emitModule.CreateStruct(midDecl.Name.ToString());
            // \todo: Fields, etc.
            return result;
        }

        private Dictionary<MidStructDecl, IEmitStruct> _structTypes = new Dictionary<MidStructDecl, IEmitStruct>();

        public IEmitVal EmitExp(
            MidExp exp,
            IEmitBlock block,
            EmitEnv env)
        {
            return EmitExpImpl((dynamic)exp, block, env);
        }

        private IEmitVal EmitExpImpl(
            MidLetExp exp,
            IEmitBlock block,
            EmitEnv env)
        {
            var letVar = exp.Var;

            var letVal = EmitExp(exp.Exp, block, env);

            var letEnv = new EmitEnv(env);
            letEnv.Insert(letVar, (b) => letVal);

            var bodyVal = EmitExp(exp.Body, block, letEnv);

            return bodyVal;
        }

        private IEmitVal EmitExpImpl(
            MidVarRef exp,
            IEmitBlock block,
            EmitEnv env)
        {
            return env.Lookup(exp.Var, block);
        }


        private IEmitVal EmitExpImpl(
            MidBuiltinApp app,
            IEmitBlock block,
            EmitEnv env)
        {
            var template = app.Decl.GetTemplate(Target.TargetName);
            if (template == null)
            {
                Diagnostics.Add(
                    Severity.Error,
                    new SourceRange(),
                    "No builtin function implementation defined for \"{0}\" for target \"{1}\"",
                    app.Decl.Name,
                    Target.TargetName);
                return block.Local("error", EmitType(app.Type, env));
            }
            var args = (from a in app.Args
                        select EmitExp(a, block, env)).ToArray();
            return block.BuiltinApp(
                EmitType(app.Type, env),
                template,
                args);
        }

        private IEmitVal EmitExpImpl(
            MidLit<UInt32> lit,
            IEmitBlock block,
            EmitEnv env)
        {
            return _target.LiteralU32(lit.Value);
        }

        private IEmitVal EmitExpImpl(
            MidLit<Int32> lit,
            IEmitBlock block,
            EmitEnv env)
        {
            return _target.LiteralS32(lit.Value);
        }

        private IEmitVal EmitExpImpl(
            MidLit<bool> lit,
            IEmitBlock block,
            EmitEnv env)
        {
            return _target.LiteralBool(lit.Value);
        }

        private IEmitVal EmitExpImpl(
            MidLit<float> lit,
            IEmitBlock block,
            EmitEnv env)
        {
            return _target.LiteralF32(lit.Value);
        }


        private IEmitVal EmitExpImpl(
            MidAttributeRef attrRef,
            IEmitBlock block,
            EmitEnv env )
        {
            return EmitAttributeRef(attrRef.Decl, block, env);
        }

        public IEmitVal EmitAttributeRef(
            MidAttributeWrapperDecl wrapper,
            IEmitBlock block,
            EmitEnv env )
        {
            return EmitAttributeRef(
                wrapper.Attribute,
                block,
                env );
        }

        public IEmitVal EmitAttributeRef(
            MidAttributeDecl decl,
            IEmitBlock block,
            EmitEnv env)
        {
            if( env.ContainsKey(decl) )
                return env.Lookup(decl, block);

            IEmitVal attributeVal = null;
            if (decl.Exp != null)
            {
                attributeVal = block.Temp(
                    decl.Name.ToString(),
                    EmitExp(decl.Exp, block, env));
            }
            else
            {
                // \todo: This is a *huge* hack,
                // since we use @Constant attributes
                // to make various D3D constants visible... :(
                attributeVal = block.BuiltinApp(
                   EmitType(decl.Type, env),
                   decl.Name.ToString(),
                   null);
            }

            env.Insert(decl, (b) => attributeVal);
            return attributeVal;
        }

        public MidElementDecl FindElement(
            MidPipelineDecl midPipeline,
            string name )
        {
            foreach( var e in midPipeline.Elements )
                if( e.Name.ToString() == name )
                    return e;

            return null;
        }

        public MidElementDecl GetElement(
            MidPipelineDecl midPipeline,
            string name)
        {
            var result = FindElement( midPipeline, name );
            if( result != null )
                return result;

            throw new KeyNotFoundException();
        }

        private string _outputName;
        private IEmitTarget _target;
    }

    public static class EmitContextExtensions
    {
        public static IEmitField AddFieldAndAccessors(
            this IEmitClass emitClass,
            IEmitType fieldType,
            string name)
        {
            var fieldName = "m_" + name;
            var field = emitClass.AddPrivateField(fieldType, fieldName );

            if (emitClass is Emit.CPlusPlus.EmitClassCPP)
            {
                var emitClassCPP = (Emit.CPlusPlus.EmitClassCPP)emitClass;

                var accessorName = CapitalizeFieldName(name);

                emitClassCPP.PublicSpan.WriteLine(
                    "{0} Get{1}() const {{ return {2}; }}",
                    fieldType.ToString(),
                    accessorName,
                    fieldName);

                emitClassCPP.PublicSpan.WriteLine(
                    "void Set{1}( {0} value ) {{ {2} = value; }}",
                    fieldType.ToString(),
                    accessorName,
                    fieldName);
            }

            return field;
        }

        private static string CapitalizeFieldName(
                    string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            if (!char.IsLower(name, 0))
                return name;

            return string.Format("{0}{1}",
                char.ToUpper(name[0]),
                name.Substring(1));
        }

        public static void WrapperWriteLine(
            this IEmitClass emitClass,
            string format,
            params object[] args)
        {
            if (emitClass is Emit.CPlusPlus.EmitClassCPP)
            {
                var emitClassCPP = (Emit.CPlusPlus.EmitClassCPP)emitClass;
                emitClassCPP.PublicSpan.WriteLine(format, args);
            }
        }

        public static void WrapperWriteLine(
            this IEmitClass emitClass,
            string value )
        {
            emitClass.WrapperWriteLine("{0}", value);
        }
    }
}
