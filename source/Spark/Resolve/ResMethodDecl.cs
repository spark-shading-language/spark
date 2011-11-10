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
    public class ResMethodDeclBuilder : NewBuilder<IResMethodDecl>
    {
        private IResVarDecl[] _parameters;
        private IResTypeExp _resultType;
        private ILazy<IResExp> _lazyBody;
        private ResMethodFlavor _flavor;

        public ResMethodDeclBuilder(
            ILazyFactory lazyFactory,
            ILazy<IResMemberLineDecl> line,
            SourceRange range,
            Identifier name )
            : base(lazyFactory)
        {
            var resMethodDecl = new ResMethodDecl(
                line,
                range,
                name,
                NewLazy(() => _parameters),
                NewLazy(() => _resultType),
                NewLazy(() => _lazyBody == null ? null : _lazyBody.Value),
                NewLazy(() => _flavor));
            SetValue(resMethodDecl);
        }


        public IEnumerable<IResVarDecl> Parameters
        {
            get { return _parameters; }
            set { AssertBuildable(); _parameters = value.ToArray(); }
        }
        public IResTypeExp ResultType
        {
            get { return _resultType; }
            set { AssertBuildable(); _resultType = value; }
        }
        public ILazy<IResExp> LazyBody
        {
            get { return _lazyBody; }
            set { AssertBuildable(); _lazyBody = value; }
        }

        public ResMethodFlavor Flavor
        {
            get { return _flavor; }
            set { AssertBuildable(); _flavor = value; }
        }
    }

    public class ResMethodDecl : ResMemberDecl, IResMethodDecl
    {
        private ILazy<IEnumerable<IResVarDecl>> _parameters;
        private ILazy<IResTypeExp> _resultType;
        private ILazy<IResExp> _body;
        private ILazy<ResMethodFlavor> _flavor;

        public ResMethodDecl(
            ILazy<IResMemberLineDecl> line,
            SourceRange range,
            Identifier name,
            ILazy<IEnumerable<IResVarDecl>> parameters,
            ILazy<IResTypeExp> resultType,
            ILazy<IResExp> body,
            ILazy<ResMethodFlavor> flavor )
            : base(line, range, name)
        {
            _parameters = parameters;
            _resultType = resultType;
            _body = body;
            _flavor = flavor;
        }

        public static IResMethodDecl Build(
            ILazyFactory lazyFactory,
            ILazy<IResMemberLineDecl> line,
            SourceRange range,
            Identifier name,
            Action<ResMethodDeclBuilder> action)
        {
            var builder = new ResMethodDeclBuilder(
                lazyFactory,
                line,
                range,
                name);
            builder.AddAction(() => action(builder));
            builder.DoneBuilding();
            return builder.Value;
        }

        // ResMemberDecl

        public override IResMemberDecl CreateInheritedDeclImpl(
                    ResolveContext resContext,
                    IResContainerBuilderRef resContainer,
                    ILazy<IResMemberLineDecl> resLine,
                    SourceRange range,
                    IResMemberRef memberRef)
        {
            var firstRef = (ResMethodRef)memberRef;
            var firstDecl = firstRef.Decl;

            var result = ResMethodDecl.Build(
                resContext.LazyFactory,
                resLine,
                range,
                firstDecl.Name,
                (builder) =>
                {
                    // \todo: More substitution needed?
                    // \todo: Add back in inheritance-related validation checks?
                    /*
                    if (firstRef.Body == null
                        && memberRefs.OfType<IResMethodRef>().Any((mr) => (mr.Body != null)))
                    {
                        throw new NotImplementedException();
                    }
                    */

                    var subst = new Substitution();
                    var newParams = new List<ResVarDecl>();
                    foreach (var oldParam in firstRef.Parameters)
                    {
                        var newParam = new ResVarDecl(
                            range,
                            oldParam.Name,
                            oldParam.Type,
                            oldParam.Decl.Flags);
                        subst.Insert(oldParam.Decl, newParam);
                        newParams.Add(newParam);
                    }

                    builder.Parameters = newParams;
                    builder.ResultType = firstRef.ResultType;
                    if (firstRef.Body != null)
                        builder.LazyBody = Lazy.Value(firstRef.Body.Substitute(subst));
                });

            return result;
        }

        public override IResMemberRef MakeRef(SourceRange range, IResMemberTerm memberTerm)
        {
            return new ResMethodRef(range, this, memberTerm);
        }

        // IResMethodDecl

        public IEnumerable<IResVarDecl> Parameters
        {
            get { return _parameters.Value; }
        }
        public IResTypeExp ResultType
        {
            get { return _resultType.Value; }
        }
        public IResExp Body
        {
            get { return _body.Value; }
        }

        public ResMethodFlavor Flavor
        {
            get { return _flavor.Value; }
        }
    }

    public class ResMethodRef : ResMemberRef<ResMethodDecl>, IResMethodRef
    {
        public ResMethodRef(
            SourceRange range,
            ResMethodDecl decl,
            IResMemberTerm memberTerm )
            : base(range, decl, memberTerm)
        {
        }

        public IResMethodRef Substitute(Substitution subst)
        {
            var memberTerm = MemberTerm.Substitute(subst);
            return new ResMethodRef(
                Range,
                (ResMethodDecl)memberTerm.Decl,
                memberTerm);
        }

        public override IResMemberRef SubstituteMemberRef(Substitution subst)
        {
            return this.Substitute(subst);
        }

        public override IResClassifier Classifier
        {
            get { return new ResMethodClassifier(); }
        }

        public IEnumerable<IResVarSpec> Parameters
        {
            get
            {
                foreach (var param in Decl.Parameters)
                    yield return new ResVarSpec(param, MemberTerm);
            }
        }

        public IResTypeExp ResultType { get { return Decl.ResultType.Substitute(MemberTerm.Subst); } }
        public IResExp Body
        {
            get
            {
                if (Decl.Body == null) return null;
                return Decl.Body.Substitute(MemberTerm.Subst);
            }
        }
    }

    public class ResMethodClassifier : IResClassifier
    {
        public override string ToString()
        {
            return "method";
        }
    }
}
