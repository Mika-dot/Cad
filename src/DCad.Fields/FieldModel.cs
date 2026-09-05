using DCad.Core;

namespace DCad.Fields;

public readonly record struct StructuredGrid3d(Vector3d Origin, double VoxelSize, int Nx, int Ny, int Nz)
{
    public int CellCount => checked(Nx * Ny * Nz);
    public Vector3d CellCenter(int i, int j, int k)
        => Origin + new Vector3d((i + .5) * VoxelSize, (j + .5) * VoxelSize, (k + .5) * VoxelSize);

    // Dense fields follow NumPy C-order for shape (nx, ny, nz): k is the fastest axis.
    public int LinearIndex(int i, int j, int k)
    {
        if ((uint)i >= (uint)Nx || (uint)j >= (uint)Ny || (uint)k >= (uint)Nz) throw new ArgumentOutOfRangeException();
        return (i * Ny + j) * Nz + k;
    }
    public (int I, int J, int K) FromLinearIndex(int id)
    {
        if ((uint)id >= (uint)CellCount) throw new ArgumentOutOfRangeException(nameof(id));
        int i = id / (Ny * Nz); int rem = id % (Ny * Nz); int j = rem / Nz; int k = rem % Nz;
        return (i, j, k);
    }

    // FEM_Voxel uses a historical element id k*nx*ny + i*ny + j. Keep conversion explicit.
    public int FemElementId(int i, int j, int k)
    {
        if ((uint)i >= (uint)Nx || (uint)j >= (uint)Ny || (uint)k >= (uint)Nz) throw new ArgumentOutOfRangeException();
        return k * Nx * Ny + i * Ny + j;
    }
    public (int I, int J, int K) FromFemElementId(int id)
    {
        if ((uint)id >= (uint)CellCount) throw new ArgumentOutOfRangeException(nameof(id));
        int k = id / (Nx * Ny); int rem = id % (Nx * Ny); int i = rem / Ny; int j = rem % Ny;
        return (i, j, k);
    }
}

public sealed class ScalarField3d
{
    public string Name { get; }
    public StructuredGrid3d Grid { get; }
    public float[] Values { get; }
    public string? Units { get; }

    public ScalarField3d(string name, StructuredGrid3d grid, float[] values, string? units = null)
    {
        if (values.Length != grid.CellCount) throw new ArgumentException("Field size does not match grid.", nameof(values));
        Name = name; Grid = grid; Values = values; Units = units;
    }

    public float this[int i, int j, int k] => Values[Grid.LinearIndex(i,j,k)];
    public (float Min, float Max) Range()
    {
        if (Values.Length == 0) return (0,0);
        float min=float.PositiveInfinity,max=float.NegativeInfinity;
        foreach(float v in Values) if(float.IsFinite(v)){min=Math.Min(min,v);max=Math.Max(max,v);}
        return float.IsFinite(min)?(min,max):(0,0);
    }
}

public sealed class MaskField3d
{
    public string Name { get; }
    public StructuredGrid3d Grid { get; }
    public byte[] Values { get; }
    public MaskField3d(string name, StructuredGrid3d grid, byte[] values)
    {
        if(values.Length!=grid.CellCount) throw new ArgumentException("Mask size does not match grid.",nameof(values));
        Name=name;Grid=grid;Values=values;
    }
    public bool this[int i,int j,int k]=>Values[Grid.LinearIndex(i,j,k)]!=0;
    public int ActiveCount()=>Values.Count(v=>v!=0);
}

public sealed class DcadFieldDocument
{
    public StructuredGrid3d Grid { get; }
    public IReadOnlyDictionary<string,MaskField3d> Masks { get; }
    public IReadOnlyDictionary<string,ScalarField3d> Scalars { get; }
    public string ManifestJson { get; }

    public DcadFieldDocument(StructuredGrid3d grid, IDictionary<string,MaskField3d> masks, IDictionary<string,ScalarField3d> scalars, string manifestJson)
    { Grid=grid;Masks=new Dictionary<string,MaskField3d>(masks);Scalars=new Dictionary<string,ScalarField3d>(scalars);ManifestJson=manifestJson; }
}
