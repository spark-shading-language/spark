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

namespace Spark.AbstractSyntax
{
    public struct AbsSyntaxInfo
    {
        public AbsSyntaxInfo(
            SourceRange range)
        {
            this.range = range;
        }

        public SourceRange range;
    }

    public abstract class AbsSyntax
    {
        public AbsSyntax(
            AbsSyntaxInfo inInfo)
        {
            _info = inInfo;
        }

        public AbsSyntaxInfo Info { get { return _info; } }
        public SourceRange Range { get { return _info.range; } }

        private AbsSyntaxInfo _info;
    }

    public class AbsSourceRecord : AbsSyntax
    {
        public AbsSourceRecord(
            AbsSyntaxInfo inInfo,
            IEnumerable<AbsGlobalDecl> inDecls)
            : base(inInfo)
        {
            _decls = inDecls.ToArray();
        }

        public IEnumerable<AbsGlobalDecl> decls { get { return _decls; } }

        private AbsGlobalDecl[] _decls;
    }

    public class AbsGlobalDecl : AbsSyntax
    {
        public AbsGlobalDecl(
            AbsSyntaxInfo info)
            : base(info)
        {
        }

        public AbsModifiers Modifiers
        {
            get { return _modifiers; }
            set { _modifiers = value; }
        }

        public bool HasModifier(AbsModifiers modifier)
        {
            return (_modifiers & modifier) != AbsModifiers.None;
        }

        private AbsModifiers _modifiers = AbsModifiers.None;
    }

    public class AbsPipelineDecl : AbsGlobalDecl
    {
        public AbsPipelineDecl(
            AbsSyntaxInfo info,
            Identifier name,
            IEnumerable<AbsTerm> bases,
            IEnumerable<AbsMemberDecl> members )
            : base(info)
        {
            _name = name;
            _bases = bases.ToArray();
            _members = members.ToArray();
        }

        public Identifier Name { get { return _name; } }
        public IEnumerable<AbsTerm> Bases { get { return _bases; } }
        public IEnumerable<AbsMemberDecl> Members { get { return _members; } }

        private Identifier _name;
        private AbsTerm[] _bases;
        private AbsMemberDecl[] _members;
    }

    public class AbsTypeSlotDecl : AbsMemberDecl
    {
        public AbsTypeSlotDecl(
            AbsSyntaxInfo info,
            Identifier name)
            : base(info, name)
        {
        }

        public IEnumerable<AbsGenericParamDecl> GenericParams
        {
            get { return _genericParams; }
            set { _genericParams = value.ToArray(); }
        }

        private AbsGenericParamDecl[] _genericParams;
    }

    public abstract class AbsGenericParamDecl : AbsSyntax
    {
        public AbsGenericParamDecl(
            AbsSyntaxInfo info,
            Identifier name)
            : base(info)
        {
            _name = name;
        }

        public Identifier Name { get { return _name; } }

        private Identifier _name;
    }

    public class AbsGenericTypeParamDecl : AbsGenericParamDecl
    {
        public AbsGenericTypeParamDecl(
            AbsSyntaxInfo info,
            Identifier name)
            : base(info, name)
        {}
    }

    public class AbsGenericValueParamDecl : AbsGenericParamDecl
    {
        public AbsGenericValueParamDecl(
            AbsSyntaxInfo info,
            AbsTerm type,
            Identifier name,
            bool isImplicit)
            : base(info, name)
        {
            _type = type;
            _isImplicit = isImplicit;
        }

        public AbsTerm Type { get { return _type; } }
        public bool IsImplicit { get { return _isImplicit; } }

        private AbsTerm _type;
        private bool _isImplicit;
    }

    [Flags]
    public enum AbsModifiers
    {
        None = 0,
        Input = 0x1,
        Abstract = 0x2,
        Bind = 0x4,
        Override = 0x8,
        New = 0x10,
        Final = 0x20,
        Virtual = 0x40,
        Implicit = 0x80,
        Concrete = 0x100,
        Primary = 0x200,
        Mixin = 0x400,
        Output = 0x800,
        Optional = 0x1000,
    }

    public class AbsMemberDecl : AbsSyntax
    {
        public AbsMemberDecl(
            AbsSyntaxInfo inInfo,
            Identifier inName)
            : base(inInfo)
        {
            _name = inName;
        }

        public Identifier Name { get { return _name; } }
        public virtual AbsModifiers Modifiers
        {
            get { return _modifiers; }
            set { _modifiers = value; }
        }

        public bool HasModifier(AbsModifiers modifier)
        {
            return Modifiers.HasFlag(modifier);
        }

        public IList<AbsAttribute> Attributes
        {
            get { return _attributes; }
        }

        private Identifier _name;
        private AbsModifiers _modifiers = AbsModifiers.None;
        private List<AbsAttribute> _attributes = new List<AbsAttribute>();
    }

