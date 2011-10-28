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

namespace Spark.Mid
{
    public class MidCleanup
    {
        private MidExpFactory _exps;

        public MidCleanup(
            MidExpFactory exps )
        {
            _exps = exps;
        }

        private List<MidAttributeDecl> _attributesToKeep = new List<MidAttributeDecl>();

        private Dictionary<MidAttributeDecl, MidAttributeDecl> _mapOldToNew = new Dictionary<MidAttributeDecl, MidAttributeDecl>();

        private Dictionary<MidAttributeDecl, MidAttributeWrapperDecl> _mapOldToWrapper = new Dictionary<MidAttributeDecl, MidAttributeWrapperDecl>();

        private List<MidAttributeWrapperDecl> _attributeWrappersToKeep = new List<MidAttributeWrapperDecl>();

        private MidTransform _transform;

        public void ApplyToModule( MidModuleDecl module )
        {
            /*
            var transform = new MidTransform(
                null, // no pre-transform
                ( e ) => SimplifyExpImpl( (dynamic) e, env ) );

            transform.ApplyToModule( module 

            return transform.Transform( exp );


            tranform

            */

            foreach( var p in module.Pipelines )
                ApplyToPipeline( p );
        }

        public void ApplyToPipeline( MidPipelineDecl p )
        {
            _attributesToKeep.Clear();
            _mapOldToNew.Clear();
            _mapOldToWrapper.Clear();
            _attributeWrappersToKeep.Clear();

            // Find all the attributes worth keeping...
            CollectPipelineInfo( p );

            // Now go ahead and blow away all the old attributes,
            // replacing them with shiny new ones!!!

            _transform = new MidTransform(
                ( e ) => CleanupExp( (dynamic) e ) );

            foreach( var e in p.Elements )
                 e.Clear();

            foreach( var a in _attributesToKeep )
            {
                if( a.Exp != null )
                    a.Exp = _transform.Transform( a.Exp );

                var newAttr = MapOldToNew( a );
            }

            // We copy the attributes array, since otherwise
            // we end up mutating it by adding new attributes
            foreach( var e in p.Elements )
                foreach( var a in e.Attributes.ToArray() )
                    _transform.ApplyToAttribute( a );
            foreach( var m in p.Methods )
            {
                if( m.Body != null )
                    m.Body = _transform.Transform( m.Body );
            }

            foreach( var e in p.Elements )
            {
                var oldWrappers = e.AttributeWrappers.ToArray();

                var newWrappers = (from w in oldWrappers
                                   where _attributeWrappersToKeep.Contains(w)
                                   select w).ToArray();

                e.AttributeWrappers = newWrappers;
            }
        }

        public MidExp CleanupExp( MidExp exp )
        {
            return exp;
        }

        public MidAttributeRef CleanupExp( MidAttributeRef exp )
        {
            var oldAttr = exp.Decl;
            var newAttr = MapOldToNew( oldAttr );
            if( newAttr == oldAttr )
                return exp;

            newAttr.TrySetName( oldAttr.Name, oldAttr.Range );
            return _exps.AttributeRef( newAttr );
        }

        public MidAttributeFetch CleanupExp( MidAttributeFetch exp )
        {
            var oldAttr = exp.Attribute;
            var newAttr = MapOldToNew( oldAttr );
            return _exps.AttributeFetch( exp.Obj, newAttr );
        }

        private MidAttributeDecl MapOldToNew( MidAttributeDecl old )
        {
            MidAttributeDecl result = null;
            if( _mapOldToNew.TryGetValue( old, out result ) )
                return result;

            MidAttributeWrapperDecl wrapper = null;
            _mapOldToWrapper.TryGetValue( old, out wrapper );

            if (old.Exp != null)
            {
                // bootstrap the _mapOldToNew, so that
                // a recursive invocation of MapOldToNew
                // on this same attribute won't cause
                // an infinite recursion:
                _mapOldToNew[old] = old;

                old.Exp = _transform.Transform(old.Exp);
            }

            if( old.Exp == null || old.IsForcedOutput || old.IsInput )
            {
                result = old;
                old.Element.AddAttribute( old );
            }
            else if( old.Name.ToString().StartsWith( "__" ) )
            {
                result = old;
                old.Element.AddAttribute( old );
            }
            else
            {
                result = old.Element.CacheAttr( old.Exp, old.Type );
            }

            if( wrapper != null )
            {
                result.TrySetName( wrapper.Name, wrapper.Range );
                wrapper.Attribute = result;
                _attributeWrappersToKeep.Add( wrapper );
            }

            _mapOldToNew[ old ] = result;
            return result;
        }

