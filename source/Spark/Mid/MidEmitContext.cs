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

using Spark.ResolvedSyntax;

namespace Spark.Mid
{
    public abstract class MidEmitEnv
    {
        public MidEmitEnv(
            MidEmitEnv parent)
        {
            _parent = parent;
        }

        public T MaybeMoveTemp<T>(MidExp exp, MidType type)
            where T : MidExp
        {
            if (exp is T)
                return (T) exp;

            return (T) (MidExp) CreateTemp(exp, type);
        }

        public abstract MidVal CreateTemp(MidExp exp, MidType type);

        public void Insert(ResLabel key, MidLabel value)
        {
            _labels[key] = value;
        }

        public MidLabel Lookup(ResLabel key)
        {
            MidLabel value;
            if (_labels.TryGetValue(key, out value))
                return value;

            if (_parent != null)
                return _parent.Lookup(key);

            throw new KeyNotFoundException();
        }

        public void Insert(IResVarDecl key, MidVal value)
        {
            _values[key] = value;
        }

        public MidVal Lookup(IResVarDecl key)
        {
            MidVal value;
            if (_values.TryGetValue(key, out value))
                return value;

            if (_parent != null)
                return _parent.Lookup(key);

            throw new KeyNotFoundException();
        }

        public void Insert(IResTypeParamDecl key, MidType value)
        {
            _types[key] = value;
        }

        public MidType Lookup(IResTypeParamDecl key)
        {
            MidType value;
            if (_types.TryGetValue(key, out value))
                return value;

            if (_parent != null)
                return _parent.Lookup(key);

            throw new KeyNotFoundException();
        }

        public void Insert(IResGenericParamDecl key, object value)
        {
            if (key is IResTypeParamDecl)
                Insert((IResTypeParamDecl)key, (MidType)value);
            else
                Insert((IResVarDecl)key, (MidVal)value);
        }

        public object Lookup(IResGenericParamDecl key)
        {
            if (key is IResTypeParamDecl)
                return Lookup( (IResTypeParamDecl) key );
            else
                return Lookup((IResVarDecl)key);
        }

        public virtual MidEmitContext Context
        {
            get { return _parent.Context; }
        }

        public ILazy<T> Lazy<T>(Func<T> generator)
        {
            return Context.Lazy<T>(generator);
        }

        protected MidEmitEnv _parent;
        private Dictionary<ResLabel, MidLabel> _labels = new Dictionary<ResLabel, MidLabel>();
        private Dictionary<IResVarDecl, MidVal> _values = new Dictionary<IResVarDecl, MidVal>();
        private Dictionary<IResTypeParamDecl, MidType> _types = new Dictionary<IResTypeParamDecl, MidType>();

        public IEnumerable<IResAttributeDecl> InheritedDecls
        {
            get
            {
                if (_inheritedDecls != null)
                    return _inheritedDecls;
                if (_parent != null)
                    return _parent.InheritedDecls;
                return null;
            }
            set { _inheritedDecls = value.ToArray(); }
        }

        private IResAttributeDecl[] _inheritedDecls;
    }

    class MidGlobalEmitEnv : MidEmitEnv
    {
        public MidGlobalEmitEnv(
            MidEmitEnv parent,
            MidEmitContext context )
            : base(parent)
        {
            _context = context;
        }

        public override MidVal CreateTemp(MidExp exp, MidType type)
        {
            throw new NotImplementedException();
        }

        public override MidEmitContext Context
        {
            get { return _context; }
        }

        private MidEmitContext _context;
    }

    class MidGenericArgEmitEnv : MidEmitEnv
    {
        public MidGenericArgEmitEnv(
            MidEmitEnv parent)
            : base(parent)
        {
        }

        public override MidVal CreateTemp(MidExp exp, MidType type)
        {
            return _parent.CreateTemp(exp, type);
        }
    }

    class MidDummyEmitEnv : MidEmitEnv
    {
        public MidDummyEmitEnv(
            MidEmitEnv parent)
            : base(parent)
        {
        }

        public override MidVal CreateTemp( MidExp exp, MidType type )
        {
            return _parent.CreateTemp( exp, type );
        }
    }

    class MidLocalEmitEnv : MidEmitEnv
    {
        private IdentifierFactory _identifiers;

        public MidLocalEmitEnv(
            MidEmitEnv parent,
            IdentifierFactory identifiers,
            MidElementDecl element,
            MidExpFactory exps )
            : base(parent)
        {
            _identifiers = identifiers;
            _element = element;
            _exps = exps;
        }

        public override MidVal CreateTemp( MidExp exp, MidType type )
        {
            if( _element != null )
            {
                var attrDecl = _element.CacheAttr( exp, type );
                return _exps.AttributeRef( attrDecl );
            }
            else
            {
                MidVar var = new MidVar( _identifiers.unique( "temp" ), type );

                Func<MidExp, MidExp> wrapper = ( body ) => new MidLetExp( var, exp, body );

                _wrappers.Insert( 0, wrapper );

                return new MidVarRef( var );
            }
        }

