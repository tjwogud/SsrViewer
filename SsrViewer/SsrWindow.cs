using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Spine;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Media;
using System.Threading;
using System.Windows.Forms;

namespace SsrViewer
{
    internal partial class SsrWindow : GameWindow
    {
        private readonly string skelPath, atlasPath;

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

        private static List<Furniture> chairList = [];

        public static SsrWindow? Instance { get; private set; }

        internal SsrWindow(string skelPath, string atlasPath, string? voiceDir = null) : base(
            new GameWindowSettings() {
                UpdateFrequency = 60
            },
            new NativeWindowSettings()
            {
                Title = "Ssr Viewer",
                ClientSize = (1600, 1200),

                API = ContextAPI.OpenGL,
                APIVersion = new(3, 3),
                Profile = ContextProfile.Core,

                AlphaBits = 8,

                WindowBorder = WindowBorder.Hidden,
                StartVisible = false
            })
        {
            this.skelPath = skelPath;
            this.atlasPath = atlasPath;

            if (voiceDir != null)
                LoadVoices(voiceDir);

            Instance = this;
        }

        protected override void OnLoad()
        {
            base.OnLoad();

            GL.ClearColor(0, 0, 0, 0);

            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);

            CreateRenderer();
            CreateBuffers();
            LoadSkeleton();

            CreateFramebuffer(Size.X, Size.Y);

            GL.Viewport(0, 0, Size.X, Size.Y);

            projection = Matrix4.CreateOrthographicOffCenter(
                -Size.X / 2f, Size.X / 2f,
                -Size.Y * 2 / 10f, Size.Y * 8 / 10f,
                -1, 1
            );

            CreateWhiteTexture();

            unsafe
            {
                IntPtr hwnd = GLFW.GetWin32Window(WindowPtr);

                var style = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
                SetWindowLongPtr(hwnd, GWL_EXSTYLE, style | WS_EX_LAYERED);

                SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE);
            }

            OnUpdateFrame(default);
            OnRenderFrame(default);
            IsVisible = true;

            if (voices.TryGetValue("Greet", out var player))
                player.Play();
        }

        protected override void OnUpdateFrame(FrameEventArgs args)
        {
            animationState.Update((float)args.Time);
            animationState.Apply(skeleton);

            skeleton.UpdateWorldTransform();
        }

        private bool drag;
        private Vector2i offset;
        private Vector2i prevMouse;
        private bool moving;
        private Stopwatch stopwatch = new();

        private const float specialGuageTime = 0.25f;
        private const float specialTime = 1;

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            if (e.Button == MouseButton.Left)
            {
                if (!animationState.GetCurrent(0).Loop)
                    return;
                GetDownFromFurnitureIfYouCan();
                drag = true;
                offset = Location - GetAbsMousePosition();
                prevMouse = GetAbsMousePosition();
                moving = false;
                stopwatch.Restart();
            }
        }

        private void FollowFurnitureIfYouCan(Furniture furniture)
        {
            if (selected != furniture) return;

            var pos = selected.GetLocation();
            Location = pos - new Vector2i(Size.X / 2, Size.Y * 4 / 5);
        }

        private void GetDownFromFurnitureIfYouCan(Furniture? furniture = null)
        {
            if (furniture != null && selected != furniture) return;
            var cur = animationState.GetCurrent(0).Animation.Name;
            if (cur == "Sit" || cur == "Sleep")
            {
                animationState.SetAnimation(0, "Relax", true);
                selected = null;
                unsafe
                {
                    IntPtr hwnd = GLFW.GetWin32Window(WindowPtr);
                    SetWindowLongPtr(hwnd, GWLP_HWNDPARENT, 0);
                }
            }
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            if (e.Button == MouseButton.Left)
            {
                var elapsed = stopwatch.Elapsed.TotalSeconds;
                stopwatch.Reset();
                if (!drag) return;
                drag = false;

                if (selected != null)
                {
                    unsafe
                    {
                        IntPtr hwnd = GLFW.GetWin32Window(WindowPtr);
                        SetWindowLongPtr(hwnd, GWLP_HWNDPARENT, selected.Handle);
                    }
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

                foreach (Furniture chair in chairList)
                {
                    chair.ToggleHighlight(false);
                }
            }
            else if (e.Button == MouseButton.Right)
            {
                var menu = new ContextMenuStrip();

                menu.Items.Add("Change Spine").Click += (s, e) =>
                {
                    var dialog = new OpenFileDialog { Filter = "Png|*.png" };
                    var result = dialog.ShowDialog();
                    if (result != DialogResult.OK) return;
                    var file = dialog.FileName;
                    if (Path.HasExtension(file))
                        file = file[..^Path.GetExtension(file).Length];

                    Program.OpenSsrWindow(file, Location);
                    Close();
                };

                menu.Items.Add(new ToolStripSeparator());

                void SpawnFurniture(Furniture.Type type)
                {
                    var thread = new Thread(() =>
                    {
                        var chair = new Furniture(type);
                        lock (chairList)
                            chairList.Add(chair);
                        chair.ShowDialog();
                    });
                    thread.SetApartmentState(ApartmentState.STA);
                    thread.IsBackground = true;
                    thread.Start();
                }

                menu.Items.Add("Spawn Chair").Click += (s, e) => SpawnFurniture(Furniture.Type.Chair);

                menu.Items.Add("Spawn Bed").Click += (s, e) => SpawnFurniture(Furniture.Type.Bed);

                menu.Items.Add(new ToolStripSeparator());

                menu.Items.Add("Exit").Click += (s, e) => Close();

                var cursorPosition = Control.MousePosition;

                menu.Show(cursorPosition);
            }
        }

        private Furniture? selected;

        protected override void OnMouseMove(MouseMoveEventArgs e)
        {
            if (drag)
            {
                if ((prevMouse - GetAbsMousePosition()).EuclideanLength > 5)
                    moving = true;

                if (!moving) return;

                Location = offset + GetAbsMousePosition();
                if (animationState.GetCurrent(0).Animation.Name != "Move")
                    animationState.SetAnimation(0, "Move", true);

                selected = chairList.MinBy(f => (GetAbsMousePosition() - f.GetLocation()).EuclideanLength);
                if (selected != null && (GetAbsMousePosition() - selected.GetLocation()).EuclideanLength > 50)
                    selected = null;
                foreach (Furniture cur in chairList)
                {
                    cur.ToggleHighlight(cur == selected);
                }
            }
        }

        private Vector2i GetAbsMousePosition()
        {
            Vector2i mouse = new((int)MousePosition.X, (int)MousePosition.Y);
            return mouse + Location;
        }
    }
}
