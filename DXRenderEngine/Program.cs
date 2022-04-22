using DXRenderEngine;
using System;
using System.Numerics;
using System.Windows.Forms;
using Vortice.DirectInput;

sealed class Program
{
    static Engine engine;
    static float MoveSpeed = 4.0f;
    static float Sensitivity = 0.084f;

    [MTAThread]
    static void Main()
    {
        EngineDescription ED = new EngineDescription(ProjectionDescription.Default, 1280, 720, 0,
            RayOnAwake, OnStart, OnUpdate, UserInput, FormWindowState.Normal);

        //RasterizingEngineDescription SD = new RasterizingEngineDescription(ED, false, true, false);
        RayTracingEngineDescription SD = new RayTracingEngineDescription(ED, 2);
        engine = Engine.Create(SD);
        engine.Run();
    }

    static void OnAwake()
    {
        engine.lights.Add(new Light(new Vector3(0.0f, 5.0f, -10.0f), new Vector3(0.0f, 0.0f, 1.0f), 0.2f, 250.0f));
        engine.lights.Add(new Light(new Vector3(-15.0f, 5.0f, 20.0f), new Vector3(1.0f, 0.0f, 0.0f), 0.2f, 250.0f));
        engine.lights.Add(new Light(new Vector3(15.0f, 5.0f, 20.0f), new Vector3(0.0f, 1.0f, 0.0f), 0.2f, 250.0f));

        engine.gameobjects.AddRange(Gameobject.GetObjects("DXRenderEngine.Objects.Objects.obj"));
        engine.gameobjects[0].ChangeObjectCharacteristics(new Vector3(1.0f, 0.0f, 0.0f), 0.2f);
        engine.gameobjects[1].ChangeObjectCharacteristics(new Vector3(0.0f, 0.0f, 1.0f), 0.3f);
        engine.gameobjects[2].ChangeObjectCharacteristics(new Vector3(0.0f, 1.0f, 0.0f), 0.4f);
        engine.gameobjects[3].ChangeObjectCharacteristics(new Vector3(1.0f, 0.0f, 1.0f), 0.5f);
        engine.gameobjects[4].ChangeObjectCharacteristics(new Vector3(1.0f, 1.0f, 0.0f), 0.8f);
        engine.gameobjects[1].ChangeGameObjectSmoothness(true);
        engine.gameobjects[2].ChangeGameObjectSmoothness(true);
        engine.gameobjects[3].ChangeGameObjectSmoothness(true);
        engine.gameobjects[4].ChangeGameObjectSmoothness(true);
        TriNormsCol[] planeVerts = new TriNormsCol[2]
        {
            new TriNormsCol(new Vector3[] { new Vector3(-100.0f, 0.0f, -100.0f), new Vector3(-100.0f, 0.0f, 100.0f), 
                new Vector3( 100.0f, 0.0f,  100.0f) }, new Vector3(0.0f, 1.0f, 0.0f), new Vector3(1.0f, 1.0f, 1.0f), 0.0f),
            new TriNormsCol(new Vector3[] { new Vector3(-100.0f, 0.0f, -100.0f), new Vector3( 100.0f, 0.0f, 100.0f), 
                new Vector3( 100.0f, 0.0f, -100.0f) }, new Vector3(0.0f, 1.0f, 0.0f), new Vector3(1.0f, 1.0f, 1.0f), 0.0f)
        };
        engine.gameobjects.Add(new Gameobject("Plane", new Vector3(0.0f, -1.0f, 0.0f), new Vector3(), new Vector3(1.0f), planeVerts));
    }

