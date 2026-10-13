namespace FvmGenerativeDemo;

// Additional *implemented*, linearized physics on the same 3D Hex8 stiffness.
// Units: N, mm, s, MPa, K, W. M has units N*s^2/mm,
// so density in kg/mm^3 is converted by dividing by 1000.
internal sealed record ModalResult(double FrequencyHz,double RelativeResidual,double[] Mode);
internal sealed record PulseResult(double MaxDisplacementMm,double MaxAccelerationMmS2,
    double MaxVonMisesMpa,double TimeStepSeconds,int Steps);
internal sealed record ThermalResult(double ConductanceWPerK,double RelativeResidual,
    double[] Temperatures);
internal sealed record ThermoElasticResult(double MaxVonMisesMpa,double MaxDisplacementMm,double RelativeResidual);

internal sealed partial class HexFem
{
    private static double Norm2(double[] a)=>Math.Sqrt(a.Sum(v=>v*v));
    private double[] MassDiagonal(double densityKgMm3)
    {
        if(densityKgMm3<=0)throw new ArgumentException("Density must be positive.");
        var m=new double[Dofs];
        var volume=_hx*_hy*_hz;
        foreach(var e in _elements)
        {
            // Constant volume weights are lumped to all eight corner nodes.
            var fraction=Math.Max(1e-4,e.FractionAtGauss.Average());
            var massPerNode=volume*fraction*densityKgMm3/(1000*8);
            for(int a=0;a<24;a++)m[e.Id[a]]+=massPerNode;
        }
        return m;
    }
    private double[] SolveLinear(double[] input,double[] mass,double aM,double[]? damping=null,
        double aC=0,int maxIterations=1800,double tolerance=2e-8)
    {
        if(input.Length!=Dofs)throw new ArgumentException("RHS length.");
        var rhs=(double[])input.Clone();
        for(int i=0;i<Dofs;i++)if(_fixed[i])rhs[i]=0;
        var u=new double[Dofs];
        var residual=(double[])rhs.Clone();
        var z=new double[Dofs];var p=new double[Dofs];var ap=new double[Dofs];
        var rhsNorm=Norm2(rhs);
        if(rhsNorm<1e-30)return u;
        for(int i=0;i<Dofs;i++)
        {
            var diag=_fixed[i]?1:_diag[i]+aM*mass[i]+aC*(damping?[i]??0);
            z[i]=p[i]=residual[i]/diag;
        }
        var rz=Dot(residual,z);
        for(var iteration=0;iteration<maxIterations;iteration++)
        {
            Multiply(p,ap,true);
            for(int i=0;i<Dofs;i++)if(!_fixed[i])
                ap[i]+=(aM*mass[i]+aC*(damping?[i]??0))*p[i];
            var denominator=Dot(p,ap);
            if(denominator<=0||!double.IsFinite(denominator))
                throw new ArithmeticException("Multi-physics PCG lost positive definiteness.");
            var step=rz/denominator;
            for(int i=0;i<Dofs;i++){u[i]+=step*p[i];residual[i]-=step*ap[i];}
            if(Norm2(residual)/rhsNorm<tolerance)return u;
            for(int i=0;i<Dofs;i++)
            {
                var diag=_fixed[i]?1:_diag[i]+aM*mass[i]+aC*(damping?[i]??0);
                z[i]=residual[i]/diag;
            }
            var next=Dot(residual,z);var beta=next/rz;
            for(int i=0;i<Dofs;i++)p[i]=z[i]+beta*p[i];
            rz=next;
        }
        throw new ArithmeticException("Multi-physics PCG did not converge.");
    }

