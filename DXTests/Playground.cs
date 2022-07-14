using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace DXTests;

[TestClass]
public class Playground
{
    static Random random = new Random();
    static float value;
    static float f => random.NextSingle();
    static float m => f * 2.0f - 1.0f;
    static float n => random.NextSingle() * 2.0f - 1.0f;
    static int ri => random.Next();

    private void Method()
    {
        value += (random.NextSingle() * 2.0f - 1.0f);
    }

    private static Matrix4x4 CreateRotationXF(float angle)
    {
        float cos = MathF.Cos(angle);
        float sin = MathF.Sin(angle);

        Matrix4x4 result = Matrix4x4.Identity;
        result.M22 = cos;
        result.M23 = sin;
        result.M32 = -sin;
        result.M33 = cos;

        return result;
    }

    private static Matrix4x4 CreateRotationYF(float angle)
    {
        float cos = MathF.Cos(angle);
        float sin = MathF.Sin(angle);

        Matrix4x4 result = Matrix4x4.Identity;
        result.M11 = cos;
        result.M13 = -sin;
        result.M31 = sin;
        result.M33 = cos;

        return result;
    }

    private static Matrix4x4 CreateRotationZF(float angle)
    {
        float cos = MathF.Cos(-angle);
        float sin = MathF.Sin(-angle);

        Matrix4x4 result = Matrix4x4.Identity;
        result.M11 = cos;
        result.M12 = sin;
        result.M21 = -sin;
        result.M22 = cos;

        return result;
    }

    private static Matrix4x4 CreateRotationF(Vector3 rot)
    {
        rot *= DEG2RADF;
        Matrix4x4 rotx = CreateRotationXF(rot.X);
        Matrix4x4 roty = CreateRotationYF(rot.Y);
        Matrix4x4 rotz = CreateRotationZF(rot.Z);
        return rotx * roty * rotz;
    }

    private static Matrix4x4 CreateRotationExplicit(Vector3 rot)
    {
        rot *= DEG2RADF;

        float cosx = MathF.Cos(rot.X);
        float cosy = MathF.Cos(rot.Y);
        float cosz = MathF.Cos(-rot.Z);

        float sinx = MathF.Sin(rot.X);
        float siny = MathF.Sin(rot.Y);
        float sinz = MathF.Sin(-rot.Z);

        return new(              cosy * cosz,                      cosy * sinz,       -siny, 0.0f,
            siny * sinx * cosz - cosx * sinz, siny * sinx * sinz + cosx * cosz, cosy * sinx, 0.0f,
            siny * cosx * cosz + sinx * sinz, siny * cosx * sinz - sinx * cosz, cosy * cosx, 0.0f,
                                        0.0f,                             0.0f,        0.0f, 1.0f);
    }

    private static Matrix4x4 CreateRotationZeroInit(Vector3 rot)
    {
        rot *= DEG2RADF;

        float cosx = MathF.Cos(rot.X);
        float cosy = MathF.Cos(rot.Y);
        float cosz = MathF.Cos(-rot.Z);

        float sinx = MathF.Sin(rot.X);
        float siny = MathF.Sin(rot.Y);
        float sinz = MathF.Sin(-rot.Z);

        Matrix4x4 output = new();

        output.M11 = cosy * cosz;
        output.M12 = cosy * sinz;
        output.M13 = -siny;
        output.M21 = siny * sinx * cosz - cosx * sinz;
        output.M22 = siny * sinx * sinz + cosx * cosz;
        output.M23 = cosy * sinx;
        output.M31 = siny * cosx * cosz + sinx * sinz;
        output.M32 = siny * cosx * sinz - sinx * cosz;
        output.M33 = cosy * cosx;
        output.M44 = 1.0f;

        return output;
    }

    private static Matrix4x4 CreateRotationIdentityInit(Vector3 rot)
    {
        rot *= DEG2RADF;

        float cosx = MathF.Cos(rot.X);
        float cosy = MathF.Cos(rot.Y);
        float cosz = MathF.Cos(-rot.Z);

        float sinx = MathF.Sin(rot.X);
        float siny = MathF.Sin(rot.Y);
        float sinz = MathF.Sin(-rot.Z);

        Matrix4x4 output = Matrix4x4.Identity;

        output.M11 = cosy * cosz;
        output.M12 = cosy * sinz;
        output.M13 = -siny;
        output.M21 = siny * sinx * cosz - cosx * sinz;
        output.M22 = siny * sinx * sinz + cosx * cosz;
        output.M23 = cosy * sinx;
        output.M31 = siny * cosx * cosz + sinx * sinz;
        output.M32 = siny * cosx * sinz - sinx * cosz;
        output.M33 = cosy * cosx;

        return output;
    }

    private static Matrix4x4 CreateViewOG(Vector3 pos, Vector3 rot)
    {
        Matrix4x4 rotation = CreateRotation(rot);
        Vector3 xaxis = Vector3.TransformNormal(Vector3.UnitX, rotation);
        Vector3 yaxis = Vector3.TransformNormal(Vector3.UnitY, rotation);
        Vector3 zaxis = Vector3.TransformNormal(Vector3.UnitZ, rotation);

        Matrix4x4 result = new();

        pos *= -1.0f;
        result.M11 = xaxis.X;
        result.M12 = yaxis.X;
        result.M13 = zaxis.X;
        result.M21 = xaxis.Y;
        result.M22 = yaxis.Y;
        result.M23 = zaxis.Y;
        result.M31 = xaxis.Z;
        result.M32 = yaxis.Z;
        result.M33 = zaxis.Z;
        result.M41 = Vector3.Dot(xaxis, pos);
        result.M42 = Vector3.Dot(yaxis, pos);
        result.M43 = Vector3.Dot(zaxis, pos);
        result.M44 = 1.0f;

        return result;
    }

