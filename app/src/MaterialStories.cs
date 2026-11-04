using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace GamesDock
{
    public sealed partial class MaterialArt
    {
        readonly Dictionary<string, FormattedText> _storyLabels = new Dictionary<string, FormattedText>();
        static readonly Brush DoorInset = Themes.Br("#59D3B790");
        static readonly Brush DoorWood = Themes.Br("#F02F2425"), DoorEdge = Themes.Br("#FFD3B790"), BoxCard = Themes.Br("#EE8A7859"), BoxInk = Themes.Br("#FF3C3B2B");
        void DrawStoryLabel(DrawingContext dc, string text, double x, double y, Brush brush, double size, double phase)
        {
            FormattedText label;
            if (!_storyLabels.TryGetValue(text, out label))
            {
                label = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal), size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                _storyLabels[text] = label;
            }
            dc.PushOpacity(Envelope(phase)); dc.DrawText(label, new Point(x - label.Width / 2, y)); dc.Pop();
        }

        void DrawLaunchStory(DrawingContext dc, Rect r, double phase)
        {
            double cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
            double act = Clamp((phase - .12) / .7), burst = Math.Sin(act * Math.PI);
            switch (_theme.Material)
            {
                case "industrial":
                    // Airlock blades close in opposing directions, then release the chosen app.
                    double close = phase < .38 ? Clamp(phase / .38) : Clamp((.65 - phase) / .27);
                    dc.PushOpacity(.4 * close);
                    dc.DrawRectangle(Vent, Stroke(_detail, .6), new Rect(r.Left + 3, r.Top, r.Width - 6, Math.Max(.1, r.Height * .42 * close)));
                    dc.DrawRectangle(Vent, Stroke(_detail, .6), new Rect(r.Left + 3, r.Bottom - r.Height * .42 * close, r.Width - 6, Math.Max(.1, r.Height * .42 * close)));
                    dc.Pop();
                    DrawStoryLabel(dc, "AIRLOCK", cx, r.Top + 2, Radar, 6.5, phase); break;
                case "portal":
                    if (phase > .2 && phase < .8)
                    {
                        double p = (phase - .2) / .6, x = Between(r.Left - 3, r.Right + 3, p);
                        Cube(dc, x, r.Bottom - 4 - Math.Sin(p * Math.PI) * 5, 4.2, p);
                    }
                    break;
                case "memory":
                    double width = r.Width * Clamp(phase * 1.35);
                    dc.DrawRoundedRectangle(null, Stroke(_detail, .8), new Rect(r.Left + 3, r.Bottom - 5, r.Width - 6, 5), 1, 1);
                    dc.DrawRectangle(PsGreen, null, new Rect(r.Left + 4, r.Bottom - 4, Math.Max(.1, Math.Min(r.Width - 8, width)), 3));
                    if (phase > .7) DrawStoryLabel(dc, "OK", cx, r.Top + 1, PsGreen, 8, phase);
                    break;
                case "orbit":
                    // A blue relay corridor with an orange omni-tool cast, distinct from portals.
                    for (int i = 0; i < 4; i++)
                    {
                        dc.PushOpacity((1 - i / 4.0) * burst * .6);
                        double x = r.Right + 3 + i * 4 + act * 7;
                        Line(dc, Stroke(MassField, .9), x, r.Top + 8 + i, x + 3, r.Bottom - 8 - i); dc.Pop();
                    }
                    DrawStoryLabel(dc, "RELAY", cx, r.Top, MassField, 7, phase); break;
                case "terminal":
                    // Decrypt rows pass the cover, then a white cursor acknowledges the launch.
                    for (int i = 0; i < 5; i++)
                    {
                        double y = r.Top + Unit(phase * 1.7 + i * .2) * r.Height;
                        dc.PushOpacity(.12); Line(dc, Stroke(Head, 1), r.Left + 2, y, r.Right - 2, y); dc.Pop();
                    }
                    if (phase > .65) dc.DrawRectangle(Head, null, new Rect(cx - 3, r.Bottom - 2, 6, 2));
                    break;
                case "cyberware":
                    // A remote quickhack locks three asymmetrical address brackets.
                    for (int i = 0; i < 3; i++)
                    {
                        double t = Clamp((phase - i * .12) * 5), x = r.Left - 3 + i * 4;
                        dc.PushOpacity(t * .8);
                        Polygon(dc, null, Stroke(i == 1 ? SignalCyan : _accent, 1), new Point(x, r.Top + 2 + i * 2), new Point(x, r.Top + 9 + i * 2), new Point(x + 5, r.Top + 9 + i * 2)); dc.Pop();
                    }
                    DrawStoryLabel(dc, "QUICKHACK", cx, r.Bottom - 7, _accent, 6, phase); break;
                case "biohazard":
                    // Classic door-loading homage: panel, knob, hinge, darkness, reveal.
                    if (phase > .14 && phase < .85)
                    {
                        double opening = Clamp((phase - .3) / .45), w = (r.Width - 9) * Math.Max(.015, Math.Cos(opening * Math.PI / 2));
                        dc.PushOpacity(Envelope((phase - .14) / .71) * .92);
                        dc.DrawRectangle(DoorWood, Stroke(DoorEdge, .8), new Rect(r.Left + 4, r.Top + 2, w, r.Height - 4));
                        if (w > 6)
                        {
                            dc.DrawRectangle(null, Stroke(DoorInset, .5), new Rect(r.Left + 6, r.Top + 5, Math.Max(.1, w - 4), r.Height * .4));
                            dc.DrawEllipse(DoorEdge, null, new Point(r.Left + w, cy + 3), .9, 1.2);
                        }
                        dc.Pop();
                    }
                    break;
                case "codec":
                    // Snake's cardboard-box trick; flaps settle before the alert disappears.
                    if (phase > .18 && phase < .75)
                    {
                        double fade = Envelope((phase - .18) / .57);
                        dc.PushOpacity(fade * .85);
                        double yBox = r.Bottom - r.Height * .47;
                        dc.DrawRectangle(BoxCard, Stroke(BoxInk, .8), new Rect(r.Left + 5, yBox, r.Width - 10, r.Height * .43));
                        Line(dc, Stroke(BoxInk, .8), cx, yBox, cx, r.Bottom - 4);
                        Line(dc, Stroke(BoxInk, .7), r.Left + 8, yBox + 5, r.Right - 8, yBox + 5);
                        DrawStoryLabel(dc, "!", cx, r.Top + 1, BioRed, 15, phase); dc.Pop();
                    }
                    break;
                case "crystal":
                    for (int i = 0; i < 3; i++)
                    {
                        double x = Between(i == 0 ? r.Left - 5 : i == 1 ? r.Right + 5 : cx, cx + (i - 1) * 5, act);
                        double y = Between(i == 2 ? r.Top - 3 : r.Bottom, r.Bottom - 4, act);
                        Glyph(dc, CrystalGlyph, x, y, i == 1 ? FacetLight : null, Stroke(_accent, .75), .3 + act * .2);
                    }
                    break;
                case "inferno":
                    // A five-point summoning seal, reserved for Diablo.
                    dc.PushOpacity(.35 * burst);
                    Glyph(dc, FiveStar, cx, cy, null, Stroke(Blood, .025), r.Width * .53);
                    dc.Pop(); break;
                case "strand":
                    Sensor(dc, r.Right - 3, cy, phase, true);
                    for (int i = 0; i < 3; i++)
                    {
                        double x = r.Left + i * 14, p = Clamp((phase - i * .15) * 3);
                        dc.PushOpacity(p * .35); dc.DrawEllipse(Tar, null, new Point(x, r.Bottom - 3), 2.2, 2.7);
                        for (int finger = 0; finger < 4; finger++) Line(dc, Stroke(Tar, .65), x - 2 + finger * 1.3, r.Bottom - 5, x - 2 + finger * 1.3, r.Bottom - 8 - finger % 2); dc.Pop();
                    }
                    break;
            }
        }
    }
}
