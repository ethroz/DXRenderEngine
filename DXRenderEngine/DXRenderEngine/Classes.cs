using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Reflection;
using Vortice.Direct3D11;
using Vortice.DirectInput;
using Vortice.Mathematics;
using static DXRenderEngine.Helpers;

namespace DXRenderEngine;

public class Gameobject
{
    public string Name;
    public Vector3 Position, Rotation, Scale;
    public TriNormsCol[] Triangles;
    public TriNormsCol[] ProjectedTriangles;
    public PackedGameobject[] Pieces;
    public Material Material;
    public int Offset;
    public Matrix4x4 World;
    public Matrix4x4 Normal;

    public Gameobject()
    {
        Name = "object";
        Position = new();
        Rotation = new();
        Scale = new(1.0f);
        Material = Material.Default;
    }

    public Gameobject(string name)
    {
        Name = name;
        Position = new();
        Rotation = new();
        Scale = new(1.0f);
        Material = Material.Default;
    }

    public Gameobject(string n, Vector3 p, Vector3 r, Vector3 s, TriNormsCol[] v, Material m)
    {
        Name = n;
        Position = p;
        Rotation = r;
        Scale = s;
        Triangles = v;
        Material = m;
    }

    public static Gameobject GetObject(string resource)
    {
        string[] document = GetFileFromResource(resource);
        if (document == null)
        {
            return null;
        }

        string name = "object";
        TriNormsCol[] triangles;
        List<Vector3> verts = new();
        List<Vector3> norms = new();
        List<Color4> colors = new();
        List<int[]> vertexIndices = new();
        List<int[]> normalIndices = new();

        for (int i = 0; i < document.Length; ++i)
        {

            if (document[i] == "" || document[i][0] == '#' || document[i][0] == 'm' || document[i][0] == 'u' || document[i][0] == 's')
                continue;
            else if (document[i][0] == 'o')
            {
                name = document[i].Substring(2);
            }
            else if (document[i].Substring(0, 2) == "v ")
            {
                List<int> values = new();
                for (int j = 1; j < document[i].Length; ++j)
                {
                    if (document[i][j] == ' ')
                        values.Add(j + 1);
                }
                values.Add(document[i].Length + 1);

                Vector3 v = new
                (
                    float.Parse(document[i].Substring(values[0], values[1] - values[0] - 1)),
                    float.Parse(document[i].Substring(values[1], values[2] - values[1] - 1)),
                    float.Parse(document[i].Substring(values[2], values[3] - values[2] - 1))
                );
                verts.Add(v);

                if (values.Count > 4)
                {
                    Color4 c = new
                    (
                        int.Parse(document[i].Substring(values[3], values[4] - values[3] - 1)),
                        int.Parse(document[i].Substring(values[4], values[5] - values[4] - 1)),
                        int.Parse(document[i].Substring(values[5]))
                    );
                    colors.Add(c);
                }
            }
            else if (document[i].Substring(0, 3) == "vn ")
            {
                List<int> values = new();
                for (int j = 2; j < document[i].Length; ++j)
                {
                    if (document[i][j] == ' ')
                        values.Add(j + 1);
                }

                Vector3 n = new
                (
                    float.Parse(document[i].Substring(values[0], values[1] - values[0] - 1)),
                    float.Parse(document[i].Substring(values[1], values[2] - values[1] - 1)),
                    float.Parse(document[i].Substring(values[2], document[i].Length - values[2]))
                );
                norms.Add(n);
            }
            else if (document[i][0] == 'f')
            {
                List<int> values = new();
                for (int j = 1; j < document[i].Length; ++j)
                {
                    if (document[i][j] == '/')
                        values.Add(j);
                    if (document[i][j] == ' ')
                        values.Add(j + 1);
                }
                values.Add(document[i].Length - 1);
                int type = 0b100;
                if (norms.Count != 0)
                    type += 1;
                int[] lineNum;
                if ((type & 0b001) > 0)
                {
                    lineNum = new int[6]
                    {
                            int.Parse(document[i].Substring(values[0], values[1] - values[0])),
                            int.Parse(document[i].Substring(values[3], values[4] - values[3])),
                            int.Parse(document[i].Substring(values[6], values[7] - values[6])),
                            int.Parse(document[i].Substring(values[2] + 1, values[3] - values[2] - 1)),
                            int.Parse(document[i].Substring(values[5] + 1, values[6] - values[5] - 1)),
                            int.Parse(document[i].Substring(values[8] + 1, values[9] - values[8]))
                    };
                    vertexIndices.Add(new int[] { lineNum[0] - 1, lineNum[1] - 1, lineNum[2] - 1 });
                    normalIndices.Add(new int[] { lineNum[3] - 1, lineNum[4] - 1, lineNum[5] - 1 });
                }
                else
                {
                    lineNum = new int[3]
                    {
                            int.Parse(document[i].Substring(values[0], values[1] - values[0] - 1)),
                            int.Parse(document[i].Substring(values[1], values[2] - values[1] - 1)),
                            int.Parse(document[i].Substring(values[2]))
                    };
                    vertexIndices.Add(new int[] { lineNum[0] - 1, lineNum[1] - 1, lineNum[2] - 1 });
                }
            }
        }

        Vector3 position = new();
        for (int i = 0; i < verts.Count; ++i)
            position += verts[i];
        position /= verts.Count;
        for (int i = 0; i < verts.Count; ++i)
            verts[i] -= position;
        triangles = new TriNormsCol[vertexIndices.Count];
        if (norms.Count == 0)
        {
            for (int i = 0; i < vertexIndices.Count; ++i)
            {
                Vector3 normal = Vector3.Normalize(Vector3.Cross(verts[vertexIndices[i][1]] - verts[vertexIndices[i][0]], verts[vertexIndices[i][2]] - verts[vertexIndices[i][1]]));
                triangles[i] = new(new Vector3[] { verts[vertexIndices[i][0]], verts[vertexIndices[i][1]], verts[vertexIndices[i][2]] }, normal);
            }
        }
        else
        {
            for (int i = 0; i < vertexIndices.Count; ++i)
            {
                triangles[i] = new(new Vector3[] { verts[vertexIndices[i][0]], verts[vertexIndices[i][1]], verts[vertexIndices[i][2]] }, new Vector3[] { norms[normalIndices[i][0]], norms[normalIndices[i][1]], norms[normalIndices[i][2]] });
            }
        }

        Vector3 averageColor = new();
        if (colors.Count > 0)
        {
            foreach (Color4 c in colors)
                averageColor += c.ToVector3();
            averageColor /= colors.Count;
        }
        else
        {
            averageColor = Colors.White.ToVector3();
        }

        Material mat = Material.Default;
        mat.DiffuseColor = averageColor;
        return new(name, position, new(), new(1.0f), triangles, mat);
    }

