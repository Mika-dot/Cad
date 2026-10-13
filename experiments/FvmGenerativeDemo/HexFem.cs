using DCad.FunctionalVoxel;

namespace FvmGenerativeDemo;

// All dimensions in mm, forces in N, Young's modulus and stress in N/mm²=MPa.
// Standard trilinear 8-node displacement element, 2x2x2 Gauss integration.
internal sealed partial class HexFem
{
    private static readonly (int X, int Y, int Z)[] Corners =
    {
        (0,0,0),(1,0,0),(1,1,0),(0,1,0),
        (0,0,1),(1,0,1),(1,1,1),(0,1,1)
    };
    private static readonly double[] Gauss = { -0.5773502691896258, 0.5773502691896258 };
    private readonly List<Element> _elements = new();
    private readonly double[] _rhs, _diag;
    private readonly bool[] _fixed;
    private readonly double[,] _elastic = new double[6,6];
    private readonly Func<double,double,double,double> _field;
    private readonly FunctionalVoxelModel? _model;
    private readonly double _epsF;
    private readonly double _emin;
    private readonly double _densityKgMm3;
    private readonly double _simpPower;
    private readonly int _nx, _ny, _nz;
    private readonly double _x0, _y0, _z0, _hx, _hy, _hz;
    internal double Volume { get; private set; }
    internal double MassKg => Volume * _densityKgMm3;
    internal int Dofs => _rhs.Length;
    internal int Elements => _elements.Count;

    private sealed class Element
    {
        internal readonly int[] Id = new int[24];
        internal readonly double[] K = new double[24*24];
        internal readonly double[] StressAtGauss = new double[8];
        internal readonly double[] FractionAtGauss = new double[8];
    }

    internal HexFem(int nx, int ny, int nz, double x0, double y0, double z0,
        double hx, double hy, double hz, double young, double poisson,
        Func<double,double,double,double> field, FunctionalVoxelModel? model,
        double loadN, double interfaceHalfWidth, double emin = 1e-5, double epsF = 30,
        Func<double,double,double,bool>? support = null,
        Func<double,double,double,(double X,double Y,double Z)>? nodalForce = null,
        double densityKgMm3=7.85e-6,double simpPower=1)
    {
        if (nx < 1 || ny < 1 || nz < 1 || hx <= 0 || hy <= 0 || hz <= 0 ||
            young <= 0 || poisson <= -1 || poisson >= 0.5 || emin <= 0 || emin >= 1 ||
             densityKgMm3<=0 || !double.IsFinite(densityKgMm3) ||
             simpPower<1 || simpPower>6 || !double.IsFinite(simpPower))
            throw new ArgumentException("Invalid finite-element mesh or elasticity.");
        _nx=nx; _ny=ny; _nz=nz;
        _x0=x0; _y0=y0; _z0=z0; _hx=hx; _hy=hy; _hz=hz;
        _field=field; _model=model; _emin=emin; _epsF=epsF;
        _densityKgMm3=densityKgMm3;_simpPower=simpPower;
        var nodeCount=(nx+1)*(ny+1)*(nz+1);
        _rhs=new double[nodeCount*3]; _diag=new double[nodeCount*3];
        _fixed=new bool[nodeCount*3];
        var lambda=young*poisson/((1+poisson)*(1-2*poisson));
        var mu=young/(2*(1+poisson));
        for (int a=0;a<3;a++)
            for (int b=0;b<3;b++) _elastic[a,b]=a==b?lambda+2*mu:lambda;
        for (int a=3;a<6;a++) _elastic[a,a]=mu;

        if(support is null)
        {
            for(int j=0;j<=ny;j++)for(int k=0;k<=nz;k++)
                for(int a=0;a<3;a++) _fixed[3*Node(0,j,k)+a]=true;
        }
        else
        {
            var supported=0;
            for(int i=0;i<=nx;i++)for(int j=0;j<=ny;j++)for(int k=0;k<=nz;k++)
                if(support(_x0+i*_hx,_y0+j*_hy,_z0+k*_hz))
                {
                    supported++;
                    for(int a=0;a<3;a++)_fixed[3*Node(i,j,k)+a]=true;
                }
            if(supported<1)throw new ArgumentException("No nodes inside the prescribed support.");
        }

        if(nodalForce is null)
        {
            // Consistent nodal traction in X on the preserved right-hand attachment.
            double totalWeight=0;
            for(int j=0;j<=ny;j++)for(int k=0;k<=nz;k++)
            {
                var y=_y0+j*_hy; var z=_z0+k*_hz;
                if(Math.Abs(y)>interfaceHalfWidth+1e-9 || Math.Abs(z)>interfaceHalfWidth+1e-9) continue;
                totalWeight+=(j==0||j==ny?0.5:1)*(k==0||k==nz?0.5:1);
            }
            if(totalWeight<=0) throw new ArgumentException("No loaded face nodes found.");
            for(int j=0;j<=ny;j++)for(int k=0;k<=nz;k++)
            {
                var y=_y0+j*_hy; var z=_z0+k*_hz;
                if(Math.Abs(y)>interfaceHalfWidth+1e-9 || Math.Abs(z)>interfaceHalfWidth+1e-9) continue;
                var weight=(j==0||j==ny?0.5:1)*(k==0||k==nz?0.5:1);
                _rhs[3*Node(nx,j,k)]+=loadN*weight/totalWeight;
            }
    
        }
        else
        {
            double netForce=0;
            for(int i=0;i<=nx;i++)for(int j=0;j<=ny;j++)for(int k=0;k<=nz;k++)
            {
                var f=nodalForce(_x0+i*_hx,_y0+j*_hy,_z0+k*_hz);
                var node=3*Node(i,j,k);
                _rhs[node]+=f.X;_rhs[node+1]+=f.Y;_rhs[node+2]+=f.Z;
                netForce+=Math.Abs(f.X)+Math.Abs(f.Y)+Math.Abs(f.Z);
            }
            if(netForce<1e-8)throw new ArgumentException("No force was applied to the loaded region.");
        }
        for(int i=0;i<nx;i++)for(int j=0;j<ny;j++)for(int k=0;k<nz;k++)
        {
            var e=new Element();
            for(int a=0;a<8;a++)
            {
                var (ix,iy,iz)=Corners[a];
                var node=Node(i+ix,j+iy,k+iz);
                for(int d=0;d<3;d++) e.Id[3*a+d]=3*node+d;
            }
            AssembleElement(e,i,j,k);
            _elements.Add(e);
            for(int a=0;a<24;a++) _diag[e.Id[a]]+=e.K[24*a+a];
        }
        for(int a=0;a<Dofs;a++)
        {
            if(_fixed[a]) { _rhs[a]=0; _diag[a]=1; }
            if(_diag[a]<=0 || !double.IsFinite(_diag[a])) throw new ArithmeticException("Nonpositive stiffness diagonal.");
        }
    }

