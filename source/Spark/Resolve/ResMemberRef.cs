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
    public abstract class ResMemberRef<D> : IResMemberRef
        where D : IResMemberDecl
    {
        public ResMemberRef(
            SourceRange range,
            D decl,
            IResMemberTerm memberTerm)
        {
            _range = range;
            _decl = decl;
            _memberTerm = memberTerm;
        }

        public override string ToString()
        {
            return _memberTerm.ToString();
        }

        public SourceRange Range { get { return _range; } }
        IResMemberDecl IResMemberRef.Decl { get { return _decl; } }
        public D Decl { get { return _decl; } }
        public IResMemberTerm MemberTerm { get { return _memberTerm; } }
        public IEnumerable<ResTag> Tags { get { return _decl.Line.Tags; } }
        public abstract IResClassifier Classifier { get; }

        IResMemberRef ISubstitutable<IResMemberRef>.Substitute(Substitution subst)
        {
            return SubstituteMemberRef(subst);
        }

        public abstract IResMemberRef SubstituteMemberRef(Substitution subst);

        private SourceRange _range;
        private D _decl;
        private IResMemberTerm _memberTerm;
    }
}
