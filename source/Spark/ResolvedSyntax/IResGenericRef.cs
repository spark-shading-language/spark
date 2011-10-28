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
    public interface IResGenericRef : IResMemberRef, ISubstitutable<IResGenericRef>
    {
        IEnumerable<IResGenericParamRef> Parameters { get; }
        IResMemberDecl InnerDecl { get; }
    }

    public static class ResGenericRefMethods
    {
        public static IResMemberRef App(
            this IResGenericRef fun,
            SourceRange range,
            IEnumerable<IResGenericArg> args)
        {
            var genericApp = new ResMemberGenericApp(
                            fun,
                            args);

            return fun.InnerDecl.MakeRef(range, genericApp);
        }
    }

    public interface IResParamSpec
    {
        Identifier Name { get; }
        IResClassifier Classifier { get; }
    }

    public interface IResGenericParamRef : IResParamSpec
    {
        IResGenericParamDecl Decl { get; }

        IResGenericArg MakeArg(SourceRange range);
    }

    public interface IResTypeParamRef : IResGenericParamRef
    {
        ResKind Kind { get; }
        new IResTypeParamDecl Decl { get; }
    }

    public interface IResValueParamSpec : IResParamSpec
    {
        IResTypeExp Type { get; }
    }
}
