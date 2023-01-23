using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using static DXRenderEngine.Helpers;
using static DXRenderEngine.Time;

namespace DXRenderEngine;

/// <summary>
/// TODO
///     shadow acne
///     pcf/pcs
///     tesselation
/// </summary>
public class RasterizingEngine : Engine
{
    public new readonly RasterizingEngineDescription Description;

    protected ID3D11RasterizerState2 shadowRasterizer;
    protected ID3D11VertexShader shadowVertexShader;
    protected ID3D11GeometryShader shadowGeometryShader;
    protected ID3D11PixelShader shadowPixelShader;

    protected Texture2DDescription1 depthViewDescription;
    protected DepthStencilViewDescription stencilViewDescription;
    protected ID3D11DepthStencilState depthState;
    protected ID3D11Texture2D1 depthBuffer;
    protected ID3D11DepthStencilView depthView;

    protected Texture2DDescription1 lightViewDesciption;
    protected ID3D11RasterizerState2 lightingRasterizer;
    protected ID3D11Texture2D1 lightViewTexture;
    protected ID3D11RenderTargetView1 lightingTargetView;

    protected ID3D11ShaderResourceView1[] shadowMaps;

    protected RasterPackedLight[] packedLights;

    protected static readonly Color4 backgroundColor = new Color4(0.0f, 0.0f, 0.0f, float.PositiveInfinity);

    protected internal RasterizingEngine(RasterizingEngineDescription ED) : base(ED)
    {
        Description = ED;
    }

    protected override void UpdateShaderConstants()
    {
        base.UpdateShaderConstants();

        ModifyShaderCode("NUM_LIGHTS 1", "NUM_LIGHTS " + lights.Count);
    }

    protected override void InitializeDeviceResources()
    {
        CreateRenderDescriptions();

        base.InitializeDeviceResources();

        InitializeRasterResources();
    }

    protected virtual void CreateRenderDescriptions()
    {
        lightViewDesciption = new(
            Format.R32G32B32A32_Float,
            Width,
            Height,
            bindFlags: BindFlags.ShaderResource | BindFlags.RenderTarget);
        depthViewDescription = new(
            Format.D32_Float,
            Width,
            Height,
            bindFlags: BindFlags.DepthStencil);
        stencilViewDescription = new(
            DepthStencilViewDimension.Texture2D,
            Format.D32_Float);
    }

    protected override void SetRenderBuffers(int width, int height)
    {
        // Perform any resizing first
        base.SetRenderBuffers(width, height);

        // lighting rendertargets
        lightViewDesciption.Width = width;
        lightViewDesciption.Height = height;
        lightViewTexture = device.CreateTexture2D1(lightViewDesciption);
        lightingTargetView = device.CreateRenderTargetView1(lightViewTexture);

        // depth buffers
        depthViewDescription.Width = width;
        depthViewDescription.Height = height;
        depthBuffer = device.CreateTexture2D1(depthViewDescription);
        depthView = device.CreateDepthStencilView(depthBuffer, stencilViewDescription);
    }

    protected override void ReleaseRenderBuffers()
    {
        // Perform any unsetting first
        base.ReleaseRenderBuffers();

        // lighting rendertargets
        lightViewTexture.Release();
        lightingTargetView.Release();

        // depth buffers
        depthBuffer.Release();
        depthView.Release();
    }