    public class AbsStructDecl : AbsMemberDecl
    {
        public AbsStructDecl(
            AbsSyntaxInfo info,
            Identifier name,
            IEnumerable<AbsMemberDecl> members )
            : base(info, name)
        {
            _members = members.ToArray();
        }

        public IEnumerable<AbsMemberDecl> Members { get { return _members; } }

        private AbsMemberDecl[] _members;
    }

    public class AbsElementDecl : AbsMemberDecl
    {
        public AbsElementDecl(
            AbsSyntaxInfo info,
            Identifier name)
            : base(info, name)
        {
        }
    }

    public class AbsSlotDecl : AbsMemberDecl
    {
        public AbsSlotDecl(
            AbsSyntaxInfo inInfo,
            Identifier inName,
            AbsTerm inType,
            AbsTerm inInit)
            : base(inInfo, inName)
        {
            _type = inType;
            _init = inInit;
        }

        public AbsTerm Type { get { return _type; } }
        public AbsTerm Init { get { return _init; } }
        public bool IsInput
        {
            get { return (this.Modifiers & AbsModifiers.Input) != 0; }
        }

        private AbsTerm _type;
        private AbsTerm _init;
    }

    public class AbsConceptDecl : AbsMemberDecl
    {
        public AbsConceptDecl(
            AbsSyntaxInfo info,
            Identifier name,
            IEnumerable<AbsGenericParamDecl> genericParams,
            IEnumerable<AbsMemberDecl> members )
            : base(info, name)
        {
            _genericParams = genericParams == null ? null :
                genericParams.ToArray();
            _members = members.ToArray();
        }

        public IEnumerable<AbsGenericParamDecl> GenericParams
        {
            get { return _genericParams; }
        }
        public IEnumerable<AbsMemberDecl> Members
        {
            get { return _members; }
        }

        private AbsGenericParamDecl[] _genericParams;
        private AbsMemberDecl[] _members;
    }

    public class AbsMethodDecl : AbsMemberDecl
    {
        public AbsMethodDecl(
            AbsSyntaxInfo inInfo,
            Identifier inName,
            AbsTerm inResultType,
            IEnumerable<AbsParamDecl> inParams,
            AbsStmt inBody)
            : base(inInfo, inName)
        {
            _params = inParams.ToArray();
            _resultType = inResultType;
            _body = inBody;
        }

        public IEnumerable<AbsParamDecl> parameters
        {
            get { return _params; }
        }
        public AbsTerm resultType
        {
            get { return _resultType; }
        }
        public AbsStmt body
        {
            get { return _body; }
        }

        public IEnumerable<AbsGenericParamDecl> GenericParams
        {
            get { return _genericParams; }
            set { _genericParams = value.ToArray(); }
        }

        private AbsGenericParamDecl[] _genericParams;

        private AbsParamDecl[] _params;
        private AbsTerm _resultType;
        private AbsStmt _body;
    }

    public class AbsParamDecl : AbsSyntax
    {
        public AbsParamDecl(
            AbsSyntaxInfo inInfo,
            Identifier inName,
            AbsTerm inType)
            : base(inInfo)
        {
            _name = inName;
            _type = inType;
        }

        public Identifier name { get { return _name; } }
        public AbsTerm type { get { return _type; } }

        private Identifier _name;
        private AbsTerm _type;
    }

    public abstract class AbsStmt : AbsSyntax
    {
        public AbsStmt(
            AbsSyntaxInfo inInfo)
            : base(inInfo)
        {
        }
    }

    public class AbsBlockStmt : AbsStmt
    {
        public AbsBlockStmt(
            AbsSyntaxInfo inInfo,
            IEnumerable<AbsStmt> inStatements)
            : base(inInfo)
        {
            _stmts = inStatements.ToArray();
        }

        public IEnumerable<AbsStmt> stmts { get { return _stmts; } }

        private AbsStmt[] _stmts;
    }

    public class AbsReturnStmt : AbsStmt
    {
        public AbsReturnStmt(
            AbsSyntaxInfo inInfo,
            AbsTerm inExp)
            : base(inInfo)
        {
            _exp = inExp;
        }

        public AbsReturnStmt(
            AbsSyntaxInfo inInfo)
            : this(inInfo, null)
        { }

        public AbsTerm exp { get { return _exp; } }

        private AbsTerm _exp;
    }

    public class AbsExpStmt : AbsStmt
    {
        public AbsExpStmt(
            AbsSyntaxInfo info,
            AbsTerm value)
            : base(info)
        {
            _value = value;
        }

        public AbsTerm Value { get { return _value; } }

        private AbsTerm _value;
    }

    public enum AbsLetFlavor
    {
        Value,      // immutable
        Variable,   // mutable
    }