        public MidExp Wrap(MidExp body)
        {
            var result = body;
            foreach (var wrapper in _wrappers)
                result = wrapper(result);
            return result;
        }

        private MidElementDecl _element;
        private List<Func<MidExp, MidExp>> _wrappers = new List<Func<MidExp, MidExp>>();
        private MidExpFactory _exps;
    }

    public class MidEmitContext
    {
        private IdentifierFactory _identifiers;
        private MidExpFactory _exps;

        public MidEmitContext( IdentifierFactory identifiers )
        {
            _identifiers = identifiers;
            _exps = new MidExpFactory( _lazy );
        }

        private LazyFactory _lazy = new LazyFactory();
        public ILazy<T> Lazy<T>(Func<T> generator)
        {
            return _lazy.Lazy<T>(generator);
        }


        private MidModuleDecl _module;
        private Dictionary<IResModuleDecl, MidModuleDecl> _modules = new Dictionary<IResModuleDecl, MidModuleDecl>();

        public MidModuleDecl EmitModule(
            IResModuleDecl resModule )
        {
            var env = new MidGlobalEmitEnv(null, this);
            var midModule = new MidModuleDecl(null, this, env);
            _module = midModule;
            _modules[ resModule ] = midModule;
            foreach (var decl in resModule.Decls)
                EmitMemberDecl(midModule, decl, env);
            _module.DoneBuilding();
            _module.ForceDeep();

            _lazy.Force();

            var midSimplifyContext = new Mid.MidSimplifyContext( _exps );
            midSimplifyContext.SimplifyModule(midModule);

            MidMarkOutputs.MarkOutputs(midModule);

            var midScalarizeOutputs = new Mid.MidScalarizeOutputs(_identifiers, _exps);
            midScalarizeOutputs.ApplyToModule(midModule);

            // Do cleanup tasks
            (new MidCleanup(_exps)).ApplyToModule( midModule );
            MidMarkOutputs.UnmarkOutputs( midModule );
            MidMarkOutputs.MarkOutputs( midModule );

            return midModule;
        }

        private MidMemberDecl EmitMemberDeclImpl(
            MidModuleDecl midModule,
            IResPipelineDecl resPipeline,
            MidEmitEnv outerEnv)
        {
            var midPipeline = new MidPipelineDecl(
                midModule,
                resPipeline.Name,
                this,
                outerEnv,
                resPipeline.Range);

            midPipeline.IsAbstract = (resPipeline.ConcretenessMode == ResMemberConcretenessMode.Abstract);
            midPipeline.IsPrimary = (resPipeline.MixinMode == ResMixinMode.Primary);

            MidEmitEnv env = new MidGlobalEmitEnv(outerEnv, outerEnv.Context);
            env.Insert(
                resPipeline.ThisParameter,
                    new MidLit<object>(null, new MidPipelineRef(midPipeline, null)));
            midPipeline.Env = env;

            midPipeline.AddBuildAction(() =>
            {
                foreach (var resFacet in resPipeline.Facets)
                {
                    var originalPipeline = (MidPipelineRef)EmitMemberTerm(
                        resFacet.OriginalPipeline.MemberTerm,
                        env);
                    var midFacet = midPipeline.AddFacet(
                        originalPipeline);

                    foreach (var resLine in resFacet.MemberLines)
                    {
                        var resDecl = resLine.EffectiveDecl;
                        var midDecl = EmitMemberDecl(midFacet, resDecl, env);
                        midPipeline.InsertMemberDecl(resDecl, midDecl);
                    }
                    midFacet.DoneBuilding();
                }
            });
            midPipeline.DoneBuilding();

            midModule.AddPipeline(midPipeline);
            midModule.InsertMemberDecl(resPipeline, midPipeline);
            return midPipeline;
        }

        private MidMemberDecl EmitMemberDecl(
            object parent,
            IResMemberDecl resMemberDecl,
            MidEmitEnv env)
        {
            return EmitMemberDeclImpl(
                (dynamic)parent,
                (dynamic)resMemberDecl,
                env);
        }

        private MidMemberDecl EmitMemberDeclImpl(
            MidFacetDecl midFacet,
            IResElementDecl resElement,
            MidEmitEnv env)
        {
            var midElement = new MidElementDecl(midFacet, resElement.Name);
            midElement.DoneBuilding();

            midFacet.AddElement(midElement);

            return midElement;
        }

        private MidExp EmitAttrExp(
            MidElementDecl midElement,
            MidType midType,
            IResExp resExp,
            MidEmitEnv env )
        {
            var savedElement = _currentElement;
            _currentElement = midElement;

            var result = EmitLocalExp( resExp, env, midElement );

            _currentElement = savedElement;

            return result;
        }

