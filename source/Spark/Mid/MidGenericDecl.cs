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

namespace Spark.Mid
{
    public class MidGenericDecl : MidMemberDecl
    {
        public MidGenericDecl(
            IBuilder parent,
            Identifier name,
            IResGenericDecl resDecl,
            MidEmitContext context,
            MidEmitEnv env )
            : base(parent)
        {
            _resDecl = resDecl;
            _context = context;
            _env = env;
        }

        public override IMidMemberRef CreateRef(MidMemberTerm memberTerm)
        {
            return new MidGenericRef( this, memberTerm );
        }

        public IResGenericDecl ResDecl { get { return _resDecl; } }
        public IResMemberDecl InnerDecl { get { return _resDecl.InnerDecl; } }
        public MidEmitContext Context { get { return _context; } }
        public MidEmitEnv Env { get { return _env; } }

        private IResGenericDecl _resDecl;
        private MidEmitContext _context;
        private MidEmitEnv _env;
    }

    public class MidGenericRef : MidMemberRef
    {
        public MidGenericRef(
            MidGenericDecl decl,
            MidMemberTerm memberTerm )
        {
            _decl = decl;
            _memberTerm = memberTerm;
        }

        public override IMidMemberRef GenericApp(IEnumerable<object> args)
        {
            return _decl.Context.SpecializeGenericDecl(
                _decl,
                args);
        }

        MidGenericDecl _decl;
        MidMemberTerm _memberTerm;
    }
}
