namespace DCad.Core;

public enum OrientationSign
{
    Negative = -1,
    Zero = 0,
    Positive = 1
}

public readonly record struct SegmentIntersection2d(
    bool Intersects,
    bool Proper,
    bool Collinear,
    Vector2d Point,
    double T,
    double U);

public static class RobustPredicates
{
    // Conservative first-stage error multiplier for the 2x2 determinant.  The fallback is used
    // only near cancellation, so normal CAD cases remain on the fast double path.
    private const double DoubleEpsilon = 2.2204460492503131e-16;

    public static OrientationSign Orient2d(Vector2d a, Vector2d b, Vector2d c)
    {
        double acx = a.X - c.X, bcx = b.X - c.X;
        double acy = a.Y - c.Y, bcy = b.Y - c.Y;
        double det = acx * bcy - acy * bcx;
        double permanent = Math.Abs(acx * bcy) + Math.Abs(acy * bcx);
        double error = (3.0 + 16.0 * DoubleEpsilon) * DoubleEpsilon * permanent;
        if (Math.Abs(det) > error) return Sign(det);

        // decimal provides ~28 significant decimal digits and is a pragmatic deterministic
        // fallback for engineering-scale coordinates.  Extreme coordinates outside decimal
        // range deliberately fall back to the double result rather than throwing.
        try
        {
            decimal dacx = (decimal)a.X - (decimal)c.X;
            decimal dbcx = (decimal)b.X - (decimal)c.X;
            decimal dacy = (decimal)a.Y - (decimal)c.Y;
            decimal dbcy = (decimal)b.Y - (decimal)c.Y;
            decimal exactish = dacx * dbcy - dacy * dbcx;
            return exactish > 0 ? OrientationSign.Positive : exactish < 0 ? OrientationSign.Negative : OrientationSign.Zero;
        }
        catch (OverflowException)
        {
            return Sign(det);
        }
    }

    public static OrientationSign Orient3d(Vector3d a, Vector3d b, Vector3d c, Vector3d d)
    {
        var ad = a - d; var bd = b - d; var cd = c - d;
        double det = Vector3d.Dot(ad, Vector3d.Cross(bd, cd));
        double scale =
            Math.Abs(ad.X) * (Math.Abs(bd.Y * cd.Z) + Math.Abs(bd.Z * cd.Y)) +
            Math.Abs(ad.Y) * (Math.Abs(bd.X * cd.Z) + Math.Abs(bd.Z * cd.X)) +
            Math.Abs(ad.Z) * (Math.Abs(bd.X * cd.Y) + Math.Abs(bd.Y * cd.X));
        double error = 16.0 * DoubleEpsilon * scale;
        if (Math.Abs(det) > error) return Sign(det);

        try
        {
            decimal ax=(decimal)a.X-(decimal)d.X, ay=(decimal)a.Y-(decimal)d.Y, az=(decimal)a.Z-(decimal)d.Z;
            decimal bx=(decimal)b.X-(decimal)d.X, by=(decimal)b.Y-(decimal)d.Y, bz=(decimal)b.Z-(decimal)d.Z;
            decimal cx=(decimal)c.X-(decimal)d.X, cy=(decimal)c.Y-(decimal)d.Y, cz=(decimal)c.Z-(decimal)d.Z;
            decimal exactish = ax*(by*cz-bz*cy) - ay*(bx*cz-bz*cx) + az*(bx*cy-by*cx);
            return exactish > 0 ? OrientationSign.Positive : exactish < 0 ? OrientationSign.Negative : OrientationSign.Zero;
        }
        catch (OverflowException)
        {
            return Sign(det);
        }
    }

    public static SegmentIntersection2d IntersectSegments(
        Vector2d a,
        Vector2d b,
        Vector2d c,
        Vector2d d,
        GeometryTolerance? tolerance = null)
    {
        var tol = tolerance ?? GeometryTolerance.Default;
        var ab = b-a; var cd=d-c; var ca=c-a;
        double denom = Vector2d.Cross(ab, cd);
        double scale = Math.Max(1.0, Math.Sqrt(ab.LengthSquared) + Math.Sqrt(cd.LengthSquared));
        double eps = tol.AtScale(scale*scale);

        var o1=Orient2d(a,b,c);var o2=Orient2d(a,b,d);var o3=Orient2d(c,d,a);var o4=Orient2d(c,d,b);
        bool collinear=o1==OrientationSign.Zero&&o2==OrientationSign.Zero&&o3==OrientationSign.Zero&&o4==OrientationSign.Zero;
        if(collinear)
        {
            // For collinear segments return overlap existence.  A single representative point is
            // enough for classification; callers needing the overlap interval can project onto
            // the dominant axis.
            bool overlap=IntervalsOverlap(a.X,b.X,c.X,d.X,eps)&&IntervalsOverlap(a.Y,b.Y,c.Y,d.Y,eps);
            if(!overlap)return new(false,false,true,default,double.NaN,double.NaN);
            var p=new Vector2d((Math.Max(Math.Min(a.X,b.X),Math.Min(c.X,d.X))+Math.Min(Math.Max(a.X,b.X),Math.Max(c.X,d.X)))*0.5,
                               (Math.Max(Math.Min(a.Y,b.Y),Math.Min(c.Y,d.Y))+Math.Min(Math.Max(a.Y,b.Y),Math.Max(c.Y,d.Y)))*0.5);
            return new(true,false,true,p,double.NaN,double.NaN);
        }

        bool intersects=(o1==OrientationSign.Zero||o2==OrientationSign.Zero||o1!=o2) &&
                        (o3==OrientationSign.Zero||o4==OrientationSign.Zero||o3!=o4);
        if(!intersects||Math.Abs(denom)<=eps)return new(false,false,false,default,double.NaN,double.NaN);
        double t=Vector2d.Cross(ca,cd)/denom;
        double u=Vector2d.Cross(ca,ab)/denom;
        var point=a+ab*t;
        bool proper=t>eps&&t<1.0-eps&&u>eps&&u<1.0-eps;
        return new(true,proper,false,point,t,u);
    }

    private static OrientationSign Sign(double v)=>v>0?OrientationSign.Positive:v<0?OrientationSign.Negative:OrientationSign.Zero;
    private static bool IntervalsOverlap(double a,double b,double c,double d,double eps)
        => Math.Max(Math.Min(a,b),Math.Min(c,d))<=Math.Min(Math.Max(a,b),Math.Max(c,d))+eps;
}
