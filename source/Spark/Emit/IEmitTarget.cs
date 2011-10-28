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
    public interface IEmitTarget
    {
        string TargetName { get; }
        IEmitType VoidType { get; }
        IEmitType GetBuiltinType(
            string template,
            IEmitTerm[] args );
        IEmitType GetOpaqueType(
            string name );
        IEmitVal GetNullPointer(
            IEmitType type);
        IEmitType Pointer(IEmitType type);
        IEmitModule CreateModule(string moduleName);

        IEmitVal LiteralBool(bool val);
        IEmitVal LiteralU32(UInt32 val);
        IEmitVal LiteralS32(Int32 val);
        IEmitVal LiteralF32(float val);
        IEmitVal Enum32( string type, string name, UInt32 val );

        void ShaderBytecodeCallback(
            string prefix,
            byte[] data );
    }

    [Flags]
    public enum EmitClassFlags
    {
        None = 0x0,
        Internal = 0x1,
        Hidden = 0x2,
        Implementation = 0x4,
        Mixin = 0x8,
    }

    public interface IEmitModule
    {
        IEmitClass CreateClass(
            string className,
            IEmitClass baseClass,
            EmitClassFlags flags);
        IEmitStruct CreateStruct(string name);
        IEmitVal LiteralString( string val );
        IEmitVal EmitGlobalStruct(
            string name,
            IEmitVal[] members );
        IEmitVal GetMethodPointer( IEmitMethod method );
    }

    public interface IEmitTerm
    {
    }

    public interface IEmitType : IEmitTerm
    {
        IEmitTarget Target { get; }
        UInt32 Size { get; }
        UInt32 Alignment { get; }
    }

    public interface IEmitVal : IEmitTerm
    {
        IEmitVal GetAddress();
        IEmitType Type { get; }
    }

    public interface IEmitMethod
    {
        IEmitVal AddParameter(IEmitType type, string name);

        IEmitVal ThisParameter { get; }
        IEmitBlock EntryBlock { get; }
    }

    public interface IEmitBlock
    {
        IEmitTarget Target { get; }
        IEmitMethod Method { get; }

        void AppendComment(string comment);
        void AppendComment(Span span);

        IEmitBlock InsertBlock();

        IEmitVal Local(string name, IEmitType type);
        IEmitVal Temp(string name, IEmitVal val);

        IEmitVal LiteralData(byte[] data);
        IEmitVal LiteralString(string val);

        IEmitVal GetArrow(IEmitVal obj, IEmitField field);
        void SetArrow(IEmitVal obj, IEmitField field, IEmitVal val);

        void StoreRaw(
            IEmitVal basePointer,
            UInt32 offset,
            IEmitVal val);

        IEmitVal CastRawPointer(IEmitVal val, IEmitType toType);
        IEmitVal GetBuiltinField(
            IEmitVal obj,
            string fieldName,
            IEmitType fieldType);

        IEmitVal Array(
            IEmitType elementType,
            IEmitVal[] elements);

        IEmitVal Struct(
            string structTypeName,
            params IEmitVal[] fields);

        void CallCOM(
            IEmitVal obj,
            string interfaceName,
            string methodName,
            params IEmitVal[] args);

        IEmitVal BuiltinApp(
            IEmitType type,
            string template,
            IEmitVal[] args);
    }

    public interface IEmitClass : IEmitType
    {
        String GetName();

        IEmitField AddPublicField(IEmitType type, string name);
        IEmitField AddPrivateField(IEmitType type, string name);

        IEmitMethod CreateCtor();
        IEmitMethod CreateDtor();
        IEmitMethod CreateMethod(IEmitType resultType, string name);

        void Seal();
    }

    public interface IEmitField
    {
        IEmitType Type { get; }
    }

    public interface IEmitStruct : IEmitType
    {
    }

    public static class EmitExtensions
    {
        public static IEmitType GetBuiltinType(
            this IEmitTarget target,
            string template )
        {
            return target.GetBuiltinType(template, null);
        }

        public static IEmitType Pointer(
            this IEmitType type)
        {
            return type.Target.Pointer(type);
        }

        public static IEmitVal Null(
            this IEmitType type)
        {
            return type.Target.GetNullPointer(type);
        }

        public static IEmitVal Enum32(
            this IEmitBlock block,
            string type,
            string name,
            Enum val)
        {
            return block.Enum32(type, name, Convert.ToUInt32(val));
        }

        public static IEmitVal Enum32(
            this IEmitBlock block,
            Enum val)
        {
            return block.Enum32(
                val.GetType().Name,
                val.ToString(),
                Convert.ToUInt32(val));
        }

        public static IEmitVal Array(
            this IEmitBlock block,
            IEmitType elementType,
            IEnumerable<IEmitVal> elements)
        {
            return block.Array(elementType, elements.ToArray());
        }





        public static IEmitVal LiteralBool(this IEmitBlock block, bool val)
        {
            return block.Target.LiteralBool(val);
        }

        public static IEmitVal LiteralU32(this IEmitBlock block, UInt32 val)
        {
            return block.Target.LiteralU32(val);
        }

        public static IEmitVal LiteralS32(this IEmitBlock block, Int32 val)
        {
            return block.Target.LiteralS32(val);
        }

        public static IEmitVal LiteralF32(this IEmitBlock block, float val)
        {
            return block.Target.LiteralF32(val);
        }

        public static IEmitVal Enum32(this IEmitBlock block, string type, string name, UInt32 val)
        {
            return block.Target.Enum32(type, name, val);
        }

        public static IEmitVal CastRawPointer(
            this IEmitBlock block,
            IEmitVal val )
        {
            return block.CastRawPointer( val, null );
        }
    }
}
