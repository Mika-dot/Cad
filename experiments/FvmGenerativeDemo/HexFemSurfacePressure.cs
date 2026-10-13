namespace FvmGenerativeDemo;

// Pressure on an arbitrary oriented triangle using exact subdivision into
// background Hex8 cells. A 7-point degree-5 triangle quadrature integrates
// the trilinear Hex8 shape functions (degree <=3 in physical coordinates)
// exactly ON EACH cell-clipped planar triangle. It avoids sampling an entire
// cross-cell triangle at three points in unrelated Hex8 elements.
internal sealed partial class HexFem
{
    private static readonly (double U,double V,double W,double Weight)[] TriangleGauss7 =
    {
        (1.0/3,1.0/3,1.0/3,0.225),
        (0.059715871789770,0.470142064105115,0.470142064105115,0.132394152788506),
        (0.470142064105115,0.059715871789770,0.470142064105115,0.132394152788506),
        (0.470142064105115,0.470142064105115,0.059715871789770,0.132394152788506),
        (0.797426985353087,0.101286507323456,0.101286507323456,0.125939180544827),
        (0.101286507323456,0.797426985353087,0.101286507323456,0.125939180544827),
        (0.101286507323456,0.101286507323456,0.797426985353087,0.125939180544827)
    };

    private static double Component(Point3 p,int a) =>
        a==0?p.X:a==1?p.Y:p.Z;

    // Intersect a flat polygon with one axis-aligned halfspace.
    // Distances are signed positive on the kept side.
    private static List<Point3> ClipPolygon(
        IReadOnlyList<Point3> polygon,int axis,double plane,bool keepAbove)
    {
        var output=new List<Point3>(polygon.Count+2);
        if(polygon.Count==0)return output;
        var prev=polygon[^1];
        double Distance(Point3 p)
        {
            var delta=Component(p,axis)-plane;
            return keepAbove?delta:-delta;
        }
        var dPrev=Distance(prev);
        foreach(var current in polygon)
        {
            var dCurrent=Distance(current);
            var prevInside=dPrev>=0;
            var currentInside=dCurrent>=0;
            if(prevInside!=currentInside)
            {
                var ratio=dPrev/(dPrev-dCurrent);
                output.Add(prev+(current-prev)*ratio);
            }
            if(currentInside)output.Add(current);
            prev=current;dPrev=dCurrent;
        }
        return output;
    }

    private void IntegrateClippedTriangle(int i,int j,int k,
        Point3 a,Point3 b,Point3 c,Point3 traction)
    {
        var area=Point3.Cross(b-a,c-a).Norm()*0.5;
        if(area<1e-13)return;
        foreach(var (u,v,w,weight) in TriangleGauss7)
        {
            var p=a*u+b*v+c*w;
            var xi=2*(p.X-_x0-i*_hx)/_hx-1;
            var eta=2*(p.Y-_y0-j*_hy)/_hy-1;
            var zeta=2*(p.Z-_z0-k*_hz)/_hz-1;
            var factor=area*weight;
            for(var q=0;q<8;q++)
            {
                var corner=Corners[q];
                var N=(1+(2*corner.X-1)*xi)*
                      (1+(2*corner.Y-1)*eta)*
                      (1+(2*corner.Z-1)*zeta)/8;
                var node=3*Node(i+corner.X,j+corner.Y,k+corner.Z);
                _rhs[node]+=factor*N*traction.X;
                _rhs[node+1]+=factor*N*traction.Y;
                _rhs[node+2]+=factor*N*traction.Z;
            }
        }
    }

