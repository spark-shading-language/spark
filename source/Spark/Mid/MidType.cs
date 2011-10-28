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

namespace Spark.Mid
{
    public abstract class MidType
    {
    }

    public class MidVoidType : MidType
    {
    }

    public class MidTypeSlotDecl : MidMemberDecl
    {
        public MidTypeSlotDecl(
            IBuilder parent,
            Identifier name,
            IEnumerable<ResBuiltinTag> tags )
            : base(parent)
        {
            _name = name;
            _tags = tags.ToArray();
        }

        public override IMidMemberRef CreateRef(MidMemberTerm memberTerm)
        {
            return new MidBuiltinType(
                _name.ToString(),
                _tags);
        }

        private Identifier _name;
        private ResBuiltinTag[] _tags;
    }

    public class MidBuiltinTypeDecl : MidMemberDecl
    {
        public MidBuiltinTypeDecl(
            IBuilder parent,
            string name,
            IEnumerable<object> args,
            IEnumerable<ResBuiltinTag> tags )
            : base(parent)
        {
            _type = new MidBuiltinType(name, args, tags);
        }

        public override IMidMemberRef CreateRef(MidMemberTerm memberTerm)
        {
            return _type;
        }

        private MidBuiltinType _type;
    }

    public class MidBuiltinType : MidType, IMidMemberRef
    {
        public MidBuiltinType(
            string name)
        {
            _name = name;
            _args = null;
            _tags = new ResBuiltinTag[] { };
        }

        public MidBuiltinType(
            string name,
            IEnumerable<ResBuiltinTag> tags )
        {
            _name = name;
            _args = null;
            _tags = tags.ToArray();
        }

        public MidBuiltinType(
            string name,
            IEnumerable<object> args,
            IEnumerable<ResBuiltinTag> tags )
        {
            _name = name;
            _args = args == null ? null : args.ToArray();
            _tags = tags.ToArray();
        }

        public override string ToString()
        {
            return _name;
        }

        public string GetTemplate(string profile)
        {
            // \todo: Right now we are crashing here for IndexBuffer
            // because the builtin tags are associated with
            // the generic decl, and not with the type-slot decl.
            //
            // In order to resolve this, we'd either need to make
            // sure that the tags of the generic decl are reflected
            // on the inner decl (via a change to that schema), or
            // ensure that the Res->Mid lowering process explicitly
            // passes down any tags as needed...

            return (from tag in _tags
                    where tag.Profile == profile
                    select tag.Template).First();
        }

        public string Name { get { return _name; } }
        public object[] Args { get { return _args; } }

        private string _name;
        private object[] _args;
        private ResBuiltinTag[] _tags;

        public IMidMemberRef GenericApp(IEnumerable<object> args)
        {
            throw new NotImplementedException();
        }

        public MidExp App(IEnumerable<MidVal> args)
        {
            throw new NotImplementedException();
        }


        public MidMemberDecl LookupMemberDecl(IResMemberDecl resMemberDecl)
        {
            throw new NotImplementedException();
        }
    }

    public class MidElementType : MidType, IMidMemberRef
    {
        public MidElementType(
            MidElementDecl element)
        {
            _element = element;
        }

        public MidElementDecl Decl { get { return _element; } }

        private MidElementDecl _element;

        public IMidMemberRef GenericApp(IEnumerable<object> args)
        {
            throw new NotImplementedException();
        }

        public MidExp App(IEnumerable<MidVal> args)
        {
            throw new NotImplementedException();
        }

        public MidMemberDecl LookupMemberDecl(IResMemberDecl resMemberDecl)
        {
            throw new NotImplementedException();
        }
    }

    public class MidStructDecl : MidContainerDecl
    {
        public MidStructDecl(
            IBuilder parent,
            Identifier name,
            MidEmitContext context,
            MidEmitEnv env)
            : base(parent, context, env)
        {
            _name = name;
        }

        public Identifier Name { get { return _name; } }

        public void AddField(MidFieldDecl field)
        {
            _fields.Add(field);
        }

        public IEnumerable<MidFieldDecl> Fields
        {
            get { return _fields; }
        }

        public override IMidMemberRef CreateRef(MidMemberTerm memberTerm)
        {
            return new MidStructRef( this, memberTerm);
        }

        private Identifier _name;
        private List<MidFieldDecl> _fields = new List<MidFieldDecl>();
    }

    public class MidStructRef : MidType, IMidMemberRef
    {
        public MidStructRef(
            MidStructDecl decl,
            MidMemberTerm memberTerm)
        {
            _decl = decl;
            _memberTerm = memberTerm;
        }

        public MidStructDecl Decl
        {
            get { return _decl; }
        }

        public IEnumerable<MidFieldDecl> Fields
        {
            get { return _decl.Fields; }
        }

        private MidStructDecl _decl;
        private MidMemberTerm _memberTerm;

        public IMidMemberRef GenericApp(IEnumerable<object> args)
        {
            throw new NotImplementedException();
        }

        public MidExp App(IEnumerable<MidVal> args)
        {
            throw new NotImplementedException();
        }

        public MidMemberDecl LookupMemberDecl(IResMemberDecl resMemberDecl)
        {
            return _decl.LookupMemberDecl(resMemberDecl);
        }
    }
}
