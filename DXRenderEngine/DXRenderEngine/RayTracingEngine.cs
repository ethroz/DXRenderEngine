using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace DXRenderEngine;

/// <summary>
/// TODO
/// 
/// Potential
///     soft shadows // need a continuous function
///     caustics // need to cast rays from light somehow
///     convert to direct compute?
/// </summary>

public sealed class RayTracingEngine : Engine
{
    public new readonly RayTracingEngineDescription Description;
    private new ScreenPositionNormal[] vertices;
    public readonly List<Sphere> spheres = new();
    private ID3D11Buffer[] buffers;
    private RayApplicationBuffer applicationData;
    private RayFrameBuffer frameData;
    private Material[] packedMaterials;
    private PackedGameobject[] packedGameobjects;
    private PackedTriangle[] packedTriangles;
    private PackedSphere[] packedSpheres;
    private PackedLight[] packedLights;
    private int triangleCount;
    private const bool BOX = false;

    public RayTracingEngine(RayTracingEngineDescription ED) : base(ED)
    {
        Description = ED;
        Assembly assembly = Assembly.GetExecutingAssembly();
        string resourceName = "DXRenderEngine.DXRenderEngine.RayShaders.hlsl";

        using (Stream stream = assembly.GetManifestResourceStream(resourceName))
        using (StreamReader reader = new(stream))
        {
            shaderCode = reader.ReadToEnd();
        }

        inputElements = new InputElementDescription[]
        {
            new("POSITION", 0, Format.R32G32_Float, 0, 0, InputClassification.PerVertexData, 0),
            new("NORMAL", 0, Format.R32G32_Float, 8, 0, InputClassification.PerVertexData, 0)
        };
    }

    protected override void UpdateShaderConstants()
    {
        base.UpdateShaderConstants();
        if (spheres.Count == 0)
            spheres.Add(new());
        triangleCount = 0;
        for (int i = 0; i < gameobjects.Count; i++)
        {
            gameobjects[i].Offset = triangleCount;
            triangleCount += gameobjects[i].Triangles.Length;
        }

        ChangeShader("NUM_MATERIALS 1", "NUM_MATERIALS " + (gameobjects.Count + spheres.Count));
        ChangeShader("NUM_OBJECTS 1", "NUM_OBJECTS " + gameobjects.Count);
        ChangeShader("NUM_TRIS 1", "NUM_TRIS " + triangleCount);
        ChangeShader("NUM_SPHERES 1", "NUM_SPHERES " + spheres.Count);
        ChangeShader("NUM_LIGHTS 1", "NUM_LIGHTS " + lights.Count);
        ChangeShader("NUM_RAYS 1", "NUM_RAYS " + (Pow(2, Description.RayDepth + 1) - 1));
        ChangeShader("BOX false", "BOX " + BOX.ToString());
    }

    protected override void SetConstantBuffers()
    {
        base.SetConstantBuffers();

        buffers = new ID3D11Buffer[3];
        applicationData = new();
        frameData = new();
        packedMaterials = new Material[gameobjects.Count + spheres.Count];
        packedTriangles = new PackedTriangle[triangleCount];
        packedGameobjects = new PackedGameobject[gameobjects.Count];
        packedSpheres = new PackedSphere[spheres.Count];
        packedLights = new PackedLight[lights.Count];

        BufferDescription bd = new(Marshal.SizeOf(applicationData), BindFlags.ConstantBuffer, ResourceUsage.Dynamic, CpuAccessFlags.Write);
        buffers[0] = device.CreateBuffer(bd);
        bd.SizeInBytes = Marshal.SizeOf(packedMaterials[0]) * packedMaterials.Length + Marshal.SizeOf(packedGameobjects[0]) * packedGameobjects.Length +
            Marshal.SizeOf(packedTriangles[0]) * packedTriangles.Length + Marshal.SizeOf(packedSpheres[0]) * packedSpheres.Length +
            Marshal.SizeOf(packedLights[0]) * packedLights.Length;
        buffers[1] = device.CreateBuffer(bd);
        bd.SizeInBytes = Marshal.SizeOf(frameData);
        buffers[2] = device.CreateBuffer(bd);

        context.PSSetConstantBuffers(0, buffers.Length, buffers);
        context.VSSetConstantBuffer(2, buffers[2]);
    }

    protected override void PackVertices()
    {
        UpdateVertices();
        PackMaterials();
        PackGameobjects();
        PackSpheres();
        PackLights();
    }