    private void InitializeRasterResources()
    {
        // depth buffers
        DepthStencilDescription dssdesc = DepthStencilDescription.Default;
        depthState = device.CreateDepthStencilState(dssdesc);
        context.OMSetDepthStencilState(depthState);
        RasterizerDescription2 rsdesc =
            Description.Wireframe
            ? RasterizerDescription2.Wireframe
            : RasterizerDescription2.CullBack;
        lightingRasterizer = device.CreateRasterizerState2(rsdesc);

        // shadow buffer
        DepthStencilViewDescription dsvdesc = new(
            DepthStencilViewDimension.Texture2DArray,
            Format.D32_Float,
            arraySize: 6);
        ShaderResourceViewDescription1 srvdesc = new(
            ShaderResourceViewDimension.TextureCube,
            Format.R32_Float,
            0, 1);
        Texture2DDescription1 td = new(Format.R32_Typeless, 1, 1, 6, 1,
            bindFlags: BindFlags.ShaderResource | BindFlags.DepthStencil, 
            miscFlags: ResourceOptionFlags.TextureCube);

        shadowMaps = new ID3D11ShaderResourceView1[lights.Count];
        for (int i = 0; i < lights.Count; ++i)
        {
            td.Width = td.Height = lights[i].ShadowRes;
            lights[i].ShadowTextures = device.CreateTexture2D1(td);
            lights[i].ShadowStencilView = device.CreateDepthStencilView(lights[i].ShadowTextures, dsvdesc);
            shadowMaps[i] = device.CreateShaderResourceView1(lights[i].ShadowTextures, srvdesc);
            lights[i].ShadowViewPort = new(0, 0, lights[i].ShadowRes, lights[i].ShadowRes, 0.0f, 1.0f);
            lights[i].ShadowProjectionMatrix = CreateProjection(90.0f, 1.0f, lights[i].NearPlane, lights[i].FarPlane);
        }

        // shadow rasterizer
        rsdesc = RasterizerDescription2.CullBack;
        shadowRasterizer = device.CreateRasterizerState2(rsdesc);
    }

    protected override void InitializeShaders()
    {
        base.InitializeShaders();

        AsyncGetShader("shadowVertexShader", "VertexShader", "vs_5_0",
            (Blob b) =>
            {
                shadowVertexShader = device.CreateVertexShader(b);
            });

        AsyncGetShader("shadowGeometryShader", "GeometryShader", "gs_5_0",
            (Blob b) =>
            {
                shadowGeometryShader = device.CreateGeometryShader(b);
            });

        AsyncGetShader("shadowPixelShader", "PixelShader", "ps_5_0",
            (Blob b) =>
            {
                shadowPixelShader = device.CreatePixelShader(b);
            });

        // Use 2 samplers.
        samplers = new ID3D11SamplerState[2];

        // color sampler
        SamplerDescription ssdesc = SamplerDescription.LinearClamp;
        samplers[0] = device.CreateSamplerState(ssdesc);

        // depth sampler
        ssdesc = new SamplerDescription(
            Filter.ComparisonMinMagMipLinear,
            TextureAddressMode.Border, 
            TextureAddressMode.Border, 
            TextureAddressMode.Border,
            comparisonFunction: ComparisonFunction.LessEqual);
        ssdesc.BorderColor = Colors.White;
        samplers[1] = device.CreateSamplerState(ssdesc);

        context.PSSetSamplers(0, samplers.Length, samplers);
    }

    protected override void SetConstantBuffers()
    {
        base.SetConstantBuffers();

        packedLights = new RasterPackedLight[lights.Count];
        PackLights();

        context.VSSetConstantBuffers(0, cBuffers.Length, buffers);
        context.GSSetConstantBuffer(1, buffers[1]);
        context.GSSetConstantBuffer(2, buffers[2]);
        context.PSSetConstantBuffers(0, cBuffers.Length, buffers);
    }

    protected override void PerApplicationUpdate()
    {
        base.PerApplicationUpdate();

        cBuffers[0].Insert(Description.ProjectionDesc.GetMatrix(), 0);
        cBuffers[0].Insert(LowerAtmosphere, 1);
        cBuffers[0].Insert(Width, 2);
        cBuffers[0].Insert(UpperAtmosphere, 3);
        cBuffers[0].Insert(Height, 4);

        UpdateConstantBuffer(0, MapMode.WriteDiscard);
    }

