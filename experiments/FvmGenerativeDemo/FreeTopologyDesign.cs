using System.Globalization;
using System.Text.Json;
using DCad.FunctionalVoxel;

namespace FvmGenerativeDemo;

// Free field-based topology: one material variable per FVM cell, not one radius per
// route. The scalar field defines both the 5 M images and immersed Hex8 physics.
// Adjoint gradients are back-propagated through Hermite M reconstruction,
// multilinear control interpolation AND normalized density filter.
internal sealed class FreeTopologyField
{
    internal readonly UniversalScene Scene;
    internal readonly FunctionalGrid Grid;
    internal readonly double[] Controls,Filtered;
    internal readonly bool[] Locked;
    internal readonly double Eps;
    private readonly double _h;
    private readonly Point3 _size;
    internal int Variables => Controls.Length;

    internal FreeTopologyField(UniversalScene scene,double[]? controls=null)
    {
        Scene=scene;
        _size=scene.Max-scene.Min;
        var count=new[]{(int)Math.Ceiling(_size.X/scene.FvmStep),
            (int)Math.Ceiling(_size.Y/scene.FvmStep),(int)Math.Ceiling(_size.Z/scene.FvmStep)};
        Grid=new FunctionalGrid(new[]{scene.Min.X,scene.Min.Y,scene.Min.Z},
            new[]{_size.X/count[0],_size.Y/count[1],_size.Z/count[2]},count);
        Eps=Math.Max(.75,scene.FvmStep*.5);
        _h=.07*scene.FvmStep;
        Controls=controls is null?Enumerable.Repeat(.90,Grid.CellCount).ToArray():(double[])controls.Clone();
        if(Controls.Length!=Grid.CellCount)throw new ArgumentException("Incorrect topology control dimensions.");
        Locked=new bool[Grid.CellCount];
        for(int i=0;i<Locked.Length;i++)
        {
            var c=Grid.Centre(i);var p=new Point3(c[0],c[1],c[2]);
            var body=scene.Bodies.Any(b=>b.Shape.F(p)>=-Grid.Step(0)*.15);
            var edge=Math.Min(Math.Min(p.X-scene.Min.X,scene.Max.X-p.X),
                Math.Min(Math.Min(p.Y-scene.Min.Y,scene.Max.Y-p.Y),
                         Math.Min(p.Z-scene.Min.Z,scene.Max.Z-p.Z)));
            var keep=scene.ObstDistance(p)<scene.AvoidSafetyMm+scene.FvmStep*.75;
            if(body||edge<1.5*scene.FvmStep||keep)
            {
                Locked[i]=true;Controls[i]=body ? .99 : .01;
            }
            else Controls[i]=Math.Clamp(Controls[i],.01,.99);
        }
        Filtered=ApplyFilter(Controls);
    }

    private void FilterStencil(int id,Action<int,double> callback)
    {
        var x=Grid.Index(id,0);var y=Grid.Index(id,1);var z=Grid.Index(id,2);
        double denom=0;
        for(int dx=-1;dx<=1;dx++)for(int dy=-1;dy<=1;dy++)for(int dz=-1;dz<=1;dz++)
        {
            int xx=x+dx,yy=y+dy,zz=z+dz;
            if(xx<0||yy<0||zz<0||xx>=Grid.Count(0)||yy>=Grid.Count(1)||zz>=Grid.Count(2))continue;
            denom+=(dx==0?2:1)*(dy==0?2:1)*(dz==0?2:1);
        }
        for(int dx=-1;dx<=1;dx++)for(int dy=-1;dy<=1;dy++)for(int dz=-1;dz<=1;dz++)
        {
            int xx=x+dx,yy=y+dy,zz=z+dz;
            if(xx<0||yy<0||zz<0||xx>=Grid.Count(0)||yy>=Grid.Count(1)||zz>=Grid.Count(2))continue;
            var w=(dx==0?2:1)*(dy==0?2:1)*(dz==0?2:1)/denom;
            callback(Grid.LinearIndex(xx,yy,zz),w);
        }
    }
    private double[] ApplyFilter(double[] x)
    {
        var a=new double[x.Length];
        for(int i=0;i<a.Length;i++)
        {
            var sum=0.0;
            FilterStencil(i,(id,w)=>sum+=w*x[id]);
            a[i]=sum;
        }
        return a;
    }
    private double[] FilterTranspose(double[] filteredGradient)
    {
        var a=new double[Variables];
        for(int i=0;i<Variables;i++)
            if(filteredGradient[i]!=0)
                FilterStencil(i,(id,w)=>a[id]+=w*filteredGradient[i]);
        return a;
    }

