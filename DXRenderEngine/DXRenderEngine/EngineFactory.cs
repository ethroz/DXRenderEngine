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
            engine = new Engine(desc);
        return engine;
    }
}
