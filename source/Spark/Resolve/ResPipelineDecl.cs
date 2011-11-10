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

namespace Spark.Resolve
{
    public class ResPipelineDeclBuilder : NewBuilder<IResPipelineDecl>, IResContainerBuilder
    {
        private ILazy<IResModuleDecl> _module;
        private SourceRange _range;
        private Identifier _name;

        private IResVarDecl _thisParameter;
        private ResFacetDeclBuilder _directFacet;
        private List<ResFacetDeclBuilder> _facets = new List<ResFacetDeclBuilder>();
        private IResPipelineRef[] _bases;

        private ResMemberConcretenessMode _concretenessMode = ResMemberConcretenessMode.Final;
        private ResMixinMode _mixinMode = ResMixinMode.Primary;
        private IResPipelineRef _thisPipeline;

        public ResPipelineDeclBuilder(
            ILazyFactory lazyFactory,
            ILazy<IResModuleDecl> module,
            SourceRange range,
            Identifier name )
            : base(lazyFactory)
        {
            _module = module;
            _range = range;
            _name = name;

            var resShaderClass = new ResPipelineDecl(
                _module,
                _range,
                _name,
                NewLazy(() => _thisParameter),
                NewLazy(() => _directFacet.Value),
                NewLazy(() => (from f in _facets select f.Value).Eager()),
                NewLazy(() => _mixinMode),
                NewLazy(() => _concretenessMode));
            SetValue(resShaderClass);
        }


        public IResVarDecl ThisParameter
        {
            get { return _thisParameter; }
            set { AssertBuildable(); _thisParameter = value; }
        }

        public ResFacetDeclBuilder DirectFacet
        {
            get { return _directFacet; }
            set { AssertBuildable(); _directFacet = value; _facets.Add(_directFacet); }
        }

        IResContainerFacetBuilder IResContainerBuilder.DirectFacetBuilder
        {
            get { return _directFacet; }
        }

        public IEnumerable<IResPipelineRef> Bases
        {
            get { return _bases; }
            set { AssertBuildable(); _bases = value.ToArray(); }
        }

        public IEnumerable<IResContainerFacetBuilder> InheritedFacets
        {
            get
            {
                return (from f in _facets
                        where f != _directFacet
                        select f);
            }
        }

        public ResMemberConcretenessMode ConcretenessMode
        {
            get { return _concretenessMode; }
            set { AssertBuildable(); _concretenessMode = value; }
        }

        public ResMixinMode MixinMode
        {
            get { return _mixinMode; }
            set { AssertBuildable(); _mixinMode = value; }
        }

        public IResPipelineRef ThisPipeline
        {
            get { return _thisPipeline; }
            set { AssertBuildable(); _thisPipeline = value; }
        }



        public IEnumerable<ResMemberLineDeclBuilder> MemberLines
        {
            get
            {
                foreach (var f in _facets)
                    foreach (var ml in f.GetMemberLines())
                        yield return ml;
            }
        }



        internal ResFacetDeclBuilder FindOrCreateFacetForBase(
            IResPipelineRef basePipelineRef)
        {
            var result = FindFacetForBase(
                basePipelineRef); // \todo: Substitution?
            if (result != null)
                return result;

            var newFacetBuilder = new ResFacetDeclBuilder(
                this.LazyFactory,
                this,
                basePipelineRef);

            _facets.Add(newFacetBuilder);
            return newFacetBuilder;
        }

        public ResFacetDeclBuilder FindFacetForBase(
            IResContainerRef basePipelineRef)
        {
            foreach (var facet in _facets)
            {
                var originalPipelineRef = facet.OriginalPipeline; // \todo: Substitution?
                if (IsSamePipeline(basePipelineRef, originalPipelineRef))
                    return facet;
            }

            return null;
        }

        private bool IsSamePipeline(
            IResContainerRef left,
            IResContainerRef right)
        {
            return IsSamePipelineImpl(
                (dynamic)left,
                (dynamic)right);
        }


        private bool IsSamePipelineImpl(
            IResPipelineRef left,
            IResPipelineRef right)
        {
            return left.Decl == right.Decl;
        }

