using SsrViewer.Properties;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
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
                FormBorderStyle = FormBorderStyle.None;
                ShowInTaskbar = false;

                pictureBox = new();
                pictureBox.Dock = DockStyle.Fill;
                pictureBox.Visible = false;
                Controls.Add(pictureBox);

                BackColor = Color.LimeGreen;
                TransparencyKey = Color.LimeGreen;

                UpdateScale();
            }

            private static Bitmap ResizeImage(Bitmap image, Size size)
            {
                var b = new Bitmap(size.Width, size.Height);
                using Graphics g = Graphics.FromImage(b);
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.DrawImage(image, 0, 0, size.Width, size.Height);
                return b;
            }

            public void UpdateScale()
            {
                Size = new(new Point(500, 300) * SsrScale);

                Bitmap image, highlightedImage;

                switch (type)
                {
                    case Type.Chair:
                        image = Resources.chair;
                        highlightedImage = Resources.highlighted_chair;
                        break;
                    case Type.Bed:
                        image = Resources.bed;
                        highlightedImage = Resources.highlighted_bed;
                        break;
                    default:
                        return;
                }

                image = ResizeImage(image, Size);
                highlightedImage = ResizeImage(highlightedImage, Size);

                BackgroundImage = image;
                pictureBox.Image = highlightedImage;
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

            public Point GetCenterLocation()
            {
                return Location + new Point(Size.Width / 2, Size.Height / 2);
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

            protected override void OnFormClosing(FormClosingEventArgs e)
            {
                base.OnFormClosing(e);
                Instance!.GetDownFromFurnitureIfYouCan(this);
                Instance!.DeleteFurniture(this);
            }
        }
    }
}
