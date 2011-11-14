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

using Spark.AbstractSyntax;
using Spark.ResolvedSyntax;

namespace Spark.Resolve
{
    public class ResolveContext
    {
        private IdentifierFactory _identifiers;
        private IDiagnosticsCollection _diagnostics;
        private LazyFactory _lazyFactory = new LazyFactory();

        public LazyFactory LazyFactory
        {
            get { return _lazyFactory; }
        }

        public ResolveContext(
            IdentifierFactory identifiers,
            IDiagnosticsCollection diagnostics)
        {
            _identifiers = identifiers;
            _diagnostics = diagnostics;
        }


        public IResModuleDecl Resolve(
                    IEnumerable<AbsSourceRecord> sourceRecords)
        {
            var lazyResModule = ResModuleDeclBuilder.Build(
                LazyFactory,
                (resModuleBuilder) =>
            {
                var globalScope = SetupBuiltins(
                    resModuleBuilder);
                var moduleScope = new ResModuleScope(resModuleBuilder);

                var env = new ResEnv(this, _diagnostics, globalScope);
                env = env.NestScope(moduleScope);

                foreach (var sr in sourceRecords)
                    foreach (var decl in sr.decls)
                        ResolveGlobalDecl(resModuleBuilder, decl, env);
            });

            _lazyFactory.Force();
            return lazyResModule.Value;
        }

        public IResModuleDecl ResolveDynamicShaderClass(
            IEnumerable<IResPipelineRef> bases )
        {
            var lazyResModule = ResModuleDeclBuilder.Build(
                LazyFactory,
                (resModuleBuilder) =>
            {
                var resEnv = new ResEnv(
                    this,
                    _diagnostics,
                    null);
                var range = new SourceRange();
                var name = _identifiers.unique("DynamicShaderClass");

                ResolveShaderClassDecl(
                    resModuleBuilder,
                    resEnv,
                    range,
                    name,
                    AbsModifiers.None,
                    (e) => bases.ToArray(),
                    new AbsMemberDecl[] { },
                    true);
            });

            _lazyFactory.Force();
            return lazyResModule.Value;
        }

        private Func<SourceRange, IResTypeExp> _builtinTypeBool;
        private Func<SourceRange, IResTypeExp> _builtinTypeInt32;
        private Func<SourceRange, IResTypeExp> _builtinTypeFloat32;

        private IResScope SetupBuiltins(
            ILazy<IResModuleDecl> module )
        {
            var scope = new ResLocalScope();

            _builtinTypeBool = AddBuiltinType(module, scope, "bool");
            _builtinTypeInt32 = AddBuiltinType(module, scope, "int" );
            _builtinTypeFloat32 = AddBuiltinType(module, scope, "float" );

            return scope;
        }

        private void AddBuiltinValue<T>(
            ResLocalScope scope,
            string name,
            T value,
            IResTypeExp type)
        {
            scope.Insert(_identifiers.simpleIdentifier(name),
                (range) => new ResLit<T>(range, type, value));
        }

        private Func<SourceRange, IResTypeExp> AddBuiltinType(
            ILazy<IResModuleDecl> module,
            ResLocalScope scope,
            string name)
        {
            var range = new SourceRange();
            var id = _identifiers.simpleIdentifier(name);

            var nameGroupBuilder = new ResMemberNameGroupBuilder(
                LazyFactory, null, id);

            var categoryGroupBuilder = nameGroupBuilder.GetMemberCategoryGroup(new ResTypeSlotCategory());

            var originalLexicalID = new ResLexicalID();
            var lineBuilder = new ResMemberLineDeclBuilder(
                categoryGroupBuilder,
                LazyFactory,
                id,
                originalLexicalID,
                new ResTypeSlotCategory());

            var decl = new ResTypeSlotDecl(
                lineBuilder,
                range,
                id);
            lineBuilder.AddTag(new ResBuiltinTag("hlsl", name));
            lineBuilder.AddTag(new ResBuiltinTag("c++", name));
            lineBuilder.AddTag(new ResBuiltinTag("llvm", name));

            nameGroupBuilder.DoneBuilding();

            Func<SourceRange, IResTypeExp> gen = (r) =>
                (IResTypeExp) decl.MakeRef(
                    r,
                    new ResGlobalMemberTerm(r, module, decl));

            scope.Insert( id, gen );
            return gen;
        }

        private void ResolveGlobalDecl(
            ResModuleDeclBuilder resModule,
            AbsGlobalDecl decl,
            ResEnv env)
        {
            ResolveGlobalDeclImpl(resModule, (dynamic)decl, env.NestDiagnostics());
        }

        private class FacetInfo
        {
            public ResFacetDeclBuilder Facet { get; set; }

            public MemberLineInfo[] MemberLines;
        }

        private class MemberLineInfo
        {
            public IResMemberLineSpec FirstSpec { get; set; }
            public List<Tuple<IResPipelineRef, IResMemberRef>> MemberRefs = new List<Tuple<IResPipelineRef, IResMemberRef>>();
        }


        private void ResolveGlobalDeclImpl(
            ResModuleDeclBuilder resModule,
            AbsPipelineDecl absPipeline,
            ResEnv env)
        {
            ResolveShaderClassDecl(
                resModule,
                env.NestDiagnostics(),
                absPipeline.Range,
                absPipeline.Name,
                absPipeline.Modifiers,
                (e) => ResolveBases(absPipeline.Bases, e),
                absPipeline.Members,
                false);
        }

        private IResPipelineRef[] ResolveBases(
            IEnumerable<AbsTerm> absBases,
            ResEnv env )
        {
            return (from b in absBases
                    let resB = ResolvePipelineRef( b, env )
                    where !(resB is ResErrorTerm)
                    select resB).Eager();
        }

        private void ResolveShaderClassDecl(
            ResModuleDeclBuilder resModule,
            ResEnv env,
            SourceRange range,
            Identifier name,
            AbsModifiers modifiers,
            Func<ResEnv, IResPipelineRef[]> resolveBases,
            IEnumerable<AbsMemberDecl> absMembers,
            bool ignoreOrderingErrors )
        {
            var resPipeline = ResPipelineDecl.Build(
                LazyFactory,
                resModule,
                range,
                name,
                (resShaderClassBuilder) =>
            {
                BuildResolvedShaderClassDecl(
                    resShaderClassBuilder,
                    resModule,
                    env,
                    range,
                    name,
                    modifiers,
                    resolveBases,
                    absMembers,
                    ignoreOrderingErrors );
            });
            resModule.AddDecl(resPipeline);
        }

        private void BuildResolvedShaderClassDecl(
            ResPipelineDeclBuilder resShaderClassBuilder,
            ResModuleDeclBuilder resModule,
            ResEnv env,
            SourceRange range,
            Identifier name,
            AbsModifiers modifiers,
            Func<ResEnv, IResPipelineRef[]> resolveBases,
            IEnumerable<AbsMemberDecl> absMembers,
            bool ignoreOrderingErrors )
        {
            var headerEnv = env.NestDiagnostics();

            var thisPipelineBuilder = new ResPipelineBuilderRef(resShaderClassBuilder);

            var thisPipeline = new ResPipelineRef(
                range,
                resShaderClassBuilder.Value,
                new ResGlobalMemberTerm(range, resModule, resShaderClassBuilder.Value));
            var thisParameter = new ResVarDecl(
                range,
                _identifiers.simpleIdentifier("this"),
                thisPipeline);

            var directFacet = new ResFacetDeclBuilder(LazyFactory, resShaderClassBuilder, thisPipeline);

            if (modifiers.HasFlag(AbsModifiers.Abstract))
                resShaderClassBuilder.ConcretenessMode = ResMemberConcretenessMode.Abstract;

            if (modifiers.HasFlag(AbsModifiers.Mixin))
            {
                if (modifiers.HasFlag(AbsModifiers.Primary))
                {
                    env.Error(range,
                        "Shader class '{0}' cannot be declared both 'primary' and 'mixin'",
                        name);
                }

                resShaderClassBuilder.MixinMode = ResMixinMode.Mixin;
            }

            resShaderClassBuilder.ThisPipeline = thisPipeline;
            resShaderClassBuilder.ThisParameter = thisParameter;
            resShaderClassBuilder.DirectFacet = directFacet;

            var innerEnv = env.NestScope(
                new ResPipelineScope(thisPipeline, thisParameter));

            var bases = resolveBases(env);
            var facetInfos = new Dictionary<ResFacetDeclBuilder, FacetInfo>();

            resShaderClassBuilder.Bases = bases;

            int baseIndex = 0;
            foreach (var b in bases)
            {
                // \todo: This is a big hack... :(
//                ((IBuilder)b.Decl).ForceDeep();

                if (b.MixinMode == ResMixinMode.Primary)
                {
                    if (baseIndex != 0 && !ignoreOrderingErrors)
                    {
                        env.Error(
                            b.Range,
                            "Class '{0}' can only list at most one primary base, and it must appear first in the list of bases",
                            name);
                    }
                }

                baseIndex++;
            }

            var baseLinearization = Linearize(
                (from b in bases
                 select (from f in b.Facets
                         select f.OriginalPipeline).ToArray()).ToArray());

            Func<IResMemberDecl, SourceRange, IResTerm> pipelineMakeRef = (m, r) =>
                (new ResMemberSpec(r, thisPipeline, m)).Bind(r,
                    new ResVarRef(r, thisParameter));

            // First, create derived-class facets for each of the base classes,
            // using the linearization we computed above. This will ensure
            // that our facets are in an appropriate order (most- to least-derived)
            foreach (var b in baseLinearization)
            {
                var facet = resShaderClassBuilder.FindOrCreateFacetForBase(b);
            }

            // Now that we've done that step, we can safely create direct
            // inheritance links from our direct facet to the associated
            // base facets that were listed in the declaration:
            foreach (var b in bases)
            {
                var derivedFacet = FindOrCreateFacetForBase(
                    resShaderClassBuilder,
                    b,
                    facetInfos,
                    innerEnv);
                directFacet.AddDirectBase(derivedFacet);
            }

            // Next, populate the list of members for each facet, by
            // going through the linearized bases again
            //
            // We proceed in linearized order (from
            // "most derived" to "most base"), so that
            // the resulting list will have the most-derived
            // declaration of the member at the front of the list.
            foreach (var b in baseLinearization)
            {
                PopulateMembersForBase(
                    resShaderClassBuilder,
                    b,
                    facetInfos,
                    innerEnv);
            }

            foreach (var b in baseLinearization)
            {
                var facet = FindOrCreateFacetForBase(
                    resShaderClassBuilder,
                    b,
                    facetInfos,
                    innerEnv);
                var facetInfo = facetInfos[facet];
                foreach (var mli in facetInfo.MemberLines)
                {
                    var memberLineInfo = mli;
                    var firstSpec = mli.FirstSpec;

                    var memberRefs = (from p in memberLineInfo.MemberRefs
                                      select p.Item2).ToArray();
                    var first = memberRefs[0];

                    var memberNameGroupBuilder = facet.GetMemberNameGroup(firstSpec.Name);
                    var memberCategoryGroupBuilder = memberNameGroupBuilder.GetMemberCategoryGroup(firstSpec.Category);

                    var newMemberLineBuilder = new ResMemberLineDeclBuilder(
                        memberCategoryGroupBuilder,
                        LazyFactory,
                        firstSpec.Name,
                        firstSpec.OriginalLexicalID,
                        firstSpec.Category);

                    memberCategoryGroupBuilder.AddLine(newMemberLineBuilder);

                    var inheritedDecls = (from mr in memberRefs
                                          select ResMemberDecl.CreateInheritedDecl(
                                            this,
                                            thisPipelineBuilder,
                                            newMemberLineBuilder,
                                            mr.Range,
                                            mr)).Eager();

                    foreach (var mr in memberRefs)
                    {
                        newMemberLineBuilder.AddTags(mr.Tags);
                    }

                    newMemberLineBuilder.MemberDeclMode = ResMemberDeclMode.Inherited;
                    newMemberLineBuilder.ConcretenessMode = (from mr in memberRefs
                                                             select mr.Decl.Line.ConcretenessMode).Max();


                    newMemberLineBuilder.InheritedDecls = inheritedDecls;

                    //                        facet.AddMemberLine(newMemberLine);

                    /*
                    // Need to determine if new declaration
                    // should be abstract/virtual/final.
                    newDecl.MemberDeclMode = ResMemberDeclMode.Inherited;
                    newDecl.ConcretenessMode = (from mr in memberRefs
                                                select mr.Decl.ConcretenessMode).Max();
                    facet.AddMember( newDecl );
                     */
                }
            }


            Func<IResMemberDecl, SourceRange, IResTerm> makeRef = (m, r) =>
                (new ResMemberSpec(r, thisPipeline, m)).Bind(r,
                    new ResVarRef(r, thisParameter));

            // Now consider direct member decls:
            foreach (var absMemberDecl in absMembers)
            {
                if (absMemberDecl.HasModifier(AbsModifiers.Override))
                    continue;
                ResolveMemberDecl(
                    thisPipelineBuilder,
                    absMemberDecl,
                    innerEnv.NestDiagnostics(),
                    makeRef);
            }

            // Then seal up the list of member lines in each facet
            // (since override decls can't add/remove lines)
            foreach (var facetBuilder in resShaderClassBuilder.Facets)
                facetBuilder.DoneBuilding();

            // Do 'override' decls after sealing up the shader class itself:
            foreach (var absMemberDecl in absMembers)
            {
                if (!absMemberDecl.HasModifier(AbsModifiers.Override))
                    continue;
                ResolveMemberDecl(
                    thisPipelineBuilder,
                    absMemberDecl,
                    innerEnv.NestDiagnostics(),
                    makeRef);
            }

            // Add some checks that should be done after the pipeline has been fully resolved:
            LazyFactory.New(() =>
            {
                var resShaderClass = resShaderClassBuilder.Value;

                // Non-abstract class cannot contain abstract members.
                if (resShaderClass.ConcretenessMode != ResMemberConcretenessMode.Abstract)
                {
                    foreach (var member in resShaderClass.Members)
                    {
                        if (member.Line.ConcretenessMode == ResMemberConcretenessMode.Abstract)
                        {
                            AbstractMemberError(resShaderClass, member.Line, env);
                        }
                    }
                }

                // Find the first 'primary' class in the inheritance
                // chain (other than the direct class...).
                var primaryBase = (from f in resShaderClass.Facets
                                    where f != resShaderClass.DirectFacet
                                    where f.OriginalPipeline.MixinMode == ResMixinMode.Primary
                                    select f.OriginalPipeline).FirstOrDefault();

                // If such a primary base is found, then assert that
                // all the other primary bases are super-classes of it.
                if (primaryBase != null)
                {
                    foreach (var f in resShaderClass.Facets)
                    {
                        if (f == resShaderClass.DirectFacet)
                            continue;

                        if (f.OriginalPipeline.MixinMode != ResMixinMode.Primary)
                            continue;

                        if (IsSubTypeOf(primaryBase, f.OriginalPipeline))
                            continue;

                        if (IsSubTypeOf(f.OriginalPipeline, primaryBase))
                        {
                            // linearization should prevent this, but
                            // it might happen if they gave us a bad
                            // list of bases to begin with...
                            continue;
                        }

                        env.Error(range,
                            "Class '{0}' cannot inherit from disjoint primary classes '{1}' and '{2}'",
                            name,
                            primaryBase,
                            f.OriginalPipeline);

                    }
                }

                return 0;
            });
        }

#if FOOBARBAZ









            } );

            resPipeline.AddPostAction(() =>
            {

            });


            resPipeline.DoneBuilding();

            resModule.AddDecl(resPipeline);
        }