    public class AbsLetStmt : AbsStmt
    {
        public AbsLetStmt(
            AbsSyntaxInfo info,
            AbsLetFlavor flavor,
            AbsTerm type,
            Identifier name,
            AbsTerm value )
            : base(info)
        {
            _flavor = flavor;
            _type = type;
            _name = name;
            _value = value;
        }

        public AbsLetFlavor Flavor { get { return _flavor; } }
        public AbsTerm Type { get { return _type; } }
        public Identifier Name { get { return _name; } }
        public AbsTerm Value { get { return _value; } }

        private AbsLetFlavor _flavor;
        private AbsTerm _type;
        private Identifier _name;
        private AbsTerm _value;
    }

    public class AbsEmptyStmt : AbsStmt
    {
        public AbsEmptyStmt(
            AbsSyntaxInfo info)
            : base(info)
        {
        }
    }

    public class AbsCase : AbsSyntax
    {
        public AbsCase(
            AbsSyntaxInfo info,
            AbsTerm value,
            AbsStmt body )
            : base(info)
        {
            _value = value;
            _body = body;
        }

        public AbsTerm Value { get { return _value; } }
        public AbsStmt Body { get { return _body; } }

        private AbsTerm _value;
        private AbsStmt _body;
    }

    public class AbsSwitchStmt : AbsStmt
    {
        public AbsSwitchStmt(
            AbsSyntaxInfo info,
            AbsTerm value,
            IEnumerable<AbsCase> cases)
            : base(info)
        {
            _value = value;
            _cases = cases.ToArray();
        }

        public AbsTerm Value { get { return _value; } }
        public IEnumerable<AbsCase> Cases { get { return _cases; } }

        private AbsTerm _value;
        private AbsCase[] _cases;
    }

    public class AbsIfStmt : AbsStmt
    {
        public AbsIfStmt(
            AbsSyntaxInfo info,
            AbsTerm condition,
            AbsStmt thenStmt,
            AbsStmt elseStmt)
            : base(info)
        {
            _condition = condition;
            _thenStmt = thenStmt;
            _elseStmt = elseStmt;
        }

        public AbsTerm Condition { get { return _condition; } }
        public AbsStmt ThenStmt { get { return _thenStmt; } }
        public AbsStmt ElseStmt { get { return _elseStmt; } }

        private AbsTerm _condition;
        private AbsStmt _thenStmt;
        private AbsStmt _elseStmt;
    }

    public class AbsForStmt : AbsStmt
    {
        public AbsForStmt(
            AbsSyntaxInfo info,
            Identifier name,
            AbsTerm sequence,
            AbsStmt body )
            : base(info)
        {
            _name = name;
            _sequence = sequence;
            _body = body;
        }

        public Identifier Name { get { return _name; } }
        public AbsTerm Sequence { get { return _sequence; } }
        public AbsStmt Body { get { return _body; } }

        private Identifier _name;
        private AbsTerm _sequence;
        private AbsStmt _body;
    }

    public class AbsSeqStmt : AbsStmt
    {
        public AbsSeqStmt(
            AbsSyntaxInfo info,
            AbsStmt head,
            AbsStmt tail)
            : base(info)
        {
            _head = head;
            _tail = tail;
        }

        public AbsStmt Head { get { return _head; } }
        public AbsStmt Tail { get { return _tail; } }

        private AbsStmt _head;
        private AbsStmt _tail;
    }

    public abstract class AbsTerm : AbsSyntax
    {
        public AbsTerm(
            AbsSyntaxInfo inInfo)
            : base(inInfo)
        {
        }
    }

    public class AbsVarRef : AbsTerm
    {
        public AbsVarRef(
            AbsSyntaxInfo inInfo,
            Identifier inName)
            : base(inInfo)
        {
            _name = inName;
        }

        public Identifier name { get { return _name; } }

        private Identifier _name;
    }

    public class AbsMemberRef : AbsTerm
    {
        public AbsMemberRef(
            AbsSyntaxInfo inInfo,
            AbsTerm inBaseObject,
            Identifier inMemberName)
            : base(inInfo)
        {
            _baseObject = inBaseObject;
            _memberName = inMemberName;
        }

        public AbsTerm baseObject { get { return _baseObject; } }
        public Identifier memberName { get { return _memberName; } }

        private AbsTerm _baseObject;
        private Identifier _memberName;
    }

    public class AbsElementRef : AbsTerm
    {
        public AbsElementRef(
            AbsSyntaxInfo inInfo,
            AbsTerm inBaseObject,
            AbsTerm inIndex)
            : base(inInfo)
        {
            _baseObject = inBaseObject;
            _index = inIndex;
        }

        public AbsTerm BaseObject { get { return _baseObject; } }
        public AbsTerm Index { get { return _index; } }

