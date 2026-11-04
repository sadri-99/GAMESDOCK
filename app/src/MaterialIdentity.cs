using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace GamesDock
{
    // Frozen, original vector interpretations of franchise emblems. No downloads or logo bitmaps.
    static class SagaIdentity
    {
        static readonly Dictionary<string, DrawingGroup> Cache = new Dictionary<string, DrawingGroup>();
        static Brush B(string color) { return Themes.Br(color); }
        static Pen P(string color, double width) { var p = new Pen(B(color), width); p.Freeze(); return p; }
        static void Shape(DrawingContext dc, string path, string fill, string stroke = null, double width = 1)
        { var g = Geometry.Parse(path); g.Freeze(); dc.DrawGeometry(fill == null ? null : B(fill), stroke == null ? null : P(stroke, width), g); }
        static void Text(DrawingContext dc, string text, double size, string color, double y, string font = "Consolas")
        {
            var f = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(new FontFamily(font), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal), size, B(color), 1);
            dc.DrawText(f, new Point(-f.Width / 2, y));
        }
        public static DrawingGroup For(string material)
        {
            if (material == null) return null;
            lock (Cache)
            {
                DrawingGroup old; if (Cache.TryGetValue(material, out old)) return old;
                var group = new DrawingGroup();
                using (var dc = group.Open())
                {
                    switch (material)
                    {
                        case "industrial":
                            Shape(dc, "M -14,-9 L -7,7 -2,-1 0,6 3,-1 8,7 14,-9 9,-9 7,0 3,-8 -2,-8 -6,0 -9,-9 Z", "#FFE9CE64");
                            for (int i = 0; i < 3; i++) dc.DrawRectangle(B("#FFB6AC6B"), null, new Rect(-12, 10 + i * 2, 24, 1));
                            Text(dc, "W/Y", 5, "#FF172B25", 7);
                            Text(dc, "ALIEN", 5.5, "#FF243C31", 16); break;
                        case "portal":
                            for (int i = 0; i < 8; i++)
                            {
                                dc.PushTransform(new RotateTransform(i * 45));
                                Shape(dc, "M -2,-4 L -7,-13 3,-13 8,-8 3,-3 Z", "#FF69CEE7"); dc.Pop();
                            }
                            break;
                        case "memory":
                            Text(dc, "P", 26, "#FFE75C65", -19, "Arial");
                            Shape(dc, "M -12,9 L -4,4 8,4 13,7 6,11 -4,12 -7,10 3,7 -2,7 -9,11 Z", "#FFF2CB5E");
                            dc.DrawRectangle(B("#FF64B9D0"), null, new Rect(-4, 7, 9, 3));
                            dc.DrawRectangle(B("#FF77BD87"), null, new Rect(-11, 9, 6, 2)); break;
                        case "orbit":
                            Text(dc, "N7", 21, "#FFF0F3F4", -14, "Arial");
                            Shape(dc, "M -14,10 L 14,10 14,14 -14,14 Z M 5,7 L 9,3 14,3 10,7 Z", "#FFE05B60"); break;
                        case "terminal":
                            Shape(dc, "M -11,-11 L 11,-11 4,-4 -2,0 -2,5 M -5,-5 L 5,5 M -11,10 L 0,10 0,13 M 6,7 L 11,7 11,13", null, "#FF91F1AC", 1.8);
                            dc.DrawRectangle(B("#FFDCFFE4"), null, new Rect(-11, -12, 3, 2));
                            Text(dc, "MATRIX", 5, "#FF93DCAE", 16); break;
                        case "cyberware":
                            Shape(dc, "M -11,-1 L -14,-13 -5,-8 0,-11 5,-8 14,-13 11,-1 8,9 0,14 -8,9 Z", "#FFD7534D");
                            Shape(dc, "M -10,-2 L -3,1 -8,3 Z M 10,-2 L 3,1 8,3 Z M -5,7 L 5,7 2,11 -2,11 Z", "#FF121B20");
                            Shape(dc, "M -6,7 L -4,10 -3,7 M 6,7 L 4,10 3,7", null, "#FFFFE65C", .8);
                            Text(dc, "2077", 6, "#FFF3DD47", 15); break;
                        case "biohazard":
                            for (int i = 0; i < 8; i++)
                            {
                                dc.PushTransform(new RotateTransform(i * 45));
                                Shape(dc, "M 0,0 L -5.4,-13 5.4,-13 Z", i % 2 == 0 ? "#FFDB5058" : "#FFF0F0E7", "#FF25312D", .4); dc.Pop();
                            }
                            break;
                        case "codec":
                            Shape(dc, "M -13,-12 L -4,-7 0,-10 4,-7 13,-12 8,5 0,13 -8,5 Z", "#FFB8CA9A");
                            Shape(dc, "M -8,-5 L -1,-2 -4,1 Z M 8,-5 L 1,-2 4,1 Z M -4,5 L 0,8 4,5 0,12 Z", "#FF253B30");
                            Text(dc, "FOX", 5, "#FF20372A", 1);
                            Text(dc, "MGS", 5.5, "#FFB8CA9A", 16); break;
                        case "crystal":
                            Shape(dc, "M 0,-16 L 8,-5 5,10 0,16 -7,6 -8,-5 Z", "#FF4C96BF", "#FFA6EFF0", .8);
                            Shape(dc, "M 0,-16 L -2,0 0,16 5,10 Z", "#FFACEEF1");
                            Shape(dc, "M -8,-5 L -2,0 8,-5 M -2,0 L -7,6", null, "#FFDBFAFF", .7);
                            Text(dc, "FF", 7, "#FFC2F3F6", 15, "Times New Roman"); break;
                        case "inferno":
                            Text(dc, "D", 29, "#FFD7AD79", -20, "Times New Roman");
                            Shape(dc, "M -9,-7 L -14,-16 -6,-11 M 8,-7 L 14,-16 6,-11", null, "#FFCB6654", 2);
                            Text(dc, "DIABLO", 5, "#FFE49B76", 10, "Times New Roman"); break;
                        case "strand":
                            dc.DrawRoundedRectangle(B("#FFBB8C4F"), P("#FFEBC58A", 1), new Rect(-10, -16, 20, 32), 8, 8);
                            dc.DrawRoundedRectangle(B("#FF4C493C"), null, new Rect(-7, -12, 14, 24), 6, 6);
                            dc.DrawEllipse(B("#FFEBC68F"), null, new Point(2, -5), 3.8, 4);
                            Shape(dc, "M 3,-2 C -7,-3 -7,5 -1,8 C 4,11 7,4 1,3 L -1,5 M -2,0 L 3,2", null, "#FFEBC68F", 1.5); break;
                    }
                }
                group.Freeze(); Cache[material] = group; return group;
            }
        }
    }

    public sealed partial class MaterialArt
    {
        DrawingGroup _identity;
        void DrawIdentity(DrawingContext dc, double top, double bottom)
        {
            // Micro-details use the old end clearance. No bay, backing plate or inset is added.
            double idle = 1 - Math.Max(Clamp(InteractionLevel), EventPhase >= 0 ? Envelope(EventPhase) : 0);
            if (idle < .01 || _length < 100) return;
            dc.PushOpacity(idle * .7);
            switch (_theme.Material)
            {
                case "industrial":
                    for (int i = 0; i < 3; i++) Line(dc, Stroke(_detail, .8), 4, _center - 3 + i * 3, 7, _center - 2 + i * 3);
                    break;
                case "portal":
                    dc.DrawEllipse(null, Stroke(Blue, .8), new Point(5, _center), 1, 4);
                    dc.DrawEllipse(null, Stroke(Orange, .8), new Point(_length - 5, _center), 1, 4);
                    break;
                case "memory":
                    for (int i = 0; i < 4; i++) dc.DrawRectangle(i == 0 ? PsGreen : i == 1 ? PsRed : i == 2 ? PsBlue : PsPink, null, new Rect(4, _center - 5 + i * 3, 2, 1.5));
                    break;
                case "orbit":
                    Line(dc, Stroke(BioRed, 1.2), 5, _center - 4, 5, _center + 4);
                    Line(dc, Stroke(Pearl, .7), 4, _center - 5, 8, _center - 5);
                    break;
                case "terminal": CodeGlyph(dc, 0, 4, _center - 2, Phosphor, .65); break;
                case "cyberware":
                    Line(dc, Stroke(_accent, 1), 4, _center - 3, 7, _center - 3);
                    Line(dc, Stroke(SignalCyan, .8), 5, _center + 2, 8, _center + 2);
                    break;
                case "biohazard":
                    dc.PushTransform(new MatrixTransform(new Matrix(.21, 0, 0, .21, 6, _center)));
                    dc.DrawDrawing(_identity); dc.Pop(); break;
                case "codec":
                    dc.DrawEllipse(_accent, null, new Point(5, _center - 3), 1, 1);
                    Line(dc, Stroke(_detail, .7), 4, _center + 2, 8, _center + 2); break;
                case "crystal": Glyph(dc, CrystalGlyph, 6, _center, null, Stroke(_detail, .8), .35); break;
                case "inferno":
                    dc.DrawEllipse(Blood, null, new Point(5, _center - 2), 1.3, 1.3);
                    dc.DrawEllipse(Aether, null, new Point(5, _center + 2), 1.3, 1.3); break;
            }
            dc.Pop();
        }

    }
}
