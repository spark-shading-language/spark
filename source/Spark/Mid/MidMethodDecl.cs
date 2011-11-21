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
    public class MidMethodDecl : MidMemberDecl, IMidMethodDecl
    {
        public MidMethodDecl(
            IBuilder parent,
            Identifier name,
            MidExpFactory exps )
            : base(parent)
        {
            _name = name;
            _exps = exps;
        }

        public Identifier Name { get { return _name; } }
        public MidExp Body
        {
            get { Force();  return _body; }
            set
            {
                if( _body == null && value != null )
                    AssertBuildable();
                _body = value;
            }
        }
        public MidType ResultType
        {
            get { Force();  return _resultType; }
            set { AssertBuildable(); _resultType = value; }
        }
        public IEnumerable<MidVar> Parameters
        {
            get { Force();  return _parameters; }
            set { AssertBuildable(); _parameters = value.ToArray(); }
        }

        public override IMidMemberRef CreateRef(MidMemberTerm memberTerm)
        {
            return new MidMethodRef( this, memberTerm, _exps );
        }

        private Identifier _name;
        private MidExp _body;
        private MidType _resultType;
        private MidVar[] _parameters;
        private MidExpFactory _exps;
    }

    public class MidMethodRef : MidMemberRef
    {
        public MidMethodRef(
            MidMethodDecl decl,
            MidMemberTerm memberTerm,
            MidExpFactory exps )
        {
            _decl = decl;
            _memberTerm = memberTerm;
            _exps = exps;
        }

        public override MidExp App(
            SourceRange range,
            IEnumerable<MidVal> args)
        {
            return _exps.MethodApp( range, _decl, args );
        }

        private MidMethodDecl _decl;
        private MidMemberTerm _memberTerm;
        private MidExpFactory _exps;
    }

    public interface IMidMethodDecl : IBuilder
    {
        MidType ResultType { get; set; }
        IEnumerable<MidVar> Parameters { get; set; }
    }

    public class MidBuiltinMethodDecl : MidMemberDecl, IMidMethodDecl
    {
        public MidBuiltinMethodDecl(
            IBuilder parent,
            Identifier name,
            IEnumerable<ResBuiltinTag> tags )
            : base(parent)
        {
            _name = name;
            _tags = tags.ToArray();
        }

        public Identifier Name { get { return _name; } }
        public MidType ResultType
        {
            get { Force();  return _resultType; }
            set { AssertBuildable(); _resultType = value; }
        }

        public IEnumerable<MidVar> Parameters
        {
            get { throw new NotImplementedException(); }
            set { }
        }

        public override IMidMemberRef CreateRef(MidMemberTerm memberTerm)
        {
            return new MidBuiltinMethodRef(this, memberTerm);
        }

        public string GetTemplate(string profile)
        {
            return (from tag in _tags
                    where tag.Profile == profile
                    select tag.Template).FirstOrDefault();
        }

        private Identifier _name;
        private MidType _resultType;
        private ResBuiltinTag[] _tags;
    }

    public class MidBuiltinMethodRef : MidMemberRef
    {
        public MidBuiltinMethodRef(
            MidBuiltinMethodDecl decl,
            MidMemberTerm memberTerm)
        {
            _decl = decl;
            _memberTerm = memberTerm;
        }

        public override MidExp App(
            SourceRange range,
            IEnumerable<MidVal> args)
        {
            return new MidBuiltinApp(range, _decl, args);
        }

        private MidBuiltinMethodDecl _decl;
        private MidMemberTerm _memberTerm;
    }

    public class MidBuiltinApp : MidExp
    {
        public MidBuiltinApp(
            SourceRange range,
            MidBuiltinMethodDecl decl,
            IEnumerable<MidVal> args)
            : base(range, decl.ResultType)
        {
            _decl = decl;
            _args = args.ToArray();
        }

        public MidBuiltinMethodDecl Decl { get { return _decl; } }
        public IEnumerable<MidVal> Args
        {
            get { return _args; }
            set { _args = value.ToArray(); }
        } 

        private MidBuiltinMethodDecl _decl;
        private MidVal[] _args;
    }
}
