using System;

namespace DXRenderEngine;

public static class EngineFactory
{
    public static Engine Create(EngineDescription desc)
    {
        Engine engine;
        if (desc is RasterizingEngineDescription)
            engine = new RasterizingEngine((RasterizingEngineDescription)desc);
        else if (desc is RayTracingEngineDescription)
            engine = new RayTracingEngine((RayTracingEngineDescription)desc);
        else
            throw new();
        return engine;
    }

    public static Engine Create(EngineDescription desc, string shaderCode)
    {
        Engine engine = new(desc);
        engine.SetShaderCode(shaderCode);
        return engine;
    }

    public static RasterizingEngine Create(RasterizingEngineDescription desc)
    {
        return new(desc);
    }

    public static RayTracingEngine Create(RayTracingEngineDescription desc)
    {
        return new(desc);
    }
}
