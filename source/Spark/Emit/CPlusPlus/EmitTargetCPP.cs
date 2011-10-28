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

namespace Spark.Emit.CPlusPlus
{
    public class EmitTargetCPP : IEmitTarget
    {
        public EmitTargetCPP()
        {
            _voidType = new EmitTypeCPP(this, "void {0}", 0, 0, null);
        }

        public IEmitModule CreateModule(string moduleName)
        {
            var headerSpan = new Span();
            var sourceSpan = new Span();

            return new EmitModuleCPP(
                this,
                moduleName,
                headerSpan,
                sourceSpan );
        }

        public string TargetName
        {
            get { return "c++"; }
        }

        public IEmitType VoidType
        {
            get { return _voidType; }
        }
        private IEmitType _voidType;

        public IEmitType Pointer(IEmitType type)
        {
            return new EmitTypeCPP(this, "*{0}", 4, 4, (IEmitTypeCPP) type);
        }

        public IEmitType Array(IEmitType type, int length)
        {
            return new EmitTypeCPP(this, "{0}[]", type.Size * (UInt32) length, type.Alignment, (EmitTypeCPP)type);
        }

        public IEmitType GetBuiltinType(string template, IEmitTerm[] args)
        {
            UInt32 pointerSize = 4;
            UInt32 pointerAlign = 4;

            UInt32 size = 0;
            UInt32 align = 0;
            if (template.EndsWith("*"))
            {
                size = 4;
            }
            else if (template.StartsWith("ID3D11"))
            {
                size = 0;
            }
            else if (template.StartsWith("D3D11_"))
            {
                size = 4;
            }
            else if (template.StartsWith("DXGI_"))
            {
                size = 4;
            }
            else if (template == "__Array")
            {
                var elementType = (EmitTypeCPP)args[0];
                var count = UInt32.Parse(((EmitValCPP)args[1]).ToString());

                size = count * elementType.Size;

                return new EmitTypeCPP(
                    this,
                    string.Format("spark::Array<{0},{1}>", elementType, count),
                    size,
                    elementType.Alignment);
            }
            else
            {
                switch (template)
                {
                    case "bool":
                    case "unsigned char":
                        size = 1;
                        break;
                    case "short":
                        size = 2;
                        break;
                    case "int":
                    case "UINT":
                    case "unsigned int":
                    case "float":
                        size = 4;
                        break;
                    case "spark::float2":
                        size = 8;
                        align = 4;
                        break;
                    case "spark::float3":
                        size = 12;
                        align = 4;
                        break;
                    case "spark::float4":
                    case "spark::uint4":
                        size = 16;
                        align = 4;
                        break;
                    case "spark::float4x4":
                        size = 64;
                        align = 4;
                        break;
                    case "const unsigned char*":
                    case "ID3D11Device*":
                    case "ID3D11DeviceContext*":
                    case "ID3D11Buffer*":
                    case "ID3D11ShaderResourceView*":
                    case "ID3D11SamplerState*":
                    case "ID3D11VertexShader*":
                    case "ID3D11HullShader*":
                    case "ID3D11DomainShader*":
                    case "ID3D11PixelShader*":
                    case "ID3D11InputLayout*":
                    case "ID3D11ClassLinkage*":
                    case "ID3D11ClassInstance**":
                    case "ID3D11BlendState*":
                        size = 4;
                        break;
                    case "spark::d3d11::IndexStream":
                        size = pointerSize + 8;
                        align = pointerAlign;
                        break;
                    case "spark::d3d11::VertexStream":
                        size = pointerSize + 8;
                        align = pointerAlign;
                        break;
                    case "spark::d3d11::PrimitiveSpan":
                        size = pointerSize + 9*4;
                        align = pointerAlign;
                        break;
                    case "spark::d3d11::DrawSpan":
                        size = 2*pointerSize + 11*4;
                        align = pointerAlign;
                        break;
                    default:
                        throw new NotImplementedException();
                }

            }

            if( align == 0 )
                align = size;

            if (args != null)
            {
                var castArgs = (from a in args
                                select ((EmitTypeCPP)a).ToString());
                return new EmitTypeCPP(
                    this,
                    string.Format(template, args),
                    size,
                    align);
            }

            return new EmitTypeCPP(this, template, size, align);
        }

