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
using System.Runtime.InteropServices;
using Vortice.D3DCompiler;
using System.Text;
using System.Reflection;

namespace DXRenderEngine;

/// <summary>
/// ToDo
///     full screen
/// </summary>
public class Engine : IDisposable
{
    // Graphics Fields and Properties
    public readonly EngineDescription Description;
    protected Form window;
    private IDXGIFactory5 factory;
    private IDXGIAdapter4 adapter;
    private IDXGIOutput output;
    protected ID3D11Device5 device;
    protected ID3D11DeviceContext3 context;
    protected IDXGISwapChain4 swapChain;
    protected ID3D11RenderTargetView1 renderTargetView;
    protected VertexPositionNormal[] vertices;
    protected ID3D11Buffer vertexBuffer;
    protected ID3D11VertexShader vertexShader;
    protected ID3D11PixelShader pixelShader;
    protected Viewport screenViewport;
    protected InputElementDescription[] inputElements;
    protected ID3D11InputLayout inputLayout;
    protected float displayRefreshRate { get; private set; }
    protected string shaderCode;
    protected readonly string vertexShaderEntry;
    protected readonly string pixelShaderEntry;

    // Controllable Graphics Settings
    public int Width 
    { 
        get => window.ClientSize.Width;
        private set => window.BeginInvoke(() => window.ClientSize = new(value, window.ClientSize.Height));
    }
    public int Height
    {
        get => window.ClientSize.Height;
        private set => window.BeginInvoke(() => window.ClientSize = new(window.ClientSize.Width, value));
    }

    // Time Fields
    public double frameTime { get; private set; }
    private long t1, t2;
    protected readonly Stopwatch sw;
    private const long MILLISECONDS_FOR_RESET = 1000;
    private long startFPSTime;
    private long lastReset = 0;
    private double avgFPS = 0.0, minFPS = double.PositiveInfinity, maxFPS = 0.0;
    public long frameCount { get; private set; }

    // Engine Fields
    public IntPtr Handle => window.Handle;
    public bool Running { get; private set; }
    private Action Setup, Start, Update, UserInput;
    public const double DEG2RAD = Math.PI / 180.0;
    public bool Focused { get; private set; }
    public readonly Input input;
    private Thread renderThread, controlsThread, debugThread;
    private bool printMessages = true;
    private static Queue<string> messages = new Queue<string>();
    protected readonly Vector3 LowerAtmosphere = new(0.0f, 0.0f, 0.0f);
    protected readonly Vector3 UpperAtmosphere = new Vector3(1.0f, 1.0f, 1.0f) * 0.0f;
    public readonly List<Gameobject> gameobjects = new List<Gameobject>();
    public readonly List<Light> lights = new List<Light>();
    public Vector3 EyePos;
    public Vector3 EyeRot;
    public Vector3 EyeStartPos;
    public Vector3 EyeStartRot;
    public float DepthBias = 0.0f;
    public float NormalBias = 0.0f;
    public bool Line = true;

    public Engine(EngineDescription desc)
    {
        print("Construction");
        Description = desc;
        Setup = desc.Setup;
        Start = desc.Start;
        Update = desc.Update;
        UserInput = desc.UserInput;
        vertexShaderEntry = "vertexShader";
        pixelShaderEntry = "pixelShader";
        inputElements = new InputElementDescription[]
        {
            new("POSITION", 0, Format.R32G32B32_Float, 0, 0, InputClassification.PerVertexData, 0),
            new("NORMAL", 0, Format.R32G32B32_Float, 12, 0, InputClassification.PerVertexData, 0),
        };

        sw = new();
        sw.Start();

        Application.EnableVisualStyles();

        window = new()
        {
            Text = "DXRenderEngine",
            ClientSize = new(desc.Width, desc.Height),
            FormBorderStyle = FormBorderStyle.Fixed3D,
            BackColor = System.Drawing.Color.Black,
            WindowState = desc.WindowState,
            StartPosition = FormStartPosition.Manual,
            Location = new System.Drawing.Point(630, 250)
        };

        input = new(window.Handle, this, UserInput);

        window.Load += Window_Load;
        window.Shown += Window_Shown;
        window.GotFocus += GotFocus;
        window.LostFocus += LostFocus;
        Focused = window.Focused;
        window.Resize += Window_Resize;
        window.FormClosing += Closing;
    }

    internal void SetShaderCode(string code)
    {
        shaderCode = code;
    }