    private static Matrix4x4 CreateViewRemix1(Vector3 pos, Vector3 rot)
    {
        Matrix4x4 rotation = CreateRotation(rot);
        Vector3 xaxis = Vector3.TransformNormal(Vector3.UnitX, rotation);
        Vector3 yaxis = Vector3.TransformNormal(Vector3.UnitY, rotation);
        Vector3 zaxis = Vector3.TransformNormal(Vector3.UnitZ, rotation);

        Matrix4x4 result = new();

        pos *= -1.0f;
        result.M11 = xaxis.X;
        result.M21 = xaxis.Y;
        result.M31 = xaxis.Z;
        result.M12 = yaxis.X;
        result.M22 = yaxis.Y;
        result.M32 = yaxis.Z;
        result.M13 = zaxis.X;
        result.M23 = zaxis.Y;
        result.M33 = zaxis.Z;
        result.M41 = Vector3.Dot(xaxis, pos);
        result.M42 = Vector3.Dot(yaxis, pos);
        result.M43 = Vector3.Dot(zaxis, pos);
        result.M44 = 1.0f;

        return result;
    }

    private static Matrix4x4 CreateViewRemix2(Vector3 pos, Vector3 rot)
    {
        Matrix4x4 rotation = CreateRotation(rot);
        Vector3 xaxis = Vector3.TransformNormal(Vector3.UnitX, rotation);
        Vector3 yaxis = Vector3.TransformNormal(Vector3.UnitY, rotation);
        Vector3 zaxis = Vector3.TransformNormal(Vector3.UnitZ, rotation);

        Matrix4x4 result = new();

        result.M11 = xaxis.X;
        result.M21 = xaxis.Y;
        result.M31 = xaxis.Z;
        result.M12 = yaxis.X;
        result.M22 = yaxis.Y;
        result.M32 = yaxis.Z;
        result.M13 = zaxis.X;
        result.M23 = zaxis.Y;
        result.M33 = zaxis.Z;
        result.M41 = -Vector3.Dot(xaxis, pos);
        result.M42 = -Vector3.Dot(yaxis, pos);
        result.M43 = -Vector3.Dot(zaxis, pos);
        result.M44 = 1.0f;

        return result;
    }

    private static Matrix4x4 CreateViewRemix3(Vector3 pos, Vector3 rot)
    {
        rot *= DEG2RADF;

        float cosx = MathF.Cos(rot.X);
        float cosy = MathF.Cos(rot.Y);
        float cosz = MathF.Cos(rot.Z);

        float sinx = MathF.Sin(rot.X);
        float siny = MathF.Sin(rot.Y);
        float sinz = MathF.Sin(-rot.Z);

        Matrix4x4 output = new();

        output.M11 = cosy * cosz;
        output.M21 = cosy * sinz;
        output.M31 = -siny;
        output.M12 = siny * sinx * cosz - cosx * sinz;
        output.M22 = siny * sinx * sinz + cosx * cosz;
        output.M32 = cosy * sinx;
        output.M13 = siny * cosx * cosz + sinx * sinz;
        output.M23 = siny * cosx * sinz - sinx * cosz;
        output.M33 = cosy * cosx;
        output.M44 = 1.0f;

        output.M41 = -Vector3.Dot(new(output.M11, output.M21, output.M31), pos);
        output.M42 = -Vector3.Dot(new(output.M12, output.M22, output.M32), pos);
        output.M43 = -Vector3.Dot(new(output.M13, output.M23, output.M33), pos);

        return output;
    }

    private static Matrix4x4 CreateViewRemix4(Vector3 pos, Vector3 rot)
    {
        rot.X *= DEG2RADF;
        rot.Y *= DEG2RADF;
        rot.Z *= -DEG2RADF;

        float cosx = MathF.Cos(rot.X);
        float cosy = MathF.Cos(rot.Y);
        float cosz = MathF.Cos(rot.Z);

        float sinx = MathF.Sin(rot.X);
        float siny = MathF.Sin(rot.Y);
        float sinz = MathF.Sin(rot.Z);

        Matrix4x4 output = new();

        output.M11 = cosy * cosz;
        output.M21 = cosy * sinz;
        output.M31 = -siny;
        output.M12 = siny * sinx * cosz - cosx * sinz;
        output.M22 = siny * sinx * sinz + cosx * cosz;
        output.M32 = cosy * sinx;
        output.M13 = siny * cosx * cosz + sinx * sinz;
        output.M23 = siny * cosx * sinz - sinx * cosz;
        output.M33 = cosy * cosx;
        output.M44 = 1.0f;

        output.M41 = -Vector3.Dot(new(output.M11, output.M21, output.M31), pos);
        output.M42 = -Vector3.Dot(new(output.M12, output.M22, output.M32), pos);
        output.M43 = -Vector3.Dot(new(output.M13, output.M23, output.M33), pos);

        return output;
    }

    private static Matrix4x4 CreateRotationXOG(float angle)
    {
        angle *= DEG2RADF;

        float cos = MathF.Cos(angle);
        float sin = MathF.Sin(angle);

        Matrix4x4 result = Matrix4x4.Identity;
        result.M22 = cos;
        result.M23 = sin;
        result.M32 = -sin;
        result.M33 = cos;

        return result;
    }