        public IEmitType GetOpaqueType(
            string name)
        {
            return new EmitTypeCPP(
                this,
                name,
                4,
                4); // \todo: sizeof(void*)!!!!
        }

        public IEmitVal GetNullPointer(IEmitType type)
        {
            return new EmitValCPP(this, "nullptr", type);
        }


        public IEmitVal LiteralBool(bool val)
        {
            return new EmitValCPP(
                this,
                val ? "true" : "false",
                this.GetBuiltinType("bool"));
        }

        public IEmitVal LiteralU32(uint val)
        {
            return new EmitValCPP(
                this,
                val.ToString(),
                this.GetBuiltinType("unsigned int"));
        }

        public IEmitVal LiteralS32(Int32 val)
        {
            return new EmitValCPP(
                this,
                val.ToString(),
                this.GetBuiltinType("int"));
        }

        public IEmitVal LiteralF32(float val)
        {
            return new EmitValCPP(
                this,
                string.Format("((float){0:R})", (double)val),
                this.GetBuiltinType("float"));
        }

        public IEmitVal Enum32( string type, string name, UInt32 val )
        {
            return new EmitValCPP(
                this,
                name,
                this.GetBuiltinType(type));
        }

        public void ShaderBytecodeCallback(
            string prefix,
            byte[] data )
        {
        }
    }

    public class EmitModuleCPP : IEmitModule
    {
        private EmitTargetCPP _target;

        public EmitTargetCPP Target { get { return _target; } }

        public EmitModuleCPP(
            EmitTargetCPP target,
            string name,
            Span headerSpan,
            Span sourceSpan)
        {
            _target = target;
            _name = name;
            _headerSpan = headerSpan;
            _sourceSpan = sourceSpan;

            _headerSpan.WriteLine("// Automatically generated code. Do not edit.");
            _headerSpan.WriteLine( "#pragma once" );
            _headerSpan.WriteLine( "#include <d3d11.h>" );
            _headerSpan.WriteLine("#include <spark/spark.h>");
            _headerSpan.WriteLine("#include <spark/context.h>");

            _sourceSpan.WriteLine("// Automatically generated code. Do not edit.");
            _sourceSpan.WriteLine("#include \"{0}.h\"", _name);
            _sourceSpan.WriteLine("#pragma warning(disable: 4100)");
            _sourceSpan.WriteLine();
        }

        public Span HeaderSpan { get { return _headerSpan; } }
        public Span SourceSpan { get { return _sourceSpan; } }

        public IEmitClass CreateClass(
            string inClassName,
            IEmitClass baseClass,
            EmitClassFlags flags )
        {
            Span classHeaderSpan = null;
            Span classSourceSpan = null;
            string className = inClassName;

            if (flags.HasFlag(EmitClassFlags.Internal))
            {
                classHeaderSpan = new Span();
                classSourceSpan = new Span();
            }
            else if (flags.HasFlag(EmitClassFlags.Hidden))
            {
                _sourceSpan.WriteLine();

                classHeaderSpan = _sourceSpan.InsertSpan();
                classSourceSpan = _sourceSpan.InsertSpan();
            }
            else
            {
                _headerSpan.WriteLine();
                _sourceSpan.WriteLine();

                classHeaderSpan = _headerSpan.InsertSpan();
                classSourceSpan = _sourceSpan.InsertSpan();
            }

            if( flags.HasFlag( EmitClassFlags.Implementation ) )
            {
                className = "_Impl_" + className;
            }

            return new EmitClassCPP(
                this,
                className,
                (EmitClassCPP) baseClass,
                classHeaderSpan,
                classSourceSpan);
        }

        public IEmitStruct CreateStruct(string name)
        {
            var structHeaderSpan = _headerSpan.InsertSpan();
            var structSourceSpan = _sourceSpan.InsertSpan();

            return new EmitStructCPP(
                Target,
                name,
                structHeaderSpan,
                structSourceSpan );
        }

        public IEmitVal LiteralString( string val )
        {
            return new EmitValCPP(
                Target,
                string.Format( "\"{0}\"", val ),
                Target.GetOpaqueType( "const char*" ) );
        }

        private int _globalCounter = 0;

