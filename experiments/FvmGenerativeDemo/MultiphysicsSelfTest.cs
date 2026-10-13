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
