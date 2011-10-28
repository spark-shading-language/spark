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
    public class MidFieldDecl : MidMemberDecl
    {
        public MidFieldDecl(
            IBuilder parent,
            Identifier name )
            : base(parent)
        {
            _name = name;
        }

        public Identifier Name { get { return _name; } }
        public MidExp Exp
        {
            get { return _exp; }
            set { _exp = value; }
        }
        public MidType Type
        {
            get { Force();  return _type; }
            set { AssertBuildable(); _type = value; }
        }

        public override IMidMemberRef CreateRef(MidMemberTerm memberTerm)
        {
            var bind = (MidMemberBind)memberTerm;
            return new MidFieldMemberRef(bind.Obj, this);
        }

        private Identifier _name;
        private MidExp _exp;
        private MidType _type;
    }

    class MidFieldMemberRef : MidMemberRef
    {
        public MidFieldMemberRef(
            MidVal obj,
            MidFieldDecl decl)
        {
            _obj = obj;
            _decl = decl;
        }

        public MidVal Obj { get { return _obj; } }
        public MidFieldDecl Decl { get { return _decl; } }

        private MidVal _obj;
        private MidFieldDecl _decl;
    }

    public class MidFieldRef : MidPath
    {
        public MidFieldRef(
            MidPath obj,
            MidFieldDecl decl)
            : base(decl.Type)
        {
            _obj = obj;
            _decl = decl;
        }

        public override string ToString()
        {
            return string.Format(
                "{0}.{1}",
                _obj,
                _decl.Name);
        }

        public MidPath Obj
        {
            get { return _obj; }
            set { _obj = value; }
        }
        public MidFieldDecl Decl { get { return _decl; } }

        private MidPath _obj;
        private MidFieldDecl _decl;
    }
}
