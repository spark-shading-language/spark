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
    public class ResAttributeFetch : ResExp
    {
        public ResAttributeFetch(
            SourceRange range,
            IResTypeExp type,
            IResExp obj,
            IResExp attribute)
            : base(range, type)
        {
            _obj = obj;
            _attribute = attribute;
        }

        public IResExp Obj { get { return _obj; } }
        public IResExp Attribute { get { return _attribute; } }

        public override IResExp Substitute(Substitution subst)
        {
            return new ResAttributeFetch(
                this.Range,
                this.Type.Substitute(subst),
                _obj.Substitute(subst),
                _attribute.Substitute(subst));
        }

        private IResExp _obj;
        private IResExp _attribute;
    }
}
