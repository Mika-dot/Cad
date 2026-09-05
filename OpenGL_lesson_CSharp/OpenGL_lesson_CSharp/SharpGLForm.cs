using System;
using System.Drawing;
using System.Windows.Forms;
using SharpGL;

namespace OpenGL_lesson_CSharp
{
    public partial class SharpGLForm : Form
    {
        private readonly Camera3D camera = new Camera3D();
        private readonly Scene3D scene = new Scene3D();
        private readonly ViewportRenderer renderer = new ViewportRenderer();

        private ToolStrip toolStrip;
        private TreeView sceneTree;
        private PropertyGrid propertyGrid;
        private ToolStripStatusLabel statusCamera;
        private ToolStripStatusLabel statusSelection;
        private Point lastMouse;
        private Point mouseDownPoint;
        private MouseButtons dragButton = MouseButtons.None;
        private bool altOrbit;

        public SharpGLForm()
        {
            InitializeComponent();
            BuildApplicationShell();
            BuildDemoScene();
            scene.SelectionChanged += Scene_SelectionChanged;
            scene.SceneChanged += Scene_SceneChanged;
            KeyPreview = true;
            UpdateSceneTree();
            UpdateStatus();
        }

        private void BuildApplicationShell()
        {
            Controls.Remove(openGLControl);

            toolStrip = new ToolStrip();
            toolStrip.GripStyle = ToolStripGripStyle.Hidden;
            toolStrip.RenderMode = ToolStripRenderMode.System;
            toolStrip.Dock = DockStyle.Top;
            AddButton("Fit", delegate { FitScene(); });
            AddButton("Iso", delegate { SetView(StandardView.Isometric); });
            AddButton("Front", delegate { SetView(StandardView.Front); });
            AddButton("Top", delegate { SetView(StandardView.Top); });
            AddButton("Right", delegate { SetView(StandardView.Right); });
            toolStrip.Items.Add(new ToolStripSeparator());
            AddButton("Perspective", delegate { ToggleProjection(); });
            AddButton("Grid", delegate { renderer.Settings.ShowGrid = !renderer.Settings.ShowGrid; UpdateStatus(); });
            AddButton("Lighting", delegate { renderer.Settings.Lighting = !renderer.Settings.Lighting; UpdateStatus(); });

            ToolStripDropDownButton render = new ToolStripDropDownButton("Render");
            foreach (ViewportRenderMode mode in Enum.GetValues(typeof(ViewportRenderMode)))
            {
                ViewportRenderMode captured = mode;
                render.DropDownItems.Add(mode.ToString(), null, delegate { renderer.Settings.Mode = captured; UpdateStatus(); });
            }
            toolStrip.Items.Add(render);

            SplitContainer split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.Orientation = Orientation.Vertical;
            split.SplitterDistance = Math.Max(650, ClientSize.Width - 285);
            split.Panel2MinSize = 230;

            openGLControl.Dock = DockStyle.Fill;
            openGLControl.BackColor = Color.FromArgb(14, 17, 22);
            split.Panel1.Controls.Add(openGLControl);

            SplitContainer inspector = new SplitContainer();
            inspector.Dock = DockStyle.Fill;
            inspector.Orientation = Orientation.Horizontal;
            inspector.SplitterDistance = 210;
            sceneTree = new TreeView { Dock = DockStyle.Fill, HideSelection = false, BorderStyle = BorderStyle.FixedSingle };
            sceneTree.AfterSelect += sceneTree_AfterSelect;
            propertyGrid = new PropertyGrid { Dock = DockStyle.Fill, HelpVisible = false, ToolbarVisible = true };
            propertyGrid.PropertyValueChanged += delegate { scene.NotifyChanged(); UpdateSceneTree(); };
            inspector.Panel1.Controls.Add(sceneTree);
            inspector.Panel2.Controls.Add(propertyGrid);
            split.Panel2.Controls.Add(inspector);

            StatusStrip status = new StatusStrip();
            statusCamera = new ToolStripStatusLabel();
            statusSelection = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleRight };
            status.Items.Add(statusCamera); status.Items.Add(statusSelection);

            Controls.Add(split);
            Controls.Add(status);
            Controls.Add(toolStrip);

            openGLControl.MouseWheel += openGLControl_MouseWheel;
            openGLControl.DoubleClick += openGLControl_DoubleClick;
            openGLControl.TabStop = true;
            Text = "DCad Viewport — OpenGL foundation";
            MinimumSize = new Size(900, 600);
        }

        private void AddButton(string text, EventHandler click)
        {
            ToolStripButton b = new ToolStripButton(text);
            b.DisplayStyle = ToolStripItemDisplayStyle.Text;
            b.Click += click;
            toolStrip.Items.Add(b);
        }

        private void BuildDemoScene()
        {
            SceneObject body = scene.Add(new SceneObject("Main body", MeshFactory.Cube(4.0)));
            body.Y = 2.0; body.Color = Color.FromArgb(183, 196, 214);
            SceneObject feature = scene.Add(new SceneObject("Feature", MeshFactory.Cube(1.6)));
            feature.X = 3.3; feature.Y = 0.8; feature.Z = 1.7; feature.Color = Color.FromArgb(91, 162, 214);
            SceneObject sphere = scene.Add(new SceneObject("Reference sphere", MeshFactory.Sphere(1.25, 24, 12)));
            sphere.X = -3.3; sphere.Y = 1.25; sphere.Z = -1.2; sphere.Color = Color.FromArgb(205, 154, 85);
            camera.Fit(scene.GetBounds());
        }

        private void openGLControl_OpenGLDraw(object sender, RenderEventArgs e)
        {
            renderer.Render(openGLControl.OpenGL, camera, scene, openGLControl.Width, openGLControl.Height);
        }