        public IEmitVal EmitGlobalStruct(
            string name,
            IEmitVal[] members )
        {
            var span = new Span();

            name = string.Format(
                "_spark_global_{0}{1}",
                name ?? "",
                _globalCounter++ );

            span.WriteLine( "struct {" );
            var typeSpan = span.IndentSpan();
            for( int ii = 0; ii < members.Length; ++ii )
            {
                var memberType = (EmitTypeCPP) members[ii].Type;
                typeSpan.WriteLine( "{0};", memberType.Declare( string.Format( "_m{0}", ii ) ) );
            }
            span.WriteLine( "}} {0} = {{", name);
            for( int ii = 0; ii < members.Length; ++ii )
            {
                if( ii != 0 )
                    span.WriteLine( "," );
                span.Write( members[ ii ].ToString() );
            }
            span.WriteLine( " };" );

            _sourceSpan.Add( span );

            return new EmitValCPP(
                Target,
                name,
                Target.GetOpaqueType( "void" ) );
        }

        public IEmitVal GetMethodPointer( IEmitMethod method )
        {
            var name = ((EmitMethodCPP) method).FullName;
            return new EmitValCPP(
                Target,
                string.Format("reinterpret_cast<void*>(&({0}))", name),
                Target.GetOpaqueType( "void*" ) );
        }

        private string _name;
        private Span _headerSpan;
        private Span _sourceSpan;
    }

    public interface IEmitTypeCPP : IEmitType
    {
        string Declare(string inner);
    }

    public class EmitTypeCPP : IEmitTypeCPP
    {
        public EmitTypeCPP(
            EmitTargetCPP target,
            string name,
            UInt32 size,
            UInt32 alignment)
            : this( target, string.Format("{0} {{0}}", name), size, alignment, null)
        {
        }


        public EmitTypeCPP(
            EmitTargetCPP target,
            string format,
            UInt32 size,
            UInt32 alignment,
            IEmitTypeCPP baseType )
        {
            _target = target;
            _format = format;
            _size = size;
            _alignment = alignment;
            _baseType = baseType;
        }

        public override string ToString()
        {
            return Declare("");
        }

        public string Declare(string inner)
        {
            var result = string.Format(_format, inner);
            if (_baseType != null)
                result = _baseType.Declare(result);
            return result;
        }

        public IEmitTarget Target
        {
            get { return _target; }
        }

        public UInt32 Size
        {
            get { return _size; }
        }

        public UInt32 Alignment
        {
            get { return _alignment; }
        }

        private EmitTargetCPP _target;
        private string _format;
        private UInt32 _size;
        private UInt32 _alignment;
        private IEmitTypeCPP _baseType;
    }

    public class EmitClassCPP : IEmitClass, IEmitTypeCPP
    {
        private EmitModuleCPP _module;

        public EmitModuleCPP Module { get { return _module; } }
        public EmitTargetCPP Target { get { return _module.Target; } }
        IEmitTarget IEmitType.Target { get { return _module.Target; } }

        public Span PublicSpan { get { return _publicSpan; } }

        public EmitClassCPP(
            EmitModuleCPP module,
            string name,
            EmitClassCPP baseClass,
            Span headerSpan,
            Span sourceSpan)
        {
            _module = module;
            _name = name;
            _base = baseClass;
            _headerSpan = headerSpan;
            _sourceSpan = sourceSpan;

            string baseClassString = "";
            if (baseClass != null)
                baseClassString = string.Format(" : public {0}", baseClass._name);

            _headerSpan.WriteLine("class {0}{1}", _name, baseClassString);
            _headerSpan.WriteLine("{");
            _headerSpan.WriteLine("public:");
            _publicSpan = _headerSpan.IndentSpan();
            _publicFieldsSpan = _publicSpan.InsertSpan();
            _headerSpan.WriteLine("public:");
            _privateSpan = _headerSpan.IndentSpan();
            _headerSpan.WriteLine("};");

            _sourceSpan.WriteLine("// {0}", _name);
        }

        public String GetName()
        {
            return _name;
        }

        public override string ToString()
        {
            return _name;
        }

        public IEmitField AddPublicField(IEmitType type, string name)
        {
            _publicSize = Align( _publicSize, type.Alignment );
            _publicAlign = Math.Max( _publicAlign, type.Alignment );
            _publicSize += type.Size;

            _publicFieldsSpan.WriteLine("{0};",
                ((IEmitTypeCPP)type).Declare(name));
            return new EmitFieldCPP(name, type);
        }

