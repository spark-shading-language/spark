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
    public class Substitution
    {
        public Substitution()
        {
        }

        public Substitution(
            Substitution baseSubst)
        {
            _base = baseSubst;
        }

        public void Insert(IResVarDecl key, Func<SourceRange, IResExp> gen)
        {
            _vars[key] = (r, subst) => gen(r);
        }

        public void Insert(IResVarDecl key, IResVarDecl value)
        {
            _vars[key] = (r, subst) => new ResVarRef(r, value, value.Type.Substitute(subst));
        }

        public void Insert(IResTypeParamDecl var, Func<SourceRange, IResTypeExp> gen)
        {
            _typeVars[var] = gen;
        }

        public void Insert(IResGenericParamDecl param, IResGenericArg arg)
        {
            if (param is IResTypeParamDecl)
            {
                Insert((IResTypeParamDecl)param, (r) => ((ResGenericTypeArg) arg).Type);
            }
            else if (param is IResValueParamDecl)
            {
                Insert((IResVarDecl)param, (r) => ((ResGenericValueArg)arg).Value);
            }
        }

        public IResExp Lookup(IResVarDecl var, SourceRange range)
        {
            var subst = this;
            while (subst != null)
            {
                Func<SourceRange, Substitution, IResExp> gen;
                if (subst._vars.TryGetValue(var, out gen))
                    return gen(range, this);

                subst = subst._base;
            }

            return new ResVarRef(range, var, var.Type.Substitute(this));
        }

        public IResTypeExp Lookup(IResTypeParamDecl var, SourceRange range)
        {
            Func<SourceRange, IResTypeExp> gen;
            if (_typeVars.TryGetValue(var, out gen))
                return gen(range);

            if (_base != null)
                return _base.Lookup(var, range);

            return new ResTypeVarRef(range, var);
        }

        public ResLabel Lookup(ResLabel key)
        {
            ResLabel result;
            if (_labels.TryGetValue(key, out result))
                return result;

            if (_base != null)
                return _base.Lookup(key);

            return key;
        }

        public void Insert(ResLabel key, ResLabel value)
        {
            _labels[key] = value;
        }

        private Substitution _base = null;
        private Dictionary<IResVarDecl, Func<SourceRange, Substitution, IResExp>> _vars = new Dictionary<IResVarDecl, Func<SourceRange, Substitution, IResExp>>();
        private Dictionary<IResTypeParamDecl, Func<SourceRange, IResTypeExp>> _typeVars = new Dictionary<IResTypeParamDecl, Func<SourceRange, IResTypeExp>>();
        private Dictionary<ResLabel, ResLabel> _labels = new Dictionary<ResLabel, ResLabel>();
    }

    public interface ISubstitutable<out T>
    {
        T Substitute(Substitution subst);
    }

    public static class SubstitutionMethods
    {
        public static T Substitute<T>(
            this ISubstitutable<T> s,
            Substitution subst)
        {
            return s.Substitute( subst );
        }
    }
}
