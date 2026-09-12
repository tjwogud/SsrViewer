using OpenTK.Mathematics;
using SsrViewer.Properties;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace SsrViewer
{
    internal partial class SsrWindow
    {
        private class Furniture : Form
        {
            public enum Type
            {
                Chair, Bed
            }

            private readonly Type type;
            private readonly PictureBox pictureBox;

            public Furniture(Type type)
            {
                this.type = type;

                StartPosition = FormStartPosition.CenterScreen;
                Size = new(500, 300);
                FormBorderStyle = FormBorderStyle.None;

                pictureBox = new();
                pictureBox.Dock = DockStyle.Fill;
                pictureBox.Visible = false;
                Controls.Add(pictureBox);

                BackColor = Color.LimeGreen;
                TransparencyKey = Color.LimeGreen;

                switch (type)
                {
                    case Type.Chair:
                        BackgroundImage = Resources.chair;
                        pictureBox.Image = Resources.highlighted_chair;
                        break;
                    case Type.Bed:
                        BackgroundImage = Resources.bed;
                        pictureBox.Image = Resources.highlighted_bed;
                        break;
                }
            }

            public Type GetFType()
            {
                return type;
            }

            public void ToggleHighlight(bool highlight)
            {
                pictureBox.Visible = highlight;
            }

            protected override void OnLoad(EventArgs e)
            {
                base.OnLoad(e);

                TopMost = true;
            }

            public Vector2i GetLocation()
            {
                var pos = Location;
                var size = Size;
                return new(pos.X + size.Width / 2, pos.Y + size.Height / 2);
            }

            private bool drag;
            private Point offset;

            protected override void OnMouseDown(MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left)
                {
                    drag = true;
                    offset = Sub(Location, MousePosition);
                }
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                drag = false;
                if (e.Button == MouseButtons.Right)
                {
                    var menu = new ContextMenuStrip();

                    menu.Items.Add("Delete").Click += (s, e) =>
                    {
                        Close();
                        Dispose();
                        Instance!.GetDownFromFurnitureIfYouCan(this);
                    };

                    var cursorPosition = MousePosition;

                    menu.Show(cursorPosition);
                }
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                if (drag)
                {
                    Location = Add(offset, MousePosition);
                    Instance!.FollowFurnitureIfYouCan(this);
                }
            }

            private static Point Add(Point a, Point b)
            {
                return new(a.X + b.X, a.Y + b.Y);
            }

            private static Point Sub(Point a, Point b)
            {
                return new(a.X - b.X, a.Y - b.Y);
            }
        }
    }
}
