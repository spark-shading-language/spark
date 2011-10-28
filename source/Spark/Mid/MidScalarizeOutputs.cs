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
    public class MidTransform
    {
        private Func<MidExp, MidExp> _preTransform;
        private Func<MidExp, MidExp> _postTransform;

        public MidTransform(
            Func<MidExp, MidExp> preTransform,
            Func<MidExp, MidExp> postTransform = null )
        {
            _preTransform = preTransform;
            _postTransform = postTransform;
        }

        public void ApplyToModule(MidModuleDecl module)
        {
            foreach (var p in module.Pipelines)
                ApplyToPipeline(p);
        }

        public void ApplyToPipeline(MidPipelineDecl pipeline)
        {
            foreach (var e in pipeline.Elements)
                ApplyToElement(e);
        }

        public void ApplyToElement(MidElementDecl element)
        {
            foreach (var a in element.Attributes)
                ApplyToAttribute(a);
        }

        public void ApplyToAttribute(MidAttributeDecl attribute)
        {
            if (attribute.Exp != null)
                attribute.Exp = Transform(attribute.Exp);
        }

        public MidExp Transform(MidExp exp)
        {
            var e = PreTransform(exp);
            TransformChildren(e);
            e = PostTransform(e);
            return e;
        }

        public MidVal Transform(MidVal exp)
        {
            var e = (MidVal) PreTransform(exp);
            TransformChildren(e);
            e = (MidVal)PostTransform(e);
            return e;
        }

        public void TransformChildren(MidExp exp)
        {
            TransformChildrenImpl((dynamic)exp);
        }

        private void TransformChildrenImpl(
            MidVal val)
        {
        }

        private void TransformChildrenImpl(
            MidStructVal structVal )
        {
            structVal.FieldVals = (from f in structVal.FieldVals
                                    select Transform( f )).ToArray();
        }

        private void TransformChildrenImpl(
            MidIfExp exp)
        {
            exp.Condition = Transform(exp.Condition);
            exp.Then = Transform(exp.Then);
            exp.Else = Transform(exp.Else);
        }

        private void TransformChildrenImpl(
            MidSwitchExp exp)
        {
            exp.Value = Transform(exp.Value);
            foreach (var c in exp.Cases)
            {
                c.Value = Transform(c.Value);
                c.Body = Transform(c.Body);
            }
        }


        private void TransformChildrenImpl(
            MidForExp exp)
        {
            exp.Seq = Transform(exp.Seq);
            exp.Body = Transform(exp.Body);
        }

        private void TransformChildrenImpl(
            MidAssignExp exp)
        {
            exp.Dest = Transform(exp.Dest);
            exp.Src = Transform(exp.Src);
        }


        private void TransformChildrenImpl(
            MidElementCtorApp app)
        {
            app.Args = (from a in app.Args
                        let ta = Transform(a.Val)
                        select new MidElementCtorArg(a.Attribute, ta)).ToArray();
        }

        private void TransformChildrenImpl(
            MidBuiltinApp app)
        {
            app.Args = (from a in app.Args
                        select Transform(a)).ToArray();
        }

        private void TransformChildrenImpl(
            MidMethodApp app)
        {
            app.Args = (from a in app.Args
                        select Transform(a)).ToArray();
        }

        private void TransformChildrenImpl(
            MidLetExp let)
        {
            let.Exp = Transform(let.Exp);
            let.Body = Transform(let.Body);
        }

        private void TransformChildrenImpl(
            MidAttributeFetch fetch)
        {
            fetch.Obj = (MidPath) Transform(fetch.Obj);
        }

        private void TransformChildrenImpl(
            MidFieldRef fetch)
        {
            fetch.Obj = (MidPath) Transform(fetch.Obj);
        }

        private void TransformChildrenImpl(
            MidLabelExp exp)
        {
            exp.Body = Transform(exp.Body);
        }

        private void TransformChildrenImpl(
            MidBreakExp exp)
        {
            exp.Value = Transform(exp.Value);
        }

        private MidExp PreTransform(MidExp exp)
        {
            if (_preTransform == null)
                return exp;
            return _preTransform(exp);
        }

        private MidExp PostTransform(MidExp exp)
        {
            if (_postTransform == null)
                return exp;
            return _postTransform(exp);
        }
    }


    public class MidScalarizeOutputs
    {
        private IdentifierFactory _identifiers;

        private class ReplacePass
        {
            public ReplacePass(
                MidExpFactory exps )
            {
                _exps = exps;
            }

            public MidExp PreTransform(MidExp exp)
            {
                return exp;
            }

            public MidExp PreTransform(
                MidAttributeFetch fetch)
            {
                AttributeInfo info;
                if (!_attrInfos.TryGetValue(fetch.Attribute, out info))
                    return fetch;

                var fieldExps = (from f in info.Fields
                                 select _exps.AttributeFetch( fetch.Obj, f.AttrDecl )).ToArray();

                foreach( var f in info.Fields )
                {
                    f.AttrDecl.IsOutput = true;
                }

                return new MidStructVal(
                    fetch.Type,
                    fieldExps );
            }

            public MidExp PreTransform(
                MidFieldRef fieldRef)
            {
                var attrFetch = fieldRef.Obj as MidAttributeFetch;
                if (attrFetch == null)
                    return fieldRef;


                AttributeInfo info;
                if (!_attrInfos.TryGetValue(attrFetch.Attribute, out info))
                    return fieldRef;

                var fieldAttr = (from f in info.Fields
                                 where f.FieldDecl == fieldRef.Decl
                                 select f.AttrDecl).First();

                fieldAttr.IsOutput = true;

                return _exps.AttributeFetch(
                    attrFetch.Obj,
                    fieldAttr);
            }

            public Dictionary<MidAttributeDecl, AttributeInfo> _attrInfos = new Dictionary<MidAttributeDecl, AttributeInfo>();
            private MidExpFactory _exps;
        }

        private ReplacePass _replacePass;

        private MidExpFactory _exps;

        public MidScalarizeOutputs(
            IdentifierFactory identifiers,
            MidExpFactory exps )
        {
            _identifiers = identifiers;
            _exps = exps;

            _replacePass = new ReplacePass( _exps );
        }

        public void ApplyToModule(MidModuleDecl module)
        {
            // Collect information on output attributes
            foreach (var p in module.Pipelines)
                foreach (var e in p.Elements)
                    foreach (var a in e.Attributes.ToArray()) // Copy to avoid concurrent mutation
                        Collect(p, e, a);

            // Replace uses of these attributes
            (new MidTransform( (e) => _replacePass.PreTransform((dynamic) e))).ApplyToModule(module);
        }

        public void Collect(
            MidPipelineDecl pipeline,
            MidElementDecl element,
            MidAttributeDecl attribute)
        {
            if (!attribute.IsOutput)
                return;

            if (!(attribute.Type is MidStructRef))
                return;

            // Unmark the attribute as an output...
            attribute.IsOutput = false;

            var structType = (MidStructRef)attribute.Type;

            AttributeInfo attrInfo;
            attrInfo.AttrDecl = attribute;
            attrInfo.Fields = (from f in structType.Fields
                               select CreateField(pipeline, element, attribute, f)).ToArray();

            _replacePass._attrInfos[attribute] = attrInfo;
        }

        private FieldInfo CreateField(
            MidPipelineDecl pipeline,
            MidElementDecl element,
            MidAttributeDecl attribute,
            MidFieldDecl field)
        {
            var midExp = _exps.FieldRef(
                    _exps.AttributeRef( attribute ),
                    field );

            var name = _identifiers.unique(
                string.Format( "{0}_{1}", attribute.Name, field.Name ) );

            var newAttr = element.CacheAttr(
                midExp,
                field.Type);
            newAttr.TrySetName(name, attribute.Range);

            return new FieldInfo{
                AttrDecl = newAttr,
                FieldDecl = field};
        }

        private void Replace(
            MidAttributeDecl attr)
        {
            if (attr.Exp == null)
                return;

            attr.Exp = Replace(attr.Exp);
        }

        private MidExp Replace(
            MidExp exp)
        {
            return ReplaceImpl((dynamic)exp);
        }

        private MidVal Replace(
            MidVal val)
        {
            return ReplaceImpl((dynamic)val);
        }

        private MidVal ReplaceImpl(
            MidAttributeRef attrRef)
        {
            throw new NotImplementedException();
        }

        struct FieldInfo
        {
            public MidFieldDecl FieldDecl;
            public MidAttributeDecl AttrDecl;
        }

        struct AttributeInfo
        {
            public MidAttributeDecl AttrDecl;
            public FieldInfo[] Fields;
        }

    }
}
