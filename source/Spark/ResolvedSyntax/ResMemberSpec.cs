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

namespace Spark.ResolvedSyntax
{
    public class ResMemberSpec : IResMemberSpec
    {
        public ResMemberSpec(
            SourceRange range,
            IResContainerRef container,
            IResMemberDecl decl)
        {
            if (decl == null)
                throw new ArgumentNullException("decl");

            _range = range;
            _container = container;
            _decl = decl;
        }

        public override string ToString()
        {
            return string.Format("{0}::{1}",
                _container,
                _decl.Name);
        }

        public Identifier Name { get { return _decl.Name; } }
        public SourceRange Range { get { return _range; } }
        public IResContainerRef Container { get { return _container; } }
        public IResMemberDecl Decl { get { return _decl; } }

        public IResClassifier Classifier
        {
            get { throw new NotImplementedException(); }
        }

        public IResMemberRef Bind(SourceRange range, IResExp obj)
        {
            return _decl.MakeRef(
                range,
                new ResMemberBind(range, obj, this));
        }

        public IResMemberSpec Substitute(Substitution subst)
        {
            return new ResMemberSpec(
                _range,
                _container.Substitute<IResContainerRef>(subst),
                _decl);
        }

        private SourceRange _range;
        private IResContainerRef _container;
        private IResMemberDecl _decl;
    }
}
