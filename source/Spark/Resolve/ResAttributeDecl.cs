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
    public class ResAttributeDecl : ResMemberDecl, IResAttributeDecl
    {
        public ResAttributeDecl(
            IResMemberLineDecl line,
            IBuilder parent,
            SourceRange range,
            Identifier name)
            : base(line, parent, range, name)
        {
        }

        public IResFreqQualType Type
        {
            get { Force(); return _type; }
            set { AssertBuildable(); _type = value; }
        }

        public IResFreqQualType Type_Build
        {
            get { return _type; }
        }


        public IResExp Init
        {
            get
            {
                Force();
                if (_initBuilder == null)
                    return null;
                return _initBuilder.Value;
            }
        }

        public ILazy<IResExp> InitBuilder
        {
            get
            {
                Force();
                return _initBuilder;
            }
            set
            {
                AssertBuildable();
                _initBuilder = value;
            }
        }

        public override IResMemberRef MakeRef(SourceRange range, IResMemberTerm memberTerm)
        {
            return new ResAttributeRef(
                range,
                this,
                memberTerm);
        }

        public override ResMemberDecl CreateInheritedDeclImpl(
                    ResolveContext resContext,
                    IResContainerBuilderRef resContainer,
                    IResMemberLineDecl resLine,
                    IBuilder parent,
                    SourceRange range,
                    IResMemberRef originalRef)
        {
            var firstRef = (ResAttributeRef)originalRef;
            var firstDecl = firstRef.Decl;

            var result = new ResAttributeDecl(
                resLine,
                parent,
                range,
                firstDecl.Name);
            result.AddBuildAction(() =>
                {
                    result.IsInput = firstDecl.IsInput;
                    result.IsOutput = firstDecl.IsOutput;
                    result.IsOptional = firstDecl.IsOptional;

                    result.Type = firstRef.Type;

                    if (firstRef.Init != null)
                    {
                        result.InitBuilder = new Lazy<IResExp>(firstRef.Init);
                    }
                });
            return result;
        }

        public bool IsInput
        {
            get { Force(); return _isInput; }
            set { AssertBuildable(); _isInput = value; }
        }

        public bool IsOutput
        {
            get { Force(); return _isOutput; }
            set { AssertBuildable(); _isOutput = value; }
        }

        public bool IsOptional
        {
            get { Force(); return _isOptional; }
            set { AssertBuildable(); _isOptional = value; }
        }

        private bool _isInput = false;
        private bool _isOutput = false;
        private bool _isOptional = false;
        private IResFreqQualType _type;
        private ILazy<IResExp> _initBuilder;
    }

    public class ResAttributeRef : ResMemberRef<ResAttributeDecl>, IResAttributeRef
    {
        public ResAttributeRef(
            SourceRange range,
            ResAttributeDecl decl,
            IResMemberTerm memberTerm,
            IResFreqQualType type,
            IResExp init )
            : base(range, decl, memberTerm)
        {
            _type = type;
            _init = init;
        }

        public ResAttributeRef(
            SourceRange range,
            ResAttributeDecl decl,
            IResMemberTerm memberTerm)
            : base(range, decl, memberTerm)
        {
        }

        public IResExp Init
        {
            get
            {
                if (_init == null)
                    _init = Decl.Init == null ? null : Decl.Init.Substitute(MemberTerm.Subst);
                return _init;
            }
        }

        public IResFreqQualType Type
        {
            get
            {
                if (_type == null)
                    _type = Decl.Type.Substitute<IResFreqQualType>(MemberTerm.Subst);
                return _type;
            }
        }
        IResTypeExp IResExp.Type
        {
            get { return this.Type; }
        }

        public override IResClassifier Classifier { get { return Type; } }

        public IResExp Substitute(Substitution subst)
        {
            var memberTerm = this.MemberTerm.Substitute(subst);

            return new ResAttributeRef(
                this.Range,
                (ResAttributeDecl) memberTerm.Decl,
                memberTerm,
                Type.Substitute<IResFreqQualType>(subst),
                Init == null ? null : Init.Substitute(subst));
        }

        public override IResMemberRef SubstituteMemberRef(Substitution subst)
        {
            return (IResMemberRef) this.Substitute(subst);
        }


        private IResFreqQualType _type;
        private IResExp _init;
    }
}
