using System.Globalization;

namespace DCad.MeshKernel;

public readonly record struct Vec3d(double X, double Y, double Z)
{
    public static Vec3d operator +(Vec3d a, Vec3d b) => new(a.X+b.X,a.Y+b.Y,a.Z+b.Z);
    public static Vec3d operator -(Vec3d a, Vec3d b) => new(a.X-b.X,a.Y-b.Y,a.Z-b.Z);
    public static Vec3d operator *(Vec3d a, double s) => new(a.X*s,a.Y*s,a.Z*s);
    public static Vec3d operator /(Vec3d a, double s) => new(a.X/s,a.Y/s,a.Z/s);
    public double Dot(Vec3d b) => X*b.X+Y*b.Y+Z*b.Z;
    public Vec3d Cross(Vec3d b) => new(Y*b.Z-Z*b.Y,Z*b.X-X*b.Z,X*b.Y-Y*b.X);
    public double Length => Math.Sqrt(Dot(this));
    public Vec3d Normalized => Length > 1e-15 ? this/Length : default;
    public static Vec3d Lerp(Vec3d a, Vec3d b, double t) => a+(b-a)*t;
}

public sealed class CsgVertex
{
    public Vec3d Position { get; }
    public CsgVertex(Vec3d position) => Position=position;
    public CsgVertex Interpolate(CsgVertex other,double t)=>new(Vec3d.Lerp(Position,other.Position,t));
}

public sealed class CsgPlane
{
    public const double Epsilon=1e-7;
    public Vec3d Normal { get; private set; }
    public double W { get; private set; }
    public CsgPlane(Vec3d n,double w){Normal=n;W=w;}
    public static CsgPlane FromPoints(Vec3d a,Vec3d b,Vec3d c)
    {
        var n=(b-a).Cross(c-a).Normalized;
        return new CsgPlane(n,n.Dot(a));
    }
    public CsgPlane Clone()=>new(Normal,W);
    public void Flip(){Normal=Normal*-1;W=-W;}

    public void SplitPolygon(CsgPolygon polygon,List<CsgPolygon> coplanarFront,List<CsgPolygon> coplanarBack,List<CsgPolygon> front,List<CsgPolygon> back)
    {
        const int Coplanar=0,Front=1,Back=2,Spanning=3;
        int polygonType=0; var types=new int[polygon.Vertices.Count];
        for(int i=0;i<polygon.Vertices.Count;i++)
        {
            double t=Normal.Dot(polygon.Vertices[i].Position)-W;
            int type=t < -Epsilon ? Back : t > Epsilon ? Front : Coplanar;
            polygonType|=type; types[i]=type;
        }
        switch(polygonType)
        {
            case Coplanar:
                if(Normal.Dot(polygon.Plane.Normal)>0) coplanarFront.Add(polygon); else coplanarBack.Add(polygon);
                break;
            case Front: front.Add(polygon); break;
            case Back: back.Add(polygon); break;
            case Spanning:
                var f=new List<CsgVertex>(); var b=new List<CsgVertex>();
                for(int i=0;i<polygon.Vertices.Count;i++)
                {
                    int j=(i+1)%polygon.Vertices.Count; int ti=types[i],tj=types[j];
                    var vi=polygon.Vertices[i]; var vj=polygon.Vertices[j];
                    if(ti!=Back) f.Add(vi); if(ti!=Front) b.Add(vi);
                    if((ti|tj)==Spanning)
                    {
                        double denom=Normal.Dot(vj.Position-vi.Position);
                        if(Math.Abs(denom)<1e-15) continue;
                        double t=(W-Normal.Dot(vi.Position))/denom;
                        var v=vi.Interpolate(vj,t); f.Add(v); b.Add(v);
                    }
                }
                if(f.Count>=3) front.Add(new CsgPolygon(f));
                if(b.Count>=3) back.Add(new CsgPolygon(b));
                break;
        }
    }
}

