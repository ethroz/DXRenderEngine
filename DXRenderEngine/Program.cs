using DXRenderEngine;
using System;
using System.Numerics;
using System.Windows.Forms;
using Vortice.DirectInput;
using Vortice.Mathematics;

sealed class Program
{
    static Engine engine;
    static float MoveSpeed = 2.0f;
    static float Sensitivity = 0.084f;
    static double distance = 30.0;
    static int theta = 65;
    static int phi = 0;

    [MTAThread]
    static void Main()
    {
        EngineDescription ED = new(ProjectionDescription.Default, 1920, 1080, 0,
            SimpleShadow, Start, Update, UserInput, cache: false);

        ED = new RasterizingEngineDescription(ED, shadows: true);
        //ED = new RayTracingEngineDescription(ED, 1);

        engine = EngineFactory.Create(ED);
        engine.Run();
    }

    static void FiveObjects()
    {
        //engine.lights.Add(new(new(), Colors.Black, 0.0f, 0.0f));
        engine.lights.Add(new(new(0.0f, 5.0f, -10.0f), Colors.Blue, 0.2f, 1.0f));
        engine.lights.Add(new(new(-15.0f, 5.0f, 20.0f), Colors.Red, 0.2f, 1.0f));
        engine.lights.Add(new(new(15.0f, 5.0f, 20.0f), Colors2.Green, 0.2f, 1.0f));

        engine.gameobjects.AddRange(Gameobject.GetObjects("DXRenderEngine.Objects.Objects.obj"));
        engine.gameobjects[0].Material = new(Colors.Red, 1.0f, Colors.White, 0.5f, 0.0f);
        engine.gameobjects[1].Material = new(Colors.Blue, 0.75f, Colors.White, 0.5f, 0.0f);
        engine.gameobjects[2].Material = new(Colors2.Green, 0.5f, Colors.White, 0.5f, 0.0f);
        engine.gameobjects[3].Material = new(Colors.Magenta, 0.25f, Colors.White, 0.5f, 0.0f);
        engine.gameobjects[4].Material = new(Colors.Yellow, 0.0f, Colors.White, 0.5f, 0.0f);
        engine.gameobjects[1].ChangeGameObjectSmoothness(true);
        engine.gameobjects[2].ChangeGameObjectSmoothness(true);
        engine.gameobjects[3].ChangeGameObjectSmoothness(true);
        engine.gameobjects[4].ChangeGameObjectSmoothness(true);

        MakePlane(-0.9999f);
    }

    static void SimpleShadow()
    {
        engine.lights.Add(new(new(0.0f, 30.0f, 0.0f), Colors.AntiqueWhite, 0.2f, 1.0f));
        //double angle = 30.0;
        //engine.lights.Add(new(new((float)(-30.0 * Math.Sin(angle * Math.PI / 180.0)), (float)(30.0 * Math.Cos(angle * Math.PI / 180.0)), 0.0f), Colors.AntiqueWhite, 0.2f, 1.0f));
        //engine.lights.Add(new(new(0.0f, 1.5f, 6.5f), Colors.AntiqueWhite, 0.2f, 1.0f));

        //engine.gameobjects.Add(Gameobject.GetObjects("DXRenderEngine.Objects.Objects.obj")[1]);
        //engine.gameobjects[0].Material = new(Colors.DeepSkyBlue, 0.5f, Colors.White, 0.4f, 0.0f);
        //engine.gameobjects[0].ChangeGameObjectSmoothness(true);

        MakePlane(0.0f);

        //engine.gameobjects[0].Rotation.Z = -(float)angle;
        //engine.gameobjects[0].Rotation.Z = -85.0f;
        engine.gameobjects[0].Rotation.Z = -theta;

        engine.EyePos = new(0.0f, 0.02f, 0.0f);
    }

