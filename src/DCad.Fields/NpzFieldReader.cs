using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DCad.Core;

namespace DCad.Fields;

internal sealed class NpyArray
{
    public string DType { get; }
    public int[] Shape { get; }
    public byte[] Data { get; }

    private NpyArray(string dtype, int[] shape, byte[] data)
    { DType=dtype; Shape=shape; Data=data; }

    public static NpyArray Read(Stream stream)
    {
        using var br=new BinaryReader(stream,Encoding.UTF8,leaveOpen:true);
        byte[] magic=br.ReadBytes(6);
        if(magic.Length!=6||magic[0]!=0x93||Encoding.ASCII.GetString(magic,1,5)!="NUMPY")throw new InvalidDataException("Invalid NPY magic.");
        byte major=br.ReadByte();br.ReadByte();
        int headerLength=major switch{1=>br.ReadUInt16(),2 or 3=>checked((int)br.ReadUInt32()),_=>throw new InvalidDataException($"Unsupported NPY version {major}.")};
        var headerEncoding=major==3?Encoding.UTF8:Encoding.ASCII;
        string header=headerEncoding.GetString(br.ReadBytes(headerLength));
        string dtype=Match(header,"['\"]descr['\"]\\s*:\\s*['\"]([^'\"]+)['\"]");
        string fortran=Match(header,"['\"]fortran_order['\"]\\s*:\\s*(True|False)");
        if(!string.Equals(fortran,"False",StringComparison.Ordinal))throw new NotSupportedException("Fortran-order NPY arrays are not supported.");
        string shapeText=Match(header,"['\"]shape['\"]\\s*:\\s*\\(([^)]*)\\)");
        int[] shape=shapeText.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Select(int.Parse).ToArray();
        byte[] data=br.ReadBytes(checked((int)(stream.Length-stream.Position)));
        return new NpyArray(dtype,shape,data);
    }

    static string Match(string text,string pattern)
    {
        var m=Regex.Match(text,pattern,RegexOptions.CultureInvariant);
        if(!m.Success)throw new InvalidDataException("Malformed NPY header: "+text);
        return m.Groups[1].Value;
    }

    public int ElementCount=>Shape.Length==0?1:Shape.Aggregate(1,checked((a,b)=>a*b));

    public byte[] AsBytes()
    {
        if(DType is not "|u1" and not "<u1" and not "|i1" and not "<i1")throw new InvalidDataException($"Expected byte NPY, got {DType}.");
        if(Data.Length!=ElementCount)throw new InvalidDataException("NPY byte count mismatch.");
        return Data.ToArray();
    }

    public int[] AsInt32()
    {
        if(DType!="<i4"&&DType!="|i4")throw new InvalidDataException($"Expected int32 NPY, got {DType}.");
        if(Data.Length!=ElementCount*4)throw new InvalidDataException("NPY int32 byte count mismatch.");
        var r=new int[ElementCount];for(int i=0;i<r.Length;i++)r[i]=BinaryPrimitives.ReadInt32LittleEndian(Data.AsSpan(i*4,4));return r;
    }

    public long[] AsInt64()
    {
        if(DType!="<i8"&&DType!="|i8")throw new InvalidDataException($"Expected int64 NPY, got {DType}.");
        if(Data.Length!=ElementCount*8)throw new InvalidDataException("NPY int64 byte count mismatch.");
        var r=new long[ElementCount];for(int i=0;i<r.Length;i++)r[i]=BinaryPrimitives.ReadInt64LittleEndian(Data.AsSpan(i*8,8));return r;
    }

