using System.Globalization;
using System.Text.Json;
using DCad.FunctionalVoxel;

namespace FvmGenerativeDemo;

// Arbitrarily nested CSG (not merely spheres). Positive implicit field = solid.
internal abstract class ImplicitSolid
{
    internal abstract double F(Point3 p);

    private static double Num(JsonElement e,string name,double fallback=double.NaN)
    {
        if(e.TryGetProperty(name,out var value))return value.GetDouble();
        if(double.IsNaN(fallback))throw new ArgumentException($"Missing required '{name}'.");
        return fallback;
    }
    internal static Point3 Vec(JsonElement e)
    {
        if(e.ValueKind!=JsonValueKind.Array||e.GetArrayLength()!=3)throw new ArgumentException("Expected vector [x,y,z].");
        return new Point3(e[0].GetDouble(),e[1].GetDouble(),e[2].GetDouble());
    }
    private static Point3 P(JsonElement e,string name)=>Vec(e.GetProperty(name));

    internal static ImplicitSolid Parse(JsonElement j)
    {
        var type=j.GetProperty("type").GetString()?.ToLowerInvariant();
        Point3 center = j.TryGetProperty("center",out var c)?Vec(c):new Point3(0,0,0);
        ImplicitSolid result=type switch
        {
            "sphere"=>new Sphere(center,Num(j,"radius")),
            "box"=>new Box(center,P(j,"halfSize")),
            "ellipsoid"=>new Ellipsoid(center,P(j,"radii")),
            "capsule"=>new Capsule(P(j,"a"),P(j,"b"),Num(j,"radius")),
            "cylinder"=>new Cylinder(P(j,"a"),P(j,"b"),Num(j,"radius")),
            "torus"=>new Torus(center,Num(j,"majorRadius"),Num(j,"minorRadius")),
            "union" or "intersection" or "difference" => ParseBoolean(type,j),
            _=>throw new ArgumentException($"Unsupported implicit CSG shape '{type}'.")
        };
        return result;
    }
    private static ImplicitSolid ParseBoolean(string type,JsonElement j)
    {
        var children=j.GetProperty("children").EnumerateArray().Select(Parse).ToArray();
        if(children.Length<2)throw new ArgumentException("CSG union/intersection/difference needs two or more children.");
        return new Csg(type,children);
    }
    internal sealed class Sphere(Point3 center,double r):ImplicitSolid
    {
        internal override double F(Point3 p)=>r-(p-center).Norm();
    }
    internal sealed class Box(Point3 c,Point3 h):ImplicitSolid
    {
        internal override double F(Point3 p)
        {
            var q=p-c;var x=Math.Abs(q.X)-h.X;var y=Math.Abs(q.Y)-h.Y;var z=Math.Abs(q.Z)-h.Z;
            var outside=Math.Sqrt(Math.Pow(Math.Max(x,0),2)+Math.Pow(Math.Max(y,0),2)+Math.Pow(Math.Max(z,0),2));
            return -outside-Math.Min(Math.Max(x,Math.Max(y,z)),0);
        }
    }
    internal sealed class Ellipsoid(Point3 c,Point3 r):ImplicitSolid
    {
        internal override double F(Point3 p)
        {
            var q=p-c;
            return Math.Min(r.X,Math.Min(r.Y,r.Z))*
                (1-Math.Sqrt(q.X*q.X/(r.X*r.X)+q.Y*q.Y/(r.Y*r.Y)+q.Z*q.Z/(r.Z*r.Z)));
        }
    }
    internal sealed class Capsule(Point3 a,Point3 b,double r):ImplicitSolid
    {
        internal override double F(Point3 p)
        {
            var ab=b-a;var den=Point3.Dot(ab,ab);
            var t=den>0?Math.Clamp(Point3.Dot(p-a,ab)/den,0,1):0;
            return r-(p-(a+ab*t)).Norm();
        }
    }
    internal sealed class Cylinder(Point3 a,Point3 b,double r):ImplicitSolid
    {
        internal override double F(Point3 p)
        {
            var length=(b-a).Norm();if(length<1e-10)throw new ArgumentException("Cylinder a/b must differ.");
            var v=(b-a)*(1/length);var q=p-a;
            var t=Point3.Dot(q,v);var radial=(q-v*t).Norm()-r;
            var axial=Math.Abs(t-length*.5)-length*.5;
            var outside=Math.Sqrt(Math.Pow(Math.Max(radial,0),2)+Math.Pow(Math.Max(axial,0),2));
            return -outside-Math.Min(Math.Max(radial,axial),0);
        }
    }
    // Symmetric torus around world Z axis; rotate by nested CSG shapes if needed in later versions.
    internal sealed class Torus(Point3 c,double major,double minor):ImplicitSolid
    {
        internal override double F(Point3 p)
        {
            var v=p-c;var radial=Math.Sqrt(v.X*v.X+v.Y*v.Y);
            return minor-Math.Sqrt(Math.Pow(radial-major,2)+v.Z*v.Z);
        }
    }
    internal sealed class Csg(string mode,ImplicitSolid[] children):ImplicitSolid
    {
        internal override double F(Point3 p)
        {
            double value=children[0].F(p);
            for(var i=1;i<children.Length;i++)
            {
                var next=children[i].F(p);
                value=mode switch
                {
                    "union"=>Math.Max(value,next),
                    "intersection"=>Math.Min(value,next),
                    "difference"=>Math.Min(value,-next),
                    _=>throw new InvalidOperationException("Unknown CSG operator.")
                };
            }
            return value;
        }
    }
}

