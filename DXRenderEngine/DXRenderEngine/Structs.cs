using System.Numerics;
using System.Runtime.InteropServices;

namespace DXRenderEngine;

[StructLayout(LayoutKind.Sequential)]
public struct POINT
{
    public int X;
    public int Y;

    public POINT(int x, int y)
    {
        X = x;
        Y = y;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 16)]
public struct VertexPositionNormalColor
{
    public readonly Vector4 Position;
    public readonly Vector4 Normal;
    public readonly Vector4 Color;

    public VertexPositionNormalColor(Vector4 position, Vector4 normal, Vector4 color)
    {
        Position = position;
        Normal = normal;
        Color = color;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 16)]
public struct ObjectInstance
{
    public readonly Matrix4x4 World;
    public readonly Matrix4x4 Normal;

    public ObjectInstance(Matrix4x4 world, Matrix4x4 normal)
    {
        World = world;
        Normal = normal;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 16)]
public struct PackedTriangle
{
    public Vector4 Vertex0;
    public Vector4 Vertex1;
    public Vector4 Vertex2;
    public Vector4 Normal0;
    public Vector4 Normal1;
    public Vector4 Normal2;
    public Vector3 Color;
    public float Reflectivity;

    public PackedTriangle(Vector4[] vertices, Vector4[] normals, Vector3 color, float relfect)
    {
        Vertex0 = vertices[0];
        Vertex1 = vertices[1];
        Vertex2 = vertices[2];
        Normal0 = normals[0];
        Normal1 = normals[1];
        Normal2 = normals[2];
        Color = color;
        Reflectivity = relfect;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 16)]
public struct PackedSphere
{
    public Vector3 Position;
    public float Radius;
    public Vector3 Color;
    public float IOR;
    public float Reflectivity;
    private Vector3 padding = new Vector3();

    public PackedSphere(Vector3 position, float radius, Vector3 color, float ior, float reflect)
    {
        Position = position;
        Radius = radius;
        Color = color;
        IOR = ior;
        Reflectivity = reflect;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 16)]
public struct PackedLight
{
    public Vector3 Position;
    public float Radius;
    public Vector3 Color;
    private int padding = 0;

    public PackedLight(Vector3 position, float radius, Vector3 color)
    {
        Position = position;
        Radius = radius;
        Color = color;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 16)]
public struct RasterApplicationBuffer
{
    public Matrix4x4 ProjectionMatrix;
    public int Width;
    public int Height;
    private long padding = 0;

    public RasterApplicationBuffer(Matrix4x4 proj, int width, int height)
    {
        ProjectionMatrix = proj;
        Width = width;
        Height = height;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 16)]
public struct RasterFrameBuffer
{
    public Matrix4x4 ViewMatrix;
    public Vector3 EyePos;
    public float ModdedTime;

    public RasterFrameBuffer(Matrix4x4 view, Vector3 eyePos, float moddedTime)
    {
        ViewMatrix = view;
        EyePos = eyePos;
        ModdedTime = moddedTime;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 16)]
public struct RasterLightBuffer
{
    public Matrix4x4 LightMatrix;

    public RasterLightBuffer(Matrix4x4 light)
    {
        LightMatrix = light;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 16)]
public struct RayApplicationBuffer
{
    public Vector3 BackgroundColor;
    public float MinBrightness;
    public int Width;
    public int Height;
    private long padding = 0;

    public RayApplicationBuffer(Vector3 backgroundColor, float minBrightness, int width, int height)
    {
        BackgroundColor = backgroundColor;
        MinBrightness = minBrightness;
        Width = width;
        Height = height;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 16)]
public struct RayFrameBuffer
{
    public Matrix4x4 EyeRot;
    public Vector3 EyePos;
    public float ModdedTime;

    public RayFrameBuffer(Matrix4x4 rot, Vector3 eyePos, float moddedTime)
    {
        EyeRot = rot;
        EyePos = eyePos;
        ModdedTime = moddedTime;
    }
}
