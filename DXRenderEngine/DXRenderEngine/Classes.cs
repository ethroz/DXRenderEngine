using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Reflection;
using Vortice.Direct3D11;
using Vortice.DirectInput;
using Vortice.Mathematics;

namespace DXRenderEngine;

public class Gameobject
{
    public string Name;
    public Vector3 Position, Rotation, Scale;
    public TriNormsCol[] Triangles;
    public TriNormsCol[] ProjectedTriangles;
    public int VerticesOffset;
    public Matrix4x4 World;
    public Matrix4x4 Normal;

    public Gameobject()
    {
        Name = "object";
        Position = new Vector3();
        Rotation = new Vector3();
        Scale = new Vector3(1.0f);
    }

    public Gameobject(string name)
    {
        this.Name = name;
        Position = new Vector3();
        Rotation = new Vector3();
        Scale = new Vector3(1.0f);
    }

    public Gameobject(string name, Vector3 p, Vector3 r, Vector3 s, TriNormsCol[] v)
    {
        this.Name = name;
        Position = p;
        Rotation = r;
        Scale = s;
        Triangles = v;
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
        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Color4> colors = new List<Color4>();
        List<int[]> vertexIndices = new List<int[]>();
        List<int[]> normalIndices = new List<int[]>();

        for (int i = 0; i < document.Length; i++)
        {

            if (document[i] == "" || document[i][0] == '#' || document[i][0] == 'm' || document[i][0] == 'u' || document[i][0] == 's')
                continue;
            else if (document[i][0] == 'o')
            {
                name = document[i].Substring(2);
            }
            else if (document[i].Substring(0, 2) == "v ")
            {
                List<int> values = new List<int>();
                for (int j = 1; j < document[i].Length; j++)
                {
                    if (document[i][j] == ' ')
                        values.Add(j + 1);
                }
                values.Add(document[i].Length + 1);

                Vector3 v = new Vector3
                (
                    float.Parse(document[i].Substring(values[0], values[1] - values[0] - 1)),
                    float.Parse(document[i].Substring(values[1], values[2] - values[1] - 1)),
                    float.Parse(document[i].Substring(values[2], values[3] - values[2] - 1))
                );
                verts.Add(v);

                if (values.Count > 4)
                {
                    Color4 c = new Color4
                    (
                        int.Parse(document[i].Substring(values[3], values[4] - values[3] - 1)),
                        int.Parse(document[i].Substring(values[4], values[5] - values[4] - 1)),
                        int.Parse(document[i].Substring(values[5]))
                    );
                    colors.Add(c);
                }
                else
                {
                    colors.Add(Colors.White);
                }
            }
            else if (document[i].Substring(0, 3) == "vn ")
            {
                List<int> values = new List<int>();
                for (int j = 2; j < document[i].Length; j++)
                {
                    if (document[i][j] == ' ')
                        values.Add(j + 1);
                }

                Vector3 n = new Vector3
                (
                    float.Parse(document[i].Substring(values[0], values[1] - values[0] - 1)),
                    float.Parse(document[i].Substring(values[1], values[2] - values[1] - 1)),
                    float.Parse(document[i].Substring(values[2], document[i].Length - values[2]))
                );
                norms.Add(n);
            }
            else if (document[i][0] == 'f')
            {
                List<int> values = new List<int>();
                for (int j = 1; j < document[i].Length; j++)
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

        Vector3 position = new Vector3();
        for (int i = 0; i < verts.Count; i++)
            position += verts[i];
        position /= verts.Count;
        for (int i = 0; i < verts.Count; i++)
            verts[i] -= position;
        triangles = new TriNormsCol[vertexIndices.Count];
        if (norms.Count == 0)
        {
            for (int i = 0; i < vertexIndices.Count; i++)
            {
                Vector3 normal = Vector3.Normalize(Vector3.Cross(verts[vertexIndices[i][1]] - verts[vertexIndices[i][0]], verts[vertexIndices[i][2]] - verts[vertexIndices[i][1]]));
                Vector3 col = (colors[vertexIndices[i][0]].ToVector3() + colors[vertexIndices[i][1]].ToVector3() + colors[vertexIndices[i][2]].ToVector3()) / 3.0f;
                triangles[i] = new TriNormsCol(new Vector3[] { verts[vertexIndices[i][0]], verts[vertexIndices[i][1]], verts[vertexIndices[i][2]] }, normal, col);
            }
        }
        else
        {
            for (int i = 0; i < vertexIndices.Count; i++)
            {
                Vector3 col = (colors[vertexIndices[i][0]].ToVector3() + colors[vertexIndices[i][1]].ToVector3() + colors[vertexIndices[i][2]].ToVector3()) / 3.0f;
                triangles[i] = new TriNormsCol(new Vector3[] { verts[vertexIndices[i][0]], verts[vertexIndices[i][1]], verts[vertexIndices[i][2]] }, new Vector3[] { norms[normalIndices[i][0]], norms[normalIndices[i][1]], norms[normalIndices[i][2]] }, col);
            }
        }
        return new Gameobject(name, position, new Vector3(), new Vector3(1.0f), triangles);
    }

    public static Gameobject[] GetObjects(string resource)
    {
        string[] document = GetFileFromResource(resource);
        if (document == null)
        {
            return null;
        }

        List<Gameobject> objs = new List<Gameobject>();
        List<Vector3> positions = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Color4> colors = new List<Color4>();
        List<int[]> vertexIndices = new List<int[]>();
        List<int[]> normalIndices = new List<int[]>();
        int vertexCount = 0;
        int verticesCount = 0;
        int normalCount = 0;
        int normalsCount = 0;
        int objectIndex = -1;

        for (int i = 0; i < document.Length; i++)
        {

            if (document[i] == "" || document[i][0] == '#' || document[i][0] == 'm' || document[i][0] == 'u' || document[i][0] == 's')
                continue;
            else if (document[i][0] == 'o')
            {

                if (objectIndex > -1)
                {
                    Vector3 pos = new Vector3();
                    for (int j = 0; j < positions.Count; j++)
                        pos += positions[j];
                    pos /= positions.Count;
                    for (int j = 0; j < positions.Count; j++)
                        positions[j] -= pos;
                    objs[objectIndex].Triangles = new TriNormsCol[vertexIndices.Count];
                    if (norms.Count == 0)
                    {
                        for (int j = 0; j < vertexIndices.Count; j++)
                        {
                            Vector3 normal = Vector3.Normalize(Vector3.Cross(positions[vertexIndices[j][1]] - positions[vertexIndices[j][0]], positions[vertexIndices[j][2]] - positions[vertexIndices[j][1]]));
                            Vector3 col = (colors[vertexIndices[j][0]].ToVector3() + colors[vertexIndices[j][1]].ToVector3() + colors[vertexIndices[j][2]].ToVector3()) / 3.0f;
                            objs[objectIndex].Triangles[j] = new TriNormsCol(new Vector3[] { positions[vertexIndices[j][0]], positions[vertexIndices[j][1]], positions[vertexIndices[j][2]] }, normal, col);
                        }
                    }
                    else
                    {
                        for (int j = 0; j < vertexIndices.Count; j++)
                        {
                            Vector3 col = (colors[vertexIndices[j][0]].ToVector3() + colors[vertexIndices[j][1]].ToVector3() + colors[vertexIndices[j][2]].ToVector3()) / 3.0f;
                            objs[objectIndex].Triangles[j] = new TriNormsCol(new Vector3[] { positions[vertexIndices[j][0]], positions[vertexIndices[j][1]], positions[vertexIndices[j][2]] }, new Vector3[] { norms[normalIndices[j][0]], norms[normalIndices[j][1]], norms[normalIndices[j][2]] }, col);
                        }
                    }
                    objs[objectIndex].Position = pos;
                    vertexCount += verticesCount;
                    normalCount += normalsCount;
                    verticesCount = 0;
                    normalsCount = 0;
                }
                objs.Add(new Gameobject(document[i].Substring(2)));
                positions = new List<Vector3>();
                norms = new List<Vector3>();
                colors = new List<Color4>();
                vertexIndices = new List<int[]>();
                normalIndices = new List<int[]>();
                objectIndex++;
            }
            else if (document[i].Substring(0, 2) == "v ")
            {
                List<int> values = new List<int>();
                for (int j = 1; j < document[i].Length; j++)
                {
                    if (document[i][j] == ' ')
                        values.Add(j + 1);
                }
                values.Add(document[i].Length + 1);

                Vector3 v = new Vector3
                (
                    float.Parse(document[i].Substring(values[0], values[1] - values[0] - 1)),
                    float.Parse(document[i].Substring(values[1], values[2] - values[1] - 1)),
                    float.Parse(document[i].Substring(values[2], values[3] - values[2] - 1))
                );
                positions.Add(v);

                if (values.Count > 4)
                {
                    Color4 c = new Color4
                    (
                        int.Parse(document[i].Substring(values[3], values[4] - values[3] - 1)),
                        int.Parse(document[i].Substring(values[4], values[5] - values[4] - 1)),
                        int.Parse(document[i].Substring(values[5]))
                    );
                    colors.Add(c);
                }
                else
                {
                    colors.Add(Colors.White);
                }
                verticesCount++;
            }
            else if (document[i].Substring(0, 3) == "vn ")
            {
                List<int> values = new List<int>();
                for (int j = 2; j < document[i].Length; j++)
                {
                    if (document[i][j] == ' ')
                        values.Add(j + 1);
                }

                Vector3 n = new Vector3
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
                List<int> values = new List<int>();
                for (int j = 1; j < document[i].Length; j++)
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
        Vector3 position = new Vector3();
        for (int j = 0; j < positions.Count; j++)
            position += positions[j];
        position /= positions.Count;
        for (int j = 0; j < positions.Count; j++)
            positions[j] -= position;
        objs[objectIndex].Triangles = new TriNormsCol[vertexIndices.Count];
        if (norms.Count == 0)
        {
            for (int i = 0; i < vertexIndices.Count; i++)
            {
                Vector3 normal = Vector3.Normalize(Vector3.Cross(positions[vertexIndices[i][1]] - positions[vertexIndices[i][0]], positions[vertexIndices[i][2]] - positions[vertexIndices[i][1]]));
                Vector3 col = (colors[vertexIndices[i][0]].ToVector3() + colors[vertexIndices[i][1]].ToVector3() + colors[vertexIndices[i][2]].ToVector3()) / 3.0f;
                objs[objectIndex].Triangles[i] = new TriNormsCol(new Vector3[] { positions[vertexIndices[i][0]], positions[vertexIndices[i][1]], positions[vertexIndices[i][2]] }, normal, col);
            }
        }
        else
        {
            for (int i = 0; i < vertexIndices.Count; i++)
            {
                Vector3 col = (colors[vertexIndices[i][0]].ToVector3() + colors[vertexIndices[i][1]].ToVector3() + colors[vertexIndices[i][2]].ToVector3()) / 3.0f;
                objs[objectIndex].Triangles[i] = new TriNormsCol(new Vector3[] { positions[vertexIndices[i][0]], positions[vertexIndices[i][1]], positions[vertexIndices[i][2]] }, new Vector3[] { norms[normalIndices[i][0]], norms[normalIndices[i][1]], norms[normalIndices[i][2]] }, col);
            }
        }
        objs[objectIndex].Position = position;

        return objs.ToArray();
    }

    private static string[] GetFileFromResource(string resource)
    {
        Assembly assembly = Assembly.GetExecutingAssembly();

        List<string> output = new List<string>();
        using (Stream stream = assembly.GetManifestResourceStream(resource))
        using (StreamReader reader = new StreamReader(stream))
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
        List<string> document = new List<string>();
        document.Add("# Made by Ethan Rozee in DXRenderEngine");
        document.Add("o " + o.Name);

        // vertices
        for (int i = 0; i < o.Triangles.Length; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                document.Add("v");
                Vector3 v = o.Triangles[i].Vertices[j];
                document[document.Count - 1] += " " + v.X.ToString("N6") + " " 
                    + v.Y.ToString("N6") + " " + v.Z.ToString("N6");
            }
        }

        // normals
        for (int i = 0; i < o.Triangles.Length; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                document.Add("vn");
                Vector3 v = o.Triangles[i].Normals[j];
                document[document.Count - 1] += " " + v.X.ToString("N4") + " "
                    + v.Y.ToString("N4") + " " + v.Z.ToString("N4");
            }
        }

        // faces
        for (int i = 0; i < o.Triangles.Length; i++)
        {
            document.Add("f");
            for (int j = 0; j < 3; j++)
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
            List<List<int[]>> normalIndices = new List<List<int[]>>();
            List<Vector3> verts = new List<Vector3>();
            List<List<Vector3>> allNorms = new List<List<Vector3>>();
            for (int i = 0; i < Triangles.Length; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    int count = 0;
                    for (int k = 0; k < verts.Count; k++)
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
                        allNorms.Add(new List<Vector3>());
                        allNorms[allNorms.Count - 1].Add(Triangles[i].Normals[j]);
                        normalIndices.Add(new List<int[]>());
                        normalIndices[normalIndices.Count - 1].Add(new int[] { i, j });
                    }
                }
            }

            // average the normals that have an angle less than max angle for each unique vertex
            Vector3[][] norms = new Vector3[allNorms.Count][];
            for (int i = 0; i < allNorms.Count; i++)
            {
                norms[i] = new Vector3[allNorms[i].Count];
                for (int j = 0; j < allNorms[i].Count; j++)
                {
                    List<Vector3> values = new List<Vector3>();
                    Vector3 average = allNorms[i][j];
                    values.Add(allNorms[i][j]);
                    if (allNorms[i].Count < minVertsInPoint)
                    {
                        for (int k = 0; k < allNorms[i].Count; k++)
                        {
                            if (j == k)
                                continue;
                            bool skip = false;
                            for (int m = 0; m < values.Count; m++)
                            {
                                if ((values[m] - allNorms[i][k]).Length() < 0.00001f)
                                {
                                    skip = true;
                                    break;
                                }
                            }
                            if (skip)
                                continue;
                            if (Math.Acos(Math.Min(Math.Max(Vector3.Dot(allNorms[i][j], allNorms[i][k]), -1.0f), 1.0f)) / Engine.DEG2RAD < maxAngleDeg)
                            {
                                values.Add(allNorms[i][k]);
                                average += allNorms[i][k];
                            }
                        }
                        norms[i][j] = Normalize(average);
                    }
                    else
                        norms[i][j] = new Vector3();
                }
            }

            // reassign normals to matching vertices
            for (int i = 0; i < norms.Length; i++)
            {
                for (int j = 0; j < norms[i].Length; j++)
                {
                    Triangles[normalIndices[i][j][0]].Normals[normalIndices[i][j][1]] = norms[i][j];
                }
            }
        }
        else // shade flat
        {
            for (int i = 0; i < Triangles.Length; i++)
            {
                TriNormsCol current = Triangles[i];
                Vector3 normal = Normalize(Vector3.Cross(current.Vertices[1] - current.Vertices[0], current.Vertices[2] - current.Vertices[1]));
                Triangles[i].Normals = new Vector3[3] { normal, normal, normal };
            }
        }
    }