    private static Matrix4x4 CreateRotationXRemix1(float angle)
    {
        angle *= DEG2RADF;

        float cos = MathF.Cos(angle);
        float sin = MathF.Sin(angle);

        Matrix4x4 output = new();

        output.M11 = 1.0f;
        output.M22 = cos;
        output.M23 = sin;
        output.M32 = -sin;
        output.M33 = cos;
        output.M44 = 1.0f;

        return output;
    }

    private static int FindRemix1(string str, char search, int pos = 0)
    {
        return str.Substring(pos, str.Length - pos).IndexOf(search) + pos;
    }

    private static int FindRemix2(string str, char search, int pos = 0)
    {
        const int mask = 3;
        int size = str.Length - pos;
        int max = size - (size & mask);
        for (int i = pos; i < max; i += 4)
        {
            if (str[i] == search)
            {
                return i;
            }
            if (str[i + 1] == search)
            {
                return i + 1;
            }
            if (str[i + 2] == search)
            {
                return i + 2;
            }
            if (str[i + 3] == search)
            {
                return i + 3;
            }
        }
        for (int i = max; i < str.Length; ++i)
        {
            if (str[i] == search)
            {
                return i;
            }
        }
        return -1;
    }

    [TestMethod]
    public void LambdaVsMethod()
    {
        const int iterations = 1_000_000;
        var lambda = () =>
        {
            value += (random.NextSingle() * 2.0f - 1.0f);
        };

        long t1 = Ticks;
        for (int i = 0; i < iterations; ++i)
        {
            lambda();
        }
        long t2 = Ticks;
        Trace.WriteLine("lambda=" + (t2 - t1));

        t1 = Ticks;
        for (int i = 0; i < iterations; ++i)
        {
            Method();
        }
        t2 = Ticks;
        Trace.WriteLine("method=" + (t2 - t1));

        var mRef = Method;

        t1 = Ticks;
        for (int i = 0; i < iterations; ++i)
        {
            mRef();
        }
        t2 = Ticks;
        Trace.WriteLine("mRef=" + (t2 - t1));

        t1 = Ticks;
        for (int i = 0; i < iterations; ++i)
        {
            value += m;
        }
        t2 = Ticks;
        Trace.WriteLine("m=" + (t2 - t1));

        t1 = Ticks;
        for (int i = 0; i < iterations; ++i)
        {
            value += n;
        }
        t2 = Ticks;
        Trace.WriteLine("n=" + (t2 - t1));

        t1 = Ticks;
        for (int i = 0; i < iterations; ++i)
        {
            value += (random.NextSingle() * 2.0f - 1.0f);
        }
        t2 = Ticks;
        Trace.WriteLine("raw=" + (t2 - t1));
    }

    [TestMethod]
    public void GenericConstBuffExperiment()
    {
        Matrix4x4 a = new(f, f, f, f, f, f, f, f, f, f, f, f, f, f, f, f);
        Vector2 b = new(f, f);
        uint c = (uint)random.Next();
        int d = random.Next();

        ConstantBuffer buffer = new(new int[] { 64, 8, 4, 4 });

        buffer.Insert(a, 0);
        buffer.Insert(b, 1);
        buffer.Insert(c, 2);
        buffer.Insert(d, 3);

        Matrix4x4 a2 = new();
        Vector2 b2 = new();
        uint c2 = 0u;
        int d2 = 0;

        unsafe
        {
            buffer.Copy((IntPtr)(&a2));
        }

        Trace.WriteLine(a + " " + b + " " + c + " " + d);
        Trace.WriteLine(a2 + " " + b2 + " " + c2 + " " + d2);
    }

    [TestMethod]
    public void SSEExample()
    {
        const int iterations = 1_000_000;

        Vector4 v = new(1.0f);
        Vector4 a = new(0.01f, 0.02f, 0.03f, 0.04f);

        long t1 = Ticks;
        for (int i = 0; i < iterations; ++i)
        {
            v.X += a.X;
        }
        long t2 = Ticks;
        Trace.WriteLine("1element=" + (t2 - t1));

        t1 = Ticks;
        for (int i = 0; i < iterations; ++i)
        {
            v.X += a.Z;
            v.Y += a.X;
            v.Z += a.W;
            v.W += a.Y;
        }
        t2 = Ticks;
        Trace.WriteLine("4elements=" + (t2 - t1));

        t1 = Ticks;
        for (int i = 0; i < iterations; ++i)
        {
            v += a;
        }
        t2 = Ticks;
        Trace.WriteLine("vector=" + (t2 - t1));
    }

