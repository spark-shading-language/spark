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
    public enum ResMixinMode
    {
        Primary,
        Mixin
    }

    public interface IResPipelineDecl : IResGlobalDecl
    {
        IResVarDecl ThisParameter { get; }

        IEnumerable<IResMemberNameGroup> LookupMembers(Identifier name);
        IResMemberLineDecl FindMember(IResMemberSpec memberSpec);

        IResFacetDecl DirectFacet { get; }
        IEnumerable<IResFacetDecl> Facets { get; }
        IEnumerable<IResMemberDecl> Members { get; }

        IEnumerable<IResMemberDecl> ImplicitMembers { get; }

        ResMixinMode MixinMode { get; }
        ResMemberConcretenessMode ConcretenessMode { get; }
    }

    public interface IResMemberNameGroup
    {
        Identifier Name { get; }
        IEnumerable<IResMemberCategoryGroup> Categories { get; }

        IResMemberCategoryGroup FindCategoryGroup(ResMemberCategory category);
    }

    public interface IResMemberNameGroupSpec
    {
        IEnumerable<IResMemberCategoryGroupSpec> Categories { get; }
    }

    public enum ResMemberFlavor
    {
        Pipeline,
        TypeSlot,
        Field,
        Attribute,
        Method,
        Struct,
        Element,
        ConceptClass,
    }

    public interface IResMemberCategoryGroup
    {
        Identifier Name { get; }
        ResMemberFlavor Flavor { get; }
        IEnumerable<IResMemberLineDecl> Lines { get; }
    }

    public interface IResMemberCategoryGroupSpec
    {
        IResMemberCategoryGroupRef Bind(SourceRange range, IResExp obj);
    }

    public interface IResMemberCategoryGroupRef : IResTerm
    {
        IEnumerable<IResMemberRef> Members { get; }
    }

    public interface IResMemberLineDecl
    {
        Identifier Name { get; }
        ResLexicalID OriginalLexicalID { get; }
        ResMemberCategory Category { get; }
        IResMemberDecl EffectiveDecl { get; }
        IEnumerable<IResMemberDecl> InheritedDecls { get; }

        ResMemberConcretenessMode ConcretenessMode { get; }
        ResMemberDeclMode MemberDeclMode { get; }
        IEnumerable<ResTag> Tags { get; }
    }

    public abstract class ResMemberCategory
    {
        public abstract ResMemberFlavor Flavor { get; }
    }

    public interface IResMemberLineSpec
    {
        Identifier Name { get; }
        ResLexicalID OriginalLexicalID { get; }
        ResMemberCategory Category { get; }
        IResMemberSpec EffectiveSpec { get; }
    }

    public interface IResFacetDecl
    {
        IResPipelineRef OriginalPipeline { get; }
        IEnumerable<IResFacetDecl> DirectBases { get; }

        IResMemberNameGroup LookupDirectMembers(Identifier name);
        IEnumerable<IResMemberLineDecl> MemberLines { get; }

        IResMemberLineDecl FindMember(IResMemberSpec memberSpec);
    }
}
