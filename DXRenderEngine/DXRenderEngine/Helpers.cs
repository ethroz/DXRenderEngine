using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;

namespace DXRenderEngine
{
    public static class Helpers
    {
        public const double PI = Math.PI;
        public const float PIF = (float)Math.PI;
        public const double DEG2RAD = Math.PI / 180.0;
        public const float DEG2RADF = (float)DEG2RAD;
        public const double HALFPI = Math.PI / 2.0;

        public const int Pack = 16;
        public const int PackMask = Pack - 1;

        public static int Pow(int b, int e)
        {
            int output = 1;
            while (e-- > 0)
            {
                output *= b;
            }
            return output;
        }

        public static int Find(string str, char search, int pos = 0)
        {
            for (int i = pos; i < str.Length; ++i)
            {
                if (str[i] == search)
                {
                    return i;
                }
            }
            return -1;
        }

        public static int Find(string str, string search, int pos = 0)
        {
            for (int i = pos; i < str.Length; ++i)
            {
                for (int j = 0; j < search.Length; ++j)
                {
                    if (str[i] == search[j])
                    {
                        return i;
                    }
                }
            }
            return -1;
        }

        public static int FindNot(string str, char search, int pos = 0)
        {
            for (int i = pos; i < str.Length; ++i)
            {
                if (str[i] != search)
                {
                    return i;
                }
            }
            return -1;
        }

        public static int FindNot(string str, string search, int pos = 0)
        {
            for (int i = pos; i < str.Length; ++i)
            {
                bool match = false;
                for (int j = 0; j < search.Length; ++j)
                {
                    if (str[i] == search[j])
                    {
                        match = true;
                        break;
                    }
                }
                if (!match)
                {
                    return i;
                }
            }
            return -1;
        }

        public static int[] FindAll(string str, string search, int pos = 0)
        {
            List<int> indices = new();
            for (int i = pos; i < str.Length; ++i)
            {
                bool found = true;
                for (int j = 0; j < search.Length; ++j)
                {
                    if (str[i + j] != search[j])
                    {
                        found = false;
                        break;
                    }
                }
                if (found)
                {
                    indices.Add(i);
                }
            }
            return indices.ToArray();
        }

        public static Matrix4x4 CreateRotationX(float angle)
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
        
        public static Matrix4x4 CreateRotationY(float angle)
        {
            angle *= DEG2RADF;

            float cos = MathF.Cos(angle);
            float sin = MathF.Sin(angle);

            Matrix4x4 output = new();

            output.M11 = cos;
            output.M13 = -sin;
            output.M22 = 1.0f;
            output.M31 = sin;
            output.M33 = cos;
            output.M44 = 1.0f;

            return output;
        }

        public static Matrix4x4 CreateRotationZ(float angle)
        {
            angle *= -DEG2RADF;

            float cos = MathF.Cos(angle);
            float sin = MathF.Sin(angle);

            Matrix4x4 output = new();

            output.M11 = cos;
            output.M12 = sin;
            output.M21 = -sin;
            output.M22 = cos;
            output.M33 = 1.0f;
            output.M44 = 1.0f;

            return output;
        }

        public static Matrix4x4 CreateRotation(Vector3 rot)
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

        public static Matrix4x4 CreateWorld(Vector3 pos, Vector3 rot, Vector3 sca)
        {
            Matrix4x4 position = Matrix4x4.CreateTranslation(pos);
            Matrix4x4 rotation = CreateRotation(rot);
            Matrix4x4 scale = Matrix4x4.CreateScale(sca);

            return scale * rotation * position;
        }

        public static Matrix4x4 CreateView(Vector3 pos, Vector3 rot)
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

        public static Matrix4x4 CreateProjection(float fovVDegrees, float aspectRatioHW, float nearPlane, float farPlane)
        {
            float fFovRad = 1.0f / (float)Math.Tan(fovVDegrees * 0.5f * DEG2RAD);
            Matrix4x4 output = new();
            output.M11 = fFovRad;
            output.M22 = aspectRatioHW * fFovRad;
            output.M33 = farPlane / (farPlane - nearPlane);
            output.M43 = (-farPlane * nearPlane) / (farPlane - nearPlane);
            output.M34 = 1.0f;
            return output;
        }

        public static int PackedSize(object o)
        {
            int size = 0;
            if (o is Array arr)
            {
                int modSize = 0;
                foreach (var el in arr)
                {
                    int elSize = PackedSize(el);
                    int newSize = modSize + elSize;
                    int newModSize = newSize & PackMask;
                    if (newSize > Pack && newModSize > 0)
                    {
                        size += Pack - modSize;
                        newModSize = elSize & PackMask;
                    }
                    modSize = newModSize;
                    size += elSize;
                }
                size = (size + PackMask) & ~PackMask;
            }
            else
            {
                size = Marshal.SizeOf(o);
            }
            return size;
        }
    }
}

namespace Vortice.Mathematics
{
    // The most disappointing extension class in existence.
    public static class Colors2
    {
        public static Color4 Green => new(0.0f, 1.0f, 0.0f, 1.0f);
    }
}