    public static Gameobject[] GetObjects(string resource)
    {
        string[] document = GetFileFromResource(resource);
        if (document == null)
        {
            return null;
        }

        List<Gameobject> objs = new();
        List<Vector3> positions = new();
        List<Vector3> norms = new();
        List<Color4> colors = new();
        List<int[]> vertexIndices = new();
        List<int[]> normalIndices = new();
        int vertexCount = 0;
        int verticesCount = 0;
        int normalCount = 0;
        int normalsCount = 0;
        int objectIndex = -1;

        for (int i = 0; i < document.Length; ++i)
        {

            if (document[i] == "" || document[i][0] == '#' || document[i][0] == 'm' || document[i][0] == 'u' || document[i][0] == 's')
                continue;
            else if (document[i][0] == 'o')
            {

                if (objectIndex > -1)
                {
                    Vector3 pos = new();
                    for (int j = 0; j < positions.Count; ++j)
                        pos += positions[j];
                    pos /= positions.Count;
                    for (int j = 0; j < positions.Count; ++j)
                        positions[j] -= pos;
                    objs[objectIndex].Triangles = new TriNormsCol[vertexIndices.Count];
                    if (norms.Count == 0)
                    {
                        for (int j = 0; j < vertexIndices.Count; ++j)
                        {
                            Vector3 normal = Vector3.Normalize(Vector3.Cross(positions[vertexIndices[j][1]] - positions[vertexIndices[j][0]], positions[vertexIndices[j][2]] - positions[vertexIndices[j][1]]));
                            objs[objectIndex].Triangles[j] = new(new Vector3[] { positions[vertexIndices[j][0]], positions[vertexIndices[j][1]], positions[vertexIndices[j][2]] }, normal);
                        }
                    }
                    else
                    {
                        for (int j = 0; j < vertexIndices.Count; ++j)
                        {
                            objs[objectIndex].Triangles[j] = new(new Vector3[] { positions[vertexIndices[j][0]], positions[vertexIndices[j][1]], positions[vertexIndices[j][2]] }, 
                                new Vector3[] { norms[normalIndices[j][0]], norms[normalIndices[j][1]], norms[normalIndices[j][2]] });
                        }
                    }
                    objs[objectIndex].Position = pos;

                    Vector3 avgColor = new();
                    if (colors.Count > 0)
                    {
                        foreach (Color4 c in colors)
                            avgColor += c.ToVector3();
                        avgColor /= colors.Count;
                    }
                    else
                    {
                        avgColor = Colors.White.ToVector3();
                    }

                    objs[objectIndex].Material = Material.Default;
                    objs[objectIndex].Material.DiffuseColor = avgColor;
                    vertexCount += verticesCount;
                    normalCount += normalsCount;
                    verticesCount = 0;
                    normalsCount = 0;
                }
                objs.Add(new(document[i].Substring(2)));
                positions = new();
                norms = new();
                colors = new();
                vertexIndices = new();
                normalIndices = new();
                objectIndex++;
            }
            else if (document[i].Substring(0, 2) == "v ")
            {
                List<int> values = new();
                for (int j = 1; j < document[i].Length; ++j)
                {
                    if (document[i][j] == ' ')
                        values.Add(j + 1);
                }
                values.Add(document[i].Length + 1);

                Vector3 v = new
                (
                    float.Parse(document[i].Substring(values[0], values[1] - values[0] - 1)),
                    float.Parse(document[i].Substring(values[1], values[2] - values[1] - 1)),
                    float.Parse(document[i].Substring(values[2], values[3] - values[2] - 1))
                );
                positions.Add(v);

                if (values.Count > 4)
                {
                    Color4 c = new
                    (
                        int.Parse(document[i].Substring(values[3], values[4] - values[3] - 1)),
                        int.Parse(document[i].Substring(values[4], values[5] - values[4] - 1)),
                        int.Parse(document[i].Substring(values[5]))
                    );
                    colors.Add(c);
                }
                verticesCount++;
            }
            else if (document[i].Substring(0, 3) == "vn ")
            {
                List<int> values = new();
                for (int j = 2; j < document[i].Length; ++j)
                {
                    if (document[i][j] == ' ')
                        values.Add(j + 1);
                }

                Vector3 n = new
                (
                    float.Parse(document[i].Substring(values[0], values[1] - values[0] - 1)),
                    float.Parse(document[i].Substring(values[1], values[2] - values[1] - 1)),
                    float.Parse(document[i].Substring(values[2], document[i].Length - values[2]))
                );
                norms.Add(n);
                normalsCount++;
            }
            else if (document[i][0] == 'f')
            {
                List<int> values = new();
                for (int j = 1; j < document[i].Length; ++j)
                {
                    if (document[i][j] == '/')
                        values.Add(j);
                    if (document[i][j] == ' ')
                        values.Add(j + 1);
                }
                values.Add(document[i].Length - 1);
                int type = 0b100;
                if (norms.Count != 0)
                    type += 1;
                int[] lineNum;
                if ((type & 0b001) > 0)
                {
                    lineNum = new int[6]
                    {
                        int.Parse(document[i].Substring(values[0], values[1] - values[0])),
                        int.Parse(document[i].Substring(values[3], values[4] - values[3])),
                        int.Parse(document[i].Substring(values[6], values[7] - values[6])),
                        int.Parse(document[i].Substring(values[2] + 1, values[3] - values[2] - 1)),
                        int.Parse(document[i].Substring(values[5] + 1, values[6] - values[5] - 1)),
                        int.Parse(document[i].Substring(values[8] + 1, values[9] - values[8]))
                    };
                    vertexIndices.Add(new int[] { lineNum[0] - 1 - vertexCount, lineNum[1] - 1 - vertexCount, lineNum[2] - 1 - vertexCount });
                    normalIndices.Add(new int[] { lineNum[3] - 1 - normalCount, lineNum[4] - 1 - normalCount, lineNum[5] - 1 - normalCount });
                }
                else
                {
                    lineNum = new int[3]
                    {
                        int.Parse(document[i].Substring(values[0], values[1] - values[0] - 1)),
                        int.Parse(document[i].Substring(values[1], values[2] - values[1] - 1)),
                        int.Parse(document[i].Substring(values[2]))
                    };
                    vertexIndices.Add(new int[] { lineNum[0] - 1 - vertexCount, lineNum[1] - 1 - vertexCount, lineNum[2] - 1 - vertexCount });
                }
            }
        }
        Vector3 position = new();
        for (int j = 0; j < positions.Count; ++j)
            position += positions[j];
        position /= positions.Count;
        for (int j = 0; j < positions.Count; ++j)
            positions[j] -= position;
        objs[objectIndex].Triangles = new TriNormsCol[vertexIndices.Count];
        if (norms.Count == 0)
        {
            for (int i = 0; i < vertexIndices.Count; ++i)
            {
                Vector3 normal = Vector3.Normalize(Vector3.Cross(positions[vertexIndices[i][1]] - positions[vertexIndices[i][0]], positions[vertexIndices[i][2]] - positions[vertexIndices[i][1]]));
                objs[objectIndex].Triangles[i] = new(new Vector3[] { positions[vertexIndices[i][0]], positions[vertexIndices[i][1]], positions[vertexIndices[i][2]] }, normal);
            }
        }
        else
        {
            for (int i = 0; i < vertexIndices.Count; ++i)
            {
                Vector3 col = (colors[vertexIndices[i][0]].ToVector3() + colors[vertexIndices[i][1]].ToVector3() + colors[vertexIndices[i][2]].ToVector3()) / 3.0f;
                objs[objectIndex].Triangles[i] = new(new Vector3[] { positions[vertexIndices[i][0]], positions[vertexIndices[i][1]], positions[vertexIndices[i][2]] }, 
                    new Vector3[] { norms[normalIndices[i][0]], norms[normalIndices[i][1]], norms[normalIndices[i][2]] });
            }
        }
        objs[objectIndex].Position = position;

        Vector3 averageColor = new();
        if (colors.Count > 0)
        {
            foreach (Color4 c in colors)
                averageColor += c.ToVector3();
            averageColor /= colors.Count;
        }
        else
        {
            averageColor = Colors.White.ToVector3();
        }

        objs[objectIndex].Material = Material.Default;
        objs[objectIndex].Material.DiffuseColor = averageColor;

        return objs.ToArray();
    }

