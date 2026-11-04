using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using GamesDock;

static class SettingsValidation
{
    static int _checks;
    static void Pump(int milliseconds)
    {
        var frame = new DispatcherFrame(); var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += delegate { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame);
    }
    static void Check(bool value, string reason) { if (!value) throw new Exception(reason); _checks++; }
    static IEnumerable<T> Find<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T) yield return (T)root;
        foreach (object child in LogicalTreeHelper.GetChildren(root))
            if (child is DependencyObject)
                foreach (T nested in Find<T>((DependencyObject)child)) yield return nested;
    }
    static void Save(FrameworkElement element, string path)
    {
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling((element.ActualWidth + 30) * 2), (int)Math.Ceiling((element.ActualHeight + 30) * 2), 192, 192, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(path)) encoder.Save(stream);
    }
    static void ValidateSection(Border section, HashSet<string> sectionIds)
    {
        string id = Convert.ToString(section.Tag);
        Check(sectionIds.Add(id), "Duplicated secondary category");
        Check(section.IsVisible && section.Child != null, "A category is hidden on entry");
    }
    [STAThread]
    static int Main(string[] args)
    {
        try { Run(args); return 0; } catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    static void Run(string[] args)
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(XamlAssets.LoadDictionary("styles"));
        Check(new Config().IconShape == "ios", "First-run icon shape is not iOS");
        Check(new Config().IdleOpacity == 100, "First-run opacity is not 100%");
        var normalize = typeof(Config).GetMethod("NormalizeAppearance", BindingFlags.Static | BindingFlags.NonPublic);
        foreach (string removed in new[] { "santuario", "ki", "hallownest", "survivor", "vice", "hearts", "pokeball" })
        {
            var previous = new Config { Theme = removed };
            normalize.Invoke(null, new object[] { previous });
            Check(!Themes.All.ContainsKey(removed) && previous.Theme == Themes.Fallback, "Removed theme not migrated: " + removed);
        }
        foreach (string shape in new[] { "ios", "defecto", "esfera", null, "unknown" })
        {
            var fixture = new Config { IconShape = shape, IdleOpacity = 20 };
            normalize.Invoke(null, new object[] { fixture });
            Check(fixture.IdleOpacity == 100, "Legacy opacity did not normalize to 100%");
            Check(fixture.IconShape == (shape == null || shape == "unknown" ? "ios" : shape), "Existing icon preference was lost");
        }
        var dockWindow = XamlAssets.Load<Window>("window"); dockWindow.Opacity = 0; dockWindow.ShowActivated = false;
        var dock = new GamesDock.Dock(dockWindow, new Config { Theme = "horizonte", IconSize = 46, IdleOpacity = 100 });
        dockWindow.Show();
        for (int i = 0; i < 5; i++) dock.Items.Add(new ItemSpec { Key = "settings-test-" + i, Name = "Game " + i, Kind = "game" });
        dock.RebuildStrip(false); dock.ApplyLayout(); dockWindow.Left = dockWindow.Top = -10000;
        var source = new BitmapImage(new Uri(Path.GetFullPath(args[1])));
        string fixtures = Path.Combine(Path.GetDirectoryName(typeof(SettingsValidation).Assembly.Location), "settings-fixtures");
        Directory.CreateDirectory(fixtures);
        int[] crops = { 68, 152, 236, 404, 656 };
        for (int i = 0; i < 5; i++)
        {
            var tile = (Border)dock.Tiles[i].Content;
            tile.Background = new ImageBrush(new CroppedBitmap(source, new Int32Rect(crops[i], 22, 62, 62)));
            tile.Child = null;
            string iconPath = Path.Combine(fixtures, "sample-" + i + ".png");
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(new CroppedBitmap(source, new Int32Rect(crops[i], 22, 62, 62))));
            using (var stream = File.Create(iconPath)) encoder.Save(stream);
            dock.Items[i].Icon = iconPath;
        }
        var build = typeof(Settings).GetMethod("BuildPanel", BindingFlags.Static | BindingFlags.NonPublic);
        foreach (string language in new[] { "es", "en" })
        {
            Lang.Apply(language);
            var panel = (GlassPanel)build.Invoke(null, new object[] { dock });
            panel.Win.Topmost = false; panel.Win.ShowActivated = false; panel.Win.Opacity = 0;
            panel.Win.WindowStartupLocation = WindowStartupLocation.Manual; panel.Win.Left = panel.Win.Top = -10000;
            panel.Win.Show(); panel.Win.UpdateLayout();
            Border hero = null; DockPreview preview = null; Grid all = null;
            foreach (var border in Find<Border>(panel.Win.Content as DependencyObject)) if (border.Name == "Customization") hero = border;
            Check(hero != null, "Missing primary customization area");
            foreach (var sample in Find<DockPreview>(hero)) preview = sample;
            Check(preview != null, "Missing interactive theme preview");
            foreach (var grid in Find<Grid>(panel.Win.Content as DependencyObject)) if (grid.Name == "AllSettings") all = grid;
            Check(all != null && all.Children.Count == 2, "All settings are not visible on entry");
            int scrollers = 0, expanders = 0, tabs = 0, dropdowns = 0;
            foreach (var unused in Find<TabControl>(panel.Win.Content as DependencyObject)) tabs++;
            foreach (var unused in Find<ComboBox>(panel.Win.Content as DependencyObject)) dropdowns++;
            Check(tabs == 0 && dropdowns == 0, "Settings introduced tabs or dropdowns");
            foreach (var scroll in Find<ScrollViewer>(panel.Win.Content as DependencyObject))
            { scrollers++; Check(Convert.ToString(scroll.Tag) == "theme-gallery", "Scrolling escaped the theme gallery"); }
            foreach (var unused in Find<Expander>(panel.Win.Content as DependencyObject)) expanders++;
            Check(scrollers == 1 && expanders == 0, "Only the theme gallery may scroll");
            int motion = 0, magnify = 0, shapes = 0, testButtons = 0, costLabels = 0;
            foreach (var check in Find<CheckBox>(hero))
            {
                if (Convert.ToString(check.Content) == Lang.T("Animaciones de los temas")) motion++;
                if (Convert.ToString(check.Content) == Lang.T("Agrandar los iconos al pasar el ratón")) magnify++;
            }
            foreach (var radio in Find<RadioButton>(hero)) if (radio.GroupName == "forma") shapes++;
            Check(motion == 1 && magnify == 1, "Animation controls are not grouped with themes");
            Check(shapes == 3, "Shape combinations are missing");
            foreach (var button in Find<Button>(hero))
                if (Convert.ToString(button.Tag) == "animation-cost")
                {
                    costLabels++;
                    Check(button.ActualWidth > 150, "Resource label is squeezed into an icon-sized button");
                    var label = (TextBlock)button.Content;
                    Check(label.ActualHeight <= button.ActualHeight && label.Text.Contains("RAM"), "Resource label is clipped");
                }
            Check(costLabels == 1, "Missing animation CPU/memory notice");
            Check(PerformanceInfo.Description.Contains("GPU") && PerformanceInfo.Description.Contains("FPS"), "Cost notice omits measurement limits");
            foreach (var text in Find<TextBlock>(panel.Win.Content as DependencyObject))
                Check(text.Text != "Opacidad en reposo" && text.Text != "Idle opacity", "Removed opacity control remains visible");
            var steppers = (IDictionary)typeof(Settings).GetField("Steppers", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            Check(!steppers.Contains("idleOpacity") && steppers.Contains("iconSize"), "Opacity stepper remains registered");
            foreach (var button in Find<Button>(preview))
            {
                if (AutomationProperties.GetName(button) != Lang.T("Probar animación")) continue;
                testButtons++; int usage = dock.Cfg.Usage.Count;
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(dock.Cfg.Usage.Count == usage, "Preview launched a real application");
            }
            Check(testButtons == 5, "Missing preview sample buttons");
            double top = hero.TranslatePoint(new Point(), panel.Body).Y;
            double moreTop = all.TranslatePoint(new Point(), panel.Body).Y;
            Check(top < moreTop, "Personalization is not visually primary");
            Check(hero.ActualWidth > 900 && hero.ActualHeight > 200, "Compact personalization layout collapsed");
            Check(panel.Win.ActualHeight <= SystemParameters.WorkArea.Height && panel.Win.ActualWidth <= SystemParameters.WorkArea.Width,
                "Settings window exceeds the desktop work area");
            foreach (string theme in new[] { "nostromo", "camara", "terminal" })
            {
                dock.ApplyTheme(theme);
                foreach (bool enabled in new[] { false, true })
                {
                    dock.Cfg.Magnify = enabled; dock.Cfg.ThemeMotion = enabled; dock.Cfg.IconShape = enabled ? "ios" : "esfera";
                    dock.NotifyAppearanceChanged(); panel.Win.UpdateLayout();
                    var material = (MaterialArt)typeof(DockPreview).GetField("_material", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(preview);
                    Check(material.MotionEnabled == enabled, "Preview motion setting is stale");
                    var current = (ThemeDef)typeof(MaterialArt).GetField("_theme", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(material);
                    Check(ReferenceEquals(current, Themes.Get(theme)), "Preview theme is stale");
                }
            }
            dock.Cfg.ThemeMotion = true; dock.Cfg.Magnify = true; dock.Cfg.IconShape = "ios";
            dock.ApplyTheme("horizonte"); dock.NotifyAppearanceChanged(); panel.Win.UpdateLayout();
            Pump(350);
            foreach (var tile in Find<Button>(preview))
                if (tile.Content is Border) Check(((Border)tile.Content).Background is ImageBrush, "Preview artwork was lost during theme changes: " + language);
            ScrollViewer gallery = null;
            foreach (var scroll in Find<ScrollViewer>(hero)) gallery = scroll;
            Check(gallery != null && gallery.ExtentHeight > 0, "Theme gallery is empty");
            Point fixedOrigin = all.TranslatePoint(new Point(), panel.Win);
            double fixedHeight = panel.Win.ActualHeight;
            foreach (var chip in Find<RadioButton>(gallery))
            {
                chip.BringIntoView(); panel.Win.UpdateLayout();
                Rect chipBounds = chip.TransformToAncestor(gallery).TransformBounds(new Rect(chip.RenderSize));
                Check(chipBounds.Top >= -1 && chipBounds.Bottom <= gallery.ViewportHeight + 1, "Theme cannot be reached by local scroll");
                Check(all.TranslatePoint(new Point(), panel.Win) == fixedOrigin && panel.Win.ActualHeight == fixedHeight, "Theme scroll moved the settings window");
            }
            Save((FrameworkElement)panel.Win.Content, Path.Combine(args[0], "Ajustes-Temas-nuevos-" + language + ".png"));
            gallery.ScrollToTop(); panel.Win.UpdateLayout();
            Save((FrameworkElement)panel.Win.Content, Path.Combine(args[0], "Ajustes-Personalizacion-" + language + ".png"));
            var sectionIds = new HashSet<string>();
            foreach (Border section in Find<Border>(all))
            {
                if (!(section.Tag is string)) continue;
                ValidateSection(section, sectionIds);
            }
            Check(sectionIds.SetEquals(new[] { "bar", "behavior", "language", "community", "system", "help" }), "Secondary categories lost functionality");
            foreach (string option in new[] { "Ficha un 15 % más grande", "Mostrar mi actividad de Steam" })
            {
                CheckBox toggle = null;
                foreach (var box in Find<CheckBox>(all)) if (Convert.ToString(box.Content) == Lang.T(option)) toggle = box;
                Check(toggle != null, "Missing card option: " + option);
                toggle.IsChecked = true;
                Check(option.StartsWith("Ficha") ? dock.Cfg.HoverCardLarge : dock.Cfg.SteamPersonalData, "Card option did not enable");
                toggle.IsChecked = false;
                Check(option.StartsWith("Ficha") ? !dock.Cfg.HoverCardLarge : !dock.Cfg.SteamPersonalData, "Card option did not disable");
            }
            var labels = new HashSet<string>();
            foreach (var button in Find<Button>(all)) labels.Add(Convert.ToString(button.Content));
            foreach (string action in new[] { "Guardar la clave", "Abrir la página de la clave", "Reescanear juegos", "Vaciar escritorio al dock",
                "Devolver al escritorio", "Abrir carpeta de ajustes", "Buscar actualizaciones", "Estrella en GitHub" })
                Check(labels.Contains(Lang.T(action)), "Missing action: " + action);
            Pump(100); panel.Win.UpdateLayout();
            Check(panel.Tit.TranslatePoint(new Point(), panel.Win).Y >= 0, "Settings header scrolls out of reach");
            foreach (var control in Find<System.Windows.Controls.Primitives.ButtonBase>(panel.Win.Content as DependencyObject))
            {
                var themeChip = control as RadioButton;
                if (themeChip != null && themeChip.GroupName == "tema") continue;
                Rect bounds = control.TransformToAncestor(panel.Win).TransformBounds(new Rect(control.RenderSize));
                Check(control.IsVisible && bounds.Left >= 0 && bounds.Top >= 0 && bounds.Right <= panel.Win.ActualWidth + 1
                    && bounds.Bottom <= panel.Win.ActualHeight + 1, "A settings control is clipped or hidden: " + control);
            }
            var persistent = new List<Button>((List<Button>)typeof(DockPreview).GetField("_tiles", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(preview));
            for (int repeat = 0; repeat < 20; repeat++) preview.Refresh();
            var updated = (List<Button>)typeof(DockPreview).GetField("_tiles", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(preview);
            for (int i = 0; i < persistent.Count; i++) Check(ReferenceEquals(persistent[i], updated[i]), "Preview rebuilds sample controls");
            var inside = typeof(DockPreview).GetField("_inside", BindingFlags.Instance | BindingFlags.NonPublic);
            var animatePointer = typeof(DockPreview).GetMethod("AnimatePointer", BindingFlags.Instance | BindingFlags.NonPublic);
            var animateProgress = typeof(DockPreview).GetMethod("AnimateProgress", BindingFlags.Instance | BindingFlags.NonPublic);
            inside.SetValue(preview, true); animateProgress.Invoke(preview, new object[] { 1.0 }); animatePointer.Invoke(preview, new object[] { 130.0 });
            Pump(900);
            Check(preview.Progress > .99 && Math.Abs(preview.Pointer - 130) < .1, "Preview springs do not settle");
            Check(!(bool)typeof(DockPreview).GetField("_frameActive", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(preview), "Preview keeps its update loop running after settling");
            inside.SetValue(preview, false); animateProgress.Invoke(preview, new object[] { 0.0 }); Pump(900);
            Check(preview.Progress < .001, "Preview does not return to idle");
            Save((FrameworkElement)panel.Win.Content, Path.Combine(args[0], "Ajustes-Organizados-" + language + ".png"));
            panel.Win.Close();
        }
        dockWindow.Close(); Anim.Stop(); app.Shutdown();
        Console.WriteLine("PASS: " + _checks + " settings checks; ES/EN fixed controls and local theme scrolling, reusable preview, spring lifecycle and defaults.");
    }
}
