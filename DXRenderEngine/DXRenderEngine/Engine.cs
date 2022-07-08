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
using static DXRenderEngine.Helpers;

namespace DXRenderEngine;

/// <summary>
/// TODO
///     full screen
/// </summary>

public class Engine : IDisposable
{
    public readonly EngineDescription Description;
    protected Form window;
    private IDXGIFactory5 factory;
    private IDXGIAdapter4 adapter;
    private IDXGIOutput output;
    protected ID3D11Device5 device;
    protected ID3D11DeviceContext3 context;
    protected IDXGISwapChain4 swapChain;

    protected InputElementDescription[] inputElements;
    protected ID3D11InputLayout inputLayout;
    protected VertexPositionNormal[] vertices;
    protected ID3D11Buffer vertexBuffer;

    protected ConstantBuffer[] cBuffers;
    protected ID3D11Buffer[] buffers;
    private Dictionary<string, int> hlslDefMap = new();
    private Dictionary<string, int> hlslTypeMap = new()
    {
        {"matrix", 64},
        {"float3x3", 48},
        {"float4", 16},
        {"float3", 12},
        {"float2", 8},
        {"float", 4},
        {"uint", 4},
        {"int", 4},
        {"bool", 4}
    };

    protected ID3D11Texture2D1 renderTargetBuffer;
    protected ID3D11RenderTargetView1 renderTargetView;
    protected ID3D11VertexShader vertexShader;
    protected ID3D11PixelShader pixelShader;
    protected Viewport screenViewport;

    protected string shaderCode;
    protected readonly string vertexShaderEntry;
    protected readonly string pixelShaderEntry;

    protected float displayRefreshRate { get; private set; }
    public int Width => Description.Width;
    public int Height => Description.Height;

    // Time Fields

    // Represents the time since the last frame
    public double frameTime { get; private set; }
    // Represents the time since the last input update
    public double ElapsedTime => input.ElapsedTime;
    private long t1, t2;
    protected static readonly Stopwatch sw = new();
    private const long MILLISECONDS_FOR_RESET = 1000;
    private long startFPSTime;
    private long lastReset = 0;
    private double avgFPS = 0.0, minFPS = double.PositiveInfinity, maxFPS = 0.0;
    public long frameCount { get; private set; }

    // Engine Fields
    public IntPtr Handle => window.Handle;
    public bool Running { get; private set; }
    public readonly bool HasShader;
    private bool printing = true;
    private Action Setup, Start, UserInput;
    protected Action Update { get; private set; }
    public bool Focused { get; private set; }
    public readonly Input input;
    private Thread renderThread, controlsThread, debugThread;
    private static Queue<string> messages = new Queue<string>();
    protected readonly Vector3 LowerAtmosphere = new(0.0f, 0.0f, 0.0f);
    protected readonly Vector3 UpperAtmosphere = new Vector3(1.0f, 1.0f, 1.0f) * 0.0f;
    public readonly List<Gameobject> gameobjects = new List<Gameobject>();
    public readonly List<Light> lights = new List<Light>();
    public Vector3 EyePos;
    public Vector3 EyeRot;
    public Vector3 EyeStartPos;
    public Vector3 EyeStartRot;

    public Engine(EngineDescription desc)
    {
        sw.Restart();

#if DEBUG
        debugThread = new(() => DebugLoop());
        debugThread.Start();
#endif

        print("Construction");
        Description = desc;
        Setup = desc.Setup;
        Start = desc.Start;
        Update = desc.Update;
        UserInput = desc.UserInput;

        HasShader = desc.ShaderResource.Length != 0;

        if (HasShader)
        {
            vertexShaderEntry = "vertexShader";
            pixelShaderEntry = "pixelShader";

            int endNamesp = desc.ShaderResource.IndexOf('.');
            string namesp = desc.ShaderResource.Substring(0, endNamesp);
            Assembly assembly = Assembly.Load(namesp);
            using (Stream stream = assembly.GetManifestResourceStream(desc.ShaderResource))
            using (StreamReader reader = new(stream))
            {
                shaderCode = reader.ReadToEnd();
            }

            inputElements = new InputElementDescription[]
            {
                new("POSITION", 0, Format.R32G32B32_Float, 0, 0, InputClassification.PerVertexData, 0),
                new("NORMAL", 0, Format.R32G32B32_Float, 12, 0, InputClassification.PerVertexData, 0),
            };
        }
        else
        {
            print("No shader");
        }

        Application.EnableVisualStyles();

        window = new()
        {
            Text = "DXRenderEngine",
            ClientSize = new(desc.Width, desc.Height),
            FormBorderStyle = FormBorderStyle.Fixed3D,
            BackColor = System.Drawing.Color.Black,
            WindowState = desc.WindowState,
            StartPosition = FormStartPosition.CenterScreen
        };

        if (desc.UserInput != null)
        {
            input = new(window.Handle, this, UserInput);
        }

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
        print("Running");
        Application.Run(window);
        printing = false;
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

        SwapChainDescription1 scd = new(Width, Height, Format.R8G8B8A8_UNorm);
        swapChain = new(factory.CreateSwapChainForHwnd(device, window.Handle, scd).NativePointer);

        AssignRenderTarget();

        UpdateShaderConstants();

        InitializeShaders();

        long time1 = sw.ElapsedMilliseconds;
        ParseConstantBuffers();
        print("Buffer parsing time=" + (sw.ElapsedMilliseconds - time1) + "ms");

        SetConstantBuffers();
    }