internal sealed record UniversalBody(string Id,string Role,ImplicitSolid Shape,Point3 Seed,Point3 Force);
internal sealed record UniversalObstacle(string Id,ImplicitSolid Shape,double Clearance);

internal sealed class UniversalScene
{
    internal readonly List<UniversalBody> Bodies=new();
    internal readonly List<UniversalObstacle> Obstacles=new();
    internal readonly List<(string Name,Dictionary<string,Point3> Loads)> LoadCases=new();
    internal Point3 Min {get;private set;}
    internal Point3 Max {get;private set;}
    internal double RouteStep {get;private set;}
    internal double FvmStep {get;private set;}
    internal double BridgeRadius {get;private set;}
    internal int[] FemCells {get;private set;}=new[]{12,10,10};
    internal double Young {get;private set;}=200000;
    internal double Poisson {get;private set;}=.3;
    internal double StressLimit {get;private set;}=250;
    internal double DisplacementLimit {get;private set;}=.6;
    internal double DensityKgMm3 {get;private set;}=7.85e-6;
    internal double MinFrequencyHz {get;private set;}=0;
    internal double MaxImpactDisplacementMm {get;private set;}=double.PositiveInfinity;
    internal double ImpactPulseSeconds {get;private set;}=.001;
    internal double ImpactMultiplier {get;private set;}=2;
    internal double DampingRatio {get;private set;}=.02;
    internal double ThermalConductivityWmK {get;private set;}=45;
    internal double MinThermalConductanceWPerK {get;private set;}=0;
    internal string? ThermalHotId {get;private set;}
    internal string? ThermalColdId {get;private set;}
    internal int Iterations {get;private set;}=3;
    internal bool AssertDetour {get;private set;}=false;
    private static double Scalar(JsonElement e,string prop,double fallback)=>
        e.TryGetProperty(prop,out var v)?v.GetDouble():fallback;
    private static int Integer(JsonElement e,string prop,int fallback)=>
        e.TryGetProperty(prop,out var v)?v.GetInt32():fallback;

