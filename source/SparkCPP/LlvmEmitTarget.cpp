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

// LlvmEmitTarget.cpp

#pragma unmanaged
#include <llvm/Type.h>
#include <llvm/DerivedTypes.h>
#include <llvm/Support/raw_os_ostream.h>
#include <D3D11.h>
#include <D3Dcompiler.h>

#pragma comment(lib, "d3dcompiler.lib")

#include <fstream>
#include <sstream>
#include <string>
#pragma managed

#include "LlvmEmitTarget.h"


#include <vcclr.h>
using namespace System;
using namespace Spark::Emit;

struct TypeInfo
{
    unsigned int size;
    unsigned int align;
};

TypeInfo GetTypeInfo(
    const llvm::Type* type )
{
    if( auto pointerType = llvm::dyn_cast<const llvm::PointerType>(type) )
    {
        TypeInfo result = { sizeof(void*), sizeof(void*) };
        return result;
    }
    else if( auto arrType = llvm::dyn_cast<const llvm::ArrayType>(type) )
    {
        auto info = GetTypeInfo( arrType->getElementType() );
        info.size *= arrType->getNumElements();
        return info;
    }
    else if( auto strType = llvm::dyn_cast<const llvm::StructType>(type) )
    {
        TypeInfo result = { 0, 1 };

        int fieldCount = (int) strType->getNumElements();
        for( int ff = 0; ff < fieldCount; ++ff )
        {
            auto fieldInfo = GetTypeInfo( strType->getElementType(ff) );
            
            result.align = max(result.align, fieldInfo.align);

            result.size = fieldInfo.align * ((result.size + fieldInfo.align - 1) / fieldInfo.align);
            result.size += fieldInfo.size;
        }

        return result;
    }
    else if( auto intType = llvm::dyn_cast<const llvm::IntegerType>(type) )
    {
        int size = ((int) intType->getBitWidth()) / 8;
        TypeInfo result = { size, size };
        return result;
    }
    else if( type->isFloatTy() )
    {
        TypeInfo result = { sizeof(float), sizeof(float) };
        return result;
    }
    else if( type->isDoubleTy() )
    {
        TypeInfo result = { sizeof(double), sizeof(double) };
        return result;
    }
    else
    {
        int f = 9;
        throw gcnew NotImplementedException();
    }
}

namespace Spark
{
    namespace Emit
    {
        namespace LLVM
        {
            // LlvmEmitVal

            LlvmEmitVal::LlvmEmitVal(
                LlvmEmitTarget^ target,
                IEmitType^ type,
                EmitValMode mode,
                llvm::Value* llvmValue )
                : _target(target)
                , _type((ILlvmEmitType^) type)
                , _mode(mode)
                , _llvmValue(llvmValue)
            {
            }

            IEmitType^ LlvmEmitVal::Type::get()
            {
                return _type;
            }

            IEmitVal^ LlvmEmitVal::GetAddress()
            {
                if( _mode != kEmitValMode_Address )
                    throw gcnew NotImplementedException();

                return gcnew LlvmEmitVal(
                    _target,
                    gcnew LlvmEmitType(
                        _target,
                        llvm::PointerType::getUnqual( _type->LlvmType ) ),
                    kEmitValMode_Value,
                    _llvmValue);
            }

            // LlvmEmitBlock

            LlvmEmitBlock::LlvmEmitBlock(
                LlvmEmitMethod^ method,
                llvm::BasicBlock* llvmEntryBlock,
                llvm::IRBuilder<>* llvmBuilder )
                : _method(method)
                , _llvmEntryBlock(llvmEntryBlock)
                , _llvmBuilder(llvmBuilder)
            {
            }


            IEmitMethod^ LlvmEmitBlock::Method::get()
            {
                throw gcnew NotImplementedException();
            }

            void LlvmEmitBlock::AppendComment(
                String^ comment )
            {
                Debug(std::string(NativeString(comment)));
            }

            void LlvmEmitBlock::AppendComment(
                Span^ span )
            {}

            IEmitBlock^ LlvmEmitBlock::InsertBlock()
            {
                Debug("InsertBlock");
                llvm::BasicBlock* subBlock = llvm::BasicBlock::Create(
                    LlvmContext,
                    "block",
                    _method->LlvmFunction );

                _llvmBuilder->CreateBr( subBlock );

                llvm::IRBuilder<>* subBuilder = new llvm::IRBuilder<>(subBlock);

                llvm::BasicBlock* afterBlock = llvm::BasicBlock::Create(
                    LlvmContext,
                    "after",
                    _method->LlvmFunction );

                _llvmBuilder->SetInsertPoint( afterBlock );

                return gcnew LlvmEmitBlock(
                    _method,
                    _llvmEntryBlock,
                    new llvm::IRBuilder<>(
                        afterBlock,
                        afterBlock->begin()));
            }

            IEmitVal^ LlvmEmitBlock::Local(
                String^ name,
                IEmitType^ type )
            {
                Debug("Local");
                llvm::IRBuilder<> entryBuilder( _llvmEntryBlock );
                auto llvmLocal = entryBuilder.CreateAlloca(
                    ((LlvmEmitType^) type)->LlvmType,
                    nullptr,
                    llvm::StringRef(NativeString(name)));

                return gcnew LlvmEmitVal(
                    TargetLlvm,
                    type,
                    kEmitValMode_Address,
                    llvmLocal);
            }

            IEmitVal^ LlvmEmitBlock::Temp(
                String^ name,
                IEmitVal^ val)
            {
                auto llvmVal = ((LlvmEmitVal^) val)->RawLlvmVal;
                llvmVal->setName(llvm::StringRef(NativeString(name)));
                return val;
            }

            IEmitVal^ LlvmEmitTarget::LiteralBool(
                bool val)
            {
                return LiteralU32( (UInt32) val );
            }

            IEmitVal^ LlvmEmitTarget::LiteralU32(
                UInt32 val)
            {
                auto llvmType = llvm::Type::getInt32Ty(_llvmContext);
                auto type = gcnew LlvmEmitType(
                    this,
                    llvmType );
                auto llvmValue = llvm::ConstantInt::get(llvmType, val);
                return gcnew LlvmEmitVal(
                    this,
                    type,
                    kEmitValMode_Value,
                    llvmValue);
            }

            IEmitVal^ LlvmEmitTarget::LiteralS32(
                Int32 val)
            {
                return LiteralU32( (UInt32) val );
            }

            IEmitVal^ LlvmEmitTarget::LiteralF32(
                float val)
            {
                auto llvmType = llvm::Type::getFloatTy(_llvmContext);
                auto llvmVal = llvm::ConstantFP::get(
                    llvmType,
                    val);

                auto type = gcnew LlvmEmitType(
                    this,
                    llvmType );

                return gcnew LlvmEmitVal(
                    this,
                    type,
                    kEmitValMode_Value,
                    llvmVal);
            }

