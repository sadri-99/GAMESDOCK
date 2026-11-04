using System;
using System.Windows;
using System.Windows.Controls;

namespace GamesDock
{
    public static partial class Settings
    {
        static Border CustomizationPanel(Dock dock)
        {
            var cfg = dock.Cfg;
            var shell = new Border { Name = "Customization", CornerRadius = new CornerRadius(12),
                BorderThickness = new Thickness(1), Padding = new Thickness(12, 10, 12, 10) };
            shell.SetResourceReference(Border.BackgroundProperty, "CtlBg");
            shell.SetResourceReference(Border.BorderBrushProperty, "CtlEdge");
            var content = new StackPanel(); shell.Child = content;
            var top = new Grid();
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(390) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            top.Children.Add(new DockPreview(dock));
            var intro = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(intro, 2); top.Children.Add(intro);
            var title = Res.Label(Lang.T("Personaliza tu dock"), 18, "PanelFg"); title.FontWeight = FontWeights.SemiBold;
            intro.Children.Add(title);
            var description = Res.Label(Lang.T("Elige un tema y combina movimiento, forma y tamaño. Los cambios se aplican al instante."), 11.5, "PanelSub");
            description.TextWrapping = TextWrapping.Wrap; description.Margin = new Thickness(0, 4, 0, 5); intro.Children.Add(description);
            var hint = Res.Label(Lang.T("Pasa el ratón por la muestra y pulsa un icono para probar su animación."), 11, "PanelSub");
            hint.TextWrapping = TextWrapping.Wrap; intro.Children.Add(hint);
            content.Children.Add(top);

            var grid = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.2, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var themes = new StackPanel(); themes.Children.Add(SecHead("Elige un tema")); var gallery = new ScrollViewer { Content = ThemeGroups(dock), Height = 192, Tag = "theme-gallery",
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Focusable = false, PanningMode = PanningMode.VerticalOnly };
            gallery.Resources.Add(typeof(System.Windows.Controls.Primitives.ScrollBar), Res.Style("IconGalleryScrollBar"));
            themes.Children.Add(gallery);
            grid.Children.Add(themes);
            var combinations = new StackPanel(); Grid.SetColumn(combinations, 2); grid.Children.Add(combinations);
            combinations.Children.Add(SecHead("Prueba combinaciones"));
            var magnify = Switch(Lang.T("Agrandar los iconos al pasar el ratón"), cfg.Magnify, dock.SetMagnify);
            magnify.Margin = new Thickness(0, 0, 0, 6); combinations.Children.Add(magnify);
            var motion = Switch(Lang.T("Animaciones de los temas"), cfg.Motion, dock.SetThemeMotion);
            motion.Margin = new Thickness(0, 0, 0, 5); motion.ToolTip = PerformanceInfo.ToolTip(); combinations.Children.Add(motion);
            var usageLabel = Res.Label(Lang.T("CPU al animar · RAM según tema"), 10.5, "MenuAccent"); usageLabel.TextWrapping = TextWrapping.Wrap;
            var usage = new Button { Style = Res.Style("InfoLink"), Content = usageLabel, HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(45, -3, 0, 4), ToolTip = PerformanceInfo.ToolTip(), Tag = "animation-cost" };
            usage.Click += delegate { Aviso(PerformanceInfo.Description); }; combinations.Children.Add(usage);
            var shapes = new WrapPanel();
            foreach (var option in new[] { new[] { "Cuadrado", "defecto" }, new[] { "iOS", "ios" }, new[] { "Esfera", "esfera" } })
            {
                string id = option[1];
                var chip = Chip(Lang.T(option[0]), "forma", id == cfg.IconShape, delegate
                { cfg.IconShape = id; cfg.Save(); dock.Refresh(false); dock.NotifyAppearanceChanged(); });
                CompactControl(chip); shapes.Children.Add(chip);
            }
            combinations.Children.Add(InlineField(Lang.T("Forma del icono"), shapes));
            combinations.Children.Add(InlineField(Lang.T("Tamaño del icono"),
                Stepper("iconSize", 28, 76, 2, cfg.IconSize, "px", value =>
                { cfg.IconSize = value; cfg.Save(); dock.ApplyLayout(); dock.Refresh(false); dock.NotifyAppearanceChanged(); })));
            content.Children.Add(grid);
            return shell;
        }

        static Grid InlineField(string label, UIElement control)
        {
            var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(98) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var text = Res.Label(label, 11, "PanelSub"); text.VerticalAlignment = VerticalAlignment.Center;
            text.TextWrapping = TextWrapping.Wrap; grid.Children.Add(text); Grid.SetColumn(control, 1); grid.Children.Add(control); return grid;
        }
    }
}