        /*

        private ResMemberConcretenessMode _concretenessMode = ResMemberConcretenessMode.Final;
        private ResMixinMode _mixinMode = ResMixinMode.Primary;
        private SourceRange _range;
        private Identifier _name;
        private ILazy<IResModuleDecl> _module;
        private IResPipelineRef[] _bases;
        private IResVarDecl _thisParameter;
        private IResPipelineRef _thisPipeline;
        private ResFacetDecl _directFacet;
        private List<ResFacetDecl> _facets = new List<ResFacetDecl>();


        private ILazy<IResModuleDecl> _module;
        private SourceRange _range;
        private Identifier _name;
        private IResVarDecl _thisParameter;
        private IResFacetDeclBuilder _directFacet;
        private List<IResFacetDeclBuilder> _facets;
        private ResMixinMode _mixinMode;
        private ResMemberConcretenessMode _concretenessMode;
         * */
    }

    public class ResPipelineDecl : IResPipelineDecl
    {
        private ILazy<IResModuleDecl>               _module;
        private SourceRange                         _range;
        private Identifier                          _name;
        private ILazy<IResVarDecl>                  _thisParameter;
        private ILazy<IResFacetDecl>                _directFacet;
        private ILazy<IEnumerable<IResFacetDecl>>   _facets;
        private ILazy<ResMixinMode>                        _mixinMode;
        private ILazy<ResMemberConcretenessMode>           _concretenessMode;

        private IResMemberDecl[] _implicitMembers;

        public ResPipelineDecl(
            ILazy<IResModuleDecl> module,
            SourceRange range,
            Identifier name,
            ILazy<IResVarDecl> thisParameter,
            ILazy<IResFacetDecl> directFacet,
            ILazy<IEnumerable<IResFacetDecl>> facets,
            ILazy<ResMixinMode> mixinMode,
            ILazy<ResMemberConcretenessMode> concretenessMode )
        {
            _range              = range;
            _name               = name;
            _module             = module;
            _thisParameter      = thisParameter;
            _directFacet        = directFacet;
            _facets             = facets;
            _mixinMode          = mixinMode;
            _concretenessMode   = concretenessMode;
        }

        public static IResPipelineDecl Build(
            ILazyFactory lazyFactory,
            ILazy<IResModuleDecl> module,
            SourceRange range,
            Identifier name,
            Action<ResPipelineDeclBuilder> action)
        {
            var builder = new ResPipelineDeclBuilder(
                lazyFactory,
                module,
                range,
                name);
            builder.AddAction(() => action(builder));
            return builder.Value;
        }

        // IResMemberDecl

        public SourceRange Range { get { return _range; } }

        public Identifier Name { get { return _name; } }

        public IResMemberLineDecl Line
        {
            get { throw new NotImplementedException(); }
        }

        public IResMemberRef MakeRef(SourceRange range, IResMemberTerm memberTerm)
        {
            throw new NotImplementedException();
        }

        // IResGlobalDecl

        public IResPipelineRef MakeRef(SourceRange range)
        {
            return new ResPipelineRef(
                range,
                this,
                new ResGlobalMemberTerm(range, _module, this));
        }

        // IResPipelineDecl

        public IResVarDecl ThisParameter
        {
            get { return _thisParameter.Value; }
        }

        public IEnumerable<IResMemberNameGroup> LookupMembers(Identifier name)
        {
            foreach( var facet in this.Facets )
            {
                var mng = facet.LookupDirectMembers( name );
                if( mng != null )
                    yield return mng;
            }
        }

        public IResMemberLineDecl FindMember(IResMemberSpec memberSpec)
        {
            var facet = FindFacetForBase(memberSpec.Container);
            return facet.FindMember(memberSpec);
        }

        public IResFacetDecl FindFacetForBase(
            IResContainerRef basePipelineRef)
        {
            foreach (var facet in Facets)
            {
                var originalPipelineRef = facet.OriginalPipeline; // \todo: Substitution?
                if (IsSamePipeline(basePipelineRef, originalPipelineRef))
                    return facet;
            }

            return null;
        }

        private bool IsSamePipeline(
            IResContainerRef left,
            IResContainerRef right)
        {
            return IsSamePipelineImpl(
                (dynamic)left,
                (dynamic)right);
        }


