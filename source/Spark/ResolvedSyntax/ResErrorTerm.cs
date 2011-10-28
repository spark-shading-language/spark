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

namespace Spark.ResolvedSyntax
{
    public class ResErrorTerm : IResExp, IResTypeExp, IResElementRef, IResPipelineRef, IResFreqQualType, IResConceptClassRef, IResMemberTerm
    {
        public static readonly ResErrorTerm Instance = new ResErrorTerm();

        private ResErrorTerm() { }

        SourceRange IResTerm.Range
        {
            get { return new SourceRange(); }
        }

        IResTypeExp IResExp.Type
        {
            get { return this; }
        }

        IResExp ISubstitutable<IResExp>.Substitute(Substitution subst)
        {
            return this;
        }

        ResKind IResTypeExp.Kind
        {
            get { return ResKind.Star; }
        }

        public IResClassifier Classifier
        {
            get { return this; }
        }

        IResTypeExp ISubstitutable<IResTypeExp>.Substitute(Substitution subst)
        {
            return this;
        }

        IResMemberDecl IResMemberRef.Decl
        {
            get { throw new NotImplementedException(); }
        }

        IResMemberTerm IResMemberRef.MemberTerm
        {
            get { return this; }
        }

        IResElementRef ISubstitutable<IResElementRef>.Substitute(Substitution subst)
        {
            return this;
        }


        public IEnumerable<ResTag> Tags
        {
            get { throw new NotImplementedException(); }
        }

        IResVarDecl IResContainerRef.ThisParameter
        {
            get { throw new NotImplementedException(); }
        }

        IEnumerable<IResFacetRef> IResPipelineRef.Facets
        {
            get { throw new NotImplementedException(); }
        }

        ResMixinMode IResPipelineRef.MixinMode
        {
            get { return ResMixinMode.Mixin; }
        }

        IResPipelineRef ISubstitutable<IResPipelineRef>.Substitute(Substitution subst)
        {
            throw new NotImplementedException();
        }

        IResContainerRef ISubstitutable<IResContainerRef>.Substitute(Substitution subst)
        {
            throw new NotImplementedException();
        }


        IResClassifier IResTerm.Classifier
        {
            get { throw new NotImplementedException(); }
        }

        IResFreqQualType ISubstitutable<IResFreqQualType>.Substitute(Substitution subst)
        {
            return this;
        }

        IResElementRef IResFreqQualType.Freq
        {
            get { return this; }
        }

        IResTypeExp IResFreqQualType.Type
        {
            get { return this; }
        }

        IEnumerable<ResTag> IResMemberRef.Tags
        {
            get { throw new NotImplementedException(); }
        }


        IResMemberLineSpec IResContainerRef.FindMember(IResMemberSpec memberSpec)
        {
            throw new NotImplementedException();
        }


        IEnumerable<IResMemberNameGroupSpec> IResContainerRef.LookupMembers(SourceRange range, Identifier name)
        {
            return new IResMemberNameGroupSpec[] { };
        }


        public IResFacetRef DirectFacet
        {
            get { throw new NotImplementedException(); }
        }


        IEnumerable<IResMemberSpec> IResContainerRef.Members
        {
            get { throw new NotImplementedException(); }
        }

        IEnumerable<IResMemberSpec> IResContainerRef.ImplicitMembers
        {
            get { throw new NotImplementedException(); }
        }

        public IEnumerable<IResMemberLineSpec> MemberLines
        {
            get { throw new NotImplementedException(); }
        }

        IResConceptClassRef ISubstitutable<IResConceptClassRef>.Substitute(Substitution subst)
        {
            return this;
        }

        IResMemberRef ISubstitutable<IResMemberRef>.Substitute(Substitution subst)
        {
            throw new NotImplementedException();
        }

        public Substitution Subst
        {
            get { throw new NotImplementedException(); }
        }

        IResMemberDecl IResMemberTerm.Decl
        {
            get { throw new NotImplementedException(); }
        }

        IResMemberTerm ISubstitutable<IResMemberTerm>.Substitute(Substitution subst)
        {
            throw new NotImplementedException();
        }
    }
}