    public void ChangeObjectCharacteristics(Vector3 color, float reflect)
    {
        for (int i = 0; i < Triangles.Length; i++)
        {
            Triangles[i].Color = color;
            Triangles[i].Reflectivity = reflect;
        }
    }

    public void CreateMatrices()
    {
        World = Engine.CreateWorld(Position, Rotation, Scale);
        Matrix4x4.Invert(World, out Matrix4x4 output);
        Normal = Matrix4x4.Transpose(output);
    }

    private static Vector3 Normalize(Vector3 v)
    {
        if (v.Length() == 0.0f)
            return new Vector3();
        double x = v.X, y = v.Y, z = v.Z, l = Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
        while ((float)l != 1.0f)
        {
            x /= l; y /= l; z /= l;
            l = Math.Sqrt(x * x + y * y + z * z);
        }
        return new Vector3((float)(x / l), (float)(y / l), (float)(z / l)); ;
    }

    internal ObjectInstance GetInstance()
    {
        return new ObjectInstance(World, Normal);
    }
}

public class Sphere
{
    public Vector3 Position;
    public float Radius;
    public Vector3 Color;
    public float IOR;
    public float Reflectivity;

    public Sphere()
    {
        Position = new Vector3();
        Color = Colors.White.ToVector3();
        Radius = 1.0f;
        IOR = 1.5f;
        Reflectivity = 0.1f;
    }