    [TestMethod]
    public void BranchPredictionOptimizationExample()
    {
        const int iterations = 64_000_000;

        /////////////////////////floating point random////////////////////////////
        Trace.WriteLine("\n######### floating point random #########");

        /////////////////////////////////////

        float a = 0.0f;
        long t1 = Ticks;
        for (int i = 0; i < iterations; ++i)
        {
            if (f > 0.5f)
                a += 1.0f;
            else
                a -= 1.0f;
        }
        long t2 = Ticks;
        Trace.WriteLine("branching=" + (t2 - t1));

        /////////////////////////////////////

        a = 0.0f;
        t1 = Ticks;
        for (int i = 0; i < iterations; ++i)
        {
            a += (int)(f * 2.0f) * 2 - 1;
        }
        t2 = Ticks;
        Trace.WriteLine("branchless=" + (t2 - t1));

        /////////////////////////////////////

        a = 0.0f;
        t1 = Ticks;
        for (int i = 0; i < iterations; i += 4)
        {
            var a1 = (int)(f * 2.0f) * 2 - 1;
            var a2 = (int)(f * 2.0f) * 2 - 1;
            var a3 = (int)(f * 2.0f) * 2 - 1;
            var a4 = (int)(f * 2.0f) * 2 - 1;
            a += a1 + a2 + a3 + a4;
        }
        t2 = Ticks;
        Trace.WriteLine("SSEBranchless=" + (t2 - t1));

        /////////////////////////////////////

        a = 0.0f;
        t1 = Ticks;
        for (int i = 0; i < iterations; i += 4)
        {
            var a1 = (int)(f * 2.0f);
            var a2 = (int)(f * 2.0f);
            var a3 = (int)(f * 2.0f);
            var a4 = (int)(f * 2.0f);
            var a5 = a1 * 2 - 1;
            var a6 = a2 * 2 - 1;
            var a7 = a3 * 2 - 1;
            var a8 = a4 * 2 - 1;
            a += a5 + a6 + a7 + a8;
        }
        t2 = Ticks;
        Trace.WriteLine("pipelinedSSEBranchless=" + (t2 - t1));

        ////////////////////floating point random with arrays/////////////////////
        Trace.WriteLine("\n######### floating point random with arrays #########");

        /////////////////////////////////////

        a = 0.0f;
        t1 = Ticks;
        float[] rands = new float[iterations];
        for (int i = 0; i < iterations; i += 4)
        {
            rands[i] = f;
            rands[i + 1] = f;
            rands[i + 2] = f;
            rands[i + 3] = f;
        }

        for (int i = 0; i < iterations; ++i)
        {
            a += (int)(rands[i] * 2.0f) * 2 - 1;
        }
        t2 = Ticks;
        Trace.WriteLine("arrayBranchless=" + (t2 - t1));

        /////////////////////////////////////

        a = 0.0f;
        t1 = Ticks;
        float[] rands2 = new float[iterations];
        for (int i = 0; i < iterations; i += 4)
        {
            rands2[i] = f;
            rands2[i + 1] = f;
            rands2[i + 2] = f;
            rands2[i + 3] = f;
        }

        for (int i = 0; i < iterations; i += 4)
        {
            var a1 = (int)(rands2[i] * 2.0f) * 2 - 1;
            var a2 = (int)(rands2[i + 1] * 2.0f) * 2 - 1;
            var a3 = (int)(rands2[i + 2] * 2.0f) * 2 - 1;
            var a4 = (int)(rands2[i + 3] * 2.0f) * 2 - 1;
            a += a1 + a2 + a3 + a4;
        }
        t2 = Ticks;
        Trace.WriteLine("arrayPipelinedBranchless=" + (t2 - t1));

        /////////////////////////////////////

        a = 0.0f;
        t1 = Ticks;
        for (int i = 0; i < iterations; ++i)
        {
            a += (int)(rands[i] * 2.0f) * 2 - 1;
        }
        t2 = Ticks;
        Trace.WriteLine("preloadedBranchless=" + (t2 - t1));

        /////////////////////////////////////

        a = 0.0f;
        t1 = Ticks;
        for (int i = 0; i < iterations; i += 4)
        {
            var a1 = (int)(rands[i] * 2.0f) * 2 - 1;
            var a2 = (int)(rands[i + 1] * 2.0f) * 2 - 1;
            var a3 = (int)(rands[i + 2] * 2.0f) * 2 - 1;
            var a4 = (int)(rands[i + 3] * 2.0f) * 2 - 1;
            a += a1 + a2 + a3 + a4;
        }
        t2 = Ticks;
        Trace.WriteLine("preloadedPipelinedBranchless=" + (t2 - t1));

        /////////////////////////////////////

        a = 0.0f;
        t1 = Ticks;
        for (int i = 0; i < iterations; i += 4)
        {
            var a1 = (int)(rands[i] * 2.0f);
            var a2 = (int)(rands[i + 1] * 2.0f);
            var a3 = (int)(rands[i + 2] * 2.0f);
            var a4 = (int)(rands[i + 3] * 2.0f);
            var a5 = a1 * 2 - 1;
            var a6 = a2 * 2 - 1;
            var a7 = a3 * 2 - 1;
            var a8 = a4 * 2 - 1;
            a += a5 + a6 + a7 + a8;
        }
        t2 = Ticks;
        Trace.WriteLine("preloadedPipelinedSSEBranchless=" + (t2 - t1));

        //////////////////////////////int random//////////////////////////////////
        Trace.WriteLine("\n######### int random #########");

        /////////////////////////////////////

        a = 0.0f;
        t1 = Ticks;
        for (int i = 0; i < iterations; ++i)
        {
            if ((ri & 1) == 1)
                a += 1.0f;
            else
                a -= 1.0f;
        }
        t2 = Ticks;
        Trace.WriteLine("branching=" + (t2 - t1));

        /////////////////////////////////////

        a = 0.0f;
        t1 = Ticks;
        for (int i = 0; i < iterations; ++i)
        {
            a += (ri & 1) * 2 - 1;
        }
        t2 = Ticks;
        Trace.WriteLine("branchless=" + (t2 - t1));

        /////////////////////////////////////

        a = 0.0f;
        t1 = Ticks;
        for (int i = 0; i < iterations; i += 4)
        {
            var a1 = (ri & 1) * 2 - 1;
            var a2 = (ri & 1) * 2 - 1;
            var a3 = (ri & 1) * 2 - 1;
            var a4 = (ri & 1) * 2 - 1;
            a += a1 + a2 + a3 + a4;
        }
        t2 = Ticks;
        Trace.WriteLine("SSEBranchless=" + (t2 - t1));

        /////////////////////////////////////

        a = 0.0f;
        t1 = Ticks;
        for (int i = 0; i < iterations; i += 4)
        {
            var a1 = ri & 1;
            var a2 = ri & 1;
            var a3 = ri & 1;
            var a4 = ri & 1;
            var a5 = a1 * 2 - 1;
            var a6 = a2 * 2 - 1;
            var a7 = a3 * 2 - 1;
            var a8 = a4 * 2 - 1;
            a += a5 + a6 + a7 + a8;
        }
        t2 = Ticks;
        Trace.WriteLine("pipelinedSSEBranchless=" + (t2 - t1));

        //////////////////////////////byte random/////////////////////////////////
        Trace.WriteLine("\n######### byte random #########");

        /////////////////////////////////////

        a = 0.0f;
        t1 = Ticks;
        byte[] c1 = new byte[iterations];
        random.NextBytes(c1);
        for (int i = 0; i < iterations; ++i)
        {
            if ((c1[i] & 1) == 1)
                a += 1.0f;
            else
                a -= 1.0f;
        }
        t2 = Ticks;
        Trace.WriteLine("branching=" + (t2 - t1));

        /////////////////////////////////////

        a = 0.0f;
        t1 = Ticks;
        byte[] c2 = new byte[iterations];
        random.NextBytes(c2);
        for (int i = 0; i < iterations; ++i)
        {
            a += (c2[i] & 1) * 2 - 1;
        }
        t2 = Ticks;
        Trace.WriteLine("branchless=" + (t2 - t1));

        /////////////////////////////////////

        a = 0.0f;
        t1 = Ticks;
        byte[] c3 = new byte[iterations];
        random.NextBytes(c3);
        for (int i = 0; i < iterations; i += 4)
        {
            var a1 = (c3[i] & 1) * 2 - 1;
            var a2 = (c3[i + 1] & 1) * 2 - 1;
            var a3 = (c3[i + 2] & 1) * 2 - 1;
            var a4 = (c3[i + 3] & 1) * 2 - 1;
            a += a1 + a2 + a3 + a4;
        }
        t2 = Ticks;
        Trace.WriteLine("SSEBranchless=" + (t2 - t1));

        /////////////////////////////////////

        a = 0.0f;
        t1 = Ticks;
        byte[] c4 = new byte[iterations];
        random.NextBytes(c4);
        for (int i = 0; i < iterations; i += 4)
        {
            var a1 = c4[i] & 1;
            var a2 = c4[i + 1] & 1;
            var a3 = c4[i + 2] & 1;
            var a4 = c4[i + 3] & 1;
            var a5 = a1 * 2 - 1;
            var a6 = a2 * 2 - 1;
            var a7 = a3 * 2 - 1;
            var a8 = a4 * 2 - 1;
            a += a5 + a6 + a7 + a8;
        }
        t2 = Ticks;
        Trace.WriteLine("pipelinedSSEBranchless=" + (t2 - t1));

        ///////////////// Fastest //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        a = 0.0f;
        t1 = Ticks;
        byte[] c5 = new byte[iterations / 8];
        random.NextBytes(c5);
        for (int i = 0; i < iterations / 8; ++i)
        {
            var a1 = c5[i] & 0b00000001;
            var a2 = c5[i] & 0b00000010;
            var a3 = c5[i] & 0b00000100;
            var a4 = c5[i] & 0b00001000;
            var a5 = c5[i] & 0b00010000;
            var a6 = c5[i] & 0b00100000;
            var a7 = c5[i] & 0b01000000;
            var a8 = c5[i] & 0b10000000;
            var b1 = a1 * 2 - 1;
            var b2 = a2 * 2 - 1;
            var b3 = a3 * 2 - 1;
            var b4 = a4 * 2 - 1;
            var b5 = a5 * 2 - 1;
            var b6 = a6 * 2 - 1;
            var b7 = a7 * 2 - 1;
            var b8 = a8 * 2 - 1;
            a += b1 + b2 + b3 + b4 + b5 + b6 + b7 + b8;
        }
        t2 = Ticks;
        Trace.WriteLine("efficientPipelinedSSEBranchless=" + (t2 - t1));

        //////////////////////////predictable pattern/////////////////////////////
        Trace.WriteLine("\n######### predictable pattern #########");

        /////////////////////////////////////

        a = 0.0f;
        t1 = Ticks;
        for (int i = 0; i < iterations; ++i)
        {
            if (i % 2 == 0)
                a += 1.0f;
            else
                a -= 1.0f;
        }
        t2 = Ticks;
        Trace.WriteLine("branching=" + (t2 - t1));

        /////////////////////////////////////

        a = 0.0f;
        t1 = Ticks;
        for (int i = 0; i < iterations; ++i)
        {
            a += ((i & 1) * 2 - 1);
        }
        t2 = Ticks;
        Trace.WriteLine("branchless=" + (t2 - t1));

        /////////////////////////////////////

        a = 0.0f;
        t1 = Ticks;
        for (int i = 0; i < iterations; i += 4)
        {
            var a1 = ((i & 1) * 2 - 1);
            var a2 = (((i + 1) & 1) * 2 - 1);
            var a3 = (((i + 2) & 1) * 2 - 1);
            var a4 = (((i + 3) & 1) * 2 - 1);
            a += a1 + a2 + a3 + a4;
        }
        t2 = Ticks;
        Trace.WriteLine("SSEBranchless=" + (t2 - t1));

        /////////////////////////////////////

        a = 0.0f;
        t1 = Ticks;
        for (int i = 0; i < iterations; i += 4)
        {
            var a1 = i & 1;
            var a2 = (i + 1) & 1;
            var a3 = (i + 2) & 1;
            var a4 = (i + 3) & 1;
            var a5 = a1 * 2 - 1;
            var a6 = a2 * 2 - 1;
            var a7 = a3 * 2 - 1;
            var a8 = a4 * 2 - 1;
            a += a5 + a6 + a7 + a8;
        }
        t2 = Ticks;
        Trace.WriteLine("pipelinedSSEBranchless=" + (t2 - t1));
    }