    // Exact linear derivative of interpolated filtered controls, including
    // clamped boundary coordinates. No nearest-cell / checkerboard Jacobian.
    internal void ControlWeights(Point3 p,Action<int,double> add)
    {
        var low=new int[3];var high=new int[3];var weight=new double[3];
        for(int a=0;a<3;a++)
        {
            double c=a==0?p.X:a==1?p.Y:p.Z;
            var u=(c-Grid.Minimum(a))/Grid.Step(a)-.5;
            u=Math.Clamp(u,0,Grid.Count(a)-1);
            low[a]=(int)Math.Floor(u);high[a]=Math.Min(low[a]+1,Grid.Count(a)-1);
            weight[a]=u-low[a];
        }
        for(int mask=0;mask<8;mask++)
        {
            double w=1;var ids=new int[3];
            for(int a=0;a<3;a++)
            {
                bool upper=(mask&(1<<a))!=0;
                ids[a]=upper?high[a]:low[a];
                w*=upper?weight[a]:1-weight[a];
            }
            if(w>0)add(Grid.LinearIndex(ids),w);
        }
    }
    // Apply the Heaviside projection AFTER spatial interpolation, exactly
    // as the FEM material field sees it, not once at independent lattice
    // centres (which would have a different Jacobian).
    private double FilteredDensity(Point3 p)
    {
        double density=0;
        ControlWeights(p,(id,w)=>density+=w*Filtered[id]);
        return density;
    }
    private double Project(double filtered)
    {
        var beta=Scene.ProjectionBeta;
        if(beta==0)return filtered;
        var reference=Math.Tanh(beta*.5);
        return (reference+Math.Tanh(beta*(filtered-.5)))/(2*reference);
    }
    private double ProjectionDerivative(double filtered)
    {
        var beta=Scene.ProjectionBeta;
        if(beta==0)return 1;
        var t=Math.Tanh(beta*(filtered-.5));
        return beta*(1-t*t)/(2*Math.Tanh(beta*.5));
    }
    private double Density(Point3 p)=>Project(FilteredDensity(p));
    internal double ProjectedAtCentre(int id)=>Project(Filtered[id]);
    private double BaseMaterial(Point3 p)
    {
        var value=double.NegativeInfinity;
        foreach(var b in Scene.Bodies)
        {
            var f=b.Shape.F(p);
            // Fully occupied protected bodies: no optimizer freedom here.
            if(f>0)f=Math.Max(f,Eps);
            value=Math.Max(value,f);
        }
        return value;
    }
    private double Boundary(Point3 p)
    {
        var gap=Math.Min(Math.Min(p.X-Scene.Min.X,Scene.Max.X-p.X),
            Math.Min(Math.Min(p.Y-Scene.Min.Y,Scene.Max.Y-p.Y),
                Math.Min(p.Z-Scene.Min.Z,Scene.Max.Z-p.Z)));
        return gap-Eps*1.20;
    }
    internal double Value(Point3 p)
    {
        var f=2*Eps*(Density(p)-.5);
        f=Math.Max(f,BaseMaterial(p));
        f=Math.Min(f,Boundary(p));
        foreach(var o in Scene.Obstacles)
            f=Math.Min(f,-o.Shape.F(p)-o.Clearance-Scene.AvoidSafetyMm-0.20*Scene.FvmStep);
        // Keep loaded STL faces physically exposed. The removed region is
        // an exterior contact-access passage, NOT removable protected body.
        f=Math.Min(f,Scene.PressureContactVoid(p,Scene.FvmStep*.35));
        return f;
    }
    private bool DesignIsActive(Point3 p)
    {
        var top=2*Eps*(Density(p)-.5);
        if(BaseMaterial(p)>=top||Boundary(p)<=top)return false;
        foreach(var o in Scene.Obstacles)
            if(-o.Shape.F(p)-o.Clearance-Scene.AvoidSafetyMm-0.20*Scene.FvmStep<=top)return false;
        if(Scene.PressureContactVoid(p,Scene.FvmStep*.35)<=top)return false;
        return true;
    }
    internal FunctionalVoxelModel Sample()
    {
        var field=FunctionalField.Analytic(3,point=>
        {
            var p=new Point3(point[0],point[1],point[2]);
            var value=Value(p);
            var gx=(Value(p+new Point3(_h,0,0))-Value(p-new Point3(_h,0,0)))/(2*_h);
            var gy=(Value(p+new Point3(0,_h,0))-Value(p-new Point3(0,_h,0)))/(2*_h);
            var gz=(Value(p+new Point3(0,0,_h))-Value(p-new Point3(0,0,_h)))/(2*_h);
            return FunctionalJet.FromValueGradient(value,new[]{gx,gy,gz});
        });
        return FunctionalVoxelModel.Sample(field,Grid);
    }
    private void FieldTranspose(Point3 p,double scale,double[] gradient)
    {
        if(scale==0||!DesignIsActive(p))return;
        var factor=ProjectionDerivative(FilteredDensity(p));
        ControlWeights(p,(id,w)=>gradient[id]+=scale*2*Eps*factor*w);
    }
    // Adjoint chain through M interpolation and all 3 sampled gradients.
    // ReconstructJet is an affine tangent at each sampled centre, despite
    // homogeneous normal normalization of the stored five M planes.
    internal double[] BackpropagateM(IReadOnlyList<(Point3 Point,double Coefficient)> gauss)
    {
        var smoothGradient=new double[Variables];
        foreach(var (point,coefficient) in gauss)
        {
            if(coefficient==0)continue;
            var lo=new int[3];var hi=new int[3];var t=new double[3];
            for(int a=0;a<3;a++)
            {
                var c=a==0?point.X:a==1?point.Y:point.Z;
                var x=(c-Grid.Minimum(a))/Grid.Step(a)-.5;
                x=Math.Clamp(x,0,Grid.Count(a)-1);
                lo[a]=(int)Math.Floor(x);hi[a]=Math.Min(lo[a]+1,Grid.Count(a)-1);
                t[a]=x-lo[a];
            }
            for(int corner=0;corner<8;corner++)
            {
                var idx=new int[3];var w=coefficient;
                for(int a=0;a<3;a++)
                {
                    bool upper=(corner&(1<<a))!=0;
                    idx[a]=upper?hi[a]:lo[a];
                    w*=upper?t[a]:1-t[a];
                }
                if(w==0)continue;
                var centre=Grid.Centre(Grid.LinearIndex(idx));
                var p=new Point3(centre[0],centre[1],centre[2]);
                FieldTranspose(p,w,smoothGradient);
                var offset=point-p;
                foreach(var (positive,negative,d) in new[]{
                    (new Point3(_h,0,0),new Point3(-_h,0,0),offset.X),
                    (new Point3(0,_h,0),new Point3(0,-_h,0),offset.Y),
                    (new Point3(0,0,_h),new Point3(0,0,-_h),offset.Z)})
                {
                    if(d==0)continue;
                    var slope=w*d/(2*_h);
                    FieldTranspose(p+positive,slope,smoothGradient);
                    FieldTranspose(p+negative,-slope,smoothGradient);
                }
            }
        }
        var raw=FilterTranspose(smoothGradient);
        for(int i=0;i<Variables;i++)if(Locked[i])raw[i]=0;
        return raw;
    }
    internal int MaterialCentreCount(FunctionalVoxelModel model)
    {
        int count=0;
        for(int i=0;i<Grid.CellCount;i++)if(model.ValueAtCentre(i)>0)count++;
        return count;
    }
}