    private static string[] GetFileFromResource(string resource)
    {
        int endNamesp = resource.IndexOf('.');
        string namesp = resource.Substring(0, endNamesp);
        Assembly assembly = Assembly.Load(namesp);

        List<string> output = new();
        using (Stream stream = assembly.GetManifestResourceStream(resource))
        using (StreamReader reader = new(stream))
        {
            for (string line = reader.ReadLine(); line != null; line = reader.ReadLine())
            {
                output.Add(line);
            }
        }

        return output.ToArray();
    }

    public static void SaveGameObjectsToFile(Gameobject o, string FileName)
    {
        List<string> document = new();
        document.Add("# Made by Ethan Rozee in DXRenderEngine");
        document.Add("o " + o.Name);

        // vertices
        for (int i = 0; i < o.Triangles.Length; ++i)
        {
            for (int j = 0; j < 3; ++j)
            {
                document.Add("v");
                Vector3 v = o.Triangles[i].Vertices[j];
                document[document.Count - 1] += " " + v.X.ToString("N6") + " " 
                    + v.Y.ToString("N6") + " " + v.Z.ToString("N6");
            }
        }

        // normals
        for (int i = 0; i < o.Triangles.Length; ++i)
        {
            for (int j = 0; j < 3; ++j)
            {
                document.Add("vn");
                Vector3 v = o.Triangles[i].Normals[j];
                document[document.Count - 1] += " " + v.X.ToString("N4") + " "
                    + v.Y.ToString("N4") + " " + v.Z.ToString("N4");
            }
        }

        // faces
        for (int i = 0; i < o.Triangles.Length; ++i)
        {
            document.Add("f");
            for (int j = 0; j < 3; ++j)
            {
                document[document.Count - 1] += " " + (i * 3 + j + 1) + "//" + (i * 3 + j + 1);
            }
        }

        File.WriteAllLines(FileName, document.ToArray());
    }