            IEmitVal^ LlvmEmitBlock::LiteralData(
                array<Byte>^ data)
            {
                Debug("LiteralData");
                auto llvmU8Type = llvm::Type::getInt8Ty(LlvmContext);

                std::vector<llvm::Constant*> elements;
                for each( auto b in data )
                {
                    elements.push_back( llvm::ConstantInt::get( llvmU8Type, b ) );
                }
                auto llvmArrayType = llvm::ArrayType::get(
                    llvmU8Type,
                    elements.size());

                auto llvmArray = llvm::ConstantArray::get(
                    llvmArrayType,
                    elements);

                auto llvmConstant = new llvm::GlobalVariable(
                    *_method->LlvmModule,
                    llvmArrayType,
                    true, // isConstant
                    llvm::GlobalVariable::InternalLinkage,
                    llvmArray,
                    "data");

                auto resultType = (LlvmEmitType^) Target->GetOpaqueType("v*");

                auto llvmResult = _llvmBuilder->CreateBitCast(
                    llvmConstant,
                    resultType->LlvmType);

                return gcnew LlvmEmitVal(
                    TargetLlvm,
                    resultType,
                    kEmitValMode_Value,
                    llvmResult);
            }

            IEmitVal^ LlvmEmitBlock::LiteralString(
                String^ val)
            {
                auto type = (LlvmEmitType^) TargetLlvm->GetOpaqueType("v*");

                auto llvmString = _llvmBuilder->CreateGlobalStringPtr(
                    std::string(NativeString(val)).c_str());

                auto llvmVal = _llvmBuilder->CreateBitCast(
                    llvmString,
                    type->LlvmType);

                return gcnew LlvmEmitVal(
                    TargetLlvm,
                    type,
                    kEmitValMode_Value,
                    llvmVal);
            }

            IEmitVal^ LlvmEmitTarget::Enum32(
                String^ type,
                String^ name,
                UInt32 val)
            {
                return LiteralU32( val );
            }

            void LlvmEmitTarget::ShaderBytecodeCallback(
                String^ prefix,
                array<Byte>^ data )
            {
                if( _callback == nullptr )
                    return;

                pin_ptr<Byte> dataPtr = &data[0];

                _callback->ProcessBytecode(
                    NativeString(prefix),
                    data->Length,
                    (const void*) dataPtr);
            }

            IEmitVal^ LlvmEmitBlock::GetArrow(
                IEmitVal^ obj,
                IEmitField^ field)
            {
                auto llvmFieldPointer = Arrow(obj, field);

                Debug("GetArrow");
                return gcnew LlvmEmitVal(
                    TargetLlvm,
                    field->Type,
                    kEmitValMode_Address,
                    llvmFieldPointer );
            }

            void LlvmEmitBlock::SetArrow(
                IEmitVal^ obj,
                IEmitField^ field,
                IEmitVal^ val)
            {
                auto llvmFieldPointer = Arrow(obj, field);
                auto llvmVal = GetLlvmVal(val);

                {
                    std::ofstream out("dump.txt");
                    llvm::raw_os_ostream stream(out);
                    llvmFieldPointer->print(stream);
                    out << std::endl;
                    llvmVal->print(stream);
                    out << std::endl;
                }

                Debug("SetArrow");
                _llvmBuilder->CreateStore(
                    llvmVal,
                    llvmFieldPointer);
            }

            void LlvmEmitBlock::StoreRaw(
                IEmitVal^ basePointer,
                UInt32 offset,
                IEmitVal^ val)
            {
                Debug("StoreRaw");
                auto llvmBasePointer = GetLlvmVal( basePointer );

                auto llvmVal = GetLlvmVal( val );
                auto llvmValType = ((ILlvmEmitType^) val->Type)->LlvmType;

                auto llvmU8PointerType = llvm::Type::getInt8PtrTy(LlvmContext);
                auto llvmValPointerType = llvm::PointerType::getUnqual(llvmValType);
                auto llvmU32Ty = llvm::Type::getInt32Ty(LlvmContext);

                auto llvmBaseU8Pointer = _llvmBuilder->CreateBitCast(
                    llvmBasePointer,
                    llvmU8PointerType);

                auto llvmOffsetU8Pointer = _llvmBuilder->CreateGEP(
                    llvmBaseU8Pointer,
                    llvm::ConstantInt::get(llvmU32Ty, offset));

                auto llvmValPointer = _llvmBuilder->CreateBitCast(
                    llvmOffsetU8Pointer,
                    llvmValPointerType);

                _llvmBuilder->CreateStore(
                    llvmVal,
                    llvmValPointer);
            }

            IEmitVal^ LlvmEmitBlock::CastRawPointer(
                IEmitVal^ val,
                IEmitType^ type)
            {
                Debug("CastRawPointer");

                ILlvmEmitType^ ty = (ILlvmEmitType^) type;
                if( ty == nullptr )
                {
                    ty = (ILlvmEmitType^) TargetLlvm->GetBuiltinType("v*");
                }

                auto llvmTy = ty->LlvmType;

                auto llvmVal = _llvmBuilder->CreateBitCast(
                    GetLlvmVal(val),
                    llvmTy);

                return gcnew LlvmEmitVal(
                    TargetLlvm,
                    ty,
                    kEmitValMode_Value,
                    llvmVal);
            }

            IEmitVal^ LlvmEmitBlock::GetBuiltinField(
                IEmitVal^ obj,
                String^ fieldName,
                IEmitType^ fieldType )
            {
                Debug("GetBuiltinField");
                int fieldIndex = -1;
                if( fieldName == "pData" )
                {
                    fieldIndex = 0;
                }
                else
                {
                    throw gcnew NotImplementedException();
                }

                auto llvmObjPtr = GetLlvmVal((LlvmEmitVal^) obj->GetAddress());

                auto llvmU32Ty = llvm::Type::getInt32Ty(LlvmContext);
                static const int kIndexCount = 2;
                llvm::Value* indices[2] = {
                    llvm::ConstantInt::get(llvmU32Ty, 0),
                    llvm::ConstantInt::get(llvmU32Ty, fieldIndex),
                };

                auto llvmFieldPointer = _llvmBuilder->CreateGEP(
                    llvmObjPtr,
                    indices,
                    indices + kIndexCount);

                return gcnew LlvmEmitVal(
                    TargetLlvm,
                    fieldType,
                    kEmitValMode_Address,
                    llvmFieldPointer );
            }

            IEmitVal^ LlvmEmitBlock::Array(
                IEmitType^ elementType,
                array<IEmitVal^>^ elements )
            {
                Debug("Array");
                auto llvmElementType = ((ILlvmEmitType^) elementType)->LlvmType;
                auto llvmArrayType = llvm::ArrayType::get(
                    llvmElementType,
                    elements->Length);

                Debug(llvmElementType);
                Debug(llvmArrayType);

                llvm::IRBuilder<> entryBuilder(_llvmEntryBlock);
                auto llvmArrayVar = entryBuilder.CreateAlloca(llvmArrayType);

                Debug(llvmArrayVar);

                auto llvmU32Ty = llvm::Type::getInt32Ty(LlvmContext);
                auto elementIndex = 0;
                for each( auto e in elements )
                {
                    auto llvmElementVal = GetLlvmVal(e);
                    Debug(llvmElementVal);

                    static const int kIndexCount = 2;
                    llvm::Value* indices[2] = {
                        llvm::ConstantInt::get(llvmU32Ty, 0),
                        llvm::ConstantInt::get(llvmU32Ty, elementIndex),
                    };

                    auto llvmElementPointer = _llvmBuilder->CreateGEP(
                        llvmArrayVar,
                        indices,
                        indices + kIndexCount);

                    _llvmBuilder->CreateStore(
                        llvmElementVal,
                        llvmElementPointer );

                    elementIndex++;
                }

                auto arrayType = gcnew LlvmEmitType(
                    TargetLlvm,
                    llvmArrayType);

                return gcnew LlvmEmitVal(
                    TargetLlvm,
                    arrayType,
                    kEmitValMode_Address,
                    llvmArrayVar);
            }

