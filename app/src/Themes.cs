using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace GamesDock
{
    // Cada tema solo pinta brochas: nada de efectos de mapa de bits, la barra sigue costando lo mismo.
    public class ThemeDef
    {
        public string Label;
        public Dictionary<string, string> C = new Dictionary<string, string>();
    }

    public static class Themes
    {
        static ThemeDef T(string label, params string[] kv)
        {
            var t = new ThemeDef { Label = label };
            for (int i = 0; i < kv.Length; i += 2) t.C[kv[i]] = kv[i + 1];
            return t;
        }

        public static readonly Dictionary<string, ThemeDef> All = new Dictionary<string, ThemeDef>
        {
            { "noche", T("Noche",
                "glass", "#660A1018", "glassBot", "#7A060A10", "edge", "#42FFFFFF",
                "sheenTop", "#3AFFFFFF", "sheenMid", "#0AFFFFFF", "sheenBot", "#14FFFFFF",
                "hover", "#8CFFFFFF", "press", "#A6FFFFFF", "badge", "#2EFFFFFF", "letter", "#FFE6E9F2",
                "tipBg", "#F0131822", "tipFg", "#FFE6E9F2", "tipEdge", "#4DFFFFFF",
                "fieldBg", "#F0131822", "fieldFg", "#FF9AA3B4", "accent", "#FF5B8DEF") },
            { "niebla", T("Niebla",
                "glass", "#8CF2F5FA", "glassBot", "#8CC6D0DE", "edge", "#B3FFFFFF",
                "sheenTop", "#CCFFFFFF", "sheenMid", "#14FFFFFF", "sheenBot", "#3399A6BC",
                "hover", "#881B2A3D", "press", "#A31B2A3D", "badge", "#331B2A3D", "letter", "#FF1E2430",
                "tipBg", "#F2F5F9FA", "tipFg", "#FF1E2430", "tipEdge", "#80FFFFFF",
                "fieldBg", "#F2F5F9FA", "fieldFg", "#FF5A6474", "accent", "#FF2F6FB8") },
            { "onix", T("Ónix",
                "glass", "#8C0C0C0F", "glassBot", "#99000000", "edge", "#33FFFFFF",
                "sheenTop", "#24FFFFFF", "sheenMid", "#05FFFFFF", "sheenBot", "#0FFFFFFF",
                "hover", "#80FFFFFF", "press", "#99FFFFFF", "badge", "#24FFFFFF", "letter", "#FFF2F2F5",
                "tipBg", "#F20D0D10", "tipFg", "#FFF2F2F5", "tipEdge", "#2EFFFFFF",
                "fieldBg", "#F20D0D10", "fieldFg", "#FF9DA2AE", "accent", "#FF8B7BF0") },
            { "neon", T("Neón",
                "glass", "#73160A26", "glassBot", "#801B0630", "edge", "#4DFF59C7",
                "sheenTop", "#40FF7AE0", "sheenMid", "#0AFFFFFF", "sheenBot", "#3359FFF6",
                "hover", "#992FA8E8", "press", "#B338E0FF", "badge", "#3359FFF6", "letter", "#FFE8FBFF",
                "tipBg", "#F2160A28", "tipFg", "#FFD8F6FF", "tipEdge", "#6659FFF6",
                "fieldBg", "#F2160A28", "fieldFg", "#FF8FD8F0", "accent", "#FFFF4FC3") },
            { "ambar", T("Ámbar",
                "glass", "#731D1406", "glassBot", "#80120B02", "edge", "#4DE8A33A",
                "sheenTop", "#40FFD08A", "sheenMid", "#0AFFFFFF", "sheenBot", "#26E8A33A",
                "hover", "#8CE8A33A", "press", "#AAFFB347", "badge", "#33FFC46B", "letter", "#FFF6E8D0",
                "tipBg", "#F218120A", "tipFg", "#FFF3E4C8", "tipEdge", "#66E8A33A",
                "fieldBg", "#F218120A", "fieldFg", "#FFB9A184", "accent", "#FFF0A83C") },
            { "bosque", T("Bosque",
                "glass", "#73081A11", "glassBot", "#8004120A", "edge", "#4D7CE8B4",
                "sheenTop", "#3AA8F0D0", "sheenMid", "#0AFFFFFF", "sheenBot", "#267CE8B4",
                "hover", "#8C2FAE7E", "press", "#A634C98F", "badge", "#337CE8B4", "letter", "#FFDCF5E8",
                "tipBg", "#F207170F", "tipFg", "#FFDCF5E8", "tipEdge", "#667CE8B4",
                "fieldBg", "#F207170F", "fieldFg", "#FF8FBFAC", "accent", "#FF3FC98F") },
            // blanco y rosa: el cristal mas claro de la familia, asi que la letra y el halo dejan de ser
            // blancos y pasan a ser tinta oscura y rosa para que el icono siga leyendose
            { "anime", T("Anime",
                "glass", "#99FFFFFF", "glassBot", "#B3FBD0E4", "edge", "#CCFFFFFF",
                "sheenTop", "#FFFFFFFF", "sheenMid", "#14FFFFFF", "sheenBot", "#59F06AA8",
                "hover", "#8CF06AA8", "press", "#B3FF3D8F", "badge", "#40F06AA8", "letter", "#FF3A2030",
                "tipBg", "#F5FFEFF6", "tipFg", "#FF33202B", "tipEdge", "#B3FFC9E2",
                "fieldBg", "#F5FFEFF6", "fieldFg", "#FF9A5C7C", "accent", "#FFFF4FA0") },
            // rojo, negro y azul: el cristal casi negro deja brillar el azul del filo y el rojo del
            // reguero inferior; el rojo manda ademas en el acento y en la pulsacion
            { "cyber", T("Cyberpunk",
                "glass", "#9905060A", "glassBot", "#A61A0508", "edge", "#592BD0FF",
                "sheenTop", "#402BD0FF", "sheenMid", "#0AFFFFFF", "sheenBot", "#4DFF2D55",
                "hover", "#8CFF2D55", "press", "#B3FF5C7A", "badge", "#332BD0FF", "letter", "#FFE8F4FF",
                "tipBg", "#F0070A12", "tipFg", "#FFD6EEFF", "tipEdge", "#4D2BD0FF",
                "fieldBg", "#F0070A12", "fieldFg", "#FF7FA8C9", "accent", "#FFFF2D55") },
            // ovni: verde fosforito en el filo y las luces, amarillo en el reguero y en el acento,
            // sobre un cristal de noche de campo
            { "ovni", T("Ovni",
                "glass", "#8004170C", "glassBot", "#8C141F06", "edge", "#59A8FF6A",
                "sheenTop", "#40C8FF8A", "sheenMid", "#0AFFFFF0", "sheenBot", "#4DF5E663",
                "hover", "#8C39D97A", "press", "#A6F5E663", "badge", "#33A8FF6A", "letter", "#FFEAFFE8",
                "tipBg", "#F0051409", "tipFg", "#FFEAFFE8", "tipEdge", "#66A8FF6A",
                "fieldBg", "#F0051409", "fieldFg", "#FF89C99B", "accent", "#FFF5E663") }
        };

        public const string Fallback = "noche";

        public static ThemeDef Get(string id)
        {
            ThemeDef t;
            if (id != null && All.TryGetValue(id, out t)) return t;
            return All[Fallback];
        }

        public static Color Col(string hex)
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }

        public static SolidColorBrush Br(string hex)
        {
            var b = new SolidColorBrush(Col(hex));
            b.Freeze();
            return b;
        }

        // misma brocha pero escalando el alfa: evita tener que inventarse un color nuevo por tema
        public static SolidColorBrush BrA(string hex, double mult)
        {
            var c = Col(hex);
            byte a = (byte)Math.Max(0.0, Math.Min(255.0, (double)c.A * mult));
            var b = new SolidColorBrush(Color.FromArgb(a, c.R, c.G, c.B));
            b.Freeze();
            return b;
        }

        public static LinearGradientBrush Grad(params string[] hexStops)
        {
            var g = new LinearGradientBrush { StartPoint = new System.Windows.Point(0, 0), EndPoint = new System.Windows.Point(0, 1) };
            int last = hexStops.Length - 1;
            for (int i = 0; i <= last; i++)
                g.GradientStops.Add(new GradientStop(Col(hexStops[i]), last > 0 ? (double)i / last : 0.0));
            g.Freeze();
            return g;
        }

        // halo de raton: degradado radial (vector, sin efectos de mapa de bits) que se apaga hacia el borde
        public static RadialGradientBrush Glow(string hex)
        {
            var col = Col(hex);
            var mid = Color.FromArgb((byte)(col.A * 0.55), col.R, col.G, col.B);
            var outer = Color.FromArgb(0, col.R, col.G, col.B);
            var g = new RadialGradientBrush
            {
                Center = new System.Windows.Point(0.5, 0.5),
                GradientOrigin = new System.Windows.Point(0.5, 0.5),
                RadiusX = 0.5,
                RadiusY = 0.5
            };
            g.GradientStops.Add(new GradientStop(col, 0.0));
            g.GradientStops.Add(new GradientStop(mid, 0.60));
            g.GradientStops.Add(new GradientStop(outer, 1.0));
            g.Freeze();
            return g;
        }
    }
}