    private void ApplyTrianglePressure(HexTrianglePressure triangle)
    {
        if(!double.IsFinite(triangle.PressureMpa)||triangle.PressureMpa==0)
            throw new ArgumentException("Triangle pressure must be finite and nonzero (MPa).");
        var a=triangle.A;var b=triangle.B;var c=triangle.C;
        var cross=Point3.Cross(b-a,c-a);
        var twiceArea=cross.Norm();
        if(!double.IsFinite(twiceArea)||twiceArea<1e-12)
            throw new ArgumentException("Pressure patch contains a degenerate triangle.");
        var pressureDirection=cross*(triangle.PressureMpa/twiceArea);
        var xMax=_x0+_nx*_hx;var yMax=_y0+_ny*_hy;var zMax=_z0+_nz*_hz;
        var eps=1e-8*Math.Max(1,Math.Max(_hx,Math.Max(_hy,_hz)));
        foreach(var v in new[]{a,b,c})
            if(v.X<_x0-eps||v.X>xMax+eps||
               v.Y<_y0-eps||v.Y>yMax+eps||
               v.Z<_z0-eps||v.Z>zMax+eps)
                throw new ArgumentException("Pressure patch triangle lies outside FEM bounding box.");

        var xs=new[]{a.X,b.X,c.X};
        var ys=new[]{a.Y,b.Y,c.Y};
        var zs=new[]{a.Z,b.Z,c.Z};
        (int Min,int Max) Span(double[] values,double origin,double h,int n)
        {
            var lo=values.Min();var hi=values.Max();
            var first=Math.Clamp((int)Math.Floor((lo-origin)/h),0,n-1);
            var last=Math.Clamp((int)Math.Floor((hi-origin)/h),0,n-1);
            // An exactly planar patch coincident with an INTERIOR element
            // face must be owned by ONE side. Otherwise it gets counted twice.
            if(hi-lo<1e-9*h)
            {
                var owner=Math.Clamp((int)Math.Floor(((hi+lo)*.5-origin)/h),0,n-1);
                return (owner,owner);
            }
            return (first,last);
        }
        var sx=Span(xs,_x0,_hx,_nx);
        var sy=Span(ys,_y0,_hy,_ny);
        var sz=Span(zs,_z0,_hz,_nz);
        if((long)(sx.Max-sx.Min+1)*(sy.Max-sy.Min+1)*(sz.Max-sz.Min+1)>2_000_000)
            throw new ArgumentException("Surface patch spans too many Hex8 elements.");
        var integratedArea=0.0;
        for(int i=sx.Min;i<=sx.Max;i++)
        for(int j=sy.Min;j<=sy.Max;j++)
        for(int k=sz.Min;k<=sz.Max;k++)
        {
            var polygon=new List<Point3>{a,b,c};
            polygon=ClipPolygon(polygon,0,_x0+i*_hx,true);
            polygon=ClipPolygon(polygon,0,_x0+(i+1)*_hx,false);
            polygon=ClipPolygon(polygon,1,_y0+j*_hy,true);
            polygon=ClipPolygon(polygon,1,_y0+(j+1)*_hy,false);
            polygon=ClipPolygon(polygon,2,_z0+k*_hz,true);
            polygon=ClipPolygon(polygon,2,_z0+(k+1)*_hz,false);
            if(polygon.Count<3)continue;
            for(var q=1;q<polygon.Count-1;q++)
            {
                var aa=polygon[0];var bb=polygon[q];var cc=polygon[q+1];
                var area=Point3.Cross(bb-aa,cc-aa).Norm()*.5;
                if(area<1e-13)continue;
                integratedArea+=area;
                IntegrateClippedTriangle(i,j,k,aa,bb,cc,pressureDirection);
            }
        }
        var originalArea=twiceArea*.5;
        if(Math.Abs(integratedArea-originalArea)>originalArea*3e-9+1e-10)
            throw new ArithmeticException(
                $"Triangle-Hex8 clipping area mismatch {integratedArea:G13} vs {originalArea:G13} mm².");
    }

    internal double[] AppliedForceVector()=>(double[])_appliedRhs.Clone();

    internal (double X,double Y,double Z) AppliedMomentNmm
    {
        get
        {
            var total=new Point3(0,0,0);
            for(var i=0;i<=_nx;i++)
            for(var j=0;j<=_ny;j++)
            for(var k=0;k<=_nz;k++)
            {
                var p=new Point3(_x0+i*_hx,_y0+j*_hy,_z0+k*_hz);
                var d=3*Node(i,j,k);
                total+=Point3.Cross(p,new Point3(
                    _appliedRhs[d],_appliedRhs[d+1],_appliedRhs[d+2]));
            }
            return (total.X,total.Y,total.Z);
        }
    }
}