    static void SphereRoom()
    {
        float pSize = 3.0f;
        
        // sphere
        engine.gameobjects.Add(Gameobject.GetObjects("DXRenderEngine.Objects.Objects.obj")[1]);
        engine.gameobjects[0].Position = new(0.0f, 1.0f - pSize, 0.0f);
        engine.gameobjects[0].Material = new(Colors.DeepSkyBlue, 0.5f, Colors.White, 0.4f, 0.0f);
        engine.gameobjects[0].ChangeGameObjectSmoothness(true);

        // planes
        TriNormsCol[] planeVerts = new TriNormsCol[2]
        {
            new(new Vector3[] { new(-1.0f, 0.0f, -1.0f), new(-1.0f, 0.0f, 1.0f),
                new( 1.0f, 0.0f,  1.0f) }, Vector3.UnitY),
            new(new Vector3[] { new(-1.0f, 0.0f, -1.0f), new( 1.0f, 0.0f, 1.0f),
                new( 1.0f, 0.0f, -1.0f) }, Vector3.UnitY)
        };
        Material planeMat = new(Colors.White, 1.0f, Colors.White, 0.0f, 0.0f);
        engine.gameobjects.Add(new("Floor", new(0.0f, -pSize, 0.0f), new(), new(pSize), planeVerts, planeMat));
        engine.gameobjects.Add(new("Ceiling", new(0.0f, pSize, 0.0f), new(0.0f, 0.0f, 180.0f), new(pSize), planeVerts, planeMat));
        engine.gameobjects.Add(new("Front", new(0.0f, 0.0f, pSize), new(-90.0f, 0.0f, 0.0f), new(pSize), planeVerts, planeMat));
        engine.gameobjects.Add(new("Back", new(0.0f, 0.0f, -pSize), new(90.0f, 0.0f, 0.0f), new(pSize), planeVerts, planeMat));
        planeMat.DiffuseColor = Colors.Red.ToVector3();
        engine.gameobjects.Add(new("Left", new(-pSize, 0.0f, 0.0f), new(0.0f, 0.0f, 90.0f), new(pSize), planeVerts, planeMat));
        planeMat.DiffuseColor = Colors.Blue.ToVector3();
        engine.gameobjects.Add(new("Right", new(pSize, 0.0f, 0.0f), new(0.0f, 0.0f, -90.0f), new(pSize), planeVerts, planeMat));

        // light
        engine.lights.Add(new(new(0.0f, pSize - 0.8f, 0.0f), Colors.White, 0.8f, 1.0f));

        // player start
        engine.EyePos = new(0.0f, 0.0f, -2.5f * pSize);
    }

    // ray tracing only
    static void FiveGlassObjects()
    {
        engine.lights.Add(new(new(0.0f, 5.0f, -10.0f), Colors.Blue, 0.2f, 1.0f));
        engine.lights.Add(new(new(-15.0f, 5.0f, 20.0f), Colors.Red, 0.2f, 1.0f));
        engine.lights.Add(new(new(15.0f, 5.0f, 20.0f), Colors2.Green, 0.2f, 1.0f));

        engine.gameobjects.AddRange(Gameobject.GetObjects("DXRenderEngine.Objects.Objects.obj"));
        Material glass = new(Colors.White, 0.2f, Colors.White, 1.0f, 1.5f);
        engine.gameobjects[0].Material = glass;
        engine.gameobjects[1].Material = glass;
        engine.gameobjects[2].Material = glass;
        engine.gameobjects[3].Material = glass;
        engine.gameobjects[4].Material = glass;
        engine.gameobjects[1].ChangeGameObjectSmoothness(true);
        engine.gameobjects[2].ChangeGameObjectSmoothness(true);
        engine.gameobjects[3].ChangeGameObjectSmoothness(true);
        engine.gameobjects[4].ChangeGameObjectSmoothness(true);

        MakePlane(-0.9999f);
    }

    // ray tracing only
    static void TwoGlassSpheres()
    {
        Material sphereMat = new(Colors.Red, 0.2f, Colors.White, 1.0f, 1.5f);
        ((RayTracingEngine)engine).spheres.Add(new(new(1.5f, 0.0f, 3.0f), 1.0f, sphereMat));
        sphereMat.DiffuseColor = Colors.White.ToVector3();
        ((RayTracingEngine)engine).spheres.Add(new(new(-1.5f, 0.0f, 3.0f), 1.0f, sphereMat));
        engine.lights.Add(new(new(0.0f, 3.0f, -5.0f), Colors.AntiqueWhite, 0.2f, 1.0f));

        MakePlane(-0.9999f);
    } 

