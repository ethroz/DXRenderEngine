using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DXTests;

[TestClass]
public class Playground
{
    static Stopwatch sw;
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

    [TestInitialize]
    public void TestSetup()
    {
        sw = new();
    }

    [TestMethod]
    public void LambdaVsMethod()
    {
        int iterations = 1_000_000;
        var lambda = () =>
        {
            value += (random.NextSingle() * 2.0f - 1.0f);
        };

        sw.Start();
        for (int i = 0; i < iterations; ++i)
        {
            lambda();
        }
        sw.Stop();
        Trace.WriteLine("lambda=" + sw.ElapsedTicks);

        sw.Restart();
        for (int i = 0; i < iterations; ++i)
        {
            Method();
        }
        sw.Stop();
        Trace.WriteLine("method=" + sw.ElapsedTicks);

        var mRef = Method;

        sw.Restart();
        for (int i = 0; i < iterations; ++i)
        {
            mRef();
        }
        sw.Stop();
        Trace.WriteLine("mRef=" + sw.ElapsedTicks);

        sw.Restart();
        for (int i = 0; i < iterations; ++i)
        {
            value += m;
        }
        sw.Stop();
        Trace.WriteLine("m=" + sw.ElapsedTicks);

        sw.Restart();
        for (int i = 0; i < iterations; ++i)
        {
            value += n;
        }
        sw.Stop();
        Trace.WriteLine("n=" + sw.ElapsedTicks);

        sw.Restart();
        for (int i = 0; i < iterations; ++i)
        {
            value += (random.NextSingle() * 2.0f - 1.0f);
        }
        sw.Stop();
        Trace.WriteLine("raw=" + sw.ElapsedTicks);
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
        int iterations = 1_000_000;

        Vector4 v = new(1.0f);
        Vector4 a = new(0.01f, 0.02f, 0.03f, 0.04f);

        sw.Start();
        for (int i = 0; i < iterations; ++i)
        {
            v.X += a.X;
        }
        sw.Stop();
        Trace.WriteLine("1element=" + sw.ElapsedTicks);

        sw.Restart();
        for (int i = 0; i < iterations; ++i)
        {
            v.X += a.Z;
            v.Y += a.X;
            v.Z += a.W;
            v.W += a.Y;
        }
        sw.Stop();
        Trace.WriteLine("4elements=" + sw.ElapsedTicks);

        sw.Restart();
        for (int i = 0; i < iterations; ++i)
        {
            v += a;
        }
        sw.Stop();
        Trace.WriteLine("vector=" + sw.ElapsedTicks);
    }

