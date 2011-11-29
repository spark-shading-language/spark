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
    public class MidLabel
    {
    }

    public class MidLabelExp : MidExp
    {
        public MidLabelExp(
            SourceRange range,
            MidLabel label,
            MidExp body,
            MidType type)
            : base(range, type)
        {
            _label = label;
            _body = body;
        }

        public override string ToString()
        {
            return string.Format("label {0} {{ {1} }}", _label, _body);
        }

        public MidLabel Label { get { return _label; } }
        public MidExp Body
        {
            get { return _body; }
            set { _body = value; }
        }

        private MidLabel _label;
        private MidExp _body;
    }

    public class MidBreakExp : MidExp
    {
        public MidBreakExp(
            SourceRange range,
            MidLabel label,
            MidVal value)
            : base(range, new MidVoidType())
        {
            _label = label;
            _value = value;
        }

        public override string ToString()
        {
            return string.Format("break {0} {1};", _label, _value);
        }

        public MidLabel Label { get { return _label; } }
        public MidVal Value
        {
            get { return _value; }
            set { _value = value; }
        }

        private MidLabel _label;
        private MidVal _value;
    }
}