        private bool IsSamePipelineImpl(
            IResPipelineRef left,
            IResPipelineRef right)
        {
            return left.Decl == right.Decl;
        }


        public IResFacetDecl DirectFacet
        {
            get { return _directFacet.Value; }
        }

        public IEnumerable<IResFacetDecl> Facets
        {
            get { return _facets.Value; }
        }

        public IEnumerable<IResMemberDecl> Members
        {
            get
            {
                foreach (var f in Facets)
                    foreach (var line in f.MemberLines)
                        yield return line.EffectiveDecl;
            }
        }

        
        public IEnumerable<IResMemberDecl> ImplicitMembers
        {
            get
            {
                if (_implicitMembers == null)
                {
                    _implicitMembers = (from m in Members
                                        where m.IsImplicit()
                                        select m).ToArray();
                }

                return _implicitMembers;
            }
        }

        public ResMemberConcretenessMode ConcretenessMode
        {
            get { return _concretenessMode.Value; }
        }

        public ResMixinMode MixinMode
        {
            get { return _mixinMode.Value; }
        }
    }

    public class ResFacetDeclBuilder : NewBuilder<IResFacetDecl>, IResContainerFacetBuilder
    {
        private IResPipelineRef _originalPipeline;
        private Dictionary<Identifier, ResMemberNameGroupBuilder> _memberNameGroups = new Dictionary<Identifier, ResMemberNameGroupBuilder>();
        private List<ResFacetDeclBuilder> _directBaseFacets = new List<ResFacetDeclBuilder>();


        public ResFacetDeclBuilder(
            ILazyFactory lazyFactory,
            ResPipelineDeclBuilder parent,
            IResPipelineRef originalPipeline)
            : base(lazyFactory)
        {
            _originalPipeline = originalPipeline;

            var resFacetDecl = new ResFacetDecl(
                Lazy.Value(originalPipeline),
                NewLazy(() => (from b in _directBaseFacets select b.Value).Eager()),
                NewLazy(() => (from mngb in _memberNameGroups.Values select mngb.Value).Eager()));

            SetValue(resFacetDecl);
        }

        /*
        public ResMemberNameGroupBuilder LookupDirectMembers(Identifier name)
        {
            ResMemberNameGroupBuilder result;
            if (_memberNameGroups.TryGetValue(name, out result))
                return result;
            return null;
        }

        public IResMemberNameGroup LookupMembers(Identifier name)
        {
            return LookupMembersImpl(name);
        }

        private IResMemberNameGroup LookupMembersImpl(Identifier name)
        {
            ResMemberNameGroup result;
            if (_memberNameGroups.TryGetValue(name, out result))
                return result;

            var derivedMembers = (from b in _directBaseFacets
                                  let mng = b.LookupMembersImpl(name)
                                  where mng != null
                                  select mng).Distinct().ToArray();

            if (derivedMembers.Length == 0)
                return null;
            if (derivedMembers.Length > 1)
                throw new NotImplementedException();

            return derivedMembers[0];
        }
        */

        public ResMemberNameGroupBuilder GetMemberNameGroup(Identifier name)
        {
            return _memberNameGroups.Cache(name,
                () => new ResMemberNameGroupBuilder(this.LazyFactory, this, name));
        }

        IEnumerable<ResMemberNameGroupBuilder> IResContainerFacetBuilder.MemberNameGroups { get { throw new NotFiniteNumberException(); } }


        /*

        public IEnumerable<ResMemberLineDeclBuilder> MemberLines
        {
            get
            {
                foreach (var mng in _memberNameGroups.Values)
                {
                    if (mng == null)
                        continue;

                    foreach (var mcg in mng.Categories_Build)
                        foreach (var ml in mcg.Lines_Build)
                            yield return ml.Value;
                }
            }
        }

        public IEnumerable<IResMemberDecl> Members
        {
            get
            {
                foreach (var ml in MemberLines)
                    yield return ml.EffectiveDecl;
            }
        }


        */

        /*
                public void AddMemberLine(ResMemberDeclLine memberLine)
                {
                    _memberLines.Add(memberLine);
                }
                */
        public void AddDirectBase(ResFacetDeclBuilder facet)
        {
            _directBaseFacets.Add(facet);
        }

