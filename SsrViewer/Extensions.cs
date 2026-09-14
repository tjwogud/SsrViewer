using System;
using System.Drawing;

namespace SsrViewer
{
    internal static class Extensions
    {
        extension(Point instance)
        {
            public static Point operator +(Point a, Point b)
            {
                return new(a.X + b.X, a.Y + b.Y);
            }

            public static Point operator -(Point a, Point b)
            {
                return new(a.X - b.X, a.Y - b.Y);
            }

            public static Point operator *(Point p, float s)
            {
                return new((int)(p.X * s), (int)(p.Y * s));
            }

            public static Point operator /(Point p, float s)
            {
                return new((int)(p.X / s), (int)(p.Y / s));
            }

            public int SqrLength => instance.X * instance.X + instance.Y * instance.Y;
            public float Length => MathF.Sqrt(instance.SqrLength);
        }
    }
}
