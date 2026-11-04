using System;
using System.Windows;
using System.Windows.Media;

namespace GamesDock
{
    // Shared immutable glyphs and drawing primitives; compositions live in Surface and Motion.
    // All motion uses the existing interaction/event clocks: no new timer or idle loop.
    public sealed partial class MaterialArt
    {
        static readonly Brush VoidInk = Themes.Br("#FF142038");
        static readonly Brush SignalCyan = Themes.Br("#FF61E5EB"), BioRed = Themes.Br("#FFE46A68");
        static readonly Brush Pearl = Themes.Br("#FFF1F3E9");
        static readonly Brush ViceTeal = Themes.Br("#FF83DDCF"), KiBlue = Themes.Br("#FF6BCBEF");
        static readonly Brush HeartSilver = Themes.Br("#FFC8D8F3"), Aether = Themes.Br("#FF83B8EE");
        static readonly Brush Blood = Themes.Br("#FFD96366");
        static readonly Brush StrandBlue = Themes.Br("#FF89C8D8"), VoxelOrange = Themes.Br("#FFE3AD71");
        FormattedText _codecFrequency;
        static readonly Geometry FiveStar = Frozen("M 0,-1 L .225,-.31 .951,-.309 .364,.118 .588,.809 0,.382 -.588,.809 -.364,.118 -.951,-.309 -.225,-.31 Z");
        static readonly Geometry HeartGlyph = Frozen("M 0,5 C -2,3 -6,0 -6,-3 C -6,-7 -1,-7 0,-3 C 1,-7 6,-7 6,-3 C 6,0 2,3 0,5 Z");
        static readonly Geometry CrownGlyph = Frozen("M -6,3 L -7,-4 -3,-1 0,-6 3,-1 7,-4 6,3 Z");
        static readonly Geometry CrystalGlyph = Frozen("M 0,-10 L 5,-3 3,6 0,10 -4,4 -5,-3 Z M 0,-10 L -1,0 0,10 M -5,-3 L -1,0 5,-3");
        static Geometry Frozen(string data) { var g = Geometry.Parse(data); g.Freeze(); return g; }

        void Glyph(DrawingContext dc, Geometry glyph, double x, double y, Brush fill, Pen pen, double scale = 1)
        {
            dc.PushTransform(new MatrixTransform(new Matrix(scale, 0, 0, scale, x, y)));
            dc.DrawGeometry(fill, pen, glyph); dc.Pop();
        }
        void Star(DrawingContext dc, double x, double y, double radius, Brush brush)
        {
            Line(dc, Stroke(brush, .9), x - radius, y, x + radius, y);
            Line(dc, Stroke(brush, .9), x, y - radius, x, y + radius);
            dc.DrawEllipse(brush, null, new Point(x, y), .7, .7);
        }
        void Palm(DrawingContext dc, double x, double y, double size, double sway)
        {
            var pen = Stroke(ViceTeal, .85);
            Line(dc, pen, x, y + size, x + 1 + sway, y);
            for (int i = -2; i <= 2; i++)
            {
                double dx = i * size * .48;
                Line(dc, pen, x + 1 + sway, y, x + dx + sway, y - size * .35 + Math.Abs(i) * size * .16);
                Line(dc, pen, x + dx + sway, y - size * .35 + Math.Abs(i) * size * .16, x + dx * 1.2 + sway, y + size * .12);
            }
        }
    }
}
