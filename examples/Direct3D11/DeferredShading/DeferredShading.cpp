//--------------------------------------------------------------------------------------
// File: BasicHLSL11.cpp
//
// This sample shows a simple example of the Microsoft Direct3D's High-Level 
// Shader Language (HLSL) using the Effect interface. 
//
// Copyright (c) Microsoft Corporation. All rights reserved.
//--------------------------------------------------------------------------------------
#include "DXUT.h"
#include "DXUTcamera.h"
#include "DXUTgui.h"
#include "DXUTsettingsDlg.h"
#include "SDKmisc.h"
#include "SDKMesh.h"
#include "resource.h"

// SPARK:
#include "DeferredShading.spark.h"
#include "Texture2D.h"
#include <memory>
#include <limits>
#include <sstream>
#include <random>
#include <algorithm>


//--------------------------------------------------------------------------------------
// Global variables
//--------------------------------------------------------------------------------------
CDXUTDialogResourceManager  g_DialogResourceManager; // manager for shared resources of dialogs
CModelViewerCamera          g_Camera;               // A model viewing camera
CDXUTDirectionWidget        g_LightControl;
float                       g_SpotLightFOV = D3DX_PI / 16;
float                       g_SpotLightAspect = 0;
CModelViewerCamera          g_SpotLight;               // A model viewing camera

CD3DSettingsDlg             g_D3DSettingsDlg;       // Device settings dialog
CDXUTDialog                 g_HUD;                  // manages the 3D   
CDXUTDialog                 g_SampleUI;             // dialog for sample specific controls
D3DXMATRIXA16               g_mCenterMesh;
float                       g_fLightScale;
int                         g_nNumActiveLights;
int                         g_nActiveLight;
bool                        g_bShowHelp = false;    // If true, it renders the UI control text

// Direct3D9 resources
CDXUTTextHelper*            g_pTxtHelper = NULL;

CDXUTSDKMesh                g_Mesh11;

ID3D11InputLayout*          g_pVertexLayout11 = NULL;
ID3D11Buffer*               g_pVertexBuffer = NULL;
ID3D11Buffer*               g_pIndexBuffer = NULL;
ID3D11VertexShader*         g_pVertexShader = NULL;
ID3D11VertexShader*         gFullScreenTriangleVS = NULL;

ID3D11PixelShader*          gForwardPS = NULL;
ID3D11SamplerState*         g_pSamLinear = NULL;
ID3D11PixelShader*          gGBufferPS = NULL;
ID3D11PixelShader*          gDirectionalLightPS = NULL;
ID3D11PixelShader*          gSpotLightPS = NULL;

ID3D11BlendState* mLightingBlendState;


// DS
bool gUseDeferred  = false;
std::vector< std::tr1::shared_ptr<Texture2D> > mGBuffer;
// Handy cache of list of RT pointers for G-buffer
std::vector<ID3D11RenderTargetView*> mGBufferRTV;
// Handy cache of list of SRV pointers for the G-buffer
std::vector<ID3D11ShaderResourceView*> mGBufferSRV;
unsigned int mGBufferWidth;
unsigned int mGBufferHeight;
std::tr1::shared_ptr<Depth2D> mDepthBuffer;
// We also need a read-only depth stencil view for techniques that read the G-buffer while also using Z-culling
ID3D11DepthStencilView* mDepthBufferReadOnlyDSV;



// SPARK:
bool gUseSpark = false;
spark::IContext* gSparkContext = nullptr;
Forward* gForwardSpark = nullptr;
GenerateGBuffer* gGenerateGBufferSpark= nullptr;
DirectionalLightGBuffer * gDirectionalLightGBuffer = nullptr;


struct CB_VS_PER_OBJECT
{
    D3DXMATRIX m_WorldViewProj;
    D3DXMATRIX m_World;
    D3DXMATRIX m_WorldView;
    D3DXVECTOR4 m_vCameraPos;
};
UINT                        g_iCBVSPerObjectBind = 0;

struct CB_PS_PER_OBJECT
{
    D3DXVECTOR4 m_vObjectColor;
};
UINT                        g_iCBPSPerObjectBind = 0;

struct CB_PS_PER_FRAME
{
    D3DXMATRIX  m_CameraProj;
    D3DXVECTOR4 m_vLightDirAmbient;

    D3DXVECTOR4 m_SpotLightPos;
    D3DXVECTOR4 m_SpotLightDir;
    D3DXVECTOR4 m_SpotLightParameters; // fov, aspect, near, far
};
UINT                        g_iCBPSPerFrameBind = 1;

ID3D11Buffer*               g_pcbVSPerObject = NULL;
ID3D11Buffer*               g_pcbPSPerObject = NULL;
ID3D11Buffer*               g_pcbPSPerFrame = NULL;

//--------------------------------------------------------------------------------------
// UI control IDs
//--------------------------------------------------------------------------------------
#define IDC_TOGGLEFULLSCREEN    1
#define IDC_TOGGLEREF           3
#define IDC_CHANGEDEVICE        4
// SPARK:
#define IDC_CHECKBOX_USE_SPARK  5
#define IDC_CHECKBOX_DEFERRED   6

//--------------------------------------------------------------------------------------
// Forward declarations 
//--------------------------------------------------------------------------------------
bool CALLBACK ModifyDeviceSettings( DXUTDeviceSettings* pDeviceSettings, void* pUserContext );
void CALLBACK OnFrameMove( double fTime, float fElapsedTime, void* pUserContext );
LRESULT CALLBACK MsgProc( HWND hWnd, UINT uMsg, WPARAM wParam, LPARAM lParam, bool* pbNoFurtherProcessing,
                          void* pUserContext );
void CALLBACK OnKeyboard( UINT nChar, bool bKeyDown, bool bAltDown, void* pUserContext );
void CALLBACK OnGUIEvent( UINT nEvent, int nControlID, CDXUTControl* pControl, void* pUserContext );

bool CALLBACK IsD3D11DeviceAcceptable(const CD3D11EnumAdapterInfo *AdapterInfo, UINT Output, const CD3D11EnumDeviceInfo *DeviceInfo,
                                       DXGI_FORMAT BackBufferFormat, bool bWindowed, void* pUserContext );
HRESULT CALLBACK OnD3D11CreateDevice( ID3D11Device* pd3dDevice, const DXGI_SURFACE_DESC* pBackBufferSurfaceDesc,
                                      void* pUserContext );
HRESULT CALLBACK OnD3D11ResizedSwapChain( ID3D11Device* pd3dDevice, IDXGISwapChain* pSwapChain,
                                          const DXGI_SURFACE_DESC* pBackBufferSurfaceDesc, void* pUserContext );
void CALLBACK OnD3D11ReleasingSwapChain( void* pUserContext );
void CALLBACK OnD3D11DestroyDevice( void* pUserContext );
void CALLBACK OnD3D11FrameRender( ID3D11Device* pd3dDevice, ID3D11DeviceContext* pd3dImmediateContext, double fTime,
                                  float fElapsedTime, void* pUserContext );

void InitApp();
void RenderText();

