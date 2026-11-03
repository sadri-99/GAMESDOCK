using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GamesDock;

static class ThemeValidation
{
    static int _checks;
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        _checks++;
    }
    static void Save(Visual visual, int width, int height, string path)
    {
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(path)) png.Save(stream);
    }
    [STAThread]
    static int Main(string[] args)
    {
        try { Run(args); return 0; }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    static void Run(string[] args)
    {
        string output = args[0]; Directory.CreateDirectory(output);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(XamlAssets.LoadDictionary("styles"));
        Check(Themes.All.Count == 15, "Expected 9 existing and 6 new themes");
        var baseline = Assembly.LoadFrom(args[1]).GetType("GamesDock.PillGeometry").GetMethod("Build");
        foreach (var entry in Themes.All)
        {
            foreach (var color in entry.Value.C) Check(Themes.Br(color.Value).IsFrozen, entry.Key + " palette");
            foreach (int size in new[] { 32, 46, 80 })
            foreach (bool vertical in new[] { false, true })
            foreach (double growth in new[] { 0.0, 0.5, 1.0 })
            foreach (double length in new[] { 150.0, 720.0 })
            {
                double half = (size + 16) / 2.0, uniform = growth * 10, amplitude = growth * 12;
                double center = half + 30, pointer = length / 2;
                var geometry = PillGeometry.Build(length, center, half, uniform, amplitude,
                    pointer, size + 16, 1.55, vertical, growth * 12, growth * 12, entry.Value.CornerRatio);
                Check(geometry.IsFrozen && !geometry.Bounds.IsEmpty, entry.Key + " geometry");
                Check(!double.IsNaN(geometry.Bounds.Width) && !double.IsInfinity(geometry.Bounds.Height), "Finite bounds");
                Check(geometry.FillContains(vertical ? new Point(center, pointer) : new Point(pointer, center)), "Solid center");
                if (entry.Value.Material == null)
                {
                    var old = (Geometry)baseline.Invoke(null, new object[] {length, center, half, uniform,
                        amplitude, pointer, (double)size + 16, 1.55, vertical, growth * 12, growth * 12});
                    Check(old.ToString(CultureInfo.InvariantCulture) == geometry.ToString(CultureInfo.InvariantCulture),
                        "Original contour changed: " + entry.Key);
                }
                var art = new MaterialArt();
                art.Update(entry.Value, geometry, length, center, half + uniform, vertical, pointer);
                art.Measure(new Size(1000, 1000)); art.Arrange(new Rect(art.DesiredSize));
                var render = new RenderTargetBitmap(900, 900, 96, 96, PixelFormats.Pbgra32);
                render.Render(art);
                Check(!art.IsHitTestVisible || art.InputHitTest(new Point(0, 0)) == null, "Overlay input");
            }
        }
        // Exercise actual theme switching and layout, without reading/saving user configuration.
        var config = new Config { IconSize = 64, IdleOpacity = 100, HoverCard = false };
        var window = XamlAssets.Load<Window>("window");
        window.ShowActivated = false;
        window.Opacity = 0;
        var dock = new GamesDock.Dock(window, config);
        window.Show();
        string[] names = { "Dawntrail", "Survival", "Counter-Strike 2", "RPG", "DayZ", "Action", "Fantasy", "Cyberpunk 2077" };
        for (int i = 0; i < names.Length; i++)
            dock.Items.Add(new ItemSpec { Key = "validation-" + i, Name = names[i], Kind = "game" });
        dock.RebuildStrip(false);
        foreach (var position in new[] { "bottom", "top", "left", "right" })
        foreach (var theme in Themes.All)
        {
            config.Position = position;
            dock.ApplyTheme(theme.Key);
            dock.ApplyLayout();
            window.Measure(new Size(window.Width, window.Height));
            window.Arrange(new Rect(0, 0, window.Width, window.Height));
            dock.RefreshPill();
            Check(dock.Tiles.Count == names.Length, "Tiles lost on theme switch");
            Check(((System.Windows.Shapes.Path)window.FindName("Glass")).Data != null, "Missing live contour");
        }
        var groupBuilder = typeof(Settings).GetMethod("ThemeGroups", BindingFlags.Static | BindingFlags.NonPublic);
        var checkUpdater = typeof(GamesDock.Dock).GetMethod("UpdateThemeChecks", BindingFlags.Instance | BindingFlags.NonPublic);
        foreach (string language in new[] { "es", "en" })
        {
            Lang.Apply(language);
            dock.InvalidateMenus();
            var menu = dock.DockMenu;
            var themeMenu = (MenuItem)menu.Items[3];
            Check(themeMenu.Items.Count == 2, "Exactly two theme submenus");
            var found = new System.Collections.Generic.HashSet<string>();
            int groupIndex = 0;
            foreach (MenuItem group in themeMenu.Items)
            {
                string groupId = Themes.GroupIds[groupIndex++];
                string label = language == "en" ? (groupId == "classic" ? "Classic" : "Animated")
                    : (groupId == "classic" ? "Clásicos" : "Animados");
                Check((string)group.Header == label, "Translated submenu title");
                Check(group.Items.Count == (groupId == "classic" ? 9 : 6), "Theme category size");
                foreach (MenuItem item in group.Items)
                {
                    string id = (string)item.Tag;
                    Check(found.Add(id) && Themes.Get(id).Group == groupId, "Unique theme in correct menu");
                }
            }
            Check(found.Count == 15, "All themes reachable");
            foreach (string selected in Themes.All.Keys)
            {
                config.Theme = selected;
                checkUpdater.Invoke(dock, null);
                int checkedMenus = 0;
                foreach (MenuItem group in themeMenu.Items)
                    foreach (MenuItem item in group.Items)
                    {
                        Check(item.IsChecked == ((string)item.Tag == selected), "Nested selection check");
                        if (item.IsChecked) checkedMenus++;
                    }
                Check(checkedMenus == 1, "Only one checked menu theme");
                var groups = (Grid)groupBuilder.Invoke(null, new object[] { dock });
                Check(groups.Children.Count == 2, "Exactly two visible theme families");
                int selectedChips = 0;
                foreach (StackPanel group in groups.Children)
                {
                    string groupId = (string)group.Tag;
                    Check(((TextBlock)group.Children[0]).Text == Lang.T(Themes.GroupLabel(groupId)), "Translated settings title");
                    Check(group.Visibility == Visibility.Visible, "Theme family is hidden");
                    var choices = (WrapPanel)group.Children[1];
                    Check(choices.Children.Count == (groupId == "classic" ? 9 : 6), "Settings category size");
                    foreach (RadioButton chip in choices.Children)
                    {
                        Check(Themes.Get((string)chip.Tag).Group == groupId, "Correct settings category");
                        if (chip.IsChecked == true) selectedChips++;
                    }
                }
                Check(selectedChips == 1, "Selected theme preserved in settings");
            }
            var preview = (Grid)groupBuilder.Invoke(null, new object[] { dock });
            preview.Width = 760;
            preview.Measure(new Size(760, double.PositiveInfinity));
            preview.Arrange(new Rect(preview.DesiredSize)); preview.UpdateLayout();
            Save(preview, 760, (int)Math.Ceiling(preview.ActualHeight), Path.Combine(output, "Categorias-" + language + ".png"));
        }
        Lang.Apply("es");
        // Contact sheet rendered by the production vector renderer with the repository's game art.
        var source = new BitmapImage(new Uri(Path.GetFullPath(args[2])));
        int[] cropX = { 68, 152, 236, 320, 404, 488, 572, 656 };
        var sheet = new DrawingVisual();
        using (var dc = sheet.RenderOpen())
        {
            dc.DrawRectangle(Themes.Br("#FF11171B"), null, new Rect(0, 0, 840, 1010));
            int row = 0;
            foreach (var entry in Themes.All)
            {
                if (entry.Value.Material == null) continue;
                double y = 22 + row * 164;
                var label = new FormattedText(entry.Value.Label, CultureInfo.GetCultureInfo("es"),
                    FlowDirection.LeftToRight, new Typeface("Segoe UI"), 15, Themes.Br("#FFE6ECEB"), 1);
                dc.DrawText(label, new Point(30, y));
                var geo = PillGeometry.Build(780, 52, 46, 0, 0, 390, 85, 1.55, false, 0, 0, entry.Value.CornerRatio);
                var panel = new Canvas { Width = 780, Height = 105 };
                panel.Children.Add(new System.Windows.Shapes.Path { Data = geo,
                    Fill = Themes.Grad(entry.Value.C["glass"], entry.Value.C["glassBot"]),
                    Stroke = Themes.Br(entry.Value.C["edge"]), StrokeThickness = 1 });
                var art = new MaterialArt { IsHitTestVisible = false };
                art.Update(entry.Value, geo, 780, 52, 46, false, 0);
                panel.Children.Add(art);
                for (int i = 0; i < cropX.Length; i++)
                {
                    var image = new Image { Source = new CroppedBitmap(source, new Int32Rect(cropX[i], 22, 62, 62)),
                        Width = 62, Height = 62, Clip = new RectangleGeometry(new Rect(0, 0, 62, 62), 13, 13) };
                    Canvas.SetLeft(image, 34 + i * 93); Canvas.SetTop(image, 21); panel.Children.Add(image);
                }
                panel.Measure(new Size(780, 105)); panel.Arrange(new Rect(0, 0, 780, 105)); panel.UpdateLayout();
                dc.PushTransform(new TranslateTransform(30, y + 31));
                dc.DrawRectangle(new VisualBrush(panel), null, new Rect(0, 0, 780, 105));
                dc.Pop(); row++;
            }
        }
        Save(sheet, 840, 1010, Path.Combine(output, "Temas-GamesDock.png"));
        window.Close();
        Console.WriteLine("PASS: " + _checks + " checks; 15 themes, legacy contours, 540 material states, 60 live layouts; grouped menus/settings in ES and EN.");
        app.Shutdown();
    }
}
