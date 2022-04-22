using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Threading;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using System.Windows.Forms;
using Vortice.Mathematics;
using Size = System.Drawing.Size;
using System.Runtime.InteropServices;
using Vortice.D3DCompiler;
using System.Text;
using System.Reflection;

/// <summary>
/// ToDo
///     make it work lmao
/// </summary>

namespace DXRenderEngine;

public class Engine : IDisposable
{
    // Graphics Fields and Properties
    public readonly EngineDescription Description;
    protected Form window;
    private IDXGIFactory5 factory;
    private IDXGIAdapter4 adapter;
    protected ID3D11Device5 device;
    protected ID3D11DeviceContext3 context;
    protected IDXGISwapChain4 swapChain;
    protected ID3D11RenderTargetView1 renderTargetView;
    protected VertexPositionNormalColor[] vertices;
    protected ObjectInstance[] instances;
    protected ID3D11Buffer vertexBuffer;
    protected ID3D11Buffer instanceBuffer;
    protected ID3D11VertexShader vertexShader;
    protected ID3D11PixelShader pixelShader;
    protected Viewport screenViewport;
    protected InputElementDescription[] inputElements;
    protected ID3D11InputLayout inputLayout;
    protected float displayRefreshRate { get; private set; }
    protected string shaderCode;
    protected readonly string vertexShaderEntry;
    protected readonly string pixelShaderEntry;
    protected PackedLight[] packedLights;

    // Controllable Graphics Settings
    public int Width 
    { 
        get => window.ClientSize.Width;
        private set => window.BeginInvoke(() => window.ClientSize = new Size(value, window.ClientSize.Height));
    }
    public int Height
    {
        get => window.ClientSize.Height;
        private set => window.BeginInvoke(() => window.ClientSize = new Size(window.ClientSize.Width, value));
    }

    // Time Fields
    public double frameTime { get; private set; }
    private long t1, t2;
    protected readonly Stopwatch sw;
    private const long MILLISECONDS_FOR_RESET = 1000;
    private long startFPSTime;
    private long lastReset = 0;
    public List<double> allFPSList { get; private set; }
    public List<double> allTimeList { get; private set; }
    private double avgFPS = 0.0, minFPS = double.PositiveInfinity, maxFPS = 0.0;
    public long frameCount { get; private set; }

    // Engine Fields
    public IntPtr Handle => window.Handle;
    public bool Running { get; private set; }
    private Action OnAwake, OnStart, OnUpdate, UserInput;
    public const float DEG2RAD = (float)Math.PI / 180.0f;
    public bool Focused { get; private set; }
    public readonly Input input;
    private Thread renderThread, controlThread;
    protected readonly Vector3 BGCol = new Vector3();
    protected readonly float MinBrightness = 0.1f;
    public readonly List<Gameobject> gameobjects = new List<Gameobject>();
    public readonly List<Light> lights = new List<Light>();
    public Vector3 EyePos;
    public Vector3 EyeRot;

    private const bool UseCache = true;

