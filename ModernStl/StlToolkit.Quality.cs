namespace DCad.StlToolkit;

public readonly record struct TriangleQuality(
    double Area,
    double MinAngleDegrees,
    double MaxAngleDegrees,
    double AspectRatio,
    double EdgeMin,
    double EdgeMax);

public readonly record struct MeshQualityReport(
    int Triangles,
    int PoorAspectTriangles,
    int SmallAngleTriangles,
    int BoundaryLoops,
    int OpenBoundaryChains,
    double MinTriangleAngleDegrees,
    double MaxTriangleAspectRatio,
    double MedianEdgeLength,
    double SuggestedTolerance)
{
    public bool HasOpenBoundary => BoundaryLoops > 0 || OpenBoundaryChains > 0;
}

public static class StlQuality
{
    private readonly record struct Q(long X,long Y,long Z);
    private readonly record struct Edge(Q A,Q B);

    public static TriangleQuality AnalyzeTriangle(StlTriangle t)
    {
        double ab=(t.B-t.A).Length,bc=(t.C-t.B).Length,ca=(t.A-t.C).Length;
        var edges=new[]{ab,bc,ca};
        double emin=edges.Min(),emax=edges.Max();
        double area=t.Area;
        double aspect=area<=1e-30 ? double.PositiveInfinity : emax*emax/(2.0*Math.Sqrt(3.0)*area);
        double a=Angle(bc,ca,ab),b=Angle(ca,ab,bc),c=Angle(ab,bc,ca);
        return new TriangleQuality(area,Math.Min(a,Math.Min(b,c)),Math.Max(a,Math.Max(b,c)),aspect,emin,emax);
    }

    public static MeshQualityReport Analyze(
        StlMesh mesh,
        double poorAspectRatio=10.0,
        double smallAngleDegrees=10.0,
        double? tolerance=null)
    {
        if(mesh.Triangles.Count==0) return new MeshQualityReport(0,0,0,0,0,0,0,0,1e-12);
        var qualities=mesh.Triangles.Select(AnalyzeTriangle).ToArray();
        var edgeLengths=new List<double>(qualities.Length*3);
        foreach(var t in mesh.Triangles)
        {
            edgeLengths.Add((t.B-t.A).Length);edgeLengths.Add((t.C-t.B).Length);edgeLengths.Add((t.A-t.C).Length);
        }
        edgeLengths.Sort();
        double median=edgeLengths[edgeLengths.Count/2];
        double suggested=Math.Max(1e-12,median*1e-8);
        double tol=tolerance.HasValue?Math.Max(1e-12,tolerance.Value):suggested;
        var boundary=BoundaryDiagnostics(mesh,tol);
        return new MeshQualityReport(
            mesh.Triangles.Count,
            qualities.Count(q=>q.AspectRatio>poorAspectRatio),
            qualities.Count(q=>q.MinAngleDegrees<smallAngleDegrees),
            boundary.Loops,
            boundary.OpenChains,
            qualities.Min(q=>q.MinAngleDegrees),
            qualities.Max(q=>q.AspectRatio),
            median,
            suggested);
    }