    [TestMethod]
    public unsafe void AnalysisOptimization()
    {
        const int pixelCount = 640*640;
        if (pixelCount % 8 != 0)
        {
            throw new NotImplementedException();
        }

        //////////////////////////////////////////////////////////////////////////
        // All white

        int[] pixels = GC.AllocateArray<int>(pixelCount, true);
        for (int i = 0; i < pixelCount; ++i)
            pixels[i] = -1;

        fixed (int* pointer = &pixels[0])
        {
            /////////////////////////////////////

            long t1 = Ticks;
            bool allWhite = true;
            int* ptr = pointer;
            for (int i = 0; i < pixelCount; ++i, ++ptr)
            {
                if (*ptr != -1)
                {
                    allWhite = false;
                    break;
                }
            }
            long t2 = Ticks;
            Assert.IsTrue(allWhite);
            Trace.WriteLine("single=" + (t2 - t1));

            /////////////////////////////////////

            t1 = Ticks;
            allWhite = true;
            int result = -1;
            ptr = pointer;
            for (int i = 0; i < pixelCount; ++i, ++ptr)
            {
                result &= (*ptr & -1);
            }
            if (result != -1)
            {
                allWhite = false;
            }
            t2 = Ticks;
            Assert.IsTrue(allWhite);
            Trace.WriteLine("branchless=" + (t2 - t1));

            /////////////////////////////////////

            t1 = Ticks;
            allWhite = true;
            ptr = pointer;
            int* end = pointer + pixelCount;
            while (ptr < end)
            {
                var total = *ptr & *(ptr + 1) & *(ptr + 2) & *(ptr + 3) & -1;
                if (total != -1)
                {
                    allWhite = false;
                    break;
                }
                ptr += 4;
            }
            t2 = Ticks;
            Assert.IsTrue(allWhite);
            Trace.WriteLine("multi=" + (t2 - t1));

            ///////////////////////////////////// 
            //              Best               //
            /////////////////////////////////////

            t1 = Ticks;
            allWhite = true;
            ptr = pointer;
            end = pointer + pixelCount;
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
            t2 = Ticks;
            Assert.IsTrue(allWhite);
            Trace.WriteLine("halfpipeSSE=" + (t2 - t1));

            /////////////////////////////////////

            t1 = Ticks;
            allWhite = true;
            ptr = pointer;
            for (int i = 0; i < pixelCount; i += 4, ptr += 4)
            {
                var col1 = *ptr & -1;
                var col2 = *(ptr + 1) & -1;
                var col3 = *(ptr + 2) & -1;
                var col4 = *(ptr + 3) & -1;
                var total = col1 & col2 & col3 & col4;
                if (total != -1)
                {
                    allWhite = false;
                }
            }
            t2 = Ticks;
            Assert.IsTrue(allWhite);
            Trace.WriteLine("SSE=" + (t2 - t1));

            /////////////////////////////////////

            t1 = Ticks;
            allWhite = true;
            ptr = pointer;
            for (int i = 0; i < pixelCount; i += 4, ptr += 4)
            {
                var col1 = *ptr;
                var col2 = *(ptr + 1);
                var col3 = *(ptr + 2);
                var col4 = *(ptr + 3);
                var temp1 = col1 & -1;
                var temp2 = col2 & -1;
                var temp3 = col3 & -1;
                var temp4 = col4 & -1;
                var total= temp1 & temp2 & temp3 & temp4;
                if (total != -1)
                {
                    allWhite = false;
                }
            }
            t2 = Ticks;
            Assert.IsTrue(allWhite);
            Trace.WriteLine("pipelinedSSE=" + (t2 - t1));
        }
    }