    // ray tracing only
    static void EnclosedRoom()
    {
        // planes
        float pSize = 3.0f;
        TriNormsCol[] planeVerts = new TriNormsCol[2]
        {
            new(new Vector3[] { new(-1.0f, 0.0f, -1.0f), new(-1.0f, 0.0f, 1.0f),
                new( 1.0f, 0.0f,  1.0f) }, Vector3.UnitY),
            new(new Vector3[] { new(-1.0f, 0.0f, -1.0f), new( 1.0f, 0.0f, 1.0f),
                new( 1.0f, 0.0f, -1.0f) }, Vector3.UnitY)
        };
        Material planeMat = new(Colors.White, 1.0f, Colors.White, 0.0f, 0.0f);
        engine.gameobjects.Add(new("Floor", new(0.0f, -pSize, 0.0f), new(), new(pSize), planeVerts, planeMat));
        engine.gameobjects.Add(new("Ceiling", new(0.0f, pSize, 0.0f), new(0.0f, 0.0f, 180.0f), new(pSize), planeVerts, planeMat));
        engine.gameobjects.Add(new("Front", new(0.0f, 0.0f, pSize), new(-90.0f, 0.0f, 0.0f), new(pSize), planeVerts, planeMat));
        engine.gameobjects.Add(new("Back", new(0.0f, 0.0f, -pSize), new(90.0f, 0.0f, 0.0f), new(pSize), planeVerts, planeMat));
        planeMat.DiffuseColor = Colors.Red.ToVector3();
        engine.gameobjects.Add(new("Left", new(-pSize, 0.0f, 0.0f), new(0.0f, 0.0f, 90.0f), new(pSize), planeVerts, planeMat));
        planeMat.DiffuseColor = Colors.Blue.ToVector3();
        engine.gameobjects.Add(new("Right", new(pSize, 0.0f, 0.0f), new(0.0f, 0.0f, -90.0f), new(pSize), planeVerts, planeMat));

        // light
        engine.lights.Add(new(new(0.0f, pSize - 0.8f, 0.0f), Colors.White, 0.8f, 1.0f));

        // spheres
        float rad = 1.0f;
        Material sphereMat = new(Colors.White, 0.3f, Colors.White, 1.0f, 0.0f);
        ((RayTracingEngine)engine).spheres.Add(new(new(-1.2f, rad - pSize, 1.5f), rad, sphereMat));
        sphereMat = new(Colors.White, 0.3f, Colors.White, 1.0f, 1.5f);
        ((RayTracingEngine)engine).spheres.Add(new(new(1.2f, rad - pSize, 0.0f), rad, sphereMat));
        sphereMat = new(Colors2.Green, 0.5f, Colors.White, 0.1f, 0.0f);
        rad = 0.5f;
        ((RayTracingEngine)engine).spheres.Add(new(new(-1.2f, rad - pSize, -1.5f), rad, sphereMat));

        // player start
        engine.EyePos = new(0.0f, 0.0f, -2.5f * pSize);
    }

    static void MakePlane(float height, float scale = 100.0f)
    {
        TriNormsCol[] planeVerts = new TriNormsCol[2]
        {
            new(new Vector3[] { new(-1.0f, 0.0f, -1.0f), new(-1.0f, 0.0f, 1.0f),
                new( 1.0f, 0.0f,  1.0f) }, Vector3.UnitY),
            new(new Vector3[] { new(-1.0f, 0.0f, -1.0f), new( 1.0f, 0.0f, 1.0f),
                new( 1.0f, 0.0f, -1.0f) }, Vector3.UnitY)
        };
        Material planeMat = new(Colors.White, 1.0f, Colors.White, 0.0f, 0.0f);
        engine.gameobjects.Add(new("Plane", new(0.0f, height, 0.0f), new(), new(scale), planeVerts, planeMat));
    }

    static void Start()
    {
        //engine.ToggleFullscreen();
    }

    static void Update()
    {
    }