// SPARK:
void FinalizeApp();


// DS
void RenderSceneHLSL( ID3D11DeviceContext* pd3dImmediateContext, D3DXVECTOR3 &vLightDir, float fAmbient, D3DXMATRIX &mWorld, D3DXMATRIX &mProj, D3DXMATRIX &mView, ID3D11VertexShader *pVS, ID3D11PixelShader *pPS );

void RenderSceneSpark( ID3D11RenderTargetView* pRTV, ID3D11DepthStencilView* pDSV, 
    D3DXMATRIX mWorld, D3DXMATRIX mView, D3DXMATRIX mProj, D3DXVECTOR3 vLightDir, 
    float fAmbient, D3DXVECTOR3 vCameraPos, ID3D11Device* pd3dDevice, 
    ID3D11DeviceContext* pd3dImmediateContext , Base * sparkShader);

void RenderScene( ID3D11DeviceContext* pd3dImmediateContext, ID3D11RenderTargetView* pRTV, 
    ID3D11DepthStencilView* pDSV, ID3D11Device* pd3dDevice, ID3D11VertexShader *pVS, 
    ID3D11PixelShader *pPS, Base * sparkShader );

void RenderGBuffer( ID3D11DeviceContext* d3dDeviceContext, ID3D11Device *pDevice )
{
    // Clear GBuffer
    // NOTE: We actually only need to clear the depth buffer here since we replace unwritten (i.e. far plane) samples
    // with the skybox. We use the depth buffer to reconstruct position and only in-frustum positions are shaded.
    // NOTE: Complementary Z buffer: clear to 0 (far)!
    FLOAT clearColor[] = {0.0f, 0.0f, 0.0f, 0.f};
    d3dDeviceContext->ClearDepthStencilView(mDepthBuffer->GetDepthStencil(), D3D11_CLEAR_DEPTH | D3D11_CLEAR_STENCIL, 1.0f, 0);
    d3dDeviceContext->ClearRenderTargetView(mGBufferRTV[0], clearColor);
    d3dDeviceContext->ClearRenderTargetView(mGBufferRTV[1], clearColor);
    d3dDeviceContext->ClearRenderTargetView(mGBufferRTV[2], clearColor);


    if (gUseSpark) {
        gGenerateGBufferSpark->GetFacet<PackGBuffer>()->SetNormalSpecularTarget( mGBufferRTV[0] );
        gGenerateGBufferSpark->GetFacet<PackGBuffer>()->SetAlbedoTarget( mGBufferRTV[1] );
        gGenerateGBufferSpark->GetFacet<PackGBuffer>()->SetPositionZGradTarget( mGBufferRTV[2] );
    }
    else {
        d3dDeviceContext->OMSetRenderTargets(static_cast<UINT>(mGBufferRTV.size()), &mGBufferRTV.front(), mDepthBuffer->GetDepthStencil());
    }

    RenderScene(d3dDeviceContext, NULL, mDepthBuffer->GetDepthStencil(), pDevice, g_pVertexShader, gGBufferPS, gGenerateGBufferSpark);

}


//--------------------------------------------------------------------------------------
// Entry point to the program. Initializes everything and goes into a message processing 
// loop. Idle time is used to render the scene.
//--------------------------------------------------------------------------------------
int WINAPI wWinMain( HINSTANCE hInstance, HINSTANCE hPrevInstance, LPWSTR lpCmdLine, int nCmdShow )
{
    // Enable run-time memory check for debug builds.
#if defined(DEBUG) | defined(_DEBUG)
    _CrtSetDbgFlag( _CRTDBG_ALLOC_MEM_DF | _CRTDBG_LEAK_CHECK_DF );
#endif

    // DXUT will create and use the best device (either D3D9 or D3D11) 
    // that is available on the system depending on which D3D callbacks are set below

    // Set DXUT callbacks
    DXUTSetCallbackDeviceChanging( ModifyDeviceSettings );
    DXUTSetCallbackMsgProc( MsgProc );
    DXUTSetCallbackKeyboard( OnKeyboard );
    DXUTSetCallbackFrameMove( OnFrameMove );

    DXUTSetCallbackD3D11DeviceAcceptable( IsD3D11DeviceAcceptable );
    DXUTSetCallbackD3D11DeviceCreated( OnD3D11CreateDevice );
    DXUTSetCallbackD3D11SwapChainResized( OnD3D11ResizedSwapChain );
    DXUTSetCallbackD3D11FrameRender( OnD3D11FrameRender );
    DXUTSetCallbackD3D11SwapChainReleasing( OnD3D11ReleasingSwapChain );
    DXUTSetCallbackD3D11DeviceDestroyed( OnD3D11DestroyDevice );

    InitApp();
    DXUTInit( true, true, NULL ); // Parse the command line, show msgboxes on error, no extra command line params
    DXUTSetCursorSettings( true, true ); // Show the cursor and clip it when in full screen
    DXUTCreateWindow( L"DeferredShading" );
    DXUTCreateDevice (D3D_FEATURE_LEVEL_9_2, true, 800, 600 );
    //DXUTCreateDevice(true, 640, 480);
    DXUTMainLoop(); // Enter into the DXUT render loop

    // SPARK:
    FinalizeApp();

    return DXUTGetExitCode();
}


//--------------------------------------------------------------------------------------
// Initialize the app 
//--------------------------------------------------------------------------------------
void InitApp()
{
    D3DXVECTOR3 vLightDir( -1, 1, -1 );
    D3DXVec3Normalize( &vLightDir, &vLightDir );
    g_LightControl.SetLightDirection( vLightDir );

    // Initialize dialogs
    g_D3DSettingsDlg.Init( &g_DialogResourceManager );
    g_HUD.Init( &g_DialogResourceManager );
    g_SampleUI.Init( &g_DialogResourceManager );

    g_HUD.SetCallback( OnGUIEvent ); int iY = 10;
    g_HUD.AddButton( IDC_TOGGLEFULLSCREEN, L"Toggle full screen", 0, iY, 170, 23 );
    g_HUD.AddButton( IDC_TOGGLEREF, L"Toggle REF (F3)", 0, iY += 26, 170, 23, VK_F3 );
    g_HUD.AddButton( IDC_CHANGEDEVICE, L"Change device (F2)", 0, iY += 26, 170, 23, VK_F2 );

    // SPARK:
    g_HUD.AddCheckBox( IDC_CHECKBOX_USE_SPARK, L"Use Spar(K)", 0, iY += 26, 140, 24, gUseSpark, 'K' );
    g_HUD.AddCheckBox( IDC_CHECKBOX_DEFERRED, L"Use Deferred (D)", 0, iY += 26, 140, 24, gUseDeferred, 'D' );

    g_SampleUI.SetCallback( OnGUIEvent ); iY = 10;

    // SPARK:
    gSparkContext = SparkCreateContext();
}


// SPARK:
//--------------------------------------------------------------------------------------
// Finalize the app 
//--------------------------------------------------------------------------------------
void FinalizeApp()
{
    SAFE_RELEASE(gSparkContext);
}