    public Engine(EngineDescription desc)
    {
        Description = desc;
        OnAwake = desc.OnAwake;
        OnStart = desc.OnStart;
        OnUpdate = desc.OnUpdate;
        UserInput = desc.UserInput;
        vertexShaderEntry = "vertexShader";
        pixelShaderEntry = "pixelShader";
        inputElements = new InputElementDescription[]
        {
            new InputElementDescription("POSITION", 0, Format.R32G32B32A32_Float, 0, 0, InputClassification.PerVertexData, 0),
            new InputElementDescription("NORMAL", 0, Format.R32G32B32A32_Float, 16, 0, InputClassification.PerVertexData, 0),
            new InputElementDescription("COLOR", 0, Format.R32G32B32A32_Float, 32, 0, InputClassification.PerVertexData, 0),
            new InputElementDescription("WORLDMATRIX", 0, Format.R32G32B32A32_Float, 0, 1, InputClassification.PerInstanceData, 1),
            new InputElementDescription("WORLDMATRIX", 1, Format.R32G32B32A32_Float, 16, 1, InputClassification.PerInstanceData, 1),
            new InputElementDescription("WORLDMATRIX", 2, Format.R32G32B32A32_Float, 32, 1, InputClassification.PerInstanceData, 1),
            new InputElementDescription("WORLDMATRIX", 3, Format.R32G32B32A32_Float, 48, 1, InputClassification.PerInstanceData, 1),
            new InputElementDescription("INVERSETRANSPOSEWORLDMATRIX", 0, Format.R32G32B32A32_Float, 64, 1, InputClassification.PerInstanceData, 1),
            new InputElementDescription("INVERSETRANSPOSEWORLDMATRIX", 1, Format.R32G32B32A32_Float, 80, 1, InputClassification.PerInstanceData, 1),
            new InputElementDescription("INVERSETRANSPOSEWORLDMATRIX", 2, Format.R32G32B32A32_Float, 96, 1, InputClassification.PerInstanceData, 1),
            new InputElementDescription("INVERSETRANSPOSEWORLDMATRIX", 3, Format.R32G32B32A32_Float, 112, 1, InputClassification.PerInstanceData, 1),
        };

        sw = new Stopwatch();
        sw.Start();
        allFPSList = new List<double>();
        allTimeList = new List<double>();

        Application.EnableVisualStyles();

        window = new Form()
        {
            Text = "DXRenderEngine",
            ClientSize = new Size(desc.Width, desc.Height),
            FormBorderStyle = FormBorderStyle.Fixed3D,
            BackColor = System.Drawing.Color.Black,
            WindowState = desc.WindowState
        };

        input = new Input(window.Handle, this, UserInput);

        window.Load += Window_Load;
        window.Shown += Window_Shown;
        window.GotFocus += GotFocus;
        window.LostFocus += LostFocus;
        Focused = window.Focused;
        window.Resize += Window_Resize;
        window.FormClosing += Closing;
    }

    public void Run()
    {
        Application.Run(window);
    }