    internal static UniversalScene Load(string file)
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(file));
        var j=doc.RootElement;
        var s=new UniversalScene{
            Min=ImplicitSolid.Vec(j.GetProperty("bounds").GetProperty("min")),
            Max=ImplicitSolid.Vec(j.GetProperty("bounds").GetProperty("max")),
            RouteStep=Scalar(j,"routingStepMm",4),
            FvmStep=Scalar(j,"fvmStepMm",3),
            BridgeRadius=Scalar(j,"bridgeRadiusMm",4),
            Iterations=Integer(j,"iterations",3),
            AssertDetour=j.TryGetProperty("assertDetour",out var flag)&&flag.GetBoolean()
        };
        if(j.TryGetProperty("femCells",out var cells))
        {
            if(cells.GetArrayLength()!=3)throw new ArgumentException("femCells requires [nx,ny,nz].");
            s.FemCells=cells.EnumerateArray().Select(e=>e.GetInt32()).ToArray();
        }
        if(j.TryGetProperty("material",out var mat))
        {
            s.Young=Scalar(mat,"youngMpa",s.Young);
            s.Poisson=Scalar(mat,"poisson",s.Poisson);
            s.DensityKgMm3=Scalar(mat,"densityKgM3",7850)*1e-9;
            s.ThermalConductivityWmK=Scalar(mat,"thermalConductivityWmK",45);
        }
        if(j.TryGetProperty("limits",out var lim))
        {
            s.StressLimit=Scalar(lim,"vonMisesMpa",s.StressLimit);
            s.DisplacementLimit=Scalar(lim,"displacementMm",s.DisplacementLimit);
            s.MinFrequencyHz=Scalar(lim,"minFirstFrequencyHz",0);
            s.MaxImpactDisplacementMm=Scalar(lim,"maxImpactDisplacementMm",double.PositiveInfinity);
            s.MinThermalConductanceWPerK=Scalar(lim,"minThermalConductanceWPerK",0);
        }
        if(j.TryGetProperty("impact",out var imp))
        {
            s.ImpactPulseSeconds=Scalar(imp,"halfSinePulseSeconds",s.ImpactPulseSeconds);
            s.ImpactMultiplier=Scalar(imp,"peakForceMultiplier",s.ImpactMultiplier);
            s.DampingRatio=Scalar(imp,"dampingRatio",s.DampingRatio);
        }
        if(j.TryGetProperty("thermal",out var thermal))
        {
            s.ThermalHotId=thermal.GetProperty("hotBody").GetString();
            s.ThermalColdId=thermal.GetProperty("coldBody").GetString();
        }
        foreach(var b in j.GetProperty("bodies").EnumerateArray())
        {
            var id=b.GetProperty("id").GetString()??throw new ArgumentException("Missing id.");
            var role=b.GetProperty("role").GetString()?.ToLowerInvariant()??"anchor";
            var shape=ImplicitSolid.Parse(b.GetProperty("shape"));
            var seed=ImplicitSolid.Vec(b.GetProperty("seed"));
            var force=b.TryGetProperty("forceN",out var l)?ImplicitSolid.Vec(l):new Point3(0,0,0);
            if(role is not ("fixed" or "anchor" or "load"))
                throw new ArgumentException($"Unknown body role '{role}'.");
            s.Bodies.Add(new UniversalBody(id,role,shape,seed,force));
        }
        if(j.TryGetProperty("obstacles",out var obs))
            foreach(var o in obs.EnumerateArray())
                s.Obstacles.Add(new UniversalObstacle(
                    o.GetProperty("id").GetString()??"keepout",
                    ImplicitSolid.Parse(o.GetProperty("shape")),
                    Scalar(o,"clearanceMm",1)));
        if(j.TryGetProperty("loadCases",out var cases))
        {
            foreach(var c in cases.EnumerateArray())
            {
                var forces=new Dictionary<string,Point3>();
                foreach(var pair in c.GetProperty("forcesN").EnumerateObject())
                    forces.Add(pair.Name,ImplicitSolid.Vec(pair.Value));
                s.LoadCases.Add((c.GetProperty("name").GetString()??"case",forces));
            }
        }
        else s.LoadCases.Add(("default",s.Bodies.Where(b=>b.Force.Norm()>0)
            .ToDictionary(b=>b.Id,b=>b.Force)));
        s.Validate();
        return s;
    }

    private void Validate()
    {
        var lengths=Max-Min;
        if(lengths.X<=0||lengths.Y<=0||lengths.Z<=0
           ||RouteStep<=0||FvmStep<=0||BridgeRadius<=0||Young<=0
           ||Poisson<=-1||Poisson>=.5||StressLimit<=0||DisplacementLimit<=0
           ||Iterations<1||Iterations>20||ImpactPulseSeconds<=0||DampingRatio<0
           ||DampingRatio>1||DensityKgMm3<=0||ThermalConductivityWmK<=0)
            throw new ArgumentException("Invalid scene dimensions, material or optimization settings.");
        if(FemCells.Any(c=>c<3||c>25))throw new ArgumentException("femCells must be 3..25 per axis.");
        var nb=new[]{lengths.X/RouteStep,lengths.Y/RouteStep,lengths.Z/RouteStep};
        if(nb.Aggregate(1.0,(v,n)=>v*(n+2))>100000)
            throw new ArgumentException("Routing grid >100k nodes; increase routingStepMm.");
        var nc=new[]{lengths.X/FvmStep,lengths.Y/FvmStep,lengths.Z/FvmStep};
        if(nc.Aggregate(1.0,(v,n)=>v*(n+2))>160000)
            throw new ArgumentException("FVM grid >160k voxels; increase fvmStepMm.");
        if(Bodies.Count<2||Bodies.Count>32||!Bodies.Any(b=>b.Role=="fixed"))
            throw new ArgumentException("Requires 2..32 bodies and at least one fixed body.");
        if(Bodies.Select(b=>b.Id).Distinct(StringComparer.Ordinal).Count()!=Bodies.Count)
            throw new ArgumentException("Duplicate body id.");
        foreach(var b in Bodies)
        {
            if(b.Shape.F(b.Seed)<.1)throw new ArgumentException($"Seed '{b.Id}' is not inside material.");
            if(!Within(b.Seed))throw new ArgumentException($"Seed '{b.Id}' is outside bounds.");
            if(Obstacles.Any(o=>o.Shape.F(b.Seed)>-o.Clearance))
                throw new ArgumentException($"Protected seed '{b.Id}' collides with keepout.");
        }
        foreach(var (name,forces) in LoadCases)
        {
            if(forces.Count==0||forces.Values.All(v=>v.Norm()<1e-9))
                throw new ArgumentException($"Load case '{name}' has no applied forces.");
            foreach(var id in forces.Keys)
                if(!Bodies.Any(b=>b.Id==id&&b.Role!="fixed"))
                    throw new ArgumentException($"Load target '{id}' absent or fixed.");
        }
        if(MinThermalConductanceWPerK>0 &&
           (ThermalHotId is null||ThermalColdId is null||
            !Bodies.Any(b=>b.Id==ThermalHotId)||
            !Bodies.Any(b=>b.Id==ThermalColdId)||
            ThermalHotId==ThermalColdId))
            throw new ArgumentException("Thermal hot/cold IDs must refer to different bodies.");
    }
    internal bool Within(Point3 p)=>p.X>=Min.X&&p.Y>=Min.Y&&p.Z>=Min.Z&&
        p.X<=Max.X&&p.Y<=Max.Y&&p.Z<=Max.Z;
    internal double ObstDistance(Point3 p)
    {
        double value=double.PositiveInfinity;
        foreach(var o in Obstacles)
            value=Math.Min(value,-o.Shape.F(p)-o.Clearance);
        return value; // positive means outside all expanded forbidden regions
    }
    internal bool SafeRoute(Point3 p)=>Within(p)&&ObstDistance(p)>BridgeRadius+.05;
}