        private MidMemberDecl EmitMemberDeclImpl(
            MidFacetDecl midFacet,
            IResAttributeDecl resAttrib,
            MidEmitEnv env)
        {
            var midAttrWrap = new MidAttributeWrapperDecl( midFacet, resAttrib.Name, resAttrib.Range);

            midAttrWrap.AddBuildAction( () =>
            {
                var fqType = resAttrib.Type;
                var freq = fqType.Freq;
                var type = fqType.Type;

                var midElement = (MidElementDecl) midFacet.Pipeline.LookupMemberDecl( freq.MemberTerm.Decl );
                var midType = EmitTypeExp( type, env );

                MidAttributeDecl midAttr = null;
                if( resAttrib.Init == null )
                {
                    midAttr = new MidAttributeDecl(
                        resAttrib.Name,
                        midElement,
                        midType,
                        null);
                    midElement.AddAttribute( midAttr );
                    if( resAttrib.Line.ConcretenessMode == ResMemberConcretenessMode.Abstract )
                        midAttr.IsAbstract = true;
                    else if( resAttrib.IsInput() )
                        midAttr.IsInput = true;
                    else if( resAttrib.IsOptional() )
                        midAttr.IsOptional = true;
                    else if( resAttrib.Line.Tags.Any( ( t ) => t is ResBuiltinTag ) )
                    {}
                    else
                        throw new NotImplementedException();

                    // Eventually:
//                    else
//                        midAttr.IsInput = true;
                }
                else
                {
                    var initEnv = new MidDummyEmitEnv(env);

                    var inheritedDecls = resAttrib.Line.InheritedDecls;
                    if (inheritedDecls != null)
                    {
                        // Skip first decl if we are already using an inherited decl:
                        if (resAttrib.Line.MemberDeclMode != ResMemberDeclMode.Inherited)
                            initEnv.InheritedDecls = inheritedDecls.Cast<IResAttributeDecl>();
                        else
                            initEnv.InheritedDecls = inheritedDecls.Cast<IResAttributeDecl>().Skip(1);
                    }

                    var midExp = EmitAttrExp(midElement, midType, resAttrib.Init, initEnv);

                    if( midExp is MidAttributeRef
                        && ((MidAttributeRef) midExp).Decl.IsOptional )
                    {
                        midAttr = ((MidAttributeRef) midExp).Decl;
                    }
                    else if( resAttrib.IsOutput() )
                    {
                        midAttr = new MidAttributeDecl(
                            resAttrib.Name,
                            midElement,
                            midType,
                            midExp );
                        midElement.AddAttribute( midAttr );
                    }
                    else
                    {
                        midAttr = midElement.CacheAttr( midExp, midType );
                        midAttr.TrySetName( resAttrib.Name, resAttrib.Range );
                    }
                }
                if( resAttrib.IsOutput() && !midAttr.IsOptional )
                    midAttr.IsForcedOutput = true;

                midAttrWrap.Attribute = midAttr;
                midElement.AddAttributeWrapper( midAttrWrap );
            } );
            midAttrWrap.DoneBuilding();

            midFacet.AddAttribute( midAttrWrap );
            return midAttrWrap;

            /*

            var midAttrib = new MidAttributeDecl(midFacet, resAttrib.Name);

            if (resAttrib.Line.ConcretenessMode == ResMemberConcretenessMode.Abstract)
                midAttrib.IsAbstract = true;

            if( resAttrib.IsOutput )
                midAttrib.IsOutput = true;

            midAttrib.AddBuildAction(() =>
            {
                var fqType = resAttrib.Type;
                var freq = fqType.Freq;
                var type = fqType.Type;

                var midElement = (MidElementDecl)midFacet.Pipeline.LookupMemberDecl(freq.MemberTerm.Decl);

                midAttrib.Type = EmitTypeExp(resAttrib.Type, env);

                if (resAttrib.Init != null)
                {
                    var savedElement = _currentElement;
                    _currentElement = midElement;

                    midAttrib.Exp = EmitLocalExp(resAttrib.Init, env);

                    _currentElement = savedElement;
                }

                midAttrib.Element = midElement;
                midElement.AddAttribute(midAttrib);
            });

            midAttrib.DoneBuilding();

            midFacet.AddAttribute(midAttrib);
            return midAttrib;
             * */
        }

        private MidMemberDecl EmitMemberDeclImpl(
            IBuilder parent,
            IResGenericDecl resGeneric,
            MidEmitEnv env)
        {
            var midGeneric = new MidGenericDecl(
                parent,
                resGeneric.Name,
                resGeneric,
                this,
                env);            // Nothing to do...
            midGeneric.DoneBuilding();
            return midGeneric;
        }