public sealed class CsgPolygon
{
    public List<CsgVertex> Vertices { get; }
    public CsgPlane Plane { get; private set; }
    public CsgPolygon(IEnumerable<CsgVertex> vertices)
    {
        Vertices=vertices.ToList();
        if(Vertices.Count<3) throw new ArgumentException("Polygon needs at least three vertices.");
        Plane=CsgPlane.FromPoints(Vertices[0].Position,Vertices[1].Position,Vertices[2].Position);
    }
    public CsgPolygon Clone()=>new(Vertices.Select(v=>new CsgVertex(v.Position)));
    public void Flip(){Vertices.Reverse();Plane.Flip();}
}

public sealed class CsgNode
{
    CsgPlane? plane;
    readonly List<CsgPolygon> polygons=new();
    CsgNode? front,back;
    public CsgNode(){}
    public CsgNode(IEnumerable<CsgPolygon> source)=>Build(source.ToList());
    public CsgNode Clone()
    {
        var n=new CsgNode{plane=plane?.Clone()};
        n.polygons.AddRange(polygons.Select(p=>p.Clone()));
        if(front!=null)n.front=front.Clone(); if(back!=null)n.back=back.Clone(); return n;
    }
    public void Invert(){foreach(var p in polygons)p.Flip();plane?.Flip();front?.Invert();back?.Invert();(front,back)=(back,front);}
    public List<CsgPolygon> ClipPolygons(List<CsgPolygon> input)
    {
        if(plane==null)return input.Select(p=>p.Clone()).ToList();
        var f=new List<CsgPolygon>();var b=new List<CsgPolygon>();
        foreach(var p in input) plane.SplitPolygon(p,f,b,f,b);
        if(front!=null)f=front.ClipPolygons(f); if(back!=null)b=back.ClipPolygons(b); else b.Clear();
        f.AddRange(b);return f;
    }
    public void ClipTo(CsgNode other){var clipped=other.ClipPolygons(polygons);polygons.Clear();polygons.AddRange(clipped);front?.ClipTo(other);back?.ClipTo(other);}
    public List<CsgPolygon> AllPolygons(){var r=polygons.Select(p=>p.Clone()).ToList();if(front!=null)r.AddRange(front.AllPolygons());if(back!=null)r.AddRange(back.AllPolygons());return r;}
    public void Build(List<CsgPolygon> input)
    {
        if(input.Count==0)return; plane ??= input[0].Plane.Clone();
        var f=new List<CsgPolygon>();var b=new List<CsgPolygon>();
        foreach(var p in input)plane.SplitPolygon(p,polygons,polygons,f,b);
        if(f.Count>0){front??=new CsgNode();front.Build(f);} if(b.Count>0){back??=new CsgNode();back.Build(b);}
    }
}

public sealed class CsgSolid
{
    public IReadOnlyList<CsgPolygon> Polygons { get; }
    public CsgSolid(IEnumerable<CsgPolygon> polygons)=>Polygons=polygons.ToList();
    public CsgSolid Clone()=>new(Polygons.Select(p=>p.Clone()));
    public CsgSolid Union(CsgSolid other)
    {
        var a=new CsgNode(Polygons.Select(p=>p.Clone()));var b=new CsgNode(other.Polygons.Select(p=>p.Clone()));
        a.ClipTo(b);b.ClipTo(a);b.Invert();b.ClipTo(a);b.Invert();a.Build(b.AllPolygons());return new CsgSolid(a.AllPolygons());
    }
    public CsgSolid Subtract(CsgSolid other)
    {
        var a=new CsgNode(Polygons.Select(p=>p.Clone()));var b=new CsgNode(other.Polygons.Select(p=>p.Clone()));
        a.Invert();a.ClipTo(b);b.ClipTo(a);b.Invert();b.ClipTo(a);b.Invert();a.Build(b.AllPolygons());a.Invert();return new CsgSolid(a.AllPolygons());
    }
    public CsgSolid Intersect(CsgSolid other)
    {
        var a=new CsgNode(Polygons.Select(p=>p.Clone()));var b=new CsgNode(other.Polygons.Select(p=>p.Clone()));
        a.Invert();b.ClipTo(a);b.Invert();a.ClipTo(b);b.ClipTo(a);a.Build(b.AllPolygons());a.Invert();return new CsgSolid(a.AllPolygons());
    }
    public IEnumerable<(Vec3d A,Vec3d B,Vec3d C)> Triangles()
    {
        foreach(var p in Polygons)for(int i=2;i<p.Vertices.Count;i++)yield return(p.Vertices[0].Position,p.Vertices[i-1].Position,p.Vertices[i].Position);
    }
}

