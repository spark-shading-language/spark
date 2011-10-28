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

// spark.h
#ifndef SPARK_SPARK_H
#define SPARK_SPARK_H

#include <D3D11.h>

#include <spark/context.h>
#include <cmath>

#ifndef SPARK_SKIP_PRAGMA_LIB
#pragma comment(lib, "SparkCPP")
#endif // SPARK_SKIP_PRAGMA_LIB

namespace spark
{
    template<typename E, int N>
    class Array
    {
    public:
        Array()
        {
        }

        Array( const E elements[N] )
        {
            for( int ii = 0; ii < N; ++ii )
                _elements[ii] = elements[ii];
        }

    private:
        E _elements[N];
    };

    class float3
    {
    public:
        float3() {}
        float3(float x, float y, float z)
            : x(x), y(y), z(z)
        {}

        float x, y, z;
    };

    inline float3 normalize( float3 value )
    {
        float invLength = 1.0f / sqrtf(value.x*value.x + value.y*value.y + value.z*value.z);
        float3 result(
            value.x * invLength,
            value.y * invLength,
            value.z * invLength );
        return result;
    }

    class float4
    {
    public:
        float4()
        {}

        float4( float x, float y, float z, float w )
            : x(x), y(y), z(z), w(w)
        {}

        float4( spark::float3 xyz, float w )
            : x(xyz.x), y(xyz.y), z(xyz.z), w(w)
        {}

        operator const float*() const { return &x; }
        operator float*() { return &x; }

        float x, y, z, w;
    };

    class float4x4
    {
    public:
        float& operator()( int r, int c )
        {
            return (&c0)[c][r];
        }

        const float& operator()( int r, int c ) const
        {
            return (&c0)[c][r];
        }

    private:
        float4 c0, c1, c2, c3;
    };

    inline float4x4 operator*(
        float4x4 left,
        float4x4 right )
    {
        float4x4 result;
        for( int r = 0; r < 4; ++r )
        {
            for( int c = 0; c < 4; ++c )
            {
                float sum = 0.0f;
                for( int ii = 0; ii < 4; ++ii )
                {
                    sum += left(r,ii) * right(ii,c);
                }
                result(r,c) = sum;
            }
        }
        return result;
    }

    inline float4x4 transpose(
        float4x4 value )
    {
        float4x4 result;
        for( int r = 0; r < 4; ++r )
        {
            for( int c = 0; c < 4; ++c )
            {
                result(r,c) = value(c,r);
            }
        }
        return result;
    }

    struct ShaderClassDesc;

    class ShaderInstance
    {
    public:
        void Acquire()
        {
            ::InterlockedIncrement( &_referenceCount );
        }

        void Release()
        {
            unsigned __int32 result = ::InterlockedDecrement( &_referenceCount );
            if( result != 0 )
            {
                return;
            }

            // \todo: This really belongs in the
            // SparkCPP.lib, but getting it in
            // there involves linker trickery
            // I don't feel like doing right now... :(
            struct ClassInfo
            {
                int a;
                int b;
                void* c;
                void* d;
                void (__stdcall * Finalize)( void* obj );
            };

            ClassInfo* ci = (ClassInfo*) _shaderClassInfo;
            ci->Finalize( this );
        }

        void* FindFacet( const char* name )
        {
            struct FacetInfo
            {
                const char* name;
                int offset;
            };
            struct ClassInfo
            {
                int size;
                int facetCount;
                FacetInfo* facets;
            };

            ClassInfo* ci = (ClassInfo*) _shaderClassInfo;

            for( int ff = 0; ff < ci->facetCount; ++ff )
            {
                auto& facetInfo = ci->facets[ff];

                if( strcmp(name, facetInfo.name) == 0 )
                {
                    return ((unsigned char*) this) + facetInfo.offset;
                }
            }

            return nullptr;
        }

        void* FindFacet( IShaderClass* shaderClass )
        {
            return FindFacet( shaderClass->GetName() );
        }

        template<typename T>
        inline T* FindFacet()
        {
            return reinterpret_cast<T*>(FindFacet( T::StaticGetShaderClassName() ));
        }



    protected:
        void* _shaderClassInfo;
        unsigned __int32 _referenceCount;
    };

    namespace d3d11
    {
        class D3D11DrawPass :
            public ShaderInstance
        {
        public:
            static inline const char* StaticGetShaderClassName() { return "D3D11DrawPass"; }

            void Submit(
                ID3D11Device* device,
                ID3D11DeviceContext* context )
            {
                // \todo: This really belongs in the
                // SparkCPP.lib, but getting it in
                // there involves linker trickery
                // I don't feel like doing right now... :(
                struct ClassInfo
                {
                    int a;
                    int b;
                    void* c;
                    void* d;
                    void* e;
                    void (__stdcall * Submit)(void* obj, ID3D11Device*, ID3D11DeviceContext*);
                };

                ClassInfo* ci = (ClassInfo*) _shaderClassInfo;
                ci->Submit( this, device, context );
            }

            ID3D11DepthStencilView*  GetDepthStencilView() const { return m_depthStencilView; }
            void SetDepthStencilView( ID3D11DepthStencilView*  value ) { m_depthStencilView = value; }

            template<typename TFacet>
            TFacet* GetFacet() { return _GetFacetImpl(static_cast<TFacet*>(nullptr)); }

        public:
            ID3D11DepthStencilView* m_depthStencilView;
        };

