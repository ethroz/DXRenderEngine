using System;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace DXRenderEngine;

public class RasterizingEngine : Engine
{
    public new readonly RasterizingEngineDescription Description;

    private ID3D11Texture2D1 lightViewTexture;
    private ID3D11Texture2D1 depthStencilBuffer;
    private ID3D11DepthStencilState depthStencilState;
    private ID3D11DepthStencilView depthStencilView;
    private ID3D11SamplerState[] samplers;

    private ID3D11RasterizerState2 shadowRasterizer;
    private ID3D11VertexShader shadowVertexShader;
    private ID3D11PixelShader shadowPixelShader;

    private ID3D11RasterizerState2 lightingRasterizer;
    private ID3D11PixelShader lightingPixelShader;
    private ID3D11RenderTargetView1 lightingTargetView;

    private ID3D11Buffer[] buffers;
    private RasterApplicationBuffer applicationData;
    private RasterFrameBuffer frameData;
    private RasterLightBuffer lightData;

    public RasterizingEngine(RasterizingEngineDescription ED) : base(ED)
    {
        Description = ED;
        Assembly assembly = Assembly.GetExecutingAssembly();
        string resourceName = "DXRenderEngine.DXRenderEngine.RasterShaders.hlsl";

        using (Stream stream = assembly.GetManifestResourceStream(resourceName))
        using (StreamReader reader = new StreamReader(stream))
        {
            shaderCode = reader.ReadToEnd();
        }
    }

    protected override void InitializeDeviceResources()
    {
        base.InitializeDeviceResources();
        InitializeRasterResources();
    }

    protected override void UpdateShaderConstants()
    {
        base.UpdateShaderConstants();

        ChangeShader("NUM_LIGHTS 1", "NUM_LIGHTS " + lights.Count);
    }

    private void InitializeRasterResources()
    {
        // light rendertargets
        Texture2DDescription1 td = new Texture2DDescription1()
        {
            Width = Width,
            Height = Height,
            ArraySize = 1,
            BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget,
            Usage = ResourceUsage.Default,
            Format = Format.R32G32B32A32_Float,
            MipLevels = 0,
            OptionFlags = ResourceOptionFlags.None,
            SampleDescription = new SampleDescription(1, 0)
        };
        for (int i = 0; i < lights.Count; i++)
        {
            lights[i].LightTexture = device.CreateTexture2D1(td);
            lights[i].LightResourceView = device.CreateShaderResourceView1(lights[i].LightTexture);
            lights[i].LightTargetView = device.CreateRenderTargetView1(lights[i].LightTexture);
        }

        // lighting rendertarget
        lightViewTexture = device.CreateTexture2D1(td);
        lightingTargetView = device.CreateRenderTargetView1(lightViewTexture);

        //depth buffers
        DepthStencilDescription dssdesc = new DepthStencilDescription();
        dssdesc.DepthEnable = true;
        dssdesc.DepthWriteMask = DepthWriteMask.All;
        dssdesc.DepthFunc = ComparisonFunction.Less;
        dssdesc.StencilEnable = false;
        depthStencilState = device.CreateDepthStencilState(dssdesc);
        context.OMSetDepthStencilState(depthStencilState);
        td.BindFlags = BindFlags.DepthStencil;
        td.CpuAccessFlags = 0;
        td.Format = Format.D32_Float;
        td.MipLevels = 1;
        depthStencilBuffer = device.CreateTexture2D1(td);
        DepthStencilViewDescription dsvdesc = new DepthStencilViewDescription();
        dsvdesc.Format = Format.D32_Float;
        dsvdesc.ViewDimension = DepthStencilViewDimension.Texture2D;
        dsvdesc.Texture2D.MipSlice = 0;
        depthStencilView = device.CreateDepthStencilView(depthStencilBuffer, dsvdesc);
        RasterizerDescription2 rsdesc = new RasterizerDescription2();
        rsdesc.AntialiasedLineEnable = false;
        rsdesc.CullMode = CullMode.Back;
        rsdesc.DepthBias = 0;
        rsdesc.DepthBiasClamp = 0.0f;
        rsdesc.DepthClipEnable = true;
        rsdesc.FillMode = Description.Wireframe ? FillMode.Wireframe : FillMode.Solid;
        rsdesc.FrontCounterClockwise = false;
        rsdesc.MultisampleEnable = false;
        rsdesc.ScissorEnable = false;
        rsdesc.SlopeScaledDepthBias = 0.0f;
        lightingRasterizer = device.CreateRasterizerState2(rsdesc);

        //shadow buffer
        dsvdesc.Format = Format.D24_UNorm_S8_UInt;
        ShaderResourceViewDescription1 srvdesc = new ShaderResourceViewDescription1();
        srvdesc.ViewDimension = ShaderResourceViewDimension.Texture2D;
        srvdesc.Format = Format.R24_UNorm_X8_Typeless;
        srvdesc.Texture2D.MipLevels = 1;
        td.Format = Format.R24G8_Typeless;
        td.BindFlags = BindFlags.ShaderResource | BindFlags.DepthStencil;
        for (int i = 0; i < lights.Count; i++)
        {
            td.Height = lights[i].ShadowRes;
            td.Width = lights[i].ShadowRes;
            lights[i].ShadowTexture = device.CreateTexture2D1(td);
            lights[i].ShadowStencilView = device.CreateDepthStencilView(lights[i].ShadowTexture, dsvdesc);
            lights[i].ShadowResourceView = device.CreateShaderResourceView1(lights[i].ShadowTexture, srvdesc);
            lights[i].ShadowViewPort = new Viewport(0, 0, lights[i].ShadowRes, lights[i].ShadowRes, 0.0f, 1.0f);
            lights[i].ShadowProjectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView((float)Math.PI / 2.0f, 1.0f, lights[i].NearPlane, lights[i].FarPlane);
        }

        // shadow rasterizer
        rsdesc = new RasterizerDescription2();
        rsdesc.CullMode = CullMode.None;
        rsdesc.FillMode = FillMode.Solid;
        shadowRasterizer = device.CreateRasterizerState2(rsdesc);
    }

