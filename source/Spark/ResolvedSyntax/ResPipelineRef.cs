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
    public class ResMemberLineSpec : IResMemberLineSpec
    {
        public ResMemberLineSpec(
            IResContainerRef container,
            IResMemberLineDecl decl)
        {
            _container = container;
            _decl = decl;
        }

        public Identifier Name
        {
            get { return _decl.Name; }
        }

        public ResLexicalID OriginalLexicalID
        {
            get { return _decl.OriginalLexicalID; }
        }

        public ResMemberCategory Category
        {
            get { return _decl.Category; }
        }

        public IResMemberSpec EffectiveSpec
        {
            get
            {
                var decl = _decl.EffectiveDecl;
                return new ResMemberSpec(
                    decl.Range,
                    _container,
                    decl);
            }
        }

        private IResContainerRef _container;
        private IResMemberLineDecl _decl;
    }

    public class ResFacetRef : IResFacetRef
    {
        public ResFacetRef(
            ResPipelineRef pipelineRef,
            IResFacetDecl facetDecl )
        {
            _pipelineRef = pipelineRef;
            _facetDecl = facetDecl;
        }

        public IResPipelineRef OriginalPipeline
        {
            get
            {
                return _facetDecl.OriginalPipeline;
            }
        }

        public IEnumerable<IResMemberLineSpec> MemberLines
        {
            get
            {
                foreach( var m in _facetDecl.MemberLines )
                    yield return new ResMemberLineSpec(_pipelineRef, m);
            }
        }

        public IEnumerable<IResFacetRef> DirectBases
        {
            get { return from f in _facetDecl.DirectBases select new ResFacetRef(_pipelineRef, f); }
        }


        private ResPipelineRef _pipelineRef;
        private IResFacetDecl _facetDecl;
    }
}
