using System.Globalization;
using System.Text;
using DCad.FunctionalVoxel;

namespace FvmGenerativeDemo;

internal static class Program
{
    private const double Length = 100, Radius = 8.5, Young = 200000, Poisson = 0.30;
    private const double LoadN = 1000, AllowableStress = 100, AllowableDisplacement = 0.030;
    private const int Nx = 8, Ny = 6, Nz = 6;
    private static readonly FunctionalGrid Grid =
        new(new[]{0.0,-12.0,-12.0},new[]{Length/Nx,24.0/Ny,24.0/Nz},new[]{Nx,Ny,Nz});

    private static int Main(string[] args)
    {
        Console.OutputEncoding=Encoding.UTF8;
        var self=args.Contains("--self-test",StringComparer.OrdinalIgnoreCase);
        try
        {
            if(args.Contains("--multiphysics-self-test",StringComparer.OrdinalIgnoreCase))
                return MultiphysicsSelfTest.Run();
            if(args.Contains("--multi-scene",StringComparer.OrdinalIgnoreCase))
                return UniversalDesign.Run(args);
            if(args.Contains("--two-spheres",StringComparer.OrdinalIgnoreCase))
                return TwoSphereDesign.Run(args);
            if(args.Length>=2 && args[0]=="--export-element-ke")
            {
                var fixture=new HexFem(1,1,1,0,0,0,10,10,10,Young,Poisson,
                    (x,y,z)=>1000,null,1000,10);
                fixture.ExportFirstElementStiffness(args[1]);
                Console.WriteLine("PASS exported 24x24 reference Hex8 stiffness in N/mm");
                return 0;
            }
            if(self)
            {
                TestMImages();
                TestBar();
                TestRefinement();
            }
            var iterations=self?2:3;
            for(int i=0;i<args.Length-1;i++)
                if(args[i]=="--iterations") iterations=int.Parse(args[i+1],CultureInfo.InvariantCulture);
            var output="output";
            for(int i=0;i<args.Length-1;i++) if(args[i]=="--out") output=args[i+1];
            if(iterations<1||iterations>20)throw new ArgumentOutOfRangeException(nameof(iterations));
            Directory.CreateDirectory(output);
            var (start,finish,steps)=Optimize(iterations,output);
            if(self)
            {
                Require(steps>=2,"Two accepted FEM-driven geometry updates");
                Require(finish.VolumeMm3<start.VolumeMm3-1,"Volume decreased");
                Require(finish.MaxVonMisesMpa<AllowableStress,"Final stress constraint");
                Require(finish.MaxDisplacementMm<AllowableDisplacement,"Final displacement constraint");
                Require(finish.RelativeEquilibriumResidual<1e-6,"Final global FEM residual");
                Console.WriteLine("SELF-TEST PASS: geometry / M0..M4 / R-functions / bar / FEM / sensitivities / shape updates");
            }
            return 0;
        }
        catch(Exception ex)
        {
            Console.Error.WriteLine($"SELF-TEST/EXPERIMENT FAIL: {ex}");
            return 1;
        }
    }

    // The actual parameterization moves the implicit surface F=0.
    // Sinusoidal basis enforces exact protected end radii at x=0 and x=L.
    private static FunctionalField Field(double[] p)
    {
        return FunctionalField.Analytic(3, v =>
        {
            var x=v[0];var y=v[1];var z=v[2];
            var s=Math.Sin(Math.PI*x/Length);
            var c=Math.Cos(Math.PI*x/Length);
            var s2=s*s;var s4=s2*s2;
            var radius=Radius-p[0]*s2-p[1]*s4;
            var dr=-2*Math.PI/Length*s*c*(p[0]+2*p[1]*s2);
            return FunctionalJet.FromValueGradient(radius*radius-y*y-z*z,
                new[]{2*radius*dr,-2*y,-2*z});
        });
    }
    private static (FunctionalVoxelModel Model,Func<double,double,double,double> Analytic) Geometry(double[] p)
    {
        var field=Field(p);
        return (FunctionalVoxelModel.Sample(field,Grid),
            (x,y,z)=>field.Evaluate(x,y,z).Value);
    }

