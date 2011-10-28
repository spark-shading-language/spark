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
    public class ResTypeSlotDecl : ResMemberDecl, IResTypeSlotDecl
    {
        public ResTypeSlotDecl(
            IResMemberLineDecl line,
            IBuilder parent,
            SourceRange range,
            Identifier name )
            : base(line, parent, range, name)
        {
        }

        public override IResMemberRef MakeRef(SourceRange range, IResMemberTerm memberTerm)
        {
            return new ResTypeSlotRef(
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
            var firstRef = (ResTypeSlotRef) memberRef;
            var firstDecl = firstRef.Decl;

            var result = new ResTypeSlotDecl(
                resLine,
                parent,
                range,
                firstDecl.Name);

            return result;
        }
    }

    public class ResTypeSlotRef : ResMemberRef<ResTypeSlotDecl>, IResTypeSlotRef
    {
        public ResTypeSlotRef(
            SourceRange range,
            ResTypeSlotDecl decl,
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
            return new ResTypeSlotRef(
                this.Range,
                (ResTypeSlotDecl) memberTerm.Decl,
                memberTerm);
        }

        public override IResMemberRef SubstituteMemberRef(Substitution subst)
        {
            return (IResMemberRef) this.Substitute(subst);
        }

    }
}