internal static class FreeTopologyDesign
{
    private sealed record Case(string Name,HexFem Fem,FemResults Result);
    private sealed class State
    {
        internal required FreeTopologyField Field;
        internal required FunctionalVoxelModel M;
        internal required Case[] Cases;
        internal required bool FromM;
        internal double Volume=>Cases[0].Result.VolumeMm3;
        internal double Compliance=>Cases.Average(x=>x.Result.ComplianceNmm);
        internal double Stress=>Cases.Max(x=>x.Result.MaxVonMisesMpa);
        internal double Displacement=>Cases.Max(x=>x.Result.MaxDisplacementMm);
        internal double FirstHz=double.NaN,ImpactMm=double.NaN,ImpactStress=double.NaN;
        internal double Thermal=double.NaN,ThermalStress=double.NaN,ThermalU=double.NaN;
        internal double Harmonic=double.NaN,Fatigue=double.NaN;
    }
    private static void Check(bool p,string message)
    {
        if(!p)throw new InvalidOperationException("FREE TOPOLOGY FAIL: "+message);
    }
    private static string Arg(string[] a,string key,string fallback)
    {
        for(int i=0;i<a.Length-1;i++)if(a[i]==key)return a[i+1];
        return fallback;
    }
    private static bool Connected(State s,UniversalScene scene)
    {
        var g=s.Field.Grid;var solid=new bool[g.CellCount];
        var seeds=new Queue<int>();var visited=new bool[g.CellCount];
        var root=scene.Bodies.First(b=>b.Role=="fixed");
        for(int i=0;i<solid.Length;i++)
        {
            var x=g.Centre(i);var p=new Point3(x[0],x[1],x[2]);
            solid[i]=(s.FromM?s.M.ValueAtCentre(i):s.Field.Value(p))>0;
            if(solid[i]&&root.Shape.F(p)>=0){seeds.Enqueue(i);visited[i]=true;}
        }
        if(seeds.Count==0)return false;
        var found=new HashSet<string>();
        while(seeds.Count>0)
        {
            int id=seeds.Dequeue();var c=g.Centre(id);
            var p=new Point3(c[0],c[1],c[2]);
            foreach(var body in scene.Bodies)if(body.Shape.F(p)>=0)found.Add(body.Id);
            for(int a=0;a<3;a++)for(int delta=-1;delta<=1;delta+=2)
            {
                var ind=new[]{g.Index(id,0),g.Index(id,1),g.Index(id,2)};
                ind[a]+=delta;
                if(ind[a]<0||ind[a]>=g.Count(a))continue;
                int next=g.LinearIndex(ind);
                if(solid[next]&&!visited[next]){visited[next]=true;seeds.Enqueue(next);}
            }
        }
        return found.Count==scene.Bodies.Count;
    }
    private static bool Feasible(State s,UniversalScene scene,double maxCompliance)
    {
        if(!double.IsFinite(s.Volume)||!double.IsFinite(s.Compliance)||
           !Connected(s,scene)||
           s.Stress>scene.StressLimit||s.Displacement>scene.DisplacementLimit||
           s.Compliance>maxCompliance)return false;
        if(scene.MinFrequencyHz>0&&!(s.FirstHz>=scene.MinFrequencyHz))return false;
        if(double.IsFinite(scene.MaxImpactDisplacementMm)&&
           !(s.ImpactMm<=scene.MaxImpactDisplacementMm))return false;
        if(double.IsFinite(scene.MaxImpactVonMisesMpa)&&
           !(s.ImpactStress<=scene.MaxImpactVonMisesMpa))return false;
        if(scene.MinThermalConductanceWPerK>0&&
           !(s.Thermal>=scene.MinThermalConductanceWPerK))return false;
        if(scene.ThermalExpansionPerK>0&&
           !(s.ThermalStress<=scene.MaxThermalStressMpa&&s.ThermalU<=scene.MaxThermalDisplacementMm))
           return false;
        if(scene.HarmonicFrequencyHz>=0&&
           !(s.Harmonic<=scene.MaxHarmonicDisplacementMm))return false;
        if(scene.FatigueCycles>0&&!(s.Fatigue<=scene.MaxFatigueDamage))return false;
        return true;
    }