    internal ModalResult FirstNaturalFrequency(double densityKgMm3,int inverseIterations=10)
    {
        var mass=MassDiagonal(densityKgMm3);
        var v=new double[Dofs];
        for(int i=0;i<Dofs;i++)
            v[i]=_fixed[i]?0:Math.Sin((i+1)*.713)+.4*Math.Cos((i+1)*.213);
        void Normalize(double[] vec)
        {
            double sq=0;for(int i=0;i<Dofs;i++)sq+=mass[i]*vec[i]*vec[i];
            if(sq<1e-30)throw new ArithmeticException("Zero modal generalized mass.");
            var scale=1/Math.Sqrt(sq);
            for(int i=0;i<Dofs;i++)vec[i]*=scale;
        }
        Normalize(v);
        for(int iter=0;iter<inverseIterations;iter++)
        {
            var rhs=new double[Dofs];
            for(int i=0;i<Dofs;i++)rhs[i]=mass[i]*v[i];
            v=SolveLinear(rhs,mass,0,maxIterations:2500);
            Normalize(v);
        }
        var kv=new double[Dofs];Multiply(v,kv,true);
        var lambda=Dot(v,kv);
        if(lambda<=0||!double.IsFinite(lambda))throw new ArithmeticException("Bad first eigenvalue.");
        double error=0,total=0;
        for(int i=0;i<Dofs;i++)
        {
            if(_fixed[i])continue;
            var d=kv[i]-lambda*mass[i]*v[i];
            error+=d*d;total+=kv[i]*kv[i];
        }
        return new ModalResult(Math.Sqrt(lambda)/(2*Math.PI),
            Math.Sqrt(error)/Math.Max(1e-20,Math.Sqrt(total)),v);
    }

    // Linear elastic half-sine force history, not physical contact or plastic impact.
    // Unconditionally-stable average-acceleration Newmark with mass-proportional damping.
    internal PulseResult SolvePulse(double densityKgMm3,double pulseSeconds,double multiplier,
        double dampingRatio=0.02,int stepsPerPulse=12)
    {
        if(pulseSeconds<=0||multiplier<=0||dampingRatio<0||dampingRatio>1||stepsPerPulse<4)
            throw new ArgumentException("Invalid impact pulse.");
        var mode=FirstNaturalFrequency(densityKgMm3,7);
        var omega=2*Math.PI*mode.FrequencyHz;
        var m=MassDiagonal(densityKgMm3);
        var damping=m.Select(x=>2*dampingRatio*omega*x).ToArray();
        var dt=pulseSeconds/stepsPerPulse;
        var total=stepsPerPulse*3;
        var u=new double[Dofs];var v=new double[Dofs];var acc=new double[Dofs];
        var maxU=0.0;var maxA=0.0;var maxStress=0.0;
        const double beta=.25,gamma=.5;
        var a0=1/(beta*dt*dt);
        var a1=gamma/(beta*dt);
        var a2=1/(beta*dt);
        var a3=1/(2*beta)-1;
        var a4=gamma/beta-1;
        var a5=dt*(gamma/(2*beta)-1);
        for(int step=1;step<=total;step++)
        {
            var time=step*dt;
            var factor=time<=pulseSeconds?Math.Sin(Math.PI*time/pulseSeconds)*multiplier:0;
            var force=new double[Dofs];
            for(int i=0;i<Dofs;i++)if(!_fixed[i])
                force[i]=factor*_rhs[i]
                    +m[i]*(a0*u[i]+a2*v[i]+a3*acc[i])
                    +damping[i]*(a1*u[i]+a4*v[i]+a5*acc[i]);
            var next=SolveLinear(force,m,a0,damping,a1,maxIterations:2200);
            var nextA=new double[Dofs];var nextV=new double[Dofs];
            for(int i=0;i<Dofs;i++)
            {
                nextA[i]=a0*(next[i]-u[i])-a2*v[i]-a3*acc[i];
                nextV[i]=v[i]+dt*((1-gamma)*acc[i]+gamma*nextA[i]);
                if(!_fixed[i])maxA=Math.Max(maxA,Math.Abs(nextA[i]));
            }
            (u,v,acc)=(next,nextV,nextA);
            for(int i=0;i<Dofs;i+=3)
            {
                var disp=Math.Sqrt(u[i]*u[i]+u[i+1]*u[i+1]+u[i+2]*u[i+2]);
                maxU=Math.Max(maxU,disp);
            }
            maxStress=Math.Max(maxStress,PeakStress(u));
        }
        return new PulseResult(maxU,maxA,maxStress,dt,total);
    }

