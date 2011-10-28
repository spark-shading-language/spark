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
    public class ResElementDecl : ResMemberDecl, IResElementDecl
    {
        public ResElementDecl(
            IResMemberLineDecl line,
            IBuilder parent,
            SourceRange range,
            Identifier name)
            : base(line, parent, range, name)
        {
        }

        public override IResMemberRef MakeRef(SourceRange range, IResMemberTerm memberTerm)
        {
            return new ResElementRef(
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
            var first = (IResElementRef)memberRef;
            var result = new ResElementDecl(
                resLine,
                parent,
                range,
                first.Decl.Name);
            return result;
        }
    }

    public class ResElementRef : ResMemberRef<IResElementDecl>, IResElementRef
    {
        public ResElementRef(
            SourceRange range,
            IResElementDecl decl,
            IResMemberTerm memberTerm )
            : base(range, decl, memberTerm)
        {
        }

        public override string ToString()
        {
            return this.MemberTerm.ToString();
        }

        IResTypeExp ISubstitutable<IResTypeExp>.Substitute(Substitution subst)
        {
            return this.Substitute<IResElementRef>(subst);
        }

        public IResElementRef Substitute(Substitution subst)
        {
            var memberTerm = this.MemberTerm.Substitute(subst);

            return new ResElementRef(
                this.Range,
                (IResElementDecl) memberTerm.Decl,
                memberTerm );
        }

        public override IResMemberRef SubstituteMemberRef(Substitution subst)
        {
            return this.Substitute(subst);
        }

        public ResKind Kind { get { return ResKind.Star; } }
        public override IResClassifier Classifier { get { return Kind; } }
    }

    public class ResElementCtorApp : ResExp, IResElementCtorApp
    {
        public ResElementCtorApp(
            SourceRange range,
            IResTypeExp type,
            IResElementRef element,
            IEnumerable<ResElementCtorArg> args)
            : base(range, type)
        {
            _element = element;
            _args = args.ToArray();
        }

        public override IResExp Substitute(Substitution subst)
        {
            return new ResElementCtorApp(
                this.Range,
                this.Type.Substitute(subst),
                _element.Substitute<IResElementRef>(subst),
                (from a in _args
                 select a.Substitute(subst)).ToArray());
        }

        public IResElementRef Element { get { return _element; } }
        public IEnumerable<ResElementCtorArg> Args { get { return _args; } }

        private IResElementRef _element;
        private ResElementCtorArg[] _args;
    }
}
