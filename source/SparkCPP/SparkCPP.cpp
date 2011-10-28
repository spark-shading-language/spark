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

// SparkCPP.cpp

// Include early to avoid FILETIME problems... :(
#define NOMINMAX
#include <Windows.h>

#define SPARK_DLL extern "C" __declspec(dllexport)

#include <spark/context.h>

#include <vcclr.h>
#include <msclr/marshal.h>

#include "LlvmEmitTarget.h"
#include <llvm/Analysis/Verifier.h>
#include <llvm/ExecutionEngine/JIT.h>
#include <llvm/PassManager.h>
#include <llvm/Support/StandardPasses.h>
#include <llvm/Target/TargetSelect.h>

#include <fstream>
#include <llvm/Support/raw_os_ostream.h>
#define SPARK_SKIP_PRAGMA_LIB
#include <spark/spark.h>

using namespace System;
using namespace Spark::Compiler;
using namespace Spark::ResolvedSyntax;

static void __stdcall debug( const char* message )
{
    OutputDebugStringA( message );
}

static spark::d3d11::DrawSpan __stdcall spark_d3d11_DrawIndexed16(
    ID3D11Buffer* buffer,
    UINT indexCount,
    UINT startIndexLocation,
    INT baseVertexLocation )
{
    return spark::d3d11::DrawIndexed(buffer, DXGI_FORMAT_R16_UINT, indexCount, startIndexLocation, baseVertexLocation);
}

static spark::d3d11::DrawSpan __stdcall spark_d3d11_DrawSpan_Create(
    spark::d3d11::IndexStream indices,
    spark::d3d11::PrimitiveSpan primitiveSpan )
{
    return spark::d3d11::DrawSpan(indices, primitiveSpan);
}

static void __stdcall spark_DrawSpan_Bind(
    spark::d3d11::DrawSpan span,
    ID3D11DeviceContext* context )
{
    span.Bind( context );
}

static void __stdcall spark_DrawSpan_Submit(
    spark::d3d11::DrawSpan span,
    ID3D11DeviceContext* context )
{
    span.Submit( context );
}


static void* LazyFunctionCreator( const std::string& name )
{
    if( name == "debug" )
        return &debug;

    if( name == "spark::d3d11::DrawIndexed16" )
        return &spark_d3d11_DrawIndexed16;

    if( name == "spark::d3d11::DrawSpan" )
        return &spark_d3d11_DrawSpan_Create;

    if( name == "{0}.Submit({1})" )
        return &spark_DrawSpan_Submit;

    if( name == "{0}.Bind({1})" )
        return &spark_DrawSpan_Bind;

    OutputDebugStringA( "Failed to load function:" );
    OutputDebugStringA( name.c_str() );

    return NULL;
}

template<typename T>
struct ValueWrapper
{
public:
    ValueWrapper() {}

    T* GetValuePtr() { return &_value; }
    const T* GetValuePtr() const { return &_value; }

    T* operator->() { return &_value; }
    const T* operator->() const { return &_value; }

private:
    T _value;
};

template<typename T>
static bool operator<( const ValueWrapper<T>& left, const ValueWrapper<T>& right )
{
    return memcmp(
        left.GetValuePtr(),
        right.GetValuePtr(),
        sizeof(T)) < 0;
}

namespace spark
{
    namespace d3d11
    {
        SPARK_DLL ID3D11SamplerState* CreateSamplerState(
            ID3D11Device* device,
            D3D11_FILTER                  filter,
            D3D11_TEXTURE_ADDRESS_MODE    addressU,
            D3D11_TEXTURE_ADDRESS_MODE    addressV,
            D3D11_TEXTURE_ADDRESS_MODE    addressW,
            float                         mipLODBias,
            UINT                          maxAnisotropy,
            D3D11_COMPARISON_FUNC         comparisonFunc,
            float4                        borderColor,
            float                         minLOD,
            float                         maxLOD )
        {
            typedef std::map<ValueWrapper<D3D11_SAMPLER_DESC>, ID3D11SamplerState*> Map;
            static Map map;

            ValueWrapper<D3D11_SAMPLER_DESC> desc;
            desc->Filter = filter;
            desc->AddressU = addressU;
            desc->AddressV = addressV;
            desc->AddressW = addressW;
            desc->MipLODBias = mipLODBias;
            desc->MaxAnisotropy = maxAnisotropy;
            desc->ComparisonFunc = comparisonFunc;
            for( int ii = 0; ii < 4; ++ii )
                desc->BorderColor[ii] = borderColor[ii];
            desc->MinLOD = minLOD;
            desc->MaxLOD = maxLOD;

            auto ii = map.find( desc );
            if( ii != map.end() )
                return ii->second;

            ID3D11SamplerState* result = nullptr;
            device->CreateSamplerState( desc.GetValuePtr(), &result );
            map.insert( std::make_pair( desc, result ) );
            return result;
        }

