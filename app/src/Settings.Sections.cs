using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace GamesDock
{
    public static partial class Settings
    {
        static Grid SecondarySections(Dock dock)
        {
            var grid = new Grid { Name = "AllSettings", Margin = new Thickness(0, 12, 0, 0) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var primary = new Grid();
            foreach (double weight in new[] { 1.0, 1.06, 1.0 })
                primary.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(weight, GridUnitType.Star) });
            AddSection(primary, 0, 0, "bar", "Barra y contenido", BarSettings(dock));
            AddSection(primary, 0, 1, "behavior", "Comportamiento", BehaviorSettings(dock));
            AddSection(primary, 0, 2, "community", "Iconos de la comunidad", CommunitySettings(dock));
            var utilities = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            foreach (double weight in new[] { .58, 1.17, 1.31 })
                utilities.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(weight, GridUnitType.Star) });
            AddSection(utilities, 0, 0, "language", "Idioma", LanguageSettings(dock));
            AddSection(utilities, 0, 1, "system", "Sistema", SystemSettings(dock));
            AddSection(utilities, 0, 2, "help", "Ayuda y proyecto", HelpSettings());
            grid.Children.Add(primary); Grid.SetRow(utilities, 1); grid.Children.Add(utilities);
            return grid;
        }

        static void AddSection(Grid grid, int row, int column, string id, string title, UIElement body)
        {
            var stack = new StackPanel();
            var heading = Res.Label(Lang.T(title), 12, "PanelFg"); heading.FontWeight = FontWeights.SemiBold;
            heading.Margin = new Thickness(0, 0, 0, 9); stack.Children.Add(heading); CompactTree(body); stack.Children.Add(body);
            var section = new Border { Tag = id, Padding = new Thickness(12, 10, 12, 8), CornerRadius = new CornerRadius(9),
                BorderThickness = new Thickness(1), Margin = new Thickness(column == 0 ? 0 : 5, row == 0 ? 0 : 8, column == 2 ? 0 : 5, 0), Child = stack };
            section.SetResourceReference(Border.BackgroundProperty, "CtlBg"); section.SetResourceReference(Border.BorderBrushProperty, "CtlEdge");
            section.SetValue(AutomationProperties.NameProperty, Lang.T(title));
            Grid.SetRow(section, row); Grid.SetColumn(section, column); grid.Children.Add(section);
        }

        static void CompactTree(DependencyObject root)
        {
            CompactControl(root);
            foreach (object child in LogicalTreeHelper.GetChildren(root)) if (child is DependencyObject) CompactTree((DependencyObject)child);
        }

        static void CompactControl(DependencyObject element)
        {
            var radio = element as RadioButton;
            if (radio != null && radio.GroupName != "tema")
            { radio.Padding = new Thickness(8, 4, 8, 4); radio.FontSize = 12; radio.Margin = new Thickness(0, 0, 5, 4); }
            var button = element as Button;
            if (button != null) { button.Padding = new Thickness(9, 5, 9, 5); button.FontSize = 12; button.Margin = new Thickness(0, 0, 5, 4); }
            var check = element as CheckBox;
            if (check != null) { check.FontSize = 12; check.Margin = new Thickness(0, 0, 0, 6); }
        }

        static StackPanel BarSettings(Dock dock)
        {
            var cfg = dock.Cfg; var panel = new StackPanel(); var positions = new WrapPanel();
            foreach (var option in new[] { new[] { "Abajo", "bottom" }, new[] { "Arriba", "top" }, new[] { "Izquierda", "left" },
                new[] { "Derecha", "right" }, new[] { "Libre", "free" } })
            {
                string id = option[1];
                positions.Children.Add(Chip(Lang.T(option[0]), "posicion", cfg.Position == id, delegate
                {
                    if (id == "free" && cfg.Position != "free") dock.AnotarPuestoActual();
                    cfg.Position = id; cfg.Save(); dock.ApplyLayout();
                }));
            }
            panel.Children.Add(Field(Lang.T("Posición de la barra"), positions, 10));
            var sections = new WrapPanel();
            Action rebuildPages = delegate
            {
                sections.Children.Clear();
                foreach (var page in cfg.Pages)
                {
                    string id = page.Id;
                    var chip = Chip(PageStore.Label(page), "seccion", cfg.Section == id, () => dock.SetSection(id));
                    chip.Content = new TextBlock { Text = PageStore.Label(page), MaxWidth = 140, TextTrimming = TextTrimming.CharacterEllipsis };
                    chip.ToolTip = PageStore.Label(page); CompactControl(chip);
                    sections.Children.Add(chip);
                }
                var add = Pill(Lang.T("+ Añadir página"), dock.CreatePage);
                add.IsEnabled = PageStore.CustomCount(cfg) < PageStore.MaxCustomPages;
                CompactControl(add); sections.Children.Add(add);
            };
            rebuildPages();
            sections.Loaded += delegate { dock.PagesChanged -= rebuildPages; dock.PagesChanged += rebuildPages; rebuildPages(); };
            sections.Unloaded += delegate { dock.PagesChanged -= rebuildPages; };
            panel.Children.Add(Field(Lang.T("Páginas"), sections, 0)); return panel;
        }

        static StackPanel BehaviorSettings(Dock dock)
        {
            var cfg = dock.Cfg; var panel = new StackPanel();
            panel.Children.Add(Switch(Lang.T("Mostrar ficha del juego al pasar el ratón"), cfg.Ficha,
                on => { cfg.HoverCard = on; cfg.Save(); dock.ActualizarGlobos(); }));
            var large = Switch(Lang.T("Ficha un 15 % más grande"), cfg.HoverCardLarge,
                on => { cfg.HoverCardLarge = on; cfg.Save(); dock.ActualizarGlobos(); });
            var personal = Switch(Lang.T("Mostrar mi actividad de Steam"), cfg.SteamPersonalData,
                on => { cfg.SteamPersonalData = on; if (!on) SteamActivity.Clear(); cfg.Save(); dock.ActualizarGlobos(); });
            personal.ToolTip = Lang.T("Horas jugadas y última partida de la cuenta activa, desde los datos locales de Steam.");
            panel.Children.Add(large); panel.Children.Add(personal);
            panel.Children.Add(Switch(Lang.T("Mantener el dock al nivel del escritorio"), cfg.DesktopLevel,
                on => { cfg.DesktopLevel = on; cfg.Save(); dock.AttachToDesktop(); dock.ApplyLayout(); }));
            return panel;
        }

        static WrapPanel LanguageSettings(Dock dock)
        {
            var panel = new WrapPanel();
            foreach (var option in new[] { new[] { "auto", "Auto" }, new[] { "es", "Español" }, new[] { "en", "English" } })
            {
                string id = option[0];
                panel.Children.Add(Chip(option[1], "idioma", id == (dock.Cfg.Language ?? "auto"), () => SetLanguage(dock, dock.Cfg, id)));
            }
            return panel;
        }

        static StackPanel CommunitySettings(Dock dock)
        {
            var cfg = dock.Cfg; var panel = new StackPanel();
            var key = new TextBox { Style = Res.Style("GlassText"), Height = 34, MinHeight = 34, Padding = new Thickness(9, 5, 9, 5), Text = cfg.SteamGridKey, Margin = new Thickness(0, 0, 0, 7) };
            Button save = null;
            Action store = delegate
            {
                cfg.SteamGridKey = (key.Text ?? string.Empty).Trim(); cfg.Save();
                bool present = cfg.SteamGridKey.Length > 0;
                save.Content = Lang.T(present ? "Clave guardada" : "Clave vaciada");
                Log.Write(present ? "SteamGridDB: clave guardada en el config" : "SteamGridDB: clave vaciada");
            };
            var help = Res.Label(Lang.T("Clave gratuita: Perfil → Preferencias → API en SteamGridDB. Cambia el arte desde «Iconos de la comunidad» en el menú de cada icono."), 11, "PanelSub");
            help.TextWrapping = TextWrapping.Wrap; help.Margin = new Thickness(0, 0, 0, 10);
            var buttons = new WrapPanel(); buttons.Children.Add(save = Pill(Lang.T("Guardar la clave"), store));
            buttons.Children.Add(Pill(Lang.T("Abrir la página de la clave"), () => Launcher.OpenUrl(SteamGrid.PaginaClave)));
            key.KeyDown += (sender, args) => { if (args.Key == System.Windows.Input.Key.Enter) { args.Handled = true; store(); } };
            panel.Children.Add(Field(Lang.T("Clave de SteamGridDB"), key, 0)); panel.Children.Add(help); panel.Children.Add(buttons);
            return panel;
        }

        static UniformGrid SystemSettings(Dock dock)
        {
            var cfg = dock.Cfg; var panel = new UniformGrid { Columns = 2 };
            Button scan = null, import = null, restore = null, startup = null, update = null;
            panel.Children.Add(scan = Pill(Lang.T("Reescanear juegos"), delegate { dock.Rebuild(); scan.Content = Lang.F("Reescaneados: {0}", dock.Cat.Items.Count); }));
            panel.Children.Add(import = Pill(Lang.T("Vaciar escritorio al dock"), delegate
            {
                if (Desktop.Import(cfg)) { dock.Rebuild(); restore.Content = Lang.T("Todo devuelto"); }
            }));
            panel.Children.Add(restore = Pill(Lang.T("Devolver al escritorio"), delegate
            {
                Desktop.Restore(cfg); dock.Rebuild(); import.Content = Lang.T("Vaciar escritorio al dock");
            }));
            panel.Children.Add(startup = Pill(AutostartLabel(cfg), delegate
            {
                bool enabled = !cfg.Autostart;
                if (Autostart.Set(enabled))
                {
                    cfg.Autostart = enabled;
                    cfg.Save();
                    startup.Content = AutostartLabel(cfg);
                }
                else Veil.Ask(_open ?? dock.Win, Lang.T("No se ha podido cambiar el inicio con Windows. Consulta dock.log."),
                    "GamesDock", MessageBoxButton.OK, MessageBoxImage.Warning);
            }));
            panel.Children.Add(Pill(Lang.T("Abrir carpeta de ajustes"), () => Launcher.OpenFolder(Paths.Root)));
            panel.Children.Add(update = Pill(Lang.T("Buscar actualizaciones"), () => BuscarActualizaciones(dock, update)));
            return panel;
        }

        static StackPanel HelpSettings()
        {
            var panel = new StackPanel();
            var gestures = Res.Label(Lang.T("Arrastra un .lnk, .exe o un enlace steam:// sobre la barra para fijarlo. " +
                "Arrastra un icono para ordenar esta página; suéltalo fuera para quitarlo solo de aquí. " +
                "Clic derecho: favoritos, renombrar, iconos de la comunidad o añadir a otra página. Clic central: favorito."), 10.5, "PanelSub");
            gestures.TextWrapping = TextWrapping.Wrap; gestures.Margin = new Thickness(0, 0, 0, 10); panel.Children.Add(gestures);
            var invitation = Res.Label(Lang.T("¿Te gusta la barra? Apoya el proyecto con una estrella o un café."), 10.5, "PanelSub");
            invitation.TextWrapping = TextWrapping.Wrap; panel.Children.Add(invitation);
            var support = new Grid();
            support.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            support.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var star = Pill(Lang.T("Estrella en GitHub"), () => Launcher.OpenUrl(Update.PaginaRepo));
            var coffee = Pill(Lang.T("invítame a un café"), () => Launcher.OpenUrl(Update.PaginaDonacion));
            coffee.ToolTip = Update.PaginaDonacion;
            support.Children.Add(star); Grid.SetColumn(coffee, 1); support.Children.Add(coffee);
            panel.Children.Add(support);
            return panel;
        }
    }
}
