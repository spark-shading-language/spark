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
    [Flags]
    public enum ResAttributeFlags
    {
        None = 0x0,
        Input = 0x1,
        Output = 0x2,
        Optional = 0x4,
    }


    public interface IResAttributeDecl : IResMemberDecl
    {
        IResFreqQualType Type { get; }
        IResExp Init { get; }
        ResAttributeFlags Flags { get; }
    }

    public interface IResAttributeRef : IResMemberRef, IResExp
    {
        IResExp Init { get; }
    }

    public static class ResAttributeExtensions
    {
        public static bool IsInput(this IResAttributeDecl attr)
        {
            return attr.Flags.HasFlag(ResAttributeFlags.Input);
        }

        public static bool IsOutput(this IResAttributeDecl attr)
        {
            return attr.Flags.HasFlag(ResAttributeFlags.Output);
        }

        public static bool IsOptional(this IResAttributeDecl attr)
        {
            return attr.Flags.HasFlag(ResAttributeFlags.Optional);
        }
    }
}
