using System;
using System.Numerics;
using System.Windows.Forms;

namespace DXRenderEngine;

public class EngineDescription
{
    public ProjectionDescription ProjectionDesc { get; internal set; }
    public int Width { get; internal set; }
    public int Height { get; internal set; }
    public int RefreshRate { get; internal set; }
    public readonly Action Setup, Start, Update, UserInput;
    public FormWindowState WindowState;
    public bool UseShaderCache { get; private set; }

    public EngineDescription(ProjectionDescription projectionDesc, int width = 640, int height = 480,
        int refreshRate = 60, Action setup = null, Action start = null, Action update = null,
        Action userInput = null, FormWindowState windowState = FormWindowState.Normal, bool cache = false)
    {
        ProjectionDesc = projectionDesc;
        Width = width;
        Height = height;
        RefreshRate = refreshRate;
        if (setup == null)
            Setup = Empty;
        else
            Setup = setup;
        if (start == null)
            Start = Empty;
        else
            Start = start;
        if (update == null)
            Update = Empty;
        else
            Update = update;
        if (userInput == null)
            UserInput = Empty;
        else
            UserInput = userInput;
        WindowState = windowState;
        UseShaderCache = cache;
    }

    private void Empty() { }
}

public class RasterizingEngineDescription : EngineDescription
{
    public bool Wireframe, Shadows, PostProcess;

    public RasterizingEngineDescription(EngineDescription ED, bool wireframe = false, bool shadows = false, 
        bool postProcess = false) : base(ED.ProjectionDesc, ED.Width, ED.Height, ED.RefreshRate, ED.Setup, 
            ED.Start, ED.Update, ED.UserInput, ED.WindowState, ED.UseShaderCache)
    {
        Wireframe = wireframe;
        Shadows = shadows;
        PostProcess = postProcess;
    }

    public RasterizingEngineDescription(ProjectionDescription projectionDesc, int width = 640, int height = 480,
        int refreshRate = 60, Action setup = null, Action start = null, Action update = null,
        Action userInput = null, FormWindowState windowState = FormWindowState.Normal, bool cache = false, bool wireframe = false,
        bool shadows = false, bool postProcess = false) : base(projectionDesc, width, height, refreshRate, setup,
            start, update, userInput, windowState, cache)
    {
        Shadows = shadows;
        PostProcess = postProcess;
        Wireframe = wireframe;
    }
}

public class RayTracingEngineDescription : EngineDescription
{
    public int RayDepth;

    public RayTracingEngineDescription(EngineDescription ED, int rayDepth = 1) : base(ED.ProjectionDesc, 
        ED.Width, ED.Height, ED.RefreshRate, ED.Setup, ED.Start, ED.Update, ED.UserInput, ED.WindowState, ED.UseShaderCache)
    {
        RayDepth = rayDepth;
    }

    public RayTracingEngineDescription(ProjectionDescription projectionDesc, int width = 640, int height = 480,
        int refreshRate = 60, Action setup = null, Action start = null, Action update = null,
        Action userInput = null, FormWindowState windowState = FormWindowState.Normal, bool cache = false, int rayDepth = 1) : 
        base(projectionDesc, width, height, refreshRate, setup, start, update, userInput, windowState, cache)
    {
        RayDepth = rayDepth;
    }
}

public struct ProjectionDescription
{
    public readonly float FOVVDegrees, AspectRatioWH, NearPlane, FarPlane;
    public static readonly ProjectionDescription Default = new(60.0f, 16.0f / 9.0f, 0.01f, 1000.0f);

    public ProjectionDescription(float fovVDegrees, float aspectRatioHW, float nearPlane, float farPlane)
    {
        FOVVDegrees = fovVDegrees;
        AspectRatioWH = aspectRatioHW;
        NearPlane = nearPlane;
        FarPlane = farPlane;
    }

    public Matrix4x4 GetMatrix()
    {
        return Engine.CreateProjection(FOVVDegrees, AspectRatioWH, NearPlane, FarPlane);
    }
}
