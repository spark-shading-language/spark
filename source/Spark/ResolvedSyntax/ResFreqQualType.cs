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
    public interface IResFreqQualType : IResTypeExp, ISubstitutable<IResFreqQualType>
    {
        IResElementRef Freq { get; }
        IResTypeExp Type { get; }
    }

    public class ResFreqQualType : IResFreqQualType
    {
        public ResFreqQualType(
            SourceRange range,
            IResElementRef freq,
            IResTypeExp type)
        {
            if (freq == null)
                throw new ArgumentNullException("freq");

            _range = range;
            _freq = freq;
            _type = type;
        }

        public override string ToString()
        {
            return string.Format("@{0} {1}",
                _freq,
                _type);
        }

        public SourceRange Range { get { return _range; } }
        public IResElementRef Freq { get { return _freq; } }
        public IResTypeExp Type { get { return _type; } }

        public ResKind Kind { get { return ResKind.FreqQualType; } }
        public IResClassifier Classifier { get { return Kind; } }

        IResTypeExp ISubstitutable<IResTypeExp>.Substitute(Substitution subst)
        {
            return this.Substitute<IResFreqQualType>(subst);
        }

        public IResFreqQualType Substitute(Substitution subst)
        {
            return new ResFreqQualType(
                _range,
                _freq.Substitute<IResElementRef>(subst),
                _type.Substitute(subst));
        }

        private SourceRange _range;
        private IResElementRef _freq;
        private IResTypeExp _type;
    }
}
