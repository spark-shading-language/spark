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
    public interface IResGenericParamDecl
    {
        SourceRange Range { get; }
        Identifier Name { get; }
        IResGenericParamRef MakeRef(SourceRange range, IResMemberTerm memberTerm);
    }

    public interface IResTypeParamDecl : IResGenericParamDecl
    {
        ResKind Kind { get; }
        int ID { get; }
    }

    public interface IResValueParamDecl : IResGenericParamDecl
    {
        IResTypeExp Type { get; }
    }

    public interface IResConceptParamDecl : IResGenericParamDecl
    {
    }

    public static class ResGenericArgMethods
    {
        public static IResGenericArg MakeGenericArg(
            this IResGenericParamDecl decl)
        {
            if (decl is IResTypeParamDecl)
                return MakeGenericArg(new ResTypeVarRef(decl.Range, (IResTypeParamDecl)decl));
            else
                return MakeGenericArg(new ResVarRef(decl.Range, (IResVarDecl)decl));
        }

        public static IResGenericArg MakeGenericArg(
            this IResTerm term)
        {
            if (term is IResTypeExp)
                return new ResGenericTypeArg((IResTypeExp) term);
            else
                return new ResGenericValueArg((IResExp) term);
        }
    }
}
