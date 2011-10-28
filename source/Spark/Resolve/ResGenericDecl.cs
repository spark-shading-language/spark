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
    public class ResGenericDecl : ResMemberDecl, IResGenericDecl, IResContainerBuilder, IResContainerFacetBuilder
    {
        public ResGenericDecl(
            IResMemberLineDecl line,
            IBuilder parent,
            SourceRange range,
            Identifier name)
            : base(line, parent, range, name)
        {
        }

        public IEnumerable<IResGenericParamDecl> Parameters
        {
            get { Force(); return _parameters; }
            set { AssertBuildable(); _parameters = value.ToArray(); }
        }

        public IResMemberDecl InnerDecl
        {
            get { return _innerDecl; }
            set { AssertBuildable(); _innerDecl = value; }
        }

        public override ResMemberDecl CreateInheritedDeclImpl(
                    ResolveContext resContext,
                    IResContainerBuilderRef resContainer,
                    IResMemberLineDecl resLine,
                    IBuilder parent,
                    SourceRange range,
                    IResMemberRef memberRef)
        {
            var firstRef = (IResGenericRef)memberRef;

            var result = new ResGenericDecl(
                resLine,
                parent,
                range,
                firstRef.Decl.Name);

            result.AddBuildAction(() =>
            {
                var newParameters = new List<IResGenericParamDecl>();
                var subst = new Substitution();
                foreach (var p in firstRef.Parameters)
                {
                    if (p is IResTypeParamRef)
                    {
                        var oldParameter = (IResTypeParamRef)p;
                        var newParameter = new ResTypeParamDecl(
                            oldParameter.Decl.Range,
                            oldParameter.Name,
                            oldParameter.Kind);
                        newParameters.Add(newParameter);
                        subst.Insert(oldParameter.Decl, (r) => new ResTypeVarRef(r, newParameter));
                    }
                    else if (p is IResVarSpec)
                    {
                        var oldParameter = (IResVarSpec)p;
                        var newParameter = new ResVarDecl(
                            oldParameter.Decl.Range,
                            oldParameter.Name,
                            Lazy.New(() => oldParameter.Type.Substitute(subst)),
                            oldParameter.Decl.Flags);
                        newParameters.Add(newParameter);
                        subst.Insert(oldParameter.Decl, (r) => new ResVarRef(r, newParameter));
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }

                }
                result.Parameters = newParameters;

                var args = (from p in newParameters
                            select p.MakeGenericArg()).ToArray();

                var innerRef = firstRef.App(range, args);


                /*
                var innerCategoryGroup = new ResMemberCategoryGroup(
                    result,
                    new ResMemberNameGroup(result, result.Name),
                    new ResMethodCategory());
                var innerLine = new ResMemberDeclLine(
                    innerCategoryGroup,
                    new ResLexicalID());
                */

                var resultRef = (IResContainerBuilderRef)resContainer.CreateMemberRef(
                    range,
                    result);

                var innerDecl = CreateInheritedDecl(
                    resContext,
                    resultRef,
                    resLine,
                    result,
                    range,
                    innerRef);

                innerDecl.DoneBuilding();
                result.InnerDecl = innerDecl;
            });

            return result;
        }

        public override IResMemberRef MakeRef(SourceRange range, IResMemberTerm memberTerm)
        {
            return new ResGenericRef(range, this, memberTerm);
        }

        private IResGenericParamDecl[] _parameters;
        private IResMemberDecl _innerDecl;

        public IResContainerFacetBuilder DirectFacetBuilder
        {
            get { return this; }
        }
        public IEnumerable<IResContainerFacetBuilder> InheritedFacets
        {
            get { return new IResContainerFacetBuilder[] { }; }
        }

        /*
        void IResContainerBuilder.AddDirectMemberLine(ResMemberDeclLine memberLine)
        {
            throw new NotImplementedException();
        }
        */
        ResMemberNameGroup IResContainerFacetBuilder.GetMemberNameGroup(Identifier name)
        {
            throw new NotImplementedException();
        }
    }

    public class ResGenericRef : ResMemberRef<ResGenericDecl>, IResGenericRef, IResContainerBuilderRef
    {
        public ResGenericRef(
            SourceRange range,
            ResGenericDecl decl,
            IResMemberTerm memberTerm)
            : base(range, decl, memberTerm)
        {
        }

        public IEnumerable<IResGenericParamRef> Parameters
        {
            get
            {
                foreach (var param in Decl.Parameters)
                    yield return param.MakeRef(this.Range, this.MemberTerm);
            }
        }

        public IResMemberDecl InnerDecl { get { return Decl.InnerDecl; } }
        public override IResClassifier Classifier { get { throw new NotImplementedException(); } }

        public IResGenericRef Substitute(Substitution subst)
        {
            var memberTerm = this.MemberTerm.Substitute(subst);

            return new ResGenericRef(
                this.Range,
                (ResGenericDecl) memberTerm.Decl,
                memberTerm );
        }

        public override IResMemberRef SubstituteMemberRef(Substitution subst)
        {
            return this.Substitute(subst);
        }

        IResContainerBuilder IResContainerBuilderRef.ContainerDecl
        {
            get { return Decl; }
        }

        IResMemberRef IResContainerBuilderRef.CreateMemberRef(SourceRange range, IResMemberDecl memberDecl)
        {
            return memberDecl.MakeRef(
                range,
                new ResMemberGenericApp(
                    this,
                    (from p in this.Parameters
                    select p.MakeArg(range)).ToArray()));
        }
    }

    public class ResTypeParamRef : IResTypeParamRef
    {
        public ResTypeParamRef(
            SourceRange range,
            IResTypeParamDecl decl)
        {
            _range = range;
            _decl = decl;
        }

        public SourceRange Range { get { return _range; } }

        public Identifier Name { get { return _decl.Name; } }
        public ResKind Kind { get { return _decl.Kind; } }
        public IResClassifier Classifier { get { return Kind; } }

        IResGenericParamDecl IResGenericParamRef.Decl { get { return _decl; } }
        public IResTypeParamDecl Decl { get { return _decl; } }

        IResGenericArg IResGenericParamRef.MakeArg(SourceRange range)
        {
            return new ResGenericTypeArg(
                new ResTypeVarRef(range, Decl));
        }

        private SourceRange _range;
        private IResTypeParamDecl _decl;
    }

    public class ResDummyTypeArg : IResTypeExp
    {
        public ResDummyTypeArg(
            IResTypeParamRef param)
        {
            _param = param;
        }

        public override string ToString()
        {
            if (ConcreteType != null)
            {
                return ConcreteType.ToString();
            }

            return string.Format(
                "{0}?{1}",
                _param.Decl.Name,
                _id);
        }

        public ResKind Kind
        {
            get { throw new NotImplementedException(); }
        }

        public SourceRange Range
        {
            get { return new SourceRange(); }
        }

        public IResClassifier Classifier
        {
            get { throw new NotImplementedException(); }
        }

        public IResTypeExp Substitute(Substitution subst)
        {
            if (ConcreteType != null)
            {
                return ConcreteType.Substitute(subst);
            }

            // \todo: Figure out what this means... :(
            return this;
            /*

            var result = new ResDummyTypeArg(
                _param); // \todo: Substitute this!


            result.LowerBounds.AddRange(
                from t in LowerBounds
                select t.Substitute(subst));

            result.UpperBounds.AddRange(
                from t in UpperBounds
                select t.Substitute(subst));

            return result;
             * */
        }

        private static int _counter = 0;
        private int _id = _counter++;
        private IResTypeParamRef _param;
        public List<IResTypeExp> LowerBounds = new List<IResTypeExp>();
        public List<IResTypeExp> UpperBounds = new List<IResTypeExp>();
        public IResTypeExp ConcreteType = null;
    }

    public class ResDummyValArg : IResExp
    {
        public ResDummyValArg(
            IResVarSpec param,
            Substitution subst )
        {
            _param = param;
            _subst = subst;
        }

        public IResVarSpec Param
        {
            get { SubstIfNeeded(); return _param; }
        }

        public IResTypeExp Type
        {
            get
            {
                if (ConcreteVal != null)
                {
                    return ConcreteVal.Type;
                }

                SubstIfNeeded();
                return _param.Type;
            }
        }

        public SourceRange Range
        {
            get { throw new NotImplementedException(); }
        }

        public IResClassifier Classifier
        {
            get { return Type; }
        }

        public IResExp Substitute(Substitution subst)
        {
            if( ConcreteVal != null )
            {
                return ConcreteVal.Substitute(subst);
            }

            SubstIfNeeded();
            var result = new ResDummyValArg(
                _param.Substitute(subst),
                null);

            foreach( var c in Constraints )
            {
                result.Constraints.Add(
                    c.Substitute(subst));
            }

            return result;
        }

        private void SubstIfNeeded()
        {
            if (_subst != null)
            {
                _param = _param.Substitute(_subst);
                _subst = null;
            }
        }

        private IResVarSpec _param;
        private Substitution _subst;

        public List<IResExp> Constraints = new List<IResExp>();
        public IResExp ConcreteVal = null;
    }
}