    private static (FemResults Result,FunctionalVoxelModel Model) Analyze(double[] p, bool useM=true)
    {
        var (model,analytic)=Geometry(p);
        // The loaded and supported interface patch is mandatory material.
        // Check the ACTUAL reconstructed five-M geometry, not only analytic F.
        foreach(var endX in new[]{0.0,Length})
            foreach(var yy in new[]{-4.0,0.0,4.0})
                foreach(var zz in new[]{-4.0,0.0,4.0})
                    if(model.Approximate(new[]{endX,yy,zz})<=0)
                        throw new InvalidOperationException("Mandatory load/support interface was removed.");
        var solver=new HexFem(Nx,Ny,Nz,0,-12,-12,Length/Nx,24.0/Ny,24.0/Nz,
            Young,Poisson,analytic,useM?model:null,LoadN,4.1);
        var result=solver.Solve();
        if(Math.Abs(result.ReactionXN+LoadN)>Math.Max(0.1,LoadN*1e-5))
            throw new ArithmeticException($"Reaction balance failed: {result.ReactionXN}");
        if(result.RelativeEquilibriumResidual>1e-6)
            throw new ArithmeticException($"Free DOF equilibrium failed: {result.RelativeEquilibriumResidual}");
        return (result,model);
    }

    private static bool Feasible(FemResults r) =>
        double.IsFinite(r.MaxVonMisesMpa) && double.IsFinite(r.MaxDisplacementMm) &&
        r.MaxVonMisesMpa<=AllowableStress &&
        r.MaxDisplacementMm<=AllowableDisplacement;

