using System.Numerics;

namespace DXTests;

[TestClass]
public class EngineMatrixTests
{
    readonly Matrix4x4 Proj = new ProjectionDescription(90.0f, 1.0f, 0.01f, 1000.0f).GetMatrix();

    public static bool Diff(float expected, float actual)
    {
        return Math.Abs(expected - actual) < 1e-6f;
    }

    [TestMethod]
    public void World()
    {
        Vector4 Actual = new Vector4(-1.0f, 1.0f, 2.0f, 1.0f);
        Matrix4x4 mat = Engine.CreateWorld(new Vector3(1.0f, 2.0f, 1.0f), new Vector3(-90.0f, -90.0f, 0.0f), new Vector3(2.0f));
        Actual = Vector4.Transform(Actual, mat);
        // Start:       (-1.0f,  1.0f,  2.0f)
        // After Scale: (-2.0f,  2.0f,  4.0f)
        // After RotX:  (-2.0f,  4.0f, -2.0f)
        // After RotY:  ( 2.0f,  4.0f, -2.0f)
        // After Trans: ( 3.0f,  6.0f, -1.0f)
        Vector4 Expected = new Vector4(3.0f, 6.0f, -1.0f, 1.0f);
        float Difference = (Expected - Actual).LengthSquared();
        Assert.IsTrue(Diff(Difference, 0.0f), "Expected:" + Expected + " Actual:" + Actual);
    }

    [TestMethod]
    public void View_NoMovement_NoRotation()
    {
        Vector3 Position = new Vector3();
        Vector3 Rotation = new Vector3();
        Vector4 Actual = new Vector4(-1.0f, 1.0f, 2.0f, 1.0f);
        Vector4 Expected = Actual;
        Matrix4x4 mat = Engine.CreateView(Position, Rotation);
        Actual = Vector4.Transform(Actual, mat);
        float Difference = (Expected - Actual).LengthSquared();
        Assert.IsTrue(Diff(Difference, 0.0f), "Expected:" + Expected + " Actual:" + Actual);
    }

    [TestMethod]
    public void View_NoMovement_90Left()
    {
        Vector3 Position = new Vector3();
        Vector3 Rotation = new Vector3(0.0f, -90.0f, 0.0f);
        Vector4 Actual = new Vector4(-1.0f, 1.0f, 2.0f, 1.0f);
        Vector4 Expected = new Vector4(2.0f, 1.0f, 1.0f, 1.0f);
        Matrix4x4 mat = Engine.CreateView(Position, Rotation);
        Actual = Vector4.Transform(Actual, mat);
        float Difference = (Expected - Actual).LengthSquared();
        Assert.IsTrue(Diff(Difference, 0.0f), "Expected:" + Expected + " Actual:" + Actual);
    }

    [TestMethod]
    public void View_NoMovement_90Right()
    {
        Vector3 Position = new Vector3();
        Vector3 Rotation = new Vector3(0.0f, 90.0f, 0.0f);
        Vector4 Actual = new Vector4(-1.0f, 1.0f, 2.0f, 1.0f);
        Vector4 Expected = new Vector4(-2.0f, 1.0f, -1.0f, 1.0f);
        Matrix4x4 mat = Engine.CreateView(Position, Rotation);
        Actual = Vector4.Transform(Actual, mat);
        float Difference = (Expected - Actual).LengthSquared();
        Assert.IsTrue(Diff(Difference, 0.0f), "Expected:" + Expected + " Actual:" + Actual);
    }

    [TestMethod]
    public void View_NoMovement_90Up()
    {
        Vector3 Position = new Vector3();
        Vector3 Rotation = new Vector3(-90.0f, 0.0f, 0.0f);
        Vector4 Actual = new Vector4(-1.0f, 1.0f, 2.0f, 1.0f);
        Vector4 Expected = new Vector4(-1.0f, -2.0f, 1.0f, 1.0f);
        Matrix4x4 mat = Engine.CreateView(Position, Rotation);
        Actual = Vector4.Transform(Actual, mat);
        float Difference = (Expected - Actual).LengthSquared();
        Assert.IsTrue(Diff(Difference, 0.0f), "Expected:" + Expected + " Actual:" + Actual);
    }

