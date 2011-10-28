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

using Spark.Resolve;

namespace Spark.ResolvedSyntax
{
    public class ResBreakExp : ResExp
    {
        public ResBreakExp(
            SourceRange range,
            ResLabel label,
            IResExp exp)
            : base(range, new ResBottomType())
        {
            _label = label;
            _exp = exp;
        }

        public ResLabel Label { get { return _label; } }
        public IResExp Value { get { return _exp; } }

        public override IResExp Substitute(Substitution subst)
        {
            var newExp = _exp == null ? null : _exp.Substitute(subst);
            return new ResBreakExp(
                this.Range,
                subst.Lookup(_label),
                newExp);
        }

        private ResLabel _label;
        private IResExp _exp;
    }

    public class ResSeqExp : ResExp
    {
        public ResSeqExp(
            SourceRange range,
            IResExp head,
            IResExp tail )
            : base(range, new ResBottomType())
        {
            _head = head;
            _tail = tail;
        }

        public IResExp Head { get { return _head; } }
        public IResExp Tail { get { return _tail; } }

        public override IResExp Substitute(Substitution subst)
        {
            return new ResSeqExp(
                this.Range,
                _head.Substitute(subst),
                _tail.Substitute(subst));
        }

        private IResExp _head;
        private IResExp _tail;
    }

    public class ResVoidExp : ResExp
    {
        public ResVoidExp(
            SourceRange range )
            : base(range, new ResVoidType())
        {
        }

        public override IResExp Substitute(Substitution subst)
        {
            return this;
        }
    }

    public class ResCase : ISubstitutable<ResCase>
    {
        public ResCase(
            SourceRange range,
            IResExp value,
            IResExp body)
        {
            _range = range;
            _value = value;
            _body = body;
        }

        public ResCase Substitute(Substitution subst)
        {
            return new ResCase(
                _range,
                _value.Substitute(subst),
                _body.Substitute(subst));
        }

        public SourceRange Range { get { return _range; } }
        public IResExp Value { get { return _value; } }
        public IResExp Body { get { return _body; } }

        private SourceRange _range;
        private IResExp _value;
        private IResExp _body;
    }

    public class ResSwitchExp : ResExp
    {
        public ResSwitchExp(
            SourceRange range,
            IResExp value,
            IEnumerable<ResCase> cases)
            : base(range, new ResVoidType())
        {
            _value = value;
            _cases = cases.ToArray();
        }

        public override IResExp Substitute(Substitution subst)
        {
            return new ResSwitchExp(
                this.Range,
                _value.Substitute(subst),
                (from c in _cases
                 select c.Substitute(subst)).ToArray());
        }

        public IResExp Value { get { return _value; } }
        public IEnumerable<ResCase> Cases { get { return _cases; } }

        private IResExp _value;
        private IEnumerable<ResCase> _cases;

    }

    public class ResLetExp : ResExp
    {
        public ResLetExp(
            SourceRange range,
            IResVarDecl var,
            IResExp value,
            IResExp body)
            : base(range, body.Type)
        {
            _var = var;
            _value = value;
            _body = body;
        }

        public override IResExp Substitute(Substitution subst)
        {
            var newVar = new ResVarDecl(
                _var.Range,
                _var.Name,
                _var.Type.Substitute(subst));

            var newSubst = new Substitution(subst);
            newSubst.Insert(_var, newVar);

            var newValue = _value == null ? null :
                _value.Substitute(subst);

            return new ResLetExp(
                this.Range,
                newVar,
                newValue,
                _body.Substitute(newSubst));
        }

        public IResVarDecl Var { get { return _var; } }
        public IResExp Value { get { return _value; } }
        public IResExp Body { get { return _body; } }

        private IResVarDecl _var;
        private IResExp _value;
        private IResExp _body;
    }

    public class ResIfExp : ResExp
    {
        public ResIfExp(
            SourceRange range,
            IResExp condition,
            IResExp thenExp,
            IResExp elseExp )
            : base(range, thenExp.Type)
        {
            _condition = condition;
            _thenExp = thenExp;
            _elseExp = elseExp;
        }

        public override IResExp Substitute(Substitution subst)
        {
            return new ResIfExp(
                this.Range,
                _condition.Substitute(subst),
                _thenExp.Substitute(subst),
                _elseExp.Substitute(subst));
        }

        public IResExp Condition { get { return _condition; } }
        public IResExp Then { get { return _thenExp; } }
        public IResExp Else { get { return _elseExp; } }

        private IResExp _condition;
        private IResExp _thenExp;
        private IResExp _elseExp;
    }

    public class ResAssignExp : ResExp
    {
        public ResAssignExp(
            SourceRange range,
            IResExp dest,
            IResExp src)
            : base(range, new ResVoidType())
        {
            _dest = dest;
            _src = src;
        }

        public override IResExp Substitute(Substitution subst)
        {
            return new ResAssignExp(
                this.Range,
                _dest.Substitute(subst),
                _src.Substitute(subst));
        }

        public IResExp Dest { get { return _dest; } }
        public IResExp Src { get { return _src; } }

        private IResExp _dest;
        private IResExp _src;
    }

    public class ResForExp : ResExp
    {
        public ResForExp(
            SourceRange range,
            IResVarDecl var,
            IResExp sequence,
            IResExp body )
            : base(range, new ResVoidType())
        {
            _var = var;
            _sequence = sequence;
            _body = body;
        }

        public override IResExp Substitute(Substitution subst)
        {
            var newVar = new ResVarDecl(
                _var.Range,
                _var.Name,
                _var.Type.Substitute(subst));

            var newSubst = new Substitution(subst);
            newSubst.Insert(_var, newVar);

            return new ResForExp(
                this.Range,
                newVar,
                _sequence.Substitute(subst),
                _body.Substitute(newSubst));
        }

        public IResVarDecl Var { get { return _var; } }
        public IResExp Sequence { get { return _sequence; } }
        public IResExp Body { get { return _body; } }

        private IResVarDecl _var;
        private IResExp _sequence;
        private IResExp _body;
    }
}