    public void Run()
    {
        print("Running");
        Application.Run(window);
        printMessages = false;
        debugThread?.Join();
    }

    protected virtual void InitializeDeviceResources()
    {
        DeviceCreationFlags flags = DeviceCreationFlags.None;
        bool debug = false;
#if DEBUG
        flags |= DeviceCreationFlags.Debug;
        debug = true;
#endif
        D3D11.D3D11CreateDevice(null, DriverType.Hardware, flags, new FeatureLevel[] { FeatureLevel.Level_11_1 }, out ID3D11Device device0);
        device = new(device0.NativePointer);
        context = device.ImmediateContext3;
        DXGI.CreateDXGIFactory2(debug, out factory);
        adapter = new(factory.GetAdapter(0).NativePointer);

        SwapChainDescription1 scd = new(Width, Height/*, Format.R8G8B8A8_UNorm*/);
        swapChain = new(factory.CreateSwapChainForHwnd(device, window.Handle, scd).NativePointer);

        AssignRenderTarget();

        UpdateShaderConstants();

        InitializeShaders();

        SetConstantBuffers();
    }

    private void AssignRenderTarget()
    {
        using (ID3D11Texture2D1 backBuffer = swapChain.GetBuffer<ID3D11Texture2D1>(0))
            renderTargetView = device.CreateRenderTargetView1(backBuffer);
        screenViewport = new(0, 0, Width, Height);
        context.RSSetViewport(screenViewport);
    }

    protected virtual void InitializeVertices()
    {
        PackVertices();

        BufferDescription bd = new(vertices.Length * Marshal.SizeOf<VertexPositionNormal>(), BindFlags.VertexBuffer);
        vertexBuffer = device.CreateBuffer(vertices, bd);
        VertexBufferView view = new(vertexBuffer, Marshal.SizeOf<VertexPositionNormal>());
        context.IASetVertexBuffer(0, view);
    }

    protected virtual void PackVertices()
    {
        // need to calculate vertex count first.
        int vertexCount = 0;
        for (int i = 0; i < gameobjects.Count; i++)
        {
            gameobjects[i].Offset = vertexCount;
            vertexCount += gameobjects[i].Triangles.Length * 3;
        }
        vertices = new VertexPositionNormal[vertexCount];

        // then pack all the vertices
        for (int i = 0; i < gameobjects.Count; i++)
        {
            for (int j = 0; j < gameobjects[i].Triangles.Length; j++)
            {
                vertices[gameobjects[i].Offset + j * 3] = gameobjects[i].Triangles[j].GetVertexPositionNormalColor(0);
                vertices[gameobjects[i].Offset + j * 3 + 1] = gameobjects[i].Triangles[j].GetVertexPositionNormalColor(1);
                vertices[gameobjects[i].Offset + j * 3 + 2] = gameobjects[i].Triangles[j].GetVertexPositionNormalColor(2);
            }
        }
    }

    protected virtual void UpdateShaderConstants()
    {
        if (lights.Count == 0)
            lights.Add(new());
        if (gameobjects.Count == 0)
            gameobjects.Add(new());
    }

    protected void ChangeShader(string target, string newValue)
    {
        int index = shaderCode.IndexOf(target);
        if (index == -1)
        {
            throw new(target + " does not exist in shaderCode");
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
        if (Description.UseShaderCache && File.Exists(vertexShaderFile))
        {
            shaderBlob = Compiler.ReadFileToBlob(vertexShaderFile);
        }
        else
        {
            Compiler.Compile(shaderCode, null, null, vertexShaderEntry, "VertexShader", "vs_5_0", sf, out shaderBlob, out Blob errorCode);
            if (shaderBlob == null)
                throw new("HLSL vertex shader compilation error:\r\n" + Encoding.ASCII.GetString(errorCode.GetBytes()));

            errorCode?.Dispose();
            Compiler.WriteBlobToFile(shaderBlob, vertexShaderFile, true);
        }

        vertexShader = device.CreateVertexShader(shaderBlob);

        inputLayout = device.CreateInputLayout(inputElements, shaderBlob);
        context.IASetInputLayout(inputLayout);
        context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);

        shaderBlob.Dispose();

        if (Description.UseShaderCache && File.Exists(pixelShaderFile))
        {
            shaderBlob = Compiler.ReadFileToBlob(pixelShaderFile);
        }
        else
        {
            Compiler.Compile(shaderCode, null, null, pixelShaderEntry, "PixelShader", "ps_5_0", sf, out shaderBlob, out Blob errorCode);
            if (shaderBlob == null)
                throw new("HLSL pixel shader compilation error:\r\n" + Encoding.ASCII.GetString(errorCode.GetBytes()));

            errorCode?.Dispose();
            Compiler.WriteBlobToFile(shaderBlob, pixelShaderFile, true);
        }

        pixelShader = device.CreatePixelShader(shaderBlob);

        shaderBlob.Dispose();

        context.VSSetShader(vertexShader);
        context.PSSetShader(pixelShader);
    }

