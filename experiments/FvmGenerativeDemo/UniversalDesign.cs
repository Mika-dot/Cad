using System.Globalization;
using System.Text.Json;
using DCad.FunctionalVoxel;

namespace FvmGenerativeDemo;

// One coherent F and five M planes; >=2 arbitrary CSG terminals and true keep-outs.
// Each admitted shape update is checked against all active static and additional
// linear multiphysics scenarios, including independent direct-F FEM checks.
internal static class UniversalDesign
{
    private sealed record ScenarioMechanics(string Name,HexFem Solver,FemResults Result);
    private sealed class State
    {
        internal required UniversalField Field;
        internal required FunctionalVoxelModel Model;
        internal required List<ScenarioMechanics> Cases;
        internal double Volume => Cases[0].Result.VolumeMm3;
        internal double Compliance => Cases.Average(c=>c.Result.ComplianceNmm);
        internal double MaxStress => Cases.Max(c=>c.Result.MaxVonMisesMpa);
        internal double MaxDisplacement => Cases.Max(c=>c.Result.MaxDisplacementMm);
        internal double FrequencyHz = double.NaN;
        internal double ImpactDisplacementMm = double.NaN;
        internal double ImpactStressMpa = double.NaN;
        internal double ThermalConductanceWPerK = double.NaN;
        internal double ThermoElasticStressMpa = double.NaN;
        internal double ThermoElasticDisplacementMm = double.NaN;
        internal double HarmonicDisplacementMm = double.NaN;
        internal double FatigueDamage = double.NaN;
    }
    private static string Arg(string[] a,string key,string fallback)
    {
        for(int i=0;i<a.Length-1;i++)if(a[i]==key)return a[i+1];
        return fallback;
    }
    private static void Check(bool cond,string msg)
    {
        if(!cond)throw new InvalidOperationException("MULTI-SCENE FAIL: "+msg);
    }
    internal static int Run(string[] args)
    {
        var path=Arg(args,"--multi-scene","");
        if(string.IsNullOrWhiteSpace(path))throw new ArgumentException("Usage: --multi-scene JSON");
        var scene=UniversalScene.Load(path);
        var steps=int.Parse(Arg(args,"--iterations",scene.Iterations.ToString(CultureInfo.InvariantCulture)),
            CultureInfo.InvariantCulture);
        if(steps<1||steps>20)throw new ArgumentException("Iterations must be 1..20.");
        var outDir=Arg(args,"--out","output/multi-scene");
        Directory.CreateDirectory(outDir);
        var router=new MultiTerminalRouter(scene);
        var routed=router.Route();
        Check(routed.Count==scene.Bodies.Count-1,"All terminals routed.");
        if(scene.AssertDetour)Check(routed.Any(b=>b.Polyline.Count>=3),
            "Forced obstacle test requires a nonstraight routed polyline.");
        // The user supplies objects and physical BCs, NOT connection paths.
        // Optionally synthesize NEW diagonal truss members on safe spans.
        // They are independent FVM design variables and receive FEM feedback.
        var bracing=scene.AutoTruss?router.AutoBrace(routed):new List<RoutedBranch>();
        if(scene.AutoTruss)
            Check(bracing.Count>0,"Requested autoTruss, but no obstacle-safe braces were generated.");
        var branches=routed.Concat(bracing).ToList();
        Console.WriteLine($"AUTO-TRUSS: {bracing.Count} self-generated diagonal members; "+
            $"{routed.Count} routed main members.");
        var summaryRoute=new List<string>{"branch,target,kind,point,x_mm,y_mm,z_mm"};
        for(int i=0;i<branches.Count;i++)
        {
            Console.WriteLine($"CSG ROUTE {i+1} to {branches[i].Target.Id} via {branches[i].Polyline.Count} knots");
            for(int k=0;k<branches[i].Polyline.Count;k++)
            {
                var v=branches[i].Polyline[k];
                summaryRoute.Add(string.Join(",",new[]{
                    i.ToString(CultureInfo.InvariantCulture),branches[i].Target.Id,branches[i].Kind,
                    k.ToString(CultureInfo.InvariantCulture),v.X.ToString("G17",CultureInfo.InvariantCulture),
                    v.Y.ToString("G17",CultureInfo.InvariantCulture),v.Z.ToString("G17",CultureInfo.InvariantCulture)}));
            }
        }
        File.WriteAllLines(Path.Combine(outDir,"routes.csv"),summaryRoute);

        var nx=scene.FemCells[0];var ny=scene.FemCells[1];var nz=scene.FemCells[2];
        var origin=scene.Min;var extent=scene.Max-scene.Min;
        var hx=extent.X/nx;var hy=extent.Y/ny;var hz=extent.Z/nz;
        var xcoords=new List<(double X,double Y,double Z)>();
        for(int i=0;i<=nx;i++)for(int j=0;j<=ny;j++)for(int k=0;k<=nz;k++)
            xcoords.Add((origin.X+i*hx,origin.Y+j*hy,origin.Z+k*hz));
        bool Supports(double x,double y,double z)
        {
            var p=new Point3(x,y,z);
            return scene.Bodies.Any(b=>b.Role=="fixed"&&b.Shape.F(p)>=0);
        }
        var supported=xcoords.Count(c=>Supports(c.X,c.Y,c.Z));
        Check(supported>=2,"At least two distinct support FEM nodes required.");

        // Per-body effective nodal force is normalized exactly: total force on
        // each physical body is preserved independently of mesh node count.
        var counts=new Dictionary<string,int>();
        foreach(var b in scene.Bodies)
        {
            var n=xcoords.Count(c=>b.Shape.F(new Point3(c.X,c.Y,c.Z))>=0);
            counts[b.Id]=n;
            Check(n>=1,$"No FEM node in protected body '{b.Id}'. Refine femCells.");
        }
        // Explicitly reject applying forces at clamped DOFs. HexFem otherwise
        // zeros their RHS values, silently changing the physical load case.
        foreach(var (name,loads) in scene.LoadCases)
            foreach(var (id,force) in loads)
            {
                var body=scene.Bodies.First(v=>v.Id==id);
                if(force.Norm()>0&&xcoords.Any(c=>Supports(c.X,c.Y,c.Z)
                    &&body.Shape.F(new Point3(c.X,c.Y,c.Z))>=0))
                    throw new InvalidOperationException(
                        $"Load case '{name}', body '{id}' shares fixed FEM nodes. "+
                        "The prescribed load would be silently lost at fixed DOFs.");
            }
        State Analyze(double[] p,bool useM,bool extras)
        {
            var field=new UniversalField(scene,branches,p);
            var model=field.Sample();
            // Check solid at all physically prescribed support and load nodes.
            foreach(var c in xcoords)
            {
                var xyz=new Point3(c.X,c.Y,c.Z);
                if(scene.Bodies.Any(b=>b.Shape.F(xyz)>=0) &&
                    model.Approximate(new[]{c.X,c.Y,c.Z})<-.35*scene.FvmStep)
                    throw new InvalidOperationException("Protected CSG geometry lost in 5M sampling.");
            }
            foreach(var o in scene.Obstacles)
            {
                // Check the interior, not merely near F=0 where interpolation
                // can shift a surface by a small fraction of the grid spacing.
                foreach(var c in xcoords)
                {
                    var point=new Point3(c.X,c.Y,c.Z);
                    if(o.Shape.F(point)>scene.FvmStep*.5 &&
                       model.Approximate(new[]{c.X,c.Y,c.Z})>0)
                        throw new InvalidOperationException($"Obstacle '{o.Id}' filled after M reconstruction.");
                }
            }
            var cases=new List<ScenarioMechanics>();
            foreach(var (name,loads) in scene.LoadCases)
            {
                (double X,double Y,double Z) Force(double x,double y,double z)
                {
                    var point=new Point3(x,y,z);
                    var net=new Point3(0,0,0);
                    foreach(var (id,value) in loads)
                    {
                        var body=scene.Bodies.First(b=>b.Id==id);
                        if(body.Shape.F(point)>=0)
                            net+=value*(1.0/counts[id]);
                    }
                    return(net.X,net.Y,net.Z);
                }
                var fem=new HexFem(nx,ny,nz,origin.X,origin.Y,origin.Z,hx,hy,hz,
                    scene.Young,scene.Poisson,
                    (x,y,z)=>field.Value(new Point3(x,y,z)),useM?model:null,
                    loadN:0,interfaceHalfWidth:0,emin:1e-4,
                    epsF:Math.Max(0.75,scene.FvmStep*.5),
                    support:Supports,nodalForce:Force,densityKgMm3:scene.DensityKgMm3);
                var solve=fem.Solve(maxIterations:3000);
                Check(solve.RelativeEquilibriumResidual<1e-6,"Static FEM residual");
                var sx=loads.Sum(q=>q.Value.X);var sy=loads.Sum(q=>q.Value.Y);var sz=loads.Sum(q=>q.Value.Z);
                Check(Math.Abs(solve.ReactionXN+sx)<Math.Max(.1,Math.Abs(sx)*.001),"X balance");
                Check(Math.Abs(solve.ReactionYN+sy)<Math.Max(.1,Math.Abs(sy)*.001),"Y balance");
                Check(Math.Abs(solve.ReactionZN+sz)<Math.Max(.1,Math.Abs(sz)*.001),"Z balance");
                cases.Add(new ScenarioMechanics(name,fem,solve));
            }
            var state=new State{Field=field,Model=model,Cases=cases};
            if(extras)
            {
                var fem=cases[0].Solver;
                if(scene.MinFrequencyHz>0||scene.HarmonicFrequencyHz>=0)
                    state.FrequencyHz=fem.FirstNaturalFrequency(scene.DensityKgMm3).FrequencyHz;
                // Dynamic forcing MUST cover ALL input load cases; one modal
                // stiffness/mass operator suffices, but modal force and impulse
                // histories depend on each load-case RHS independently.
                if(scene.HarmonicFrequencyHz>=0)
                    state.HarmonicDisplacementMm=cases.Max(c=>
                        c.Solver.HarmonicFirstModeMaxDisplacement(
                            scene.DensityKgMm3,scene.HarmonicFrequencyHz,scene.DampingRatio));
                if(double.IsFinite(scene.MaxImpactDisplacementMm)||
                    double.IsFinite(scene.MaxImpactVonMisesMpa))
                {
                    var impacts=cases.Select(c=>c.Solver.SolvePulse(
                        scene.DensityKgMm3,scene.ImpactPulseSeconds,
                        scene.ImpactMultiplier,scene.DampingRatio)).ToArray();
                    state.ImpactDisplacementMm=impacts.Max(v=>v.MaxDisplacementMm);
                    state.ImpactStressMpa=impacts.Max(v=>v.MaxVonMisesMpa);
                }
                if(scene.MinThermalConductanceWPerK>0||scene.ThermalExpansionPerK>0)
                {
                    var hot=scene.Bodies.First(b=>b.Id==scene.ThermalHotId);
                    var cold=scene.Bodies.First(b=>b.Id==scene.ThermalColdId);
                    var fieldT=fem.ThermalConductance(
                        scene.ThermalConductivityWmK,
                        (x,y,z)=>hot.Shape.F(new Point3(x,y,z))>=0,
                        (x,y,z)=>cold.Shape.F(new Point3(x,y,z))>=0);
                    state.ThermalConductanceWPerK=fieldT.ConductanceWPerK;
                    if(scene.ThermalExpansionPerK>0)
                    {
                        var thermo=fem.SolveThermoElastic(fieldT,
                            scene.ThermalExpansionPerK,scene.ThermalColdK,
                            scene.ThermalHotK,scene.ThermalReferenceK);
                        Check(thermo.RelativeResidual<1e-5,"Thermoelastic FEM residual");
                        state.ThermoElasticStressMpa=thermo.MaxVonMisesMpa;
                        state.ThermoElasticDisplacementMm=thermo.MaxDisplacementMm;
                    }
                }
                if(scene.FatigueCycles>0)
                {
                    // Explicit constant-amplitude fully-reversed S-N screen.
                    // For general cycles use rainflow, mean stress and
                    // validated multi-axial material curves in future work.
                    var sigmaA=state.MaxStress;
                    var life=scene.FatigueReferenceCycles*Math.Pow(
                        scene.FatigueReferenceStressMpa/Math.Max(1e-12,sigmaA),
                        scene.FatigueExponent);
                    state.FatigueDamage=scene.FatigueCycles/life;
                }
            }
            return state;
        }

        // Discrete six-neighbour material connectivity of F_M: each terminal
        // must be reached by at least one positive cell inside its exact CSG solid.
        bool Connected(State s)
        {
            var g=s.Model.Grid;var n=g.CellCount;
            var positive=new bool[n];var supportedCells=new Queue<int>();
            var visited=new bool[n];var found=new HashSet<string>();
            // A disconnected fixed component must not be considered connected
            // just because it is ALSO fixed. Start from ONE physical support.
            var root=scene.Bodies.First(b=>b.Role=="fixed");
            for(int i=0;i<n;i++)
            {
                var a=g.Centre(i);var p=new Point3(a[0],a[1],a[2]);
                positive[i]=s.Model.ValueAtCentre(i)>0;
                if(positive[i]&&root.Shape.F(p)>=0)
                {
                    supportedCells.Enqueue(i);visited[i]=true;
                }
            }
            if(supportedCells.Count==0)return false;
            while(supportedCells.Count!=0)
            {
                var index=supportedCells.Dequeue();
                var a=g.Centre(index);var p=new Point3(a[0],a[1],a[2]);
                foreach(var b in scene.Bodies)if(b.Shape.F(p)>=0)found.Add(b.Id);
                for(int axis=0;axis<3;axis++)for(int delta=-1;delta<=1;delta+=2)
                {
                    var ids=new[]{g.Index(index,0),g.Index(index,1),g.Index(index,2)};
                    ids[axis]+=delta;
                    if(ids[axis]<0||ids[axis]>=g.Count(axis))continue;
                    var next=g.LinearIndex(ids);
                    if(positive[next]&&!visited[next]){visited[next]=true;supportedCells.Enqueue(next);}
                }
            }
            return found.Count==scene.Bodies.Count;
        }
        bool Feasible(State state)
        {
            return state.MaxStress<=scene.StressLimit
                && state.MaxDisplacement<=scene.DisplacementLimit
                && (scene.MinFrequencyHz<=0||state.FrequencyHz>=scene.MinFrequencyHz)
                && (!double.IsFinite(scene.MaxImpactDisplacementMm)
                    ||state.ImpactDisplacementMm<=scene.MaxImpactDisplacementMm)
                && (!double.IsFinite(scene.MaxImpactVonMisesMpa)
                    ||state.ImpactStressMpa<=scene.MaxImpactVonMisesMpa)
                && (scene.MinThermalConductanceWPerK<=0
                    ||state.ThermalConductanceWPerK>=scene.MinThermalConductanceWPerK)
                && (scene.ThermalExpansionPerK<=0||(
                    state.ThermoElasticStressMpa<=scene.MaxThermalStressMpa &&
                    state.ThermoElasticDisplacementMm<=scene.MaxThermalDisplacementMm))
                && (scene.HarmonicFrequencyHz<0
                    ||state.HarmonicDisplacementMm<=scene.MaxHarmonicDisplacementMm)
                && (scene.FatigueCycles<=0
                    ||state.FatigueDamage<=scene.MaxFatigueDamage)
                && Connected(state);
        }
        var zero=new double[branches.Count];
        var initial=Analyze(zero,true,true);
        var initialDirect=Analyze(zero,false,true);
        Check(Feasible(initial)&&Feasible(initialDirect),
            "Initial CSG layout violates constraints in M or direct F.");
        var current=initial;
        var metrics=new List<string>{
            "iteration,volume_mm3,max_von_mises_mpa,max_displacement_mm,first_frequency_hz,impact_peak_mm,impact_peak_mpa,thermal_conductance_w_k,thermoelastic_peak_mpa,thermoelastic_displacement_mm,harmonic_peak_mm,fatigue_damage_screening,compliance_nmm,accepted_step_mm,volume_direct_f_mm3"
        };
        void Row(int iteration,State m,State direct,double step)
        {
            string Num(double d)=>d.ToString("G17",CultureInfo.InvariantCulture);
            metrics.Add(string.Join(",",new[]{(double)iteration,m.Volume,
                m.MaxStress,m.MaxDisplacement,m.FrequencyHz,m.ImpactDisplacementMm,
                m.ImpactStressMpa,m.ThermalConductanceWPerK,
                m.ThermoElasticStressMpa,m.ThermoElasticDisplacementMm,
                m.HarmonicDisplacementMm,m.FatigueDamage,
                m.Compliance,step,direct.Volume}.Select(Num)));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "MULTI {0}: V={1:F2} mm3 VM={2:F2} MPa u={3:F5} mm f1={4:F2} Hz shock={5:F5} mm thermal={6:F6} W/K C={7:F4}",
                iteration,m.Volume,m.MaxStress,m.MaxDisplacement,m.FrequencyHz,
                m.ImpactDisplacementMm,m.ThermalConductanceWPerK,m.Compliance));
        }
        Row(0,current,initialDirect,0);
        int accepted=0;
        for(int iter=1;iter<=steps;iter++)
        {
            // Adjoint compliance & volume from actual Hex8 Gauss points. The
            // derivative dF_M/dp is obtained by perturbing ONLY the five-M
            // reconstruction, never by re-solving FEM for every parameter.
            var gradientV=new double[zero.Length];
            var gradientC=new double[zero.Length];
            var gauss=current.Cases.Select(c=>c.Solver.GaussSensitivities(c.Result)).ToArray();
            const double perturb=.08;
            for(int v=0;v<zero.Length;v++)
            {
                var plus=(double[])current.Field.P.Clone();
                var minus=(double[])current.Field.P.Clone();
                plus[v]+=perturb;minus[v]-=perturb;
                var high=new UniversalField(scene,branches,plus).Sample();
                var low=new UniversalField(scene,branches,minus).Sample();
                for(int c=0;c<gauss.Length;c++)
                    foreach(var q in gauss[c])
                    {
                        var at=new[]{q.X,q.Y,q.Z};
                        var d=(high.Approximate(at)-low.Approximate(at))/(2*perturb);
                        if(c==0)gradientV[v]+=q.DVolumeDField*d;
                        gradientC[v]+=q.DComplianceDField*d/gauss.Length;
                    }
            }
            var blend=.10*current.Volume/Math.Max(.01,current.Compliance);
            var combined=new double[zero.Length];
            for(int v=0;v<zero.Length;v++)
                combined[v]=gradientV[v]+blend*gradientC[v];
            var scale=combined.Select(Math.Abs).DefaultIfEmpty(0).Max();
            if(scale<1e-8){Console.WriteLine("MULTI STOP: zero FEM gradient.");break;}
            var improving=combined.Select(g=>-g/scale).ToArray();
            bool found=false;
            var step=1.0;
            for(var attempt=0;attempt<8;attempt++,step*=.5)
            {
                var next=new double[combined.Length];
                for(var i=0;i<next.Length;i++)
                    next[i]=Math.Clamp(current.Field.P[i]+improving[i]*step,
                        -scene.BridgeRadius*.5,scene.BridgeRadius*.35);
                try
                {
                    var trial=Analyze(next,true,true);
                    if(trial.Volume>=current.Volume-.1||!Feasible(trial))continue;
                    // Trust but verify: M interpolation can under-predict stress.
                    var direct=Analyze(next,false,true);
                    if(!Feasible(direct))
                    {
                        Console.WriteLine("MULTI REJECT: direct F fails one of the physics criteria.");
                        continue;
                    }
                    accepted++;current=trial;Row(iter,trial,direct,step);
                    found=true;break;
                }
                catch(ArithmeticException)
                {
                    // Failed numerical solve is an invalid trial, NOT a PASS.
                }
            }
            if(!found)
            {
                Console.WriteLine("MULTI STOP: no lighter design satisfying ALL active scenarios.");
                break;
            }
        }
        Check(Feasible(current),"Final multi CSG M geometry violates a physical constraint.");
        var directFinal=Analyze(current.Field.P,false,true);
        Check(Feasible(directFinal),"Final direct-F fails a physics criterion.");
        Check(Connected(current),"Final CSG terminal network disconnected.");
        File.WriteAllLines(Path.Combine(outDir,"metrics.csv"),metrics);
        static double? Finite(double x)=>double.IsFinite(x)?x:null;
        var result=new{
            scene=Path.GetFileName(path),terminals=scene.Bodies.Count,
            supports=scene.Bodies.Count(x=>x.Role=="fixed"),
            obstacles=scene.Obstacles.Count,loadCases=scene.LoadCases.Count,
            routedBranches=routed.Count,
            autogeneratedTrussMembers=bracing.Count,
            initialVolumeMm3=initial.Volume,
            finalVolumeMm3=current.Volume,
            savedVolumePercent=100*(1-current.Volume/initial.Volume),
            optimizedSteps=accepted,
            finalMaximumVonMisesMpa=current.MaxStress,
            finalMaximumDisplacementMm=current.MaxDisplacement,
            firstModeHz=Finite(current.FrequencyHz),
            impulsePeakDisplacementMm=Finite(current.ImpactDisplacementMm),
            impulsePeakStressMpa=Finite(current.ImpactStressMpa),
            thermalConductanceWPerK=Finite(current.ThermalConductanceWPerK),
            thermoelasticVonMisesMpa=Finite(current.ThermoElasticStressMpa),
            thermoelasticDisplacementMm=Finite(current.ThermoElasticDisplacementMm),
            harmonicFirstModeDisplacementMm=Finite(current.HarmonicDisplacementMm),
            fatigueDamageScreening=Finite(current.FatigueDamage),
            directFMaximumVonMisesMpa=directFinal.MaxStress,
            directFMaximumDisplacementMm=directFinal.MaxDisplacement,
            model="regularized immersed Hex8/FVM, linear elastic, NOT manufacturing-certified",
            status="PASS"
        };
        File.WriteAllText(Path.Combine(outDir,"summary.json"),
            JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
        TwoSphereDesign.SphereOutputs.WriteStl(Path.Combine(outDir,"initial.stl"),initial.Model,null);
        TwoSphereDesign.SphereOutputs.WriteStl(Path.Combine(outDir,"optimized.stl"),current.Model,null);
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "MULTI CSG PASS: {0} bodies, {1} keep-outs, {2} load cases, {3} accepted FEM+physics updates, FEM volume saving {4:F2}%, output={5}",
            scene.Bodies.Count,scene.Obstacles.Count,scene.LoadCases.Count,
            accepted,100*(1-current.Volume/initial.Volume),outDir));
        if(args.Contains("--self-test"))
            Check(accepted>0,"Integration test expected at least one accepted physical optimization step.");
        return 0;
    }
}
