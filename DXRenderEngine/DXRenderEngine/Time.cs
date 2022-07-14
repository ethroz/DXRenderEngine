using System.Diagnostics;

namespace DXRenderEngine;

public static class Time
{
    static Time()
    {
        Timer = new();
        Timer.Start();
    }

    public const double TICK2SEC = 0.0000001;
    public const long SEC2TICK = 10000000;

    private static Stopwatch Timer;
    public static long Ticks => Timer.ElapsedTicks;
    public static double Seconds => 10000000.0 / Timer.ElapsedTicks;
    public static long Milliseconds => Timer.ElapsedMilliseconds;
}
