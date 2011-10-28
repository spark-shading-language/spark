//--------------------------------------------------------------------------------------
// File: CubeMapGS.cpp
//
// Copyright (c) Microsoft Corporation. All rights reserved.
//--------------------------------------------------------------------------------------
#include "DXUT.h"
#include "DXUTgui.h"
#include "DXUTcamera.h"
#include "DXUTsettingsdlg.h"
#include "SDKmesh.h"
#include "SDKmisc.h"
#include "resource.h"

#include "d3dx11effect.h"

// BEGIN SPARK
#include "CubeMapGS.spark.h"
static bool gUseSpark = true;
// END SPARK

#define ENVMAPSIZE 256
#define MIPLEVELS 9
#define DEG2RAD( a ) ( a * D3DX_PI / 180.f )

struct CubeMapVertex
{
    D3DXVECTOR3 Pos;
    D3DXVECTOR3 Normal;
    D3DXVECTOR2 Tex;
};


//--------------------------------------------------------------------------------------
// Global variables
//--------------------------------------------------------------------------------------
bool                                g_bShowHelp = true;     // If true, it renders the UI control text
CModelViewerCamera                  g_Camera;               // A model viewing camera
CDXUTDialogResourceManager          g_DialogResourceManager; // manager for shared resources of dialogs
CD3DSettingsDlg                     g_D3DSettingsDlg;       // Device settings dialog
CDXUTDialog                         g_HUD;                  // manages the 3D UI
CDXUTDialog                         g_SampleUI;             // dialog for sample specific controls
bool                                g_bRenderCar = false;                // Whether to render the car or the ball

enum CubeMapTechnique
{
    kCubeMapTechnique_Multipass = 0,
    kCubeMapTechnique_GSLoop,
    kCubeMapTechnique_IAInstancing,
    kCubeMapTechnique_GSInstancing,
};
CubeMapTechnique gCubeMapTech = kCubeMapTechnique_IAInstancing;

CDXUTTextHelper*                    g_pTxtHelper = NULL;

ID3D11InputLayout*                  g_pVertexLayout = NULL;
ID3D11InputLayout*                  g_pVertexLayoutCM = NULL;
ID3D11InputLayout*                  g_pVertexLayoutCMInst = NULL;
ID3D11InputLayout*                  g_pVertexLayoutCMGSInst = NULL;
ID3D11InputLayout*                  g_pVertexLayoutEnv = NULL;

CDXUTSDKMesh                        g_MeshCar;
CDXUTSDKMesh                        g_MeshCarInnards;
CDXUTSDKMesh                        g_MeshCarGlass;
CDXUTSDKMesh                        g_MeshRoom;
CDXUTSDKMesh                        g_MeshMonitors;
CDXUTSDKMesh                        g_MeshArm;
CDXUTSDKMesh                        g_MeshBall;

ID3D11Texture2D*                    g_pEnvMap;          // Environment map
ID3D11RenderTargetView*             g_pEnvMapRTV;       // Render target view for the alpha map
ID3D11RenderTargetView*             g_apEnvMapOneRTV[6];// 6 render target view, each view is used for 1 face of the env map
ID3D11ShaderResourceView*           g_pEnvMapSRV;       // Shader resource view for the cubic env map
ID3D11ShaderResourceView*           g_apEnvMapOneSRV[6];// Single-face shader resource view
ID3D11Texture2D*                    g_pEnvMapDepth;     // Depth stencil for the environment map
ID3D11DepthStencilView*             g_pEnvMapDSV;       // Depth stencil view for environment map for all 6 faces
ID3D11DepthStencilView*             g_pEnvMapOneDSV;    // Depth stencil view for environment map for all 1 face
ID3D11Buffer*                       g_pVBVisual;        // Vertex buffer for quad used for visualization
ID3D11ShaderResourceView*           g_pFalloffTexRV;    // Resource view for the falloff texture

D3DXMATRIX                          g_mWorldRoom;    // World matrix of the room
D3DXMATRIX                          g_mWorldArm;     // World matrix of the Arm
D3DXMATRIX                          g_mWorldCar;     // World matrix of the car
D3DXMATRIX g_amCubeMapViewAdjust[6]; // Adjustment for view matrices when rendering the cube map
D3DXMATRIX                          g_mProjCM;       // Projection matrix for cubic env map rendering

ID3DX11Effect*                       g_pEffect;

ID3DX11EffectMatrixVariable*         g_pmWorldViewProj;
ID3DX11EffectMatrixVariable*         g_pmWorldView;
ID3DX11EffectMatrixVariable*         g_pmWorld;
ID3DX11EffectMatrixVariable*         g_pmView;
ID3DX11EffectMatrixVariable*         g_pmProj;
ID3DX11EffectShaderResourceVariable* g_ptxDiffuse;
ID3DX11EffectShaderResourceVariable* g_ptxEnvMap;
ID3DX11EffectShaderResourceVariable* g_ptxFalloffMap;
ID3DX11EffectVectorVariable*         g_pvDiffuse;
ID3DX11EffectVectorVariable*         g_pvSpecular;
ID3DX11EffectVectorVariable*         g_pvEye;
ID3DX11EffectMatrixVariable*         g_pmViewCM;

ID3DX11EffectTechnique*              g_pRenderCubeMapTech;
ID3DX11EffectTechnique*              g_pRenderCubeMapIAInstTech;
ID3DX11EffectTechnique*              g_pRenderCubeMapGSInstTech;
ID3DX11EffectTechnique*              g_pRenderSceneTech;
ID3DX11EffectTechnique*              g_pRenderEnvMappedSceneTech;
ID3DX11EffectTechnique*              g_pRenderEnvMappedSceneNoTexTech;
ID3DX11EffectTechnique*              g_pRenderEnvMappedGlassTech;

// BEGIN SPARK
spark::IContext*                    gSparkContext = nullptr;

RenderCubeMapSpark*                 gRenderCubeMapSpark = nullptr;
RenderCubeMapInstSpark*             gRenderCubeMapInstSpark = nullptr;
RenderCubeMapGSInstSpark*           gRenderCubeMapGSInstSpark = nullptr;
RenderSceneSpark*                   gRenderSceneSpark = nullptr;
RenderEnvMappedCarSpark*            gRenderEnvMappedCarSpark = nullptr;
RenderEnvMappedMetalSpark*          gRenderEnvMappedMetalSpark = nullptr;
RenderEnvMappedGlassSpark*          gRenderEnvMappedGlassSpark = nullptr;
// END SPARK

//--------------------------------------------------------------------------------------
// UI control IDs
//--------------------------------------------------------------------------------------
#define IDC_TOGGLEFULLSCREEN    1
#define IDC_TOGGLEREF           3
#define IDC_CHANGEDEVICE        4
#define IDC_RENDERCAR           5
#define IDC_TOGGLEWARP          7
// BEGIN SPARK
// SPARK:
#define IDC_CHECKBOX_USE_SPARK  8
// END SPARK
#define IDC_STATIC_TECHNIQUE    9
#define IDC_COMBOBOX_TECHNIQUE  10


//--------------------------------------------------------------------------------------
// Forward declarations 
//--------------------------------------------------------------------------------------
bool CALLBACK ModifyDeviceSettings( DXUTDeviceSettings* pDeviceSettings, void* pUserContext );
void CALLBACK OnFrameMove( double fTime, float fElapsedTime, void* pUserContext );
LRESULT CALLBACK MsgProc( HWND hWnd, UINT uMsg, WPARAM wParam, LPARAM lParam, bool* pbNoFurtherProcessing,
                          void* pUserContext );
void CALLBACK KeyboardProc( UINT nChar, bool bKeyDown, bool bAltDown, void* pUserContext );
void CALLBACK OnGUIEvent( UINT nEvent, int nControlID, CDXUTControl* pControl, void* pUserContext );

bool CALLBACK IsD3D11DeviceAcceptable(const CD3D11EnumAdapterInfo *AdapterInfo, UINT Output, const CD3D11EnumDeviceInfo *DeviceInfo,
                                       DXGI_FORMAT BackBufferFormat, bool bWindowed, void* pUserContext );