//--------------------------------------------------------------------------------------
// Called right before creating a D3D9 or D3D11 device, allowing the app to modify the device settings as needed
//--------------------------------------------------------------------------------------
bool CALLBACK ModifyDeviceSettings( DXUTDeviceSettings* pDeviceSettings, void* pUserContext )
{
    // Uncomment this to get debug information from D3D11
    //pDeviceSettings->d3d11.CreateFlags |= D3D11_CREATE_DEVICE_DEBUG;

    // For the first device created if its a REF device, optionally display a warning dialog box
    static bool s_bFirstTime = true;
    if( s_bFirstTime )
    {
        s_bFirstTime = false;
        if( ( DXUT_D3D11_DEVICE == pDeviceSettings->ver &&
              pDeviceSettings->d3d11.DriverType == D3D_DRIVER_TYPE_REFERENCE ) )
        {
            DXUTDisplaySwitchingToREFWarning( pDeviceSettings->ver );
        }
    }

    return true;
}


//--------------------------------------------------------------------------------------
// Handle updates to the scene.  This is called regardless of which D3D API is used
//--------------------------------------------------------------------------------------
void CALLBACK OnFrameMove( double fTime, float fElapsedTime, void* pUserContext )
{
    // Update the camera's position based on user input 
    g_Camera.FrameMove( fElapsedTime );
}


//--------------------------------------------------------------------------------------
// Render the help and statistics text
//--------------------------------------------------------------------------------------
void RenderText()
{
    UINT nBackBufferHeight = ( DXUTIsAppRenderingWithD3D9() ) ? DXUTGetD3D9BackBufferSurfaceDesc()->Height :
            DXUTGetDXGIBackBufferSurfaceDesc()->Height;

    g_pTxtHelper->Begin();
    g_pTxtHelper->SetInsertionPos( 2, 0 );
    g_pTxtHelper->SetForegroundColor( D3DXCOLOR( 1.0f, 1.0f, 0.0f, 1.0f ) );
    g_pTxtHelper->DrawTextLine( DXUTGetFrameStats( DXUTIsVsyncEnabled() ) );
    g_pTxtHelper->DrawTextLine( DXUTGetDeviceStats() );

    // Draw help
    if( g_bShowHelp )
    {
        g_pTxtHelper->SetInsertionPos( 2, nBackBufferHeight - 20 * 6 );
        g_pTxtHelper->SetForegroundColor( D3DXCOLOR( 1.0f, 0.75f, 0.0f, 1.0f ) );
        g_pTxtHelper->DrawTextLine( L"Controls:" );

        g_pTxtHelper->SetInsertionPos( 20, nBackBufferHeight - 20 * 5 );
        g_pTxtHelper->DrawTextLine( L"Rotate model: Left mouse button\n"
                                    L"Rotate light: Right mouse button\n"
                                    L"Rotate camera: Middle mouse button\n"
                                    L"Zoom camera: Mouse wheel scroll\n" );

        g_pTxtHelper->SetInsertionPos( 550, nBackBufferHeight - 20 * 5 );
        g_pTxtHelper->DrawTextLine( L"Hide help: F1\n"
                                    L"Quit: ESC\n" );
    }
    else
    {
        g_pTxtHelper->SetForegroundColor( D3DXCOLOR( 1.0f, 1.0f, 1.0f, 1.0f ) );
        g_pTxtHelper->DrawTextLine( L"Press F1 for help" );
    }

    g_pTxtHelper->End();
}


//--------------------------------------------------------------------------------------
// Handle messages to the application
//--------------------------------------------------------------------------------------
LRESULT CALLBACK MsgProc( HWND hWnd, UINT uMsg, WPARAM wParam, LPARAM lParam, bool* pbNoFurtherProcessing,
                          void* pUserContext )
{
    // Pass messages to dialog resource manager calls so GUI state is updated correctly
    *pbNoFurtherProcessing = g_DialogResourceManager.MsgProc( hWnd, uMsg, wParam, lParam );
    if( *pbNoFurtherProcessing )
        return 0;

    // Pass messages to settings dialog if its active
    if( g_D3DSettingsDlg.IsActive() )
    {
        g_D3DSettingsDlg.MsgProc( hWnd, uMsg, wParam, lParam );
        return 0;
    }

    // Give the dialogs a chance to handle the message first
    *pbNoFurtherProcessing = g_HUD.MsgProc( hWnd, uMsg, wParam, lParam );
    if( *pbNoFurtherProcessing )
        return 0;
    *pbNoFurtherProcessing = g_SampleUI.MsgProc( hWnd, uMsg, wParam, lParam );
    if( *pbNoFurtherProcessing )
        return 0;

    g_LightControl.HandleMessages( hWnd, uMsg, wParam, lParam );

    // Pass all remaining windows messages to camera so it can respond to user input
    g_Camera.HandleMessages( hWnd, uMsg, wParam, lParam );

    return 0;
}


//--------------------------------------------------------------------------------------
// Handle key presses
//--------------------------------------------------------------------------------------
void CALLBACK OnKeyboard( UINT nChar, bool bKeyDown, bool bAltDown, void* pUserContext )
{
    if( bKeyDown )
    {
        switch( nChar )
        {
            case VK_F1:
                g_bShowHelp = !g_bShowHelp; break;
        }
    }
}


//--------------------------------------------------------------------------------------
// Handles the GUI events
//--------------------------------------------------------------------------------------
void CALLBACK OnGUIEvent( UINT nEvent, int nControlID, CDXUTControl* pControl, void* pUserContext )
{
    switch( nControlID )
    {
        case IDC_TOGGLEFULLSCREEN:
            DXUTToggleFullScreen(); break;
        case IDC_TOGGLEREF:
            DXUTToggleREF(); break;
        case IDC_CHANGEDEVICE:
            g_D3DSettingsDlg.SetActive( !g_D3DSettingsDlg.IsActive() ); break;

        // SPARK:
        case IDC_CHECKBOX_USE_SPARK:
            gUseSpark = ((CDXUTCheckBox*)pControl)->GetChecked();
            break;

        case IDC_CHECKBOX_DEFERRED:
            gUseDeferred = ((CDXUTCheckBox*)pControl)->GetChecked();
            break;
    }

}


//--------------------------------------------------------------------------------------
// Reject any D3D11 devices that aren't acceptable by returning false
//--------------------------------------------------------------------------------------
bool CALLBACK IsD3D11DeviceAcceptable( const CD3D11EnumAdapterInfo *AdapterInfo, UINT Output, const CD3D11EnumDeviceInfo *DeviceInfo,
                                       DXGI_FORMAT BackBufferFormat, bool bWindowed, void* pUserContext )
{
    return true;
}

