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
    class ResFieldDecl : ResMemberDecl, IResFieldDecl
    {
        public ResFieldDecl(
            IResMemberLineDecl line,
            IBuilder parent,
            SourceRange range,
            Identifier name)
            : base(line, parent, range, name)
        {
        }

        public IResTypeExp Type
        {
            get { Force(); return _type; }
            set { AssertBuildable(); _type = value; }
        }

        public IResExp Init
        {
            get { Force(); return _init; }
            set { AssertBuildable(); _init = value; }
        }

        public override IResMemberRef MakeRef(SourceRange range, IResMemberTerm memberTerm)
        {
            return new ResFieldRef(
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
                    IResMemberRef memberRef)
        {
            var firstRef = (ResFieldRef)memberRef;
            var firstDecl = firstRef.Decl;

            var result = new ResFieldDecl(
                resLine,
                parent,
                range,
                firstDecl.Name);
            result.AddBuildAction(() =>
            {
                result.Type = firstRef.Type;
                result.Init = firstRef.Init;
            });
            return result;
        }

        private IResTypeExp _type;
        private IResExp _init;
    }

    class ResFieldRef : ResMemberRef<ResFieldDecl>, IResFieldRef
    {
        public ResFieldRef(
            SourceRange range,
            ResFieldDecl decl,
            IResMemberTerm memberTerm,
            IResTypeExp type )
            : base(range, decl, memberTerm)
        {
            _type = type;
        }

        public ResFieldRef(
            SourceRange range,
            ResFieldDecl decl,
            IResMemberTerm memberTerm )
            : this(range, decl, memberTerm, decl.Type.Substitute(memberTerm.Subst))
        {
        }

        public IResExp Init
        {
            get
            {
                if (this.Decl.Init == null) return null;
                return this.Decl.Init.Substitute(this.MemberTerm.Subst);
            }
        }

        public IResExp Substitute(Substitution subst)
        {
            var memberTerm = this.MemberTerm.Substitute(subst);
            return new ResFieldRef(
                this.Range,
                (ResFieldDecl)memberTerm.Decl,
                memberTerm,
                _type.Substitute(subst));
        }

        public override IResMemberRef SubstituteMemberRef(Substitution subst)
        {
            return (IResMemberRef)this.Substitute(subst);
        }

        public IResTypeExp Type
        {
            get { return _type; }
        }
        public override IResClassifier Classifier { get { return Type; } }

        private IResTypeExp _type;
    }
}
