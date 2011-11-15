// Automatically generated code. Do not edit.
#pragma once
#include <d3d11.h>
#include <spark/spark.h>
#include <spark/context.h>

class BasicSpark11 : public spark::d3d11::D3D11DrawPass
{
public:
	static const char* StaticGetShaderClassName() { return "BasicSpark11"; }
	spark::float4x4  GetWorld() const { return m_world; }
	void SetWorld( spark::float4x4  value ) { m_world = value; }
	spark::float4x4  GetView() const { return m_view; }
	void SetView( spark::float4x4  value ) { m_view = value; }
	spark::float4x4  GetProj() const { return m_proj; }
	void SetProj( spark::float4x4  value ) { m_proj = value; }
	spark::float4  GetObjectColor() const { return m_objectColor; }
	void SetObjectColor( spark::float4  value ) { m_objectColor = value; }
	spark::float3  GetLightDir() const { return m_lightDir; }
	void SetLightDir( spark::float3  value ) { m_lightDir = value; }
	float  GetAmbient() const { return m_ambient; }
	void SetAmbient( float  value ) { m_ambient = value; }
	ID3D11ShaderResourceView*  GetDiffuseTexture() const { return m_diffuseTexture; }
	void SetDiffuseTexture( ID3D11ShaderResourceView*  value ) { m_diffuseTexture = value; }
	ID3D11SamplerState*  GetLinearSampler() const { return m_linearSampler; }
	void SetLinearSampler( ID3D11SamplerState*  value ) { m_linearSampler = value; }
	spark::float3  GetCameraPos() const { return m_cameraPos; }
	void SetCameraPos( spark::float3  value ) { m_cameraPos = value; }
	spark::d3d11::VertexStream  GetMyVertexStream() const { return m_myVertexStream; }
	void SetMyVertexStream( spark::d3d11::VertexStream  value ) { m_myVertexStream = value; }
	spark::d3d11::DrawSpan  GetMyDrawSpan() const { return m_myDrawSpan; }
	void SetMyDrawSpan( spark::d3d11::DrawSpan  value ) { m_myDrawSpan = value; }
	template<typename TFacet>
	TFacet* GetFacet() { return _GetFacetImpl(static_cast<TFacet*>(nullptr)); }
	ID3D11RenderTargetView*  GetMyTarget() const { return m_myTarget; }
	void SetMyTarget( ID3D11RenderTargetView*  value ) { m_myTarget = value; }
	static const spark::ShaderClassDesc* GetShaderClassDesc();
public:
	spark::float4x4 m_world;
	spark::float4x4 m_view;
	spark::float4x4 m_proj;
	spark::float4 m_objectColor;
	spark::float3 m_lightDir;
	float m_ambient;
	ID3D11ShaderResourceView* m_diffuseTexture;
	ID3D11SamplerState* m_linearSampler;
	spark::float3 m_cameraPos;
	spark::d3d11::VertexStream m_myVertexStream;
	spark::d3d11::DrawSpan m_myDrawSpan;
	ID3D11RenderTargetView* m_myTarget;
};
