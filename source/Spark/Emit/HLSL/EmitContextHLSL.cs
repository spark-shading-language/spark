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

using Spark.Mid;

namespace Spark.Emit.HLSL
{
    public interface IHlslCompiler
    {
        byte[] Compile(
            string source,
            string entry,
            string profile,
            out string errors);
    }

    public static class HlslCompilerHelper
    {
        private static IHlslCompiler _compiler;

        public static void Register(IHlslCompiler compiler)
        {
            _compiler = compiler;
        }

        public static IHlslCompiler Get()
        {
            return _compiler;
        }
    }

    public interface ITypeHLSL
    {
        EmitValHLSL CreateVal(string name);
    }

    public abstract class EmitTypeHLSL : ITypeHLSL
    {
        public abstract EmitValHLSL CreateVal(string name);
    }

    public abstract class RealTypeHLSL : EmitTypeHLSL
    {
        public override EmitValHLSL CreateVal(string name)
        {
            return new SimpleValHLSL(name, this);
        }

        public abstract string DeclareVar(string name, string suffix = "");
    }

    public abstract class SimpleTypeHLSL : RealTypeHLSL
    {
        public SimpleTypeHLSL(
            string name)
        {
            _name = name;
        }

        public override string ToString()
        {
            return _name;
        }

        public override string DeclareVar(string name, string suffix)
        {
            return string.Format("{0} {1}{2}",
                _name,
                name,
                suffix);
        }

        private string _name;
    }

    public class ScalarTypeHLSL : SimpleTypeHLSL
    {
        public ScalarTypeHLSL(
            string name)
            : base(name)
        {}
    }

    public interface IAggTypeHLSL : ITypeHLSL
    {
        int GetFieldCount();
        string GetFieldName(int ii);
        ITypeHLSL GetFieldType(int ii);
    }

    public class ConnectorTypeHLSL : SimpleTypeHLSL, IAggTypeHLSL
    {
        public struct Field
        {
            public string Name;
            public ITypeHLSL Type;
        }

        public ConnectorTypeHLSL(
            string name,
            MidElementDecl element)
            : base(name)
        {
            _elementDecl = element;
        }

        public void AddField(
            string name,
            ITypeHLSL type)
        {
            _fields.Add(new Field { Name = name, Type = type });
        }

        public int GetFieldCount()
        {
            return _fields.Count;
        }

        public int GetFieldIndex(string name)
        {
            int ii = 0;
            foreach (var f in _fields)
            {
                if (f.Name == name)
                    return ii;
                ++ii;
            }
            return -1;
        }

        public string GetFieldName(int ii)
        {
            return _fields[ii].Name;
        }

        public ITypeHLSL GetFieldType(int ii)
        {
            return _fields[ii].Type;
        }

        public MidElementDecl ElementDecl
        {
            get { return _elementDecl; }
        }

        private MidElementDecl _elementDecl;
        private List<Field> _fields = new List<Field>();
    }

    public interface IArrayTypeHLSL : ITypeHLSL
    {
        ITypeHLSL ElementType { get; }
        EmitValHLSL ElementCount { get; }
    }

    public class ArrayTypeBaseHLSL : RealTypeHLSL, IArrayTypeHLSL
    {
        public ArrayTypeBaseHLSL(
            RealTypeHLSL elementType,
            EmitValHLSL elementCount)
        {
            _elementType = elementType;
            _elementCount = elementCount;
        }

        public RealTypeHLSL ElementType
        {
            get { return _elementType; }
        }

        ITypeHLSL IArrayTypeHLSL.ElementType
        {
            get { return _elementType; }
        }

        public EmitValHLSL ElementCount
        {
            get { return _elementCount; }
        }

        public override string DeclareVar(string name, string suffix)
        {
            return _elementType.DeclareVar(
                name,
                string.Format("[{0}]{1}", _elementCount, suffix));
        }

        private RealTypeHLSL _elementType;
        private EmitValHLSL _elementCount;
    }

    public class ArrayTypeHLSL : ArrayTypeBaseHLSL
    {
        public ArrayTypeHLSL(
            RealTypeHLSL elementType,
            EmitValHLSL elementCount)
            : base(elementType, elementCount)
        {}

        public override string ToString()
        {
            return string.Format("{0}[{1}]", ElementType, ElementCount);
        }
    }

    public class VoidTypeHLSL : EmitTypeHLSL
    {
        public VoidTypeHLSL()
        {
        }

        public override string ToString()
        {
            return "void";
        }

        public override EmitValHLSL CreateVal(string name)
        {
            return new VoidValHLSL(this);
        }
    }

    public class TupleTypeHLSL : EmitTypeHLSL, IAggTypeHLSL
    {
        public struct Field
        {
            public string Name;
            public ITypeHLSL Type;
        }

        public TupleTypeHLSL(
            string name )
        {
            _name = name;
        }

        public override string ToString()
        {
            return _name;
        }

        public string Name
        {
            get { return _name; }
        }

        public void AddField(
            string name,
            ITypeHLSL type)
        {
            _fields.Add(new Field { Name = name, Type = type });
        }

        public int GetFieldCount()
        {
            return _fields.Count;
        }

        public int GetFieldIndex(string name)
        {
            int ii = 0;
            foreach (var f in _fields)
            {
                if (f.Name == name)
                    return ii;
                ++ii;
            }
            return -1;
        }

        public string GetFieldName(int ii)
        {
            return _fields[ii].Name;
        }

        public ITypeHLSL GetFieldType(int ii)
        {
            return _fields[ii].Type;
        }

        private string _name;
        private List<Field> _fields = new List<Field>();

        public override EmitValHLSL CreateVal(string name)
        {
            int fieldCount = GetFieldCount();
            EmitValHLSL[] fieldVals = new EmitValHLSL[fieldCount];
            for (int ff = 0; ff < fieldCount; ++ff)
            {
                fieldVals[ff] = GetFieldType(ff).CreateVal(name + GetFieldName(ff));
            }
            return new TupleValHLSL(this, fieldVals);
        }
    }

    public class PseudoArrayTypeHLSL : EmitTypeHLSL
    {
        private ITypeHLSL _elementType;
        private EmitValHLSL _elementCount;

        public PseudoArrayTypeHLSL(
            ITypeHLSL elementType,
            EmitValHLSL elementCount)
        {
            _elementType = elementType;
            _elementCount = elementCount;
        }

        public override string ToString()
        {
            return string.Format("{0}[|{1}|]", ElementType, ElementCount);
        }

        public override EmitValHLSL CreateVal(string name)
        {
            return new PseudoArrayValHLSL(
                this,
                _elementType.CreateVal(name));
        }

        public ITypeHLSL ElementType { get { return _elementType; } }
        public EmitValHLSL ElementCount { get { return _elementCount; } }
    }

    public class PseudoArrayValHLSL : EmitValHLSL
    {
        public PseudoArrayValHLSL(
            PseudoArrayTypeHLSL type,
            EmitValHLSL innerVal)
            : base(type)
        {
            _innerVal = innerVal;
        }

        public EmitValHLSL InnerVal
        {
            get { return _innerVal; }
        }

        private EmitValHLSL _innerVal;
    }

    public class PseudoArrayElemTypeHLSL : ArrayTypeBaseHLSL
    {
        public PseudoArrayElemTypeHLSL(
            RealTypeHLSL elementType,
            EmitValHLSL elementCount)
            : base(elementType, elementCount)
        { }
    }

    //

    public abstract class EmitValHLSL
    {
        public EmitValHLSL(
            ITypeHLSL type)
        {
            _type = type;
        }

        public ITypeHLSL Type { get { return _type; } }

        private ITypeHLSL _type;
    }

    public class SimpleValHLSL : EmitValHLSL
    {
        public SimpleValHLSL(
            string name,
            RealTypeHLSL type)
            : base(type)
        {
            _name = name;
        }

        public string Name
        {
            get { return _name; }
        }

        public override string ToString()
        {
            return _name;
        }

        public RealTypeHLSL RealType
        {
            get { return (RealTypeHLSL)Type; }
        }

        string _name;
    }

    public class LitHLSL : SimpleValHLSL
    {
        public LitHLSL(
            RealTypeHLSL type,
            string val)
            : base(val, type)
        {
        }
    }

    public class VoidValHLSL : EmitValHLSL
    {
        public VoidValHLSL(
            VoidTypeHLSL type)
            : base(type)
        {
        }
    }

    public class TupleValHLSL : EmitValHLSL
    {
        public TupleValHLSL(
            IAggTypeHLSL type,
            IEnumerable<EmitValHLSL> fieldVals)
            : base(type)
        {
            _fieldVals = fieldVals.ToArray();
        }

        public override string ToString()
        {
            return string.Format("{{{0}}}",
                (from f in _fieldVals
                 where !(f is VoidValHLSL)
                 select f.ToString()).Separate(", ").Concat());
        }

        public IEnumerable<EmitValHLSL> FieldVals
        {
            get { return _fieldVals; }
        }

        public int GetFieldCount() { return _fieldVals.Length; }

        public EmitValHLSL GetFieldVal(int ii)
        {
            return _fieldVals[ii];
        }