        public IEmitField AddPrivateField(IEmitType type, string name)
        {
            _privateSize = Align( _privateSize, type.Alignment );
            _privateAlign = Math.Max( _privateAlign, type.Alignment );
            _privateSize += type.Size;

            _privateSpan.WriteLine( "{0};",
                ((IEmitTypeCPP)type).Declare(name));
            return new EmitFieldCPP(name, type);
        }

        public IEmitMethod CreateCtor()
        {
            _publicSpan.WriteLine();
            _sourceSpan.WriteLine();
            return new EmitMethodCPP(
                this,
                string.Format("{0}::", _name),
                "Initialize",
                "__stdcall",
                Target.VoidType,
                _publicSpan.InsertSpan(),
                _sourceSpan.InsertSpan());
        }

        public IEmitMethod CreateDtor()
        {
            _publicSpan.WriteLine();
            _sourceSpan.WriteLine();
            return new EmitMethodCPP(
                this,
                string.Format("{0}::", _name),
                "Finalize",
                "__stdcall",
                Target.VoidType,
                _publicSpan.InsertSpan(),
                _sourceSpan.InsertSpan());
        }

        public IEmitMethod CreateMethod(IEmitType resultType, string name)
        {
            _publicSpan.WriteLine();
            _sourceSpan.WriteLine();
            return new EmitMethodCPP(
                this,
                string.Format("{0}::", _name),
                name,
                "__stdcall",
                resultType,
                _publicSpan.InsertSpan(),
                _sourceSpan.InsertSpan());
        }

        public void Seal()
        {
        }

        string IEmitTypeCPP.Declare(string inner)
        {
            return string.Format("{0} {1}", _name, inner);
        }

        UInt32 IEmitType.Size
        {
            get
            {
                if( _size == 0 )
                    _size = ComputeSize();
                return _size;
            }
        }

        UInt32 IEmitType.Alignment
        {
            get
            {
                return Math.Max( _publicAlign, _privateAlign );
            }
        }

        private UInt32 ComputeSize()
        {
            UInt32 result = 0;
            if( _base != null )
            {
                result += ((IEmitType) _base).Size;
            }

            result = Align( result, _publicAlign );
            result += _publicSize;

            result = Align( result, _privateAlign );
            result += _privateSize;
            return result;
        }

        private UInt32 Align( UInt32 value, UInt32 alignment )
        {
            return alignment * ((value + alignment - 1) / alignment);
        }

        private string _name;
        private EmitClassCPP _base;
        private Span _headerSpan;
        private Span _sourceSpan;

        private Span _publicSpan;
        private Span _publicFieldsSpan;
        private Span _privateSpan;

        UInt32 _publicSize = 0;
        UInt32 _publicAlign = 1;
        UInt32 _privateSize = 0;
        UInt32 _privateAlign = 1;

        private UInt32 _size = 0;
    }

    public class EmitFieldCPP : IEmitField
    {
        public EmitFieldCPP(
            string name,
            IEmitType type)
        {
            _name = name;
            _type = type;
        }

        public IEmitType Type
        {
            get { return _type; }
        }

        public override string ToString()
        {
            return _name;
        }

        private string _name;
        private IEmitType _type;
    }

    public class EmitStructCPP : IEmitStruct
    {
        public EmitStructCPP(
            EmitTargetCPP target,
            string name,
            Span headerSpan,
            Span sourceSpan)
        {
            _target = target;
            _name = name;
            _headerSpan = headerSpan;
            _sourceSpan = sourceSpan;
        }

        IEmitTarget IEmitType.Target { get { return _target; } }
        UInt32 IEmitType.Size { get { throw new NotImplementedException(); } }
        UInt32 IEmitType.Alignment { get { throw new NotImplementedException(); } }

        private EmitTargetCPP _target;
        private string _name;
        private Span _headerSpan;
        private Span _sourceSpan;
    }

    public class EmitMethodCPP : IEmitMethod
    {
        private EmitClassCPP _class;

        public EmitModuleCPP Module { get { return _class.Module; } }
        public EmitTargetCPP Target { get { return _class.Target; } }
        private string _fullName;
        public string FullName { get { return _fullName; } }