    static void RayOnAwake()
    {
        ((RayTracingEngine)engine).spheres.Add(new Sphere(new Vector3(1.5f, 0.0f, 3.0f), 1.0f, new Vector3(1.0f, 1.0f, 1.0f), 1.5f, 0.1f));
        ((RayTracingEngine)engine).spheres.Add(new Sphere(new Vector3(-1.5f, 0.0f, 3.0f), 1.0f, new Vector3(1.0f, 1.0f, 1.0f), 1.5f, 0.1f));
        engine.lights.Add(new Light(new Vector3(0.0f, 5.0f, -10.0f), new Vector3(1.0f, 1.0f, 1.0f), 0.2f, 1.0f));

        //engine.gameobjects.AddRange(Gameobject.GetObjects("DXRenderEngine.Objects.Objects.obj"));
        //engine.gameobjects[0].ChangeObjectCharacteristics(new Vector3(1.0f, 0.0f, 0.0f), 0.2f);
        //engine.gameobjects[1].ChangeObjectCharacteristics(new Vector3(0.0f, 0.0f, 1.0f), 0.3f);
        //engine.gameobjects[2].ChangeObjectCharacteristics(new Vector3(0.0f, 1.0f, 0.0f), 0.4f);
        //engine.gameobjects[3].ChangeObjectCharacteristics(new Vector3(1.0f, 0.0f, 1.0f), 0.5f);
        //engine.gameobjects[4].ChangeObjectCharacteristics(new Vector3(1.0f, 1.0f, 0.0f), 0.8f);
        //engine.gameobjects[1].ChangeGameObjectSmoothness(true);
        //engine.gameobjects[2].ChangeGameObjectSmoothness(true);
        //engine.gameobjects[3].ChangeGameObjectSmoothness(true);
        //engine.gameobjects[4].ChangeGameObjectSmoothness(true);
        TriNormsCol[] planeVerts = new TriNormsCol[2]
        {
            new TriNormsCol(new Vector3[] { new Vector3(-100.0f, 0.0f, -100.0f), new Vector3(-100.0f, 0.0f, 100.0f),
                new Vector3( 100.0f, 0.0f,  100.0f) }, new Vector3(0.0f, 1.0f, 0.0f), new Vector3(1.0f, 1.0f, 1.0f), 0.05f),
            new TriNormsCol(new Vector3[] { new Vector3(-100.0f, 0.0f, -100.0f), new Vector3( 100.0f, 0.0f, 100.0f),
                new Vector3( 100.0f, 0.0f, -100.0f) }, new Vector3(0.0f, 1.0f, 0.0f), new Vector3(1.0f, 1.0f, 1.0f), 0.05f)
        };
        engine.gameobjects.Add(new Gameobject("Plane", new Vector3(0.0f, -1.0f, 0.0f), new Vector3(), new Vector3(1.0f), planeVerts));
        //engine.spheres.Add(new Sphere(new Vector3(0.0f, 0.0f, 3.0f), 1.0f, new Vector4(0.0f, 0.0f, 0.0f, 0.3f), new Vector4(1.5f, 0.0f, 0.0f, 0.0f)));
        //engine.spheres.Add(new Sphere(new Vector3(0.0f, 0.0f, -1000.0f), 1.0f, new Vector4(0.0f, 0.0f, 0.0f, 1.0f), new Vector4(1.5f, 0.0f, 0.0f, 0.0f)));
        //engine.EyePos = new Vector3(-20.0f, 10.0f, 5.0f);
        //engine.EyeRot = new Vector2(30.0f, 90.0f);
    } 

    static void OnStart()
    {
        foreach (var g in engine.gameobjects)
        {
            Engine.print(g.Position);
        }
        Engine.print(engine.EyePos + " " + engine.EyeRot);
        //engine.ToggleFullscreen();
    }

    static void OnUpdate()
    {
    }

