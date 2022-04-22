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

public class RayTracingEngine : Engine
{
    public new readonly RayTracingEngineDescription Description;
    protected new Vector3[] vertices;
    private readonly Vector3[] planeVertices = new Vector3[]
    {
        new Vector3(-1.0f, -1.0f, 0.0f),
        new Vector3(-1.0f, 1.0f, 0.0f),
        new Vector3(1.0f, 1.0f, 0.0f),
        new Vector3(-1.0f, -1.0f, 0.0f),
        new Vector3(1.0f, 1.0f, 0.0f),
        new Vector3(1.0f, -1.0f, 0.0f)
    };
    public readonly List<Sphere> spheres = new List<Sphere>();
    private ID3D11Buffer[] buffers;
    private RayApplicationBuffer applicationData;
    private RayFrameBuffer frameData;
    private PackedTriangle[] packedTriangles;
    private PackedSphere[] packedSpheres;
    private int triangleCount;

    public RayTracingEngine(RayTracingEngineDescription ED) : base(ED)
    {
        Description = ED;
        Assembly assembly = Assembly.GetExecutingAssembly();
        string resourceName = "DXRenderEngine.DXRenderEngine.RayShaders.hlsl";

        using (Stream stream = assembly.GetManifestResourceStream(resourceName))
        using (StreamReader reader = new StreamReader(stream))
        {
            shaderCode = reader.ReadToEnd();
        }

        inputElements = new InputElementDescription[]
        {
            new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0, InputClassification.PerVertexData, 0)
        };
    }

    protected override void UpdateShaderConstants()
    {
        base.UpdateShaderConstants();
        if (spheres.Count == 0)
            spheres.Add(new Sphere());
        triangleCount = 0;
        for (int i = 0; i < gameobjects.Count; i++)
            triangleCount += gameobjects[i].Triangles.Length;

        ChangeShader("NUM_TRIS 1", "NUM_TRIS " + triangleCount);
        ChangeShader("NUM_SPHERES 1", "NUM_SPHERES " + spheres.Count);
        ChangeShader("NUM_LIGHTS 1", "NUM_LIGHTS " + lights.Count);
        ChangeShader("NUM_RAYS 1", "NUM_RAYS " + (Pow(2, Description.RayDepth + 1) - 1));
    }

    protected override void SetConstantBuffers()
    {
        base.SetConstantBuffers();
        PackVertices();

        buffers = new ID3D11Buffer[3];
        applicationData = new RayApplicationBuffer();
        frameData = new RayFrameBuffer();
        BufferDescription bd = new BufferDescription(Marshal.SizeOf(applicationData), BindFlags.ConstantBuffer, ResourceUsage.Dynamic, CpuAccessFlags.Write);
        buffers[0] = device.CreateBuffer(bd);
        bd.SizeInBytes = Marshal.SizeOf<PackedTriangle>() * packedTriangles.Length + Marshal.SizeOf<PackedSphere>() *
            packedSpheres.Length + Marshal.SizeOf<PackedLight>() * packedLights.Length;
        buffers[1] = device.CreateBuffer(bd);
        bd.SizeInBytes = Marshal.SizeOf(frameData);
        buffers[2] = device.CreateBuffer(bd);

        context.PSSetConstantBuffers(0, buffers.Length, buffers);
    }

    protected override void PackVertices()
    {
        UpdateVertices();
        PackTriangles();
        PackSpheres();
        PackLights();
    }

    protected override void InitializeVertices()
    {
        vertices = planeVertices;

        BufferDescription bd = new BufferDescription(vertices.Length * Marshal.SizeOf<VertexPositionNormalColor>(), BindFlags.VertexBuffer);
        vertexBuffer = device.CreateBuffer(vertices, bd);
        context.IASetVertexBuffer(0, new VertexBufferView(vertexBuffer, Marshal.SizeOf<Vector3>()));
    }

    protected override void PerApplicationUpdate()
    {
        applicationData = new RayApplicationBuffer(BGCol, MinBrightness, Width, Height);

        MappedSubresource resource = context.Map(buffers[0], MapMode.WriteDiscard);
        Marshal.StructureToPtr(applicationData, resource.DataPointer, true);
        context.Unmap(buffers[0]);
    }

    private void GeometryUpdate()
    {
        PackVertices();

        MappedSubresource resource = context.Map(buffers[1], MapMode.WriteDiscard);
        IntPtr pointer = resource.DataPointer;
        WriteStructArray(packedTriangles, ref pointer);
        WriteStructArray(packedSpheres, ref pointer);
        WriteStructArray(packedLights, ref pointer);
        context.Unmap(buffers[1]);
    }

    protected override void PerFrameUpdate()
    {
        GeometryUpdate();

        frameData = new RayFrameBuffer(CreateRotation(EyeRot), EyePos, sw.ElapsedTicks % 60L);

        MappedSubresource resource = context.Map(buffers[2], MapMode.WriteNoOverwrite);
        Marshal.StructureToPtr(frameData, resource.DataPointer, true);
        context.Unmap(buffers[2]);
    }

    private void UpdateVertices()
    {
        for (int i = 0; i < gameobjects.Count; i++)
        {
            gameobjects[i].ProjectedTriangles = new TriNormsCol[gameobjects[i].Triangles.Length];
            gameobjects[i].CreateMatrices();
            for (int j = 0; j < gameobjects[i].Triangles.Length; j++)
            {
                gameobjects[i].ProjectedTriangles[j] = new TriNormsCol(new Vector3[3], new Vector3[3], 
                    gameobjects[i].Triangles[j].Color, gameobjects[i].Triangles[j].Reflectivity);
                for (int k = 0; k < 3; k++)
                {
                    gameobjects[i].ProjectedTriangles[j].Vertices[k] = Vector3.Transform(gameobjects[i].Triangles[j].Vertices[k], gameobjects[i].World);
                    gameobjects[i].ProjectedTriangles[j].Normals[k] = Vector3.TransformNormal(gameobjects[i].Triangles[j].Normals[k], gameobjects[i].Normal);
                }
            }
        }
    }

    private void PackTriangles()
    {
        packedTriangles = new PackedTriangle[triangleCount];

        int num = 0;
        for (int i = 0; i < gameobjects.Count; i++)
        {
            for (int j = 0; j < gameobjects[i].ProjectedTriangles.Length; j++)
            {
                packedTriangles[num++] = gameobjects[i].ProjectedTriangles[j].Pack();
            }
        }
    }

    private void PackSpheres()
    {
        packedSpheres = new PackedSphere[spheres.Count];

        for (int i = 0; i < spheres.Count; i++)
        {
            packedSpheres[i] = spheres[i].Pack();
        }
    }

    protected override void Render()
    {
        context.ClearRenderTargetView(renderTargetView, Colors.Black);
        context.OMSetRenderTargets(renderTargetView);
        context.Draw(vertices.Length, 0);
    }
}