        private void openGLControl_OpenGLInitialized(object sender, EventArgs e)
        {
            renderer.Initialize(openGLControl.OpenGL);
        }

        private void openGLControl_Resized(object sender, EventArgs e)
        {
            openGLControl.Invalidate();
        }

        private void openGLControl_MouseDown(object sender, MouseEventArgs e)
        {
            openGLControl.Focus();
            lastMouse = mouseDownPoint = e.Location;
            dragButton = e.Button;
            altOrbit = (ModifierKeys & Keys.Alt) == Keys.Alt;
        }

        private void SharpGLForm_MouseMove(object sender, MouseEventArgs e)
        {
            int dx = e.X - lastMouse.X, dy = e.Y - lastMouse.Y;
            if (dragButton == MouseButtons.Right || (dragButton == MouseButtons.Left && altOrbit))
                camera.Orbit(dx, dy);
            else if (dragButton == MouseButtons.Middle || (dragButton == MouseButtons.Left && (ModifierKeys & Keys.Shift) == Keys.Shift))
                camera.Pan(dx, dy, openGLControl.Height);
            lastMouse = e.Location;
            UpdateStatus();
        }

        private void openGLControl_MouseUp(object sender, MouseEventArgs e)
        {
            int dist = Math.Abs(e.X - mouseDownPoint.X) + Math.Abs(e.Y - mouseDownPoint.Y);
            if (e.Button == MouseButtons.Left && dist < 5 && !altOrbit)
                scene.Pick(camera.BuildRay(e.X, e.Y, openGLControl.Width, openGLControl.Height));
            dragButton = MouseButtons.None;
            altOrbit = false;
        }

        private void openGLControl_MouseWheel(object sender, MouseEventArgs e)
        {
            camera.Zoom(e.Delta);
            UpdateStatus();
        }

        private void openGLControl_DoubleClick(object sender, EventArgs e)
        {
            if (scene.Selected != null) camera.Focus(scene.Selected.Position, scene.Selected.WorldBoundingRadius);
            else FitScene();
            UpdateStatus();
        }

        private void openGLControl_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.F: FitScene(); break;
                case Keys.P: ToggleProjection(); break;
                case Keys.G: renderer.Settings.ShowGrid = !renderer.Settings.ShowGrid; break;
                case Keys.L: renderer.Settings.Lighting = !renderer.Settings.Lighting; break;
                case Keys.W: CycleRenderMode(); break;
                case Keys.D1: SetView(StandardView.Front); break;
                case Keys.D2: SetView(StandardView.Back); break;
                case Keys.D3: SetView(StandardView.Left); break;
                case Keys.D4: SetView(StandardView.Right); break;
                case Keys.D5: SetView(StandardView.Top); break;
                case Keys.D6: SetView(StandardView.Bottom); break;
                case Keys.D7: SetView(StandardView.Isometric); break;
                case Keys.Escape: scene.Select(null); break;
                case Keys.A: camera.MoveLocal(-1, 0, 0); break;
                case Keys.D: camera.MoveLocal(1, 0, 0); break;
                case Keys.S: camera.MoveLocal(0, 0, -1); break;
                case Keys.Up: camera.MoveLocal(0, 0, 1); break;
                case Keys.Q: camera.MoveLocal(0, -1, 0); break;
                case Keys.E: camera.MoveLocal(0, 1, 0); break;
            }
            UpdateStatus();
        }

        private void FitScene() { camera.Fit(scene.GetBounds()); UpdateStatus(); }
        private void SetView(StandardView view) { camera.SetView(view); camera.Fit(scene.GetBounds()); UpdateStatus(); }
        private void ToggleProjection()
        {
            camera.Projection = camera.Projection == CameraProjection.Perspective ? CameraProjection.Orthographic : CameraProjection.Perspective;
            UpdateStatus();
        }
        private void CycleRenderMode()
        {
            int count = Enum.GetValues(typeof(ViewportRenderMode)).Length;
            renderer.Settings.Mode = (ViewportRenderMode)(((int)renderer.Settings.Mode + 1) % count);
        }

        private void Scene_SelectionChanged(object sender, EventArgs e)
        {
            propertyGrid.SelectedObject = scene.Selected;
            if (sceneTree != null)
                foreach (TreeNode node in sceneTree.Nodes)
                    if (ReferenceEquals(node.Tag, scene.Selected)) { sceneTree.SelectedNode = node; break; }
            UpdateStatus();
        }
        private void Scene_SceneChanged(object sender, EventArgs e) { UpdateStatus(); }
        private void sceneTree_AfterSelect(object sender, TreeViewEventArgs e) { scene.Select(e.Node == null ? null : e.Node.Tag as SceneObject); }

        private void UpdateSceneTree()
        {
            if (sceneTree == null) return;
            SceneObject selected = scene.Selected;
            sceneTree.BeginUpdate(); sceneTree.Nodes.Clear();
            foreach (SceneObject obj in scene.Objects)
            {
                TreeNode n = new TreeNode(obj.Name);
                n.Tag = obj;
                n.ForeColor = obj.Visible ? SystemColors.ControlText : SystemColors.GrayText;
                sceneTree.Nodes.Add(n);
                if (obj == selected) sceneTree.SelectedNode = n;
            }
            sceneTree.EndUpdate();
        }

        private void UpdateStatus()
        {
            if (statusCamera == null) return;
            statusCamera.Text = string.Format("{0} | {1} | Grid {2} | Light {3}", camera.Projection, renderer.Settings.Mode,
                renderer.Settings.ShowGrid ? "on" : "off", renderer.Settings.Lighting ? "on" : "off");
            statusSelection.Text = scene.Selected == null ? "No selection" : "Selected: " + scene.Selected.Name;
        }
    }
}