        public IResPipelineRef OriginalPipeline
        {
            get { return _originalPipeline; }
        }

        public IEnumerable<ResFacetDeclBuilder> DirectBases
        {
            get { return _directBaseFacets; }
        }


    }

    public class ResFacetDecl : IResFacetDecl
    {
        private ILazy<IResPipelineRef> _originalPipeline;
        private ILazy<IEnumerable<IResFacetDecl>> _directBases;
        private ILazy<IEnumerable<IResMemberNameGroup>> _memberNameGroups;

        private Dictionary<Identifier, IResMemberNameGroup> _cachedMemberNameGroups;
        private IResMemberLineDecl[] _cachedMemberLines;

        public ResFacetDecl(
            ILazy<IResPipelineRef> originalPipeline,
            ILazy<IEnumerable<IResFacetDecl>> directBases,
            ILazy<IEnumerable<IResMemberNameGroup>> memberNameGroups)
        {
            _originalPipeline = originalPipeline;
            _directBases = directBases;
            _memberNameGroups = memberNameGroups;
        }

        public IResPipelineRef OriginalPipeline
        {
            get { return _originalPipeline.Value; }
        }

        public IEnumerable<IResFacetDecl> DirectBases
        {
            get { return _directBases.Value; }
        }

        private Dictionary<Identifier, IResMemberNameGroup> CachedMemberNameGroups
        {
            get
            {
                if (_cachedMemberNameGroups == null)
                {
                    _cachedMemberNameGroups = new Dictionary<Identifier, IResMemberNameGroup>();
                    foreach (var memberNameGroup in _memberNameGroups.Value)
                        _cachedMemberNameGroups.Add(memberNameGroup.Name, memberNameGroup);
                }
                return _cachedMemberNameGroups;
            }
        }

        public IResMemberNameGroup LookupDirectMembers(Identifier name)
        {
            IResMemberNameGroup result = null;
            CachedMemberNameGroups.TryGetValue(name, out result);
            return result;
        }

        public IEnumerable<IResMemberLineDecl> MemberLines
        {
            get
            {
                if (_cachedMemberLines == null)
                {
                    var lines = new List<IResMemberLineDecl>();
                    foreach (var memberNameGroup in CachedMemberNameGroups.Values)
                        foreach (var memberCategoryGroup in memberNameGroup.Categories)
                            foreach (var memberLine in memberCategoryGroup.Lines)
                                lines.Add(memberLine);
                    _cachedMemberLines = lines.ToArray();
                }
                return _cachedMemberLines;
            }
        }

        public IResMemberLineDecl FindMember(IResMemberSpec memberSpec)
        {
            var key = new MemberSpecDesc
            {
                Name = memberSpec.Name,
                Category = memberSpec.Decl.Line.Category,
                OriginalLexicalID = memberSpec.Decl.Line.OriginalLexicalID
            };
            return _memberCache.Cache(key,
                () => FindMemberImpl(memberSpec));
        }

        private struct MemberSpecDesc
        {
            public Identifier Name;
            public ResMemberCategory Category;
            public ResLexicalID OriginalLexicalID;

            public override bool Equals(object obj)
            {
                if (obj is MemberSpecDesc)
                {
                    var other = (MemberSpecDesc)obj;
                    return other.Name == Name
                        && other.Category == Category
                        && other.OriginalLexicalID == OriginalLexicalID;
                }

                return false;
            }

            public override int GetHashCode()
            {
                return Name.GetHashCode() ^ Category.GetHashCode() ^ OriginalLexicalID.GetHashCode();
            }
        }

        private Dictionary<MemberSpecDesc, IResMemberLineDecl> _memberCache = new Dictionary<MemberSpecDesc, IResMemberLineDecl>();

        private IResMemberLineDecl FindDirectMember(IResMemberSpec memberSpec)
        {
            IResMemberNameGroup mng = null;
            CachedMemberNameGroups.TryGetValue(memberSpec.Name, out mng);
            if (mng == null)
                return null;

            var mcg = mng.FindCategoryGroup(memberSpec.Decl.Line.Category);
            if (mcg == null)
                return null;

            foreach (var ml in mcg.Lines)
            {
                if (ml.OriginalLexicalID == memberSpec.Decl.Line.OriginalLexicalID)
                    return ml;
            }

            return null;
        }