    public float[] AsFloat32()
    {
        if(DType=="<f4"||DType=="|f4")
        {
            if(Data.Length!=ElementCount*4)throw new InvalidDataException("NPY float32 byte count mismatch.");
            var r=new float[ElementCount];for(int i=0;i<r.Length;i++)r[i]=BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(Data.AsSpan(i*4,4)));return r;
        }
        if(DType=="<f8"||DType=="|f8")return AsFloat64().Select(v=>(float)v).ToArray();
        throw new InvalidDataException($"Expected floating NPY, got {DType}.");
    }

    public double[] AsFloat64()
    {
        if(DType=="<f8"||DType=="|f8")
        {
            if(Data.Length!=ElementCount*8)throw new InvalidDataException("NPY float64 byte count mismatch.");
            var r=new double[ElementCount];for(int i=0;i<r.Length;i++)r[i]=BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(Data.AsSpan(i*8,8)));return r;
        }
        if(DType=="<f4"||DType=="|f4")return AsFloat32().Select(v=>(double)v).ToArray();
        throw new InvalidDataException($"Expected floating NPY, got {DType}.");
    }

    public string AsUnicodeString()
    {
        var m=Regex.Match(DType,@"^[<|]U(\d+)$");
        if(!m.Success)throw new InvalidDataException($"Expected Unicode NPY, got {DType}.");
        int chars=int.Parse(m.Groups[1].Value);
        if(ElementCount!=1)throw new InvalidDataException("Only scalar Unicode NPY values are supported.");
        int expected=checked(chars*4);if(Data.Length!=expected)throw new InvalidDataException("NPY Unicode byte count mismatch.");
        return Encoding.UTF32.GetString(Data).TrimEnd('\0');
    }
}

public static class DcadFieldArchive
{
    static readonly string[] MaskNames={"anchor_mask","load_mask","obstacle_mask","preserve_mask","design_mask","connector_mask"};

    public static DcadFieldDocument Load(string path)
    {
        using var zip=ZipFile.OpenRead(path);
        var arrays=new Dictionary<string,NpyArray>(StringComparer.OrdinalIgnoreCase);
        foreach(var entry in zip.Entries)
        {
            if(!entry.FullName.EndsWith(".npy",StringComparison.OrdinalIgnoreCase))continue;
            using var s=entry.Open();arrays[Path.GetFileNameWithoutExtension(entry.Name)]=NpyArray.Read(s);
        }

        NpyArray Need(string name)=>arrays.TryGetValue(name,out var a)?a:throw new InvalidDataException($"DCad field is missing '{name}'.");
        var origin=Need("origin").AsFloat64();var shape=Need("shape").AsInt32();double voxel=Need("voxel_size").AsFloat64()[0];
        if(origin.Length!=3||shape.Length!=3)throw new InvalidDataException("Invalid field grid header.");
        var grid=new StructuredGrid3d(new Vector3d(origin[0],origin[1],origin[2]),voxel,shape[0],shape[1],shape[2]);

        string manifest=Need("manifest_json").AsUnicodeString();
        using var manifestDoc=JsonDocument.Parse(manifest);
        string stressUnits="N/mm^2";
        if(manifestDoc.RootElement.TryGetProperty("units",out var units)&&units.TryGetProperty("stress",out var su))stressUnits=su.GetString()??stressUnits;

        var masks=new Dictionary<string,MaskField3d>();
        foreach(string name in MaskNames)
            if(arrays.TryGetValue(name,out var m)){ValidateShape(m,grid,name);masks[name]=new MaskField3d(name,grid,m.AsBytes());}

        var scalars=new Dictionary<string,ScalarField3d>();
        if(arrays.TryGetValue("density",out var density)){ValidateShape(density,grid,"density");scalars["density"]=new ScalarField3d("density",grid,density.AsFloat32(),"1");}
        if(arrays.TryGetValue("stress",out var stress)){ValidateShape(stress,grid,"stress");scalars["stress"]=new ScalarField3d("stress",grid,stress.AsFloat32(),stressUnits);}
        return new DcadFieldDocument(grid,masks,scalars,manifest);
    }

    static void ValidateShape(NpyArray array,StructuredGrid3d grid,string name)
    {
        if(array.Shape.Length!=3||array.Shape[0]!=grid.Nx||array.Shape[1]!=grid.Ny||array.Shape[2]!=grid.Nz)
            throw new InvalidDataException($"Field '{name}' shape does not match grid.");
    }
}
