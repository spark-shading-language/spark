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
    public interface IResContainerFacetBuilder
    {
        ResMemberNameGroupBuilder GetMemberNameGroup(Identifier name);
        IEnumerable<ResMemberNameGroupBuilder> MemberNameGroups { get; }
    }

    public interface IResContainerBuilder
    {
        IResContainerFacetBuilder DirectFacetBuilder { get; }
        IEnumerable<IResContainerFacetBuilder> InheritedFacets { get; }
    }

    public interface IResContainerBuilderRef
        // : IResMemberRef
    {
        IResContainerBuilder ContainerDecl { get; }
        IResMemberRef CreateMemberRef(SourceRange range, IResMemberDecl memberDecl);
    }

    public static class ResContainerFacetBuilderExtensions
    {
        public static IEnumerable<ResMemberLineDeclBuilder> GetMemberLines(
            this IResContainerFacetBuilder facetBuilder)
        {
            foreach (var mng in facetBuilder.MemberNameGroups)
                foreach (var mcg in mng.Categories)
                    foreach (var line in mcg.Lines)
                        yield return line;
        }
    }
}
