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
    public class ResTag
    {
    }

    public class ResBuiltinTag : ResTag
    {
        public ResBuiltinTag(
            string profile,
            string template)
        {
            _profile = profile;
            _template = template;
        }

        public string Profile { get { return _profile; } }
        public string Template { get { return _template; } }

        private string _profile;
        private string _template;
    }

    public class ResImplicitTag : ResTag
    {
    }

    public class ResConcreteTag : ResTag
    {
    }

    public static class ResTagExtensions
    {
        public static bool IsImplicit(
            this IResMemberDecl decl)
        {
            return decl.Line.Tags.Any(
                (tag) => tag is ResImplicitTag);
        }

        public static bool IsConcrete(
            this IResMemberDecl decl)
        {
            return decl.Line.Tags.Any(
                (tag) => tag is ResConcreteTag);
        }
    }
}
