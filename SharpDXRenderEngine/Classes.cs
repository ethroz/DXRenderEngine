using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DirectInput;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using Color = SharpDX.Color;

namespace SharpDXRenderEngine
{
    public class Gameobject
    {
        public string name;
        public Vector3 position, rotation, scale;
        public TriNormsCol[] triangles;
        public TriNormsCol[] projectedTriangles;
        public VertexPositionNormalColor[] vertices;
        public Vector3[] vectors;
        public Matrix world;

        public Gameobject()
        {
            name = "object";
            position = new Vector3();
            rotation = new Vector3();
            scale = new Vector3(1.0f);
        }

        public Gameobject(string name)
        {
            this.name = name;
            position = new Vector3();
            rotation = new Vector3();
            scale = new Vector3(1.0f);
        }

        public Gameobject(string name, Vector3 p, Vector3 r, Vector3 s, TriNormsCol[] v)
        {
            position = p;
            rotation = r;
            scale = s;
            triangles = v;
        }

        public static Gameobject GetObjectFromFile(string FileName)
        {
            if (!File.Exists(FileName))
                return null;
            string[] document = File.ReadAllLines(FileName);
            if (document == null)
                return null;

            string name = "object";
            TriNormsCol[] triangles;
            List<Vector3> verts = new List<Vector3>();
            List<Vector3> norms = new List<Vector3>();
            List<Color> colors = new List<Color>();
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
                        Color c = new Color
                        (
                            int.Parse(document[i].Substring(values[3], values[4] - values[3] - 1)),
                            int.Parse(document[i].Substring(values[4], values[5] - values[4] - 1)),
                            int.Parse(document[i].Substring(values[5]))
                        );
                        colors.Add(c);
                    }
                    else
                    {
                        colors.Add(Color.White);
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
                    Vector4 col = (colors[vertexIndices[i][0]].ToVector4() + colors[vertexIndices[i][1]].ToVector4() + colors[vertexIndices[i][2]].ToVector4()) / 3.0f;
                    triangles[i] = new TriNormsCol(new Vector3[] { verts[vertexIndices[i][0]], verts[vertexIndices[i][1]], verts[vertexIndices[i][2]] }, normal, col);
                }
            }
            else
            {
                for (int i = 0; i < vertexIndices.Count; i++)
                {
                    Vector4 col = (colors[vertexIndices[i][0]].ToVector4() + colors[vertexIndices[i][1]].ToVector4() + colors[vertexIndices[i][2]].ToVector4()) / 3.0f;
                    triangles[i] = new TriNormsCol(new Vector3[] { verts[vertexIndices[i][0]], verts[vertexIndices[i][1]], verts[vertexIndices[i][2]] }, new Vector3[] { norms[normalIndices[i][0]], norms[normalIndices[i][1]], norms[normalIndices[i][2]] }, col, new float[3] { 0.0f, 0.0f, 0.0f });
                }
            }
            return new Gameobject(name, position, new Vector3(), new Vector3(1.0f), triangles);
        }

        public static Gameobject[] GetObjectsFromFile(string FileName)
        {
            if (!File.Exists(FileName))
                return null;
            string[] document = File.ReadAllLines(FileName);
            if (document == null)
                return null;

            List<Gameobject> objs = new List<Gameobject>();
            List<Vector3> positions = new List<Vector3>();
            List<Vector3> norms = new List<Vector3>();
            List<Color> colors = new List<Color>();
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
                        objs[objectIndex].triangles = new TriNormsCol[vertexIndices.Count];
                        if (norms.Count == 0)
                        {
                            for (int j = 0; j < vertexIndices.Count; j++)
                            {
                                Vector3 normal = Vector3.Normalize(Vector3.Cross(positions[vertexIndices[j][1]] - positions[vertexIndices[j][0]], positions[vertexIndices[j][2]] - positions[vertexIndices[j][1]]));
                                Vector4 col = (colors[vertexIndices[j][0]].ToVector4() + colors[vertexIndices[j][1]].ToVector4() + colors[vertexIndices[j][2]].ToVector4()) / 3.0f;
                                objs[objectIndex].triangles[j] = new TriNormsCol(new Vector3[] { positions[vertexIndices[j][0]], positions[vertexIndices[j][1]], positions[vertexIndices[j][2]] }, normal, col);
                            }
                        }
                        else
                        {
                            for (int j = 0; j < vertexIndices.Count; j++)
                            {
                                Vector4 col = (colors[vertexIndices[j][0]].ToVector4() + colors[vertexIndices[j][1]].ToVector4() + colors[vertexIndices[j][2]].ToVector4()) / 3.0f;
                                objs[objectIndex].triangles[j] = new TriNormsCol(new Vector3[] { positions[vertexIndices[j][0]], positions[vertexIndices[j][1]], positions[vertexIndices[j][2]] }, new Vector3[] { norms[normalIndices[j][0]], norms[normalIndices[j][1]], norms[normalIndices[j][2]] }, col, new float[3] { 0.0f, 0.0f, 0.0f });
                            }
                        }
                        objs[objectIndex].position = pos;
                        vertexCount += verticesCount;
                        normalCount += normalsCount;
                        verticesCount = 0;
                        normalsCount = 0;
                    }
                    objs.Add(new Gameobject(document[i].Substring(2)));
                    positions = new List<Vector3>();
                    norms = new List<Vector3>();
                    colors = new List<Color>();
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
                        Color c = new Color
                        (
                            int.Parse(document[i].Substring(values[3], values[4] - values[3] - 1)),
                            int.Parse(document[i].Substring(values[4], values[5] - values[4] - 1)),
                            int.Parse(document[i].Substring(values[5]))
                        );
                        colors.Add(c);
                    }
                    else
                    {
                        colors.Add(Color.White);
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
            objs[objectIndex].triangles = new TriNormsCol[vertexIndices.Count];
            if (norms.Count == 0)
            {
                for (int i = 0; i < vertexIndices.Count; i++)
                {
                    Vector3 normal = Vector3.Normalize(Vector3.Cross(positions[vertexIndices[i][1]] - positions[vertexIndices[i][0]], positions[vertexIndices[i][2]] - positions[vertexIndices[i][1]]));
                    Vector4 col = (colors[vertexIndices[i][0]].ToVector4() + colors[vertexIndices[i][1]].ToVector4() + colors[vertexIndices[i][2]].ToVector4()) / 3.0f;
                    objs[objectIndex].triangles[i] = new TriNormsCol(new Vector3[] { positions[vertexIndices[i][0]], positions[vertexIndices[i][1]], positions[vertexIndices[i][2]] }, normal, col);
                }
            }
            else
            {
                for (int i = 0; i < vertexIndices.Count; i++)
                {
                    Vector4 col = (colors[vertexIndices[i][0]].ToVector4() + colors[vertexIndices[i][1]].ToVector4() + colors[vertexIndices[i][2]].ToVector4()) / 3.0f;
                    objs[objectIndex].triangles[i] = new TriNormsCol(new Vector3[] { positions[vertexIndices[i][0]], positions[vertexIndices[i][1]], positions[vertexIndices[i][2]] }, new Vector3[] { norms[normalIndices[i][0]], norms[normalIndices[i][1]], norms[normalIndices[i][2]] }, col, new float[3] { 0.0f, 0.0f, 0.0f });
                }
            }
            objs[objectIndex].position = position;

            return objs.ToArray();
        }

        public static void SaveGameObjectsToFile(Gameobject o, string FileName)
        {
            List<string> document = new List<string>();
            document.Add("# Made by Ethan Rozee in SharpDXRayTracingEngine");
            document.Add("o " + o.name);

            // vertices
            for (int i = 0; i < o.triangles.Length; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    document.Add("v");
                    for (int k = 0; k < 3; k++)
                    {
                        document[document.Count - 1] += " " + o.triangles[i].Vertices[j].ToArray()[k].ToString("N6");
                    }
                }
            }

            // normals
            for (int i = 0; i < o.triangles.Length; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    document.Add("vn");
                    for (int k = 0; k < 3; k++)
                    {
                        document[document.Count - 1] += " " + o.triangles[i].Normals[j].ToArray()[k].ToString("N4");
                    }
                }
            }

            // faces
            for (int i = 0; i < o.triangles.Length; i++)
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
                for (int i = 0; i < triangles.Length; i++)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        int count = 0;
                        for (int k = 0; k < verts.Count; k++)
                        {
                            if (triangles[i].Vertices[j] == verts[k])
                            {
                                allNorms[k].Add(triangles[i].Normals[j]);
                                normalIndices[k].Add(new int[] { i, j });
                                count++;
                            }
                        }
                        if (count == 0)
                        {
                            verts.Add(triangles[i].Vertices[j]);
                            allNorms.Add(new List<Vector3>());
                            allNorms[allNorms.Count - 1].Add(triangles[i].Normals[j]);
                            normalIndices.Add(new List<int[]>());
                            normalIndices[normalIndices.Count - 1].Add(new int[] { i, j });
                        }
                    }
                }

                // average the normals that have an angle less than max angle for each unique vertex
                Vector3[][] norms = new Vector3[allNorms.Count][];
                float[][] specials = new float[allNorms.Count][];
                for (int i = 0; i < allNorms.Count; i++)
                {
                    norms[i] = new Vector3[allNorms[i].Count];
                    specials[i] = new float[allNorms[i].Count];
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
                                if (Math.Acos(Math.Min(Math.Max(Vector3.Dot(allNorms[i][j], allNorms[i][k]), -1.0f), 1.0f)) / Engine.Deg2Rad < maxAngleDeg)
                                {
                                    values.Add(allNorms[i][k]);
                                    average += allNorms[i][k];
                                }
                            }
                            specials[i][j] = 0.0f;
                        }
                        else
                            specials[i][j] = 1.0f;
                        norms[i][j] = Normalize(average);
                    }
                }

                // reassign normals to matching vertices
                for (int i = 0; i < norms.Length; i++)
                {
                    for (int j = 0; j < norms[i].Length; j++)
                    {
                        triangles[normalIndices[i][j][0]].Normals[normalIndices[i][j][1]] = norms[i][j];
                        triangles[normalIndices[i][j][0]].Specials[normalIndices[i][j][1]] = specials[i][j];
                    }
                }
            }
            else // shade flat
            {
                for (int i = 0; i < triangles.Length; i++)
                {
                    TriNormsCol current = triangles[i];
                    Vector3 normal = Normalize(Vector3.Cross(current.Vertices[1] - current.Vertices[0], current.Vertices[2] - current.Vertices[1]));
                    triangles[i].Normals = new Vector3[3] { normal, normal, normal };
                    triangles[i].Specials = new float[3] { 0.0f, 0.0f, 0.0f };
                }
            }
        }

        public void ChangeGameObjectColor(Vector4 color)
        {
            for (int i = 0; i < triangles.Length; i++)
            {
                triangles[i].Color = color;
            }
        }

        public static Vector3 Normalize(Vector3 v)
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
    }

    public class Sphere
    {
        public Vector3 position;
        public float radius;
        public Vector4 color;
        public Vector4 Data;

        public Sphere()
        {
            position = new Vector3();
            color = Color.White.ToVector4();
            radius = 0.5f;
            Data = new Vector4(float.PositiveInfinity, 0.0f, 0.0f, 0.0f);
        }

        public Sphere(Vector3 p, float r, Vector4 c, Vector4 d)
        {
            position = p;
            color = c;
            radius = r;
            Data = d;
        }
    }

    public class Light
    {
        public Vector3 Position;
        public float Radius;
        public Vector4 Color;
        public float Luminosity;
        public float NearPlane = 0.1f;
        public float FarPlane = 100.0f;
        public int ShadowRes = 4096;
        public DepthStencilView ShadowStencilView;
        public ShaderResourceView ShadowResourceView;
        public Texture2D ShadowBuffer;
        public RenderTargetView LightTargetView;
        public ShaderResourceView LightResourceView;
        public Texture2D LightTexture;
        public Viewport ShadowViewPort;
        public Matrix ShadowProjectionMatrix;
        public Matrix ShadowViewMatrix;

        public Light(Vector3 position, Vector4 color, float radius, float luminosity)
        {
            Position = position;
            Color = color;
            Radius = radius;
            Luminosity = luminosity;
        }
    }

    public class Chey
    {
        public Key key;
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

    public class DirectBitmap : IDisposable
    {
        public Bitmap Bitmap { get; private set; }
        public int[] Bits { get; private set; }
        public bool Disposed { get; private set; }
        public int Height { get; private set; }
        public int Width { get; private set; }

        public GCHandle BitsHandle { get; private set; }

        public DirectBitmap(int width, int height)
        {
            Width = width;
            Height = height;
            Bits = new int[width * height];
            BitsHandle = GCHandle.Alloc(Bits, GCHandleType.Pinned);
            Bitmap = new Bitmap(width, height, width * 4, PixelFormat.Format32bppArgb, BitsHandle.AddrOfPinnedObject());
        }

        public void SetPixel(int x, int y, Color color)
        {
            int index = x + (y * Width);
            int col = (int)(color.A << 24) + (int)(color.R << 16) + (int)(color.G << 8) + color.B;

            Bits[index] = col;
        }

        public Color GetPixel(int x, int y)
        {
            int index = x + (y * Width);
            int col = Bits[index];
            Color result = new Color((col >> 16) & 0xFF, (col >> 8) & 0xFF, col & 0xFF);

            return result;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool boolean)
        {
            if (Disposed) return;
            Disposed = true;
            Bitmap.Dispose();
            BitsHandle.Free();
        }
    }

    public struct EngineDescription
    {
        public int Width, Height, RefreshRate, RayDepth;
        public Action OnAwake, OnStart, OnUpdate, UserInput;
        public Engine.WindowState WindowState;
        public Engine.RenderType RenderType;
        public PrimitiveTopology Topology;
        public ProjectionDescription ProjectionDesc;
    }

    public struct ProjectionDescription
    {
        public float FOVVDegrees, AspectRatioWH, NearPlane, FarPlane;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VertexPositionNormalColor
    {
        public readonly Vector4 Normal;
        public readonly Vector4 Color;
        public readonly Vector3 Position;

        public VertexPositionNormalColor(Vector3 position, Vector4 normal, Vector4 color)
        {
            Color = color;
            Position = position;
            Normal = normal;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 16)]
    public struct VertexPositionTexture
    {
        public VertexPositionTexture(Vector3 position)
        {
            Position = new Vector4(position, 1.0f);
            TextureUV = new Vector2();
            padding = new Vector2();
        }

        public VertexPositionTexture(Vector3 position, Vector2 textureUV)
        {
            Position = new Vector4(position, 1.0f);
            TextureUV = textureUV;
            padding = new Vector2();
        }

        public Vector4 Position;
        public Vector2 TextureUV;
        private Vector2 padding;
    }

    public struct TriNormsCol
    {
        public TriNormsCol(int n)
        {
            Vertices = new Vector3[3];
            Normals = new Vector3[3];
            Specials = new float[3];
            Color = new Vector4();
        }

        public TriNormsCol(Vector3[] verts, Vector3 normal, Vector4 color, float special = 0.0f)
        {
            Vertices = verts;
            Normals = new Vector3[3] { normal, normal, normal };
            Specials = new float[3] { special, special, special };
            Color = color;
        }

        public TriNormsCol(Vector3[] verts, Vector3[] normals, Vector4 color, float[] specials)
        {
            Vertices = verts;
            Normals = normals;
            Specials = specials;
            Color = color;
        }

        public Vector3[] Vertices;
        public Vector3[] Normals;
        public float[] Specials;
        public Vector4 Color;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 32)]
    public struct MainBuffer
    {
        public MainBuffer(Matrix3x3 eyerot, Vector3 eyepos, Vector3 bgcol, float width, float height, float minbright, float moddedtime, int raydepth, int numtris, int numspheres, int numlights)
        {
            EyeRota = new Vector4(eyerot.Row1, 0.0f);
            EyeRotb = new Vector4(eyerot.Row2, 0.0f);
            EyeRotc = new Vector4(eyerot.Row3, 0.0f);
            EyePos = eyepos;
            Width = width;
            BGCol = bgcol;
            Height = height;
            MinBrightness = minbright;
            ModdedTime = moddedtime;
            RayDepth = raydepth;
            NumTris = numtris;
            NumSpheres = numspheres;
            NumLights = numlights;
        }

        public Vector4 EyeRota;
        public Vector4 EyeRotb;
        public Vector4 EyeRotc;
        public Vector3 EyePos;
        public float Width;
        public Vector3 BGCol;
        public float Height;
        public float MinBrightness;
        public float ModdedTime;
        public int RayDepth;
        public int NumTris;
        public int NumSpheres;
        public int NumLights;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 64)]
    public struct MatrixBuffer
    {
        public Matrix ProjectionMatrix;
        public Matrix ViewMatrix;
        public Matrix WorldMatrix;
        public Matrix NormalMatrix;
        public Matrix LightProjectionMatrix;
        public int LightIndex;
    }

    public struct EmptyBuffer
    {
        public EmptyBuffer(int size) { this.size = size; }
        public int size;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }
}