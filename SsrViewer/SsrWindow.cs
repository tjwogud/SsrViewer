using OpenTK.GLControl;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using Spine;
using SsrViewer.Properties;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Media;
using System.Threading;
using System.Windows.Forms;

namespace SsrViewer
{
    internal partial class SsrWindow : Form
    {
        private readonly string skelPath;
        private readonly string atlasPath;
        private string? voiceDir;

        private Skeleton skeleton = null!;
        private AnimationState animationState = null!;

        private int shaderProgram;
        private int vao, vbo;
        private int ebo;

        private int projectionLocation;
        private int textureLocation;

        private Matrix4 projection;

        private int fbo;
        private int fboTexture;
        private int fboDepth;

        private bool specialAnimAvailable;

        private int whiteTexture;

        private Dictionary<string, SoundPlayer> voices = [];

        private static List<Furniture> furnitures = [];

        public static SsrWindow? Instance { get; private set; }

        public static float SsrScale { get; private set; } = 1;
        public static int ScaledWidth => (int)(1600 * SsrScale);
        public static int ScaledHeight => (int)(1000 * SsrScale);
        public static Point CenterOffset => new(ScaledWidth / 2, ScaledHeight * 4 / 5);

        private System.Windows.Forms.Timer timer = null!;
        private DateTime lastUpdate;