    static void UserInput()
    {
        if (engine.input.KeyDown(Key.Escape))
            engine.Stop();
        if (engine.input.KeyDown(Key.F11))
            engine.ToggleFullscreen();

        if (engine.input.KeyHeld(Key.LeftShift))
            MoveSpeed *= 2.0f;
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
            engine.EyePos -= right * (float)engine.input.ElapsedTime * MoveSpeed;
        if (engine.input.KeyHeld(Key.D))
            engine.EyePos += right * (float)engine.input.ElapsedTime * MoveSpeed;
        if (engine.input.KeyHeld(Key.W))
            engine.EyePos += forward * (float)engine.input.ElapsedTime * MoveSpeed;
        if (engine.input.KeyHeld(Key.S))
            engine.EyePos -= forward * (float)engine.input.ElapsedTime * MoveSpeed;
        if (engine.input.KeyHeld(Key.Q))
            engine.EyePos -= up * (float)engine.input.ElapsedTime * MoveSpeed;
        if (engine.input.KeyHeld(Key.E))
            engine.EyePos += up * (float)engine.input.ElapsedTime * MoveSpeed;
        if (engine.input.KeyHeld(Key.LeftShift))
            MoveSpeed /= 2.0f;

        if (engine is RayTracingEngine)
        {
            if (engine.input.KeyHeld(Key.F))
                ((RayTracingEngine)engine).spheres[0].Position.X -= MoveSpeed * (float)engine.input.ElapsedTime;
            if (engine.input.KeyHeld(Key.H))
                ((RayTracingEngine)engine).spheres[0].Position.X += MoveSpeed * (float)engine.input.ElapsedTime;
            if (engine.input.KeyHeld(Key.T))
                ((RayTracingEngine)engine).spheres[0].Position.Z += MoveSpeed * (float)engine.input.ElapsedTime;
            if (engine.input.KeyHeld(Key.G))
                ((RayTracingEngine)engine).spheres[0].Position.Z -= MoveSpeed * (float)engine.input.ElapsedTime;
            if (engine.input.KeyHeld(Key.R))
                ((RayTracingEngine)engine).spheres[0].Position.Y -= MoveSpeed * (float)engine.input.ElapsedTime;
            if (engine.input.KeyHeld(Key.Y))
                ((RayTracingEngine)engine).spheres[0].Position.Y += MoveSpeed * (float)engine.input.ElapsedTime;
        }

        //if (engine.input.KeyHeld(Key.F))
        //    engine.gameobjects[1].Position.X -= MoveSpeed * (float)engine.input.ElapsedTime;
        //if (engine.input.KeyHeld(Key.H))
        //    engine.gameobjects[1].Position.X += MoveSpeed * (float)engine.input.ElapsedTime;
        //if (engine.input.KeyHeld(Key.T))
        //    engine.gameobjects[1].Position.Z += MoveSpeed * (float)engine.input.ElapsedTime;
        //if (engine.input.KeyHeld(Key.G))
        //    engine.gameobjects[1].Position.Z -= MoveSpeed * (float)engine.input.ElapsedTime;
        //if (engine.input.KeyHeld(Key.R))
        //    engine.gameobjects[1].Position.Y -= MoveSpeed * (float)engine.input.ElapsedTime;
        //if (engine.input.KeyHeld(Key.Y))
        //    engine.gameobjects[1].Position.Y += MoveSpeed * (float)engine.input.ElapsedTime;

        //if (engine.input.KeyHeld(Key.J))
        //    engine.lights[0].Position.X -= MoveSpeed * (float)engine.input.ElapsedTime;
        //if (engine.input.KeyHeld(Key.L))
        //    engine.lights[0].Position.X += MoveSpeed * (float)engine.input.ElapsedTime;
        //if (engine.input.KeyHeld(Key.I))
        //    engine.lights[0].Position.Z += MoveSpeed * (float)engine.input.ElapsedTime;
        //if (engine.input.KeyHeld(Key.K))
        //    engine.lights[0].Position.Z -= MoveSpeed * (float)engine.input.ElapsedTime;
        //if (engine.input.KeyHeld(Key.U))
        //    engine.lights[0].Position.Y -= MoveSpeed * (float)engine.input.ElapsedTime;
        //if (engine.input.KeyHeld(Key.O))
        //    engine.lights[0].Position.Y += MoveSpeed * (float)engine.input.ElapsedTime;

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
