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

namespace Spark
{
    public abstract class Identifier
    {
        internal Identifier(
            IdentifierFactory factory )
        {
            _factory = factory;
        }

        public IdentifierFactory Factory { get { return _factory; } }

        private IdentifierFactory _factory = null;
    }

    public class SimpleIdentifier : Identifier
    {
        internal SimpleIdentifier(
            string inName,
            IdentifierFactory factory)
            : base(factory)
        {
            _name = inName;
        }

        public override string ToString()
        {
            return _name;
        }

        public string name { get { return _name; } }

        private string _name;
    }

    public class OperatorIdentifier : Identifier
    {
        internal OperatorIdentifier(
            string operatorName,
            IdentifierFactory factory )
            : base( factory )
        {
            _operator = operatorName;
        }

        public override string ToString()
        {
            return string.Format( "operator({0})", _operator );
        }

        public string Operator { get { return _operator; } }

        private string _operator;
    }

    public class UniqueIdentifier : Identifier
    {
        internal UniqueIdentifier(
            string name,
            int counter,
            IdentifierFactory factory )
            : base( factory )
        {
            _name = name;
            _counter = counter;
        }

        public override string ToString()
        {
            return string.Format("{0}${1}",
                _name,
                _counter );
            ;
        }

        private string _name;
        private int _counter;
    }

    public class IdentifierFactory
    {
        public Identifier simpleIdentifier(string name)
        {
            return _simpleIdentifiers.Cache( name,
                () => new SimpleIdentifier( name, this ) );
        }

        public Identifier operatorIdentifier(string operatorName)
        {
            return _operatorIdentifiers.Cache( operatorName,
                () => new OperatorIdentifier( operatorName, this ) );
        }

        public Identifier unique( string inName )
        {
            return new UniqueIdentifier( inName, _counter++, this);
        }

        private IDictionary<string, SimpleIdentifier> _simpleIdentifiers =
            new Dictionary<string, SimpleIdentifier>();
        private IDictionary<string, OperatorIdentifier> _operatorIdentifiers =
            new Dictionary<string, OperatorIdentifier>();
        private int _counter = 0;
    }
}
