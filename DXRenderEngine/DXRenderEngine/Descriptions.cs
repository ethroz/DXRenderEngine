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
    public readonly Action OnAwake, OnStart, OnUpdate, UserInput;
    public FormWindowState WindowState;

    public EngineDescription(ProjectionDescription projectionDesc, int width = 640, int height = 480,
        int refreshRate = 60, Action onAwake = null, Action onStart = null, Action onUpdate = null,
        Action userInput = null, FormWindowState windowState = FormWindowState.Normal)
    {
        ProjectionDesc = projectionDesc;
        Width = width;
        Height = height;
        RefreshRate = refreshRate;
        if (onAwake == null)
            OnAwake = Empty;
        else
            OnAwake = onAwake;
        if (onStart == null)
            OnStart = Empty;
        else
            OnStart = onStart;
        if (onUpdate == null)
            OnUpdate = Empty;
        else
            OnUpdate = onUpdate;
        if (userInput == null)
            UserInput = Empty;
        else
            UserInput = userInput;
        WindowState = windowState;
    }

    private void Empty() { }
}

public class RasterizingEngineDescription : EngineDescription
{
    public bool Wireframe, Shadows, PostProcess;

    public RasterizingEngineDescription(EngineDescription ED, bool wireframe = false, bool shadows = false, 
        bool postProcess = false) : base(ED.ProjectionDesc, ED.Width, ED.Height, ED.RefreshRate, ED.OnAwake, 
            ED.OnStart, ED.OnUpdate, ED.UserInput, ED.WindowState)
    {
        Wireframe = wireframe;
        Shadows = shadows;
        PostProcess = postProcess;
    }

    public RasterizingEngineDescription(ProjectionDescription projectionDesc, int width = 640, int height = 480,
        int refreshRate = 60, Action onAwake = null, Action onStart = null, Action onUpdate = null,
        Action userInput = null, FormWindowState windowState = FormWindowState.Normal, bool wireframe = false,
        bool shadows = false, bool postProcess = false) : base(projectionDesc, width, height, refreshRate, onAwake,
            onStart, onUpdate, userInput, windowState)
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
        ED.Width, ED.Height, ED.RefreshRate, ED.OnAwake, ED.OnStart, ED.OnUpdate, ED.UserInput, ED.WindowState)
    {
        RayDepth = rayDepth;
    }

    public RayTracingEngineDescription(ProjectionDescription projectionDesc, int width = 640, int height = 480,
        int refreshRate = 60, Action onAwake = null, Action onStart = null, Action onUpdate = null,
        Action userInput = null, FormWindowState windowState = FormWindowState.Normal, int rayDepth = 1) : 
        base(projectionDesc, width, height, refreshRate, onAwake, onStart, onUpdate, userInput, windowState)
    {
        RayDepth = rayDepth;
    }
}

public struct ProjectionDescription
{
    public readonly float FOVVDegrees, AspectRatioWH, NearPlane, FarPlane;
    public static readonly ProjectionDescription Default = new ProjectionDescription(60.0f, 16.0f / 9.0f, 0.01f, 1000.0f);

    public ProjectionDescription(float fovVDegrees = 60.0f, float aspectRatioHW = 16.0f / 9.0f, float nearPlane = 0.01f, float farPlane = 1000.0f)
    {
        FOVVDegrees = fovVDegrees;
        AspectRatioWH = aspectRatioHW;
        NearPlane = nearPlane;
        FarPlane = farPlane;
    }

    public Matrix4x4 MakeMatrix()
    {
        return Matrix4x4.CreatePerspectiveFieldOfView(FOVVDegrees * Engine.DEG2RAD, AspectRatioWH, NearPlane, FarPlane);
    }
}