    internal static int Run(string[] args)
    {
        var path=Arg(args,"--free-topology","");
        if(string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Usage --free-topology scene.json");
        var scene=UniversalScene.Load(path);
        if(args.Contains("--require-mesh-convergence")&&
           !args.Contains("--refinement-report"))
            throw new ArgumentException(
                "--require-mesh-convergence requires --refinement-report.");
        int steps=int.Parse(Arg(args,"--iterations",scene.Iterations.ToString(CultureInfo.InvariantCulture)),
            CultureInfo.InvariantCulture);
        if(steps<1||steps>40)throw new ArgumentException("Iterations must be 1..40");
        var outDir=Arg(args,"--out","output/free-topology");
        Directory.CreateDirectory(outDir);
        var origin=scene.Min;var size=scene.Max-scene.Min;
        var nx=scene.FemCells[0];var ny=scene.FemCells[1];var nz=scene.FemCells[2];
        double hx=0,hy=0,hz=0;
        var nodes=new List<Point3>();
        bool Fixed(double x,double y,double z)
        {
            var p=new Point3(x,y,z);
            return scene.Bodies.Any(b=>b.Role=="fixed"&&b.Shape.F(p)>=0);
        }
        Dictionary<string,int> counts=new();
        // Small mandatory fixtures cannot be silently lost by a coarse
        // background Hex8 mesh. Refine it automatically until every fixture
        // and loaded region has enough nodes for meaningful FEM boundary data.
        for(int pass=0;pass<5;pass++)
        {
            hx=size.X/nx;hy=size.Y/ny;hz=size.Z/nz;
            nodes.Clear();
            for(int i=0;i<=nx;i++)for(int j=0;j<=ny;j++)for(int k=0;k<=nz;k++)
                nodes.Add(new Point3(origin.X+i*hx,origin.Y+j*hy,origin.Z+k*hz));
            counts=scene.Bodies.ToDictionary(b=>b.Id,
                b=>nodes.Count(p=>b.Shape.F(p)>=0));
            if(counts.Values.All(n=>n>=2)&&
                nodes.Count(p=>Fixed(p.X,p.Y,p.Z))>=3)break;
            Check(pass<4&&nx<25&&ny<25&&nz<25,
                "FEM background grid cannot resolve a prescribed fixture within 25 cells/axis.");
            nx=Math.Min(25,(int)Math.Ceiling(nx*1.30)+1);
            ny=Math.Min(25,(int)Math.Ceiling(ny*1.30)+1);
            nz=Math.Min(25,(int)Math.Ceiling(nz*1.30)+1);
            Console.WriteLine($"AUTO FEM REFINEMENT: fixture masks need more nodes; nx={nx} ny={ny} nz={nz}");
        }
        foreach(var (name,forces) in scene.LoadCases)
            foreach(var (id,v) in forces)
            {
                var body=scene.Bodies.First(b=>b.Id==id);
                Check(!nodes.Any(p=>Fixed(p.X,p.Y,p.Z)&&body.Shape.F(p)>=0),
                    $"Load '{name}/{id}' overlaps fixed nodes");
            }
        var start=new FreeTopologyField(scene);
        int free=start.Locked.Count(x=>!x);
        Check(free>=24,"FVM design domain too small for free topology optimization.");

        State Analyze(double[] density,bool useM,bool additional)
        {
            var field=new FreeTopologyField(scene,density);
            var model=field.Sample();
            // Fail closed for free-density designs when an STL traction
            // patch is not on an actual exposed material/void interface.
            // Background rho=0.9 may otherwise bury a valid SOURCE-CAD face.
            if(!scene.PressureSurfacesExposed(q=>
                useM?model.Approximate(new[]{q.X,q.Y,q.Z}):field.Value(q),
                Math.Max(.5,scene.FvmStep*.5)))
                throw new ArithmeticException(
                    "FREE FEM STL pressure face is buried in material; "+
                    "an exposed physical interface is required.");
            var cases=new List<Case>();
            foreach(var (name,load) in scene.LoadCases)
            {
                (double X,double Y,double Z) NodalLoad(double x,double y,double z)
                {
                    var p=new Point3(x,y,z);var sum=new Point3(0,0,0);
                    foreach(var (id,value) in load)
                    {
                        var body=scene.Bodies.First(b=>b.Id==id);
                        if(body.Shape.F(p)>=0)sum+=value*(1.0/counts[id]);
                    }
                    return(sum.X,sum.Y,sum.Z);
                }
                var fem=new HexFem(nx,ny,nz,origin.X,origin.Y,origin.Z,
                    hx,hy,hz,scene.Young,scene.Poisson,
                    (x,y,z)=>field.Value(new Point3(x,y,z)),useM?model:null,
                    loadN:0,interfaceHalfWidth:0,emin:1e-4,epsF:field.Eps,
                    support:Fixed,nodalForce:NodalLoad,
                    densityKgMm3:scene.DensityKgMm3,simpPower:3,
                    trianglePressures:scene.PressureFor(name));
                var solved=fem.Solve(maxIterations:3000);
                Check(solved.RelativeEquilibriumResidual<1e-6,
                    $"FEM residual {name}");
                var total=scene.AppliedForce(name,load);
                foreach(var (force,react) in new[]{
                    (total.X,solved.ReactionXN),
                    (total.Y,solved.ReactionYN),
                    (total.Z,solved.ReactionZN)})
                    Check(Math.Abs(force+react)<Math.Max(.1,Math.Abs(force)*.001),
                        $"FEM global reaction balance {name}");
                cases.Add(new Case(name,fem,solved));
            }
            var state=new State{Field=field,M=model,Cases=cases.ToArray(),FromM=useM};
            if(additional)
            {
                if(scene.MinFrequencyHz>0||scene.HarmonicFrequencyHz>=0)
                    state.FirstHz=cases[0].Fem.FirstNaturalFrequency(scene.DensityKgMm3).FrequencyHz;
                if(scene.HarmonicFrequencyHz>=0)
                    state.Harmonic=cases.Max(c=>c.Fem.HarmonicFirstModeMaxDisplacement(
                        scene.DensityKgMm3,scene.HarmonicFrequencyHz,scene.DampingRatio));
                if(double.IsFinite(scene.MaxImpactDisplacementMm)||
                   double.IsFinite(scene.MaxImpactVonMisesMpa))
                {
                    var p=cases.Select(c=>c.Fem.SolvePulse(scene.DensityKgMm3,
                        scene.ImpactPulseSeconds,scene.ImpactMultiplier,scene.DampingRatio)).ToArray();
                    state.ImpactMm=p.Max(q=>q.MaxDisplacementMm);
                    state.ImpactStress=p.Max(q=>q.MaxVonMisesMpa);
                }
                if(scene.MinThermalConductanceWPerK>0||scene.ThermalExpansionPerK>0)
                {
                    var hot=scene.Bodies.First(b=>b.Id==scene.ThermalHotId);
                    var cold=scene.Bodies.First(b=>b.Id==scene.ThermalColdId);
                    var temp=cases[0].Fem.ThermalConductance(scene.ThermalConductivityWmK,
                        (x,y,z)=>hot.Shape.F(new Point3(x,y,z))>=0,
                        (x,y,z)=>cold.Shape.F(new Point3(x,y,z))>=0);
                    state.Thermal=temp.ConductanceWPerK;
                    if(scene.ThermalExpansionPerK>0)
                    {
                        var t=cases[0].Fem.SolveThermoElastic(temp,
                            scene.ThermalExpansionPerK,scene.ThermalColdK,
                            scene.ThermalHotK,scene.ThermalReferenceK);
                        Check(t.RelativeResidual<1e-5,"Thermoelastic residual");
                        state.ThermalStress=t.MaxVonMisesMpa;
                        state.ThermalU=t.MaxDisplacementMm;
                    }
                }
                if(scene.FatigueCycles>0)
                {
                    var n=scene.FatigueReferenceCycles*Math.Pow(
                        scene.FatigueReferenceStressMpa/Math.Max(1e-9,state.Stress),
                        scene.FatigueExponent);
                    state.Fatigue=scene.FatigueCycles/n;
                }
            }
            return state;
        }

        // Mandatory manufacturing sanity check: mechanics of a binary
        // solid/void reconstruction (no SIMP grey interpolation). Use the
        // SAME actual 5M field whose zero-isosurface is exported to STL.
        // A successful continuous-density solve is NOT enough by itself.
        (double Volume,double Stress,double Displacement,double Compliance,bool AdditionalPass) BinaryCheck(State state)
        {
            var shape=state.M;
            double volume=0,stress=0,displacement=0,compliance=0;
            bool additionalPass=true;
            int caseIndex=0;
            foreach(var (name,loads) in scene.LoadCases)
            {
                (double X,double Y,double Z) Load(double x,double y,double z)
                {
                    var p=new Point3(x,y,z);var f=new Point3(0,0,0);
                    foreach(var (id,v) in loads)
                    {
                        var b=scene.Bodies.First(q=>q.Id==id);
                        if(b.Shape.F(p)>=0)f+=v*(1.0/counts[id]);
                    }
                    return (f.X,f.Y,f.Z);
                }
                var fem=new HexFem(nx,ny,nz,origin.X,origin.Y,origin.Z,hx,hy,hz,
                    scene.Young,scene.Poisson,
                    (x,y,z)=>shape.Approximate(new[]{x,y,z})>=0?
                        state.Field.Eps*1.01:-state.Field.Eps*1.01,
                    model:null,loadN:0,interfaceHalfWidth:0,emin:1e-4,
                    epsF:state.Field.Eps,support:Fixed,nodalForce:Load,
                    densityKgMm3:scene.DensityKgMm3,simpPower:1,
                    trianglePressures:scene.PressureFor(name));
                var r=fem.Solve(maxIterations:3000);
                Check(r.RelativeEquilibriumResidual<1e-6,
                    "Binary FEM numerical residual: "+name);
                if(volume==0)volume=r.VolumeMm3;
                stress=Math.Max(stress,r.MaxVonMisesMpa);
                displacement=Math.Max(displacement,r.MaxDisplacementMm);
                compliance+=r.ComplianceNmm/scene.LoadCases.Count;
                // Revalidate enabled dynamics and thermal limits for the
                // BINARY model, not only its continuously gray SIMP proxy.
                if(scene.MinFrequencyHz>0)
                    additionalPass &= fem.FirstNaturalFrequency(
                        scene.DensityKgMm3).FrequencyHz>=scene.MinFrequencyHz;
                if(scene.HarmonicFrequencyHz>=0)
                    additionalPass &= fem.HarmonicFirstModeMaxDisplacement(
                        scene.DensityKgMm3,scene.HarmonicFrequencyHz,
                        scene.DampingRatio)<=scene.MaxHarmonicDisplacementMm;
                if(double.IsFinite(scene.MaxImpactDisplacementMm)||
                   double.IsFinite(scene.MaxImpactVonMisesMpa))
                {
                    var impulse=fem.SolvePulse(scene.DensityKgMm3,
                        scene.ImpactPulseSeconds,scene.ImpactMultiplier,
                        scene.DampingRatio);
                    additionalPass &= impulse.MaxDisplacementMm<=scene.MaxImpactDisplacementMm;
                    additionalPass &= impulse.MaxVonMisesMpa<=scene.MaxImpactVonMisesMpa;
                }
                if(caseIndex==0&&(scene.MinThermalConductanceWPerK>0||
                                  scene.ThermalExpansionPerK>0))
                {
                    var hot=scene.Bodies.First(b=>b.Id==scene.ThermalHotId);
                    var cold=scene.Bodies.First(b=>b.Id==scene.ThermalColdId);
                    var thermal=fem.ThermalConductance(
                        scene.ThermalConductivityWmK,
                        (x,y,z)=>hot.Shape.F(new Point3(x,y,z))>=0,
                        (x,y,z)=>cold.Shape.F(new Point3(x,y,z))>=0);
                    additionalPass &= thermal.ConductanceWPerK>=scene.MinThermalConductanceWPerK;
                    if(scene.ThermalExpansionPerK>0)
                    {
                        var thermo=fem.SolveThermoElastic(thermal,
                            scene.ThermalExpansionPerK,scene.ThermalColdK,
                            scene.ThermalHotK,scene.ThermalReferenceK);
                        additionalPass &= thermo.RelativeResidual<1e-5;
                        additionalPass &= thermo.MaxVonMisesMpa<=scene.MaxThermalStressMpa;
                        additionalPass &= thermo.MaxDisplacementMm<=scene.MaxThermalDisplacementMm;
                    }
                }
                caseIndex++;
            }
            if(scene.FatigueCycles>0)
            {
                var cycles=scene.FatigueReferenceCycles*Math.Pow(
                    scene.FatigueReferenceStressMpa/Math.Max(stress,1e-9),
                    scene.FatigueExponent);
                additionalPass &= scene.FatigueCycles/cycles<=scene.MaxFatigueDamage;
            }
            return (volume,stress,displacement,compliance,additionalPass);
        }
        bool BinaryFeasible(State state,double maxAllowedCompliance)
        {
            var binary=BinaryCheck(state);
            return binary.Stress<=scene.StressLimit &&
                binary.Displacement<=scene.DisplacementLimit &&
                binary.Compliance<=maxAllowedCompliance &&
                binary.AdditionalPass;
        }

        var initial=Analyze(start.Controls,true,true);
        var initialDirect=Analyze(start.Controls,false,true);
        var maxCompliance=initial.Compliance*2.5;
        void Diagnostics(string name,State st)
        {
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "FREE PHYSICS {0}: M={1} VM={2:F3} u={3:F6} C={4:F5} connected={5} f1={6:G7}Hz impulse={7:G7}mm/{8:G7}MPa thermal={9:G7}W/K thermo={10:G7}MPa/{11:G7}mm harmonic={12:G7}mm fatigue={13:G7}",
                name,st.FromM,st.Stress,st.Displacement,st.Compliance,
                Connected(st,scene),st.FirstHz,st.ImpactMm,st.ImpactStress,
                st.Thermal,st.ThermalStress,st.ThermalU,st.Harmonic,st.Fatigue));
        }
        Diagnostics("initial-five-M",initial);
        Diagnostics("initial-direct-F",initialDirect);
        Check(Feasible(initial,scene,maxCompliance)&&Feasible(initialDirect,scene,maxCompliance),
            "Initial design fails physics, connectivity, or selected load limits; see FREE PHYSICS.");
        var current=initial;
        int accepted=0,nucleated=0;
        var metrics=new List<string>{"iteration,kind,volume_mm3,compliance_nmm,von_mises_mpa,displacement_mm,solid_m_centres,accepted"};
        void Report(int iteration,string kind,State s)
        {
            string d(double x)=>x.ToString("G17",CultureInfo.InvariantCulture);
            metrics.Add(string.Join(",",new[]{
                iteration.ToString(CultureInfo.InvariantCulture),kind,d(s.Volume),
                d(s.Compliance),d(s.Stress),d(s.Displacement),
                s.Field.MaterialCentreCount(s.M).ToString(CultureInfo.InvariantCulture),"true"}));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "FREE {0} {1}: V={2:F2}mm3 compliance={3:F3} Nmm VM={4:F2}MPa u={5:F5}mm solidCells={6}",
                iteration,kind,s.Volume,s.Compliance,s.Stress,s.Displacement,
                s.Field.MaterialCentreCount(s.M)));
        }
        Report(0,"initial",current);
        // Keep an unchanged initial STL for comparison against the resulting
        // independent free-field topological design.
        TwoSphereDesign.SphereOutputs.WriteStl(Path.Combine(outDir,"initial.stl"),initial.M,null);
        for(int iteration=1;iteration<=steps;iteration++)
        {
            var gC=new double[current.Field.Variables];
            var gV=new double[gC.Length];
            for(int loadIndex=0;loadIndex<current.Cases.Length;loadIndex++)
            {
                var c=current.Cases[loadIndex];
                var q=c.Fem.GaussSensitivities(c.Result);
                var compliance=current.Field.BackpropagateM(q.Select(x=>
                    (new Point3(x.X,x.Y,x.Z),x.DComplianceDField/current.Cases.Length)).ToArray());
                for(int i=0;i<gC.Length;i++)gC[i]+=compliance[i];
                if(loadIndex==0)
                    gV=current.Field.BackpropagateM(q.Select(x=>
                        (new Point3(x.X,x.Y,x.Z),x.DVolumeDField)).ToArray());
            }
            if(iteration==1 && args.Contains("--gradient-check"))
            {
                // Full finite difference of the exact FIVE-M/FEM objective,
                // not just a difference of fields or element stiffness.
                // Gradient is ∂compliance/∂raw_control through filter,
                // local M hyperplanes and the equilibrium solve.
                // Three spatially separated controls catch indexing, filter
                // transpose and x/y/z local tangent-plane sign errors which
                // a single high-sensitivity point might not expose.
                var picked=new List<int>();
                var grid=current.Field.Grid;
                foreach(var index in Enumerable.Range(0,gC.Length)
                    .Where(i=>!current.Field.Locked[i]&&
                        Math.Abs(gV[i])>1e-5&&Math.Abs(gC[i])>1e-8)
                    .OrderByDescending(i=>Math.Abs(gC[i])))
                {
                    if(picked.All(previous=>
                        Math.Abs(grid.Index(index,0)-grid.Index(previous,0))+
                        Math.Abs(grid.Index(index,1)-grid.Index(previous,1))+
                        Math.Abs(grid.Index(index,2)-grid.Index(previous,2))>=3))
                        picked.Add(index);
                    if(picked.Count==3)break;
                }
                Check(picked.Count==3,
                    "Not enough spatially separated active topology controls for rigorous FEM gradient checks.");
                const double delta=.002;
                foreach(var index in picked)
                {
                    var plus=(double[])current.Field.Controls.Clone();
                    var minus=(double[])current.Field.Controls.Clone();
                    plus[index]+=delta;minus[index]-=delta;
                    var high=Analyze(plus,true,false);
                    var low=Analyze(minus,true,false);
                    var numericalC=(high.Compliance-low.Compliance)/(2*delta);
                    var numericalV=(high.Volume-low.Volume)/(2*delta);
                    var errorC=Math.Abs(gC[index]-numericalC)/
                        Math.Max(1e-8,Math.Abs(numericalC));
                    var errorV=Math.Abs(gV[index]-numericalV)/
                        Math.Max(1e-8,Math.Abs(numericalV));
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "FREE GRADIENT: index={0} dC analytic={1:G12}, FD={2:G12}, rel={3:P3}, dV analytic={4:G12}, FD={5:G12}, rel={6:P3}",
                        index,gC[index],numericalC,errorC,gV[index],numericalV,errorV));
                    Check(errorC<.12&&errorV<.08,
                        $"FVM five-M adjoint failed gradient at cell {index}: {errorC:P3}/{errorV:P3}");
                }
            }
            // Generalized OC update: minimize SIMP compliance with material
            // budget schedule. Unlike routing-radii gradients, there is one
            // variable for every non-protected M control cell.
            var old=current.Field.Controls;
            var targetMass=old.Sum()-free*.075;
            var move=.20;
            var candidate=new double[old.Length];
            var lo=1e-12;var hi=1e12;
            for(int k=0;k<65;k++)
            {
                var lambda=Math.Sqrt(lo*hi);
                for(int i=0;i<candidate.Length;i++)
                {
                    if(current.Field.Locked[i]){candidate[i]=old[i];continue;}
                    var ratio=Math.Max(1e-12,-gC[i]/
                        Math.Max(1e-9,gV[i]*lambda));
                    candidate[i]=Math.Clamp(old[i]*Math.Sqrt(ratio),
                        Math.Max(.01,old[i]-move),
                        Math.Min(.99,old[i]+move));
                }
                if(candidate.Sum()>targetMass)lo=lambda;else hi=lambda;
            }
            bool found=false;
            for(double blend=1;blend>=.0625;blend*=.5)
            {
                var trialControls=new double[old.Length];
                for(int i=0;i<old.Length;i++)
                    trialControls[i]=old[i]+blend*(candidate[i]-old[i]);
                try
                {
                    var trial=Analyze(trialControls,true,true);
                    if(trial.Volume>current.Volume-.15||!Feasible(trial,scene,maxCompliance))continue;
                    var direct=Analyze(trialControls,false,true);
                    if(!Feasible(direct,scene,maxCompliance))continue;
                    if(!BinaryFeasible(trial,maxCompliance))continue;
                    current=trial;accepted++;found=true;
                    Report(iteration,"OC",trial);break;
                }
                catch(ArithmeticException){/* failed FEM = rejected step */ }
            }
            if(!found)
            {
                Console.WriteLine("FREE STOP: OC cannot find a lighter fully feasible topology.");
                break;
            }

            // Explicit topology change: a low-energy 3x3x3 patch is allowed
            // to turn into a void, even if no pre-existing routing path covers
            // it. Only its FEM / CSG / multi-physics-validated result is kept.
            // This is actual void nucleation, not thinning a predefined beam.
            var choices=Enumerable.Range(0,current.Field.Variables)
                // Select low-energy exterior-accessible removal zones first:
                // buried voids would create an unvented secondary cavity STL
                // shell, whereas a topology opening through the exterior is
                // unambiguously printable and preserves one closed boundary.
                .Where(i=>
                {
                    if(current.Field.Locked[i])return false;
                    var g=current.Field.Grid;
                    var x=g.Index(i,0);var y=g.Index(i,1);var z=g.Index(i,2);
                    if(x<2||y<2||z<2||x>g.Count(0)-3||
                       y>g.Count(1)-3||z>g.Count(2)-3)return false;
                    return x==2||y==2||z==2||
                        x==g.Count(0)-3||y==g.Count(1)-3||z==g.Count(2)-3;
                })
                .OrderBy(i=>Math.Abs(gC[i])/(Math.Abs(gV[i])+1e-8))
                .Take(8).ToArray();
            foreach(var centre in choices)
            {
                var g=current.Field.Grid;
                var x=g.Index(centre,0);var y=g.Index(centre,1);var z=g.Index(centre,2);
                var nucleatedControls=(double[])current.Field.Controls.Clone();
                int n=0;
                for(int dx=-1;dx<=1;dx++)for(int dy=-1;dy<=1;dy++)
                    for(int dz=-1;dz<=1;dz++)
                    {
                        var id=g.LinearIndex(x+dx,y+dy,z+dz);
                        if(current.Field.Locked[id])continue;
                        nucleatedControls[id]=.01;n++;
                    }
                if(n<20)continue;
                try
                {
                    var trial=Analyze(nucleatedControls,true,true);
                    if(trial.Field.Value(new Point3(g.Centre(centre)[0],
                        g.Centre(centre)[1],g.Centre(centre)[2]))>=0)continue;
                    if(trial.Volume>current.Volume-.15||!Feasible(trial,scene,maxCompliance))continue;
                    var direct=Analyze(nucleatedControls,false,true);
                    if(!Feasible(direct,scene,maxCompliance))continue;
                    if(!BinaryFeasible(trial,maxCompliance))continue;
                    current=trial;accepted++;nucleated++;
                    Report(iteration,"new-void",trial);break;
                }
                catch(ArithmeticException){/* reject */ }
            }
        }
        var finalDirect=Analyze(current.Field.Controls,false,true);
        Check(Feasible(current,scene,maxCompliance)&&Feasible(finalDirect,scene,maxCompliance),
            "Final FEM/multi-physics or direct F violates constraints.");
        var finalBinary=BinaryCheck(current);
        Check(finalBinary.Stress<=scene.StressLimit&&
            finalBinary.Displacement<=scene.DisplacementLimit&&
            finalBinary.Compliance<=maxCompliance&&finalBinary.AdditionalPass,
            "Exported binary field violates final static or multi-physics restrictions.");
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "BINARY FEM PASS: V={0:F2} mm3, VM={1:F3} MPa, u={2:F6} mm, C={3:F5} Nmm (actual 5M material/void classification)",
            finalBinary.Volume,finalBinary.Stress,finalBinary.Displacement,finalBinary.Compliance));
        // Three nested Hex8 levels for the same printed five-M sign geometry.
        // Solves on a new FEM grid reveal discretization drift and potentially
        // missed stress peaks. They are diagnostics, NOT certification.
        (double Volume,double Stress,double Displacement)? refined=null;
        bool? meshConverged=null;
        if(args.Contains("--refinement-report"))
        {
            var levels=new List<(int[] Cells,double Volume,double Stress,double Displacement)>
            {
                (new[]{nx,ny,nz},finalBinary.Volume,finalBinary.Stress,
                    finalBinary.Displacement)
            };
            foreach(var multiplier in new[]{1.5,2.0})
            {
                int rx=Math.Min(25,(int)Math.Ceiling(nx*multiplier));
                int ry=Math.Min(25,(int)Math.Ceiling(ny*multiplier));
                int rz=Math.Min(25,(int)Math.Ceiling(nz*multiplier));
                var prev=levels[^1].Cells;
                if(rx==prev[0]&&ry==prev[1]&&rz==prev[2])continue;
                double dx=size.X/rx,dy=size.Y/ry,dz=size.Z/rz;
                var refinedNodes=new List<Point3>();
                for(int i=0;i<=rx;i++)for(int j=0;j<=ry;j++)for(int k=0;k<=rz;k++)
                    refinedNodes.Add(new Point3(origin.X+i*dx,
                        origin.Y+j*dy,origin.Z+k*dz));
                var countsOnGrid=scene.Bodies.ToDictionary(b=>b.Id,
                    b=>refinedNodes.Count(p=>b.Shape.F(p)>=0));
                Check(countsOnGrid.Values.All(v=>v>=2),
                    "Refined FEM lost required geometric fixture nodes.");
                double v=0,vm=0,u=0;int loadCaseIndex=0;
                foreach(var (name,loads) in scene.LoadCases)
                {
                    (double X,double Y,double Z) ForceRefined(double x,double y,double z)
                    {
                        var p=new Point3(x,y,z);var net=new Point3(0,0,0);
                        foreach(var (id,force) in loads)
                        {
                            var body=scene.Bodies.First(b=>b.Id==id);
                            if(body.Shape.F(p)>=0)net+=force*(1.0/countsOnGrid[id]);
                        }
                        return(net.X,net.Y,net.Z);
                    }
                    var extraFem=new HexFem(rx,ry,rz,origin.X,origin.Y,origin.Z,
                        dx,dy,dz,scene.Young,scene.Poisson,
                        (x,y,z)=>current.M.Approximate(new[]{x,y,z})>=0
                            ?current.Field.Eps*1.01:-current.Field.Eps*1.01,
                        model:null,loadN:0,interfaceHalfWidth:0,emin:1e-4,
                        epsF:current.Field.Eps,support:Fixed,nodalForce:ForceRefined,
                        densityKgMm3:scene.DensityKgMm3,simpPower:1,
                        trianglePressures:scene.PressureFor(name));
                    var answer=extraFem.Solve(maxIterations:5000);
                    Check(answer.RelativeEquilibriumResidual<1e-6,
                        "Refined binary FEM solver residual");
                    if(loadCaseIndex++==0)v=answer.VolumeMm3;
                    vm=Math.Max(vm,answer.MaxVonMisesMpa);
                    u=Math.Max(u,answer.MaxDisplacementMm);
                }
                var previous=levels[^1];
                var relativeV=100*(v/Math.Max(previous.Volume,1e-12)-1);
                var relativeVM=100*(vm/Math.Max(previous.Stress,1e-12)-1);
                var relativeU=100*(u/Math.Max(previous.Displacement,1e-12)-1);
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "MESH REFINEMENT DIAGNOSTIC: {0}x{1}x{2} -> {3}x{4}x{5}; binary V change={6:F2}%, VM change={7:F2}%, u change={8:F2}%. NOT a proof of convergence.",
                    previous.Cells[0],previous.Cells[1],previous.Cells[2],
                    rx,ry,rz,relativeV,relativeVM,relativeU));
                levels.Add((new[]{rx,ry,rz},v,vm,u));
                refined=(v,vm,u);
                // The refined shape uses the SAME M geometry and physical
                // loads, but catches stress/displacement constraint violations
                // that the original coarse-grid solve could overlook.
                Check(vm<=scene.StressLimit&&u<=scene.DisplacementLimit,
                    "More detailed binary Hex8 violates user-specified strength or deflection limits.");
            }
            var coarse=levels[0];
            var last=levels[^1];
            var previousLevel=levels.Count>1?levels[^2]:coarse;
            var stressChange=100*(last.Stress/Math.Max(1e-12,previousLevel.Stress)-1);
            var displacementChange=100*(last.Displacement/
                Math.Max(1e-12,previousLevel.Displacement)-1);
            var volumeChange=100*(last.Volume/Math.Max(1e-12,previousLevel.Volume)-1);
            // Requiring only the LAST step to be small can falsely pass
            // under oscillatory/discontinuous cut-cell stress sampling. A
            // 57% first jump followed by a 3% second jump is NOT enough
            // to establish any stability of the initial design stress.
            var transitions=Enumerable.Range(1,levels.Count-1).Select(index=>
            {
                var a=levels[index-1];var b=levels[index];
                return new{
                    fromCells=a.Cells,toCells=b.Cells,
                    relativeVolumePercent=100*(b.Volume/Math.Max(1e-12,a.Volume)-1),
                    relativeStressPercent=100*(b.Stress/Math.Max(1e-12,a.Stress)-1),
                    relativeDisplacementPercent=100*(b.Displacement/Math.Max(1e-12,a.Displacement)-1)
                };
            }).ToArray();
            meshConverged=levels.Count>=3 && transitions.All(t=>
                Math.Abs(t.relativeStressPercent)<10 &&
                Math.Abs(t.relativeDisplacementPercent)<10 &&
                Math.Abs(t.relativeVolumePercent)<5);
            Console.WriteLine($"MESH CONVERGENCE SCREEN: " +
                (meshConverged.Value?"PASS indicative tolerance on BOTH refinements":"NOT CONVERGED, at least one Hex8 mesh transition exceeds tolerance"));
            var report=new{
                entries=levels.Select(l=>new{
                    cells=l.Cells,volumeMm3=l.Volume,
                    maxVonMisesMpa=l.Stress,maxDisplacementMm=l.Displacement
                }).ToArray(),
                transitions,
                convergenceScreenPassed=meshConverged,
                lastRefinementRelativeVolumePercent=volumeChange,
                lastRefinementRelativeStressPercent=stressChange,
                lastRefinementRelativeDisplacementPercent=displacementChange,
                maxAbsRefinementVolumePercent=transitions.Max(t=>Math.Abs(t.relativeVolumePercent)),
                maxAbsRefinementStressPercent=transitions.Max(t=>Math.Abs(t.relativeStressPercent)),
                maxAbsRefinementDisplacementPercent=transitions.Max(t=>Math.Abs(t.relativeDisplacementPercent)),
                caveat="Requires BOTH sequential refinements within tolerance, but three background Hex8 levels do not prove convergence of stress near CSG/STL cut cells"
            };
            File.WriteAllText(Path.Combine(outDir,"mesh_refinement.json"),
                JsonSerializer.Serialize(report,
                    new JsonSerializerOptions{WriteIndented=true}));
            if(args.Contains("--require-mesh-convergence")&&!meshConverged.Value)
                throw new InvalidOperationException(
                    "MESH CONVERGENCE NOT DEMONSTRATED: design withheld. "+
                    "Refine FVM/FEM grids and boundary conditions and rerun; "+
                    "do not use the current coarse stress estimate for manufacturing.");
        }
        File.WriteAllLines(Path.Combine(outDir,"metrics.csv"),metrics);
        TwoSphereDesign.SphereOutputs.WriteStl(Path.Combine(outDir,"optimized.stl"),
            current.M,null,pruneTinyShellFraction:1e-4);
        using(var writer=new StreamWriter(Path.Combine(outDir,"density.csv")))
        {
            writer.WriteLine("cell,x_mm,y_mm,z_mm,raw_density,filtered_density,projected_density,locked");
            for(int i=0;i<current.Field.Variables;i++)
            {
                var c=current.Field.Grid.Centre(i);
                writer.WriteLine(string.Join(",",new[]{
                    i.ToString(CultureInfo.InvariantCulture),
                    c[0].ToString("G17",CultureInfo.InvariantCulture),
                    c[1].ToString("G17",CultureInfo.InvariantCulture),
                    c[2].ToString("G17",CultureInfo.InvariantCulture),
                    current.Field.Controls[i].ToString("G17",CultureInfo.InvariantCulture),
                    current.Field.Filtered[i].ToString("G17",CultureInfo.InvariantCulture),
                    current.Field.ProjectedAtCentre(i).ToString("G17",CultureInfo.InvariantCulture),
                    current.Field.Locked[i].ToString()}));
            }
        }
        var summary=new{
            mode="free-topology-fvm-simp-oc",scene=Path.GetFileName(path),
            inputBodies=scene.Bodies.Count,obstacles=scene.Obstacles.Count,
            variables=free,heavisideProjectionBeta=scene.ProjectionBeta,
            materialDensityKgMm3=scene.DensityKgMm3,
            initialVolumeMm3=initial.Volume,finalVolumeMm3=current.Volume,
            initialMassKg=initial.Volume*scene.DensityKgMm3,
            finalMassKg=current.Volume*scene.DensityKgMm3,
            estimatedBinaryMassKg=finalBinary.Volume*scene.DensityKgMm3,
            volumeReductionPercent=(1-current.Volume/initial.Volume)*100,
            acceptedUpdates=accepted,nucleatedVoidPatches=nucleated,
            complianceNmm=current.Compliance,
            maximumVonMisesMpa=current.Stress,maximumDisplacementMm=current.Displacement,
            directFVolumeMm3=finalDirect.Volume,
            binaryFvmVolumeMm3=finalBinary.Volume,
            binaryMaximumVonMisesMpa=finalBinary.Stress,
            binaryMaximumDisplacementMm=finalBinary.Displacement,
            binaryComplianceNmm=finalBinary.Compliance,
            refinedBinaryVolumeMm3=refined?.Volume,
            refinedBinaryVonMisesMpa=refined?.Stress,
            refinedBinaryDisplacementMm=refined?.Displacement,
            meshConvergenceScreenPassed=meshConverged,
            verifiedManufacturingStrength=false,
            binaryMultiphysicsFeasible=finalBinary.AdditionalPass,
            firstFrequencyHz=double.IsFinite(current.FirstHz)?current.FirstHz:(double?)null,
            worstImpactMm=double.IsFinite(current.ImpactMm)?current.ImpactMm:(double?)null,
            worstImpactStressMpa=double.IsFinite(current.ImpactStress)?current.ImpactStress:(double?)null,
            thermalConductanceWPerK=double.IsFinite(current.Thermal)?current.Thermal:(double?)null,
            thermoelasticStressMpa=double.IsFinite(current.ThermalStress)?current.ThermalStress:(double?)null,
            harmonicFirstModeMm=double.IsFinite(current.Harmonic)?current.Harmonic:(double?)null,
            fatigueDamageScreening=double.IsFinite(current.Fatigue)?current.Fatigue:(double?)null,
            caveat="regularized SIMP ersatz Hex8 and five M, not certified exact-boundary FEM",
            status="PASS"
        };
        File.WriteAllText(Path.Combine(outDir,"summary.json"),JsonSerializer.Serialize(summary,
            new JsonSerializerOptions{WriteIndented=true}));
        if(args.Contains("--self-test"))
        {
            Check(accepted>0,"No successful free-field design update.");
            Check(current.Volume<initial.Volume,"No actual volume reduction");
        }
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "FREE TOPOLOGY PASS: {0} controls ({1} free), {2} approved updates, {3} new voids, V reduction {4:F3}%, output={5}",
            current.Field.Variables,free,accepted,nucleated,
            summary.volumeReductionPercent,outDir));
        return 0;
    }
}
