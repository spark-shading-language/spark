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

namespace Spark.Resolve
{
    class ResOverloadedTerm : IResTerm
    {
        public ResOverloadedTerm(
            SourceRange range,
            IEnumerable<IResTerm> terms )
        {
            _range = range;
            _terms = terms.ToArray();
        }

        public SourceRange Range { get { return _range; } }
        public IEnumerable<IResTerm> Terms { get { return _terms; } }
        public IResClassifier Classifier { get { throw new NotImplementedException(); } }

        private SourceRange _range;
        private IResTerm[] _terms;
    }

    class ResLayeredTerm : IResTerm
    {
        public ResLayeredTerm(
            SourceRange range,
            IResTerm first,
            Func<IResTerm> restGen )
        {
            _range = range;
            _first = first;
            _restGen = restGen;
        }

        public SourceRange Range { get { return _range; } }

        public IResTerm First { get { return _first; } }
        public IResTerm Rest
        {
            get
            {
                if (_rest == null)
                    _rest = _restGen();
                return _rest;
            }
        }
        public IResClassifier Classifier { get { throw new NotImplementedException(); } }

        private SourceRange _range;
        private IResTerm _first;
        private Func<IResTerm> _restGen;
        private IResTerm _rest;
    }
}
