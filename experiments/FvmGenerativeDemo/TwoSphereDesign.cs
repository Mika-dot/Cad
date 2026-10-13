using System.Globalization;
using System.Text;
using DCad.FunctionalVoxel;

namespace FvmGenerativeDemo;

// Free-form implicit design field: 54 overlapping 3D Gaussian shape degrees of freedom.
// They deform ONE differentiable F; five M-images are regenerated from its coherent jets.
// This is NOT density thresholding/element deletion or the old two-parameter taper.
internal sealed class TwoSphereField
{
    internal const int ParameterCount=54;
    private readonly double[] _p;
    private readonly SphereScene _scene;
    internal readonly FunctionalGrid Grid;
    private double Length => _scene.Length;
    private double BridgeRadius => .7*_scene.MinRadius;

    internal TwoSphereField(double[] p,SphereScene scene)
    {
        if(p.Length!=ParameterCount||p.Any(v=>!double.IsFinite(v)))
            throw new ArgumentException("Expected 54 finite 3D shape parameters.");
        _p=(double[])p.Clone();
        _scene=scene;
        if(scene.StandardGeometry)
            Grid=new FunctionalGrid(new[]{-12.0,-18.0,-18.0},
                new[]{4.0,3.0,3.0},new[]{26,12,12});
        else
        {
            var (lo,hi)=scene.Bounds(scene.MaxRadius*.75);
            // Rotated / unequal spheres require a finer five-M lattice:
            // coarse first-order reconstruction can understate FEM stresses.
            var step=scene.MinRadius*.30;
            var counts=new int[3];var sizes=new double[3];
            for(int d=0;d<3;d++)
            {
                counts[d]=Math.Clamp((int)Math.Ceiling((hi[d]-lo[d])/step),4,70);
                sizes[d]=(hi[d]-lo[d])/counts[d];
            }
            Grid=new FunctionalGrid(lo,sizes,counts);
        }
    }

    private static double SmoothMax(double a,double b,double t) =>
        .5*(a+b+Math.Sqrt((a-b)*(a-b)+t*t));
    private static double SmoothMin(double a,double b,double t) =>
        .5*(a+b-Math.Sqrt((a-b)*(a-b)+t*t));
    private static double Sphere(double x,double y,double z,double cx,double radius) =>
        radius-Math.Sqrt((x-cx)*(x-cx)+y*y+z*z+1e-8);

    // Each basis controls a local volume of space (not a ring/radius scalar).
    internal double Basis(int i,double x,double y,double z,
        out double bx,out double by,out double bz)
    {
        var station=i/9; var lateral=i%9;
        double cx=Length*(.125+station*.15),
            cy=((lateral/3)-1)*(.75*_scene.MinRadius),
            cz=((lateral%3)-1)*(.75*_scene.MinRadius);
        double sx=.175*Length, sy=.75*_scene.MinRadius;
        var s=Math.Clamp(x/Length,0,1);
        double sin=Math.Sin(Math.PI*s),cos=Math.Cos(Math.PI*s);
        var fade=sin*sin;
        var df=(x>=0 && x<=Length)?2*sin*cos*Math.PI/Length:0;
        double dx=(x-cx)/(sx*sx),dy=(y-cy)/(sy*sy),dz=(z-cz)/(sy*sy);
        var g=Math.Exp(-.5*((x-cx)*dx+(y-cy)*dy+(z-cz)*dz));
        var value=fade*g;
        bx=(df-fade*dx)*g;by=-value*dy;bz=-value*dz;
        return value;
    }

    private double Tube(double x,double y,double z)
    {
        double t=BridgeRadius-Math.Sqrt(y*y+z*z+1e-6);
        for(int i=0;i<ParameterCount;i++)
        {
            var b=Basis(i,x,y,z,out _,out _,out _);
            t+=_p[i]*b;
        }
        return t;
    }

    private (double Field,double DFieldDTube) EvaluateWithFactor(double x,double y,double z)
    {
        var local=_scene.Local(new Point3(x,y,z));
        x=local.X;y=local.Y;z=local.Z;
        var t=Tube(x,y,z);
        var smoothing=.035*_scene.MinRadius;
        var left=x+_scene.RadiusA;
        var right=Length+_scene.RadiusB-x;
        var cut1=SmoothMin(t,left,smoothing);
        var d1=.5*(1+(left-t)/Math.Sqrt((left-t)*(left-t)+smoothing*smoothing));
        var cut2=SmoothMin(cut1,right,smoothing);
        var d2=.5*(1+(right-cut1)/Math.Sqrt((right-cut1)*(right-cut1)+smoothing*smoothing));
        var solids=SmoothMax(Sphere(x,y,z,0,_scene.RadiusA),
            Sphere(x,y,z,Length,_scene.RadiusB),smoothing);
        var value=SmoothMax(solids,cut2,smoothing);
        var d3=.5*(1+(cut2-solids)/Math.Sqrt((cut2-solids)*(cut2-solids)+smoothing*smoothing));
        return (value,d1*d2*d3);
    }