    protected virtual void InitializeDeviceResources()
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
            BufferCount = 2,
            Format = Format.R8G8B8A8_UNorm,
            Height = Height,
            Width = Width,
            SampleDescription = new SampleDescription(1, 0),
            SwapEffect = SwapEffect.FlipDiscard,
            BufferUsage = Usage.RenderTargetOutput
        };
        swapChain = new IDXGISwapChain4(factory.CreateSwapChainForHwnd(device, window.Handle, scd).NativePointer);

        AssignRenderTarget();

        UpdateShaderConstants();

        InitializeShaders();

        SetConstantBuffers();
    }

    private void AssignRenderTarget()
    {
        using (ID3D11Texture2D1 backBuffer = swapChain.GetBuffer<ID3D11Texture2D1>(0))
            renderTargetView = device.CreateRenderTargetView1(backBuffer);
        screenViewport = new Viewport(0, 0, Width, Height);
        context.RSSetViewport(screenViewport);
    }

    protected virtual void InitializeVertices()
    {
        PackVertices();

        BufferDescription bd = new BufferDescription(vertices.Length * Marshal.SizeOf<VertexPositionNormalColor>(), BindFlags.VertexBuffer);
        vertexBuffer = device.CreateBuffer(vertices, bd);
        bd = new BufferDescription(instances.Length * Marshal.SizeOf<ObjectInstance>(), BindFlags.VertexBuffer, ResourceUsage.Dynamic, CpuAccessFlags.Write);
        instanceBuffer = device.CreateBuffer(instances, bd);
        VertexBufferView[] views = new VertexBufferView[2];
        views[0] = new VertexBufferView(vertexBuffer, Marshal.SizeOf<VertexPositionNormalColor>());
        views[1] = new VertexBufferView(instanceBuffer, Marshal.SizeOf<ObjectInstance>());
        context.IASetVertexBuffers(0, views);
    }

    protected virtual void PackVertices()
    {
        int vertexCount = 0;
        for (int i = 0; i < gameobjects.Count; i++)
        {
            gameobjects[i].VerticesOffset = vertexCount;
            vertexCount += gameobjects[i].Triangles.Length * 3;
        }
        vertices = new VertexPositionNormalColor[vertexCount];
        instances = new ObjectInstance[gameobjects.Count];
        for (int i = 0; i < gameobjects.Count; i++)
        {
            for (int j = 0; j < gameobjects[i].Triangles.Length; j++)
            {
                vertices[gameobjects[i].VerticesOffset + j * 3] = gameobjects[i].Triangles[j].GetVertexPositionNormalColor(0);
                vertices[gameobjects[i].VerticesOffset + j * 3 + 1] = gameobjects[i].Triangles[j].GetVertexPositionNormalColor(1);
                vertices[gameobjects[i].VerticesOffset + j * 3 + 2] = gameobjects[i].Triangles[j].GetVertexPositionNormalColor(2);
            }
            instances[i] = new ObjectInstance(gameobjects[i].World, gameobjects[i].Normal);
        }
    }

    protected virtual void UpdateShaderConstants()
    {
        if (lights.Count == 0)
            lights.Add(new Light());
        if (gameobjects.Count == 0)
            gameobjects.Add(new Gameobject());
    }

    protected void ChangeShader(string target, string newValue)
    {
        int index = shaderCode.IndexOf(target);
        if (index == -1)
        {
            throw new Exception(target + " does not exist");
        }

        shaderCode = shaderCode.Replace(target, newValue);
    }

    protected virtual void InitializeShaders()
    {
        string fileLocation = Directory.GetCurrentDirectory();
        string vertexShaderFile = fileLocation + @"\VertexShaderCache.blob";
        string pixelShaderFile = fileLocation + @"\PixelShaderCache.blob";

        ShaderFlags sf = ShaderFlags.OptimizationLevel3;
#if DEBUG
        sf = ShaderFlags.Debug;
#endif

        Blob shaderBlob;
        if (UseCache && File.Exists(vertexShaderFile))
        {
            shaderBlob = Compiler.ReadFileToBlob(vertexShaderFile);
        }
        else
        {
            Compiler.Compile(shaderCode, null, null, vertexShaderEntry, "VertexShader", "vs_5_0", sf, out shaderBlob, out Blob errorCode);
            if (shaderBlob == null)
                throw new Exception("HLSL vertex shader compilation error:\r\n" + Encoding.ASCII.GetString(errorCode.GetBytes()));

            Compiler.WriteBlobToFile(shaderBlob, vertexShaderFile, true);
        }

        vertexShader = device.CreateVertexShader(shaderBlob);

        inputLayout = device.CreateInputLayout(inputElements, shaderBlob);
        context.IASetInputLayout(inputLayout);
        context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);

        shaderBlob.Dispose();

        if (UseCache && File.Exists(pixelShaderFile))
        {
            shaderBlob = Compiler.ReadFileToBlob(pixelShaderFile);
        }
        else
        {
            Compiler.Compile(shaderCode, null, null, pixelShaderEntry, "PixelShader", "ps_5_0", sf, out shaderBlob, out Blob errorCode);
            if (shaderBlob == null)
                throw new Exception("HLSL pixel shader compilation error:\r\n" + Encoding.ASCII.GetString(errorCode.GetBytes()));

            Compiler.WriteBlobToFile(shaderBlob, pixelShaderFile, true);
        }

        pixelShader = device.CreatePixelShader(shaderBlob);

        shaderBlob.Dispose();

        context.VSSetShader(vertexShader);
        context.PSSetShader(pixelShader);
    }

    protected virtual void SetConstantBuffers()
    {
    }

    protected void PackLights()
    {
        packedLights = new PackedLight[lights.Count];

        for (int i = 0; i < lights.Count; i++)
        {
            packedLights[i] = lights[i].Pack();
        }
    }

    private void GetTime()
    {
        t2 = sw.ElapsedTicks;
        frameTime = (t2 - t1) / 10000000.0;
        if (Description.RefreshRate != 0)
        {
            while (1.0 / (frameTime) > Description.RefreshRate)
            {
                t2 = sw.ElapsedTicks;
                frameTime = (t2 - t1) / 10000000.0;
            }
        }
        t1 = t2;
        double fps = 1.0 / frameTime;
        maxFPS = Math.Max(fps, maxFPS);
        minFPS = Math.Min(fps, minFPS);
        frameCount++;
        allFPSList.Add(fps);
        allTimeList.Add(sw.ElapsedTicks / 10000000.0);

        // reset counters
        if (sw.ElapsedMilliseconds / MILLISECONDS_FOR_RESET > lastReset)
        {
            avgFPS = 10000000.0 * frameCount / (sw.ElapsedTicks - startFPSTime);
            startFPSTime = sw.ElapsedTicks;
            string text = "DXRenderEngine   " + avgFPS.ToString("G4") +
            "fps (" + minFPS.ToString("G4") + "fps, " + maxFPS.ToString("G4") + "fps)";
            window.BeginInvoke(new Action(() => window.Text = text));
            maxFPS = 0.0;
            minFPS = double.PositiveInfinity;
            frameCount = 0;
            lastReset = sw.ElapsedMilliseconds / MILLISECONDS_FOR_RESET;
        }
    }

    public void Stop()
    {
        if (Running)
            window.BeginInvoke(window.Close);
    }

    public void ToggleFullscreen()
    {
        if (swapChain.IsFullscreen)
        {
            Width = Description.Width;
            Height = Description.Width;
        }
        else
        {
            Width = Screen.PrimaryScreen.Bounds.Width;
            Height = Screen.PrimaryScreen.Bounds.Height;
        }
    }

    private void Resize()
    {
        renderTargetView.Release();
        swapChain.ResizeBuffers(0, Width, Height);

        AssignRenderTarget();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool boolean)
    {
        Stop();
        input.Dispose();
        window.Dispose();
    }

    /////////////////////////////////////

    private void RenderLoop()
    {
        while (Running)
        {
            GetTime();
            if (window.WindowState == FormWindowState.Minimized)
                return;
            Render();
            OnUpdate();
            PerFrameUpdate();
            swapChain.Present(swapChain.IsFullscreen ? 1 : 0);
        }
    }

    protected virtual void PerApplicationUpdate()
    {
        foreach (Light l in lights)
        {
            l.GenerateMatrix();
        }
    }

    protected virtual void PerFrameUpdate()
    {
        for (int i = 0; i < gameobjects.Count; i++)
        {
            gameobjects[i].CreateMatrices();
            instances[i] = gameobjects[i].GetInstance();
        }

        // update object matrices
        MappedSubresource resource = context.Map(instanceBuffer, MapMode.WriteNoOverwrite);
        IntPtr pointer = resource.DataPointer;
        WriteStructArray(instances, ref pointer);
        context.Unmap(instanceBuffer);
    }

    protected virtual void Render()
    {
        context.ClearRenderTargetView(renderTargetView, Colors.Black);
        context.OMSetRenderTargets(renderTargetView);
        context.Draw(vertices.Length, 0);
    }

    /////////////////////////////////////

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
        Running = false;
        renderThread.Join();
        controlThread.Join();

        Dispose();
    }

    private void Window_Resize(object sender, EventArgs e)
    {
        print("resize");
        if (window.ClientSize == Screen.PrimaryScreen.Bounds.Size)
            if (!swapChain.IsFullscreen)
                swapChain.SetFullscreenState(true);
        else if (swapChain.IsFullscreen)
            swapChain.SetFullscreenState(false);

        Resize();
    }

    private void Window_Load(object sender, EventArgs e)
    {
        if (shaderCode == null)
        {
            throw new ArgumentNullException("shader code");
        }
        Running = true;
        OnAwake();
        InitializeDeviceResources();
        GetDisplayRefreshRate();
        InitializeVertices();
        OnStart();
        PerApplicationUpdate();
    }

    private void Window_Shown(object sender, EventArgs e)
    {
        input.InitializeInputs();

        controlThread = new Thread(() => input.ControlLoop());
        renderThread = new Thread(() => RenderLoop());

        t1 = startFPSTime = sw.ElapsedTicks;
        renderThread.Start();
        controlThread.Start();
    }

    private void GetDisplayRefreshRate()
    {
        int num = 0;
        while (true)
        {
            var output = adapter.GetOutput(num);
            if (output == null)
                break;
            var modes = output.GetDisplayModeList(Format.R8G8B8A8_UNorm, DisplayModeEnumerationFlags.DisabledStereo);
            foreach (var mode in modes)
            {
                displayRefreshRate = Math.Max(displayRefreshRate, mode.RefreshRate.Numerator / (float)mode.RefreshRate.Denominator);
            }
            num++;
        }
    }

    ///////////////////////////////////////

    public static int Pow(int b, int e)
    {
        int output = b;
        while (e-- > 1)
        {
            output *= b;
        }
        return output;
    }

    protected static void WriteStructArray<T>(T[] data, ref IntPtr pointer)
    {
        for (int i = 0; i < data.Length; i++)
        {
            Marshal.StructureToPtr(data[i], pointer, true);
            pointer += Marshal.SizeOf<T>();
        }
    }

    public static Matrix4x4 CreateView(Vector3 pos, Vector3 rot)
    {
        Matrix4x4 rotation = CreateRotation(rot);
        Vector3 xaxis = Vector3.TransformNormal(Vector3.UnitX, rotation);
        Vector3 yaxis = Vector3.TransformNormal(Vector3.UnitY, rotation);
        Vector3 zaxis = Vector3.TransformNormal(Vector3.UnitZ, rotation);

        Matrix4x4 result = Matrix4x4.Identity;
        result.M11 = xaxis.X;
        result.M21 = xaxis.Y;
        result.M31 = xaxis.Z;
        result.M12 = yaxis.X;
        result.M22 = yaxis.Y;
        result.M32 = yaxis.Z;
        result.M13 = zaxis.X;
        result.M23 = zaxis.Y;
        result.M33 = zaxis.Z;
        result.M41 = -Vector3.Dot(xaxis, pos);
        result.M42 = -Vector3.Dot(yaxis, pos);
        result.M43 = -Vector3.Dot(zaxis, pos);

        return result;
    }

    public static Matrix4x4 CreateRotation(Vector3 rot)
    {
        Matrix4x4 rotx = CreateRotationX(rot.X);
        Matrix4x4 roty = CreateRotationY(rot.Y);
        //Matrix4x4 rotz = CreateRotationZ(rot.Z);
        return rotx * roty;
    }

    private static Matrix4x4 CreateRotationX(float angle)
    {
        float cos = (float)Math.Cos(angle * DEG2RAD);
        float sin = (float)Math.Sin(angle * DEG2RAD);

        Matrix4x4 result = Matrix4x4.Identity;
        result.M22 = cos;
        result.M23 = sin;
        result.M32 = -sin;
        result.M33 = cos;

        return result;
    }

    private static Matrix4x4 CreateRotationY(float angle)
    {
        float cos = (float)Math.Cos(angle * DEG2RAD);
        float sin = (float)Math.Sin(angle * DEG2RAD);

        Matrix4x4 result = Matrix4x4.Identity;
        result.M11 = cos;
        result.M13 = -sin;
        result.M31 = sin;
        result.M33 = cos;

        return result;
    }

    private static Matrix4x4 CreateRotationZ(float angle)
    {
        float cos = (float)Math.Cos(angle * DEG2RAD);
        float sin = (float)Math.Sin(angle * DEG2RAD);

        Matrix4x4 result = Matrix4x4.Identity;
        result.M11 = cos;
        result.M12 = sin;
        result.M21 = -sin;
        result.M22 = cos;

        return result;
    }

    public static Matrix4x4 CreateWorld(Vector3 pos, Vector3 rot, Vector3 sca)
    {
        //Matrix4x4 rotation = CreateRotation(rot);
        //Vector3 forward = Vector3.TransformNormal(Vector3.UnitZ, rotation);
        //Vector3 up = Vector3.TransformNormal(Vector3.UnitY, rotation);
        //return Matrix4x4.CreateWorld(pos, forward, up);
        Matrix4x4 position = Matrix4x4.CreateTranslation(pos);
        Matrix4x4 rotation = CreateRotation(rot);
        Matrix4x4 scale = Matrix4x4.CreateScale(sca);

        return position * rotation * scale;
    }

    public static void print(object message)
    {
        Trace.WriteLine(message);
    }

    public static void print()
    {
        Trace.WriteLine("");
    }

    public static Engine Create(EngineDescription desc)
    {
        return new Engine(desc);
    }

    public static RasterizingEngine Create(RasterizingEngineDescription desc)
    {
        return new RasterizingEngine(desc);
    }

    public static RayTracingEngine Create(RayTracingEngineDescription desc)
    {
        return new RayTracingEngine(desc);
    }
}