    protected override void InitializeShaders()
    {
        base.InitializeShaders();

        ShaderFlags sf = ShaderFlags.OptimizationLevel3;
#if DEBUG
        sf = ShaderFlags.Debug;
#endif
        Compiler.Compile(shaderCode, null, null, vertexShaderEntry, "shadowVertexShader", "vs_5_0", sf, out Blob shaderBlob, out Blob errorCode);
        if (shaderBlob == null)
            throw new Exception("HLSL vertex shader compilation error:\r\n" + Encoding.ASCII.GetString(errorCode.GetBytes()));
        shadowVertexShader = device.CreateVertexShader(shaderBlob);

        shaderBlob.Dispose();

        Compiler.Compile(shaderCode, null, null, pixelShaderEntry, "shadowPixelShader", "ps_5_0", sf, out shaderBlob, out errorCode);
        if (shaderCode == null)
            throw new Exception("HLSL pixel shader compilation error:\r\n" + Encoding.ASCII.GetString(errorCode.GetBytes()));
        shadowPixelShader = device.CreatePixelShader(shaderBlob);

        shaderBlob.Dispose();

        Compiler.Compile(shaderCode, null, null, pixelShaderEntry, "lightingPixelShader", "ps_5_0", sf, out shaderBlob, out errorCode);
        if (shaderCode == null)
            throw new Exception("HLSL pixel shader compilation error:\r\n" + Encoding.ASCII.GetString(errorCode.GetBytes()));
        lightingPixelShader = device.CreatePixelShader(shaderBlob);

        shaderBlob.Dispose();

        samplers = new ID3D11SamplerState[2];

        // color sampler
        SamplerDescription ssdesc = new SamplerDescription
        {
            AddressU = TextureAddressMode.Clamp,
            AddressV = TextureAddressMode.Clamp,
            AddressW = TextureAddressMode.Clamp,
            MinLOD = 0.0f,
            MaxLOD = float.MaxValue,
            MipLODBias = 0.0f,
            Filter = Filter.MinMagMipLinear
        };
        samplers[0] = device.CreateSamplerState(ssdesc);

        // depth sampler
        ssdesc.AddressU = TextureAddressMode.Border;
        ssdesc.AddressV = TextureAddressMode.Border;
        ssdesc.AddressW = TextureAddressMode.Border;
        ssdesc.BorderColor = new Color4(1.0f, 1.0f, 1.0f, 1.0f);
        ssdesc.MaxAnisotropy = 0;
        ssdesc.ComparisonFunction = ComparisonFunction.LessEqual;
        ssdesc.Filter = Filter.ComparisonMinMagMipPoint;
        samplers[1] = device.CreateSamplerState(ssdesc);

        context.PSSetSamplers(0, samplers);
    }

    protected unsafe override void SetConstantBuffers()
    {
        base.SetConstantBuffers();

        PackLights();
        buffers = new ID3D11Buffer[3];
        applicationData = new RasterApplicationBuffer(Description.ProjectionDesc.MakeMatrix(), Width, Height);
        frameData = new RasterFrameBuffer();
        lightData = new RasterLightBuffer();
        BufferDescription bd = new BufferDescription(Marshal.SizeOf(applicationData) + Marshal.SizeOf<PackedLight>() * packedLights.Length, BindFlags.ConstantBuffer, ResourceUsage.Dynamic, CpuAccessFlags.Write);
        buffers[0] = device.CreateBuffer(bd);
        bd.SizeInBytes = Marshal.SizeOf(frameData);
        buffers[1] = device.CreateBuffer(bd);
        bd.SizeInBytes = Marshal.SizeOf(lightData);
        buffers[2] = device.CreateBuffer(bd);

        context.VSSetConstantBuffers(0, 3, buffers);
        context.PSSetConstantBuffers(0, 2, buffers);
    }

