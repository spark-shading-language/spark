//--------------------------------------------------------------------------------------
// File: BasicHLSL11_VS.hlsl
//
// The vertex shader file for the BasicHLSL11 sample.  
// 
// Copyright (c) Microsoft Corporation. All rights reserved.
//--------------------------------------------------------------------------------------

//--------------------------------------------------------------------------------------
// Globals
//--------------------------------------------------------------------------------------
cbuffer cbPerObject : register( b0 )
{
	matrix		g_mWorldViewProjection	: packoffset( c0 );
	matrix		g_mWorld				: packoffset( c4 );
	matrix		g_mWorldView            : packoffset( c8 );
    float4      g_vCameraPos            : packoffset( c12) ;
};

//--------------------------------------------------------------------------------------
// Input / Output structures
//--------------------------------------------------------------------------------------
struct VS_INPUT
{
	float4 vPosition	: POSITION;
	float3 vNormal		: NORMAL;
	float2 vTexcoord	: TEXCOORD0;
};

struct VS_OUTPUT
{
	float3 vViewVector  : VIEWVECTOR;
    float3 vPositionView : POSITION_VIEW;
	float3 vNormal		: NORMAL;
	float2 vTexcoord	: TEXCOORD0;
	float4 vPosition	: SV_POSITION;
};

//--------------------------------------------------------------------------------------
// Vertex Shader
//--------------------------------------------------------------------------------------
VS_OUTPUT VSMain( VS_INPUT Input )
{
	VS_OUTPUT Output;
	
	Output.vViewVector = normalize((g_vCameraPos.xyz - mul(Input.vPosition, g_mWorld).xyz));
	Output.vPositionView = mul( Input.vPosition, g_mWorldView).xyz;
	Output.vPosition = mul( Input.vPosition, g_mWorldViewProjection );
	Output.vNormal = mul( Input.vNormal, (float3x3)g_mWorld );
	Output.vTexcoord = Input.vTexcoord;
	
	return Output;
}