    [TestMethod]
    public void MathvsMathF()
    {
        const int iterations = 100_000_000;

        float[] fRands = new float[iterations];
        double[] dRands = new double[iterations];
        for (int i = 0; i < iterations; i += 4)
        {
            var temp1 = random.NextSingle() * 2.0f - 1.0f;
            var temp2 = random.NextSingle() * 2.0f - 1.0f;
            var temp3 = random.NextSingle() * 2.0f - 1.0f;
            var temp4 = random.NextSingle() * 2.0f - 1.0f;
            fRands[i] = temp1 * PIF;
            fRands[i + 1] = temp2 * PIF;
            fRands[i + 2] = temp3 * PIF;
            fRands[i + 3] = temp4 * PIF;
            var temp5 = random.NextDouble() * 2.0 - 1.0;
            var temp6 = random.NextDouble() * 2.0 - 1.0;
            var temp7 = random.NextDouble() * 2.0 - 1.0;
            var temp8 = random.NextDouble() * 2.0 - 1.0;
            dRands[i] = temp5 * PI;
            dRands[i + 1] = temp6 * PI;
            dRands[i + 2] = temp7 * PI;
            dRands[i + 3] = temp8 * PI;
        }

        long t1 = Ticks;
        for (int i = 0; i < iterations; ++i)
        {
            MathF.Sin(fRands[i]);
        }
        long t2 = Ticks;
        Trace.WriteLine("sinf=" + (t2 - t1));

        t1 = Ticks;
        for (int i = 0; i < iterations; ++i)
        {
            MathF.Cos(fRands[i]);
        }
        t2 = Ticks;
        Trace.WriteLine("cosf=" + (t2 - t1));

        t1 = Ticks;
        for (int i = 0; i < iterations; ++i)
        {
            Math.Sin(dRands[i]);
        }
        t2 = Ticks;
        Trace.WriteLine("sin=" + (t2 - t1));

        t1 = Ticks;
        for (int i = 0; i < iterations; ++i)
        {
            Math.Cos(dRands[i]);
        }
        t2 = Ticks;
        Trace.WriteLine("cos=" + (t2 - t1));

        t1 = Ticks;
        for (int i = 0; i < iterations; ++i)
        {
            Math.Cos(HALFPI - dRands[i]);
        }
        t2 = Ticks;
        Trace.WriteLine("sin2=" + (t2 - t1));
    }

