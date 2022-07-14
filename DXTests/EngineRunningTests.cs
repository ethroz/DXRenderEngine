using System.Diagnostics;
using System.Reflection;
using System.Windows.Forms;
using Vortice.DirectInput;

namespace DXTests;

[TestClass]
public class EngineRunningTests
{
    static EngineRunningTests()
    {
        // set magnitudes
        double start = 1.0 / Pow(10, minSigFigs);
        int num = maxSigFigs - minSigFigs + 1;
        magnitudes = new double[num];
        for (int i = 0; i < num; ++i)
        {
            magnitudes[i] = start;
            start /= 10.0;
        }

        // set results folder path
        string assembly = Assembly.GetExecutingAssembly().FullName;
        assembly = assembly.Substring(0, assembly.IndexOf(','));
        resultsPath = Assembly.GetExecutingAssembly().Location;
        while (!resultsPath.EndsWith(assembly))
            resultsPath = Directory.GetParent(resultsPath).FullName;
        resultsPath += Path.DirectorySeparatorChar + "Results" + Path.DirectorySeparatorChar;

        // Get the correct file suffix to avoid overwriting
        if (Overwrite == 0)
        {
            fileSuffix = 0;
            var files = Directory.GetFiles(resultsPath);
            foreach (var file in files)
            {
                int index = -1;
                for (int i = 0; i < file.Length; ++i)
                {
                    if (char.IsDigit(file[i]))
                    {
                        index = i;
                    }
                    else if (index != -1)
                    {
                        fileSuffix = Math.Max(fileSuffix, int.Parse(file.Substring(index, i - index)));
                        break;
                    }
                }
            }
            ++fileSuffix;
        }
        else
        {
            fileSuffix = Overwrite;
        }
    }

    const bool Hidden = true;
    const bool DoManual = true;
    const bool Save = false;
    const int Overwrite = 0;

    static AnalysisEngine engine;
    static List<double> biases = new();
    static readonly string resultsPath;
    static readonly int fileSuffix;
    static long t1;
    static bool allWhite;
    static int magIndex;
    static int lastIndex;
    static double loBias;
    static double hiBias;
    const double loStart = -0.001;
    const double hiStart = 7.0;
    static readonly double threshold = 1.1 / Pow(10, maxSigFigs);
    const int minSigFigs = 0;
    const int maxSigFigs = 4;
    static readonly double[] magnitudes;
    static Vars v1;
    static Vars v2;
    static float minimum;
    static float maximum;
    static float increment;
    static bool Manual;
    const float MoveSpeed = 4.0f;
    const float Sensitivity = 0.084f;

    private static void UpdateConstants()
    {
        float phi = -v2.phi;
        engine.lights[0].Position = new(0.0f, (float)(v2.dist * Math.Cos(phi * DEG2RAD)), (float)(v2.dist * Math.Sin(phi * DEG2RAD)));
        engine.lights[0].FarPlane = v2.far;
        engine.lights[0].ShadowRes = v2.res;
        float total = phi - v2.theta;
        engine.gameobjects[0].Rotation.X = total;
        if (!Manual)
        {
            float eyeDist = engine.Description.ProjectionDesc.NearPlane * 1.0001f;
            engine.EyePos = new(0.0f, (float)(eyeDist * Math.Cos(total * DEG2RAD)), (float)(eyeDist * Math.Sin(total * DEG2RAD)));
            engine.EyeRot = new(90.0f + total, 0.0f, 0.0f);
        }
    }

