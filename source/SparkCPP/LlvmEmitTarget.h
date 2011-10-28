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

// LlvmEmitTarget.h
#pragma once

#pragma unmanaged
#include <llvm/LLVMContext.h>
#include <llvm/Module.h>
#include <llvm/Support/IRBuilder.h>
#pragma managed

#pragma comment(lib, "LLVMJIT.lib")
#pragma comment(lib, "LLVMInterpreter.lib")
#pragma comment(lib, "LLVMX86CodeGen.lib")
#pragma comment(lib, "LLVMExecutionEngine.lib")
#pragma comment(lib, "LLVMAsmPrinter.lib")
#pragma comment(lib, "LLVMSelectionDAG.lib")
#pragma comment(lib, "LLVMX86AsmPrinter.lib")
#pragma comment(lib, "LLVMX86Info.lib")
#pragma comment(lib, "LLVMX86Utils.lib")
#pragma comment(lib, "LLVMMCParser.lib")
#pragma comment(lib, "LLVMCodeGen.lib")
#pragma comment(lib, "LLVMScalarOpts.lib")
#pragma comment(lib, "LLVMInstCombine.lib")
#pragma comment(lib, "LLVMTransformUtils.lib")
#pragma comment(lib, "LLVMipa.lib")
#pragma comment(lib, "LLVMAnalysis.lib")
#pragma comment(lib, "LLVMTarget.lib")
#pragma comment(lib, "LLVMCore.lib")
#pragma comment(lib, "LLVMMC.lib")
#pragma comment(lib, "llvmipo.lib")
#pragma comment(lib, "LLVMSupport.lib")

#include <vcclr.h>

#include "../include/spark/context.h"

using namespace System;
using namespace System::Collections::Generic;
using namespace System::Runtime::InteropServices;
using namespace Spark::Emit;

namespace Spark
{
    namespace Emit
    {
        namespace LLVM
        {
            using namespace System::Runtime::InteropServices;

            class NativeString
            {
            public:
                NativeString( String^ value )
                {
                    _value = (const char*)(void*)Marshal::StringToHGlobalAnsi(value);
                }

                ~NativeString()
                {
                    Marshal::FreeHGlobal((IntPtr)(void*)_value);
                }

                operator const char*()
                {
                    return _value;
                }

            private:
                const char* _value;
            };

            ref class LlvmEmitClass;
            ref class LlvmEmitField;
            ref class LlvmEmitMethod;
            ref class LlvmEmitModule;
            ref class LlvmEmitTarget;
            ref class LlvmEmitType;
            ref class LlvmEmitVal;

            enum EmitValMode
            {
                kEmitValMode_Value,
                kEmitValMode_Address,
            };

            interface class ILlvmEmitType :
                public IEmitType
            {
            public:
                virtual property const llvm::Type* LlvmType
                {
                    const llvm::Type* get() = 0;
                }
            };

            ref class LlvmEmitVal : public IEmitVal
            {
            public:
                LlvmEmitVal(
                    LlvmEmitTarget^ target,
                    IEmitType^ type,
                    EmitValMode mode,
                    llvm::Value* llvmValue );

                virtual property IEmitType^ Type
                {
                    IEmitType^ get();
                }

                virtual IEmitVal^ GetAddress();

                property llvm::Value* RawLlvmVal
                {
                    llvm::Value* get() { return _llvmValue; }
                    void set(llvm::Value* value) { _llvmValue = value; }
                }

                property EmitValMode Mode
                {
                    EmitValMode get() { return _mode; }
                }

                property String^ Name;

            private:
                LlvmEmitTarget^ _target;
                ILlvmEmitType^ _type;
                EmitValMode _mode;
                llvm::Value* _llvmValue;
            };