        public IAggTypeHLSL AggType
        {
            get { return (IAggTypeHLSL)Type; }
        }

        EmitValHLSL[] _fieldVals;
    }

    public class ArrayValHLSL : EmitValHLSL
    {
        public ArrayValHLSL(
            string name,
            IArrayTypeHLSL type)
            : base(type)
        {
            _name = name;
        }

        public string Name
        {
            get { return _name; }
        }

        public override string ToString()
        {
            return _name;
        }

        public IArrayTypeHLSL ArrType
        {
            get { return (IArrayTypeHLSL)Type; }
        }

        string _name;
    }


    //

    public class ErrorTypeHLSL : EmitTypeHLSL
    {
        public ErrorTypeHLSL()
        {
        }

        public override string ToString()
        {
            return "<error>";
        }

        public override EmitValHLSL CreateVal(string name)
        {
            return new ErrorValHLSL();
        }
    }

    public class ErrorValHLSL : EmitValHLSL
    {
        public ErrorValHLSL()
            : base(new ErrorTypeHLSL())
        {
        }

        public override string ToString()
        {
            return "<error>";
        }
    }

    //

    public class SharedContextHLSL
    {
        private IdentifierFactory _identifiers;
        private IDiagnosticsCollection _diagnostics;
        private Dictionary<object, string> _mapName = new Dictionary<object, string>();
        private HashSet<string> _names = new HashSet<string>();

        public struct UniformInfo
        {
            public string Name;
            public MidVal Val;
            public int Slot;
            public int ByteOffset;
        }

        public IEnumerable<UniformInfo> Uniforms { get { return _uniforms; } }
        public int ConstantBufferSize { get { return _totalUniformSlots * 16; } }

        private List<UniformInfo> _uniforms = new List<UniformInfo>();
        private int _totalUniformSlots;

        public SharedContextHLSL(
            IdentifierFactory identifiers,
            IDiagnosticsCollection diagnostics)
        {
            _identifiers = identifiers;
            _diagnostics = diagnostics;
        }

        public IDiagnosticsCollection Diagnostics { get { return _diagnostics; } }

        public string MapName(MidAttributeDecl decl)
        {
            return MapNameImpl(decl, string.Format("a_{0}_", decl.Name));
        }

        private string MapNameImpl(object decl, string baseName)
        {
            string result;
            if (_mapName.TryGetValue(decl, out result))
                return result;

            result = GenerateName(baseName);
            _mapName[decl] = result;
            return result;
        }

        public string GenerateName(string baseName)
        {
            var name = NormalizeName(baseName);

            if (!_names.Contains(name))
            {
                _names.Add(name);
                return name;
            }

            int id = 0;
            while (true)
            {
                var newName = string.Format("{0}_{1}", name, id);
                if (!_names.Contains(newName))
                {
                    _names.Add(newName);
                    return newName;
                }

                id++;
            }
        }

        public static string NormalizeName(string name)
        {
            return new string((from c in name
                               select Char.IsLetterOrDigit(c) ? c : '_').ToArray());
        }

        public string EmitUniformRef(
    MidVal uniformVal)
        {
            if (uniformVal is MidAttributeRef)
            {
                var attrDecl = ((MidAttributeRef)uniformVal).Decl;

                return _uniformAttributes.Cache(attrDecl,
                    () => CreateUniform(attrDecl.Name.ToString(), uniformVal));
            }

            return CreateUniform("u_", uniformVal);
        }

        private Dictionary<MidAttributeDecl, string> _uniformAttributes = new Dictionary<MidAttributeDecl, string>();

        public string CreateUniform(
            string baseName,
            MidVal uniformVal)
        {
            var name = GenerateName(baseName);

            int slotSize = 16;

            int valSlot = _totalUniformSlots;
            int valSlotCount = CountSlots(uniformVal.Type);

            _totalUniformSlots += valSlotCount;

            _uniforms.Add(new UniformInfo
            {
                Name = name,
                Val = uniformVal,
                Slot = valSlot,
                ByteOffset = valSlot * slotSize,
            });

            return name;
        }

        private int CountSlots(MidType type)
        {
            return CountSlotsImpl((dynamic)type);
        }

        private int CountSlotsImpl(MidBuiltinType builtin)
        {
            switch (builtin.Name)
            {
                case "float": return 1;
                case "float3": return 1;
                case "float4": return 1;
                case "float4x4": return 4;
                case "Array":
                    {
                        var args = builtin.Args.ToArray();
                        var elementType = ((MidType)args[0]);
                        var elementTypeSlots = CountSlots(elementType);
                        var elementCountVal = ((MidVal)args[1]);

                        var elementCount = GetIntLit(elementCountVal);

                        return elementCount * elementTypeSlots;
                    }
                default:
                    throw new NotImplementedException();
            }
        }

        private int GetIntLit(MidExp exp)
        {
            if (exp is MidLit)
                return ((MidLit<Int32>)exp).Value;
            else if (exp is MidAttributeRef)
                return GetIntLit(((MidAttributeRef)exp).Decl.Exp);
            else
                throw new NotImplementedException();
        }
    }

    public class EmitContextHLSL
    {
        private SharedContextHLSL _shared;
        private SourceRange _defaultRange;
        private string _shaderClassName;

        private Span _span;
        private Span _typeHeaderSpan;
        private Span _resourceHeaderSpan;
        private Span _cbHeaderSpan;
        private Span _subroutineHeaderSpan;
        private Span _entryPointSpan;

        private Dictionary<MidAttributeDecl, EmitValHLSL> _attrVals = new Dictionary<MidAttributeDecl, EmitValHLSL>();
        private Dictionary<MidVar, EmitValHLSL> _varVals = new Dictionary<MidVar, EmitValHLSL>();

        private List<MidVal> _shaderResources = new List<MidVal>();
        private List<MidVal> _samplerStates = new List<MidVal>();

        private VoidValHLSL _voidVal = new VoidValHLSL(new VoidTypeHLSL());

        public EmitContextHLSL(
            SharedContextHLSL shared,
            SourceRange defaultRange,
            string shaderClassName )
        {
            _shared = shared;
            _defaultRange = defaultRange;
            _shaderClassName = shaderClassName;

            _span = new Span();
            var subSpan = NoteRange(_span, _defaultRange);
            var builtinsSpan = subSpan.InsertSpan();
            _typeHeaderSpan = subSpan.InsertSpan();
            _resourceHeaderSpan = subSpan.InsertSpan();
            _cbHeaderSpan = subSpan.InsertSpan();
            _subroutineHeaderSpan = subSpan.InsertSpan();
            _entryPointSpan = subSpan.InsertSpan();

            builtinsSpan.WriteLine("struct Range { int lower; int upper; };");
            builtinsSpan.WriteLine("Range __Range( int lower, int upper ) { Range result; result.lower=lower; result.upper=upper; return result; }");
        }

        public IDiagnosticsSink Diagnostics { get { return _shared.Diagnostics; } }

        public Span Span { get { return _span; } }

        public Span EntryPointSpan { get { return _entryPointSpan; } }
        public Span TypeHeaderSpan { get { return _typeHeaderSpan; } }
        public Span SubroutineHeaderSpan { get { return _subroutineHeaderSpan; } }

        public IEnumerable<MidVal> ShaderResources { get { return _shaderResources; } }
        public IEnumerable<MidVal> SamplerStates { get { return _samplerStates; } }

        public VoidValHLSL VoidVal { get { return _voidVal; } }

        //

        public string MapName(MidAttributeDecl decl)
        {
            return _shared.MapName(decl);
        }

        // Emit an attribute that *should* have a literal value

        public string EmitAttrLit(
            MidAttributeWrapperDecl wrapper)
        {
            return EmitAttrLit(wrapper.Attribute);
        }

        public string EmitAttrLit(
            MidAttributeDecl attr)
        {
            if (attr.Exp == null)
            {
                throw new NotImplementedException();
            }

            var init = attr.Exp;
            if (init == null)
            {
                throw new NotImplementedException();
            }

            return EmitAttrLit(init);
        }

        private string EmitAttrLit(
            MidExp exp )
        {
            return EmitAttrLitImpl((dynamic)exp);
        }

        private string EmitAttrLitImpl(
            MidLit exp )
        {
            return EmitVal(exp, null).ToString();
        }

        private string EmitAttrLitImpl(
            MidAttributeRef exp )
        {
            EmitValHLSL val;
            if( _attrVals.TryGetValue(exp.Decl, out val) )
                return val.ToString();

            throw new NotImplementedException();
        }

        // Bind an attribute to a given value

        public void BindAttr(
            MidAttributeWrapperDecl wrapper,
            EmitValHLSL val)
        {
            BindAttr(wrapper.Attribute, val);
        }

        public void BindAttr(
            MidAttributeDecl attr,
            EmitValHLSL val)
        {
            _attrVals[attr] = val;
        }

        // \todo: This is hacky and should be removed
        public void UnbindAttribute(
            MidAttributeDecl attribute)
        {
            _attrVals.Remove(attribute);
        }


