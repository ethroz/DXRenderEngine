using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using static DXRenderEngine.Helpers;
using static DXRenderEngine.Time;

namespace DXRenderEngine;

/// <summary>
/// TODO
/// 
/// Potential
///     soft shadows // need a continuous function
///     caustics // need to cast rays from light somehow
///     convert to direct compute?
/// </summary>

public class RayTracingEngine : Engine
{
    public new readonly RayTracingEngineDescription Description;
    private new ScreenPositionNormal[] vertices;
    public readonly List<Sphere> spheres = new();
    private Material[] packedMaterials;
    private PackedGameobject[] packedGameobjects;
    private PackedTriangle[] packedTriangles;
    private PackedSphere[] packedSpheres;
    private PackedLight[] packedLights;
    private int triangleCount;
    private const bool BOX = false;

    protected internal RayTracingEngine(RayTracingEngineDescription ED) : base(ED)
    {
        Description = ED;
    }

    protected override void SetInputElements()
    {
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
        for (int i = 0; i < gameobjects.Count; ++i)
        {
            gameobjects[i].Offset = triangleCount;
            triangleCount += gameobjects[i].Triangles.Length;
        }

        ModifyShaderCode("NUM_MATERIALS 1", "NUM_MATERIALS " + (gameobjects.Count + spheres.Count));
        ModifyShaderCode("NUM_OBJECTS 1", "NUM_OBJECTS " + gameobjects.Count);
        ModifyShaderCode("NUM_TRIS 1", "NUM_TRIS " + triangleCount);
        ModifyShaderCode("NUM_SPHERES 1", "NUM_SPHERES " + spheres.Count);
        ModifyShaderCode("NUM_LIGHTS 1", "NUM_LIGHTS " + lights.Count);
        ModifyShaderCode("NUM_RAYS 1", "NUM_RAYS " + (Pow(2, Description.RayDepth + 1) - 1));
        ModifyShaderCode("BOX false", "BOX " + BOX.ToString());
    }

    protected override void SetConstantBuffers()
    {
        base.SetConstantBuffers();

        packedMaterials = new Material[gameobjects.Count + spheres.Count];
        packedTriangles = new PackedTriangle[triangleCount];
        packedGameobjects = new PackedGameobject[gameobjects.Count];
        packedSpheres = new PackedSphere[spheres.Count];
        packedLights = new PackedLight[lights.Count];

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

        for (int i = 0; i < gameobjects.Count; ++i)
        {
            gameobjects[i].ProjectedTriangles = new TriNormsCol[gameobjects[i].Triangles.Length];
            for (int j = 0; j < gameobjects[i].Triangles.Length; ++j)
                gameobjects[i].ProjectedTriangles[j] = new(new Vector3[3], new Vector3[3]);
        }

        BufferDescription bd = new(vertices.Length * Marshal.SizeOf<ScreenPositionNormal>(), BindFlags.VertexBuffer);
        vertexBuffer = device.CreateBuffer<ScreenPositionNormal>(vertices, bd);
        context.IASetVertexBuffer(0, vertexBuffer, Marshal.SizeOf<ScreenPositionNormal>());
    }

    protected override void PerApplicationUpdate()
    {
        cBuffers[0].Insert(LowerAtmosphere, 0);
        cBuffers[0].Insert(Width, 1);
        cBuffers[0].Insert(UpperAtmosphere, 2);
        cBuffers[0].Insert(Height, 3);

        UpdateConstantBuffer(0, MapMode.WriteDiscard);
    }

    private void GeometryUpdate()
    {
        PackVertices();

        int i = cBuffers[1].InsertArray(packedMaterials, 0);
        i = cBuffers[1].InsertArray(packedGameobjects, i);
        i = cBuffers[1].InsertArray(packedTriangles, i);
        i = cBuffers[1].InsertArray(packedSpheres, i);
        cBuffers[1].InsertArray(packedLights, i);

        UpdateConstantBuffer(1);
    }

    protected override void PerFrameUpdate()
    {
        GeometryUpdate();

        cBuffers[2].Insert(CreateRotation(EyeRot), 0);
        cBuffers[2].Insert(EyePos, 1);
        cBuffers[2].Insert((float)(Ticks % 60L), 2);

        UpdateConstantBuffer(2);
    }

    private void UpdateVertices()
    {
        for (int i = 0; i < gameobjects.Count; ++i)
        {
            gameobjects[i].CreateMatrices();
            for (int j = 0; j < gameobjects[i].Triangles.Length; ++j)
            {
                for (int k = 0; k < 3; ++k)
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
        for (int i = 0; i < gameobjects.Count; ++i)
        {
            Material mat = gameobjects[i].Material;
            mat.Roughness = Math.Clamp(mat.Roughness, 0.001f, 0.999f);
            packedMaterials[num++] = mat;
        }
        for (int i = 0; i < spheres.Count; ++i)
        {
            Material mat = spheres[i].Material;
            mat.Roughness = Math.Clamp(mat.Roughness, 0.001f, 0.999f);
            packedMaterials[num++] = mat;
        }
    }

    private void PackGameobjects()
    {
        int num = 0;
        for (int i = 0; i < gameobjects.Count; ++i)
        {
            packedGameobjects[i] = gameobjects[i].Pack();
            for (int j = 0; j < gameobjects[i].ProjectedTriangles.Length; ++j)
            {
                packedTriangles[num++] = gameobjects[i].ProjectedTriangles[j].Pack();
            }
        }
    }

    private void PackSpheres()
    {
        for (int i = 0; i < spheres.Count; ++i)
        {
            packedSpheres[i] = spheres[i].Pack();
        }
    }

    private void PackLights()
    {
        for (int i = 0; i < lights.Count; ++i)
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
}
