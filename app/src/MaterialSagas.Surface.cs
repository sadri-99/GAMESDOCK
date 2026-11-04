using System;
using System.Windows;
using System.Windows.Media;

namespace GamesDock
{
    public sealed partial class MaterialArt
    {
        static readonly Brush CyberPlate = Themes.Br("#D5E5D737"), CyberInk = Themes.Br("#FF202B2D");
        static readonly Brush MonitorGlass = Themes.Br("#5026483A"), MonitorGrid = Themes.Br("#175AAA80");
        static readonly Brush Sunset = Themes.Br("#37F090A4"), Sea = Themes.Br("#2676D9D3");
        static readonly Brush GiOrange = Themes.Br("#75EF9B45"), GiBlue = Themes.Br("#994B8BBF"), RadarGreen = Themes.Br("#FF9DD488");
        static readonly Brush CodecScreen = Themes.Br("#54244335"), CodecGrid = Themes.Br("#148DB18C");
        static readonly Brush GlassViolet = Themes.Br("#313A376E"), GlassBlue = Themes.Br("#31396080");
        static readonly Brush FacetLight = Themes.Br("#2448BECD"), FacetBlue = Themes.Br("#244A80BB");
        static readonly Brush Stone = Themes.Br("#554B3B34"), EmberDark = Themes.Br("#603B1820");
        static readonly Brush CaveCeiling = Themes.Br("#55202D48");
        static readonly Brush Tar = Themes.Br("#7010161B"), Chiral = Themes.Br("#B4E0B377");
        static readonly Brush Cell = Themes.Br("#6033412D"), HealthPixel = Themes.Br("#FFCB7E71"), WaterPixel = Themes.Br("#FF8BBFC4");

        void Polygon(DrawingContext dc, Brush fill, Pen pen, params Point[] points)
        {
            if (points.Length < 2) return;
            var g = new StreamGeometry();
            using (var c = g.Open())
            { c.BeginFigure(points[0], fill != null, fill != null); for (int i = 1; i < points.Length; i++) c.LineTo(points[i], true, false); }
            g.Freeze(); dc.DrawGeometry(fill, pen, g);
        }
        void Curve(DrawingContext dc, Pen pen, Point a, Point b, Point c, Point d)
        {
            var g = new StreamGeometry();
            using (var path = g.Open()) { path.BeginFigure(a, false, false); path.BezierTo(b, c, d, true, false); }
            g.Freeze(); dc.DrawGeometry(null, pen, g);
        }
        void DrawSagaBase(DrawingContext dc, double start, double end, double top, double bottom)
        {
            // Each surface is a separate composition. The only common boundary is the dock clip.
            switch (_theme.Material)
            {
                case "cyberware": SurfaceCyber(dc, top, bottom); break;
                case "biohazard": SurfaceMedical(dc, top, bottom); break;
                case "codec": SurfaceCodec(dc, top, bottom); break;
                case "crystal": SurfaceCrystal(dc, top, bottom); break;
                case "inferno": SurfaceGothic(dc, top, bottom); break;
                case "strand": SurfaceStrand(dc, top, bottom); break;
            }
        }

        void SurfaceCyber(DrawingContext dc, double top, double bottom)
        {
            double x = _length * .32, t = top + 1;
            Polygon(dc, CyberPlate, null, new Point(0, t), new Point(x, t), new Point(x + 8, t + 5), new Point(24, t + 5), new Point(18, t + 11), new Point(0, t + 11));
            Polygon(dc, null, Stroke(SignalCyan, .8), new Point(_length * .58, bottom - 2), new Point(_length - 31, bottom - 2), new Point(_length - 24, bottom - 8), new Point(_length - 7, bottom - 8));
            for (int i = 0; i < 3; i++) Line(dc, Stroke(_detail, 1), 8, _center - 5 + i * 4, 17 - i * 2, _center - 5 + i * 4);
            for (int i = 0; i < 3; i++) Line(dc, Stroke(CyberInk, .8), x - 21 + i * 5, t + 1, x - 18 + i * 5, t + 4);
            Polygon(dc, null, Stroke(_detail, .8), new Point(_length - 15, _center - 8), new Point(_length - 8, _center - 8), new Point(_length - 8, _center + 2), new Point(_length - 12, _center + 6));
        }

