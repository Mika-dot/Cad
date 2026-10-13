using System.Globalization;
using System.Text;

namespace FvmGenerativeDemo;

// An STL is a TRIANGULATED pressure PATCH, not necessarily a closed solid.
// The oriented triangles determine the pressure direction. A real engineering
// scenario must select triangles lying on a physically exposed interface.
internal static class SurfacePatchStl
{
    internal static IReadOnlyList<HexTrianglePressure> Read(
        string path,double pressureMpa)
    {
        if(!double.IsFinite(pressureMpa)||pressureMpa==0)
            throw new ArgumentException("STL patch pressure must be nonzero and finite.");
        var data=File.ReadAllBytes(path);
        var faces=new List<HexTrianglePressure>();
        void Add(Point3 a,Point3 b,Point3 c)
        {
            var normal=Point3.Cross(b-a,c-a);
            if(!double.IsFinite(normal.Norm())||normal.Norm()<1e-10)
                throw new ArgumentException("STL surface pressure patch has degenerate facet.");
            faces.Add(new HexTrianglePressure(a,b,c,pressureMpa));
        }
        // Binary format: 80-byte header + 4-byte face count + 50 bytes/facet.
        // Do not rely on the first characters (binary STL can start 'solid').
        if(data.Length>=84)
        {
            var count=BitConverter.ToUInt32(data,80);
            if(count>0&&count<=1_000_000&&84UL+50UL*count==(ulong)data.Length)
            {
                using var input=new BinaryReader(new MemoryStream(data));
                input.ReadBytes(84);
                for(var i=0;i<count;i++)
                {
                    input.ReadBytes(12); // supplied normal is advisory only
                    Point3 Vertex()=>new(input.ReadSingle(),input.ReadSingle(),input.ReadSingle());
                    var a=Vertex();var b=Vertex();var c=Vertex();
                    input.ReadUInt16();
                    Add(a,b,c);
                }
                return faces;
            }
        }
        var ascii=new StreamReader(new MemoryStream(data),Encoding.ASCII);
        var vertices=new List<Point3>(3);
        string? line;
        while((line=ascii.ReadLine()) is not null)
        {
            var parts=line.Split((char[]?)null,
                StringSplitOptions.RemoveEmptyEntries);
            if(parts.Length==0||!parts[0].Equals("vertex",
                    StringComparison.OrdinalIgnoreCase))continue;
            if(parts.Length!=4)
                throw new ArgumentException("Malformed ASCII STL vertex in pressure patch.");
            var p=new Point3(
                double.Parse(parts[1],CultureInfo.InvariantCulture),
                double.Parse(parts[2],CultureInfo.InvariantCulture),
                double.Parse(parts[3],CultureInfo.InvariantCulture));
            vertices.Add(p);
            if(vertices.Count==3)
            {
                Add(vertices[0],vertices[1],vertices[2]);
                vertices.Clear();
            }
        }
        if(vertices.Count!=0||faces.Count==0)
            throw new ArgumentException("Pressure STL must contain complete triangles.");
        return faces;
    }
}
