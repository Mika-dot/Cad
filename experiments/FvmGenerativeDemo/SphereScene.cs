using System.Text.Json;
using System.Globalization;

namespace FvmGenerativeDemo;

// User-editable two-sphere engineering scene: coordinates, radius, load, material,
// limits. All coordinates mm; forces N; E/stresses MPa.
internal readonly record struct Point3(double X,double Y,double Z)
{
    public static Point3 operator +(Point3 a,Point3 b)=>new(a.X+b.X,a.Y+b.Y,a.Z+b.Z);
    public static Point3 operator -(Point3 a,Point3 b)=>new(a.X-b.X,a.Y-b.Y,a.Z-b.Z);
    public static Point3 operator *(Point3 a,double b)=>new(a.X*b,a.Y*b,a.Z*b);
    internal static double Dot(Point3 a,Point3 b)=>a.X*b.X+a.Y*b.Y+a.Z*b.Z;
    internal static Point3 Cross(Point3 a,Point3 b)=>
        new(a.Y*b.Z-a.Z*b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X);
    internal double Norm()=>Math.Sqrt(Dot(this,this));
    internal Point3 Unit()=>this*(1/Norm());
}
internal sealed class SphereScene
{
    internal Point3 A { get; }
    internal Point3 B { get; }
    internal double RadiusA { get; }
    internal double RadiusB { get; }
    internal Point3 Force { get; }
    internal double Young { get; }
    internal double Poisson { get; }
    internal double StressLimit { get; }
    internal double DisplacementLimit { get; }
    internal int Iterations { get; }
    internal double Length { get; }
    internal Point3 Ex { get; }
    internal Point3 Ey { get; }
    internal Point3 Ez { get; }
    internal double MinRadius => Math.Min(RadiusA,RadiusB);
    internal double MaxRadius => Math.Max(RadiusA,RadiusB);
    internal bool StandardGeometry => A==new Point3(0,0,0) && B==new Point3(80,0,0)
        && RadiusA==10 && RadiusB==10;

    internal static SphereScene Default => new(new Point3(0,0,0),new Point3(80,0,0),
        10,10,new Point3(1200,180,0),200000,.30,200,.08,10);

    internal SphereScene(Point3 a,Point3 b,double radiusA,double radiusB,
        Point3 force,double young,double poisson,double stressLimit,
        double displacementLimit,int iterations)
    {
        A=a;B=b;RadiusA=radiusA;RadiusB=radiusB;Force=force;
        Young=young;Poisson=poisson;StressLimit=stressLimit;
        DisplacementLimit=displacementLimit;Iterations=iterations;
        Length=(b-a).Norm();
        if(radiusA<=0||radiusB<=0||Length<3*Math.Max(radiusA,radiusB) ||
            young<=0||poisson<=-1||poisson>=.5||stressLimit<=0||
            displacementLimit<=0||iterations<1||iterations>50)
            throw new ArgumentException("Sphere geometry/material/limits are not valid.");
        foreach(var v in new[]{a.X,a.Y,a.Z,b.X,b.Y,b.Z,
            radiusA,radiusB,force.X,force.Y,force.Z,young,poisson,
            stressLimit,displacementLimit,Length})
            if(!double.IsFinite(v))throw new ArgumentException("Non-finite scene parameter.");
        if(force.Norm()<1e-9)throw new ArgumentException("Nonzero load required.");
        Ex=(B-A).Unit();
        // Project world Y onto plane orthogonal to connector axis.
        var trial=Math.Abs(Ex.Y)<.95?new Point3(0,1,0):new Point3(0,0,1);
        Ey=(trial-Ex*Point3.Dot(trial,Ex)).Unit();
        Ez=Point3.Cross(Ex,Ey);
    }

    internal Point3 Local(Point3 world)
    {
        var d=world-A;
        return new Point3(Point3.Dot(d,Ex),Point3.Dot(d,Ey),Point3.Dot(d,Ez));
    }

    internal Point3 VectorWorld(double lx,double ly,double lz) => Ex*lx+Ey*ly+Ez*lz;
    internal double SupportRadius => RadiusA*.8;
    internal double LoadedRadius => RadiusB*.8;

    internal static SphereScene FromJson(string path)
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(path));
        var root=doc.RootElement;
        var baseScene=Default;
        static Point3 Vector(JsonElement j)
        {
            if(j.GetArrayLength()!=3)throw new ArgumentException("Vector needs 3 coordinates.");
            return new Point3(j[0].GetDouble(),j[1].GetDouble(),j[2].GetDouble());
        }
        static double Property(JsonElement r,string name,double fallback)=>
            r.TryGetProperty(name,out var v)?v.GetDouble():fallback;
        static int IntProperty(JsonElement r,string name,int fallback)=>
            r.TryGetProperty(name,out var v)?v.GetInt32():fallback;
        static (Point3,double) Sphere(JsonElement root,string key)
        {
            var j=root.GetProperty(key);
            return (Vector(j.GetProperty("center")),j.GetProperty("radius").GetDouble());
        }
        var (a,ra)=Sphere(root,"sphereA");
        var (b,rb)=Sphere(root,"sphereB");
        var load=root.TryGetProperty("loadN",out var f)?Vector(f):baseScene.Force;
        return new SphereScene(a,b,ra,rb,load,
            Property(root,"youngMpa",baseScene.Young),
            Property(root,"poisson",baseScene.Poisson),
            Property(root,"maxVonMisesMpa",baseScene.StressLimit),
            Property(root,"maxDisplacementMm",baseScene.DisplacementLimit),
            IntProperty(root,"iterations",baseScene.Iterations));
    }

    internal SphereScene With(double fx,double fy,double fz,double young,
        double stress,double displacement,int steps) =>
        new(A,B,RadiusA,RadiusB,new Point3(fx,fy,fz),young,Poisson,stress,displacement,steps);

    internal (double[] Minimum,double[] Maximum) Bounds(double padding)
    {
        var r=MaxRadius+padding;
        return (
            new[]{Math.Min(A.X,B.X)-r,Math.Min(A.Y,B.Y)-r,Math.Min(A.Z,B.Z)-r},
            new[]{Math.Max(A.X,B.X)+r,Math.Max(A.Y,B.Y)+r,Math.Max(A.Z,B.Z)+r});
    }
}