#endif

        // Create a linearized list of superclasses (direct and direct),
        // based on the superclass linearizations of the declared direct
        // base classes.
        //
        // This algorithm is based on that used by the Scala programming
        // language.
        private IResPipelineRef[] Linearize(
            IResPipelineRef[][] linearizations)
        {
            if (linearizations.Length == 0)
                return new IResPipelineRef[] { };

            return linearizations.Reverse().Aggregate(
                Linearize).ToArray();
        }

        private IResPipelineRef[] Linearize(
            IResPipelineRef[] left,
            IResPipelineRef[] right)
        {
            return Linearize(left, 0, right, 0).ToArray();
        }

        private IEnumerable<IResPipelineRef> Linearize(
            IResPipelineRef[] left, int startLeft,
            IResPipelineRef[] right, int startRight )
        {
            if (left.Length <= startLeft)
                return right;
            if (right.Length <= startRight)
                return left;

            var firstLeft = left[startLeft];

            if (Array.FindIndex(right, startRight, (x) => IsSameMemberTerm(x.MemberTerm, firstLeft.MemberTerm)) != -1)
            {
                // the right array contains this value, so we'll get to it eventually
                return Linearize(
                    left, startLeft + 1,
                    right, startRight);
            }
            else
            {
                return new[] { firstLeft }.Concat(
                    Linearize(left, startLeft + 1, right, startRight));
            }
        }

        private void AbstractMemberError(
            IResPipelineDecl pipeline,
            IResMemberLineDecl memberLine,
            ResEnv env)
        {
            if (memberLine.MemberDeclMode == ResMemberDeclMode.Inherited)
            {
                env.Error(
                    pipeline.Range,
                    "Non-abstract pass fails to override inherited member: {0}",
                    memberLine.Name);
            }
            else
            {
                env.Error(
                    memberLine.EffectiveDecl.Range,
                    "Non-abstract pass cannot contain abstract member '{0}'",
                    memberLine.Name);
            }
        }


        private ResFacetDeclBuilder FindOrCreateFacetForBase(
            ResPipelineDeclBuilder derivedPipelineDecl,
            IResPipelineRef basePipelineRef,
            IDictionary<ResFacetDeclBuilder, FacetInfo> facetInfos,
            ResEnv env)
        {
            var derivedFacetDecl = derivedPipelineDecl.FindOrCreateFacetForBase(basePipelineRef);
            var baseFacetRef = basePipelineRef.DirectFacet;

            var baseMemberLines = baseFacetRef.MemberLines.Eager();
            var memberCount = baseMemberLines.Length;

            FacetInfo derivedFacetInfo = null;
            if (!facetInfos.TryGetValue(derivedFacetDecl, out derivedFacetInfo))
            {
                derivedFacetInfo = new FacetInfo {
                    Facet = derivedFacetDecl,
                    MemberLines = new MemberLineInfo[memberCount],
                };
                facetInfos[derivedFacetDecl] = derivedFacetInfo;
                for (int ii = 0; ii < memberCount; ++ii)
                    derivedFacetInfo.MemberLines[ii] =
                        new MemberLineInfo { FirstSpec = baseMemberLines[ii] };

                PopulateFacetsForBase(
                    derivedPipelineDecl,
                    basePipelineRef,
                    facetInfos,
                    env);
            }

            foreach (var b in baseFacetRef.DirectBases)
            {
                var ignored = FindOrCreateFacetForBase(
                    derivedPipelineDecl,
                    b.OriginalPipeline,
                    facetInfos,
                    env);
            }

            return derivedFacetDecl;
        }

        private void PopulateFacetsForBase(
            ResPipelineDeclBuilder derivedPipelineDecl,
            IResPipelineRef basePipelineRef,
            IDictionary<ResFacetDeclBuilder, FacetInfo> facetInfos,
            ResEnv env)
        {
            var baseDirectFacetRef = basePipelineRef.DirectFacet;
            var derivedFacetForBase = FindOrCreateFacetForBase(
                derivedPipelineDecl,
                basePipelineRef,
                facetInfos,
                env);
            foreach (var b in baseDirectFacetRef.DirectBases)
            {
                derivedFacetForBase.AddDirectBase(
                    FindOrCreateFacetForBase(
                        derivedPipelineDecl,
                        b.OriginalPipeline,
                        facetInfos,
                        env));
            }
        }

        private void PopulateMembersForBase(
            ResPipelineDeclBuilder derivedPipelineDecl,
            IResPipelineRef basePipelineRef,
            IDictionary<ResFacetDeclBuilder, FacetInfo> facetInfos,
            ResEnv env)
        {
            foreach (var baseFacetRef in basePipelineRef.Facets)
            {
                var derivedFacetDecl = FindOrCreateFacetForBase(
                    derivedPipelineDecl,
                    baseFacetRef.OriginalPipeline,
                    facetInfos,
                    env);

                var derivedFacetInfo = facetInfos[derivedFacetDecl];

                var baseMemberLines = baseFacetRef.MemberLines.Eager();
                var memberCount = baseMemberLines.Length;

                for (int ii = 0; ii < memberCount; ++ii)
                {
                    var memberSpec = baseMemberLines[ii].EffectiveSpec;
                    var range = memberSpec.Range;
                    var memberRef = memberSpec.Bind(
                        range,
                        new ResVarRef(range, derivedPipelineDecl.ThisParameter));

                    // Skip purely inherited members, since they will be represented
                    // just fine by the original decl(s).
                    if( memberRef.Decl.Line.MemberDeclMode == ResMemberDeclMode.Inherited )
                        continue;

                    derivedFacetInfo.MemberLines[ii].MemberRefs.Add(Tuple.Create(basePipelineRef, memberRef));
                }
            }
        }

        private IResPipelineRef ResolvePipelineRef(
            AbsTerm absTerm,
            ResEnv env)
        {
            var term = ResolveTerm(absTerm, env);
            return CoerceToPipelineRef(term, env);
        }

        private IResPipelineRef CoerceToPipelineRef(
            IResTerm term,
            ResEnv env )
        {
            var pipeline = Coerce(
                term,
                ResKind.PipelineClass,
                env);

            if( pipeline is ResErrorTerm )
                return ResErrorTerm.Instance;

            if( !(pipeline is IResPipelineRef) )
            {
                env.Error(
                    term.Range,
                    "Internal Error: Expected a pipeline reference");
                return ResErrorTerm.Instance;
            }

            return (IResPipelineRef) pipeline;
        }

        private void ResolveMemberDecl(
            IResContainerBuilderRef resContainer,
            AbsMemberDecl absMemberDecl,
            ResEnv env,
            Func<IResMemberDecl, SourceRange, IResTerm> makeRef )
        {
            var category = GetMemberCategory(resContainer.ContainerDecl, absMemberDecl);

            // There are really two cases here: members marked
            // as overriding an existing declaration, and new
            // members.
            if (absMemberDecl.HasModifier(AbsModifiers.Override))
            {
                // We need to find a compatible declaration to override.
                // As a result, we can't pin the resolution of the member
                // to a particular member name/category group yet.
                // This could prove tricky:

                ResolveOverrideMemberDecl(
                    resContainer,
                    category,
                    absMemberDecl,
                    env.NestDiagnostics());
            }
            else
            {
                // We know that this is a new declaration
                // (although we may have to warn if there *was*
                // a compatible inherited declaration and they
                // didn't specify the 'new' keyword).

                var name = absMemberDecl.Name;

                var directFacetBuilder = resContainer.ContainerDecl.DirectFacetBuilder;
                var memberNameGroup = resContainer.ContainerDecl.DirectFacetBuilder.GetMemberNameGroup(name);
                var memberCategoryGroup = memberNameGroup.GetMemberCategoryGroup(category);

                var memberLineBuilder = CreateDirectLine(
                    resContainer,
                    memberCategoryGroup,
                    absMemberDecl,
                    env);

                memberLineBuilder.AddAction(() =>
                {
                    ResolveMemberDecl(
                        resContainer,
                        memberLineBuilder,
                        category,
                        absMemberDecl,
                        env.NestDiagnostics());
                });
            }

#if FOOBARBAZ
            var name = absMemberDecl.Name;
            var memberNameGroup = resContainer.ContainerDecl.DirectFacetBuilder.GetMemberNameGroup(name);
            var category = GetMemberCategory(resContainer.ContainerDecl, absMemberDecl);
            var memberCategoryGroup = memberNameGroup.GetMemberCategoryGroup(category);

            memberCategoryGroup.AddBuildAction(() =>
            {
                ResolveMemberDecl(
                    resContainer,
                    memberCategoryGroup,
                    absMemberDecl,
                    env.NestDiagnostics());
            });
#endif

            /*

            ResMemberDeclGroup memberGroup = null;
            if (absMemberDecl.HasModifier(AbsModifiers.New))
            {
                // The user has explicitly requested a 'new' member,
                // so we will only look for compatible direct
                // member groups to extend.
                memberGroup = (from mg in FindExistingMemberGroups(
                                    resContainer,
                                    absMemberDecl,
                                    env)
                               where mg.Mode == ResMemberDeclMode.Direct
                               select mg).FirstOrDefault();
            }
            else
            {
                // The user has not specified a 'new' member, so look
                // to see if there are any available member groups to extend.
                var memberGroups = FindExistingMemberGroups(
                    resContainer,
                    absMemberDecl,
                    env).ToArray();

                if (memberGroups.Length != 0)
                {
                    if (memberGroups.Length > 1)
                    {
                        env.Error(
                            absMemberDecl.Range,
                            "Cannot determine which member '{0}' to extend.",
                            absMemberDecl.Name);
                    }

                    memberGroup = memberGroups[0];

                    if (memberGroup.Mode != ResMemberDeclMode.Direct
                        && !absMemberDecl.HasModifier(AbsModifiers.Override))
                    {
                        env.Error(
                            absMemberDecl.Range,
                            "Member has same name as inherited member {0}. Use 'override' or 'new' to disambiguate.",
                            memberGroup.Name);
                    }

                    // Checks for overriding/extension of a 'final' member are performed later.
                }
            }
            // If we don't find an existing member group to add our member to, we must create one.
            if (memberGroup == null)
            {
                if (absMemberDecl.HasModifier(AbsModifiers.Override))
                {
                    env.Error(
                        absMemberDecl.Range,
                        "Could not find existing declaration of '{0}' to override.",
                        absMemberDecl.Name);
                }

                memberGroup = CreateNewMemberGroup(resContainer, absMemberDecl);
            }

            memberGroup.AddBuildAction(() =>
            {
                ResolveGroupMemberDecl(
                    resContainer,
                    memberGroup,
                    absMemberDecl,
                    env);
            });*/
        }

        public void ResolveMemberDecl(
            IResContainerBuilderRef resContainer,
            ResMemberLineDeclBuilder resLine,
            ResMemberCategory resCategory,
            AbsMemberDecl absMemberDecl,
            ResEnv env )
        {
            ResolveMemberDeclImpl(
                (dynamic)resContainer,
                resLine,
                (dynamic)resCategory,
                (dynamic)absMemberDecl,
                env);
        }

        public void ResolveOverrideMemberDecl(
            IResContainerBuilderRef resContainer,
            ResMemberCategory resCategory,
            AbsMemberDecl absMemberDecl,
            ResEnv env)
        {
            ResolveOverrideMemberDeclImpl(
                (dynamic)resContainer,
                (dynamic)resCategory,
                (dynamic)absMemberDecl,
                env);
        }

        public void ResolveOverrideMemberDeclImpl(
            IResContainerBuilderRef resContainer,
            ResAttributeCategory resCategory,
            AbsSlotDecl absSlotDecl,
            ResEnv env)
        {
            ResolveMemberDecl(
                resContainer,
                (ResMemberLineDeclBuilder) null,
                resCategory,
                absSlotDecl,
                env);
        }

        public void ResolveOverrideMemberDeclImpl(
            IResContainerBuilderRef resContainer,
            ResMethodCategory resCategory,
            AbsMethodDecl absMethodDecl,
            ResEnv env)
        {
            ResolveMemberDecl(
                resContainer,
                (ResMemberLineDeclBuilder)null,
                resCategory,
                absMethodDecl,
                env);
        }

        public void ResolveOverrideMemberDeclImpl(
            IResContainerBuilderRef resContainer,
            ResMemberCategory resCategory,
            AbsMemberDecl absMemberDecl,
            ResEnv env)
        {
            env.Error(absMemberDecl.Range, "This type of declaration does not support the 'override' keyword");
        }

        private ResMemberCategory GetMemberCategory(
            IResContainerBuilder resContainer,
            AbsMemberDecl absMemberDecl)
        {
            return GetMemberCategoryImpl(
                (dynamic)resContainer,
                (dynamic)absMemberDecl);
        }

        private ResMemberCategory GetMemberCategoryImpl(
            IResContainerBuilder parent,
            AbsStructDecl absMember )
        {
            return new ResStructCategory();
        }

        private ResMemberCategory GetMemberCategoryImpl(
            ResPipelineDeclBuilder parent,
            AbsSlotDecl absMember)
        {
            return new ResAttributeCategory();
        }

        private ResMemberCategory GetMemberCategoryImpl(
            ResStructDeclBuilder parent,
            AbsSlotDecl absMember)
        {
            return new ResFieldCategory();
        }

        private ResMemberCategory GetMemberCategoryImpl(
            IResContainerBuilder parent,
            AbsMethodDecl absMember)
        {
            return new ResMethodCategory();
        }

        private ResMemberCategory GetMemberCategoryImpl(
            IResContainerBuilder parent,
            AbsElementDecl absMember)
        {
            return new ResElementCategory();
        }

        private ResMemberCategory GetMemberCategoryImpl(
            IResContainerBuilder parent,
            AbsTypeSlotDecl absMember)
        {
            return new ResTypeSlotCategory();
        }

        private ResMemberCategory GetMemberCategoryImpl(
            IResContainerBuilder parent,
            AbsConceptDecl absMember)
        {
            return new ResConceptClassCategory();
        }

        private void ResolveMemberDeclImpl(
            IResContainerBuilderRef resContainer,
            ResMemberLineDeclBuilder resLine,
            ResMemberCategory category,
            AbsElementDecl absElementDecl,
            ResEnv env)
        {
            // \todo: Assert that element decl can't be 'override'
            // \todo: Check that element decl doesn't need 'new'

            var resElement = new ResElementDecl(
                resLine,
                absElementDecl.Range,
                absElementDecl.Name);

            resLine.DirectDecl = resElement;
        }



        private bool IsSameKind(
            ResKind left,
            ResKind right)
        {
            return IsSubKindOf(left, right)
                && IsSubKindOf(right, left);
        }


        private IResGenericParamDecl ResolveGenericParam(
            AbsGenericParamDecl absParam,
            ResEnv env,
            ResLocalScope insertScope)
        {
            return ResolveGenericParamImpl(
                (dynamic)absParam,
                env,
                insertScope);
        }

        private IResGenericParamDecl ResolveGenericParamImpl(
            AbsGenericTypeParamDecl absParam,
            ResEnv env,
            ResLocalScope insertScope)
        {
            var resParam = new ResTypeParamDecl(
                absParam.Range,
                absParam.Name,
                ResKind.Star);

            insertScope.Insert(resParam.Name, (r) => new ResTypeVarRef(r, resParam));

            return resParam;
        }

        private IResGenericParamDecl ResolveGenericParamImpl(
            AbsGenericValueParamDecl absParam,
            ResEnv env,
            ResLocalScope insertScope)
        {
            var resFlags = ResVarFlags.None;
            if (absParam.IsImplicit)
                resFlags |= ResVarFlags.Implicit;

            var resParam = new ResVarDecl(
                absParam.Range,
                absParam.Name,
                LazyFactory.New(() =>
                {
                    var resTerm = ResolveTerm(
                        absParam.Type,
                        env);
                    // \todo: Needs to either be a type
                    // of kind * or @F T for some T of kind *... :(
                    var resType = CoerceToTypeExp(resTerm, env);
                    return resType;
                }),
                resFlags);

            insertScope.Insert(resParam.Name, (r) => new ResVarRef(r, resParam));

            return resParam;
        }


        private void ResolveMemberDeclImpl(
            IResContainerBuilderRef resContainer,
            ResMemberLineDeclBuilder resLineBuilder,
            ResAttributeCategory category,
            AbsSlotDecl absAttributeDecl,
            ResEnv env)
        {
            IResFreqQualType resType = null;
            if (absAttributeDecl.Type != null)
            {
                resType = (IResFreqQualType)Coerce(
                    ResolveTerm(absAttributeDecl.Type, env),
                    ResKind.FreqQualType,
                    env);
            }

            if (resLineBuilder == null)
            {
                resLineBuilder = FindOrCreateAttributeLine(
                    resContainer,
                    category,
                    absAttributeDecl,
                    ref resType,
                    env);
            }

            var prevDecl = (ResAttributeDecl)resLineBuilder.EffectiveDecl;

            bool isInput = absAttributeDecl.HasModifier(AbsModifiers.Input);
            bool isOutput = absAttributeDecl.HasModifier(AbsModifiers.Output);
            bool isOptional = absAttributeDecl.HasModifier(AbsModifiers.Optional);

            if (prevDecl != null)
            {
                if (prevDecl.IsInput())
                {
                    env.Error(
                        absAttributeDecl.Range,
                        "Cannot override 'input' attribute");
                    isInput = true;
                }

                if (prevDecl.IsOutput())
                {
                    isOutput = true;
                }

                if (isOptional && !prevDecl.IsOptional())
                {
                    env.Error(
                        absAttributeDecl.Range,
                        "Cannot make non-optional attribute optional.");
                }

                if (prevDecl.IsOptional())
                {
                    isOptional = true;
                }
            }

            var resAttribute = ResAttributeDecl.Build(
                LazyFactory,
                resLineBuilder,
                absAttributeDecl.Range,
                absAttributeDecl.Name,
                (builder) =>
                {
                    if (isInput)
                        builder.Flags |= ResAttributeFlags.Input;
                    if (isOutput)
                        builder.Flags |= ResAttributeFlags.Output;
                    if (isOptional)
                        builder.Flags |= ResAttributeFlags.Optional;

                    builder.Type = resType;

                    var resLine = resLineBuilder.Value;

                    if (absAttributeDecl.Init != null)
                    {
                        if (resLine.ConcretenessMode == ResMemberConcretenessMode.Abstract)
                        {
                            env.Error(
                                absAttributeDecl.Range,
                                "Abstract attribute cannot have an initializer");
                        }
                        if (isInput)
                        {
                            env.Error(
                                absAttributeDecl.Range,
                                "Input attribute cannot have an initializer");
                        }

                        var initEnv = env.NestDiagnostics().NestBaseAttributeType(resType);
                        var lazyInit = initEnv.Lazy(() =>
                        {
                            var initTerm = ResolveTerm(absAttributeDecl.Init, initEnv);
                            var init = Coerce(initTerm, resType, initEnv);
                            return init;
                        });

                        builder.LazyInit = lazyInit;
                    }
                    else
                    {
                        if (resLine.ConcretenessMode != ResMemberConcretenessMode.Abstract)
                        {
                            if (!isInput && !isOptional)
                            {
                                if (!resLine.Tags.Any((tag) => tag is ResBuiltinTag))
                                {
                                    env.Error(
                                        absAttributeDecl.Range,
                                        "Non-abstract, non-input attribute must have an initializer");
                                }
                            }
                        }
                    }

                });
            resLineBuilder.DirectDecl = resAttribute;
        }

#if FOOBARBAZ

        private void ResolveMemberDeclImpl(
            IResContainerBuilderRef resContainer,
            ResMemberLineDeclBuilder resLine,
            ResAttributeCategory category,
            AbsSlotDecl absAttributeDecl,
            ResEnv env)
        {
            IResFreqQualType resType = null;
            if (absAttributeDecl.Type != null)
            {
                resType = (IResFreqQualType)Coerce(
                    ResolveTerm(absAttributeDecl.Type, env),
                    ResKind.FreqQualType,
                    env);
            }

            var resMemberLineBuilder = FindOrCreateAttributeLine(
                resContainer,
                resGroup,
                absAttributeDecl,
                ref resType,
                env);

            var prevDecl = (ResAttributeDecl)resMemberLineBuilder.EffectiveDecl;

            bool isInput = absAttributeDecl.HasModifier(AbsModifiers.Input);
            bool isOutput = absAttributeDecl.HasModifier(AbsModifiers.Output);
            bool isOptional = absAttributeDecl.HasModifier(AbsModifiers.Optional);

            if (prevDecl != null)
            {
                if (prevDecl.IsInput())
                {
                    env.Error(
                        absAttributeDecl.Range,
                        "Cannot override 'input' attribute");
                    isInput = true;
                }

                if (prevDecl.IsOutput())
                {
                    isOutput = true;
                }

                if (isOptional && !prevDecl.IsOptional())
                {
                    env.Error(
                        absAttributeDecl.Range,
                        "Cannot make non-optional attribute optional.");
                }

                if (prevDecl.IsOptional())
                {
                    isOptional = true;
                }
            }

            var resAttribute = ResAttributeDecl.Build(
                LazyFactory,
                resMemberLineBuilder,
                absAttributeDecl.Range,
                absAttributeDecl.Name,
                (builder) =>
            {
                if (isInput)
                    builder.Flags |= ResAttributeFlags.Input;
                if (isOutput)
                    builder.Flags |= ResAttributeFlags.Output;
                if (isOptional)
                    builder.Flags |= ResAttributeFlags.Optional;

                builder.Type = resType;

                var resLine = resMemberLineBuilder.Value;

                if( absAttributeDecl.Init != null )
                {
                    if (resLine.ConcretenessMode == ResMemberConcretenessMode.Abstract)
                    {
                        env.Error(
                            absAttributeDecl.Range,
                            "Abstract attribute cannot have an initializer" );
                    }
                    if( isInput )
                    {
                        env.Error(
                            absAttributeDecl.Range,
                            "Input attribute cannot have an initializer" );
                    }

                    var initEnv = env.NestDiagnostics().NestBaseAttributeType(resType);
                    var lazyInit = initEnv.Lazy(() =>
                    {
                        var initTerm = ResolveTerm(absAttributeDecl.Init, initEnv);
                        var init = Coerce(initTerm, resType, initEnv);
                        return init;
                    } );

                    builder.LazyInit = lazyInit;
                }
                else
                {
                    if (resLine.ConcretenessMode != ResMemberConcretenessMode.Abstract)
                    {
                        if( !isInput && !isOptional )
                        {
                            if (!resLine.Tags.Any((tag) => tag is ResBuiltinTag))
                            {
                                env.Error(
                                    absAttributeDecl.Range,
                                    "Non-abstract, non-input attribute must have an initializer" );
                            }
                        }
                    }
                }

            });
            resMemberLineBuilder.DirectDecl = resAttribute;
        }