    public Sphere(Vector3 position, float radius, Vector3 color, float ior, float reflect)
    {
        Position = position;
        Radius = radius;
        Color = color;
        IOR = ior;
        Reflectivity = reflect;
    }

    internal PackedSphere Pack()
    {
        return new PackedSphere(Position, Radius, Color, IOR, Reflectivity);
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
    public int ShadowRes = 1024;
    public ID3D11DepthStencilView ShadowStencilView;
    public ID3D11ShaderResourceView1 ShadowResourceView;
    public ID3D11Texture2D1 ShadowTexture;
    public ID3D11RenderTargetView1 LightTargetView;
    public ID3D11ShaderResourceView1 LightResourceView;
    public ID3D11Texture2D1 LightTexture;
    public Viewport ShadowViewPort;
    public Matrix4x4 ShadowProjectionMatrix;
    public Matrix4x4 LightMatrix;

    public Light()
    {
        Position = new Vector3();
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

    internal PackedLight Pack()
    {
        return new PackedLight(Position, Radius, Color);
    }

    public void GenerateMatrix()
    {
        Matrix4x4 shadowViewMatrix = Engine.CreateView(Position, new Vector3(90.0f, 0.0f, 0.0f));
        LightMatrix = shadowViewMatrix * ShadowProjectionMatrix;
    }

}

public class TriNormsCol
{
    public Vector3[] Vertices;
    public Vector3[] Normals;
    public Vector3 Color;
    public float Reflectivity;

    public TriNormsCol()
    {
        Vertices = new Vector3[3];
        Normals = new Vector3[3];
        Color = new Vector3();
        Reflectivity = 0.0f;
    }

    public TriNormsCol(Vector3[] verts, Vector3 normal, Vector3 color, float reflect = 0.0f)
    {
        Vertices = verts;
        Normals = new Vector3[3] { normal, normal, normal };
        Color = color;
        Reflectivity = reflect;
    }

    public TriNormsCol(Vector3[] verts, Vector3[] normals, Vector3 color, float reflect = 0.0f)
    {
        Vertices = verts;
        Normals = normals;
        Color = color;
        Reflectivity = reflect;
    }

    public VertexPositionNormalColor GetVertexPositionNormalColor(int index)
    {
        return new VertexPositionNormalColor(new Vector4(Vertices[index], 1.0f), new Vector4(Normals[index], 0.0f), new Vector4(Color, Reflectivity));
    }

    internal PackedTriangle Pack()
    {
        Vector4[] vs = new Vector4[3];
        Vector4[] ns = new Vector4[3];
        for (int i = 0; i < 3; i++)
        {
            vs[i] = new Vector4(Vertices[i], 1.0f);
            ns[i] = new Vector4(Normals[i], 0.0f);
        }
        return new PackedTriangle(vs, ns, Color, Reflectivity);
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