    public void ChangeGameObjectSmoothness(bool smooth, float maxAngleDeg = 90.0f, int minVertsInPoint = 10)
    {
        if (smooth) // shade smooth
        {
            // make sure it is flat
            ChangeGameObjectSmoothness(false);

            // add unique vertices only and duplicates get their normal added to the unique vertices' normal list
            List<List<int[]>> normalIndices = new();
            List<Vector3> verts = new();
            List<List<Vector3>> allNorms = new();
            for (int i = 0; i < Triangles.Length; ++i)
            {
                for (int j = 0; j < 3; ++j)
                {
                    int count = 0;
                    for (int k = 0; k < verts.Count; ++k)
                    {
                        if (Triangles[i].Vertices[j] == verts[k])
                        {
                            allNorms[k].Add(Triangles[i].Normals[j]);
                            normalIndices[k].Add(new int[] { i, j });
                            count++;
                        }
                    }
                    if (count == 0)
                    {
                        verts.Add(Triangles[i].Vertices[j]);
                        allNorms.Add(new());
                        allNorms[allNorms.Count - 1].Add(Triangles[i].Normals[j]);
                        normalIndices.Add(new());
                        normalIndices[normalIndices.Count - 1].Add(new int[] { i, j });
                    }
                }
            }

            // average the normals that have an angle less than max angle for each unique vertex
            Vector3[][] norms = new Vector3[allNorms.Count][];
            for (int i = 0; i < allNorms.Count; ++i)
            {
                norms[i] = new Vector3[allNorms[i].Count];
                for (int j = 0; j < allNorms[i].Count; ++j)
                {
                    List<Vector3> values = new();
                    Vector3 average = allNorms[i][j];
                    values.Add(allNorms[i][j]);
                    if (allNorms[i].Count < minVertsInPoint)
                    {
                        for (int k = 0; k < allNorms[i].Count; ++k)
                        {
                            if (j == k)
                                continue;
                            bool skip = false;
                            for (int m = 0; m < values.Count; ++m)
                            {
                                if ((values[m] - allNorms[i][k]).Length() < 0.00001f)
                                {
                                    skip = true;
                                    break;
                                }
                            }
                            if (skip)
                                continue;
                            if (Math.Acos(Math.Min(Math.Max(Vector3.Dot(allNorms[i][j], allNorms[i][k]), -1.0f), 1.0f)) / DEG2RAD < maxAngleDeg)
                            {
                                values.Add(allNorms[i][k]);
                                average += allNorms[i][k];
                            }
                        }
                        norms[i][j] = Normalize(average);
                    }
                    else
                        norms[i][j] = new();
                }
            }

            // reassign normals to matching vertices
            for (int i = 0; i < norms.Length; ++i)
            {
                for (int j = 0; j < norms[i].Length; ++j)
                {
                    Triangles[normalIndices[i][j][0]].Normals[normalIndices[i][j][1]] = norms[i][j];
                }
            }

            // make sure zero length normals are last
            for (int i = 0; i < Triangles.Length; ++i)
            {
                int index = -1;
                for (int j = 0; j < 3; ++j)
                {
                    if (Triangles[i].Normals[j].LengthSquared() == 0.0f)
                    {
                        index = j;
                        break;
                    }
                }

                if (index != -1)
                {
                    for (int j = 0; j < 2 - index; ++j)
                    {
                        // cycle vertices
                        Vector3 temp = Triangles[i].Normals[2];
                        Vector3 temp2 = Triangles[i].Vertices[2];
                        Triangles[i].Normals[2] = Triangles[i].Normals[1];
                        Triangles[i].Vertices[2] = Triangles[i].Vertices[1];
                        Triangles[i].Normals[1] = Triangles[i].Normals[0];
                        Triangles[i].Vertices[1] = Triangles[i].Vertices[0];
                        Triangles[i].Normals[0] = temp;
                        Triangles[i].Vertices[0] = temp2;
                    }
                }
            }
        }
        else // shade flat
        {
            for (int i = 0; i < Triangles.Length; ++i)
            {
                TriNormsCol current = Triangles[i];
                Vector3 normal = Normalize(Vector3.Cross(current.Vertices[1] - current.Vertices[0], current.Vertices[2] - current.Vertices[1]));
                Triangles[i].Normals = new Vector3[3] { normal, normal, normal };
            }
        }
    }

