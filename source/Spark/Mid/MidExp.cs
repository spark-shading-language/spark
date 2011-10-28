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
    public abstract class MidLitFactory {}

    public class MidLitFactory<T> : MidLitFactory
    {
        public MidLit Lit( T value, MidType type )
        {
            return _values.Cache( value, () => new MidLit<T>( value, type ) );
        }

        private Dictionary<T, MidLit> _values = new Dictionary<T,MidLit>();
    }

    public class MidTreeCache
    {
        public MidTreeCache Get( object value )
        {
            return _children.Cache( value, () => new MidTreeCache() );
        }

        public MidTreeCache GetList( IEnumerable<object> value )
        {
            var result = this;
            foreach( var v in value )
                result = result.Get(v);
            return result;
        }

        public MidExp Cache( Func<MidExp> generator )
        {
            if( _exp == null )
                _exp = generator();
            return _exp;
        }

        private Dictionary<object, MidTreeCache> _children = new Dictionary<object, MidTreeCache>();
        private MidExp _exp = null;
    };

    public class MidExpFactory
    {
        public MidExpFactory()
        {
        }

        public MidLit Lit<T>( T value, MidType type )
        {
            var lits = (MidLitFactory<T>) _litFactories.Cache(
                typeof( T ),
                () => (MidLitFactory) new MidLitFactory<T>() );
            return lits.Lit( value, type );
        }

        public MidExp MethodApp(
            MidMethodDecl method,
            IEnumerable<MidVal> args )
        {
            return _methodApps.Get( method ).GetList( args ).Cache(
                () => new MidMethodApp( method, args ) );
        }

        public MidAttributeRef AttributeRef(
            MidAttributeDecl attr )
        {
            return (MidAttributeRef) _attrRefs.Get( attr ).Cache(
                () => new MidAttributeRef( attr ) );
        }

        public MidVoidExp Void { get { return _void; } }

        public MidAttributeFetch AttributeFetch(
            MidPath obj,
            MidAttributeDecl attribute )
        {
            return (MidAttributeFetch) _attrFetches.Get( obj ).Get( attribute ).Cache(
                () => new MidAttributeFetch( obj, attribute ) );
        }

        public MidFieldRef FieldRef(
            MidPath obj,
            MidFieldDecl decl )
        {
            return (MidFieldRef) _fieldRefs.Get( obj ).Get( decl ).Cache(
                () => new MidFieldRef( obj, decl ) );
        }

        private Dictionary<Type, MidLitFactory> _litFactories = new Dictionary<Type, MidLitFactory>();
        private MidTreeCache _methodApps = new MidTreeCache();
        private MidTreeCache _attrRefs = new MidTreeCache();
        private MidTreeCache _attrFetches = new MidTreeCache();
        private MidTreeCache _fieldRefs = new MidTreeCache();

        private MidVoidExp _void = new MidVoidExp();
    }

    public abstract class MidExp
    {
        public MidExp(
            MidType type)
        {
            if (type == null)
                throw new ArgumentNullException("type");
            _type = type;
        }

        public virtual MidType Type { get { return _type; } }

        private MidType _type;
    }

    public abstract class MidPath : MidExp
    {
        public MidPath(
            MidType type)
            : base(type)
        {
        }
    }

    public abstract class MidVal : MidPath
    {
        public MidVal(
            MidType type)
            : base(type)
        {
        }
    }

    public abstract class MidLit : MidVal
    {
        public MidLit(
            MidType type)
            : base(type)
        {
        }
    }

    public class MidLit<T> : MidLit
    {
        public MidLit(T value, MidType type)
            : base(type)
        {
            _value = value;
        }

        public override string ToString()
        {
            return _value.ToString();
        }

        public T Value { get { return _value; } }

        private T _value;
    }

    public class MidMethodApp : MidExp
    {
        public MidMethodApp(
            MidMethodDecl decl,
            IEnumerable<MidVal> args)
            : base(decl.ResultType)
        {
            _decl = decl;
            _args = args.ToArray();
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendFormat("{0}(", _decl.Name);
            bool first = true;
            foreach (var a in _args)
            {
                if( !first ) builder.Append(", ");
                first = false;
                builder.AppendFormat("{0}", a);
            }
            builder.Append(")");
            return builder.ToString();
        }

        public MidMethodDecl MethodDecl { get { return _decl; } }
        public IEnumerable<MidVal> Args
        {
            get { return _args; }
            set { _args = value.ToArray(); }
        } 

        private MidMethodDecl _decl;
        private MidVal[] _args;
    }

    public class MidDummyType : MidType
    {
    }

    public class MidAttributeRef : MidVal
    {
        public MidAttributeRef(
            MidAttributeDecl decl)
            : base(new MidDummyType())
        {
            _type = Lazy.New(() => Decl.Type);
            _decl = Lazy.Value(decl);
        }

        public MidAttributeRef(
            MidType type,
            ILazy<MidAttributeDecl> decl)
            : base(new MidDummyType())
        {
            _type = Lazy.Value(type);
            _decl = decl;
            _isLazy = true;
        }

        public override string ToString()
        {
            return string.Format("{0}", Decl.Name);
        }

        public override MidType Type
        {
            get
            {
                return _type.Value;
            }
        }

        public MidAttributeDecl Decl { get { return _decl.Value; } }

        public bool IsLazy { get { return _isLazy; } }

        private ILazy<MidType> _type;
        private ILazy<MidAttributeDecl> _decl;
        private bool _isLazy;
    }

    public class MidForExp : MidExp
    {
        public MidForExp(
            MidVar var,
            MidVal seq,
            MidExp body,
            SourceRange range)
            : base(new MidVoidType())
        {
            _var = var;
            _seq = seq;
            _body = body;
            _range = range;
        }

        public MidVar Var
        {
            get { return _var; }
        }

        public MidVal Seq
        {
            get { return _seq; }
            set { _seq = value; }
        }

        public MidExp Body
        {
            get { return _body; }
            set { _body = value; }
        }

        public SourceRange Range { get { return _range; } }

        private MidVar _var;
        private MidVal _seq;
        private MidExp _body;
        private SourceRange _range;
    }

    public class MidAssignExp : MidExp
    {
        public MidAssignExp(
            MidVal dest,
            MidVal src,
            SourceRange range)
            : base(new MidVoidType())
        {
            _dest = dest;
            _src = src;
            _range = range;
        }

        public MidVal Dest
        {
            get { return _dest; }
            set { _dest = value; }
        }
        public MidVal Src
        {
            get { return _src; }
            set { _src = value; }
        }

        public SourceRange Range { get { return _range; } }

        private MidVal _dest;
        private MidVal _src;
        private SourceRange _range;
    }

    public class MidVoidExp : MidVal
    {
        public MidVoidExp()
            : base(new MidVoidType())
        {
        }
    }

    public class MidCase
    {
        public MidCase(
            MidVal value,
            MidExp body,
            SourceRange range)
        {
            this.Value = value;
            this.Body = body;
            _range = range;
        }

        public MidVal Value { get; set; }
        public MidExp Body { get; set; }
        public SourceRange Range { get { return _range; } }

        private SourceRange _range;
    }

    public class MidSwitchExp : MidExp
    {
        public MidSwitchExp(
            MidVal value,
            IEnumerable<MidCase> cases,
            SourceRange range)
            : base(new MidVoidType())
        {
            _value = value;
            _cases = cases.ToArray();
            _range = range;
        }

        public MidVal Value
        {
            get { return _value; }
            set { _value = value; }
        }

        public IEnumerable<MidCase> Cases
        {
            get { return _cases; }
        }
        public SourceRange Range { get { return _range; } }

        private MidVal _value;
        private MidCase[] _cases;
        private SourceRange _range;
    }

    public class MidIfExp : MidExp
    {
        public MidIfExp(
            MidVal condition,
            MidExp thenExp,
            MidExp elseExp,
            SourceRange range)
            : base(new MidDummyType())
        {
            this.Condition = condition;
            this.Then = thenExp;
            this.Else = elseExp;
            _range = range;
        }

        public override MidType Type
        {
            get
            {
                return Then.Type;
            }
        }

        public SourceRange Range { get { return _range; } }

        public MidVal Condition { get; set; }
        public MidExp Then { get; set; }
        public MidExp Else { get; set; }
        private SourceRange _range;
    }

    public class MidStructVal : MidVal
    {
        public MidStructVal(
            MidType type,
            IEnumerable<MidExp> fieldVals )
            : base(type)
        {
            _fieldVals = fieldVals.ToArray();
        }

        public IEnumerable<MidExp> FieldVals
        {
            get { return _fieldVals; }
            set { _fieldVals = value.ToArray(); }
        }

        private MidExp[] _fieldVals;

    }
}
