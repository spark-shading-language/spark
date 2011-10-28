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
    public interface IResGenericArg : ISubstitutable<IResGenericArg>
    {
    }

    public class ResGenericTypeArg : IResGenericArg
    {
        public ResGenericTypeArg(
            IResTypeExp type)
        {
            _type = type;
        }

        public override string ToString()
        {
            return _type.ToString();
        }

        public IResGenericArg Substitute(Substitution subst)
        {
            return new ResGenericTypeArg(
                _type.Substitute(subst));
        }

        public IResTypeExp Type { get { return _type; } }

        private IResTypeExp _type;
    }

    public class ResGenericValueArg : IResGenericArg
    {
        public ResGenericValueArg(
            IResExp value)
        {
            _value = value;
        }

        public override string ToString()
        {
            return _value.ToString();
        }

        public IResGenericArg Substitute(Substitution subst)
        {
            return new ResGenericValueArg(
                _value.Substitute(subst));
        }

        public IResExp Value { get { return _value; } }

        private IResExp _value;
    }

    public class ResMemberGenericApp : IResMemberTerm
    {
        public ResMemberGenericApp(
            IResGenericRef fun,
            IEnumerable<IResGenericArg> args)
        {
            _fun = fun;
            _args = args.ToArray();

            _subst = new Substitution(_fun.MemberTerm.Subst);
            foreach( var pair in _fun.Parameters.Zip( _args, Tuple.Create ) )
            {
                _subst.Insert( pair.Item1.Decl, pair.Item2 );
            }
        }

        public override string ToString()
        {
            var builder = new StringBuilder();

            builder.AppendFormat("{0}[", _fun.MemberTerm);

            bool first = true;
            foreach (var a in _args)
            {
                if (!first) builder.Append(", ");
                first = false;

                builder.AppendFormat("{0}", a);
            }

            builder.AppendFormat("]");

            return builder.ToString();
        }

        public IResMemberTerm Substitute(Substitution subst)
        {
            return new ResMemberGenericApp(
                _fun.Substitute<IResGenericRef>( subst ),
                from a in _args select a.Substitute(subst) );
        }

        public IResMemberDecl Decl { get { return _fun.InnerDecl; } }
        public IResGenericRef Fun { get { return _fun; } }
        public IEnumerable<IResGenericArg> Args { get { return _args; } }

        public Substitution Subst { get { return _subst; } }

        private IResGenericRef _fun;
        private IResGenericArg[] _args;
        private Substitution _subst;
    }
}
