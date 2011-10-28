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
    public class ResTypeVarRef : IResTypeExp
    {
        public ResTypeVarRef(
            SourceRange range,
            IResTypeParamDecl varDecl)
        {
            _range = range;
            _varDecl = varDecl;
        }

        public override string ToString()
        {
            return string.Format("{0}#{1}",
                _varDecl.Name,
                _varDecl.ID);
        }

        public IResTypeExp Substitute(Substitution subst)
        {
            return subst.Lookup(_varDecl, _range);
        }

        public SourceRange Range { get { return _range; } }
        public ResKind Kind { get { return _varDecl.Kind; } }
        public IResClassifier Classifier { get { return Kind; } }
        public IResTypeParamDecl Decl { get { return _varDecl; } }

        private SourceRange _range;
        private IResTypeParamDecl _varDecl;
    }
}