        public void BindAttrLit(
            MidAttributeWrapperDecl attr,
            string lit)
        {
            var type = EmitType(attr.Type);
            if (!(type is RealTypeHLSL))
            {
                throw new NotImplementedException();
            }
            var val = new LitHLSL((RealTypeHLSL)type, lit);
            BindAttr(attr, val);
        }

        // Declare function parameters
        private void DeclareParam(
            EmitValHLSL val,
            string prefix,
            string semantic,
            ref bool first,
            Span span)
        {
            var decls = DeclareBase(
                val,
                prefix,
                semantic,
                "");

            foreach (var d in decls)
            {
                if (!first)
                    span.WriteLine(",");
                first = false;
                span.Write(d);
            }
        }

        private EmitValHLSL DeclareParam(
            string name,
            ITypeHLSL type,
            Span span,
            ref bool first,
            string semantic = null,
            string prefix = "")
        {
            var val = type.CreateVal(name);

            DeclareParam(
                val,
                prefix,
                semantic,
                ref first,
                span );

            return val;
        }

        private EmitValHLSL DeclareOutParam(
            string name,
            ITypeHLSL type,
            Span span,
            ref bool first,
            string semantic = null)
        {
            var val = type.CreateVal(name);

            DeclareParam(
                val,
                "out ",
                semantic,
                ref first,
                span);

            return val;
        }


        // Declare a parameter that binds to a given attribute
        // (usually an 'input' attribute for the shader stage)
        public void DeclareParamAndBind(
            MidAttributeWrapperDecl attr,
            string semantic,
            ref bool firstParam,
            Span span,
            string prefix = "")
        {
            DeclareParamAndBind(
                attr,
                EmitType(attr.Type),
                semantic,
                ref firstParam,
                span,
                prefix);
        }

        public void DeclareParamAndBind(
            MidAttributeWrapperDecl attr,
            ITypeHLSL attrType,
            string semantic,
            ref bool firstParam,
            Span span,
            string prefix = "")
        {
            var attrName = attr.Name.ToString();

            // \todo: Insist that the type
            // is a "real" type (rather than
            // a synthetic one that might be zero or more
            // real parameters...)

            var attrVal = DeclareParam(
                attrName,
                attrType,
                span,
                ref firstParam,
                semantic == null ? "" : ": " + semantic,
                prefix: prefix);

            BindAttr(attr, attrVal);
        }


        public void DeclareConnectorAndBind(
            MidElementDecl element,
            MidAttributeWrapperDecl attr,
            ref bool firstParam,
            Span span)
        {
            var attrType = GenerateConnectorType(element);
            DeclareParamAndBind(
                attr,
                attrType,
                null,
                ref firstParam,
                span );
        }

        // Declaration helpers

        public void DeclareFields(
            ITypeHLSL type,
            Span span,
            string name,
            string prefix = "",
            string semantic = null,
            string suffix = "")
        {
            var val = type.CreateVal(name);

            var decls = DeclareBase(
                val,
                prefix,
                semantic,
                suffix + ";").ToArray();

            foreach (var d in decls)
            {
                span.WriteLine(d);
            }
        }

        public IEnumerable<string> DeclareBase(
            EmitValHLSL val,
            string prefix,
            string semantic,
            string suffix)
        {
            return DeclareBaseImpl(
                (dynamic) val,
                prefix,
                semantic,
                suffix);
        }

        public IEnumerable<string> DeclareBaseImpl(
            VoidValHLSL val,
            string prefix,
            string semantic,
            string suffix)
        {
            return new string[] { };
        }

        public IEnumerable<string> DeclareBaseImpl(
            SimpleValHLSL val,
            string prefix,
            string semantic,
            string suffix)
        {
            yield return string.Format("{0} {1}{2}{3}",
                prefix,
                val.RealType.DeclareVar(val.Name),
                semantic,
                suffix);
        }

        public IEnumerable<string> DeclareBaseImpl(
            TupleValHLSL val,
            string prefix,
            string semantic,
            string suffix)
        {
            int fieldCount = val.GetFieldCount();
            for (int ff = 0; ff < fieldCount; ++ff)
            {
                var decls = DeclareBase(
                    val.GetFieldVal(ff),
                    prefix,
                    semantic == null ? null : semantic + val.AggType.GetFieldName(ff),
                    suffix).Eager();

                foreach (var d in decls)
                    yield return d;
            }
        }

        public IEnumerable<string> DeclareBaseImpl(
            PseudoArrayValHLSL val,
            string prefix,
            string semantic,
            string suffix)
        {
            return DeclareBase(
                val.InnerVal,
                prefix,
                semantic,
                suffix);
        }

        // Emit types

        public ITypeHLSL EmitType(MidType type)
        {
            return EmitTypeImpl((dynamic)type);
        }

        public IAggTypeHLSL  EmitType(MidElementDecl element)
        {
            IAggTypeHLSL result;
            if (_elementTypes.TryGetValue(element, out result))
                return result;

            // Actually emit the type, then...
            var name = string.Format("Tup_{0}", element.Name.ToString());
            TupleTypeHLSL tupleType = new TupleTypeHLSL(name);
            _elementTypes[element] = tupleType;

            foreach (var a in element.Attributes)
            {
                if (!a.IsOutput) continue;

                var attrName = MapName(a);

                tupleType.AddField(
                    attrName,
                    EmitType(a.Type));
            }

            return tupleType;
        }
        private Dictionary<MidElementDecl, IAggTypeHLSL> _elementTypes = new Dictionary<MidElementDecl, IAggTypeHLSL>();

        public EmitTypeHLSL EmitType(MidStructDecl structDecl)
        {
            throw new NotImplementedException();
            /*
            EmitTypeHLSL result;
            if (_structs.TryGetValue(structDecl, out result))
                return result;

            // Actually emit the type, then...
            var name = structDecl.Name.ToString();
            var structType = new StructTypeHLSL(name, null);
            result = structType;
            _structs[structDecl] = result;

            foreach (var f in structDecl.Fields)
            {
                var fName = f.Name.ToString();

                structType.AddField(
                    fName,
                    EmitType(f.Type));
            }

            return result;
             * */
        }
        private Dictionary<MidStructDecl, EmitTypeHLSL> _structs = new Dictionary<MidStructDecl, EmitTypeHLSL>();

        private ITypeHLSL EmitTypeImpl(MidElementType element)
        {
            return EmitType(element.Decl);
        }

        public ITypeHLSL EmitTypeImpl(MidVoidType midType)
        {
            return VoidVal.Type;
        }

        public ITypeHLSL EmitTypeImpl(MidStructRef structRef)
        {
            return EmitType(structRef.Decl);
        }

        public ITypeHLSL MakeArrayType(
            ITypeHLSL elementType,
            EmitValHLSL elementCount)
        {
            if (elementType is RealTypeHLSL)
            {
                return new ArrayTypeHLSL(
                    (RealTypeHLSL)elementType,
                    elementCount);
            }
            else
            {
                return new PseudoArrayTypeHLSL(
                    MakePseudoArrayElemType(elementType, elementCount),
                    elementCount);
            }
        }

        private ITypeHLSL MakePseudoArrayElemType(
            ITypeHLSL type,
            EmitValHLSL count)
        {
            return MakePseudoArrayElemTypeImpl(
                (dynamic)type,
                count);
        }

        private ITypeHLSL MakePseudoArrayElemTypeImpl(
            RealTypeHLSL type,
            EmitValHLSL count)
        {
            return new PseudoArrayElemTypeHLSL(type, count);
        }

        private ITypeHLSL MakePseudoArrayElemTypeImpl(
            TupleTypeHLSL type,
            EmitValHLSL count )
        {
            int fieldCount = type.GetFieldCount();
            TupleTypeHLSL result = new TupleTypeHLSL(type.Name);
            for (int ff = 0; ff < fieldCount; ++ff)
            {
                result.AddField(
                    type.GetFieldName(ff),
                    MakePseudoArrayElemType(
                        type.GetFieldType(ff),
                        count));
            }
            return result;
        }

        private ITypeHLSL MakePseudoArrayElemTypeImpl(
            PseudoArrayTypeHLSL type,
            EmitValHLSL count)
        {
            return new PseudoArrayTypeHLSL(
                MakePseudoArrayElemType(
                    type.ElementType,
                    count),
                type.ElementCount);
        }

        private ITypeHLSL EmitTypeImpl(MidBuiltinType type)
        {
            var template = type.GetTemplate("hlsl");
            if (template == null)
            {
                Diagnostics.Add(
                    Severity.Error,
                    new SourceRange(),
                    "No HLSL equivalent for type {0}", type);
                return new ErrorTypeHLSL();
            }
            if (type.Args == null)
                return new ScalarTypeHLSL(template);

            var args = (from a in type.Args
                        select EmitGenericArg(a)).Eager();

            // I hate hacks... :(
            if (template == "__Array")
            {
                var baseType = (EmitTypeHLSL)args[0];
                var count = (EmitValHLSL)args[1];

                return MakeArrayType(
                    baseType,
                    count);
            }

            return new ScalarTypeHLSL(string.Format(template, args));
        }

        private object EmitGenericArg(object arg)
        {
            if (arg is MidType)
                return EmitType((MidType)arg);
            else
                return EmitExp((MidExp)arg, null);
        }