        public EmitMethodCPP(
            EmitClassCPP clazz,
            string prefix,
            string name,
            string cconv,
            IEmitType resultType,
            Span headerSpan,
            Span sourceSpan)
        {
            _class = clazz;
            _name = name;
            _cconv = cconv;
            _resultType = resultType;
            _headerSpan = headerSpan;
            _sourceSpan = sourceSpan;

            _headerSpan.Write( "static " );
            _headerParamsSpan = CreateSignatureSpan(
                _name,
                _headerSpan);
            _headerSpan.WriteLine(";");

            _fullName = prefix + _name;

            _sourceParamsSpan = CreateSignatureSpan(
                prefix + _name,
                _sourceSpan);
            _sourceSpan.WriteLine();
            _sourceSpan.WriteLine("{");
            _bodySpan = _sourceSpan.IndentSpan();
            _sourceSpan.WriteLine("}");

            _thisParameter = (EmitValCPP) AddParameter(
                Target.Pointer( clazz ),
                "self" );
            _entryBlock = new EmitBlockCPP(this, _bodySpan.InsertSpan());
        }

        private Span CreateSignatureSpan(
            string qualifiedName,
            Span span )
        {
            var sigSpan = span.InsertSpan();

            if (_resultType != null)
            {
                sigSpan.Write("{0} ", _resultType);
            }
            sigSpan.Write("{0} {1}(", _cconv, qualifiedName);
            var paramsSpan = sigSpan.IndentSpan();
            sigSpan.Write(")");

            return paramsSpan;
        }

        public IEmitVal AddParameter(IEmitType type, string name)
        {
            bool first = _parameters.Count == 0;

            var param = new EmitValCPP(Target, name, type);
            _parameters.Add(param);

            WriteParameter(type, name, first, _headerParamsSpan);
            WriteParameter(type, name, first, _sourceParamsSpan);

            return param;
        }

        public void WriteParameter(
            IEmitType type,
            string name,
            bool first,
            Span span)
        {
            if (!first)
                span.Write(",");
            span.WriteLine();

            span.Write(((IEmitTypeCPP) type).Declare(name));
        }

        public IEmitVal ThisParameter { get { return _thisParameter; } }
        public IEmitBlock EntryBlock { get { return _entryBlock; } }

        private string _name;
        private string _cconv;
        private IEmitType _resultType;
        private Span _headerSpan;
        private Span _sourceSpan;

        private Span _headerParamsSpan;
        private Span _sourceParamsSpan;
        private Span _bodySpan;

        private EmitValCPP _thisParameter;
        private EmitBlockCPP _entryBlock;

        private List<EmitValCPP> _parameters = new List<EmitValCPP>();
    }

    public class EmitBlockCPP : IEmitBlock
    {
        private EmitMethodCPP _method;
        private Span _span;
        private int _counter = 0;

        public EmitTargetCPP Target { get { return _method.Target; } }
        IEmitTarget IEmitBlock.Target { get { return _method.Target; } }

        public EmitBlockCPP(
            EmitMethodCPP method,
            Span span)
        {
            _method = method;
            _span = span;
        }

        private string GenSym(string name)
        {
            return string.Format(
                "{0}_{1}",
                name,
                _counter++);
        }

        public IEmitMethod Method { get { return _method; } }

        public void AppendComment(string comment)
        {
            _span.WriteLine("// {0}", comment);
        }

        public void AppendComment(Span span)
        {
            _span.Add(span.Prefix("// "));
            _span.WriteLine();
        }

        public IEmitBlock InsertBlock()
        {
            return new EmitBlockCPP(
                _method,
                _span.InsertSpan());
        }

        public IEmitVal Local(string name, IEmitType type)
        {
            var sym = GenSym(name);
            _span.WriteLine(
                "{0};",
                ((IEmitTypeCPP) type).Declare(sym));
            return new EmitValCPP(Target, sym, type);
        }

        public IEmitVal Temp(
            string name,
            IEmitVal val )
        {
            var type = (IEmitTypeCPP) val.Type;
            var sym = GenSym(name);
            _span.WriteLine(
                "{0} = {1};",
                type.Declare(sym),
                val);
            return new EmitValCPP(Target, sym, type, ((EmitValCPP) val).IsOwnAddress);
        }