        private MidMemberDecl EmitMemberDeclImpl(
            IBuilder parent,
            IResMethodDecl resMethod,
            MidEmitEnv env)
        {
            var builtinTags = (from tag in resMethod.Line.Tags
                               let builtinTag = tag as ResBuiltinTag
                               where builtinTag != null
                               select builtinTag).ToArray();

            if( resMethod.Body != null )
            {
                // Don't use builtin version if there's an inline impl (probably
                // because its an override...)
                builtinTags = new ResBuiltinTag[] { };
            }


            IMidMethodDecl midMethod = null;
            if (builtinTags.Length != 0)
            {
                midMethod = new MidBuiltinMethodDecl(
                    parent,
                    resMethod.Name,
                    builtinTags);
            }
            else
            {
                midMethod = new MidMethodDecl(
                    parent,
                    resMethod.Name,
                    _exps );
            }

            midMethod.AddBuildAction(() =>
            {
                var resultType = EmitTypeExp(resMethod.ResultType, env);

                midMethod.ResultType = resultType;

                var midParams = (from p in resMethod.Parameters
                                 select new MidVar(p.Name, EmitTypeExp(p.Type, env))).ToArray();

                midMethod.Parameters = midParams;

                if( resMethod.Body != null && !IsCrossFrequencyMethod( resMethod ) )
                {
                    var paramEnv = new MidGlobalEmitEnv(env, env.Context);
                    foreach (var pair in midParams.Zip(resMethod.Parameters, Tuple.Create))
                    {
                        var midParam = pair.Item1;
                        var resParam = pair.Item2;

                        paramEnv.Insert(resParam, new MidVarRef(midParam));
                    }

                    ((MidMethodDecl) midMethod).Body = EmitLocalExp(resMethod.Body, paramEnv);
                }
            });
            midMethod.DoneBuilding();

            if( (parent is MidFacetDecl) && (midMethod is MidMethodDecl) )
                ((MidFacetDecl) parent).AddMethod((MidMethodDecl) midMethod);
            return (MidMemberDecl)midMethod;
        }

        private MidMemberDecl EmitMemberDeclImpl(
            IBuilder parent,
            IResTypeSlotDecl resTypeSlot,
            MidEmitEnv env)
        {
            var builtinTags = (from tag in resTypeSlot.Line.Tags
                               let builtinTag = tag as ResBuiltinTag
                               where builtinTag != null
                               select builtinTag).ToArray();

            if (builtinTags.Length != 0)
            {
                IEnumerable<object> args = null;

                var genericDecl = resTypeSlot.Line.EffectiveDecl as IResGenericDecl;
                if (genericDecl != null)
                {
                    args = (from p in genericDecl.Parameters
                            select env.Lookup(p)).ToArray();
                }

                var midBuiltin = new MidBuiltinTypeDecl(
                    parent,
                    resTypeSlot.Name.ToString(),
                    args,
                    builtinTags);
                midBuiltin.DoneBuilding();
                return midBuiltin;
            }

            var midTypeSlot = new MidTypeSlotDecl(
                parent,
                resTypeSlot.Name,
                resTypeSlot.Line.Tags.OfType<ResBuiltinTag>());
            midTypeSlot.DoneBuilding();
            return midTypeSlot;
        }

        private MidMemberDecl EmitMemberDeclImpl(
            IBuilder parent,
            IResStructDecl resStruct,
            MidEmitEnv env)
        {
            var midStruct = new MidStructDecl(parent, resStruct.Name, this, env);

            midStruct.AddBuildAction(() =>
            {
                foreach (var resDecl in resStruct.GetMembers())
                {
                    var midDecl = EmitMemberDecl(midStruct, resDecl, env);
                    midStruct.InsertMemberDecl(resDecl, midDecl);
                }
            });
            midStruct.DoneBuilding();
            return midStruct;
        }

        private MidMemberDecl EmitMemberDeclImpl(
            MidStructDecl midStruct,
            IResFieldDecl resField,
            MidEmitEnv env)
        {
            var midField = new MidFieldDecl(midStruct, resField.Name);

            midField.AddBuildAction(() =>
            {
                midField.Type = EmitTypeExp(resField.Type, env);
            });
            midField.DoneBuilding();

            midStruct.AddField(midField);
            return midField;
        }

        private MidMemberDecl EmitMemberDeclImpl(
            IBuilder parent,
            IResConceptClassDecl resConceptClass,
            MidEmitEnv env )
        {
            var midConcept = new MidConceptClassDecl(
                parent,
                resConceptClass.Name,
                resConceptClass.GetMembers(),
                this,
                env );
            midConcept.DoneBuilding();
            return midConcept;
        }

        public IMidMemberRef SpecializeGenericDecl(
            MidGenericDecl genericDecl,
            IEnumerable<object> args)
        {
            var resGeneric = genericDecl.ResDecl;
            var env = new MidGlobalEmitEnv(genericDecl.Env, genericDecl.Env.Context);
            foreach (var p in args.Zip(resGeneric.Parameters, Tuple.Create))
            {
                env.Insert(p.Item2, p.Item1);
            }

            var builder = new Builder(null);
            var midDecl = EmitMemberDecl(
                builder,
                resGeneric.InnerDecl,
                env);
            builder.DoneBuilding();
            builder.ForceDeep();

            return midDecl.CreateRef(null);
        }

