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
    public class MidAttributeWrapperDecl : MidMemberDecl
    {
        public MidAttributeWrapperDecl(
            IBuilder parent,
            Identifier name,
            SourceRange range)
            : base( parent )
        {
            _name = name;
            _range = range;
        }

        public override string ToString()
        {
            return _name.ToString();
        }

        public Identifier Name { get { return _name; } }

        public MidAttributeDecl Attribute
        {
            get { Force(); return _attribute; }
            set { if( _attribute == null ) { AssertBuildable(); } _attribute = value; }
        }

        public override IMidMemberRef CreateRef( MidMemberTerm memberTerm )
        {
            return new MidAttributeWrapperMemberRef( this );
        }

        public MidType Type
        {
            get { return Attribute.Type; }
        }

        public SourceRange Range { get { return _range; } }

        private Identifier _name;
        private MidAttributeDecl _attribute;
        private SourceRange _range;
    }

    public class MidAttributeDecl
    {
        public MidAttributeDecl(
            Identifier name,
            MidElementDecl element,
            MidType type,
            MidExp exp )
        {
            _name = name;
            _element = element;
            _type = type;
            _exp = exp;
        }

        public override string ToString()
        {
            return _name.ToString();
        }

        public void TrySetName( Identifier name, SourceRange range )
        {
            if (_name is UniqueIdentifier)
            {
                _name = name;
            }

            TrySetRange(range);
        }

        public void TrySetRange(SourceRange range)
        {
            if (_range.fileName == null
                || (_range.fileName == "Standard Library" && range.fileName != "Standard Library"))
            {
                _range = range;
            }
        }

        public Identifier Name { get { return _name; } }
        public MidExp Exp
        {
            get { return _exp; }
            set { _exp = value; }
        }
        public MidType Type
        {
            get { return _type; }
            set { _type = value; }
        }

        public bool IsInput
        {
            get { return _isInput; }
            set { _isInput = value; }
        }

        public bool IsOutput
        {
            get { return _isOutput || _isForcedOutput; }
            set { _isOutput = value; }
        }

        public bool IsForcedOutput
        {
            get { return _isForcedOutput; }
            set { _isForcedOutput = value; }
        }

        public bool IsOptional
        {
            get { return _isOptional; }
            set { _isOptional = value; }
        }

        public bool IsAbstract
        {
            get { return _isAbstract; }
            set { _isAbstract = value; }
        }

        public MidElementDecl Element
        {
            get { return _element; }
        }

        public SourceRange Range { get { return _range; } }

        private Identifier _name;
        private MidExp _exp;
        private MidType _type;
        private bool _isInput;
        private bool _isOutput;
        private bool _isForcedOutput;
        private bool _isOptional;
        private bool _isAbstract = false;
        private MidElementDecl _element;
        private SourceRange _range;
    }

    class MidAttributeMemberRef : MidMemberRef
    {
        public MidAttributeMemberRef(
            MidAttributeDecl decl)
        {
            _decl = decl;
        }

        public MidAttributeDecl Decl { get { return _decl; } }

        private MidAttributeDecl _decl;
    }

    class MidAttributeWrapperMemberRef : MidMemberRef
    {
        public MidAttributeWrapperMemberRef(
            MidAttributeWrapperDecl decl )
        {
            _decl = decl;
        }

        public MidAttributeWrapperDecl Decl { get { return _decl; } }

        private MidAttributeWrapperDecl _decl;
    }
}