        class D3D11GeometryShader
        {
        public:
            static inline const char* StaticGetShaderClassName() { return "D3D11GeometryShader"; }

            D3D11DrawPass * _GetFacetImpl( D3D11DrawPass * ) { return _Base_D3D11DrawPass; }

            template<typename TFacet>
            TFacet* GetFacet() { return _GetFacetImpl(static_cast<TFacet*>(nullptr)); }

        public:
            D3D11DrawPass *_Base_D3D11DrawPass;
        };

        class D3D11NullTessellation
        {
        public:
            static inline const char* StaticGetShaderClassName() { return "D3D11NullTessellation"; }

            D3D11DrawPass * _GetFacetImpl( D3D11DrawPass * ) { return _Base_D3D11DrawPass; }

            template<typename TFacet>
            TFacet* GetFacet() { return _GetFacetImpl(static_cast<TFacet*>(nullptr)); }

        public:
            D3D11DrawPass *_Base_D3D11DrawPass;
        };

        class D3D11Tessellation
        {
        public:
            static inline const char* StaticGetShaderClassName() { return "D3D11Tessellation"; }

            D3D11DrawPass * _GetFacetImpl( D3D11DrawPass * ) { return _Base_D3D11DrawPass; }

            template<typename TFacet>
            TFacet* GetFacet() { return _GetFacetImpl(static_cast<TFacet*>(nullptr)); }

        public:
            D3D11DrawPass *_Base_D3D11DrawPass;
        };

        class D3D11QuadTessellation
        {
        public:
            static inline const char* StaticGetShaderClassName() { return "D3D11QuadTessellation"; }

            D3D11DrawPass * _GetFacetImpl( D3D11DrawPass * ) { return _Base_D3D11DrawPass; }
            D3D11Tessellation * _GetFacetImpl( D3D11Tessellation * ) { return _Mixin_D3D11Tessellation; }

            template<typename TFacet>
            TFacet* GetFacet() { return _GetFacetImpl(static_cast<TFacet*>(nullptr)); }

        public:
            D3D11DrawPass *_Base_D3D11DrawPass;
            D3D11Tessellation *_Mixin_D3D11Tessellation;
        };

        class D3D11TriTessellation
        {
        public:
            static inline const char* StaticGetShaderClassName() { return "D3D11TriTessellation"; }

            D3D11DrawPass * _GetFacetImpl( D3D11DrawPass * ) { return _Base_D3D11DrawPass; }
            D3D11Tessellation * _GetFacetImpl( D3D11Tessellation * ) { return _Mixin_D3D11Tessellation; }

            template<typename TFacet>
            TFacet* GetFacet() { return _GetFacetImpl(static_cast<TFacet*>(nullptr)); }

        public:
            D3D11DrawPass *_Base_D3D11DrawPass;
            D3D11Tessellation *_Mixin_D3D11Tessellation;
        };

        struct PrimitiveSpan
        {
        public:
            enum Flavor
            {
                kDirect = 0x0,
                kIndexed = 0x1,
                kAuto = 0x2,
                kInstanced = 0x4,
                kIndirect = 0x8,

                kDraw = kDirect,
                kDrawAuto = kAuto,
                kDrawIndexed = kIndexed,
                kDrawIndexedInstanced = kIndexed | kInstanced,
                kDrawIndexedInstancedIndirect = kIndexed | kInstanced | kIndirect,
                kDrawInstanced= kInstanced,
                kDrawInstancedIndirect = kInstanced | kIndirect,
            };

            Flavor flavor;
            D3D11_PRIMITIVE_TOPOLOGY primitiveTopology;
            ID3D11Buffer* indexBuffer;
            DXGI_FORMAT indexFormat;
            UINT indexOffset;
            union {
                struct {
                    UINT indexCount;
                    UINT instanceCount;
                    UINT baseIndexIndex;
                    INT baseVertexIndex;
                    UINT baseInstanceIndex;
                } direct;
                struct {
                    ID3D11Buffer* argumentBuffer;
                    UINT argumentOffset;
                } indirect;
            };

            PrimitiveSpan()
            {
                memset(this, 0, sizeof(*this));
            }

            __forceinline void Bind(
                ID3D11DeviceContext* context )
            {
                context->IASetPrimitiveTopology( primitiveTopology );
            }

