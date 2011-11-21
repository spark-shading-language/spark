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

namespace Spark.Mid
{
    public class MidPipelineDecl : MidContainerDecl
    {
        public MidPipelineDecl(
            IBuilder parent,
            Identifier name,
            MidEmitContext context,
            MidEmitEnv env,
            SourceRange range)
            : base(parent, context, env)
        {
            _name = name;
            _range = range;
        }

        private SourceRange _range;
        public SourceRange Range { get { return _range; } }

        public Identifier Name { get { return _name; } }

        public bool IsAbstract
        {
            get { Force(); return _isAbstract; }
            set { AssertBuildable(); _isAbstract = value; }
        }

        public bool IsPrimary
        {
            get { Force(); return _isPrimary; }
            set { AssertBuildable(); _isPrimary = value; }
        }

        public MidFacetDecl AddFacet(MidPipelineRef originalPipeline)
        {
            var facet = new MidFacetDecl(
                this,
                this.EmitContext,
                this.Env,
                this,
                originalPipeline);
            _facets.Add(facet);
            return facet;
        }

        public IEnumerable<MidElementDecl> Elements
        {
            get
            {
                Force();
                foreach (var f in _facets)
                    foreach (var e in f.Elements)
                        yield return e;
            }
        }

        public IEnumerable<MidMethodDecl> Methods
        {
            get
            {
                Force();
                foreach (var f in _facets)
                    foreach (var m in f.Methods)
                        yield return m;
            }
        }

        public IEnumerable<MidFacetDecl> Facets
        {
            get
            {
                Force();
                return _facets;
            }
        }

        public MidFacetDecl DirectFacet
        {
            get
            {
                return _facets.First();
            }
        }

        private Identifier _name;
        private bool _isAbstract = false;
        private bool _isPrimary = false;
        private List<MidFacetDecl> _facets = new List<MidFacetDecl>();

        public override IMidMemberRef CreateRef(MidMemberTerm memberTerm)
        {
            return new MidPipelineRef(this, memberTerm);
        }
    }

    public class MidPipelineRef : MidType, IMidMemberRef
    {
        public MidPipelineRef(
            MidPipelineDecl decl,
            MidMemberTerm memberTerm)
        {
            _decl = decl;
            _memberTerm = memberTerm;
        }

        public MidPipelineDecl Decl
        {
            get { return _decl; }
        }

        private MidPipelineDecl _decl;
        private MidMemberTerm _memberTerm;


        public IMidMemberRef GenericApp(IEnumerable<object> args)
        {
            throw new NotImplementedException();
        }

        public MidExp App(
            SourceRange range,
            IEnumerable<MidVal> args)
        {
            throw new NotImplementedException();
        }

        public MidMemberDecl LookupMemberDecl(IResMemberDecl resMemberDecl)
        {
            return _decl.LookupMemberDecl(resMemberDecl);
        }
    }

    public class MidFacetDecl : MidContainerDecl
    {
        public MidFacetDecl(
            IBuilder parent,
            MidEmitContext context,
            MidEmitEnv env,
            MidPipelineDecl parentPipeline,
            MidPipelineRef originalPipeline)
            : base(parent, context, env)
        {
            _parentPipeline = parentPipeline;
            _originalPipeline = originalPipeline;
        }

        public void AddAttribute(MidAttributeWrapperDecl attr)
        {
            _attributes.Add(attr);
        }

        public IEnumerable<MidAttributeDecl> Attributes
        {
            get
            {
                Force();
                foreach( var wrapper in _attributes )
                    yield return wrapper.Attribute;
            }
        }

        public void AddElement(MidElementDecl element)
        {
            _elements.Add(element);
        }

        public IEnumerable<MidElementDecl> Elements { get { Force();  return _elements; } }

        public void AddMethod(MidMethodDecl method)
        {
            _methods.Add(method);
        }

        public IEnumerable<MidMethodDecl> Methods { get { Force();  return _methods; } }

        public override IMidMemberRef CreateRef(MidMemberTerm memberTerm)
        {
            throw new NotImplementedException();
        }

        public MidPipelineDecl Pipeline
        {
            get { return _parentPipeline; }
        }

        public MidPipelineRef OriginalShaderClass
        {
            get { return _originalPipeline; }
        }

        private MidPipelineDecl _parentPipeline;
        private MidPipelineRef _originalPipeline;

        private List<MidAttributeWrapperDecl> _attributes = new List<MidAttributeWrapperDecl>();
        private List<MidElementDecl> _elements = new List<MidElementDecl>();
        private List<MidMethodDecl> _methods = new List<MidMethodDecl>();
    }




    public class MidConceptClassDecl : MidContainerDecl
    {
        public MidConceptClassDecl(
            IBuilder parent,
            Identifier name,
            IEnumerable<IResMemberDecl> members,
            MidEmitContext context,
            MidEmitEnv env )
            : base(parent, context, env)
        {
            _name = name;
            _members = members.ToArray();
        }

        public Identifier Name { get { return _name; } }

        public override IMidMemberRef CreateRef(MidMemberTerm memberTerm)
        {
            return new MidConceptClassRef(this, memberTerm);
        }

        public IEnumerable<IResMemberDecl> Members
        {
            get { return _members; }
        }

        private Identifier _name;
        new private IResMemberDecl[] _members;
    }

    public class MidConceptClassRef : MidType, IMidMemberRef
    {
        public MidConceptClassRef(
            MidConceptClassDecl decl,
            MidMemberTerm memberTerm)
        {
            _decl = decl;
            _memberTerm = memberTerm;
        }

        private MidConceptClassDecl _decl;
        private MidMemberTerm _memberTerm;

        public MidConceptClassDecl Decl { get { return _decl; } }

        public IMidMemberRef GenericApp(IEnumerable<object> args)
        {
            throw new NotImplementedException();
        }

        public MidExp App(
            SourceRange range,
            IEnumerable<MidVal> args)
        {
            throw new NotImplementedException();
        }

        public MidMemberDecl LookupMemberDecl(IResMemberDecl resMemberDecl)
        {
            return _decl.LookupMemberDecl(resMemberDecl);
        }
    }

    public class MidConceptVal : MidVal
    {
        public MidConceptVal(
            SourceRange range,
            MidConceptClassRef conceptClass,
            IEnumerable<IMidMemberRef> memberRefs)
            : base(range, conceptClass)
        {
            _memberRefs = memberRefs.ToArray();
        }

        public IEnumerable<IMidMemberRef> MemberRefs { get { return _memberRefs; } }

        private IMidMemberRef[] _memberRefs;
    }


}