    private static void Setup()
    {
        engine.lights.Add(new());

        TriNormsCol[] planeVerts = new TriNormsCol[2]
        {
            new(new Vector3[] { new(-1.0f, 0.0f, -1.0f), new(-1.0f, 0.0f, 1.0f),
                new( 1.0f, 0.0f,  1.0f) }, Vector3.UnitY),
            new(new Vector3[] { new(-1.0f, 0.0f, -1.0f), new( 1.0f, 0.0f, 1.0f),
                new( 1.0f, 0.0f, -1.0f) }, Vector3.UnitY)
        };
        engine.gameobjects.Add(new("Plane", new(), new(), new(1.0f), planeVerts, Material.Default));

        if (Manual)
        {
            Manual = false;
            UpdateConstants();
            Manual = true;
        }
        else
        {
            UpdateConstants();
        }
    }

    private static void Next()
    {
        if (engine.DepthBias == 0.0)
            engine.DepthBias = 0.0;
        if (!Hidden)
            Trace.WriteLine("v=" + v2.ToString() + " bias=" + engine.DepthBias);
        biases.Add(engine.DepthBias);

        v2 += increment;

        // exit condition
        if (v2 > maximum)
            engine.Stop();

        // reset
        magIndex = 0;
        UpdateConstants();
    }

    private static void SmartNext()
    {
        Next();
        double diff = Round(Math.Abs(engine.DepthBias - loBias), true);
        if (diff == 0.0)
            magIndex = maxSigFigs;
        else
            magIndex = Math.Min((int)Math.Log10(1.0 / diff) + 1, maxSigFigs);
        loBias = engine.DepthBias;
        ChangeBias(Math.Sign(increment) * magnitudes[magIndex]);
    }

    private static void ChangeBias(double amount)
    {
        engine.DepthBias = (float)Math.Round(engine.DepthBias + amount, maxSigFigs, MidpointRounding.ToEven);
    }

    private static void SetBias(double newVal, bool up)
    {
        engine.DepthBias = Math.Round(newVal, maxSigFigs, up ? MidpointRounding.ToPositiveInfinity : MidpointRounding.ToNegativeInfinity);
    }

    private static double Round(double val, bool up)
    {
        return Math.Round(val, maxSigFigs, up ? MidpointRounding.ToPositiveInfinity : MidpointRounding.ToNegativeInfinity);
    }

    private static void ResetPointers()
    {
        loBias = loStart;
        hiBias = hiStart;
        engine.DepthBias = 0.0;
    }

    private static void BinarySearch()
    {
        // skip a cycle
        if (magIndex == -2)
        {
            magIndex = 0;
            ResetPointers();
        }
        // remove all shadow acne from the plane
        else
        {
            if (allWhite)
            {
                if (hiBias - loBias < threshold)
                {
                    Next();
                    ResetPointers();
                }
                else
                {
                    hiBias = engine.DepthBias;
                    SetBias((hiBias + loBias) / 2.0, false);
                }
            }
            else
            {
                loBias = engine.DepthBias;
                SetBias((hiBias + loBias) / 2.0, true);
            }
        }
    }

    private static void SmartSearch()
    {
        // This must be true
        Trace.Assert(minSigFigs == 0);

        // skip a cycle
        if (magIndex == -2)
        {
            magIndex = 0;
            lastIndex = 0;
            loBias = 0.0;
            engine.DepthBias = 0.0;
        }
        // remove all shadow acne from the plane
        else
        {
            if (allWhite)
            {
                if (lastIndex > 0)
                {
                    if (magIndex == magnitudes.Length - 1)
                    {
                        lastIndex = 0;
                        SmartNext();
                    }
                    else
                    {
                        lastIndex = 0;
                        ChangeBias(-magnitudes[magIndex++] * 0.9);
                    }
                }
                else
                {
                    lastIndex--;
                    ChangeBias(-magnitudes[magIndex]);
                }
            }
            else
            {
                if (lastIndex < 0)
                {
                    if (magIndex == magnitudes.Length - 1)
                    {
                        lastIndex = 0;
                        ChangeBias(magnitudes[magIndex]);
                        SmartNext();
                    }
                    else
                    {
                        lastIndex = 0;
                        ChangeBias(magnitudes[magIndex++] * 0.9);
                    }
                }
                else
                {
                    lastIndex++;
                    ChangeBias(magnitudes[magIndex]);
                }
            }
        }
    }

