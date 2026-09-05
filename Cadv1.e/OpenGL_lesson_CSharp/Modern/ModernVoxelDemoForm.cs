using OpenGL_lesson_CSharp.Modern;
using SharpGL;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace OpenGL_lesson_CSharp
{
    public sealed class ModernVoxelDemoForm : Form
    {
        readonly OpenGLControl glc = new OpenGLControl();
        readonly SparseVoxelGrid grid = new SparseVoxelGrid();
        readonly Label status = new Label();
        readonly CheckBox wire = new CheckBox { Text = "Wire", AutoSize = true };
        readonly CheckBox heat = new CheckBox { Text = "Heat", AutoSize = true };
        double yaw = -38, pitch = 26, distance = 42;
        bool orbit; Point last;

        public ModernVoxelDemoForm()
        {
            Text = "DCad V1 - Sparse Voxel CAD"; Width = 1280; Height = 800;
            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 36, Padding = new Padding(4) };
            bar.Controls.Add(MakeButton("Demo", (s,e) => BuildDemo()));
            bar.Controls.Add(MakeButton("Add sphere", (s,e) => { grid.AddSphere(6,6,10,4,2); RefreshStatus(); }));
            bar.Controls.Add(MakeButton("Cut sphere", (s,e) => { grid.SubtractSphere(6,6,7,3); RefreshStatus(); }));
            bar.Controls.Add(wire); bar.Controls.Add(heat); bar.Controls.Add(status);
            glc.Dock = DockStyle.Fill; glc.DrawFPS = true; glc.FrameRate = 60;
            glc.OpenGLVersion = SharpGL.Version.OpenGLVersion.OpenGL2_1;
            glc.RenderContextType = RenderContextType.DIBSection; glc.RenderTrigger = RenderTrigger.TimerBased;
            glc.OpenGLInitialized += Initialized; glc.OpenGLDraw += Draw; glc.Resized += Resized;
            glc.MouseDown += (s,e) => { if(e.Button==MouseButtons.Left){orbit=true;last=e.Location;} };
            glc.MouseUp += (s,e) => orbit=false; glc.MouseMove += MouseMove; glc.MouseWheel += MouseWheel;
            Controls.Add(glc); Controls.Add(bar); BuildDemo();
        }

        Button MakeButton(string text, EventHandler handler)
        { var b=new Button{Text=text,AutoSize=true}; b.Click+=handler; return b; }

        void BuildDemo()
        {
            grid.Clear(); grid.AddBox(0,0,0,12,12,8,1); grid.SubtractBox(2,2,2,10,10,8);
            grid.AddSphere(6,6,9,4.5,2); grid.SubtractSphere(6,6,9,2); grid.AddCylinderZ(6,6,0,14,1.4,3);
            grid.SetScalar((x,y,z)=>(float)(z-5+Math.Sin(x*.45)*2)); RefreshStatus();
        }

        void RefreshStatus() { status.Text=string.Format("  voxels {0:N0} | surface faces {1:N0}",grid.Count,grid.SurfaceFaceCount()); }
        void Initialized(object s, EventArgs e) { var gl=glc.OpenGL; gl.ClearColor(.055f,.065f,.08f,1); gl.Enable(OpenGL.GL_DEPTH_TEST); gl.Enable(OpenGL.GL_CULL_FACE); }
        void Resized(object s, EventArgs e) { glc.OpenGL.Viewport(0,0,Math.Max(1,glc.Width),Math.Max(1,glc.Height)); }

        void Draw(object s, RenderEventArgs e)
        {
            var gl=glc.OpenGL; gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT|OpenGL.GL_DEPTH_BUFFER_BIT);
            gl.MatrixMode(OpenGL.GL_PROJECTION); gl.LoadIdentity(); gl.Perspective(50.0,Math.Max(1.0,(double)glc.Width/Math.Max(1,glc.Height)),.1,1000);
            gl.MatrixMode(OpenGL.GL_MODELVIEW); gl.LoadIdentity();
            double yr=yaw*Math.PI/180, pr=pitch*Math.PI/180, cp=Math.Cos(pr);
            gl.LookAt(6+distance*cp*Math.Cos(yr),6+distance*Math.Sin(pr),6+distance*cp*Math.Sin(yr),6,6,6,0,1,0);
            DrawGrid(gl); DrawFaces(gl,false); if(wire.Checked) DrawFaces(gl,true); gl.Flush();
        }

        void DrawGrid(OpenGL gl)
        {
            gl.Disable(OpenGL.GL_CULL_FACE); gl.Begin(OpenGL.GL_LINES); gl.Color(.18f,.21f,.26f);
            for(int i=-10;i<=25;i++){gl.Vertex(i,0,-10);gl.Vertex(i,0,25);gl.Vertex(-10,0,i);gl.Vertex(25,0,i);}
            gl.Color(1f,.2f,.2f);gl.Vertex(0,0,0);gl.Vertex(8,0,0); gl.Color(.2f,1f,.3f);gl.Vertex(0,0,0);gl.Vertex(0,8,0); gl.Color(.2f,.5f,1f);gl.Vertex(0,0,0);gl.Vertex(0,0,8);
            gl.End(); gl.Enable(OpenGL.GL_CULL_FACE);
        }

        void DrawFaces(OpenGL gl,bool outline)
        {
            if(outline){gl.PolygonMode(OpenGL.GL_FRONT_AND_BACK,OpenGL.GL_LINE);gl.Disable(OpenGL.GL_CULL_FACE);gl.Color(.05f,.05f,.06f);} else {gl.PolygonMode(OpenGL.GL_FRONT_AND_BACK,OpenGL.GL_FILL);gl.Enable(OpenGL.GL_CULL_FACE);}
            gl.Begin(OpenGL.GL_QUADS); foreach(var f in grid.SurfaceFaces()){if(!outline) SetColor(gl,f); Emit(gl,f);} gl.End();
            gl.PolygonMode(OpenGL.GL_FRONT_AND_BACK,OpenGL.GL_FILL);gl.Enable(OpenGL.GL_CULL_FACE);
        }

        void SetColor(OpenGL gl,VoxelFace f)
        {
            float r=.65f,g=.72f,b=.78f;
            if(heat.Checked){float t=Math.Max(0,Math.Min(1,(f.Data.Scalar+8)/20));r=Math.Min(1,t*1.6f);b=Math.Min(1,(1-t)*1.6f);g=.18f+.55f*(1-Math.Abs(t*2-1));}
            else if(f.Data.Material==2){r=.88f;g=.48f;b=.18f;} else if(f.Data.Material==3){r=.3f;g=.68f;b=.95f;}
            float sh=(f.Direction==VoxelFaceDirection.PositiveY)?1f:(f.Direction==VoxelFaceDirection.NegativeY?.55f:.78f); gl.Color(r*sh,g*sh,b*sh);
        }

        static void Emit(OpenGL gl,VoxelFace f)
        {
            double x=f.Cell.X,y=f.Cell.Y,z=f.Cell.Z,x1=x+1,y1=y+1,z1=z+1;
            switch(f.Direction){
                case VoxelFaceDirection.NegativeX:gl.Vertex(x,y,z);gl.Vertex(x,y,z1);gl.Vertex(x,y1,z1);gl.Vertex(x,y1,z);break;
                case VoxelFaceDirection.PositiveX:gl.Vertex(x1,y,z1);gl.Vertex(x1,y,z);gl.Vertex(x1,y1,z);gl.Vertex(x1,y1,z1);break;
                case VoxelFaceDirection.NegativeY:gl.Vertex(x,y,z1);gl.Vertex(x,y,z);gl.Vertex(x1,y,z);gl.Vertex(x1,y,z1);break;
                case VoxelFaceDirection.PositiveY:gl.Vertex(x,y1,z);gl.Vertex(x,y1,z1);gl.Vertex(x1,y1,z1);gl.Vertex(x1,y1,z);break;
                case VoxelFaceDirection.NegativeZ:gl.Vertex(x1,y,z);gl.Vertex(x,y,z);gl.Vertex(x,y1,z);gl.Vertex(x1,y1,z);break;
                default:gl.Vertex(x,y,z1);gl.Vertex(x1,y,z1);gl.Vertex(x1,y1,z1);gl.Vertex(x,y1,z1);break;}
        }

        void MouseMove(object s,MouseEventArgs e){if(!orbit)return;yaw+=(e.X-last.X)*.45;pitch-=(e.Y-last.Y)*.45;pitch=Math.Max(-85,Math.Min(85,pitch));last=e.Location;}
        void MouseWheel(object s,MouseEventArgs e){distance*=e.Delta>0?.88:1.14;distance=Math.Max(4,Math.Min(300,distance));}
    }
}
