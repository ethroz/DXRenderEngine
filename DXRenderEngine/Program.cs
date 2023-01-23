using DXRenderEngine;
using System;
using System.Numerics;
using Vortice.DirectInput;
using Vortice.Mathematics;
using static DXRenderEngine.Helpers;
using static DXRenderEngine.Win32;

sealed class Program
{
    static Engine engine;
    static float MoveSpeed = 4.0f;
    static float Sensitivity = 0.084f;

    [MTAThread]
    static void Main()
    {
        EngineDescription ED = new(ProjectionDescription.Default, "", 1920, 1080, -1, -1, 0,
            FiveGlassObjects, Start, Update, UserInput, cache: false);

        //ED = new RasterizingEngineDescription(ED, shadows: true);
        ED = new RayTracingEngineDescription(ED, 3);

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
        engine.lights.Add(new(new(0.0f, 1.5f, 6.5f), Colors.AntiqueWhite, 0.2f, 1.0f));

        engine.gameobjects.Add(Gameobject.GetObjects("DXRenderEngine.Objects.Objects.obj")[1]);
        engine.gameobjects[0].Material = new(Colors.DeepSkyBlue, 0.5f, Colors.White, 0.4f, 0.0f);
        engine.gameobjects[0].ChangeGameObjectSmoothness(true);
        engine.gameobjects[0].Position.Z = 6.5f;

        MakePlane(-1.0f);
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
        Material planeMat = Material.Default;
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
        Material planeMat = Material.Default;
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
        engine.gameobjects.Add(new("Plane", new(0.0f, height, 0.0f), new(), new(scale), planeVerts, Material.Default));
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
        float normalizer = Math.Max((float)Math.Sqrt((engine.input.KeyHeld(Key.A) ^ engine.input.KeyHeld(Key.D) ? 1 : 0) + 
            (engine.input.KeyHeld(Key.W) ^ engine.input.KeyHeld(Key.S) ? 1 : 0) + (engine.input.KeyHeld(Key.E) ^ engine.input.KeyHeld(Key.Q) ? 1 : 0)), 1.0f);
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

        if (engine is RayTracingEngine)
        {
            if (engine.input.KeyHeld(Key.F))
                ((RayTracingEngine)engine).spheres[0].Position.X -= speed * (float)engine.ElapsedTime;
            if (engine.input.KeyHeld(Key.H))
                ((RayTracingEngine)engine).spheres[0].Position.X += speed * (float)engine.ElapsedTime;
            if (engine.input.KeyHeld(Key.T))
                ((RayTracingEngine)engine).spheres[0].Position.Z += speed * (float)engine.ElapsedTime;
            if (engine.input.KeyHeld(Key.G))
                ((RayTracingEngine)engine).spheres[0].Position.Z -= speed * (float)engine.ElapsedTime;
            if (engine.input.KeyHeld(Key.R))
                ((RayTracingEngine)engine).spheres[0].Position.Y -= speed * (float)engine.ElapsedTime;
            if (engine.input.KeyHeld(Key.Y))
                ((RayTracingEngine)engine).spheres[0].Position.Y += speed * (float)engine.ElapsedTime;
        }
        else
        {
            if (engine.input.KeyHeld(Key.F))
                engine.gameobjects[0].Position.X -= speed * (float)engine.ElapsedTime;
            if (engine.input.KeyHeld(Key.H))
                engine.gameobjects[0].Position.X += speed * (float)engine.ElapsedTime;
            if (engine.input.KeyHeld(Key.T))
                engine.gameobjects[0].Position.Z += speed * (float)engine.ElapsedTime;
            if (engine.input.KeyHeld(Key.G))
                engine.gameobjects[0].Position.Z -= speed * (float)engine.ElapsedTime;
            if (engine.input.KeyHeld(Key.R))
                engine.gameobjects[0].Position.Y -= speed * (float)engine.ElapsedTime;
            if (engine.input.KeyHeld(Key.Y))
                engine.gameobjects[0].Position.Y += speed * (float)engine.ElapsedTime;
        }

        if (engine.input.KeyHeld(Key.J))
            engine.lights[0].Position.X -= speed * (float)engine.ElapsedTime;
        if (engine.input.KeyHeld(Key.L))
            engine.lights[0].Position.X += speed * (float)engine.ElapsedTime;
        if (engine.input.KeyHeld(Key.I))
            engine.lights[0].Position.Z += speed * (float)engine.ElapsedTime;
        if (engine.input.KeyHeld(Key.K))
            engine.lights[0].Position.Z -= speed * (float)engine.ElapsedTime;
        if (engine.input.KeyHeld(Key.U))
            engine.lights[0].Position.Y -= speed * (float)engine.ElapsedTime;
        if (engine.input.KeyHeld(Key.O))
            engine.lights[0].Position.Y += speed * (float)engine.ElapsedTime;

        if (engine.input.KeyHeld(Key.NumberPad8))
            engine.gameobjects[0].Rotation.X += speed * (float)engine.ElapsedTime * 20.0f;
        if (engine.input.KeyHeld(Key.NumberPad5))
            engine.gameobjects[0].Rotation.X -= speed * (float)engine.ElapsedTime * 20.0f;
        if (engine.input.KeyHeld(Key.NumberPad6))
            engine.gameobjects[0].Rotation.Y += speed * (float)engine.ElapsedTime * 20.0f;
        if (engine.input.KeyHeld(Key.NumberPad4))
            engine.gameobjects[0].Rotation.Y -= speed * (float)engine.ElapsedTime * 20.0f;
        if (engine.input.KeyHeld(Key.NumberPad9))
            engine.gameobjects[0].Rotation.Z += speed * (float)engine.ElapsedTime * 20.0f;
        if (engine.input.KeyHeld(Key.NumberPad7))
            engine.gameobjects[0].Rotation.Z -= speed * (float)engine.ElapsedTime * 20.0f;
    }
}
