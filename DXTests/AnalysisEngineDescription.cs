namespace DXTests;

internal enum Var : uint
{
    Undefined = 0,
    Theta = 1,
    Phi = 2,
    Distance = 3,
    Far = 4,
    Resolution = 5
}

struct Vars
{
    public Var var;
    public float theta;
    public float phi;
    public float dist;
    public float far;
    public int res;

    public Vars(Var v, float val, float f1, float f2, float f3, float f4)
    {
        var = v;
        switch (v)
        {
            case Var.Theta:
                theta = val;
                phi = f1;
                dist = f2;
                far = f3;
                res = (int)f4;
                break;
            case Var.Phi:
                theta = f1;
                phi = val;
                dist = f2;
                far = f3;
                res = (int)f4;
                break;
            case Var.Distance:
                theta = f1;
                phi = f2;
                dist = val;
                far = f3;
                res = (int)f4;
                break;
            case Var.Far:
                theta = f1;
                phi = f2;
                dist = f3;
                far = val;
                res = (int)f4;
                break;
            case Var.Resolution:
                theta = f1;
                phi = f2;
                dist = f3;
                far = f4;
                res = (int)val;
                break;
            default:
                throw new NotImplementedException("Unknown variable");
        }
    }

    public static Vars operator +(Vars v, float f)
    {
        switch (v.var)
        {
            case Var.Theta:
                v.theta += f;
                break;
            case Var.Phi:
                v.phi += f;
                break;
            case Var.Distance:
                v.dist += f;
                break;
            case Var.Far:
                v.far += f;
                break;
            case Var.Resolution:
                v.res += (int)f;
                break;
            default:
                throw new NotImplementedException("Unknown variable");
        }
        return v;
    }

    public static bool operator <(Vars v, float max)
    {
        switch (v.var)
        {
            case Var.Theta:
                return v.theta < max;
            case Var.Phi:
                return v.phi < max;
            case Var.Distance:
                return v.dist < max;
            case Var.Far:
                return v.far < max;
            case Var.Resolution:
                return v.res < max;
            default:
                throw new NotImplementedException("Unknown variable");
        }
    }

    public static bool operator >(Vars v, float min)
    {
        switch (v.var)
        {
            case Var.Theta:
                return v.theta > min;
            case Var.Phi:
                return v.phi > min;
            case Var.Distance:
                return v.dist > min;
            case Var.Far:
                return v.far > min;
            case Var.Resolution:
                return v.res > min;
            default:
                throw new NotImplementedException("Unknown variable");
        }
    }

    public override string ToString()
    {
        switch (var)
        {
            case Var.Theta:
                return theta.ToString();
            case Var.Phi:
                return phi.ToString();
            case Var.Distance:
                return dist.ToString();
            case Var.Far:
                return far.ToString();
            case Var.Resolution:
                return res.ToString();
            default:
                throw new NotImplementedException("Unknown variable");
        }
    }
}

public class AnalysisEngineDescription : RasterizingEngineDescription
{
    public Action<IntPtr, int> Analyze { get; private set; }
    public bool Hidden;
    public bool Manual;
    private const string shader = "DXTests.TestShaders.hlsl";

    public AnalysisEngineDescription(RasterizingEngineDescription ED, Action<IntPtr, int> analyze, bool hidden, bool manual) : base(ED)
    {
        ShaderResource = shader;
        if (analyze == null)
        {
            throw new ArgumentNullException("Analyzer cannot be null");
        }
        Analyze = analyze;
        Hidden = hidden;
        Manual = manual;
    }
}