    private double PeakStress(double[] u)
    {
        var b=new double[6,24];double max=0;
        foreach(var e in _elements)
        {
            var q=0;
            foreach(var xi in Gauss)foreach(var eta in Gauss)foreach(var zeta in Gauss)
            {
                if(e.FractionAtGauss[q++]<.5)continue;
                Shape(xi,eta,zeta,b);
                var strain=new double[6];var stress=new double[6];
                for(int i=0;i<6;i++)for(int j=0;j<24;j++)
                    strain[i]+=b[i,j]*u[e.Id[j]];
                for(int i=0;i<6;i++)for(int j=0;j<6;j++)
                    stress[i]+=_elastic[i,j]*strain[j];
                var sx=stress[0];var sy=stress[1];var sz=stress[2];
                var vm=Math.Sqrt(.5*((sx-sy)*(sx-sy)+(sy-sz)*(sy-sz)+(sz-sx)*(sz-sx))
                    +3*(stress[3]*stress[3]+stress[4]*stress[4]+stress[5]*stress[5]));
                max=Math.Max(max,vm);
            }
        }
        return max;
    }

    // Steady-state Fourier conduction, fixed hot=1K and cold=0K on protected
    // material nodes. Conductance in W/K for this unit temperature difference.
    internal ThermalResult ThermalConductance(double conductivityWmK,
        Func<double,double,double,bool> hot,
        Func<double,double,double,bool> cold)
    {
        var n=(_nx+1)*(_ny+1)*(_nz+1);
        var fixedT=new bool[n];var prescribed=new double[n];
        var hotNodes=0;var coldNodes=0;
        for(int i=0;i<=_nx;i++)for(int j=0;j<=_ny;j++)for(int k=0;k<=_nz;k++)
        {
            var x=_x0+i*_hx;var y=_y0+j*_hy;var z=_z0+k*_hz;
            var node=Node(i,j,k);
            if(hot(x,y,z)){prescribed[node]=1;fixedT[node]=true;hotNodes++;}
            else if(cold(x,y,z)){prescribed[node]=0;fixedT[node]=true;coldNodes++;}
        }
        if(hotNodes==0||coldNodes==0)throw new ArgumentException("Thermal Dirichlet patches contain no nodes.");
        var local=new List<(int[] Nodes,double[] Matrix)>(_elements.Count);
        var diag=new double[n];var bb=new double[6,24];
        var det=_hx*_hy*_hz/8;
        var factor=conductivityWmK/1000; // W/(m*K) -> W/(mm*K)
        var elementIndex=0;
        for(int i=0;i<_nx;i++)for(int j=0;j<_ny;j++)for(int k=0;k<_nz;k++)
        {
            var e=_elements[elementIndex++];
            var nodes=new int[8];
            for(int a=0;a<8;a++)nodes[a]=e.Id[a*3]/3;
            var ke=new double[64];var q=0;
            foreach(var xi in Gauss)foreach(var eta in Gauss)foreach(var zeta in Gauss)
            {
                Shape(xi,eta,zeta,bb);
                var material=Math.Max(1e-4,e.FractionAtGauss[q++]);
                var weight=det*factor*material;
                for(int a=0;a<8;a++)for(int c=0;c<8;c++)
                {
                    double product=bb[0,3*a]*bb[0,3*c]
                        +bb[1,3*a+1]*bb[1,3*c+1]
                        +bb[2,3*a+2]*bb[2,3*c+2];
                    ke[8*a+c]+=weight*product;
                }
            }
            for(int a=0;a<8;a++)diag[nodes[a]]+=ke[8*a+a];
            local.Add((nodes,ke));
        }
        void Apply(double[] x,double[] y,bool constraints)
        {
            Array.Clear(y,0,y.Length);
            foreach(var (ids,ke) in local)
                for(int a=0;a<8;a++)for(int b=0;b<8;b++)
                    y[ids[a]]+=ke[8*a+b]*x[ids[b]];
            if(constraints)for(int i=0;i<n;i++)if(fixedT[i])y[i]=x[i];
        }
        var rhs=new double[n];var tmp=new double[n];
        Apply(prescribed,tmp,false);
        for(int i=0;i<n;i++)if(!fixedT[i])rhs[i]=-tmp[i];
        var temp=new double[n];var rr=(double[])rhs.Clone();
        var zz=new double[n];var p=new double[n];var ap=new double[n];
        var rhsnorm=Norm2(rhs);
        if(rhsnorm>0)
        {
            for(int i=0;i<n;i++)if(!fixedT[i])
                p[i]=zz[i]=rr[i]/diag[i];
            double rz=Dot(rr,zz);
            bool converged=false;
            for(int iteration=0;iteration<3000;iteration++)
            {
                Apply(p,ap,true);
                var denom=Dot(p,ap);
                if(denom<=0)throw new ArithmeticException("Thermal matrix invalid.");
                var alpha=rz/denom;
                for(int i=0;i<n;i++){temp[i]+=alpha*p[i];rr[i]-=alpha*ap[i];}
                if(Norm2(rr)/rhsnorm<1e-8){converged=true;break;}
                for(int i=0;i<n;i++)zz[i]=fixedT[i]?0:rr[i]/diag[i];
                var next=Dot(rr,zz);var beta=next/rz;
                for(int i=0;i<n;i++)p[i]=zz[i]+beta*p[i];
                rz=next;
            }
            if(!converged)throw new ArithmeticException("Thermal solver did not converge.");
        }
        for(int i=0;i<n;i++)if(fixedT[i])temp[i]=prescribed[i];
        Apply(temp,tmp,false);
        double flux=0,residual=0,scale=0;
        for(int i=0;i<n;i++)
        {
            if(fixedT[i]&&prescribed[i]>.5)flux+=tmp[i];
            if(!fixedT[i]){residual+=tmp[i]*tmp[i];scale+=diag[i]*diag[i]*temp[i]*temp[i];}
        }
        if(flux<=0||!double.IsFinite(flux))
            throw new ArithmeticException("Nonpositive thermal conductance.");
        return new ThermalResult(flux,Math.Sqrt(residual)/Math.Max(1e-12,Math.Sqrt(scale)),temp);
    }