    protected override unsafe void PerApplicationUpdate()
    {
        base.PerApplicationUpdate();

        PackLights();
        applicationData = new RasterApplicationBuffer(Description.ProjectionDesc.MakeMatrix(), Width, Height);

        MappedSubresource resource = context.Map(buffers[0], MapMode.WriteDiscard);
        IntPtr pointer = resource.DataPointer;
        Marshal.StructureToPtr(applicationData, pointer, true);
        pointer += Marshal.SizeOf(applicationData);
        WriteStructArray(packedLights, ref pointer);
        context.Unmap(buffers[0]);
    }

    protected override void PerFrameUpdate()
    {
        base.PerFrameUpdate();

        frameData = new RasterFrameBuffer(CreateView(EyePos, EyeRot), EyePos, sw.ElapsedTicks % 60L);

        MappedSubresource resource = context.Map(buffers[1], MapMode.WriteNoOverwrite);
        Marshal.StructureToPtr(frameData, resource.DataPointer, true);
        context.Unmap(buffers[1]);
    }

    private void PerLightUpdate()
    {
        MappedSubresource resource = context.Map(buffers[2], MapMode.WriteNoOverwrite);
        Marshal.StructureToPtr(lightData, resource.DataPointer, true);
        context.Unmap(buffers[2]);
    }

    protected override void Render()
    {
        //// shadow rasterization
        //context.RSSetState(shadowRasterizer);
        //context.VSSetShader(shadowVertexShader);
        //context.PSSetShader(shadowPixelShader);
        //for (int i = 0; i < lights.Count; i++)
        //{
        //    context.ClearDepthStencilView(lights[i].ShadowStencilView, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1.0f, 0);
        //    context.OMSetRenderTargets(new ID3D11RenderTargetView[0], lights[i].ShadowStencilView);
        //    context.RSSetViewport(lights[i].ShadowViewPort);
        //    PerLightUpdate();

        //    for (int j = 0; j < gameobjects.Count; j++)
        //    {
        //        context.DrawInstanced(gameobjects[j].Triangles.Length * 3, 1, gameobjects[j].VerticesOffset, j);
        //    }
        //}

        //// lighting shader pass
        //context.RSSetState(lightingRasterizer);
        //context.VSSetShader(vertexShader);
        //context.PSSetShader(lightingPixelShader);
        //context.RSSetViewport(screenViewport);
        //for (int i = 0; i < lights.Count; i++)
        //{
        //    context.ClearRenderTargetView(lights[i].LightTargetView, Colors.Black);
        //    context.ClearDepthStencilView(depthStencilView, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1.0f, 0);
        //    context.OMSetRenderTargets(lights[i].LightTargetView, depthStencilView);

        //    if (i != 0)
        //    {
        //        ID3D11ShaderResourceView[] views = new ID3D11ShaderResourceView[2];
        //        views[0] = lights[i - 1].LightResourceView;
        //        views[1] = lights[i].ShadowResourceView;
        //        context.PSSetShaderResources(0, views);
        //    }
        //    else
        //    {
        //        context.PSSetShaderResource(1, lights[i].ShadowResourceView);
        //    }

        //    for (int j = 0; j < gameobjects.Count; j++)
        //    {
        //        context.DrawInstanced(gameobjects[j].Triangles.Length * 3, 1, gameobjects[j].VerticesOffset, j);
        //    }
        //}

        // color calculation
        context.ClearRenderTargetView(renderTargetView, new Color4(0.0f, 0.0f, 0.0f, float.PositiveInfinity));
        context.ClearDepthStencilView(depthStencilView, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1.0f, 0);
        context.OMSetRenderTargets(renderTargetView, depthStencilView);
        context.PSSetShader(pixelShader);
        //ID3D11ShaderResourceView1[] shaderViews = new ID3D11ShaderResourceView1[2];
        //shaderViews[0] = lights[lights.Count - 1].LightResourceView;
        //context.PSSetShaderResources(0, shaderViews);
        for (int i = 0; i < gameobjects.Count; i++)
        {
            context.DrawInstanced(gameobjects[i].Triangles.Length * 3, 1, gameobjects[i].VerticesOffset, i);
        }

        // post processing
        //context.GenerateMips(postResourceView);
        //context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleStrip;
        //context.OutputMerger.SetRenderTargets(renderTargetView);
        //context.InputAssembler.InputLayout = planeInputLayout;
        //context.VertexShader.Set(planeVertexShader);
        //context.PixelShader.Set(postProcessPixelShader);
        //context.PixelShader.SetShaderResource(0, postResourceView);

        //UpdateMainBufferResources();
        //matrixBuffer.LightIndex = filter;
        //context.UpdateSubresource(ref matrixBuffer, projectionBuffer, 0);

        //context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(planeVertexBuffer, Utilities.SizeOf<VertexPositionTexture>(), 0));
        //context.Draw(planeVertices.Length, 0);
    }
}