            ref class LlvmEmitBlock : public IEmitBlock
            {
            public:
                LlvmEmitBlock(
                    LlvmEmitMethod^ method,
                    llvm::BasicBlock* llvmEntryBlock,
                    llvm::IRBuilder<>* llvmBuilder );

                virtual property IEmitMethod^ Method
                {
                    IEmitMethod^ get();
                }

                virtual void AppendComment(
                    String^ comment );

                virtual void AppendComment(
                    Span^ span );

                virtual IEmitBlock^ InsertBlock();

                virtual IEmitVal^ Local(
                    String^ name,
                    IEmitType^ type );
                virtual IEmitVal^ Temp(
                    String^ name,
                    IEmitVal^ val);

                virtual IEmitVal^ LiteralData(
                    array<Byte>^ data);
                virtual IEmitVal^ LiteralString(
                    String^ val);

                virtual IEmitVal^ GetArrow(
                    IEmitVal^ obj,
                    IEmitField^ field);
                virtual void SetArrow(
                    IEmitVal^ obj,
                    IEmitField^ field,
                    IEmitVal^ val);

                virtual void StoreRaw(
                    IEmitVal^ basePointer,
                    UInt32 offset,
                    IEmitVal^ val);

                virtual IEmitVal^ CastRawPointer(
                    IEmitVal^ val,
                    IEmitType^ type);
                virtual IEmitVal^ GetBuiltinField(
                    IEmitVal^ obj,
                    String^ fieldName,
                    IEmitType^ fieldType );

                virtual IEmitVal^ Array(
                    IEmitType^ elementType,
                    array<IEmitVal^>^ elements );

                virtual IEmitVal^ Struct(
                    String^ structTypeName,
                    array<IEmitVal^>^ fields);

                virtual void CallCOM(
                    IEmitVal^ obj,
                    String^ interfaceName,
                    String^ methodName,
                    array<IEmitVal^>^ args);

                virtual IEmitVal^ BuiltinApp(
                    IEmitType^ type,
                    String^ format,
                    array<IEmitVal^>^ args);

                property llvm::LLVMContext& LlvmContext
                {
                    llvm::LLVMContext& get();
                }

                property LlvmEmitTarget^ TargetLlvm
                {
                    LlvmEmitTarget^ get();
                }

                virtual property IEmitTarget^ Target
                {
                    IEmitTarget^ get();
                }

                llvm::Value* GetLlvmVal(
                    IEmitVal^ val );

                llvm::Value* Arrow(
                        IEmitVal^ obj,
                        IEmitField^ field );

                void Debug(const std::string& message);
                void Debug(const llvm::Type* type);
                void Debug(const llvm::Value* val);

            private:
                LlvmEmitMethod^ _method;

                llvm::BasicBlock* _llvmEntryBlock;
                llvm::IRBuilder<>* _llvmBuilder;

                UInt32 _debugCounter;
            };

            ref class LlvmEmitMethod : public IEmitMethod
            {
            public:
                LlvmEmitMethod(
                    LlvmEmitClass^ outerClass,
                    IEmitType^ resultType,
                    String^ name);

                virtual IEmitVal^ AddParameter(
                    IEmitType^ type,
                    String^ name );

                virtual property IEmitVal^ ThisParameter
                {
                    IEmitVal^ get();
                }

                virtual property IEmitBlock^ EntryBlock
                {
                    IEmitBlock^ get();
                }

                property llvm::Function* LlvmFunction
                {
                    llvm::Function* get();
                }

                property llvm::LLVMContext& LlvmContext
                {
                    llvm::LLVMContext& get();
                }

                property LlvmEmitTarget^ Target
                {
                    LlvmEmitTarget^ get();
                }

                property llvm::Module* LlvmModule
                {
                    llvm::Module* get();
                }

                property LlvmEmitModule^ Module
                {
                    LlvmEmitModule^ get();
                }

                void Flush();

            private:
                LlvmEmitClass^ _outerClass;
                LlvmEmitType^ _resultType;
                String^ _name;
                List<LlvmEmitVal^>^ _parameters;

                llvm::Function* _llvmFunction;
                IEmitBlock^ _entryBlock;
                LlvmEmitVal^ _thisParam;
            };