        public void CollectPipelineInfo( MidPipelineDecl pipeline )
        {
            foreach( var e in pipeline.Elements )
                CollectElementInfo( e );
        }

        public void CollectElementInfo( MidElementDecl element )
        {
            foreach( var a in element.Attributes )
                CollectAttributeInfo( a );
            foreach( var wrapper in element.AttributeWrappers )
            {
                _mapOldToWrapper[ wrapper.Attribute ] = wrapper;
            }
        }

        public void CollectAttributeInfo( MidAttributeDecl attribute )
        {
            // Explicit inputs and outputs needs to be kept
            if( attribute.IsForcedOutput || attribute.IsInput )
            {
                _attributesToKeep.Add( attribute );
            }
        }
    }

    public class MidSimplifyContext
    {
        private MidExpFactory _exps;

        public MidSimplifyContext(
            MidExpFactory exps)
        {
            _exps = exps;
        }

        private class SimplifyEnv
        {
            public SimplifyEnv(
                SimplifyEnv parent)
            {
                _parent = parent;
            }

            public void Insert( MidVar var, MidVal val )
            {
                _vars[var] = val;
            }

            public MidVal Lookup(MidVarRef varRef)
            {
                MidVal val;
                if (_vars.TryGetValue(varRef.Var, out val))
                    return val;
                if (_parent != null)
                    return _parent.Lookup(varRef);
                return varRef;
            }

            private Dictionary<MidVar, MidVal> _vars = new Dictionary<MidVar, MidVal>();
            private SimplifyEnv _parent;
        }

        public void SimplifyModule(MidModuleDecl module)
        {
            foreach (var p in module.Pipelines)
                SimplifyPipeline(p);
        }

        public void SimplifyPipeline(MidPipelineDecl pipeline)
        {
            foreach (var e in pipeline.Elements)
                SimplifyElement(e);

            foreach (var m in pipeline.Methods)
                SimplifyMethod(m);
        }

        public void SimplifyElement(MidElementDecl element)
        {
            foreach (var a in element.Attributes)
                SimplifyAttribute(a);
        }

        public void SimplifyAttribute(MidAttributeDecl attribute)
        {
            attribute.Exp = SimplifyExp(attribute.Exp, new SimplifyEnv(null));
        }

        public void SimplifyMethod(MidMethodDecl method)
        {
            method.Body = SimplifyExp(method.Body, new SimplifyEnv(null));
        }


        private MidExp SimplifyExp(MidExp exp, SimplifyEnv env)
        {
            if (exp == null)
                return null;

            var transform = new MidTransform(
                null, // no pre-transform
                (e) => SimplifyExpImpl((dynamic)e, env));
            return transform.Transform(exp);
        }

        private MidExp SimplifyExpImpl(MidExp exp, SimplifyEnv env)
        {
            return exp;
        }

        private MidExp SimplifyExpImpl(MidAttributeRef val, SimplifyEnv env)
        {
            if (val.IsLazy)
            {
                return _exps.AttributeRef(val.Decl);
            }
            return val;
        }

        private MidExp SimplifyExpImpl(MidAttributeFetch exp, SimplifyEnv env)
        {
            return _exps.AttributeFetch(
                exp.Obj,
                exp.Attribute);
        }

        private MidExp SimplifyExpImpl(MidVarRef val, SimplifyEnv env)
        {
            return env.Lookup(val);
        }

        private MidExp SimplifyExpImpl(MidLabelExp exp, SimplifyEnv env)
        {
            return SimplifyLabelExpImpl(exp, (dynamic) exp.Body, env);
        }

