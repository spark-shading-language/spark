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
    public interface IResScope
    {
        IResTerm Lookup(SourceRange range, Identifier name);

        IEnumerable<IResTerm> GetImplicits();
        /*
        IEnumerable<T> LookupImplicits<T>(
            SourceRange range,
            Func<IResTerm, IEnumerable<T>> filter);
        */
    }

    public abstract class ResScope : IResScope
    {
        public abstract IResTerm Lookup(SourceRange range, Identifier name);
        public IEnumerable<IResTerm> GetImplicits()
        {
            if( _implicits == null )
                _implicits = GetImplicitsImpl().ToArray();
            return _implicits;
        }

        public virtual IEnumerable<IResTerm> GetImplicitsImpl()
        {
            return _emptyImplicits;
        }

        private IResTerm[] _implicits;
        private static IResTerm[] _emptyImplicits = new IResTerm[] { };
    }

    public enum ResOverloadScore
    {
        NoConversion,
        ImplicitTypeConversion,
    }

    public class Cell<T>
    {
        public Cell()
        {
        }

        public Cell(T value)
        {
            _value = value;
        }

        public T Value
        {
            get { return _value; }
            set { _value = value; }
        }

        private T _value;
    }

    public class ResEnv
    {
        public ResEnv(
            ResolveContext context,
            IDiagnosticsSink diagnostics,
            IResScope scope,
            Cell<ResOverloadScore> score,
            IResElementRef implicitFreq,
            bool disableConversions,
            IResTypeExp baseAttributeType)
        {
            _context = context;
            _diagnostics = diagnostics;
            _scope = scope;
            _score = score;
            _implicitFreq = implicitFreq;
            _disableConversions = disableConversions;
            _baseAttributeType = baseAttributeType;
        }

        public ResEnv(
            ResolveContext context,
            IDiagnosticsSink diagnostics,
            IResScope scope )
            : this(context, diagnostics, scope, null, null, false, null)
        {}

        public IResTerm Lookup(SourceRange range, Identifier name)
        {
            return _scope.Lookup(range, name);
        }

        public IEnumerable<T> LookupImplicits<T>(
            SourceRange range,
            Func<IResTerm, IEnumerable<T>> filter)
        {
            return (from i in _scope.GetImplicits()
                    from j in filter(i)
                    select j).ToArray();
        }

        public void AddDiagnostic(IDiagnosticsSource diagnostic)
        {
            _diagnostics.Add(diagnostic);
        }

        public void AddDiagnostic(
            Severity severity,
            SourceRange range,
            string message )
        {
            _diagnostics.Add(severity, range, message);
        }

        public void AddDiagnostic(
            Severity severity,
            SourceRange range,
            string format,
            params object[] args )
        {
            _diagnostics.Add(severity, range, format, args: args);
        }

        public void Error(
            SourceRange range,
            string format,
            params object[] args)
        {
            AddDiagnostic(Severity.Error, range, format, args: args);
        }

        public ResEnv NestScope(
            IResScope scope)
        {
            return new ResEnv(
                _context,
                _diagnostics.InsertSink(),
                new ResPairScope(_scope, scope),
                _score,
                _implicitFreq,
                _disableConversions,
                _baseAttributeType);
        }

        public ResEnv NestDiagnostics()
        {
            return new ResEnv(
                _context,
                _diagnostics.InsertSink(),
                _scope,
                _score,
                _implicitFreq,
                _disableConversions,
                _baseAttributeType);
        }

        public ResEnv NestDiagnostics(
            IDiagnosticsSink sink)
        {
            return new ResEnv(
                _context,
                sink,
                _scope,
                _score,
                _implicitFreq,
                _disableConversions,
                _baseAttributeType);
        }

        public ResEnv NestDisableConversions()
        {
            return new ResEnv(
                _context,
                _diagnostics,
                _scope,
                _score,
                _implicitFreq,
                true,
                _baseAttributeType);
        }

        public ResOverloadScore Score
        {
            get
            {
                if (_score == null)
                    return ResOverloadScore.NoConversion;
                return _score.Value;
            }
        }

        public void UpdateScore(ResOverloadScore score)
        {
            if (_score == null)
                return;

            if (score > _score.Value)
                _score.Value = score;
        }

        public ResEnv NestScore()
        {
            return new ResEnv(
                _context,
                _diagnostics,
                _scope,
                new Cell<ResOverloadScore>(),
                _implicitFreq,
                _disableConversions,
                _baseAttributeType);
        }

        public IResElementRef ImplicitFreq
        {
            get { return _implicitFreq; }
        }

        public ResEnv NestImplicitFreq( IResElementRef freq )
        {
            return new ResEnv(
                _context,
                _diagnostics,
                _scope,
                _score,
                freq,
                _disableConversions,
                _baseAttributeType);
        }

        public bool DisableConversions
        {
            get { return _disableConversions; }
        }

        public ResEnv NestBaseAttributeType(IResTypeExp type)
        {
            return new ResEnv(
                _context,
                _diagnostics,
                _scope,
                _score,
                _implicitFreq,
                _disableConversions,
                type);
        }

        public IResTypeExp BaseAttributeType
        {
            get { return _baseAttributeType; }
        }

        public ILazy<T> Lazy<T>(Func<T> generator)
        {
            return _context.LazyFactory.Lazy<T>(generator);
        }

        private ResolveContext _context;
        private IDiagnosticsSink _diagnostics;
        private IResScope _scope;
        private Cell<ResOverloadScore> _score;
        private IResElementRef _implicitFreq;
        private bool _disableConversions = false;

        private IResTypeExp _baseAttributeType = null;
    }

    public class ResLocalScope : ResScope
    {
        public override IResTerm Lookup(SourceRange range, Identifier name)
        {
            Func<SourceRange, IResTerm> func;
            if (_bindings.TryGetValue(name, out func))
                return func(range);

            return null;
        }

        public void Insert(Identifier name, Func<SourceRange, IResTerm> func)
        {
            // \todo: Collisions
            _bindings[name] = func;
        }

        private Dictionary<Identifier, Func<SourceRange, IResTerm>> _bindings = new Dictionary<Identifier, Func<SourceRange, IResTerm>>();
    }

    public class ResPairScope : ResScope
    {
        public ResPairScope(
            IResScope outer,
            IResScope inner)
        {
            _outer = outer;
            _inner = inner;
        }

        public override IResTerm Lookup(SourceRange range, Identifier name)
        {
            var innerTerm = _inner.Lookup(range, name);
            if (innerTerm != null)
            {
                return new ResLayeredTerm(
                    range,
                    innerTerm,
                    () => _outer.Lookup(range, name));
            }

            return _outer.Lookup(range, name);
        }

        public override IEnumerable<IResTerm> GetImplicitsImpl()
        {
            return _inner.GetImplicits().Concat(_outer.GetImplicits());
        }

        private IResScope _outer;
        private IResScope _inner;
    }

    public class ResModuleScope : ResScope
    {
        public ResModuleScope(
            ILazy<IResModuleDecl> module)
        {
            _module = module;
        }

        public override IResTerm Lookup(SourceRange range, Identifier name)
        {
            var globalDecls = _module.Value.LookupDecls(name).ToArray();
            switch (globalDecls.Length)
            {
                case 0:
                    return null;
                case 1:
                    return globalDecls[0].MakeRef( range );
                default:
                    throw new NotImplementedException();
            }
        }

        private ILazy<IResModuleDecl> _module;
    }

    public class ResPipelineScope : ResScope
    {
        public ResPipelineScope(
            IResContainerRef pipeline,
            IResVarDecl thisParameter )
        {
            _pipeline = pipeline;
            _thisParameter = thisParameter;
        }

        public override IResTerm Lookup(SourceRange range, Identifier name)
        {
            var memberNameGroupSpecs = _pipeline.LookupMembers(range, name).Reverse().ToArray();
            if (memberNameGroupSpecs.Length == 0)
                return null;

            IResTerm result = null;

            foreach( var mngs in memberNameGroupSpecs )
            {
                var memberCategorySpecs = mngs.Categories.ToArray();

                IResTerm term = null;

                switch( memberCategorySpecs.Length )
                {
                case 0:
                    break;
                case 1:
                    var thisRef = new ResVarRef( range, _thisParameter );
                    term = memberCategorySpecs[ 0 ].Bind( range, thisRef );
                    break;
                default:
                    term = new ResOverloadedTerm(
                        range,
                        from mcs in memberCategorySpecs
                        let tr = new ResVarRef( range, _thisParameter )
                        select mcs.Bind( range, tr ) );
                    break;
                }

                if( term != null )
                {
                    var oldResult = result;
                    result = new ResLayeredTerm(
                        term.Range,
                        term,
                        () => oldResult );
                }
            }

            return result;
        }

        public override IEnumerable<IResTerm> GetImplicitsImpl()
        {
            return (from m in _pipeline.ImplicitMembers
                    let thisRef = new ResVarRef(_pipeline.Range, _thisParameter)
                    let memberRef = m.Bind(m.Decl.Range, thisRef)
                    select memberRef);
        }

        private IResContainerRef _pipeline;
        private IResVarDecl _thisParameter;
    }
}