HRESULT CALLBACK OnD3D11CreateDevice( ID3D11Device* pd3dDevice, const DXGI_SURFACE_DESC* pBackBufferSurfaceDesc,
                                      void* pUserContext );
HRESULT CALLBACK OnD3D11SwapChainResized( ID3D11Device* pd3dDevice, IDXGISwapChain* pSwapChain,
                                          const DXGI_SURFACE_DESC* pBackBufferSurfaceDesc, void* pUserContext );
void CALLBACK OnD3D11SwapChainReleasing( void* pUserContext );
void CALLBACK OnD3D11DestroyDevice( void* pUserContext );
void CALLBACK OnD3D11FrameRender( ID3D11Device* pd3dDevice, ID3D11DeviceContext* pd3dImmediateContext, double fTime,
                                  float fElapsedTime, void* pUserContext );

void InitApp();
void RenderText();


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
    DXUTSetCallbackKeyboard( KeyboardProc );
    DXUTSetCallbackFrameMove( OnFrameMove );
    DXUTSetCallbackD3D11DeviceAcceptable( IsD3D11DeviceAcceptable );
    DXUTSetCallbackD3D11DeviceCreated( OnD3D11CreateDevice );
    DXUTSetCallbackD3D11SwapChainResized( OnD3D11SwapChainResized );
    DXUTSetCallbackD3D11FrameRender( OnD3D11FrameRender );
    DXUTSetCallbackD3D11SwapChainReleasing( OnD3D11SwapChainReleasing );
    DXUTSetCallbackD3D11DeviceDestroyed( OnD3D11DestroyDevice );

    InitApp();
    DXUTInit( true, true, NULL ); // Parse the command line, show msgboxes on error, no extra command line params
    DXUTSetCursorSettings( true, true ); // Show the cursor and clip it when in full screen
    DXUTCreateWindow( L"CubeMapGS" );
    DXUTCreateDevice( D3D_FEATURE_LEVEL_11_0, true, 640, 480 );
    DXUTMainLoop(); // Enter into the DXUT render loop

    return DXUTGetExitCode();
}


//--------------------------------------------------------------------------------------
// Initialize the app 
//--------------------------------------------------------------------------------------
void InitApp()
{
    // Initialize dialogs
    g_D3DSettingsDlg.Init( &g_DialogResourceManager );
    g_HUD.Init( &g_DialogResourceManager );
    g_SampleUI.Init( &g_DialogResourceManager );

    g_HUD.SetCallback( OnGUIEvent ); int iY = 10;
    g_HUD.AddButton( IDC_TOGGLEFULLSCREEN, L"Toggle full screen", 35, iY, 125, 22 );
    g_HUD.AddButton( IDC_CHANGEDEVICE, L"Change device (F2)", 35, iY += 24, 125, 22, VK_F2 );
    g_HUD.AddButton( IDC_TOGGLEREF, L"Toggle REF (F3)", 35, iY += 24, 125, 22, VK_F3 );
    g_HUD.AddButton( IDC_TOGGLEWARP, L"Toggle WARP (F4)", 35, iY += 24, 125, 22, VK_F4 );

    iY = 0;

    // BEGIN SPARK
    g_SampleUI.AddCheckBox( IDC_CHECKBOX_USE_SPARK, L"Use Spar(K)", 35, iY += 24, 125, 22, gUseSpark, 'K' );
    // END SPARK

    g_SampleUI.AddCheckBox( IDC_RENDERCAR, L"Render Car", 35, iY += 24, 125, 22, g_bRenderCar );


    g_SampleUI.AddStatic( IDC_STATIC_TECHNIQUE, L"Technique:", 0, iY += 24, 55, 22 );
    CDXUTComboBox *pCombo;
    g_SampleUI.AddComboBox( IDC_COMBOBOX_TECHNIQUE, 15, iY += 26, 145, 26, 0, true, &pCombo );
    if( pCombo )
    {
        pCombo->SetDropHeight( 60 );
        pCombo->AddItem( L"Multipass", NULL );
        pCombo->AddItem( L"GS Loop", NULL );
        pCombo->AddItem( L"IA Instancing", NULL );
        pCombo->AddItem( L"GS Instancing", NULL );
        pCombo->SetSelectedByIndex( (int) gCubeMapTech );
    }

    g_SampleUI.SetCallback( OnGUIEvent ); iY = 10;
}

//--------------------------------------------------------------------------------------
// Called right before creating a D3D9 or D3D11 device, allowing the app to modify the device settings as needed
//--------------------------------------------------------------------------------------
bool CALLBACK ModifyDeviceSettings( DXUTDeviceSettings* pDeviceSettings, void* pUserContext )
{
    // Uncomment this to get debug information from D3D11
//    pDeviceSettings->d3d11.CreateFlags |= D3D11_CREATE_DEVICE_DEBUG;

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
    g_Camera.FrameMove( fElapsedTime );

    float fRotSpeed = 60.0f;

    // Rotate the arm
    D3DXMatrixRotationY( &g_mWorldArm, ( float )( fTime * DEG2RAD(fRotSpeed) ) );
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

    // Pass all remaining windows messages to camera so it can respond to user input
    g_Camera.HandleMessages( hWnd, uMsg, wParam, lParam );

    return 0;
}