    protected override void InitializeVertices()
    {
        float aspect = (float)Height / Width;
        vertices = new ScreenPositionNormal[]
        {
            new(new(-1.0f, -1.0f), new(-0.1f, -0.1f * aspect)),
            new(new(-1.0f, 1.0f), new(-0.1f, 0.1f * aspect)),
            new(new(1.0f, 1.0f), new(0.1f, 0.1f * aspect)),
            new(new(-1.0f, -1.0f), new(-0.1f, -0.1f * aspect)),
            new(new(1.0f, 1.0f), new(0.1f, 0.1f * aspect)),
            new(new(1.0f, -1.0f), new(0.1f, -0.1f * aspect))
        };

        for (int i = 0; i < gameobjects.Count; i++)
        {
            gameobjects[i].ProjectedTriangles = new TriNormsCol[gameobjects[i].Triangles.Length];
            for (int j = 0; j < gameobjects[i].Triangles.Length; j++)
                gameobjects[i].ProjectedTriangles[j] = new(new Vector3[3], new Vector3[3]);
        }

        BufferDescription bd = new(vertices.Length * Marshal.SizeOf<ScreenPositionNormal>(), BindFlags.VertexBuffer);
        vertexBuffer = device.CreateBuffer(vertices, bd);
        context.IASetVertexBuffer(0, new(vertexBuffer, Marshal.SizeOf<ScreenPositionNormal>()));
    }

    protected override void PerApplicationUpdate()
    {
        applicationData = new(LowerAtmosphere, UpperAtmosphere, Width, Height);

        MappedSubresource resource = context.Map(buffers[0], MapMode.WriteDiscard);
        Marshal.StructureToPtr(applicationData, resource.DataPointer, true);
        context.Unmap(buffers[0]);
    }

    private void GeometryUpdate()
    {
        PackVertices();

        MappedSubresource resource = context.Map(buffers[1], MapMode.WriteNoOverwrite);
        IntPtr pointer = resource.DataPointer;
        WriteStructArray(packedMaterials, ref pointer);
        WriteStructArray(packedGameobjects, ref pointer);
        WriteStructArray(packedTriangles, ref pointer);
        WriteStructArray(packedSpheres, ref pointer);
        WriteStructArray(packedLights, ref pointer);
        context.Unmap(buffers[1]);
    }

    protected override void PerFrameUpdate()
    {
        GeometryUpdate();

        frameData = new(CreateRotation(EyeRot), EyePos, sw.ElapsedTicks % 60L);

        MappedSubresource resource = context.Map(buffers[2], MapMode.WriteNoOverwrite);
        Marshal.StructureToPtr(frameData, resource.DataPointer, true);
        context.Unmap(buffers[2]);
    }

    private void UpdateVertices()
    {
        for (int i = 0; i < gameobjects.Count; i++)
        {
            gameobjects[i].CreateMatrices();
            for (int j = 0; j < gameobjects[i].Triangles.Length; j++)
            {
                for (int k = 0; k < 3; k++)
                {
                    gameobjects[i].ProjectedTriangles[j].Vertices[k] = Vector3.Transform(gameobjects[i].Triangles[j].Vertices[k], gameobjects[i].World);
                    gameobjects[i].ProjectedTriangles[j].Normals[k] = Vector3.TransformNormal(gameobjects[i].Triangles[j].Normals[k], gameobjects[i].Normal);
                }
            }
        }
    }

    private void PackMaterials()
    {
        int num = 0;
        for (int i = 0; i < gameobjects.Count; i++)
        {
            Material mat = gameobjects[i].Material;
            mat.Roughness = Math.Clamp(mat.Roughness, 0.001f, 0.999f);
            packedMaterials[num++] = mat;
        }
        for (int i = 0; i < spheres.Count; i++)
        {
            Material mat = spheres[i].Material;
            mat.Roughness = Math.Clamp(mat.Roughness, 0.001f, 0.999f);
            packedMaterials[num++] = mat;
        }
    }

    private void PackGameobjects()
    {
        int num = 0;
        for (int i = 0; i < gameobjects.Count; i++)
        {
            packedGameobjects[i] = gameobjects[i].Pack();
            for (int j = 0; j < gameobjects[i].ProjectedTriangles.Length; j++)
            {
                packedTriangles[num++] = gameobjects[i].ProjectedTriangles[j].Pack();
            }
        }
    }

    private void PackSpheres()
    {
        for (int i = 0; i < spheres.Count; i++)
        {
            packedSpheres[i] = spheres[i].Pack();
        }
    }

    private void PackLights()
    {
        for (int i = 0; i < lights.Count; i++)
        {
            packedLights[i] = lights[i].Pack();
        }
    }

    protected override void Render()
    {
        context.ClearRenderTargetView(renderTargetView, Colors.Black);
        context.OMSetRenderTargets(renderTargetView);
        context.Draw(vertices.Length, 0);
    }

    protected override void Dispose(bool boolean)
    {
        for (int i = 0; i < buffers.Length; i++)
        {
            buffers[i].Dispose();
        }
        base.Dispose(boolean);
    }
}
