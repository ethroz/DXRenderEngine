using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using static DXRenderEngine.Helpers;
using static DXRenderEngine.Logger;
using static DXRenderEngine.Time;

namespace DXRenderEngine;

/// <summary>
/// TODO
///     Initializing shaders screen -> fixes input device errors?
///     multithreaded compiling
///     frame stutter -> DX12U
/// </summary>

public class Engine : IDisposable
{
    // DirectX fields
    public readonly string Name;
    public readonly EngineDescription Description;
    protected Form window;
    private IDXGIFactory5 factory;
    private IDXGIAdapter4 adapter;
    private IDXGIOutput display;
    protected ID3D11Device5 device;
    protected ID3D11DeviceContext3 context;
    protected IDXGISwapChain4 swapChain;

    protected InputElementDescription[] inputElements;
    protected ID3D11InputLayout inputLayout;
    protected VertexPositionNormal[] vertices;
    protected ID3D11Buffer vertexBuffer;

    protected ConstantBuffer[] cBuffers;
    protected ID3D11Buffer[] buffers;
    protected ID3D11SamplerState[] samplers;
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

    protected ID3D11RenderTargetView1 renderTargetView;
    protected ID3D11VertexShader vertexShader;
    protected ID3D11PixelShader pixelShader;
    protected Viewport screenViewport;

    protected readonly string exeLocation;
    protected string shaderCode;
    protected readonly string vertexShaderEntry;
    protected readonly string pixelShaderEntry;

    protected float displayRefreshRate { get; private set; }
    public int Width => Description.Width;
    public int Height => Description.Height;

    // Time Fields

    // Represents the time since the last frame
    public double RenderTime { get; private set; }
    // Represents the time since the last input update
    public double ElapsedTime => input.ElapsedTime;
    private long t1, t2;
    internal protected const int UNFOCUSED_TIMEOUT = 50;
    internal const long STATS_DUR = 1000L;
    private long startFPSTime;
    private long lastReset = 0;
    private double avgFPS = 0.0, minFPS = double.PositiveInfinity, maxFPS = 0.0;
    private long frameCount;

    // Engine Fields
    public IntPtr Handle => window.Handle;
    public bool Running { get; private set; }
    public bool IsDisposed { get; private set; }
    public readonly bool HasShader;
    public bool ShadersReady { get; private set; }
    private Action Setup => Description.Setup;
    private Action Start => Description.Start;
    protected Action Update => Description.Update;
    public bool Focused { get; private set; }
    public bool FullScreen 
    {
        get => Description.FullScreen;
        protected set => Description.FullScreen = value;
    }
    public readonly Input input;
    internal double inputAvgFPS = 0.0;
    internal double inputAvgSleep = 0.0;
    protected Thread uiThread, controlThread, renderThread;
    protected List<Task> compilerTasks = new();
    protected volatile Queue<Action> commands = new();
    protected readonly Vector3 LowerAtmosphere = new(0.0f, 0.0f, 0.0f);
    protected readonly Vector3 UpperAtmosphere = new Vector3(1.0f, 1.0f, 1.0f) * 0.0f;
    public readonly List<Gameobject> gameobjects = new();
    public readonly List<Light> lights = new();
    public Vector3 EyePos;
    public Vector3 EyeRot;
    public Vector3 EyeStartPos;
    public Vector3 EyeStartRot;

    // Methods
    public void Run()
    {
        print("Running");
        Application.Run(window);
    }

    public void ToggleFullscreen()
    {
        // Make sure we are the UI thread.
        if (Thread.CurrentThread != uiThread)
        {
            // Queue the command to run when we are not rendering
            commands.Enqueue(() => { window.Invoke(ToggleFullscreen); });
            return;
        }

        if (FullScreen)
        {
            FullScreen = false;

            SetRenderBuffers(Description.Width, Description.Height);
            window.ClientSize = new(Description.Width, Description.Height);
            window.FormBorderStyle = FormBorderStyle.Fixed3D;
            window.Location = new(Description.X, Description.Y);
        }
        else
        {
            FullScreen = true;

            SetRenderBuffers(Screen.PrimaryScreen.Bounds.Size.Width, Screen.PrimaryScreen.Bounds.Size.Height);
            window.FormBorderStyle = FormBorderStyle.None;
            window.ClientSize = new(Screen.PrimaryScreen.Bounds.Size.Width, Screen.PrimaryScreen.Bounds.Size.Height);
            window.Location = new();
        }
    }