    [TestMethod]
    public void View_NoMovement_90Down()
    {
        Vector3 Position = new Vector3();
        Vector3 Rotation = new Vector3(90.0f, 0.0f, 0.0f);
        Vector4 Actual = new Vector4(-1.0f, 1.0f, 2.0f, 1.0f);
        Vector4 Expected = new Vector4(-1.0f, 2.0f, -1.0f, 1.0f);
        Matrix4x4 mat = Engine.CreateView(Position, Rotation);
        Actual = Vector4.Transform(Actual, mat);
        float Difference = (Expected - Actual).LengthSquared();
        Assert.IsTrue(Diff(Difference, 0.0f), "Expected:" + Expected + " Actual:" + Actual);
    }

    [TestMethod]
    public void View_Forwards10_90Right()
    {
        Vector3 Position = new Vector3(0.0f, 0.0f, 10.0f);
        Vector3 Rotation = new Vector3(0.0f, 90.0f, 0.0f);
        Vector4 Actual = new Vector4(-1.0f, 1.0f, 2.0f, 1.0f);
        Vector4 Expected = new Vector4(8.0f, 1.0f, -1.0f, 1.0f);
        Matrix4x4 mat = Engine.CreateView(Position, Rotation);
        Actual = Vector4.Transform(Actual, mat);
        float Difference = (Expected - Actual).LengthSquared();
        Assert.IsTrue(Diff(Difference, 0.0f), "Expected:" + Expected + " Actual:" + Actual);
    }

    [TestMethod]
    public void View_Forwards10_Rotated()
    {
        Vector3 Position = new Vector3(0.0f, 0.0f, 10.0f);
        Vector3 Rotation = new Vector3(-90.0f, 90.0f, 0.0f);
        Vector4 Actual = new Vector4(-1.0f, 1.0f, 2.0f, 1.0f);
        Vector4 Expected = new Vector4(8.0f, 1.0f, 1.0f, 1.0f);
        Matrix4x4 mat = Engine.CreateView(Position, Rotation);
        Actual = Vector4.Transform(Actual, mat);
        float Difference = (Expected - Actual).LengthSquared();
        Assert.IsTrue(Diff(Difference, 0.0f), "Expected:" + Expected + " Actual:" + Actual);
    }

    [TestMethod]
    public void View_Displaced_Rotated()
    {
        Vector3 Position = new Vector3(3.0f, -5.0f, 10.0f);
        Vector3 Rotation = new Vector3(-90.0f, 90.0f, 0.0f);
        Vector4 Actual = new Vector4(-1.0f, 1.0f, 2.0f, 1.0f);
        Vector4 Expected = new Vector4(8.0f, 4.0f, 6.0f, 1.0f);
        Matrix4x4 mat = Engine.CreateView(Position, Rotation);
        Actual = Vector4.Transform(Actual, mat);
        float Difference = (Expected - Actual).LengthSquared();
        Assert.IsTrue(Diff(Difference, 0.0f), "Expected:" + Expected + " Actual:" + Actual);
    }

    [TestMethod]
    public void Proj_Near()
    {
        Vector4 Actual = new Vector4(0.0f, 0.0f, 0.01f, 1.0f);
        Vector4 Expected = new Vector4(0.0f, 0.0f, 0.0f, 1.0f);
        Actual = Vector4.Transform(Actual, Proj);
        Actual /= Actual.W;
        float Difference = (Expected - Actual).LengthSquared();
        Assert.IsTrue(Diff(Difference, 0.0f), "Expected:" + Expected + " Actual:" + Actual);
    }

    [TestMethod]
    public void Proj_Far()
    {
        Vector4 Actual = new Vector4(0.0f, 0.0f, 1000.0f, 1.0f);
        Vector4 Expected = new Vector4(0.0f, 0.0f, 1.0f, 1.0f);
        Actual = Vector4.Transform(Actual, Proj);
        Actual /= Actual.W;
        float Difference = (Expected - Actual).LengthSquared();
        Assert.IsTrue(Diff(Difference, 0.0f), "Expected:" + Expected + " Actual:" + Actual);
    }

    [TestMethod]
    public void Proj_SmallDisplacement()
    {
        Vector4 Actual = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
        Vector4 Expected = new Vector4(1.0f, 1.0f, 0.990009900099f, 1.0f);
        Actual = Vector4.Transform(Actual, Proj);
        Actual /= Actual.W;
        float Difference = (Expected - Actual).LengthSquared();
        Assert.IsTrue(Diff(Difference, 0.0f), "Expected:" + Expected + " Actual:" + Actual);
    }
}