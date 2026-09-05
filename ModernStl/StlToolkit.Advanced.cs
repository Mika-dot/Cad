namespace DCad.StlToolkit;

public readonly record struct TopologyAudit(
    int UniqueVertices,
    int ConnectedComponents,
    int IsolatedTriangles,
    int InconsistentDirectedEdges);

public static class StlAdvanced
{
    private readonly record struct Q(long X,long Y,long Z);
    private readonly record struct Edge(Q A,Q B);
    private readonly record struct DirectedEdge(Q A,Q B);
    private readonly record struct TriKey(Q A,Q B,Q C);

    public static TopologyAudit AnalyzeTopology(StlMesh mesh,double tolerance=1e-7)
    {
        tolerance=Math.Max(tolerance,1e-12);
        var vertices=new HashSet<Q>();
        var edgeFaces=new Dictionary<Edge,List<int>>();
        var directed=new Dictionary<DirectedEdge,int>();

        for(int fi=0;fi<mesh.Triangles.Count;fi++)
        {
            var t=mesh.Triangles[fi];
            var q=new[]{Qv(t.A,tolerance),Qv(t.B,tolerance),Qv(t.C,tolerance)};
            foreach(var v in q)vertices.Add(v);
            AddEdge(q[0],q[1],fi);AddEdge(q[1],q[2],fi);AddEdge(q[2],q[0],fi);
        }

        var adjacency=new List<int>[mesh.Triangles.Count];
        for(int i=0;i<adjacency.Length;i++)adjacency[i]=new List<int>();
        foreach(var faces in edgeFaces.Values)
            for(int i=0;i<faces.Count;i++)for(int j=i+1;j<faces.Count;j++)
            {adjacency[faces[i]].Add(faces[j]);adjacency[faces[j]].Add(faces[i]);}

        int components=0,isolated=0;var seen=new bool[mesh.Triangles.Count];var queue=new Queue<int>();
        for(int root=0;root<seen.Length;root++)
        {
            if(seen[root])continue;components++;int size=0;seen[root]=true;queue.Enqueue(root);
            while(queue.Count>0){int f=queue.Dequeue();size++;foreach(int n in adjacency[f])if(!seen[n]){seen[n]=true;queue.Enqueue(n);}}
            if(size==1)isolated++;
        }

        int inconsistent=0;
        foreach(var pair in directed)
        {
            var reverse=new DirectedEdge(pair.Key.B,pair.Key.A);
            if(pair.Value>0 && !directed.ContainsKey(reverse))inconsistent+=pair.Value;
        }
        return new TopologyAudit(vertices.Count,components,isolated,inconsistent);

        void AddEdge(Q a,Q b,int face)
        {
            var undirected=Compare(a,b)<=0?new Edge(a,b):new Edge(b,a);
            if(!edgeFaces.TryGetValue(undirected,out var list)){list=new List<int>();edgeFaces[undirected]=list;}list.Add(face);
            var d=new DirectedEdge(a,b);directed.TryGetValue(d,out int count);directed[d]=count+1;
        }
    }

    public static StlMesh WeldVertices(StlMesh mesh,double tolerance=1e-7)
    {
        tolerance=Math.Max(tolerance,1e-12);var representatives=new Dictionary<Q,Vec3d>();
        Vec3d Get(Vec3d p){var q=Qv(p,tolerance);if(representatives.TryGetValue(q,out var r))return r;representatives[q]=p;return p;}
        return new StlMesh(mesh.Triangles.Select(t=>new StlTriangle(Get(t.A),Get(t.B),Get(t.C))));
    }

    public static StlMesh RemoveDuplicateTriangles(StlMesh mesh,double tolerance=1e-7)
    {
        tolerance=Math.Max(tolerance,1e-12);var seen=new HashSet<TriKey>();var output=new List<StlTriangle>();
        foreach(var t in mesh.Triangles)
        {
            var q=new[]{Qv(t.A,tolerance),Qv(t.B,tolerance),Qv(t.C,tolerance)};Array.Sort(q,Comparer<Q>.Create(Compare));
            if(seen.Add(new TriKey(q[0],q[1],q[2])))output.Add(t);
        }
        return new StlMesh(output);
    }

    public static StlMesh RepairBasic(StlMesh mesh,double weldTolerance=1e-7,double areaTolerance=1e-12)
        => RemoveDuplicateTriangles(WeldVertices(mesh,weldTolerance).RemoveDegenerate(areaTolerance),weldTolerance);

    private static Q Qv(Vec3d p,double t)=>new((long)Math.Round(p.X/t),(long)Math.Round(p.Y/t),(long)Math.Round(p.Z/t));
    private static int Compare(Q a,Q b){int c=a.X.CompareTo(b.X);if(c!=0)return c;c=a.Y.CompareTo(b.Y);return c!=0?c:a.Z.CompareTo(b.Z);}
}