        private MidExp SimplifyExpImpl(MidLetExp exp, SimplifyEnv env)
        {
            if (exp.Exp is MidVal)
            {
                // The variable is just being bound to a simple
                // value, so substitute it away:

                var innerEnv = new SimplifyEnv(env);
                innerEnv.Insert(exp.Var, (MidVal) exp.Exp);

                return SimplifyExp(exp.Body, innerEnv);
            }

            if (exp.Exp is MidBreakExp)
            {
                // Well, we can't possibly get to the rest of the
                // expression, right?
                return exp.Exp;
            }

            if (exp.Body is MidVarRef)
            {
                var midVarRef = (MidVarRef)exp.Body;
                if (midVarRef.Var == exp.Var)
                    return exp.Exp;
            }

            if (exp.Exp is MidPath)
            {
                var midPath = (MidPath)exp.Exp;


                // Try to substitute this path into
                // any down-stream field references...

                var midLet = exp;
                while (midLet != null)
                {
                    midLet.Exp = TryFoldPath(exp.Var, midLet.Exp, midPath);
                    midLet.Body = TryFoldPath(exp.Var, midLet.Body, midPath);


                    midLet = midLet.Body as MidLetExp;
                }
            }

            if (!UsesVar(exp.Body, exp.Var) && !MightHaveSideEffects(exp.Exp))
            {
                // \todo: Should be able to DCE the let expression away,
                // but to do that we need to be sure it doesn't have
                // any side-effects... :(
                return exp.Body;
            }

            return exp;
        }

        private MidExp TryFoldPath(
            MidVar var,
            MidExp exp,
            MidPath path)
        {
            if (exp is MidFieldRef)
            {
                var midFieldRef = (MidFieldRef)exp;
                if (midFieldRef.Obj is MidVarRef)
                {
                    var midVarRef = (MidVarRef) midFieldRef.Obj;
                    if (midVarRef.Var == var)
                    {
                        midFieldRef.Obj = path;
                        return midFieldRef;
                    }
                }
            }
            return exp;
        }

        private MidExp SimplifyLabelExpImpl(
            MidLabelExp labelExp,
            MidLetExp letExp,
            SimplifyEnv env)
        {
            // As long as the label doesn't occur in the
            // bound expression, we can move it outside
            // of the label's bounds.
            if (!UsesLabel(letExp.Exp, labelExp.Label))
            {
                MidExp result = new MidLetExp(
                    letExp.Var,
                    letExp.Exp,
                    new MidLabelExp(
                        labelExp.Label,
                        letExp.Body,
                        letExp.Type));
                result = SimplifyExp(result, env);
                return result;
            }

            return labelExp;
        }

        private MidExp SimplifyLabelExpImpl(
            MidLabelExp labelExp,
            MidBreakExp breakExp,
            SimplifyEnv env)
        {
            if (labelExp.Label == breakExp.Label)
            {
                return breakExp.Value;
            }

            return labelExp;
        }

        private MidExp SimplifyLabelExpImpl(
            MidLabelExp labelExp,
            MidExp exp,
            SimplifyEnv env)
        {
            if (!UsesLabel(exp, labelExp.Label))
                return exp;

            return labelExp;
        }


        private bool UsesLabel(
            MidExp exp,
            MidLabel label)
        {

            bool result = false;
            var transform = new MidTransform(
                (e) =>
                {
                    if (e is MidBreakExp && (e as MidBreakExp).Label == label)
                        result = true;
                    return e;
                });

            transform.Transform(exp);

            return result;
        }

        private bool UsesVar(
            MidExp exp,
            MidVar var)
        {

            bool result = false;
            var transform = new MidTransform(
                (e) =>
                {
                    if (e is MidVarRef && (e as MidVarRef).Var == var)
                        result = true;
                    return e;
                });

            transform.Transform(exp);

            return result;
        }

        private bool MightHaveSideEffects(
            MidExp exp )
        {

            bool result = false;
            var transform = new MidTransform(
                (e) =>
                {
                    if (e is MidAssignExp)
                        result = true;
                    if (e is MidBreakExp)
                        result = true;
                    if (e is MidIfExp)
                        result = true;
                    if (e is MidForExp)
                        result = true;
                    if (e is MidBuiltinApp)
                    {
                        // \todo: Need a *huge* fix for this. Stdlib functions that might
                        // have side-effects need to be marked in some way to avoid this kind of thing... :(
                        var app = (MidBuiltinApp)e;
                        if (app.Decl.Name.ToString() == "Append")
                            result = true;
                    }
                    return e;
                });

            transform.Transform(exp);

            return result;
        }
    }
}
