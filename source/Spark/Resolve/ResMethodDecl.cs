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
    public class ResMethodDecl : ResMemberDecl, IResMethodDecl
    {
        public ResMethodDecl(
            IResMemberLineDecl line,
            IBuilder parent,
            SourceRange range,
            Identifier name)
            : base(line, parent, range, name)
        {
        }

        public override ResMemberDecl CreateInheritedDeclImpl(
                    ResolveContext resContext,
                    IResContainerBuilderRef resContainer,
                    IResMemberLineDecl resLine,
                    IBuilder parent,
                    SourceRange range,
                    IResMemberRef memberRef)
        {
            var firstRef = (ResMethodRef)memberRef;
            var firstDecl = firstRef.Decl;

            var result = new ResMethodDecl(
                resLine,
                parent,
                range,
                firstDecl.Name);
            // \todo: Substitution!!!

            result.AddBuildAction(() =>
            {
                // \todo: Add back in inheritance-related validation checks.
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
                        oldParam.Decl.Flags );
                    subst.Insert(oldParam.Decl, newParam);
                    newParams.Add(newParam);
                }

                result.Parameters = newParams;
                result.ResultType = firstRef.ResultType;
                if (firstRef.Body != null)
                    result.Body = firstRef.Body.Substitute(subst);
            });
            return result;
        }

        public override IResMemberRef MakeRef(SourceRange range, IResMemberTerm memberTerm)
        {
            return new ResMethodRef(range, this, memberTerm);
        }

        public IEnumerable<IResVarDecl> Parameters
        {
            get { Force(); return _parameters; }
            set { AssertBuildable(); _parameters = value.ToArray(); }
        }
        public IResTypeExp ResultType
        {
            get { Force(); return _resultType; }
            set { AssertBuildable(); _resultType = value; }
        }
        public IResExp Body
        {
            get { Force(); return _body; }
            set { _body = value; }
        }

        public ResMethodFlavor Flavor
        {
            get { Force(); return _flavor; }
            set { AssertBuildable();  _flavor = value; }
        }

        private IResVarDecl[] _parameters;
        private IResTypeExp _resultType;
        private IResExp _body;
        private ResMethodFlavor _flavor = ResMethodFlavor.Ordinary;
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