        /*
        public MidMemberDecl SpecializeGenericDeclImpl(
            MidGenericDecl genericDecl,
            IEnumerable<object> args,
            IResGenericDecl resGeneric,
            IResTypeSlotDecl resTypeSlot)
        {
            var builtinTags = resGeneric.Line.Tags.OfType<ResBuiltinTag>().ToArray();
            if (builtinTags.Length != 0)
            {
                return new MidBuiltinTypeDecl(
                    resTypeSlot.Name.ToString(),
                    args,
                    builtinTags);
            }

            throw new NotImplementedException();
        }
        */
        /*
        private object MapDeclImpl(
            IBuilder parent,
            IResConceptClassDecl resConceptClass,
            MidEmitEnv env)
        {
            return new MidConceptClassDecl(
                parent,
                resConceptClass.Name,
                resConceptClass.Members,
                this,
                env);
        }

        private object MapDeclImpl(
            IBuilder parent,
            IResPipelineDecl resPipeline,
            MidEmitEnv env)
        {
            return new MidPipelineDecl(
                parent,
                resPipeline.Name,
                this,
                env);
        }

        */

        private MidVal EmitVal(IResExp resExp, MidEmitEnv env)
        {
            var midType = EmitTypeExp(resExp.Type, env);
            var midExp = EmitExpRaw(resExp, env );
            return env.MaybeMoveTemp<MidVal>(midExp, midType);
        }

        private MidPath EmitPath(IResExp resExp, MidEmitEnv env)
        {
            var midType = EmitTypeExp(resExp.Type, env);
            var midExp = EmitExpRaw(resExp, env);
            return env.MaybeMoveTemp<MidPath>(midExp, midType);
        }

        private MidExp EmitExpRaw(IResExp exp, MidEmitEnv env)
        {
            return EmitExpImpl((dynamic)exp, env);
        }

        private MidExp EmitExpImpl(
            ResBaseExp exp,
            MidEmitEnv env)
        {
            var inheritedDecls = env.InheritedDecls;
            if (inheritedDecls == null )
            {
                throw new NotImplementedException();
            }

            var inheritedDeclsArr = inheritedDecls.ToArray();

            var firstDecl = inheritedDeclsArr[0];
            if (firstDecl.Init == null)
            {
                throw new NotImplementedException();
            }

            var nestedEnv = new MidDummyEmitEnv(env);
            nestedEnv.InheritedDecls = inheritedDeclsArr.Skip(1);

            return EmitExpRaw(
                firstDecl.Init,
                nestedEnv);
        }

        private MidExp EmitExpImpl(
            Resolve.ResDummyValArg exp,
            MidEmitEnv env)
        {
            if (exp.ConcreteVal == null)
            {
                throw new NotImplementedException();
            }

            return EmitExpRaw(exp.ConcreteVal, env);
        }

        private MidExp EmitLocalExp(
            IResExp resExp,
            MidEmitEnv env,
            MidElementDecl element = null )
        {
            var localEnv = new MidLocalEmitEnv( env, _identifiers, element, _exps );
            var midExp = EmitExpRaw(resExp, localEnv);
            return localEnv.Wrap(midExp);
        }

        private MidExp EmitExpImpl(
            ResIfExp exp,
            MidEmitEnv env)
        {
            var condExp = EmitVal(exp.Condition, env);
            var thenExp = EmitLocalExp(exp.Then, env);
            var elseExp = EmitLocalExp(exp.Else, env);

            return new MidIfExp(condExp, thenExp, elseExp, exp.Range);
        }

        private MidExp EmitExpImpl(
            ResSwitchExp exp,
            MidEmitEnv env)
        {
            var value = EmitVal(exp.Value, env);

            var cases = (from c in exp.Cases
                         let v = EmitVal(c.Value, env)
                         let b = EmitLocalExp(c.Body, env)
                         select new MidCase(v, b, c.Range)).ToArray();

            return new MidSwitchExp(value, cases, exp.Range);
        }

        private MidExp EmitExpImpl(
            ResVoidExp resVoid,
            MidEmitEnv env)
        {
            return _exps.Void;
        }

        private MidExp EmitExpImpl(
            ResAssignExp resAssign,
            MidEmitEnv env)
        {
            var dest = EmitVal(resAssign.Dest, env);
            var src = EmitVal(resAssign.Src, env);

            return new MidAssignExp(
                dest,
                src,
                resAssign.Range);
        }


        private MidExp EmitExpImpl(
            ResForExp resFor,
            MidEmitEnv env)
        {
            // \todo: Set up the variable... :(
            var resVar = resFor.Var;
            var midVar = new MidVar(
                resVar.Name,
                EmitTypeExp(resVar.Type, env));

            var seq = EmitVal(resFor.Sequence, env);

            var bodyEnv = new MidLocalEmitEnv( env, _identifiers, null, _exps );
            bodyEnv.Insert(resVar, new MidVarRef(midVar));
            var body = bodyEnv.Wrap(EmitVal(resFor.Body, bodyEnv));

            return new MidForExp(
                midVar,
                seq,
                body,
                resFor.Range);
        }


        private MidExp EmitExpImpl(
            ResSeqExp resSeq,
            MidEmitEnv env)
        {
            // \todo: Need a MidSeqExp?
            var head = EmitVal(resSeq.Head, env);
            var tail = EmitVal(resSeq.Tail, env);

            return tail;

//            return new MidLetExp(
//                new MidVar(_identifiers.unique("_"), head.Type),
//                head,
//                tail);
        }