#endif

        private ResMemberLineDeclBuilder FindOrCreateAttributeLine(
            IResContainerBuilderRef resContainer,
            ResMemberCategory category,
            AbsSlotDecl absAttribute,
            ref IResFreqQualType resType,
            ResEnv env)
        {
            var matchType = resType;

            var emptySubst = new Substitution();

            if (!absAttribute.HasModifier(AbsModifiers.New))
            {
                var candidates = (from facet in resContainer.ContainerDecl.InheritedFacets
                                  let mng = facet.FindMemberNameGroup(absAttribute.Name)
                                  where mng != null
                                  let mcg = mng.FindMemberCategoryGroup(category)
                                  where mcg != null
                                  from ml in mcg.Lines
                                  let decl = ml.EffectiveDecl
                                  where IsAttributeOverloadMatch(
                                    ml,
                                    decl,
                                    absAttribute,
                                    matchType,
                                    env)
                                  select ml).ToArray();

                if (candidates.Length > 0)
                {
                    if (candidates.Length > 1)
                    {
                        env.Error(
                            absAttribute.Range,
                            "Ambiguous attribute override");
                    }

                    var result = candidates[0];
                    resType = ((ResAttributeDecl)result.EffectiveDecl).Type;
                    UpdateExtendedMemberLine(
                            result,
                            absAttribute,
                            env);
                    return result;
                }
                else
                {
                    if (absAttribute.HasModifier(AbsModifiers.Override))
                    {
                        env.Error(
                            absAttribute.Range,
                            "No matching declaration of '{0}' found to override",
                            absAttribute.Name);
                    }
                }
            }

            if( resType == null )
            {
                env.Error(
                        absAttribute.Range,
                        "Attribute '{0}' must declare a type",
                        absAttribute.Name);
                resType = ResErrorTerm.Instance;
            }
            else if (resType is ResErrorTerm
                || resType.Freq is ResErrorTerm
                || resType.Type is ResErrorTerm)
            {
            }
            else
            {
                if (absAttribute.Init == null
                    && !absAttribute.HasModifier(AbsModifiers.Abstract)
                    && resType.Freq.Decl.IsConcrete()
                    && resType.Freq.Decl.Line.MemberDeclMode != ResMemberDeclMode.Direct)
                {
                    // \todo: This isn't quite right. We want to catch cases where
                    // the user adds a new attribute to an element that is concrete,
                    // but *not* in cases where the element was just declared concrete
                    // (for the first time) in the current pass.
                    env.Error(
                        absAttribute.Range,
                        "Cannot add new input attribute '{0}' to inherited concrete element '{1}'",
                        absAttribute.Name,
                        resType.Freq);
                }
            }

            return CreateDirectLine(
                resContainer,
                resContainer.ContainerDecl.DirectFacetBuilder.GetMemberNameGroup(absAttribute.Name).GetMemberCategoryGroup(category),
                absAttribute,
                env);
        }

        private bool IsAttributeOverloadMatch(
            ResMemberLineDeclBuilder resLine,
            IResMemberDecl resDecl,
            AbsSlotDecl absAttribute,
            IResFreqQualType resType,
            ResEnv env)
        {
            if (resDecl is ResAttributeDecl)
            {
                var resAttribute = (ResAttributeDecl) resDecl;

                if (resLine.MemberDeclMode!= ResMemberDeclMode.Inherited)
                    return false;

                if (resLine.ConcretenessMode == ResMemberConcretenessMode.Final)
                    return false;

                if ((resType != null) && !IsSameType(resType, resAttribute.Type))
                    return false;

                return true;
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private void UpdateExtendedMemberLine(
            ResMemberLineDeclBuilder resLine,
            AbsMemberDecl absDecl,
            ResEnv env)
        {
            if (!absDecl.HasModifier(AbsModifiers.Override))
            {
                env.Error(absDecl.Range,
                    "Declaration of '{0}' matches previous declaration. It must either be declared 'new' or 'override'.",
                    absDecl.Name);
            }

            if (resLine.ConcretenessMode == ResMemberConcretenessMode.Final)
            {
                env.Error(absDecl.Range,
                    "Cannot override 'final' declaration of '{0}'",
                    absDecl.Name);
            }

            resLine.MemberDeclMode = ResMemberDeclMode.Extended;
            resLine.ConcretenessMode = ResMemberConcretenessMode.Virtual;

            UpdateLineTagsAndModes(
                resLine,
                absDecl,
                env);
        }

        private void ResolveMemberDeclImpl(
            IResContainerBuilderRef resContainer,
            ResMemberLineDeclBuilder resLineBuilder,
            ResFieldCategory category,
            AbsSlotDecl absFieldDecl,
            ResEnv env)
        {
            var resField = ResFieldDecl.Build(
                LazyFactory,
                resLineBuilder,
                absFieldDecl.Range,
                absFieldDecl.Name,
                (builder) =>
            {
                var type = ResolveTypeExp(absFieldDecl.Type, env);
                var initTerm = ResolveTerm(absFieldDecl.Init, env);
                var init = CoerceUnqualified(initTerm, type, env);

                builder.Init = init;
                builder.Type = type;
            });
            resLineBuilder.DirectDecl = resField;
        }

        public void ResolveMemberDeclImpl(
            IResContainerBuilderRef resContainer,
            ResMemberLineDeclBuilder resLineBuilder,
            ResMethodCategory category,
            AbsMethodDecl absMethodDecl,
            ResEnv env)
        {
            var genericParamScope = new ResLocalScope();
            var genericParamEnv = env.NestScope(genericParamScope);

            IResGenericParamDecl[] resGenericParams = null;
            if (absMethodDecl.GenericParams != null)
            {
                resGenericParams = (from p in absMethodDecl.GenericParams
                                    select ResolveGenericParam(p, genericParamEnv, genericParamScope)).Eager();
            }
            if (resGenericParams != null)
            {
                foreach (var resValParam in resGenericParams.OfType<ResVarDecl>())
                {
                    if (!resValParam.IsImplicit()) continue;

                    var resParamContainerType = resValParam.Type as IResContainerRef;
                    if (resParamContainerType == null) continue;

                    genericParamEnv = genericParamEnv.NestScope(
                        new ResPipelineScope(
                            resParamContainerType,
                            resValParam));

                }
            }

            var paramScope = new ResLocalScope();
            var paramEnv = genericParamEnv.NestScope(paramScope);

            var resParams = (from absParam in absMethodDecl.parameters
                             select ResolveParamDecl(absParam, paramScope, paramEnv)).Eager();

            if (resLineBuilder == null)
            {
                resLineBuilder = FindOrCreateMethod(
                    resContainer,
                    absMethodDecl.Name,
                    category,
                    absMethodDecl,
                    resParams,
                    resGenericParams,
                    env);
            }

            var resMethod = ResMethodDecl.Build(
                LazyFactory,
                resLineBuilder,
                absMethodDecl.Range,
                absMethodDecl.Name,
                (builder) =>
                {
                    var resLine = resLineBuilder.Value;

                    var tags = (from absTag in absMethodDecl.Attributes
                                let tag = ResolveTag(absTag, env)
                                where tag != null
                                select tag).Eager();
                    resLineBuilder.AddTags(tags);

                    builder.Parameters = resParams;

                    var resultType = ResolveTypeExp(absMethodDecl.resultType, paramEnv);
                    builder.ResultType = resultType;

                    IResElementRef implicitFreq = null;
                    if (resultType is ResFreqQualType)
                    {
                        var resultFQType = (ResFreqQualType)resultType;
                        var resultFreq = resultFQType.Freq;

                        // All parameters must have frequencies...
                        foreach (var p in resParams)
                        {
                            if (!(p.Type is ResFreqQualType))
                            {
                                env.Error(
                                    p.Range,
                                    "Method must declare an explicit frequency on every parameter");
                            }
                        }

                        if (resParams.Count() == 0
                            ||
                            resParams.All((p) => (p.Type is ResFreqQualType)
                            && IsSameType(
                                ((IResFreqQualType)p.Type).Freq,
                                resultFreq)))
                        {
                            implicitFreq = resultFreq;
                            builder.Flavor = ResMethodFlavor.SingleFreq;
                        }
                        else
                        {
                            builder.Flavor = ResMethodFlavor.Conversion;
                        }
                    }
                    else
                    {
                        // Parameters must not have frequencies
                        foreach (var p in resParams)
                        {
                            if (p.Type is ResFreqQualType)
                            {
                                env.Error(
                                    p.Range,
                                    "Method with ordinary return type cannot have frequency-qualified parameter type");
                            }
                        }

                        builder.Flavor = ResMethodFlavor.Ordinary;
                    }

                    // Now deal with the method body
                    if (absMethodDecl.body == null)
                    {
                        if (resLine.ConcretenessMode != ResMemberConcretenessMode.Abstract)
                        {
                            if (!resLine.Tags.Any((tag) => tag is ResBuiltinTag))
                            {
                                env.Error(
                                    absMethodDecl.Range,
                                    "Non-abstract method '{0}' must declare a body",
                                    absMethodDecl.Name);
                            }
                        }
                    }
                    else
                    {
                        var absBody = absMethodDecl.body;
                        var bodyEnv = paramEnv.NestImplicitFreq(implicitFreq);

                        var resLazyBody = LazyFactory.New(() =>
                        {
                            var returnTarget = new ResLabel(
                                absBody.Range,
                                _identifiers.unique("return"),
                                resultType);
                            var stmtContext = new StmtContext
                            {
                                ReturnTarget = returnTarget,
                                ImplicitFreq = implicitFreq,
                            };
                            var bodyStmt = ResolveStmt(absMethodDecl.body, bodyEnv, stmtContext);

                            var resBody = new ResLabelExp(
                                absBody.Range,
                                returnTarget,
                                bodyStmt);

                            return resBody;
                        });

                        builder.LazyBody = resLazyBody;
                    }
                });

            IResMemberDecl decl = resMethod;
            if (resGenericParams != null)
            {
                var resGeneric = ResGenericDecl.Build(
                    LazyFactory,
                    resLineBuilder,
                    absMethodDecl.Range,
                    absMethodDecl.Name,
                    (builder) =>
                    {
                        builder.Parameters = resGenericParams;
                        builder.InnerDecl = resMethod;
                    });

                decl = resGeneric;
            }
            resLineBuilder.DirectDecl = decl;
        }

#if FOOBARBAZ
        public void ResolveMemberDeclImpl(
            IResContainerBuilderRef resContainer,
            ResMemberLineDeclBuilder resLineBuilder,
            ResMethodCategory category,
            AbsMethodDecl absMethodDecl,
            ResEnv env)
        {
            var genericParamScope = new ResLocalScope();
            var genericParamEnv = env.NestScope(genericParamScope);

            IResGenericParamDecl[] resGenericParams = null;
            if (absMethodDecl.GenericParams != null)
            {
                resGenericParams = (from p in absMethodDecl.GenericParams
                                    select ResolveGenericParam(p, genericParamEnv, genericParamScope)).Eager();
            }
            if (resGenericParams != null)
            {
                foreach (var resValParam in resGenericParams.OfType<ResVarDecl>())
                {
                    if (!resValParam.IsImplicit()) continue;

                    var resParamContainerType = resValParam.Type as IResContainerRef;
                    if (resParamContainerType == null) continue;

                    genericParamEnv = genericParamEnv.NestScope(
                        new ResPipelineScope(
                            resParamContainerType,
                            resValParam));

                }
            }
            /*
            if (absMethodDecl.WhereClauses != null)
            {
                if (resGenericParams == null)
                {
                    env.Error(
                        absMethodDecl.Range,
                        "Method '{0}' cannot have 'where' clause without generic parameters",
                        absMethodDecl.Name);
                    resGenericParams = new IResGenericParamDecl[] { };
                }

                resGenericParams = resGenericParams.Concat(
                    (from c in absMethodDecl.WhereClauses
                     select ResolveWhereClause(resGroup, c, genericParamEnv))).Eager();
            }



                foreach (var resWhere in resGenericParams.OfType<ResConceptParamDecl>())
                {
                    genericParamEnv = genericParamEnv.NestScope(
                        new ResPipelineScope(
                            resWhere.ConceptClass,
                            resWhere));
                }
            }*/

            var paramScope = new ResLocalScope();
            var paramEnv = genericParamEnv.NestScope(paramScope);

            var resParams = (from absParam in absMethodDecl.parameters
                             select ResolveParamDecl(absParam, paramScope, paramEnv)).Eager();

            var resLineBuilder = FindOrCreateMethod(
                resContainer,
                absMethodDecl.Name,
                category,
                absMethodDecl,
                resParams,
                resGenericParams,
                env);


            var resMethod = ResMethodDecl.Build(
                LazyFactory,
                resLineBuilder,
                absMethodDecl.Range,
                absMethodDecl.Name,
                (builder) =>
            {
                var resLine = resLineBuilder.Value;

                var tags = (from absTag in absMethodDecl.Attributes
                            let tag = ResolveTag(absTag, env)
                            where tag != null
                            select tag).Eager();
                resLineBuilder.AddTags(tags);

                builder.Parameters = resParams;

                var resultType = ResolveTypeExp(absMethodDecl.resultType, paramEnv);
                builder.ResultType = resultType;

                IResElementRef implicitFreq = null;
                if (resultType is ResFreqQualType)
                {
                    var resultFQType = (ResFreqQualType)resultType;
                    var resultFreq = resultFQType.Freq;

                    // All parameters must have frequencies...
                    foreach (var p in resParams)
                    {
                        if (!(p.Type is ResFreqQualType))
                        {
                            env.Error(
                                p.Range,
                                "Method must declare an explicit frequency on every parameter");
                        }
                    }

                    if (resParams.Count() == 0
                        ||
                        resParams.All((p) => (p.Type is ResFreqQualType)
                        && IsSameType(
                            ((IResFreqQualType)p.Type).Freq,
                            resultFreq)))
                    {
                        implicitFreq = resultFreq;
                        builder.Flavor = ResMethodFlavor.SingleFreq;
                    }
                    else
                    {
                        builder.Flavor = ResMethodFlavor.Conversion;
                    }
                }
                else
                {
                    // Parameters must not have frequencies
                    foreach (var p in resParams)
                    {
                        if (p.Type is ResFreqQualType)
                        {
                            env.Error(
                                p.Range,
                                "Method with ordinary return type cannot have frequency-qualified parameter type");
                        }
                    }

                    builder.Flavor = ResMethodFlavor.Ordinary;
                }

                // Now deal with the method body
                if (absMethodDecl.body == null)
                {
                    if (resLine.ConcretenessMode != ResMemberConcretenessMode.Abstract)
                    {
                        if (!resLine.Tags.Any((tag) => tag is ResBuiltinTag))
                        {
                            env.Error(
                                absMethodDecl.Range,
                                "Non-abstract method '{0}' must declare a body",
                                absMethodDecl.Name);
                        }
                    }
                }
                else
                {
                    var absBody = absMethodDecl.body;
                    var bodyEnv = paramEnv.NestImplicitFreq(implicitFreq);

                    var resLazyBody = LazyFactory.New(() =>
                    {
                        var returnTarget = new ResLabel(
                            absBody.Range,
                            _identifiers.unique("return"),
                            resultType);
                        var stmtContext = new StmtContext
                        {
                            ReturnTarget = returnTarget,
                            ImplicitFreq = implicitFreq,
                        };
                        var bodyStmt = ResolveStmt(absMethodDecl.body, bodyEnv, stmtContext);

                        var resBody = new ResLabelExp(
                            absBody.Range,
                            returnTarget,
                            bodyStmt);

                        return resBody;
                    });

                    builder.LazyBody = resLazyBody;
                }
            });

            IResMemberDecl decl = resMethod;
            if (resGenericParams != null)
            {
                var resGeneric = ResGenericDecl.Build(
                    LazyFactory,
                    resLineBuilder,
                    absMethodDecl.Range,
                    absMethodDecl.Name,
                    (builder) =>
                {
                    builder.Parameters = resGenericParams;
                    builder.InnerDecl = resMethod;
                });

                decl = resGeneric;
            }
            resLineBuilder.DirectDecl = decl;
        }
#endif

        /*
        private IResGenericParamDecl ResolveWhereClause(
            IBuilder parentBuilder,
            AbsWhereClause absWhere,
            ResEnv env)
        {
            var result = new ResParamDecl(
                parentBuilder,
                absWhere.Range,
                _identifiers.unique("_"));

            result.AddBuildAction(() =>
            {
                var resTerm = ResolveTerm(
                    absWhere.Constraint,
                    env);

                var resConceptClass = (IResConceptClassRef) Coerce(
                    resTerm,
                    ResKind.ConceptClass,
                    env);

                result.Type = resConceptClass;
            });
            result.DoneBuilding();
            return result;
        }*/

        private ResMemberLineDeclBuilder FindOrCreateMethod(
            IResContainerBuilderRef resContainer,
            Identifier name,
            ResMemberCategory category,
            AbsMethodDecl absMethodDecl,
            IResVarDecl[] resParams,
            IResGenericParamDecl[] resGenericParams,
            ResEnv env)
        {
            var emptySubst = new Substitution();

            if (!absMethodDecl.HasModifier(AbsModifiers.New))
            {
                var candidates = (from facet in resContainer.ContainerDecl.InheritedFacets
                                  let mng = facet.FindMemberNameGroup(name)
                                  where mng != null
                                  let mcg = mng.FindMemberCategoryGroup(category)
                                  where mcg != null
                                  from ml in mcg.Lines
                                  let decl = ml.EffectiveDecl
                                  where IsMethodOverloadMatch(
                                            ml,
                                            decl,
                                            absMethodDecl,
                                            resParams,
                                            resGenericParams,
                                            emptySubst)
                                  select ml).ToArray();

                if (candidates.Length > 0)
                {
                    if (candidates.Length > 1)
                    {
                        env.Error(
                            absMethodDecl.Range,
                            "Ambiguous method override");
                    }

                    var result = candidates[0];
                    UpdateExtendedMemberLine(
                            result,
                            absMethodDecl,
                            env);
                    return result;
                }
                else
                {
                    if (absMethodDecl.HasModifier(AbsModifiers.Override))
                    {
                        env.Error(
                            absMethodDecl.Range,
                            "No matching declaration of '{0}' found to override",
                            absMethodDecl.Name);
                    }
                }
            }

            return CreateDirectLine(
                resContainer,
                resContainer.ContainerDecl.DirectFacetBuilder.GetMemberNameGroup(name).GetMemberCategoryGroup(category),
                absMethodDecl,
                env);
        }

        private ResMemberLineDeclBuilder CreateDirectLine(
            IResContainerBuilderRef resContainer,
            ResMemberCategoryGroupBuilder resGroup,
            AbsMemberDecl absMemberDecl,
            ResEnv env)
        {
            var result = new ResMemberLineDeclBuilder(
                resGroup,
                LazyFactory,
                absMemberDecl.Name,
                new ResLexicalID(),
                resGroup.Category);

            resGroup.AddLine(result);
        //            resContainer.ContainerDecl.AddDirectMemberLine(result);

            result.MemberDeclMode = ResMemberDeclMode.Direct;

            UpdateLineTagsAndModes(
                result,
                absMemberDecl,
                env);

            return result;
        }

        private void UpdateLineTagsAndModes(
            ResMemberLineDeclBuilder resLine,
            AbsMemberDecl absMemberDecl,
            ResEnv env)
        {
            foreach (var absTag in absMemberDecl.Attributes)
            {
                var resTag = ResolveTag(absTag, env);
                resLine.AddTag(resTag);
            }

            if (absMemberDecl.HasModifier(AbsModifiers.Abstract))
                resLine.ConcretenessMode = ResMemberConcretenessMode.Abstract;
            if (absMemberDecl.HasModifier(AbsModifiers.Virtual))
                resLine.ConcretenessMode = ResMemberConcretenessMode.Virtual;
            if (absMemberDecl.HasModifier(AbsModifiers.Final))
                resLine.ConcretenessMode = ResMemberConcretenessMode.Final;

            if (absMemberDecl.HasModifier(AbsModifiers.Implicit))
            {
                resLine.AddTag(new ResImplicitTag());
            }

            if (absMemberDecl.HasModifier(AbsModifiers.Concrete))
            {
                resLine.AddTag(new ResConcreteTag());
            }
        }

        private bool IsMethodOverloadMatch(
            ResMemberLineDeclBuilder resLine,
            IResMemberDecl resDecl,
            AbsMethodDecl absDecl,
            IResVarDecl[] resParams,
            IResGenericParamDecl[] resGenericParams,
            Substitution subst)
        {
            if (resDecl is ResMethodDecl)
            {
                if (resGenericParams != null)
                    return false;

                var resMethod = (ResMethodDecl)resDecl;

                if (resLine.MemberDeclMode != ResMemberDeclMode.Inherited)
                    return false;

                if (resLine.ConcretenessMode == ResMemberConcretenessMode.Final)
                    return false;

                var otherParams = resMethod.Parameters.Eager();

                if (resParams.Length != otherParams.Length)
                    return false;

                int paramCount = resParams.Length;
                for (int ii = 0; ii < paramCount; ++ii)
                {
                    var resParam = resParams[ii];
                    var otherParam = otherParams[ii];

                    if (!IsSameType(resParam.Type.Substitute(subst), otherParam.Type))
                        return false;
                }

                return true;
            }
            else if (resDecl is ResGenericDecl)
            {
                if (resGenericParams == null)
                    return false;

                var resGeneric = (ResGenericDecl) resDecl;

                var otherGenericParams = resGeneric.Parameters.Eager();
                if (resGenericParams.Length != otherGenericParams.Length)
                    return false;

                int genericParamCount = resGenericParams.Length;

                var newSubst = new Substitution(subst);
                for (int ii = 0; ii < genericParamCount; ++ii)
                {
                    var resParam = resGenericParams[ii];
                    var otherParam = otherGenericParams[ii];

                    if( resParam is IResTypeParamDecl )
                    {
                        if( !(otherParam is IResTypeParamDecl) )
                            return false;

                        var resTypeParam = (IResTypeParamDecl) resParam;
                        var otherTypeParam = (IResTypeParamDecl) otherParam;

                        newSubst.Insert(
                            resTypeParam,
                            (r) => new ResTypeVarRef(r, otherTypeParam));
                    }
                    else if( resParam is IResValueParamDecl )
                    {
                        if (!(otherParam is IResValueParamDecl))
                            return false;

                        var resValParam = (IResVarDecl)resParam;
                        var otherValParam = (IResVarDecl)otherParam;

                        newSubst.Insert(
                            resValParam,
                            (r) => new ResVarRef(r, otherValParam));
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                }

                for (int ii = 0; ii < genericParamCount; ++ii)
                {
                    var resParam = resGenericParams[ii];
                    var otherParam = otherGenericParams[ii];

                    if( resParam is IResTypeParamDecl )
                    {
                        var resTypeParam = (IResTypeParamDecl) resParam;
                        var otherTypeParam = (IResTypeParamDecl) otherParam;

                        if (!IsSameKind(resTypeParam.Kind.Substitute(newSubst), otherTypeParam.Kind))
                            return false;
                    }
                    else if( resParam is IResValueParamDecl )
                    {
                        var resValParam = (IResVarDecl)resParam;
                        var otherValParam = (IResVarDecl)otherParam;

                        if (!IsSameType(resValParam.Type.Substitute(newSubst), otherValParam.Type))
                            return false;
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                }

                return IsMethodOverloadMatch(
                    resLine,
                    resGeneric.InnerDecl,
                    absDecl,
                    resParams,
                    null,
                    newSubst);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private void ResolveMemberDeclImpl(
            IResContainerBuilderRef resContainer,
            ResMemberLineDeclBuilder resLineBuilder,
            ResStructCategory category,
            AbsStructDecl absStruct,
            ResEnv env)
        {
            var resStruct = ResStructDecl.Build(
                LazyFactory,
                resLineBuilder,
                absStruct.Range,
                absStruct.Name,
                (builder) =>
            {
                ResStructBuilderRef thisStructBuilder = new ResStructBuilderRef(
                    builder );

                var thisStruct = (IResStructRef)resContainer.CreateMemberRef(
                    absStruct.Range,
                    builder.Value);

                var thisParameter = new ResVarDecl(
                    absStruct.Range,
                    _identifiers.simpleIdentifier("this"),
                    thisStruct);

                builder.ThisStruct = thisStruct;
                builder.ThisParameter = thisParameter;

                var memberEnv = env;
                foreach (var absMember in absStruct.Members)
                {
                    ResolveMemberDecl(
                        thisStructBuilder,
                        absMember,
                        memberEnv,
                        makeRef: null);
                }
            });
            resLineBuilder.DirectDecl = resStruct;
        }

        private void ResolveMemberDeclImpl(
            IResContainerBuilderRef resContainer,
            ResMemberLineDeclBuilder resLineBuilder,
            ResTypeSlotCategory category,
            AbsTypeSlotDecl absTypeDecl,
            ResEnv env)
        {
            // No overriding/overloading allowed...
            // Need to ensure this... :(

            var resTypeDecl = new ResTypeSlotDecl(
                resLineBuilder,
                absTypeDecl.Range,
                absTypeDecl.Name);

            var tags = (from absTag in absTypeDecl.Attributes
                        let tag = ResolveTag(absTag, env)
                        where tag != null
                        select tag).Eager();
            resLineBuilder.AddTags(tags);

            var resGenericParamScope = new ResLocalScope();
            var resGenericParamEnv = env.NestScope(resGenericParamScope);

            IResMemberDecl decl = resTypeDecl;
            if (absTypeDecl.GenericParams != null)
            {
                var resGenericParams = (from p in absTypeDecl.GenericParams
                                        select ResolveGenericParam(p, resGenericParamEnv, resGenericParamScope)).Eager();

                var resGeneric = ResGenericDecl.Build(
                    LazyFactory,
                    resLineBuilder,
                    absTypeDecl.Range,
                    absTypeDecl.Name,
                    (builder) =>
                {
                    builder.Parameters = resGenericParams;
                    builder.InnerDecl = resTypeDecl;
                });

                decl = resGeneric;
            }

            resLineBuilder.DirectDecl = decl;
        }

        private void ResolveMemberDeclImpl(
            IResContainerBuilderRef resContainer,
            ResMemberLineDeclBuilder resLineBuilder,
            ResConceptClassCategory category,
            AbsConceptDecl absDecl,
            ResEnv env)
        {
            // No overriding/overloading allowed...
            // Need to ensure this... :(

            var resGenericParamScope = new ResLocalScope();
            var resGenericParamEnv = env.NestScope(resGenericParamScope);

            var resConceptClassDecl = ResConceptClassDecl.Build(
                LazyFactory,
                resLineBuilder,
                absDecl.Range,
                absDecl.Name,
                (builder) =>
            {
                var thisConceptClassBuilder = new ResConceptClassBuilderRef(
                    absDecl.Range,
                    builder);

                var thisConceptClass = (IResConceptClassRef)resContainer.CreateMemberRef(
                    absDecl.Range,
                    builder.Value);

                var thisParameter = new ResVarDecl(
                    absDecl.Range,
                    _identifiers.simpleIdentifier("this"),
                    thisConceptClass);

                builder.ThisConceptClass = thisConceptClass;
                builder.ThisParameter = thisParameter;

                var innerEnv = resGenericParamEnv.NestScope(
                    new ResPipelineScope(thisConceptClass, thisParameter));

                var memberEnv = innerEnv;
                foreach (var absMember in absDecl.Members)
                {
                    ResolveMemberDecl(
                        thisConceptClassBuilder,
                        absMember,
                        memberEnv,
                        makeRef: null);
                }
            });

            IResMemberDecl resDecl = null;
            if (absDecl.GenericParams != null)
            {
                var resGenericParams = (from p in absDecl.GenericParams
                                        select ResolveGenericParam(p, resGenericParamEnv, resGenericParamScope)).Eager();

                var resGeneric = ResGenericDecl.Build(
                    LazyFactory,
                    resLineBuilder,
                    absDecl.Range,
                    absDecl.Name,
                    (builder) =>
                {
                    builder.Parameters = resGenericParams;
                    builder.InnerDecl = resConceptClassDecl;
                });

                resDecl = resGeneric;
            }

            resLineBuilder.DirectDecl = resDecl;
        }

        private ResTag ResolveTag(
            AbsAttribute absTag,
            ResEnv env)
        {
            var args = absTag.Args.Eager();

            if (absTag.Name == _identifiers.simpleIdentifier("Builtin"))
            {
                var profile = ResolveTagString(args[0]);
                var template = ResolveTagString(args[1]);
                return new ResBuiltinTag(profile, template);
            }

            env.Error(absTag.Range, "Uknown meta-data tag");
            return null;
        }

        private string ResolveTagString(AbsArg absArg)
        {
            var positionArg = (AbsPositionalArg)absArg;
            var stringLit = (AbsLit<string>)positionArg.Term;
            return stringLit.Value;
        }

        private IResVarDecl ResolveParamDecl(
            AbsParamDecl absParam,
            ResLocalScope insertScope,
            ResEnv env)
        {
            var resParam = new ResVarDecl(
                absParam.Range,
                absParam.name,
                LazyFactory.New(() => ResolveTypeExp(absParam.type, env)));

            insertScope.Insert(absParam.name, (range) => new ResVarRef(range, resParam));

            return resParam;
        }

        private IResTerm ResolveTerm(AbsTerm term, ResEnv env)
        {
            if (term == null) return null;
            return ResolveTermImpl((dynamic)term, env);
        }

        private IResTerm ResolveTermImpl(
            AbsIfTerm absExp,
            ResEnv env)
        {
            // \todo: Must CoerceUnqualified to
            // the appropriate type frequency-qualified
            // for the present method...
            var resCondition = Coerce(
                ResolveTerm(absExp.Condition, env),
                _builtinTypeBool(absExp.Range),
                env);

            var resThenTerm = ResolveTerm(absExp.Then, env);
            var resThenExp = CoerceToExp(resThenTerm, env);

            var resElseTerm = ResolveTerm(absExp.Else, env);

            // For now, we always try to coerce the 'else'
            // expression to the type of the 'then' expression:

            var resElseExp = Coerce(
                resElseTerm,
                resThenExp.Type,
                env);

            var result = new ResIfExp(
                absExp.Range,
                resCondition,
                resThenExp,
                resElseExp);

            return result;
        }

        private IResTerm ResolveTermImpl(
            AbsBaseExp absExp,
            ResEnv env)
        {
            var type = env.BaseAttributeType;
            if (type == null)
            {
                env.Error(absExp.Range, "The 'base' keyword can only be used inside an attribute declaration");
                return ResErrorTerm.Instance;
            }

            return new ResBaseExp(
                absExp.Range,
                type);
        }


        private IResTerm ResolveTermImpl(
            AbsVoid absVoid,
            ResEnv env)
        {
            return new ResVoidType();
        }

        private IResTerm ResolveTermImpl(
            AbsAssign absAssign,
            ResEnv env)
        {
            var dest = CoerceToExp(
                ResolveTerm(absAssign.Left, env),
                env);
            var src = CoerceUnqualified(
                ResolveTerm(absAssign.Right, env),
                dest.Type,
                env);

            // \todo: Check compatibility/assignability of target...

            return new ResAssignExp(
                absAssign.Range,
                dest,
                src);
        }

        private IResTerm ResolveSingleTerm(
            IResTerm term,
            ResEnv env)
        {
            return ResolveSingleTermImpl((dynamic) term, env);
        }

        private IResTerm ResolveSingleTermImpl(
            IResTerm term,
            ResEnv env)
        {
            return term;
        }

        private IResTerm ResolveSingleTermImpl(
            ResOverloadedTerm term,
            ResEnv env)
        {
            env.Error(
                term.Range,
                "Ambiguous reference");
            return ResErrorTerm.Instance;
        }

        private IResTerm ResolveSingleTermImpl(
            ResLayeredTerm term,
            ResEnv env)
        {
            return ResolveSingleTermImpl(term.First, env);
        }

        private IResTerm ResolveTermImpl(
            AbsMemberRef absMemberRef,
            ResEnv env)
        {
            var obj = ResolveSingleTerm(ResolveTerm(absMemberRef.baseObject, env), env);
            return ResolveMemberRefImpl((dynamic)obj, absMemberRef, env);
        }

        private IResTerm ResolveMemberRefImpl(
            ResLayeredTerm layered,
            AbsMemberRef absMemberRef,
            ResEnv env )
        {
            if( layered.Rest != null )
            {
                throw new NotImplementedException();
            }

            return ResolveMemberRefImpl(
                (dynamic) layered.First,
                absMemberRef,
                env );
        }

        private IResTerm ResolveMemberRefImpl(
            ResErrorTerm err,
            AbsMemberRef absMemberRef,
            ResEnv env)
        {
            return err;
        }

        private IResTerm ResolveMemberRefImpl(
            IResTypeExp type,
            AbsMemberRef absMemberRef,
            ResEnv env)
        {
            var range = type.Range;

#if FOOBARBAZ
            var memberSpec = LookupMember(range, type, absMemberRef.memberName);
            if (memberSpec != null)
                return memberSpec;

            env.Error(
                range,
                "Type {0} has no member '{1}'",
                type,
                absMemberRef.memberName);
            return ResErrorTerm.Instance;
#endif
            throw new NotImplementedException();
        }


        private IResTerm ResolveMemberRefImpl(
            ResMemberCategoryGroupRef group,
            AbsMemberRef absMemberRef,
            ResEnv env)
        {
            var members = group.Members.Eager();
            if (members.Length == 1)
            {
                return ResolveMemberRefImpl(
                    (dynamic)members[0],
                    absMemberRef,
                    env);
            }

            throw new NotImplementedException();
        }

        private IResTerm ResolveMemberRefImpl(
            IResExp obj,
            AbsMemberRef absMemberRef,
            ResEnv env)
        {
            var range = absMemberRef.Range;
            IResTypeExp type = obj.Type;
            IResElementRef freq = null;

            if (type is ResFreqQualType)
            {
                var fqType = (ResFreqQualType)type;
                type = fqType.Type;
                freq = fqType.Freq;
            }

            if (type is ResErrorTerm)
                return ResErrorTerm.Instance;

            var result = LookupMember(range, type, obj, absMemberRef.memberName);
            if (result != null)
                return result;

            // Fallback: Look for an in-scope method with
            // an appropriate name that we can apply to
            // the given object:

            var methodName = _identifiers.operatorIdentifier(absMemberRef.memberName.ToString());
            var method = env.Lookup(absMemberRef.Range, methodName);

            if (method == null)
            {
                env.Error(
                    range,
                    "Expression of type {0} has no member '{1}'",
                    type,
                    absMemberRef.memberName);
                return ResErrorTerm.Instance;
            }

            return ResolveApp(
                null,
                absMemberRef.Range,
                method,
                new ResArg<IResTerm>[] { new ResPositionalArg<IResTerm>(absMemberRef.Range, obj) },
                env);
        }

        private IResTerm LookupMember(
            SourceRange range,
            IResTypeExp type,
            IResExp obj,
            Identifier name)
        {
            return LookupMemberImpl(range, (dynamic)type, obj, name);
        }

        public IResTerm LookupMemberImpl(
            SourceRange range,
            IResTypeExp type,
            IResExp obj,
            Identifier name)
        {
            return null;
        }

        public IResTerm LookupMemberImpl(
            SourceRange range,
            IResStructRef structRef,
            IResExp obj,
            Identifier name)
        {
            var nameGroupSpecs = structRef.LookupMembers(range, name).ToArray();

            if( nameGroupSpecs.Length == 0 )
                return null;
            if( nameGroupSpecs.Length > 1 )
            {
                throw new NotImplementedException();
            }
            var nameGroupSpec = nameGroupSpecs[ 0 ];
            if (nameGroupSpec == null)
                return null;
            var members = (from categoryGroupSpec in nameGroupSpec.Categories
                           select categoryGroupSpec.Bind(range, obj)).Eager();

            if (members.Length == 0)
                return null;

            if (members.Length == 1)
            {
                return members[0];
            }

            return new ResOverloadedTerm(
                range,
                members);
        }

        public IResTerm LookupMemberImpl(
            SourceRange range,
            IResPipelineRef pipeline,
            IResExp obj,
            Identifier name)
        {
            var memberNameGroupSpec = pipeline.LookupMembers(range, name);

            throw new NotImplementedException();
        }

        private IResTerm ResolveTermImpl(
            AbsFreqQualTerm absFreqQualTerm,
            ResEnv env)
        {
            var freq = ResolveElementRef(absFreqQualTerm.Freq, env);
            var type = ResolveTypeExp(absFreqQualTerm.Type, env);

            return new ResFreqQualType(absFreqQualTerm.Range, freq, type);
        }

        private IResTerm ResolveTermImpl(
            AbsVarRef absVarRef,
            ResEnv env)
        {
            var resTerm = env.Lookup(absVarRef.Range, absVarRef.name);
            if (resTerm != null)
                return resTerm;

            env.Error(
                absVarRef.Range,
                "Undefined identifier '{0}'",
                absVarRef.name);

            // For debugging:
//            resTerm = env.Lookup(absVarRef.Range, absVarRef.name);

            return ResErrorTerm.Instance;
        }

        private class ResArg<T>
        {
            public ResArg(
                SourceRange range,
                T term)
            {
                _range = range;
                _term = term;
            }

            public SourceRange Range { get { return _range; } }
            public T Term { get { return _term; } }

            private SourceRange _range;
            private T _term;
        }

        private class ResPositionalArg<T> : ResArg<T>
        {
            public ResPositionalArg(
                SourceRange range,
                T term)
                : base(range, term)
            {
            }
        }

        private class ResKeywordArg<T> : ResArg<T>
        {
            public ResKeywordArg(
                SourceRange range,
                Identifier name,
                T term)
                : base(range, term)
            {
                _name = name;
            }

            public Identifier Name { get { return _name; } }

            private Identifier _name;
        }

        private ResArg<IResTerm> ResolveArg(
            AbsArg absArg,
            ResEnv env)
        {
            return ResolveArgImpl((dynamic)absArg, env);
        }

        private ResArg<IResTerm> ResolveArgImpl(
            AbsPositionalArg absArg,
            ResEnv env)
        {
            return new ResPositionalArg<IResTerm>(
                absArg.Range,
                ResolveTerm(absArg.Term, env));
        }

        private ResArg<IResTerm> ResolveArgImpl(
            AbsKeywordArg absArg,
            ResEnv env)
        {
            return new ResKeywordArg<IResTerm>(
                absArg.Range,
                absArg.Name,
                ResolveTerm(absArg.Term, env));
        }


        private IResTerm ResolveTermImpl(
            AbsApp absApp,
            ResEnv env)
        {
            var fun = ResolveTerm(absApp.function, env);
            var args = (from absArg in absApp.arguments
                        select ResolveArg(absArg, env)).Eager();
            return ResolveApp(
                absApp,
                absApp.Range,
                fun,
                args,
                env);
        }

        private interface IResCandidate<AbsAppT>
        {
            IResCandidate<AbsAppT> Filter(
                Func<ResCandidate<AbsAppT>, bool> filter);

            IEnumerable<ResCandidate<AbsAppT>> AllCandidates { get; }

            ResCandidate<AbsAppT> BestCandidate { get; }
        }

        private abstract class ResCandidate<AbsAppT> : IResCandidate<AbsAppT>
        {
            public ResolveContext _context { get; set; }
            public DiagnosticSink _diagnostics { get; set; }
            public ResEnv _env { get; set; }
            public ResArg<IResTerm>[] _rawArgs { get; set; }
            public SourceRange _range { get; set; }
            public IResGenericArg[] _dummyArgs { get; set; }

            public abstract bool CheckArity();
            public abstract bool CheckTypes();
            public abstract IResTerm Gen();

            public IResCandidate<AbsAppT> Filter(
                Func<ResCandidate<AbsAppT>, bool> filter)
            {
                if (filter(this))
                    return this;
                return null;
            }

            public IEnumerable<ResCandidate<AbsAppT>> AllCandidates
            {
                get { yield return this; }
            }

            public ResCandidate<AbsAppT> BestCandidate { get { return this; } }

            public ResOverloadScore Score { get { return _env.Score; } }

            public bool PostCheckTypeArgs()
            {
                if (_dummyArgs == null)
                    return true;

                return _context.CheckDummyArgs(
                    _range,
                    _dummyArgs,
                    _env);
            }
        }

        public bool CheckDummyArgs(
            SourceRange range,
            IEnumerable<IResGenericArg> args,
            ResEnv env )
        {
            // \todo: do something with the type args
            // to see if we can satisfy the assignment...
            foreach (var a in args)
            {
                if (!CheckDummyArg(range, a, env))
                    return false;
            }

            return true;
        }

        private bool CheckDummyArg(
            SourceRange range,
            IResGenericArg arg,
            ResEnv env )
        {
            return CheckDummyArgImpl(range, (dynamic) arg, env);
        }

        private bool CheckDummyArgImpl(
            SourceRange range,
            ResGenericTypeArg arg,
            ResEnv env )
        {
            var a = (ResDummyTypeArg) arg.Type;
            // try to resolve between the lower and upper bounds
            IResTypeExp type = null;
            bool fail = false;

            foreach (var t in a.LowerBounds)
            {
                if (type == null) type = t;
                if (!IsSubTypeOf(t, type))
                {
                    fail = true;
                    break;
                }
            }

            foreach (var t in a.UpperBounds)
            {
                if (type == null) type = t;
                if (!IsSubTypeOf(type, t))
                {
                    fail = true;
                    break;
                }
            }

            if (fail)
            {
                env.Error(
                    range,
                    "Could not deduce type argument");
                return false;
            }

            a.ConcreteType = type;
            return true;
        }

        private bool CheckDummyArgImpl(
            SourceRange range,
            ResGenericValueArg arg,
            ResEnv env)
        {
            var a = (ResDummyValArg)arg.Value;

            // Try to find a value that matches
            IResExp val = null;
            bool fail = false;

            foreach (var c in a.Constraints)
            {
                if (val == null) val = c;
                else if (!IsSameExp(val, c))
                {
                    fail = true;
                    break;
                }
            }

            if (val == null)
            {
                if (a.Param.IsImplicit)
                {
                    val = TryFindImplicitArgVal(
                        range,
                        a.Type,
                        env);
                }
            }

            if (val == null)
            {
                fail = true;
            }

            if (fail)
            {
                env.Error(
                    range,
                    "Could not deduce generic argument");
                return false;
            }

            a.ConcreteVal = val;
            return true;
        }

        private IResExp TryFindImplicitArgVal(
            SourceRange range,
            IResTypeExp type,
            ResEnv env)
        {
            // \todo: Eventually just look for
            // an in-scope implicit parameter
            // with the given type as a first step.

            // Otherwise, for a concept-type parameter
            // we look for in-scope methods with the
            // specified names that take precisely
            // the types specified...
            // \todo: Should just be for an 'auto' concept...

            if (type is IResConceptClassRef)
            {
                var conceptClassRef = (IResConceptClassRef)type;

                var dummyVar = new ResVarDecl(
                    range,
                    _identifiers.unique("_"),
                    conceptClassRef);

                var memberRefs = new List<IResMemberRef>();
                foreach (var m in conceptClassRef.Members)
                {
                    var expectedRef = m.Bind(
                        range,
                        new ResVarRef(range, dummyVar));
                    var memberRef = TryFindConceptMember(
                        range,
                        expectedRef,
                        env);

                    if( memberRef == null )
                        return null;

                    memberRefs.Add(memberRef);
                }

                return new ResConceptVal(
                    range,
                    conceptClassRef,
                    memberRefs);
            }

            return null;
        }

        private IResMemberRef TryFindConceptMember(
            SourceRange range,
            IResMemberRef expectedRef,
            ResEnv env)
        {
            return TryFindConceptMemberImpl(
                range,
                (dynamic)expectedRef,
                env);
        }

        private IResMemberRef TryFindConceptMemberImpl(
            SourceRange range,
            IResMemberRef expectedRef,
            ResEnv env)
        {
            var memberBind = (ResMemberBind)expectedRef.MemberTerm;
            var memberSpec = memberBind.MemberSpec;
            env.Error(
                range,
                "Could not find a value to satisfy concept member '{0}'",
                memberSpec);
            return null;
        }

        private IResMemberRef TryFindConceptMemberImpl(
            SourceRange range,
            IResMethodRef expectedRef,
            ResEnv env)
        {
            var allTerms = env.Lookup(range, expectedRef.Decl.Name);

            var bestTerm = CoerceFilter<IResMethodRef>(
                allTerms,
                new[] { ResMemberFlavor.Method },
                env,
                (term) => IsMatchingMethod(term, expectedRef, env));

            if( bestTerm == null )
            {
                var memberBind = (ResMemberBind) expectedRef.MemberTerm;
                var memberSpec = memberBind.MemberSpec;
                env.Error(
                    range,
                    "Could not find a value to satisfy concept member '{0}'",
                    memberSpec );
            }

            return bestTerm;
        }

        private T CoerceFilter<T>(
            IResTerm term,
            ResMemberFlavor[] flavors,
            ResEnv env,
            Func<T, bool> predicate)
            where T : IResTerm
        {
            if (term is T)
            {
                var t = (T)term;
                if (predicate(t))
                    return t;
                return default(T);
            }
            else if (term is ResLayeredTerm)
            {
                var layered = (ResLayeredTerm)term;

                var first = CoerceFilter<T>(
                    layered.First,
                    flavors,
                    env,
                    predicate);
                if (first != null)
                    return first;

                if (layered.Rest == null)
                    return default(T);

                return CoerceFilter<T>(
                    layered.Rest,
                    flavors,
                    env,
                    predicate);
            }
            else if (term is IResMemberCategoryGroupRef)
            {
                var group = (ResMemberCategoryGroupRef)term;
                if (!flavors.Contains(group.Flavor))
                    return default(T);

                return CoerceFilter<T>(
                    group.Members,
                    flavors,
                    env,
                    predicate);
            }
            else if (term is ResOverloadedTerm)
            {
                var overloaded = (ResOverloadedTerm)term;
                return CoerceFilter<T>(
                    overloaded.Terms,
                    flavors,
                    env,
                    predicate);
            }

            throw new NotImplementedException();
        }

        private T CoerceFilter<T>(
            IEnumerable<IResTerm> terms,
            ResMemberFlavor[] flavors,
            ResEnv env,
            Func<T, bool> predicate)
            where T : IResTerm
        {
            var filtered = (from t in terms
                            let coerced = CoerceFilter<T>(
                                t,
                                flavors,
                                env,
                                predicate)
                            where coerced != null
                            select coerced).ToArray();

            if (filtered.Length == 1)
                return filtered[0];
            return default(T);
        }

        private bool IsMatchingMethod(
            IResMethodRef methodRef,
            IResMethodRef expectedRef,
            ResEnv env )
        {
            if (!IsSubTypeOf(methodRef.ResultType, expectedRef.ResultType))
                return false;

            if (!(methodRef.Parameters.Count() == expectedRef.Parameters.Count()))
                return false;

            foreach (var p in methodRef.Parameters.Zip(expectedRef.Parameters, Tuple.Create))
            {
                var paramRef = p.Item1;
                var expectedParamRef = p.Item2;

                if (!IsSubTypeOf(expectedParamRef.Type, paramRef.Type))
                    return false;
            }

            return true;
        }

        private abstract class ResBasicCandidate<DeclRefT, ParamSpecT, AbsAppT> : ResCandidate<AbsAppT>
            where ParamSpecT : IResParamSpec
        {
            public override string ToString()
            {
                return string.Format("{0}({1})",
                    _context.OverloadContextName(DeclRef),
                    (from p in _params
                     select string.Format("{0} {1}", p.Classifier, p.Name)).Separate(", ").Concat());
            }

            protected abstract DeclRefT DeclRef { get; }
            protected abstract IEnumerable<ParamSpecT> Parameters { get; }

            protected ParamSpecT[] _params;
            protected IResTerm[] _matchedArgs;
            protected IResTerm[] _checkedArgs;

            public override bool CheckArity()
            {
                _params = Parameters.Eager();
                int paramCount = _params.Length;

                _matchedArgs = new IResTerm[paramCount];

                bool pass = true;
                foreach (var a in _rawArgs.OfType<ResKeywordArg<IResTerm>>())
                {
                    int p = 0;
                    for (; p < paramCount; ++p)
                    {
                        if (_params[p].Name == a.Name)
                            break;
                    }

                    if (p == paramCount)
                    {
                        _env.Error(
                            a.Range,
                            "No matching parameter found for keyword argument '{0}'",
                            a.Name);
                        pass = false;
                        continue;
                    }

                    _matchedArgs[p] = _context.CoerceToExp(a.Term, _env);
                }

                int argIndex = 0;
                foreach (var a in _rawArgs.OfType<ResPositionalArg<IResTerm>>())
                {
                    while (_matchedArgs[argIndex] != null)
                    {
                        argIndex++;
                        if (argIndex == paramCount)
                        {
                            _env.Error(
                                _range,
                                "Too many arguments for call. Expected {0}, got {1}",
                                _params.Length,
                                _rawArgs.Length);
                            return false;
                        }
                    }

                    _matchedArgs[argIndex] = a.Term;
                }

                while (argIndex < paramCount && _matchedArgs[argIndex] != null)
                    argIndex++;

                if (argIndex != paramCount)
                {
                    _env.Error(
                        _range,
                        "Too few arguments for call. Expected {0}, got {1}",
                        _params.Length,
                        _rawArgs.Length);
                    return false;
                }

                return pass;
            }

            public override bool CheckTypes()
            {
                int paramCount = _params.Length;
                _checkedArgs = new IResTerm[paramCount];

                for (int ii = 0; ii < paramCount; ++ii)
                {
                    var param = _params[ii];
                    var arg = _matchedArgs[ii];

                    var checkedArg = Coerce(
                        arg,
                        param );

                    _checkedArgs[ii] = checkedArg;
                }

                if (_checkedArgs.Any((t) => t is ResErrorTerm))
                {
                    return false;
                }

                return true;
            }

            protected abstract IResTerm Coerce(
                IResTerm arg,
                ParamSpecT param );
        }

        private class ResMethodCandidate : ResBasicCandidate<IResMethodRef, IResValueParamSpec, AbsApp>
        {
            public IResMethodRef _method { get; set; }

            protected override IResTerm Coerce(
                IResTerm argTerm,
                IResValueParamSpec param)
            {
                IResExp arg = _context.CoerceToExp(argTerm, _env);

                if (_context.IsCrossFreqMethod(_method))
                {
                    // The method has explicit frequency
                    // qualifiers, and thus we simply need
                    // to coerce each argument to the appropriate
                    // frequency of the parameter.

                    return _context.Coerce(
                        arg,
                        param.Type,
                        _env);
                }
                else
                {
                    var fromType = arg.Type;
                    if (fromType is ResFreqQualType)
                    {
                        var fqType = fromType as ResFreqQualType;
                        fromType = fqType.Type;
                    }

                    return _context.Coerce(
                        arg,
                        fromType,
                        param.Type,
                        _env);
                }
            }

            public override IResTerm Gen()
            {
                return new ResMethodApp(
                    _range,
                    _method.ResultType,
                    _method,
                    _checkedArgs.Cast<IResExp>());
            }

            protected override IResMethodRef DeclRef
            {
                get { return _method; }
            }

            protected override IEnumerable<IResValueParamSpec> Parameters
            {
                get { return _method.Parameters; }
            }
        }

        private class ResGenericCandidate : ResBasicCandidate<IResGenericRef, IResGenericParamRef, AbsGenericApp>
        {
            public IResGenericRef _generic { get; set; }

            protected override IResTerm Coerce(
                IResTerm arg,
                IResGenericParamRef param)
            {
                // Disable implicit type conversions inside of generic argument lists.
                // This helps to avoid a circual-reference problem that could otherwise
                // crop up.
                return _context.Coerce(
                    arg,
                    param.Classifier,
                    _env.NestDisableConversions());
            }

            public override IResTerm Gen()
            {
                return _generic.App(
                    _range,
                    (from a in _checkedArgs
                     select a.MakeGenericArg()).Eager());
            }

            protected override IResGenericRef DeclRef
            {
                get { return _generic; }
            }

            protected override IEnumerable<IResGenericParamRef> Parameters
            {
                get { return _generic.Parameters; }
            }
        }

        private class ResAttributeCandidate : ResCandidate<AbsApp>
        {
            public IResExp _attribute { get; set; }

            private IResExp _checkedArg;
            private IResTypeExp _resultType;

            public override bool CheckArity()
            {
                if (_rawArgs.Length != 1
                    || !(_rawArgs[0] is ResPositionalArg<IResTerm>))
                {
                    _env.Error(_range,
                        "Expected single argument for attribute fetch");

                    return false;
                }

                _checkedArg = _context.CoerceToExp(_rawArgs[0].Term, _env);
                return true;
            }

            public override bool CheckTypes()
            {
                if (_checkedArg is ResErrorTerm)
                    return false;

                if (!(_attribute.Type is ResFreqQualType))
                {
                    _env.Error(
                        _attribute.Range,
                        "Expected a frequency-qualified value");
                    return false;
                }
                var attrFQType = (ResFreqQualType)_attribute.Type;
                var attrFreq = attrFQType.Freq;
                var attrType = attrFQType.Type;

                var argType = _checkedArg.Type;
                if (argType is ResFreqQualType)
                {
                    argType = ((ResFreqQualType)argType).Type;
                }

                if (!(_context.IsSameType(argType, attrFreq)))
                {
                    _env.Error(
                        _range,
                        "Cannot project attribute of type {0} out of value of type {1}",
                        _attribute.Type,
                        _checkedArg.Type);
                    return false;
                }

                _resultType = attrType;
                return true;
            }

            public override IResTerm Gen()
            {
                return new ResAttributeFetch(
                    _range,
                    _resultType,
                    _checkedArg,
                    _attribute);
            }
        };

        private class ResElementCtorCandidate : ResCandidate<AbsApp>
        {
            public override string ToString()
            {
                return string.Format("{0}({1})",
                    _context.OverloadContextName(_elementRef),
                    (from a in _attributes
                     select string.Format("{0} {1}", a.Classifier, a.Decl.Name)).Separate(", ").Concat());
            }

            public ResElementRef _elementRef { get; set; }

            private IResAttributeRef[] _attributes;
            private IResTerm[] _matchedArgs;
            private IResExp[] _checkedArgs;

            private IEnumerable<IResAttributeRef> CollectAttributes()
            {
                var memberBind = _elementRef.MemberTerm as ResMemberBind;
                var obj = memberBind.Obj;
                var container = memberBind.MemberSpec.Container;

                foreach (var memberSpec in container.Members)
                {
                    if (!(memberSpec.Decl is IResAttributeDecl))
                        continue;

                    var attrDecl = (ResAttributeDecl)memberSpec.Decl;

                    if( !attrDecl.IsInput() )
                        continue;

                    if (attrDecl.Line.ConcretenessMode == ResMemberConcretenessMode.Abstract)
                        continue;
                    if (attrDecl.Init != null)
                        continue;

                    var attrRef = (IResAttributeRef)memberSpec.Bind(
                        _range,
                        obj);

                    var attrFQType = (IResFreqQualType) attrRef.Type;
                    var attrFreq = attrFQType.Freq;
                    if (attrFreq is ResErrorTerm)
                        continue;

                    if (!_context.IsSameMemberTerm(attrFreq.MemberTerm, _elementRef.MemberTerm))
                        continue;

                    yield return attrRef;
                }
            }

            public override bool CheckArity()
            {
                _attributes = CollectAttributes().Eager();
                _matchedArgs = new IResTerm[_attributes.Length];

                bool pass = true;
                foreach (var rawArg in _rawArgs)
                {
                    if (!(rawArg is ResKeywordArg<IResTerm>))
                    {
                        _env.Error(
                            rawArg.Range,
                            "Arguments to element construct must be keywords");
                        pass = false;
                        continue;
                    }

                    var kwArg = (ResKeywordArg<IResTerm>)rawArg;

                    var name = kwArg.Name;
                    var argTerm = kwArg.Term;

                    int attrIndex = -1;
                    for (int ii = 0; ii < _attributes.Length; ++ii)
                    {
                        if (_attributes[ii].Decl.Name == name)
                        {
                            attrIndex = ii;
                            break;
                        }
                    }

                    if (attrIndex == -1)
                    {
                        _env.Error(
                            rawArg.Range,
                            "No input attribute named '{0}' found in element '{1}'",
                            name,
                            _elementRef);
                        pass = false;
                        continue;
                    }

                    if (_matchedArgs[attrIndex] != null)
                    {
                        _env.Error(
                            rawArg.Range,
                            "Duplicate keyword argument '{0}'",
                            name);
                        pass = false;
                        continue;
                    }

                    _matchedArgs[attrIndex] = argTerm;
                }

                for (int ii = 0; ii < _attributes.Length; ++ii)
                {
                    if (_matchedArgs[ii] == null)
                    {
                        var attrRef = _attributes[ ii ];
                        var attrDecl = attrRef.Decl;
                        var attrFQType = (IResFreqQualType) attrRef.Type;
                        var attrType = attrFQType.Type;

                        IResTerm matchedArg = null;

                        if( attrDecl.IsImplicit() )
                        {
                            // For an implicit attribute, we can look for an in-scope
                            // value with the type we expect.

                            var implicits = _env.LookupImplicits(
                                _range,
                                ( t ) => GetImplicitAttrs( t, attrType, _env ) ).Eager();

                            if( implicits.Length == 1 )
                            {
                                matchedArg = implicits[ 0 ];
                            }
                        }

                        if( matchedArg != null )
                        {
                            _matchedArgs[ ii ] = matchedArg;
                        }
                        else
                        {
                            _env.Error(
                                _range,
                                "No keyword argument provided for input attribute '{0}'",
                                _attributes[ ii ].Decl.Name );
                            pass = false;
                        }
                    }
                }

                return pass;
            }

            private IEnumerable<IResAttributeRef> GetImplicitAttrs(
                IResTerm t,
                IResTypeExp type,
                ResEnv env )
            {
                if( t is IResAttributeRef )
                {
                    var attrRef = (IResAttributeRef) t;
                    var attrFQType = (IResFreqQualType) attrRef.Type;
                    var attrFreq = attrFQType.Freq;
                    var attrType = attrFQType.Type;

                    if( _context.IsSameType( attrFreq, env.ImplicitFreq ) )
                    {
                        if( _context.IsSameType( attrType, type ) )
                        {
                            yield return attrRef;
                        }
                    }
                }
            }

            public override bool CheckTypes()
            {
                _checkedArgs = new IResExp[_attributes.Length];

                bool pass = true;
                for (int ii = 0; ii < _attributes.Length; ++ii)
                {
                    var attr = _attributes[ii];
                    var attrFQType = (IResFreqQualType) attr.Type;
                    var attrDataType = attrFQType.Type;

                    var arg = _context.CoerceToExp(_matchedArgs[ii], _env);

                    var fromType = arg.Type;
                    if (fromType is ResFreqQualType)
                    {
                        var fqType = fromType as ResFreqQualType;
                        fromType = fqType.Type;
                    }

                    arg = _context.Coerce(
                        arg,
                        fromType,
                        attrDataType,
                        _env);

                    if (arg is ResErrorTerm)
                        pass = false;

                    _checkedArgs[ii] = arg;
                }

                return pass;
            }

            public override IResTerm Gen()
            {
                var args = _attributes.Zip(_checkedArgs,
                    (attr, arg) => new ResElementCtorArg { Attribute = attr, Value = arg }).Eager();

                return new ResElementCtorApp(
                    _range,
                    _elementRef,
                    _elementRef,
                    args);
            }
        }

        private class ResLayeredCandidate<T> : IResCandidate<T>
        {
            public ResLayeredCandidate(
                IResCandidate<T> first,
                IResCandidate<T> rest)
            {
                _first = first;
                _rest = rest;
            }

            public IResCandidate<T> Filter(Func<ResCandidate<T>, bool> filter)
            {
                var newFirst = _first.Filter(filter);
                var newRest = _rest.Filter(filter);

                if (newFirst == null) return newRest;
                if (newRest == null) return newFirst;

                return new ResLayeredCandidate<T>(
                    newFirst,
                    newRest);
            }

            public IEnumerable<ResCandidate<T>> AllCandidates
            {
                get { throw new NotImplementedException(); }
            }

            public ResCandidate<T> BestCandidate
            {
                get { return null; }
            }

            private IResCandidate<T> _first;
            private IResCandidate<T> _rest;
        }

        private class ResOverloadedCandidate<T> : IResCandidate<T>
        {
            public ResOverloadedCandidate(
                IResCandidate<T>[] candidates)
            {
                _candidates = candidates;

                if (_candidates.Length <= 1)
                    throw new ArgumentException("Must have more than one candidate", "candidates");
            }

            public IResCandidate<T> Filter(Func<ResCandidate<T>, bool> filter)
            {
                var newCandidates = (from c in _candidates
                               let newC = c.Filter(filter)
                               where newC != null
                               select newC).Eager();

                if (newCandidates.Length == 0)
                    return null;

                if (newCandidates.Length == 1)
                    return newCandidates[0];

                return new ResOverloadedCandidate<T>(
                    newCandidates);
            }

            public IEnumerable<ResCandidate<T>> AllCandidates
            {
                get
                {
                    foreach (var c in _candidates)
                        foreach (var cc in c.AllCandidates)
                            yield return cc;
                }
            }

            public ResCandidate<T> BestCandidate
            {
                get { return null; }
            }

            private IResCandidate<T>[] _candidates;
        }

        private IResCandidate<AbsAppT> CreateOverloadCandidate<AbsAppT>(
            IEnumerable<IResCandidate<AbsAppT>> candidates)
        {
            var filtered = (from c in candidates
                            where c != null
                            select c).Eager();
            if (filtered.Length == 0)
                return null;
            if (filtered.Length == 1)
                return filtered[0];

            return new ResOverloadedCandidate<AbsAppT>(filtered);
        }

        private IResCandidate<AbsAppT> CreateAppCandidates<AbsAppT>(
            Func<SourceRange, IResTerm, ResArg<IResTerm>[], ResEnv, IResCandidate<AbsAppT>> generator,
            SourceRange range,
            IResTerm term,
            ResArg<IResTerm>[] args,
            ResEnv env)
        {
            if (term is ResMemberCategoryGroupRef)
            {
                var groupRef = (ResMemberCategoryGroupRef)term;

                // \todo: Filter based on group flavor...?

                return CreateOverloadCandidate(
                    from m in groupRef.Members
                    select CreateAppCandidates(generator, range, m, args, env));
            }
            else if (term is ResOverloadedTerm)
            {
                var overload = (ResOverloadedTerm)term;
                return CreateOverloadCandidate(
                    from t in overload.Terms
                    select CreateAppCandidates(generator, range, t, args, env));
            }
            else if (term is ResLayeredTerm)
            {
                var layered = (ResLayeredTerm)term;
                var firstCandidate = CreateAppCandidates(
                    generator,
                    range,
                    layered.First,
                    args,
                    env);
                var restCandidate = CreateAppCandidates(
                    generator,
                    range,
                    layered.Rest,
                    args,
                    env);

                if (restCandidate == null)
                    return firstCandidate;
                if (firstCandidate == null)
                    return restCandidate;

                return new ResLayeredCandidate<AbsAppT>(
                    firstCandidate,
                    restCandidate);
            }
            else
            {
                return generator(range, term, args, env);
            }
        }


        private IResCandidate<AbsApp> CreateValAppCandidates(
            SourceRange range,
            IResTerm term,
            ResArg<IResTerm>[] args,
            ResEnv env)
        {
            if (term is IResMethodRef)
            {
                var method = (IResMethodRef)term;

                var diagnostics = new DiagnosticSink();
                return new ResMethodCandidate
                {
                    _context = this,
                    _diagnostics = diagnostics,
                    _env = env.NestDiagnostics(diagnostics).NestScore(),
                    _rawArgs = args,
                    _range = range,
                    _method = method,
                };
            }
            else if (term is IResGenericRef)
            {
                var generic = (IResGenericRef)term;
                var dummyArgSubst = new Substitution();
                var dummyArgs = (from p in generic.Parameters
                                 select MakeDummyArg(p, dummyArgSubst)).Eager();

                // \todo: Add post-process step... :(
                var subCandidate = (ResCandidate<AbsApp>) CreateValAppCandidates(
                    range,
                    generic.App(range, dummyArgs),
                    args,
                    env);
                if( subCandidate != null )
                    subCandidate._dummyArgs = dummyArgs;
                return subCandidate;
            }
            else if (term is IResExp)
            {
                var exp = (IResExp)term;
                var type = exp.Type;

                if (type is IResFreqQualType)
                {
                    var diagnostics = new DiagnosticSink();
                    return new ResAttributeCandidate
                    {
                        _context = this,
                        _diagnostics = diagnostics,
                        _env = env.NestDiagnostics(diagnostics).NestScore(),
                        _rawArgs = args,
                        _range = range,
                        _attribute = exp,
                    };
                }
/*
                // Try to apply the operator() method
                // to the terms...
                var newTerm = env.Lookup(
                    range,
                    _identifiers.operatorIdentifier("()"));
                if (newTerm != null)
                {
                    var newArgs = new ResArg<IResTerm>[]{ new ResPositionalArg<IResTerm>(range, exp) }.Concat(args).ToArray();

                    foreach (var c in CreateValAppCandidates(range, newTerm, newArgs, env))
                        yield return c;
                }
*/
            }
            else if (term is ResElementRef)
            {
                var elementRef = (ResElementRef) term;

                var diagnostics = new DiagnosticSink();
                return new ResElementCtorCandidate
                {
                    _context = this,
                    _diagnostics = diagnostics,
                    _env = env.NestDiagnostics(diagnostics).NestScore(),
                    _rawArgs = args,
                    _range = range,
                    _elementRef = elementRef,
                };
            }

            return null;
        }

        private IResCandidate<AbsGenericApp> CreateGenericAppCandidates(
            SourceRange range,
            IResTerm term,
            ResArg<IResTerm>[] args,
            ResEnv env)
        {
            if (term is IResGenericRef)
            {
                var generic = (IResGenericRef)term;

                var diagnostics = new DiagnosticSink();
                return new ResGenericCandidate
                {
                    _context = this,
                    _diagnostics = diagnostics,
                    _env = env.NestDiagnostics(diagnostics).NestScore(),
                    _rawArgs = args,
                    _range = range,
                    _generic = generic,
                };
            }

            return null;
        }

        private IResGenericArg MakeDummyArg(
            IResGenericParamRef param,
            Substitution subst )
        {
            return MakeDummyArgImpl((dynamic)param, subst);
        }

        private IResGenericArg MakeDummyArgImpl(
            IResTypeParamRef param,
            Substitution subst )
        {
            var type = new ResDummyTypeArg(param);
            var arg = new ResGenericTypeArg(type);
            subst.Insert(param.Decl, (r) => type);
            return arg;
        }

        private IResGenericArg MakeDummyArgImpl(
            IResVarSpec param,
            Substitution subst )
        {
            return new ResGenericValueArg(
                new ResDummyValArg(param, subst));
        }

        private IResTerm ResolveApp(
            AbsApp absApp,
            SourceRange range,
            IResTerm term,
            ResArg<IResTerm>[] args,
            ResEnv env )
        {
            var candidates = CreateAppCandidates<AbsApp>(
                CreateValAppCandidates,
                range,
                term,
                args,
                env);

            // Also include candidates based on applying
            // the term using operator().

            var operatorApp = env.Lookup(
                range,
                _identifiers.operatorIdentifier("()"));
            if (operatorApp != null)
            {
                var operatorArgs = new ResArg<IResTerm>[] {
                    new ResPositionalArg<IResTerm>(range, term) }.Concat(args).Eager();

                var operatorCandidates = CreateAppCandidates<AbsApp>(
                    CreateValAppCandidates,
                    range,
                    operatorApp,
                    operatorArgs,
                    env);

                candidates = CreateOverloadCandidate(
                    new []{
                        candidates,
                        operatorCandidates });
            }


            return ResolveApp<AbsApp>(
                absApp,
                range,
                term,
                args,
                env,
                candidates,
                false);
        }

        private IResTerm ResolveGenericApp(
            AbsGenericApp absApp,
            SourceRange range,
            IResTerm term,
            ResArg<IResTerm>[] args,
            ResEnv env)
        {
            var candidates = CreateAppCandidates<AbsGenericApp>(
                CreateGenericAppCandidates,
                range,
                term,
                args,
                env);
            return ResolveApp<AbsGenericApp>(
                absApp,
                range,
                term,
                args,
                env,
                candidates,
                true);
        }

        private IResTerm ResolveApp<AbsAppT>(
            AbsAppT absApp,
            SourceRange range,
            IResTerm term,
            ResArg<IResTerm>[] args,
            ResEnv env,
            IResCandidate<AbsAppT> candidates,
            bool allowAmbiguous )
        {
            if (term is ResErrorTerm
                || args.Any((a) => a.Term is ResErrorTerm)
                || args.Select((a) => a.Term).OfType<IResExp>().Any((a) => a.Type is ResErrorTerm))
            {
                return ResErrorTerm.Instance;
            }

            if (candidates == null)
            {
                // \todo: Better error message.
                env.Error(
                    range,
                    "Unexpected term used as a function");
                return ResErrorTerm.Instance;
            }


            var filters = new Func<ResCandidate<AbsAppT>, bool>[] {
                (c) => c.CheckArity(),
                (c) => c.CheckTypes(),
            };

            int filterCount = filters.Length;
            int ff = 0;
            for(; ff < filterCount; ++ff )
            {
                if (candidates is ResCandidate<AbsAppT>)
                {
                    var soleCandidate = (ResCandidate<AbsAppT>)candidates;
                    env.AddDiagnostic(soleCandidate._diagnostics);

                    for (; ff < filterCount; ++ff)
                    {
                        if (!filters[ff](soleCandidate))
                            return ResErrorTerm.Instance;
                    }

                    var result = soleCandidate.Gen();
                    if (!soleCandidate.PostCheckTypeArgs())
                        return ResErrorTerm.Instance;

                    return result;
                }

                // Otherwise filter the candidate list
                // with the next filtering operation.

                var newCandidates = candidates.Filter(filters[ff]);

                if (newCandidates == null)
                {
                    // \todo: Better error message.
                    // Include listing of available candidates.
                    env.Error(
                        range,
                        "No applicable overload found for {0} with arguments ({1})",
                        OverloadContextName(term),
                        (from a in args
                         select OverloadArgName(a)).Separate(", ").Concat());

                    foreach (var c in candidates.AllCandidates)
                    {
                        env.AddDiagnostic(
                            Severity.Info,
                            range,
                            "Candidate: {0}",
                            c);

                        // For debugging:
//                        env.AddDiagnostic(c._diagnostics);
                    }

                    return ResErrorTerm.Instance;
                }

                candidates = newCandidates;
            }

            // We first filter out any candidates that
            // have higher than the best (lowest) score.
            // "Score" accounts for things like implicit
            // type conversions.
            var bestScore = (from c in candidates.AllCandidates
                             select c.Score).Min();
            candidates = candidates.Filter(
                (c) => c.Score == bestScore);

            // \todo: Need to filter candidates based
            // on which are "more specific" for the
            // call site.
            // The simplest example is to eliminate
            // candidates whose parameter types are
            // all super-types (at least one being a
            // strict super type) of another.
            // Another example would be to favor
            // candidates that didn't have defaulted
            // arguments, or that didn't have generic
            // arguments derived....

            if (candidates.BestCandidate != null)
            {
                var soleCandidate = candidates.BestCandidate;
                // Only one candidate, so just run through
                // the remaining steps with it.
                env.AddDiagnostic(soleCandidate._diagnostics);

                var result = soleCandidate.Gen();
                if (!soleCandidate.PostCheckTypeArgs())
                    return ResErrorTerm.Instance;

                return result;
            }

            if (!allowAmbiguous)
            {
                env.Error(
                    range,
                    "Ambiguous call to {0} with arguments ({1})",
                    OverloadContextName(term),
                    (from a in args
                     select OverloadArgName(a)).Separate(", ").Concat());

                foreach (var c in candidates.AllCandidates)
                {
                    env.AddDiagnostic(
                        Severity.Info,
                        range,
                        "Candidate: {0}",
                        c);
                }

                return ResErrorTerm.Instance;
            }
            else
            {
                var results = (from c in candidates.AllCandidates
                               where c.PostCheckTypeArgs()
                               select c.Gen()).Eager();

                return new ResOverloadedTerm(
                    range,
                    results);
            }
        }

        private string OverloadContextName(
            object context )
        {
            return OverloadContextNameImpl((dynamic)context);
        }

        private string OverloadContextNameImpl(
            object context)
        {
            return "unknown term";
        }

        private string OverloadContextNameImpl(
            ResElementRef context )
        {
            return OverloadContextName( context.MemberTerm );
        }


        private string OverloadContextNameImpl(
            ResVarRef term)
        {
            return term.Decl.Name.ToString();
        }

        private string OverloadContextNameImpl(
            ResLayeredTerm term)
        {
            return OverloadContextName(term.First);
        }

        private string OverloadContextNameImpl(
            ResMemberCategoryGroupRef term)
        {
            return OverloadContextName(term.Spec);
        }

        private string OverloadContextNameImpl(
            ResMemberCategoryGroupSpec term)
        {
            return string.Format(
                "{0}::{1}",
                term.ContainerRef,
                term.Decl.Name);
        }

        private string OverloadContextNameImpl(
            AbsVarRef term)
        {
            return string.Format("'{0}'", term.name);
        }

        private string OverloadContextNameImpl(
            IResMethodRef methodRef)
        {
            return OverloadContextName(methodRef.MemberTerm);
        }

        private string OverloadContextNameImpl(
            ResMemberGenericApp app)
        {
            return string.Format("{0}[{1}]",
                OverloadContextName(app.Fun.MemberTerm),
                (from a in app.Args
                 select a.ToString()).Separate(", ").Concat());
        }

        private string OverloadContextNameImpl(
            ResMemberBind memberBind)
        {
            return string.Format("{0}::{1}",
                OverloadContextName(memberBind.MemberSpec.Container.MemberTerm),
                memberBind.MemberSpec.Name);
        }

        private string OverloadContextNameImpl(
            ResGlobalMemberTerm global)
        {
            return global.Decl.Name.ToString();
        }

        private string OverloadArgName(
            ResArg<IResTerm> arg)
        {
            if (arg is ResKeywordArg<IResTerm>)
            {
                var name = ((ResKeywordArg<IResTerm>)arg).Name;
                return string.Format("{0}: {1}", name,
                    OverloadArgName(arg.Term));
            }

            return OverloadArgName(arg.Term);
        }

        private string OverloadArgName(
            IResTerm term)
        {
            if (term is ResMemberCategoryGroupRef)
            {
                var category = (ResMemberCategoryGroupRef)term;
                var members = category.Members.Eager();

                if (members.Length == 1)
                {
                    term = members[0];
                }
                else
                {
                    return "overloaded term";
                }
            }

            if (term is IResExp)
            {
                var exp = (IResExp)term;
                return exp.Type.ToString();
            }

            if (term is ResLayeredTerm)
            {
                return OverloadArgName(
                    ((ResLayeredTerm)term).First);
            }

            return "unknown";
        }

        private IResTypeExp FreqQual(
            IResElementRef freq,
            IResTypeExp type)
        {
            return FreqQual(type.Range, freq, type);
        }

        private IResTypeExp FreqQual(
            SourceRange range,
            IResElementRef freq,
            IResTypeExp type)
        {
            if (freq == null)
                return type;

            return new ResFreqQualType(range, freq, type);
        }

        private bool IsCrossFreqMethod(
            IResMethodRef method)
        {
            if (method.ResultType is ResFreqQualType)
                return true;

            foreach (var p in method.Parameters)
            {
                if (p.Type is ResFreqQualType)
                    return true;
            }

            return false;
        }

        private IResTerm ResolveTermImpl(
            AbsGenericApp absApp,
            ResEnv env)
        {
            var fun = ResolveTerm(absApp.function, env);
            var args = (from absArg in absApp.arguments
                        select ResolveArg(absArg, env)).Eager();

            return ResolveGenericApp(
                absApp,
                absApp.Range,
                fun,
                args,
                env);
        }

        private IResTerm ResolveTermImpl(
            AbsLit<Int32> absLit,
            ResEnv env)
        {
            return new ResLit<Int32>(
                absLit.Range,
                _builtinTypeInt32(absLit.Range),
                absLit.Value );
        }

        private IResTerm ResolveTermImpl(
            AbsLit<Double> absLit,
            ResEnv env)
        {
            return new ResLit<float>(
                absLit.Range,
                _builtinTypeFloat32(absLit.Range),
                (float) absLit.Value);
        }

        private IResTerm ResolveTermImpl(
            AbsLit<bool> absLit,
            ResEnv env)
        {
            return new ResLit<bool>(
                absLit.Range,
                _builtinTypeBool(absLit.Range),
                absLit.Value);
        }

        private IResTypeExp ResolveTypeExp(
            AbsTerm absTerm,
            ResEnv env)
        {
            var term = ResolveTerm(absTerm, env);
            return CoerceToTypeExp(term, env);
        }

        private IResExp CoerceToExp(
            IResTerm term,
            ResEnv env)
        {
            if (term is ResErrorTerm)
                return ResErrorTerm.Instance;

            var exp = CoerceFilterFirst<IResExp>(
                term,
                new ResMemberFlavor[]{
                    ResMemberFlavor.Attribute,
                    ResMemberFlavor.Field },
                env,
                "Expected an expression");

            if (exp is ResErrorTerm)
            {
                env.Error(
                    term.Range,
                    "Expected an expression");
            }

            return exp;
        }

        private IResTypeExp CoerceToTypeExp(
            IResTerm term,
            ResEnv env )
        {
            var type = CoerceFilterFirst<IResTypeExp>(
                term,
                new ResMemberFlavor[]{
                    ResMemberFlavor.Element,
                    ResMemberFlavor.Struct,
                    ResMemberFlavor.TypeSlot,
                    ResMemberFlavor.Pipeline },
                env,
                "Expected a type");

            return type;

            /*
            if (term is ResOverloadedTerm)
            {
                var overload = (ResOverloadedTerm)term;
                return ResolveOverload(
                    (ResOverloadedTerm)term,
                    env,
                    (t, e) => CoerceToTypeExp(t, e),
                    (t) => !(t is ResErrorTerm),
                    (e) => { e.Error(term.Range, "Expected a type!!"); return ResErrorTerm.Instance; },
                    (e) => { e.Error(term.Range, "Ambiguous!!"); return ResErrorTerm.Instance; });
            }
            */
        }

        private IResElementRef ResolveElementRef(
            AbsTerm absTerm,
            ResEnv env)
        {
            var term = ResolveTerm(absTerm, env);
            return CoerceToElementRef(term, env);
        }

        private IResElementRef CoerceToElementRef(
            IResTerm term,
            ResEnv env )
        {
            var result = CoerceFilterFirst<IResElementRef>(
                term,
                new ResMemberFlavor[] { ResMemberFlavor.Element },
                env,
                "Expected an element reference");

            if (result != null)
                return result;

            if (term is IResElementRef)
                return (IResElementRef) term;

            env.Error(
                term.Range,
                "Expected an element reference");
            return ResErrorTerm.Instance;
        }

        private T CoerceFilterFirst<T>(
            IResTerm term,
            IEnumerable<ResMemberFlavor> flavors,
            ResEnv env,
            string message)
        {
            var coerced = CoerceFilter<T>(term, flavors, env).Eager();
            if (coerced.Any((t) => t is ResErrorTerm))
            {
                return (T)(object)ResErrorTerm.Instance;
            }
            else if (coerced.Length != 1)
            {
                env.Error(term.Range, message);
                return (T)(object)ResErrorTerm.Instance;
            }

            return coerced[0];
        }

        private IEnumerable<T> CoerceFilter<T>(
            IResTerm term,
            IEnumerable<ResMemberFlavor> flavors,
            ResEnv env)
        {
            if (term is T)
            {
                yield return (T)term;
            }
            else if (term is ResMemberCategoryGroupRef)
            {
                var group = (ResMemberCategoryGroupRef)term;

                if (flavors.Contains(group.Flavor))
                {
                    foreach (var m in group.Members)
                        foreach (var t in CoerceFilter<T>(m, flavors, env))
                            yield return t;
                }
            }
            else if (term is ResOverloadedTerm)
            {
                var overloaded = (ResOverloadedTerm)term;
                foreach (var o in overloaded.Terms)
                    foreach (var t in CoerceFilter<T>(o, flavors, env))
                        yield return t;
            }
            else if (term is ResLayeredTerm)
            {
                var layered = (ResLayeredTerm)term;
                var first = CoerceFilter<T>(
                    layered.First,
                    flavors,
                    env).Eager();

                if( first.Length != 0 )
                {
                    foreach( var t in first )
                        yield return t;
                }
                else
                {
                    // None of the terms found at this
                    // layer matched the criteria, so
                    // we can freely search the next layer.
                    
                    foreach( var t in CoerceFilter<T>(
                        layered.Rest,
                        flavors,
                        env) )
                    {
                        yield return t;
                    }
                }
            }
            else
            {
                //                throw new NotImplementedException();
            }
        }

        private IResExp CoerceUnqualified(
            IResTerm term,
            IResTypeExp type,
            ResEnv env)
        {
            if (term == null)
                return null;

            var exp = Coerce(term, type, env);
            if ((exp is ResErrorTerm) || (type is ResErrorTerm))
                return ResErrorTerm.Instance;

            if( !(type is ResFreqQualType) )
            {
                return ApplyFreq(
                    env.ImplicitFreq,
                    exp,
                    env);
            }
            else
            {
                return exp;
            }
        }

        private IResExp Coerce(
            IResTerm term,
            IResTypeExp type,
            ResEnv env)
        {
            if (term == null) return null;

            var exp = CoerceToExp(term, env);

            if ((exp is ResErrorTerm) || (type is ResErrorTerm))
                return ResErrorTerm.Instance;


            var fromType = exp.Type;

            return Coerce(
                exp,
                fromType,
                type,
                env);
        }

        private IResExp Coerce(
            IResExp exp,
            IResTypeExp fromType,
            IResTypeExp toType,
            ResEnv env)
        {
            if (IsSubTypeOf(fromType, toType))
                return exp;

            return TryCoerce(exp, fromType, toType, env);
        }

        private IResExp TryCoerce(
            IResExp exp,
            IResTypeExp fromType,
            IResTypeExp toType,
            ResEnv env)
        {
            return TryCoerceImpl(
                exp,
                (dynamic)fromType,
                (dynamic)toType,
                env);
        }

        private IResExp TryCoerceImpl(
            IResExp exp,
            IResTypeExp fromType,
            ResFreqQualType toType,
            ResEnv env)
        {
            // NOTE: We know that 'fromType' is not
            // a ResFreqQualType, because otherwise
            // the more specific case would have triggered.

            // \todo: Check that fromType.IsOfKind( Star )

            // The situation here is that we have some expression
            // that was either computed without a known frequency
            // (e.g. it might have just been math on literals),
            // or involved applying a "lifted" operation to some
            // frequency-qualified values, which would then need
            // to have a revised frequency applied later.

            // First, we need to attempt data-type conversion on the expression.

            var toDataType = toType.Type;
            var newExp = Coerce(exp, fromType, toDataType, env);

            // If data-type conversion failed, we don't procede.
            if (newExp is ResErrorTerm)
                return newExp;

            // We now walk the structure of the expression,
            // re-writing it to one that explicitly has the new frequency.
            return ApplyFreq(
                toType.Freq,
                newExp,
                env);
        }

        private IResExp ApplyFreq(
            IResElementRef freq,
            IResExp exp,
            ResEnv env )
        {
            return ApplyFreqImpl(
                freq,
                (dynamic) exp,
                (dynamic) exp.Type,
                env );
        }


        private IResExp ApplyFreqImpl(
            IResElementRef freq,
            IResExp exp,
            IResTypeExp type,
            ResEnv env )
        {
            env.Error(exp.Range,
                "Unhandled case in ApplyFreq");
            return ResErrorTerm.Instance;
        }

        private IResExp ApplyFreqImpl(
            IResElementRef freq,
            ResIfExp exp,
            IResTypeExp type,
            ResEnv env)
        {
            var condExp = ApplyFreq(freq, exp.Condition, env);
            var thenExp = ApplyFreq(freq, exp.Then, env);
            var elseExp = ApplyFreq(freq, exp.Else, env);

            return new ResIfExp(
                exp.Range,
                condExp,
                thenExp,
                elseExp);
        }

        private IResExp ApplyFreqImpl(
            IResElementRef freq,
            ResBaseExp exp,
            IResTypeExp type,
            ResEnv env)
        {
            if (type is ResFreqQualType)
            {
                var fqType = (ResFreqQualType)type;
                var result = Coerce(
                    exp,
                    FreqQual(freq, fqType.Type),
                    env);
                return result;
            }

            env.Error(
                exp.Range,
                CoerceErrorMessage(
                    type,
                    FreqQual(exp.Range, freq, type)));
            return ResErrorTerm.Instance;
        }



        private IResExp ApplyFreqImpl(
            IResElementRef freq,
            ResVarRef exp,
            IResTypeExp type,
            ResEnv env)
        {
            if (freq != null)
            {
                if(type is ResFreqQualType)
                {
                    var fqType = (ResFreqQualType) type;
                    var result = Coerce(
                        exp,
                        FreqQual(freq, fqType.Type),
                        env);
                    return result;
                }
            }
            else
            {
                if(!(type is ResFreqQualType))
                {
                    return exp;
                }
            }

            env.Error(
                exp.Range,
                CoerceErrorMessage(
                    type,
                    FreqQual(exp.Range, freq, type)));
            return ResErrorTerm.Instance;
        }

        private IResExp ApplyFreqImpl(
            IResElementRef freq,
            ResElementCtorApp ctorApp,
            IResTypeExp type,
            ResEnv env)
        {
            var args = (from ctorArg in ctorApp.Args
                        let attr = ctorArg.Attribute
                        let val = ApplyFreq(freq, ctorArg.Value, env)
                        select new ResElementCtorArg(attr, val)).Eager();

            if (args.Any((a) => a.Value is ResErrorTerm))
                return ResErrorTerm.Instance;

            return new ResElementCtorApp(
                ctorApp.Range,
                FreqQual(
                    ctorApp.Range,
                    freq,
                    ctorApp.Type),
                ctorApp.Element,
                args);
        }

        private IResExp ApplyFreqImpl(
            IResElementRef freq,
            ResAttributeRef attr,
            ResFreqQualType attrType,
            ResEnv env)
        {
            // A simple attribute reference needs
            // to be coerced to the desired frequency,
            // while leaving its type unaffected.

            return Coerce(
                attr,
                FreqQual(attr.Range, freq, attrType.Type),
                env);
        }

        private IResExp ApplyFreqImpl(
            IResElementRef freq,
            ResMethodApp app,
            ResFreqQualType appType,
            ResEnv env)
        {
            // A method application that returns a value with
            // an explicit rate can simply be coerced -
            // all of its arguments will have been rate-coerced
            // as part of overload resolution.

            return Coerce(
                app,
                FreqQual(app.Range, freq, appType.Type),
                env);
        }

        private IResExp ApplyFreqImpl<T>(
            IResElementRef freq,
            ResLit<T> lit,
            IResTypeExp type,
            ResEnv env)
        {
            if (type is ResFreqQualType)
                throw new NotImplementedException();

            return new ResLit<T>(
                lit.Range,
                FreqQual(lit.Range, freq, type),
                lit.Value);
        }

        private IResExp ApplyFreqImpl(
            IResElementRef freq,
            ResAttributeFetch attrFetch,
            IResTypeExp type,
            ResEnv env)
        {
            if (type is ResFreqQualType)
                throw new NotImplementedException();

            return new ResAttributeFetch(
                attrFetch.Range,
                FreqQual(attrFetch.Range, freq, type),
                ApplyFreq(freq, attrFetch.Obj, env),
                attrFetch.Attribute);
        }

        private IResExp ApplyFreqImpl(
            IResElementRef freq,
            ResMethodApp app,
            IResTypeExp type,
            ResEnv env)
        {
            var args = (from a in app.Args
                        select ApplyFreq(freq, a, env)).Eager();
            if (args.Any((a) => a is ResErrorTerm))
                return ResErrorTerm.Instance;

            return new ResMethodApp(
                app.Range,
                FreqQual(app.Range, freq, type),
                app.Method,
                args);
        }

        private IResExp ApplyFreqImpl(
            IResElementRef freq,
            ResFieldRef fieldRef,
            IResTypeExp fieldType,
            ResEnv env)
        {
            if (fieldType is ResFreqQualType)
                throw new NotImplementedException();

            var bind = fieldRef.MemberTerm as ResMemberBind;
            if( bind == null )
                throw new NotImplementedException();

            return new ResFieldRef(
                fieldRef.Range,
                fieldRef.Decl,
                new ResMemberBind(
                    fieldRef.Range,
                    ApplyFreq(freq, bind.Obj, env),
                    bind.MemberSpec),
                    FreqQual(fieldRef.Range, freq, fieldType));
        }

        private IResExp TryCoerceImpl(
            IResExp exp,
            IResTypeExp fromType,
            IResTypeExp toType,
            ResEnv env)
        {
            // Look for a suitable implicit conversion
            var conversion = FindImplicitConversion(
                exp.Range,
                env,
                fromType,
                toType,
                1);

            if (conversion != null)
            {
                env.UpdateScore(ResOverloadScore.ImplicitTypeConversion);
                return (IResExp)conversion.Apply(exp.Range, exp, env);
            }

            env.Error(
                exp.Range,
                CoerceErrorMessage(fromType, toType));

            return ResErrorTerm.Instance;
        }

        private IResExp TryCoerceImpl(
            IResExp exp,
            ResFreqQualType fromFQType,
            ResFreqQualType toFQType,
            ResEnv env)
        {
            var fromFreq = fromFQType.Freq;
            var toFreq = toFQType.Freq;

            var fromType = fromFQType.Type;
            var toType = toFQType.Type;

            var e = exp;
            if (!IsSameType(fromType, toType))
            {
                e = Coerce(e, fromType, toType, env);
                if (e is ResErrorTerm)
                    return ResErrorTerm.Instance;
            }


            // Look for a suitable implicit conversion
            var conversion = FindImplicitConversion(
                exp.Range,
                env,
                fromFQType,
                toFQType,
                100); // high enough for all practical purposes

            if (conversion != null)
            {
                return (IResExp) conversion.Apply(exp.Range, e, env);
            }

            env.Error(
                exp.Range,
                CoerceErrorMessage(fromFQType, toFQType));
            /*/
            // Once more for debugger:
            conversion = FindImplicitConversion(
                exp.Range,
                env,
                fromFQType,
                toFQType,
                100); // high enough for all practical purposes
            //*/
            return ResErrorTerm.Instance;
        }

        private bool IsCheaper(
            ImplicitConversionCost[] left,
            ImplicitConversionCost[] right )
        {
            if( left.Length != right.Length )
                return left.Length < right.Length;

            bool anyBetter = false;
            bool anyWorse = false;
            for( int ii = 0; ii < left.Length; ++ii )
            {
                if( left[ii] < right[ii] )
                {
                    anyBetter = true;
                }
                else if( left[ii] > right[ii] )
                {
                    anyWorse = true;
                }
            }

            return anyBetter && !anyWorse;
        }

        private void DumpImplicitConversionCandidate(
            SourceRange range,
            ResEnv env,
            ImplicitConversion conversion)
        {
            StringBuilder sb = new StringBuilder();
            foreach( var c in conversion.Costs )
                sb.AppendFormat("{0} ", c);
            env.AddDiagnostic(Severity.Info, range, "Candidate {0} -> {1} costs: {2}", conversion.FromType, conversion.ToType, sb.ToString());
        }

        private ImplicitConversion FindImplicitConversion(
            SourceRange range,
            ResEnv env,
            IResTypeExp fromType,
            IResTypeExp toType,
            int lengthRestriction)
        {
            if (env.DisableConversions)
            {
                return null;
            }

            // Generate an initial set of "conversions" consisting
            // of no conversion at all...
            ImplicitConversion[] implicits = new ImplicitConversion[]{
                new ImplicitConversion{
                    FromType = toType,
                    ToType = toType,
                    Apply = (r, exp, e) => exp,
                },
            };

            int depth = 0;
            while (true)
            {
                // Check how many of our candidate conversions
                // can get us to the desired output type.
                var usable = (from i in implicits
                              where IsSameType(i.FromType, fromType)
                              select i).Eager();

                foreach( var u in usable )
                {
                    usable = (from o in usable
                              where !IsCheaper( u.Costs, o.Costs )
                              select o).Eager();
                }

                // If there are several, error:
                if (usable.Length > 1)
                {
                    env.Error(
                        range,
                        "Ambiguous implicit conversion");

                    foreach (var u in usable)
                    {
                        DumpImplicitConversionCandidate(range, env, u);
                    }

                    return null;
                }
                // If only one, then use it:
                else if (usable.Length == 1)
                {
                    return usable[0];
                }

                // Otherwise we need to find another conversion
                // step that will get us to any of the available
                // types in our set of usable conversions

                implicits = (from i in implicits
                             from n in FindImplicitConversions(
                                range,
                                env,
                                (ft, tt) => IsSameType(tt, i.FromType))
                             select new ImplicitConversion
                             {
                                 ToType = i.ToType,
                                 FromType = n.FromType,
                                 Apply = (r, exp, e) => i.Apply(r, n.Apply(r, exp,e),e),
                                 Costs = n.Costs.Concat(i.Costs).ToArray(),
                             }).Eager();

                if (implicits.Length == 0)
                {
                    // No conversion possible...
                    return null;
                }

                if (depth++ >= lengthRestriction)
                    return null;
            }
        }

        private ImplicitConversion[] FindImplicitConversions(
            SourceRange range,
            ResEnv env,
            Func<IResTypeExp, IResTypeExp, bool> filter)
        {
            return env.LookupImplicits(
                range,
                (term) => GetImplicitConversions(term, env, filter)).Eager();
        }

        struct ImplicitConversionCost
        {
            public static readonly ImplicitConversionCost Default = new ImplicitConversionCost(0);

            public static ImplicitConversionCost Generic(int paramCount)
            {
                return new ImplicitConversionCost(-paramCount);
            }

            public static bool operator <(ImplicitConversionCost left, ImplicitConversionCost right)
            {
                return left._cost > right._cost;
            }

            public static bool operator >(ImplicitConversionCost left, ImplicitConversionCost right)
            {
                return left._cost < right._cost;
            }

            public override string ToString()
            {
                return _cost.ToString();
            }

            private ImplicitConversionCost(int cost)
            {
                _cost = cost;
            }

            private int _cost;
        }

        /*
        enum ImplicitConversionCost
        {
            Default = 0,
            Generic = 1,
        }
         * */

        class ImplicitConversion
        {
            public IResTypeExp FromType { get; set; }
            public IResTypeExp ToType { get; set; }
            public Func<SourceRange, IResExp, ResEnv, IResExp> Apply { get; set; }
            public ImplicitConversionCost[] Costs
            {
                get { if( _costs == null ) _costs = new ImplicitConversionCost[] { }; return _costs; }
                set { _costs = value; }
            }

            private ImplicitConversionCost[] _costs = null;
        }

        private IEnumerable<ImplicitConversion> GetImplicitConversions(
            IResTerm term,
            ResEnv env,
            Func<IResTypeExp, IResTypeExp, bool> filter )
        {
            return GetImplicitConversions(
                term,
                env,
                filter,
                ImplicitConversionCost.Default );
        }

        private IEnumerable<ImplicitConversion> GetImplicitConversions(
            IResTerm term,
            ResEnv env,
            Func<IResTypeExp, IResTypeExp, bool> filter,
            ImplicitConversionCost cost )
        {
            if (term is IResMethodRef)
            {
                var methodRef = (IResMethodRef)term;
                var parameters = methodRef.Parameters.Eager();
                if (parameters.Length == 1)
                {
                    var fromType = parameters[0].Type;
                    var toType = methodRef.ResultType;
                    if (filter(fromType, toType))
                    {
                        yield return new ImplicitConversion
                        {
                            Apply = (r, exp, e) => (IResExp)ResolveApp(
                                null,
                                r,
                                methodRef,
                                new[] { new ResPositionalArg<IResTerm>(r, exp) },
                                e.NestDisableConversions()),
                            FromType = fromType,
                            ToType = toType,
                            Costs = new ImplicitConversionCost[]{ cost },
                        };
                    }
                }
            }
            else if (term is IResGenericRef)
            {
                var genericRef = (IResGenericRef)term;
                var dummyArgSubst = new Substitution();
                var args = (from p in genericRef.Parameters
                            select MakeDummyArg(p, dummyArgSubst)).Eager();
                var app = genericRef.App(
                    term.Range,
                    args);

                var genericCost = ImplicitConversionCost.Generic(args.Length);

                var results = GetImplicitConversions(app, env, filter, genericCost).Eager();
                if (results.Length != 0)
                {
                    var diagnostics = new DiagnosticSink();
                    var checkEnv = env.NestDiagnostics(diagnostics);

                    if (CheckDummyArgs(term.Range, args, checkEnv))
                    {
                        foreach (var c in results)
                            yield return c;
                    }
                }
            }
        }

        private string CoerceErrorMessage(
            IResTypeExp fromType,
            IResTypeExp toType)
        {
            return string.Format(
                "Could not convert from type {0} to type {1}",
                fromType,
                toType);
        }

        private IResTypeExp Coerce(IResTerm term, ResKind kind, ResEnv env)
        {
            if (term is ResErrorTerm)
                return ResErrorTerm.Instance;

            var type = CoerceFilterFirst<IResTypeExp>(
                term,
                new[] {
                    ResMemberFlavor.Element,
                    ResMemberFlavor.Pipeline,
                    ResMemberFlavor.Struct,
                    ResMemberFlavor.TypeSlot},
                env,
                string.Format("Expected a {0}", kind));

            var fromKind = type.Kind;

            if (IsSubKindOf(fromKind, kind))
                return type;

            env.Error(
                term.Range,
                "Expected a {0}, found a {1}",
                kind,
                fromKind);
            return ResErrorTerm.Instance;
        }

        private IResTerm Coerce(IResTerm term, IResClassifier classifier, ResEnv env)
        {
            if (classifier is ResKind)
                return Coerce(term, (ResKind)classifier, env);
            else if (classifier is IResTypeExp)
                return Coerce(term, (IResTypeExp)classifier, env);
            throw new NotImplementedException();
        }

        private bool IsSubKindOf(
            ResKind sub,
            ResKind sup)
        {
            if (sub == sup)
                return true;

            return IsSubKindOfImpl((dynamic)sub, (dynamic)sup);
        }

        private bool IsSubKindOfImpl(
            ResKind sub,
            ResKind sup)
        {
            return false;
        }

        private bool IsSubKindOfImpl(
            ResIntervalKind sub,
            ResIntervalKind sup)
        {
            return IsSubTypeOf( sub.UpperBound, sup.UpperBound)
                && IsSubTypeOf(sup.LowerBound, sub.LowerBound);
        }

        private bool IsSubKindOfImpl(
            ResFreqQualTypeKind sub,
            ResFreqQualTypeKind sup)
        {
            return true;
        }

        private bool IsSubTypeOf(
            IResTypeExp sub,
            IResTypeExp sup)
        {
            if (sub is ResErrorTerm) return true;
            if (sup is ResErrorTerm) return true;

            return IsSubTypeOfImpl( (dynamic) sub, (dynamic) sup );
        }

        private bool IsSubTypeOfImpl(
            ResDummyTypeArg sub,
            ResFreqQualType sup)
        {
            return IsSubTypeOfImpl(
                sub,
                (IResTypeExp) sup);
        }

        private bool IsSubTypeOfImpl(
            ResFreqQualType sub,
            ResDummyTypeArg sup )
        {
            return IsSubTypeOfImpl(
                (IResTypeExp) sub,
                sup );
        }

        private bool IsSubTypeOfImpl(
            IResTypeExp sub,
            IResTypeExp sup)
        {
            return false;
        }

        private bool IsSubTypeOfImpl(
            ResElementRef sub,
            ResTypeSlotRef sup)
        {
            return false;
        }

        private bool IsSubTypeOfImpl(
            ResTypeSlotRef sub,
            ResElementRef sup)
        {
            return false;
        }

        private bool IsSubTypeOfImpl(
            ResTypeVarRef sub,
            ResTypeSlotRef sup)
        {
            return false;
        }

        private bool IsSubTypeOfImpl(
            ResTypeSlotRef sub,
            ResTypeVarRef sup)
        {
            return false;
        }

        private bool IsSubTypeOfImpl(
            ResVoidType sub,
            ResVoidType sup )
        {
            return true;
        }

        private bool IsSubTypeOfImpl(
            ResPipelineRef sub,
            ResPipelineRef sup)
        {
            foreach (var f in sub.Facets)
            {
                var b = f.OriginalPipeline;

                if (IsSameMemberTerm(b.MemberTerm, sup.MemberTerm))
                    return true;
            }

            return false;
        }

        private bool IsSubTypeOfImpl(
            ResDummyTypeArg sub,
            ResDummyTypeArg sup)
        {
            if (sub.ConcreteType != null)
                return IsSubTypeOf(sub.ConcreteType, sup);

            if (sup.ConcreteType != null)
                return IsSubTypeOf(sub, sup.ConcreteType);

            sup.LowerBounds.Add(sub);
            sub.UpperBounds.Add(sup);
            return true;
        }

        private bool IsSubTypeOfImpl(
            ResDummyTypeArg sub,
            IResTypeExp sup)
        {
            if (sub.ConcreteType != null)
                return IsSubTypeOf(sub.ConcreteType, sup);

            sub.UpperBounds.Add(sup);
            return true;
        }

        private bool IsSubTypeOfImpl(
            IResTypeExp sub,
            ResDummyTypeArg sup)
        {
            if (sup.ConcreteType != null)
                return IsSubTypeOf(sub, sup.ConcreteType);

            sup.LowerBounds.Add(sub);
            return true;
        }

        private bool IsSubTypeOfImpl(
            IResStructRef sub,
            IResStructRef sup)
        {
            // \todo: This really needs to consider the member terms...
            if (sub.Decl == sup.Decl) return true;
            return false;
        }

        private bool IsSubTypeOfImpl(
            IResElementRef sub,
            IResElementRef sup)
        {
            // \todo: This really needs to consider the member terms...
            if (sub.Decl == sup.Decl) return true;
            return false;
        }

        private bool IsSubTypeOfImpl(
            IResConceptClassRef sub,
            IResConceptClassRef sup)
        {
            if (sub.Decl.Line.OriginalLexicalID != sup.Decl.Line.OriginalLexicalID)
                return false;

            // \todo: co- and contra-variance
            return IsSameMemberTerm(
                sub.MemberTerm,
                sup.MemberTerm);
        }

        private bool IsSubTypeOfImpl(
            IResTypeSlotRef sub,
            IResTypeSlotRef sup)
        {
            if (sub.Decl.Line.OriginalLexicalID != sup.Decl.Line.OriginalLexicalID)
                return false;

            // \todo: co- and contra-variance
            return IsSameMemberTerm(
                sub.MemberTerm,
                sup.MemberTerm);
        }

        private bool IsSubTypeOfImpl(
            ResBottomType sub,
            IResTypeExp sup)
        {
            return true;
        }

        private bool IsSubTypeOfImpl(
            IResTypeExp sub,
            ResTopType sup)
        {
            return true;
        }

        private bool IsSubTypeOfImpl(
            ResFreqQualType sub,
            ResFreqQualType sup)
        {
            return IsSameType(sub.Freq, sup.Freq)
                && IsSubTypeOf(sub.Type, sup.Type);
        }

        private bool IsSubTypeOfImpl(
            ResFreqQualType sub,
            IResTypeExp sup)
        {
            return false;
        }

        private bool IsSubTypeOfImpl(
            IResTypeExp sub,
            ResFreqQualType sup)
        {
            return false;
        }

        private bool IsSubTypeOfImpl(
            ResTypeVarRef sub,
            ResTypeVarRef sup)
        {
            return sub.Decl == sup.Decl;
        }

        public bool IsSameType(
            IResTypeExp left,
            IResTypeExp right)
        {
            if (left == right) return true;
            return IsSubTypeOf(left, right)
                && IsSubTypeOf(right, left);
        }

        private struct StmtContext
        {
            public ResLabel ReturnTarget;
            public IResElementRef ImplicitFreq;
        }

        private IResExp ResolveStmt(
            AbsStmt absStmt,
            ResEnv env,
            StmtContext context )
        {
            return ResolveStmtRaw(absStmt, env, context, null);
        }

        private IResExp Continue(
            Func<ResEnv, IResExp> continuation,
            SourceRange range,
            ResEnv env,
            IResExp exp)
        {
            IResExp continueExp = continuation == null ? null : continuation(env);

            if (continueExp != null && exp != null)
            {
                return new ResSeqExp(
                    range,
                    exp,
                    continueExp);
            }

            if (continueExp != null)
                return continueExp;
            if (exp != null)
                return exp;

            return new ResVoidExp(range);
        }

        private IResExp ResolveStmtRaw(
            AbsStmt absStmt,
            ResEnv env,
            StmtContext context,
            Func<ResEnv, IResExp> continuation)
        {
            return ResolveStmtRawImpl((dynamic)absStmt, env, context, continuation);
        }

        private IResExp ResolveStmtRawImpl(
            AbsEmptyStmt absStmt,
            ResEnv env,
            StmtContext context,
            Func<ResEnv, IResExp> continuation)
        {
            return Continue(continuation, absStmt.Range, env, null);
        }

        private IResExp ResolveStmtRawImpl(
            AbsLetStmt absStmt,
            ResEnv env,
            StmtContext context,
            Func<ResEnv, IResExp> continuation)
        {
            var name = absStmt.Name;
            var range = absStmt.Range;

            var implicitFreq = context.ImplicitFreq;

            var resType = CoerceToTypeExp(
                ResolveTerm(absStmt.Type, env),
                env);

            if (resType is ResFreqQualType)
            {
            }
            else if( IsSubKindOf(resType.Kind, ResKind.Star) )
            {
                if (implicitFreq != null)
                {
                    resType = new ResFreqQualType(
                        absStmt.Range,
                        implicitFreq,
                        resType);
                }
            }
            else
            {
                env.Error(
                    absStmt.Range,
                    "Cannot declare local variable of non-proper type {0}",
                    resType);
                return ResErrorTerm.Instance;
            }

            var resVar = new ResVarDecl(
                range,
                name,
                resType);

            IResExp resVal = CoerceUnqualified(
                ResolveTerm(absStmt.Value, env),
                resType,
                env);

            var localScope = new ResLocalScope();
            localScope.Insert(name,
                (r) => new ResVarRef(r, resVar));
            var localEnv = env.NestScope(localScope);

            return new ResLetExp(
                range,
                resVar,
                resVal,
                Continue(continuation, range, localEnv, null));
        }

        private IResExp ResolveStmtRawImpl(
            AbsSeqStmt absStmt,
            ResEnv env,
            StmtContext context,
            Func<ResEnv, IResExp> continuation)
        {
            return ResolveStmtRaw(
                absStmt.Head,
                env,
                context,
                (e) => ResolveStmtRaw(
                    absStmt.Tail,
                    e,
                    context,
                    continuation));
        }

        private IResExp ResolveStmtRawImpl(
            AbsReturnStmt absReturn,
            ResEnv env,
            StmtContext context,
            Func<ResEnv, IResExp> continuation)
        {
            var returnTarget = context.ReturnTarget;
            var term = ResolveTerm(absReturn.exp, env);
            var exp = CoerceUnqualified(term, returnTarget.Type, env);

            var breakExp = new ResBreakExp(absReturn.Range, returnTarget, exp);

            return Continue(
                continuation,
                absReturn.Range,
                env,
                breakExp);
        }

        private IResExp ResolveStmtRawImpl(
            AbsSwitchStmt absStmt,
            ResEnv env,
            StmtContext context,
            Func<ResEnv, IResExp> continuation)
        {
            var resExp = CoerceToExp(
                ResolveTerm(
                    absStmt.Value,
                    env),
                env);

            var resCases = (from c in absStmt.Cases
                            select ResolveCase(c, env, context)).Eager();

            var switchExp = new ResSwitchExp(
                absStmt.Range,
                resExp,
                resCases);

            return Continue(
                continuation,
                absStmt.Range,
                env,
                switchExp);

        }

        private ResCase ResolveCase(
            AbsCase absCase,
            ResEnv env,
            StmtContext context )
        {
            var resCaseExp = CoerceToExp(
                ResolveTerm(
                    absCase.Value,
                    env),
                env);
            var resCaseBody = ResolveStmt(
                absCase.Body,
                env,
                context);

            return new ResCase(
                absCase.Range,
                resCaseExp,
                resCaseBody);
        }

        private IResExp ResolveStmtRawImpl(
            AbsIfStmt absStmt,
            ResEnv env,
            StmtContext context,
            Func<ResEnv, IResExp> continuation)
        {
            // \todo: Must CoerceUnqualified to
            // the appropriate type frequency-qualified
            // for the present method...
            var resCondition = Coerce(
                ResolveTerm(absStmt.Condition, env),
                _builtinTypeBool(absStmt.Range),
                env);

            var resThen = ResolveStmt(
                absStmt.ThenStmt,
                env,
                context);

            var resElse = absStmt.ElseStmt == null ?
                new ResVoidExp(absStmt.Range)
                : ResolveStmt(absStmt.ElseStmt, env, context);

            var ifExp = new ResIfExp(
                absStmt.Range,
                resCondition,
                resThen,
                resElse);

            return Continue(
                continuation,
                absStmt.Range,
                env,
                ifExp);

        }

        private IResExp ResolveStmtRawImpl(
            AbsExpStmt absStmt,
            ResEnv env,
            StmtContext context,
            Func<ResEnv, IResExp> continuation)
        {
            var resExp = CoerceToExp(
                ResolveTerm(absStmt.Value, env),
                env);

            return Continue(
                continuation,
                absStmt.Range,
                env,
                resExp);
        }

        private IResExp ResolveStmtRawImpl(
            AbsForStmt absStmt,
            ResEnv env,
            StmtContext context,
            Func<ResEnv, IResExp> continuation)
        {
            var resSeq = CoerceToExp(
                ResolveTerm(absStmt.Sequence, env),
                env);

            var range = absStmt.Range;
            IResTypeExp resType = FreqQual(
                absStmt.Range,
                env.ImplicitFreq,
                _builtinTypeInt32(absStmt.Range));
            var resVar = new ResVarDecl(
                range,
                absStmt.Name,
                resType);

            var localScope = new ResLocalScope();
            localScope.Insert(resVar.Name, (r) => new ResVarRef(r, resVar));
            var localEnv = env.NestScope(localScope);

            // \todo: set up 'break' and 'continue' points for body
            var resBody = ResolveStmt(
                absStmt.Body,
                localEnv,
                context);

            var resForExp = new ResForExp(
                range,
                resVar,
                resSeq,
                resBody);

            return Continue(
                continuation,
                absStmt.Range,
                env,
                resForExp);

        }

        public bool IsSubMemberTermOf(
            IResMemberTerm sub,
            IResMemberTerm sup)
        {
            return IsSubMemberTermOfImpl(
                (dynamic)sub,
                (dynamic)sup);
        }

        private bool IsSubMemberTermOfImpl(
            IResMemberTerm sub,
            IResMemberTerm sup)
        {
            return IsSameMemberTerm(sub, sup);
        }

        private bool IsSubMemberTermOfImpl(
            ResMemberBind sub,
            ResMemberBind sup)
        {
            return IsSameExp(sub.Obj, sup.Obj)
                && IsSubTypeOf(sub.MemberSpec.Container, sup.MemberSpec.Container);
        }

        private bool IsSubMemberTermOfImpl(
            ResMemberGenericApp sub,
            ResMemberGenericApp sup)
        {
            if (!IsSameMemberTerm(sub.Fun.MemberTerm, sup.Fun.MemberTerm))
                return false;

            // \todo: variance

            foreach (var p in sub.Args.Zip(sup.Args, Tuple.Create))
            {
                var subArg = ((ResGenericTypeArg) p.Item1).Type;
                var supArg = ((ResGenericTypeArg) p.Item2).Type;

                if (!IsSameType(subArg, supArg))
                    return false;
            }

            return true;
        }

        private bool IsSameMemberTerm(
            IResMemberTerm left,
            IResMemberTerm right)
        {
            return IsSameMemberTermImpl(
                (dynamic)left,
                (dynamic)right);
        }

        private bool IsSameMemberTermImpl(
            ResGlobalMemberTerm left,
            ResGlobalMemberTerm right)
        {
            return left.Decl == right.Decl;
        }

        private bool IsSameMemberTermImpl(
            ResMemberBind left,
            ResMemberBind right)
        {
            if (!IsSameExp(left.Obj, right.Obj))
                return false;

            return IsSameMemberSpec(
                left.MemberSpec,
                right.MemberSpec);
        }

        private bool IsSameMemberTermImpl(
            ResMemberGenericApp left,
            ResMemberGenericApp right)
        {
            if (!IsSameMemberTerm(left.Fun.MemberTerm, right.Fun.MemberTerm))
                return false;

            foreach (var p in left.Args.Zip(right.Args, Tuple.Create))
            {
                var leftArg = p.Item1;
                var rightArg = p.Item2;

                if (leftArg is ResGenericTypeArg)
                {
                    var leftType = ((ResGenericTypeArg)leftArg).Type;
                    var rightType = ((ResGenericTypeArg)rightArg).Type;

                    if (!IsSameType(leftType, rightType))
                        return false;
                }
                else
                {
                    var leftVal = ((ResGenericValueArg)leftArg).Value;
                    var rightVal = ((ResGenericValueArg)rightArg).Value;

                    if (!IsSameExp(leftVal, rightVal))
                        return false;
                }
            }

            return true;
        }

        private bool IsSameMemberSpec(
            IResMemberSpec left,
            IResMemberSpec right )
        {
            if (left.Decl.Line.OriginalLexicalID
                != right.Decl.Line.OriginalLexicalID)
            {
                return false;
            }

            // \todo: This takes no account of inheritance/variance.
            if (!IsSameMemberTerm(
                left.Container.MemberTerm,
                right.Container.MemberTerm))
            {
                return false;
            }

            return true;
        }

        private bool IsSameExp(
            IResExp left,
            IResExp right)
        {
            return IsSameExpImpl(
                (dynamic)left,
                (dynamic)right);
        }

        private bool IsSameExpImpl(
            ResDummyValArg left,
            ResDummyValArg right)
        {
            if (left.ConcreteVal != null)
                return IsSameExp(left.ConcreteVal, right);
            if (right.ConcreteVal != null)
                return IsSameExp(left, right.ConcreteVal);

            left.Constraints.Add(right);
            right.Constraints.Add(left);
            return true;
        }


        private bool IsSameExpImpl(
            IResExp left,
            ResDummyValArg right)
        {
            if (right.ConcreteVal != null)
                return IsSameExp(left, right.ConcreteVal);

            right.Constraints.Add(left);
            return true;
        }

        private bool IsSameExpImpl(
            ResDummyValArg left,
            IResExp right )
        {
            if (left.ConcreteVal != null)
                return IsSameExp(left.ConcreteVal, right);

            left.Constraints.Add(right);
            return true;
        }

        private bool IsSameExpImpl(
            ResVarRef left,
            ResVarRef right)
        {
            return left.Decl == right.Decl;
        }

        private bool IsSameExpImpl(
            ResAttributeRef left,
            ResAttributeRef right)
        {
            return IsSameMemberTerm(
                left.MemberTerm,
                right.MemberTerm);
        }

        private bool IsSameExpImpl(
            ResLit<Int32> left,
            ResLit<Int32> right)
        {
            return left.Value == right.Value;
        }
    }
}