        // Generate "connector" struct type for a particular stage
        public ConnectorTypeHLSL GenerateConnectorType(MidElementDecl element)
        {
            if (_elementTypes.ContainsKey(element) && !_connectorTypes.ContainsKey(element))
            {
                throw new NotImplementedException();
            }

            ConnectorTypeHLSL result;
            if (_connectorTypes.TryGetValue(element, out result))
                return result;

            // Actually emit the type, then...
            var name = string.Format("T_{0}", element.Name.ToString());
            result = new ConnectorTypeHLSL(name, element);
            _connectorTypes[element] = result;

            _elementTypes[element] = result;

            var span = new Span();

            span.WriteLine("struct {0}", result);
            span.WriteLine("{");
            var memberSpan = span.IndentSpan();
            foreach (var a in element.Attributes)
            {
                // Only output attributes go in the connector
                if (!a.IsOutput) continue;

                var attrName = MapName(a);
                var attrType = EmitType(a.Type);


                var rawName = a.Name.ToString();
                string semantic = "";
                switch (rawName)
                {
                    case "HS_EdgeFactors":
                        semantic = ": SV_TessFactor";
                        break;
                    case "HS_InsideFactors":
                        semantic = ": SV_InsideTessFactor";
                        break;
                    case "__RS_Position":
                        semantic = ": SV_Position";
                        break;
                    case "__RS_RenderTargetArrayIndex":
                        semantic = ": SV_RenderTargetArrayIndex";
                        break;

                    default:
                        semantic = string.Format(" : USER_{0}", attrName);
                        break;
                }

                var attrRep = DeclareConnectorFields(
                    attrType,
                    attrName,
                    semantic,
                    memberSpan.IndentSpan() );

                result.AddField(
                    attrName,
                    attrRep);
            }
            span.WriteLine("};");
            span.WriteLine();

            _typeHeaderSpan.Add(span);

            return result;
        }
        private Dictionary<MidElementDecl, ConnectorTypeHLSL> _connectorTypes = new Dictionary<MidElementDecl, ConnectorTypeHLSL>();

        private ITypeHLSL DeclareConnectorFields(
            ITypeHLSL rep,
            string name,
            string semantic,
            Span span,
            EmitValHLSL[] arrayDims = null)
        {
            DeclareFields(
                rep,
                span,
                name,
                semantic: semantic);
            return rep;
        }

        public void DeclareAndInitLocal(
            EmitValHLSL local,
            EmitValHLSL init,
            Span span)
        {
            DeclareAndInitLocalImpl(
                (dynamic)local,
                (dynamic)init,
                span);
        }

        public void DeclareAndInitLocalImpl(
            VoidValHLSL local,
            VoidValHLSL init,
            Span span)
        {
        }

        public void DeclareAndInitLocalImpl(
            SimpleValHLSL local,
            SimpleValHLSL init,
            Span span)
        {
            span.WriteLine("{0} = {1};",
                local.RealType.DeclareVar(local.Name),
                init);
        }

        public void DeclareAndInitLocalImpl(
            PseudoArrayValHLSL local,
            PseudoArrayValHLSL init,
            Span span)
        {
            DeclareAndInitLocal(
                local.InnerVal,
                init.InnerVal,
                span);
        }

        public void DeclareAndInitLocalImpl(
            SimpleValHLSL local,
            LitHLSL init,
            Span span)
        {
            span.WriteLine("{0} = {1};",
                local.RealType.DeclareVar(local.Name),
                init);
        }

        public void DeclareAndInitLocalImpl(
            SimpleValHLSL local,
            TupleValHLSL init,
            Span span)
        {
            // If the record has *no* non-void fields, then
            // we shouldn't initialize
            if (!init.FieldVals.Any((fv) => !(fv is VoidValHLSL)))
            {
                span.WriteLine("{0};",
                    local.RealType.DeclareVar(local.Name));
                return;
            }

            span.WriteLine("{0} = {1};",
                local.RealType.DeclareVar(local.Name),
                init);
        }

        public void DeclareAndInitLocalImpl(
            TupleValHLSL local,
            TupleValHLSL init,
            Span span)
        {
            int fieldCount = local.GetFieldCount();
            if (fieldCount != init.GetFieldCount())
            {
                Diagnostics.Add(
                    Severity.Error,
                    new SourceRange(),
                    "Mismatch between initializer and variable in HLSL emit!");
                return;
            }

            for (int ii = 0; ii < fieldCount; ++ii)
            {
                DeclareAndInitLocal(
                    local.GetFieldVal(ii),
                    init.GetFieldVal(ii),
                    span);
            }
        }

        public void DeclareAndInitLocalImpl(
            TupleValHLSL local,
            EmitValHLSL init,
            Span span)
        {
            int fieldCount = local.GetFieldCount();

            var aggType = local.AggType;

            for (int ii = 0; ii < fieldCount; ++ii)
            {
                var fieldInit = GetField(
                    init,
                    aggType.GetFieldType(ii),
                    aggType.GetFieldName(ii),
                    ii,
                    span);

                DeclareAndInitLocal(
                    local.GetFieldVal(ii),
                    fieldInit,
                    span);
            }
        }

        public void Assign(
            EmitValHLSL dst,
            EmitValHLSL src,
            Span span)
        {
            AssignImpl(
                (dynamic)dst,
                (dynamic)src,
                span);
        }

        private void AssignImpl(
            SimpleValHLSL dest,
            SimpleValHLSL src,
            Span span)
        {
            span.WriteLine("{0} = {1};",
                dest,
                src);
        }

        private void AssignImpl(
            TupleValHLSL dest,
            TupleValHLSL src,
            Span span)
        {
            int fieldCount = src.GetFieldCount();

            for (int ii = 0; ii < fieldCount; ++ii)
            {
                Assign(
                    dest.GetFieldVal(ii),
                    src.GetFieldVal(ii),
                    span);
            }
        }

        private void AssignImpl(
            SimpleValHLSL dest,
            TupleValHLSL src,
            Span span)
        {
            int fieldCount = src.GetFieldCount();
            var aggType = src.AggType;

            for (int ii = 0; ii < fieldCount; ++ii)
            {
                var destField = GetField(
                    dest,
                    aggType.GetFieldType(ii),
                    aggType.GetFieldName(ii),
                    ii,
                    span);
                Assign(
                    destField,
                    src.GetFieldVal(ii),
                    span);
            }
        }

        private void AssignImpl(
            PseudoArrayValHLSL dest,
            PseudoArrayValHLSL src,
            Span span)
        {
            Assign(
                dest.InnerVal,
                src.InnerVal,
                span);
        }


        // Expressions

        public void PreEmitExp(
            MidExp exp,
            Span span)
        {
            PreEmitExpImpl((dynamic)exp, span);
        }

        public void PreEmitExpImpl(
            MidAttributeRef exp,
            Span span)
        {
            // Ensure that we've already emitted the attribute
            EmitAttribRef(exp.Decl, span);
        }

        public void PreEmitExpImpl(
            MidLetExp exp,
            Span span)
        {
            PreEmitExp(exp.Exp, span);
            PreEmitExp(exp.Body, span);
        }

        public void PreEmitExpImpl(
            MidAssignExp exp,
            Span span)
        {
            PreEmitExp(exp.Dest, span);
            PreEmitExp(exp.Src, span);
        }

        public void PreEmitExpImpl(
            MidForExp exp,
            Span span)
        {
            PreEmitExp(exp.Seq, span);
            PreEmitExp(exp.Body, span);
        }

        public void PreEmitExpImpl(
            MidElementCtorApp app,
            Span span)
        {
            foreach (var a in app.Args)
                PreEmitExp(a.Val, span);
        }

        public void PreEmitExpImpl(
            MidMethodApp app,
            Span span)
        {
            foreach (var a in app.Args)
                PreEmitExp(a, span);
        }

        public void PreEmitExpImpl(
            MidBuiltinApp app,
            Span span)
        {
            var template = app.Decl.GetTemplate("hlsl");

            // Ugly special cases for @Uniform and @Constant parameters
            // \todo: Get rid of these by handling these references
            // on-demand...
            switch (template)
            {
                case "__ConstantRef":
                    return;
                case "__UniformRef":
                    return;
                default:
                    break;
            }

            foreach (var a in app.Args)
            {
                PreEmitExp(a, span);
            }
        }

        public void PreEmitExpImpl(
            MidAttributeFetch exp,
            Span span)
        {
            PreEmitExp(exp.Obj, span);
        }

        public void PreEmitExpImpl(
            MidVal exp,
            Span span)
        {
        }

        public EmitValHLSL EmitExp(
            MidExp exp,
            Span span)
        {
            if (exp is MidVal)
                return EmitVal((MidVal)exp, span);

            EmitValHLSL expVal = EmitExpRaw(exp, span);

            if (exp is MidLetExp)
                return expVal;

            if (exp.Type == null)
            {
                throw new NotImplementedException();
            }

            if (exp.Type is MidVoidType)
            {
                return VoidVal;
            }
            if (expVal is VoidValHLSL)
            {
                return expVal;
            }

            // \todo: Why not use the type of the result?
            var expType = EmitType(exp.Type);
            var temp = expType.CreateVal(
                _shared.GenerateName("_t"));

            DeclareAndInitLocal(
                temp,
                expVal,
                span);

            return temp;
        }