            ref class LlvmEmitClass :
                public IEmitClass,
                public ILlvmEmitType
            {
            public:
                LlvmEmitClass(
                    LlvmEmitTarget^ target,
                    LlvmEmitModule^ module,
                    String^ name,
                    LlvmEmitClass^ baseClass,
                    EmitClassFlags flags);

                virtual String^ GetName();

                virtual IEmitField^ AddPublicField(
                    IEmitType^ type,
                    String^ name );

                virtual IEmitField^ AddPrivateField(
                    IEmitType^ type,
                    String^ name );

                virtual IEmitMethod^ CreateCtor();

                virtual IEmitMethod^ CreateDtor();

                virtual IEmitMethod^ CreateMethod(IEmitType^ resultType, String^ name);

                virtual void Seal();

                property llvm::LLVMContext& LlvmContext
                {
                    llvm::LLVMContext& get();
                }

                property LlvmEmitModule^ Module
                {
                    LlvmEmitModule^ get();
                }

                property const llvm::Type* LlvmPointerType
                {
                    const llvm::Type* get();
                }

                virtual property const llvm::Type* LlvmType
                {
                    const llvm::Type* get();
                }

                virtual property IEmitTarget^ Target
                {
                    IEmitTarget^ get();
                }

                virtual property unsigned int Size
                {
                    unsigned int get();
                }

                virtual property unsigned int Alignment
                {
                    unsigned int get();
                }

            private:
                LlvmEmitField^ CreateField(
                    IEmitType^ type,
                    String^ name );

                LlvmEmitTarget^ _target;
                LlvmEmitModule^ _module;
                String^ _name;
                LlvmEmitClass^ _baseClass;
                EmitClassFlags _flags;

                llvm::PATypeHolder* _abstractType;
                llvm::PointerType* _pointerType;

                List<LlvmEmitField^>^ _publicFields;
                List<LlvmEmitField^>^ _privateFields;
                const llvm::Type* _structType;

                LlvmEmitMethod^ _ctor;
                LlvmEmitMethod^ _dtor;
                LlvmEmitMethod^ _submit;

                List<LlvmEmitMethod^>^ _methods;
            };

            ref class LlvmEmitStruct :
                public IEmitStruct,
                public ILlvmEmitType
            {
            public:
                LlvmEmitStruct(
                    llvm::LLVMContext& llvmContext,
                    String^ name);

                IEmitField^ AddField(
                    IEmitType^ type,
                    String^ name );

                virtual property UInt32 Size
                {
                    UInt32 get();
                }

                virtual property UInt32 Alignment
                {
                    UInt32 get();
                }

                virtual property IEmitTarget^ Target
                {
                    IEmitTarget^ get() { throw gcnew NotImplementedException(); }
                }

                virtual property const llvm::Type* LlvmType
                {
                    const llvm::Type* get() { throw gcnew NotImplementedException();  }
                }

            private:
                llvm::PATypeHolder* _abstractType;
                llvm::PointerType* _pointerType;
                std::vector<const llvm::Type*>* _fieldTypes;
            };

            ref class LlvmEmitField : public IEmitField
            {
            public:
                LlvmEmitField(
                    ILlvmEmitType^ type,
                    String^ name,
                    UInt32 fieldIndex );

                virtual property IEmitType^ Type
                {
                    IEmitType^ get();
                }

                property String^ Name
                {
                    String^ get() { return _name; }
                }

                property UInt32 FieldIndex
                {
                    UInt32 get() { return _fieldIndex; }
                    void set( UInt32 value ) { _fieldIndex = value; }
                }

            private:
                ILlvmEmitType^ _type;
                String^ _name;
                UInt32 _fieldIndex;
            };

            ref class LlvmBuiltinFunction
            {
            public:
                property llvm::Function* LlvmFunction;
            };

            ref class LlvmEmitModule : public IEmitModule
            {
            public:
                LlvmEmitModule(
                    LlvmEmitTarget^ target,
                    llvm::LLVMContext& llvmContext,
                    String^ name);

                virtual IEmitClass^ CreateClass(
                    String^ className,
                    IEmitClass^ baseClass,
                    EmitClassFlags flags)
                {
                    return gcnew LlvmEmitClass(_target, this, className, (LlvmEmitClass^) baseClass, flags);
                }

                virtual IEmitStruct^ CreateStruct(String^ name);

                virtual IEmitVal^ LiteralString(String^ val);

                virtual IEmitVal^ EmitGlobalStruct(
                    String^ name,
                    array<IEmitVal^>^ members );

                virtual IEmitVal^ GetMethodPointer(
                    IEmitMethod^ method );

                property llvm::LLVMContext& LlvmContext
                {
                    llvm::LLVMContext& get();
                }

                property llvm::Module* LlvmModule
                {
                    llvm::Module* get();
                }

                property LlvmEmitTarget^ Target
                {
                    LlvmEmitTarget^ get() { return _target; }
                }

                property llvm::Function* DebugFunction
                {
                    llvm::Function* get() { return _debugFunction; }
                }

                llvm::Function* GetBuiltinFunction(
                    String^ name,
                    IEmitType^ resultType,
                    IEnumerable<IEmitVal^>^ args );

            private:
                LlvmEmitTarget^ _target;
                llvm::LLVMContext& _llvmContext;
                llvm::Module* _llvmModule;

                llvm::Function* _debugFunction;

                Dictionary<String^, LlvmBuiltinFunction^>^ _builtinFunctions;
            };

