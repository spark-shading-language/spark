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
    public class ResTypeParamDecl : IResTypeParamDecl
    {
        public ResTypeParamDecl(
            SourceRange range,
            Identifier name,
            ResKind kind)
        {
            _range = range;
            _name = name;
            _kind = kind;
        }

        public SourceRange Range { get { return _range; } }
        public Identifier Name { get { return _name; } }
        public ResKind Kind { get { return _kind; } }
        public int ID { get { return _id; } }

        public IResGenericParamRef MakeRef(SourceRange range, IResMemberTerm memberTerm)
        {
            return new ResTypeParamRef(range, this);
        }

        private SourceRange _range;
        private Identifier _name;
        private ResKind _kind;
        private int _id = _counter++;

        private static int _counter = 0;
    }
}