            IEmitVal^ LlvmEmitBlock::Struct(
                String^ structTypeName,
                array<IEmitVal^>^ fields)
            {
                Debug("Struct");
                auto structType = (LlvmEmitType^) TargetLlvm->GetBuiltinType(structTypeName);
                auto llvmStructType = structType->LlvmType;

                llvm::IRBuilder<> entryBuilder(_llvmEntryBlock);
                auto llvmStructVar = entryBuilder.CreateAlloca(llvmStructType);

                auto llvmU32Ty = llvm::Type::getInt32Ty(LlvmContext);
                auto fieldIndex = 0;
                for each( auto f in fields )
                {
                    auto llvmFieldVal = GetLlvmVal(f);

                    static const int kIndexCount = 2;
                    llvm::Value* indices[2] = {
                        llvm::ConstantInt::get(llvmU32Ty, 0),
                        llvm::ConstantInt::get(llvmU32Ty, fieldIndex),
                    };

                    auto llvmFieldPointer = _llvmBuilder->CreateGEP(
                        llvmStructVar,
                        indices,
                        indices + kIndexCount);

                    _llvmBuilder->CreateStore(
                        llvmFieldVal,
                        llvmFieldPointer );

                    fieldIndex++;
                }


                return gcnew LlvmEmitVal(
                    TargetLlvm,
                    structType,
                    kEmitValMode_Address,
                    llvmStructVar);
            }

            void LlvmEmitBlock::CallCOM(
                IEmitVal^ obj,
                String^ interfaceName,
                String^ methodName,
                array<IEmitVal^>^ args)
            {
                Debug("CallCOM");
                auto methodInfo = TargetLlvm->GetCOMMethod(interfaceName, methodName);

                auto llvmMethodType = methodInfo->LlvmType;
                auto methodIndex = methodInfo->Index;

                auto llvmMethodPointerType = llvm::PointerType::getUnqual(llvmMethodType);
                auto llvmVtblType = llvm::PointerType::getUnqual(llvmMethodPointerType);
                auto llvmInterfaceType = llvm::PointerType::getUnqual(llvmVtblType);

                auto llvmVoidPointerType = ((LlvmEmitType^) TargetLlvm->GetBuiltinType("v*"))->LlvmType;

                auto llvmObjVal = GetLlvmVal(obj);

                auto llvmInterfaceVal = _llvmBuilder->CreateBitCast(
                    llvmObjVal,
                    llvmInterfaceType);

                auto llvmVtblVal = _llvmBuilder->CreateLoad( llvmInterfaceVal );

                auto llvmU32Ty = llvm::Type::getInt32Ty(LlvmContext);
                auto llvmVtblSlotVal = _llvmBuilder->CreateGEP(
                    llvmVtblVal,
                    llvm::ConstantInt::get(llvmU32Ty, methodIndex));

                auto llvmMethod = _llvmBuilder->CreateLoad( llvmVtblSlotVal );


                std::vector<llvm::Value*> llvmArgs;
                llvmArgs.push_back( llvmObjVal );

                for each( auto a in args )
                {
                    auto llvmArgVal = GetLlvmVal(a);
                    if( llvmArgVal->getType()->isPointerTy() )
                    {
                        llvmArgVal = _llvmBuilder->CreateBitCast(
                            llvmArgVal,
                            llvmVoidPointerType);
                    }
                    llvmArgs.push_back( llvmArgVal );
                }

                auto llvmCall = _llvmBuilder->CreateCall(
                    llvmMethod,
                    llvmArgs.begin(),
                    llvmArgs.end());

                llvmCall->setCallingConv(llvm::CallingConv::X86_StdCall);
            }

