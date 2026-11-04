using System;
using System.Windows;
using System.Windows.Media;

namespace GamesDock
{
    public sealed partial class MaterialArt
    {
        static readonly Brush TrackerScreen = Themes.Br("#FF233E32"), LabGrid = Themes.Br("#205A7782");
        static readonly Brush PsGreen = Themes.Br("#FF69C4A5"), PsRed = Themes.Br("#FFEE767D"), PsBlue = Themes.Br("#FF80A8E5"), PsPink = Themes.Br("#FFDDA2D5");
        static readonly Brush Omni = Themes.Br("#FFF1A95F"), ArmorRed = Themes.Br("#99472E35"), PlateGray = Themes.Br("#55767B79");
        void DrawLegacySurface(DrawingContext dc, double start, double end, double top, double bottom)
        {
            switch (_theme.Material)
            {
                case "industrial":
                    dc.DrawRectangle(PlateGray, null, new Rect(39, top + 2, Math.Max(1, _length - 43), 5));
                    for (int i = 0; i < 4; i++) Line(dc, Stroke(Vent, 1), 44 + i * 6, top + 2, 47 + i * 6, top + 7);
                    for (int i = 0; i < 3; i++) Line(dc, Stroke(Radar, .65), _length - 7, _center - 4 + i * 4, _length - 4, _center - 4 + i * 4);
                    for (double x = start; x < end; x += 36) dc.DrawRectangle(Vent, null, new Rect(x, bottom - 6, 15, 2));
                    break;
                case "portal":
                    for (double x = 42; x < _length - 14; x += 61)
                    { Line(dc, Stroke(LabGrid, .6), x, top + 2, x, bottom - 2); }
                    dc.DrawEllipse(null, Stroke(Orange, 2), new Point(_length - 5, _center), 1.5, Math.Max(5, _half * .3));
                    for (int i = 0; i < 3; i++) dc.DrawEllipse(Blue, null, new Point(43 + i * 7, bottom - 5), 1.3, 1.3);
                    break;
                case "memory":
                    dc.DrawRoundedRectangle(Seam, null, new Rect(40, bottom - 10, Math.Max(1, _length - 51), 8), 2, 2);
                    for (double x = 46; x < _length - 16; x += 11) dc.DrawRectangle(_dim, null, new Rect(x, bottom - 8, 6, 4));
                    for (int i = 0; i < 4; i++) PadGlyph(dc, i, end - 30 + i * 8, bottom - 5, 1.6);
                    dc.DrawEllipse(PsGreen, null, new Point(41, top + 5), 1.8, 1.8);
                    break;
                case "orbit":
                    Polygon(dc, ArmorRed, null, new Point(0, bottom - 7), new Point(_length - 45, bottom - 7), new Point(_length - 36, bottom - 14), new Point(_length, bottom - 14), new Point(_length, bottom), new Point(0, bottom));
                    // Relay fork is engraved into the existing end clearance, not a side bay.
                    Polygon(dc, null, Stroke(MassField, .8), new Point(_length - 3, _center - 8), new Point(_length - 8, _center - 5), new Point(_length - 8, _center + 5), new Point(_length - 3, _center + 8));
                    Line(dc, Stroke(_dim, .7), 42, top + 2, _length - 40, top + 2);
                    break;
                case "terminal":
                    for (int i = 0; i < 4; i++)
                    { dc.PushOpacity(.18 + i * .03); CodeGlyph(dc, i, _length - 20 + i % 2 * 6, _center - 9 + i / 2 * 9, Phosphor, 1.4); dc.Pop(); }
                    for (double x = 43; x < _length - 25; x += 29) Line(dc, Stroke(_dim, .45), x, top + 2, x, top + 6);
                    break;
            }
        }