    private static void Update()
    {
        // skip a cycle
        if (magIndex == -2)
        {
            magIndex = 0;
        }
        // remove all shadow acne from the plane
        else
        {
            if (allWhite)
            {
                if (lastIndex > 0)
                {
                    if (magIndex == magnitudes.Length - 1)
                    {
                        lastIndex = 0;
                        Next();
                    }
                    else
                    {
                        lastIndex = 1;
                        ChangeBias(-magnitudes[magIndex] + magnitudes[++magIndex]);
                    }
                }
                else
                {
                    lastIndex--;
                    ChangeBias(-magnitudes[magIndex]);
                }
            }
            else
            {
                if (lastIndex < 0)
                {
                    if (magIndex == magnitudes.Length - 1)
                    {
                        lastIndex = 0;
                        ChangeBias(magnitudes[magIndex]);
                        Next();
                    }
                    else
                    {
                        lastIndex = -1;
                        ChangeBias(magnitudes[magIndex] - magnitudes[++magIndex]);
                    }
                }
                else
                {
                    lastIndex++;
                    ChangeBias(magnitudes[magIndex]);
                }
            }
        }
    }

    private static void Update2()
    {
        // skip a cycle
        if (magIndex == -2)
        {
            magIndex = 0;
        }
        // remove all shadow acne from the plane
        else
        {
            if (allWhite)
            {
                if (lastIndex > 0)
                {
                    if (magIndex == magnitudes.Length - 1)
                    {
                        lastIndex = 0;
                        Next();
                        engine.DepthBias = 0.0;
                    }
                    else
                    {
                        lastIndex = 0;
                        ChangeBias(-magnitudes[magIndex++] * 0.9);
                    }
                }
                else
                {
                    lastIndex--;
                    ChangeBias(-magnitudes[magIndex]);
                }
            }
            else
            {
                if (lastIndex < 0)
                {
                    if (magIndex == magnitudes.Length - 1)
                    {
                        lastIndex = 0;
                        ChangeBias(magnitudes[magIndex]);
                        Next();
                        engine.DepthBias = 0.0;
                    }
                    else
                    {
                        lastIndex = 0;
                        ChangeBias(magnitudes[magIndex++] * 0.1);
                    }
                }
                else
                {
                    lastIndex++;
                    ChangeBias(magnitudes[magIndex]);
                }
            }
        }
    }

    private static void TimedStop()
    {
        // Just wait to exit
        if (Milliseconds - t1 > 2000)
        {
            engine.Stop();
        }
    }