//--------------------------------------------------------------------------------------
// Find and compile the specified shader
//--------------------------------------------------------------------------------------
HRESULT CompileShaderFromFile( WCHAR* szFileName, LPCSTR szEntryPoint, LPCSTR szShaderModel, ID3DBlob** ppBlobOut )
{
    HRESULT hr = S_OK;

    // find the file
    WCHAR str[MAX_PATH];
    V_RETURN( DXUTFindDXSDKMediaFileCch( str, MAX_PATH, szFileName ) );

    DWORD dwShaderFlags = D3D10_SHADER_ENABLE_STRICTNESS;
#if defined( DEBUG ) || defined( _DEBUG )
    // Set the D3D10_SHADER_DEBUG flag to embed debug information in the shaders.
    // Setting this flag improves the shader debugging experience, but still allows 
    // the shaders to be optimized and to run exactly the way they will run in 
    // the release configuration of this program.
    dwShaderFlags |= D3D10_SHADER_DEBUG;
#endif

    ID3DBlob* pErrorBlob;
    hr = D3DX11CompileFromFile( str, NULL, NULL, szEntryPoint, szShaderModel, 
        dwShaderFlags, 0, NULL, ppBlobOut, &pErrorBlob, NULL );
    if( FAILED(hr) )
    {
        if( pErrorBlob != NULL )
            OutputDebugStringA( (char*)pErrorBlob->GetBufferPointer() );
        SAFE_RELEASE( pErrorBlob );
        return hr;
    }
    SAFE_RELEASE( pErrorBlob );

    return S_OK;
}


//--------------------------------------------------------------------------------------
// Create any D3D11 resources that aren't dependant on the back buffer
//--------------------------------------------------------------------------------------
HRESULT CALLBACK OnD3D11CreateDevice( ID3D11Device* pd3dDevice, const DXGI_SURFACE_DESC* pBackBufferSurfaceDesc,
                                      void* pUserContext )
{
    HRESULT hr;

    ID3D11DeviceContext* pd3dImmediateContext = DXUTGetD3D11DeviceContext();
    V_RETURN( g_DialogResourceManager.OnD3D11CreateDevice( pd3dDevice, pd3dImmediateContext ) );
    V_RETURN( g_D3DSettingsDlg.OnD3D11CreateDevice( pd3dDevice ) );
    g_pTxtHelper = new CDXUTTextHelper( pd3dDevice, pd3dImmediateContext, &g_DialogResourceManager, 15 );

    D3DXVECTOR3 vCenter( 0.25767413f, -28.503521f, 111.00689f );
    FLOAT fObjectRadius = 378.15607f;

    D3DXMatrixTranslation( &g_mCenterMesh, -vCenter.x, -vCenter.y, -vCenter.z );
    D3DXMATRIXA16 m;
    D3DXMatrixRotationY( &m, D3DX_PI );
    g_mCenterMesh *= m;
    D3DXMatrixRotationX( &m, D3DX_PI / 2.0f );
    g_mCenterMesh *= m;

    // Compile the shaders to a model based on the feature level we acquired
    ID3DBlob* pVertexShaderBuffer = NULL;
    ID3DBlob* pFullScreenVSBuffer = NULL;

    ID3DBlob* pForwardPSBuffer = NULL;
    ID3DBlob* pGBufferPSBuffer = NULL;
    ID3DBlob* pDirectionalLightPSBuffer = NULL;
    ID3DBlob* pSpotLightPSBuffer = NULL;
  
    switch( DXUTGetD3D11DeviceFeatureLevel() )
    {
        case D3D_FEATURE_LEVEL_11_0:
           {
            V_RETURN( CompileShaderFromFile( L"DeferredShading_VS.hlsl", "VSMain", "vs_5_0", &pVertexShaderBuffer ) );
            V_RETURN( CompileShaderFromFile( L"DeferredShading_PS.hlsl", "FullScreenTriangleVS", "vs_5_0", &pFullScreenVSBuffer ) );
            V_RETURN( CompileShaderFromFile( L"DeferredShading_PS.hlsl", "PSMain", "ps_5_0", &pForwardPSBuffer ) );
            V_RETURN( CompileShaderFromFile( L"DeferredShading_PS.hlsl", "GBufferPS", "ps_5_0", &pGBufferPSBuffer ) );
            V_RETURN( CompileShaderFromFile( L"DeferredShading_PS.hlsl", "DirectionalLightPS", "ps_5_0", &pDirectionalLightPSBuffer) );
            V_RETURN( CompileShaderFromFile( L"DeferredShading_PS.hlsl", "SpotLightPS", "ps_5_0", &pSpotLightPSBuffer) );
            break;
           }
    }

    // Create the shaders
    V_RETURN( pd3dDevice->CreateVertexShader( pVertexShaderBuffer->GetBufferPointer(),
                                              pVertexShaderBuffer->GetBufferSize(), NULL, &g_pVertexShader ) );
    V_RETURN( pd3dDevice->CreateVertexShader( pFullScreenVSBuffer->GetBufferPointer(),
                                              pFullScreenVSBuffer->GetBufferSize(), NULL, &gFullScreenTriangleVS) );
    V_RETURN( pd3dDevice->CreatePixelShader( pForwardPSBuffer->GetBufferPointer(),
                                             pForwardPSBuffer->GetBufferSize(), NULL, &gForwardPS ) );
    V_RETURN( pd3dDevice->CreatePixelShader( pGBufferPSBuffer->GetBufferPointer(),
                                             pGBufferPSBuffer->GetBufferSize(), NULL, &gGBufferPS) );
    V_RETURN( pd3dDevice->CreatePixelShader( pDirectionalLightPSBuffer->GetBufferPointer(),
                                             pDirectionalLightPSBuffer->GetBufferSize(), NULL, &gDirectionalLightPS) );
    V_RETURN( pd3dDevice->CreatePixelShader( pSpotLightPSBuffer->GetBufferPointer(),
                                             pSpotLightPSBuffer->GetBufferSize(), NULL, &gSpotLightPS) );

    // Create our vertex input layout
    const D3D11_INPUT_ELEMENT_DESC layout[] =
    {
        { "POSITION",  0, DXGI_FORMAT_R32G32B32_FLOAT, 0, 0,  D3D11_INPUT_PER_VERTEX_DATA, 0 },
        { "NORMAL",    0, DXGI_FORMAT_R32G32B32_FLOAT, 0, 12, D3D11_INPUT_PER_VERTEX_DATA, 0 },
        { "TEXCOORD",  0, DXGI_FORMAT_R32G32_FLOAT,    0, 24, D3D11_INPUT_PER_VERTEX_DATA, 0 },
    };

    V_RETURN( pd3dDevice->CreateInputLayout( layout, ARRAYSIZE( layout ), pVertexShaderBuffer->GetBufferPointer(),
                                             pVertexShaderBuffer->GetBufferSize(), &g_pVertexLayout11 ) );

    SAFE_RELEASE( pVertexShaderBuffer );
    SAFE_RELEASE( pForwardPSBuffer );
    SAFE_RELEASE( pGBufferPSBuffer);

    // Load the mesh
    V_RETURN( g_Mesh11.Create( pd3dDevice, L"tiny\\tiny.sdkmesh", true ) );

    // Create a sampler state
    D3D11_SAMPLER_DESC SamDesc;
    SamDesc.Filter = D3D11_FILTER_MIN_MAG_MIP_LINEAR;
    SamDesc.AddressU = D3D11_TEXTURE_ADDRESS_WRAP;
    SamDesc.AddressV = D3D11_TEXTURE_ADDRESS_WRAP;
    SamDesc.AddressW = D3D11_TEXTURE_ADDRESS_WRAP;
    SamDesc.MipLODBias = 0.0f;
    SamDesc.MaxAnisotropy = 1;
    SamDesc.ComparisonFunc = D3D11_COMPARISON_ALWAYS;
    SamDesc.BorderColor[0] = SamDesc.BorderColor[1] = SamDesc.BorderColor[2] = SamDesc.BorderColor[3] = 0;
    SamDesc.MinLOD = 0;
    SamDesc.MaxLOD = D3D11_FLOAT32_MAX;
    V_RETURN( pd3dDevice->CreateSamplerState( &SamDesc, &g_pSamLinear ) );

    // Create lighting phase blend state
    {
        CD3D11_BLEND_DESC desc(D3D11_DEFAULT);
        // Additive blending
        desc.RenderTarget[0].BlendEnable = true;
        desc.RenderTarget[0].SrcBlend = D3D11_BLEND_ONE;
        desc.RenderTarget[0].DestBlend = D3D11_BLEND_ONE;
        desc.RenderTarget[0].BlendOp = D3D11_BLEND_OP_ADD;
        desc.RenderTarget[0].SrcBlendAlpha = D3D11_BLEND_ONE;
        desc.RenderTarget[0].DestBlendAlpha = D3D11_BLEND_ONE;
        desc.RenderTarget[0].BlendOpAlpha = D3D11_BLEND_OP_ADD;
        pd3dDevice->CreateBlendState(&desc, &mLightingBlendState);
    }


    // Setup constant buffers
    D3D11_BUFFER_DESC Desc;
    Desc.Usage = D3D11_USAGE_DYNAMIC;
    Desc.BindFlags = D3D11_BIND_CONSTANT_BUFFER;
    Desc.CPUAccessFlags = D3D11_CPU_ACCESS_WRITE;
    Desc.MiscFlags = 0;

    Desc.ByteWidth = sizeof( CB_VS_PER_OBJECT );
    V_RETURN( pd3dDevice->CreateBuffer( &Desc, NULL, &g_pcbVSPerObject ) );

    Desc.ByteWidth = sizeof( CB_PS_PER_OBJECT );
    V_RETURN( pd3dDevice->CreateBuffer( &Desc, NULL, &g_pcbPSPerObject ) );

    Desc.ByteWidth = sizeof( CB_PS_PER_FRAME );
    V_RETURN( pd3dDevice->CreateBuffer( &Desc, NULL, &g_pcbPSPerFrame ) );

    // Setup the camera's view parameters
    D3DXVECTOR3 vecEye( 0.0f, 0.0f, -100.0f );
    D3DXVECTOR3 vecAt ( 0.0f, 0.0f, -0.0f );
    g_Camera.SetViewParams( &vecEye, &vecAt );
    g_Camera.SetRadius( fObjectRadius * 3.0f, fObjectRadius * 0.5f, fObjectRadius * 10.0f );

    // Setup the spotlight's view parameters
    D3DXVECTOR3 vecSpotLight( 0.0f, 0.0f, -100.0f );
    D3DXVECTOR3 vecSpotLightLookAt ( 0.0f, 0.0f, -0.0f );
    g_SpotLight.SetViewParams( &vecSpotLight, &vecSpotLightLookAt );
    // May change this later
    g_SpotLight.SetRadius( fObjectRadius * 3.0f, fObjectRadius * 0.5f, fObjectRadius * 10.0f );


    // SPARK:
    gForwardSpark = gSparkContext->CreateShaderInstance<Forward>( pd3dDevice );
    gGenerateGBufferSpark = gSparkContext->CreateShaderInstance<GenerateGBuffer>( pd3dDevice );
    gDirectionalLightGBuffer = gSparkContext->CreateShaderInstance<DirectionalLightGBuffer>( pd3dDevice);

    return S_OK;
}