    public void CreateMatrices()
    {
        World = CreateWorld(Position, Rotation, Scale);
        Matrix4x4.Invert(World, out Matrix4x4 output);
        Normal = Matrix4x4.Transpose(output);
    }

    private static Vector3 Normalize(Vector3 v)
    {
        if (v.Length() == 0.0f)
            return new();
        double x = v.X, y = v.Y, z = v.Z, l = Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
        while ((float)l != 1.0f)
        {
            x /= l; y /= l; z /= l;
            l = Math.Sqrt(x * x + y * y + z * z);
        }
        return new((float)(x / l), (float)(y / l), (float)(z / l)); ;
    }

    internal ObjectInstance GetInstance()
    {
        return new(World, Normal);
    }

    internal PackedGameobject Pack()
    {
        float radius = 0.0f;
        foreach (TriNormsCol t in Triangles)
        {
            foreach (Vector3 v in t.Vertices)
            {
                Vector3 temp = v * Scale;
                radius = Math.Max(temp.LengthSquared(), radius);
            }
        }
        radius = (float)Math.Sqrt(radius);

        return new(Position, radius, Offset, Offset + Triangles.Length);
    }
}

public class Sphere
{
    public Vector3 Position;
    public float Radius;
    public Material Material;

    public Sphere()
    {
        Position = new();
        Radius = 0.0f;
        Material = Material.Default;
    }

