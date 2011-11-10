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
    public class ResConceptClassDeclBuilder : NewBuilder<IResConceptClassDecl>, IResContainerBuilder, IResContainerFacetBuilder
    {
        public ResConceptClassDeclBuilder(
            ILazyFactory lazyFactory,
            ILazy<IResMemberLineDecl> line,
            SourceRange range,
            Identifier name )
            : base(lazyFactory)
        {
            var resConceptClassDecl = new ResConceptClassDecl(
                line,
                range,
                name,
                NewLazy(() => _thisParameter),
                NewLazy(() => (from mngb in _memberNameGroups.Values select mngb.Value).Eager()));
            SetValue(resConceptClassDecl);
        }

        public IResContainerFacetBuilder DirectFacetBuilder
        {
            get { return this; }
        }

        public IEnumerable<IResContainerFacetBuilder> InheritedFacets
        {
            get { return new IResContainerFacetBuilder[] { }; }
        }

        public ResMemberNameGroupBuilder GetMemberNameGroup(Identifier name)
        {
            return _memberNameGroups.Cache(name,
                () => new ResMemberNameGroupBuilder(this.LazyFactory, this, name));
        }

        public IResVarDecl ThisParameter
        {
            get { return _thisParameter; }
            set { AssertBuildable(); _thisParameter = value; }
        }

        public IResConceptClassRef ThisConceptClass
        {
            get { return _thisConceptClass; }
            set { AssertBuildable(); _thisConceptClass = value; }
        }

        private Dictionary<Identifier, ResMemberNameGroupBuilder> _memberNameGroups = new Dictionary<Identifier, ResMemberNameGroupBuilder>();
        private IResVarDecl _thisParameter;
        private IResConceptClassRef _thisConceptClass;

        public void AddDirectMemberLine(ResMemberLineDeclBuilder memberLine)
        {
        }
        /*
        public IResMemberLineDecl FindMember(IResMemberSpec memberSpec)
        {
            foreach (var ml in this.MemberLines)
            {
                if (ml.OriginalLexicalID == memberSpec.Decl.Line.OriginalLexicalID)
                    return ml;
            }

            throw new KeyNotFoundException();
        }

        public IEnumerable<ResMemberLineDeclBuilder> MemberLines
        {
            get
            {
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
        */

        IEnumerable<ResMemberNameGroupBuilder> IResContainerFacetBuilder.MemberNameGroups { get { throw new NotFiniteNumberException(); } }

    }

    public class ResConceptClassDecl : ResMemberDecl, IResConceptClassDecl
    {
        private ILazy<IResVarDecl> _thisParameter;
        private ILazy<IEnumerable<IResMemberNameGroup>> _memberNameGroups;
        private Dictionary<Identifier, IResMemberNameGroup> _cachedMemberNameGroups;

        public ResConceptClassDecl(
            ILazy<IResMemberLineDecl> line,
            SourceRange range,
            Identifier name,
            ILazy<IResVarDecl> thisParameter,
            ILazy<IEnumerable<IResMemberNameGroup>> memberNameGroups)
            : base(line, range, name)
        {
            _thisParameter = thisParameter;
            _memberNameGroups = memberNameGroups;
        }

        public static IResConceptClassDecl Build(
            ILazyFactory lazyFactory,
            ILazy<IResMemberLineDecl> line,
            SourceRange range,
            Identifier name,
            Action<ResConceptClassDeclBuilder> action)
        {
            var builder = new ResConceptClassDeclBuilder(
                lazyFactory,
                line,
                range,
                name);
            builder.AddAction(() => action(builder));
            builder.DoneBuilding();
            return builder.Value;
        }

        // ResMemberDecl

        public override IResMemberRef MakeRef(SourceRange range, IResMemberTerm memberTerm)
        {
            return new ResConceptClassRef(
                range,
                this,
                memberTerm);
        }

        public override IResMemberDecl CreateInheritedDeclImpl(
                    ResolveContext resContext,
                    IResContainerBuilderRef resContainer,
                    ILazy<IResMemberLineDecl> resLine,
                    SourceRange range,
                    IResMemberRef originalRef)
        {
            var firstRef = originalRef as IResConceptClassRef;
            var firstDecl = (IResConceptClassDecl)firstRef.Decl;

            var result = ResConceptClassDecl.Build(
                resContext.LazyFactory,
                resLine,
                range,
                firstDecl.Name,
                (builder) =>
            {
                var thisConceptClassBuilder = new ResConceptClassBuilderRef(
                    range,
                    builder);

                var thisConceptClass = (IResConceptClassRef)resContainer.CreateMemberRef(
                    range,
                    builder.Value);
                var thisParameter = new ResVarDecl(
                    range,
                    firstDecl.ThisParameter.Name,
                    thisConceptClass);

                builder.ThisConceptClass = thisConceptClass;
                builder.ThisParameter = thisParameter;

                foreach (var ml in firstRef.MemberLines)
                {
                    var memberLine = ml; // Freaking C# variable capture!!!!

                    var newMemberLineBuilder = new ResMemberLineDeclBuilder(
                        builder.LazyFactory,
                        memberLine.Name,
                        memberLine.OriginalLexicalID,
                        memberLine.Category);

                    builder
                        .GetMemberNameGroup(memberLine.Name)
                        .GetMemberCategoryGroup(memberLine.Category)
                        .AddLine(newMemberLineBuilder);

                    newMemberLineBuilder.AddAction(() =>
                    {
                        var memberRef = memberLine.EffectiveSpec.Bind(
                            range,
                            new ResVarRef(range, thisParameter));

                        var newMemberDecl = CreateInheritedDecl(
                            resContext,
                            thisConceptClassBuilder,
                            newMemberLineBuilder,
                            range,
                            memberRef);
                        newMemberLineBuilder.DirectDecl = newMemberDecl;
                    });

                    builder.AddDirectMemberLine(newMemberLineBuilder);
                }
            });

            return result;
        }

        // IResConceptClassDecl

        /*
        public IEnumerable<IResMemberDecl> Members
        {
            get
            {
                foreach (var mng in _memberNameGroups.Value)
                {
                    if (mng == null)
                        continue;
                    foreach (var mcg in mng.Categories)
                        foreach (var ml in mcg.Lines)
                            yield return ml.EffectiveDecl;
                }
            }
        }

        public IEnumerable<IResMemberLineDecl> MemberLines
        {
            get
            {
                foreach (var mng in _memberNameGroups.Value)
                {
                    if (mng == null)
                        continue;

                    foreach (var mcg in mng.Categories)
                        foreach (var ml in mcg.Lines)
                            yield return ml;
                }
            }
        }

        public IEnumerable<IResMemberDecl> LookupMembers(Identifier name)
        {
            foreach (var m in Members)
            {
                if (m.Name == name)
                    yield return m;
            }
        }
         * */

        public IResVarDecl ThisParameter
        {
            get { return _thisParameter.Value; }
        }

        public IResMemberNameGroup LookupMemberNameGroup(Identifier name)
        {
            if (_cachedMemberNameGroups == null)
            {
                _cachedMemberNameGroups = new Dictionary<Identifier, IResMemberNameGroup>();
                foreach (var memberNameGroup in _memberNameGroups.Value)
                    _cachedMemberNameGroups.Add(memberNameGroup.Name, memberNameGroup);
            }

            return _cachedMemberNameGroups.Cache(name, () => null);
        }

        public IEnumerable<IResMemberNameGroup> MemberNameGroups { get { return _memberNameGroups.Value; } }
    }

    public class ResConceptClassBuilderRef : IResContainerBuilderRef
    {
        private SourceRange _range;
        private ResConceptClassDeclBuilder _conceptClassDeclBuilder;

        public ResConceptClassBuilderRef(
            SourceRange range,
            ResConceptClassDeclBuilder conceptClassDeclBuilder)
        {
            _range = range;
            _conceptClassDeclBuilder = conceptClassDeclBuilder;
        }

        IResContainerBuilder IResContainerBuilderRef.ContainerDecl
        {
            get { return _conceptClassDeclBuilder; }
        }

        IResMemberRef IResContainerBuilderRef.CreateMemberRef(SourceRange range, IResMemberDecl memberDecl)
        {
            throw new NotImplementedException();
            /*
            return memberDecl.MakeRef(
                range,
                new ResMemberBind(
                    range,
                    new ResVarRef(range, ThisParameter, this),
                    new ResMemberSpec(range, this, memberDecl)));
             */
        }
    }

    public class ResConceptClassRef : ResMemberRef<ResConceptClassDecl>, IResConceptClassRef
    {
        public ResConceptClassRef(
            SourceRange range,
            ResConceptClassDecl decl,
            IResMemberTerm memberTerm)
            : base(range, decl, memberTerm)
        {
        }

        public override string ToString()
        {
            return this.MemberTerm.ToString();
        }

        public ResKind Kind
        {
            get { return ResKind.ConceptClass; }
        }
        public override IResClassifier Classifier
        {
            get { return Kind; }
        }

        IResTypeExp ISubstitutable<IResTypeExp>.Substitute(Substitution subst)
        {
            return this.Substitute<IResConceptClassRef>(subst);
        }

        public IResConceptClassRef Substitute(Substitution subst)
        {
            var memberTerm = this.MemberTerm.Substitute(subst);
            return new ResConceptClassRef(
                this.Range,
                (ResConceptClassDecl)memberTerm.Decl,
                memberTerm);
        }

        public override IResMemberRef SubstituteMemberRef(Substitution subst)
        {
            return this.Substitute(subst);
        }

        public IEnumerable<IResMemberNameGroupSpec> LookupMembers(SourceRange range, Identifier name)
        {
            var nameGroup = Decl.LookupMemberNameGroup(name);
            if( nameGroup != null )
            {
                yield return new ResMemberNameGroupSpec(
                    range,
                    this,
                    nameGroup );
            }
        }

        public IEnumerable<IResMemberLineSpec> MemberLines
        {
            get
            {
                foreach (var ml in this.Decl.GetMemberLines())
                    yield return new ResMemberLineSpec(
                        this,
                        ml);
            }
        }

        public IEnumerable<IResMemberSpec> Members
        {
            get
            {
                foreach (var ml in MemberLines)
                    yield return ml.EffectiveSpec;
            }
        }

        public IEnumerable<IResMemberSpec> ImplicitMembers
        {
            get
            {
                return new IResMemberSpec[] { };
            }
        }

        IResContainerRef ISubstitutable<IResContainerRef>.Substitute(Substitution subst)
        {
            return (IResContainerRef)this.Substitute(subst);
        }

        public IResVarDecl ThisParameter
        {
            get { return this.Decl.ThisParameter; }
        }

        public IResMemberLineSpec FindMember(IResMemberSpec memberSpec)
        {
            var memberLine = this.Decl.FindMember(memberSpec);
            return new ResMemberLineSpec(this, memberLine);
        }
    }
}
