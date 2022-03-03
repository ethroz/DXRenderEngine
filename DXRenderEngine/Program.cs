using DXRenderEngine;
using Vortice.Direct3D;
using System;

sealed class Program
{
    static Engine engine;
    static float MoveSpeed = 4.0f;
    static float Sensitivity = 0.042f;

    [MTAThread]
    static void Main()
    {
        EngineDescription ED = new EngineDescription(new ProjectionDescription(), 1280, 720, 0, 1, OnAwake, OnStart, OnUpdate,
            UserInput, Engine.WindowState.Minimized, Engine.RenderType.RasterizedGPU, PrimitiveTopology.TriangleList);
        engine = new Engine(ED);
        engine.Run();
        engine.Dispose();
    }

    static void OnAwake()
    {
        //engine.gameobjects.Add(new Gameobject("triangle", new Vector3(), new Vector3(), new Vector3(1.0f), new TriNormsCol[1] { new TriNormsCol(new Vector3[] { new Vector3(-0.05f, 0.0f, 0.1f), new Vector3(0.0f, 0.15f, 0.6f), new Vector3(0.6f, -0.3f, 1.1f) }, new Vector3(0.0f, 0.0f, 1.0f), new Vector4(1.0f, 1.0f, 1.0f, 0.0f)) }));
        //engine.spheres.Add(new Sphere(new Vector3(0.0f, 0.0f, 3.0f), 1.0f, new Vector4(0.0f, 0.0f, 0.0f, 0.3f), new Vector4(1.5f, 0.0f, 0.0f, 0.0f)));
        //engine.lights.Add(new Light(new Vector3(0.0f, 5.0f, -10.0f), new Vector4(0.0f, 0.0f, 1.0f, 1.0f), 0.2f, 250.0f));
        //return;

        engine.lights.Add(new Light(new Vector3(0.0f, 5.0f, -10.0f), new Vector4(0.0f, 0.0f, 1.0f, 1.0f), 0.2f, 250.0f));
        engine.lights.Add(new Light(new Vector3(-15.0f, 5.0f, 20.0f), new Vector4(1.0f, 0.0f, 0.0f, 1.0f), 0.2f, 250.0f));
        engine.lights.Add(new Light(new Vector3(15.0f, 5.0f, 20.0f), new Vector4(0.0f, 1.0f, 0.0f, 1.0f), 0.2f, 250.0f));
        engine.gameobjects.AddRange(Gameobject.GetObjectsFromFile(engine.path + @"\Objects\Objects.obj"));
        //engine.gameobjects[0].ChangeGameObjectColor(new Vector4(1.0f, 0.0f, 0.0f, 0.3f));
        //engine.gameobjects[1].ChangeGameObjectColor(new Vector4(0.0f, 0.0f, 1.0f, 0.2f));
        //engine.gameobjects[2].ChangeGameObjectColor(new Vector4(0.0f, 1.0f, 0.0f, 0.2f));
        //engine.gameobjects[3].ChangeGameObjectColor(new Vector4(1.0f, 0.0f, 1.0f, 0.3f));
        //engine.gameobjects[4].ChangeGameObjectColor(new Vector4(1.0f, 1.0f, 0.0f, 0.3f));
        engine.gameobjects[0].ChangeGameObjectColor(new Vector4(1.0f, 1.0f, 1.0f, 0.3f));
        engine.gameobjects[1].ChangeGameObjectColor(new Vector4(1.0f, 1.0f, 1.0f, 0.2f));
        engine.gameobjects[2].ChangeGameObjectColor(new Vector4(1.0f, 1.0f, 1.0f, 0.2f));
        engine.gameobjects[3].ChangeGameObjectColor(new Vector4(1.0f, 1.0f, 1.0f, 0.3f));
        engine.gameobjects[4].ChangeGameObjectColor(new Vector4(1.0f, 1.0f, 1.0f, 0.3f));
        engine.gameobjects[1].ChangeGameObjectSmoothness(true);
        engine.gameobjects[2].ChangeGameObjectSmoothness(true);
        engine.gameobjects[3].ChangeGameObjectSmoothness(true);
        engine.gameobjects[4].ChangeGameObjectSmoothness(true);
        TriNormsCol[] planeVerts = new TriNormsCol[2]
        {
                new TriNormsCol(new Vector3[] { new Vector3(-100.0f, 0.0f, -100.0f), new Vector3(-100.0f, 0.0f, 100.0f), new Vector3( 100.0f, 0.0f,  100.0f) },
                new Vector3(0.0f, 1.0f, 0.0f), new Vector4(1.0f, 1.0f, 1.0f, 0.0f)),
                new TriNormsCol(new Vector3[] { new Vector3(-100.0f, 0.0f, -100.0f), new Vector3( 100.0f, 0.0f, 100.0f), new Vector3( 100.0f, 0.0f, -100.0f) },
                new Vector3(0.0f, 1.0f, 0.0f), new Vector4(1.0f, 1.0f, 1.0f, 0.0f))
        };
        engine.gameobjects.Add(new Gameobject("Plane", new Vector3(0.0f, -1.0f, 0.0f), new Vector3(), new Vector3(1.0f), planeVerts));
        engine.spheres.Add(new Sphere(new Vector3(0.0f, 0.0f, 3.0f), 1.0f, new Vector4(0.0f, 0.0f, 0.0f, 0.3f), new Vector4(1.5f, 0.0f, 0.0f, 0.0f)));
        //engine.spheres.Add(new Sphere(new Vector3(0.0f, 0.0f, -1000.0f), 1.0f, new Vector4(0.0f, 0.0f, 0.0f, 1.0f), new Vector4(1.5f, 0.0f, 0.0f, 0.0f)));
        //engine.EyePos = new Vector3(-20.0f, 10.0f, 5.0f);
        //engine.EyeRot = new Vector2(30.0f, 90.0f);
    }

