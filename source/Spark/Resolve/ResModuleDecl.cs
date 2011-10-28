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
    public class ResModuleDecl : IResModuleDecl
    {
        private ILazy<IResGlobalDecl[]> _decls;

        public ResModuleDecl(
            ILazy<IResGlobalDecl[]> decls)
        {
            _decls = decls;
        }

        IEnumerable<IResGlobalDecl> IResModuleDecl.LookupDecls(Identifier name)
        {
            foreach (var decl in _decls.Value)
                if (decl.Name == name)
                    yield return decl;
        }

        IEnumerable<IResGlobalDecl> IResModuleDecl.Decls
        {
            get { return _decls.Value; }
        }
    }

    public class ResModuleDeclBuilder : NewBuilder<IResModuleDecl>
    {
        private ResModuleDeclBuilder(
            LazyFactory lazy )
            : base(lazy)
        {
        }

        public static ILazy<IResModuleDecl> Build(
            LazyFactory lazy,
            Action<ResModuleDeclBuilder> action )
        {
            var builder = new ResModuleDeclBuilder(lazy);
            var resModuleDecl = new ResModuleDecl(
                builder.NewLazy(() => builder._decls.ToArray()));
            builder.SetValue(resModuleDecl);
            builder.AddAction(() => { action(builder); });
            builder.DoneBuilding();
            return builder;
        }

        public void AddDecl(IResGlobalDecl decl)
        {
            AssertBuildable();
            _decls.Add(decl);
        }

        private List<IResGlobalDecl> _decls = new List<IResGlobalDecl>();
    }
}
