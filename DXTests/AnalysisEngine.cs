using Vortice.Direct3D11;

namespace DXTests;

public class AnalysisEngine : RasterizingEngine
{
    public new readonly AnalysisEngineDescription Description;
    protected ID3D11Texture2D1 analysisBuffer;
    public readonly Action<IntPtr, int> Analyze;
    public double DepthBias = 0.0;

    public AnalysisEngine(AnalysisEngineDescription ED) : base(ED)
    {
        window.ShowInTaskbar = !ED.Hidden;
        Description = ED;
        Analyze = ED.Analyze;
    }

    protected override void ReleaseRenderBuffers()
    {
        // Perform any unsetting first
        base.ReleaseRenderBuffers();

        analysisBuffer.Release();
    }

    protected override void SetRenderBuffers(int width, int height)
    {
        base.SetRenderBuffers(width, height);

        using (var buffer = swapChain.GetBuffer<ID3D11Texture2D1>(0))
        {
            var desc1 = buffer.Description1;
            desc1.BindFlags = BindFlags.None;
            desc1.Usage = ResourceUsage.Staging;
            desc1.CPUAccessFlags = CpuAccessFlags.Read;
            analysisBuffer = device.CreateTexture2D1(desc1);
        }
    }

    protected override void PerLightUpdate(int index)
    {
        cBuffers[2].Insert((float)DepthBias, 7);
        cBuffers[2].Insert(Description.Manual, 8);

        base.PerLightUpdate(index);
    }

    protected override void RenderFlow()
    {
        if (Description.Hidden && Focused)
            window.Hide();

        GetTime();
        Update();
        PerFrameUpdate();
        Render();

        // make sure it has finished rendering
        context.Flush();

        // analyze the output
        context.CopyResource(analysisBuffer, renderTargetView.Resource);
        var mapped = context.Map(analysisBuffer, 0, MapMode.Read, MapFlags.None);
        Analyze(mapped.DataPointer, Width * Height);
        context.Unmap(analysisBuffer, 0);

        if (!Description.Hidden)
            swapChain.Present(0);
    }

    protected override void Dispose(bool boolean)
    {
        analysisBuffer?.Dispose();

        base.Dispose(boolean);
    }
}