    static void UserInput()
    {
        if (engine.input.KeyDown(Key.Escape))
            engine.Stop();
        if (engine.input.KeyDown(Key.F11))
            engine.ToggleFullscreen();

        if (engine.input.KeyDown(Key.Back))
            engine.EyePos = engine.EyeStartPos;

        float speed = MoveSpeed;

        if (engine.input.KeyHeld(Key.CapsLock))
            speed *= 10.0f;
        else if (engine.input.KeyHeld(Key.LeftShift))
            speed *= 2.0f;
        else if (engine.input.KeyHeld(Key.LeftControl))
            speed /= 5.0f;
        POINT pos = engine.input.GetDeltaMousePos();
        engine.EyeRot.Y += pos.X * Sensitivity;
        engine.EyeRot.X += pos.Y * Sensitivity;
        engine.EyeRot.X = Math.Max(Math.Min(engine.EyeRot.X, 90.0f), -90.0f);
        if (engine.input.KeyHeld(Key.Comma))
            engine.EyeRot.Z -= speed * (float)engine.ElapsedTime * 20.0f;
        if (engine.input.KeyHeld(Key.Period))
            engine.EyeRot.Z += speed * (float)engine.ElapsedTime * 20.0f;
        Matrix4x4 rot = CreateRotation(engine.EyeRot);
        float normalizer = Math.Max((float)Math.Sqrt((engine.input.KeyHeld(Key.A) ^ engine.input.KeyHeld(Key.D) ? 1 : 0) + (engine.input.KeyHeld(Key.W) ^ engine.input.KeyHeld(Key.S) ? 1 : 0) + (engine.input.KeyHeld(Key.E) ^ engine.input.KeyHeld(Key.Q) ? 1 : 0)), 1.0f);
        Vector3 forward = Vector3.TransformNormal(Vector3.UnitZ, rot) / normalizer;
        Vector3 right = Vector3.TransformNormal(Vector3.UnitX, rot) / normalizer;
        Vector3 up = Vector3.TransformNormal(Vector3.UnitY, rot) / normalizer;
        if (engine.input.KeyHeld(Key.A))
            engine.EyePos -= right * (float)engine.ElapsedTime * speed;
        if (engine.input.KeyHeld(Key.D))
            engine.EyePos += right * (float)engine.ElapsedTime * speed;
        if (engine.input.KeyHeld(Key.W))
            engine.EyePos += forward * (float)engine.ElapsedTime * speed;
        if (engine.input.KeyHeld(Key.S))
            engine.EyePos -= forward * (float)engine.ElapsedTime * speed;
        if (engine.input.KeyHeld(Key.Q))
            engine.EyePos -= up * (float)engine.ElapsedTime * speed;
        if (engine.input.KeyHeld(Key.E))
            engine.EyePos += up * (float)engine.ElapsedTime * speed;

        // Depth bias adjustment
        if (engine.input.KeyHeld(Key.CapsLock))
            magIndex = 0;
        else if (engine.input.KeyHeld(Key.LeftShift))
            magIndex = 1;
        else if (engine.input.KeyHeld(Key.LeftControl))
            magIndex = 2;
        else if (engine.input.KeyHeld(Key.Space))
            magIndex = 4;
        else
            magIndex = 3;

        double mult = 0.0;
        if (engine.input.KeyDown(Key.Down))
            mult = -1.0;
        if (engine.input.KeyDown(Key.Up))
            mult = 1.0;

        if (mult != 0.0)
            ChangeBias(mult * magnitudes[magIndex]);

        if (engine.input.KeyDown(Key.Backslash))
            engine.DepthBias = 0.0f;

        // Variable incrementer
        int m = 0;
        if (engine.input.KeyDown(Key.Left))
            m = -1;
        if (engine.input.KeyDown(Key.Right))
            m = 1;
        if (engine.input.KeyHeld(Key.LeftShift))
            m *= 10;

        v2 += m;

        // print if buttons pressed
        if (m != 0 || mult != 0.0 || engine.input.KeyDown(Key.Backslash))
        {
            Trace.WriteLine(string.Format("bias={0:F" + maxSigFigs + "} theta={1} phi={2} light={3} plane={4}", 
                engine.DepthBias, v2.theta, v2.phi, engine.lights[0].Position, engine.gameobjects[0].Rotation.X));
            UpdateConstants();
        }
    }

    private static void SaveResults()
    {
        if (!Save)
            return;

        // get path
        string path = resultsPath + v2.var.ToString() + fileSuffix + ".txt";

        // find max variable sig figs
        int varFigs = 0;
        for (float i = minimum; i < maximum; i += increment)
        {
            varFigs = Math.Max((int)Math.Log10(1.0 / Math.Abs(i)), varFigs);
        }
        varFigs = Math.Min(varFigs, 4);

        // format and save
        string[] lines = new string[biases.Count];
        float val = minimum;
        for (int i = 0; i < lines.Length; ++i, val += increment)
            lines[i] = string.Format("{0:F" + varFigs + "}\t{1:F" + maxSigFigs + "}", val, biases[i]);
        File.WriteAllLines(path, lines);
        Trace.WriteLine("Saved to: " + path);
    }