    static void OnStart()
    {
    }

    static void OnUpdate()
    {
        engine.UpdateObjectMatrices();
    }

    static void UserInput()
    {
        if (engine.input.KeyDown(Key.Tab))
            engine.State = (Engine.WindowState)(((int)engine.State + 1) % 4);
        if (engine.input.KeyDown(Key.Escape))
            engine.Stop();

        if (engine.input.KeyDown(Key.Return))
            engine.filter = engine.filter == 0 ? 1 : 0;

        if (engine.input.KeyHeld(Key.LeftShift))
            MoveSpeed *= 2.0f;
        Vector2 pos = engine.input.GetDeltaMousePos();
        engine.EyeRot.Y += pos.X * Sensitivity;
        engine.EyeRot.X += pos.Y * Sensitivity;
        engine.EyeRot.X = Math.Max(Math.Min(engine.EyeRot.X, 90.0f), -90.0f);
        Matrix roty = Matrix.RotationY(engine.EyeRot.Y * Engine.Deg2Rad);
        Matrix rotx = Matrix.RotationX(engine.EyeRot.X * Engine.Deg2Rad);
        Matrix rot = rotx * roty;
        float normalizer = Math.Max((float)Math.Sqrt((engine.input.KeyHeld(Key.A) ^ engine.input.KeyHeld(Key.D) ? 1 : 0) + (engine.input.KeyHeld(Key.W) ^ engine.input.KeyHeld(Key.S) ? 1 : 0) + (engine.input.KeyHeld(Key.E) ^ engine.input.KeyHeld(Key.Q) ? 1 : 0)), 1.0f);
        Vector3 forward = Vector3.TransformNormal(Vector3.ForwardLH, rot) / normalizer;
        Vector3 right = Vector3.TransformNormal(Vector3.Right, rot) / normalizer;
        Vector3 up = Vector3.TransformNormal(Vector3.Up, rot) / normalizer;
        if (engine.input.KeyHeld(Key.A))
            engine.EyePos -= right * (float)engine.input.elapsedTime * MoveSpeed;
        if (engine.input.KeyHeld(Key.D))
            engine.EyePos += right * (float)engine.input.elapsedTime * MoveSpeed;
        if (engine.input.KeyHeld(Key.W))
            engine.EyePos += forward * (float)engine.input.elapsedTime * MoveSpeed;
        if (engine.input.KeyHeld(Key.S))
            engine.EyePos -= forward * (float)engine.input.elapsedTime * MoveSpeed;
        if (engine.input.KeyHeld(Key.Q))
            engine.EyePos -= up * (float)engine.input.elapsedTime * MoveSpeed;
        if (engine.input.KeyHeld(Key.E))
            engine.EyePos += up * (float)engine.input.elapsedTime * MoveSpeed;
        if (engine.input.KeyHeld(Key.LeftShift))
            MoveSpeed /= 2.0f;

        if (engine.input.KeyHeld(Key.F))
            engine.spheres[0].position.X -= MoveSpeed * (float)engine.input.elapsedTime;
        if (engine.input.KeyHeld(Key.H))
            engine.spheres[0].position.X += MoveSpeed * (float)engine.input.elapsedTime;
        if (engine.input.KeyHeld(Key.T))
            engine.spheres[0].position.Z += MoveSpeed * (float)engine.input.elapsedTime;
        if (engine.input.KeyHeld(Key.G))
            engine.spheres[0].position.Z -= MoveSpeed * (float)engine.input.elapsedTime;
        if (engine.input.KeyHeld(Key.R))
            engine.spheres[0].position.Y -= MoveSpeed * (float)engine.input.elapsedTime;
        if (engine.input.KeyHeld(Key.Y))
            engine.spheres[0].position.Y += MoveSpeed * (float)engine.input.elapsedTime;

        if (engine.input.KeyHeld(Key.F))
            engine.gameobjects[1].position.X -= MoveSpeed * (float)engine.input.elapsedTime;
        if (engine.input.KeyHeld(Key.H))
            engine.gameobjects[1].position.X += MoveSpeed * (float)engine.input.elapsedTime;
        if (engine.input.KeyHeld(Key.T))
            engine.gameobjects[1].position.Z += MoveSpeed * (float)engine.input.elapsedTime;
        if (engine.input.KeyHeld(Key.G))
            engine.gameobjects[1].position.Z -= MoveSpeed * (float)engine.input.elapsedTime;
        if (engine.input.KeyHeld(Key.R))
            engine.gameobjects[1].position.Y -= MoveSpeed * (float)engine.input.elapsedTime;
        if (engine.input.KeyHeld(Key.Y))
            engine.gameobjects[1].position.Y += MoveSpeed * (float)engine.input.elapsedTime;

        if (engine.input.KeyHeld(Key.J))
            engine.lights[0].Position.X -= MoveSpeed * (float)engine.input.elapsedTime;
        if (engine.input.KeyHeld(Key.L))
            engine.lights[0].Position.X += MoveSpeed * (float)engine.input.elapsedTime;
        if (engine.input.KeyHeld(Key.I))
            engine.lights[0].Position.Z += MoveSpeed * (float)engine.input.elapsedTime;
        if (engine.input.KeyHeld(Key.K))
            engine.lights[0].Position.Z -= MoveSpeed * (float)engine.input.elapsedTime;
        if (engine.input.KeyHeld(Key.U))
            engine.lights[0].Position.Y -= MoveSpeed * (float)engine.input.elapsedTime;
        if (engine.input.KeyHeld(Key.O))
            engine.lights[0].Position.Y += MoveSpeed * (float)engine.input.elapsedTime;

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

    //static void UserInput()
    //{
    //    if (engine.input.KeyDown(Key.Tab))
    //        engine.IncrementWindowState();
    //    if (engine.input.KeyDown(Key.Escape))
    //        Environment.Exit(0);
    //    if (engine.input.KeyDown(Key.P))
    //        engine.Running = !engine.Running;
    //    if (!engine.Running)
    //        return;

    //    if (engine.input.KeyHeld(Key.LeftShift))
    //        MoveSpeed *= 2.0f;
    //    Vector2 pos = engine.input.GetDeltaMousePos();
    //    engine.EyeRot.Y += pos.X * Sensitivity;
    //    engine.EyeRot.X += pos.Y * Sensitivity;
    //    engine.EyeRot.X = Math.Max(Math.Min(engine.EyeRot.X, 90.0f), -90.0f);
    //    Matrix roty = Matrix.RotationY(engine.EyeRot.Y * Engine.Deg2Rad);
    //    Matrix rotx = Matrix.RotationX(engine.EyeRot.X * Engine.Deg2Rad);
    //    Matrix rot = rotx * roty;
    //    float normalizer = Math.Max((float)Math.Sqrt((engine.input.KeyHeld(Key.A) ^ engine.input.KeyHeld(Key.D) ? 1 : 0) + (engine.input.KeyHeld(Key.W) ^ engine.input.KeyHeld(Key.S) ? 1 : 0) + (engine.input.KeyHeld(Key.E) ^ engine.input.KeyHeld(Key.Q) ? 1 : 0)), 1.0f);
    //    Vector3 forward = Vector3.TransformNormal(Vector3.ForwardLH, rot) / normalizer;
    //    Vector3 right = Vector3.TransformNormal(Vector3.Right, rot) / normalizer;
    //    Vector3 up = Vector3.TransformNormal(Vector3.Up, rot) / normalizer;
    //    if (engine.input.KeyHeld(Key.A))
    //        engine.EyePos -= right * (float)engine.frameTime * MoveSpeed;
    //    if (engine.input.KeyHeld(Key.D))
    //        engine.EyePos += right * (float)engine.frameTime * MoveSpeed;
    //    if (engine.input.KeyHeld(Key.W))
    //        engine.EyePos += forward * (float)engine.frameTime * MoveSpeed;
    //    if (engine.input.KeyHeld(Key.S))
    //        engine.EyePos -= forward * (float)engine.frameTime * MoveSpeed;
    //    if (engine.input.KeyHeld(Key.Q))
    //        engine.EyePos -= up * (float)engine.frameTime * MoveSpeed;
    //    if (engine.input.KeyHeld(Key.E))
    //        engine.EyePos += up * (float)engine.frameTime * MoveSpeed;
    //    if (engine.input.KeyHeld(Key.LeftShift))
    //        MoveSpeed /= 2.0f;

    //    if (engine.input.KeyHeld(Key.F))
    //        engine.spheres[0].position.X -= MoveSpeed * (float)engine.frameTime;
    //    if (engine.input.KeyHeld(Key.H))
    //        engine.spheres[0].position.X += MoveSpeed * (float)engine.frameTime;
    //    if (engine.input.KeyHeld(Key.T))
    //        engine.spheres[0].position.Z += MoveSpeed * (float)engine.frameTime;
    //    if (engine.input.KeyHeld(Key.G))
    //        engine.spheres[0].position.Z -= MoveSpeed * (float)engine.frameTime;
    //    if (engine.input.KeyHeld(Key.R))
    //        engine.spheres[0].position.Y -= MoveSpeed * (float)engine.frameTime;
    //    if (engine.input.KeyHeld(Key.Y))
    //        engine.spheres[0].position.Y += MoveSpeed * (float)engine.frameTime;

    //    if (engine.input.KeyHeld(Key.F))
    //        engine.gameobjects[0].position.X -= MoveSpeed * (float)engine.frameTime;
    //    if (engine.input.KeyHeld(Key.H))
    //        engine.gameobjects[0].position.X += MoveSpeed * (float)engine.frameTime;
    //    if (engine.input.KeyHeld(Key.T))
    //        engine.gameobjects[0].position.Z += MoveSpeed * (float)engine.frameTime;
    //    if (engine.input.KeyHeld(Key.G))
    //        engine.gameobjects[0].position.Z -= MoveSpeed * (float)engine.frameTime;
    //    if (engine.input.KeyHeld(Key.R))
    //        engine.gameobjects[0].position.Y -= MoveSpeed * (float)engine.frameTime;
    //    if (engine.input.KeyHeld(Key.Y))
    //        engine.gameobjects[0].position.Y += MoveSpeed * (float)engine.frameTime;

    //    if (engine.input.KeyHeld(Key.J))
    //        engine.lights[0].position.X -= MoveSpeed * (float)engine.frameTime;
    //    if (engine.input.KeyHeld(Key.L))
    //        engine.lights[0].position.X += MoveSpeed * (float)engine.frameTime;
    //    if (engine.input.KeyHeld(Key.I))
    //        engine.lights[0].position.Z += MoveSpeed * (float)engine.frameTime;
    //    if (engine.input.KeyHeld(Key.K))
    //        engine.lights[0].position.Z -= MoveSpeed * (float)engine.frameTime;
    //    if (engine.input.KeyHeld(Key.U))
    //        engine.lights[0].position.Y -= MoveSpeed * (float)engine.frameTime;
    //    if (engine.input.KeyHeld(Key.O))
    //        engine.lights[0].position.Y += MoveSpeed * (float)engine.frameTime;

    //    //if (engine.input.KeyHeld(Key.Down))
    //    //    engine.gameobjects[0].rotation.X += 20.0f * (float)engine.frameTime;
    //    //if (engine.input.KeyHeld(Key.Up))
    //    //    engine.gameobjects[0].rotation.X -= 20.0f * (float)engine.frameTime;
    //    //if (engine.input.KeyHeld(Key.Right))
    //    //    engine.gameobjects[0].rotation.Y += 20.0f * (float)engine.frameTime;
    //    //if (engine.input.KeyHeld(Key.Left))
    //    //    engine.gameobjects[0].rotation.Y -= 20.0f * (float)engine.frameTime;
    //    //if (engine.input.KeyHeld(Key.Comma))
    //    //    engine.gameobjects[0].rotation.Z -= 20.0f * (float)engine.frameTime;
    //    //if (engine.input.KeyHeld(Key.Period))
    //    //    engine.gameobjects[0].rotation.Z += 20.0f * (float)engine.frameTime;
    //}
}