            IEmitVal^ LlvmEmitBlock::BuiltinApp(
                IEmitType^ type,
                String^ format,
                array<IEmitVal^>^ args)
            {
                Debug("BuiltinApp");

                if( args == nullptr )
                {
                    if( format == "D3D11_PRIMITIVE_TOPOLOGY_3_CONTROL_POINT_PATCHLIST" )
                        return Target->LiteralU32(D3D11_PRIMITIVE_TOPOLOGY_3_CONTROL_POINT_PATCHLIST);
                    else if( format == "D3D11_PRIMITIVE_TOPOLOGY_32_CONTROL_POINT_PATCHLIST" )
                        return Target->LiteralU32(D3D11_PRIMITIVE_TOPOLOGY_32_CONTROL_POINT_PATCHLIST);
                    else if( format == "D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST" )
                        return Target->LiteralU32(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
                }
                else if( format == "0" || format =="1" || format == "2" )
                {
                    auto index = Int32::Parse(format);
                    auto baseVal = GetLlvmVal((LlvmEmitVal^) args[0]);

                    auto fieldVal = _llvmBuilder->CreateExtractValue(
                        baseVal,
                        index);

                    return gcnew LlvmEmitVal(
                        TargetLlvm,
                        type,
                        kEmitValMode_Value,
                        fieldVal);
                }
                else if( format == "__C2U" )
                {
                    for each( IEmitVal^ a in args )
                        return a;
                }
                else if( format == "__NULL" )
                {
                    return Target->GetNullPointer( type );
                }
                else// if( format == "spark::d3d11::DrawIndexed16" )
                {
                    auto llvmBuiltin = _method->Module->GetBuiltinFunction(format, type, args);
                    
                    std::vector<llvm::Value*> llvmArgs;
                    for each( IEmitVal^ a in args )
                    {
                        llvmArgs.push_back( GetLlvmVal(a) );
                    }

                    auto llvmCall = _llvmBuilder->CreateCall(
                        llvmBuiltin,
                        llvmArgs.begin(),
                        llvmArgs.end());
                    llvmCall->setCallingConv(llvm::CallingConv::X86_StdCall);

                    return gcnew LlvmEmitVal(
                        TargetLlvm,
                        type,
                        kEmitValMode_Value,
                        llvmCall );
                }

                throw gcnew NotImplementedException();
            }

            llvm::LLVMContext& LlvmEmitBlock::LlvmContext::get()
            {
                return _method->LlvmContext;
            }

            LlvmEmitTarget^ LlvmEmitBlock::TargetLlvm::get()
            {
                return _method->Target;
            }

            IEmitTarget^ LlvmEmitBlock::Target::get()
            {
                return _method->Target;
            }

            llvm::Value* LlvmEmitBlock::GetLlvmVal(
                IEmitVal^ inVal )
            {
                auto val = (LlvmEmitVal^) inVal;

                if( val->Mode == kEmitValMode_Value )
                {
                    return val->RawLlvmVal;
                }
                else if( val->Mode == kEmitValMode_Address )
                {
                    auto llvmAddr = val->RawLlvmVal;
                    auto llvmVal = _llvmBuilder->CreateLoad( llvmAddr );
                    return llvmVal;
                }
                else
                {
                    throw gcnew NotImplementedException();
                }
            }

            llvm::Value* LlvmEmitBlock::Arrow(
                    IEmitVal^ obj,
                    IEmitField^ field )
            {
                Debug("Arrow");

                auto llvmObj = GetLlvmVal(obj);
                auto fieldIndex = ((LlvmEmitField^) field)->FieldIndex;
/*
                {
                std::ofstream dumpFile("./dump.txt");
                llvm::raw_os_ostream dumpStream(dumpFile);
                dumpStream << "obj: " << *llvmObj;
                dumpStream << "type: " << *(llvmObj->getType());
                }
*/
                auto llvmU32Ty = llvm::Type::getInt32Ty(LlvmContext);
                static const int kIndexCount = 2;
                llvm::Value* indices[2] = {
                    llvm::ConstantInt::get(llvmU32Ty, 0),
                    llvm::ConstantInt::get(llvmU32Ty, fieldIndex),
                };

                auto llvmFieldPointer = _llvmBuilder->CreateGEP(
                    llvmObj,
                    indices,
                    indices + kIndexCount);

                return llvmFieldPointer;
            }

            void LlvmEmitBlock::Debug(const std::string& message)
            {
                /*/
                OutputDebugStringA(message.c_str());
                OutputDebugStringA("\n");

                char buffer[1024];
                sprintf(buffer, "%s:%d", message.c_str(), _debugCounter++);

                auto messageString = _llvmBuilder->CreateGlobalStringPtr(buffer);

                auto debugFunc = _method->Module->DebugFunction;

                auto llvmCall = _llvmBuilder->CreateCall(
                    debugFunc,
                    messageString);
                llvmCall->setCallingConv(llvm::CallingConv::X86_StdCall);
                //*/
            }

            void LlvmEmitBlock::Debug(const llvm::Type* type)
            {
                std::ostringstream out;
                llvm::raw_os_ostream stream(out);
                type->print(stream);
                Debug( out.str() );
            }

            void LlvmEmitBlock::Debug(const llvm::Value* val)
            {
                std::ostringstream out;
                llvm::raw_os_ostream stream(out);
                val->print(stream);
                Debug( out.str() );
            }

            // LlvmEmitMethod

            LlvmEmitMethod::LlvmEmitMethod(
                LlvmEmitClass^ outerClass,
                IEmitType^ resultType,
                String^ name)
                : _outerClass(outerClass)
                , _resultType((LlvmEmitType^) resultType)
                , _name(name)
                , _llvmFunction(nullptr)
                , _entryBlock(nullptr)
                , _thisParam(nullptr)
            {
                _parameters = gcnew List<LlvmEmitVal^>();
            }

            IEmitVal^ LlvmEmitMethod::AddParameter(
                IEmitType^ type,
                String^ name )
            {
                if( _llvmFunction != nullptr )
                    throw gcnew NotImplementedException();

                auto parameter = gcnew LlvmEmitVal(
                    Target,
                    type,
                    kEmitValMode_Value,
                    nullptr);
                parameter->Name = name;
                _parameters->Add(parameter);
                return parameter;
            }

            IEmitVal^ LlvmEmitMethod::ThisParameter::get()
            {
                auto ignored = LlvmFunction;
                return _thisParam;
            }

            IEmitBlock^ LlvmEmitMethod::EntryBlock::get()
            {
                auto ignored = LlvmFunction;
                return _entryBlock;
            }

            llvm::Function* LlvmEmitMethod::LlvmFunction::get()
            {
                if( _llvmFunction != nullptr )
                    return _llvmFunction;

                auto& llvmContext = _outerClass->LlvmContext;
                const llvm::Type* llvmResultType = llvm::Type::getVoidTy(llvmContext);
                if( _resultType != nullptr )
                    llvmResultType = _resultType->LlvmType;
                std::vector<const llvm::Type*> llvmParamTypes;

                auto llvmThisParamType = _outerClass->LlvmPointerType;
                auto thisParamType = gcnew LlvmEmitType(
                    Target,
                    llvmThisParamType);
                llvmParamTypes.push_back( llvmThisParamType );
                for each( LlvmEmitVal^ p in _parameters )
                {
                    auto paramType = (LlvmEmitType^) p->Type;
                    llvmParamTypes.push_back( paramType->LlvmType );
                }

                auto llvmFunctionType = llvm::FunctionType::get(
                    llvmResultType,
                    llvmParamTypes,
                    false);

                _llvmFunction = llvm::Function::Create(
                    llvmFunctionType,
                    llvm::Function::ExternalLinkage,
                    llvm::StringRef(NativeString(_name)),
                    _outerClass->Module->LlvmModule);
                _llvmFunction->setCallingConv(llvm::CallingConv::X86_StdCall);


                auto argIterator = _llvmFunction->arg_begin();
                llvm::Value* llvmThisParam = argIterator;
                llvmThisParam->setName("this");
                ++argIterator;

                for each( LlvmEmitVal^ p in _parameters )
                {
                    llvm::Value* llvmParamVal = argIterator;
                    llvmParamVal->setName(llvm::StringRef(NativeString(p->Name)));
                    p->RawLlvmVal = llvmParamVal;
                    ++argIterator;
                }

                _thisParam = gcnew LlvmEmitVal(
                    Target,
                    thisParamType,
                    kEmitValMode_Value,
                    llvmThisParam);

                _entryBlock = gcnew Spark::Emit::LazyEmitBlock(Target, this);

                return _llvmFunction;
            }

            llvm::LLVMContext& LlvmEmitMethod::LlvmContext::get()
            {
                return _outerClass->LlvmContext;
            }

            LlvmEmitTarget^ LlvmEmitMethod::Target::get()
            {
                return _outerClass->Module->Target;
            }

            llvm::Module* LlvmEmitMethod::LlvmModule::get()
            {
                return _outerClass->Module->LlvmModule;
            }

            LlvmEmitModule^ LlvmEmitMethod::Module::get()
            {
                return _outerClass->Module;
            }

            void LlvmEmitMethod::Flush()
            {
                auto llvmEntryBlock = llvm::BasicBlock::Create(
                    LlvmContext,
                    "entry",
                    _llvmFunction);

                auto emitEntryBlock = gcnew LlvmEmitBlock(
                    this,
                    llvmEntryBlock,
                    new llvm::IRBuilder<>(llvmEntryBlock));

                ((Spark::Emit::LazyEmitBlock^) _entryBlock)->ApplyTo( emitEntryBlock );

                // \todo: proper tail block!!!
                llvm::IRBuilder<> tailBuilder(llvmEntryBlock);
                tailBuilder.CreateRetVoid();
            }

            // LlvmEmitClass

            LlvmEmitClass::LlvmEmitClass(
                LlvmEmitTarget^ target,
                LlvmEmitModule^ module,
                String^ name,
                LlvmEmitClass^ baseClass,
                EmitClassFlags flags)
                : _target(target)
                , _module(module)
                , _name(name)
                , _baseClass(baseClass)
                , _flags(flags)
                , _abstractType(nullptr)
                , _pointerType(nullptr)
                , _publicFields(nullptr)
                , _privateFields(nullptr)
                , _structType(nullptr)
            {
                _abstractType = new llvm::PATypeHolder(llvm::OpaqueType::get(LlvmContext));
                _pointerType = llvm::PointerType::getUnqual(*_abstractType);

                _publicFields = gcnew List<LlvmEmitField^>();
                _privateFields = gcnew List<LlvmEmitField^>();

                _methods = gcnew List<LlvmEmitMethod^>();

                if( _baseClass != nullptr )
                {
                    for each( LlvmEmitField^ f in _baseClass->_publicFields )
                        _publicFields->Add( f );
                    for each( LlvmEmitField^ f in _baseClass->_privateFields )
                        _privateFields->Add( f );
                }
            }

            String^ LlvmEmitClass::GetName()
            {
                return _name;
            }

            IEmitField^ LlvmEmitClass::AddPublicField(
                IEmitType^ type,
                String^ name )
            {
                auto result = CreateField(type, name);
                _publicFields->Add(result);
                return result;
            }

            IEmitField^ LlvmEmitClass::AddPrivateField(
                IEmitType^ type,
                String^ name )
            {
                auto result = CreateField(type, name);
                _privateFields->Add(result);
                return result;
            }

            IEmitMethod^ LlvmEmitClass::CreateCtor()
            {
                auto result = gcnew LlvmEmitMethod(
                    this,
                    nullptr,
                    String::Format("{0}::{1}", _name, _name));
                _ctor = result;
                _methods->Add(result);
                return result;
            }

            IEmitMethod^ LlvmEmitClass::CreateDtor()
            {
                auto result = gcnew LlvmEmitMethod(
                    this,
                    nullptr,
                    String::Format("{0}::~{1}", _name, _name));
                _dtor = result;
                _methods->Add(result);
                return result;
            }

            IEmitMethod^ LlvmEmitClass::CreateMethod(IEmitType^ resultType, String^ name)
            {
                auto result = gcnew LlvmEmitMethod(
                    this,
                    resultType,
                    name);
                _submit = result;
                _methods->Add(result);
                return result;
            }

            void LlvmEmitClass::Seal()
            {
                if( _structType == nullptr )
                {
//                    std::ofstream dumpFile("./dump.txt");

//                    dumpFile << std::string(NativeString(_name)).c_str();

                    std::vector<const llvm::Type*> fieldTypes;
                    UInt32 fieldIndex = 0;

                    for each( LlvmEmitField^ f in _publicFields )
                    {
                        f->FieldIndex = fieldIndex++;
                        fieldTypes.push_back( ((ILlvmEmitType^) f->Type)->LlvmType );

//                        dumpFile << fieldIndex << " : " << std::string(NativeString(f->Name)).c_str() << std::endl;

                    }
                    for each( LlvmEmitField^ f in _privateFields )
                    {
                        f->FieldIndex = fieldIndex++;
                        fieldTypes.push_back( ((ILlvmEmitType^) f->Type)->LlvmType );

//                        dumpFile << fieldIndex << " : " << std::string(NativeString(f->Name)).c_str() << std::endl;
                    }

                    _structType = llvm::StructType::get(LlvmContext, fieldTypes);
                    llvm::cast<llvm::OpaqueType>(_abstractType->get())->refineAbstractTypeTo(_structType);

//                    _publicFields = nullptr;
//                    _privateFields = nullptr;
                }

                for each( LlvmEmitMethod^ m in _methods )
                {
                    m->Flush();
                }
            }

            llvm::LLVMContext& LlvmEmitClass::LlvmContext::get()
            {
                return _module->LlvmContext;
            }

            LlvmEmitModule^ LlvmEmitClass::Module::get()
            {
                return _module;
            }

            LlvmEmitField^ LlvmEmitClass::CreateField(
                IEmitType^ type,
                String^ name )
            {
                return gcnew LlvmEmitField(
                    (ILlvmEmitType^) type,
                    name,
                    (UInt32) 0xffffffff);
            }

            const llvm::Type* LlvmEmitClass::LlvmPointerType::get()
            {
                return _pointerType;
            }

            const llvm::Type* LlvmEmitClass::LlvmType::get()
            {
                return _structType;
            }

            IEmitTarget^ LlvmEmitClass::Target::get()
            {
                return _target;
            }

            unsigned int LlvmEmitClass::Size::get()
            {
                return GetTypeInfo(_structType).size;
            }

            unsigned int LlvmEmitClass::Alignment::get()
            {
                return GetTypeInfo(_structType).align;
            }

            // LlvmEmitStruct

            LlvmEmitStruct::LlvmEmitStruct(
                    llvm::LLVMContext& llvmContext,
                    String^ name)
                    
            {
                _abstractType = new llvm::PATypeHolder(llvm::OpaqueType::get(llvmContext));
                _pointerType = llvm::PointerType::getUnqual(*_abstractType);

                _fieldTypes = new std::vector<const llvm::Type*>();
            }

            IEmitField^ LlvmEmitStruct::AddField(
                IEmitType^ type,
                String^ name )
            {
                auto llvmEmitType = (LlvmEmitType^) type;
                auto llvmType = llvmEmitType->LlvmType;

                auto fieldIndex = _fieldTypes->size();
                _fieldTypes->push_back(llvmType);

                return gcnew LlvmEmitField(
                    (LlvmEmitType^) type,
                    name,
                    (UInt32) fieldIndex);
            }

            UInt32 LlvmEmitStruct::Size::get()
            {
                throw gcnew NotImplementedException();
            }

            UInt32 LlvmEmitStruct::Alignment::get()
            {
                throw gcnew NotImplementedException();
            }

            // LlvmEmitField

            LlvmEmitField::LlvmEmitField(
                ILlvmEmitType^ type,
                String^ name,
                UInt32 fieldIndex )
                : _type(type)
                , _name(name)
                , _fieldIndex(fieldIndex)
            {
            }

            IEmitType^ LlvmEmitField::Type::get()
            {
                return _type;
            }

            // LlvmEmitModule

            LlvmEmitModule::LlvmEmitModule(
                LlvmEmitTarget^ target,
                llvm::LLVMContext& llvmContext,
                String^ name)
                : _target(target)
                , _llvmContext(llvmContext)
            {
                _llvmModule = new llvm::Module(
                    llvm::StringRef(NativeString(name)),
                    _llvmContext);

                // Create a "debug" function...

                auto debugResultType = llvm::Type::getVoidTy(llvmContext);

                std::vector<const llvm::Type*> debugParamTypes;
                debugParamTypes.push_back( llvm::Type::getInt8PtrTy(llvmContext) );

                auto debugFunctionType = llvm::FunctionType::get(
                    debugResultType,
                    debugParamTypes,
                    false);

                _debugFunction = llvm::Function::Create(
                    debugFunctionType,
                    llvm::Function::ExternalLinkage,
                    "debug",
                    _llvmModule);
                _debugFunction->setCallingConv(llvm::CallingConv::X86_StdCall);

                _builtinFunctions = gcnew Dictionary<String^, LlvmBuiltinFunction^>();
            }

            IEmitStruct^ LlvmEmitModule::CreateStruct(String^ name)
            {
                return gcnew LlvmEmitStruct(
                    _llvmContext,
                    name);
            }

            IEmitVal^ LlvmEmitModule::LiteralString(String^ val)
            {
                auto type = (LlvmEmitType^) Target->GetOpaqueType("v*");

                llvm::Constant* strConstant = llvm::ConstantArray::get(_llvmContext, llvm::StringRef(NativeString(val)), true);

                llvm::GlobalVariable* strVar = new llvm::GlobalVariable(
                    *_llvmModule,
                    strConstant->getType(),
                    true,
                    llvm::GlobalValue::InternalLinkage,
                    strConstant,
                    "",
                    0,
                    false);

                llvm::Constant* voidPtrVal = llvm::ConstantExpr::getBitCast(strVar, type->LlvmType);

                return gcnew LlvmEmitVal(
                    Target,
                    type,
                    kEmitValMode_Value,
                    voidPtrVal);
            }

            IEmitVal^ LlvmEmitModule::EmitGlobalStruct(
                String^ name,
                array<IEmitVal^>^ members )
            {
                std::vector<llvm::Constant*> llvmMembers;
                for each( IEmitVal^ member in members )
                {
                    // \todo: What if we have an address instead of a value!!!!
                    auto llvmMember = ((LlvmEmitVal^) member)->RawLlvmVal;
                    llvmMembers.push_back((llvm::Constant*) llvmMember);
                }

                llvm::Constant* structVal = llvm::ConstantStruct::get(_llvmContext, llvmMembers, false);

                bool isInternal = name == nullptr;

                llvm::GlobalVariable* structVar = new llvm::GlobalVariable(
                    *_llvmModule,
                    structVal->getType(),
                    true,
                    isInternal
                        ? llvm::GlobalValue::InternalLinkage
                        : llvm::GlobalValue::ExternalLinkage,
                    structVal,
                    isInternal
                        ? ""
                        : llvm::StringRef(NativeString(name)),
                    0,
                    false);

                auto emptyStructType = gcnew LlvmEmitType(
                    Target,
                    llvm::StructType::get(_llvmContext));
                auto voidPtrType = (LlvmEmitType^) Target->GetOpaqueType("v*");

                llvm::Constant* voidPtrVal = llvm::ConstantExpr::getBitCast(structVar, voidPtrType->LlvmType);
                return gcnew LlvmEmitVal(
                    Target,
                    emptyStructType,
                    kEmitValMode_Address,
                    voidPtrVal);
            }

            IEmitVal^ LlvmEmitModule::GetMethodPointer(
                    IEmitMethod^ method )
            {
                auto llvmEmitMethod = (LlvmEmitMethod^) method;

                auto voidPtrType = (LlvmEmitType^) Target->GetOpaqueType("v*");
                llvm::Constant* voidPtrVal = llvm::ConstantExpr::getBitCast(llvmEmitMethod->LlvmFunction, voidPtrType->LlvmType);

                return gcnew LlvmEmitVal(
                    Target,
                    voidPtrType,
                    kEmitValMode_Value,
                    voidPtrVal);

            }

            llvm::LLVMContext& LlvmEmitModule::LlvmContext::get()
            {
                return _llvmContext;
            }

            llvm::Module* LlvmEmitModule::LlvmModule::get()
            {
                return _llvmModule;
            }

            llvm::Function* LlvmEmitModule::GetBuiltinFunction(
                    String^ name,
                    IEmitType^ resultType,
                    IEnumerable<IEmitVal^>^ args )
            {
                if( _builtinFunctions->ContainsKey( name ) )
                {
                    return _builtinFunctions[ name ]->LlvmFunction;
                }

                auto llvmResultType = ((ILlvmEmitType^) resultType)->LlvmType;

                std::vector<const llvm::Type*> llvmParamTypes;
                for each( IEmitVal^ a in args )
                {
                    llvmParamTypes.push_back( ((ILlvmEmitType^) a->Type)->LlvmType );
                }

                auto llvmFunctionType = llvm::FunctionType::get(
                    llvmResultType,
                    llvmParamTypes,
                    false);

                auto llvmFunction = llvm::Function::Create(
                    llvmFunctionType,
                    llvm::Function::ExternalLinkage,
                    llvm::StringRef(NativeString(name)),
                    _llvmModule);
                llvmFunction->setCallingConv(llvm::CallingConv::X86_StdCall);

                auto builtinFunction = gcnew LlvmBuiltinFunction();
                builtinFunction->LlvmFunction = llvmFunction;

                _builtinFunctions[name] = builtinFunction;
                return llvmFunction;
            }

            // LlvmEmitType

            IEmitTarget^ LlvmEmitType::Target::get()
            {
                return _target;
            }


            // LlvmEmitTarget

            LlvmEmitTarget::LlvmEmitTarget()
                : _llvmContext(llvm::getGlobalContext())
                , _callback(nullptr)
            {
                _builtinTypes = gcnew Dictionary<String^, LlvmEmitType^>();

                auto llvmVoidType = llvm::Type::getVoidTy(_llvmContext);
                _voidType = gcnew LlvmEmitType(this, llvmVoidType);
                AddBuiltinType("void", llvmVoidType);

                auto voidPointerType = llvm::PointerType::getUnqual(llvm::StructType::get(_llvmContext));
                auto u32 = llvm::IntegerType::get(_llvmContext, 32);

                auto byteType = llvm::IntegerType::get(_llvmContext, 8);
                AddBuiltinType("v*", voidPointerType);
                AddBuiltinType("u16", llvm::IntegerType::get(_llvmContext, 16));
                AddBuiltinType("u32", u32);
                AddBuiltinType("s32", llvm::IntegerType::get(_llvmContext, 32));
                AddBuiltinType("int", llvm::IntegerType::get(_llvmContext, 32));

                auto floatType = llvm::Type::getFloatTy(_llvmContext);
                AddBuiltinType("f32", floatType);
                AddBuiltinType("float", floatType);

                auto float3Type = MakeVectorType(floatType, 3);
                AddBuiltinType("f32^3", float3Type);
                auto float3x3Type = MakeVectorType(float3Type, 3);
                AddBuiltinType("f32^3^3", float3x3Type);

                auto float4Type = MakeVectorType(floatType, 4);
                AddBuiltinType("f32^4", float4Type);
                auto float4x4Type = MakeVectorType(float4Type, 4);
                AddBuiltinType("f32^4^4", float4x4Type);

                AddBuiltinType("UINT", u32);

                auto rtBlendDesc = llvm::StructType::get(
                    _llvmContext,
                    u32, u32, u32, u32,
                    u32, u32, u32, u32, NULL);
                AddBuiltinType(
                    "D3D11_RENDER_TARGET_BLEND_DESC",
                    rtBlendDesc );

                auto rtBlendDescArray = llvm::ArrayType::get(
                    rtBlendDesc,
                    8 );

                AddBuiltinType(
                    "D3D11_BLEND_DESC",
                    llvm::StructType::get(_llvmContext,
                        u32, u32, rtBlendDescArray, NULL ));

                AddBuiltinType(
                    "D3D11_INPUT_ELEMENT_DESC",
                    llvm::StructType::get(_llvmContext,
                        voidPointerType, u32, u32, u32,
                        u32, u32, u32, NULL));

                AddBuiltinType(
                    "D3D11_SUBRESOURCE_DATA",
                    llvm::StructType::get(_llvmContext,
                        voidPointerType, u32, u32, NULL));

                AddBuiltinType(
                    "D3D11_MAPPED_SUBRESOURCE",
                    llvm::StructType::get(_llvmContext,
                        voidPointerType, u32, u32, NULL));

                auto primitiveSpanType = AddBuiltinType(
                    "spark::d3d11::PrimitiveSpan",
                    llvm::StructType::get(_llvmContext,
                    u32, u32,
                    voidPointerType,
                    u32, u32,
                    u32, u32, u32, u32, u32,
                    NULL));

                auto indexStreamType = AddBuiltinType(
                    "spark::d3d11::IndexStream",
                    llvm::StructType::get(_llvmContext,
                        voidPointerType,
                        u32, u32, NULL));

                AddBuiltinType(
                    "spark::d3d11::DrawSpan",
                    llvm::StructType::get(_llvmContext,
                        indexStreamType,
                        primitiveSpanType,
                        NULL));

                AddBuiltinType(
                    "spark::d3d11::VertexStream",
                    llvm::StructType::get(_llvmContext,
                        voidPointerType,
                        u32, u32, NULL));

                AddBuiltinType(
                    "D3D11_BUFFER_DESC",
                    llvm::StructType::get(_llvmContext,
                        u32, u32, u32,
                        u32, u32, u32, NULL));

                _comMethods = gcnew Dictionary<String^, COMMethodInfo^>();

                AddCOM("IUnknown", "AddRef", 1, COMFunctionType("u32", gcnew array<String^> {}));
                AddCOM("IUnknown", "Release", 2, COMFunctionType("u32", gcnew array<String^> {}));
                AddCOM("ID3D11Device", "CreateBuffer", 3, COMFunctionType("u32", gcnew array<String^> {"v*","v*","v*",}));
                AddCOM("ID3D11Device", "CreateVertexShader", 12, COMFunctionType("u32", gcnew array<String^> {"v*","u32","v*","v*",}));
                AddCOM("ID3D11Device", "CreateHullShader", 16, COMFunctionType("u32", gcnew array<String^> {"v*","u32","v*","v*",}));
                AddCOM("ID3D11Device", "CreateDomainShader", 17, COMFunctionType("u32", gcnew array<String^> {"v*","u32","v*","v*",}));
                AddCOM("ID3D11Device", "CreateGeometryShader", 13, COMFunctionType("u32", gcnew array<String^> {"v*","u32","v*","v*",}));
                AddCOM("ID3D11Device", "CreatePixelShader", 15, COMFunctionType("u32", gcnew array<String^> {"v*","u32","v*","v*",}));
                AddCOM("ID3D11Device", "CreateBlendState", 20, COMFunctionType("u32", gcnew array<String^> {"v*","v*",}));
                AddCOM("ID3D11Device", "CreateInputLayout", 11, COMFunctionType("u32", gcnew array<String^> {"v*","u32","v*","u32","v*",}));
                AddCOM("ID3D11Buffer", "Release", 2, COMFunctionType("u32", gcnew array<String^> {}));
                AddCOM("ID3D11DeviceContext", "Map", 14, COMFunctionType("u32", gcnew array<String^> {"v*","u32","u32","u32","v*",}));
                AddCOM("ID3D11DeviceContext", "Unmap", 15, COMFunctionType("void", gcnew array<String^> {"v*","u32",}));
                AddCOM("ID3D11DeviceContext", "VSSetShader", 11, COMFunctionType("void", gcnew array<String^> {"v*","v*","u32",}));
                AddCOM("ID3D11DeviceContext", "VSSetConstantBuffers", 7, COMFunctionType("void", gcnew array<String^> {"u32","u32","v*",}));
                AddCOM("ID3D11DeviceContext", "VSSetShaderResources", 25, COMFunctionType("void", gcnew array<String^> {"u32","u32","v*",}));
                AddCOM("ID3D11DeviceContext", "VSSetSamplers", 26, COMFunctionType("void", gcnew array<String^> {"u32","u32","v*",}));
                AddCOM("ID3D11DeviceContext", "HSSetShader", 60, COMFunctionType("void", gcnew array<String^> {"v*","v*","u32",}));
                AddCOM("ID3D11DeviceContext", "HSSetConstantBuffers", 62, COMFunctionType("void", gcnew array<String^> {"u32","u32","v*",}));
                AddCOM("ID3D11DeviceContext", "HSSetShaderResources", 59, COMFunctionType("void", gcnew array<String^> {"u32","u32","v*",}));
                AddCOM("ID3D11DeviceContext", "HSSetSamplers", 61, COMFunctionType("void", gcnew array<String^> {"u32","u32","v*",}));
                AddCOM("ID3D11DeviceContext", "DSSetShader", 64, COMFunctionType("void", gcnew array<String^> {"v*","v*","u32",}));
                AddCOM("ID3D11DeviceContext", "DSSetConstantBuffers", 66, COMFunctionType("void", gcnew array<String^> {"u32","u32","v*",}));
                AddCOM("ID3D11DeviceContext", "DSSetShaderResources", 63, COMFunctionType("void", gcnew array<String^> {"u32","u32","v*",}));
                AddCOM("ID3D11DeviceContext", "DSSetSamplers", 65, COMFunctionType("void", gcnew array<String^> {"u32","u32","v*",}));
                AddCOM("ID3D11DeviceContext", "GSSetShader", 23, COMFunctionType("void", gcnew array<String^> {"v*","v*","u32",}));
                AddCOM("ID3D11DeviceContext", "GSSetConstantBuffers", 22, COMFunctionType("void", gcnew array<String^> {"u32","u32","v*",}));
                AddCOM("ID3D11DeviceContext", "GSSetShaderResources", 31, COMFunctionType("void", gcnew array<String^> {"u32","u32","v*",}));
                AddCOM("ID3D11DeviceContext", "GSSetSamplers", 32, COMFunctionType("void", gcnew array<String^> {"u32","u32","v*",}));
                AddCOM("ID3D11DeviceContext", "PSSetShader", 9, COMFunctionType("void", gcnew array<String^> {"v*","v*","u32",}));
                AddCOM("ID3D11DeviceContext", "PSSetConstantBuffers", 16, COMFunctionType("void", gcnew array<String^> {"u32","u32","v*",}));
                AddCOM("ID3D11DeviceContext", "PSSetShaderResources", 8, COMFunctionType("void", gcnew array<String^> {"u32","u32","v*",}));
                AddCOM("ID3D11DeviceContext", "PSSetSamplers", 10, COMFunctionType("void", gcnew array<String^> {"u32","u32","v*",}));
                AddCOM("ID3D11DeviceContext", "OMSetBlendState", 35, COMFunctionType("void", gcnew array<String^> {"v*","v*","u32",}));
                AddCOM("ID3D11DeviceContext", "OMSetRenderTargets", 33, COMFunctionType("void", gcnew array<String^> {"u32","v*","v*",}));
                AddCOM("ID3D11DeviceContext", "IASetInputLayout", 17, COMFunctionType("void", gcnew array<String^> {"v*",}));
                AddCOM("ID3D11DeviceContext", "IASetVertexBuffers", 18, COMFunctionType("void", gcnew array<String^> {"u32","u32","v*","v*","v*",}));
                AddCOM("ID3D11DeviceContext", "IASetPrimitiveTopology", 24, COMFunctionType("void", gcnew array<String^> {"u32",}));
                AddCOM("ID3D11DeviceContext", "RSSetState", 43, COMFunctionType("void", gcnew array<String^> {"v*",}));
                AddCOM("ID3D11DeviceContext", "OMSetDepthStencilState", 36, COMFunctionType("void", gcnew array<String^> {"v*","u32",}));
            }

            IEmitType^ LlvmEmitTarget::VoidType::get()
            {
                return _voidType;
            }

            IEmitType^ LlvmEmitTarget::GetBuiltinType(
                String^ format,
                array<IEmitTerm^>^ args )
            {
                if( format == "__Array" )
                {
                    auto elementType = (ILlvmEmitType^) args[0];
                    auto elementCountVal = (LlvmEmitVal^) args[1];

                    auto llvmCountVal = elementCountVal->RawLlvmVal;
                    auto elementCount = llvm::cast<llvm::ConstantInt>(llvmCountVal)->getZExtValue();

                    auto llvmType = llvm::ArrayType::get(
                        elementType->LlvmType,
                        elementCount);

                    return gcnew LlvmEmitType(
                        this,
                        llvmType);

                }

                return GetBuiltinType( format );
            }

            IEmitType^ LlvmEmitTarget::GetOpaqueType(
                String^ name )
            {
                return GetBuiltinType( "v*" );
            }

            IEmitType^ LlvmEmitTarget::Pointer(
                IEmitType^ type )
            {
                auto llvmType = llvm::PointerType::getUnqual(
                    ((ILlvmEmitType^) type)->LlvmType );
                return gcnew LlvmEmitType(
                    this,
                    llvmType);
            }

            IEmitVal^ LlvmEmitTarget::GetNullPointer(
                IEmitType^ type )
            {
                auto llvmVal = llvm::Constant::getNullValue(
                    ((LlvmEmitType^) type)->LlvmType );
                return gcnew LlvmEmitVal(
                    this,
                    type,
                    kEmitValMode_Value,
                    llvmVal);
            }

            const llvm::Type* LlvmEmitTarget::AddBuiltinType(
                String^ name,
                const llvm::Type* llvmType )
            {
                _builtinTypes[name] = gcnew LlvmEmitType(this, llvmType);
                return llvmType;
            }

            IEmitType^ LlvmEmitTarget::GetBuiltinType(
                String^ name )
            {
                return _builtinTypes[ name ];
            }

            const llvm::Type* LlvmEmitTarget::MakeVectorType(
                const llvm::Type* elementType,
                int elementCount )
            {
                std::vector<const llvm::Type*> elementTypes;
                for( int ii = 0; ii < elementCount; ++ii)
                {
                    elementTypes.push_back(elementType);
                }
                return llvm::StructType::get(_llvmContext, elementTypes);
            }

            COMMethodInfo^ LlvmEmitTarget::GetCOMMethod(
                    String^ interfaceName,
                    String^ methodName )
            {
                return _comMethods[ interfaceName + "::" + methodName ];
            }

            void LlvmEmitTarget::AddCOM(
                String^ interfaceName,
                String^ methodName,
                UInt32 methodIndex,
                const llvm::Type* functionType )
            {
                auto name = interfaceName + "::" + methodName;

                auto methodInfo = gcnew COMMethodInfo();
                methodInfo->Name = name;
                methodInfo->Index = methodIndex;
                methodInfo->LlvmType = functionType;

                _comMethods[name] = methodInfo;
            }

            const llvm::Type* LlvmEmitTarget::COMFunctionType(
                String^ res,
                array<String^>^ args )
            {
                auto resType = (LlvmEmitType^) GetBuiltinType(res);
                auto thisType = (LlvmEmitType^) GetBuiltinType("v*");

                auto llvmResType = resType->LlvmType;
                auto llvmThisType = thisType->LlvmType;

                std::vector<const llvm::Type*> llvmParamTypes;
                llvmParamTypes.push_back( llvmThisType );
                for each( auto a in args )
                {
                    auto argType = (LlvmEmitType^) GetBuiltinType(a);
                    auto llvmArgType = argType->LlvmType;

                    llvmParamTypes.push_back( llvmArgType );
                }

                auto llvmFunctionType = llvm::FunctionType::get(
                    llvmResType,
                    llvmParamTypes,
                    false);

                return llvmFunctionType;
            }
        }