    public void Stop()
    {
        if (Running)
        {
            if (Thread.CurrentThread != uiThread)
                window.BeginInvoke(window.Close);
            else
                window.Close();
        }
    }

    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /////////////////////////////////////

    protected internal Engine(EngineDescription desc)
    {
        print("Construction");
        Description = desc;
        IsDisposed = false;

        Name = GetType().Name;
        exeLocation = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar;

        HasShader = desc.ShaderResource.Length != 0;
        ShadersReady = false;

        if (HasShader)
        {
            vertexShaderEntry = "vertexShader";
            pixelShaderEntry = "pixelShader";
            GetEmbeddedShader();
            SetInputElements();
        }
        else
        {
            print("Continuing with no shader");
        }

        ConstructWindow();

        if (desc.UserInput != null)
        {
            input = new(window.Handle, this, desc.UserInput);
        }

        window.Load += Window_Load;
        window.Shown += Window_Shown;
        window.GotFocus += GotFocus;
        window.LostFocus += LostFocus;
        Focused = window.Focused;
        window.FormClosing += Closing;
    }

    protected virtual void GetEmbeddedShader()
    {
        int endNamesp = Description.ShaderResource.IndexOf('.');
        string namesp = Description.ShaderResource.Substring(0, endNamesp);
        Assembly assembly = Assembly.Load(namesp);
        using (Stream stream = assembly.GetManifestResourceStream(Description.ShaderResource))
        using (StreamReader reader = new(stream))
        {
            shaderCode = reader.ReadToEnd();
        }
    }

    protected virtual void SetInputElements()
    {
        inputElements = new InputElementDescription[]
        {
            new("POSITION", 0, Format.R32G32B32_Float, 0, 0, InputClassification.PerVertexData, 0),
            new("NORMAL", 0, Format.R32G32B32_Float, 12, 0, InputClassification.PerVertexData, 0),
        };
    }

    protected virtual void ConstructWindow()
    {
        window = new()
        {
            Text = Name,
            ClientSize = new(Description.Width, Description.Height),
            FormBorderStyle = FormBorderStyle.Fixed3D,
            BackColor = System.Drawing.Color.Black,
            WindowState = Description.WindowState,
            MaximizeBox = false
        };

        if (Description.X == -1 || Description.Y == -1)
        {
            window.StartPosition = FormStartPosition.CenterScreen;

            // Save the window position upon showing
            commands.Enqueue(() =>
            {
                Description.X = window.Location.X;
                Description.Y = window.Location.Y;
            });
        }
        else
        {
            window.StartPosition = FormStartPosition.Manual;
            window.Location = new System.Drawing.Point(Description.X, Description.Y);
        }
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

        SetRenderBuffers(Width, Height);
    }

    protected virtual void SetRenderBuffers(int width, int height)
    {
        if (renderTargetView != null)
        {
            ReleaseRenderBuffers();
            swapChain.ResizeBuffers(0, width, height);
        }

        using (var buffer = swapChain.GetBuffer<ID3D11Texture2D1>(0))
            renderTargetView = device.CreateRenderTargetView1(buffer);
        screenViewport = new(0, 0, width, height);
        context.RSSetViewport(screenViewport);
    }

    protected virtual void ReleaseRenderBuffers()
    {
        context.UnsetRenderTargets();
        renderTargetView.Release();
    }