    private static unsafe void Analyzer(IntPtr pointer, int length)
    {
        if (length % 8 != 0)
        {
            throw new NotImplementedException();
        }

        allWhite = true;
        int* ptr = (int*)pointer;
        int* end = ptr + length;
        while (ptr < end)
        {
            var temp1 = *ptr & *(ptr + 1);
            var temp2 = *(ptr + 2) & *(ptr + 3);
            var temp3 = *(ptr + 4) & *(ptr + 5);
            var temp4 = *(ptr + 6) & *(ptr + 7);
            var temp5 = temp1 & temp2;
            var temp6 = temp3 & temp4;
            var total = temp5 & temp6 & -1;
            if (total != -1)
            {
                allWhite = false;
                break;
            }
            ptr += 8;
        }
    }

    private static void CreateAndRun(Var var, float min, float max, float inc, float f1, float f2, float f3, float f4)
    {
        minimum = min;
        maximum = max;
        increment = inc;
        v2 = new(var, min, f1, f2, f3, f4);

        engine = new(new(new(new(new(90.0f, 0.01f, 100.0f), "", 640, 640, -1, -1, 0, setup: Setup, update: SmartSearch, 
            windowState: Hidden ? FormWindowState.Minimized : FormWindowState.Normal), shadows: true), 
            Analyzer, Hidden, Manual));
        t1 = Milliseconds;
        engine.Run();
        engine.Dispose();

        SaveResults();
    }

    [TestInitialize]
    public void TestSetup()
    {
        magIndex = -2;
        lastIndex = 0;
        Manual = false;
    }

    [TestCleanup]
    public void TestClean()
    {
        biases.Clear();
        if (engine != null)
            engine.Dispose();
        engine = null;
    }

    [TestMethod]
    public void RunManual()
    {
        if (!DoManual)
            return;

        v2 = new(Var.Distance, 100.0f, 0.0f, 0.0f, 100.0f, 4096.0f);

        engine = new(new(new(new(new(90.0f, 0.01f, 100.0f), "", 640, 640, 0, setup: Setup, userInput: UserInput), shadows: true), 
            Analyzer, false, Manual));

        Manual = false; // With this set to false, changing variable value will reset the eye position and view direction
        engine.Run();
    }

    [TestMethod]
    public void VoidLoop()
    {
        if (Hidden || !DoManual)
            return;

        v2 = new(Var.Phi, 0.0f, 0.0f, 10.0f, 100.0f, 4096.0f);

        engine = new(new(new(new(new(90.0f, 0.01f, 100.0f), "", 640, 640, 0, setup: Setup, update: TimedStop), shadows: true),
            Analyzer, Hidden, Manual));

        engine.DepthBias = 1.0f;
        t1 = Milliseconds;
        engine.Run();
    }

    [TestMethod]
    public void ThreadAbort()
    {
        if (!DoManual)
            return;

        Engine eng = null;
        bool loopRan = false;

        var update = () =>
        {
            eng.Stop();
            while (true) { loopRan = true; }
        };

        eng = EngineFactory.Create(new(new(), "", update: update));
        eng.Run();
        Assert.IsTrue(loopRan);
    }

    [TestMethod]
    public void Shadow_Test_Theta()
    {
        CreateAndRun(Var.Theta, 0.0f, 89.0f, 1.0f, 0.0f, 10.0f, 100.0f, 4096.0f);
    }

    [TestMethod]
    public void Shadow_Test_Distance()
    {
        CreateAndRun(Var.Distance, 1.0f, 99.0f, 1.0f, 0.0f, 0.0f, 100.0f, 4096.0f);
    }

    [TestMethod]
    public void Shadow_Test_Phi()
    {
        CreateAndRun(Var.Phi, 0.0f, 89.0f, 1.0f, 0.0f, 10.0f, 100.0f, 4096.0f);
    }
}
