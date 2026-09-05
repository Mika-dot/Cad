using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace OpenGL_lesson_CSharp.Modern
{
    public abstract class VoxelOperation
    {
        public abstract string Kind { get; }
        public abstract void Apply(SparseVoxelGrid grid);
        public abstract string Serialize();
        public override string ToString() { return Serialize(); }

        protected static string F(double value) { return value.ToString("R", CultureInfo.InvariantCulture); }
        protected static string I(int value) { return value.ToString(CultureInfo.InvariantCulture); }
        protected static string B(byte value) { return value.ToString(CultureInfo.InvariantCulture); }
    }

    public sealed class AddBoxOperation : VoxelOperation
    {
        public int X0, Y0, Z0, X1, Y1, Z1;
        public byte Material;
        public override string Kind { get { return "ADD_BOX"; } }
        public AddBoxOperation(int x0, int y0, int z0, int x1, int y1, int z1, byte material = 1)
        { X0=x0;Y0=y0;Z0=z0;X1=x1;Y1=y1;Z1=z1;Material=material; }
        public override void Apply(SparseVoxelGrid grid) { grid.AddBox(X0,Y0,Z0,X1,Y1,Z1,Material); }
        public override string Serialize() { return string.Join("|", Kind,I(X0),I(Y0),I(Z0),I(X1),I(Y1),I(Z1),B(Material)); }
    }

    public sealed class SubtractBoxOperation : VoxelOperation
    {
        public int X0, Y0, Z0, X1, Y1, Z1;
        public override string Kind { get { return "SUB_BOX"; } }
        public SubtractBoxOperation(int x0, int y0, int z0, int x1, int y1, int z1)
        { X0=x0;Y0=y0;Z0=z0;X1=x1;Y1=y1;Z1=z1; }
        public override void Apply(SparseVoxelGrid grid) { grid.SubtractBox(X0,Y0,Z0,X1,Y1,Z1); }
        public override string Serialize() { return string.Join("|",Kind,I(X0),I(Y0),I(Z0),I(X1),I(Y1),I(Z1)); }
    }

    public sealed class AddSphereOperation : VoxelOperation
    {
        public double X,Y,Z,Radius; public byte Material;
        public override string Kind { get { return "ADD_SPHERE"; } }
        public AddSphereOperation(double x,double y,double z,double radius,byte material=1)
        { X=x;Y=y;Z=z;Radius=radius;Material=material; }
        public override void Apply(SparseVoxelGrid grid) { grid.AddSphere(X,Y,Z,Radius,Material); }
        public override string Serialize() { return string.Join("|",Kind,F(X),F(Y),F(Z),F(Radius),B(Material)); }
    }

    public sealed class SubtractSphereOperation : VoxelOperation
    {
        public double X,Y,Z,Radius;
        public override string Kind { get { return "SUB_SPHERE"; } }
        public SubtractSphereOperation(double x,double y,double z,double radius)
        { X=x;Y=y;Z=z;Radius=radius; }
        public override void Apply(SparseVoxelGrid grid) { grid.SubtractSphere(X,Y,Z,Radius); }
        public override string Serialize() { return string.Join("|",Kind,F(X),F(Y),F(Z),F(Radius)); }
    }

    public sealed class AddCylinderZOperation : VoxelOperation
    {
        public double X,Y,Radius; public int Z0,Z1; public byte Material;
        public override string Kind { get { return "ADD_CYLINDER_Z"; } }
        public AddCylinderZOperation(double x,double y,int z0,int z1,double radius,byte material=1)
        { X=x;Y=y;Z0=z0;Z1=z1;Radius=radius;Material=material; }
        public override void Apply(SparseVoxelGrid grid) { grid.AddCylinderZ(X,Y,Z0,Z1,Radius,Material); }
        public override string Serialize() { return string.Join("|",Kind,F(X),F(Y),I(Z0),I(Z1),F(Radius),B(Material)); }
    }

    public sealed class ExtrudePolygonOperation : VoxelOperation
    {
        public double[] Xs; public double[] Ys; public int Z0,Z1; public byte Material; public bool Subtract;
        public override string Kind { get { return "EXTRUDE_POLYGON"; } }
        public ExtrudePolygonOperation(IEnumerable<double> xs,IEnumerable<double> ys,int z0,int z1,byte material=1,bool subtract=false)
        {
            Xs=xs.ToArray();Ys=ys.ToArray();Z0=z0;Z1=z1;Material=material;Subtract=subtract;
            if(Xs.Length!=Ys.Length || Xs.Length<3) throw new ArgumentException("Polygon requires paired coordinates.");
        }
        public override void Apply(SparseVoxelGrid grid) { grid.ExtrudePolygon(Xs,Ys,Z0,Z1,Material,Subtract); }
        public override string Serialize()
        {
            string xs=string.Join(";",Xs.Select(F)); string ys=string.Join(";",Ys.Select(F));
            return string.Join("|",Kind,xs,ys,I(Z0),I(Z1),B(Material),Subtract?"1":"0");
        }
    }

    public sealed class TranslateOperation : VoxelOperation
    {
        public int X,Y,Z;
        public override string Kind { get { return "TRANSLATE"; } }
        public TranslateOperation(int x,int y,int z) { X=x;Y=y;Z=z; }
        public override void Apply(SparseVoxelGrid grid) { grid.Translate(X,Y,Z); }
        public override string Serialize() { return string.Join("|",Kind,I(X),I(Y),I(Z)); }
    }

    public static class VoxelOperationCodec
    {
        private static readonly CultureInfo CI = CultureInfo.InvariantCulture;
        public static VoxelOperation Parse(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) throw new FormatException("Empty operation.");
            string[] p=line.Split('|');
            switch(p[0].Trim().ToUpperInvariant())
            {
                case "ADD_BOX": return new AddBoxOperation(Int(p,1),Int(p,2),Int(p,3),Int(p,4),Int(p,5),Int(p,6),Byte(p,7));
                case "SUB_BOX": return new SubtractBoxOperation(Int(p,1),Int(p,2),Int(p,3),Int(p,4),Int(p,5),Int(p,6));
                case "ADD_SPHERE": return new AddSphereOperation(Double(p,1),Double(p,2),Double(p,3),Double(p,4),Byte(p,5));
                case "SUB_SPHERE": return new SubtractSphereOperation(Double(p,1),Double(p,2),Double(p,3),Double(p,4));
                case "ADD_CYLINDER_Z": return new AddCylinderZOperation(Double(p,1),Double(p,2),Int(p,3),Int(p,4),Double(p,5),Byte(p,6));
                case "EXTRUDE_POLYGON": return new ExtrudePolygonOperation(Doubles(p,1),Doubles(p,2),Int(p,3),Int(p,4),Byte(p,5),p[6]=="1");
                case "TRANSLATE": return new TranslateOperation(Int(p,1),Int(p,2),Int(p,3));
                default: throw new FormatException("Unknown DCad operation: "+p[0]);
            }
        }
        private static int Int(string[] p,int i) { Require(p,i); return int.Parse(p[i],NumberStyles.Integer,CI); }
        private static byte Byte(string[] p,int i) { Require(p,i); return byte.Parse(p[i],NumberStyles.Integer,CI); }
        private static double Double(string[] p,int i) { Require(p,i); return double.Parse(p[i],NumberStyles.Float,CI); }
        private static double[] Doubles(string[] p,int i) { Require(p,i); return p[i].Split(';').Select(x=>double.Parse(x,NumberStyles.Float,CI)).ToArray(); }
        private static void Require(string[] p,int i) { if(i>=p.Length) throw new FormatException("Operation has too few fields."); }
    }

    public sealed class VoxelDocument
    {
        private readonly List<VoxelOperation> operations = new List<VoxelOperation>();
        private int cursor;
        public SparseVoxelGrid Grid { get; private set; }
        public int OperationCount { get { return cursor; } }
        public bool CanUndo { get { return cursor>0; } }
        public bool CanRedo { get { return cursor<operations.Count; } }
        public IEnumerable<VoxelOperation> Operations { get { return operations.Take(cursor); } }

        public VoxelDocument() { Grid=new SparseVoxelGrid(); }

        public void Execute(VoxelOperation operation)
        {
            if(operation==null) throw new ArgumentNullException(nameof(operation));
            if(cursor<operations.Count) operations.RemoveRange(cursor,operations.Count-cursor);
            operations.Add(operation); cursor++;
            operation.Apply(Grid);
        }

        public bool Undo()
        {
            if(!CanUndo) return false;
            cursor--; Rebuild(); return true;
        }

        public bool Redo()
        {
            if(!CanRedo) return false;
            operations[cursor].Apply(Grid); cursor++; return true;
        }

        public void Clear()
        {
            operations.Clear(); cursor=0; Grid=new SparseVoxelGrid();
        }

        public void Rebuild()
        {
            Grid=new SparseVoxelGrid();
            for(int i=0;i<cursor;i++) operations[i].Apply(Grid);
        }

        public void Save(string path)
        {
            using(var writer=new StreamWriter(path))
            {
                writer.WriteLine("DCAD-V1|1");
                foreach(var operation in Operations) writer.WriteLine(operation.Serialize());
            }
        }

        public static VoxelDocument Load(string path)
        {
            string[] lines=File.ReadAllLines(path);
            if(lines.Length==0 || lines[0].Trim()!="DCAD-V1|1") throw new FormatException("Unsupported DCad V1 document.");
            var doc=new VoxelDocument();
            for(int i=1;i<lines.Length;i++) if(!string.IsNullOrWhiteSpace(lines[i])) doc.Execute(VoxelOperationCodec.Parse(lines[i]));
            return doc;
        }
    }

    public static class VoxelDocumentSelfTest
    {
        public static void Run()
        {
            var doc=new VoxelDocument();
            doc.Execute(new AddBoxOperation(0,0,0,10,8,4));
            int full=doc.Grid.Count;
            if(full!=320) throw new InvalidOperationException("Document self-test: AddBox count.");
            doc.Execute(new SubtractSphereOperation(5,4,2,2));
            int cut=doc.Grid.Count;
            if(cut>=full || cut<=0) throw new InvalidOperationException("Document self-test: subtraction.");
            doc.Execute(new AddCylinderZOperation(5,4,0,6,1.2,2));
            int afterCylinder=doc.Grid.Count;
            if(!doc.Undo() || doc.Grid.Count!=cut) throw new InvalidOperationException("Document self-test: undo.");
            if(!doc.Redo() || doc.Grid.Count!=afterCylinder) throw new InvalidOperationException("Document self-test: redo.");

            string path=Path.Combine(Path.GetTempPath(),"dcad-v1-selftest.dcad");
            doc.Save(path);
            var loaded=VoxelDocument.Load(path);
            File.Delete(path);
            if(loaded.Grid.Count!=doc.Grid.Count || loaded.OperationCount!=doc.OperationCount)
                throw new InvalidOperationException("Document self-test: serialization.");
        }
    }
}