public static class MeshFactory
{
    static CsgPolygon P(params Vec3d[] v)=>new(v.Select(x=>new CsgVertex(x)));
    public static CsgSolid Box(Vec3d min,Vec3d max)
    {
        var v=new[]{new Vec3d(min.X,min.Y,min.Z),new Vec3d(max.X,min.Y,min.Z),new Vec3d(max.X,max.Y,min.Z),new Vec3d(min.X,max.Y,min.Z),new Vec3d(min.X,min.Y,max.Z),new Vec3d(max.X,min.Y,max.Z),new Vec3d(max.X,max.Y,max.Z),new Vec3d(min.X,max.Y,max.Z)};
        return new CsgSolid(new[]{P(v[0],v[3],v[2],v[1]),P(v[4],v[5],v[6],v[7]),P(v[0],v[1],v[5],v[4]),P(v[1],v[2],v[6],v[5]),P(v[2],v[3],v[7],v[6]),P(v[3],v[0],v[4],v[7])});
    }
    public static CsgSolid Cylinder(Vec3d center,double radius,double height,int segments=48)
    {
        segments=Math.Max(8,segments);double z0=center.Z-height*.5,z1=center.Z+height*.5;var polys=new List<CsgPolygon>();
        var bottom=new List<CsgVertex>();var top=new List<CsgVertex>();
        for(int i=0;i<segments;i++){double a=2*Math.PI*i/segments;bottom.Add(new CsgVertex(new Vec3d(center.X+radius*Math.Cos(a),center.Y+radius*Math.Sin(a),z0)));top.Add(new CsgVertex(new Vec3d(center.X+radius*Math.Cos(a),center.Y+radius*Math.Sin(a),z1)));}
        polys.Add(new CsgPolygon(bottom.AsEnumerable().Reverse()));polys.Add(new CsgPolygon(top));
        for(int i=0;i<segments;i++){int j=(i+1)%segments;polys.Add(new CsgPolygon(new[]{bottom[i],bottom[j],top[j],top[i]}));}
        return new CsgSolid(polys);
    }
}

public readonly record struct MeshStats(int Triangles,double Area,double SignedVolume,Vec3d Min,Vec3d Max);
public static class MeshAnalysis
{
    public static MeshStats Analyze(CsgSolid solid)
    {
        int n=0;double area=0,vol=0;var min=new Vec3d(double.PositiveInfinity,double.PositiveInfinity,double.PositiveInfinity);var max=new Vec3d(double.NegativeInfinity,double.NegativeInfinity,double.NegativeInfinity);
        foreach(var (a,b,c) in solid.Triangles())
        {
            n++;area+=(b-a).Cross(c-a).Length*.5;vol+=a.Dot(b.Cross(c))/6.0;
            foreach(var p in new[]{a,b,c}){min=new Vec3d(Math.Min(min.X,p.X),Math.Min(min.Y,p.Y),Math.Min(min.Z,p.Z));max=new Vec3d(Math.Max(max.X,p.X),Math.Max(max.Y,p.Y),Math.Max(max.Z,p.Z));}
        }
        return new MeshStats(n,area,vol,min,max);
    }
}

public static class StlWriter
{
    public static void WriteAscii(string path,CsgSolid solid,string name="dcad")
    {
        using var w=new StreamWriter(path);var ci=CultureInfo.InvariantCulture;w.WriteLine("solid "+name);
        foreach(var (a,b,c) in solid.Triangles())
        {
            var n=(b-a).Cross(c-a).Normalized;w.WriteLine($"  facet normal {n.X.ToString("G17",ci)} {n.Y.ToString("G17",ci)} {n.Z.ToString("G17",ci)}");w.WriteLine("    outer loop");
            foreach(var p in new[]{a,b,c})w.WriteLine($"      vertex {p.X.ToString("G17",ci)} {p.Y.ToString("G17",ci)} {p.Z.ToString("G17",ci)}");
            w.WriteLine("    endloop");w.WriteLine("  endfacet");
        }
        w.WriteLine("endsolid "+name);
    }
}