    private static void Log(string label,double[] p,FemResults r,string detail="")
    {
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "{0,-12} p=({1:F3},{2:F3}) V={3:F2}mm³ mass={4:F6}kg VMmax={5:F3}MPa |u|max={6:F6}mm C={7:F6}Nmm R_x={8:F4}N residual={9:E2} CG={10} feasible={11} {12}",
            label,p[0],p[1],r.VolumeMm3,r.MassKg,r.MaxVonMisesMpa,
            r.MaxDisplacementMm,r.ComplianceNmm,r.ReactionXN,
            r.RelativeEquilibriumResidual,r.CgIterations,Feasible(r),detail));
    }

    private static (FemResults Before,FemResults After,int Steps) Optimize(int limit,string output)
    {
        var p=new[]{0.0,0.0};
        var (current,currentModel)=Analyze(p);
        if(!Feasible(current)) throw new InvalidOperationException("Initial design violates prescribed constraints; aborting.");
        var (direct,_) = Analyze(p,false);
        Log("baseline-M",p,current);
        Log("direct-F",p,direct,"no M reconstruction: fair geometric-control baseline");
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "FVM reconstruction effect: dV={0:F3}% dVM={1:F3}% dU={2:F3}%",
            100*(current.VolumeMm3/direct.VolumeMm3-1),
            100*(current.MaxVonMisesMpa/direct.MaxVonMisesMpa-1),
            100*(current.MaxDisplacementMm/direct.MaxDisplacementMm-1)));
        var first=current;var firstModel=currentModel;
        var lines=new List<string>
        {
            "iteration,p0_mm,p1_mm,volume_mm3,mass_kg,vm_max_mpa,u_max_mm,compliance_Nmm,reaction_x_N,residual,feasible,delta_volume_mm3"
        };
        void SaveMetric(int index,double[] pp,FemResults rr,double delta) =>
            lines.Add(string.Join(",",new[]{
                index.ToString(CultureInfo.InvariantCulture),
                pp[0].ToString("G17",CultureInfo.InvariantCulture),
                pp[1].ToString("G17",CultureInfo.InvariantCulture),
                rr.VolumeMm3.ToString("G17",CultureInfo.InvariantCulture),
                rr.MassKg.ToString("G17",CultureInfo.InvariantCulture),
                rr.MaxVonMisesMpa.ToString("G17",CultureInfo.InvariantCulture),
                rr.MaxDisplacementMm.ToString("G17",CultureInfo.InvariantCulture),
                rr.ComplianceNmm.ToString("G17",CultureInfo.InvariantCulture),
                rr.ReactionXN.ToString("G17",CultureInfo.InvariantCulture),
                rr.RelativeEquilibriumResidual.ToString("G17",CultureInfo.InvariantCulture),
                Feasible(rr).ToString(),delta.ToString("G17",CultureInfo.InvariantCulture)}));
        SaveMetric(0,p,current,0);
        var steps=0;
        for(int iteration=1;iteration<=limit;iteration++)
        {
            var dv=new double[2];var ds=new double[2];var du=new double[2];
            const double h=0.08;
            for(int i=0;i<2;i++)
            {
                var plus=(double[])p.Clone();var minus=(double[])p.Clone();
                plus[i]+=h;minus[i]-=h;
                var a=Analyze(plus).Result;var b=Analyze(minus).Result;
                dv[i]=(a.VolumeMm3-b.VolumeMm3)/(2*h);
                ds[i]=(a.MaxVonMisesMpa-b.MaxVonMisesMpa)/(2*h);
                du[i]=(a.MaxDisplacementMm-b.MaxDisplacementMm)/(2*h);
            }
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "sensitivities {0}: dV/dp=({1:F3},{2:F3}) mm²; dVM/dp=({3:F3},{4:F3}) MPa/mm; dU/dp=({5:E3},{6:E3})",
                iteration,dv[0],dv[1],ds[0],ds[1],du[0],du[1]));
            // A volume descent direction limited by the *computed* mechanical
            // derivatives, followed by a full nonlinear-in-geometry FEM acceptance test.
            var norm=Math.Sqrt(dv[0]*dv[0]+dv[1]*dv[1]);
            if(norm<1e-8){Console.WriteLine("STOP: vanishing volume sensitivity");break;}
            var dir=new[]{-dv[0]/norm,-dv[1]/norm};
            var step=0.42;
            var predictedSigmaSlope=ds[0]*dir[0]+ds[1]*dir[1];
            var predictedDispSlope=du[0]*dir[0]+du[1]*dir[1];
            if(predictedSigmaSlope>0)
                step=Math.Min(step,0.80*(AllowableStress-current.MaxVonMisesMpa)/predictedSigmaSlope);
            if(predictedDispSlope>0)
                step=Math.Min(step,0.80*(AllowableDisplacement-current.MaxDisplacementMm)/predictedDispSlope);
            var accepted=false;
            for(var backtrack=0;backtrack<9&&step>1e-4;backtrack++,step*=0.5)
            {
                var proposal=new[]{p[0]+step*dir[0],p[1]+step*dir[1]};
                if(proposal[0]<0||proposal[1]<0||proposal[0]+proposal[1]>3.2)continue;
                var (next,nextModel)=Analyze(proposal);
                if(Feasible(next)&&next.VolumeMm3<current.VolumeMm3-0.005)
                {
                    AssertMRebuild(nextModel,Field(proposal),1e-8);
                    var diff=next.VolumeMm3-current.VolumeMm3;
                    p=proposal; current=next;currentModel=nextModel;
                    Log($"step-{iteration}",p,current,$"dV={diff:F3}mm³ FEM accepted");
                    SaveMetric(iteration,p,current,diff);
                    steps++;accepted=true;break;
                }
            }
            if(!accepted)
            {
                Console.WriteLine("STOP: no mechanically feasible volume-decreasing shape step.");
                break;
            }
        }
        File.WriteAllLines(Path.Combine(output,"metrics.csv"),lines);
        Visuals.Write(output,Grid,firstModel,currentModel,first,current);
        var comparison=new StringBuilder();
        comparison.AppendLine("# Direct analytic F vs reconstruction from five M-images");
        comparison.AppendLine("Both use exactly the same 3D Hex8 elements, Gauss points, load and material.");
        comparison.AppendLine($"Direct F: V={direct.VolumeMm3:G9} mm3, VM={direct.MaxVonMisesMpa:G9} MPa, U={direct.MaxDisplacementMm:G9} mm.");
        comparison.AppendLine($"Five M: V={first.VolumeMm3:G9} mm3, VM={first.MaxVonMisesMpa:G9} MPa, U={first.MaxDisplacementMm:G9} mm.");
        comparison.AppendLine($"Final five M: V={current.VolumeMm3:G9} mm3, VM={current.MaxVonMisesMpa:G9} MPa, U={current.MaxDisplacementMm:G9} mm.");
        comparison.AppendLine("This is NOT a numerical comparison with the original Python FEM_Voxel SIMP/OC.");
        File.WriteAllText(Path.Combine(output,"comparison.txt"),comparison.ToString());
        Console.WriteLine($"Result: {steps} accepted changes; images and CSV in {Path.GetFullPath(output)}");
        if(steps==0)Console.WriteLine("UNSATISFIED: optimizer could not find a feasible improvement.");
        return (first,current,steps);
    }

    private static void Require(bool condition,string text)
    {
        if(!condition) throw new InvalidOperationException("FAIL "+text);
        Console.WriteLine("PASS "+text);
    }

    private static void AssertMRebuild(FunctionalVoxelModel model,FunctionalField field,double tolerance)
    {
        Require(model.ChannelCount==5,"exactly five M-images");
        double largest=0;
        for(int id=0;id<model.VoxelCount;id++)
        {
            var pos=model.Grid.Centre(id);
            var expect=field.Evaluate(pos);
            var actual=model.JetAtCentre(id);
            largest=Math.Max(largest,Math.Abs(expect.Value-actual.Value));
            for(int axis=0;axis<3;axis++)
                largest=Math.Max(largest,Math.Abs(expect.Gradient(axis)-actual.Gradient(axis)));
            var sum=0.0;
            for(int channel=0;channel<5;channel++)
                sum+=Math.Pow(model.Coefficient(channel,id),2);
            if(Math.Abs(sum-1)>1e-10) throw new ArithmeticException("M-plane is not unit normalized.");
        }
        if(largest>tolerance)throw new ArithmeticException($"F/M jets inconsistent: {largest}");
        Console.WriteLine($"PASS M-image jet reconstruction max error={largest:E3}");
    }

    private static void TestMImages()
    {
        var g=new FunctionalGrid(new[]{1.0,2.0,3.0},new[]{2.0,3.0,4.0},new[]{5,4,3});
        Require(g.LinearIndex(3,2,1)==(3*4+2)*3+1,"C# 5x4x3 index and XYZ order");
        var line=FunctionalField.Analytic(3, x =>
            FunctionalJet.FromValueGradient(5+2*x[0]-3*x[1]+4*x[2],new[]{2.0,-3.0,4.0}));
        var model=FunctionalVoxelModel.Sample(line,g);
        AssertMRebuild(model,line,1e-9);
        var probe=g.Centre(g.LinearIndex(3,2,1));
        Require(Math.Abs(model.Approximate(probe)-line.Evaluate(probe).Value)<1e-9,
            "five-M continuous field at centre");
        var sphere=FunctionalField.Sphere(0,0,0,4);
        var drill=FunctionalField.CylinderZ(0,0,1,-5,5);
        var difference=sphere.Difference(drill);
        Require(difference.Evaluate(2,0,0).Value>0,"R-difference retained shell sign");
        Require(difference.Evaluate(0.2,0,0).Value<0,"R-difference drilled cavity sign");
        Require(difference.Evaluate(5,0,0).Value<0,"R-difference exterior sign");
        Require(sphere.Union(drill).Evaluate(0,0,0).Value>0,"R-union interior sign");
    }

    private static void TestRefinement()
    {
        // Same background FEM, same material and loading; change *only* M sampling.
        var p=new[]{0.0,0.0};
        var analytic=Field(p);
        var refinedGrid=new FunctionalGrid(new[]{0.0,-12.0,-12.0},
            new[]{Length/16.0,2.0,2.0},new[]{16,12,12});
        var refined=FunctionalVoxelModel.Sample(analytic,refinedGrid);
        var field=(Func<double,double,double,double>)((x,y,z)=>analytic.Evaluate(x,y,z).Value);
        FemResults SolveOn(FunctionalVoxelModel? m) =>
            new HexFem(Nx,Ny,Nz,0,-12,-12,Length/Nx,24.0/Ny,24.0/Nz,
                Young,Poisson,field,m,LoadN,4.1).Solve();
        var direct=SolveOn(null);
        var coarse=SolveOn(FunctionalVoxelModel.Sample(analytic,Grid));
        var fine=SolveOn(refined);
        var errorCoarse=Math.Abs(coarse.VolumeMm3-direct.VolumeMm3);
        var errorFine=Math.Abs(fine.VolumeMm3-direct.VolumeMm3);
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "FVM sampling convergence: direct V={0:F3}, 8x6x6 V={1:F3} error={2:F3}, 16x12x12 V={3:F3} error={4:F3} mm³",
            direct.VolumeMm3,coarse.VolumeMm3,errorCoarse,fine.VolumeMm3,errorFine));
        Require(errorFine<errorCoarse*0.70,"refining five-M lattice reduces this fixture's volume bias");
    }

    private static void TestBar()
    {
        var barGrid=new FunctionalGrid(new[]{0.0,-5.0,-5.0},
            new[]{12.5,5.0,5.0},new[]{8,2,2});
        var model=FunctionalVoxelModel.Sample(FunctionalField.Constant(3,100),barGrid);
        var fem=new HexFem(8,2,2,0,-5,-5,12.5,5,5,
            200000,0,(x,y,z)=>100,model,1000,5);
        var r=fem.Solve();
        Log("axial-bar",new[]{0.0,0.0},r);
        Require(Math.Abs(r.MaxVonMisesMpa-10)<0.20,"bar nominal stress 10 MPa");
        Require(Math.Abs(r.MaxDisplacementMm-0.005)<0.0001,"bar axial extension 0.005 mm");
        Require(Math.Abs(r.ReactionXN+1000)<0.1,"bar reaction = -1000 N");
        Require(r.RelativeEquilibriumResidual<1e-6,"bar equilibrium");
        Require(Math.Abs(r.VolumeMm3-10000)<1e-5,"bar volume 10000 mm3");
    }
}

