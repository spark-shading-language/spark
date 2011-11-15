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
    float3 vPositionView : POSITION_VIEW;
	float3 vNormal		: NORMAL;
	float2 vTexcoord	: TEXCOORD0;
};

// Data that we can read or derive from the surface shader outputs
struct SurfaceData
{
    float3 positionView;         // View space position
//    float3 positionViewDX;       // Screen space derivatives
//    float3 positionViewDY;       // of view space position
    float3 normal;               // View space normal
    float4 albedo;
    float specularAmount;        // Treated as a multiplier on albedo
    float specularPower;
};

SurfaceData ComputeSurfaceDataFromGeometry(PS_INPUT input)
{
    SurfaceData surface;
    surface.positionView = input.vPositionView;

    surface.normal = normalize(input.vNormal);
    
    surface.albedo = g_txDiffuse.Sample( g_samLinear, input.vTexcoord );
    //surface.albedo.rgb = mUI.lightingOnly ? float3(1.0f, 1.0f, 1.0f) : surface.albedo.rgb;

    // Map NULL diffuse textures to white
//    uint2 textureDim;
//    gDiffuseTexture.GetDimensions(textureDim.x, textureDim.y);
//    surface.albedo = (textureDim.x == 0U ? float4(1.0f, 1.0f, 1.0f, 1.0f) : surface.albedo);

    // We don't really have art asset-related values for these, so set them to something
    // reasonable for now... the important thing is that they are stored in the G-buffer for
    // representative performance measurement.
    surface.specularAmount = 0.9f;
    surface.specularPower = 25.0f;

    return surface;
}

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

//--------------------------------------------------------------------------------------
// GBuffer and related common utilities and structures
struct GBuffer
{
    float4 normal_specular : SV_Target0;
    float4 albedo : SV_Target1;
    float2 positionZGrad : SV_Target2;
};

float2 EncodeSphereMap(float3 n)
{
    return n.xy * rsqrt(8.0f - 8.0f * n.z) + 0.5f;
}

float3 DecodeSphereMap(float2 e)
{
    float3 n;
    float2 tmp = e - e * e;
    float f = tmp.x + tmp.y;
    float m = sqrt(4.0f * f - 1.0f);
    n.xy = m * (e * 4.0f - 2.0f);
    n.z  = 3.0f - 8.0f * f;
    return n;
}


//--------------------------------------------------------------------------------------
// G-buffer rendering
//--------------------------------------------------------------------------------------
void GBufferPS(PS_INPUT input, out GBuffer outputGBuffer)
{
    SurfaceData surface = ComputeSurfaceDataFromGeometry(input);
    outputGBuffer.normal_specular = float4(EncodeSphereMap(surface.normal),
                                           surface.specularAmount,
                                           surface.specularPower);
    outputGBuffer.albedo = surface.albedo;
    outputGBuffer.positionZGrad = float2(ddx_coarse(surface.positionView.z),
                                         ddy_coarse(surface.positionView.z));
}

