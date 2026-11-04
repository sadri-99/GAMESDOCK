using System;
using System.Windows;
using System.Windows.Media;

namespace GamesDock
{
    public sealed partial class MaterialArt
    {
        static readonly Brush SoulLight = Themes.Glow("#90DCEBFF");
        static readonly Brush ScanShade = Themes.Br("#1022E9DE");
        void DrawNewSagaMotion(DrawingContext dc, double start, double end, double top, double bottom, double pos, double phase, bool triggered)
        {
            double span = Math.Max(1, end - start), wave = phase * Math.PI * 2;
            switch (_theme.Material)
            {
                case "cyberware":
                    // Segmented upload: short packets follow an interrupted, angular circuit.
                    for (int i = 0; i < 6; i++)
                    {
                        double x = start + Unit(phase * 1.5 + i * .161) * span;
                        double y = bottom - (x > _length * .58 ? 3 : 7);
                        dc.PushOpacity(i % 3 == 0 ? 1 : .38);
                        Line(dc, Stroke(i % 2 == 0 ? SignalCyan : _accent, 1.5), x, y, x + 7, y); dc.Pop();
                    }
                    if (triggered && phase > .19 && phase < .3)
                    { Line(dc, Stroke(SignalCyan, 1), pos - 24, top + 10, pos - 3, top + 10); Line(dc, Stroke(_accent, 1), pos + 5, bottom - 12, pos + 22, bottom - 12); }
                    break;
                case "biohazard":
                    double head = start + phase * span;
                    for (int i = 0; i < 46; i++)
                    {
                        double x = head - i * 1.8;
                        if (x < start + 2 || x > end) continue;
                        dc.PushOpacity(Math.Pow(1 - i / 46.0, 1.4));
                        Line(dc, Stroke(_accent, 1.1), x - 1.8, bottom - 5 - Ecg(Unit((x - start - 1.8) / 88)) * 7,
                            x, bottom - 5 - Ecg(Unit((x - start) / 88)) * 7); dc.Pop();
                    }
                    dc.DrawEllipse(_accent, null, new Point(head, bottom - 5 - Ecg(Unit((head - start) / 88)) * 7), 1.2, 1.2);
                    break;
                case "codec":
                    // Two voice channels use paired amplitudes; no traveling ring or dust.
                    for (int i = 0; i < 16; i++)
                    {
                        double x = start + i * Math.Min(7, span / 40), h = 1 + Math.Abs(Math.Sin(i * 1.3 + wave * 2)) * 6;
                        Line(dc, Stroke(_detail, 1), x, bottom - 5, x, bottom - 5 - h);
                        Line(dc, Stroke(_detail, 1), _length - x, bottom - 5, _length - x, bottom - 5 - h);
                    }
                    if (triggered && phase > .12 && phase < .44)
                    { Line(dc, Stroke(_accent, 1.5), pos, top + 2, pos, top + 7); dc.DrawEllipse(_accent, null, new Point(pos, top + 10), .8, .8); }
                    break;
                case "crystal":
                    double comet = start + phase * span;
                    for (int i = 0; i < 11; i++)
                    {
                        dc.PushOpacity(Math.Pow(1 - i / 11.0, 2) * .8);
                        Line(dc, Stroke(i % 3 == 0 ? _accent : Aether, .8), comet - i * 3, top + 3 + i * .35, comet - i * 3 - 2, top + 3 + i * .35); dc.Pop();
                    }
                    Star(dc, comet, top + 3, 1.5, _accent);
                    break;
                case "inferno":
                    // Embers belong to Diablo alone in this collection, rising from the seal.
                    for (int i = 0; i < 6; i++)
                    {
                        double age = Unit(phase * 1.2 + i * .173), x = pos + Math.Sin(i * 2.4 + age) * (9 + age * 22);
                        dc.PushOpacity((1 - age) * .9); dc.DrawEllipse(i % 2 == 0 ? Blood : _accent, null, new Point(x, bottom - 7 - age * _half * 1.3), .65, 1.4); dc.Pop();
                    }
                    for (int i = 0; i < 5; i++)
                    { dc.PushOpacity(.2 + Math.Max(0, Math.Sin(wave - i)) * .7); Rune(dc, pos + (i - 2) * 12, bottom - 5, i, _accent); dc.Pop(); }
                    break;
                case "strand":
                    DrawSlidingDrops(dc, top, bottom, phase);
                    if (triggered)
                    {
                        double node = start + phase * span;
                        dc.DrawEllipse(_accent, null, new Point(node, bottom - 4), 1.3, 1.3);
                        Curve(dc, Stroke(_detail, .7), new Point(pos, bottom - 4), new Point(pos + 15, top + 5), new Point(node - 8, top + 10), new Point(node, bottom - 4));
                    }
                    break;
            }
        }

        static readonly Brush WaterTrail = Themes.Br("#537F9BA4"), WaterBead = Themes.Br("#AB9BB8C1"), WaterGlint = Themes.Br("#CFE4F0F1");
        void DrawSlidingDrops(DrawingContext dc, double top, double bottom, double phase)
        {
            Rect live = _clip.Bounds;
            if (_vertical) live = new Rect(live.Y, live.X, live.Height, live.Width);
            double start = Math.Min(_half + 6, _length * .25), span = Math.Max(1, _length - start * 2);
            int count = Math.Max(16, Math.Min(34, (int)(_length / 24)));
            double height = Math.Max(4, live.Height - 6);
            for (int i = 0; i < count; i++)
            {
                double age = Unit(phase * (i % 3 == 0 ? 2 : 1) + i * .137);
                double release = Clamp((age - .23) / .77);
                double x = start + Unit(i * .6180339887) * span + Math.Sin(i * 1.7 + release * 2) * 1.2;
                double y = live.Top + 3 + release * release * height;
                double tail = Math.Min(14, release * 24), alpha = Clamp((1 - age) * 8);
                dc.PushOpacity(alpha);
                if (tail > .2)
                    Curve(dc, Stroke(WaterTrail, .65), new Point(x - .6, y - tail), new Point(x + .9, y - tail * .7), new Point(x - .6, y - tail * .25), new Point(x, y));
                double rx = .7 + i % 3 * .2, ry = 1 + release * 1.1;
                dc.DrawEllipse(WaterBead, null, new Point(x, y), rx, ry);
                dc.DrawEllipse(WaterGlint, null, new Point(x - .25, y - ry * .35), .25, .45);
                dc.Pop();
            }
        }