    [TestMethod]
    public void RotationMatrixOptimization()
    {
        const int iterations = 10_000_000;

        Vector3[] rands = new Vector3[iterations];
        for (int i = 0; i < iterations; ++i)
        {
            var temp1 = random.NextSingle() * 2.0f - 1.0f;
            var temp2 = random.NextSingle() * 2.0f - 1.0f;
            var temp3 = random.NextSingle() * 2.0f - 1.0f;
            rands[i].X = temp1 * 360.0f;
            rands[i].Y = temp2 * 360.0f;
            rands[i].Z = temp3 * 360.0f;
        }

        long t1 = Ticks;
        for (int i = 0; i < iterations; ++i)
        {
            CreateRotationF(rands[i]);
        }
        long t2 = Ticks;
        Trace.WriteLine("MathF=" + (t2 - t1));

        t1 = Ticks;
        for (int i = 0; i < iterations; ++i)
        {
            CreateRotationExplicit(rands[i]);
        }
        t2 = Ticks;
        Trace.WriteLine("explicit=" + (t2 - t1));

        t1 = Ticks;
        for (int i = 0; i < iterations; ++i)
        {
            CreateRotationZeroInit(rands[i]);
        }
        t2 = Ticks;
        Trace.WriteLine("zeroinit=" + (t2 - t1));

        t1 = Ticks;
        for (int i = 0; i < iterations; ++i)
        {
            CreateRotationIdentityInit(rands[i]);
        }
        t2 = Ticks;
        Trace.WriteLine("identityInit=" + (t2 - t1));
    }

    [TestMethod]
    public void ViewMatrixOptimization()
    {
        const int iterations = 10_000_000;

        Vector3[] rots = new Vector3[iterations];
        Vector3[] poss = new Vector3[iterations];
        for (int i = 0; i < iterations; ++i)
        {
            var temp1 = random.NextSingle() * 2.0f - 1.0f;
            var temp2 = random.NextSingle() * 2.0f - 1.0f;
            var temp3 = random.NextSingle() * 2.0f - 1.0f;
            rots[i].X = temp1 * 360.0f;
            rots[i].Y = temp2 * 360.0f;
            rots[i].Z = temp3 * 360.0f;
            var temp4 = random.NextSingle() * 2.0f - 1.0f;
            var temp5 = random.NextSingle() * 2.0f - 1.0f;
            var temp6 = random.NextSingle() * 2.0f - 1.0f;
            poss[i].X = temp4 * 30.0f;
            poss[i].Y = temp5 * 30.0f;
            poss[i].Z = temp6 * 30.0f;
        }

        long t1 = Ticks;
        for (int i = 0; i < iterations; ++i)
        {
            CreateViewOG(poss[i], rots[i]);
        }
        long t2 = Ticks;
        Trace.WriteLine("og=" + (t2 - t1));

        t1 = Ticks;
        for (int i = 0; i < iterations; ++i)
        {
            CreateViewRemix1(poss[i], rots[i]);
        }
        t2 = Ticks;
        Trace.WriteLine("re1=" + (t2 - t1));

        t1 = Ticks;
        for (int i = 0; i < iterations; ++i)
        {
            CreateViewRemix2(poss[i], rots[i]);
        }
        t2 = Ticks;
        Trace.WriteLine("re2=" + (t2 - t1));

        t1 = Ticks;
        for (int i = 0; i < iterations; ++i)
        {
            CreateViewRemix3(poss[i], rots[i]);
        }
        t2 = Ticks;
        Trace.WriteLine("re3=" + (t2 - t1));

        t1 = Ticks;
        for (int i = 0; i < iterations; ++i)
        {
            CreateViewRemix4(poss[i], rots[i]);
        }
        t2 = Ticks;
        Trace.WriteLine("re4=" + (t2 - t1));
    }