        private MidExp EmitExpImpl(
            ResConceptVal resConcept,
            MidEmitEnv env)
        {
            var conceptClass = (MidConceptClassRef) EmitTypeExp(
                resConcept.Type,
                env );
            var memberRefs = (from m in resConcept.MemberRefs
                              select EmitMemberTerm(m.MemberTerm, env)).ToArray();
            return new MidConceptVal(
                conceptClass,
                memberRefs);
        }

        private MidExp EmitExpImpl(
            ResLetExp resLet,
            MidEmitEnv env)
        {
            var resLetType = resLet.Var.Type;

            MidVal midVal = null;
            if (resLetType is ResFreqQualType)
            {
                var resFreq = ((ResFreqQualType)resLetType).Freq;
                var midElem = ((MidElementType)EmitTypeExp(resFreq, env)).Decl;

                midVal = EmitValToElement(resLet.Value, midElem, env);
            }

            if( midVal == null )
            {
                midVal = EmitVal(resLet.Value, env);
            }

            env.Insert(
                resLet.Var,
                midVal);

            return EmitExpRaw(
                resLet.Body,
                env);
        }

        private MidExp EmitExpImpl(
            IResElementCtorApp ctorApp,
            MidEmitEnv env)
        {
            var elementDecl = ((MidElementType)EmitTypeExp(ctorApp.Element, env)).Decl;

            var args = (from a in ctorApp.Args
                        select EmitElementCtorArg(a, env)).ToArray();

            return new MidElementCtorApp(
                elementDecl,
                args);
        }

        private MidElementCtorArg EmitElementCtorArg(
            ResElementCtorArg resArg,
            MidEmitEnv env)
        {
            var attribWrapperMemberRef = (MidAttributeWrapperMemberRef) EmitMemberTerm(resArg.Attribute.MemberTerm, env);
            var attribDecl = attribWrapperMemberRef.Decl.Attribute;

            var val = EmitVal(resArg.Value, env);

            return new MidElementCtorArg(
                attribDecl,
                val);
        }

        private MidExp EmitExpImpl(
            ResAttributeFetch attrFetch,
            MidEmitEnv env)
        {
            // Get the base object to fetch attribute from:
            var obj = EmitPath(attrFetch.Obj, env);

            // Now, emit the attribute expression to
            // its associated element:
            var attrFreq = ((ResFreqQualType) attrFetch.Attribute.Type).Freq;

            var attrElement = ((MidElementType)EmitTypeExp(attrFreq, env)).Decl;

            var attrVal = EmitValToElement(attrFetch.Attribute, attrElement, env);

            if (attrVal is MidAttributeRef)
            {
                // We want to avoid forcing the decl
                // of the MidAttributeRef, in case it is lazy
                var attrRef = (MidAttributeRef)attrVal;

                return new MidAttributeFetch(
                    obj,
                    attrVal.Type,
                    env.Lazy(() => attrRef.Decl));
            }
            else
            {
                // some other kind of value... maybe just a literal...
                return attrVal;
            }
        }

        private MidExp EmitExpImpl(ResLabelExp exp, MidEmitEnv env)
        {
            var midType = EmitTypeExp(exp.Type, env);
            var localEnv = new MidLocalEmitEnv( env, _identifiers, null, _exps );
            var midLabel = new MidLabel();
            localEnv.Insert(exp.Label, midLabel);
            var midBody = EmitLocalExp(exp.Body, localEnv);
            midBody = localEnv.Wrap( midBody );
            return new MidLabelExp(
                midLabel,
                midBody,
                midType);
        }

        private MidExp EmitExpImpl(ResBreakExp exp, MidEmitEnv env)
        {
            MidLabel label = env.Lookup(exp.Label);
            MidVal val = EmitVal(exp.Value, env);

            return new MidBreakExp(label, val);
        }

        private MidExp EmitExpImpl<T>(ResLit<T> resLit, MidEmitEnv env)
        {
            return _exps.Lit( resLit.Value, EmitTypeExp( resLit.Type, env ) );
        }

        private MidExp EmitExpImpl(ResVarRef resVarRef, MidEmitEnv env)
        {
            return env.Lookup(resVarRef.Decl);
        }

        private MidExp EmitExpImpl(ResMethodApp resApp, MidEmitEnv env)
        {
            var resMethod = resApp.Method;
            if (IsCrossFrequencyMethod(resMethod))
            {
                var resBody = resMethod.Body;
                if (resBody != null)
                {
                    var bindEnv = new MidDummyEmitEnv( env );
//                    var bindEnv = new MidLocalEmitEnv(
//                        env,
//                        _identifiers,
//                        null,
//                        _exps );

                    BindForMemberTerm(resMethod.MemberTerm, bindEnv);

                    foreach (var p in resApp.Args.Zip(resMethod.Parameters, Tuple.Create))
                    {
                        var resArg = p.Item1;
                        var resParam = p.Item2.Decl;

                        var paramElement = GetFreq(resApp.Type, bindEnv);
                        if (resParam.Type is ResFreqQualType)
                        {
                            var resParamType = (ResFreqQualType)resParam.Type;
                            var resParamFreq = (MidElementType)EmitTypeExp(resParamType.Freq, bindEnv);
                            paramElement = resParamFreq.Decl;
                        }

                        var argVal = EmitValToElement(resArg, paramElement, bindEnv);

                        // insert value into env...
                        bindEnv.Insert(resParam, argVal);
                    }

                    return EmitVal( resBody, bindEnv );
//                    return bindEnv.Wrap(EmitVal(resBody, bindEnv));
                }
            }

            var methodRef= EmitMemberTerm(resMethod.MemberTerm, env);
            var args = from a in resApp.Args
                       select EmitVal(a, env);

            return methodRef.App(args);
        }