            ref class LlvmEmitType : public ILlvmEmitType
            {
            public:
                LlvmEmitType(
                    LlvmEmitTarget^ target,
                    const llvm::Type* llvmType )
                    : _target(target)
                    , _llvmType(llvmType)
                {
                }

                virtual property UInt32 Size
                {
                    UInt32 get() { throw gcnew NotImplementedException(); }
                }

                virtual property UInt32 Alignment
                {
                    UInt32 get() { throw gcnew NotImplementedException(); }
                }

                virtual property IEmitTarget^ Target
                {
                    IEmitTarget^ get();
                }

                virtual property const llvm::Type* LlvmType
                {
                    const llvm::Type* get() { return _llvmType; }
                }

            private:
                LlvmEmitTarget^ _target;
                const llvm::Type* _llvmType;
            };

            ref class COMMethodInfo
            {
            public:
                property String^ Name;
                property UInt32 Index;
                property const llvm::Type* LlvmType
                {
                    const llvm::Type* get() { return _type; }
                    void set(const llvm::Type* value) { _type = value; }
                }

            private:
                const llvm::Type* _type;
            };

            ref class LlvmEmitTarget : public IEmitTarget
            {
            public:
                LlvmEmitTarget();

                virtual IEmitModule^ CreateModule(String^ moduleName)
                {
                    return gcnew LlvmEmitModule(this, _llvmContext, moduleName);
                }

                virtual property String^ TargetName
                {
                    String^ get() { return "llvm"; }
                }

                virtual property IEmitType^ VoidType
                {
                    IEmitType^ get();
                }

                virtual IEmitType^ GetBuiltinType(
                    String^ format,
                    array<IEmitTerm^>^ args );

                virtual IEmitType^ GetOpaqueType(
                    String^ name );

                virtual IEmitType^ Pointer(
                    IEmitType^ type );

                virtual IEmitVal^ GetNullPointer(
                    IEmitType^ type );

                IEmitType^ GetBuiltinType(
                    String^ name );

                COMMethodInfo^ GetCOMMethod(
                    String^ interfaceName,
                    String^ methodName );


                virtual IEmitVal^ LiteralBool(
                    bool val);
                virtual IEmitVal^ LiteralU32(
                    UInt32 val);
                virtual IEmitVal^ LiteralS32(
                    Int32 val);
                virtual IEmitVal^ LiteralF32(
                    float val);
                virtual IEmitVal^ Enum32(
                    String^ type,
                    String^ name,
                    UInt32 val);

                virtual void ShaderBytecodeCallback(
                    String^ prefix,
                    array<Byte>^ data );

                void SetCallback(
                    spark::IShaderBytecodeCallback* callback )
                {
                    _callback = callback;
                }

            private:
                const llvm::Type* AddBuiltinType(
                    String^ name,
                    const llvm::Type* llvmType );
                const llvm::Type* MakeVectorType(
                    const llvm::Type* elementType,
                    int elementCount );

                void AddCOM(
                    String^ interfaceName,
                    String^ methodName,
                    UInt32 methodIndex,
                    const llvm::Type* functionType );

                const llvm::Type* COMFunctionType(
                    String^ res,
                    array<String^>^ args );

                llvm::LLVMContext& _llvmContext;
                Dictionary<String^, LlvmEmitType^>^ _builtinTypes;
                Dictionary<String^, COMMethodInfo^>^ _comMethods;

                LlvmEmitType^ _voidType;
                spark::IShaderBytecodeCallback* _callback;
            };
        }

        namespace HLSL
        {
            ref class HlslCompiler :
                public Spark::Emit::HLSL::IHlslCompiler
            {
            public:
                HlslCompiler();

                virtual array<Byte>^ Compile(
                    String^ source,
                    String^ entry,
                    String^ profile,
                    String^% errors );
            };
        }
    }
}