    [TestMethod]
    public void RotxMatrixOptimization()
    {
        const int iterations = 10_000_000;

        float[] rands = new float[iterations];
        for (int i = 0; i < iterations; i += 4)
        {
            var temp1 = random.NextSingle() * 2.0f - 1.0f;
            var temp2 = random.NextSingle() * 2.0f - 1.0f;
            var temp3 = random.NextSingle() * 2.0f - 1.0f;
            var temp4 = random.NextSingle() * 2.0f - 1.0f;
            rands[i] = temp1 * 360.0f;
            rands[i + 1] = temp2 * 360.0f;
            rands[i + 2] = temp3 * 360.0f;
            rands[i + 3] = temp4 * 360.0f;
        }

        long t1 = Ticks;
        for (int i = 0; i < iterations; ++i)
        {
            CreateRotationXOG(rands[i]);
        }
        long t2 = Ticks;
        Trace.WriteLine("og=" + (t2 - t1));

        t1 = Ticks;
        for (int i = 0; i < iterations; ++i)
        {
            CreateRotationXRemix1(rands[i]);
        }
        t2 = Ticks;
        Trace.WriteLine("re1=" + (t2 - t1));
    }

    [TestMethod]
    public unsafe void UnsafeWriting()
    {
        /////////////////////////////////////
        //             Don't               //
        /////////////////////////////////////
        //        But I did anyways        //
        /////////////////////////////////////
        Matrix4x4 ogData = new();

        float* pointer = &ogData.M11;
        float* end = pointer + 16;
        for (float* ptr = pointer; ptr < end; ptr += 4)
        {
            *ptr = random.NextSingle();
            *(ptr + 1) = random.NextSingle();
            *(ptr + 2) = random.NextSingle();
            *(ptr + 3) = random.NextSingle();
        }

        object source = ogData;

        byte[] destination = new byte[PackedSize(source)];

        Assert.AreEqual(4 * 16, destination.Length);

        fixed (byte* ptr = &destination[0])
        {
            // Need to cast
            int size = Unsafe.SizeOf<Matrix4x4>();
            Unsafe.WriteUnaligned(ptr, (Matrix4x4)source);

            for (float* ptr2 = (float*)ptr; pointer < end; ++pointer, ++ptr2)
            {
                Assert.AreEqual(*pointer, *ptr2);
            }
        }
    }

    [TestMethod]
    public void WaitingExperiment()
    {
        print("Sleep");
        Thread.Sleep(1);
        print("Done");
        const long ticks = SEC2TICK / 1000;
        print("Delay");
        Task.Delay(new TimeSpan(ticks)).Wait();
        print("Done");
    }

    [TestMethod]
    public void PrintingOptimization()
    {
        const int iterations = 100_000;
        const int count = 10;
        const int total = count * iterations;
        const int charLimit = 20;

        string[] data = new string[total];
        Queue<string> messages = new(total);
        Queue<int> lengths = new(total);
        Queue<long> times = new(total);

        for (int i = 0; i < total; ++i)
        {
            data[i] = new('a', (random.Next() % charLimit) + 1);
            messages.Enqueue(data[i]);
            lengths.Enqueue(data[i].Length);
            times.Enqueue(Ticks);
        }

        long t1 = Ticks;
        for (int i = 0; i < iterations; ++i)
        {
            int charCount = count * 22;
            for (int j = 0; j < count; ++j)
            {
                charCount += lengths.Dequeue();
            }
            StringBuilder sb = new();
            for (int j = 0; j < count; ++j)
            {
                sb.Append(times.Dequeue());
                sb.Append(": ");
                sb.Append(messages.Dequeue());
                sb.Append('\n');
            }
            var output = sb.ToString();
        }
        long t2 = Ticks;
        Trace.WriteLine("og=" + (t2 - t1));

        for (int i = 0; i < total; ++i)
        {
            messages.Enqueue(data[i]);
            lengths.Enqueue(data[i].Length);
            times.Enqueue(Ticks);
        }

        t1 = Ticks;
        for (int i = 0; i < iterations; ++i)
        {
            var output = "";
            for (int j = 0; j < count; ++j)
            {
                output += times.Dequeue() + ": " + messages.Dequeue() + '\n';
            }
        }
        t2 = Ticks;
        Trace.WriteLine("re1=" + (t2 - t1));
    }

    [TestMethod]
    public void FindOptimization()
    {
        const int iterations = 100_000;
        const int length = 1_000;
        const int total = iterations * length;

        string[] strings = new string[iterations];
        char[] matches = new char[iterations];
        byte[] randoms = new byte[total];
        random.NextBytes(randoms);
        char[] chars = new char[total];
        Encoding.ASCII.GetChars(randoms, chars);
        int j = 0;
        for (int i = 0; i < iterations; ++i, j += length)
        {
            strings[i] = new(chars, j, length);
            matches[i] = strings[i][randoms[i] % length];
        }

        long t1 = Ticks;
        for (int i = 0; i < iterations; ++i)
        {
            Find(strings[i], matches[i]);
        }
        long t2 = Ticks;
        Trace.WriteLine("OG=" + (t2 - t1));

        t1 = Ticks;
        for (int i = 0; i < iterations; ++i)
        {
            strings[i].IndexOf(matches[i]);
        }
        t2 = Ticks;
        Trace.WriteLine("std=" + (t2 - t1));

        t1 = Ticks;
        for (int i = 0; i < iterations; ++i)
        {
            FindRemix1(strings[i], matches[i]);
        }
        t2 = Ticks;
        Trace.WriteLine("Re1=" + (t2 - t1));

        t1 = Ticks;
        for (int i = 0; i < iterations; ++i)
        {
            FindRemix2(strings[i], matches[i]);
        }
        t2 = Ticks;
        Trace.WriteLine("Re2=" + (t2 - t1));
    }
}