        SPARK_DLL ID3D11RasterizerState* CreateRasterizerState(
            D3D11_FILL_MODE   fillMode,
            D3D11_CULL_MODE   cullMode,
            bool              frontCounterClockwise,
            int               depthBias,
            float             depthBiasClamp,
            float             slopeScaledDepthBias,
            bool              depthClipEnable,
            bool              scissorEnable,
            bool              multisampleEnable,
            bool              antialiasedLineEnable )
        {
            return nullptr;
        }
    }
}


namespace spark
{
    class ShaderClass;
    class Module;
    class Context;

    struct ShaderClassDesc
    {
        unsigned int sizeInBytes;
        unsigned int facetCount;
        const void* facetInfo;
        void (__stdcall *Initialize)( void* obj, void* device );
        void (__stdcall *Finalize)( void* obj );
        void (__stdcall *Submit)( void* obj, void* device, void* context );
    };

    class ShaderClass : public IShaderClass
    {
    public:
        ShaderClass(
            IResPipelineRef^ resShaderClass,
            const ShaderClassDesc* desc,
            const char* name)
            : _desc(desc)
            , _name(name)
        {
            _resShaderClass = resShaderClass;
        }

        virtual const char* SPARK_CALL GetName()
        {
            return _name.c_str();
        }

        virtual void* SPARK_CALL CreateInstance(
            ID3D11Device* device )
        {
            struct Instance
            {
                const void* info;
                unsigned int referenceCount;
            };

            auto result = (Instance*) calloc( _desc->sizeInBytes, 1 );
            result->info = _desc;
            result->referenceCount = 1;

            _desc->Initialize( result, device );

            return result;
        }

        IResPipelineRef^ GetResShaderClass()
        {
            return _resShaderClass;
        }

    private:
        gcroot<IResPipelineRef^> _resShaderClass;
        const ShaderClassDesc* _desc;
        std::string _name;
    };

    class Module : public IModule
    {
    public:
        Module(
            Context* context,
            IResModuleDecl^ resModule,
            Spark::Emit::LLVM::LlvmEmitModule^ emitModule )
            : _context(context)
        {
            _resModule = resModule;
            _emitModule = emitModule;
            _llvmModule = _emitModule->LlvmModule;
            _llvmEngine = nullptr;
        }

        virtual IShaderClass* SPARK_CALL FindShaderClass(
            const char* inClassName )
        {
            std::string className(inClassName);

            // Need to find the 'res' version of the class, too:
            auto resClass = ResModuleHelpers::FindShaderClass( _resModule, msclr::interop::marshal_as<String^>(inClassName) );
            if( resClass == nullptr )
                return nullptr;


            auto classDescGlobal = _llvmModule->getNamedValue(className.c_str());
            ShaderClassDesc* classDesc = nullptr;
            if( classDescGlobal != nullptr )
            {
                classDesc = (ShaderClassDesc*) _llvmEngine->getPointerToGlobal( classDescGlobal );
            }

            return new ShaderClass( resClass, classDesc, inClassName );
        }

        void OptimizeAndCompile()
        {
            LLVMLinkInJIT();
            llvm::InitializeNativeTarget();

            std::string errorStr;


            llvm::PassManager passManager;

            passManager.add(llvm::createVerifierPass());                  // Verify that input is correct

            auto inliningPass = llvm::createFunctionInliningPass();

            llvm::createStandardFunctionPasses(&passManager, 3);

            llvm::createStandardModulePasses(
                &passManager,
                3,
                /*OptimizeSize=*/ false,
                /*UnitAtATime=*/ true,
                /*UnrollLoops=*/ true,
                /*SimplifyLibCalls=*/ true,
                /*HaveExceptions=*/ true,
                inliningPass);

            passManager.run( *_llvmModule );

            /*/
            {
                std::ofstream dumpFile("./dump.txt");

                dumpFile << errorStr.c_str() << "\n\n";

                llvm::raw_os_ostream dumpStream(dumpFile);
                llvmModule->print(dumpStream, nullptr);
            }
            //*/

            llvm::EngineBuilder engineBuilder(_llvmModule);
            engineBuilder.setEngineKind(llvm::EngineKind::JIT);
            engineBuilder.setErrorStr(&errorStr);


            _llvmEngine = engineBuilder.create();
            if( _llvmEngine == nullptr )
            {
                char buffer[1024];
                sprintf(buffer, "%s\n", errorStr.c_str());

                throw "Couldn't create engine";
            }

            _llvmEngine->DisableLazyCompilation();
            _llvmEngine->InstallLazyFunctionCreator( &LazyFunctionCreator );

            _llvmEngine->runStaticConstructorsDestructors(false);

            // Compile everything.
            for( auto ii = _llvmModule->begin(), ie = _llvmModule->end(); ii != ie; ++ii)
            {
                auto& function = *ii;
                _llvmEngine->runJITOnFunction(&function);
            }
        }

        virtual IShaderClass* SPARK_CALL CreateShaderClass(
            size_t mixinCount,
            IShaderClass*const* mixins,
            IShaderBytecodeCallback* callback = nullptr );

    private:
        Context* _context;
        gcroot<IResModuleDecl^> _resModule;
        gcroot<Spark::Emit::LLVM::LlvmEmitModule^> _emitModule;
        llvm::Module* _llvmModule;
        llvm::ExecutionEngine* _llvmEngine;
    };

