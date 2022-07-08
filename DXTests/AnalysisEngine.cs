using Vortice.Direct3D11;

namespace DXTests;

public class AnalysisEngine : RasterizingEngine
{
    public new readonly AnalysisEngineDescription Description;
    public readonly Action<IntPtr, int> Analyze;
    public double DepthBias = 0.0;

    public AnalysisEngine(AnalysisEngineDescription ED) : base(ED)
    {
        window.ShowInTaskbar = !ED.Hidden;
        Description = ED;
        Analyze = ED.Analyze;
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
        var desc1 = renderTargetBuffer.Description1;
        desc1.BindFlags = BindFlags.None;
        desc1.Usage = ResourceUsage.Staging;
        desc1.CpuAccessFlags = CpuAccessFlags.Read;
        ID3D11Texture2D1 tex = device.CreateTexture2D1(desc1);
        context.CopyResource(tex, renderTargetBuffer);
        var mapped = context.Map(tex, 0, MapMode.Read, MapFlags.None);
        Analyze(mapped.DataPointer, Width * Height);
        context.Unmap(tex, 0);
        tex.Dispose();

        if (!Description.Hidden)
            swapChain.Present(0);
    }
}
