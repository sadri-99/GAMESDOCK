using System;
using System.Windows;
using System.Windows.Controls;

namespace GamesDock
{
    // A bounded reference measurement, not continuous telemetry or a guarantee about game FPS.
    public static class PerformanceInfo
    {
        public static FrameworkElement SettingsHeader()
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new TextBlock { Text = Lang.T("Ajustes") });
            var badge = new TextBlock { Text = Lang.T("Consumo"), FontSize = 10.5,
                Margin = new Thickness(12, 1, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            badge.SetResourceReference(TextBlock.ForegroundProperty, "MenuAccent");
            row.Children.Add(badge); return row;
        }
        public static ToolTip ToolTip()
        {
            var tip = new ToolTip { Content = new TextBlock { Text = Description, MaxWidth = 390, TextWrapping = TextWrapping.Wrap },
                BorderThickness = new Thickness(1), Padding = new Thickness(12), HasDropShadow = false };
            tip.SetResourceReference(Control.BackgroundProperty, "PanelBg");
            tip.SetResourceReference(Control.ForegroundProperty, "PanelFg");
            tip.SetResourceReference(Control.BorderBrushProperty, "PanelEdge");
            return tip;
        }
        public static string Description
        {
            get
            {
                return Lang.T("Las animaciones tienen un coste medible mientras se mueven: usan CPU y GPU. En reposo no mantienen un bucle de efectos.") + "\n\n" +
                    Lang.T("Referencia local de los 11 temas animados conservados, con ocho accesos de 64 px y 12 procesadores lógicos: al mover el cursor, aproximadamente 2,6–5,3 % de CPU total. Ventana fuera del área visible; no es un monitor en directo.") + "\n\n" +
                    Lang.T("La RAM varía con el tema y las cachés. En la medición anterior de los seis temas originales, activar efectos añadió aproximadamente 1–4 MB de memoria privada y hasta 1,2 MB de memoria física. Ese incremento no se ha medido por separado para los doce nuevos. El total también incluye Windows, iconos, catálogo y fichas.") + "\n\n" +
                    Lang.T("El consumo cambia con el equipo, el tema, la resolución y la interacción. No se ha medido la GPU ni el impacto en los FPS de juegos. Puedes desactivar los efectos; el agrandado es independiente.");
            }
        }
    }
}
