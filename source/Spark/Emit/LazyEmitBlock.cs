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

namespace Spark.Emit
{
    public class LazyEmitType : IEmitType
    {
        private Func<IEmitType> _func;
        private IEmitType _type;

        public LazyEmitType(Func<IEmitType> func)
        {
            _func = func;
        }

        public IEmitType Type
        {
            get
            {
                if (_type != null)
                    return _type;
                if (_func != null)
                {
                    _type = _func();
                    _func = null;
                    return _type;
                }

                throw new NotImplementedException();
            }
            set
            {
                _type = value;
                _func = null;
            }
        }


        public IEmitTarget Target
        {
            get { throw new NotImplementedException(); }
        }

        public uint Size
        {
            get { throw new NotImplementedException(); }
        }

        public uint Alignment
        {
            get { throw new NotImplementedException(); }
        }
    }

    public class LazyEmitVal : IEmitVal
    {
        private Func<IEmitVal> _func;
        private IEmitVal _val;

        public LazyEmitVal()
        {
        }

        public LazyEmitVal(Func<IEmitVal> func)
        {
            _func = func;
        }

        public IEmitVal Val
        {
            get
            {
                if (_val != null)
                    return _val;
                if (_func != null)
                {
                    _val = _func();
                    _func = null;
                    return _val;
                }

                throw new NotImplementedException();
            }
            set
            {
                _val = value;
                _func = null;
            }
        }

        public IEmitVal GetAddress()
        {
            return new LazyEmitVal(() => Val.GetAddress());
        }

        public IEmitType Type
        {
            get { return new LazyEmitType(() => Val.Type); }
        }
    }

    public class LazyEmitBlock : IEmitBlock
    {
        private IEmitTarget _target;
        private IEmitMethod _method;
        private List<Action<IEmitBlock>> _actions = new List<Action<IEmitBlock>>();

        public LazyEmitBlock(
            IEmitTarget target,
            IEmitMethod method )
        {
            _target = target;
            _method = method;
        }

        private void Defer(Action<IEmitBlock> action)
        {
            _actions.Add(action);
        }

        private IEmitVal Defer(Func<IEmitBlock, IEmitVal> action)
        {
            var lazyVal = new LazyEmitVal();
            Defer((b) =>
            {
                var nonLazyVal = action(b);
                lazyVal.Val = nonLazyVal;
            });
            return lazyVal;
        }

        public void ApplyTo(IEmitBlock block)
        {
            foreach (var a in _actions)
                a(block);
        }

        private IEmitType Un(IEmitType type)
        {
            if (type is LazyEmitType)
            {
                return ((LazyEmitType)type).Type;
            }
            return type;
        }

        private IEmitVal Un(IEmitVal val)
        {
            if (val is LazyEmitVal)
            {
                return ((LazyEmitVal)val).Val;
            }
            return val;
        }

        private IEmitVal[] Un(IEnumerable<IEmitVal> vals)
        {
            if (vals == null)
                return null;

            return (from v in vals
                    select Un(v)).ToArray();
        }

        public IEmitTarget Target
        {
            get { return _target; }
        }

        public IEmitMethod Method
        {
            get { return _method; }
        }

        public void AppendComment(string comment)
        {
            Defer((b) => { b.AppendComment(comment); });
        }

        public void AppendComment(Span span)
        {
            Defer((b) => { b.AppendComment(span); });
        }

        public IEmitBlock InsertBlock()
        {
            var result = new LazyEmitBlock(_target, _method);
            Defer((b) => { result.ApplyTo(b); });
            return result;
        }

        public IEmitVal Local(string name, IEmitType type)
        {
            return Defer((b) => b.Local(name, Un(type)));
        }

        public IEmitVal Temp(string name, IEmitVal val)
        {
            return Defer((b) => b.Temp(name, Un(val)));
        }

        public IEmitVal LiteralBool(bool val)
        {
            return Defer((b) => b.LiteralBool(val));
        }

        public IEmitVal LiteralU32(uint val)
        {
            return Defer((b) => b.LiteralU32(val));
        }

        public IEmitVal LiteralS32(int val)
        {
            return Defer((b) => b.LiteralS32(val));
        }

        public IEmitVal LiteralF32(float val)
        {
            return Defer((b) => b.LiteralF32(val));
        }

        public IEmitVal LiteralData(byte[] data)
        {
            return Defer((b) => b.LiteralData(data));
        }

        public IEmitVal LiteralString(string val)
        {
            return Defer((b) => b.LiteralString(val));
        }

        public IEmitVal Enum32(string type, string name, UInt32 val)
        {
            return Defer((b) => b.Enum32(type,name,val));
        }

        public IEmitVal GetArrow(IEmitVal obj, IEmitField field)
        {
            return Defer((b) => b.GetArrow(Un(obj), field));
        }

        public void SetArrow(IEmitVal obj, IEmitField field, IEmitVal val)
        {
            Defer((b) => { b.SetArrow(Un(obj), field, Un(val)); });
        }

        public void StoreRaw(IEmitVal basePointer, uint offset, IEmitVal val)
        {
            Defer((b) => { b.StoreRaw(Un(basePointer), offset, Un(val)); });
        }

        public IEmitVal CastRawPointer(IEmitVal val, IEmitType type)
        {
            return Defer((b) => b.CastRawPointer(Un(val), Un(type)));
        }

        public IEmitVal GetBuiltinField(IEmitVal obj, string fieldName, IEmitType fieldType)
        {
            return Defer((b) => b.GetBuiltinField(Un(obj), fieldName, Un(fieldType)));
        }

        public IEmitVal Array(IEmitType elementType, IEmitVal[] elements)
        {
            return Defer((b) => b.Array(Un(elementType), Un(elements)));
        }

        public IEmitVal Struct(string structTypeName, params IEmitVal[] fields)
        {
            return Defer((b) => b.Struct(structTypeName, Un(fields)));
        }

        public void CallCOM(IEmitVal obj, string interfaceName, string methodName, params IEmitVal[] args)
        {
            Defer((b) => { b.CallCOM(Un(obj), interfaceName, methodName, Un(args)); });
        }

        public IEmitVal BuiltinApp(IEmitType type, string template, IEmitVal[] args)
        {
            return Defer((b) => b.BuiltinApp(Un(type), template, Un(args)));
        }
    }
}