    private int Node(int i,int j,int k) => (i*(_ny+1)+j)*(_nz+1)+k;
    private double Evaluate(double x,double y,double z) =>
        _model is null ? _field(x,y,z) : _model.Approximate(new[]{x,y,z});

    private double Fraction(double f)
    {
        // Smooth quadrature material indicator; NOT a stored density or a sixth M-image.
        // A regularization band in field-value units, not a true exact cut-cell formula.
        return Math.Clamp(0.5+f/(2*_epsF),0,1);
    }

    private void Shape(double xi,double eta,double zeta,double[,] b)
    {
        Array.Clear(b,0,b.Length);
        for(int a=0;a<8;a++)
        {
            var (i,j,k)=Corners[a];
            var sx=2*i-1; var sy=2*j-1; var sz=2*k-1;
            var dx=sx*(1+sy*eta)*(1+sz*zeta)/(4*_hx);
            var dy=sy*(1+sx*xi)*(1+sz*zeta)/(4*_hy);
            var dz=sz*(1+sx*xi)*(1+sy*eta)/(4*_hz);
            var c=3*a;
            b[0,c]=dx; b[1,c+1]=dy; b[2,c+2]=dz;
            b[3,c+1]=dz; b[3,c+2]=dy; // engineering gamma_yz
            b[4,c]=dz; b[4,c+2]=dx;   // engineering gamma_xz
            b[5,c]=dy; b[5,c+1]=dx;   // engineering gamma_xy
        }
    }