    // First-mode forced harmonic approximation, not a full multimodal frequency response.
    internal double HarmonicFirstModeMaxDisplacement(double densityKgMm3,double forceFrequencyHz,
        double dampingRatio)
    {
        if(forceFrequencyHz<0||dampingRatio<=0||dampingRatio>1)
            throw new ArgumentException("Harmonic frequency/damping invalid.");
        var first=FirstNaturalFrequency(densityKgMm3);
        var wn=2*Math.PI*first.FrequencyHz;
        var w=2*Math.PI*forceFrequencyHz;
        var modalLoad=Dot(first.Mode,_rhs);
        var q=Math.Abs(modalLoad)/Math.Sqrt(
            Math.Pow(wn*wn-w*w,2)+Math.Pow(2*dampingRatio*wn*w,2));
        var max=0.0;
        for(int i=0;i<Dofs;i+=3)
            max=Math.Max(max,q*Math.Sqrt(
                first.Mode[i]*first.Mode[i]+first.Mode[i+1]*first.Mode[i+1]
                +first.Mode[i+2]*first.Mode[i+2]));
        return max;
    }

    // Sequential thermo-elastic solve: imported steady temperature field
    // T=coldK+(hotK-coldK)*normalizedT. Mechanical stiffness currently uses
    // constant E,nu and small-strain alpha*(T-reference) volumetric eigenstrain.
    internal ThermoElasticResult SolveThermoElastic(ThermalResult thermal,
        double expansionPerK,double coldK,double hotK,double referenceK)
    {
        var t=thermal.Temperatures;
        if(t.Length!=(_nx+1)*(_ny+1)*(_nz+1)||expansionPerK<0)
            throw new ArgumentException("Thermal temperature/mat parameters invalid.");
        var force=(double[])_rhs.Clone();
        var b=new double[6,24];var det=_hx*_hy*_hz/8;
        var count=0;
        foreach(var e in _elements)
        {
            var q=0;
            foreach(var xi in Gauss)foreach(var eta in Gauss)foreach(var zeta in Gauss)
            {
                Shape(xi,eta,zeta,b);
                var normalized=0.0;
                for(int a=0;a<8;a++)
                {
                    var (i,j,k)=Corners[a];
                    var shape=(1+(2*i-1)*xi)*(1+(2*j-1)*eta)*(1+(2*k-1)*zeta)/8;
                    normalized+=shape*t[e.Id[3*a]/3];
                }
                var actualT=coldK+(hotK-coldK)*normalized;
                var th=expansionPerK*(actualT-referenceK);
                var h=_emin+(1-_emin)*e.FractionAtGauss[q++];
                // D*[th,th,th,0,0,0] = (3λ+2μ)*th.
                var thermalStress=(_elastic[0,0]+_elastic[0,1]+_elastic[0,2])*th;
                for(int a=0;a<24;a++)
                    force[e.Id[a]]+=det*h*thermalStress*(b[0,a]+b[1,a]+b[2,a]);
            }
            count++;
        }
        var u=SolveLinear(force,new double[Dofs],0,maxIterations:3000);
        var ku=new double[Dofs];Multiply(u,ku,false);
        var residual=0.0;var forceNorm=0.0;
        for(int i=0;i<Dofs;i++)if(!_fixed[i])
        {
            residual+=Math.Pow(ku[i]-force[i],2);
            forceNorm+=force[i]*force[i];
        }
        var maxU=0.0;
        for(int i=0;i<Dofs;i+=3)
            maxU=Math.Max(maxU,Math.Sqrt(u[i]*u[i]+u[i+1]*u[i+1]+u[i+2]*u[i+2]));
        var maxVM=0.0;
        foreach(var e in _elements)
        {
            var q=0;
            foreach(var xi in Gauss)foreach(var eta in Gauss)foreach(var zeta in Gauss)
            {
                if(e.FractionAtGauss[q]<.5){q++;continue;}
                Shape(xi,eta,zeta,b);
                double temp=0;
                for(int a=0;a<8;a++)
                {
                    var (i,j,k)=Corners[a];
                    var shape=(1+(2*i-1)*xi)*(1+(2*j-1)*eta)*(1+(2*k-1)*zeta)/8;
                    temp+=shape*t[e.Id[3*a]/3];
                }
                q++;
                var th=expansionPerK*(coldK+(hotK-coldK)*temp-referenceK);
                var strain=new double[6];var stress=new double[6];
                for(int a=0;a<6;a++)for(int c=0;c<24;c++)
                    strain[a]+=b[a,c]*u[e.Id[c]];
                for(int a=0;a<3;a++)strain[a]-=th;
                for(int a=0;a<6;a++)for(int c=0;c<6;c++)
                    stress[a]+=_elastic[a,c]*strain[c];
                var sx=stress[0];var sy=stress[1];var sz=stress[2];
                var vm=Math.Sqrt(.5*((sx-sy)*(sx-sy)+(sy-sz)*(sy-sz)+(sz-sx)*(sz-sx))
                    +3*(stress[3]*stress[3]+stress[4]*stress[4]+stress[5]*stress[5]));
                maxVM=Math.Max(maxVM,vm);
            }
        }
        return new ThermoElasticResult(maxVM,maxU,
            Math.Sqrt(residual)/Math.Max(1e-12,Math.Sqrt(forceNorm)));
    }

}