    internal double Value(double x,double y,double z) => EvaluateWithFactor(x,y,z).Field;
    private double Factor(double x,double y,double z) => EvaluateWithFactor(x,y,z).DFieldDTube;

    internal FunctionalVoxelModel Model()
    {
        var field=FunctionalField.Analytic(3,v=>
        {
            var x=v[0];var y=v[1];var z=v[2];const double h=.075;
            var value=Value(x,y,z);
            var gx=(Value(x+h,y,z)-Value(x-h,y,z))/(2*h);
            var gy=(Value(x,y+h,z)-Value(x,y-h,z))/(2*h);
            var gz=(Value(x,y,z+h)-Value(x,y,z-h))/(2*h);
            return FunctionalJet.FromValueGradient(value,new[]{gx,gy,gz});
        });
        return FunctionalVoxelModel.Sample(field,Grid);
    }

    // Differentiate the same continuous M reconstruction with respect to each
    // 3D field coefficient. A local interpolated plane value is affine in its
    // centre jet, so interpolating derivative jets yields the M-shape Jacobian.
    internal double[][][] DerivativeJets()
    {
        var jets=new double[ParameterCount][][];
        for(int i=0;i<ParameterCount;i++)
        {
            jets[i]=new double[Grid.CellCount][];
        }
        const double h=.075;
        for(int v=0;v<Grid.CellCount;v++)
        {
            var a=Grid.Centre(v);
            var x=a[0];var y=a[1];var z=a[2];
            var f=Factor(x,y,z);
            double fx=(Factor(x+h,y,z)-Factor(x-h,y,z))/(2*h);
            var fy=(Factor(x,y+h,z)-Factor(x,y-h,z))/(2*h);
            var fz=(Factor(x,y,z+h)-Factor(x,y,z-h))/(2*h);
            for(int i=0;i<ParameterCount;i++)
            {
                var local=_scene.Local(new Point3(x,y,z));
                var b=Basis(i,local.X,local.Y,local.Z,out var dx,out var dy,out var dz);
                var world=_scene.VectorWorld(dx,dy,dz);
                jets[i][v]=new[]{f*b,fx*b+f*world.X,fy*b+f*world.Y,fz*b+f*world.Z};
            }
        }
        return jets;
    }

    // Exactly mirrors FunctionalVoxelModel.Approximate multilinear plane blending.
    internal double ModelDerivative(int i,double x,double y,double z,double[][][] jets)
    {
        var at=new[]{x,y,z};
        var lo=new int[3];var hi=new int[3];var frac=new double[3];
        for(int d=0;d<3;d++)
        {
            var t=Math.Clamp((at[d]-Grid.Minimum(d))/Grid.Step(d)-.5,0,Grid.Count(d)-1);
            lo[d]=(int)Math.Floor(t);hi[d]=Math.Min(lo[d]+1,Grid.Count(d)-1);
            frac[d]=t-lo[d];
        }
        double sum=0;
        for(int corner=0;corner<8;corner++)
        {
            double w=1; var ids=new int[3];
            for(int d=0;d<3;d++)
            {
                bool high=((corner>>d)&1)!=0;
                ids[d]=high?hi[d]:lo[d];
                w*=high?frac[d]:1-frac[d];
            }
            if(w==0)continue;
            var index=Grid.LinearIndex(ids);
            var q=jets[i][index];var c=Grid.Centre(index);
            sum+=w*(q[0]+q[1]*(x-c[0])+q[2]*(y-c[1])+q[3]*(z-c[2]));
        }
        return sum;
    }
}

internal static class TwoSphereDesign
{
    private const int NX=10,NY=6,NZ=6;
    private const double Eps=2.0;
    internal sealed record State(double[] P,TwoSphereField Field,
        FunctionalVoxelModel Model,HexFem Fem,FemResults Mechanics);

    private static string Arg(string[] a,string name,string fallback)
    {
        for(int i=0;i<a.Length-1;i++)if(a[i]==name)return a[i+1];
        return fallback;
    }
    private static double Number(string[] args,string option,double defaultValue) =>
        double.Parse(Arg(args,option,defaultValue.ToString("G17",CultureInfo.InvariantCulture)),
            CultureInfo.InvariantCulture);
    private static void Ensure(bool v,string why)
    {
        if(!v)throw new InvalidOperationException("TWO-SPHERES FAIL: "+why);
    }

