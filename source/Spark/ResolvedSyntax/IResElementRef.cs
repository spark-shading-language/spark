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
    public interface IResElementRef : IResMemberRef, IResTypeExp, ISubstitutable<IResElementRef>
    {
    }

    public struct ResElementCtorArg
    {
        public ResElementCtorArg(
            IResAttributeRef attribute,
            IResExp value)
        {
            this.Attribute = attribute;
            this.Value = value;
        }

        public ResElementCtorArg Substitute(Substitution subst)
        {
            return new ResElementCtorArg(
                (IResAttributeRef)Attribute.Substitute<IResMemberRef>(subst),
                Value.Substitute(subst));
        }

        public IResAttributeRef Attribute;
        public IResExp Value;
    }

    public interface IResElementCtorApp : IResExp
    {
        IResElementRef Element { get; }
        IEnumerable<ResElementCtorArg> Args { get; }
    }
}
