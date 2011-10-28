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
    public class ResVarDecl : IResVarDecl, IResGenericParamDecl, IResValueParamDecl
    {
        public ResVarDecl(
            SourceRange range,
            Identifier name,
            ILazy<IResTypeExp> type,
            ResVarFlags flags = ResVarFlags.None)
        {
            _range = range;
            _name = name;
            _type = type;
            _flags = flags;
        }

        public ResVarDecl(
            SourceRange range,
            Identifier name,
            IResTypeExp type,
            ResVarFlags flags = ResVarFlags.None)
            : this(range, name, Lazy.Value(type), flags)
        {
        }

        public override string ToString()
        {
            return string.Format("{0}#{1}", _name, _id);
        }

        public SourceRange Range { get { return _range; } }
        public Identifier Name { get { return _name; } }
        public IResTypeExp Type { get { return _type.Value; } }
        public ResVarFlags Flags { get { return _flags; } }

        public IResGenericParamRef MakeRef(SourceRange range, IResMemberTerm memberTerm)
        {
            return new ResVarSpec(this, memberTerm);
        }

        private SourceRange _range;
        private Identifier _name;
        private ILazy<IResTypeExp> _type;
        private ResVarFlags _flags;

        private int _id = _counter++;
        private static int _counter = 0;
    }
}