    internal static int Run(string[] args)
    {
        var test=args.Contains("--self-test",StringComparer.OrdinalIgnoreCase);
        var json=Arg(args,"--scene","");
        var input=string.IsNullOrWhiteSpace(json)?SphereScene.Default:SphereScene.FromJson(json);
        var steps=(int)Number(args,"--iterations",test?3:input.Iterations);
        Ensure(steps>=1 && steps<=50,"Iterations must be 1..50.");
        var root=Arg(args,"--out",test?"output/two-spheres-self-test":"output/two-spheres");
        var forceX=Number(args,"--force-x",input.Force.X);
        var forceY=Number(args,"--force-y",input.Force.Y);
        var forceZ=Number(args,"--force-z",input.Force.Z);
        var young=Number(args,"--young",input.Young);
        var stressLimit=Number(args,"--stress-limit",input.StressLimit);
        var deflectionLimit=Number(args,"--displacement-limit",input.DisplacementLimit);
        var scene=input.With(forceX,forceY,forceZ,young,stressLimit,deflectionLimit,steps);
        Directory.CreateDirectory(root);

        // Maintain exact backward-compatibility for the independently validated
        // reference scene. For custom arbitrary orientations, adapt the 3D FEM
        // background box and its spatial discretization to the user-supplied spheres.
        int nx=NX,ny=NY,nz=NZ;
        double x0=-10,y0=-18,z0=-18,hx=10,hy=6,hz=6;
        if(!scene.StandardGeometry)
        {
            var (low,high)=scene.Bounds(scene.MaxRadius*.5);
            var count=new int[3];var widths=new double[3];
            for(int d=0;d<3;d++)
            {
                count[d]=Math.Clamp((int)Math.Ceiling(
                    (high[d]-low[d])/(scene.MinRadius*.7)),6,28);
                widths[d]=(high[d]-low[d])/count[d];
            }
            (nx,ny,nz)=(count[0],count[1],count[2]);
            (x0,y0,z0)=(low[0],low[1],low[2]);
            (hx,hy,hz)=(widths[0],widths[1],widths[2]);
        }

        // Full 3D regions, not an artificially fixed x=0 FEM face.
        // A: all nodes within the attachment core of the first sphere.
        // B: distributed node force inside the second sphere.
        bool IsSupport(double x,double y,double z)=>
            (new Point3(x,y,z)-scene.A).Norm()<=scene.SupportRadius+1e-7;
        bool IsLoad(double x,double y,double z)=>
            (new Point3(x,y,z)-scene.B).Norm()<=scene.LoadedRadius+1e-7;
        var loaded=0;var supported=0;
        for(int i=0;i<=nx;i++)for(int j=0;j<=ny;j++)for(int k=0;k<=nz;k++)
        {
            var x=x0+i*hx;var y=y0+j*hy;var z=z0+k*hz;
            if(IsLoad(x,y,z))loaded++;
            if(IsSupport(x,y,z))supported++;
        }
        Ensure(supported>=3,"Support region must contain multiple FEM nodes.");
        Ensure(loaded>=3,"Load region must contain multiple FEM nodes.");

        State Analyze(double[] p,bool useM=true)
        {
            var field=new TwoSphereField(p,scene);
            var model=field.Model();
            // Actual prescribed nodes must remain material in the reconstructed
            // five-M representation, not merely in the underlying analytic F.
            for(int i=0;i<=nx;i++)for(int j=0;j<=ny;j++)for(int k=0;k<=nz;k++)
            {
                var x=x0+i*hx;var y=y0+j*hy;var z=z0+k*hz;
                if((IsSupport(x,y,z)||IsLoad(x,y,z))&&
                    model.Approximate(new[]{x,y,z})<=0)
                    throw new InvalidOperationException(
                        "Prescribed support/load lies outside the reconstructed FVM material.");
            }
            var fem=new HexFem(nx,ny,nz,x0,y0,z0,hx,hy,hz,young,scene.Poisson,
                (x,y,z)=>field.Value(x,y,z),useM?model:null,0,0,
                emin:1e-4,epsF:Eps,support:IsSupport,
                nodalForce:(x,y,z)=>IsLoad(x,y,z)
                    ? (forceX/loaded,forceY/loaded,forceZ/loaded):(0.0,0.0,0.0));
            var r=fem.Solve(maxIterations:5000);
            Ensure(r.RelativeEquilibriumResidual<1e-6,"FEM residual");
            Ensure(Math.Abs(r.ReactionXN+forceX)<Math.Max(.1,Math.Abs(forceX)*.0001),
                "X force equilibrium");
            Ensure(Math.Abs(r.ReactionYN+forceY)<Math.Max(.1,Math.Abs(forceY)*.0001),
                "Y force equilibrium");
            Ensure(Math.Abs(r.ReactionZN+forceZ)<Math.Max(.1,Math.Abs(forceZ)*.0001),
                "Z force equilibrium");
            return new State((double[])p.Clone(),field,model,fem,r);
        }

        bool Feasible(State s) =>
            double.IsFinite(s.Mechanics.MaxVonMisesMpa) &&
            s.Mechanics.MaxVonMisesMpa<=stressLimit &&
            s.Mechanics.MaxDisplacementMm<=deflectionLimit &&
            Connected(s.Model,scene);
        var current=Analyze(new double[TwoSphereField.ParameterCount]);
        Ensure(Feasible(current),"Initial 3D bridge must be mechanically feasible and connected.");
        Ensure(Feasible(Analyze(current.P,false)),
            "Initial analytic F must also satisfy stress/displacement limits.");
        var start=current;
        var csv=new List<string>{"iteration,volume_mm3,mass_kg,vm_max_mpa,u_max_mm,compliance_Nmm,reaction_x_N,reaction_y_N,residual,accepted_step_mm,active_gradient_channels"};
        void Store(int iter,State s,double step,int active)
        {
            var r=s.Mechanics;
            csv.Add(string.Join(",",new[]{(double)iter,r.VolumeMm3,r.MassKg,
                r.MaxVonMisesMpa,r.MaxDisplacementMm,r.ComplianceNmm,r.ReactionXN,
                r.ReactionYN,r.RelativeEquilibriumResidual,step,(double)active}.Select(
                v=>v.ToString("G17",CultureInfo.InvariantCulture))));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "sphere-step {0}: V={1:F2}mm3 mass={2:F5}kg VM={3:F3}MPa u={4:F6}mm C={5:F4}Nmm Rx={6:F2}N Ry={7:F2}N residual={8:E2} step={9:F3}",
                iter,r.VolumeMm3,r.MassKg,r.MaxVonMisesMpa,r.MaxDisplacementMm,
                r.ComplianceNmm,r.ReactionXN,r.ReactionYN,r.RelativeEquilibriumResidual,step));
        }
        Store(0,current,0,0);
        var accepted=0;var gradChecked=false;
        for(int iter=1;iter<=steps;iter++)
        {
            var sensitivities=current.Fem.GaussSensitivities(current.Mechanics);
            Ensure(sensitivities.Count>0,"Active implicit boundary Gauss points");
            var jets=current.Field.DerivativeJets();
            var dv=new double[TwoSphereField.ParameterCount];
            var dc=new double[dv.Length];
            foreach(var gp in sensitivities)
                for(int i=0;i<dv.Length;i++)
                {
                    var b=current.Field.ModelDerivative(i,gp.X,gp.Y,gp.Z,jets);
                    dv[i]+=gp.DVolumeDField*b;
                    dc[i]+=gp.DComplianceDField*b;
                }

            if(test&&!gradChecked)
            {
                // One-sided field sensitivity is never enough: compare actual
                // five-M + full FEM central differences with the adjoint gradient.
                var id=Array.IndexOf(dv,dv.Max());
                const double h=.045;
                var plus=(double[])current.P.Clone();plus[id]+=h;
                var minus=(double[])current.P.Clone();minus[id]-=h;
                var a=Analyze(plus).Mechanics;var b=Analyze(minus).Mechanics;
                var fdV=(a.VolumeMm3-b.VolumeMm3)/(2*h);
                var fdC=(a.ComplianceNmm-b.ComplianceNmm)/(2*h);
                var eV=Math.Abs(fdV-dv[id])/Math.Max(1,Math.Abs(fdV));
                var eC=Math.Abs(fdC-dc[id])/Math.Max(.01,Math.Abs(fdC));
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "3D M-sensitivity consistency id={0} dV={1:F5}/{2:F5} err={3:P2}, dC={4:F6}/{5:F6} err={6:P2}",
                    id,dv[id],fdV,eV,dc[id],fdC,eC));
                Ensure(eV<.10&&eC<.22,"adjoint against re-solved finite differences");
                gradChecked=true;
            }
            // Structural compliance term prevents an exclusively geometric
            // isotropic shrink. Stress & displacement are independently guarded
            // by the full FEM acceptance test for each candidate.
            var lambda=.14*current.Mechanics.VolumeMm3/
                Math.Max(.1,current.Mechanics.ComplianceNmm);
            var objectiveGradient=new double[dv.Length];
            for(int i=0;i<dv.Length;i++)objectiveGradient[i]=dv[i]+lambda*dc[i];
            var absmax=objectiveGradient.Max(v=>Math.Abs(v));
            if(absmax<1e-5)break;
            var direction=objectiveGradient.Select(v=>-v/absmax).ToArray();
            bool found=false;
            double step=1.0;
            for(int trial=0;trial<10;trial++,step*=.5)
            {
                var p=new double[dv.Length];
                for(int i=0;i<p.Length;i++)
                    p[i]=Math.Clamp(current.P[i]+step*direction[i],-8,5);
                try
                {
                    var candidate=Analyze(p);
                    if(Feasible(candidate)&&candidate.Mechanics.VolumeMm3<
                        current.Mechanics.VolumeMm3-.05)
                    {
                        // The M interpolation must not create a false
                        // mechanical PASS: reject designs for which the same
                        // FEM solved on direct F violates the constraints.
                        var directCandidate=Analyze(p,false);
                        if(!Feasible(directCandidate))
                        {
                            Console.WriteLine("REJECT: direct analytic F FEM violates limits.");
                            continue;
                        }
                        current=candidate;found=true;accepted++;
                        Store(iter,current,step,objectiveGradient.Count(v=>Math.Abs(v)>1e-6));
                        break;
                    }
                }
                catch(ArithmeticException) { /* PCG failure: reject step */ }
            }
            if(!found)
            {
                Console.WriteLine("STOP: no volume-reducing FEM-feasible 3D field step.");
                break;
            }
        }
        Ensure(accepted>0,"No design improvement found.");
        Ensure(current.Mechanics.VolumeMm3<start.Mechanics.VolumeMm3,
            "Final material is lighter than initial bridge.");
        Ensure(Feasible(current),"Final 3D design must retain both anchors and constraints.");
        Ensure(Feasible(Analyze(current.P,false)),
            "Final direct analytic F FEM must independently satisfy stress and displacement limits.");
        Ensure(current.Model.ChannelCount==5,"Geometry has exactly five M-images");
        for(int id=0;id<current.Model.VoxelCount;id++)
        {
            var total=0.0;
            for(int ch=0;ch<5;ch++)
                total+=Math.Pow(current.Model.Coefficient(ch,id),2);
            Ensure(Math.Abs(total-1)<1e-9,"M normalization");
        }
        File.WriteAllLines(Path.Combine(root,"metrics.csv"),csv);
        // Independent geometric-reconstruction control: same FEM mesh, load,
        // regularization and accepted parameters; change ONLY the F evaluator.
        var directStart=Analyze(start.P,false).Mechanics;
        var directEnd=Analyze(current.P,false).Mechanics;
        var geometricComparison=new List<string>{
            "stage,volume_from_M_mm3,volume_analytic_F_mm3,relative_volume_error_pct,vm_from_M_mpa,vm_analytic_F_mpa,relative_vm_error_pct,displacement_from_M_mm,displacement_analytic_F_mm,relative_displacement_error_pct"
        };
        void Compare(string stage,FemResults fromM,FemResults analytic)
        {
            double Relative(double a,double b)=>100*(a/Math.Max(1e-15,b)-1);
            var values=new[]{fromM.VolumeMm3,analytic.VolumeMm3,
                Relative(fromM.VolumeMm3,analytic.VolumeMm3),
                fromM.MaxVonMisesMpa,analytic.MaxVonMisesMpa,
                Relative(fromM.MaxVonMisesMpa,analytic.MaxVonMisesMpa),
                fromM.MaxDisplacementMm,analytic.MaxDisplacementMm,
                Relative(fromM.MaxDisplacementMm,analytic.MaxDisplacementMm)};
            geometricComparison.Add(stage+","+
                string.Join(",",values.Select(v=>v.ToString("G17",CultureInfo.InvariantCulture))));
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "{0}: M/direct F: dV={1:F2}%, dVM={2:F2}%, dU={3:F2}% (same FEM)",
                stage,values[2],values[5],values[8]));
        }
        Compare("initial",start.Mechanics,directStart);
        Compare("optimized",current.Mechanics,directEnd);
        File.WriteAllLines(Path.Combine(root,"fvm_vs_analytic_f.csv"),geometricComparison);
        SphereOutputs.Write(root,start,current,scene);
        var reduction=100*(1-current.Mechanics.VolumeMm3/start.Mechanics.VolumeMm3);
        File.WriteAllText(Path.Combine(root,"summary.txt"),
            string.Format(CultureInfo.InvariantCulture,
                "3D two-sphere FVM/FEM design. A=({0},{1},{2}), B=({3},{4},{5}) mm, radii=({6},{7})mm. "+
                "Fixed: core A, radius={8}mm; loaded: core B, radius={9}mm, "+
                "force=({10},{11},{12})N. E={13}MPa, nu={14}, "+
                "VM limit={15}MPa, deflection limit={16}mm. "+
                "FEM={17}x{18}x{19} Hex8, smooth indicator epsF={20}mm, E_min/E=0.0001. "+
                "Initial volume={21:F3}mm3, final volume={22:F3}mm3, reduction={23:F3}%, "+
                "steps={24}, final peak VM={25:F4}MPa, max displacement={26:F7}mm, "+
                "residual={27:E3}. Five M planes, 54 3D Gaussian F shape variables. "+
                "STL is a discretized surface; FEM stress/mesh convergence and safety factor still need engineering certification.\n",
                scene.A.X,scene.A.Y,scene.A.Z,scene.B.X,scene.B.Y,scene.B.Z,
                scene.RadiusA,scene.RadiusB,scene.SupportRadius,scene.LoadedRadius,
                forceX,forceY,forceZ,young,scene.Poisson,stressLimit,deflectionLimit,
                nx,ny,nz,Eps,start.Mechanics.VolumeMm3,current.Mechanics.VolumeMm3,
                reduction,accepted,current.Mechanics.MaxVonMisesMpa,
                current.Mechanics.MaxDisplacementMm,
                current.Mechanics.RelativeEquilibriumResidual));
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "TWO SPHERES PASS: {0} FEM-validated spatial updates, {1:F2}% material reduction, STL={2}",
            accepted,reduction,Path.Combine(root,"optimized.stl")));
        return 0;
    }

    // Six-neighbour connectivity only tests the sampled reconstructed F solid,
    // explicitly starting at sphere A and reaching sphere B. This is stricter
    // than simply checking two patch signs, though not a topological proof.
    private static bool Connected(FunctionalVoxelModel m,SphereScene scene)
    {
        var g=m.Grid;var n=g.CellCount;
        var solid=new bool[n];var start=-1;
        for(int i=0;i<n;i++)
        {
            var v=g.Centre(i);
            solid[i]=m.ValueAtCentre(i)>0;
            if(solid[i]&&(new Point3(v[0],v[1],v[2])-scene.A).Norm()<
                scene.SupportRadius)start=i;
        }
        if(start<0)return false;
        var visited=new bool[n];var q=new Queue<int>();
        visited[start]=true;q.Enqueue(start);
        while(q.Count>0)
        {
            var id=q.Dequeue();var a=g.Centre(id);
            if((new Point3(a[0],a[1],a[2])-scene.B).Norm()<scene.LoadedRadius)
                return true;
            for(int d=0;d<3;d++)for(int delta=-1;delta<=1;delta+=2)
            {
                var ix=new[]{g.Index(id,0),g.Index(id,1),g.Index(id,2)};
                ix[d]+=delta;
                if(ix[d]<0||ix[d]>=g.Count(d))continue;
                var other=g.LinearIndex(ix);
                if(solid[other]&&!visited[other]){visited[other]=true;q.Enqueue(other);}
            }
        }
        return false;
    }

    internal static class SphereOutputs
    {
        internal static void Write(string root,State before,State after,SphereScene scene)
        {
            var grid=after.Field.Grid;
            for(int ch=0;ch<5;ch++)
            {
                var channel=ch;
                WriteBitmap(Path.Combine(root,$"M{ch}_initial.bmp"),grid.Count(0)*8,grid.Count(1)*8,
                    (x,y)=>Gray(before.Model.Coefficient(channel,
                        grid.LinearIndex(x/8,y/8,grid.Count(2)/2))));
                WriteBitmap(Path.Combine(root,$"M{ch}_optimized.bmp"),grid.Count(0)*8,grid.Count(1)*8,
                    (x,y)=>Gray(after.Model.Coefficient(channel,
                        grid.LinearIndex(x/8,y/8,grid.Count(2)/2))));
            }
            var zcentre=.5*(grid.Minimum(2)+grid.Minimum(2)+
                grid.Count(2)*grid.Step(2));
            var zSlices=scene.StandardGeometry?new[]{-6.0,0.0,6.0}:
                new[]{zcentre-.6*scene.MinRadius,zcentre,zcentre+.6*scene.MinRadius};
            for(int slice=0;slice<3;slice++)
            {
                var z=zSlices[slice];
                var label=scene.StandardGeometry?
                    z.ToString("+0;-0;0",CultureInfo.InvariantCulture):
                    slice.ToString(CultureInfo.InvariantCulture);
                WriteBitmap(Path.Combine(root,$"geometry_initial_z{label}.bmp"),416,144,
                    (i,j)=>SolidPixel(before.Model,i,j,z));
                WriteBitmap(Path.Combine(root,$"geometry_optimized_z{label}.bmp"),416,144,
                    (i,j)=>SolidPixel(after.Model,i,j,z));
                WriteBitmap(Path.Combine(root,$"geometry_diff_z{label}.bmp"),416,144,
                    (i,j)=>
                    {
                        var a=Solid(before.Model,i,j,z);var b=Solid(after.Model,i,j,z);
                        return a&&!b?((byte)230,(byte)80,(byte)60):
                            !a&&b?((byte)70,(byte)170,(byte)75):
                            a?((byte)105,(byte)105,(byte)105):((byte)248,(byte)248,(byte)248);
                    });
            }
            WriteStl(Path.Combine(root,"initial.stl"),before.Model,scene);
            WriteStl(Path.Combine(root,"optimized.stl"),after.Model,scene);
        }

        private static double X(FunctionalVoxelModel m,int i)=>
            m.Grid.Minimum(0)+(i+.5)*m.Grid.Step(0)*m.Grid.Count(0)/416;
        private static double Y(FunctionalVoxelModel m,int j)=>
            m.Grid.Minimum(1)+(j+.5)*m.Grid.Step(1)*m.Grid.Count(1)/144;
        private static bool Solid(FunctionalVoxelModel m,int i,int j,double z) =>
            m.Approximate(new[]{X(m,i),Y(m,j),z})>0;
        private static (byte,byte,byte) SolidPixel(FunctionalVoxelModel m,int i,int j,double z)=>
            Solid(m,i,j,z)?((byte)50,(byte)135,(byte)195):((byte)248,(byte)248,(byte)248);
        private static (byte,byte,byte) Gray(double n)
        {
            var v=(byte)Math.Clamp(Math.Round(127.5*(1+Math.Clamp(n,-1,1))),0,255);
            return(v,v,v);
        }
        private static void WriteBitmap(string path,int w,int h,Func<int,int,(byte R,byte G,byte B)> pixel)
        {
            var stride=(3*w+3)&~3;
            using var b=new BinaryWriter(File.Create(path));
            b.Write((byte)'B');b.Write((byte)'M');b.Write(54+stride*h);b.Write(0);b.Write(54);
            b.Write(40);b.Write(w);b.Write(h);b.Write((short)1);b.Write((short)24);
            b.Write(0);b.Write(stride*h);b.Write(2835);b.Write(2835);b.Write(0);b.Write(0);
            for(int j=0;j<h;j++)
            {
                for(int i=0;i<w;i++)
                {
                    var c=pixel(i,j);b.Write(c.B);b.Write(c.G);b.Write(c.R);
                }
                for(int i=w*3;i<stride;i++)b.Write((byte)0);
            }
        }

        // Marching tetrahedra, six consistent tetrahedra per background cube.
        // STL triangles are oriented using the outward implicit normal -grad(F).
        // The geometry is extracted from the *same five-M reconstruction* as FEM.
        private readonly record struct V(double X,double Y,double Z)
        {
            public static V operator +(V a,V b)=>new(a.X+b.X,a.Y+b.Y,a.Z+b.Z);
            public static V operator -(V a,V b)=>new(a.X-b.X,a.Y-b.Y,a.Z-b.Z);
            public static V operator *(V a,double s)=>new(a.X*s,a.Y*s,a.Z*s);
        }
        private static V Cross(V a,V b)=>new(a.Y*b.Z-a.Z*b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X);
        private static double Dot(V a,V b)=>a.X*b.X+a.Y*b.Y+a.Z*b.Z;
        private static V Normal(V a)
        {
            var d=Math.Sqrt(Dot(a,a));
            return d>1e-14?a*(1/d):new V(0,0,0);
        }
        internal static void WriteStl(string path,FunctionalVoxelModel model,SphereScene? scene)
        {
            var grid=model.Grid;
            int nx=scene?.StandardGeometry==true?52:2*grid.Count(0);
            int ny=scene?.StandardGeometry==true?18:2*grid.Count(1);
            int nz=scene?.StandardGeometry==true?18:2*grid.Count(2);
            var ox=grid.Minimum(0);var oy=grid.Minimum(1);var oz=grid.Minimum(2);
            var hx=grid.Count(0)*grid.Step(0)/nx;
            var hy=grid.Count(1)*grid.Step(1)/ny;
            var hz=grid.Count(2)*grid.Step(2)/nz;
            var vals=new double[nx+1,ny+1,nz+1];
            for(int i=0;i<=nx;i++)for(int j=0;j<=ny;j++)for(int k=0;k<=nz;k++)
                vals[i,j,k]=model.Approximate(new[]{ox+hx*i,oy+hy*j,oz+hz*k});
            // Do NOT orient facets independently using noisy central-gradient
            // samples. The numerical FVM reconstruction is not a signed-distance
            // field and gradient-based orientation may flip adjacent faces.
            // Gather facets first; propagate a consistent edge winding over the
            // shared topological mesh, then orient every connected shell outward.
            var triangles=new List<(V A,V B,V C)>();
            // Cache each crossing by global grid-edge node IDs. Computing the
            // same edge in opposite tetrahedra otherwise changes the last
            // floating-point bits and can break STL vertex identity.
            var intersectionVertices=new Dictionary<(int,int),V>();
            void Triangle(V a,V b,V c)
            {
                if(Dot(Cross(b-a,c-a),Cross(b-a,c-a))<1e-26)return;
                triangles.Add((a,b,c));
            }
            var offsets=new[]{new[]{0,0,0},new[]{1,0,0},new[]{1,1,0},new[]{0,1,0},
                              new[]{0,0,1},new[]{1,0,1},new[]{1,1,1},new[]{0,1,1}};
            var tets=new[]{new[]{0,5,1,6},new[]{0,1,2,6},new[]{0,2,3,6},
                           new[]{0,3,7,6},new[]{0,7,4,6},new[]{0,4,5,6}};
            for(int i=0;i<nx;i++)for(int j=0;j<ny;j++)for(int k=0;k<nz;k++)
            {
                var point=new V[8];var sign=new double[8];
                for(int a=0;a<8;a++)
                {
                    var oi=offsets[a][0];var oj=offsets[a][1];var ok=offsets[a][2];
                    point[a]=new V(ox+hx*(i+oi),oy+hy*(j+oj),oz+hz*(k+ok));
                    sign[a]=vals[i+oi,j+oj,k+ok];
                }
                foreach(var tet in tets)
                {
                    var inside=tet.Where(t=>sign[t]>=0).ToArray();
                    var outside=tet.Where(t=>sign[t]<0).ToArray();
                    if(inside.Length==0||inside.Length==4)continue;
                    V Edge(int a,int b)
                    {
                        int Global(int n)=>
                            ((i+offsets[n][0])*(ny+1)+(j+offsets[n][1]))*(nz+1)
                            +(k+offsets[n][2]);
                        if(Global(a)>Global(b))(a,b)=(b,a);
                        var key=(Global(a),Global(b));
                        if(intersectionVertices.TryGetValue(key,out var cached))return cached;
                        var d=sign[a]-sign[b];
                        var t=Math.Abs(d)<1e-14?.5:Math.Clamp(sign[a]/d,0,1);
                        var vertex=point[a]+(point[b]-point[a])*t;
                        intersectionVertices.Add(key,vertex);
                        return vertex;
                    }
                    if(inside.Length==1)
                    {
                        var a=inside[0];
                        Triangle(Edge(a,outside[0]),Edge(a,outside[1]),Edge(a,outside[2]));
                    }
                    else if(inside.Length==3)
                    {
                        var b=outside[0];
                        Triangle(Edge(b,inside[0]),Edge(b,inside[1]),Edge(b,inside[2]));
                    }
                    else
                    {
                        var a=inside[0];var b=inside[1];var c=outside[0];var d=outside[1];
                        var ac=Edge(a,c);var ad=Edge(a,d);var bc=Edge(b,c);var bd=Edge(b,d);
                        Triangle(ac,ad,bc);Triangle(ad,bd,bc);
                    }
                }
            }
            if(triangles.Count<100)
                throw new InvalidOperationException("STL produced too few surface facets.");

            // Match vertices after the G9 STL decimal serialization. On every
            // undirected edge there must be exactly two triangles with opposing
            // directions. If one direction disagrees, flip the entire connected
            // neighbourhood instead of introducing a local crack or inversion.
            (long X,long Y,long Z) VertexId(V v) =>
                ((long)Math.Round(v.X*10000),(long)Math.Round(v.Y*10000),
                 (long)Math.Round(v.Z*10000));
            var edges=new Dictionary<
                ((long,long,long) A,(long,long,long) B),List<(int Triangle,int Direction)>>();
            for(var i=0;i<triangles.Count;i++)
            {
                var (a,b,c)=triangles[i];
                foreach(var (from,to) in new[]{(a,b),(b,c),(c,a)})
                {
                    var v=VertexId(from);var u=VertexId(to);
                    if(v==u)throw new InvalidOperationException("Collapsed STL edge.");
                    var forward=Comparer<(long,long,long)>.Default.Compare(v,u)<0;
                    var key=forward?(v,u):(u,v);
                    if(!edges.TryGetValue(key,out var list))
                        edges[key]=list=new List<(int,int)>(2);
                    list.Add((i,forward?1:-1));
                }
            }
            var neighbours=new List<(int Other,int Rule)>[triangles.Count];
            for(var i=0;i<neighbours.Length;i++)neighbours[i]=new();
            foreach(var edge in edges.Values)
            {
                if(edge.Count!=2)
                    throw new InvalidOperationException(
                        $"STL not watertight: edge has {edge.Count} incident facets.");
                var a=edge[0];var b=edge[1];
                var rule=a.Direction==b.Direction?-1:1;
                neighbours[a.Triangle].Add((b.Triangle,rule));
                neighbours[b.Triangle].Add((a.Triangle,rule));
            }
            var orientation=new int[triangles.Count];
            var shells=0;
            for(int start=0;start<triangles.Count;start++)
            {
                if(orientation[start]!=0)continue;
                shells++;
                var queue=new Queue<int>();var component=new List<int>();
                orientation[start]=1;queue.Enqueue(start);
                while(queue.Count!=0)
                {
                    var i=queue.Dequeue();
                    component.Add(i);
                    foreach(var (other,rule) in neighbours[i])
                    {
                        var expected=orientation[i]*rule;
                        if(orientation[other]==0)
                        {
                            orientation[other]=expected;queue.Enqueue(other);
                        }
                        else if(orientation[other]!=expected)
                            throw new InvalidOperationException(
                                "STL has a non-orientable or inconsistent manifold component.");
                    }
                }
                double signedVolume=0;
                foreach(var id in component)
                {
                    var (a,b,c)=triangles[id];
                    signedVolume+=Dot(a,Cross(b,c))*orientation[id]/6;
                }
                if(Math.Abs(signedVolume)<1e-6)
                    throw new InvalidOperationException("STL shell has zero signed volume.");
                if(signedVolume<0)
                    foreach(var id in component)orientation[id]*=-1;
            }
            using var w=new StreamWriter(path,false,new UTF8Encoding(false));
            w.WriteLine("solid two_spheres_fvm_fem");
            string F(double d)=>d.ToString("G9",CultureInfo.InvariantCulture);
            for(int i=0;i<triangles.Count;i++)
            {
                var (a,b,c)=triangles[i];
                if(orientation[i]<0)(b,c)=(c,b);
                var n=Normal(Cross(b-a,c-a));
                w.WriteLine($"  facet normal {F(n.X)} {F(n.Y)} {F(n.Z)}");
                w.WriteLine("    outer loop");
                foreach(var v in new[]{a,b,c})
                    w.WriteLine($"      vertex {F(v.X)} {F(v.Y)} {F(v.Z)}");
                w.WriteLine("    endloop");
                w.WriteLine("  endfacet");
            }
            w.WriteLine("endsolid two_spheres_fvm_fem");
            Console.WriteLine($"STL {path}: {triangles.Count} triangles, {shells} closed oriented shell(s), marching tetrahedra.");

        }
    }
}