        private EmitValHLSL EmitExpRaw(MidExp exp, Span span)
        {
            return EmitExpImpl((dynamic)exp, span);
        }

        private EmitValHLSL EmitExpImpl(MidIfExp exp, Span span)
        {
            var condition = EmitExp(exp.Condition, span);

            bool isVoid = exp.Type is MidVoidType;

            ITypeHLSL expType = VoidVal.Type;
            EmitValHLSL temp = VoidVal;
            if (!isVoid)
            {
                expType = EmitType(exp.Type);
                temp = expType.CreateVal(
                    _shared.GenerateName("_if"));
                DeclareLocal(temp, span);
            }

            span.WriteLine("if ({0})", condition);
            span.WriteLine("{");
            var thenVal = EmitExp(exp.Then, span.IndentSpan());
            if (!isVoid)
            {
                Assign(temp, thenVal, span.IndentSpan());
            }
            span.WriteLine("}");
            span.WriteLine("else");
            span.WriteLine("{");
            var elseVal = EmitExp(exp.Else, span.IndentSpan());
            if (!isVoid)
            {
                Assign(temp, elseVal, span.IndentSpan());
            }
            span.WriteLine("}");

            return temp;
        }

        private EmitValHLSL EmitExpImpl(MidSwitchExp exp, Span span)
        {
            var value = EmitExp(exp.Value, span);

            span.WriteLine("switch ({0})", value);
            span.WriteLine("{");
            foreach (var c in exp.Cases)
            {
                var cVal = EmitVal(c.Value, span);
                span.WriteLine("case {0}:", cVal);
                var cBody = EmitExp(c.Body, span.IndentSpan());
                span.WriteLine("\tbreak;", cVal);
            }
            span.WriteLine("}");
            return VoidVal;
        }

        private abstract class Label
        {
            public abstract void Emit(
                EmitValHLSL val,
                Span span);
        }

        private class ReturnLabel : Label
        {
            public ReturnLabel(
                EmitContextHLSL context,
                EmitValHLSL resultVar)
            {
                _context = context;
                _resultVar = resultVar;
            }

            public override void Emit(
                EmitValHLSL val,
                Span span)
            {
                _context.Assign(
                    _resultVar,
                    val,
                    span);
                span.WriteLine("return;");
            }

            private EmitContextHLSL _context;
            private EmitValHLSL _resultVar;
        }

        private Dictionary<MidLabel, Label> _labels = new Dictionary<MidLabel, Label>();

        private EmitValHLSL EmitExpImpl(MidBreakExp exp, Span span)
        {
            var value = EmitVal(exp.Value, span);

            Label label;
            if (_labels.TryGetValue(exp.Label, out label))
            {
                label.Emit(value, span);
                return VoidVal;
            }

            throw new NotImplementedException();
        }

        private EmitValHLSL EmitExpImpl(MidLabelExp exp, Span span)
        {
            if (_labels.ContainsKey(exp.Label))
            {
                return EmitExp(exp.Body, span);
            }

            Diagnostics.Add(
                Severity.Error,
                new SourceRange(),
                "Control flow too complex for HLSL emit!");
            return new ErrorValHLSL();
        }

        private EmitValHLSL EmitExpImpl(MidAssignExp exp, Span span)
        {
            var dest = EmitExpRaw(exp.Dest, span);
            var src = EmitExp(exp.Src, span);

            Assign(dest, src, span);
            return VoidVal;
        }

        private EmitValHLSL EmitExpImpl(MidForExp exp, Span span)
        {
            var seq = EmitExp(exp.Seq, span);

            var counterType = new ScalarTypeHLSL("int");
            var counterName = _shared.GenerateName("ii");
            var counter = counterType.CreateVal(counterName);

            _varVals[exp.Var] = counter;
            span.WriteLine("for(int {0} = ({1}).lower; ({0}) < ({1}).upper; ++({0}))", counterName, seq);
            span.WriteLine("{");
            var ignored = EmitExp(exp.Body, span.IndentSpan());
            span.WriteLine("}");
            return VoidVal;
        }

        private EmitValHLSL EmitExpImpl(MidElementCtorApp val, Span span)
        {
            var ctorInfo = GetElementCtor(val.Element);

            var elemType = EmitType(val.Element);
            var elemVal = elemType.CreateVal(_shared.GenerateName("e_"));

            DeclareLocal(elemVal, span);

            var sb = new Span();
            sb.Write("{0}(", ctorInfo.Name);
            bool first = true;

            AddArgs(elemVal, ref first, sb);

            foreach (var param in ctorInfo.InputAttributes)
            {
                var arg = (from a in val.Args
                           where a.Attribute == param
                           select a.Val).First();

                AddArgs(
                    EmitVal(arg, span),
                    ref first,
                    sb);
            }
            sb.WriteLine(");");

            span.Add(sb);

            return elemVal;
        }

        private EmitValHLSL EmitExpImpl(MidVal val, Span span)
        {
            return EmitVal(val, span);
        }

        private EmitValHLSL EmitExpImpl(MidFieldRef fieldRef, Span span)
        {
            var obj = EmitExp(fieldRef.Obj, span);

            var fieldDecl = fieldRef.Decl;
            var fieldName = fieldDecl.Name.ToString();
            var fieldType = EmitType(fieldDecl.Type);
            var fieldIndex = -1;

            var aggType = (IAggTypeHLSL) obj.Type;
            int fieldCount = aggType.GetFieldCount();
            for (int ff = 0; ff < fieldCount; ++ff)
            {
                if (aggType.GetFieldName(ff) != fieldName)
                    continue;

                fieldIndex = ff;
                break;
            }

            return GetField(
                obj,
                fieldType,
                fieldName,
                fieldIndex,
                span );
        }

        public string GetMethod(MidMethodDecl method)
        {
            string result;
            if (_methods.TryGetValue(method, out result))
                return result;

            var name = _shared.GenerateName(method.Name.ToString());
            result = name;
            _methods[method] = result;

            Span span = new Span();


            span.WriteLine("void {0}(",
                name);

            bool firstParam = true;

            var resultType = EmitType(method.ResultType);
            var resultVar = DeclareOutParam(
                "__result",
                resultType,
                span,
                ref firstParam);

            foreach (var p in method.Parameters)
            {
                var pName = _shared.GenerateName(p.Name.ToString());

                var pType = EmitType(p.Type);
                var pVal = DeclareParam(
                    pName,
                    pType,
                    span,
                    ref firstParam);

                _varVals[p] = pVal;
            }

            span.WriteLine(")");
            span.WriteLine("{");

            var bodySpan = span.IndentSpan();

            // Scan for a label expression
            // along the 'spine' of the method

            var bodyExp = method.Body;
            while (bodyExp is MidLetExp)
            {
                bodyExp = ((MidLetExp)bodyExp).Body;
            }

            if (bodyExp is MidLabelExp)
            {
                var labelExp = (MidLabelExp)bodyExp;

                // a 'break' to this label should be
                // emitted as a 'return':

                _labels[labelExp.Label] = new ReturnLabel(this, resultVar);

                EmitExp(method.Body, bodySpan);
            }
            else
            {
                // Otherwise, the body is an expression,
                // and we should return it if it is non-null...
                var resultExp = EmitExp(method.Body, bodySpan);

                if (method.ResultType != null
                    && !(method.ResultType is MidVoidType))
                {
                    Assign(resultVar, resultExp, span);
                }
            }

            span.WriteLine("}");

            _subroutineHeaderSpan.Add(span);
            return result;
        }
        private Dictionary<MidMethodDecl, string> _methods = new Dictionary<MidMethodDecl, string>();

        private EmitValHLSL EmitExpImpl(MidMethodApp app, Span span)
        {
            // Make sure we've emitted the method.
            var method = GetMethod(app.MethodDecl);

            Span builder = new Span();
            builder.Write("{0}(", method);

            var resultType = EmitType(app.MethodDecl.ResultType);
            var resultVar = resultType.CreateVal(
                _shared.GenerateName(app.MethodDecl.Name.ToString()));

            DeclareLocal(resultVar, span);

            bool first = true;
            AddArgs(
                resultVar,
                ref first,
                builder);

            foreach (var a in app.Args)
            {
                var argVal = EmitVal(a, span);
                AddArgs(
                    argVal,
                    ref first,
                    builder);
            }
            builder.WriteLine(");");

            span.Add(builder);
            return resultVar;
        }

        private EmitValHLSL EmitBuiltinAppArg(
            MidVal midVal,
            Span span)
        {
            var emitVal = EmitVal(midVal, span);

            // If passing a "connector" into
            // a builtin function, we need to
            // make sure to change from record
            // value to simple value
            if (emitVal is TupleValHLSL)
            {
                var tupleVal = (TupleValHLSL)emitVal;
                if (tupleVal.Type is RealTypeHLSL)
                {
                    var realType = (RealTypeHLSL)tupleVal.Type;
                    var newVal = realType.CreateVal(_shared.GenerateName("__record"));
                    DeclareAndInitLocal(newVal, emitVal, span);
                    return newVal;
                }
            }

            return emitVal;
        }

