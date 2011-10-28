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

// context.h
#ifndef SPARK_CONTEXT_H
#define SPARK_CONTEXT_H

#include <D3D11.h>
#include <spark/common.h>

namespace spark
{
    class IContext;
    class IModule;
    class IShaderClass;

    struct ShaderClassDesc;

    class IShaderBytecodeCallback
    {
    public:
        virtual void SPARK_CALL ProcessBytecode(
            const char* stageName,
            unsigned int dataSize,
            const void* data ) = 0;
    };

    class IContext
    {
    public:
        virtual void Acquire() = 0;
        virtual void Release() = 0;

        virtual IModule* SPARK_CALL CompileFile(const char* filename) = 0;

        virtual IShaderClass* SPARK_CALL FindOrLoadShaderClass( const ShaderClassDesc* desc ) = 0;

        template<typename ShaderT>
        __forceinline ShaderT* CreateShaderInstance( ID3D11Device* device )
        {
            auto shaderClass = FindOrLoadShaderClass( ShaderT::GetShaderClassDesc() );
            if( shaderClass == nullptr )
                return nullptr;
            return reinterpret_cast<ShaderT*>(shaderClass->CreateInstance( device ));
        }
    };

    class IModule
    {
    public:
        virtual IShaderClass* SPARK_CALL FindShaderClass( const char* name ) = 0;

        template<typename T>
        inline IShaderClass* FindShaderClass()
        {
            return FindShaderClass( T::StaticGetShaderClassName() );
        }

        virtual IShaderClass* SPARK_CALL CreateShaderClass(
            size_t mixinCount,
            IShaderClass*const* mixins,
            IShaderBytecodeCallback* callback = nullptr ) = 0;
    };

    class IShaderClass
    {
    public:
        virtual const char* SPARK_CALL GetName() = 0;
        virtual void* SPARK_CALL CreateInstance( ID3D11Device* device ) = 0;
    };
}

SPARK_DLL spark::IContext* SparkCreateContext();

#endif
