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
    public interface IResSimpleContainerDecl : IResMemberDecl
    {
        IResVarDecl ThisParameter { get; }
        IResMemberNameGroup LookupMemberNameGroup(Identifier name);
        IEnumerable<IResMemberNameGroup> MemberNameGroups { get; }
    }

    public interface IResStructDecl : IResSimpleContainerDecl
    {
    }

    public interface IResStructRef : IResMemberRef, IResTypeExp, IResContainerRef
    {
        IEnumerable<IResMemberLineSpec> MemberLines { get; }
    }

    public static class ResSimpleContainerDeclExtensions
    {
        public static IEnumerable<IResMemberLineDecl> GetMemberLines(
            this IResSimpleContainerDecl container)
        {
            foreach (var mng in container.MemberNameGroups)
                foreach (var mcg in mng.Categories)
                    foreach (var line in mcg.Lines)
                        yield return line;
        }

        public static IEnumerable<IResMemberDecl> GetMembers(
            this IResSimpleContainerDecl container)
        {
            foreach (var line in container.GetMemberLines())
                yield return line.EffectiveDecl;
        }

        public static IResMemberLineDecl FindMember(
            this IResSimpleContainerDecl container,
            IResMemberSpec memberSpec)
        {
            var mng = container.LookupMemberNameGroup(memberSpec.Name);
            if (mng == null)
                throw new KeyNotFoundException();

            foreach (var mcg in mng.Categories)
                foreach (var line in mcg.Lines)
                    if (line.OriginalLexicalID == memberSpec.Decl.Line.OriginalLexicalID)
                        return line;

            throw new KeyNotFoundException();
        }
    }
}
