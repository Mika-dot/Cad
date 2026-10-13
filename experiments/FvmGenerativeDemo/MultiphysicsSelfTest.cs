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