internal static class Visuals
{
    internal static void Write(string root,FunctionalGrid grid,FunctionalVoxelModel before,
        FunctionalVoxelModel after,FemResults mechanicsBefore,FemResults mechanicsAfter)
    {
        var stressesScale=Math.Max(mechanicsBefore.MaxVonMisesMpa,mechanicsAfter.MaxVonMisesMpa);
        for(int slice=0;slice<3;slice++)
        {
            var z=-6.0+6*slice;
            var suffix=$"z{slice}";
            Bitmap(Path.Combine(root,$"before_geometry_{suffix}.bmp"),150,100,(i,j)=>
                GeometryPixel(before,i,j,z));
            Bitmap(Path.Combine(root,$"after_geometry_{suffix}.bmp"),150,100,(i,j)=>
                GeometryPixel(after,i,j,z));
            Bitmap(Path.Combine(root,$"before_stress_{suffix}.bmp"),150,100,(i,j)=>
                StressPixel(before,mechanicsBefore,i,j,z,stressesScale));
            Bitmap(Path.Combine(root,$"after_stress_{suffix}.bmp"),150,100,(i,j)=>
                StressPixel(after,mechanicsAfter,i,j,z,stressesScale));
            Bitmap(Path.Combine(root,$"material_change_{suffix}.bmp"),150,100,(i,j)=>
            {
                var x=(i+0.5)*100/150;var y=-12+(j+0.5)*24/100;
                var was=before.Approximate(new[]{x,y,z})>=0;
                var now=after.Approximate(new[]{x,y,z})>=0;
                if(was&&!now)return ((byte)230,(byte)75,(byte)50);
                if(!was&&now)return ((byte)55,(byte)165,(byte)70);
                return was?((byte)90,(byte)90,(byte)90):((byte)250,(byte)250,(byte)250);
            });
        }
        for(int layer=0;layer<5;layer++)
        {
            var c=layer;
            // Nearest-neighbour enlargement: preserve raw coefficients while making BMP readable.
            Bitmap(Path.Combine(root,$"before_M{c}.bmp"),16*grid.Count(0),16*grid.Count(1),(i,j)=>
                Gray(before.Coefficient(c,grid.LinearIndex(i/16,j/16,grid.Count(2)/2))));
            Bitmap(Path.Combine(root,$"after_M{c}.bmp"),16*grid.Count(0),16*grid.Count(1),(i,j)=>
                Gray(after.Coefficient(c,grid.LinearIndex(i/16,j/16,grid.Count(2)/2))));
        }
    }

