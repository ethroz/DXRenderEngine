using System.Diagnostics;

namespace DXTests;

internal enum Var : int
{
    Theta,
    Phi,
    Dist,
    Far,
    Res
}

struct Range
{
    public float Min;
    public float Max;
    public float Inc;

    public Range(float min, float max, float inc)
    {
        Trace.Assert(min < max);
        Trace.Assert(inc > 0.0f);
        float realMax = min;
        while (realMax < max)
            realMax += inc;
        Min = min;
        Max = realMax;
        Inc = inc;
    }

    public override string ToString()
    {
        return '[' + Min.ToString() + ", " + Max.ToString() + "):" + Inc.ToString();
    }

    public int GetIterations()
    {
        return (int)((Max - Min) / Inc);
    }

    public Range GetSubRange(float min = float.NaN, float max = float.NaN)
    {
        if (float.IsNaN(max))
            max = Max;
        if (float.IsNaN(min))
            min = Min;
        Trace.Assert(min < max);
        Trace.Assert(min >= Min);
        Trace.Assert(max <= Max);
        float realMin = Min;
        while (realMin < min)
            realMin += Inc;
        return new(realMin - Inc, max, Inc);
    }

    public Range GetSubRangeFromIter(int min, int max)
    {
        if (max == -1)
            max = GetIterations();
        Trace.Assert(min < max);
        int iterations = GetIterations();
        Trace.Assert(max <= iterations);
        float realMin = Min;
        for (int i = 0; i < min; ++i)
            realMin += Inc;
        float realMax = Min;
        for (int i = 0; i < max; ++i)
            realMax += Inc;
        return new(realMin, realMax, Inc);
    }
}

struct Vars
{
    static Vars()
    {
        MaxVars = 0;
        while (true)
        {
            if (((Var)MaxVars).ToString() == MaxVars.ToString())
            {
                break;
            }
            ++MaxVars;
        }
    }

    public static readonly int MaxVars;
    private Var[] variables;
    private Range[] ranges;
    private readonly float[] values;
    private readonly int size;
    public float Theta => values[(int)Var.Theta];
    public float Phi => values[(int)Var.Phi];
    public float Dist => values[(int)Var.Dist];
    public float Far => values[(int)Var.Far];
    public int Res => (int)values[(int)Var.Res];

    // Fastest to slowest in array
    public Vars(float f1, float f2, float f3, float f4, float f5, Var[] variables, Range[] ranges)
    {
        Trace.Assert(variables.Length <= MaxVars);
        Trace.Assert(variables.Length == ranges.Length);
        size = variables.Length;
        this.variables = variables;
        this.ranges = ranges;
        values = new float[] { f1, f2, f3, f4, f5 };
        for (int i = 0; i < variables.Length; ++i)
            values[(int)variables[i]] = ranges[i].Min;
    }

    public bool Increment()
    {
        for (int i = 0; i < variables.Length; ++i)
        {
            int index = (int)variables[i];
            values[index] += ranges[i].Inc;
            if (values[index] < ranges[i].Max)
                return false;
            else
                values[index] = ranges[i].Min;
        }
        return true;
    }

    public void Reset()
    {
        for (int i = 0; i < variables.Length; ++i)
        {
            values[(int)variables[i]] = ranges[i].Min;
        }
    }

    public float GetVar(int index = 0)
    {
        return values[(int)variables[index]];
    }

    public float GetMin(int index = 0)
    {
        return ranges[index].Min;
    }

    public float GetMax(int index = 0)
    {
        return ranges[index].Max;
    }

    public float GetInc(int index = 0)
    {
        return ranges[index].Inc;
    }

    public int GetSize()
    {
        return size;
    }

    public int GetIterations(int index = 0)
    {
        if (index < variables.Length)
            return ranges[index].GetIterations();
        return 1;
    }

    public string ConstantsWithValues()
    {
        string output = "";
        for (int i = 0; i < MaxVars; ++i)
        {
            bool contains = false;
            for (int j = 0; j < variables.Length; ++j)
            {
                if (i == (int)variables[j])
                { 
                    contains = true;
                    break;
                }
            }
            if (!contains)
                output += ((Var)i).ToString() + '=' + values[i].ToString() + ' ';
        }
        return output;
    }

    public string VariablesWithValues()
    {
        string output = "";
        for (int i = 0; i < variables.Length; i++)
            output += variables[i].ToString() + '=' + values[(int)variables[i]].ToString() + ' ';
        return output;
    }

    public string VariablesWithoutValues()
    {
        string output = "";
        for (int i = 0; i < variables.Length; i++)
            output += variables[i].ToString() + ' ';
        return output;
    }

    public string VariablesGetTableRange(int min = -1, int max = -1)
    {
        string output = "";
        for (int i = 0; i < variables.Length; ++i)
        {
            output += variables[i].ToString();
            if (i > 0)
            {
                output += '=';
                if (i == 1)
                    output += ranges[i].GetSubRangeFromIter(min, max).ToString();
                else
                    output += values[(int)variables[i]].ToString();
            }
            output += ' ';
        }
        return output;
    }

    public int NumTables()
    {
        int total = 1;
        for (int i = 2; i < ranges.Length; ++i)
        {
            total *= GetIterations(i);
        }
        return total;
    }

    public override string ToString()
    {
        string output = "";
        for(int i = 0; i < variables.Length; i++)
        {
            output += variables[i].ToString() + '=' + values[(int)variables[i]].ToString("G" + EngineRunningTests.maxSigFigs) + ' ';
        }
        return output;
    }
}

public class AnalysisEngineDescription : RasterizingEngineDescription
{
    public Action<IntPtr, int> Analyze { get; private set; }
    public Func<string> PercentDone { get; private set; }
    public bool Hidden;
    public bool Manual;
    private const string shader = "DXTests.TestShaders.hlsl";

    public AnalysisEngineDescription(RasterizingEngineDescription ED, Action<IntPtr, int> analyze, 
        Func<string> percentDone, bool hidden, bool manual) : base(ED)
    {
        ShaderResource = shader;
        if (analyze == null)
        {
            throw new ArgumentNullException("Analyzer cannot be null");
        }
        Analyze = analyze;
        PercentDone = percentDone;
        Hidden = hidden;
        Manual = manual;
    }
}
