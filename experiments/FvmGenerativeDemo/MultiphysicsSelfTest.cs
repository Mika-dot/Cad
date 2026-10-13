namespace FvmGenerativeDemo;

internal static class MultiphysicsSelfTest
{
    private static void Check(bool success,string problem)
    {
        if(!success)throw new InvalidOperationException("MULTIPHYSICS ANALYTIC FAIL: "+problem);
    }
    internal static int Run()
    {
        const double e=200000,force=1000,length=100,area=100;
        // Three-dimensional Hex8 rod under uniaxial traction, nu=0.
        var fem=new HexFem(4,1,1,0,0,0,25,10,10,
            e,0,(_x,_y,_z)=>1,null,
            loadN:force,interfaceHalfWidth:20,
            emin:1e-5,epsF:.1);
        var stat=fem.Solve();
        var expected=force*length/(e*area);
        Check(Math.Abs(stat.MaxDisplacementMm-expected)<2e-6,
            "Static reference bar displacement P*L/(E*A)");
        Check(Math.Abs(stat.MaxVonMisesMpa-force/area)<1e-4,
            "Static reference bar stress P/A");
        Check(Math.Abs(stat.ReactionXN+force)<1e-4,"Static force balance");

        // True prescribed external-face pressure via consistent Hex8 surface
        // quadrature, not force/number-of-volume-nodes. Test three grids and
        // independent normal orientations with reaction equilibrium.
        foreach(var divisions in new[]{4,8,16})
        {
            var pressureBar=new HexFem(divisions,2,2,0,0,0,
                length/divisions,5,5,e,0,(_x,_y,_z)=>1,null,
                loadN:0,interfaceHalfWidth:0,emin:1e-5,epsF:.1,
                boundaryPressures:new[]{
                    new HexBoundaryPressure("xmax",force/area)
                });
            var loaded=pressureBar.Solve();
            Check(Math.Abs(loaded.ReactionXN+force)<1e-5,
                "External face traction must balance total physical pressure * area");
            Check(Math.Abs(loaded.MaxDisplacementMm-expected)<1e-6,
                "True traction Hex8 rod u=pressure*length/E independent of mesh size");
            Check(Math.Abs(loaded.MaxVonMisesMpa-force/area)<1e-3,
                "True traction Hex8 von Mises agrees with uniform axial stress");
            Console.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "FACE PRESSURE PASS: {0}x2x2 Hex8, p={1:G7} MPa, Fx={2:G7} N, reaction={3:G8} N, u={4:G8} mm",
                divisions,force/area,force,loaded.ReactionXN,loaded.MaxDisplacementMm));
        }
        var allFaces=new HexFem(4,2,2,0,0,0,25,5,5,
            e,0,(_x,_y,_z)=>1,null,
            loadN:0,interfaceHalfWidth:0,emin:1e-5,epsF:.1,
            boundaryPressures:new[]{
                new HexBoundaryPressure("xmax",10), // +1000 N = 10 MPa * 100 mm²
                new HexBoundaryPressure("ymax",1),  // +1000 N = 1 MPa * 1000 mm²
                new HexBoundaryPressure("zmin",2)   // -2000 N = 2 MPa * 1000 mm²
            });
        var reactions=allFaces.Solve();
        Check(Math.Abs(reactions.ReactionXN+1000)<1e-4 &&
              Math.Abs(reactions.ReactionYN+1000)<1e-4 &&
              Math.Abs(reactions.ReactionZN-2000)<1e-4,
              "Mixed X/Y/Z surface pressures must balance all applied forces, including supported face nodes");

        // The same exact physical pressure integrated over arbitrary triangle
        // patches. This exercises background Hex8 interpolation instead of
        // relying on an axis-aligned face selector.
        foreach(var (axialCells,faceCells) in new[]{(4,1),(8,2),(16,4)})
        {
            var patch=new List<HexTrianglePressure>();
            var increment=10.0/faceCells;
            for(var j=0;j<faceCells;j++)for(var k=0;k<faceCells;k++)
            {
                var y0=j*increment;var y1=(j+1)*increment;
                var z0=k*increment;var z1=(k+1)*increment;
                var a=new Point3(length,y0,z0);
                var b=new Point3(length,y1,z0);
                var c=new Point3(length,y1,z1);
                var d=new Point3(length,y0,z1);
                patch.Add(new HexTrianglePressure(a,b,c,force/area));
                patch.Add(new HexTrianglePressure(a,c,d,force/area));
            }
            var immersed=new HexFem(axialCells,faceCells,faceCells,
                0,0,0,length/axialCells,increment,increment,
                e,0,(_x,_y,_z)=>1,null,loadN:0,interfaceHalfWidth:0,
                emin:1e-5,epsF:.1,trianglePressures:patch);
            var applied=immersed.AppliedResultantN;
            Check(Math.Abs(applied.X-force)<1e-9 &&
                  Math.Abs(applied.Y)<1e-9 &&
                  Math.Abs(applied.Z)<1e-9,
                  "Immersed triangular surface quadrature must conserve total pressure force");
            var result=immersed.Solve();
            Check(Math.Abs(result.ReactionXN+force)<2e-5 &&
                  Math.Abs(result.MaxDisplacementMm-expected)<2e-6 &&
                  Math.Abs(result.MaxVonMisesMpa-force/area)<.002,
                  "Triangulated surface pressure must agree with axial bar analytic reference");
            Console.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "TRIANGULATED SURFACE PRESSURE PASS: {0}x{1}x{1} mesh, triangles={2}, Fx={3:G9}N, Rx={4:G9}N, u={5:G9}mm",
                axialCells,faceCells,patch.Count,applied.X,result.ReactionXN,result.MaxDisplacementMm));
        }

        var thermal=fem.ThermalConductance(45,
            (x,y,z)=>x>=length-1e-8,
            (x,y,z)=>x<=1e-8);
        var expectedFlux=(45/1000.0)*area/length;
        Check(Math.Abs(thermal.ConductanceWPerK-expectedFlux)/
            expectedFlux<1e-7,"Fourier bar k*A/L");
        Check(thermal.RelativeResidual<1e-7,"Thermal free-node equilibrium");

        var zeroAlpha=fem.SolveThermoElastic(thermal,0,
            coldK:293.15,hotK:373.15,referenceK:293.15);
        Check(Math.Abs(zeroAlpha.MaxVonMisesMpa-stat.MaxVonMisesMpa)<2e-4,
            "Thermoelastic alpha=0 must recover static FEM");
        Check(Math.Abs(zeroAlpha.MaxDisplacementMm-stat.MaxDisplacementMm)<2e-6,
            "Thermoelastic zero expansion displacement");
        Check(zeroAlpha.RelativeResidual<1e-6,"Thermoelastic free-node equilibrium");

        var nonzero=fem.SolveThermoElastic(thermal,12e-6,
            coldK:293.15,hotK:373.15,referenceK:293.15);
        Check(nonzero.MaxDisplacementMm>0&&double.IsFinite(nonzero.MaxVonMisesMpa),
            "Nonzero thermal expansion solution");
        Check(nonzero.RelativeResidual<1e-6,"Nonzero thermal eigenstrain equilibrium");

        // Use transverse bending force to excite the FIRST bending mode.
        // An axial force would almost orthogonally miss that mode, yielding
        // a numerically nonzero but physically uninformative response.
        var transverse=new HexFem(4,1,1,0,0,0,25,10,10,
            e,0,(_x,_y,_z)=>1,null,
            loadN:0,interfaceHalfWidth:0,emin:1e-5,epsF:.1,
            nodalForce:(x,y,z)=>x>=length-1e-8
                ?(0.0,force/4,0.0):(0.0,0.0,0.0));
        var mode=transverse.FirstNaturalFrequency(7.85e-6,12);
        Check(mode.FrequencyHz>1&&double.IsFinite(mode.FrequencyHz),
            "Generalized mass and stiffness eigenfrequency");
        Check(mode.RelativeResidual<.3,"First-mode approximate eigen-residual");

        var harmonic=transverse.HarmonicFirstModeMaxDisplacement(7.85e-6,
            forceFrequencyHz:mode.FrequencyHz,dampingRatio:.03);
        Check(harmonic>1e-5&&double.IsFinite(harmonic),
            "Bending-mode harmonic response must be measurable");
        var offResonance=transverse.HarmonicFirstModeMaxDisplacement(7.85e-6,
            forceFrequencyHz:0,dampingRatio:.03);
        Check(harmonic>offResonance*10,"Resonance magnifies transverse response");

        // Engineering eigenfrequency must converge with mesh resolution;
        // a tiny eigen-residual alone does NOT prove a correct physical mode.
        // Classical slender cantilever beam Euler-Bernoulli reference:
        // f1 = beta1^2/(2*pi*L^2)*sqrt(E*I/(rho*A)).
        var finer=new HexFem(16,2,2,0,0,0,length/16,5,5,
            e,0,(_x,_y,_z)=>1,null,
            loadN:force,interfaceHalfWidth:20,
            emin:1e-5,epsF:.1);
        var refinedFrequency=finer.FirstNaturalFrequency(7.85e-6,18);
        var inertia=10*Math.Pow(10,3)/12;
        var rhoInertial=7.85e-6/1000;
        var beamExpected=(1.87510406871*1.87510406871)/
            (2*Math.PI*length*length)*
            Math.Sqrt(e*inertia/(rhoInertial*area));
        var beamError=Math.Abs(refinedFrequency.FrequencyHz/beamExpected-1);
        // Thermal eigenstrain must use the SAME SIMP material interpolation
        // as elastic stiffness. For spatially uniform rho and no external
        // traction, changing power p rescales K and f_th equally, so u_th
        // and stress must be invariant. This catches rho vs rho^p bugs.
        HexFem UniformGray(double power)=>new HexFem(4,1,1,0,0,0,25,10,10,
            e,0,(_x,_y,_z)=>0,null,
            loadN:0,interfaceHalfWidth:20,
            emin:1e-5,epsF:1,simpPower:power);
        var linearGray=UniformGray(1);
        var cubicGray=UniformGray(3);
        var grayT=linearGray.ThermalConductance(45,
            (x,y,z)=>x>=length-1e-8,
            (x,y,z)=>x<=1e-8);
        var grayT3=cubicGray.ThermalConductance(45,
            (x,y,z)=>x>=length-1e-8,
            (x,y,z)=>x<=1e-8);
        var gray1=linearGray.SolveThermoElastic(grayT,12e-6,
            coldK:293.15,hotK:313.15,referenceK:293.15);
        var gray3=cubicGray.SolveThermoElastic(grayT3,12e-6,
            coldK:293.15,hotK:313.15,referenceK:293.15);
        Check(Math.Abs(gray1.MaxDisplacementMm-gray3.MaxDisplacementMm)<1e-7,
            "SIMP eigenstrain/K consistency: thermal displacement must not depend on p for homogeneous density");
        Check(Math.Abs(gray1.MaxVonMisesMpa-gray3.MaxVonMisesMpa)<1e-4,
            "SIMP eigenstrain/K consistency: thermal stress must not depend on p for homogeneous density");
        Console.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "SIMP THERMAL CONSISTENCY PASS: rho=0.5, p1/p3 u={0:G12}/{1:G12}mm VM={2:G12}/{3:G12}MPa",
            gray1.MaxDisplacementMm,gray3.MaxDisplacementMm,
            gray1.MaxVonMisesMpa,gray3.MaxVonMisesMpa));

        Console.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "MODAL REFINEMENT: coarse={0:F2} Hz, finer={1:F2} Hz, Euler={2:F2} Hz, error={3:P2}",
            mode.FrequencyHz,refinedFrequency.FrequencyHz,beamExpected,beamError));
        Check(refinedFrequency.RelativeResidual<.08,"Refined modal eigenvalue residual");
        Check(beamError<.30,"Refined 3D bending frequency vs slender Euler beam");

        var pulse=fem.SolvePulse(7.85e-6,pulseSeconds:.002,multiplier:2,
            dampingRatio:.03,stepsPerPulse:12);
        Check(pulse.MaxDisplacementMm>0&&pulse.MaxAccelerationMmS2>0&&
              pulse.MaxVonMisesMpa>0&&
              double.IsFinite(pulse.MaxVonMisesMpa),"Newmark nonzero elastic response");
        Console.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "MULTIPHYSICS SELF-TEST PASS: bar u={0:F7} mm, stress={1:F4} MPa, "+
            "Fourier G={2:F9} W/K expected={3:F9}, f1={4:F3}Hz residual={5:E3}, "+
            "harmonic near/on-resonance={6:F7}/{7:F7}mm, pulse={8:F7}mm, thermoelastic={9:F7}mm",
            stat.MaxDisplacementMm,stat.MaxVonMisesMpa,
            thermal.ConductanceWPerK,expectedFlux,
            mode.FrequencyHz,mode.RelativeResidual,offResonance,harmonic,
            pulse.MaxDisplacementMm,nonzero.MaxDisplacementMm));
        return 0;
    }
}