    protected virtual void InitializeVertices()
    {
        PackVertices();

        BufferDescription bd = new(vertices.Length * Marshal.SizeOf<VertexPositionNormal>(), BindFlags.VertexBuffer);
        vertexBuffer = device.CreateBuffer<VertexPositionNormal>(vertices, bd);
        context.IASetVertexBuffer(0, vertexBuffer, Marshal.SizeOf<VertexPositionNormal>());
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

    protected void ModifyShaderCode(string target, string newValue)
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
            // get all the items in each buffer
            for (int i = 0; i < indices.Count; ++i)
            {
                cBuffers[i] = new ConstantBuffer(ParseBlock(indices[i]));
            }
        }
    }

    protected void ParseSamplers()
    {
        // get the number of samplers
        int count = 0;
        int index = 0;
        while (true)
        {
            int startType = shaderCode.IndexOf("SamplerState", index + 1);
            if (startType == -1)
            {
                startType = shaderCode.IndexOf("SamplerComparisonState", index + 1);
                if (startType == -1)
                    break;
            }

            index = startType;
            int endType = Find(shaderCode, ' ', startType);
            int startName = FindNot(shaderCode, ' ', endType);
            int endName = Find(shaderCode, ' ', startName);
            Trace.Assert(endName != -1);

            int openBracket = Find(shaderCode, '[', startName);
            int arraySize = 1;

            // if there is an array
            if (openBracket != -1 && openBracket < endName)
            {
                int closeBracket = Find(shaderCode, ']', openBracket);
                string arraySizeString = shaderCode.Substring(openBracket + 1, closeBracket - openBracket - 1);

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
                else if (!hlslDefMap.TryGetValue(arraySizeString, out arraySize))
                {
                    throw new Exception(string.Format("\"{}\" is not defined in HLSL shader", arraySizeString));
                }
            }

            count += arraySize;
        }

        samplers = new ID3D11SamplerState[count];
    }

    protected virtual void InitializeShaders()
    {
        AsyncGetShader(vertexShaderEntry, "VertexShader", "vs_5_0", 
            (Blob b) => 
            {
                vertexShader = device.CreateVertexShader(b);

                inputLayout = device.CreateInputLayout(inputElements, b);
                context.IASetInputLayout(inputLayout);
                context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
                
                context.VSSetShader(vertexShader);
            });

        AsyncGetShader(pixelShaderEntry, "PixelShader", "ps_5_0", 
            (Blob b) => 
            {
                pixelShader = device.CreatePixelShader(b);
                context.PSSetShader(pixelShader);
            });
    }

    protected void AsyncGetShader(string entryPoint, string sourceName, string profile, Action<Blob> callback)
    {
        Blob shaderBlob = Task.Factory.StartNew(() => GetShader(entryPoint, sourceName, profile)).Result;
        callback(shaderBlob);
        shaderBlob.Dispose();
    }

    protected Blob GetShader(string entryPoint, string sourceName, string profile)
    {
        string file = exeLocation + Name + '_' + entryPoint + "Cache.blob";
        if (Description.UseShaderCache && File.Exists(file))
        {
            return Compiler.ReadFileToBlob(file);
        }
        else
        {
            ShaderFlags sf = ShaderFlags.OptimizationLevel3;
#if DEBUG
            sf = ShaderFlags.Debug;
#endif
            Compiler.Compile(shaderCode, null, null, entryPoint, sourceName, profile, sf, out Blob shaderBlob, out Blob errorCode);
            if (shaderBlob == null)
                throw new("HLSL " + sourceName + " compilation error:\r\n" + errorCode.AsString());

            errorCode?.Dispose();
            Compiler.WriteBlobToFile(shaderBlob, file, true);
            return shaderBlob;
        }
    }

    protected virtual void SetConstantBuffers()
    {
        EyeStartPos = EyePos;
        EyeStartRot = EyeRot;

        buffers = new ID3D11Buffer[cBuffers.Length];

        BufferDescription bd = new(0, BindFlags.ConstantBuffer, ResourceUsage.Dynamic, CpuAccessFlags.Write);
        for (int i = 0; i < cBuffers.Length; ++i)
        {
            bd.ByteWidth = cBuffers[i].GetSize();
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
        t2 = Ticks;
        if (Description.RefreshRate != 0)
        {
            long max = (long)(SEC2TICK / (double)Description.RefreshRate);
            while ((t2 - t1) < max)
            {
                t2 = Ticks;
            }
        }
        RenderTime = (t2 - t1) * TICK2SEC;
        t1 = t2;
        double fps = 1.0 / RenderTime;
        maxFPS = Math.Max(fps, maxFPS);
        minFPS = Math.Min(fps, minFPS);
        frameCount++;

        // reset counters
        if (Milliseconds / STATS_DUR > lastReset)
        {
            avgFPS = frameCount / (double)(Ticks - startFPSTime) * SEC2TICK;
            startFPSTime = Ticks;
            string text = CreateTitle();
            window.BeginInvoke(new(() => window.Text = text));
            maxFPS = 0.0;
            minFPS = double.PositiveInfinity;
            frameCount = 0;
            lastReset = Milliseconds / STATS_DUR;
        }
    }

    protected virtual string CreateTitle()
    {
        return Name + "   Frame: " + avgFPS.ToString("G4") + "fps (" + minFPS.ToString("G4") + 
            "fps, " + maxFPS.ToString("G4") + "fps)   Input: " + inputAvgFPS.ToString("G4") +
            "fps (" + inputAvgSleep.ToString("G3") + ")";
    }

    // subclasses should always dispose this last and never assume that anything will be set
    protected virtual void Dispose(bool boolean)
    {
        gameobjects.Clear();
        lights.Clear();
        commands.Clear();
        commands = null;
        if (buffers != null)
            foreach (var buffer in buffers)
                buffer?.Dispose();
        if (samplers != null)
            foreach (var sampler in samplers)
                sampler?.Dispose();
        inputLayout?.Dispose();
        pixelShader?.Dispose();
        vertexShader?.Dispose();
        vertexBuffer?.Dispose();
        renderTargetView?.Dispose();
        swapChain?.Dispose();
        context?.Dispose();
        display?.Dispose();
        adapter?.Dispose();
        device?.Dispose();
        input?.Dispose();
        window?.Dispose();
    }

    protected virtual void RenderFlow()
    {
        GetTime();
        if (!Focused)
        {
            Thread.Sleep(UNFOCUSED_TIMEOUT);
            return;
        }
        Render();
        Update();
        PerFrameUpdate();
        swapChain.Present(swapChain.IsFullscreen ? 1 : 0);
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

    }

    /////////////////////////////////////

    private void Window_Load(object sender, EventArgs e)
    {
        print("Loading");
        Running = true;
        uiThread = Thread.CurrentThread;
        uiThread.Name = "UI";
        Setup();
        InitializeDeviceResources();
        GetDisplayRefreshRate();
    }

    private void Window_Shown(object sender, EventArgs e)
    {
        print("Shown");

        GC.Collect();

        if (input != null)
        {
            input.InitializeInputs();
            controlThread = new(input.ControlLoop);
            controlThread.IsBackground = true;
            controlThread.Name = "Input";
            controlThread.Start();
        }

        // Always start the render thread
        renderThread = new(RenderLoop);
        renderThread.IsBackground = true;
        renderThread.Name = "Render";
        renderThread.Start();

        if (FullScreen)
        {
            ToggleFullscreen();
        }

        // will run while compiling.

        if (HasShader)
        {
            UpdateShaderConstants();

            InitializeShaders();

            // wait for shader compilation to finish
            Task.WaitAll(compilerTasks.ToArray(), -1);

            long time1 = Milliseconds;
            // get any hlsl defs in case arrays use them in the shader
            GetDefs();

            ParseConstantBuffers();

            ParseSamplers();
            print("Shader parsing time=" + (Milliseconds - time1) + "ms");

            SetConstantBuffers();

            InitializeVertices();

            ShadersReady = true;
        }

        Start();
        PerApplicationUpdate();
    }

    private void Closing(object sender, FormClosingEventArgs e)
    {
        print("Closing");
        Running = false;

        // wait up to a second for the threads to exit
        long startWait = Ticks + SEC2TICK;
        Thread[] threads = new Thread[] { controlThread, renderThread };
        bool allDone = false;
        while (startWait > Ticks)
        {
            int done = 0;
            foreach (var t in threads)
                if (t == null || !t.IsAlive)
                    ++done;
            if (done == threads.Length)
            {
                allDone = true;
                break;
            }
        }
        if (!allDone)
        {
            for (int i = 0; i < threads.Length; ++i)
            {
                if (threads[i] != null)
                {
                    print("Aborting " + threads[i].Name + " thread");
                    threads[i] = null;
                }
            }
        }

        Dispose();
    }

    private void GotFocus(object sender, EventArgs e)
    {
        Focused = true;
    }

    private void LostFocus(object sender, EventArgs e)
    {
        Focused = false;
    }

    private void GetDisplayRefreshRate()
    {
        int num = 0;
        int main = -1;
        displayRefreshRate = 0.0f;
        Screen screen = Screen.FromPoint(window.Location);
        List<IDXGIOutput> displays = new List<IDXGIOutput>();
        while (true)
        {
            adapter.EnumOutputs(num, out IDXGIOutput output);
            if (output == null)
                break;
            displays.Add(output);
            var modes = output.GetDisplayModeList(Format.R8G8B8A8_UNorm, DisplayModeEnumerationFlags.DisabledStereo);
            foreach (var mode in modes)
            {
                float rate = mode.RefreshRate.Numerator / (float)mode.RefreshRate.Denominator;
                if (screen.Bounds.Width == mode.Width && screen.Bounds.Height == mode.Height && rate > displayRefreshRate)
                {
                    displayRefreshRate = rate;
                    main = num;
                }
            }
            num++;
        }

        for (int i = 0; i < displays.Count; ++i)
        {
            if (i == main)
            {
                display = displays[i];
            }
            else
            {
                displays[i].Release();
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
    
    private void RenderLoop()
    {
        t1 = startFPSTime = Ticks;
        while (Running)
        {
            while (commands.Count != 0)
                commands.Dequeue()();
            RenderFlow();
        }
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
}