        private IResMemberLineDecl FindMemberImpl(IResMemberSpec memberSpec)
        {
            var memberLine = FindDirectMember(memberSpec);
            if (memberLine != null)
                return memberLine;

            var memberLines = (from b in _directBases.Value
                               let ml = b.FindMember(memberSpec)
                               where ml != null
                               select ml).Distinct().ToArray();

            if (memberLines.Length == 0)
                return null;
            if (memberLines.Length > 1)
                throw new NotImplementedException();

            return memberLines[0];
        }
    }





    public class ResPipelineBuilderRef : IResContainerBuilderRef
    {
        private ResPipelineDeclBuilder _container;

        public ResPipelineBuilderRef(
            ResPipelineDeclBuilder container)
        {
            _container = container;
        }

        IResMemberRef IResContainerBuilderRef.CreateMemberRef(SourceRange range, IResMemberDecl memberDecl)
        {
            return memberDecl.MakeRef(
                range,
                new ResMemberBind(
                    range,
                    new ResVarRef(range, _container.ThisParameter, _container.ThisPipeline),
                    new ResMemberSpec(range, _container.ThisPipeline, memberDecl)));
        }

        public IResContainerBuilder ContainerDecl
        {
            get { return _container; }
        }
    }

    public class ResPipelineRef : ResMemberRef<IResPipelineDecl>, IResPipelineRef
    {
        public ResPipelineRef(
            SourceRange range,
            IResPipelineDecl decl,
            IResMemberTerm memberTerm)
            : base(range, decl, memberTerm)
        {
        }

        public override string ToString()
        {
            return this.MemberTerm.ToString();
        }

        public IResVarDecl ThisParameter { get { return this.Decl.ThisParameter; } }

        public ResKind Kind { get { return ResKind.PipelineClass; } }
        public override IResClassifier Classifier { get { return Kind; } }

        public ResMixinMode MixinMode
        {
            get { return Decl.MixinMode; }
        }

        public IEnumerable<IResMemberSpec> Members
        {
            get
            {
                foreach (var memberDecl in Decl.Members)
                    yield return new ResMemberSpec(memberDecl.Range, this, memberDecl);
            }
        }

        private IResMemberSpec[] _implicitMembers;
        public IEnumerable<IResMemberSpec> ImplicitMembers
        {
            get
            {
                if (_implicitMembers == null)
                {
                    _implicitMembers = (from m in Decl.ImplicitMembers
                                        select new ResMemberSpec(m.Range, this, m)).ToArray();
                }
                return _implicitMembers;
            }
        }

        public IEnumerable<IResMemberNameGroupSpec> LookupMembers(SourceRange range, Identifier name)
        {
            foreach (var memberNameGroupDecl in this.Decl.LookupMembers(name))
            {
                yield return new ResMemberNameGroupSpec(range, this, memberNameGroupDecl);
            }
        }

        public IResMemberLineSpec FindMember(IResMemberSpec memberSpec)
        {
            var memberDecl = this.Decl.FindMember(memberSpec);
            if (memberDecl == null)
                throw new NotImplementedException();

            return new ResMemberLineSpec(this, memberDecl);
        }

        public IResFacetRef DirectFacet
        {
            get { return new ResFacetRef(this, Decl.DirectFacet); }
        }

        public IEnumerable<IResFacetRef> Facets
        {
            get { foreach (var f in this.Decl.Facets) yield return new ResFacetRef(this, f); }
        }

        public IResPipelineRef Substitute(Substitution subst)
        {
            return this;
        }

        IResTypeExp ISubstitutable<IResTypeExp>.Substitute(Substitution subst)
        {
            return this;
        }

        IResContainerRef ISubstitutable<IResContainerRef>.Substitute(Substitution subst)
        {
            return this;
        }

        public override IResMemberRef SubstituteMemberRef(Substitution subst)
        {
            return this.Substitute(subst);
        }
    }


}
