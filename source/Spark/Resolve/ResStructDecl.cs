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
    public class ResStructDecl : ResMemberDecl, IResStructDecl, IResContainerBuilder, IResContainerFacetBuilder
    {
        public ResStructDecl(
            IResMemberLineDecl line,
            IBuilder parent,
            SourceRange range,
            Identifier name)
            : base(line, parent, range, name)
        {
        }

        public override IResMemberRef MakeRef(SourceRange range, IResMemberTerm memberTerm)
        {
            return new ResStructRef(
                range,
                this,
                memberTerm);
        }

        public override ResMemberDecl CreateInheritedDeclImpl(
                    ResolveContext resContext,
                    IResContainerBuilderRef resContainer,
                    IResMemberLineDecl resLine,
                    IBuilder parent,
                    SourceRange range,
                    IResMemberRef originalRef)
        {
            var result = new ResStructDecl(
                resLine,
                parent,
                range,
                resLine.Name);

            var firstRef = originalRef as IResStructRef;
            var firstDecl = (IResStructDecl) firstRef.Decl;

            result.AddBuildAction(() =>
            {
                var thisStruct = (IResStructRef) resContainer.CreateMemberRef(
                    range,
                    result);
                var thisParameter = new ResVarDecl(
                    range,
                    firstDecl.ThisParameter.Name,
                    thisStruct);

                result.ThisStruct = thisStruct;
                result.ThisParameter = thisParameter;

                foreach (var ml in firstRef.MemberLines)
                {
                    var memberLine = ml; // Freaking C# variable capture!!!!



                    var newMemberLineBuilder = new ResMemberLineDeclBuilder(
                        parent,
                        memberLine.Name,
                        memberLine.OriginalLexicalID,
                        memberLine.Category);

                    result
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
                            (IResContainerBuilderRef) thisStruct,
                            newMemberLineBuilder.Value,
                            newMemberLineBuilder,
                            range,
                            memberRef );
                        newMemberDecl.DoneBuilding();
                        newMemberLineBuilder.DirectDecl = newMemberDecl;
                    });

                    result.AddDirectMemberLine(newMemberLineBuilder);
                }
            });

            return result;
        }

        public IEnumerable<IResMemberDecl> Members
        {
            get
            {
                Force();
                foreach (var mng in _memberNameGroups.Values)
                    foreach (var mcg in mng.Categories)
                        foreach (var ml in mcg.Lines)
                            yield return ml.EffectiveDecl;
            }
        }

        public IEnumerable<IResMemberLineDecl> MemberLines
        {
            get
            {
                Force();
                foreach (var mng in _memberNameGroups.Values)
                    foreach (var mcg in mng.Categories)
                        foreach (var ml in mcg.Lines)
                            yield return ml;
            }
        }

        public IEnumerable<IResMemberDecl> LookupMembers(Identifier name)
        {
            Force();

            foreach (var m in Members)
            {
                if (m.Name == name)
                    yield return m;
            }
        }

        public IResContainerFacetBuilder DirectFacetBuilder
        {
            get { return this; }
        }

        public IEnumerable<IResContainerFacetBuilder> InheritedFacets
        {
            get { return new IResContainerFacetBuilder[] { }; }
        }

        public ResMemberNameGroup GetMemberNameGroup(Identifier name)
        {
            return _memberNameGroups.Cache(name,
                () => new ResMemberNameGroup(this, name));
        }

        public IResMemberNameGroup LookupMemberNameGroup(Identifier name)
        {
            Force();
            return _memberNameGroups.Cache(name, () => null);
        }

        public IResVarDecl ThisParameter
        {
            get { Force();  return _thisParameter; }
            set { AssertBuildable(); _thisParameter = value; }
        }

        public IResStructRef ThisStruct
        {
            get { Force();  return _thisStruct; }
            set { AssertBuildable(); _thisStruct = value; }
        }

        private Dictionary<Identifier, ResMemberNameGroup> _memberNameGroups = new Dictionary<Identifier, ResMemberNameGroup>();
        private IResVarDecl _thisParameter;
        private IResStructRef _thisStruct;

        public void AddDirectMemberLine( ResMemberLineDeclBuilder memberLine)
        {
        }

        public IResMemberLineDecl FindMember(IResMemberSpec memberSpec)
        {
            foreach (var ml in this.MemberLines)
            {
                if (ml.OriginalLexicalID == memberSpec.Decl.Line.OriginalLexicalID)
                    return ml;
            }

            throw new KeyNotFoundException();
        }
    }

    public class ResStructRef : ResMemberRef<ResStructDecl>, IResStructRef, IResContainerBuilderRef
    {
        public ResStructRef(
            SourceRange range,
            ResStructDecl decl,
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
            get { return ResKind.Star; }
        }
        public override IResClassifier Classifier
        {
            get { return Kind; }
        }

        public IResTypeExp Substitute(Substitution subst)
        {
            var memberTerm = this.MemberTerm.Substitute(subst);
            return new ResStructRef(
                this.Range,
                (ResStructDecl) memberTerm.Decl,
                memberTerm);
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
                foreach (var ml in this.Decl.MemberLines)
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

        public override IResMemberRef SubstituteMemberRef(Substitution subst)
        {
            return (IResMemberRef) this.Substitute(subst);
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

        public IResContainerBuilder ContainerDecl
        {
            get { return Decl; }
        }

        IResMemberRef IResContainerBuilderRef.CreateMemberRef(SourceRange range, IResMemberDecl memberDecl)
        {
            return memberDecl.MakeRef(
                range,
                new ResMemberBind(
                    range,
                    new ResVarRef(range, ThisParameter, this),
                    new ResMemberSpec(range, this, memberDecl)));
        }
    }
}
