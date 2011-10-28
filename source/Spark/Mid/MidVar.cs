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
    public class MidVar
    {
        public MidVar(
            Identifier name,
            MidType type)
        {
            _name = name;
            _type = type;
        }

        public override string ToString()
        {
            return _name.ToString();
        }

        public Identifier Name { get { return _name; } }
        public MidType Type { get { return _type; } }

        private Identifier _name;
        private MidType _type;
    }

    public class MidVarRef : MidVal
    {
        public MidVarRef(
            MidVar var)
            : base(var.Type)
        {
            _var = var;
        }

        public override string ToString()
        {
            return _var.ToString();
        }

        public MidVar Var { get { return _var; } }

        private MidVar _var;
    }

    public class MidLetExp : MidExp
    {
        public MidLetExp(
            MidVar var,
            MidExp exp,
            MidExp body)
            : base(new MidDummyType())
        {
            _var = var;
            _exp = exp;
            _body = body;
        }

        public override string ToString()
        {
            return string.Format("let {0} = {1} in {2}", _var, _exp, _body);
        }

        public override MidType Type
        {
            get
            {
                return _body.Type;
            }
        }

        public MidVar Var { get { return _var; } }
        public MidExp Exp { get { return _exp; } set { _exp = value; } }
        public MidExp Body
        {
            get { return _body; }
            set { _body = value; }
        }

        private MidVar _var;
        private MidExp _exp;
        private MidExp _body;
    }
}
