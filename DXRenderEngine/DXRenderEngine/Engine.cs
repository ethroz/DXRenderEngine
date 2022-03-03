using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using System.Windows.Forms;
using System.Drawing;
using Vortice.Mathematics;

/// <summary>
/// ToDo
///     Switch to Vortice / winforms
///     Refactor into inherited classes
/// </summary>

namespace DXRenderEngine;

public class Engine : IDisposable
{
    // Graphics Fields and Properties
    public EngineDescription engineDescription { get; private set; }
    private Form window;
    private IDXGIFactory5 factory;
    private IDXGIAdapter4 adapter;
    private ID3D11Device5 device;
    private ID3D11DeviceContext3 context;
    private IDXGISwapChain4 swapChain;
    private ID3D11RenderTargetView1 renderTargetView;
    private ID3D11DepthStencilView depthStencilView;
    private ID3D11DepthStencilState depthStencilState;
    private ID3D11RenderTargetView1 postLightTargetView;
    private ID3D11ShaderResourceView1 postLightResourceView;
    private ID3D11RenderTargetView1 postTargetView;
    private ID3D11ShaderResourceView1 postResourceView;
    private ID3D11RasterizerState2 lightingRasterizer;
    private ID3D11RasterizerState2 shadowRasterizer;
    private Viewport ScreenViewPort;
    public VertexPositionNormalColor[] Vertices;
    private readonly VertexPositionTexture[] planeVertices = new VertexPositionTexture[]
    {
            new VertexPositionTexture(new Vector3(-1.0f, -1.0f, 0.0f), new Vector2(0.0f, 1.0f)),
            new VertexPositionTexture(new Vector3(-1.0f, 1.0f, 0.0f), new Vector2(0.0f, 0.0f)),
            new VertexPositionTexture(new Vector3(1.0f, -1.0f, 0.0f), new Vector2(1.0f, 1.0f)),
            new VertexPositionTexture(new Vector3(1.0f, 1.0f, 0.0f), new Vector2(1.0f, 0.0f))
    };
    private ID3D11Buffer planeVertexBuffer;
    private BufferDescription constantBD;
    private ID3D11Buffer buffer;
    private BufferDescription projectionBD;
    private ID3D11Buffer projectionBuffer;
    private ID3D11VertexShader vertexShader;
    private ID3D11PixelShader pixelShader;
    private ID3D11PixelShader pixelShader2;
    private ID3D11VertexShader shadowVertexShader;
    private ID3D11PixelShader shadowPixelShader;
    private ID3D11VertexShader planeVertexShader;
    private ID3D11PixelShader shadowBlurPixelShader;
    private ID3D11PixelShader postProcessPixelShader;
    private ID3D11SamplerState depthSampler;
    private ID3D11SamplerState colorSampler;
    private readonly InputElementDescription[] texturedInputElements = new InputElementDescription[]
    {
            new InputElementDescription("POSITION", 0, Format.R32G32B32A32_Float, 0, 0, InputClassification.PerVertexData, 0),
            new InputElementDescription("TEXCOORD", 0, Format.R32G32B32A32_Float, 16, 0, InputClassification.PerVertexData, 0)
    };
    private readonly InputElementDescription[] coloredInputElements = new InputElementDescription[]
    {
            new InputElementDescription("NORMAL", 0, Format.R32G32B32A32_Float, 0, 0, InputClassification.PerVertexData, 0),
            new InputElementDescription("COLOR", 0, Format.R32G32B32A32_Float, 16, 0, InputClassification.PerVertexData, 0),
            new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 32, 0, InputClassification.PerVertexData, 0)
    };
    /*private readonly InputElementDescription[] shadowInputElements = new InputElementDescription[]
    {
        new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0, InputClassification.PerVertexData, 0)
    };*/
    private ID3D11InputLayout inputLayout;
    //private ID3D11InputLayout shadowInputLayout;
    private ID3D11InputLayout planeInputLayout;
    private Texture2DDescription td;
    private QuickBitmap dbmp;
    private ID3D11Texture2D1 texture;
    public MatrixBuffer matrixBuffer;
    private MainBuffer mainBuffer;
    private EmptyBuffer emptyVertexData;
    private EmptyBuffer emptyLightData;
    private EmptyBuffer emptyLightMatrixData;
    private ID3D11Buffer vertexBuffer;
    private ID3D11Buffer lightBuffer;
    private ID3D11Buffer lightMatrixBuffer;
    private int NumberOfTriangles;
    private PrimitiveTopology topology;
    private int DisplayRefreshRate;

    // Controllable Graphics Settings
    public int RefreshRate; // set to 0 for uncapped
    public int Width { private set; get; }
    public int Height { private set; get; }
    public enum RenderType { RasterizedCPU, RasterizedGPU, RayTracedCPU, RayTracedGPU, Custom };
    private RenderType RType;
    public enum WindowState { Minimized = 0, Normal = 1, Maximized = 2, FullScreen = 3, NotSet = 4 };
    private WindowState prevState = WindowState.NotSet;
    public WindowState State;

    // Time Fields
    public double frameTime;
    private long t1, t2;
    private Stopwatch sw;
    private const long millisecondsForReset = 1000;
    private long lastReset = 0;
    public List<double> allFPSList = new List<double>();
    public List<double> allTimeList = new List<double>();
    private double avgFPS = 0.0, minFPS = double.PositiveInfinity, maxFPS = 0.0;
    private double testFPS = double.PositiveInfinity;
    private long frameCount, startFPSTime, updateBufferTime, drawTime, presentTime;

    // Engine Fields
    public bool Running { get; private set; }
    private Action OnAwake, OnStart, OnUpdate;
    internal Action UserInput;
    public const float Deg2Rad = (float)Math.PI / 180.0f;
    public bool Focused;
    public string path;
    public Input input;
    private Vector3 BGCol = new Vector3();
    private float MinBrightness = 0.0f;
    private int RayDepth;
    public List<Gameobject> gameobjects = new List<Gameobject>();
    public List<Sphere> spheres = new List<Sphere>();
    public List<Light> lights = new List<Light>();
    public Vector3 EyePos = new Vector3();
    public Vector2 EyeRot = new Vector2();
    public int filter = 0;

    public Engine(EngineDescription ED)
    {
        engineDescription = ED;
        Width = ED.Width;
        Height = ED.Height;
        RefreshRate = ED.RefreshRate;
        OnAwake = ED.OnAwake;
        OnStart = ED.OnStart;
        OnUpdate = ED.OnUpdate;
        UserInput = ED.UserInput;
        State = ED.WindowState;
        RType = ED.RenderType;
        RayDepth = ED.RayDepth;
        topology = ED.Topology;
        input = new Input(this);
        path = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);

        Running = true;
        sw = new Stopwatch();
        sw.Start();

        Application.EnableVisualStyles();

        window = new Form()
        {
            Text = "DXRenderEngine",
            ClientSize = new Size(Width, Height),
            FormBorderStyle = FormBorderStyle.Fixed3D,
            BackColor = Color.Black
        };
        Application.Run(window);

        InitializeInputs();
        InitializeDeviceResources();

        window.GotFocus += GotFocus;
        window.LostFocus += LostFocus;
        Focused = window.Focused;
        window.FormClosing += Closing;
        window.Resize += Resize;
    }

    private virtual void InitializeDeviceResources()
    {
        DeviceCreationFlags flags = DeviceCreationFlags.None;
#if DEBUG
        flags = DeviceCreationFlags.Debug;
#endif
        D3D11.D3D11CreateDevice(null, DriverType.Hardware, flags, new FeatureLevel[] { FeatureLevel.Level_11_1 }, out ID3D11Device device0);
        device = new ID3D11Device5(device0.NativePointer);
        context = device.ImmediateContext3;
        factory = device.QueryInterface<IDXGIDevice>().GetParent<IDXGIAdapter>().GetParent<IDXGIFactory5>();
        adapter = new IDXGIAdapter4(factory.GetAdapter1(0).NativePointer);

        SwapChainDescription1 scd = new SwapChainDescription1()
        {
            BufferCount = 1,
            Format = Format.R8G8B8A8_UNorm,
            Height = Height,
            Width = Width,
            SampleDescription = new SampleDescription(1, 0),
            SwapEffect = SwapEffect.Discard,
            BufferUsage = Usage.RenderTargetOutput
        };
        swapChain = new IDXGISwapChain4(factory.CreateSwapChainForHwnd(device, window.Handle, scd).NativePointer);

        AssignRenderTarget();

        InitializeShaders();

        InitializeVertices();
        context.IASetVertexBuffer(0, vertexBuffer, Marshal.SizeOf<VertexPositionTexture>());



        dbmp = new QuickBitmap(Width, Height);
        td = new Texture2DDescription
        {
            Width = dbmp.Width,
            Height = dbmp.Height,
            ArraySize = 1,
            BindFlags = BindFlags.ShaderResource,
            Usage = ResourceUsage.Default,
            Format = Format.R8G8B8A8_UNorm,
            MipLevels = 1,
            OptionFlags = ResourceOptionFlags.None,
            SampleDescription = new SampleDescription(1, 0)
        };

        renderTargetView = new RenderTargetView(device1, swapChain1.GetBackBuffer<Texture2D>(0));
        deviceContext1.OutputMerger.SetRenderTargets(renderTargetView);
        ScreenViewPort = new Viewport(0, 0, Width, Height);
        deviceContext1.Rasterizer.SetViewport(ScreenViewPort);
    }

    private void AssignRenderTarget()
    {
        using (ID3D11Texture2D1 backBuffer = swapChain.GetBuffer<ID3D11Texture2D1>(0))
            renderTargetView = device.CreateRenderTargetView1(backBuffer);
        context.RSSetViewport(0, 0, Width, Height);
        context.OMSetRenderTargets(renderTargetView);
    }

    private bool Test()
    {
        print("Done");
        return false;
    }

    public void Run()
    {
        Running = true;
        OnAwake();
        UpdateShaderPolyCount();
        GetMaxDisplayRefreshrate();
        UpdateWindowState();
        OnStart();
        UpdateObjectMatrices();

        if (Test())
        {
            Console.ReadKey();
            Stop();
            renderForm.Close();
            Dispose();
            return;
        }

        Thread t = new Thread(() => input.ControlLoop());
        //Process Proc = Process.GetCurrentProcess();
        //long AffinityMask = (long)Proc.ProcessorAffinity & 0xFFFFFFFFE;
        //print(t.ManagedThreadId);
        //for (int i = 0; i < Proc.Threads.Count; i++)
        //{
        //    if (Proc.Threads[i].ThreadState == System.Diagnostics.ThreadState.Wait)
        //        print(Proc.Threads[i].WaitReason);
        //    if (Proc.Threads[i].Id == t.ManagedThreadId)
        //    {
        //        Proc.Threads[i].ProcessorAffinity = (IntPtr)1;
        //    }
        //    else
        //    {
        //        Proc.Threads[i].ProcessorAffinity = (IntPtr)AffinityMask;
        //    }
        //}
        t.Start();
        t1 = startFPSTime = sw.ElapsedTicks;
        switch (RType)
        {
            case RenderType.RasterizedCPU:
                RenderLoop.Run(renderForm, RasterCPURenderCallBack);
                break;
            case RenderType.RasterizedGPU:
                RenderLoop.Run(renderForm, RasterGPURenderCallBack);
                break;
            case RenderType.RayTracedCPU:
                RenderLoop.Run(renderForm, RayCPURenderCallBack);
                break;
            case RenderType.RayTracedGPU:
                RenderLoop.Run(renderForm, RayGPURenderCallBack);
                break;
            case RenderType.Custom:
                RenderLoop.Run(renderForm, CustomRenderCallBack);
                break;
        }
    }

    private void RasterCPURenderCallBack()
    {
        UpdateWindowState();
        GetTime();
        if (prevState == WindowState.Minimized)
            return;
        OnUpdate();
        CPURasterRender();
        UpdateMainBufferResources();
        DrawRasterCPU();
        if (!Running)
        {
            renderForm.Close();
            Dispose();
        }
    }

    private void RasterGPURenderCallBack()
    {
        UpdateWindowState();
        GetTime();
        if (prevState == WindowState.Minimized)
            return;
        updateBufferTime = sw.ElapsedTicks;
        UpdateRastGPUShaderResources();
        updateBufferTime -= sw.ElapsedTicks;
        drawTime = sw.ElapsedTicks;
        DrawRasterGPU();
        drawTime -= sw.ElapsedTicks;
        OnUpdate();
        Present();
        if (!Running)
        {
            renderForm.Close();
            Dispose();
        }
    }

    private void RayCPURenderCallBack()
    {
        UpdateWindowState();
        GetTime();
        if (prevState == WindowState.Minimized)
            return;
        OnUpdate();
        CPURayRender();
        UpdateMainBufferResources();
        DrawRayCPU();
        if (!Running)
        {
            renderForm.Close();
            Dispose();
        }
    }

    private void RayGPURenderCallBack()
    {
        UpdateWindowState();
        GetTime();
        if (prevState == WindowState.Minimized)
            return;
        OnUpdate();
        GPURayObjectUpdate();
        UpdateRayGPUShaderResources();
        DrawRayGPU();
        if (!Running)
        {
            renderForm.Close();
            Dispose();
        }
    }

    private void CustomRenderCallBack()
    {
        UpdateWindowState();
        GetTime();
        if (prevState == WindowState.Minimized)
            return;
        DrawRasterCPU();
        OnUpdate();
        deviceContext1.UpdateSubresource(ref matrixBuffer, projectionBuffer, 0);
        InitializeVertexBuffer();
        Present();
        if (!Running)
        {
            renderForm.Close();
            Dispose();
        }
    }

    private unsafe void InitializeRasterGPUDeviceResources()
    {
        // device and swapchain init
        SwapChainDescription1 swapChainDesc = new SwapChainDescription1()
        {
            Width = Width,
            Height = Height,
            Format = Format.R8G8B8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage = Usage.RenderTargetOutput,
            BufferCount = 1,
            SwapEffect = SwapEffect.Discard
        };

        D3D11.Device device = new D3D11.Device(DriverType.Hardware, DeviceCreationFlags.None);
        device1 = new D3D11.Device1(device.NativePointer);
        deviceContext1 = device1.ImmediateContext1;
        swapChain1 = new SwapChain1(new Factory2(), device1, renderForm.Handle, ref swapChainDesc);

        // final rendertarget
        renderTargetView = new RenderTargetView(device1, swapChain1.GetBackBuffer<Texture2D>(0));

        // light rendertargets
        Texture2DDescription td = new Texture2DDescription()
        {
            Width = Width,
            Height = Height,
            ArraySize = 1,
            BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget,
            Usage = ResourceUsage.Default,
            Format = Format.R32G32B32A32_Float,
            MipLevels = 0,
            OptionFlags = ResourceOptionFlags.GenerateMipMaps,
            SampleDescription = new SampleDescription(1, 0)
        };
        for (int i = 0; i < lights.Count; i++)
        {
            lights[i].LightTexture = new Texture2D(device1, td);
            lights[i].LightResourceView = new ShaderResourceView(device1, lights[i].LightTexture);
            lights[i].LightTargetView = new RenderTargetView(device1, lights[i].LightTexture);
        }

        // lit scene rendertarget
        td = new Texture2DDescription()
        {
            Width = Width,
            Height = Height,
            ArraySize = 1,
            BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget,
            Usage = ResourceUsage.Default,
            Format = Format.R32G32B32A32_Float,
            MipLevels = 1,
            OptionFlags = ResourceOptionFlags.None,
            SampleDescription = new SampleDescription(1, 0)
        };
        postLightTexture = new Texture2D(device1, td);
        postLightTargetView = new RenderTargetView(device1, postLightTexture);
        postLightResourceView = new ShaderResourceView(device1, postLightTexture);

        // post process rendertarget
        td = new Texture2DDescription()
        {
            Width = Width,
            Height = Height,
            ArraySize = 1,
            BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget,
            Usage = ResourceUsage.Default,
            Format = Format.R32G32B32A32_Float,
            MipLevels = 0,
            OptionFlags = ResourceOptionFlags.GenerateMipMaps,
            SampleDescription = new SampleDescription(1, 0)
        };
        texture = new Texture2D(device1, td);
        postResourceView = new ShaderResourceView(device1, texture);
        postTargetView = new RenderTargetView(device1, texture);

        //depth buffers
        Texture2DDescription t2ddesc = new Texture2DDescription();
        t2ddesc.ArraySize = 1;
        t2ddesc.BindFlags = BindFlags.DepthStencil;
        t2ddesc.CpuAccessFlags = 0;
        t2ddesc.Format = Format.D32_Float;
        t2ddesc.Width = Width;
        t2ddesc.Height = Height;
        t2ddesc.MipLevels = 1;
        t2ddesc.SampleDescription = new SampleDescription(1, 0);
        t2ddesc.Usage = ResourceUsage.Default;
        depthStencilBuffer = new Texture2D(device1, t2ddesc);
        DepthStencilStateDescription dssdesc = new DepthStencilStateDescription();
        dssdesc.IsDepthEnabled = true;
        dssdesc.DepthWriteMask = DepthWriteMask.All;
        dssdesc.DepthComparison = Comparison.Less;
        dssdesc.IsStencilEnabled = false;
        depthStencilState = new DepthStencilState(device1, dssdesc);
        DepthStencilViewDescription dsvdesc = new DepthStencilViewDescription();
        dsvdesc.Format = Format.D32_Float;
        dsvdesc.Dimension = DepthStencilViewDimension.Texture2D;
        dsvdesc.Texture2D.MipSlice = 0;
        depthStencilView = new DepthStencilView(device1, depthStencilBuffer, dsvdesc);
        RasterizerStateDescription rsdesc = new RasterizerStateDescription();
        rsdesc.IsAntialiasedLineEnabled = false;
        rsdesc.CullMode = CullMode.Back;
        rsdesc.DepthBias = 0;
        rsdesc.DepthBiasClamp = 0.0f;
        rsdesc.IsDepthClipEnabled = true;
        switch (topology)
        {
            case PrimitiveTopology.TriangleList:
                rsdesc.FillMode = FillMode.Solid;
                break;
            case PrimitiveTopology.LineList:
                rsdesc.FillMode = FillMode.Wireframe;
                break;
            default:
                throw new NotImplementedException();
        }
        rsdesc.IsFrontCounterClockwise = false;
        rsdesc.IsMultisampleEnabled = false;
        rsdesc.IsScissorEnabled = false;
        rsdesc.SlopeScaledDepthBias = 0.0f;
        lightingRasterizer = new RasterizerState(device1, rsdesc);
        ScreenViewPort = new Viewport(0, 0, Width, Height, 0.0f, 1.0f);

        //shadow buffer
        DepthStencilViewDescription depthStencilViewDesc = new DepthStencilViewDescription();
        depthStencilViewDesc.Format = Format.D24_UNorm_S8_UInt;
        depthStencilViewDesc.Dimension = DepthStencilViewDimension.Texture2D;
        depthStencilViewDesc.Texture2D.MipSlice = 0;
        ShaderResourceViewDescription shaderResourceViewDesc = new ShaderResourceViewDescription();
        shaderResourceViewDesc.Dimension = ShaderResourceViewDimension.Texture2D;
        shaderResourceViewDesc.Format = Format.R24_UNorm_X8_Typeless;
        shaderResourceViewDesc.Texture2D.MipLevels = 1;
        for (int i = 0; i < lights.Count; i++)
        {
            Texture2DDescription shadowMapDesc = new Texture2DDescription();
            shadowMapDesc.Format = Format.R24G8_Typeless;
            shadowMapDesc.MipLevels = 1;
            shadowMapDesc.ArraySize = 1;
            shadowMapDesc.CpuAccessFlags = 0;
            shadowMapDesc.SampleDescription = new SampleDescription(1, 0);
            shadowMapDesc.BindFlags = BindFlags.ShaderResource | BindFlags.DepthStencil;
            shadowMapDesc.Height = lights[i].ShadowRes;
            shadowMapDesc.Width = lights[i].ShadowRes;
            lights[i].ShadowBuffer = new Texture2D(device1, shadowMapDesc);
            lights[i].ShadowStencilView = new DepthStencilView(device1, lights[i].ShadowBuffer, depthStencilViewDesc);
            lights[i].ShadowResourceView = new ShaderResourceView(device1, lights[i].ShadowBuffer, shaderResourceViewDesc);
            lights[i].ShadowViewPort = new Viewport(0, 0, lights[i].ShadowRes, lights[i].ShadowRes, 0.0f, 1.0f);
            lights[i].ShadowProjectionMatrix = Matrix.PerspectiveFovLH((float)Math.PI / 1.1f, 1.0f, lights[i].NearPlane, lights[i].FarPlane);
        }
        rsdesc = new RasterizerStateDescription();
        rsdesc.CullMode = CullMode.None;
        rsdesc.IsDepthClipEnabled = true;
        rsdesc.FillMode = FillMode.Solid;
        shadowRasterizer = new RasterizerState(device1, rsdesc);
        deviceContext1.OutputMerger.SetDepthStencilState(depthStencilState, 0);
    }

    private void GetMaxDisplayRefreshrate()
    {
        using (Factory1 f = new Factory1())
        {
            foreach (var o in f.GetAdapter1(0).Outputs)
            {
                var l = o.GetDisplayModeList(Format.R8G8B8A8_UNorm, DisplayModeEnumerationFlags.DisabledStereo);
                foreach (var m in l)
                    DisplayRefreshRate = Math.Max(DisplayRefreshRate, (int)(m.RefreshRate.Numerator / (float)m.RefreshRate.Denominator));
            }
        }
    }

    private void InitializePlaneBuffer()
    {
        planeVertexBuffer = D3D11.Buffer.Create(device1, BindFlags.VertexBuffer, planeVertices);
    }

    private void InitializeVertexBuffer()
    {
        updateBufferTime = sw.ElapsedTicks;
        if (vertexBuffer != null)
            vertexBuffer.Dispose();
        BufferDescription bd = new BufferDescription(Utilities.SizeOf<VertexPositionNormalColor>() * Vertices.Length, ResourceUsage.Default, BindFlags.VertexBuffer,
            CpuAccessFlags.None, ResourceOptionFlags.None, 0);
        vertexBuffer = D3D11.Buffer.Create(device1, Vertices, bd);
        deviceContext1.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(vertexBuffer, Utilities.SizeOf<VertexPositionNormalColor>(), 0));
        updateBufferTime -= sw.ElapsedTicks;
    }

    public void PackVertexBuffer()
    {
        int vertexCount = 0;
        for (int i = 0; i < gameobjects.Count; i++)
        {
            gameobjects[i].verticesOffset = vertexCount;
            vertexCount += gameobjects[i].triangles.Length * 3;
        }
        Vertices = new VertexPositionNormalColor[vertexCount];
        for (int i = 0; i < gameobjects.Count; i++)
        {
            for (int j = 0; j < gameobjects[i].triangles.Length; j++)
            {
                Vertices[gameobjects[i].verticesOffset + j * 3] = new VertexPositionNormalColor(gameobjects[i].triangles[j].Vertices[0], new Vector4(gameobjects[i].triangles[j].Normals[0], gameobjects[i].triangles[j].Specials[0]), gameobjects[i].triangles[j].Color);
                Vertices[gameobjects[i].verticesOffset + j * 3 + 1] = new VertexPositionNormalColor(gameobjects[i].triangles[j].Vertices[1], new Vector4(gameobjects[i].triangles[j].Normals[1], gameobjects[i].triangles[j].Specials[1]), gameobjects[i].triangles[j].Color);
                Vertices[gameobjects[i].verticesOffset + j * 3 + 2] = new VertexPositionNormalColor(gameobjects[i].triangles[j].Vertices[2], new Vector4(gameobjects[i].triangles[j].Normals[2], gameobjects[i].triangles[j].Specials[2]), gameobjects[i].triangles[j].Color);
            }
        }
        BufferDescription bd = new BufferDescription(Utilities.SizeOf<VertexPositionNormalColor>() * Vertices.Length, ResourceUsage.Default, BindFlags.VertexBuffer,
            CpuAccessFlags.None, ResourceOptionFlags.None, 0);
        vertexBuffer = D3D11.Buffer.Create(device1, Vertices, bd);
        deviceContext1.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(vertexBuffer, Utilities.SizeOf<VertexPositionNormalColor>(), 0));
    }

    private void InitializeShaders()
    {
        switch (RType)
        {
            case RenderType.RasterizedGPU:
                using (var vertexShaderByteCode = ShaderBytecode.CompileFromFile("Shaders.hlsl", "vertexShader", "vs_5_0", ShaderFlags.Debug))
                {
                    inputSignature = ShaderSignature.GetInputSignature(vertexShaderByteCode);
                    vertexShader = new VertexShader(device1, vertexShaderByteCode);
                }
                inputLayout = new InputLayout(device1, inputSignature, coloredInputElements);
                break;
            case RenderType.Custom:
            case RenderType.RasterizedCPU:
                using (var vertexShaderByteCode = ShaderBytecode.CompileFromFile("Shaders.hlsl", "vertexShaderPassthrough", "vs_5_0", ShaderFlags.Debug))
                {
                    inputSignature = ShaderSignature.GetInputSignature(vertexShaderByteCode);
                    vertexShader = new VertexShader(device1, vertexShaderByteCode);
                }
                inputLayout = new InputLayout(device1, inputSignature, coloredInputElements);
                break;
            default:
                using (var vertexShaderByteCode = ShaderBytecode.CompileFromFile("Shaders.hlsl", "planePassthrough", "vs_5_0", ShaderFlags.Debug))
                {
                    inputSignature = ShaderSignature.GetInputSignature(vertexShaderByteCode);
                    vertexShader = new VertexShader(device1, vertexShaderByteCode);
                }
                inputLayout = new InputLayout(device1, inputSignature, texturedInputElements);
                topology = PrimitiveTopology.TriangleStrip;
                break;
        }
        switch (RType)
        {
            case RenderType.RasterizedGPU:
                using (var pixelShaderByteCode = ShaderBytecode.CompileFromFile("Shaders.hlsl", "pixelShader", "ps_5_0", ShaderFlags.Debug))
                {
                    pixelShader = new PixelShader(device1, pixelShaderByteCode);
                }
                break;
            case RenderType.RayTracedGPU:
                using (var pixelShaderByteCode = ShaderBytecode.CompileFromFile("Shaders.hlsl", "rayPixelShader", "ps_5_0", ShaderFlags.Debug))
                {
                    pixelShader = new PixelShader(device1, pixelShaderByteCode);
                }
                break;
            case RenderType.Custom:
                using (var pixelShaderByteCode = ShaderBytecode.CompileFromFile("Shaders.hlsl", "pixelShaderCleanPassthrough", "ps_5_0", ShaderFlags.Debug))
                {
                    pixelShader = new PixelShader(device1, pixelShaderByteCode);
                }
                break;
            case RenderType.RasterizedCPU:
                using (var pixelShaderByteCode = ShaderBytecode.CompileFromFile("Shaders.hlsl", "pixelShaderPassthrough", "ps_5_0", ShaderFlags.Debug))
                {
                    pixelShader = new PixelShader(device1, pixelShaderByteCode);
                }
                break;
            default:
                using (var pixelShaderByteCode = ShaderBytecode.CompileFromFile("Shaders.hlsl", "textureShader", "ps_5_0", ShaderFlags.Debug))
                {
                    pixelShader = new PixelShader(device1, pixelShaderByteCode);
                }
                break;
        }
        using (var vertexShaderByteCode = ShaderBytecode.CompileFromFile("Shaders.hlsl", "planePassthrough", "vs_5_0", ShaderFlags.Debug))
        {
            planeInputSignature = ShaderSignature.GetInputSignature(vertexShaderByteCode);
            planeVertexShader = new VertexShader(device1, vertexShaderByteCode);
        }
        planeInputLayout = new InputLayout(device1, planeInputSignature, texturedInputElements);
        using (var pixelShaderByteCode = ShaderBytecode.CompileFromFile("Shaders.hlsl", "postProcessPixelShader", "ps_5_0", ShaderFlags.Debug))
        {
            postProcessPixelShader = new PixelShader(device1, pixelShaderByteCode);
        }
        using (var pixelShaderByteCode = ShaderBytecode.CompileFromFile("Shaders.hlsl", "shadowBlurPixelShader", "ps_5_0", ShaderFlags.Debug))
        {
            shadowBlurPixelShader = new PixelShader(device1, pixelShaderByteCode);
        }
        using (var vertexShaderByteCode = ShaderBytecode.CompileFromFile("Shaders.hlsl", "shadowVertexShader", "vs_5_0", ShaderFlags.Debug))
        {
            //shadowInputSignature = ShaderSignature.GetInputSignature(vertexShaderByteCode);
            shadowVertexShader = new VertexShader(device1, vertexShaderByteCode);
        }
        using (var pixelShaderByteCode = ShaderBytecode.CompileFromFile("Shaders.hlsl", "shadowPixelShader", "ps_5_0", ShaderFlags.Debug))
        {
            shadowPixelShader = new PixelShader(device1, pixelShaderByteCode);
        }
        using (var pixelShaderByteCode = ShaderBytecode.CompileFromFile("Shaders.hlsl", "pixelShader2", "ps_5_0", ShaderFlags.Debug))
        {
            pixelShader2 = new PixelShader(device1, pixelShaderByteCode);
        }
        deviceContext1.VertexShader.Set(vertexShader);
        deviceContext1.PixelShader.Set(pixelShader);

        var samplerStateDescription = new SamplerStateDescription
        {
            AddressU = TextureAddressMode.Clamp,
            AddressV = TextureAddressMode.Clamp,
            AddressW = TextureAddressMode.Clamp,
            MinimumLod = 0.0f,
            MaximumLod = float.MaxValue,
            MipLodBias = 0.0f,
            Filter = Filter.MinMagMipLinear
        };
        colorSampler = new SamplerState(device1, samplerStateDescription);
        deviceContext1.PixelShader.SetSampler(0, colorSampler);

        SamplerStateDescription comparisonSamplerDesc = new SamplerStateDescription();
        comparisonSamplerDesc.AddressU = TextureAddressMode.Border;
        comparisonSamplerDesc.AddressV = TextureAddressMode.Border;
        comparisonSamplerDesc.AddressW = TextureAddressMode.Border;
        comparisonSamplerDesc.BorderColor = new SharpDX.Mathematics.Interop.RawColor4(1.0f, 1.0f, 1.0f, 1.0f);
        comparisonSamplerDesc.MinimumLod = 0.0f;
        comparisonSamplerDesc.MaximumLod = float.MaxValue;
        comparisonSamplerDesc.MipLodBias = 0.0f;
        comparisonSamplerDesc.MaximumAnisotropy = 0;
        comparisonSamplerDesc.ComparisonFunction = Comparison.LessEqual;
        comparisonSamplerDesc.Filter = Filter.ComparisonMinMagMipPoint;
        depthSampler = new SamplerState(device1, comparisonSamplerDesc);
        deviceContext1.PixelShader.SetSampler(1, depthSampler);
        deviceContext1.InputAssembler.InputLayout = inputLayout;
        if (RType == RenderType.RasterizedGPU)
            deviceContext1.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
        else
            deviceContext1.InputAssembler.PrimitiveTopology = topology;
    }

    private void SetShaderResources()
    {
        projectionBD = new BufferDescription(AssignSize<MatrixBuffer>(), ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, 0, 0);
        matrixBuffer = new MatrixBuffer();
        matrixBuffer.ProjectionMatrix = Matrix.PerspectiveFovLH(engineDescription.ProjectionDesc.FOVVDegrees * Deg2Rad, engineDescription.ProjectionDesc.AspectRatioWH,
            engineDescription.ProjectionDesc.NearPlane, engineDescription.ProjectionDesc.FarPlane);
        projectionBuffer = D3D11.Buffer.Create(device1, ref matrixBuffer, projectionBD);
        deviceContext1.VertexShader.SetConstantBuffer(0, projectionBuffer);
        deviceContext1.PixelShader.SetConstantBuffer(0, projectionBuffer);

        constantBD = new BufferDescription(AssignSize<MainBuffer>(), ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, 0, 0);
        mainBuffer = new MainBuffer();
        buffer = D3D11.Buffer.Create(device1, ref mainBuffer, constantBD);
        deviceContext1.PixelShader.SetConstantBuffer(1, buffer);

        BufferDescription BD = new BufferDescription(emptyVertexData.size * 16, ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, 0, 0);
        vertexBuffer = D3D11.Buffer.Create(device1, ref emptyVertexData, BD);
        deviceContext1.PixelShader.SetConstantBuffer(2, vertexBuffer);

        BD = new BufferDescription(emptyLightData.size * 16, ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, 0, 0);
        lightBuffer = D3D11.Buffer.Create(device1, ref emptyLightData, BD);
        deviceContext1.PixelShader.SetConstantBuffer(3, lightBuffer);

        emptyLightMatrixData = new EmptyBuffer();
        BD = new BufferDescription(lights.Count * 64, ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, 0, 0);
        lightMatrixBuffer = D3D11.Buffer.Create(device1, ref emptyLightMatrixData, BD);
        deviceContext1.PixelShader.SetConstantBuffer(4, lightMatrixBuffer);
    }

    private unsafe void UpdateRayGPUShaderResources()
    {
        //buffer data
        Matrix3x3 rotx = Matrix3x3.RotationX(EyeRot.X * Deg2Rad);
        Matrix3x3 roty = Matrix3x3.RotationY(EyeRot.Y * Deg2Rad);
        Matrix3x3 rot = rotx * roty;
        mainBuffer = new MainBuffer(rot, EyePos, BGCol, Width, Height, MinBrightness, sw.ElapsedTicks % 60L, RayDepth, NumberOfTriangles, spheres.Count, lights.Count);
        deviceContext1.UpdateSubresource(ref mainBuffer, buffer, 0);

        //vertex data
        Vector4[] data = new Vector4[emptyVertexData.size];
        int counter = 0;
        for (int i = 0; i < gameobjects.Count; i++)
        {
            for (int j = 0; j < gameobjects[i].triangles.Length; j++)
            {
                data[counter * 3] = new Vector4(gameobjects[i].projectedTriangles[j].Vertices[0], 0.0f);
                data[counter * 3 + 1] = new Vector4(gameobjects[i].projectedTriangles[j].Vertices[1], 0.0f);
                data[counter * 3 + 2] = new Vector4(gameobjects[i].projectedTriangles[j].Vertices[2], 0.0f);
                data[NumberOfTriangles * 3 + counter * 3] = new Vector4(gameobjects[i].projectedTriangles[j].Normals[0], gameobjects[i].projectedTriangles[j].Specials[0]);
                data[NumberOfTriangles * 3 + counter * 3 + 1] = new Vector4(gameobjects[i].projectedTriangles[j].Normals[1], gameobjects[i].projectedTriangles[j].Specials[1]);
                data[NumberOfTriangles * 3 + counter * 3 + 2] = new Vector4(gameobjects[i].projectedTriangles[j].Normals[2], gameobjects[i].projectedTriangles[j].Specials[2]);
                data[NumberOfTriangles * 6 + counter] = gameobjects[i].projectedTriangles[j].Color;
                counter++;
            }
        }
        for (int i = 0; i < spheres.Count; i++)
        {
            data[(NumberOfTriangles * 7) + (i * 3)] = new Vector4(spheres[i].position, spheres[i].radius);
            data[(NumberOfTriangles * 7) + (i * 3) + 1] = spheres[i].color;
            data[(NumberOfTriangles * 7) + (i * 3) + 2] = spheres[i].Data;
        }
        fixed (Vector4* fp = &data[0])
            deviceContext1.UpdateSubresourceSafe(new DataBox((IntPtr)fp), vertexBuffer, 16, 0);

        //light data
        data = new Vector4[emptyLightData.size];
        for (int i = 0; i < lights.Count; i++)
        {
            data[(i * 3)] = new Vector4(lights[i].Position, 0.0f);
            data[(i * 3) + 1] = lights[i].Color;
            data[(i * 3) + 2] = new Vector4(lights[i].Radius, lights[i].Luminosity, 0.0f, 0.0f);
        }
        fixed (Vector4* fp = &data[0])
            deviceContext1.UpdateSubresourceSafe(new DataBox((IntPtr)fp), lightBuffer, 16, 0);
    }

    private unsafe void UpdateRastGPUShaderResources()
    {
        UpdateMainBufferResources();

        // light data
        Vector4[] data = new Vector4[emptyLightData.size];
        for (int i = 0; i < lights.Count; i++)
        {
            data[(i * 3)] = new Vector4(lights[i].Position, 0.0f);
            data[(i * 3) + 1] = lights[i].Color;
            data[(i * 3) + 2] = new Vector4(lights[i].Radius, lights[i].Luminosity, 10.0f / lights[i].ShadowRes * lights[i].NearPlane, 0.0f);
        }
        fixed (Vector4* fp = &data[0])
            deviceContext1.UpdateSubresourceSafe(new DataBox((IntPtr)fp), lightBuffer, 16, 0);
    }

    private void UpdateMainBufferResources()
    {
        mainBuffer = new MainBuffer(new Matrix3x3(), EyePos, BGCol, Width, Height, MinBrightness, sw.ElapsedTicks % 60L, RayDepth, NumberOfTriangles, spheres.Count, lights.Count);
        deviceContext1.UpdateSubresource(ref mainBuffer, buffer, 0);
    }

    public void UpdateObjectMatrices()
    {
        for (int i = 0; i < gameobjects.Count; i++)
        {
            Matrix scale = Matrix.Scaling(gameobjects[i].scale);
            Matrix rotation = Matrix.RotationZ(gameobjects[i].rotation.Z * Deg2Rad) * Matrix.RotationY(gameobjects[i].rotation.Y * Deg2Rad) * Matrix.RotationX(gameobjects[i].rotation.X * Deg2Rad);
            Matrix translation = Matrix.Translation(gameobjects[i].position);
            gameobjects[i].world = scale * rotation * translation;
        }
    }

    private int AssignSize<T>() where T : struct
    {
        int size = Utilities.SizeOf<T>();
        return size + (16 - (size % 16));
    }

    private void GetTime()
    {
        t2 = sw.ElapsedTicks;
        frameTime = (t2 - t1) / 10000000.0;
        if (RefreshRate != 0)
        {
            double elapsed = 1.0 / RefreshRate - frameTime;
            if (elapsed > 0.001)
                Thread.Sleep((int)(1000 * elapsed));
            t2 = sw.ElapsedTicks;
            frameTime = (t2 - t1) / 10000000.0;
            while (1.0 / (frameTime) > RefreshRate)
            {
                t2 = sw.ElapsedTicks;
                frameTime = (t2 - t1) / 10000000.0;
            }
        }
        t1 = t2;
        double fps = 1.0 / frameTime;
        double loopFPS = 10000000.0 / (-updateBufferTime - drawTime - presentTime);
        if (fps < testFPS)
        {
            sw.Stop();
            testFPS = fps;
            sw.Start();
        }
        maxFPS = Math.Max(fps, maxFPS);
        minFPS = Math.Min(fps, minFPS);
        frameCount++;
        allFPSList.Add(fps);
        allTimeList.Add(sw.ElapsedTicks / 10000000.0);

        // reset counters
        if (sw.ElapsedMilliseconds / millisecondsForReset > lastReset)
        {
            avgFPS = 10000000.0 * frameCount / (sw.ElapsedTicks - startFPSTime);
            startFPSTime = sw.ElapsedTicks;
            renderForm.Text = "DXRenderEngine   " + avgFPS.ToString("G4") + "fps (" + minFPS.ToString("G4") + "fps, " + maxFPS.ToString("G4") + "fps)";
            maxFPS = 0.0;
            minFPS = testFPS = double.PositiveInfinity;
            frameCount = 0;
            lastReset = sw.ElapsedMilliseconds / millisecondsForReset;
        }
    }

    public void Stop()
    {
        if (!Running)
            return;
        Running = false;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool boolean)
    {
        Running = false;
        input.mouse.Dispose();
        input.keyboard.Dispose();
        DisposeGraphics();
        renderForm.Dispose();
    }

    private void DisposeGraphics()
    {
        device1.Dispose();
        deviceContext1.Dispose();
        swapChain1.Dispose();
        renderTargetView.Dispose();
        if (depthStencilView != null)
        {
            depthStencilView.Dispose();
            depthStencilBuffer.Dispose();
            depthStencilState.Dispose();
            for (int i = 0; i < lights.Count; i++)
            {
                lights[i].ShadowBuffer.Dispose();
                lights[i].ShadowStencilView.Dispose();
                lights[i].ShadowResourceView.Dispose();
            }
        }
        planeVertexBuffer.Dispose();
        buffer.Dispose();
        vertexBuffer.Dispose();
        projectionBuffer.Dispose();
        vertexShader.Dispose();
        pixelShader.Dispose();
        if (shadowVertexShader != null)
        {
            shadowVertexShader.Dispose();
            shadowPixelShader.Dispose();
        }
        inputLayout.Dispose();
        inputSignature.Dispose();
        dbmp.Dispose();
        if (texture != null)
            texture.Dispose();
        lightBuffer.Dispose();
    }

    /////////////////////////////////////

    private void DrawRasterCPU()
    {
        deviceContext1.ClearRenderTargetView(renderTargetView, Color.Black);
        //deviceContext1.UpdateSubresource(ref matrixBuffer, projectionBuffer, 0);
        //if (Vertices.Length * Utilities.SizeOf<VertexPositionNormalColor>() != vertexBuffer.Description.SizeInBytes)
        //    InitializeVertexBuffer();
        //else
        //    updateBufferTime = 0;
        drawTime = sw.ElapsedTicks;
        deviceContext1.Draw(Vertices.Length, 0);
        drawTime -= sw.ElapsedTicks;
    }

    private void DrawRasterGPU()
    {
        //deviceContext1.SwapDeviceContextState();

        // shadow rasterization
        deviceContext1.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(vertexBuffer, Utilities.SizeOf<VertexPositionNormalColor>(), 0));
        deviceContext1.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
        deviceContext1.InputAssembler.InputLayout = inputLayout;
        deviceContext1.Rasterizer.State = shadowRasterizer;
        deviceContext1.VertexShader.Set(shadowVertexShader);
        deviceContext1.PixelShader.Set(shadowPixelShader);
        for (int i = 0; i < lights.Count; i++)
        {
            deviceContext1.ClearDepthStencilView(lights[i].ShadowStencilView, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1.0f, 0);
            deviceContext1.OutputMerger.SetRenderTargets(lights[i].ShadowStencilView);
            deviceContext1.Rasterizer.SetViewport(lights[i].ShadowViewPort);
            lights[i].ShadowViewMatrix = Matrix.LookAtLH(lights[i].Position, lights[i].Position + Vector3.Down, Vector3.ForwardLH);
            Matrix lightMat = lights[i].ShadowViewMatrix * lights[i].ShadowProjectionMatrix;

            for (int j = 0; j < gameobjects.Count; j++)
            {
                matrixBuffer.LightProjectionMatrix = gameobjects[j].world * lightMat;
                deviceContext1.UpdateSubresource(ref matrixBuffer, projectionBuffer, 0);
                deviceContext1.Draw(gameobjects[j].triangles.Length * 3, gameobjects[j].verticesOffset);
            }
        }

        // lighting shader pass
        deviceContext1.Rasterizer.SetViewport(ScreenViewPort);
        deviceContext1.Rasterizer.State = lightingRasterizer;
        deviceContext1.VertexShader.Set(vertexShader);
        deviceContext1.PixelShader.Set(pixelShader);
        Matrix cameraRot = Matrix.RotationX(EyeRot.X * Deg2Rad) * Matrix.RotationY(EyeRot.Y * Deg2Rad);
        Vector3 vForwards = EyePos + Vector3.TransformNormal(Vector3.ForwardLH, cameraRot);
        Vector3 vUpwards = Vector3.TransformNormal(Vector3.Up, cameraRot);
        matrixBuffer.ViewMatrix = Matrix.LookAtLH(EyePos, vForwards, vUpwards);
        for (int i = 0; i < lights.Count; i++)
        {
            deviceContext1.ClearRenderTargetView(lights[i].LightTargetView, Color.Black);
            deviceContext1.ClearDepthStencilView(depthStencilView, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1.0f, 0);
            deviceContext1.OutputMerger.SetRenderTargets(depthStencilView, lights[i].LightTargetView);
            Matrix lightMat = lights[i].ShadowViewMatrix * lights[i].ShadowProjectionMatrix;
            for (int j = 0; j < gameobjects.Count; j++)
            {
                matrixBuffer.WorldMatrix = gameobjects[j].world;
                matrixBuffer.NormalMatrix = Matrix.Transpose(Matrix.Invert(gameobjects[j].world));
                matrixBuffer.LightProjectionMatrix = gameobjects[j].world * lightMat;
                matrixBuffer.LightIndex = i;
                deviceContext1.UpdateSubresource(ref matrixBuffer, projectionBuffer, 0);

                if (i != 0)
                    deviceContext1.PixelShader.SetShaderResources(0, lights[i - 1].LightResourceView);
                deviceContext1.PixelShader.SetShaderResources(1, lights[i].ShadowResourceView);

                deviceContext1.Draw(gameobjects[j].triangles.Length * 3, gameobjects[j].verticesOffset);
            }
        }

        // shadow blurring
        deviceContext1.GenerateMips(lights[lights.Count - 1].LightResourceView);
        deviceContext1.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleStrip;
        deviceContext1.OutputMerger.SetRenderTargets(postLightTargetView);
        deviceContext1.InputAssembler.InputLayout = planeInputLayout;
        deviceContext1.VertexShader.Set(planeVertexShader);
        deviceContext1.PixelShader.Set(shadowBlurPixelShader);
        deviceContext1.PixelShader.SetShaderResources(0, lights[lights.Count - 1].LightResourceView);
        deviceContext1.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(planeVertexBuffer, Utilities.SizeOf<VertexPositionTexture>(), 0));
        deviceContext1.Draw(planeVertices.Length, 0);

        // color calculation
        deviceContext1.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(vertexBuffer, Utilities.SizeOf<VertexPositionNormalColor>(), 0));
        deviceContext1.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
        deviceContext1.InputAssembler.InputLayout = inputLayout;
        deviceContext1.ClearRenderTargetView(postTargetView, new Color4(0.0f, 0.0f, 0.0f, float.PositiveInfinity));
        deviceContext1.ClearDepthStencilView(depthStencilView, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1.0f, 0);
        deviceContext1.OutputMerger.SetRenderTargets(depthStencilView, postTargetView);
        deviceContext1.VertexShader.Set(vertexShader);
        deviceContext1.PixelShader.Set(pixelShader2);
        deviceContext1.PixelShader.SetShaderResources(0, postLightResourceView);
        for (int i = 0; i < gameobjects.Count; i++)
        {
            matrixBuffer.WorldMatrix = gameobjects[i].world;
            matrixBuffer.NormalMatrix = Matrix.Transpose(Matrix.Invert(gameobjects[i].world));
            deviceContext1.UpdateSubresource(ref matrixBuffer, projectionBuffer, 0);

            deviceContext1.Draw(gameobjects[i].triangles.Length * 3, gameobjects[i].verticesOffset);
        }

        // post processing
        deviceContext1.Rasterizer.SetViewport(ScreenViewPort);
        deviceContext1.GenerateMips(postResourceView);
        deviceContext1.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleStrip;
        deviceContext1.OutputMerger.SetRenderTargets(renderTargetView);
        deviceContext1.InputAssembler.InputLayout = planeInputLayout;
        deviceContext1.VertexShader.Set(planeVertexShader);
        deviceContext1.PixelShader.Set(postProcessPixelShader);
        deviceContext1.PixelShader.SetShaderResource(0, postResourceView);

        UpdateMainBufferResources();
        matrixBuffer.LightIndex = filter;
        deviceContext1.UpdateSubresource(ref matrixBuffer, projectionBuffer, 0);

        deviceContext1.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(planeVertexBuffer, Utilities.SizeOf<VertexPositionTexture>(), 0));
        deviceContext1.Draw(planeVertices.Length, 0);
    }

    private void DrawRayCPU()
    {
        texture = new Texture2D(device1, td, new DataRectangle(dbmp.BitsHandle.AddrOfPinnedObject(), Width * 4));
        ShaderResourceView textureView = new ShaderResourceView(device1, texture);
        deviceContext1.PixelShader.SetShaderResource(0, textureView);
        texture.Dispose();
        textureView.Dispose();

        deviceContext1.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(planeVertexBuffer, Utilities.SizeOf<VertexPositionTexture>(), 0));
        deviceContext1.Draw(planeVertices.Length, 0);

        swapChain1.Present(0, PresentFlags.None);
    }

    private void DrawRayGPU()
    {
        deviceContext1.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(planeVertexBuffer, Utilities.SizeOf<VertexPositionTexture>(), 0));
        deviceContext1.Draw(planeVertices.Length, 0);

        swapChain1.Present(0, PresentFlags.None);
    }

    private void Present()
    {
        presentTime = sw.ElapsedTicks;
        swapChain1.Present(0, PresentFlags.None, new PresentParameters());
        presentTime -= sw.ElapsedTicks;
    }

    private void CPURasterRender()
    {
        List<TriNormsCol> trisToRaster = new List<TriNormsCol>();
        List<VertexPositionNormalColor> vertexp = new List<VertexPositionNormalColor>();

        //projection matrices
        Vector3 vForwards = new Vector3(0.0f, 0.0f, 1.0f);
        Vector3 vUpwards = new Vector3(0.0f, 1.0f, 0.0f);
        Matrix cameraRot = Matrix.RotationX(EyeRot.X * Deg2Rad) * Matrix.RotationY(EyeRot.Y * Deg2Rad);
        vForwards = Vector3.TransformNormal(vForwards, cameraRot);
        vUpwards = Vector3.TransformNormal(vUpwards, cameraRot);
        vForwards += EyePos;
        Matrix matView = Matrix.LookAtLH(EyePos, vForwards, vUpwards);

        //vertex shader
        for (int i = 0; i < gameobjects.Count; i++)
        {
            Matrix objTrans = Matrix.Translation(gameobjects[i].position);
            Matrix objRot = Matrix.RotationZ(-gameobjects[i].rotation.Z * Deg2Rad) * Matrix.RotationX(gameobjects[i].rotation.X * Deg2Rad) * Matrix.RotationY(gameobjects[i].rotation.Y * Deg2Rad);
            Matrix objScale = Matrix.Scaling(gameobjects[i].scale);
            trisToRaster.AddRange(Render3DObject(gameobjects[i], matrixBuffer.ProjectionMatrix, matView, objTrans, objRot, objScale));
        }

        // rasterize
        QuickSort(ref trisToRaster, 0, trisToRaster.Count - 1);

        if (topology == PrimitiveTopology.TriangleList)
        {
            foreach (TriNormsCol t in trisToRaster)
            {
                for (int k = 0; k < 3; k++)
                {
                    vertexp.Add(new VertexPositionNormalColor(new Vector3(t.Vertices[k].X, t.Vertices[k].Y, 0.0f), new Vector4(), t.Color));
                }
            }
        }
        else if (topology == PrimitiveTopology.LineList)
        {
            foreach (TriNormsCol t in trisToRaster)
            {
                for (int k = 0; k < 3; k++)
                {
                    vertexp.Add(new VertexPositionNormalColor(new Vector3(t.Vertices[k].X, t.Vertices[k].Y, 0.0f), new Vector4(), t.Color));
                    vertexp.Add(new VertexPositionNormalColor(new Vector3(t.Vertices[(k + 1) % 3].X, t.Vertices[(k + 1) % 3].Y, 0.0f), new Vector4(), t.Color));
                }
            }
        }
        Vertices = vertexp.ToArray();
    }

    private void CPURayRender()
    {
        // vertex shader
        for (int i = 0; i < gameobjects.Count; i++)
        {
            gameobjects[i].projectedTriangles = new TriNormsCol[gameobjects[i].triangles.Length];
            for (int j = 0; j < gameobjects[i].triangles.Length; j++)
            {
                gameobjects[i].projectedTriangles[j].Vertices = new Vector3[3];
                gameobjects[i].projectedTriangles[j].Normals = new Vector3[3];
                gameobjects[i].projectedTriangles[j].Specials = new float[3];
                for (int k = 0; k < 3; k++)
                {
                    gameobjects[i].projectedTriangles[j].Vertices[k] = gameobjects[i].triangles[j].Vertices[k] + gameobjects[i].position;
                    gameobjects[i].projectedTriangles[j].Normals[k] = gameobjects[i].triangles[j].Normals[k];
                    gameobjects[i].projectedTriangles[j].Specials[k] = gameobjects[i].triangles[j].Specials[k];
                }
                gameobjects[i].projectedTriangles[j].Color = gameobjects[i].triangles[j].Color;
            }
        }

        //pixel shader
        Matrix rotx = Matrix.RotationX(EyeRot.X * Deg2Rad);
        Matrix roty = Matrix.RotationY(EyeRot.Y * Deg2Rad);
        Matrix rot = rotx * roty;
        //void row(int y)
        //{
        //    float pitch = (y * 2.0f / Height - 1.0f) * (Height / (float)Width) * 0.1f;
        //    for (int x = 0; x < Width; x++)
        //    {
        //        float yaw = (x * 2.0f / Width - 1.0f) * 0.1f;
        //        Vector3 direction = new Vector3(yaw, pitch, 0.1f);
        //        direction = Vector3.TransformNormal(direction, rot);
        //        Ray ray = new Ray(direction + EyePos, Vector3.Normalize(direction));
        //        List<Color> colors = new List<Color>();
        //        float[] impact = new float[RayDepth];
        //        float[] distances = new float[RayDepth];
        //        for (int i = 0; i < RayDepth; i++)
        //        {
        //            colors.Add(RayCast(ref ray, i, ref distances, out impact[i]));
        //            if (distances[i] == float.PositiveInfinity)
        //                break;
        //        }
        //        int r = 0, g = 0, b = 0;
        //        for (int i = 0; i < colors.Count; i++)
        //        {
        //            r += (int)(colors[i].R * impact[i]);
        //            g += (int)(colors[i].G * impact[i]);
        //            b += (int)(colors[i].B * impact[i]);
        //        }
        //        r = Math.Min(255, r / colors.Count);
        //        g = Math.Min(255, g / colors.Count);
        //        b = Math.Min(255, b / colors.Count);
        //        Color col = new Color(r, g, b);
        //        dbmp.SetPixel(x, y, col);
        //    }
        //};
        //Task[] tasks = new Task[Height];
        //int Y = 0;
        //while (true)
        //{
        //    tasks[Y] = Task.Run(() => row(Y));
        //    if (Y == Height - 1)
        //        break;
        //    Y++;
        //}
        //Task.WhenAll(tasks);
        //var tasks = Enumerable.Range(0, Height).Select(delegate(int y)
        //{
        //    print("ran");
        //    float pitch = (y * 2.0f / Height - 1.0f) * (Height / (float)Width) * 0.1f;
        //    for (int x = 0; x < Width; x++)
        //    {
        //        float yaw = (x * 2.0f / Width - 1.0f) * 0.1f;
        //        Vector3 direction = new Vector3(yaw, pitch, 0.1f);
        //        direction = Vector3.TransformNormal(direction, rot);
        //        Ray ray = new Ray(direction + EyePos, Vector3.Normalize(direction));
        //        List<Color> colors = new List<Color>();
        //        float[] impact = new float[RayDepth];
        //        float[] distances = new float[RayDepth];
        //        for (int i = 0; i < RayDepth; i++)
        //        {
        //            colors.Add(RayCast(ref ray, i, ref distances, out impact[i]));
        //            if (distances[i] == float.PositiveInfinity)
        //                break;
        //        }
        //        int r = 0, g = 0, b = 0;
        //        for (int i = 0; i < colors.Count; i++)
        //        {
        //            r += (int)(colors[i].R * impact[i]);
        //            g += (int)(colors[i].G * impact[i]);
        //            b += (int)(colors[i].B * impact[i]);
        //        }
        //        r = Math.Min(255, r / colors.Count);
        //        g = Math.Min(255, g / colors.Count);
        //        b = Math.Min(255, b / colors.Count);
        //        Color col = new Color(r, g, b);
        //        dbmp.SetPixel(x, y, col);
        //    }
        //    return 0;
        //});
        //foreach (Task t in tasks)
        //    t.RunSynchronously();
        //var query = Enumerable.Range(0, Height).AsParallel(); 
        //query.ForAll(y =>
        //{
        //    float pitch = (y * 2.0f / Height - 1.0f) * (Height / (float)Width) * 0.1f;
        //    for (int x = 0; x < Width; x++)
        //    {
        //        float yaw = (x * 2.0f / Width - 1.0f) * 0.1f;
        //        Vector3 direction = new Vector3(yaw, pitch, 0.1f);
        //        direction = Vector3.TransformNormal(direction, rot);
        //        Ray ray = new Ray(direction + EyePos, Vector3.Normalize(direction));
        //        List<Color> colors = new List<Color>();
        //        float[] impact = new float[RayDepth];
        //        float[] distances = new float[RayDepth];
        //        for (int i = 0; i < RayDepth; i++)
        //        {
        //            colors.Add(RayCast(ref ray, i, ref distances, out impact[i]));
        //            if (distances[i] == float.PositiveInfinity)
        //                break;
        //        }
        //        int r = 0, g = 0, b = 0;
        //        for (int i = 0; i < colors.Count; i++)
        //        {
        //            r += (int)(colors[i].R * impact[i]);
        //            g += (int)(colors[i].G * impact[i]);
        //            b += (int)(colors[i].B * impact[i]);
        //        }
        //        r = Math.Min(255, r / colors.Count);
        //        g = Math.Min(255, g / colors.Count);
        //        b = Math.Min(255, b / colors.Count);
        //        Color col = new Color(r, g, b);
        //        dbmp.SetPixel(x, y, col);
        //    }
        //});
        Parallel.For(0, Height, y =>
        {
            float pitch = (y * -2.0f / Height + 1.0f) * (Height / (float)Width) * 0.1f;
            for (int x = 0; x < Width; x++)
            {
                float yaw = (x * 2.0f / Width - 1.0f) * 0.1f;
                Vector3 direction = new Vector3(yaw, pitch, 0.1f);
                direction = Vector3.TransformNormal(direction, rot);
                Ray ray = new Ray(direction + EyePos, Vector3.Normalize(direction));
                List<Color> colors = new List<Color>();
                float[] impact = new float[RayDepth];
                float[] distances = new float[RayDepth];
                for (int i = 0; i < RayDepth; i++)
                {
                    colors.Add(RayCast(ref ray, i, ref distances, out impact[i]));
                    if (distances[i] == float.PositiveInfinity)
                        break;
                }
                int r = 0, g = 0, b = 0;
                for (int i = 0; i < colors.Count; i++)
                {
                    r += (int)(colors[i].R * impact[i]);
                    g += (int)(colors[i].G * impact[i]);
                    b += (int)(colors[i].B * impact[i]);
                }
                r = Math.Min(255, r / colors.Count);
                g = Math.Min(255, g / colors.Count);
                b = Math.Min(255, b / colors.Count);
                Color col = new Color(r, g, b);
                dbmp.SetPixel(x, y, col);
            }
        });
    }

    private void GPURayObjectUpdate()
    {
        for (int i = 0; i < gameobjects.Count; i++)
        {
            gameobjects[i].projectedTriangles = new TriNormsCol[gameobjects[i].triangles.Length];
            for (int j = 0; j < gameobjects[i].triangles.Length; j++)
            {
                gameobjects[i].projectedTriangles[j].Vertices = new Vector3[3];
                gameobjects[i].projectedTriangles[j].Normals = new Vector3[3];
                gameobjects[i].projectedTriangles[j].Specials = new float[3];
                for (int k = 0; k < 3; k++)
                {
                    gameobjects[i].projectedTriangles[j].Vertices[k] = gameobjects[i].triangles[j].Vertices[k] + gameobjects[i].position;
                    gameobjects[i].projectedTriangles[j].Normals[k] = gameobjects[i].triangles[j].Normals[k];
                    gameobjects[i].projectedTriangles[j].Specials[k] = gameobjects[i].triangles[j].Specials[k];
                }
                gameobjects[i].projectedTriangles[j].Color = gameobjects[i].triangles[j].Color;
            }
        }
    }

    /////////////////////////////////////

    private TriNormsCol[] Render3DObject(Gameobject obj, Matrix proj, Matrix view, Matrix trans, Matrix rot, Matrix scale)
    {
        List<TriNormsCol> trisToRaster = new List<TriNormsCol>();
        for (int i = 0; i < obj.triangles.Length; i++)
        {
            TriNormsCol triProjected = new TriNormsCol(0), triTransformed = new TriNormsCol(0), triViewed = new TriNormsCol(0);

            for (int j = 0; j < 3; j++)
            {
                triTransformed.Vertices[j] = Vector3.TransformCoordinate(obj.triangles[i].Vertices[j], scale);
                triTransformed.Vertices[j] = Vector3.TransformCoordinate(triTransformed.Vertices[j], rot);
                triTransformed.Vertices[j] = Vector3.TransformCoordinate(triTransformed.Vertices[j], trans);
            }

            Vector3 line1 = triTransformed.Vertices[1] - triTransformed.Vertices[0],
                  line2 = triTransformed.Vertices[2] - triTransformed.Vertices[0],
                  normal = Vector3.Cross(line1, line2);
            normal = Gameobject.Normalize(normal);

            Vector3 vCameraRay = triTransformed.Vertices[0] - EyePos;

            if (Vector3.Dot(normal, vCameraRay) < 0.0f)
            {
                for (int j = 0; j < 3; j++)
                {
                    Vector3 ray = lights[0].Position - triTransformed.Vertices[j];
                    float dp = Vector3.Dot(normal, Vector3.Normalize(ray));
                    float distanceSquared = ray.LengthSquared();
                    float brightness = lights[0].Luminosity / (float)(4.0f * Math.PI * distanceSquared);
                    float shade = Math.Min(Math.Max(MinBrightness, brightness * dp), 1.0f);

                    ///////////////////////

                    triViewed.Vertices[j] = Vector3.TransformCoordinate(triTransformed.Vertices[j], view);
                }
                triViewed.Color = obj.triangles[i].Color;

                TriNormsCol[] clipped = new TriNormsCol[2];
                int nClippedTriangles = TriangleClipAgainstPlane(new Vector3(0.0f, 0.0f, 0.1f), new Vector3(0.0f, 0.0f, 1.0f), ref triViewed, out clipped[0], out clipped[1]);

                for (int j = 0; j < nClippedTriangles; j++)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        Vector4 temp = Vector4.Transform(new Vector4(clipped[j].Vertices[k], 1.0f), proj);
                        triProjected.Vertices[k] = new Vector3(temp.X, temp.Y, temp.Z);
                        triProjected.Vertices[k] /= temp.W;
                    }
                    triProjected.Color = clipped[j].Color;

                    TriNormsCol[] edged = new TriNormsCol[2];
                    List<TriNormsCol> listTriangles = new List<TriNormsCol>
                        {
                            triProjected
                        };
                    int nNewTriangles = 1;

                    for (int p = 0; p < 4; p++)
                    {
                        while (nNewTriangles > 0)
                        {
                            int nTrisToAdd;
                            TriNormsCol test = listTriangles[0];
                            listTriangles.RemoveAt(0);
                            nNewTriangles--;

                            switch (p)
                            {
                                case 0:
                                    nTrisToAdd = TriangleClipAgainstPlane(new Vector3(0.0f, -1.0f, 0.0f), new Vector3(0.0f, 1.0f, 0.0f), ref test, out edged[0], out edged[1]);
                                    break;
                                case 1:
                                    nTrisToAdd = TriangleClipAgainstPlane(new Vector3(0.0f, 1.0f, 0.0f), new Vector3(0.0f, -1.0f, 0.0f), ref test, out edged[0], out edged[1]);
                                    break;
                                case 2:
                                    nTrisToAdd = TriangleClipAgainstPlane(new Vector3(-1.0f, 0.0f, 0.0f), new Vector3(1.0f, 0.0f, 0.0f), ref test, out edged[0], out edged[1]);
                                    break;
                                default:
                                    nTrisToAdd = TriangleClipAgainstPlane(new Vector3(1.0f, 0.0f, 0.0f), new Vector3(-1.0f, 0.0f, 0.0f), ref test, out edged[0], out edged[1]);
                                    break;
                            }

                            for (int w = 0; w < nTrisToAdd; w++)
                                listTriangles.Add(new TriNormsCol(new Vector3[] { edged[w].Vertices[0], edged[w].Vertices[1], edged[w].Vertices[2] }, normal, edged[w].Color));
                        }
                        nNewTriangles = listTriangles.Count;
                    }
                    for (int k = 0; k < listTriangles.Count; k++)
                        trisToRaster.Add(listTriangles[k]);
                }
            }
        }
        return trisToRaster.ToArray();
    }

    private int TriangleClipAgainstPlane(Vector3 planeP, Vector3 planeN, ref TriNormsCol tri, out TriNormsCol tri1, out TriNormsCol tri2)
    {
        planeN.Normalize();
        float dist(Vector3 p)
        {
            return (Vector3.Dot(planeN, p) - Vector3.Dot(planeN, planeP));
        }

        Vector3 normal = Vector3.Cross(tri.Vertices[1] - tri.Vertices[0], tri.Vertices[2] - tri.Vertices[0]);
        Vector3[] insidep = new Vector3[3]; int nInsidePointCount = 0;
        Vector3[] outsidep = new Vector3[3]; int nOutsidePointCount = 0;

        float d0 = dist(tri.Vertices[0]);
        float d1 = dist(tri.Vertices[1]);
        float d2 = dist(tri.Vertices[2]);

        if (d0 >= 0) { insidep[nInsidePointCount++] = tri.Vertices[0]; }
        else { outsidep[nOutsidePointCount++] = tri.Vertices[0]; }
        if (d1 >= 0) { insidep[nInsidePointCount++] = tri.Vertices[1]; }
        else { outsidep[nOutsidePointCount++] = tri.Vertices[1]; }
        if (d2 >= 0) { insidep[nInsidePointCount++] = tri.Vertices[2]; }
        else { outsidep[nOutsidePointCount++] = tri.Vertices[2]; }

        if (nInsidePointCount == 0)
        {
            tri1 = new TriNormsCol(0);
            tri2 = new TriNormsCol(0);
            return 0;
        }
        else if (nInsidePointCount == 3)
        {
            tri1 = tri;
            tri2 = new TriNormsCol(0);
            return 1;
        }
        else if (nInsidePointCount == 1 && nOutsidePointCount == 2)
        {
            tri1 = new TriNormsCol(0);
            tri2 = new TriNormsCol(0);
            tri1.Vertices[0] = insidep[0];
            tri1.Vertices[1] = VectorIntersectPlane(planeP, planeN, insidep[0], outsidep[1]);
            tri1.Vertices[2] = VectorIntersectPlane(planeP, planeN, insidep[0], outsidep[0]);
            tri1.Normals = tri.Normals;
            tri1.Color = tri.Color;
            //tri1.Color = new Vector4(0.0f, 0.0f, 1.0f, 1.0f);
            Vector3 normal1 = Vector3.Cross(tri1.Vertices[1] - tri1.Vertices[0], tri1.Vertices[2] - tri1.Vertices[0]);
            if (Vector3.Dot(normal1, normal) < 0.0f)
            {
                Vector3 temp = tri1.Vertices[1];
                tri1.Vertices[1] = tri1.Vertices[2];
                tri1.Vertices[2] = temp;
            }
            return 1;
        }
        else if (nInsidePointCount == 2 && nOutsidePointCount == 1)
        {
            tri1 = new TriNormsCol(0);
            tri2 = new TriNormsCol(0);
            tri1.Vertices[0] = insidep[0];
            tri1.Vertices[1] = VectorIntersectPlane(planeP, planeN, insidep[1], outsidep[0]);
            tri1.Vertices[2] = insidep[1];
            tri1.Normals = tri.Normals;
            tri1.Color = tri.Color;
            //tri1.Color = new Vector4(0.0f, 1.0f, 0.0f, 1.0f);
            tri2.Vertices[0] = insidep[0];
            tri2.Vertices[1] = VectorIntersectPlane(planeP, planeN, insidep[0], outsidep[0]);
            tri2.Vertices[2] = VectorIntersectPlane(planeP, planeN, insidep[1], outsidep[0]);
            tri2.Normals = tri.Normals;
            tri2.Color = tri.Color;
            //tri2.Color = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
            Vector3 normal1 = Vector3.Cross(tri1.Vertices[1] - tri1.Vertices[0], tri1.Vertices[2] - tri1.Vertices[0]);
            if (Vector3.Dot(normal1, normal) < 0.0f)
            {
                Vector3 temp = tri1.Vertices[1];
                tri1.Vertices[1] = tri1.Vertices[2];
                tri1.Vertices[2] = temp;
            }
            Vector3 normal2 = Vector3.Cross(tri2.Vertices[1] - tri2.Vertices[0], tri2.Vertices[2] - tri2.Vertices[0]);
            if (Vector3.Dot(normal2, normal) < 0.0f)
            {
                Vector3 temp = tri2.Vertices[1];
                tri2.Vertices[1] = tri2.Vertices[2];
                tri2.Vertices[2] = temp;
            }
            return 2;
        }
        else { tri1 = new TriNormsCol(0); tri2 = new TriNormsCol(0); return 0; }
    }

    private Vector3 VectorIntersectPlane(Vector3 planeP, Vector3 planeN, Vector3 lineStart, Vector3 lineEnd)
    {
        float planeD = Vector3.Dot(planeN, planeP);
        float ad = Vector3.Dot(lineStart, planeN);
        float bd = Vector3.Dot(lineEnd, planeN);
        float t = (planeD - ad) / (bd - ad);
        Vector3 lineStartToEnd = lineEnd - lineStart;
        Vector3 lineToIntersect = lineStartToEnd * t;
        return lineStart + lineToIntersect;
    }

    private void BubbleSort(ref List<TriNormsCol> list)
    {
        if (list.Count == 0)
            return;
        int n = list.Count;
        while (n >= 1)
        {
            int newn = 0;
            for (int i = 1; i < n; i++)
            {
                if (CompareTriangles(list[i - 1], list[i]) == 1)
                {
                    TriNormsCol temp = list[i - 1];
                    list[i - 1] = list[i];
                    list[i] = temp;
                    newn = i;
                }
            }
            n = newn;
        }
    }

    private void QuickSort(ref List<TriNormsCol> A, int lo, int hi)
    {
        if (lo < hi)
        {
            int p = Partition(ref A, lo, hi);
            QuickSort(ref A, lo, p - 1);
            QuickSort(ref A, p + 1, hi);
        }
    }

    private int Partition(ref List<TriNormsCol> A, int lo, int hi)
    {
        TriNormsCol pivot = A[hi];
        int i = lo;
        for (int j = lo; j < hi; j++)
        {
            if (CompareTriangles(A[j], pivot) == -1)
            {
                TriNormsCol temp = A[j];
                A[j] = A[i];
                A[i] = temp;
                i++;
            }
        }
        TriNormsCol temp2 = A[hi];
        A[hi] = A[i];
        A[i] = temp2;
        return i;
    }

    private int CompareTriangles(TriNormsCol a, TriNormsCol b)
    {
        float z1 = Math.Max(Math.Max(a.Vertices[0].Z, a.Vertices[1].Z), a.Vertices[2].Z);
        float z2 = Math.Max(Math.Max(b.Vertices[0].Z, b.Vertices[1].Z), b.Vertices[2].Z);
        if (z1 < z2)
            return 1;
        else
            return -1;
    }

    private List<TriNormsCol> PopFront(List<TriNormsCol> list)
    {
        List<TriNormsCol> temp = new List<TriNormsCol>();
        for (int i = 1; i < list.Count; i++)
            temp.Add(list[i]);
        return temp;
    }

    private Color RayCast(ref Ray ray, int iteration, ref float[] distances, out float impact)
    {
        float bestDistance = float.PositiveInfinity;
        int[] indices = new int[2];
        for (int i = 0; i < gameobjects.Count; i++)
        {
            for (int j = 0; j < gameobjects[i].triangles.Length; j++)
            {
                float distance = TriangleIntersect(ray, gameobjects[i].projectedTriangles[j]);
                if (distance >= 0.0f && distance < bestDistance)   // check that the triangle intersects ray and is the closest 
                {
                    bestDistance = distance;
                    indices[0] = i;
                    indices[1] = j;
                }
            }
        }
        if (bestDistance != float.PositiveInfinity)
        {
            float bestLightDistance = float.PositiveInfinity;
            int index = -1;
            for (int i = 0; i < lights.Count; i++)
            {
                float distance = LightIntersect(ray, lights[i]);
                if (distance != -1.0f && distance < bestDistance)   // check that the triangle intersects ray and is the closest 
                {
                    bestLightDistance = distance;
                    index = i;
                }
            }
            if (index != -1.0f && bestLightDistance < bestDistance) // check if a light is closer
            {
                distances[iteration] += bestLightDistance;
                impact = lights[index].Luminosity;
                return Color.White;
            }
            distances[iteration] += bestDistance;
            TriNormsCol tri = gameobjects[indices[0]].projectedTriangles[indices[1]];
            Vector3 normal = Vector3.Normalize(Vector3.Cross(tri.Vertices[1] - tri.Vertices[0], tri.Vertices[2] - tri.Vertices[0]));
            ray.Position = ray.Position + ray.Direction * bestDistance;
            ray.Direction = Vector3.Reflect(ray.Direction, normal); // reflect the ray off of the surface for the next raycast
            Vector3 color = new Vector3(tri.Color.X, tri.Color.Y, tri.Color.Z);
            impact = MinBrightness;
            for (int i = 0; i < lights.Count; i++)
            {
                Vector3 toLight = lights[i].Position - ray.Position;
                if (Vector3.Dot(toLight, normal) <= 0) // check if the surface is opposing the light
                    continue;
                float brightness = lights[i].Luminosity / 4 / (float)Math.PI / Vector3.Dot(toLight, toLight);
                if (brightness > 1)
                {
                    impact += brightness;
                    color += ColorLerp(color, new Vector3(1.0f), (brightness - 1) / 100);
                }
                else
                {
                    impact += 1.0f;
                    color += ColorLerp(new Vector3(), color, brightness);
                }
            }
            impact /= lights.Count;
            color /= lights.Count;
            return new Color(color.X, color.Y, color.Z);
        }
        else
        {
            distances[iteration] = bestDistance;
            impact = 1.0f;
            return new Color(BGCol.X, BGCol.Y, BGCol.Z);
        }
    }

    private float TriangleIntersect(Ray ray, TriNormsCol tri)
    {
        Vector3 normal = Vector3.Normalize(Vector3.Cross(tri.Vertices[1] - tri.Vertices[0], tri.Vertices[2] - tri.Vertices[0]));
        float numerator = Vector3.Dot(normal, tri.Vertices[0] - ray.Position);
        float denominator = Vector3.Dot(normal, ray.Direction);
        if (denominator >= 0.0f)
        {
            return -1.0f;
        }
        float intersection = numerator / denominator;
        if (intersection <= 0.0f)
        {
            return -1.0f;
        }

        // test if intersection is inside triangle ////////////////////////////

        Vector3 point = ray.Position + ray.Direction * intersection;
        Vector3 edge0 = tri.Vertices[1] - tri.Vertices[0];
        Vector3 edge1 = tri.Vertices[2] - tri.Vertices[1];
        Vector3 edge2 = tri.Vertices[0] - tri.Vertices[2];
        Vector3 C0 = point - tri.Vertices[0];
        Vector3 C1 = point - tri.Vertices[1];
        Vector3 C2 = point - tri.Vertices[2];
        if (!(Vector3.Dot(normal, Vector3.Cross(C0, edge0)) <= 0.00001f &&
            Vector3.Dot(normal, Vector3.Cross(C1, edge1)) <= 0.00001f &&
            Vector3.Dot(normal, Vector3.Cross(C2, edge2)) <= 0.00001f))
        {
            return -1.0f; // point is inside the triangle
        }
        return intersection;
    }

    private float LightIntersect(Ray ray, Light light)
    {
        Vector3 toSphere = ray.Position - light.Position;
        float a = Vector3.Dot(ray.Direction, toSphere);
        float discriminant = a * a - toSphere.LengthSquared() + light.Radius * light.Radius;
        if (discriminant < 0.0f)
        {
            return -1.0f;
        }
        float intersection = -Vector3.Dot(ray.Direction, ray.Position - light.Position) - (float)Math.Sqrt(discriminant);
        if (intersection <= 0.0f)
        {
            return -1.0f;
        }
        return intersection;
    }

    private Vector3 ColorLerp(Vector3 x, Vector3 y, float t)
    {
        if (t >= 1)
            return y;
        else if (t <= 0)
            return x;
        float r = x.X + (int)((y.X - x.X) * t),
            g = x.Y + (int)((y.Y - x.Y) * t),
            b = x.Z + (int)((y.Z - x.Z) * t);
        return new Vector3(r, g, b);
    }

    private void UpdateShaderPolyCount()
    {
        if (lights.Count == 0)
            lights.Add(new Light(new Vector3(), new Vector4(), 1.0f, 1.0f));
        if (spheres.Count == 0)
            spheres.Add(new Sphere());
        if (gameobjects.Count == 0)
            gameobjects.Add(new Gameobject("", new Vector3(), new Vector3(), new Vector3(), new TriNormsCol[] { new TriNormsCol(0) }));
        NumberOfTriangles = 0;
        for (int i = 0; i < gameobjects.Count; i++)
            NumberOfTriangles += gameobjects[i].triangles.Length;
        emptyVertexData = new EmptyBuffer(NumberOfTriangles * 7 + spheres.Count * 3);
        emptyLightData = new EmptyBuffer(lights.Count * 3);
        string[] shader = File.ReadAllLines(path + @"\Shaders.hlsl");
        int index0 = -1, index1 = -1, index2 = -1, index3 = -1;
        for (int i = 0; i < shader.Length; i++)
        {
            if (shader[i].Contains("float3 Vertices["))
                index0 = i;
            if (shader[i].Contains("float4 Lights["))
                index1 = i;
            if (shader[i].Contains("float distances["))
            {
                if (index2 == -1)
                    index2 = i;
                else
                    index3 = i;
            }
        }
        shader[index0] = "    float3 Vertices[" + (NumberOfTriangles * 3) + "];";
        shader[index0 + 1] = "    float4 Normals[" + (NumberOfTriangles * 3) + "];";
        shader[index0 + 2] = "    float4 Colors[" + NumberOfTriangles + "];";
        shader[index0 + 3] = "    float4 Spheres[" + (spheres.Count * 3) + "];";
        shader[index1] = "    float4 Lights[" + (lights.Count * 3) + "];";
        shader[index2] = "    float distances[" + (NumberOfTriangles + spheres.Count + lights.Count) + "];";
        shader[index3] = "    float distances[" + (NumberOfTriangles + spheres.Count + lights.Count) + "];";
        shader[index3 + 1] = "    Ray rays[" + (RayDepth + 1) + "];";
        shader[index3 + 2] = "	float dropOff[" + (RayDepth + 1) + "];";
        File.WriteAllLines(path + @"\Shaders.hlsl", shader);
    }

    private void UpdateWindowState()
    {
        if (State == prevState)
            return;
        prevState = State;
        switch (prevState)
        {
            case WindowState.Minimized:
                if (renderForm.WindowState != FormWindowState.Minimized)
                    renderForm.WindowState = FormWindowState.Minimized;
                RefreshRate = DisplayRefreshRate;
                break;
            case WindowState.Normal:
                renderForm.FormBorderStyle = FormBorderStyle.Fixed3D;
                if (renderForm.WindowState != FormWindowState.Normal)
                    renderForm.WindowState = FormWindowState.Normal;
                renderForm.ClientSize = new Size(engineDescription.Width, engineDescription.Height);
                Width = engineDescription.Width;
                Height = (int)(Width / (float)renderForm.ClientSize.Width * renderForm.ClientSize.Height);
                UpdateSwapChain();
                RefreshRate = engineDescription.RefreshRate;
                break;
            case WindowState.Maximized:
                renderForm.FormBorderStyle = FormBorderStyle.Fixed3D;
                if (renderForm.WindowState != FormWindowState.Maximized)
                    renderForm.WindowState = FormWindowState.Maximized;
                Width = engineDescription.Width;
                Height = (int)(Width / (float)renderForm.ClientSize.Width * renderForm.ClientSize.Height);
                UpdateSwapChain();
                RefreshRate = engineDescription.RefreshRate;
                break;
            case WindowState.FullScreen:
                renderForm.FormBorderStyle = FormBorderStyle.None;
                if (renderForm.WindowState != FormWindowState.Maximized)
                    renderForm.WindowState = FormWindowState.Maximized;
                Width = engineDescription.Width;
                Height = (int)(Width / (float)renderForm.ClientSize.Width * renderForm.ClientSize.Height);
                UpdateSwapChain();
                swapChain1.IsFullScreen = true;
                RefreshRate = engineDescription.RefreshRate;
                break;
        }
    }

    private void UpdateSwapChain()
    {
        if (device1 != null)
            DisposeGraphics();

        if (RType == RenderType.RasterizedGPU)
            InitializeRasterGPUDeviceResources();
        else
            InitializeDeviceResources();
        InitializePlaneBuffer();
        if (RType == RenderType.RasterizedCPU || RType == RenderType.Custom)
            InitializeVertexBuffer();
        else if (RType == RenderType.RasterizedGPU)
            PackVertexBuffer();
        InitializeShaders();
        SetShaderResources();
    }

    private void LostFocus(object sender, EventArgs e)
    {
        Focused = false;
    }

    private void GotFocus(object sender, EventArgs e)
    {
        Focused = true;
    }

    private void Closing(object sender, FormClosingEventArgs e)
    {
        Stop();
        Dispose();
    }

    private void Resize(object sender, EventArgs e)
    {
        switch (renderForm.WindowState)
        {
            case FormWindowState.Minimized:
                State = WindowState.Minimized;
                break;
            case FormWindowState.Normal:
                State = WindowState.Normal;
                break;
            case FormWindowState.Maximized:
                if (renderForm.FormBorderStyle == FormBorderStyle.None)
                    State = WindowState.FullScreen;
                else
                    State = WindowState.Maximized;
                break;
        }
    }

    public static void print(object message)
    {
        Console.WriteLine(message);
    }

    public static void print()
    {
        Console.WriteLine();
    }
}