    class Context : public IContext
    {
    public:
        Context()
            : _referenceCount(1)
        {
            _identifiers = gcnew Spark::IdentifierFactory();
        }

        virtual void Acquire()
        {
            ::InterlockedIncrement( &_referenceCount );
        }

        virtual void Release()
        {
            unsigned __int32 result = ::InterlockedDecrement( &_referenceCount );
            if( result != 0 )
            {
                return;
            }

            delete this;
        }

        virtual IModule* SPARK_CALL CompileFile(const char* filename)
        {
            auto compiler = gcnew Compiler();
            compiler->Identifiers = _identifiers;

            auto cliFilename = gcnew String(filename);
            compiler->AddInput( cliFilename );

            int errorCount = 0;
            errorCount += compiler->Parse();
            if( errorCount != 0 )
                return nullptr;

            errorCount += compiler->Resolve();
            if( errorCount != 0 )
                return nullptr;

            _midContext = gcnew Spark::Mid::MidEmitContext(_identifiers);
            errorCount += compiler->Lower(_midContext);
            if( errorCount != 0 )
                return nullptr;

            auto midModule = compiler->MidModule;

            auto target = gcnew Spark::Emit::LLVM::LlvmEmitTarget();
            _emitContext = gcnew Spark::Emit::EmitContext();
            _emitContext->Target = target;
            _emitContext->Identifiers = compiler->Identifiers;
            _emitContext->Diagnostics = compiler->Diagnostics;

            auto emitModule = (Spark::Emit::LLVM::LlvmEmitModule^) _emitContext->EmitModule(midModule);

            auto module = new Module( this, compiler->ResModule, emitModule );
            module->OptimizeAndCompile();
            return module;
        }

        virtual IShaderClass* SPARK_CALL FindOrLoadShaderClass( const ShaderClassDesc* desc )
        {
            return new ShaderClass(
                nullptr,
                desc,
                * ((const char**) desc->facetInfo));
        }

        Spark::IdentifierFactory^ GetIdentifiers() { return _identifiers; }
        Spark::Mid::MidEmitContext^ GetMidContext() { return _midContext; }
        Spark::Emit::EmitContext^ GetEmitContext() { return _emitContext; }

    private:
        unsigned __int32 _referenceCount;
        gcroot<Spark::IdentifierFactory^> _identifiers;
        gcroot<Spark::Mid::MidEmitContext^> _midContext;
        gcroot<Spark::Emit::EmitContext^> _emitContext;
    };

    //

    ref class DiagnosticsWriter :
        Spark::IDiagnosticsWriter
    {
    public:
        virtual void Write(String^ value)
        {
            msclr::interop::marshal_context m;
            OutputDebugStringA(m.marshal_as<const char*>(value));
        }
    };

    IShaderClass* Module::CreateShaderClass(
        size_t mixinCount,
        IShaderClass*const* mixins,
        IShaderBytecodeCallback* callback )
    {
        auto resMixins = gcnew List<IResPipelineRef^>();

        for( size_t ii = 0; ii < mixinCount; ++ii )
        {
            IShaderClass* mixin = mixins[ii];
            if( mixin == nullptr )
                return nullptr;

            IResPipelineRef^ resMixin = ((ShaderClass*) mixin)->GetResShaderClass();
            resMixins->Add(resMixin);
        }

        auto identifiers = _context->GetIdentifiers();
        auto diagnostics = gcnew Spark::DiagnosticSink();
        auto resolveContext = gcnew Spark::Resolve::ResolveContext(identifiers, diagnostics);

        auto resModule = resolveContext->ResolveDynamicShaderClass( resMixins );

        if( Spark::DiagnosticsExtensions::Dump(diagnostics, gcnew DiagnosticsWriter()) != 0 )
        {
            return nullptr;
        }

        IResMemberDecl^ resShaderClass = nullptr;
        for each( IResMemberDecl^ d in resModule->Decls )
        {
            resShaderClass = d;
        }

        msclr::interop::marshal_context marshal;
        std::string name = marshal.marshal_as<const char*>(resShaderClass->Name->ToString());

        auto midContext = _context->GetMidContext();
        auto midModule = midContext->EmitModule( resModule );

        auto emitContext = _context->GetEmitContext();
        auto emitTarget = (Spark::Emit::LLVM::LlvmEmitTarget^) emitContext->Target;

        emitTarget->SetCallback( callback );
        auto emitModule = (Spark::Emit::LLVM::LlvmEmitModule^) emitContext->EmitModule(midModule);
        emitTarget->SetCallback( nullptr );

        auto module = new Module( _context, resModule, emitModule );
        module->OptimizeAndCompile();

        auto shaderClass = module->FindShaderClass(name.c_str());
        return shaderClass;
    }
}

SPARK_DLL spark::IContext* SparkCreateContext()
{
    return new spark::Context();
}

SPARK_DLL void SparkRegisterHlslCompiler()
{
    auto compiler = gcnew Spark::Emit::HLSL::HlslCompiler();
}
