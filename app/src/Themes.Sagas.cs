using System.Collections.Generic;

namespace GamesDock
{
    public static partial class Themes
    {
        static Dictionary<string, ThemeDef> AddSagas(Dictionary<string, ThemeDef> themes)
        {
            themes.Add("nightcity", M("Cyberpunk 2077", "cyberware", .12,
                "#F3252A25", "#F10C161B", "#C8F0E447", "#FFF4E94C", "#FF121D23", "#FFF2F0D2", "#FFBEC8B5"));
            themes.Add("raccoon", M("Resident Evil", "biohazard", .22,
                "#F11C2827", "#F20A1113", "#89608479", "#FF9AD6AA", "#FF111D1B", "#FFE5EEE6", "#FF9EB7A9"));
            themes.Add("codec", M("Metal Gear Solid", "codec", .08,
                "#F02D3C33", "#F21A2A23", "#AA789987", "#FFA7CEAA", "#FF13261F", "#FFDFEDDE", "#FFA3B9A4"));
            themes.Add("crystal", M("Final Fantasy", "crystal", .38,
                "#EF183B53", "#F011203A", "#A081C3D8", "#FFA6F0EE", "#FF122C43", "#FFE0FBFC", "#FF9FCBD7"));
            themes.Add("inferno", M("Diablo", "inferno", .16,
                "#F1302325", "#F2161016", "#AA9A7154", "#FFE7AB73", "#FF23191D", "#FFF1DFCA", "#FFBFA28D"));
            themes.Add("strand", M("Death Stranding", "strand", .14,
                "#ED26373F", "#F1132029", "#A4799DA8", "#FFEDBD76", "#FF182A33", "#FFE3EFF1", "#FFA6C1C7"));
            return themes;
        }

        public static string SagaHint(string material)
        {
            switch (material)
            {
                case "cyberware": return "Cyberpunk 2077: circuitos, amarillo ácido, enlace cian y glitch de implantes.";
                case "biohazard": return "Resident Evil: monitor clínico, pulso de estado, vial y marcas de cuarentena.";
                case "codec": return "Metal Gear Solid: enlace Codec, señal táctica, radar y alerta de infiltración.";
                case "crystal": return "Final Fantasy: cristal facetado, flujo de éter y estela de meteoro.";
                case "inferno": return "Diablo: orbes de sangre, círculo de invocación, runas y brasas.";
                case "strand": return "Death Stranding: lluvia de declive, Odradek, enlace quiral y hilos de conexión.";
                default: return null;
            }
        }
    }
}
