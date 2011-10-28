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
    public abstract class ResKind : IResClassifier, ISubstitutable<ResKind>
    {
        public static readonly ResKind Star = new ResIntervalKind();
        public static readonly ResKind PipelineClass = new ResPipelineClassKind();
        public static readonly ResKind FreqQualType = new ResFreqQualTypeKind();
        public static readonly ResKind ConceptClass = new ResConceptClassKind();

        public abstract ResKind Substitute(Substitution subst);
    }

    public class ResArrowKind : ResKind
    {
        public ResArrowKind(
            IEnumerable<IResTypeParamDecl> parameters,
            ResKind resultKind)
        {
            _parameters = parameters.ToArray();
            _resultKind = resultKind;
        }

        public override ResKind Substitute(Substitution subst)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<IResTypeParamDecl> Parameters { get { return _parameters; } }
        public ResKind ResultKind { get { return _resultKind; } }

        private IResTypeParamDecl[] _parameters;
        private ResKind _resultKind;
    }

    public class ResIntervalKind : ResKind
    {
        public ResIntervalKind(
            IResTypeExp lowerBound,
            IResTypeExp upperBound)
        {
            _lowerBound = lowerBound;
            _upperBound = upperBound;
        }

        public ResIntervalKind(
            IResTypeExp upperBound)
            : this(new ResBottomType(), upperBound)
        {
        }

        public ResIntervalKind()
            : this(new ResBottomType(), new ResTopType())
        {
        }

        public override ResKind Substitute(Substitution subst)
        {
            if (this == ResKind.Star) return this;

            return new ResIntervalKind(
                _lowerBound.Substitute(subst),
                _upperBound.Substitute(subst));
        }

        public IResTypeExp LowerBound { get { return _lowerBound; } }
        public IResTypeExp UpperBound { get { return _upperBound; } }
/*
        public override Kind Substitute(ISubstitution subst)
        {
            return new IntervalKind(
                LowerBound.Substitute(subst),
                UpperBound.Substitute(subst));
        }
*/
        public override string ToString()
        {
            if (LowerBound is ResBottomType
                && UpperBound is ResTopType)
            {
                return "type";
            }

            if (LowerBound is ResBottomType)
            {
                return string.Format(
                    "type <: {0}",
                    UpperBound);
            }

            return string.Format(
                "type :> {0} <: {1}",
                LowerBound,
                UpperBound);
        }

        private IResTypeExp _lowerBound;
        private IResTypeExp _upperBound;
    }

    public class ResSimpleKind : ResKind
    {
        public override ResKind Substitute(Substitution subst)
        {
            return this;
        }
    }

    public class ResPipelineClassKind : ResSimpleKind
    {
        public override string ToString()
        {
            return "pipeline class";
        }
    }

    public class ResConceptClassKind : ResSimpleKind
    {
        public override string ToString()
        {
            return "concept class";
        }
    }

    public class ResFreqQualTypeKind : ResSimpleKind
    {
        public override string ToString()
        {
            return "frequency-qualified type";
        }
    }
}
