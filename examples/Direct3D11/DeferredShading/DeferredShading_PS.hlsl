//--------------------------------------------------------------------------------------
// File: BasicHLSL11_PS.hlsl
//
// The pixel shader file for the BasicHLSL11 sample.  
// 
// Copyright (c) Microsoft Corporation. All rights reserved.
//--------------------------------------------------------------------------------------

// Constants 

#define D3DX_PI    3.141592654f

//--------------------------------------------------------------------------------------
// Globals
//--------------------------------------------------------------------------------------
cbuffer cbPerObject : register( b0 )
{
	float4		g_vObjectColor			: packoffset( c0 );
};

cbuffer cbPerFrame : register( b1 )
{
    matrix      mCameraProj             : packoffset( c0);
	float3		g_vLightDir				: packoffset( c4 );
	float		g_fAmbient				: packoffset( c4.w );

    float4      g_SpotLightPosView      : packoffset( c5 );
    float4      g_SpotLightDir          : packoffset( c6 );
    float4      g_SpotLightParams       : packoffset( c7 );

    uint        gUseSpotLight           : packoffset( c8 );
//    uint        padding1;
//    uint        padding2;
//    uint        padding3;
};

//--------------------------------------------------------------------------------------
// Textures and Samplers
//--------------------------------------------------------------------------------------
Texture2D	g_txDiffuse : register( t0 );
Texture2D   g_ShadowMap : register (t1 ) ;
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

float4 ComputeSpotLighting(float4 vDiffuse, float3 positionView, float3 normalView)
{
    float d = distance(positionView, g_SpotLightPosView.xyz);
    float3 lightDirView = normalize(g_SpotLightPosView.xyz - positionView);
    float fLighting = saturate(dot(lightDirView, normalView));

    float cosoutside = cos (g_SpotLightParams.x);
    float cosinside = cos (g_SpotLightParams.x - D3DX_PI/36.0f);
    float cosangle = saturate(dot(normalize(-g_SpotLightDir.xyz), lightDirView)) ;
    float atten = pow(cosangle, 2.0f) / (d * d);
    atten *= smoothstep(cosoutside, cosinside, cosangle);
    const float intensity = 20000.0f;
    return atten * intensity *  vDiffuse * fLighting;
}

//--------------------------------------------------------------------------------------
// Pixel Shader
//--------------------------------------------------------------------------------------
float4 PSMain( PS_INPUT Input ) : SV_TARGET
{
    float4 vDiffuse = g_txDiffuse.Sample( g_samLinear, Input.vTexcoord );
    
    float fLighting = saturate( dot( g_vLightDir, normalize(Input.vNormal) ) );
    fLighting = max( fLighting, g_fAmbient );
    return vDiffuse * fLighting;
}

float4 PSMainSpotLight( PS_INPUT Input ) : SV_TARGET
{
    float4 vDiffuse = g_txDiffuse.Sample( g_samLinear, Input.vTexcoord );
//    float fLighting = saturate( dot( g_vLightDir, normalize(Input.vNormal) ) );
//    fLighting = max( fLighting, g_fAmbient );
    return ComputeSpotLighting(float4(1.0f, 0.0f, 1.0f, 0.0f), Input.vPositionView, Input.vNormal);
}

//--------------------------------------------------------------------------------------
// GBuffer and related common utilities and structures
struct GBuffer
{
    float4 normal_specular : SV_Target0;
    float4 albedo : SV_Target1;
    float2 positionZGrad : SV_Target2;
};

// Above values PLUS depth buffer (last element)
Texture2DMS<float4, 1> gGBufferTextures[4] : register(t0);


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


// Full screen pass

struct FullScreenTriangleVSOut
{
    float4 positionViewport : SV_Position;
};

