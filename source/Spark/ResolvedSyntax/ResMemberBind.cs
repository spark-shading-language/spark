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
    public class ResMemberBind : IResMemberTerm
    {
        public ResMemberBind(
            SourceRange range,
            IResExp obj,
            IResMemberSpec memberSpec)
        {
            _range = range;
            _obj = obj;
            _memberSpec = memberSpec;

            _subst = new Substitution( memberSpec.Container.MemberTerm.Subst );
            // \todo: Need to ensure "obj" is clone-able...
            // \todo: Need to iteratively re-subst...
            _subst.Insert(
                memberSpec.Container.ThisParameter,
                (r) => obj);
        }

        public override string ToString()
        {
#if VERBOSE
            return string.Format("{1}::{2}<{0}>",
                _obj,
                _memberSpec.Container,
                _memberSpec.Decl.Name);
#else
            if (_obj.Type is IResPipelineRef)
                return _memberSpec.Name.ToString();

            return string.Format("{0}.{1}",
                _obj,
                _memberSpec.Name);
#endif
        }

        public IResMemberTerm Substitute(Substitution subst)
        {
            var obj = _obj.Substitute(subst);
            var memberSpec = _memberSpec.Substitute(subst);

            var objType = obj.Type;
            var dataType = objType;
            if (objType is ResFreqQualType)
            {
                dataType = ((ResFreqQualType)objType).Type;
            }
            if (dataType is Spark.Resolve.ResDummyTypeArg)
            {
                var typeArg = (Spark.Resolve.ResDummyTypeArg)dataType;
                if(typeArg.ConcreteType != null )
                    dataType = typeArg.ConcreteType;
            }
            if (dataType is ResErrorTerm)
            {
            }
            else if (dataType is IResContainerRef)
            {
                var containerRef = (IResContainerRef)dataType;
                memberSpec = containerRef.FindMember(memberSpec).EffectiveSpec;
            }

            return new ResMemberBind(
                _range,
                obj,
                memberSpec);
        }

        public IResMemberDecl Decl { get { return _memberSpec.Decl; } }
        public IResExp Obj { get { return _obj; } }
        public IResMemberSpec MemberSpec { get { return _memberSpec; } }

        public Substitution Subst { get { return _subst; } }


        private SourceRange _range;
        private IResExp _obj;
        private IResMemberSpec _memberSpec;
        private Substitution _subst;
    }
}
