using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Vortice.Mathematics;
using static DXRenderEngine.Helpers;

namespace DXRenderEngine;

[StructLayout(LayoutKind.Sequential, Pack = 16)]
public struct VertexPositionNormal
{
    public Vector3 Position;
    public Vector3 Normal;

    public VertexPositionNormal(Vector3 position, Vector3 normal)
    {
        Position = position;
        Normal = normal;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 16)]
public struct ScreenPositionNormal
{
    public Vector2 Position;
    public Vector2 Normal;

    public ScreenPositionNormal(Vector2 position, Vector2 normal)
    {
        Position = position;
        Normal = normal;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 16)]
public struct ObjectInstance
{
    public Matrix4x4 World;
    public Matrix4x4 Normal;

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

    public static readonly Material Default = new(Colors.White, 1.0f, Colors.White, 0.0f, 0.0f);

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
    public Vector3 Position;
    public float Radius;
    public uint StartIndex;
    public uint EndIndex;
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

public struct Data
{
    public int offset = 0;
    public int size = 0;

    public Data(int offset, int size)
    {
        this.offset = offset;
        this.size = size;
    }

    public Data(int offset)
    {
        this.offset = offset;
    }
}

public struct ConstantBuffer
{
    private byte[] buffer;
    private Data[] objects;

#if DEBUG

    public ConstantBuffer(int[] sizes)
    {
        objects = new Data[sizes.Length];
        int total = 0;
        for (int i = 0; i < sizes.Length; ++i)
        {
            objects[i] = new(total, sizes[i]);
            total += sizes[i];
        }
        int mod = total & PackMask;
        if (mod > 0)
        {
            total += Pack - mod;
        }
        buffer = new byte[total];
    }

    public unsafe int Insert<T>(T str, int index) where T : unmanaged
    {
        const int mask4 = 3;
        Data info = objects[index];
        int size = (sizeof(T) + mask4) & ~mask4;
        if (info.size != size)
        {
            throw new ArgumentException("index=" + index + " inSize=" + size + " expSize=" + info.size);
        }
        fixed (byte* pointer = &buffer[info.offset])
        {
            Unsafe.Write(pointer, str);
        }

        return index + 1;
    }

    public unsafe int InsertArray<T>(T[] strs, int index) where T : unmanaged
    {
        for (int i = 0; i < strs.Length; ++i)
        {
            index = Insert(strs[i], index);
        }

        return index;
    }

#else

    public ConstantBuffer(int[] sizes)
    {
        objects = new Data[sizes.Length];
        int total = 0;
        for (int i = 0; i < sizes.Length; ++i)
        {
            objects[i] = new(total);
            total += sizes[i];
        }
        total = (total + PackMask) & ~PackMask;
        buffer = new byte[total];
    }

    public unsafe int Insert<T>(T str, int index) where T : unmanaged
    {
        fixed (byte* pointer = &buffer[objects[index].offset])
        {
            Unsafe.Write(pointer, str);
        }

        return index + 1;
    }

    public unsafe int InsertArray<T>(T[] strs, int index) where T : unmanaged
    {
        fixed (byte* pointer = &buffer[objects[index].offset])
        {
            byte* ptr = pointer;
            for (int i = 0; i < strs.Length; ++i, ptr += sizeof(T))
            {
                Unsafe.Write(ptr, strs[i]);
            }
        }

        return index + strs.Length;
    }

#endif

    public void Copy(IntPtr dest)
    {
        Marshal.Copy(buffer, 0, dest, buffer.Length);
    }

    public byte[] GetBuffer()
    {
        return buffer;
    }

    public int GetSize()
    {
        return buffer.Length;
    }
}
