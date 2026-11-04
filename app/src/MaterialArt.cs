using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace GamesDock
{
    // Vector overlay clipped to the live pill. Animation clocks exist only during interaction.
    public sealed partial class MaterialArt : FrameworkElement
    {
        public bool ShowIdentity { get; set; } = true;
        ThemeDef _theme;
        Geometry _clip;
        double _length, _center, _half, _marker = -1;
        bool _vertical;
        Brush _accent, _detail, _dim, _gold, _focusGlow;
        readonly Dictionary<Brush, Dictionary<double, Pen>> _pens = new Dictionary<Brush, Dictionary<double, Pen>>();
        static readonly Brush Blue = Themes.Br("#DD40C7EB"), Orange = Themes.Br("#DDF7A343");
        static readonly Brush Vent = Themes.Br("#66445049"), Seam = Themes.Br("#77514E60");

        public void Update(ThemeDef theme, Geometry clip, double length, double center,
                           double half, bool vertical, double marker)
        {
            bool changed = _theme != theme;
            if (changed)
            {
                _theme = theme;
                _identity = SagaIdentity.For(theme.Material);
                _codecFrequency = null; _medicalStatus = null; _storyLabels.Clear();
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
            if (ShowIdentity) DrawIdentity(dc, top, bottom);
            DrawSagaMotion(dc, start, end, top, bottom);
            if (_marker >= start && _marker <= end) DrawMarker(dc, _marker, bottom - 3, Stroke(_detail, 1));
            if (_vertical) dc.Pop();
            dc.Pop();
        }

        void DrawBase(DrawingContext dc, double start, double end, double top, double bottom)
        {
            if (Themes.SagaHint(_theme.Material) != null) DrawSagaBase(dc, start, end, top, bottom);
            else DrawLegacySurface(dc, start, end, top, bottom);
        }

        void DrawMarker(DrawingContext dc, double x, double y, Pen pen)
        {
            if (_theme.Material == "memory")
                dc.DrawRectangle(_accent, null, new Rect(x - 1.5, y - 1, 3, 2));
            else if (_theme.Material == "portal" || _theme.Material == "terminal")
                Line(dc, pen, x - 5, y, x + 5, y);
            else dc.DrawEllipse(_accent, null, new Point(x, y), 1.5, 1.5);
        }
    }
}