        static double Ecg(double x)
        {
            if (x < .32 || x > .66) return 0;
            if (x < .40) return (x - .32) * 3;
            if (x < .46) return .24 - (x - .40) * 10;
            if (x < .50) return -.36 + (x - .46) * 31;
            if (x < .56) return .88 - (x - .50) * 19;
            return -.26 + (x - .56) * 2.6;
        }

        void Sensor(DrawingContext dc, double x, double y, double phase, bool alert)
        {
            double opening = .28 + .65 * (.5 - .5 * Math.Cos(phase * Math.PI * (alert ? 4 : 2)));
            for (int i = 0; i < 5; i++)
            {
                double a = -.8 + (i - 2) * opening * .65;
                double dx = Math.Cos(a), dy = Math.Sin(a), r = 5 + opening * 4;
                Line(dc, Stroke(alert ? _accent : StrandBlue, 1.2), x, y, x + dx * r, y + dy * r);
                Line(dc, Stroke(_accent, .9), x + dx * r, y + dy * r, x + dx * r - dy * 3, y + dy * r + dx * 3);
            }
            dc.DrawEllipse(_accent, null, new Point(x, y), 1.1, 1.1);
        }

        void DrawNewSagaFocus(DrawingContext dc, Rect r, double phase, double radius)
        {
            double cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2, y = r.Bottom + 1;
            switch (_theme.Material)
            {
                case "cyberware":
                    Polygon(dc, null, Stroke(_accent, 1.2), new Point(r.Left, r.Top + 12), new Point(r.Left, r.Top + 3), new Point(r.Left + 4, r.Top - 1), new Point(r.Left + 17, r.Top - 1));
                    Polygon(dc, null, Stroke(SignalCyan, 1), new Point(r.Right, r.Bottom - 12), new Point(r.Right, r.Bottom - 3), new Point(r.Right - 4, r.Bottom + 1), new Point(r.Right - 17, r.Bottom + 1));
                    dc.DrawRectangle(_accent, null, new Rect(r.Left + 4, y, Math.Max(1, (r.Width - 9) * phase), 1.5));
                    break;
                case "biohazard":
                    // Diagnostic underline and two patient-monitor lamps, no enclosing frame.
                    Line(dc, Stroke(_detail, .65), r.Left + 3, y, r.Right - 3, y);
                    for (int i = 0; i < 24; i++)
                    { double a = i / 24.0; Line(dc, Stroke(_accent, 1), r.Left + a * r.Width, y - Ecg(Unit(a - phase)) * 4, r.Left + (a + 1 / 24.0) * r.Width, y - Ecg(Unit(a + 1 / 24.0 - phase)) * 4); }
                    dc.DrawEllipse(_accent, null, new Point(r.Left + 2, r.Top), 1.2, 1.2);
                    dc.DrawEllipse(BioRed, null, new Point(r.Left + 7, r.Top), .75, .75);
                    break;
                case "codec":
                    // Codec's two vertical channels stay mechanically straight.
                    Line(dc, Stroke(_detail, .9), r.Left - 1, r.Top + 4, r.Left - 1, r.Bottom - 4);
                    Line(dc, Stroke(_detail, .9), r.Right + 1, r.Top + 4, r.Right + 1, r.Bottom - 4);
                    double scan = r.Top + 3 + phase * (r.Height - 6);
                    dc.DrawRectangle(ScanShade, null, new Rect(r.Left + 2, scan, r.Width - 4, 2));
                    for (int i = 0; i < 4; i++) Line(dc, Stroke(_accent, .8), cx - 9 + i * 5, y, cx - 9 + i * 5, y - 1 - Math.Abs(Math.Sin(phase * 8 + i)) * 3);
                    break;
                case "crystal":
                    // Facets hug alternate corners; no diamond is drawn across the cover.
                    Polygon(dc, FacetLight, Stroke(_detail, .6), new Point(r.Left - 1, r.Top + 18), new Point(r.Left + 3, r.Top), new Point(r.Left + 14, r.Top + 1));
                    Polygon(dc, FacetBlue, Stroke(_detail, .6), new Point(r.Right + 1, r.Bottom - 18), new Point(r.Right - 3, r.Bottom), new Point(r.Right - 14, r.Bottom - 1));
                    Star(dc, r.Left + 4 + phase * (r.Width - 8), r.Top - 1, 1.9, _accent);
                    Glyph(dc, CrystalGlyph, cx, y, null, Stroke(_detail, .7), .3);
                    break;
                case "strand":
                    // A tethered identification tag, not a targeting frame.
                    double pulse = .5 + .5 * Math.Sin(phase * Math.PI * 2);
                    Line(dc, Stroke(_detail, .75), cx, y - 3, cx, y + 4);
                    dc.DrawRectangle(null, Stroke(_accent, .8), new Rect(cx - 4, y + 3, 8, 3));
                    Line(dc, Stroke(StrandBlue, .7), r.Left + 5, r.Top - 1, r.Left + 5 + (r.Width - 10) * pulse, r.Top - 1);
                    break;
            }
        }
    }
}