        private void BindForMemberTerm(
            IResMemberTerm memberTerm,
            MidEmitEnv env)
        {
            BindForMemberTermImpl((dynamic)memberTerm, env);
        }

        private void BindForMemberTermImpl(
            ResMemberBind memberBind,
            MidEmitEnv env)
        {
            var objVal = EmitVal(memberBind.Obj, env);
            env.Insert(
                memberBind.MemberSpec.Container.ThisParameter,
                objVal);
        }

        private void BindForMemberTermImpl(
            ResMemberGenericApp genericApp,
            MidEmitEnv env)
        {
            BindForMemberTerm(genericApp.Fun.MemberTerm, env);

            foreach (var pair in genericApp.Args.Zip(genericApp.Fun.Parameters, Tuple.Create))
            {
                EmitGenericArg(pair.Item1, pair.Item2, env);
            }
        }

        private object EmitGenericArg(
            IResGenericArg arg,
            MidEmitEnv env)
        {
            return EmitGenericArg(arg, null, env);
        }

        private object EmitGenericArg(
            IResGenericArg arg,
            IResGenericParamRef param,
            MidEmitEnv env)
        {
            return EmitGenericArgImpl((dynamic) arg, (dynamic) param, env);
        }

        private object EmitGenericArgImpl(
            ResGenericValueArg arg,
            IResVarSpec param,
            MidEmitEnv env)
        {
            var midVal = EmitVal(arg.Value, env);
            if (param != null)
            {
                env.Insert(
                    param.Decl,
                    midVal);
            }
            return midVal;
        }


        private object EmitGenericArgImpl(
            ResGenericTypeArg arg,
            IResTypeParamRef param,
            MidEmitEnv env)
        {
            var midType = EmitTypeExp(arg.Type, env);
            if (param != null)
            {
                env.Insert(
                    param.Decl,
                    midType);
            }
            return midType;
        }

        private MidElementDecl GetFreq(IResTypeExp type, MidEmitEnv env)
        {
            if (type is ResFreqQualType)
            {
                var fqType = (ResFreqQualType)type;
                var elementType = (MidElementType)EmitTypeExp(fqType.Freq, env);
                return elementType.Decl;
            }

            return null;
        }

        private bool IsCrossFrequencyMethod(IResMethodRef method)
        {
            if (method.ResultType is ResFreqQualType) return true;
            foreach (var p in method.Parameters)
            {
                if (p.Type is ResFreqQualType) return true;
            }

            return false;
        }

        private bool IsCrossFrequencyMethod( IResMethodDecl method )
        {
            if( method.ResultType is ResFreqQualType )
                return true;
            foreach( var p in method.Parameters )
            {
                if( p.Type is ResFreqQualType )
                    return true;
            }

            return false;
        }

        private MidElementDecl _currentElement = null;

        private MidVal EmitValToElement(
            IResExp resExp,
            MidElementDecl element,
            MidEmitEnv parentEnv)
        {
            if (element == _currentElement)
            {
                return EmitVal(resExp, parentEnv);
            }

            var savedElement = _currentElement;
            _currentElement = element;

            var result = EmitValToElementImpl(
                resExp,
                element,
                parentEnv);

            _currentElement = savedElement;
            return result;
        }

        private MidVal EmitValToElementImpl(
            IResExp resExp,
            MidElementDecl element,
            MidEmitEnv parentEnv)
        {
            var midExp = EmitLocalExp(resExp, parentEnv, element);
            if (midExp is MidVal)
                return (MidVal) midExp;

            var midType = EmitTypeExp( resExp.Type, parentEnv );
            var attr = element.CacheAttr(
                midExp,
                midType );
            return _exps.AttributeRef( attr );
        }

        private MidExp EmitExpImpl(IResFieldRef resField, MidEmitEnv env)
        {
            var resBind = resField.MemberTerm as ResMemberBind;

            var obj = EmitPath(resBind.Obj, env);

            var container = EmitMemberTerm(resBind.MemberSpec.Container.MemberTerm, env);

            var decl = (MidFieldDecl) container.LookupMemberDecl(resBind.Decl);

            return _exps.FieldRef( obj, decl );
        }

