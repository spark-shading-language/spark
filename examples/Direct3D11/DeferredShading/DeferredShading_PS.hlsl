//--------------------------------------------------------------------------------------
// File: BasicHLSL11_PS.hlsl
//
// The pixel shader file for the BasicHLSL11 sample.  
// 
// Copyright (c) Microsoft Corporation. All rights reserved.
//--------------------------------------------------------------------------------------

//--------------------------------------------------------------------------------------
// Globals
//--------------------------------------------------------------------------------------
cbuffer cbPerObject : register( b0 )
{
	float4		g_vObjectColor			: packoffset( c0 );
};

cbuffer cbPerFrame : register( b1 )
{
	float3		g_vLightDir				: packoffset( c0 );
	float		g_fAmbient				: packoffset( c0.w );
};

//--------------------------------------------------------------------------------------
// Textures and Samplers
//--------------------------------------------------------------------------------------
Texture2D	g_txDiffuse : register( t0 );
SamplerState g_samLinear : register( s0 );

//--------------------------------------------------------------------------------------
// Input / Output structures
//--------------------------------------------------------------------------------------
struct PS_INPUT
{
	float3 vViewVector  : VIEWVECTOR;
	float3 vNormal		: NORMAL;
	float2 vTexcoord	: TEXCOORD0;
};

//--------------------------------------------------------------------------------------
// Pixel Shader
//--------------------------------------------------------------------------------------
float4 PSMain( PS_INPUT Input ) : SV_TARGET
{
	float4 vDiffuse = g_txDiffuse.Sample( g_samLinear, Input.vTexcoord );
	
	float fLighting = saturate( dot( g_vLightDir, Input.vNormal ) );
	fLighting = max( fLighting, g_fAmbient );
	return vDiffuse * fLighting;

    float3 vView = normalize(Input.vViewVector);
    float3 vNormal = normalize(Input.vNormal);
    float3 vLightDir = normalize(g_vLightDir);

    //  Specular lighting.
    float fNormalDotLight = saturate ( dot(vNormal, vLightDir) );  
    float3 vLightReflect = 2.0 * fNormalDotLight * vNormal - vLightDir;
    float fViewDotReflect = saturate( dot(vView, vLightReflect) );
    float fSpecIntensity1 = pow(fViewDotReflect, 2.0f);
    float4 fSpecIntensity = (float4)fSpecIntensity1;

    float4 colSpecular = float4(1.0f, 1.0f, 1.0f, 1.0f);
    float4 lightSpecular = 0.1 * float4(1.0f, 1.0f, 1.0f, 1.0f);
    float4 specular = fSpecIntensity * colSpecular * lightSpecular;
	return specular;
}