        void DrawLegacyMotion(DrawingContext dc, double start, double end, double top, double bottom, double pos, double phase, bool launch)
        {
            switch (_theme.Material)
            {
                case "industrial":
                    double a = phase * Math.PI * 2, x = pos, trackerY = bottom - 5;
                    for (int i = 0; i < 8; i++)
                    {
                        dc.PushOpacity((1 - i / 8.0) * .65);
                        Line(dc, Stroke(Radar, .8), x, trackerY, x + Math.Cos(a - i * .1) * 10, trackerY + Math.Sin(a - i * .1) * 3); dc.Pop();
                    }
                    dc.DrawEllipse(Radar, null, new Point(x - 4, trackerY + 1), .9, .9);
                    for (int i = 0; i < 5; i++) dc.DrawRectangle(i <= (int)(phase * 5) ? Radar : Vent, null, new Rect(start + i * 8, top + 3, 5, 2));
                    if (launch) Line(dc, Stroke(Radar, 1.1), pos - 20, bottom - 4, pos + 20, bottom - 4);
                    break;
                case "portal":
                    // A companion cube actually enters one mouth and comes out of the other.
                    double t = Unit(phase), cx = t < .45 ? Between(start, end - 12, t / .45) : Between(43, pos, (t - .45) / .55);
                    double drop = t < .45 ? Math.Sin(t / .45 * Math.PI) * 7 : Math.Sin((t - .45) / .55 * Math.PI) * 5;
                    Cube(dc, cx, bottom - 9 - drop, 5, phase * .3);
                    double mouth = 3 + Math.Sin(phase * Math.PI) * 2;
                    dc.DrawEllipse(null, Stroke(Blue, 1.4), new Point(44, _center), mouth, Math.Max(8, _half * .6));
                    dc.DrawEllipse(null, Stroke(Orange, 1.4), new Point(_length - 14, _center), mouth, Math.Max(8, _half * .6));
                    break;
                case "memory":
                    for (int i = 0; i < 4; i++)
                    { dc.PushOpacity(phase * 4 > i ? 1 : .12); PadGlyph(dc, i, pos - 19 + i * 13, top + 5, 2.8); dc.Pop(); }
                    int cells = Math.Max(1, (int)((end - start) / 11));
                    for (int i = 0; i < cells; i++)
                        if (phase > i / (double)cells) dc.DrawRectangle(i % 4 == 0 ? PsGreen : _accent, null, new Rect(start + i * 11, bottom - 8, 6, 3));
                    if (launch) DrawStoryLabel(dc, "MEMORY WRITE", pos, bottom - 19, _accent, 6.5, phase);
                    break;
                case "orbit":
                    double gate = _length - 22;
                    for (int i = 0; i < 3; i++)
                    { dc.PushOpacity(.65 - i * .18); Line(dc, Stroke(MassField, 1), gate - 3 - i, _center - 9, gate - 2 + i, _center + 9); dc.Pop(); }
                    double ship = Between(start, end - 5, phase);
                    Polygon(dc, null, Stroke(MassField, .9), new Point(ship - 8, bottom - 8), new Point(ship, bottom - 11), new Point(ship + 8, bottom - 8), new Point(ship - 1, bottom - 6));
                    if (launch) Line(dc, Stroke(MassField, 1.2), ship - 20, bottom - 8, ship - 8, bottom - 8);
                    break;
                case "terminal":
                    int columns = Math.Max(4, Math.Min(25, (int)(_length / 28)));
                    dc.PushClip(new RectangleGeometry(new Rect(40, top + 1, Math.Max(1, _length - 67), Math.Max(1, bottom - top - 6))));
                    for (int col = 0; col < columns; col++)
                    {
                        double xx = Between(44, _length - 29, (col + .5) / columns);
                        double head = top + Unit(phase * (col % 2 == 0 ? 1 : 1.7) + col * .271) * (bottom - top + 30);
                        var placement = new TranslateTransform(xx, head); placement.Freeze();
                        dc.PushTransform(placement);
                        dc.DrawDrawing(CodeTrail((col + (int)(phase * 19)) % 4));
                        dc.Pop();
                    }
                    dc.Pop();
                    break;
            }
        }