    private void AssembleElement(Element e,int i,int j,int k)
    {
        var b=new double[6,24]; var db=new double[6,24];
        var det=_hx*_hy*_hz/8;
        var q=0;
        foreach(var xi in Gauss)foreach(var eta in Gauss)foreach(var zeta in Gauss)
        {
            var x=_x0+(i+0.5*(1+xi))*_hx;
            var y=_y0+(j+0.5*(1+eta))*_hy;
            var z=_z0+(k+0.5*(1+zeta))*_hz;
            var h=Fraction(Evaluate(x,y,z));
            e.FractionAtGauss[q++]=h;
            Volume+=det*h;
            Shape(xi,eta,zeta,b);
            for(int a=0;a<6;a++)for(int c=0;c<24;c++)
            {
                double sum=0;
                for(int d=0;d<6;d++)sum+=_elastic[a,d]*b[d,c];
                db[a,c]=sum;
            }
            var factor=det*(_emin+(1-_emin)*Math.Pow(h,_simpPower));
            for(int a=0;a<24;a++)for(int c=0;c<24;c++)
            {
                double v=0;
                for(int d=0;d<6;d++)v+=b[d,a]*db[d,c];
                e.K[24*a+c]+=factor*v;
            }
        }
    }

    private void Multiply(double[] x,double[] y,bool constrained)
    {
        Array.Clear(y,0,y.Length);
        foreach(var e in _elements)
        {
            for(int a=0;a<24;a++)
            {
                double sum=0;
                for(int b=0;b<24;b++)sum+=e.K[a*24+b]*x[e.Id[b]];
                y[e.Id[a]]+=sum;
            }
        }
        if(constrained) for(int a=0;a<Dofs;a++)if(_fixed[a]) y[a]=x[a];
    }

    internal FemResults Solve(int maxIterations=3000, double tolerance=2e-9)
    {
        var u=new double[Dofs]; var residual=(double[])_rhs.Clone();
        var p=new double[Dofs]; var z=new double[Dofs]; var ap=new double[Dofs];
        double rhsNorm=Math.Sqrt(Dot(_rhs,_rhs));
        if(rhsNorm==0) throw new ArithmeticException("Empty load.");
        for(int a=0;a<Dofs;a++) p[a]=z[a]=residual[a]/_diag[a];
        double rz=Dot(residual,z), ratio=1;
        var iterations=0;
        for(;iterations<maxIterations;iterations++)
        {
            Multiply(p,ap,true);
            var denominator=Dot(p,ap);
            if(denominator<=0 || !double.IsFinite(denominator))
                throw new ArithmeticException("PCG lost positive definiteness.");
            var alpha=rz/denominator;
            for(int a=0;a<Dofs;a++)
            {
                u[a]+=alpha*p[a];
                residual[a]-=alpha*ap[a];
            }
            ratio=Math.Sqrt(Dot(residual,residual))/rhsNorm;
            if(ratio<tolerance){iterations++;break;}
            for(int a=0;a<Dofs;a++) z[a]=residual[a]/_diag[a];
            var rzNew=Dot(residual,z);
            var beta=rzNew/rz;
            for(int a=0;a<Dofs;a++)p[a]=z[a]+beta*p[a];
            rz=rzNew;
        }
        if(ratio>=tolerance) throw new ArithmeticException($"PCG did not converge ({ratio:G5}).");

        var ku=new double[Dofs];
        Multiply(u,ku,false);
        double freeResidual2=0, reactionX=0, reactionY=0, reactionZ=0, compliance=0;
        for(int a=0;a<Dofs;a++)
        {
            var error=ku[a]-_rhs[a];
            if(_fixed[a]) { if(a%3==0) reactionX+=error; else if(a%3==1) reactionY+=error; else reactionZ+=error; }
            else freeResidual2+=error*error;
            compliance+=_rhs[a]*u[a];
        }
        double maxDisp=0;
        for(int i=0;i<=_nx;i++)for(int j=0;j<=_ny;j++)for(int k=0;k<=_nz;k++)
        {
            var x=_x0+i*_hx;var y=_y0+j*_hy;var coordinateZ=_z0+k*_hz;
            if(Evaluate(x,y,coordinateZ)<0)continue;
            var id=Node(i,j,k)*3;
            maxDisp=Math.Max(maxDisp,Math.Sqrt(u[id]*u[id]+u[id+1]*u[id+1]+u[id+2]*u[id+2]));
        }
        var stresses=new double[_elements.Count];
        double maxStress=0;
        var index=0;
        var bMatrix=new double[6,24];
        foreach(var e in _elements)
        {
            var q=0;var peak=0.0;
            foreach(var xi in Gauss)foreach(var eta in Gauss)foreach(var zeta in Gauss)
            {
                Shape(xi,eta,zeta,bMatrix);
                var strain=new double[6]; var stress=new double[6];
                for(int a=0;a<6;a++)for(int c=0;c<24;c++)
                    strain[a]+=bMatrix[a,c]*u[e.Id[c]];
                for(int a=0;a<6;a++)for(int d=0;d<6;d++)
                    stress[a]+=_elastic[a,d]*strain[d];
                var sx=stress[0];var sy=stress[1];var sz=stress[2];
                var von=Math.Sqrt(0.5*((sx-sy)*(sx-sy)+(sy-sz)*(sy-sz)+(sz-sx)*(sz-sx))
                    +3*(stress[3]*stress[3]+stress[4]*stress[4]+stress[5]*stress[5]));
                // Count real material only, not the stress of an ersatz void.
                if(e.FractionAtGauss[q]>=0.5) peak=Math.Max(peak,von);
                e.StressAtGauss[q++]=von;
            }
            stresses[index++]=peak;
            maxStress=Math.Max(maxStress,peak);
        }
        return new FemResults(u,stresses,Volume,MassKg,maxStress,maxDisp,
            compliance,Math.Sqrt(freeResidual2)/rhsNorm,reactionX,iterations,reactionY,reactionZ);
    }