    [TestMethod]
    public void BranchPredictionOptimizationExample()
    {
        int iterations = 64_000_000;

        /////////////////////////floating point random////////////////////////////
        Trace.WriteLine("\n######### floating point random #########");

        /////////////////////////////////////

        float a = 0.0f;
        sw.Start();
        for (int i = 0; i < iterations; ++i)
        {
            if (f > 0.5f)
                a += 1.0f;
            else
                a -= 1.0f;
        }
        sw.Stop();
        Trace.WriteLine("branching=" + sw.ElapsedTicks);

        /////////////////////////////////////

        a = 0.0f;
        sw.Restart();
        for (int i = 0; i < iterations; ++i)
        {
            a += (int)(f * 2.0f) * 2 - 1;
        }
        sw.Stop();
        Trace.WriteLine("branchless=" + sw.ElapsedTicks);

        /////////////////////////////////////

        a = 0.0f;
        sw.Restart();
        for (int i = 0; i < iterations; i += 4)
        {
            var a1 = (int)(f * 2.0f) * 2 - 1;
            var a2 = (int)(f * 2.0f) * 2 - 1;
            var a3 = (int)(f * 2.0f) * 2 - 1;
            var a4 = (int)(f * 2.0f) * 2 - 1;
            a += a1 + a2 + a3 + a4;
        }
        sw.Stop();
        Trace.WriteLine("SSEBranchless=" + sw.ElapsedTicks);

        /////////////////////////////////////

        a = 0.0f;
        sw.Restart();
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
        sw.Stop();
        Trace.WriteLine("pipelinedSSEBranchless=" + sw.ElapsedTicks);

        ////////////////////floating point random with arrays/////////////////////
        Trace.WriteLine("\n######### floating point random with arrays #########");

        /////////////////////////////////////

        a = 0.0f;
        sw.Restart();
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
        sw.Stop();
        Trace.WriteLine("arrayBranchless=" + sw.ElapsedTicks);

        /////////////////////////////////////

        a = 0.0f;
        sw.Restart();
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
        sw.Stop();
        Trace.WriteLine("arrayPipelinedBranchless=" + sw.ElapsedTicks);

        /////////////////////////////////////

        a = 0.0f;
        sw.Restart();
        for (int i = 0; i < iterations; ++i)
        {
            a += (int)(rands[i] * 2.0f) * 2 - 1;
        }
        sw.Stop();
        Trace.WriteLine("preloadedBranchless=" + sw.ElapsedTicks);

        /////////////////////////////////////

        a = 0.0f;
        sw.Restart();
        for (int i = 0; i < iterations; i += 4)
        {
            var a1 = (int)(rands[i] * 2.0f) * 2 - 1;
            var a2 = (int)(rands[i + 1] * 2.0f) * 2 - 1;
            var a3 = (int)(rands[i + 2] * 2.0f) * 2 - 1;
            var a4 = (int)(rands[i + 3] * 2.0f) * 2 - 1;
            a += a1 + a2 + a3 + a4;
        }
        sw.Stop();
        Trace.WriteLine("preloadedPipelinedBranchless=" + sw.ElapsedTicks);

        /////////////////////////////////////

        a = 0.0f;
        sw.Restart();
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
        sw.Stop();
        Trace.WriteLine("preloadedPipelinedSSEBranchless=" + sw.ElapsedTicks);

        //////////////////////////////int random//////////////////////////////////
        Trace.WriteLine("\n######### int random #########");

        /////////////////////////////////////

        a = 0.0f;
        sw.Restart();
        for (int i = 0; i < iterations; ++i)
        {
            if ((ri & 1) == 1)
                a += 1.0f;
            else
                a -= 1.0f;
        }
        sw.Stop();
        Trace.WriteLine("branching=" + sw.ElapsedTicks);

        /////////////////////////////////////

        a = 0.0f;
        sw.Restart();
        for (int i = 0; i < iterations; ++i)
        {
            a += (ri & 1) * 2 - 1;
        }
        sw.Stop();
        Trace.WriteLine("branchless=" + sw.ElapsedTicks);

        /////////////////////////////////////

        a = 0.0f;
        sw.Restart();
        for (int i = 0; i < iterations; i += 4)
        {
            var a1 = (ri & 1) * 2 - 1;
            var a2 = (ri & 1) * 2 - 1;
            var a3 = (ri & 1) * 2 - 1;
            var a4 = (ri & 1) * 2 - 1;
            a += a1 + a2 + a3 + a4;
        }
        sw.Stop();
        Trace.WriteLine("SSEBranchless=" + sw.ElapsedTicks);

        /////////////////////////////////////

        a = 0.0f;
        sw.Restart();
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
        sw.Stop();
        Trace.WriteLine("pipelinedSSEBranchless=" + sw.ElapsedTicks);

        //////////////////////////////byte random/////////////////////////////////
        Trace.WriteLine("\n######### byte random #########");

        /////////////////////////////////////

        a = 0.0f;
        sw.Restart();
        byte[] c1 = new byte[iterations];
        random.NextBytes(c1);
        for (int i = 0; i < iterations; ++i)
        {
            if ((c1[i] & 1) == 1)
                a += 1.0f;
            else
                a -= 1.0f;
        }
        sw.Stop();
        Trace.WriteLine("branching=" + sw.ElapsedTicks);

        /////////////////////////////////////

        a = 0.0f;
        sw.Restart();
        byte[] c2 = new byte[iterations];
        random.NextBytes(c2);
        for (int i = 0; i < iterations; ++i)
        {
            a += (c2[i] & 1) * 2 - 1;
        }
        sw.Stop();
        Trace.WriteLine("branchless=" + sw.ElapsedTicks);

        /////////////////////////////////////

        a = 0.0f;
        sw.Restart();
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
        sw.Stop();
        Trace.WriteLine("SSEBranchless=" + sw.ElapsedTicks);

        /////////////////////////////////////

        a = 0.0f;
        sw.Restart();
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
        sw.Stop();
        Trace.WriteLine("pipelinedSSEBranchless=" + sw.ElapsedTicks);

        ///////////////// Fastest //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        a = 0.0f;
        sw.Restart();
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
        sw.Stop();
        Trace.WriteLine("efficientPipelinedSSEBranchless=" + sw.ElapsedTicks);

        //////////////////////////predictable pattern/////////////////////////////
        Trace.WriteLine("\n######### predictable pattern #########");

        /////////////////////////////////////

        a = 0.0f;
        sw.Restart();
        for (int i = 0; i < iterations; ++i)
        {
            if (i % 2 == 0)
                a += 1.0f;
            else
                a -= 1.0f;
        }
        sw.Stop();
        Trace.WriteLine("branching=" + sw.ElapsedTicks);

        /////////////////////////////////////

        a = 0.0f;
        sw.Restart();
        for (int i = 0; i < iterations; ++i)
        {
            a += ((i & 1) * 2 - 1);
        }
        sw.Stop();
        Trace.WriteLine("branchless=" + sw.ElapsedTicks);

        /////////////////////////////////////

        a = 0.0f;
        sw.Restart();
        for (int i = 0; i < iterations; i += 4)
        {
            var a1 = ((i & 1) * 2 - 1);
            var a2 = (((i + 1) & 1) * 2 - 1);
            var a3 = (((i + 2) & 1) * 2 - 1);
            var a4 = (((i + 3) & 1) * 2 - 1);
            a += a1 + a2 + a3 + a4;
        }
        sw.Stop();
        Trace.WriteLine("SSEBranchless=" + sw.ElapsedTicks);

        /////////////////////////////////////

        a = 0.0f;
        sw.Restart();
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
        sw.Stop();
        Trace.WriteLine("pipelinedSSEBranchless=" + sw.ElapsedTicks);
    }