FullScreenTriangleVSOut FullScreenTriangleVS(uint vertexID : SV_VertexID)
{
    FullScreenTriangleVSOut output;

    // Parametrically work out vertex location for full screen triangle
    float2 grid = float2((vertexID << 1) & 2, vertexID & 2);
    output.positionViewport = float4(grid * float2(2.0f, -2.0f) + float2(-1.0f, 1.0f), 0.0f, 1.0f);
    
    return output;
}


//--------------------------------------------------------------------------------------
float3 ComputePositionViewFromZ(float2 positionScreen,
                                float viewSpaceZ)
{
    float2 screenSpaceRay = float2(positionScreen.x / mCameraProj._11,
                                   positionScreen.y / mCameraProj._22);
    
    float3 positionView;
    positionView.z = viewSpaceZ;
    // Solve the two projection equations
    positionView.xy = screenSpaceRay.xy * positionView.z;
    
    return positionView;
}
SurfaceData ComputeSurfaceDataFromGBufferSample(uint2 positionViewport, uint sampleIndex)
{
    // Load the raw data from the GBuffer
    GBuffer rawData;
    rawData.normal_specular = gGBufferTextures[0].Load(positionViewport.xy, sampleIndex).xyzw;
    rawData.albedo = gGBufferTextures[1].Load(positionViewport.xy, sampleIndex).xyzw;
    rawData.positionZGrad = gGBufferTextures[2].Load(positionViewport.xy, sampleIndex).xy;
    float zBuffer = gGBufferTextures[3].Load(positionViewport.xy, sampleIndex).x;


    float2 gbufferDim;
    uint dummy;
    gGBufferTextures[0].GetDimensions(gbufferDim.x, gbufferDim.y, dummy);
    
    // Compute screen/clip-space position and neighbour positions
    // NOTE: Mind DX11 viewport transform and pixel center!
    // NOTE: This offset can actually be precomputed on the CPU but it's actually slower to read it from
    // a constant buffer than to just recompute it.
    float2 screenPixelOffset = float2(2.0f, -2.0f) / gbufferDim;
    float2 positionScreen = (float2(positionViewport.xy) + 0.5f) * screenPixelOffset.xy + float2(-1.0f, 1.0f);
    float2 positionScreenX = positionScreen + float2(screenPixelOffset.x, 0.0f);
    float2 positionScreenY = positionScreen + float2(0.0f, screenPixelOffset.y);
        
    // Decode into reasonable outputs
    SurfaceData data;
        
    // Unproject depth buffer Z value into view space
    float viewSpaceZ = mCameraProj._43 / (zBuffer - mCameraProj._33);

    data.positionView = ComputePositionViewFromZ(positionScreen, viewSpaceZ);
//    data.positionViewDX = ComputePositionViewFromZ(positionScreenX, viewSpaceZ + rawData.positionZGrad.x) - data.positionView;
//    data.positionViewDY = ComputePositionViewFromZ(positionScreenY, viewSpaceZ + rawData.positionZGrad.y) - data.positionView;

    data.normal = DecodeSphereMap(rawData.normal_specular.xy);
    data.albedo = rawData.albedo;

    data.specularAmount = rawData.normal_specular.z;
    data.specularPower = rawData.normal_specular.w;
    
    return data;
}



float4 DirectionalLightPS(FullScreenTriangleVSOut input) : SV_Target
{
    SurfaceData surface = ComputeSurfaceDataFromGBufferSample(uint2(input.positionViewport.xy), 0);
    float4 vDiffuse = surface.albedo;
        
    float fLighting = saturate( dot( g_vLightDir, surface.normal) );
    fLighting = max( fLighting, g_fAmbient );
    return vDiffuse * fLighting;
}




float4 SpotLightPS(FullScreenTriangleVSOut input) : SV_Target
{
    SurfaceData surface = ComputeSurfaceDataFromGBufferSample(uint2(input.positionViewport.xy), 0);
    return ComputeSpotLighting(/*surface.albedo*/float4(1.0f, 0.0f, 1.0f, 0.0f), surface.positionView, surface.normal);
}