    static void UserInput()
    {
        if (engine.input.KeyDown(Key.Escape))
            engine.Stop();
        if (engine.input.KeyDown(Key.F11))
            engine.ToggleFullscreen();

        if (engine.input.KeyDown(Key.Back))
        {
            engine.EyePos = engine.EyeStartPos;
            //engine.EyeRot = engine.EyeStartRot;
        }

        float speed = MoveSpeed;

        if (engine.input.KeyHeld(Key.CapsLock))
            speed *= 20.0f;
        else if (engine.input.KeyHeld(Key.LeftShift))
            speed *= 5.0f;
        else if (engine.input.KeyHeld(Key.LeftControl))
            speed /= 10.0f;
        POINT pos = engine.input.GetDeltaMousePos();
        engine.EyeRot.Y += pos.X * Sensitivity;
        engine.EyeRot.X += pos.Y * Sensitivity;
        engine.EyeRot.X = Math.Max(Math.Min(engine.EyeRot.X, 90.0f), -90.0f);
        Matrix4x4 rot = Engine.CreateRotation(engine.EyeRot);
        float normalizer = Math.Max((float)Math.Sqrt((engine.input.KeyHeld(Key.A) ^ engine.input.KeyHeld(Key.D) ? 1 : 0) + (engine.input.KeyHeld(Key.W) ^ engine.input.KeyHeld(Key.S) ? 1 : 0) + (engine.input.KeyHeld(Key.E) ^ engine.input.KeyHeld(Key.Q) ? 1 : 0)), 1.0f);
        Vector3 forward = Vector3.TransformNormal(Vector3.UnitZ, rot) / normalizer;
        Vector3 right = Vector3.TransformNormal(Vector3.UnitX, rot) / normalizer;
        Vector3 up = Vector3.TransformNormal(Vector3.UnitY, rot) / normalizer;
        if (engine.input.KeyHeld(Key.A))
            engine.EyePos -= right * (float)engine.input.ElapsedTime * speed;
        if (engine.input.KeyHeld(Key.D))
            engine.EyePos += right * (float)engine.input.ElapsedTime * speed;
        if (engine.input.KeyHeld(Key.W))
            engine.EyePos += forward * (float)engine.input.ElapsedTime * speed;
        if (engine.input.KeyHeld(Key.S))
            engine.EyePos -= forward * (float)engine.input.ElapsedTime * speed;
        if (engine.input.KeyHeld(Key.Q))
            engine.EyePos -= up * (float)engine.input.ElapsedTime * speed;
        if (engine.input.KeyHeld(Key.E))
            engine.EyePos += up * (float)engine.input.ElapsedTime * speed;

        if (engine is RayTracingEngine)
        {
            if (engine.input.KeyHeld(Key.F))
                ((RayTracingEngine)engine).spheres[0].Position.X -= speed * (float)engine.input.ElapsedTime;
            if (engine.input.KeyHeld(Key.H))
                ((RayTracingEngine)engine).spheres[0].Position.X += speed * (float)engine.input.ElapsedTime;
            if (engine.input.KeyHeld(Key.T))
                ((RayTracingEngine)engine).spheres[0].Position.Z += speed * (float)engine.input.ElapsedTime;
            if (engine.input.KeyHeld(Key.G))
                ((RayTracingEngine)engine).spheres[0].Position.Z -= speed * (float)engine.input.ElapsedTime;
            if (engine.input.KeyHeld(Key.R))
                ((RayTracingEngine)engine).spheres[0].Position.Y -= speed * (float)engine.input.ElapsedTime;
            if (engine.input.KeyHeld(Key.Y))
                ((RayTracingEngine)engine).spheres[0].Position.Y += speed * (float)engine.input.ElapsedTime;
        }
        else
        {
            if (engine.input.KeyHeld(Key.F))
                engine.gameobjects[0].Position.X -= speed * (float)engine.input.ElapsedTime;
            if (engine.input.KeyHeld(Key.H))
                engine.gameobjects[0].Position.X += speed * (float)engine.input.ElapsedTime;
            if (engine.input.KeyHeld(Key.T))
                engine.gameobjects[0].Position.Z += speed * (float)engine.input.ElapsedTime;
            if (engine.input.KeyHeld(Key.G))
                engine.gameobjects[0].Position.Z -= speed * (float)engine.input.ElapsedTime;
            if (engine.input.KeyHeld(Key.R))
                engine.gameobjects[0].Position.Y -= speed * (float)engine.input.ElapsedTime;
            if (engine.input.KeyHeld(Key.Y))
                engine.gameobjects[0].Position.Y += speed * (float)engine.input.ElapsedTime;
        }

        if (engine.input.KeyHeld(Key.J))
            engine.lights[0].Position.X -= speed * (float)engine.input.ElapsedTime;
        if (engine.input.KeyHeld(Key.L))
            engine.lights[0].Position.X += speed * (float)engine.input.ElapsedTime;
        if (engine.input.KeyHeld(Key.I))
            engine.lights[0].Position.Z += speed * (float)engine.input.ElapsedTime;
        if (engine.input.KeyHeld(Key.K))
            engine.lights[0].Position.Z -= speed * (float)engine.input.ElapsedTime;
        if (engine.input.KeyHeld(Key.U))
            engine.lights[0].Position.Y -= speed * (float)engine.input.ElapsedTime;
        if (engine.input.KeyHeld(Key.O))
            engine.lights[0].Position.Y += speed * (float)engine.input.ElapsedTime;

        // Toggle lines
        if (engine.input.KeyDown(Key.Return))
            engine.Line = !engine.Line;

        //if (engine.input.KeyHeld(Key.N))
        //    engine.DepthBias = Math.Max(0.0f, engine.DepthBias - speed * (float)engine.input.ElapsedTime);
        //if (engine.input.KeyHeld(Key.M))
        //    engine.DepthBias = Math.Min(4.0f, engine.DepthBias + speed * (float)engine.input.ElapsedTime);
        //if (engine.input.KeyHeld(Key.Comma))
        //    engine.NormalBias = Math.Max(0.0f, engine.NormalBias - speed * (float)engine.input.ElapsedTime);
        //if (engine.input.KeyHeld(Key.Period))
        //    engine.NormalBias = Math.Min(0.0f, engine.NormalBias + speed * (float)engine.input.ElapsedTime);

        // Depth bias adjustment
        double mult = 0.0;
        if (engine.input.KeyDown(Key.Down))
            mult = -1.0;
        if (engine.input.KeyDown(Key.Up))
            mult = 1.0;
        if (mult != 0.0)
        {
            if (engine.input.KeyHeld(Key.CapsLock))
            {
                mult *= 1.0;
            }
            else if (engine.input.KeyHeld(Key.LeftShift))
            {
                mult *= 0.1;
            }
            else if (engine.input.KeyHeld(Key.LeftControl))
            {
                mult *= 0.001;
            }
            else if (engine.input.KeyHeld(Key.Space))
            {
                mult *= 0.0001;
            }
            else
            {
                mult *= 0.01;
            }

            engine.DepthBias = (float)Math.Round(engine.DepthBias + mult, 4, MidpointRounding.ToEven);
        }

        if (engine.input.KeyDown(Key.Backslash))
            engine.DepthBias = 0.0f;

        // Normal incrementer

        int m = 0;
        if (engine.input.KeyDown(Key.Left))
            m = -1;
        if (engine.input.KeyDown(Key.Right))
            m = 1;

        // mark the angle on the plane
        //if (m != 0)
        //{
        //    if (engine.input.KeyHeld(Key.LeftShift))
        //    {
        //        m *= 10;
        //    }

        //    engine.NormalBias += m;
        //}

        // rotate the plane
        //if (m != 0)
        //{
        //    if (engine.input.KeyHeld(Key.LeftShift))
        //    {
        //        m *= 10;
        //    }

        //    engine.gameobjects[0].Rotation.Z -= m;
        //}

        // change the light position
        //if (m != 0)
        //{
        //    if (m == 1)
        //    {
        //        engine.lights[0].Position.Y = Math.Min(2.0f * engine.lights[0].Position.Y, 64.0f);
        //    }
        //    else
        //    {
        //        engine.lights[0].Position.Y = Math.Max(0.5f * engine.lights[0].Position.Y, 1.0f);
        //    }
        //}

        // change the light angle
        if (m != 0)
        {
            if (engine.input.KeyHeld(Key.LeftShift))
            {
                m *= 10;
            }

            phi = Math.Min(90, Math.Max(0, phi + m));

            engine.lights[0].Position = new(-(float)(distance * Math.Sin(phi * Math.PI / 180.0)), (float)(distance * Math.Cos(phi * Math.PI / 180.0)), 0.0f);
            engine.gameobjects[0].Rotation.Z = -(phi + theta);
        }

        // print if buttons pressed
        if (m != 0 || mult != 0.0 || engine.input.KeyDown(Key.Backslash))
        {
            //Engine.print("depth=" + engine.DepthBias + " normal=" + engine.NormalBias);
            //Engine.print("depth=" + engine.DepthBias + " normal=" + -engine.gameobjects[0].Rotation.Z);
            //Engine.print("depth=" + engine.DepthBias + " light=" + engine.lights[0].Position.Y);
            Engine.print("depth=" + engine.DepthBias + " phi=" + phi + " theta=" + theta + " light=" + engine.lights[0].Position + " plane=" + engine.gameobjects[0].Rotation.Z);
        }

        //if (engine.input.KeyHeld(Key.Down))
        //    engine.gameobjects[0].rotation.X += 20.0f * (float)engine.input.elapsedTime;
        //if (engine.input.KeyHeld(Key.Up))
        //    engine.gameobjects[0].rotation.X -= 20.0f * (float)engine.input.elapsedTime;
        //if (engine.input.KeyHeld(Key.Right))
        //    engine.gameobjects[0].rotation.Y += 20.0f * (float)engine.input.elapsedTime;
        //if (engine.input.KeyHeld(Key.Left))
        //    engine.gameobjects[0].rotation.Y -= 20.0f * (float)engine.input.elapsedTime;
        //if (engine.input.KeyHeld(Key.Comma))
        //    engine.gameobjects[0].rotation.Z -= 20.0f * (float)engine.input.elapsedTime;
        //if (engine.input.KeyHeld(Key.Period))
        //    engine.gameobjects[0].rotation.Z += 20.0f * (float)engine.input.elapsedTime;
    }
}