    private static (byte,byte,byte) GeometryPixel(FunctionalVoxelModel model,int i,int j,double z)
    {
        var x=(i+0.5)*100/150;var y=-12+(j+0.5)*24/100;
        return model.Approximate(new[]{x,y,z})>=0?((byte)30,(byte)125,(byte)190):
            ((byte)248,(byte)248,(byte)248);
    }
    private static (byte,byte,byte) StressPixel(FunctionalVoxelModel model,FemResults r,int i,int j,double z,double scale)
    {
        var x=(i+0.5)*100/150;var y=-12+(j+0.5)*24/100;
        if(model.Approximate(new[]{x,y,z})<0)return (248,248,248);
        var ix=Math.Clamp((int)(x/12.5),0,7);
        var iy=Math.Clamp((int)((y+12)/4),0,5);
        var iz=Math.Clamp((int)((z+12)/4),0,5);
        var vm=r.ElementVonMises[(ix*6+iy)*6+iz];
        var t=Math.Clamp(vm/Math.Max(1e-12,scale),0,1);
        return ((byte)(255*t),(byte)(55+130*(1-t)),(byte)(235*(1-t)));
    }
    private static (byte,byte,byte) Gray(double n)
    {
        var c=(byte)Math.Clamp(Math.Round(127.5*(Math.Clamp(n,-1,1)+1)),0,255);
        return (c,c,c);
    }
    private static void Bitmap(string path,int w,int h,Func<int,int,(byte R,byte G,byte B)> color)
    {
        var stride=(w*3+3)&~3;
        using var bw=new BinaryWriter(File.Create(path));
        bw.Write((byte)'B');bw.Write((byte)'M');bw.Write(54+stride*h);bw.Write(0);bw.Write(54);
        bw.Write(40);bw.Write(w);bw.Write(h);bw.Write((short)1);bw.Write((short)24);
        bw.Write(0);bw.Write(stride*h);bw.Write(2835);bw.Write(2835);bw.Write(0);bw.Write(0);
        for(int j=0;j<h;j++)
        {
            for(int i=0;i<w;i++)
            {
                var (r,g,b)=color(i,j);
                bw.Write(b);bw.Write(g);bw.Write(r);
            }
            for(int k=w*3;k<stride;k++)bw.Write((byte)0);
        }
    }
}
