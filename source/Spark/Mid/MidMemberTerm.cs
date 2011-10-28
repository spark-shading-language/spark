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

namespace Spark.Mid
{
    public class MidMemberTerm
    {
    }

    public class MidGlobalMemberTerm : MidMemberTerm
    {
        public MidGlobalMemberTerm(
            MidMemberDecl decl)
        {
            _decl = decl;
        }

        private MidMemberDecl _decl;
    }

    public class MidMemberGenericApp : MidMemberTerm
    {
        public MidMemberGenericApp(
            MidMemberTerm fun,
            IEnumerable<MidType> args)
        {
        }
    }

    public class MidMemberBind : MidMemberTerm
    {
        public MidMemberBind(
            MidVal obj,
            MidMemberDecl decl)
        {
            _obj = obj;
            _decl = decl;
        }

        public MidVal Obj { get { return _obj; } }
        public MidMemberDecl Decl { get { return _decl; } }

        private MidVal _obj;
        private MidMemberDecl _decl;
    }

    public interface IMidMemberRef
    {
        IMidMemberRef GenericApp(IEnumerable<object> args);
        MidExp App(IEnumerable<MidVal> args);

        MidMemberDecl LookupMemberDecl(Spark.ResolvedSyntax.IResMemberDecl resMemberDecl);
    }

    public class MidMemberRef : IMidMemberRef
    {
        public virtual IMidMemberRef GenericApp(IEnumerable<object> args) { throw new NotImplementedException(); }
        public virtual MidExp App(IEnumerable<MidVal> args) { throw new NotImplementedException(); }
        public virtual MidMemberDecl LookupMemberDecl(Spark.ResolvedSyntax.IResMemberDecl resMemberDecl) { throw new NotImplementedException(); }
    }
}
