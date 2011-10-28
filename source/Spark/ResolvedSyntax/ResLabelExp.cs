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
    public class ResLabel
    {
        public ResLabel(
            SourceRange range,
            Identifier name,
            IResTypeExp type)
        {
            _range = range;
            _name = name;
            _type = type;
        }

        public override string ToString()
        {
            return _name.ToString();
        }

        public SourceRange Range { get { return _range; } }
        public Identifier Name { get { return _name; } }
        public IResTypeExp Type { get { return _type; } }

        private SourceRange _range;
        private Identifier _name;
        private IResTypeExp _type;
    }

    public class ResLabelExp : ResExp
    {
        public ResLabelExp(
            SourceRange range,
            ResLabel label,
            IResExp body )
            : base(range, label.Type)
        {
            _label = label;
            _body = body;
        }

        public override IResExp Substitute(Substitution subst)
        {
            var newLabel = new ResLabel(
                _label.Range,
                _label.Name,
                _label.Type.Substitute(subst));

            var newSubst = new Substitution(subst);
            newSubst.Insert(_label, newLabel);

            return new ResLabelExp(
                this.Range,
                newLabel,
                this.Body.Substitute(newSubst));

            throw new NotImplementedException();
        }

        public ResLabel Label { get { return _label; } }
        public IResExp Body { get { return _body; } }

        private ResLabel _label;
        private IResExp _body;
    }
}