//--------------------------------------------------------------------------------------
// Create any D3D11 resources that depend on the back buffer
//--------------------------------------------------------------------------------------
HRESULT CALLBACK OnD3D11ResizedSwapChain( ID3D11Device* pd3dDevice, IDXGISwapChain* pSwapChain,
                                          const DXGI_SURFACE_DESC* pBackBufferSurfaceDesc, void* pUserContext )
{
    HRESULT hr;

    V_RETURN( g_DialogResourceManager.OnD3D11ResizedSwapChain( pd3dDevice, pBackBufferSurfaceDesc ) );
    V_RETURN( g_D3DSettingsDlg.OnD3D11ResizedSwapChain( pd3dDevice, pBackBufferSurfaceDesc ) );

    // Setup the camera's projection parameters
    float fAspectRatio = pBackBufferSurfaceDesc->Width / ( FLOAT )pBackBufferSurfaceDesc->Height;
    g_Camera.SetProjParams( D3DX_PI / 4, fAspectRatio, 2.0f, 4000.0f );
    g_Camera.SetWindow( pBackBufferSurfaceDesc->Width, pBackBufferSurfaceDesc->Height );
    g_Camera.SetButtonMasks( MOUSE_MIDDLE_BUTTON, MOUSE_WHEEL, MOUSE_LEFT_BUTTON );

    g_SpotLightAspect = 1.0f;
    g_SpotLight.SetProjParams( g_SpotLightFOV, g_SpotLightAspect, 2.0f, 4000.0f );
//    g_SpotLight.SetWindow( pBackBufferSurfaceDesc->Width, pBackBufferSurfaceDesc->Height );
//    g_SpotLight.SetButtonMasks( MOUSE_MIDDLE_BUTTON, MOUSE_WHEEL, MOUSE_LEFT_BUTTON );


    g_HUD.SetLocation( pBackBufferSurfaceDesc->Width - 170, 0 );
    g_HUD.SetSize( 170, 170 );
    g_SampleUI.SetLocation( pBackBufferSurfaceDesc->Width - 170, pBackBufferSurfaceDesc->Height - 300 );
    g_SampleUI.SetSize( 170, 300 );


// DS

    mGBufferWidth = pBackBufferSurfaceDesc->Width;
    mGBufferHeight = pBackBufferSurfaceDesc->Height;

    // Create/recreate any textures related to screen size
    mGBuffer.resize(0);
    mGBufferRTV.resize(0);
    mGBufferSRV.resize(0);
    mDepthBuffer = 0;
    SAFE_RELEASE(mDepthBufferReadOnlyDSV);

    // G-Buffer
    DXGI_SAMPLE_DESC sampleDesc;
    sampleDesc.Count = 1;
    sampleDesc.Quality = 0;

    // standard depth/stencil buffer
    mDepthBuffer = std::tr1::shared_ptr<Depth2D>(new Depth2D(
        pd3dDevice, mGBufferWidth, mGBufferHeight,
        D3D11_BIND_DEPTH_STENCIL | D3D11_BIND_SHADER_RESOURCE,
        sampleDesc,
        false// Include stencil if using MSAA
        ));

    // read-only depth stencil view
    {
        D3D11_DEPTH_STENCIL_VIEW_DESC desc;
        mDepthBuffer->GetDepthStencil()->GetDesc(&desc);
        desc.Flags = D3D11_DSV_READ_ONLY_DEPTH;

        pd3dDevice->CreateDepthStencilView(mDepthBuffer->GetTexture(), &desc, &mDepthBufferReadOnlyDSV);
    }

    // normal_specular
    mGBuffer.push_back(std::tr1::shared_ptr<Texture2D>(new Texture2D(
        pd3dDevice, mGBufferWidth, mGBufferHeight, DXGI_FORMAT_R16G16B16A16_FLOAT,
        D3D11_BIND_RENDER_TARGET | D3D11_BIND_SHADER_RESOURCE,
        sampleDesc)));

    // albedo
    mGBuffer.push_back(std::tr1::shared_ptr<Texture2D>(new Texture2D(
        pd3dDevice, mGBufferWidth, mGBufferHeight, DXGI_FORMAT_R8G8B8A8_UNORM,
        D3D11_BIND_RENDER_TARGET | D3D11_BIND_SHADER_RESOURCE,
        sampleDesc)));

    // positionZgrad
    mGBuffer.push_back(std::tr1::shared_ptr<Texture2D>(new Texture2D(
        pd3dDevice, mGBufferWidth, mGBufferHeight, DXGI_FORMAT_R16G16_FLOAT,
        D3D11_BIND_RENDER_TARGET | D3D11_BIND_SHADER_RESOURCE,
        sampleDesc)));

    // Set up GBuffer resource list
    mGBufferRTV.resize(mGBuffer.size(), 0);
    mGBufferSRV.resize(mGBuffer.size() + 1, 0);
    for (std::size_t i = 0; i < mGBuffer.size(); ++i) {
        mGBufferRTV[i] = mGBuffer[i]->GetRenderTarget();
        mGBufferSRV[i] = mGBuffer[i]->GetShaderResource();
    }
    // Depth buffer is the last SRV that we use for reading
    mGBufferSRV.back() = mDepthBuffer->GetShaderResource();

    return S_OK;
}