        void DrawLegacyFocus(DrawingContext dc, Rect r, double phase, double radius)
        {
            double cx = r.Left + r.Width / 2, cy = r.Top + r.Height / 2;
            switch (_theme.Material)
            {
                case "industrial":
                    // Range marks and a local body scan, aligned with the tracker sweep.
                    for (int i = 0; i < 4; i++)
                    { double y = r.Top + 5 + i * (r.Height - 10) / 3; Line(dc, Stroke(Radar, 1), r.Left - 1, y, r.Left + (i % 2 == 0 ? 5 : 2), y); }
                    double scan = r.Top + phase * r.Height;
                    dc.PushOpacity(.3); Line(dc, Stroke(Radar, 1), r.Left + 3, scan, r.Right - 3, scan); dc.Pop();
                    break;
                case "portal":
                    dc.DrawEllipse(null, Stroke(Blue, 1.6), new Point(r.Left + 1, cy), 3, r.Height * .48);
                    dc.DrawEllipse(null, Stroke(Orange, 1.6), new Point(r.Right - 1, cy), 3, r.Height * .48);
                    break;
                case "memory":
                    dc.DrawRoundedRectangle(null, Stroke(Seam, .8), new Rect(r.Left + 4, r.Bottom - 2, r.Width - 8, 5), 1, 1);
                    break;
                case "orbit":
                    // Omni-tool geometry, not another pair of orbiting arcs.
                    dc.PushOpacity(.6);
                    Polygon(dc, null, Stroke(Omni, .9), new Point(r.Left, cy - 11), new Point(r.Left - 3, cy - 3), new Point(r.Left - 3, cy + 10), new Point(r.Left + 8, cy + 18), new Point(r.Left + 18, cy + 17));
                    for (int i = 0; i < 3; i++) Line(dc, Stroke(Omni, .9), r.Left + 3 + i * 4, r.Bottom - 2, r.Left + 3 + i * 4, r.Bottom + 2); dc.Pop();
                    break;
                case "terminal":
                    double yRow = r.Top + Math.Floor(phase * r.Height / 4) * 4;
                    dc.PushOpacity(.16); dc.DrawRectangle(Phosphor, null, new Rect(r.Left, yRow, r.Width, 2)); dc.Pop();
                    CodeGlyph(dc, (int)(phase * 8) % 4, r.Right - 3, r.Bottom - 6, Head, .85);
                    break;
            }
        }

        void PadGlyph(DrawingContext dc, int glyph, double x, double y, double radius)
        {
            Brush b = glyph == 0 ? PsGreen : glyph == 1 ? PsRed : glyph == 2 ? PsBlue : PsPink;
            var p = Stroke(b, .9);
            if (glyph == 0) Polygon(dc, null, p, new Point(x, y - radius), new Point(x + radius, y + radius), new Point(x - radius, y + radius), new Point(x, y - radius));
            if (glyph == 1) dc.DrawEllipse(null, p, new Point(x, y), radius, radius);
            if (glyph == 2) { Line(dc, p, x - radius, y - radius, x + radius, y + radius); Line(dc, p, x + radius, y - radius, x - radius, y + radius); }
            if (glyph == 3) dc.DrawRectangle(null, p, new Rect(x - radius, y - radius, radius * 2, radius * 2));
        }
        void Cube(DrawingContext dc, double x, double y, double size, double rotation)
        {
            dc.PushTransform(new TranslateTransform(x, y)); dc.PushTransform(new RotateTransform(rotation * 90));
            dc.DrawRoundedRectangle(HeartSilver, Stroke(Seam, .8), new Rect(-size, -size, size * 2, size * 2), 1, 1);
            Glyph(dc, HeartGlyph, 0, 0, PsPink, null, size * .09); dc.Pop(); dc.Pop();
        }
        static readonly Geometry[] MatrixGlyphs =
        {
            Frozen("M 0,0 L 5,0 3,3 1,2 4,5"),
            Frozen("M 1,0 L 1,5 M 1,2 L 5,3"),
            Frozen("M 0,0 L 0,3 M 4,0 L 4,3 2,5"),
            Frozen("M 0,1 L 5,1 4,5 3,5 M 3,0 L 2,4")
        };
        static DrawingGroup[] _codeTrails;
        static readonly object CodeTrailGate = new object();
        static DrawingGroup CodeTrail(int variant)
        {
            lock (CodeTrailGate)
            {
                if (_codeTrails == null)
                {
                    var trails = new DrawingGroup[4];
                    for (int v = 0; v < 4; v++)
                    {
                        var drawing = new DrawingGroup();
                        using (var context = drawing.Open())
                        for (int row = 0; row < 6; row++)
                        {
                            var transform = new MatrixTransform(new Matrix(.8, 0, 0, .8, 0, -row * 6)); transform.Freeze();
                            var pen = new Pen(row == 0 ? Head : Phosphor, .8); pen.Freeze();
                            context.PushOpacity(row == 0 ? .95 : .55 * (1 - row / 6.0));
                            context.PushTransform(transform); context.DrawGeometry(null, pen, MatrixGlyphs[(v + row) % 4]);
                            context.Pop(); context.Pop();
                        }
                        drawing.Freeze(); trails[v] = drawing;
                    }
                    _codeTrails = trails;
                }
                return _codeTrails[variant];
            }
        }
        void CodeGlyph(DrawingContext dc, int glyph, double x, double y, Brush brush, double scale)
        {
            var placement = new MatrixTransform(new Matrix(scale, 0, 0, scale, x, y)); placement.Freeze();
            dc.PushTransform(placement);
            dc.DrawGeometry(null, Stroke(brush, .8), MatrixGlyphs[glyph % 4]);
            dc.Pop();
        }
    }
}