        FormattedText _medicalStatus;
        void SurfaceMedical(DrawingContext dc, double top, double bottom)
        {
            if (_length > 150)
            {
                if (_medicalStatus == null) _medicalStatus = new FormattedText("FINE", System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, new Typeface("Consolas"), 7, _detail, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                dc.DrawText(_medicalStatus, new Point(44, top + 1));
            }
            double band = Math.Min(11, _half * .32), y = bottom - band;
            dc.DrawRectangle(MonitorGlass, null, new Rect(19, y, Math.Max(1, _length - 39), band - 1));
            Line(dc, Stroke(_dim, .6), 19, y, _length - 20, y);
            for (double x = 26; x < _length - 24; x += 17) Line(dc, Stroke(MonitorGrid, .5), x, y + 1, x, bottom - 2);
            for (int i = 0; i < 3; i++) dc.DrawRectangle(i == 0 ? BioRed : _dim, null, new Rect(8, _center - 6 + i * 5, 4, 2));
            dc.DrawRoundedRectangle(null, Stroke(_detail, .8), new Rect(_length - 13, _center - 10, 5, 18), 1.5, 1.5);
            dc.DrawRectangle(_detail, null, new Rect(_length - 12, _center + 1, 3, 5));
            if (_length > 150) for (int i = 0; i < 3; i++) Line(dc, Stroke(BioRed, 1), _length - 40 + i * 5, top + 2, _length - 37 + i * 5, top + 5);
        }

        void SurfaceCodec(DrawingContext dc, double top, double bottom)
        {
            dc.DrawRectangle(CodecScreen, null, new Rect(4, top + 4, Math.Max(1, _length - 8), bottom - top - 8));
            dc.DrawRectangle(null, Stroke(_dim, .65), new Rect(4, top + 4, Math.Max(1, _length - 8), bottom - top - 8));
            for (double y = top + 9; y < bottom - 5; y += 7) Line(dc, Stroke(CodecGrid, .5), 6, y, _length - 6, y);
            Line(dc, Stroke(_detail, 1), 12, _center - 10, 12, _center + 10);
            Line(dc, Stroke(_detail, 1), _length - 12, _center - 10, _length - 12, _center + 10);
            if (_length > 150)
            {
                if (_codecFrequency == null) _codecFrequency = new FormattedText("140.85", System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, new Typeface("Consolas"), 8, _detail, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                dc.DrawText(_codecFrequency, new Point(_length / 2 - 15, top + 1));
            }
            for (int i = 0; i < 4; i++) dc.DrawRectangle(_dim, null, new Rect(_length - 25 + i * 3, bottom - 9 - i, 1.5, 3 + i));
        }

        void SurfaceCrystal(DrawingContext dc, double top, double bottom)
        {
            double x = _length * .27;
            Polygon(dc, FacetLight, null, new Point(0, bottom), new Point(x, top), new Point(_length * .49, bottom));
            Polygon(dc, FacetBlue, null, new Point(x, top), new Point(_length * .62, top), new Point(_length * .49, bottom));
            Polygon(dc, FacetLight, null, new Point(_length * .74, top), new Point(_length, top), new Point(_length, bottom));
            Polygon(dc, null, Stroke(_dim, .6), new Point(0, bottom - 2), new Point(x, top + 1), new Point(_length * .49, bottom - 1));
            Glyph(dc, CrystalGlyph, 13, _center, _dim, Stroke(_detail, .8), .85);
            Line(dc, Stroke(Aether, .6), _length - 17, _center - 7, _length - 10, _center + 5);
            Line(dc, Stroke(_detail, .6), _length - 10, _center + 5, _length - 10, _center - 4);
        }

        void SurfaceGothic(DrawingContext dc, double top, double bottom)
        {
            double r = Math.Min(8, _half * .3);
            Polygon(dc, Stone, null, new Point(0, top), new Point(23, top), new Point(16, _center - 9), new Point(20, bottom), new Point(0, bottom));
            Polygon(dc, Stone, null, new Point(_length, top), new Point(_length - 23, top), new Point(_length - 16, _center - 9), new Point(_length - 20, bottom), new Point(_length, bottom));
            dc.DrawEllipse(EmberDark, Stroke(_detail, 1), new Point(11, _center), r, r);
            dc.DrawEllipse(Blood, null, new Point(11, _center + 2), r * .68, r * .58);
            dc.DrawEllipse(VoidInk, Stroke(_detail, 1), new Point(_length - 11, _center), r, r);
            dc.DrawEllipse(Aether, null, new Point(_length - 11, _center + 2), r * .6, r * .55);
            Curve(dc, Stroke(_dim, .8), new Point(25, bottom - 2), new Point(_length * .3, bottom - 9), new Point(_length * .7, bottom - 9), new Point(_length - 25, bottom - 2));
        }

        void SurfaceStrand(DrawingContext dc, double top, double bottom)
        {
            double active = Math.Max(Clamp(InteractionLevel), EventPhase >= 0 ? Envelope(EventPhase) : 0);
            if (active < .999)
            {
                dc.PushOpacity(1 - active);
                DrawSlidingDrops(dc, top, bottom, 0);
                dc.Pop();
            }
            Curve(dc, Stroke(StrandBlue, .6), new Point(22, bottom - 4), new Point(_length * .3, bottom - 1), new Point(_length * .7, bottom - 7), new Point(_length - 22, bottom - 4));
        }

        void Rune(DrawingContext dc, double x, double y, int glyph, Brush brush)
        {
            var p = Stroke(brush, .7);
            Line(dc, p, x, y - 3, x, y + 3);
            if (glyph % 2 == 0) { Line(dc, p, x - 2, y - 2, x + 2, y); Line(dc, p, x + 2, y, x - 2, y + 2); }
            else { Line(dc, p, x - 2, y + 1, x + 2, y - 1); Line(dc, p, x + 2, y - 1, x + 2, y - 3); }
        }
    }
}