static spark::float4x4 Convert( const D3DXMATRIX& m )
{
    spark::float4x4 result;
    D3DXMatrixTranspose( reinterpret_cast<D3DXMATRIX*>(&result), &m );
    return result;
}

static spark::float3 Convert( const D3DXVECTOR3& v )
{
    return *reinterpret_cast<const spark::float3*>(&v);
}

void RenderForward( ID3D11DeviceContext* pd3dImmediateContext, ID3D11Device* pd3dDevice );

void RenderDeferredLighting( ID3D11DeviceContext* d3dDeviceContext, ID3D11Device* pd3dDevice ) 
{
    auto pDSV = DXUTGetD3D11DepthStencilView();
    auto pRTV = DXUTGetD3D11RenderTargetView();
    D3DXVECTOR3 vLightDir = g_LightControl.GetLightDirection();
    float fAmbient = 0.1f;

    if (gUseSpark) {
        gDirectionalLightGBuffer->GetFacet<UnpackGBuffer>()->SetNormalSpecularTexture( mGBufferSRV[0] );
        gDirectionalLightGBuffer->GetFacet<UnpackGBuffer>()->SetAlbedoTexture( mGBufferSRV[1] );
        gDirectionalLightGBuffer->GetFacet<UnpackGBuffer>()->SetZGradTexture( mGBufferSRV[2] );
        gDirectionalLightGBuffer->GetFacet<UnpackGBuffer>()->SetZBufferTexture( mGBufferSRV[3] );
        gDirectionalLightGBuffer->GetFacet<DirectionalLight>()->SetMyTarget(pRTV);
        gDirectionalLightGBuffer->SetDepthStencilView( pDSV );
        gDirectionalLightGBuffer->SetAmbient(fAmbient);
        gDirectionalLightGBuffer->SetLightDir(Convert(vLightDir));
        gDirectionalLightGBuffer->Submit(pd3dDevice, d3dDeviceContext); 
    } else {
        float ClearColor[4] = { 0.0f, 0.0f, 0.0f, 1.0f };
        d3dDeviceContext->ClearRenderTargetView(pRTV, ClearColor );
        d3dDeviceContext->OMSetRenderTargets(1, &pRTV, pDSV);
        d3dDeviceContext->OMSetBlendState(mLightingBlendState, 0, 0xFFFFFFFF);

        // Full screen triangle setup
        d3dDeviceContext->IASetInputLayout(0);
        d3dDeviceContext->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
        d3dDeviceContext->IASetVertexBuffers(0, 0, 0, 0, 0);

        d3dDeviceContext->VSSetShader(gFullScreenTriangleVS, 0, 0);
        d3dDeviceContext->GSSetShader(0, 0, 0);


        d3dDeviceContext->PSSetShaderResources(0, static_cast<UINT>(mGBufferSRV.size()), &mGBufferSRV.front());

        // Do pixel frequency shading
            // Get the light direction

        D3D11_MAPPED_SUBRESOURCE MappedResource;
        d3dDeviceContext->Map( g_pcbPSPerFrame, 0, D3D11_MAP_WRITE_DISCARD, 0, &MappedResource );
        CB_PS_PER_FRAME* pPerFrame = ( CB_PS_PER_FRAME* )MappedResource.pData;
        pPerFrame->m_vLightDirAmbient = D3DXVECTOR4( vLightDir.x, vLightDir.y, vLightDir.z, fAmbient );
        auto vDir = *g_SpotLight.GetLookAtPt() - *g_SpotLight.GetEyePt();
        pPerFrame->m_SpotLightDir = D3DXVECTOR4(vDir.x, vDir.y, vDir.z, 0.0f);
        pPerFrame->m_SpotLightPos = D3DXVECTOR4(*g_SpotLight.GetEyePt(), 1.0f);
        pPerFrame->m_SpotLightParameters = D3DXVECTOR4(g_SpotLightFOV, g_SpotLightAspect, g_SpotLight.GetNearClip(), g_SpotLight.GetFarClip());
        d3dDeviceContext->Unmap( g_pcbPSPerFrame, 0 );

        d3dDeviceContext->PSSetConstantBuffers( g_iCBPSPerFrameBind, 1, &g_pcbPSPerFrame );

        d3dDeviceContext->PSSetShader(gDirectionalLightPS, 0, 0);
    //    d3dDeviceContext->OMSetDepthStencilState(mEqualStencilState, 0);
        d3dDeviceContext->Draw(3, 0);

        d3dDeviceContext->PSSetShader(gSpotLightPS, 0, 0);
    //    d3dDeviceContext->OMSetDepthStencilState(mEqualStencilState, 0);
        d3dDeviceContext->Draw(3, 0);
        d3dDeviceContext->OMSetBlendState(NULL, 0, 0xFFFFFFFF);
    }
}

void RenderDeferred( ID3D11DeviceContext* pd3dImmediateContext, ID3D11Device* pd3dDevice ) 
{
    RenderGBuffer(pd3dImmediateContext, pd3dDevice);
    RenderDeferredLighting(pd3dImmediateContext, pd3dDevice);
}

