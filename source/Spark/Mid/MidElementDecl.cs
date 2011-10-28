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
    public class MidElementDecl : MidMemberDecl
    {
        public MidElementDecl(
            IBuilder parent,
            Identifier name)
            : base(parent)
        {
            _name = name;
            _parent = parent;
        }

        public Identifier Name { get { return _name; } }

        public override string ToString()
        {
            return _name.ToString();
        }

        public IBuilder Parent
        {
            get { return _parent; }
        }

        public void AddAttributeWrapper(
            MidAttributeWrapperDecl attributeWrapper )
        {
            _attributeWrappers.Add( attributeWrapper );
        }

        public void AddAttribute(MidAttributeDecl attribute)
        {
            _attributes.Add(attribute);
        }

        public IEnumerable<MidAttributeWrapperDecl> AttributeWrappers
        {
            get { return _attributeWrappers; }
            set { _attributeWrappers = value.ToList(); }
        }

        public IEnumerable<MidAttributeDecl> Attributes { get { return _attributes.OrderBy((a) => a.Name.ToString()); } }

        public IEnumerable<MidAttributeDecl> Outputs
        {
            get
            {
                foreach (var a in Attributes)
                {
                    if (a.IsOutput)
                        yield return a;
                }
            }
        }

        public override IMidMemberRef CreateRef(MidMemberTerm memberTerm)
        {
            return new MidElementType(this);
        }

        public MidAttributeDecl CacheAttr(
            MidExp exp,
            MidType type )
        {
            MidAttributeDecl attrDecl = null;
            if( _attrCache.TryGetValue( exp, out attrDecl ) )
                return attrDecl;

            if( exp is MidAttributeRef )
            {
                var attrRef = (MidAttributeRef) exp;
                attrDecl = attrRef.Decl;
                if( attrDecl.Element == this && attrDecl.Exp != null )
                {
                    _attrCache[ exp ] = attrDecl;
                    return attrDecl;
                }
            }

            attrDecl = new MidAttributeDecl(
                _name.Factory.unique( "attr" ),
                this,
                type,
                exp );
            _attrCache[ exp ] = attrDecl;
            AddAttribute( attrDecl );
            return attrDecl;
        }

        public void Clear()
        {
            _attributes.Clear();
            _attrCache.Clear();
        }

        private Identifier _name;
        private IBuilder _parent;
        private List<MidAttributeWrapperDecl> _attributeWrappers = new List<MidAttributeWrapperDecl>();
        private List<MidAttributeDecl> _attributes = new List<MidAttributeDecl>();

        private Dictionary<MidExp, MidAttributeDecl> _attrCache = new Dictionary<MidExp, MidAttributeDecl>();
    }

    public class MidElementCtorArg
    {
        public MidElementCtorArg(
            MidAttributeDecl attribute,
            MidVal val)
        {
            _attribute = attribute;
            _val = val;
        }

        public MidAttributeDecl Attribute { get { return _attribute; } }
        public MidVal Val { get { return _val; } }

        private MidAttributeDecl _attribute;
        private MidVal _val;
    }

    public class MidElementCtorApp : MidExp
    {
        public MidElementCtorApp(
            MidElementDecl element,
            IEnumerable<MidElementCtorArg> args)
            : base(new MidElementType(element))
        {
            _element = element;
            _args = args.ToArray();
        }

        public MidElementDecl Element
        {
            get { return _element; }
        }

        public IEnumerable<MidElementCtorArg> Args
        {
            get { return _args; }
            set { _args = value.ToArray(); }
        }

        private MidElementDecl _element;
        private MidElementCtorArg[] _args;
    }
}