        private GLControl glControl;
        private Form glForm;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_TOPMOST;
                cp.ExStyle |= WS_EX_LAYERED;
                cp.ExStyle |= WS_EX_TOOLWINDOW;
                cp.ExStyle &= ~WS_EX_APPWINDOW;
                return cp;
            }
        }

        internal SsrWindow(string skelPath, string atlasPath, string? voiceDir = null)
        {
            Text = "Ssr Viewer";
            ClientSize = new(ScaledWidth, ScaledHeight);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;

            glControl = new GLControl
            {
                API = ContextAPI.OpenGL,
                APIVersion = new(3, 3, 0),
                Profile = ContextProfile.Core,
                Dock = DockStyle.Fill
            };
            glControl.Load += GLControl_Load;

            glForm = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                ShowInTaskbar = false,

                Location = new Point(-20_000, -20_000),
                Size = new Size(1, 1)
            };

            glForm.Controls.Add(glControl);
            glForm.ShowAsync();

            this.skelPath = skelPath;
            this.atlasPath = atlasPath;
            this.voiceDir = voiceDir;

            LoadVoices();

            Instance = this;
        }

        private void GLControl_Load(object? sender, EventArgs e)
        {
            glControl.MakeCurrent();

            GL.ClearColor(0, 0, 0, 0);

            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);

            CreateRenderer();
            CreateBuffers();
            LoadSkeleton();

            ChangeScale(SsrScale, false);

            CreateWhiteTexture();

            timer = new();
            timer.Interval = 1000 / 60;
            timer.Tick += (s, e) =>
            {
                var now = DateTime.UtcNow;
                var deltaTime = (now - lastUpdate).TotalSeconds;
                lastUpdate = now;
                animationState.Update((float)deltaTime);
                animationState.Apply(skeleton);

                skeleton.UpdateWorldTransform();

                RenderFrame();
            };
            timer.Start();
            lastUpdate = DateTime.UtcNow;

            if (voices.TryGetValue("Greet", out var player))
                player.Play();
        }

        private bool drag;
        private Point offset;
        private Point prevMouse;
        private bool moving;
        private Stopwatch specialStopwatch = new();

        private const float specialGuageTime = 0.25f;
        private const float specialTime = 1;

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (!animationState.GetCurrent(0).Loop)
                    return;
                GetDownFromFurnitureIfYouCan();
                drag = true;
                offset = Location - MousePosition;
                prevMouse = MousePosition;
                moving = false;
                specialStopwatch.Restart();
            }
        }

        private void FollowFurnitureIfYouCan(Furniture? furniture)
        {
            if (furniture == null || selected != furniture) return;

            var pos = selected.GetCenterLocation();
            Location = pos - CenterOffset;
        }

        private void GetDownFromFurnitureIfYouCan(Furniture? furniture = null)
        {
            if (furniture != null && selected != furniture) return;
            var cur = animationState.GetCurrent(0).Animation.Name;
            if (cur == "Sit" || cur == "Sleep")
            {
                animationState.SetAnimation(0, "Relax", true);
                selected = null;
                Owner = null;
            }
        }

        private void DeleteFurniture(Furniture furniture)
        {
            furnitures.Remove(furniture);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                var elapsed = specialStopwatch.Elapsed.TotalSeconds;
                specialStopwatch.Reset();
                if (!drag) return;
                drag = false;

                if (selected != null)
                {
                    Owner = selected;
                    FollowFurnitureIfYouCan(selected);
                    switch (selected.GetFType())
                    {
                        case Furniture.Type.Chair:
                            animationState.SetAnimation(0, "Sit", true);
                            break;
                        case Furniture.Type.Bed:
                            animationState.SetAnimation(0, "Sleep", true);
                            break;
                    }
                }
                else if (!moving)
                {
                    if (elapsed >= specialTime)
                    {
                        animationState.SetAnimation(0, specialAnimAvailable ? "Special" : "Interact", false);

                        if (voices.TryGetValue("Special", out var player))
                            player.Play();
                    }
                    else
                    {
                        animationState.SetAnimation(0, "Interact", false);

                        if (voices.TryGetValue("Interact", out var player))
                            player.Play();
                    }
                    animationState.AddAnimation(0, "Relax", true, 0);
                }
                else
                {
                    animationState.SetAnimation(0, "Relax", true);
                }

                foreach (Furniture chair in furnitures)
                {
                    chair.ToggleHighlight(false);
                }
            }
            else if (e.Button == MouseButtons.Right)
            {
                var menu = new ContextMenuStrip();

                menu.Items.Add("Change Spine").Click += (s, e) =>
                {
                    var result = Program.SelectSpine();
                    if (result == null) return;
                    Program.OpenSsrWindow(result, Location);
                    Close();
                };

                menu.Items.Add("Import Voices").Click += (s, e) =>
                {
                    var dialog = new FolderBrowserDialog();
                    var result = dialog.ShowDialog();
                    if (result != DialogResult.OK) return;
                    voiceDir = dialog.SelectedPath;
                    LoadVoices();
                    if (voices.TryGetValue("Greet", out var player))
                        player.Play();
                };

                menu.Items.Add(new ToolStripSeparator());

                void SpawnFurniture(Furniture.Type type)
                {
                    var thread = new Thread(() =>
                    {
                        var chair = new Furniture(type);
                        lock (furnitures)
                            furnitures.Add(chair);
                        chair.ShowDialog();
                    });
                    thread.SetApartmentState(ApartmentState.STA);
                    thread.IsBackground = true;
                    thread.Start();
                }

                var scaleDropdown = new ToolStripMenuItem("Change Scale");

                for (int i = 25; i <= 150; i += 25)
                {
                    int captured = i;
                    var item = scaleDropdown.DropDownItems.Add($"{i}%");
                    item.Click += (s, e) => ChangeScale(captured / 100f);
                    if (i == SsrScale * 100)
                    {
                        item.Image = Resources.check;
                    }
                }

                var custom = new ToolStripTextBox("Custom");
                custom.KeyDown += (s, e) =>
                {
                    if (e.KeyCode != System.Windows.Forms.Keys.Enter) return;
                    if (!int.TryParse(custom.Text, out int i) || i <= 0) return;
                    ChangeScale(i / 100f);
                };
                custom.TextBox.BorderStyle = BorderStyle.FixedSingle;
                scaleDropdown.DropDownItems.Add(custom);

                menu.Items.Add(scaleDropdown);

                menu.Items.Add(new ToolStripSeparator());

                menu.Items.Add("Spawn Chair").Click += (s, e) => SpawnFurniture(Furniture.Type.Chair);

                menu.Items.Add("Spawn Bed").Click += (s, e) => SpawnFurniture(Furniture.Type.Bed);

                menu.Items.Add(new ToolStripSeparator());

                menu.Items.Add("Exit").Click += (s, e) => Close();

                menu.Show(MousePosition);
            }
        }

        private void ChangeScale(float scale, bool updateLocation = true)
        {
            if (updateLocation)
            {
                var prevCenter = Location + CenterOffset;
                SsrScale = scale;
                Location = prevCenter - CenterOffset;
            }

            CreateFramebuffer(ScaledWidth, ScaledHeight);

            GL.Viewport(0, 0, ScaledWidth, ScaledHeight);

            projection = Matrix4.CreateOrthographicOffCenter(
                -CenterOffset.X, CenterOffset.X,
                CenterOffset.Y, -(ScaledHeight - CenterOffset.Y),
                -1, 1
            );

            skeleton.ScaleX = SsrScale;
            skeleton.ScaleY = SsrScale;
            skeleton.UpdateWorldTransform();

            foreach (var furniture in furnitures)
            {
                furniture.UpdateScale();
            }

            FollowFurnitureIfYouCan(selected);
        }

        private Furniture? selected;

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (drag)
            {
                if ((prevMouse - MousePosition).Length > 5)
                    moving = true;

                if (!moving) return;

                Location = offset + MousePosition;
                if (animationState.GetCurrent(0).Animation.Name != "Move")
                    animationState.SetAnimation(0, "Move", true);

                selected = furnitures.MinBy(f => (MousePosition - f.GetCenterLocation()).Length);
                if (selected != null && (MousePosition  - selected.GetCenterLocation()).Length > 50)
                    selected = null;
                foreach (Furniture cur in furnitures)
                {
                    cur.ToggleHighlight(cur == selected);
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            glForm.Close();
            timer.Stop();
        }
    }
}