//--------------------------------------------------------------------------------------
// Render the scene using the D3D11 device
//--------------------------------------------------------------------------------------
void CALLBACK OnD3D11FrameRender( ID3D11Device* pd3dDevice, ID3D11DeviceContext* pd3dImmediateContext, double fTime,
                                  float fElapsedTime, void* pUserContext )
{
    // If the settings dialog is being shown, then render it instead of rendering the app's scene
    if( g_D3DSettingsDlg.IsActive() )
    {
        g_D3DSettingsDlg.OnRender( fElapsedTime );
        return;
    }
    // Clear the render target and depth stencil
    float ClearColor[4] = { 0.0f, 0.25f, 0.25f, 0.55f };
    ID3D11RenderTargetView* pRTV = DXUTGetD3D11RenderTargetView();
    pd3dImmediateContext->ClearRenderTargetView( pRTV, ClearColor );
    ID3D11DepthStencilView* pDSV = DXUTGetD3D11DepthStencilView();
    pd3dImmediateContext->ClearDepthStencilView( pDSV, D3D11_CLEAR_DEPTH, 1.0, 0 );


    if (gUseDeferred) {
        RenderDeferred(pd3dImmediateContext, pd3dDevice);
    } else {
        RenderForward(pd3dImmediateContext, pd3dDevice);
    }


    DXUT_BeginPerfEvent( DXUT_PERFEVENTCOLOR, L"HUD / Stats" );
    g_HUD.OnRender( fElapsedTime );
    g_SampleUI.OnRender( fElapsedTime );
    RenderText();
    DXUT_EndPerfEvent();
}


//--------------------------------------------------------------------------------------
// Release D3D11 resources created in OnD3D11ResizedSwapChain 
//--------------------------------------------------------------------------------------
void CALLBACK OnD3D11ReleasingSwapChain( void* pUserContext )
{
    g_DialogResourceManager.OnD3D11ReleasingSwapChain();
}


//--------------------------------------------------------------------------------------
// Release D3D11 resources created in OnD3D11CreateDevice 
//--------------------------------------------------------------------------------------
void CALLBACK OnD3D11DestroyDevice( void* pUserContext )
{
    g_DialogResourceManager.OnD3D11DestroyDevice();
    g_D3DSettingsDlg.OnD3D11DestroyDevice();
    //CDXUTDirectionWidget::StaticOnD3D11DestroyDevice();
    DXUTGetGlobalResourceCache().OnDestroyDevice();
    SAFE_DELETE( g_pTxtHelper );

    g_Mesh11.Destroy();
                
    SAFE_RELEASE( g_pVertexLayout11 );
    SAFE_RELEASE( g_pVertexBuffer );
    SAFE_RELEASE( g_pIndexBuffer );
    SAFE_RELEASE( g_pVertexShader );
    SAFE_RELEASE( gFullScreenTriangleVS);
    SAFE_RELEASE( gForwardPS );
    SAFE_RELEASE( g_pSamLinear );
    SAFE_RELEASE( gGBufferPS );
    SAFE_RELEASE( gDirectionalLightPS);
    SAFE_RELEASE( mLightingBlendState );

    SAFE_RELEASE( g_pcbVSPerObject );
    SAFE_RELEASE( g_pcbPSPerObject );
    SAFE_RELEASE( g_pcbPSPerFrame );

    // SPARK:
    SAFE_RELEASE( gForwardSpark );
    SAFE_RELEASE( gGenerateGBufferSpark);
    SAFE_RELEASE( gDirectionalLightGBuffer );
}


void RenderForward( ID3D11DeviceContext* pd3dImmediateContext, ID3D11Device* pd3dDevice )
{
    ID3D11RenderTargetView* pRTV = DXUTGetD3D11RenderTargetView();
    ID3D11DepthStencilView* pDSV = DXUTGetD3D11DepthStencilView();
    if (gUseSpark) {
        gForwardSpark->GetFacet<DirectionalLight>()->SetMyTarget( pRTV );
    }
    RenderScene(pd3dImmediateContext, pRTV, pDSV, pd3dDevice, g_pVertexShader, gForwardPS, gForwardSpark);
}

void RenderSceneHLSL( ID3D11DeviceContext* pd3dImmediateContext, D3DXVECTOR3 &vLightDir, float fAmbient, D3DXMATRIX &mWorld, D3DXMATRIX &mProj, D3DXMATRIX &mView, ID3D11VertexShader *pVS, ID3D11PixelShader *pPS )
{
    HRESULT hr;

    // Per frame cb update
    D3D11_MAPPED_SUBRESOURCE MappedResource;
    V( pd3dImmediateContext->Map( g_pcbPSPerFrame, 0, D3D11_MAP_WRITE_DISCARD, 0, &MappedResource ) );
    CB_PS_PER_FRAME* pPerFrame = ( CB_PS_PER_FRAME* )MappedResource.pData;
    pPerFrame->m_vLightDirAmbient = D3DXVECTOR4( vLightDir.x, vLightDir.y, vLightDir.z, fAmbient );
    pd3dImmediateContext->Unmap( g_pcbPSPerFrame, 0 );

    pd3dImmediateContext->PSSetConstantBuffers( g_iCBPSPerFrameBind, 1, &g_pcbPSPerFrame );

    //Get the mesh
    //IA setup
    pd3dImmediateContext->IASetInputLayout( g_pVertexLayout11 );
    UINT Strides[1];
    UINT Offsets[1];
    ID3D11Buffer* pVB[1];
    pVB[0] = g_Mesh11.GetVB11( 0, 0 );
    Strides[0] = ( UINT )g_Mesh11.GetVertexStride( 0, 0 );
    Offsets[0] = 0;
    pd3dImmediateContext->IASetVertexBuffers( 0, 1, pVB, Strides, Offsets );
    pd3dImmediateContext->IASetIndexBuffer( g_Mesh11.GetIB11( 0 ), g_Mesh11.GetIBFormat11( 0 ), 0 );

    // Set the shaders
    pd3dImmediateContext->VSSetShader( pVS, NULL, 0 );
    pd3dImmediateContext->PSSetShader( pPS, NULL, 0 );

    // Set the per object constant data
    mWorld = g_mCenterMesh * *g_Camera.GetWorldMatrix();
    mProj = *g_Camera.GetProjMatrix();
    mView = *g_Camera.GetViewMatrix();

    D3DXMATRIX mWorldViewProjection;
    mWorldViewProjection = mWorld * mView * mProj;

    D3DXMATRIX mWorldView;
    mWorldView = mWorld * mView ;

    // VS Per object
    V( pd3dImmediateContext->Map( g_pcbVSPerObject, 0, D3D11_MAP_WRITE_DISCARD, 0, &MappedResource ) );
    CB_VS_PER_OBJECT* pVSPerObject = ( CB_VS_PER_OBJECT* )MappedResource.pData;
    D3DXMatrixTranspose( &pVSPerObject->m_WorldViewProj, &mWorldViewProjection );
    D3DXMatrixTranspose( &pVSPerObject->m_World, &mWorld );
    D3DXMatrixTranspose( &pVSPerObject->m_WorldView, &mWorldView );
    pd3dImmediateContext->Unmap( g_pcbVSPerObject, 0 );
    pVSPerObject->m_vCameraPos = D3DXVECTOR4(*g_Camera.GetEyePt(), 1.0f);

    pd3dImmediateContext->VSSetConstantBuffers( g_iCBVSPerObjectBind, 1, &g_pcbVSPerObject );

    // PS Per object
    V( pd3dImmediateContext->Map( g_pcbPSPerObject, 0, D3D11_MAP_WRITE_DISCARD, 0, &MappedResource ) );
    CB_PS_PER_OBJECT* pPSPerObject = ( CB_PS_PER_OBJECT* )MappedResource.pData;
    pPSPerObject->m_vObjectColor = D3DXVECTOR4( 1, 1, 1, 1 );
    pd3dImmediateContext->Unmap( g_pcbPSPerObject, 0 );

    pd3dImmediateContext->PSSetConstantBuffers( g_iCBPSPerObjectBind, 1, &g_pcbPSPerObject );

    //Render
    SDKMESH_SUBSET* pSubset = NULL;
    D3D11_PRIMITIVE_TOPOLOGY PrimType;

    pd3dImmediateContext->PSSetSamplers( 0, 1, &g_pSamLinear );

    for( UINT subset = 0; subset < g_Mesh11.GetNumSubsets( 0 ); ++subset )
    {
        // Get the subset
        pSubset = g_Mesh11.GetSubset( 0, subset );

        PrimType = CDXUTSDKMesh::GetPrimitiveType11( ( SDKMESH_PRIMITIVE_TYPE )pSubset->PrimitiveType );
        pd3dImmediateContext->IASetPrimitiveTopology( PrimType );

        // TODO: D3D11 - material loading
        ID3D11ShaderResourceView* pDiffuseRV = g_Mesh11.GetMaterial( pSubset->MaterialID )->pDiffuseRV11;
        pd3dImmediateContext->PSSetShaderResources( 0, 1, &pDiffuseRV );

        pd3dImmediateContext->DrawIndexed( ( UINT )pSubset->IndexCount, 0, ( UINT )pSubset->VertexStart );
    }
}