        private EmitValHLSL EmitExpImpl(MidBuiltinApp app, Span span)
        {
            var template = app.Decl.GetTemplate("hlsl");

            switch (template)
            {
                case "__ConstantRef":
                    return EmitConstantRef(app.Args.First(), span);
                case "__UniformRef":
                    return EmitUniformRef(app.Args.First(), span);
                default:
                    break;
            }

            var args = (from a in app.Args
                        select EmitBuiltinAppArg(a, span)).ToArray();

            switch (template)
            {
                case "__GetElem":
                    return GetElem(
                        args[0],
                        args[1]);
                default:
                    break;
            }


            var resultType = EmitType(app.Type);

            var resultString = string.Format(template, args);

            if (resultType is VoidTypeHLSL)
            {
                span.WriteLine("{0};", resultString);
                return VoidVal;
            }

            if (!(resultType is RealTypeHLSL))
            {
                Diagnostics.Add(
                    Severity.Error,
                    new SourceRange(),
                    "Invalid return type for HLSL builtin function '{0}'", app.Decl.Name);
                return new ErrorValHLSL();
            }

            return new SimpleValHLSL(
                resultString,
                (RealTypeHLSL) resultType);
        }

        private EmitValHLSL EmitExpImpl(MidLetExp let, Span span)
        {
            var initVal = EmitExp(let.Exp, span);

#if ALWAYSBIND
            // \todo: Just use the type from init?
            var varType = EmitType(let.Var.Type);
            var var = varType.CreateVal(
                _shared.GenerateName("_var"));
            _varVals[let.Var] = var;

            DeclareAndInitLocal(
                var,
                initVal,
                span);
#else
            _varVals[let.Var] = initVal;
#endif

            return EmitExp(let.Body, span);
        }

        private EmitValHLSL EmitExpImpl(MidAttributeFetch fetch, Span span)
        {
            var obj = EmitExp(fetch.Obj, span);
            return FetchAttr(obj, fetch.Attribute, span);
        }

        //

        public EmitValHLSL FetchAttr(
            EmitValHLSL objVal,
            MidAttributeDecl attr,
            Span span)
        {
            var attrType = EmitType(attr.Type);
            var attrName = MapName(attr);

            int attrIndex = 0;
            foreach (var a in attr.Element.Outputs)
            {
                if (a == attr)
                    break;
                attrIndex++;
            }

            var objType = (IAggTypeHLSL)objVal.Type;

            return GetField(
                objVal,
                objType.GetFieldType(attrIndex),
                attrName,
                attrIndex,
                span);
        }

        private EmitValHLSL GetField(
            EmitValHLSL objVal,
            ITypeHLSL fieldRep,
            string fieldName,
            int fieldIndex,
            Span span)
        {
            return GetFieldImpl(
                (dynamic)objVal,
                (dynamic)fieldRep,
                fieldName,
                fieldIndex,
                span);
        }

        private EmitValHLSL GetFieldImpl(
            SimpleValHLSL objVal,
            RealTypeHLSL fieldType,
            string fieldName,
            int fieldIndex,
            Span span)
        {
            return new SimpleValHLSL(
                string.Format("({0}).{1}", objVal, fieldName),
                fieldType);
        }
/*
        private EmitValHLSL GetFieldImpl(
            SimpleValHLSL objVal,
            IArrayTypeHLSL fieldRep,
            string fieldName,
            int fieldIndex,
            Span span)
        {
            return new ArrayValHLSL(
                string.Format("({0}).{1}", objVal, fieldName),
                fieldRep);
        }
*/
        private EmitValHLSL GetFieldImpl(
            SimpleValHLSL objVal,
            TupleTypeHLSL fieldRep,
            string fieldName,
            int fieldIndex,
            Span span)
        {
            List<EmitValHLSL> fieldFieldVals = new List<EmitValHLSL>();
            int fieldFieldCount = fieldRep.GetFieldCount();
            for (int ff = 0; ff < fieldFieldCount; ++ff)
            {
                var fieldFieldVal = GetField(
                    objVal,
                    fieldRep.GetFieldType(ff),
                    fieldName + fieldRep.GetFieldName(ff),
                    -1,
                    span);
                fieldFieldVals.Add(fieldFieldVal);
            }

            return new TupleValHLSL(
                fieldRep,
                fieldFieldVals);
        }

        private EmitValHLSL GetFieldImpl(
            SimpleValHLSL objVal,
            PseudoArrayTypeHLSL fieldRep,
            string fieldName,
            int fieldIndex,
            Span span)
        {
            var innerVal = GetField(
                objVal,
                fieldRep.ElementType,
                fieldName,
                fieldIndex,
                span);

            return new PseudoArrayValHLSL(
                fieldRep,
                innerVal);
        }

        private EmitValHLSL GetFieldImpl(
            TupleValHLSL objVal,
            EmitTypeHLSL fieldType,
            string fieldName,
            int fieldIndex,
            Span span)
        {
            return objVal.GetFieldVal(fieldIndex);
        }

        //

        public EmitValHLSL GetElem(
            EmitValHLSL obj,
            EmitValHLSL idx )
        {
            return GetElemImpl(
                (dynamic)obj,
                idx);
        }

        public EmitValHLSL GetElemImpl(
            SimpleValHLSL obj,
            EmitValHLSL idx)
        {
            return new SimpleValHLSL(
                string.Format("{0}[{1}]", obj, idx),
                ((ArrayTypeBaseHLSL)obj.Type).ElementType);
        }
        public EmitValHLSL GetElemImpl(
            TupleValHLSL obj,
            EmitValHLSL idx)
        {
            int fieldCount = obj.GetFieldCount();
            EmitValHLSL[] fieldVals = new EmitValHLSL[fieldCount];
            TupleTypeHLSL resultType = new TupleTypeHLSL("temp");
            for (int ff = 0; ff < fieldCount; ++ff)
            {
                fieldVals[ff] = GetElem(
                    obj.GetFieldVal(ff),
                    idx);
                resultType.AddField(
                    obj.AggType.GetFieldName(ff),
                    fieldVals[ff].Type);
            }
            return new TupleValHLSL(
                resultType,
                fieldVals);
        }

        public EmitValHLSL GetElemImpl(
            PseudoArrayValHLSL obj,
            EmitValHLSL idx)
        {
            return GetElem(
                obj.InnerVal,
                idx);
        }

        //

        private class ElementCtorInfo
        {
            public ElementCtorInfo(
                string name,
                MidElementDecl elementDecl,
                IEnumerable<MidAttributeDecl> inputAttributes)
            {
                _name = name;
                _elementDecl = elementDecl;
                _inputAttributes = inputAttributes.ToArray();
            }

            public string Name { get { return _name; } }
            public MidElementDecl Element { get { return _elementDecl; } }
            public IEnumerable<MidAttributeDecl> InputAttributes { get { return _inputAttributes; } }

            private string _name;
            private MidElementDecl _elementDecl;
            private MidAttributeDecl[] _inputAttributes;
        }

        private Dictionary<MidElementDecl, ElementCtorInfo> _elementCtors = new Dictionary<MidElementDecl, ElementCtorInfo>();

        private ElementCtorInfo GetElementCtor(MidElementDecl elementDecl)
        {
            ElementCtorInfo result;
            if (_elementCtors.TryGetValue(elementDecl, out result))
                return result;

            string name = "Ctor_" + elementDecl.Name.ToString();
            var inputAttributes = (from a in elementDecl.Attributes
                                   where a.Exp == null // \todo: real test for input-ness
                                   select a).ToArray();

            result = new ElementCtorInfo(
                name,
                elementDecl,
                inputAttributes);

            var span = new Span();

            span.WriteLine("void {0}(", name);

            var resultType = EmitType(elementDecl);

            bool first = true;
            var resultParam = DeclareOutParam("__result", resultType, span, ref first);

            foreach (var a in inputAttributes)
            {
                var attrName = MapName(a);
                var attrType = EmitType(a.Type);

                var attrParam = DeclareParam(
                    attrName,
                    attrType,
                    span,
                    ref first);

                _attrVals[a] = attrParam;

            }
            span.WriteLine(")");
            span.WriteLine("{");

            var resultAttrs = (from a in elementDecl.Attributes
                               where a.IsOutput
                               select EmitAttribRef(a, span)).ToArray();

            var resultVal = new TupleValHLSL(
                (IAggTypeHLSL) resultType,
                resultAttrs);

            Assign(resultParam, resultVal, span);

            span.WriteLine("}");

            _subroutineHeaderSpan.Add(span);

            _elementCtors[elementDecl] = result;
            return result;
        }

        private void AddArgs(
            EmitValHLSL val,
            ref bool first,
            Span span)
        {
            AddArgsImpl(
                (dynamic)val,
                ref first,
                span);
        }

        private void AddArgsImpl(
            SimpleValHLSL val,
            ref bool first,
            Span span)
        {
            if (!first)
                span.Write(", ");
            first = false;

            span.Write("{0}", val);
        }

