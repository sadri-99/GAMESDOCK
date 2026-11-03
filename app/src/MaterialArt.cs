using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace GamesDock
{
    // Vector overlay clipped to the live pill. Animation clocks exist only during interaction.
    public sealed partial class MaterialArt : FrameworkElement
    {
        ThemeDef _theme;
        Geometry _clip;
        double _length, _center, _half, _marker = -1;
        bool _vertical;
        Geometry _orbitBase;
        Brush _accent, _detail, _dim, _gold, _focusGlow;
        readonly Dictionary<Brush, Dictionary<double, Pen>> _pens = new Dictionary<Brush, Dictionary<double, Pen>>();
        static readonly Brush Blue = Themes.Br("#DD40C7EB"), Orange = Themes.Br("#DDF7A343");
        static readonly Brush Vent = Themes.Br("#66445049"), Seam = Themes.Br("#77514E60");

        public void Update(ThemeDef theme, Geometry clip, double length, double center,
                           double half, bool vertical, double marker)
        {
            bool changed = _theme != theme;
            if (changed || _length != length || _center != center || _half != half) _orbitBase = null;
            if (changed)
            {
                _theme = theme;
                _pens.Clear();
                _accent = Themes.Br(theme.C["accent"]);
                _detail = Themes.BrA(theme.C["accent"], 0.65);
                _dim = Themes.BrA(theme.C["accent"], 0.22);
                _focusGlow = Themes.Glow("#48" + theme.C["accent"].Substring(3));
                var gold = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
                gold.GradientStops.Add(new GradientStop(Colors.Transparent, 0));
                gold.GradientStops.Add(new GradientStop(Themes.Col(theme.C["accent"]), .5));
                gold.GradientStops.Add(new GradientStop(Colors.Transparent, 1)); gold.Freeze(); _gold = gold;
            }
            _clip = clip; _length = length; _center = center; _half = half;
            _vertical = vertical; _marker = marker;
            Width = vertical ? Math.Max(1, center + half) : length;
            Height = vertical ? length : Math.Max(1, center + half);
            Overlay.Width = Width; Overlay.Height = Height;
            if (changed) ResetMotion(true);
            InvalidateVisual();
            Overlay.InvalidateVisual();
        }

        Pen Stroke(Brush brush, double width)
        {
            Dictionary<double, Pen> widths;
            if (!_pens.TryGetValue(brush, out widths)) { widths = new Dictionary<double, Pen>(); _pens.Add(brush, widths); }
            Pen pen;
            if (!widths.TryGetValue(width, out pen)) { pen = new Pen(brush, width); pen.Freeze(); widths.Add(width, pen); }
            return pen;
        }
        static void Line(DrawingContext dc, Pen pen, double x1, double y1, double x2, double y2)
        {
            dc.DrawLine(pen, new Point(x1, y1), new Point(x2, y2));
        }

        protected override void OnRender(DrawingContext dc)
        {
            if (_theme == null || _theme.Material == null || _clip == null || _length < 20) return;
            dc.PushClip(_clip);
            if (_vertical) dc.PushTransform(new MatrixTransform(new Matrix(0, 1, 1, 0, 0, 0)));
            double top = _center - _half, bottom = _center + _half;
            double start = Math.Min(_half + 6, _length * 0.25), end = _length - start;
            DrawBase(dc, start, end, top, bottom);
            DrawSagaMotion(dc, start, end, top, bottom);
            if (_marker >= start && _marker <= end) DrawMarker(dc, _marker, bottom - 3, Stroke(_detail, 1));
            if (_vertical) dc.Pop();
            dc.Pop();
        }

        void DrawBase(DrawingContext dc, double start, double end, double top, double bottom)
        {
            var fine = Stroke(_detail, 1);
            var dim = Stroke(_dim, 1);
            switch (_theme.Material)
            {
                case "industrial":
                    dc.DrawRectangle(_accent, null, new Rect(start, top + 1.5, Math.Min(26, end - start), 2));
                    dc.DrawRectangle(Vent, null, new Rect(start + 30, top + 1.5, 15, 2));
                    for (int i = -1; i <= 1; i++)
                    {
                        dc.DrawRoundedRectangle(Vent, null, new Rect(9, _center + i * 7 - 2, 3, 4), 1, 1);
                        dc.DrawRoundedRectangle(Vent, null, new Rect(_length - 12, _center + i * 7 - 2, 3, 4), 1, 1);
                    }
                    Line(dc, Stroke(Vent, 1), start, bottom - 3, end, bottom - 3);
                    break;
                case "shrine":
                    Line(dc, Stroke(_gold, 1), start, top + 1.5, end, top + 1.5);
                    dc.DrawEllipse(_dim, null, new Point(_length / 2, bottom - 3), 5, 1.5);
                    dc.DrawEllipse(_accent, null, new Point(_length / 2, bottom - 3), 1.2, 1.5);
                    break;
                case "portal":
                    dc.DrawEllipse(null, Stroke(Blue, 2), new Point(8, _center), 1.5, _half * 0.45);
                    dc.DrawEllipse(null, Stroke(Orange, 2), new Point(_length - 8, _center), 1.5, _half * 0.45);
                    Line(dc, Stroke(Vent, 1), start, bottom - 3, end, bottom - 3);
                    break;
                case "memory":
                    Line(dc, Stroke(Seam, 1), 14, bottom - 4, _length - 14, bottom - 4);
                    dc.DrawEllipse(_accent, null, new Point(9, _center - 10), 1.5, 1.5);
                    for (int i = -2; i <= 2; i++)
                        Line(dc, Stroke(Seam, 1), _length - 12, _center + i * 4, _length - 7, _center + i * 4);
                    break;
                case "orbit":
                    if (_orbitBase == null)
                    {
                        var orbit = new StreamGeometry();
                        using (var ctx = orbit.Open())
                        {
                            ctx.BeginFigure(new Point(start, bottom - 5), false, false);
                            ctx.BezierTo(new Point(_length * 0.35, bottom + 1),
                                new Point(_length * 0.65, bottom + 1), new Point(end, bottom - 5), true, false);
                        }
                        orbit.Freeze(); _orbitBase = orbit;
                    }
                    dc.DrawGeometry(null, dim, _orbitBase);
                    dc.DrawEllipse(_accent, null, new Point(_length * 0.68, bottom - 1.8), 1.5, 1.5);
                    break;
                case "terminal":
                    Line(dc, fine, 8, top + 3, 21, top + 3);
                    Line(dc, fine, 8, top + 3, 8, top + 8);
                    Line(dc, fine, _length - 8, bottom - 3, _length - 21, bottom - 3);
                    Line(dc, fine, _length - 8, bottom - 3, _length - 8, bottom - 8);
                    Line(dc, fine, 8, _center - 3, 11, _center);
                    Line(dc, fine, 11, _center, 8, _center + 3);
                    Line(dc, fine, 13, _center + 4, 17, _center + 4);
                    break;
            }
        }

        void DrawMarker(DrawingContext dc, double x, double y, Pen pen)
        {
            if (_theme.Material == "shrine")
            {
                var arc = new StreamGeometry();
                using (var ctx = arc.Open())
                {
                    ctx.BeginFigure(new Point(x - 3, y - 2), false, false);
                    ctx.ArcTo(new Point(x + 3, y - 2), new Size(3, 3), 0, true,
                        SweepDirection.Counterclockwise, true, false);
                }
                arc.Freeze(); dc.DrawGeometry(null, pen, arc);
            }
            else if (_theme.Material == "memory")
                dc.DrawRectangle(_accent, null, new Rect(x - 1.5, y - 1, 3, 2));
            else if (_theme.Material == "portal" || _theme.Material == "terminal")
                Line(dc, pen, x - 5, y, x + 5, y);
            else dc.DrawEllipse(_accent, null, new Point(x, y), 1.5, 1.5);
        }
    }
}