    [TestMethod]
    public unsafe void AnalysisOptimization()
    {
        int pixelCount = 640*640;
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
            
            sw.Start();
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
            sw.Stop();
            Assert.IsTrue(allWhite);
            Trace.WriteLine("single=" + sw.ElapsedTicks);

            /////////////////////////////////////

            sw.Restart();
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
            sw.Stop();
            Assert.IsTrue(allWhite);
            Trace.WriteLine("branchless=" + sw.ElapsedTicks);

            /////////////////////////////////////

            sw.Restart();
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
            sw.Stop();
            Assert.IsTrue(allWhite);
            Trace.WriteLine("multi=" + sw.ElapsedTicks);

            ///////////////////////////////////// 
            //              Best               //
            /////////////////////////////////////

            sw.Restart();
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
            sw.Stop();
            Assert.IsTrue(allWhite);
            Trace.WriteLine("halfpipeSSE=" + sw.ElapsedTicks);

            /////////////////////////////////////

            sw.Restart();
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
            sw.Stop();
            Assert.IsTrue(allWhite);
            Trace.WriteLine("SSE=" + sw.ElapsedTicks);

            /////////////////////////////////////

            sw.Restart();
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
            sw.Stop();
            Assert.IsTrue(allWhite);
            Trace.WriteLine("pipelinedSSE=" + sw.ElapsedTicks);
        }
    }

    [TestMethod]
    public void MathvsMathF()
    {
        int iterations = 10_000_000;

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

        sw.Start();
        for (int i = 0; i < iterations; ++i)
        {
            MathF.Sin(fRands[i]);
        }
        sw.Stop();
        Trace.WriteLine("sinf=" + sw.ElapsedTicks);

        sw.Restart();
        for (int i = 0; i < iterations; ++i)
        {
            MathF.Cos(fRands[i]);
        }
        sw.Stop();
        Trace.WriteLine("cosf=" + sw.ElapsedTicks);

        sw.Restart();
        for (int i = 0; i < iterations; ++i)
        {
            Math.Sin(dRands[i]);
        }
        sw.Stop();
        Trace.WriteLine("sin=" + sw.ElapsedTicks);

        sw.Restart();
        for (int i = 0; i < iterations; ++i)
        {
            Math.Cos(dRands[i]);
        }
        sw.Stop();
        Trace.WriteLine("cos=" + sw.ElapsedTicks);

        sw.Restart();
        for (int i = 0; i < iterations; ++i)
        {
            Math.Cos(HALFPI - dRands[i]);
        }
        sw.Stop();
        Trace.WriteLine("sin2=" + sw.ElapsedTicks);
    }

    [TestMethod]
    public void RotationMatrixOptimization()
    {
        int iterations = 10_000_000;

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

        sw.Start();
        for (int i = 0; i < iterations; ++i)
        {
            CreateRotationF(rands[i]);
        }
        sw.Stop();
        Trace.WriteLine("MathF=" + sw.ElapsedTicks);

        sw.Restart();
        for (int i = 0; i < iterations; ++i)
        {
            CreateRotationExplicit(rands[i]);
        }
        sw.Stop();
        Trace.WriteLine("explicit=" + sw.ElapsedTicks);

        sw.Restart();
        for (int i = 0; i < iterations; ++i)
        {
            CreateRotationZeroInit(rands[i]);
        }
        sw.Stop();
        Trace.WriteLine("zeroinit=" + sw.ElapsedTicks);

        sw.Restart();
        for (int i = 0; i < iterations; ++i)
        {
            CreateRotationIdentityInit(rands[i]);
        }
        sw.Stop();
        Trace.WriteLine("identityInit=" + sw.ElapsedTicks);
    }

    [TestMethod]
    public void ViewMatrixOptimization()
    {
        int iterations = 10_000_000;

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

        sw.Start();
        for (int i = 0; i < iterations; ++i)
        {
            CreateViewOG(poss[i], rots[i]);
        }
        sw.Stop();
        Trace.WriteLine("og=" + sw.ElapsedTicks);

        sw.Restart();
        for (int i = 0; i < iterations; ++i)
        {
            CreateViewRemix1(poss[i], rots[i]);
        }
        sw.Stop();
        Trace.WriteLine("re1=" + sw.ElapsedTicks);

        sw.Restart();
        for (int i = 0; i < iterations; ++i)
        {
            CreateViewRemix2(poss[i], rots[i]);
        }
        sw.Stop();
        Trace.WriteLine("re2=" + sw.ElapsedTicks);

        sw.Restart();
        for (int i = 0; i < iterations; ++i)
        {
            CreateViewRemix3(poss[i], rots[i]);
        }
        sw.Stop();
        Trace.WriteLine("re3=" + sw.ElapsedTicks);

        sw.Restart();
        for (int i = 0; i < iterations; ++i)
        {
            CreateViewRemix4(poss[i], rots[i]);
        }
        sw.Stop();
        Trace.WriteLine("re4=" + sw.ElapsedTicks);
    }

    [TestMethod]
    public void RotxMatrixOptimization()
    {
        int iterations = 10_000_000;

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

        sw.Start();
        for (int i = 0; i < iterations; ++i)
        {
            CreateRotationXOG(rands[i]);
        }
        sw.Stop();
        Trace.WriteLine("og=" + sw.ElapsedTicks);

        sw.Restart();
        for (int i = 0; i < iterations; ++i)
        {
            CreateRotationXRemix1(rands[i]);
        }
        sw.Stop();
        Trace.WriteLine("re1=" + sw.ElapsedTicks);
    }

    [TestMethod]
    public unsafe void UnsafeWriting()
    {
        /////////////////////////////////////
        //             Don't               //
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
}