        private void AddArgsImpl(
            TupleValHLSL val,
            ref bool first,
            Span span)
        {
            int fieldCount = val.GetFieldCount();
            for (int ii = 0; ii < fieldCount; ++ii)
            {
                AddArgs(
                    val.GetFieldVal(ii),
                    ref first,
                    span);
            }
        }

        public void DeclareLocal(
            EmitValHLSL val,
            Span span)
        {
            DeclareLocalImpl(
                (dynamic)val,
                span);
        }

        private void DeclareLocalImpl(
            SimpleValHLSL val,
            Span span)
        {
            span.WriteLine("{0};",
                val.RealType.DeclareVar(val.Name));
        }

        private void DeclareLocalImpl(
            TupleValHLSL val,
            Span span)
        {
            int fieldCount = val.GetFieldCount();
            for (int ii = 0; ii < fieldCount; ++ii)
            {
                DeclareLocal(
                    val.GetFieldVal(ii),
                    span);
            }
        }


        //

        private EmitValHLSL EmitConstantRef(MidVal constantVal, Span span)
        {
            if (constantVal is MidLit)
                return EmitExp(constantVal, span);
            else if (constantVal is MidAttributeRef)
            {
                var attr = ((MidAttributeRef)constantVal);
                return EmitExp(attr.Decl.Exp, span);
            }
            else
            {
                Diagnostics.Add(
                    Severity.Error,
                    new SourceRange(),
                    "Unacceptable constant expression in HLSL emit");
                return new ErrorValHLSL();
            }
        }






        //

        private EmitValHLSL EmitVal(MidVal val, Span span)
        {
            return EmitValImpl((dynamic)val, span);
        }

        private EmitValHLSL EmitValImpl(MidStructVal val, Span span)
        {
            var recordType = (IAggTypeHLSL)EmitType(val.Type);
            var fieldVals = (from f in val.FieldVals
                             select EmitExp(f, span)).ToArray();
            return new TupleValHLSL(
                recordType,
                fieldVals);
        }

        private EmitValHLSL EmitValImpl(MidVoidExp val, Span span)
        {
            return VoidVal;
        }

        private EmitValHLSL EmitValImpl(MidLit<float> val, Span span)
        {
            return new LitHLSL(
                (RealTypeHLSL) EmitType(val.Type),
                string.Format("{0:f10}f", val.Value));
        }

        private EmitValHLSL EmitValImpl(MidLit<int> val, Span span)
        {
            return new LitHLSL(
                (RealTypeHLSL)EmitType(val.Type),
                string.Format("{0}", val.Value));
        }

        private EmitValHLSL EmitValImpl(MidLit<bool> val, Span span)
        {
            return new LitHLSL(
                (RealTypeHLSL)EmitType(val.Type),
                val.Value ? "true" : "false");
        }

        private EmitValHLSL EmitValImpl(MidAttributeRef attribRef, Span span)
        {
            return EmitAttribRef(attribRef.Decl, span);
        }

        private EmitValHLSL EmitValImpl(MidVarRef varRef, Span span)
        {
            EmitValHLSL val;
            if (_varVals.TryGetValue(varRef.Var, out val))
                return val;

            Diagnostics.Add(
                Severity.Error,
                new SourceRange(),
                "Can't find value for variable '{0}' during HLSL emit", varRef.Var.Name);
            return new ErrorValHLSL();
        }

        private EmitValHLSL EmitUniformRef(MidVal uniformVal, Span span)
        {
            var uniformType = uniformVal.Type;
            if (uniformType is MidBuiltinType)
            {
                var builtinType = (MidBuiltinType)uniformType;
                switch (builtinType.Name)
                {
                    case "Buffer":
                    case "Texture1D":
                    case "Texture1DArray":
                    case "Texture2D":
                    case "Texture2DArray":
                    case "Texture3D":
                    case "Texture3DArray":
                    case "TextureCube":
                    case "TextureCubeArray":
                        return EmitShaderResourceRef(builtinType, uniformVal, span);
                    case "SamplerState":
                    case "SamplerComparisonState":
                        return EmitSamplerStateRef(builtinType, uniformVal, span);
                }
            }

            var uString = _shared.EmitUniformRef(uniformVal);
            var uType = EmitType(uniformVal.Type);

            return new SimpleValHLSL(
                uString,
                (RealTypeHLSL) uType);
        }

        private Dictionary<object, EmitValHLSL> _uniformResourceCache = new Dictionary<object, EmitValHLSL>();

        private object GetUniformValKey(
            MidVal val)
        {
            return GetUniformValKeyImpl((dynamic)val);
        }

        private object GetUniformValKeyImpl(
            MidAttributeRef val)
        {
            return val.Decl;
        }

        private EmitValHLSL EmitShaderResourceRef(
            MidBuiltinType type,
            MidVal uniformVal,
            Span span)
        {
            object key = GetUniformValKey(uniformVal);
            EmitValHLSL result = VoidVal;
            if (_uniformResourceCache.TryGetValue(key, out result))
            {
                return result;
            }

            int index = _shaderResources.Count;
            string name = _shared.GenerateName(uniformVal.ToString());

            DeclareFields(
                EmitType(type),
                _resourceHeaderSpan,
                name,
                suffix: string.Format(" : register(t{0})", index));
            _shaderResources.Add(uniformVal);

            result = new SimpleValHLSL(
                name,
                (SimpleTypeHLSL)EmitType(uniformVal.Type));
            _uniformResourceCache[key] = result;
            return result;
        }

        private EmitValHLSL EmitSamplerStateRef(
            MidBuiltinType type,
            MidVal uniformVal,
            Span span)
        {
            object key = GetUniformValKey(uniformVal);
            EmitValHLSL result = VoidVal;
            if (_uniformResourceCache.TryGetValue(key, out result))
            {
                return result;
            }

            int index = _samplerStates.Count;
            string name = _shared.GenerateName(uniformVal.ToString());

            DeclareFields(
                EmitType(type),
                _resourceHeaderSpan,
                name,
                suffix: string.Format(" : register(s{0})", index));
            _samplerStates.Add(uniformVal);


            result = new SimpleValHLSL(
                name,
                (SimpleTypeHLSL)EmitType(uniformVal.Type));
            _uniformResourceCache[key] = result;
            return result;
        }

        private Span NoteRange(
            Span span,
            SourceRange range)
        {
            if (span == null || range.fileName == null)
                return span;

            span.WriteLine("{0}{1}", startSourceRange, range);
            var subSpan = span.InsertSpan();
            span.WriteLine(endSourceRange);
            return subSpan;
        }

        //

        public EmitValHLSL EmitAttribRef(
            MidAttributeWrapperDecl wrapper,
            Span span)
        {
            return EmitAttribRef(
                wrapper.Attribute,
                NoteRange(span, wrapper.Range));
        }

        public EmitValHLSL EmitAttribRef(
            MidAttributeDecl attr,
            Span inSpan)
        {
            var span = NoteRange(inSpan, attr.Range);

            EmitValHLSL attrVal;
            if (_attrVals.TryGetValue(attr, out attrVal))
                return attrVal;

            if (attr.Exp == null)
            {
                Diagnostics.Add(
                    Severity.Error,
                    new SourceRange(),
                    "No definition for attribute '{0}' during HLSL emit",
                    attr.Name);
                return new ErrorValHLSL();
            }

            // This is a (very) special case to try to deal with
            // attribute references used to define things like
            // array sizes (which appear in types, and thus
            // shouldn't really spill out other expressions...)
            if (span == null && attr.Exp is MidVal)
            {
                return EmitVal((MidVal)attr.Exp, span);
            }

            var attrName = MapName(attr);
            var attrType = EmitType(attr.Type);
            attrVal = attrType.CreateVal(attrName);

            _attrVals[attr] = attrVal;

            PreEmitExp(attr.Exp, span);

            var initVal = EmitExpRaw(attr.Exp, span);

            DeclareAndInitLocal(
                attrVal,
                initVal,
                span);

            return attrVal ;
        }

        // Constant buffer stuff
        public void EmitConstantBufferDecl()
        {
            var cbSpan = new Span();
            cbSpan.WriteLine("cbuffer Uniforms");
            cbSpan.WriteLine("{");
            foreach (var u in _shared.Uniforms)
            {
                DeclareFields(
                    EmitType(u.Val.Type),
                    cbSpan,
                    u.Name,
                    suffix: string.Format(" : packoffset(c{0})", u.Slot));
            }
            cbSpan.WriteLine("}");
            _cbHeaderSpan.Add(cbSpan);
        }

        // Main shader body stuff


        public EmitValHLSL EmitConnectorCtor(
            Span span,
            MidElementDecl element)
        {
            var name = _shared.GenerateName("__result");
            var type = GenerateConnectorType(element);
            var val = type.CreateVal(name);

            DeclareAndInitRecord(
                span,
                element,
                val);

            return val;
        }