    // Linear elasticity adjoint: dC/dF_q = -u^T(dK/dF_q)u, C=f^T u.
    // This derives sensitivities from each 3D Gauss point, not voxel deletion.
    internal IReadOnlyList<GaussSensitivity> GaussSensitivities(FemResults solved)
    {
        if(solved.Displacements.Length!=Dofs)throw new ArgumentException("Wrong displacement vector.");
        var result=new List<GaussSensitivity>(_elements.Count*8);
        var b=new double[6,24];
        var det=_hx*_hy*_hz/8;
        var u=solved.Displacements;
        var ei=0;
        for(int i=0;i<_nx;i++)for(int j=0;j<_ny;j++)for(int k=0;k<_nz;k++)
        {
            var e=_elements[ei++];
            foreach(var xi in Gauss)foreach(var eta in Gauss)foreach(var zeta in Gauss)
            {
                var x=_x0+(i+.5*(1+xi))*_hx;
                var y=_y0+(j+.5*(1+eta))*_hy;
                var z=_z0+(k+.5*(1+zeta))*_hz;
                var field=Evaluate(x,y,z);
                if(Math.Abs(field)>=_epsF)continue;
                Shape(xi,eta,zeta,b);
                var strain=new double[6];
                for(int a=0;a<6;a++)for(int c=0;c<24;c++)
                    strain[a]+=b[a,c]*u[e.Id[c]];
                double energy=0;
                for(int a=0;a<6;a++)for(int d=0;d<6;d++)
                    energy+=strain[a]*_elastic[a,d]*strain[d];
                var dv=det/(2*_epsF);
                var h=e.FractionAtGauss[Array.IndexOf(Gauss,xi)*4+Array.IndexOf(Gauss,eta)*2+Array.IndexOf(Gauss,zeta)];
                var dK=(1-_emin)*_simpPower*Math.Pow(Math.Max(1e-12,h),_simpPower-1);
                result.Add(new GaussSensitivity(x,y,z,dv,-dv*dK*energy));
            }
        }
        return result;
    }

    internal void ExportFirstElementStiffness(string path)
    {
        if(_elements.Count!=1)throw new InvalidOperationException("This fixture expects exactly one Hex8.");
        using var writer=new StreamWriter(path,false,System.Text.Encoding.UTF8);
        var ke=_elements[0].K;
        for(int i=0;i<24;i++)
        {
            var line=new string[24];
            for(int j=0;j<24;j++)
                line[j]=ke[24*i+j].ToString("G17",System.Globalization.CultureInfo.InvariantCulture);
            writer.WriteLine(string.Join(",",line));
        }
    }

    private static double Dot(double[] a,double[] b)
    {
        double sum=0;for(var i=0;i<a.Length;i++)sum+=a[i]*b[i];return sum;
    }
}

internal sealed record FemResults(double[] Displacements,double[] ElementVonMises,
    double VolumeMm3,double MassKg,double MaxVonMisesMpa,double MaxDisplacementMm,
    double ComplianceNmm,double RelativeEquilibriumResidual,double ReactionXN,int CgIterations,
    double ReactionYN=0,double ReactionZN=0);

internal sealed record GaussSensitivity(double X,double Y,double Z,double DVolumeDField,double DComplianceDField);