        namespace HLSL
        {
            HlslCompiler::HlslCompiler()
            {
                Spark::Emit::HLSL::HlslCompilerHelper::Register( this );
            }

            static array<Byte>^ GetBytes( String^ s )
            {
                return System::Text::ASCIIEncoding::ASCII->GetBytes( s );
            }

            array<Byte>^ HlslCompiler::Compile(
                String^ source,
                String^ entry,
                String^ profile,
                String^% errors )
            {
                pin_ptr<Byte> pinSource = &GetBytes( source )[0];
                pin_ptr<Byte> pinEntry = &GetBytes( entry )[0];
                pin_ptr<Byte> pinProfile = &GetBytes( profile )[0];

                ID3DBlob* codeBlob;
                ID3DBlob* errorBlob;

                HRESULT hr = D3DCompile(
                    (LPCSTR) pinSource,             // source
                    source->Length,                 // source size (bytes)
                    "<generated>",                  // source name
                    nullptr,                        // defines
                    nullptr,                        // includes
                    (LPCSTR) pinEntry,              // entry point
                    (LPCSTR) pinProfile,            // shader profile
                    D3DCOMPILE_ENABLE_STRICTNESS,   // shader flags
                    0,                              // effect flags
                    &codeBlob,                      // output code blob
                    &errorBlob);                    // output error blob

                if( errorBlob != nullptr )
                {
                    errors = gcnew String( (const char *) errorBlob->GetBufferPointer() );
                    errorBlob->Release();
                }
                else
                {
                    errors = nullptr;
                }

                array<Byte>^ code;
                if( codeBlob != nullptr )
                {
                    auto codeSize = codeBlob->GetBufferSize();
                    code = gcnew array<Byte>(codeSize);
                    pin_ptr<Byte> pinCode = &code[0];

                    memcpy( pinCode, codeBlob->GetBufferPointer(), codeSize );

                    codeBlob->Release();
                }
                else
                {
                    code = gcnew array<Byte>{};
                }

                return code;
            }
        }

    }
}