    public static (int Loops,int OpenChains,IReadOnlyList<IReadOnlyList<Vec3d>> Paths) BoundaryDiagnostics(StlMesh mesh,double tolerance=1e-7)
    {
        tolerance=Math.Max(1e-12,tolerance);
        var edgeCounts=new Dictionary<Edge,int>();
        var representative=new Dictionary<Q,Vec3d>();
        foreach(var t in mesh.Triangles)
        {
            var a=Qv(t.A,tolerance);var b=Qv(t.B,tolerance);var c=Qv(t.C,tolerance);
            representative.TryAdd(a,t.A);representative.TryAdd(b,t.B);representative.TryAdd(c,t.C);
            Add(a,b);Add(b,c);Add(c,a);
        }
        var adjacency=new Dictionary<Q,List<Q>>();
        foreach(var pair in edgeCounts.Where(p=>p.Value==1))
        {
            AddAdj(pair.Key.A,pair.Key.B);AddAdj(pair.Key.B,pair.Key.A);
        }
        var unused=new HashSet<Edge>(edgeCounts.Where(p=>p.Value==1).Select(p=>p.Key));
        var paths=new List<IReadOnlyList<Vec3d>>();int loops=0,open=0;
        while(unused.Count>0)
        {
            var first=unused.First();
            Q start=Degree(first.A)!=2?first.A:Degree(first.B)!=2?first.B:first.A;
            var qpath=new List<Q>{start};Q prev=default;Q cur=start;bool hasPrev=false;
            while(true)
            {
                var nextCandidates=adjacency.GetValueOrDefault(cur,[])
                    .Where(n=>unused.Contains(Key(cur,n)) && (!hasPrev || !n.Equals(prev) || Degree(cur)==1)).ToList();
                if(nextCandidates.Count==0) break;
                var next=nextCandidates[0];unused.Remove(Key(cur,next));prev=cur;cur=next;hasPrev=true;qpath.Add(cur);
                if(cur.Equals(start)) break;
            }
            bool closed=qpath.Count>2 && qpath[^1].Equals(start);if(closed)loops++;else open++;
            paths.Add(qpath.Select(q=>representative[q]).ToArray());
        }
        return(loops,open,paths);

        int Degree(Q q)=>adjacency.TryGetValue(q,out var list)?list.Count:0;
        void Add(Q a,Q b){var key=Key(a,b);edgeCounts.TryGetValue(key,out int n);edgeCounts[key]=n+1;}
        void AddAdj(Q a,Q b){if(!adjacency.TryGetValue(a,out var list)){list=[];adjacency[a]=list;}list.Add(b);}
    }

    public static double ScaleAwareTolerance(StlMesh mesh,double relative=1e-9,double absoluteFloor=1e-12)
    {
        if(mesh.Triangles.Count==0)return absoluteFloor;
        double minX=double.PositiveInfinity,minY=double.PositiveInfinity,minZ=double.PositiveInfinity;
        double maxX=double.NegativeInfinity,maxY=double.NegativeInfinity,maxZ=double.NegativeInfinity;
        foreach(var t in mesh.Triangles)
            foreach(var p in new[]{t.A,t.B,t.C})
            {minX=Math.Min(minX,p.X);minY=Math.Min(minY,p.Y);minZ=Math.Min(minZ,p.Z);maxX=Math.Max(maxX,p.X);maxY=Math.Max(maxY,p.Y);maxZ=Math.Max(maxZ,p.Z);}
        double diagonal=Math.Sqrt((maxX-minX)*(maxX-minX)+(maxY-minY)*(maxY-minY)+(maxZ-minZ)*(maxZ-minZ));
        return Math.Max(absoluteFloor,diagonal*Math.Max(relative,0.0));
    }

    private static double Angle(double adjacent1,double adjacent2,double opposite)
    {
        double denom=2.0*adjacent1*adjacent2;if(denom<=1e-30)return 0.0;
        double cosine=Math.Clamp((adjacent1*adjacent1+adjacent2*adjacent2-opposite*opposite)/denom,-1.0,1.0);
        return Math.Acos(cosine)*180.0/Math.PI;
    }
    private static Q Qv(Vec3d p,double t)=>new((long)Math.Round(p.X/t),(long)Math.Round(p.Y/t),(long)Math.Round(p.Z/t));
    private static Edge Key(Q a,Q b)=>Compare(a,b)<=0?new Edge(a,b):new Edge(b,a);
    private static int Compare(Q a,Q b){int c=a.X.CompareTo(b.X);if(c!=0)return c;c=a.Y.CompareTo(b.Y);return c!=0?c:a.Z.CompareTo(b.Z);}
}
