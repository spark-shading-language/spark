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
    public class ResPipelineDecl : Builder, IResPipelineDecl, IResContainerBuilder
    {
        public ResPipelineDecl(
            IBuilder parentBuilder,
            ILazy<IResModuleDecl> module,
            SourceRange range,
            Identifier name )
            : base(parentBuilder)
        {
            _range = range;
            _name = name;
            _module = module;
        }

        public SourceRange Range { get { return _range; } }
        public Identifier Name { get { return _name; } }

        public IResPipelineRef MakeRef(SourceRange range)
        {
            return new ResPipelineRef(
                range,
                this,
                new ResGlobalMemberTerm(range, _module, this));
        }


        public IEnumerable<IResMemberNameGroup> LookupMembers(Identifier name)
        {
            Force();

            foreach( var facet in _facets )
            {
                var mng = facet.LookupDirectMembers( name );
                if( mng != null )
                    yield return mng;
            }
        }

        public IEnumerable<IResPipelineRef> Bases
        {
            get { Force(); return _bases; }
            set { AssertBuildable(); _bases = value.ToArray(); }
        }

        public IResVarDecl ThisParameter
        {
            get { return _thisParameter; }
            set { AssertBuildable(); _thisParameter = value; }
        }

        public IResPipelineRef ThisPipeline
        {
            get { return _thisPipeline; }
            set { AssertBuildable(); _thisPipeline = value; }
        }

        public ResFacetDecl DirectFacet
        {
            get { return _directFacet; }
            set { AssertBuildable(); _directFacet = value; _facets.Add(_directFacet); }
        }

        IResFacetDecl IResPipelineDecl.DirectFacet
        {
            get { Force();  return _directFacet; }
        }
        public IEnumerable<IResFacetDecl> Facets { get { Force();  return _facets; } }


        public IResContainerFacetBuilder DirectFacetBuilder
        {
            get { return _directFacet; }
        }

        public IEnumerable<IResContainerFacetBuilder> InheritedFacets
        {
            get
            {
                Force();
                return (from f in _facets
                        where f != _directFacet
                        select f);
            }
        }


        public IEnumerable<IResMemberLineDecl> MemberLines
        {
            get
            {
                Force();

                foreach (var f in _facets)
                    foreach (var ml in f.MemberLines)
                        yield return ml;
            }
        }

        public IEnumerable<IResMemberDecl> Members
        {
            get
            {
                Force();

                foreach (var f in _facets)
                    foreach (var m in f.Members)
                        yield return m;
            }
        }

        private IResMemberDecl[] _implicitMembers;
        public IEnumerable<IResMemberDecl> ImplicitMembers
        {
            get
            {
                Force();

                if (_implicitMembers == null)
                    _implicitMembers = (from m in Members
                                        where m.IsImplicit()
                                        select m).ToArray();

                return _implicitMembers;
            }
        }

        internal ResFacetDecl FindOrCreateFacetForBase(
            IResPipelineRef basePipelineRef )
        {
            var result = FindFacetForBase(
                basePipelineRef); // \todo: Substitution?
            if (result != null)
                return result;

            var newFacet = new ResFacetDecl(this, basePipelineRef);
            _facets.Add(newFacet);
            return newFacet;
        }

        public ResFacetDecl FindFacetForBase(
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
                (dynamic) left,
                (dynamic) right );
        }


        private bool IsSamePipelineImpl(
            IResPipelineRef left,
            IResPipelineRef right)
        {
            return left.Decl == right.Decl;
        }

        public IResMemberLineDecl FindMember(IResMemberSpec memberSpec)
        {
            Force();

            var facet = FindFacetForBase(memberSpec.Container);
            return facet.FindMember(memberSpec);
        }

        public ResMemberConcretenessMode ConcretenessMode
        {
            get { return _concretenessMode; }
            set { _concretenessMode = value; }
        }

        public ResMixinMode MixinMode
        {
            get {  return _mixinMode; }
            set { _mixinMode = value; }
        }


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