void RenderSceneSpark( ID3D11RenderTargetView* pRTV, ID3D11DepthStencilView* pDSV, 
    D3DXMATRIX mWorld, D3DXMATRIX mView, D3DXMATRIX mProj, D3DXVECTOR3 vLightDir, 
    float fAmbient, D3DXVECTOR3 vCameraPos, ID3D11Device* pd3dDevice, 
    ID3D11DeviceContext* pd3dImmediateContext, Base * sparkShader )
{
    // Set color and depth/stencil targets
    sparkShader->SetDepthStencilView( pDSV );

    // Set various uniform inputs
    sparkShader->SetWorld( Convert( mWorld ) );
    sparkShader->SetView( Convert( mView ) );
    sparkShader->SetProj( Convert( mProj ) );
    sparkShader->SetObjectColor( spark::float4(1, 1, 1, 1) );
    sparkShader->SetLightDir( Convert(vLightDir) );
    sparkShader->SetAmbient( fAmbient );
    sparkShader->SetLinearSampler( g_pSamLinear );
//    gShaderInstance->SetCameraPos(Convert(vCameraPos));

    // Set up the vertex stream
    ID3D11Buffer* vb = g_Mesh11.GetVB11( 0, 0 );
    UINT vbOffset = 0;
    UINT vbStride = g_Mesh11.GetVertexStride( 0, 0 );
    sparkShader->SetMyVertexStream(
        spark::d3d11::VertexStream(
        vb, vbOffset, vbStride ) );

    // Create the index stream (part of the DrawSpan)
    ID3D11Buffer* ib = g_Mesh11.GetIB11( 0 );
    DXGI_FORMAT ibFormat = g_Mesh11.GetIBFormat11( 0 );
    UINT ibOffset = 0;
    spark::d3d11::IndexStream indexStream(
        ib, ibFormat, ibOffset );

    // For each subset...
    for( UINT subset = 0; subset < g_Mesh11.GetNumSubsets( 0 ); ++subset )
    {
        // Get the subset
        SDKMESH_SUBSET* pSubset = g_Mesh11.GetSubset( 0, subset );

        // Set up draw span, now that we know the number and
        // type of primitives to render.
        D3D11_PRIMITIVE_TOPOLOGY primTopo =
            CDXUTSDKMesh::GetPrimitiveType11( ( SDKMESH_PRIMITIVE_TYPE )pSubset->PrimitiveType );
        spark::d3d11::DrawSpan drawSpan =
            spark::d3d11::IndexedDrawSpan(
            primTopo,
            indexStream,
            (UINT)pSubset->IndexCount,
            (UINT)pSubset->IndexStart,
            (UINT)pSubset->VertexStart);
        sparkShader->SetMyDrawSpan( drawSpan );

        // Set the diffuse texture from the subset material
        ID3D11ShaderResourceView* diffuseTexture =
            g_Mesh11.GetMaterial( pSubset->MaterialID )->pDiffuseRV11;
        sparkShader->SetDiffuseTexture( diffuseTexture );

        // Submit a rendering operation using this configuration
        sparkShader->Submit( pd3dDevice, pd3dImmediateContext );
    }

    // Restore color and depth/stencil targets to what the D3D code expects
    pd3dImmediateContext->OMSetRenderTargets(1, &pRTV, pDSV);
}

void RenderScene( ID3D11DeviceContext* pd3dImmediateContext, ID3D11RenderTargetView* pRTV, 
    ID3D11DepthStencilView* pDSV, ID3D11Device* pd3dDevice, ID3D11VertexShader *pVS, 
    ID3D11PixelShader *pPS, Base * sparkShader )
{
    // Common:
    D3DXVECTOR3 vLightDir;
    D3DXMATRIX mWorld;
    D3DXMATRIX mView;
    D3DXMATRIX mProj;

    // Get the projection & view matrix from the camera class
    mProj = *g_Camera.GetProjMatrix();
    mView = *g_Camera.GetViewMatrix();
    D3DXVECTOR3 vCameraPos = *g_Camera.GetEyePt();

    // Get the light direction
    vLightDir = g_LightControl.GetLightDirection();

    float fAmbient = 0.1f;

    mWorld = g_mCenterMesh * *g_Camera.GetWorldMatrix();
    mProj = *g_Camera.GetProjMatrix();
    mView = *g_Camera.GetViewMatrix();
    

    // D3D11:
    if(!gUseSpark)
    {
        RenderSceneHLSL(pd3dImmediateContext, vLightDir, fAmbient, mWorld, mProj, mView, pVS, pPS);
    }
    else
    {
        // SPARK:
        RenderSceneSpark(pRTV, pDSV, mWorld, mView, mProj, vLightDir, fAmbient, vCameraPos, pd3dDevice, pd3dImmediateContext, sparkShader);
    }
}



