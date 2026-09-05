using OpenTK.Mathematics;

namespace DCad.Renderer;

public sealed class MeshData
{
    public float[] Vertices { get; }
    public uint[] Indices { get; }

    // Vertex layout: position xyz, normal xyz, scalar.
    public MeshData(float[] vertices, uint[] indices)
    {
        if (vertices.Length % 7 != 0) throw new ArgumentException("Vertex buffer must use 7 floats per vertex.");
        Vertices = vertices;
        Indices = indices;
    }

    public static MeshData DemoChamferLikeBox()
    {
        var p = new[]
        {
            new Vector3(-1,-1,-1), new Vector3( 1,-1,-1), new Vector3( 1, 1,-1), new Vector3(-1, 1,-1),
            new Vector3(-1,-1, 1), new Vector3( 1,-1, 1), new Vector3( 1, 1, 1), new Vector3(-1, 1, 1),
        };
        var faces = new (int a,int b,int c,int d,Vector3 n)[]
        {
            (0,3,2,1,-Vector3.UnitZ), (4,5,6,7, Vector3.UnitZ),
            (0,1,5,4,-Vector3.UnitY), (3,7,6,2, Vector3.UnitY),
            (0,4,7,3,-Vector3.UnitX), (1,2,6,5, Vector3.UnitX),
        };

        var v = new List<float>();
        var idx = new List<uint>();
        uint baseVertex = 0;
        foreach (var f in faces)
        {
            var ids = new[] { f.a, f.b, f.c, f.d };
            foreach (int id in ids)
            {
                var q = p[id];
                float scalar = (q.Y + 1f) * .5f;
                v.Add(q.X); v.Add(q.Y); v.Add(q.Z);
                v.Add(f.n.X); v.Add(f.n.Y); v.Add(f.n.Z);
                v.Add(scalar);
            }
            idx.Add(baseVertex); idx.Add(baseVertex+1); idx.Add(baseVertex+2);
            idx.Add(baseVertex); idx.Add(baseVertex+2); idx.Add(baseVertex+3);
            baseVertex += 4;
        }
        return new MeshData(v.ToArray(), idx.ToArray());
    }
}