        private AbsTerm _baseObject;
        private AbsTerm _index;
    }

    public class AbsApp : AbsTerm
    {
        public AbsApp(
            AbsSyntaxInfo inInfo,
            AbsTerm inFunction,
            IEnumerable<AbsArg> inArguments)
            : base(inInfo)
        {
            _function = inFunction;
            _arguments = inArguments.ToArray();
        }

        public AbsTerm function { get { return _function; } }
        public IEnumerable<AbsArg> arguments { get { return _arguments; } }

        private AbsTerm _function;
        private AbsArg[] _arguments;
    }

    public class AbsGenericApp : AbsTerm
    {
        public AbsGenericApp(
            AbsSyntaxInfo inInfo,
            AbsTerm inFunction,
            IEnumerable<AbsArg> inArguments)
            : base(inInfo)
        {
            _function = inFunction;
            _arguments = inArguments.ToArray();
        }

        public AbsTerm function { get { return _function; } }
        public IEnumerable<AbsArg> arguments { get { return _arguments; } }

        private AbsTerm _function;
        private AbsArg[] _arguments;
    }

    public class AbsAssign : AbsTerm
    {
        public AbsAssign(
            AbsSyntaxInfo info,
            AbsTerm left,
            AbsTerm right)
            : base(info)
        {
            _left = left;
            _right = right;
        }

        public AbsTerm Left { get { return _left; } }
        public AbsTerm Right { get { return _right; } }

        private AbsTerm _left;
        private AbsTerm _right;
    }

    public class AbsIfTerm : AbsTerm
    {
        public AbsIfTerm(
            AbsSyntaxInfo info,
            AbsTerm condition,
            AbsTerm thenTerm,
            AbsTerm elseTerm)
            : base(info)
        {
            _condition = condition;
            _thenTerm = thenTerm;
            _elseTerm = elseTerm;
        }

        public AbsTerm Condition { get { return _condition; } }
        public AbsTerm Then { get { return _thenTerm; } }
        public AbsTerm Else { get { return _elseTerm; } }

        private AbsTerm _condition;
        private AbsTerm _thenTerm;
        private AbsTerm _elseTerm;
    }

    public abstract class AbsArg : AbsSyntax
    {
        public AbsArg(
            AbsSyntaxInfo info)
            : base(info)
        {
        }
    }

    public class AbsPositionalArg : AbsArg
    {
        public AbsPositionalArg(
            AbsSyntaxInfo info,
            AbsTerm term)
            : base(info)
        {
            _term = term;
        }

        public AbsTerm Term { get { return _term; } }

        private AbsTerm _term;
    }

    public class AbsKeywordArg : AbsArg
    {
        public AbsKeywordArg(
            AbsSyntaxInfo info,
            Identifier name,
            AbsTerm term)
            : base(info)
        {
            _name = name;
            _term = term;
        }

        public Identifier Name { get { return _name; } }
        public AbsTerm Term { get { return _term; } }

        private Identifier _name;
        private AbsTerm _term;
    }

    public class AbsLit : AbsTerm
    {
        public AbsLit(
            AbsSyntaxInfo inInfo)
            : base(inInfo)
        {
        }
    }

    public class AbsLit<T> : AbsLit
    {
        public AbsLit(
            AbsSyntaxInfo inInfo,
            T inValue)
            : base(inInfo)
        {
            _value = inValue;
        }

        public T Value { get { return _value; } }

        private T _value;
    }

    public class AbsVoid : AbsTerm
    {
        public AbsVoid(
            AbsSyntaxInfo info)
            : base(info)
        {
        }
    }

    public class AbsFreqQualTerm : AbsTerm
    {
        public AbsFreqQualTerm(
            AbsSyntaxInfo info,
            AbsTerm frequency,
            AbsTerm type)
            : base(info)
        {
            _freq = frequency;
            _type = type;
        }

        public AbsTerm Freq { get { return _freq; } }
        public AbsTerm Type { get { return _type; } }

        private AbsTerm _freq;
        private AbsTerm _type;
    }

    public class AbsAttribute : AbsSyntax
    {
        public AbsAttribute(
            AbsSyntaxInfo info,
            Identifier name,
            IEnumerable<AbsArg> args)
            : base(info)
        {
            _name = name;
            _args = args.ToArray();
        }

        public AbsAttribute(
            AbsSyntaxInfo info,
            Identifier name)
            : this(info, name, new AbsArg[] { })
        {
        }

        public Identifier Name { get { return _name; } }
        public IEnumerable<AbsArg> Args { get { return _args; } }

        private Identifier _name;
        private AbsArg[] _args;

    }

    public class AbsBaseExp : AbsTerm
    {
        public AbsBaseExp(
            AbsSyntaxInfo info)
            : base(info)
        { }
    }
}
