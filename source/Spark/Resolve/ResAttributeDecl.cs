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
    public class ResAttributeDeclBuilder : NewBuilder<IResAttributeDecl>
    {
        public ResAttributeDeclBuilder(
            ILazyFactory lazyFactory,
            ILazy<IResMemberLineDecl> line,
            SourceRange range,
            Identifier name )
            : base(lazyFactory)
        {
            var resAttributeDecl = new ResAttributeDecl(
                line,
                range,
                name,
                NewLazy(() => _type),
                NewLazy(() => _lazyInit == null ? null : _lazyInit.Value),
                NewLazy(() => _flags));
            SetValue(resAttributeDecl);
        }

        public IResFreqQualType Type
        {
            get { return _type; }
            set { AssertBuildable(); _type = value; }
        }

        public ILazy<IResExp> LazyInit
        {
            get { return _lazyInit; }
            set { AssertBuildable(); _lazyInit = value; }
        }

        public ResAttributeFlags Flags
        {
            get { return _flags; }
            set { AssertBuildable(); _flags = value; }
        }

        private IResFreqQualType _type;
        private ILazy<IResExp> _lazyInit;
        private ResAttributeFlags _flags;
    }

    public class ResAttributeDecl : ResMemberDecl, IResAttributeDecl
    {
        private ILazy<IResFreqQualType> _type;
        private ILazy<IResExp> _init;
        private ILazy<ResAttributeFlags> _flags;

        public ResAttributeDecl(
            ILazy<IResMemberLineDecl> line,
            SourceRange range,
            Identifier name,
            ILazy<IResFreqQualType> type,
            ILazy<IResExp> init,
            ILazy<ResAttributeFlags> flags )
            : base(line, range, name)
        {
            _type = type;
            _init = init;
            _flags = flags;
        }

        public static IResAttributeDecl Build(
            ILazyFactory lazyFactory,
            ILazy<IResMemberLineDecl> line,
            SourceRange range,
            Identifier name,
            Action<ResAttributeDeclBuilder> action)
        {
            var builder = new ResAttributeDeclBuilder(
                lazyFactory,
                line,
                range,
                name);
            builder.AddAction(() => action(builder));
            builder.DoneBuilding();
            return builder.Value;
        }

        // IResAttributeDecl

        public IResFreqQualType Type
        {
            get { return _type.Value; }
        }

        public IResExp Init
        {
            get { return _init == null ? null : _init.Value; }
        }

        public ResAttributeFlags Flags
        {
            get { return _flags.Value; }
        }

        // ResMemberDecl

        public override IResMemberRef MakeRef(SourceRange range, IResMemberTerm memberTerm)
        {
            return new ResAttributeRef(
                range,
                this,
                memberTerm);
        }

        public override IResMemberDecl CreateInheritedDeclImpl(
                    ResolveContext resContext,
                    IResContainerBuilderRef resContainer,
                    ILazy<IResMemberLineDecl> resLine,
                    SourceRange range,
                    IResMemberRef originalRef)
        {
            var firstRef = (ResAttributeRef)originalRef;
            var firstDecl = firstRef.Decl;

            var result = ResAttributeDecl.Build(
                resContext.LazyFactory,
                resLine,
                range,
                firstDecl.Name,
                (builder) =>
                {
                    builder.Flags = firstDecl.Flags;
                    builder.Type = firstRef.Type;
                    builder.LazyInit = 
                        builder.LazyFactory.New(() => firstRef.Init);
                });

            return result;
        }
    }

    public class ResAttributeRef : ResMemberRef<ResAttributeDecl>, IResAttributeRef
    {
        public ResAttributeRef(
            SourceRange range,
            ResAttributeDecl decl,
            IResMemberTerm memberTerm,
            ILazy<IResFreqQualType> lazyType,
            ILazy<IResExp> lazyInit )
            : base(range, decl, memberTerm)
        {
            _lazyType = lazyType;
            _lazyInit = lazyInit;
        }

        public ResAttributeRef(
            SourceRange range,
            ResAttributeDecl decl,
            IResMemberTerm memberTerm)
            : base(range, decl, memberTerm)
        {
            _lazyType = Lazy.New(new LazyFactory(), () => Decl.Type.Substitute<IResFreqQualType>(MemberTerm.Subst));
            _lazyInit = Lazy.New(new LazyFactory(), () => Decl.Init == null ? null : Decl.Init.Substitute(MemberTerm.Subst));
        }

        public IResExp Init
        {
            get { return _lazyInit == null ? null : _lazyInit.Value; }
        }

        public IResFreqQualType Type
        {
            get { return _lazyType.Value; }
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
                Lazy.New(new LazyFactory(), () => Type.Substitute<IResFreqQualType>(subst)),
                Lazy.New(new LazyFactory(), () => Init == null ? null : Init.Substitute(subst)));
        }

        public override IResMemberRef SubstituteMemberRef(Substitution subst)
        {
            return (IResMemberRef) this.Substitute(subst);
        }


        private ILazy<IResFreqQualType> _lazyType;
        private ILazy<IResExp> _lazyInit;
    }
}