        private MidExp EmitExpImpl(IResAttributeRef resAttrib, MidEmitEnv env)
        {
            var wrapper = (MidAttributeWrapperMemberRef) EmitMemberTerm(resAttrib.MemberTerm, env);

            var type = EmitTypeExp(resAttrib.Type, env);
            return new MidAttributeRef(type, env.Lazy(() => {
                return wrapper.Decl.Attribute;
            }));
        }

        private IMidMemberRef EmitMemberTerm(IResMemberTerm resMemberTerm, MidEmitEnv env)
        {
            return EmitMemberTermImpl((dynamic)resMemberTerm, env);
        }

        private IMidMemberRef EmitMemberTermImpl(ResMemberGenericApp resApp, MidEmitEnv env)
        {
            var fun = EmitMemberTerm(resApp.Fun.MemberTerm, env);

            var argEnv = new MidGenericArgEmitEnv(env);

            var args = resApp.Args.Zip(resApp.Fun.Parameters,
                        (a,p) => EmitGenericArg(a, p, argEnv)).Eager();

            return fun.GenericApp(args);
        }

        private IMidMemberRef EmitMemberTermImpl(ResMemberBind resBind, MidEmitEnv env)
        {
            var obj = EmitVal(resBind.Obj, env);

            var container = EmitMemberTerm(resBind.MemberSpec.Container.MemberTerm, env);

            return LookupMemberImpl((dynamic) container, (dynamic)obj, resBind.Decl, env);
        }

        private IMidMemberRef LookupMemberImpl(
            IMidMemberRef container,
            MidVal obj,
            IResMemberDecl resDecl,
            MidEmitEnv env )
        {
            var midDecl = container.LookupMemberDecl(resDecl);
            return midDecl.CreateRef(new MidMemberBind(obj, midDecl));
        }

        private IMidMemberRef LookupMemberImpl(
            MidConceptClassRef container,
            MidConceptVal obj,
            IResMemberDecl resMemberDecl,
            MidEmitEnv env)
        {
            var resMembersDecls = container.Decl.Members.ToArray();
            var midMemberRefs = obj.MemberRefs.ToArray();

            int memberDeclCount = resMembersDecls.Length;
            for (int ii = 0; ii < memberDeclCount; ++ii)
            {
                if (resMembersDecls[ii] == resMemberDecl)
                    return midMemberRefs[ii];
            }

            throw new NotImplementedException();
        }

        private IMidMemberRef EmitMemberTermImpl(ResGlobalMemberTerm resGlobal, MidEmitEnv env)
        {
            var resDecl = resGlobal.Decl;
            var resModule = resGlobal.Module;

            var midModule = _modules[ resModule ];

            var midDecl = midModule.LookupMemberDecl( resDecl );

            if (midDecl == null)
            {
                if( midModule == _module )
                {
                    midDecl = EmitMemberDecl(
                        null,
                        resDecl,
                        _module.Env );
                    _module.InsertMemberDecl( resDecl, midDecl );
                    midDecl.ForceDeep();
                }
                else
                {
                    throw new NotImplementedException();
                }
            }

            return midDecl.CreateRef(new MidGlobalMemberTerm(midDecl));
        }

        private MidType EmitTypeExp(IResTypeExp resType, MidEmitEnv env)
        {
            return EmitTypeExpImpl((dynamic)resType, env);
        }

        private MidType EmitTypeExpImpl(
            ResVoidType voidType,
            MidEmitEnv env)
        {
            return new MidVoidType();
        }

        private MidType EmitTypeExpImpl(
            ResBottomType bottomType,
            MidEmitEnv env)
        {
            // Well, bottom really means "never returns"...
            return new MidVoidType();
        }

        private MidType EmitTypeExpImpl(Spark.Resolve.ResConceptClassRef type, MidEmitEnv env)
        {
            return (MidType)EmitMemberTerm(type.MemberTerm, env);
        }

        private MidType EmitTypeExpImpl(Spark.Resolve.ResDummyTypeArg type, MidEmitEnv env)
        {
            return EmitTypeExp(type.ConcreteType, env);
        }

        private MidType EmitTypeExpImpl(IResTypeSlotRef slotRef, MidEmitEnv env)
        {
            return (MidType) EmitMemberTerm(slotRef.MemberTerm, env);
        }

        private MidType EmitTypeExpImpl(ResTypeVarRef varRef, MidEmitEnv env)
        {
            return env.Lookup(varRef.Decl);
        }

        private MidType EmitTypeExpImpl(ResFreqQualType fqType, MidEmitEnv env)
        {
            return EmitTypeExp(fqType.Type, env);
        }

        private MidType EmitTypeExpImpl(IResElementRef resElement, MidEmitEnv env)
        {
            return (MidType)EmitMemberTerm(resElement.MemberTerm, env);
        }

        private MidType EmitTypeExpImpl(IResPipelineRef resPipeline, MidEmitEnv env)
        {
            return (MidType)EmitMemberTerm(resPipeline.MemberTerm, env);
        }

        private MidType EmitTypeExpImpl(IResStructRef resStruct, MidEmitEnv env)
        {
            return (MidType)EmitMemberTerm(resStruct.MemberTerm, env);
        }
    }
}