//        private Dictionary<Identifier, ResMemberNameGroup> _memberNameGroups = new Dictionary<Identifier, ResMemberNameGroup>();

        public ResLexicalID OriginalLexicalID
        {
            get { throw new NotImplementedException(); }
        }

        public IResMemberRef MakeRef(SourceRange range, IResMemberTerm memberTerm)
        {
            throw new NotImplementedException();
        }

        public ResMemberDeclMode MemberDeclMode
        {
            get { throw new NotImplementedException(); }
        }

        public IEnumerable<ResTag> Tags
        {
            get { throw new NotImplementedException(); }
        }
        /*
        public void AddDirectMemberLine(ResMemberDeclLine memberLine)
        {
            _directFacet.AddMemberLine(memberLine);
        }
        */
        public IResMemberLineDecl Line
        {
            get { throw new NotImplementedException(); }
        }
    }

    public class ResFacetDecl : Builder, IResFacetDecl, IResContainerFacetBuilder
    {
        public ResFacetDecl(
            IBuilder parent,
            IResPipelineRef originalPipeline )
            : base(parent)
        {
            _originalPipeline = originalPipeline;
        }

        public IResMemberNameGroup LookupDirectMembers( Identifier name )
        {
            ResMemberNameGroup result;
            if( _memberNameGroups.TryGetValue( name, out result ) )
                return result;
            return null;
        }

        public IResMemberNameGroup LookupMembers(Identifier name)
        {
            Force();
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

        public ResMemberNameGroup GetMemberNameGroup(Identifier name)
        {
            if (this.IsBuildable())
            {
                return _memberNameGroups.Cache(name,
                    () => new ResMemberNameGroup(this, name));
            }
            else
            {
                return _memberNameGroups.Cache(name, () => null);
            }
        }

        public IEnumerable<IResMemberLineDecl> MemberLines
        {
            get
            {
                Force();
                foreach (var mng in _memberNameGroups.Values)
                {
                    if (mng == null)
                        continue;

                    foreach (var mcg in mng.Categories)
                        foreach (var ml in mcg.Lines)
                            yield return ml;
                }
            }
        }

        private IEnumerable<IResMemberLineDecl> MemberLines_Build
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

        struct MemberSpecDesc
        {
            public Identifier Name;
            public ResMemberCategory Category;
            public ResLexicalID OriginalLexicalID;

            public override bool Equals( object obj )
            {
                if( obj is MemberSpecDesc )
                {
                    var other = (MemberSpecDesc) obj;
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

        public IResMemberLineDecl FindDirectMember(IResMemberSpec memberSpec)
        {
            ResMemberNameGroup mng = null;
            _memberNameGroups.TryGetValue(memberSpec.Name, out mng);
            if(mng == null)
                return null;

            var mcg = mng.FindCategoryGroup(memberSpec.Decl.Line.Category);
            if (mcg == null)
                return null;

            foreach (var ml in mcg.Lines_Build)
            {
                if( ml.Value.OriginalLexicalID == memberSpec.Decl.Line.OriginalLexicalID )
                    return ml.Value;
            }

            return null;
        }

        public IResMemberLineDecl FindMember( IResMemberSpec memberSpec )
        {
            var key = new MemberSpecDesc
            {
                Name = memberSpec.Name,
                Category = memberSpec.Decl.Line.Category,
                OriginalLexicalID = memberSpec.Decl.Line.OriginalLexicalID
            };
            return _memberCache.Cache( key,
                () => FindMemberImpl( memberSpec ) );
        }

        public IResMemberLineDecl FindMemberImpl(IResMemberSpec memberSpec)
        {
            var memberLine = FindDirectMember(memberSpec);
            if (memberLine != null)
                return memberLine;

            var memberLines = (from b in _directBaseFacets
                               let ml = b.FindMember(memberSpec)
                               where ml != null
                               select ml).Distinct().ToArray();

            if (memberLines.Length == 0)
                return null;
            if (memberLines.Length > 1)
                throw new NotImplementedException();

            return memberLines[0];
        }

        /*
                public void AddMemberLine(ResMemberDeclLine memberLine)
                {
                    _memberLines.Add(memberLine);
                }
                */
        public void AddDirectBase( ResFacetDecl facet )
        {
            _directBaseFacets.Add( facet );
        }

        public IResPipelineRef OriginalPipeline
        {
            get { return _originalPipeline; }
        }

        public IEnumerable<IResFacetDecl> DirectBases
        {
            get { return _directBaseFacets; }
        }

        private IResPipelineRef _originalPipeline;


        private Dictionary<Identifier, ResMemberNameGroup> _memberNameGroups = new Dictionary<Identifier, ResMemberNameGroup>();
        private List<ResFacetDecl> _directBaseFacets = new List<ResFacetDecl>();
    }
}
