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
    public class ResVarSpec : IResVarSpec, IResGenericParamRef
    {
        public ResVarSpec(
            IResVarDecl decl,
            IResTypeExp type )
        {
            _decl = decl;
            _type = type;
        }

        public ResVarSpec(
            IResVarDecl decl,
            IResMemberTerm memberTerm)
            : this(decl, decl.Type.Substitute(memberTerm.Subst))
        {
        }

        public Identifier Name { get { return _decl.Name; } }
        public IResVarDecl Decl { get { return _decl; } }
        public IResTypeExp Type { get { return _type; } }

        public IResClassifier Classifier { get { return _type; } }

        private IResVarDecl _decl;
        private IResTypeExp _type;

        IResGenericParamDecl IResGenericParamRef.Decl
        {
            get { return (IResGenericParamDecl) _decl; }
        }

        IResGenericArg IResGenericParamRef.MakeArg(SourceRange range)
        {
            return new ResGenericValueArg(
                new ResVarRef(range, Decl, Type));
        }

        public IResVarSpec Substitute(Substitution subst)
        {
            return new ResVarSpec(
                _decl,
                _type.Substitute(subst));
        }

        public bool IsImplicit { get { return _decl.IsImplicit(); } }
    }

    public class ResVarRef : ResExp
    {
        public ResVarRef(
            SourceRange range,
            IResVarDecl varDecl,
            IResTypeExp type)
            : base(range, type)
        {
            _varDecl = varDecl;
        }

        public ResVarRef(
            SourceRange range,
            IResVarDecl varDecl)
            : this(range, varDecl, varDecl.Type)
        {
        }

        public override string ToString()
        {
            return _varDecl.ToString();
        }

        public IResVarDecl Decl { get { return _varDecl; } }

        public override IResExp Substitute(Substitution subst)
        {
            return subst.Lookup(_varDecl, this.Range);
        }

        private IResVarDecl _varDecl;
    }
}
