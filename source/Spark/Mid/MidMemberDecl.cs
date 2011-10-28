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
    public abstract class MidMemberDecl : Builder
    {
        public MidMemberDecl(
            IBuilder parent)
            : base(parent)
        {
        }

        public abstract IMidMemberRef CreateRef(MidMemberTerm memberTerm);
        public virtual MidMemberDecl LookupMemberDecl(
            IResMemberDecl resMemberDecl)
        {
            throw new NotImplementedException();
        }
    }

    public abstract class MidContainerDecl : MidMemberDecl
    {
        public MidContainerDecl(
            IBuilder parent,
            MidEmitContext context,
            MidEmitEnv env)
            : base(parent)
        {
            _context = context;
            _env = env;
        }

        public override MidMemberDecl LookupMemberDecl(
            IResMemberDecl resMemberDecl)
        {
            Force();

            MidMemberDecl result;
            if (_members.TryGetValue(resMemberDecl, out result))
                return result;

            return null;
        }

        public virtual void InsertMemberDecl(
            IResMemberDecl resMemberDecl,
            MidMemberDecl midMemberDecl)
        {
            _members[resMemberDecl] = midMemberDecl;
        }

        public MidEmitContext EmitContext
        {
            get { return _context; }
        }

        public MidEmitEnv Env
        {
            get { return _env; }
            set { _env = value; }
        }

        private MidEmitContext _context;
        private MidEmitEnv _env;
        protected Dictionary<IResMemberDecl, MidMemberDecl> _members = new Dictionary<IResMemberDecl, MidMemberDecl>();
    }
}