internal sealed class RoutedBranch
{
    internal required UniversalBody Target;
    internal readonly List<Point3> Polyline=new();
}

internal sealed class MultiTerminalRouter
{
    private readonly UniversalScene _s;
    private readonly int _nx,_ny,_nz;
    private readonly bool[] _safe;
    private readonly Point3 _step;
    private readonly int _total;
    internal MultiTerminalRouter(UniversalScene s)
    {
        _s=s;var span=s.Max-s.Min;
        _nx=(int)Math.Ceiling(span.X/s.RouteStep)+1;
        _ny=(int)Math.Ceiling(span.Y/s.RouteStep)+1;
        _nz=(int)Math.Ceiling(span.Z/s.RouteStep)+1;
        _step=new Point3(span.X/(_nx-1),span.Y/(_ny-1),span.Z/(_nz-1));
        _total=_nx*_ny*_nz;
        _safe=new bool[_total];
        for(var i=0;i<_total;i++)_safe[i]=s.SafeRoute(Pos(i));
    }
    private int Index(int x,int y,int z)=>(x*_ny+y)*_nz+z;
    private (int,int,int) Decode(int i)=>(i/(_ny*_nz),(i/_nz)%_ny,i%_nz);
    private Point3 Pos(int i)
    {
        var (x,y,z)=Decode(i);
        return new Point3(_s.Min.X+x*_step.X,_s.Min.Y+y*_step.Y,_s.Min.Z+z*_step.Z);
    }
    private int Nearest(Point3 p)
    {
        var i=(int)Math.Round((p.X-_s.Min.X)/_step.X);
        var j=(int)Math.Round((p.Y-_s.Min.Y)/_step.Y);
        var k=(int)Math.Round((p.Z-_s.Min.Z)/_step.Z);
        return Index(Math.Clamp(i,0,_nx-1),Math.Clamp(j,0,_ny-1),Math.Clamp(k,0,_nz-1));
    }
    private bool SegmentFree(Point3 a,Point3 b)
    {
        int count=Math.Max(2,(int)Math.Ceiling((b-a).Norm()/(_s.RouteStep*.35)));
        for(var i=0;i<=count;i++)
            if(!_s.SafeRoute(a+(b-a)*(i/(double)count)))return false;
        return true;
    }
    private int SafeNearest(Point3 p)
    {
        // Snap only if the physically continuous small connecting segment is clear.
        var at=Nearest(p);
        if(_safe[at]&&SegmentFree(p,Pos(at)))return at;
        var best=-1;double score=double.PositiveInfinity;
        for(var i=0;i<_total;i++)
        {
            if(!_safe[i])continue;
            var cost=(Pos(i)-p).Norm();
            if(cost<score && cost<=_s.RouteStep*2 && SegmentFree(p,Pos(i)))
            {
                best=i;score=cost;
            }
        }
        if(best<0)throw new InvalidOperationException("A terminal cannot be snapped to a safe route node.");
        return best;
    }
    internal List<RoutedBranch> Route()
    {
        var root=_s.Bodies.First(b=>b.Role=="fixed");
        var network=new HashSet<int>{SafeNearest(root.Seed)};
        var results=new List<RoutedBranch>();
        // Multiple supports are connected to the same joint network,
        // then loads and ordinary anchors are added.
        var remaining=_s.Bodies.Where(b=>b!=root).ToList();
        while(remaining.Count>0)
        {
            var chosen=remaining.OrderBy(b=>network.Min(k=>(Pos(k)-b.Seed).Norm())).First();
            remaining.Remove(chosen);
            var dest=SafeNearest(chosen.Seed);
            var cost=Enumerable.Repeat(double.PositiveInfinity,_total).ToArray();
            var parent=Enumerable.Repeat(-1,_total).ToArray();
            var pq=new PriorityQueue<int,double>();
            foreach(var a in network){cost[a]=0;pq.Enqueue(a,0);}
            while(pq.TryDequeue(out var current,out var priority))
            {
                if(priority>cost[current]+1e-9)continue;
                if(current==dest)break;
                var (x,y,z)=Decode(current);
                for(var dx=-1;dx<=1;dx++)for(var dy=-1;dy<=1;dy++)for(var dz=-1;dz<=1;dz++)
                {
                    if(dx==0&&dy==0&&dz==0)continue;
                    int xx=x+dx,yy=y+dy,zz=z+dz;
                    if(xx<0||xx>=_nx||yy<0||yy>=_ny||zz<0||zz>=_nz)continue;
                    var ni=Index(xx,yy,zz);
                    if(!_safe[ni]||!SegmentFree(Pos(current),Pos(ni)))continue;
                    var next=cost[current]+(Pos(ni)-Pos(current)).Norm();
                    if(next>=cost[ni])continue;
                    cost[ni]=next;parent[ni]=current;pq.Enqueue(ni,next);
                }
            }
            if(!double.IsFinite(cost[dest]))
                throw new InvalidOperationException($"No obstacle-free route to body '{chosen.Id}'.");
            var chain=new List<int>();
            for(var cur=dest;cur>=0;cur=parent[cur])
            {
                chain.Add(cur);
                if(network.Contains(cur))break;
            }
            chain.Reverse();
            var branch=new RoutedBranch{Target=chosen};
            branch.Polyline.Add(Pos(chain[0]));
            foreach(var node in chain.Skip(1))branch.Polyline.Add(Pos(node));
            branch.Polyline.Add(chosen.Seed);
            // Ensure first and last points join exact material seeds.
            if(results.Count==0)branch.Polyline.Insert(0,root.Seed);
            // Remove intermediate collinear and obstacle-free waypoints.
            // Without this, repeated implicit F evaluations create O(grid*route nodes)
            // segments and can dominate the finite-element solution.
            var reduced=new List<Point3>();
            int from=0;
            while(from<branch.Polyline.Count-1)
            {
                reduced.Add(branch.Polyline[from]);
                int to=from+1;
                for(int candidate=branch.Polyline.Count-1;candidate>from+1;candidate--)
                {
                    if(SegmentFree(branch.Polyline[from],branch.Polyline[candidate]))
                    {
                        to=candidate;break;
                    }
                }
                from=to;
            }
            reduced.Add(branch.Polyline[^1]);
            branch.Polyline.Clear();
            branch.Polyline.AddRange(reduced);
            foreach(var node in chain)network.Add(node);
            results.Add(branch);
        }
        return results;
    }
}