//--------------------------------------------------------------------------------------
// Handle key presses
//--------------------------------------------------------------------------------------
void CALLBACK KeyboardProc( UINT nChar, bool bKeyDown, bool bAltDown, void* pUserContext )
{
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
        case IDC_TOGGLEWARP:
            DXUTToggleWARP(); break;
        case IDC_RENDERCAR:
            g_bRenderCar = !g_bRenderCar; break;

        case IDC_COMBOBOX_TECHNIQUE:
            gCubeMapTech = (CubeMapTechnique) ((CDXUTComboBox*)pControl)->GetSelectedIndex();
            break;

        // BEGIN SPARK
        case IDC_CHECKBOX_USE_SPARK:
            gUseSpark = ((CDXUTCheckBox*)pControl)->GetChecked();
            break;
        // END SPARK
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

//

HRESULT WINAPI CompileAndCreateEffectFromFile(LPCTSTR szFileName, UINT FXFlags, ID3D11Device *pDevice, ID3DX11Effect **ppEffect)
{
    HRESULT hr = S_OK;

    // Find the path for the file
    WCHAR strPathW[MAX_PATH];
    V_RETURN( DXUTFindDXSDKMediaFileCch( strPathW, sizeof( strPathW ) / sizeof( WCHAR ), szFileName ) );

    // Open the file
    HANDLE hFile = CreateFile( strPathW, FILE_READ_DATA, FILE_SHARE_READ, NULL, OPEN_EXISTING, FILE_FLAG_SEQUENTIAL_SCAN,
                          NULL );
    if( INVALID_HANDLE_VALUE == hFile )
        return DXUTERR_MEDIANOTFOUND;

    // Get the file size
    LARGE_INTEGER FileSize;
    GetFileSizeEx( hFile, &FileSize );
    UINT cBytes = FileSize.LowPart;

    // Allocate memory
    BYTE* data = new BYTE[ cBytes ];
    if( !data )
    {
        CloseHandle( hFile );
        return E_OUTOFMEMORY;
    }

    // Read in the file
    DWORD dwBytesRead;
    if( !ReadFile( hFile, data, cBytes, &dwBytesRead, NULL ) )
        hr = E_FAIL;

    CloseHandle( hFile );

    if( !SUCCEEDED( hr ) )
    {
        delete[] data;
        return hr;
    }

    // Compile the shader
    ID3D10Blob* codeBlob = nullptr;
    ID3D10Blob* errorBlob = nullptr;

    hr = D3DCompile(
        data, cBytes,
        "<input>",
        nullptr,
        nullptr,
        nullptr,
        "fx_5_0",
        0,
        0,
        &codeBlob,
        &errorBlob);
    delete[] data;

    if( errorBlob != nullptr )
    {
        OutputDebugStringA((const char*) errorBlob->GetBufferPointer());
    }
    
    if( !SUCCEEDED( hr ) || (codeBlob == nullptr) )
    {
        SAFE_RELEASE(codeBlob);
        SAFE_RELEASE(errorBlob);
        return hr;
    }

    hr = D3DX11CreateEffectFromMemory(
        codeBlob->GetBufferPointer(),
        codeBlob->GetBufferSize(),
        FXFlags,
        pDevice,
        ppEffect );

    SAFE_RELEASE(codeBlob);
    SAFE_RELEASE(errorBlob);
    return hr;
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

    DWORD dwShaderFlags = D3D10_SHADER_ENABLE_STRICTNESS;
#if defined( DEBUG ) || defined( _DEBUG )
    // Set the D3D11_SHADER_DEBUG flag to embed debug information in the shaders.
    // Setting this flag improves the shader debugging experience, but still allows 
    // the shaders to be optimized and to run exactly the way they will run in 
    // the release configuration of this program.
    dwShaderFlags |= D3D10_SHADER_DEBUG;
    #endif

    // Read the D3DX effect file
    WCHAR str[MAX_PATH];
    V_RETURN( DXUTFindDXSDKMediaFileCch( str, MAX_PATH, L"CubeMapGS.fx" ) );
    V_RETURN( CompileAndCreateEffectFromFile( str, 0, pd3dDevice, &g_pEffect ) );

    D3DXMatrixScaling( &g_mWorldRoom, 3.7f, 3.7f, 3.7f );
    D3DXMATRIX m;
    D3DXMatrixTranslation( &m, 0.0f, 0.0f, 0.0f );
    D3DXMatrixMultiply( &g_mWorldRoom, &g_mWorldRoom, &m );

    D3DXMatrixIdentity( &g_mWorldArm );

    D3DXMatrixScaling( &m, 1.0f, 1.0f, 1.0f );
    D3DXMatrixRotationY( &g_mWorldCar, D3DX_PI * 0.5f );
    D3DXMatrixMultiply( &g_mWorldCar, &g_mWorldCar, &m );
    D3DXMatrixTranslation( &m, 0.0f, 3.2f, 0.0f );
    D3DXMatrixMultiply( &g_mWorldCar, &g_mWorldCar, &m );

    // Generate cube map view matrices
    float fHeight = 1.5f;
    D3DXVECTOR3 vEyePt = D3DXVECTOR3( 0.0f, fHeight, 0.0f );
    D3DXVECTOR3 vLookDir;
    D3DXVECTOR3 vUpDir;

    vLookDir = D3DXVECTOR3( 1.0f, fHeight, 0.0f );
    vUpDir = D3DXVECTOR3( 0.0f, 1.0f, 0.0f );
    D3DXMatrixLookAtLH( &g_amCubeMapViewAdjust[0], &vEyePt, &vLookDir, &vUpDir );
    vLookDir = D3DXVECTOR3( -1.0f, fHeight, 0.0f );
    vUpDir = D3DXVECTOR3( 0.0f, 1.0f, 0.0f );
    D3DXMatrixLookAtLH( &g_amCubeMapViewAdjust[1], &vEyePt, &vLookDir, &vUpDir );
    vLookDir = D3DXVECTOR3( 0.0f, fHeight + 1.0f, 0.0f );
    vUpDir = D3DXVECTOR3( 0.0f, 0.0f, -1.0f );
    D3DXMatrixLookAtLH( &g_amCubeMapViewAdjust[2], &vEyePt, &vLookDir, &vUpDir );
    vLookDir = D3DXVECTOR3( 0.0f, fHeight - 1.0f, 0.0f );
    vUpDir = D3DXVECTOR3( 0.0f, 0.0f, 1.0f );
    D3DXMatrixLookAtLH( &g_amCubeMapViewAdjust[3], &vEyePt, &vLookDir, &vUpDir );
    vLookDir = D3DXVECTOR3( 0.0f, fHeight, 1.0f );
    vUpDir = D3DXVECTOR3( 0.0f, 1.0f, 0.0f );
    D3DXMatrixLookAtLH( &g_amCubeMapViewAdjust[4], &vEyePt, &vLookDir, &vUpDir );
    vLookDir = D3DXVECTOR3( 0.0f, fHeight, -1.0f );
    vUpDir = D3DXVECTOR3( 0.0f, 1.0f, 0.0f );
    D3DXMatrixLookAtLH( &g_amCubeMapViewAdjust[5], &vEyePt, &vLookDir, &vUpDir );

    // Obtain the technique handles
    g_pRenderCubeMapTech = g_pEffect->GetTechniqueByName( "RenderCubeMap" );
    g_pRenderCubeMapIAInstTech = g_pEffect->GetTechniqueByName( "RenderCubeMap_Inst" );
    g_pRenderCubeMapGSInstTech = g_pEffect->GetTechniqueByName( "RenderCubeMap_GSInst" );
    g_pRenderSceneTech = g_pEffect->GetTechniqueByName( "RenderScene" );
    g_pRenderEnvMappedSceneTech = g_pEffect->GetTechniqueByName( "RenderEnvMappedScene" );
    g_pRenderEnvMappedSceneNoTexTech = g_pEffect->GetTechniqueByName( "RenderEnvMappedScene_NoTexture" );
    g_pRenderEnvMappedGlassTech = g_pEffect->GetTechniqueByName( "RenderEnvMappedGlass" );

    // Obtain the parameter handles
    g_pmWorldViewProj = g_pEffect->GetVariableByName( "mWorldViewProj" )->AsMatrix();
    g_pmWorldView = g_pEffect->GetVariableByName( "mWorldView" )->AsMatrix();
    g_pmWorld = g_pEffect->GetVariableByName( "mWorld" )->AsMatrix();
    g_pmView = g_pEffect->GetVariableByName( "mView" )->AsMatrix();
    g_pmProj = g_pEffect->GetVariableByName( "mProj" )->AsMatrix();
    g_ptxDiffuse = g_pEffect->GetVariableByName( "g_txDiffuse" )->AsShaderResource();
    g_ptxEnvMap = g_pEffect->GetVariableByName( "g_txEnvMap" )->AsShaderResource();
    g_ptxFalloffMap = g_pEffect->GetVariableByName( "g_txFalloff" )->AsShaderResource();
    g_pvDiffuse = g_pEffect->GetVariableByName( "vMaterialDiff" )->AsVector();
    g_pvSpecular = g_pEffect->GetVariableByName( "vMaterialSpec" )->AsVector();
    g_pvEye = g_pEffect->GetVariableByName( "vEye" )->AsVector();
    g_pmViewCM = g_pEffect->GetVariableByName( "g_mViewCM" )->AsMatrix();

    // Load a simple 1d falloff map for our car shader
    V_RETURN( DXUTFindDXSDKMediaFileCch( str, MAX_PATH, L"ExoticCar\\FalloffRamp.dds" ) );
    V_RETURN( D3DX11CreateShaderResourceViewFromFile( pd3dDevice, str, NULL, NULL, &g_pFalloffTexRV, NULL ) );

    // Create cubic depth stencil texture.
    D3D11_TEXTURE2D_DESC dstex;
    dstex.Width = ENVMAPSIZE;
    dstex.Height = ENVMAPSIZE;
    dstex.MipLevels = 1;
    dstex.ArraySize = 6;
    dstex.SampleDesc.Count = 1;
    dstex.SampleDesc.Quality = 0;
    dstex.Format = DXGI_FORMAT_D32_FLOAT;
    dstex.Usage = D3D11_USAGE_DEFAULT;
    dstex.BindFlags = D3D11_BIND_DEPTH_STENCIL;
    dstex.CPUAccessFlags = 0;
    dstex.MiscFlags = D3D11_RESOURCE_MISC_TEXTURECUBE;
    V_RETURN( pd3dDevice->CreateTexture2D( &dstex, NULL, &g_pEnvMapDepth ) );

    // Create the depth stencil view for the entire cube
    D3D11_DEPTH_STENCIL_VIEW_DESC DescDS;
    DescDS.Format = DXGI_FORMAT_D32_FLOAT;
    DescDS.ViewDimension = D3D11_DSV_DIMENSION_TEXTURE2DARRAY;
    DescDS.Flags = 0;
    DescDS.Texture2DArray.FirstArraySlice = 0;
    DescDS.Texture2DArray.ArraySize = 6;
    DescDS.Texture2DArray.MipSlice = 0;
    V_RETURN( pd3dDevice->CreateDepthStencilView( g_pEnvMapDepth, &DescDS, &g_pEnvMapDSV ) );

    // Create the depth stencil view for single face rendering
    DescDS.Texture2DArray.ArraySize = 1;
    V_RETURN( pd3dDevice->CreateDepthStencilView( g_pEnvMapDepth, &DescDS, &g_pEnvMapOneDSV ) );

    // Create the cube map for env map render target
    dstex.Format = DXGI_FORMAT_R8G8B8A8_UNORM;
    dstex.BindFlags = D3D11_BIND_RENDER_TARGET | D3D11_BIND_SHADER_RESOURCE;
    dstex.MiscFlags = D3D11_RESOURCE_MISC_GENERATE_MIPS | D3D11_RESOURCE_MISC_TEXTURECUBE;
    dstex.MipLevels = MIPLEVELS;
    V_RETURN( pd3dDevice->CreateTexture2D( &dstex, NULL, &g_pEnvMap ) );

    // Create the 6-face render target view
    D3D11_RENDER_TARGET_VIEW_DESC DescRT;
    DescRT.Format = dstex.Format;
    DescRT.ViewDimension = D3D11_RTV_DIMENSION_TEXTURE2DARRAY;
    DescRT.Texture2DArray.FirstArraySlice = 0;
    DescRT.Texture2DArray.ArraySize = 6;
    DescRT.Texture2DArray.MipSlice = 0;
    V_RETURN( pd3dDevice->CreateRenderTargetView( g_pEnvMap, &DescRT, &g_pEnvMapRTV ) );

    // Create the one-face render target views
    DescRT.Texture2DArray.ArraySize = 1;
    for( int i = 0; i < 6; ++i )
    {
        DescRT.Texture2DArray.FirstArraySlice = i;
        V_RETURN( pd3dDevice->CreateRenderTargetView( g_pEnvMap, &DescRT, &g_apEnvMapOneRTV[i] ) );
    }

    // Create the shader resource view for the cubic env map
    D3D11_SHADER_RESOURCE_VIEW_DESC SRVDesc;
    ZeroMemory( &SRVDesc, sizeof( SRVDesc ) );
    SRVDesc.Format = dstex.Format;
    SRVDesc.ViewDimension = D3D11_SRV_DIMENSION_TEXTURECUBE;
    SRVDesc.TextureCube.MipLevels = MIPLEVELS;
    SRVDesc.TextureCube.MostDetailedMip = 0;
    V_RETURN( pd3dDevice->CreateShaderResourceView( g_pEnvMap, &SRVDesc, &g_pEnvMapSRV ) );

    // Define our vertex data layout
    const D3D11_INPUT_ELEMENT_DESC layout[] =
    {
        { "POSITION", 0, DXGI_FORMAT_R32G32B32_FLOAT, 0, 0, D3D11_INPUT_PER_VERTEX_DATA, 0 },
        { "NORMAL", 0, DXGI_FORMAT_R32G32B32_FLOAT, 0, 12, D3D11_INPUT_PER_VERTEX_DATA, 0 },
        { "TEXCOORD", 0, DXGI_FORMAT_R32G32_FLOAT, 0, 24, D3D11_INPUT_PER_VERTEX_DATA, 0 },
    };

    D3DX11_PASS_DESC PassDesc;
    g_pRenderSceneTech->GetPassByIndex( 0 )->GetDesc( &PassDesc );
    V_RETURN( pd3dDevice->CreateInputLayout( layout, 3, PassDesc.pIAInputSignature,
                                             PassDesc.IAInputSignatureSize, &g_pVertexLayout ) );

    g_pRenderCubeMapTech->GetPassByIndex( 0 )->GetDesc( &PassDesc );
    V_RETURN( pd3dDevice->CreateInputLayout( layout, 3, PassDesc.pIAInputSignature,
                                             PassDesc.IAInputSignatureSize, &g_pVertexLayoutCM ) );

    g_pRenderCubeMapGSInstTech->GetPassByIndex( 0 )->GetDesc( &PassDesc );
    V_RETURN( pd3dDevice->CreateInputLayout( layout, 3, PassDesc.pIAInputSignature,
                                             PassDesc.IAInputSignatureSize, &g_pVertexLayoutCMGSInst ) );

    g_pRenderCubeMapIAInstTech->GetPassByIndex( 0 )->GetDesc( &PassDesc );
    V_RETURN( pd3dDevice->CreateInputLayout( layout, 3, PassDesc.pIAInputSignature,
                                             PassDesc.IAInputSignatureSize, &g_pVertexLayoutCMInst ) );

    g_pRenderEnvMappedSceneTech->GetPassByIndex( 0 )->GetDesc( &PassDesc );
    V_RETURN( pd3dDevice->CreateInputLayout( layout, 3, PassDesc.pIAInputSignature,
                                             PassDesc.IAInputSignatureSize, &g_pVertexLayoutEnv ) );

    // Create mesh objects
    g_MeshCar.Create( pd3dDevice, L"exoticcar\\ExoticCar.sdkmesh", true );
    g_MeshCarInnards.Create( pd3dDevice, L"exoticcar\\CarInnards.sdkmesh", true );
    g_MeshCarGlass.Create( pd3dDevice, L"exoticcar\\CarGlass.sdkmesh", true );
    g_MeshRoom.Create( pd3dDevice, L"Scanner\\ScannerRoom.sdkmesh", true );
    g_MeshMonitors.Create( pd3dDevice, L"Scanner\\ScannerMonitors.sdkmesh", true );
    g_MeshArm.Create( pd3dDevice, L"Scanner\\ScannerArm.sdkmesh", true );
    g_MeshBall.Create( pd3dDevice, L"misc\\ReflectSphere.sdkmesh", true );

    // Create the projection matrices
    D3DXMatrixPerspectiveFovLH( &g_mProjCM, D3DX_PI * 0.5f, 1.0f, .5f, 1000.f );

    // Create a vertex buffer consisting of a quad for visualization
    {
        D3D11_BUFFER_DESC bufferdesc =
        {
            6 * sizeof( CubeMapVertex ),
            D3D11_USAGE_DEFAULT,
            D3D11_BIND_VERTEX_BUFFER,
            0,
            0
        };
        CubeMapVertex Quad[6] =
        {
            { D3DXVECTOR3( -1.0f, 1.0f, 0.5f ),
                D3DXVECTOR3( 0.0f, 0.0f, -1.0f ),
                D3DXVECTOR2( 0.0f, 0.0f )
            },
            { D3DXVECTOR3( 1.0f, 1.0f, 0.5f ),
                D3DXVECTOR3( 0.0f, 0.0f, -1.0f ),
                D3DXVECTOR2( 1.0f, 0.0f )
            },
            { D3DXVECTOR3( -1.0f, -1.0f, 0.5f ),
                D3DXVECTOR3( 0.0f, 0.0f, -1.0f ),
                D3DXVECTOR2( 0.0f, 1.0f )
            },
            { D3DXVECTOR3( -1.0f, -1.0f, 0.5f ),
                D3DXVECTOR3( 0.0f, 0.0f, -1.0f ),
                D3DXVECTOR2( 0.0f, 1.0f )
            },
            { D3DXVECTOR3( 1.0f, 1.0f, 0.5f ),
                D3DXVECTOR3( 0.0f, 0.0f, -1.0f ),
                D3DXVECTOR2( 1.0f, 0.0f )
            },
            { D3DXVECTOR3( 1.0f, -1.0f, 0.5f ),
                D3DXVECTOR3( 0.0f, 0.0f, -1.0f ),
                D3DXVECTOR2( 1.0f, 1.0f )
            }
        };
        D3D11_SUBRESOURCE_DATA InitData =
        {
            Quad,
            sizeof( Quad ),
            sizeof( Quad )
        };
        V_RETURN( pd3dDevice->CreateBuffer( &bufferdesc, &InitData, &g_pVBVisual ) );
    }

    D3DXVECTOR3 vecEye( 0.0f, 5.7f, -6.5f );
    D3DXVECTOR3 vecAt ( 0.0f, 4.7f, -0.0f );
    g_Camera.SetViewParams( &vecEye, &vecAt );

    // BEGIN SPARK

    // Load the Spark runtime library
    gSparkContext = SparkCreateContext();

    // Create shader instances from previously-compiled shader classes
    gRenderCubeMapSpark =               gSparkContext->CreateShaderInstance<RenderCubeMapSpark>( pd3dDevice );
    gRenderCubeMapInstSpark =           gSparkContext->CreateShaderInstance<RenderCubeMapInstSpark>( pd3dDevice );
    gRenderCubeMapGSInstSpark =         gSparkContext->CreateShaderInstance<RenderCubeMapGSInstSpark>( pd3dDevice );
    gRenderSceneSpark =                 gSparkContext->CreateShaderInstance<RenderSceneSpark>( pd3dDevice );
    gRenderEnvMappedCarSpark =          gSparkContext->CreateShaderInstance<RenderEnvMappedCarSpark>( pd3dDevice );
    gRenderEnvMappedMetalSpark =        gSparkContext->CreateShaderInstance<RenderEnvMappedMetalSpark>( pd3dDevice );
    gRenderEnvMappedGlassSpark =        gSparkContext->CreateShaderInstance<RenderEnvMappedGlassSpark>( pd3dDevice );

    // END SPARK

    return S_OK;
}


//--------------------------------------------------------------------------------------
// Create any D3D11 resources that depend on the back buffer
//--------------------------------------------------------------------------------------
HRESULT CALLBACK OnD3D11SwapChainResized( ID3D11Device* pd3dDevice, IDXGISwapChain* pSwapChain,
                                          const DXGI_SURFACE_DESC* pBackBufferSurfaceDesc, void* pUserContext )
{
    HRESULT hr = S_OK;

    V_RETURN( g_DialogResourceManager.OnD3D11ResizedSwapChain( pd3dDevice, pBackBufferSurfaceDesc ) );
    V_RETURN( g_D3DSettingsDlg.OnD3D11ResizedSwapChain( pd3dDevice, pBackBufferSurfaceDesc ) );

    // Setup the camera's projection parameters
    float fAspectRatio = pBackBufferSurfaceDesc->Width / ( FLOAT )pBackBufferSurfaceDesc->Height;
    g_Camera.SetProjParams( D3DX_PI / 3, fAspectRatio, 0.5f, 1000.0f );
    g_Camera.SetWindow( pBackBufferSurfaceDesc->Width, pBackBufferSurfaceDesc->Height );
    g_Camera.SetButtonMasks( 0, MOUSE_WHEEL, MOUSE_RIGHT_BUTTON | MOUSE_LEFT_BUTTON );

    g_HUD.SetLocation( pBackBufferSurfaceDesc->Width - 170, 0 );
    g_HUD.SetSize( 170, 170 );
    g_SampleUI.SetLocation( pBackBufferSurfaceDesc->Width - 170, pBackBufferSurfaceDesc->Height - 300 );
    g_SampleUI.SetSize( 170, 300 );

    return hr;
}

//

void RenderMeshInstanced( ID3D11DeviceContext* pd3dContext,
                          CDXUTSDKMesh* pMesh,
                          ID3DX11EffectTechnique* pTechnique,
                          ID3DX11EffectShaderResourceVariable* ptxDiffuse )
{
    ID3D11Buffer* pVB[1];
    UINT Strides[1];
    UINT Offsets[1] = {0};

    // Render the mesh instanced
    pVB[0] = pMesh->GetVB11( 0, 0 );
    Strides[0] = ( UINT )pMesh->GetVertexStride( 0, 0 );

    pd3dContext->IASetVertexBuffers( 0, 1, pVB, Strides, Offsets );
    pd3dContext->IASetIndexBuffer( pMesh->GetIB11( 0 ), pMesh->GetIBFormat11( 0 ), 0 );

    SDKMESH_SUBSET* pSubset = NULL;
    SDKMESH_MATERIAL* pMat = NULL;
    D3D11_PRIMITIVE_TOPOLOGY PrimType;
    D3DX11_TECHNIQUE_DESC techDesc;
    pTechnique->GetDesc( &techDesc );
    for( UINT p = 0; p < techDesc.Passes; ++p )
    {
        for( UINT subset = 0; subset < pMesh->GetNumSubsets( 0 ); ++subset )
        {
            pSubset = pMesh->GetSubset( 0, subset );

            PrimType = pMesh->GetPrimitiveType11( ( SDKMESH_PRIMITIVE_TYPE )pSubset->PrimitiveType );
            pd3dContext->IASetPrimitiveTopology( PrimType );

            pMat = pMesh->GetMaterial( pSubset->MaterialID );
            if( pMat )
                ptxDiffuse->SetResource( pMat->pDiffuseRV11 );

            pTechnique->GetPassByIndex( p )->Apply( 0, pd3dContext );
            pd3dContext->DrawIndexedInstanced( ( UINT )pSubset->IndexCount, 6, 0, ( UINT )pSubset->VertexStart, 0 );
        }
    }
}

void RenderMesh( ID3D11DeviceContext* pd3dContext,
                 CDXUTSDKMesh* pMesh,
                 ID3DX11EffectTechnique* pTechnique,
                 ID3DX11EffectShaderResourceVariable* ptxDiffuse = nullptr,
                 ID3DX11EffectShaderResourceVariable* ptxNormal = nullptr,
                 ID3DX11EffectShaderResourceVariable* ptxSpecular = nullptr,
                 ID3DX11EffectVectorVariable* pvDiffuse = nullptr,
                 ID3DX11EffectVectorVariable* pvSpecular = nullptr)
{
    ID3D11Buffer* pVB[1];
    UINT Strides[1];
    UINT Offsets[1] = {0};

    // Render the mesh
    pVB[0] = pMesh->GetVB11( 0, 0 );
    Strides[0] = ( UINT )pMesh->GetVertexStride( 0, 0 );

    pd3dContext->IASetVertexBuffers( 0, 1, pVB, Strides, Offsets );
    pd3dContext->IASetIndexBuffer( pMesh->GetIB11( 0 ), pMesh->GetIBFormat11( 0 ), 0 );

    SDKMESH_SUBSET* pSubset = NULL;
    SDKMESH_MATERIAL* pMat = NULL;
    D3D11_PRIMITIVE_TOPOLOGY PrimType;
    D3DX11_TECHNIQUE_DESC techDesc;
    pTechnique->GetDesc( &techDesc );
    for( UINT p = 0; p < techDesc.Passes; ++p )
    {
        for( UINT subset = 0; subset < pMesh->GetNumSubsets( 0 ); ++subset )
        {
            pSubset = pMesh->GetSubset( 0, subset );

            PrimType = pMesh->GetPrimitiveType11( ( SDKMESH_PRIMITIVE_TYPE )pSubset->PrimitiveType );
            pd3dContext->IASetPrimitiveTopology( PrimType );

            pMat = pMesh->GetMaterial( pSubset->MaterialID );
            if( pMat != nullptr )
            {
                if( ptxDiffuse != nullptr )
                    ptxDiffuse->SetResource( pMat->pDiffuseRV11 );
                if( ptxNormal != nullptr )
                    ptxNormal->SetResource( pMat->pNormalRV11 );
                if( ptxSpecular != nullptr )
                    ptxSpecular->SetResource( pMat->pSpecularRV11 );
                if( pvDiffuse != nullptr )
                    pvDiffuse->SetFloatVector( pMat->Diffuse );
                if( pvSpecular != nullptr )
                    pvSpecular->SetFloatVector( pMat->Specular );
            }

            pTechnique->GetPassByIndex( p )->Apply( 0, pd3dContext );
            pd3dContext->DrawIndexed( ( UINT )pSubset->IndexCount, 0, ( UINT )pSubset->VertexStart );
        }
    }
}

inline spark::float4x4 Convert( D3DXMATRIX m )
{
    spark::float4x4 result;
    D3DXMatrixTranspose( reinterpret_cast<D3DXMATRIX*>(&result), &m );
    return result;
}

inline spark::float3 Convert( D3DXVECTOR3 v )
{
    spark::float3 result;
    result.x = v.x;
    result.y = v.y;
    result.z = v.z;
    return result;
}

inline spark::float4 Convert( D3DXVECTOR4 v )
{
    spark::float4 result;
    result.x = v.x;
    result.y = v.y;
    result.z = v.z;
    result.w = v.w;
    return result;
}

void RenderMeshSpark(
    ID3D11DeviceContext* pd3dContext,
    CDXUTSDKMesh* pMesh,
    Base* shaderInstance,
    UINT instanceCount = 1 )
{
    D3DXVECTOR3 vCameraPos = *g_Camera.GetEyePt();
    shaderInstance->SetEyePosition( Convert( *g_Camera.GetEyePt() ) );

    shaderInstance->SetVertexStream(
        spark::d3d11::VertexStream(
            pMesh->GetVB11( 0, 0 ),
            0,
            (UINT) pMesh->GetVertexStride( 0, 0 ) ) );

    spark::d3d11::IndexStream indexStream(
        pMesh->GetIB11( 0 ),
        pMesh->GetIBFormat11( 0 ),
        0 );

    UINT subsetCount = pMesh->GetNumSubsets( 0 );
    for( UINT ii = 0; ii < subsetCount; ++ii )
    {
        auto subset = pMesh->GetSubset( 0, ii );

        auto primTopo = pMesh->GetPrimitiveType11( ( SDKMESH_PRIMITIVE_TYPE) subset->PrimitiveType );

        auto material = pMesh->GetMaterial( subset->MaterialID );
        if( material != nullptr )
        {
            shaderInstance->SetTxDiffuse( material->pDiffuseRV11 );
            shaderInstance->SetMaterialDiff( Convert( material->Diffuse ) );
            shaderInstance->SetMaterialSpec( Convert( material->Specular ) );
        }

        spark::d3d11::DrawSpan drawSpan = spark::d3d11::IndexedDrawSpan(
            primTopo,
            indexStream,
            subset->IndexCount,
            0,
            subset->VertexStart );

        if( instanceCount > 1 )
        {
            drawSpan = spark::d3d11::InstancedDrawSpan(drawSpan, instanceCount);
        }

        shaderInstance->SetDrawSpan( drawSpan );
        shaderInstance->Submit( DXUTGetD3D11Device(), pd3dContext );
    }
}

//--------------------------------------------------------------------------------------
// Render the scene
//--------------------------------------------------------------------------------------
void RenderSceneCommonHLSL(
    ID3D11DeviceContext* pd3dContext,
    const D3DXMATRIX& mView,
    const D3DXMATRIX& mProj,
    ID3DX11EffectTechnique* pTechnique )
{
    g_pmView->SetMatrix( ( float* )&mView );
    g_pmProj->SetMatrix( ( float* )&mProj );

    //
    // Render room
    //

    // Calculate the matrix world*view*proj
    D3DXMATRIX mWorldView;
    D3DXMATRIX mWorldViewProj;
    D3DXMatrixMultiply( &mWorldView, &g_mWorldRoom, &mView );
    D3DXMatrixMultiply( &mWorldViewProj, &mWorldView, &mProj );
    g_pmWorldViewProj->SetMatrix( ( float* )&mWorldViewProj );
    g_pmWorld->SetMatrix( ( float* )&g_mWorldRoom );

    if( gCubeMapTech == kCubeMapTechnique_IAInstancing )
    {
        RenderMeshInstanced( pd3dContext, &g_MeshRoom, pTechnique, g_ptxDiffuse );
        RenderMeshInstanced( pd3dContext, &g_MeshMonitors, pTechnique, g_ptxDiffuse );
    }
    else
    {
        RenderMesh( pd3dContext, &g_MeshRoom, pTechnique, g_ptxDiffuse );
        RenderMesh( pd3dContext, &g_MeshMonitors, pTechnique, g_ptxDiffuse );
    }

    D3DXMATRIX mArm;
    D3DXMatrixMultiply( &mArm, &g_mWorldArm, &g_mWorldRoom );
    D3DXMatrixMultiply( &mWorldView, &mArm, &mView );
    D3DXMatrixMultiply( &mWorldViewProj, &mWorldView, &mProj );
    g_pmWorldViewProj->SetMatrix( ( float* )&mWorldViewProj );
    g_pmWorld->SetMatrix( ( float* )&mArm );

    if( gCubeMapTech == kCubeMapTechnique_IAInstancing )
    {
        RenderMeshInstanced( pd3dContext, &g_MeshArm, pTechnique, g_ptxDiffuse );
    }
    else
    {
        RenderMesh( pd3dContext, &g_MeshArm, pTechnique, g_ptxDiffuse );
    }
}

void RenderSceneCommonSpark(
    ID3D11DeviceContext* d3dContext,
    const D3DXMATRIX& view,
    const D3DXMATRIX& proj,
    Base* shaderInstance,
    UINT instanceCount = 1 )
{
    shaderInstance->SetView( Convert( view ) );
    shaderInstance->SetProj( Convert( proj ) );

    // Calculate the matrix world*view*proj
    D3DXMATRIX worldView;
    D3DXMATRIX worldViewProj;
    D3DXMatrixMultiply( &worldView, &g_mWorldRoom, &view );
    D3DXMatrixMultiply( &worldViewProj, &worldView, &proj );

    shaderInstance->SetWorld( Convert( g_mWorldRoom ) );

    RenderMeshSpark( d3dContext, &g_MeshRoom, shaderInstance, instanceCount );
    RenderMeshSpark( d3dContext, &g_MeshMonitors, shaderInstance, instanceCount );

    D3DXMATRIX arm;
    D3DXMatrixMultiply( &arm, &g_mWorldArm, &g_mWorldRoom );
    D3DXMatrixMultiply( &worldView, &arm, &view );
    D3DXMatrixMultiply( &worldViewProj, &worldView, &proj );

    shaderInstance->SetWorld( Convert( arm ) );

    RenderMeshSpark( d3dContext, &g_MeshArm, shaderInstance, instanceCount );
}

void RenderSceneEnvMappedHLSL(
    ID3D11DeviceContext* pd3dContext,
    const D3DXMATRIX& mView,
    const D3DXMATRIX& mProj )
{
    // Set IA parameters
    pd3dContext->IASetInputLayout( g_pVertexLayoutEnv );

    // Calculate the matrix world*view*proj
    D3DXMATRIX mWorldView;
    D3DXMATRIX mWorldViewProj;
    D3DXMatrixMultiply( &mWorldView, &g_mWorldCar, &mView );
    D3DXMatrixMultiply( &mWorldViewProj, &mWorldView, &mProj );
    g_pmWorldView->SetMatrix( ( float* )&mWorldView );
    g_pmWorldViewProj->SetMatrix( ( float* )&mWorldViewProj );
    g_pmWorld->SetMatrix( ( float* )&g_mWorldCar );
    g_ptxFalloffMap->SetResource( g_pFalloffTexRV );

    // Set cube texture parameter in the effect
    g_ptxEnvMap->SetResource( g_pEnvMapSRV );

    if( g_bRenderCar )
    {
        // Render the shiny car shell
        RenderMesh( pd3dContext, &g_MeshCar, g_pRenderEnvMappedSceneTech,
            nullptr, nullptr, nullptr,
            g_pvDiffuse, g_pvSpecular );

        // Render the rest of the car that doesn't need to have a fresnel shader
        RenderMesh( pd3dContext, &g_MeshCarInnards, g_pRenderEnvMappedSceneNoTexTech,
            nullptr, nullptr, nullptr,
            g_pvDiffuse, g_pvSpecular );

        // Finally, render the glass
        RenderMesh( pd3dContext, &g_MeshCarGlass, g_pRenderEnvMappedGlassTech,
            nullptr, nullptr, nullptr,
            g_pvDiffuse, g_pvSpecular );
    }
    else
    {
        // Just render a shiny ball
        RenderMesh( pd3dContext, &g_MeshBall, g_pRenderEnvMappedSceneNoTexTech,
            nullptr, nullptr, nullptr,
            g_pvDiffuse, g_pvSpecular );
    }
}

void RenderEnvMappedMeshSpark(
    ID3D11DeviceContext* d3dContext,
    const D3DXMATRIX& world,
    const D3DXMATRIX& view,
    const D3DXMATRIX& proj,
    const D3DXMATRIX& worldView,
    const D3DXMATRIX& worldViewProj,
    CDXUTSDKMesh* mesh,
    Base* shaderInstance )
{
    shaderInstance->SetTarget( DXUTGetD3D11RenderTargetView() );
    shaderInstance->SetDepthStencilView( DXUTGetD3D11DepthStencilView() );

    shaderInstance->SetWorld( Convert( world ) );
    shaderInstance->SetView( Convert( view ) );
    shaderInstance->SetProj( Convert( proj ) );

    shaderInstance->SetTxFalloff( g_pFalloffTexRV );

    // Set cube texture parameter in the effect
    shaderInstance->SetTxEnvMap( g_pEnvMapSRV );

    RenderMeshSpark(
        d3dContext,
        mesh,
        shaderInstance );
}

void RenderSceneEnvMappedSpark(
    ID3D11DeviceContext* d3dContext,
    const D3DXMATRIX& view,
    const D3DXMATRIX& proj )
{
    // Calculate the matrix world*view*proj
    D3DXMATRIX worldView;
    D3DXMATRIX worldViewProj;
    D3DXMatrixMultiply( &worldView, &g_mWorldCar, &view );
    D3DXMatrixMultiply( &worldViewProj, &worldView, &proj );

    if( g_bRenderCar )
    {
        // Render the shiny car shell
        RenderEnvMappedMeshSpark(
            d3dContext,
            g_mWorldCar,
            view,
            proj,
            worldView,
            worldViewProj,
            &g_MeshCar,
            gRenderEnvMappedCarSpark );

        // Render the rest of the car that doesn't need to have a fresnel shader
        RenderEnvMappedMeshSpark(
            d3dContext,
            g_mWorldCar,
            view,
            proj,
            worldView,
            worldViewProj,
            &g_MeshCarInnards,
            gRenderEnvMappedMetalSpark );

        // Finally, render the glass
        RenderEnvMappedMeshSpark(
            d3dContext,
            g_mWorldCar,
            view,
            proj,
            worldView,
            worldViewProj,
            &g_MeshCarGlass,
            gRenderEnvMappedGlassSpark );
    }
    else
    {
        // Just render a shiny ball
        RenderEnvMappedMeshSpark(
            d3dContext,
            g_mWorldCar,
            view,
            proj,
            worldView,
            worldViewProj,
            &g_MeshBall,
            gRenderEnvMappedMetalSpark );
    }
}

void RenderScenePrimary(
    ID3D11DeviceContext* pd3dContext,
    const D3DXMATRIX& mView,
    const D3DXMATRIX& mProj )
{
    // Clear the render target
    ID3D11RenderTargetView* pRTV = DXUTGetD3D11RenderTargetView();
    float ClearColor[4] = { 0.0, 0.0, 0.0, 0.0 };
    pd3dContext->ClearRenderTargetView( pRTV, ClearColor );

    // Clear the depth stencil
    ID3D11DepthStencilView* pDSV = DXUTGetD3D11DepthStencilView();
    pd3dContext->ClearDepthStencilView( pDSV, D3D11_CLEAR_DEPTH, 1.0, 0 );

    //
    // Render room
    //

    if( gUseSpark )
    {
        gRenderSceneSpark->SetTarget( pRTV );
        gRenderSceneSpark->SetDepthStencilView( pDSV );
        RenderSceneCommonSpark(
            pd3dContext,
            mView,
            mProj,
            gRenderSceneSpark );
    }
    else
    {
        RenderSceneCommonHLSL(
            pd3dContext,
            mView,
            mProj,
            g_pRenderSceneTech );
    }

    //
    // Render environment-mapped objects
    //

    if( gUseSpark )
    {
        RenderSceneEnvMappedSpark(
            pd3dContext,
            mView,
            mProj );
    }
    else
    {
        RenderSceneEnvMappedHLSL(
            pd3dContext,
            mView,
            mProj );
    }
}

//--------------------------------------------------------------------------------------
// Render the scene into a cube map
//--------------------------------------------------------------------------------------
HRESULT RenderSceneIntoCubeMap( ID3D11DeviceContext* pd3dContext )
{
    HRESULT hr = S_OK;

    // Save the old RT and DS buffer views
    ID3D11RenderTargetView* apOldRTVs[1] = { NULL };
    ID3D11DepthStencilView* pOldDS = NULL;
    pd3dContext->OMGetRenderTargets( 1, apOldRTVs, &pOldDS );

    // Save the old viewport
    D3D11_VIEWPORT OldVP;
    UINT cRT = 1;
    pd3dContext->RSGetViewports( &cRT, &OldVP );

    // Set a new viewport for rendering to cube map
    D3D11_VIEWPORT SMVP;
    SMVP.Height = ENVMAPSIZE;
    SMVP.Width = ENVMAPSIZE;
    SMVP.MinDepth = 0;
    SMVP.MaxDepth = 1;
    SMVP.TopLeftX = 0;
    SMVP.TopLeftY = 0;
    pd3dContext->RSSetViewports( 1, &SMVP );

    // Here, compute the view matrices used for cube map rendering.
    // First, construct mViewAlignCM, a view matrix with the same
    // orientation as m_mView but with eye point at the car position.
    //
    D3DXMATRIX mViewAlignCM;
    D3DXMatrixIdentity( &mViewAlignCM );
    mViewAlignCM._41 = -g_mWorldCar._41;
    mViewAlignCM._42 = -g_mWorldCar._42;
    mViewAlignCM._43 = -g_mWorldCar._43;

    // Combine with the 6 different view directions to obtain the final
    // view matrices.
    //
    D3DXMATRIX amViewCM[6];
    for( int view = 0; view < 6; ++view )
        D3DXMatrixMultiply( &amViewCM[view], &mViewAlignCM, &g_amCubeMapViewAdjust[view] );

    if( gCubeMapTech != kCubeMapTechnique_Multipass )
    {
        float ClearColor[4] = { 0.0, 1.0, 0.0, 0.0 };

        pd3dContext->ClearRenderTargetView( g_pEnvMapRTV, ClearColor );
        pd3dContext->ClearDepthStencilView( g_pEnvMapDSV, D3D11_CLEAR_DEPTH, 1.0, 0 );

        if( !gUseSpark )
        {
            ID3D11InputLayout* pLayout = nullptr;
            ID3DX11EffectTechnique* pTechnique = nullptr;
            switch( gCubeMapTech )
            {
            case kCubeMapTechnique_GSLoop:
                pLayout = g_pVertexLayoutCM;
                pTechnique = g_pRenderCubeMapTech;
                break;
            case kCubeMapTechnique_IAInstancing:
                pLayout = g_pVertexLayoutCMInst;
                pTechnique = g_pRenderCubeMapIAInstTech;
                break;
            case kCubeMapTechnique_GSInstancing:
                pLayout = g_pVertexLayoutCMGSInst;
                pTechnique = g_pRenderCubeMapGSInstTech;
                break;
            default:
                throw 1;
                break;
            }

            ID3D11RenderTargetView* aRTViews[ 1 ] = { g_pEnvMapRTV };
            pd3dContext->OMSetRenderTargets( sizeof( aRTViews ) / sizeof( aRTViews[0] ), aRTViews, g_pEnvMapDSV );
            pd3dContext->IASetInputLayout( pLayout );
            g_pmViewCM->SetMatrixArray( ( float* )amViewCM, 0, 6 );

            RenderSceneCommonHLSL(
                pd3dContext,
                amViewCM[0],
                g_mProjCM,
                pTechnique );
        }
        else
        {
            Base* shaderInstance = nullptr;
            int instanceCount = 1;
            switch( gCubeMapTech )
            {
            case kCubeMapTechnique_GSLoop:
                shaderInstance = gRenderCubeMapSpark;
                break;
            case kCubeMapTechnique_IAInstancing:
                shaderInstance = gRenderCubeMapInstSpark;
                instanceCount = 6;
                break;
            case kCubeMapTechnique_GSInstancing:
                shaderInstance = gRenderCubeMapGSInstSpark;
                break;
            default:
                throw 1;
                break;
            }

            shaderInstance->SetTarget( g_pEnvMapRTV );
            shaderInstance->SetDepthStencilView( g_pEnvMapDSV );

            spark::float4x4 sparkViewMatrices[6];
            for( int ii = 0; ii < 6; ++ii )
                sparkViewMatrices[ii] = Convert( amViewCM[ii] );
            shaderInstance->SetViewCM( sparkViewMatrices );

            RenderSceneCommonSpark(
                pd3dContext,
                amViewCM[0],
                g_mProjCM,
                shaderInstance,
                instanceCount );
        }
    }
    else
    {
        //
        // Render one cube face at a time
        //

        pd3dContext->IASetInputLayout( g_pVertexLayout );

        for( int view = 0; view < 6; ++view )
        {
            float ClearColor[4] = { 0.0, 0.0, 0.0, 0.0 };
            pd3dContext->ClearRenderTargetView( g_apEnvMapOneRTV[view], ClearColor );
            pd3dContext->ClearDepthStencilView( g_pEnvMapOneDSV, D3D11_CLEAR_DEPTH, 1.0, 0 );

            if( !gUseSpark )
            {
                ID3D11RenderTargetView* aRTViews[ 1 ] = { g_apEnvMapOneRTV[view] };
                pd3dContext->OMSetRenderTargets( sizeof( aRTViews ) / sizeof( aRTViews[0] ), aRTViews, g_pEnvMapOneDSV );

                RenderSceneCommonHLSL(
                    pd3dContext,
                    amViewCM[view],
                    g_mProjCM,
                    g_pRenderSceneTech );
            }
            else
            {
                gRenderSceneSpark->SetTarget( g_apEnvMapOneRTV[view] );
                gRenderSceneSpark->SetDepthStencilView( g_pEnvMapOneDSV );
                RenderSceneCommonSpark(
                    pd3dContext,
                    amViewCM[view],
                    g_mProjCM,
                    gRenderSceneSpark );
            }
        }
    }

    // Restore old view port
    pd3dContext->RSSetViewports( 1, &OldVP );

    // Restore old RT and DS buffer views
    pd3dContext->OMSetRenderTargets( 1, apOldRTVs, pOldDS );

    // Generate Mip Maps
    pd3dContext->GenerateMips( g_pEnvMapSRV );

    SAFE_RELEASE( apOldRTVs[0] );
    SAFE_RELEASE( pOldDS );

    return hr;
}


//--------------------------------------------------------------------------------------
// Render the scene using the D3D11 device
//--------------------------------------------------------------------------------------
void CALLBACK OnD3D11FrameRender(
    ID3D11Device* pd3dDevice,
    ID3D11DeviceContext* pd3dImmediateContext,
    double fTime,
    float fElapsedTime,
    void* pUserContext )
{
    D3DXVECTOR3 vCameraPos = *g_Camera.GetEyePt();
    g_pvEye->SetFloatVector( ( float* )&vCameraPos );

    // Construct the cube map
    if( !g_D3DSettingsDlg.IsActive() )
        RenderSceneIntoCubeMap( pd3dImmediateContext );

    ID3D11ShaderResourceView*const pSRV[4] = { NULL, NULL, NULL, NULL };
    pd3dImmediateContext->PSSetShaderResources( 0, 4, pSRV );

    pd3dImmediateContext->IASetInputLayout( g_pVertexLayout );
    ID3D11RenderTargetView* pRTV = DXUTGetD3D11RenderTargetView();

    float ClearColor[4] = { 0.0, 1.0, 1.0, 0.0 };
    pd3dImmediateContext->ClearRenderTargetView( pRTV, ClearColor );
    ID3D11DepthStencilView* pDSV = DXUTGetD3D11DepthStencilView();
    pd3dImmediateContext->ClearDepthStencilView( pDSV, D3D11_CLEAR_DEPTH, 1.0, 0 );

    // If the settings dialog is being shown, then render it instead of rendering the app's scene
    if( g_D3DSettingsDlg.IsActive() )
    {
        g_D3DSettingsDlg.OnRender( fElapsedTime );
        return;
    }

    D3DXMATRIX mView = *g_Camera.GetViewMatrix();
    D3DXMATRIX mProj = *g_Camera.GetProjMatrix();

    RenderScenePrimary( pd3dImmediateContext, mView, mProj );

    if( gUseSpark )
    {
        // Restore OM state expected by rest of code
        pd3dImmediateContext->OMSetRenderTargets(1, &pRTV, pDSV);
    }

    pd3dImmediateContext->PSSetShaderResources( 0, 4, pSRV );

    DXUT_BeginPerfEvent( DXUT_PERFEVENTCOLOR, L"HUD / Stats" );
    RenderText();
    g_HUD.OnRender( fElapsedTime );
    g_SampleUI.OnRender( fElapsedTime );
    DXUT_EndPerfEvent();
}


//--------------------------------------------------------------------------------------
// Render the help and statistics text
//--------------------------------------------------------------------------------------
void RenderText()
{
    // Output statistics
    g_pTxtHelper->Begin();
    g_pTxtHelper->SetInsertionPos( 5, 5 );
    g_pTxtHelper->SetForegroundColor( D3DXCOLOR( 1.0f, 1.0f, 0.0f, 1.0f ) );
    g_pTxtHelper->DrawTextLine( DXUTGetFrameStats( DXUTIsVsyncEnabled() ) );
    g_pTxtHelper->DrawTextLine( DXUTGetDeviceStats() );
    g_pTxtHelper->End();
}


//--------------------------------------------------------------------------------------
// Release D3D11 resources created in OnD3D11ResizedSwapChain 
//--------------------------------------------------------------------------------------
void CALLBACK OnD3D11SwapChainReleasing( void* pUserContext )
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
    DXUTGetGlobalResourceCache().OnDestroyDevice();
    SAFE_DELETE( g_pTxtHelper )
    SAFE_RELEASE( g_pEffect );
    SAFE_RELEASE( g_pVertexLayout );
    SAFE_RELEASE( g_pVertexLayoutCM );
    SAFE_RELEASE( g_pVertexLayoutCMInst );
    SAFE_RELEASE( g_pVertexLayoutEnv );
    SAFE_RELEASE( g_pEnvMap );
    SAFE_RELEASE( g_pEnvMapRTV );
    for( int i = 0; i < 6; ++i )
    {
        SAFE_RELEASE( g_apEnvMapOneSRV[i] );
        SAFE_RELEASE( g_apEnvMapOneRTV[i] );
    }
    SAFE_RELEASE( g_pEnvMapSRV );
    SAFE_RELEASE( g_pEnvMapDepth );
    SAFE_RELEASE( g_pEnvMapDSV );
    SAFE_RELEASE( g_pEnvMapOneDSV );
    SAFE_RELEASE( g_pVBVisual );

    SAFE_RELEASE( g_pFalloffTexRV );

    g_MeshCar.Destroy();
    g_MeshCarInnards.Destroy();
    g_MeshCarGlass.Destroy();
    g_MeshRoom.Destroy();
    g_MeshMonitors.Destroy();
    g_MeshArm.Destroy();
    g_MeshBall.Destroy();

    SAFE_RELEASE(gRenderCubeMapSpark);
    SAFE_RELEASE(gRenderCubeMapInstSpark);
    SAFE_RELEASE(gRenderCubeMapGSInstSpark);
    SAFE_RELEASE(gRenderSceneSpark);
    SAFE_RELEASE(gRenderEnvMappedCarSpark);
    SAFE_RELEASE(gRenderEnvMappedMetalSpark);
    SAFE_RELEASE(gRenderEnvMappedGlassSpark);
}

