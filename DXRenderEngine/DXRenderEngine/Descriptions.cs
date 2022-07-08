using System;
using System.Numerics;
using System.Windows.Forms;
using static DXRenderEngine.Helpers;

namespace DXRenderEngine;

public class EngineDescription
{
    public ProjectionDescription ProjectionDesc { get; private set; }
    public string ShaderResource { get; protected set; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public int RefreshRate { get; private set; }
    public readonly Action Setup, Start, Update, UserInput;
    public FormWindowState WindowState { get; internal set; }
    public bool Resizeable { get; private set; }
    public bool UseShaderCache { get; private set; }

    public EngineDescription(ProjectionDescription projectionDesc, string shaderResource, int width = 640, int height = 480,
        int refreshRate = 60, Action setup = null, Action start = null, Action update = null,
        Action userInput = null, FormWindowState windowState = FormWindowState.Normal, bool resizeable = true, bool cache = false)
    {
        ProjectionDesc = new(projectionDesc.FOVVDegrees, projectionDesc.NearPlane, projectionDesc.FarPlane, (float)width / height);
        ShaderResource = shaderResource;
        Width = width;
        Height = height;
        RefreshRate = refreshRate;
        Setup = setup == null ? Empty :setup;
        Start = start == null ? Empty : start;
        Update = update == null ? Empty : update;
        UserInput = userInput;
        WindowState = windowState;
        Resizeable = resizeable;
        UseShaderCache = cache;
    }

    public EngineDescription(EngineDescription copy)
    {
        ProjectionDesc = copy.ProjectionDesc;
        ShaderResource = copy.ShaderResource;
        Width = copy.Width;
        Height = copy.Height;
        RefreshRate = copy.RefreshRate;
        Setup = copy.Setup;
        Start = copy.Start;
        Update = copy.Update;
        UserInput = copy.UserInput;
        WindowState = copy.WindowState;
        Resizeable = copy.Resizeable;
        UseShaderCache = copy.UseShaderCache;
    }

    private void Empty() { }
}

public class RasterizingEngineDescription : EngineDescription
{
    public bool Wireframe, Shadows, PostProcess;
    private const string shader = "DXRenderEngine.DXRenderEngine.RasterShaders.hlsl";

    public RasterizingEngineDescription(EngineDescription ED, bool wireframe = false, bool shadows = false, bool postProcess = false)
        : base(ED)
    {
        ShaderResource = shader;
        Wireframe = wireframe;
        Shadows = shadows;
        PostProcess = postProcess;
    }

    public RasterizingEngineDescription(RasterizingEngineDescription copy) : base(copy)
    {
        ShaderResource = shader;
        Wireframe = copy.Wireframe;
        Shadows = copy.Shadows;
        PostProcess = copy.PostProcess;
    }
}

public class RayTracingEngineDescription : EngineDescription
{
    public int RayDepth;
    private const string shader = "DXRenderEngine.DXRenderEngine.RayShaders.hlsl";

    public RayTracingEngineDescription(EngineDescription ED, int rayDepth = 1) : base(ED)
    {
        ShaderResource = shader;
        RayDepth = rayDepth;
    }

    public RayTracingEngineDescription(RayTracingEngineDescription copy) : base(copy)
    {
        ShaderResource = shader;
        RayDepth = copy.RayDepth;
    }
}

public struct ProjectionDescription
{
    public readonly float FOVVDegrees, NearPlane, FarPlane, AspectRatioWH;
    public static readonly ProjectionDescription Default = new(60.0f, 0.01f, 1000.0f, 16.0f / 9.0f);

    public ProjectionDescription(float fovVDegrees, float nearPlane, float farPlane, float aspectRatioHW = 1.0f)
    {
        FOVVDegrees = fovVDegrees;
        NearPlane = nearPlane;
        FarPlane = farPlane;
        AspectRatioWH = aspectRatioHW;
    }

    public Matrix4x4 GetMatrix()
    {
        return CreateProjection(FOVVDegrees, AspectRatioWH, NearPlane, FarPlane);
    }
}