    public Sphere(Vector3 position, float radius, Material mat)
    {
        Position = position;
        Radius = radius;
        Material = mat;
    }

    internal PackedSphere Pack()
    {
        return new(Position, Radius);
    }
}

public class Light
{
    public Vector3 Position;
    public float Radius;
    public Vector3 Color;
    public float Luminosity;
    public float NearPlane = 0.1f;
    public float FarPlane = 100.0f;
    public int ShadowRes = 128;
    public ID3D11DepthStencilView ShadowStencilView;
    public ID3D11Texture2D1 ShadowTextures;
    public Viewport ShadowViewPort;
    public Matrix4x4 ShadowProjectionMatrix;
    public readonly Matrix4x4[] ShadowMatrices = new Matrix4x4[6];

    public Light()
    {
        Position = new();
        Color = Colors.White.ToVector3();
        Radius = 1.0f;
        Luminosity = 1.0f;
    }

    public Light(Vector3 position, Vector3 color, float radius, float luminosity)
    {
        Position = position;
        Color = color;
        Radius = radius;
        Luminosity = luminosity;
    }

    public Light(Vector3 position, Color4 color, float radius, float luminosity)
    {
        Position = position;
        Color = color.ToVector3();
        Radius = radius;
        Luminosity = luminosity;
    }

    internal PackedLight Pack()
    {
        return new(Position, Radius, Color, Luminosity);
    }

    internal RasterPackedLight RasterPack()
    {
        return new(Position, Radius, Color, Luminosity, ShadowRes, FarPlane);
    }

    public void GenerateMatrix()
    {
        ShadowMatrices[0] = CreateView(Position, new(0.0f, 90.0f, 0.0f)) * ShadowProjectionMatrix;
        ShadowMatrices[1] = CreateView(Position, new(0.0f, -90.0f, 0.0f)) * ShadowProjectionMatrix;
        ShadowMatrices[2] = CreateView(Position, new(-90.0f, 0.0f, 0.0f)) * ShadowProjectionMatrix;
        ShadowMatrices[3] = CreateView(Position, new(90.0f, 0.0f, 0.0f)) * ShadowProjectionMatrix;
        ShadowMatrices[4] = CreateView(Position, new(0.0f, 0.0f, 0.0f)) * ShadowProjectionMatrix;
        ShadowMatrices[5] = CreateView(Position, new(0.0f, 180.0f, 0.0f)) * ShadowProjectionMatrix;
    }

}

public class TriNormsCol
{
    public Vector3[] Vertices;
    public Vector3[] Normals;

    public TriNormsCol()
    {
        Vertices = new Vector3[3];
        Normals = new Vector3[3];
    }

    public TriNormsCol(Vector3[] verts, Vector3 normal)
    {
        Vertices = verts;
        Normals = new Vector3[3] { normal, normal, normal };
    }

    public TriNormsCol(Vector3[] verts, Vector3[] normals)
    {
        Vertices = verts;
        Normals = normals;
    }

    public VertexPositionNormal GetVertexPositionNormalColor(int index)
    {
        return new(Vertices[index], Normals[index]);
    }

    internal PackedTriangle Pack()
    {
        Vector4[] vs = new Vector4[3];
        Vector4[] ns = new Vector4[3];
        for (int i = 0; i < 3; ++i)
        {
            vs[i] = new(Vertices[i], 1.0f);
            ns[i] = new(Normals[i], 0.0f);
        }
        return new(vs, ns);
    }
}

public class Chey
{
    public readonly Key key;
    public bool Down, Up, Held, Raised;

    public Chey(Key key)
    {
        this.key = key;
        Down = Up = Held = false;
        Raised = true;
    }
}

public class Button
{
    public bool Down, Up, Held, Raised;

    public Button()
    {
        Down = Up = Held = false;
        Raised = true;
    }
}
