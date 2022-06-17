using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Mathematics;

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
public struct VertexPositionNormal
{
    public readonly Vector3 Position;
    public readonly Vector3 Normal;

    public VertexPositionNormal(Vector3 position, Vector3 normal)
    {
        Position = position;
        Normal = normal;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 16)]
public struct ScreenPositionNormal
{
    public readonly Vector2 Position;
    public readonly Vector2 Normal;

    public ScreenPositionNormal(Vector2 position, Vector2 normal)
    {
        Position = position;
        Normal = normal;
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
public struct Material
{
    public Vector3 DiffuseColor;
    public float Roughness;
    public Vector3 SpecularColor;
    public float Shininess;
    public float IOR;
    private Vector3 padding = new();

    public Material(Vector3 diffuseColor, float roughness, Vector3 specularColor, float shine, float iOR)
    {
        DiffuseColor = diffuseColor;
        Roughness = roughness;
        SpecularColor = specularColor;
        Shininess = shine;
        IOR = iOR;
    }

    public Material(Color4 diffuseColor, float roughness, Color4 specularColor, float shine, float iOR)
    {
        DiffuseColor = diffuseColor.ToVector3();
        Roughness = roughness;
        SpecularColor = specularColor.ToVector3();
        Shininess = shine;
        IOR = iOR;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 16)]
public struct PackedGameobject
{
    public readonly Vector3 Position;
    public readonly float Radius;
    public readonly uint StartIndex;
    public readonly uint EndIndex;
    private long padding = 0;

    public PackedGameobject(Vector3 position, float radius, int startIndex, int endIndex)
    {
        Position = position;
        Radius = radius;
        StartIndex = (uint)startIndex;
        EndIndex = (uint)endIndex;
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

    public PackedTriangle(Vector4[] vertices, Vector4[] normals)
    {
        Vertex0 = vertices[0];
        Vertex1 = vertices[1];
        Vertex2 = vertices[2];
        Normal0 = normals[0];
        Normal1 = normals[1];
        Normal2 = normals[2];
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 16)]
public struct PackedSphere
{
    public Vector3 Position;
    public float Radius;

    public PackedSphere(Vector3 position, float radius)
    {
        Position = position;
        Radius = radius;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 16)]
public struct PackedLight
{
    public Vector3 Position;
    public float Radius;
    public Vector3 Color;
    public float Luminosity;

    public PackedLight(Vector3 position, float radius, Vector3 color, float lumin)
    {
        Position = position;
        Radius = radius;
        Color = color;
        Luminosity = lumin;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 16)]
public struct RasterPackedLight
{
    public PackedLight Base;
    public float Res;
    public float Far;
    private Vector2 padding = new();

    public RasterPackedLight(Vector3 position, float radius, Vector3 color, float lumin, float res, float far)
    {
        Base.Position = position;
        Base.Radius = radius;
        Base.Color = color;
        Base.Luminosity = lumin;
        Res = res;
        Far = far;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 16)]
public struct RasterApplicationBuffer
{
    public Matrix4x4 ProjectionMatrix;
    public Vector3 LowerAtmosphere;
    public uint Width;
    public Vector3 UpperAtmosphere;
    public uint Height;

    public RasterApplicationBuffer(Matrix4x4 projectionMatrix, Vector3 lowerAtmosphere, Vector3 upperAtmosphere, int width, int height)
    {
        ProjectionMatrix = projectionMatrix;
        LowerAtmosphere = lowerAtmosphere;
        Width = (uint)width;
        UpperAtmosphere = upperAtmosphere;
        Height = (uint)height;
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
    public Matrix4x4 LightMatrixRight;
    public Matrix4x4 LightMatrixLeft;
    public Matrix4x4 LightMatrixUp;
    public Matrix4x4 LightMatrixDown;
    public Matrix4x4 LightMatrixForward;
    public Matrix4x4 LightMatrixBackward;
    public uint Index;
    public float DepthBias;
    public float NormalBias;
    public bool Line;

    public RasterLightBuffer(Matrix4x4 lightMatrixRight, Matrix4x4 lightMatrixLeft, Matrix4x4 lightMatrixUp, Matrix4x4 lightMatrixDown, 
        Matrix4x4 lightMatrixForward, Matrix4x4 lightMatrixBack, int index, float depth, float normal, bool line)
    {
        LightMatrixRight = lightMatrixRight;
        LightMatrixLeft = lightMatrixLeft;
        LightMatrixUp = lightMatrixUp;
        LightMatrixDown = lightMatrixDown;
        LightMatrixForward = lightMatrixForward;
        LightMatrixBackward = lightMatrixBack;
        Index = (uint)index;
        DepthBias = depth;
        NormalBias = normal;
        Line = line;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 16)]
public struct RasterObjectBuffer
{
    public Matrix4x4 WorldMatrix;
    public Matrix4x4 NormaldMatrix;
    public Material Material;

    public RasterObjectBuffer(Matrix4x4 worldMatrix, Matrix4x4 normaldMatrix, Material material)
    {
        WorldMatrix = worldMatrix;
        NormaldMatrix = normaldMatrix;
        Material = material;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 16)]
public struct RayApplicationBuffer
{
    public Vector3 LowerAtmosphere;
    public uint Width;
    public Vector3 UpperAtmosphere;
    public uint Height;

    public RayApplicationBuffer(Vector3 lowerAtmosphere, Vector3 upperAtmosphere, int width, int height)
    {
        LowerAtmosphere = lowerAtmosphere;
        UpperAtmosphere = upperAtmosphere;
        Width = (uint)width;
        Height = (uint)height;
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