    protected virtual void SetConstantBuffers()
    {
        EyeStartPos = EyePos;
        EyeStartRot = EyeRot;
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

        // reset counters
        if (sw.ElapsedMilliseconds / MILLISECONDS_FOR_RESET > lastReset)
        {
            avgFPS = 10000000.0 * frameCount / (sw.ElapsedTicks - startFPSTime);
            startFPSTime = sw.ElapsedTicks;
            string text = "DXRenderEngine   " + avgFPS.ToString("G4") +
            "fps (" + minFPS.ToString("G4") + "fps, " + maxFPS.ToString("G4") + "fps)";
            window.BeginInvoke(new(() => window.Text = text));
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

    // subclasses should always dispose this last
    protected virtual void Dispose(bool boolean)
    {
        Stop();
        input.Dispose();
        inputLayout.Dispose();
        pixelShader.Dispose();
        vertexShader.Dispose();
        vertexBuffer.Dispose();
        renderTargetView.Dispose();
        swapChain.Dispose();
        context.Dispose();
        output?.Dispose();
        adapter.Dispose();
        device.Dispose();
        window.Dispose();
    }

    private void GetRefCounts()
    {
        Type type = GetType();
        uint total = 0;

        print("Directx objects:");
        foreach (var mem in type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
        {
            string mType = mem.FieldType.ToString();
            if (mType.Contains("IDXGI") || mType.Contains("ID3D11"))
            {
                var dxObj = mem.GetValue(this);

                if (dxObj is null)
                {
                    continue;
                }
                else if (dxObj is SharpGen.Runtime.ComObject)
                {
                    SharpGen.Runtime.ComObject comObject = (SharpGen.Runtime.ComObject)dxObj;
                    if (comObject.NativePointer == IntPtr.Zero)
                    {
                        continue;
                    }
                    comObject.AddRef();
                    uint count = comObject.Release();
                    total += count;
                    print(mem.Name + " Ref#: " + count);
                }
                else if (dxObj is Array && ((Array)dxObj).GetValue(0) is SharpGen.Runtime.ComObject)
                {
                    Array arr = (Array)dxObj;
                    if (arr == null)
                    {
                        continue;
                    }
                    for (int i = 0; i < arr.Length; i++)
                    {
                        SharpGen.Runtime.ComObject comObject = (SharpGen.Runtime.ComObject)arr.GetValue(i);
                        if (comObject.NativePointer == IntPtr.Zero)
                        {
                            continue;
                        }
                        comObject.AddRef();
                        uint count = comObject.Release();
                        total += count;
                        print(mem.Name + "[" + i + "] Ref#: " + count);
                    }
                }
            }
        }
        print("Total Refs: " + total);
    }

    /////////////////////////////////////

    private void RenderLoop()
    {
        while (Running)
        {
            long c1 = sw.ElapsedTicks;
            GetTime();
            if (!Focused)
                continue;
            long c2 = sw.ElapsedTicks;
            Render();
            long c3 = sw.ElapsedTicks;
            Update();
            long c4 = sw.ElapsedTicks;
            PerFrameUpdate();
            long c5 = sw.ElapsedTicks;
            swapChain.Present(swapChain.IsFullscreen ? 1 : 0);
            long c6 = sw.ElapsedTicks;
            //print("GetTime:" + (c2 - c1) + " Render:" + (c3 - c2) + " Update:" + (c4 - c3) + " Frame:" + (c5 - c4) + " Present:" + (c6 - c5));
        }
    }

    private void DebugLoop()
    {
        long t1 = sw.ElapsedTicks;
        long t2 = t1;
        while (printMessages)
        {
            // frame limiter
            while (10000000.0 / (t2 - t1) > 144.0)
            {
                t2 = sw.ElapsedTicks;
            }

            // message printer
            if (messages.Count > 0)
            {
                string output = "";
                while (messages.Count > 0)
                {
                    output += messages.Dequeue() + "\n";
                }
                Debug.Write(output);
            }
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
        // objects can only move in between frames
        for (int i = 0; i < gameobjects.Count; i++)
        {
            gameobjects[i].CreateMatrices();
        }
    }

    protected virtual void PerLightUpdate(int index)
    {

    }

    protected virtual void PerObjectUpdate(int index)
    {

    }

    protected virtual void Render()
    {
        context.ClearRenderTargetView(renderTargetView, Colors.Black);
        context.OMSetRenderTargets(renderTargetView);
        context.Draw(vertices.Length, 0);
    }

    /////////////////////////////////////

    private void Window_Load(object sender, EventArgs e)
    {
        print("Loading");
        if (shaderCode == null)
        {
            throw new("shader code is null");
        }
        Running = true;
        Setup();
        InitializeDeviceResources();
        GetDisplayRefreshRate();
        InitializeVertices();
        Start();
        PerApplicationUpdate();
    }

    private void Window_Shown(object sender, EventArgs e)
    {
        print("Shown");
        input.InitializeInputs();

        controlsThread = new(() => input.ControlLoop());
        renderThread = new(() => RenderLoop());
#if DEBUG
        debugThread = new(() => DebugLoop());
#endif

        GC.Collect();

        t1 = startFPSTime = sw.ElapsedTicks;
        renderThread.Start();
        controlsThread.Start();
        debugThread?.Start();
    }

    private void Closing(object sender, FormClosingEventArgs e)
    {
        print("Closing");
        Running = false;
        renderThread.Join();
        controlsThread.Join();

        Dispose();
    }

    private void LostFocus(object sender, EventArgs e)
    {
        Focused = false;
    }

    private void GotFocus(object sender, EventArgs e)
    {
        Focused = true;
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

    private void GetDisplayRefreshRate()
    {
        int num = 0;
        int main = -1;
        List<IDXGIOutput> outputs = new List<IDXGIOutput>();
        while (true)
        {
            outputs.Add(adapter.GetOutput(num));
            if (output == null)
                break;
            var modes = output.GetDisplayModeList(Format.R8G8B8A8_UNorm, DisplayModeEnumerationFlags.DisabledStereo);
            foreach (var mode in modes)
            {
                float temp = mode.RefreshRate.Numerator / (float)mode.RefreshRate.Denominator;
                if (temp > displayRefreshRate)
                {
                    displayRefreshRate = temp;
                    main = num;
                }
            }
            num++;
            output.Release();
        }

        for (int i = 0; i < outputs.Count; i++)
        {
            if (i == main)
            {
                output = outputs[i];
            }
            else
            {
                outputs[i].Release();
            }
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
        float cos = (float)Math.Cos(-angle * DEG2RAD);
        float sin = (float)Math.Sin(-angle * DEG2RAD);

        Matrix4x4 result = Matrix4x4.Identity;
        result.M11 = cos;
        result.M12 = sin;
        result.M21 = -sin;
        result.M22 = cos;

        return result;
    }

    public static Matrix4x4 CreateRotation(Vector3 rot)
    {
        Matrix4x4 rotx = CreateRotationX(rot.X);
        Matrix4x4 roty = CreateRotationY(rot.Y);
        Matrix4x4 rotz = CreateRotationZ(rot.Z);
        return rotz * rotx * roty;
    }

    public static Matrix4x4 CreateWorld(Vector3 pos, Vector3 rot, Vector3 sca)
    {
        Matrix4x4 position = Matrix4x4.CreateTranslation(pos);
        Matrix4x4 rotation = CreateRotation(rot);
        Matrix4x4 scale = Matrix4x4.CreateScale(sca);

        return scale * rotation * position;
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

    public static Matrix4x4 CreateProjection(float fovVDegrees, float aspectRatioHW, float nearPlane, float farPlane)
    {
        float fFovRad = 1.0f / (float)Math.Tan(fovVDegrees * 0.5f * DEG2RAD);
        Matrix4x4 mat = new();
        mat.M11 = fFovRad;
        mat.M22 = aspectRatioHW * fFovRad;
        mat.M33 = farPlane / (farPlane - nearPlane);
        mat.M43 = (-farPlane * nearPlane) / (farPlane - nearPlane);
        mat.M34 = 1.0f;
        return mat;
    }

    public static void print(object message)
    {
#if DEBUG
        messages.Enqueue(DateTime.Now.Ticks + ": " + message.ToString());
#endif
    }

    public static void print()
    {
        print("");
    }
}