    private void AssignRenderTarget()
    {
        renderTargetBuffer?.Dispose();
        renderTargetBuffer = swapChain.GetBuffer<ID3D11Texture2D1>(0);
        renderTargetView = device.CreateRenderTargetView1(renderTargetBuffer);
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
        for (int i = 0; i < gameobjects.Count; ++i)
        {
            gameobjects[i].Offset = vertexCount;
            vertexCount += gameobjects[i].Triangles.Length * 3;
        }
        vertices = new VertexPositionNormal[vertexCount];

        // then pack all the vertices
        for (int i = 0; i < gameobjects.Count; ++i)
        {
            for (int j = 0; j < gameobjects[i].Triangles.Length; ++j)
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

    protected void ParseConstantBuffers()
    {
        // get all the buffers
        List<int> indices = new();
        int index = 0;
        while (true)
        {
            int before = index;
            index = shaderCode.IndexOf("cbuffer", index + 1);
            if (index == -1)
                break;
            indices.Add(index);
        }

        cBuffers = new ConstantBuffer[indices.Count];

        if (indices.Count > 0)
        {
            // get any hlsl defs in case arrays use them in buffers
            GetDefs();

            // get all the items in each buffer
            for (int i = 0; i < indices.Count; ++i)
            {
                cBuffers[i] = new ConstantBuffer(ParseBlock(indices[i]));
            }
        }
    }

    private void GetDefs()
    {
        int defIndex = shaderCode.IndexOf("#define");
        while (defIndex != -1)
        {
            int startName = FindNot(shaderCode, ' ', Find(shaderCode, ' ', defIndex));
            int endName = Find(shaderCode, ' ', startName);
            int startVal = FindNot(shaderCode, ' ', endName);

            // raw number
            if (char.IsDigit(shaderCode[startVal]))
            {
                string name = shaderCode.Substring(startName, endName - startName);
                int endVal = Find(shaderCode, " \r\n", startVal);
                string val = shaderCode.Substring(startVal, endVal - startVal);
                char typeEnding = shaderCode[endVal - 1];

                // int
                if (typeEnding == 'u')
                {
                    hlslDefMap.Add(name, (int)uint.Parse(val.Substring(0, val.Length - 1)));
                }
                // uint
                else if (char.IsDigit(typeEnding))
                {
                    hlslDefMap.Add(name, int.Parse(val));
                }
            }
            // ignore all other types

            // find the next macro
            defIndex = shaderCode.IndexOf("#define", defIndex + 1);
        }
    }

    private int[] ParseBlock(int blockLocation)
    {
        int startBlock = shaderCode.IndexOf('{', blockLocation);
        int endBlock = shaderCode.IndexOf('}', startBlock);
        string block = shaderCode.Substring(startBlock, endBlock - startBlock);

        List<int> sizes = new();
        int nextIndex = FindNot(block, " \t\r\n", 1);
        while (true)
        {
            int startType = nextIndex;
            if (startType == -1)
            {
                break;
            }
            else if (block[startType] == '/')
            {
                // skip comments
                nextIndex = FindNot(block, " \t\r\n", Find(block, "\r\n", nextIndex));
                continue;
            }
            int endType = Find(block, ' ', startType);
            int startName = FindNot(block, ' ', endType);
            nextIndex = FindNot(block, " \t\r\n", Find(block, ';', startName) + 1);
            string type = block.Substring(startType, endType - startType);

            int openBracket = Find(block, '[', startName);
            int arraySize = 1;

            // if there is an array
            if (openBracket != -1 && (openBracket < nextIndex || nextIndex == -1))
            {
                int closeBracket = Find(block, ']', openBracket);
                string arraySizeString = block.Substring(openBracket + 1, closeBracket - openBracket - 1);

                // literal
                if (char.IsDigit(arraySizeString[0]))
                {
                    // uint for array size
                    if (arraySizeString.EndsWith('u'))
                    {
                        arraySizeString = arraySizeString.Substring(0, arraySizeString.Length - 1);
                    }

                    arraySize = int.Parse(arraySizeString);
                }
                // def
                else if(!hlslDefMap.TryGetValue(arraySizeString, out arraySize))
                {
                    throw new Exception(string.Format("\"{}\" is not defined in HLSL shader", arraySizeString));
                }
            }

            // Get the size of the element
            if (!hlslTypeMap.TryGetValue(type, out int size))
            {
                int newStruct = shaderCode.IndexOf("struct " + type);
                if (newStruct == -1)
                {
                    throw new KeyNotFoundException(string.Format("\"{}\" is not a valid type", type));
                }

                // recursively link through structs until a primitive type is found
                int[] structSizes = ParseBlock(newStruct);

                // Calculate the packed-size of the struct.
                size = 0;
                int modSize = 0;
                foreach (var structSize in structSizes)
                {
                    int newSize = modSize + structSize;
                    int newModSize = newSize & PackMask;
                    if (newSize > Pack && newModSize > 0)
                    {
                        size += Pack - modSize;
                        newModSize = structSize & PackMask;
                    }
                    modSize = newModSize;
                    size += structSize;
                }
                size = (size + PackMask) & ~PackMask;

                // add the new struct to the type map
                hlslTypeMap.Add(type, size);
            }

            // add the object to the list of objects
            if (arraySize > 1)
            {
                for (int i = 0; i < arraySize; ++i)
                    sizes.Add(size);
            }
            else
            {
                sizes.Add(size);
            }
        }

        return sizes.ToArray();
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

        buffers = new ID3D11Buffer[cBuffers.Length];

        BufferDescription bd = new(0, BindFlags.ConstantBuffer, ResourceUsage.Dynamic, CpuAccessFlags.Write);
        for (int i = 0; i < cBuffers.Length; ++i)
        {
            bd.SizeInBytes = cBuffers[i].GetSize();
            buffers[i] = device.CreateBuffer(bd);
        }
    }

    protected void UpdateConstantBuffer(int index, MapMode mode = MapMode.WriteNoOverwrite)
    {
        MappedSubresource resource = context.Map(buffers[index], mode);
        cBuffers[index].Copy(resource.DataPointer);
        context.Unmap(buffers[index]);
    }

    protected void GetTime()
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
            window.ClientSize = new System.Drawing.Size(Description.Width, Description.Height);
        }
        else
        {
            window.ClientSize = Screen.PrimaryScreen.Bounds.Size;
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

    // subclasses should always dispose this last and never assume that anything will be set
    protected virtual void Dispose(bool boolean)
    {
        Stop();
        if (buffers != null)
            foreach (var buffer in buffers)
                buffer?.Dispose();
        input?.Dispose();
        inputLayout?.Dispose();
        pixelShader?.Dispose();
        vertexShader?.Dispose();
        vertexBuffer?.Dispose();
        renderTargetBuffer?.Dispose();
        renderTargetView?.Dispose();
        swapChain?.Dispose();
        context?.Dispose();
        output?.Dispose();
        adapter?.Dispose();
        device?.Dispose();
        window?.Dispose();
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
                    for (int i = 0; i < arr.Length; ++i)
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

    protected virtual void RenderFlow()
    {
        //long c1 = sw.ElapsedTicks;
        GetTime();
        if (!Focused)
            return;
        //long c2 = sw.ElapsedTicks;
        Render();
        //long c3 = sw.ElapsedTicks;
        Update();
        //long c4 = sw.ElapsedTicks;
        PerFrameUpdate();
        //long c5 = sw.ElapsedTicks;
        swapChain.Present(swapChain.IsFullscreen ? 1 : 0);
        //long c6 = sw.ElapsedTicks;
        //print("GetTime:" + (c2 - c1) + " Render:" + (c3 - c2) + " Update:" + (c4 - c3) + " Frame:" + (c5 - c4) + " Present:" + (c6 - c5));
    }

    private void DebugLoop()
    {
        long t1 = sw.ElapsedTicks;
        long t2 = t1;
        while (printing)
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
            l.GenerateMatrix();
    }

    protected virtual void PerFrameUpdate()
    {
        // objects can only move in between frames
        foreach (var go in gameobjects)
            go.CreateMatrices();
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
        Running = true;
        Setup();
        if (HasShader)
        {
            InitializeDeviceResources();
            GetDisplayRefreshRate();
            InitializeVertices();
        }
        Start();
        PerApplicationUpdate();
    }

    private void Window_Shown(object sender, EventArgs e)
    {
        print("Shown");

        if (input != null)
        {
            input.InitializeInputs();
            controlsThread = new(() => input.ControlLoop());
        }

        if (HasShader)
        {
            renderThread = new(() =>
            {
                t1 = startFPSTime = sw.ElapsedTicks;
                while (Running)
                {
                    RenderFlow();
                }
            });
        }

        GC.Collect();

        renderThread?.Start();
        controlsThread?.Start();
    }

    private void Closing(object sender, FormClosingEventArgs e)
    {
        print("Closing");
        Running = false;
        renderThread?.Join();
        controlsThread?.Join();

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
        if (!Description.Resizeable || !HasShader) return;

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

        for (int i = 0; i < outputs.Count; ++i)
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

    [Conditional("DEBUG")]
    public static void print(object message = null)
    {
        messages.Enqueue(sw.ElapsedTicks + ": " + message?.ToString());
    }
}