        public EmitValHLSL EmitTempRecordCtor(
            Span span,
            MidElementDecl recordToConstruct,
            MidAttributeWrapperDecl wrapper )
        {
            var attr = wrapper.Attribute;
            var attrName = attr.Name.ToString();
            var attrType = EmitType(attr.Type);
            var attrVal = attrType.CreateVal(attrName);
            BindAttr(attr, attrVal);

            return DeclareAndInitRecord(
                span,
                recordToConstruct,
                attrVal);
        }

        private EmitValHLSL DeclareAndInitRecord(
            Span span,
            MidElementDecl record,
            EmitValHLSL destVar)
        {
            var recordVal = EvaluateRecordAttrs(span, record);

            DeclareAndInitLocal(
                destVar,
                recordVal,
                span);

            return destVar;
        }

        public EmitValHLSL InitRecord(
            Span span,
            MidElementDecl record,
            EmitValHLSL destVar)
        {
            var recordVal = EvaluateRecordAttrs(span, record);

            Assign(
                destVar,
                recordVal,
                span);

            return destVar;
        }

        private EmitValHLSL EvaluateRecordAttrs(
            Span span,
            MidElementDecl record )
        {
            var recordType = EmitType(record);
            var attribVals = (from a in record.Attributes
                              where a.IsOutput
                              select EmitAttribRef(a, span)).Eager();

            var recordVal = new TupleValHLSL(
                recordType,
                attribVals);
            return recordVal;
        }

        // Compilation of resulting HLSL
        [System.Runtime.InteropServices.DllImport("SparkCPP.dll")]
        static extern void SparkRegisterHlslCompiler();

        private IHlslCompiler LoadHlslCompiler()
        {
            // Try to ping the SparkCPP DLL to register itself
            SparkRegisterHlslCompiler();
            return HlslCompilerHelper.Get();
        }

        private IHlslCompiler _hlslCompiler;

        private static readonly string startSourceRange = "//spark-start-range: ";
        private static readonly string endSourceRange = "//spark-end-range";

        private static readonly string PushErrorMaskString = "//spark-push-error-mask: ";
        private static readonly string PopErrorMaskString = "//spark-pop-error-mask";

        public Span PushErrorMask(Span span, string error)
        {
            span.WriteLine("{0}{1}", PushErrorMaskString, error);
            var subSpan = span.InsertSpan();
            span.WriteLine(PopErrorMaskString);
            return subSpan;
        }

        public byte[] Compile(string profile)
        {
            if (_hlslCompiler == null)
                _hlslCompiler = LoadHlslCompiler();

            if (_hlslCompiler == null)
            {
                _shared.Diagnostics.Add(
                    Severity.Error,
                    new SourceRange(),
                    "Could not load HLSL compiler from SparkCPP.dll");
                return new byte[] { };
            }

            string hlslErrors = null;
            var result = _hlslCompiler.Compile(
                Span.ToString(),
                "main",
                profile,
                out hlslErrors);

            if (!string.IsNullOrEmpty(hlslErrors))
            {
                FormatDiagnostics(hlslErrors, _shared.Diagnostics, profile);
            }

            return result;
        }

        class DumpedShaderInfo
        {
            public int[] hlslLineRemap;
            public SourceRange[] sparkLineRanges;
            public string[][] lineErrorMasks;
            public string strippedText;
        }

        class DumpedFileInfo
        {
            public string path;
        }

        private DumpedShaderInfo DumpShader()
        {
            DumpedShaderInfo result = new DumpedShaderInfo();

            string text = Span.ToString();
            var lines = text.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None);

            var lineCount = lines.Length;
            var sparkLineRanges = new SourceRange[lineCount];
            var lineErrorMasks = new string[lineCount][];
            var hlslLineRemap = new int[lineCount];

            var rangeStack = new Stack<SourceRange>();
            rangeStack.Push(_defaultRange);

            var writer = new System.IO.StringWriter();

            var maskedErrors = new Stack<string>();

            int remapLineNumber = 1;
            for (int ii = 0; ii < lineCount; ++ii)
            {
                var line = lines[ii];
                var lineNumber = ii + 1;

                hlslLineRemap[ii] = remapLineNumber;
                if (line.StartsWith(startSourceRange))
                {
                    var rangeStr = line.Substring(startSourceRange.Length);
                    SourceRange range = SourceRange.Parse(rangeStr);
                    rangeStack.Push(range);
                }
                else if (line.StartsWith(endSourceRange))
                {
                    rangeStack.Pop();
                }
                else if (line.StartsWith(PushErrorMaskString))
                {
                    var errorStr = line.Substring(PushErrorMaskString.Length);
                    maskedErrors.Push(errorStr.Trim());
                }
                else if (line.StartsWith(PopErrorMaskString))
                {
                    maskedErrors.Pop();
                }
                else
                {
                    if (writer != null)
                        writer.WriteLine(lines[ii]);
                    remapLineNumber++;
                }
                sparkLineRanges[ii] = rangeStack.Peek();
                lineErrorMasks[ii] = maskedErrors.ToArray();
            }

            result.hlslLineRemap = hlslLineRemap;
            result.sparkLineRanges = sparkLineRanges;
            result.strippedText = writer.ToString();
            result.lineErrorMasks = lineErrorMasks;

            return result;
        }


        private DumpedFileInfo DumpFile(DumpedShaderInfo shader, string profile)
        {
            DumpedFileInfo result = new DumpedFileInfo();

            var baseName = string.Format(
                "{0}_{1}_{2}",
                _defaultRange.fileName,
                _shaderClassName,
                profile);
            baseName = baseName.Replace('.', '_');
            var path = baseName + ".hlsl";

            var writer = new System.IO.StreamWriter(path);
            writer.Write(shader.strippedText);
            writer.Close();

            result.path = path;

            return result;
        }

        [Flags]
        enum DiagnosticAction
        {
            Suppress = 0,
            Report = 1,
            DumpAndReport = 2,
        }

        private DiagnosticAction ActionForHLSLDiagnostic(
            string errorCodeStr,
            string[] lineErrorMask)
        {
            if (lineErrorMask.Contains(errorCodeStr.Trim()))
            {
                return DiagnosticAction.Suppress;
            }

            switch (errorCodeStr)
            {
                default:
                    return DiagnosticAction.DumpAndReport;
            }
        }

        public void FormatDiagnostics(
            string hlslMessage,
            IDiagnosticsCollection diagnostics,
            string profile)
        {
            DumpedShaderInfo dumpedShader = null;
            DumpedFileInfo dumpedFile = null;

            var lines = hlslMessage.Split(new string[] { Environment.NewLine, "\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                string rangeEndStr = "): ";
                int rangeEndIdx = line.IndexOf(rangeEndStr);

                int hlslLineNumber = 1;
                int hlslLineIdx = 0;
                int hlslColNumber = 0;
                string messageStr = line;

                if (rangeEndIdx >= 0)
                {
                    var rangeStr = line.Substring(0, rangeEndIdx);
                    rangeStr = rangeStr.Substring(rangeStr.LastIndexOf('(') + 1);

                    var lineStr = rangeStr.Substring(0, rangeStr.IndexOf(','));
                    hlslLineNumber = int.Parse(lineStr);
                    hlslLineIdx = hlslLineNumber - 1;
                    var hlslColStr = rangeStr.Substring(rangeStr.IndexOf(',') + 1);
                    hlslColNumber = int.Parse(hlslColStr);

                    messageStr = line.Substring(rangeEndIdx + rangeEndStr.Length);
                }

                var severityEndStr = " ";
                int severityEndIdx = messageStr.IndexOf(severityEndStr);
                var severityStr = messageStr.Substring(0, severityEndIdx);
                Severity severity = Severity.Error;
                switch (severityStr)
                {
                    case "error":
                        severity = Severity.Error;
                        break;
                    case "warning":
                        severity = Severity.Warning;
                        break;
                    default:
                        break;
                }

                messageStr = messageStr.Substring(severityEndIdx + severityEndStr.Length);

                int errorCodeSplitIdx = messageStr.IndexOf(':');
                var errorCodeStr = messageStr.Substring(0, errorCodeSplitIdx);
                messageStr = messageStr.Substring(errorCodeSplitIdx + 1);

                if (dumpedShader == null)
                {
                    dumpedShader = DumpShader();
                }

                var action = ActionForHLSLDiagnostic(errorCodeStr, dumpedShader.lineErrorMasks[hlslLineIdx]);

                if (action >= DiagnosticAction.Report)
                {

                    var sparkRange = dumpedShader.sparkLineRanges[hlslLineIdx];
                    diagnostics.Add(
                        severity,
                        sparkRange,
                        string.Format("HLSL compiler {0} {1}:{2}", severityStr, errorCodeStr, messageStr));
                }

                if (action >= DiagnosticAction.DumpAndReport && dumpedFile == null)
                {
                    dumpedFile = DumpFile(dumpedShader, profile);

                    var hlslRemappedLineNumber = dumpedShader.hlslLineRemap[hlslLineIdx];

                    var hlslRange = new SourceRange(
                        dumpedFile.path,
                        new SourcePos(hlslRemappedLineNumber, hlslColNumber));

                    diagnostics.Add(
                        severity,
                        hlslRange,
                        string.Format("{0}:{1}", errorCodeStr, messageStr));
                }
            }
        }
    }
}