        public IEmitVal LiteralData(byte[] data)
        {
            var sym = GenSym("data");
            _span.WriteLine(
               "static const unsigned char {0}[] = {{",
               sym);

            var dataSpan = _span.IndentSpan();
            foreach (var b in data)
            {
                dataSpan.Write("0x{0:x2}, ", b);
            }
            dataSpan.WriteLine();
            _span.WriteLine("};");

            return new EmitValCPP(
                Target,
                sym,
                Target.GetOpaqueType("const unsigned char*"));
        }

        public IEmitVal LiteralString( string val )
        {
            return _method.Module.LiteralString( val );
        }

        public IEmitVal GetArrow(IEmitVal obj, IEmitField field)
        {
            return new EmitValCPP(
                Target,
                string.Format("({0}->{1})", obj, field),
                field.Type);
        }

        public void SetArrow(IEmitVal obj, IEmitField field, IEmitVal val)
        {
            _span.WriteLine(
                "{0}->{1} = {2};",
                obj,
                field,
                val);
        }

        public void StoreRaw(
            IEmitVal basePointer,
            UInt32 offset,
            IEmitVal val)
        {
            _span.WriteLine(
                "*(({0}) (({1}) + ({2}))) = {3};",
                val.Type.Pointer(),
                basePointer,
                offset,
                val);
        }

        public IEmitVal CastRawPointer(IEmitVal val, IEmitType type)
        {
            var ty = type ?? Target.Pointer( Target.GetBuiltinType( "unsigned char" ) );

            return new EmitValCPP(
                Target,
                string.Format( "(({0}) {1})", ty, val ),
                ty);
        }

        public IEmitVal GetBuiltinField(
            IEmitVal obj,
            string fieldName,
            IEmitType fieldType)
        {
            return new EmitValCPP(
                Target,
                string.Format("({0}.{1})", obj, fieldName),
                fieldType);
        }

        public IEmitVal Array(
            IEmitType elementType,
            IEmitVal[] elements)
        {
            var span = new Span();
            span.WriteLine("{");
            foreach (var e in elements)
            {
                span.WriteLine("\t{0},", e);
            }
            span.Write("}");

            return new EmitValCPP(
                Target,
                span.ToString(),
                Target.Array(elementType, elements.Length),
                true);
        }

        public IEmitVal Struct(
            string structTypeName,
            params IEmitVal[] fields)
        {
            var structType = Target.GetBuiltinType(structTypeName);

            var span = new Span();
            span.WriteLine("{");
            foreach (var f in fields)
            {
                span.WriteLine("\t{0},", f);
            }
            span.Write("}");

            return new EmitValCPP(
                Target,
                span.ToString(),
                structType);
        }

        public void CallCOM(
            IEmitVal obj,
            string interfaceName,
            string methodName,
            params IEmitVal[] args)
        {
            _span.WriteLine("// CallCOM: {0}::{1}", interfaceName, methodName);

            _span.Write(
                "{0}->{1}(",
                obj,
                methodName);
            bool first = true;
            foreach (var a in args)
            {
                if (!first)
                    _span.Write(", ");
                first = false;
                _span.Write("{0}", a);
            }
            _span.WriteLine(");");
        }

        public IEmitVal BuiltinApp(
            IEmitType type,
            string template,
            IEmitVal[] args)
        {
            var valString = args == null ? template :
                string.Format(template, args);

            if (type == null || (type == Target.VoidType))
            {
                _span.WriteLine("{0};", valString);
                return null;
            }

            return new EmitValCPP(
                Target,
                valString,
                type);

        }
    }

    public class EmitValCPP : IEmitVal
    {
        public EmitValCPP(
            EmitTargetCPP target,
            string name,
            IEmitType type,
            bool isOwnAddress = false )
        {
            _target = target;
            _name = name;
            _type = type;
            _isOwnAddress = isOwnAddress;
        }

        public override string ToString()
        {
            return _name;
        }

        public IEmitVal GetAddress()
        {
            return new EmitValCPP(
                _target,
                _isOwnAddress ? _name : string.Format("(&({0}))", _name),
                _target.Pointer(_type));
        }

        public IEmitType Type
        {
            get { return _type; }
        }

        public bool IsOwnAddress
        {
            get { return _isOwnAddress; }
        }

        private EmitTargetCPP _target;
        private string _name;
        private IEmitType _type;
        private bool _isOwnAddress;
    }

}