    protected override void PerFrameUpdate()
    {
        base.PerFrameUpdate();

        PackLights();

        cBuffers[1].Insert(CreateView(EyePos, EyeRot), 0);
        cBuffers[1].Insert(EyePos, 1);
        cBuffers[1].Insert((float)(Ticks % 60L), 2);
        cBuffers[1].InsertArray(packedLights, 3);

        foreach (var l in lights)
            l.GenerateMatrix();

        UpdateConstantBuffer(1);
    }

    protected override void PerLightUpdate(int index)
    {
        base.PerLightUpdate(index);

        cBuffers[2].InsertArray(lights[index].ShadowMatrices, 0);
        cBuffers[2].Insert((uint)index, 6);

        UpdateConstantBuffer(2, MapMode.WriteDiscard);
    }

    protected override void PerObjectUpdate(int index)
    {
        base.PerObjectUpdate(index);

        cBuffers[3].Insert(gameobjects[index].World, 0);
        cBuffers[3].Insert(gameobjects[index].Normal, 1);
        cBuffers[3].Insert(gameobjects[index].Material, 2);

        UpdateConstantBuffer(3, MapMode.WriteDiscard);
    }

    protected void PackLights()
    {
        for (int i = 0; i < lights.Count; ++i)
        {
            packedLights[i] = lights[i].RasterPack();
        }
    }

    protected override void Render()
    {
        // Multi Light /////////////////////////////////////////////////////////////////////////////

        // shadow rasterization
        context.RSSetState(shadowRasterizer);
        context.VSSetShader(shadowVertexShader);
        context.GSSetShader(shadowGeometryShader);
        context.PSSetShader(shadowPixelShader);
        // clear all resource views to avoid conflicts
        ID3D11ShaderResourceView[] nulls = new ID3D11ShaderResourceView[lights.Count + 1];
        context.PSSetShaderResources(0, nulls);
        for (int i = 0; i < lights.Count; ++i)
        {
            context.ClearDepthStencilView(lights[i].ShadowStencilView, DepthStencilClearFlags.Depth, 1.0f, 0);
            context.OMSetRenderTargets(new ID3D11RenderTargetView[i], lights[i].ShadowStencilView);
            context.RSSetViewport(lights[i].ShadowViewPort);
            PerLightUpdate(i);

            for (int j = 0; j < gameobjects.Count; ++j)
            {
                PerObjectUpdate(j);
                context.Draw(gameobjects[j].Triangles.Length * 3, gameobjects[j].Offset);
            }
        }

        // object lighting
        context.RSSetState(lightingRasterizer);
        context.VSSetShader(vertexShader);
        context.GSSetShader(null);
        context.PSSetShader(pixelShader);
        context.RSSetViewport(screenViewport);
        context.ClearDepthStencilView(depthView, DepthStencilClearFlags.Depth, 1.0f, 0);
        context.ClearRenderTargetView(renderTargetView, backgroundColor);
        context.OMSetRenderTargets(renderTargetView, depthView);
        context.PSSetShaderResources(1, shadowMaps);
        for (int i = 0; i < gameobjects.Count; ++i)
        {
            PerObjectUpdate(i);
            context.Draw(gameobjects[i].Triangles.Length * 3, gameobjects[i].Offset);
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

        //context.InputAssembler.SetVertexBuffers(0, new(planeVertexBuffer, Utilities.SizeOf<VertexPositionTexture>(), 0));
        //context.Draw(planeVertices.Length, 0);
    }

    protected override void Dispose(bool boolean)
    {
        if (shadowMaps != null)
            foreach (var map in shadowMaps)
                map?.Dispose();
        lightingTargetView?.Dispose();
        lightingRasterizer?.Dispose();
        shadowPixelShader?.Dispose();
        shadowVertexShader?.Dispose();
        shadowRasterizer?.Dispose();
        depthView?.Dispose();
        depthState?.Dispose();
        depthBuffer?.Dispose();
        lightViewTexture?.Dispose();

        base.Dispose(boolean);
    }
}