            __forceinline void Submit(
                ID3D11DeviceContext* context )
            {
                switch( flavor )
                {
                case kDraw:
                    context->Draw(
                        direct.indexCount,
                        direct.baseIndexIndex);
                    break;
                case kDrawAuto:
                    context->DrawAuto();
                    break;
                case kDrawIndexed:
                    context->DrawIndexed(
                        direct.indexCount,
                        direct.baseIndexIndex,
                        direct.baseVertexIndex);
                    break;
                case kDrawIndexedInstanced:
                    context->DrawIndexedInstanced(
                        direct.indexCount,
                        direct.instanceCount,
                        direct.baseIndexIndex,
                        direct.baseVertexIndex,
                        direct.baseInstanceIndex);
                    break;
                case kDrawIndexedInstancedIndirect:
                    context->DrawIndexedInstancedIndirect(
                        indirect.argumentBuffer,
                        indirect.argumentOffset );
                    break;
                case kDrawInstanced:
                    context->DrawInstanced(
                        direct.indexCount,
                        direct.instanceCount,
                        direct.baseIndexIndex,
                        direct.baseInstanceIndex);
                    break;
                case kDrawInstancedIndirect:
                    context->DrawInstancedIndirect(
                        indirect.argumentBuffer,
                        indirect.argumentOffset );
                    break;
                default:
                    break;
                }
            }
        };

        struct IndexStream
        {
        public:
            ID3D11Buffer* buffer;
            DXGI_FORMAT format;
            UINT offset;
            
            IndexStream()
            {
                memset(this, 0, sizeof(*this));
            }

            IndexStream(
                ID3D11Buffer* buffer,
                DXGI_FORMAT format,
                UINT offset )
                : buffer(buffer)
                , format(format)
                , offset(offset)
            {
            }

            __forceinline void Bind(
                ID3D11DeviceContext* context )
            {
                context->IASetIndexBuffer(
                    buffer,
                    format,
                    offset );
            }
        };

        struct DrawSpan
        {
        public:
            IndexStream indexStream;
            PrimitiveSpan primitiveSpan;

            DrawSpan()
            {}

            DrawSpan(
                const IndexStream& indexStream,
                const PrimitiveSpan& primitiveSpan )
                : indexStream(indexStream)
                , primitiveSpan(primitiveSpan)
            {
            }

            __forceinline void Bind(
                ID3D11DeviceContext* context )
            {
                primitiveSpan.Bind(context);
                indexStream.Bind(context);
            }

            __forceinline void Submit(
                ID3D11DeviceContext* context )
            {
                primitiveSpan.Submit(context);
            }
        };

        static inline DrawSpan DrawIndexed(
            ID3D11Buffer* buffer,
            DXGI_FORMAT indexFormat,
            UINT indexCount,
            UINT baseIndexIndex,
            INT baseVertexIndex )
        {
            IndexStream indexStream(
                buffer,
                indexFormat,
                0);
            PrimitiveSpan primitiveSpan;
            primitiveSpan.flavor = PrimitiveSpan::kDrawIndexed;
            primitiveSpan.direct.indexCount = indexCount;
            primitiveSpan.direct.baseIndexIndex = baseIndexIndex;
            primitiveSpan.direct.baseVertexIndex = baseVertexIndex;

            return DrawSpan(indexStream, primitiveSpan);
        }

        static inline DrawSpan IndexedDrawSpan(
            D3D11_PRIMITIVE_TOPOLOGY primitiveTopology,
            const IndexStream& indexStream,
            UINT indexCount,
            UINT baseIndexIndex,
            INT baseVertexIndex )
        {
            PrimitiveSpan primitiveSpan;
            primitiveSpan.primitiveTopology = primitiveTopology;
            primitiveSpan.flavor = PrimitiveSpan::kDrawIndexed;
            primitiveSpan.direct.indexCount = indexCount;
            primitiveSpan.direct.baseIndexIndex = baseIndexIndex;
            primitiveSpan.direct.baseVertexIndex = baseVertexIndex;

            return DrawSpan(indexStream, primitiveSpan);
        }

        static inline DrawSpan InstancedDrawSpan(
            const DrawSpan& drawSpan,
            UINT instanceCount )
        {
            DrawSpan result = drawSpan;
            result.primitiveSpan.flavor = (PrimitiveSpan::Flavor)(result.primitiveSpan.flavor | PrimitiveSpan::kInstanced);
            result.primitiveSpan.direct.instanceCount = instanceCount;
            result.primitiveSpan.direct.baseInstanceIndex = 0;
            return result;
        }

        struct VertexStream
        {
        public:
            ID3D11Buffer* buffer;
            UINT offset;
            UINT stride;

            VertexStream()
            {
                memset(this, 0, sizeof(*this));
            }

            VertexStream(
                ID3D11Buffer* buffer,
                UINT offset,
                UINT stride)
                : buffer(buffer)
                , offset(offset)
                , stride(stride)
            {
            }
        };

        // The following routines are used to create various state blocks.
        // Due to current limitations in the compiler, these get called
        // in the per-frame rendering logic (rather than run ahead-of-time).
        // This will, of course, need to be rectified eventually, but for
        // now it means that they need to cache/memoize their results
        // internally, so that we don't allocate new states per-frame. :(

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
            float                         maxLOD );

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
            bool              antialiasedLineEnable );

    }
}

#endif