// Single continuously defined F with exact positive prescribed CSG solids and
// negative keep-outs; branch widths are optimization variables.
internal sealed class UniversalField
{
    private readonly UniversalScene _scene;
    internal readonly List<RoutedBranch> Branches;
    internal readonly double[] P;
    internal readonly FunctionalGrid Grid;

    internal UniversalField(UniversalScene scene,List<RoutedBranch> branches,double[] p)
    {
        _scene=scene;Branches=branches;P=(double[])p.Clone();
        if(p.Length!=branches.Count)throw new ArgumentException("Wrong number of bridge shape parameters.");
        var v=scene.Max-scene.Min;
        var c=new[]{
            (int)Math.Ceiling(v.X/scene.FvmStep),
            (int)Math.Ceiling(v.Y/scene.FvmStep),
            (int)Math.Ceiling(v.Z/scene.FvmStep)};
        Grid=new FunctionalGrid(new[]{scene.Min.X,scene.Min.Y,scene.Min.Z},
            new[]{v.X/c[0],v.Y/c[1],v.Z/c[2]},c);
    }
    internal double Value(Point3 point)
    {
        var value=double.NegativeInfinity;
        foreach(var body in _scene.Bodies)
            value=Math.Max(value,body.Shape.F(point));
        for(var b=0;b<Branches.Count;b++)
        {
            var path=Branches[b].Polyline;
            var r=_scene.BridgeRadius+P[b];
            for(var i=1;i<path.Count;i++)
            {
                var start=path[i-1];var delta=path[i]-start;
                var dd=Point3.Dot(delta,delta);
                var t=dd>0?Math.Clamp(Point3.Dot(point-start,delta)/dd,0,1):0;
                value=Math.Max(value,r-(point-(start+delta*t)).Norm());
            }
        }
        foreach(var obstacle in _scene.Obstacles)
            value=Math.Min(value,-obstacle.Shape.F(point)-obstacle.Clearance);
        return value;
    }
    internal FunctionalVoxelModel Sample()
    {
        var h=.07*_scene.FvmStep;
        var f=FunctionalField.Analytic(3,x=>
        {
            var p=new Point3(x[0],x[1],x[2]);
            var v=Value(p);
            var gx=(Value(p+new Point3(h,0,0))-Value(p-new Point3(h,0,0)))/(2*h);
            var gy=(Value(p+new Point3(0,h,0))-Value(p-new Point3(0,h,0)))/(2*h);
            var gz=(Value(p+new Point3(0,0,h))-Value(p-new Point3(0,0,h)))/(2*h);
            return FunctionalJet.FromValueGradient(v,new[]{gx,gy,gz});
        });
        return FunctionalVoxelModel.Sample(f,Grid);
    }
}
