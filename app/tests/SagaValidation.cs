using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using GamesDock;

static class SagaValidation
{
    static int checks;
    static readonly string[] Ids = { "nostromo", "camara", "memory", "horizonte", "terminal", "nightcity", "raccoon", "codec", "crystal", "inferno", "strand" };
    sealed class Scene { public MaterialArt Art; public List<Button> Tiles = new List<Button>(); public ThemeDef Theme; public Rect Focus; }
    static void Check(bool value, string reason) { if (!value) throw new Exception(reason); checks++; }
    static void Pump(int ms)
    {
        var frame = new DispatcherFrame(); var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        timer.Tick += delegate { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame);
    }
    static RenderTargetBitmap Render(Visual visual)
    { var b = new RenderTargetBitmap(1680, (int)Math.Ceiling(((FrameworkElement)visual).ActualHeight * 2), 192, 192, PixelFormats.Pbgra32); b.Render(visual); b.Freeze(); return b; }
    static void Save(BitmapSource bitmap, string file)
    { var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using (var stream = File.Create(file)) png.Save(stream); }
    static Scene Build(Canvas root, int row, string id, BitmapSource source)
    {
        var theme = Themes.Get(id); double y = row * 170 + 16;
        var title = new TextBlock { Text = theme.Label, FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = Themes.Br("#FFE8ECEE") };
        Canvas.SetLeft(title, 30); Canvas.SetTop(title, y); root.Children.Add(title);
        var layout = new Layout(new Config { Theme = id, IconSize = 64 });
        double length = layout.RowLen(8) + 2 * layout.RowInset, focusX = layout.TileCenter(2, 8, 0);
        var panel = new Canvas { Width = length, Height = 138 };
        Canvas.SetLeft(panel, (840 - length) / 2); Canvas.SetTop(panel, y + 23); root.Children.Add(panel);
        var geometry = PillGeometry.Build(length, 66, 46, 5, 7, focusX, 90, ThemeMotionProfile.For(theme.Material).Peak, false, 0, 0, theme.CornerRatio);
        panel.Children.Add(new System.Windows.Shapes.Path { Data = geometry, Fill = Themes.Grad(theme.C["glass"], theme.C["glassBot"]), Stroke = Themes.Br(theme.C["edge"]), StrokeThickness = 1 });
        var art = new MaterialArt { MotionEnabled = true, IsHitTestVisible = false };
        art.Update(theme, geometry, length, 66, 51, false, focusX); panel.Children.Add(art);
        var scene = new Scene { Art = art, Theme = theme, Focus = new Rect(focusX - 40, 26, 80, 80) };
        int[] crop = { 68, 152, 236, 320, 404, 488, 572, 656 };
        for (int i = 0; i < 8; i++)
        {
            double size = i == 2 ? 80 : 64;
            var tile = new Button { Width = size, Height = size, Padding = new Thickness(), BorderThickness = new Thickness(), Background = Brushes.Transparent,
                Content = new Border { Width = size, Height = size, CornerRadius = new CornerRadius(13), Background = new ImageBrush(new CroppedBitmap(source, new Int32Rect(crop[i], 22, 62, 62))) } };
            Canvas.SetLeft(tile, layout.TileCenter(i, 8, 0) - size / 2); Canvas.SetTop(tile, 66 - size / 2); panel.Children.Add(tile); scene.Tiles.Add(tile);
        }
        panel.Children.Add(art.Overlay);
        return scene;
    }
    [STAThread] static int Main(string[] args)
    {
        try { Run(args); Console.WriteLine("PASS: " + checks + " animated saga checks; 11 signatures, profiles, localization and live animation reels."); return 0; }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    static void Run(string[] args)
    {
        Directory.CreateDirectory(args[0]);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(XamlAssets.LoadDictionary("styles"));
        var window = new Window { WindowStyle = WindowStyle.None, Width = 840, Height = 1040, ShowActivated = false, ShowInTaskbar = false, Opacity = 0, Left = -10000, Top = -10000 };
        window.Show();
        var source = new BitmapImage(new Uri(Path.GetFullPath(args[1])));
        var materials = new HashSet<string>();
        foreach (string id in Ids)
        {
            var theme = Themes.Get(id);
            Check(theme != Themes.Get(Themes.Fallback) && theme.Group == "animated", "Missing saga: " + id);
            Check(materials.Add(theme.Material), "Shared saga renderer");
            Check(!ThemeMotionProfile.For(theme.Material).Classic && ThemeMotionProfile.For(theme.Material).Lift <= 5, "Invalid saga profile");
            Lang.Apply("en"); Check(Lang.T(theme.MotionHint) != theme.MotionHint, "Untranslated saga hint");
            Lang.Apply("es");
        }
        // Neutralize palette and contour so a recolor of one template cannot pass as a new design.
        var signatures = new List<byte[]>();
        foreach (string id in Ids)
        {
            var original = Themes.Get(id);
            var neutral = new ThemeDef { Material = original.Material, CornerRatio = .4 };
            foreach (var color in original.C) neutral.C[color.Key] = "#FFBAC5C8";
            var geometry = PillGeometry.Build(650, 50, 44, 0, 0, 325, 90, 1.3, false, 0, 0, .4);
            var art = new MaterialArt { MotionEnabled = true };
            art.Update(neutral, geometry, 650, 50, 44, false, 325);
            art.SetFocus(new Rect(293, 18, 64, 64), 12, 1);
            art.InteractionLevel = 1; art.EventPhase = .43;
            typeof(MaterialArt).GetField("_eventPosition", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(art, 325.0);
            art.Measure(new Size(650, 100)); art.Arrange(new Rect(0, 0, 650, 100));
            art.Overlay.Measure(new Size(650, 100)); art.Overlay.Arrange(new Rect(0, 0, 650, 100));
            var image = new RenderTargetBitmap(650, 100, 96, 96, PixelFormats.Pbgra32);
            image.Render(art); image.Render(art.Overlay);
            byte[] rgba = new byte[650 * 100 * 4], mask = new byte[650 * 100];
            image.CopyPixels(rgba, 650 * 4, 0);
            for (int pixel = 0; pixel < mask.Length; pixel++) mask[pixel] = rgba[pixel * 4 + 3] > 32 ? (byte)1 : (byte)0;
            foreach (byte[] previous in signatures)
            {
                int different = 0;
                for (int pixel = 0; pixel < mask.Length; pixel++) if (previous[pixel] != mask[pixel]) different++;
                Check(different > 150, "Saga composition collapsed to another template: " + id);
            }
            signatures.Add(mask);
            art.SetFocus(Rect.Empty, 0, 0);
            typeof(MaterialArt).GetField("_launchEvent", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(art, true);
            art.Overlay.InvalidateVisual(); art.Overlay.UpdateLayout();
            var keyboard = new RenderTargetBitmap(650, 100, 96, 96, PixelFormats.Pbgra32); keyboard.Render(art.Overlay);
            byte[] keyboardPixels = new byte[650 * 100 * 4]; keyboard.CopyPixels(keyboardPixels, 650 * 4, 0);
            int visible = 0; for (int pixel = 3; pixel < keyboardPixels.Length; pixel += 4) if (keyboardPixels[pixel] > 16) visible++;
            Check(visible > 10, "Launch story requires a previously hovered icon: " + id);
            art.StopMotion();
        }
        var export = Assembly.LoadFrom(Path.Combine(Path.GetDirectoryName(typeof(SagaValidation).Assembly.Location), "MotionValidation.exe"))
            .GetType("MotionValidation").GetMethod("SaveGif", BindingFlags.Static | BindingFlags.NonPublic);
        for (int page = 0; page < (Ids.Length + 5) / 6; page++)
        {
            var root = new Canvas { Width = 840, Height = Math.Min(6, Ids.Length - page * 6) * 170 + 20, Background = Themes.Br("#FF10171D") };
            var scenes = new List<Scene>();
            for (int row = 0; row < 6 && page * 6 + row < Ids.Length; row++) scenes.Add(Build(root, row, Ids[page * 6 + row], source));
            window.Content = root; window.UpdateLayout();
            foreach (var scene in scenes)
            {
                scene.Art.StopMotion(); scene.Art.SetFocus(scene.Focus, 13, 1);
                scene.Art.InteractionLevel = 1; scene.Art.EventPhase = .43;
                typeof(MaterialArt).GetField("_eventPosition", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(scene.Art, scene.Focus.X + 40);
            }
            root.UpdateLayout(); Save(Render(root), Path.Combine(args[0], "Sagas-" + (page + 1) + ".png"));
            foreach (var scene in scenes) { scene.Art.StopMotion(); scene.Art.SetInteraction(true); }
            var gif = new GifBitmapEncoder();
            for (int frame = 0; frame < 48; frame++)
            {
                foreach (var scene in scenes)
                {
                    if (frame == 8) { scene.Art.Launch(scene.Focus.X + 40); SagaTileMotion.Launch(scene.Tiles[2], scene.Theme.Material, false); }
                    if (frame == 41) { scene.Art.SetInteraction(false); scene.Art.SetFocus(Rect.Empty, 0, 0); }
                    if (frame < 41) SagaTileMotion.Hover(scene.Tiles[2], scene.Theme.Material, 1, .2, false, 1, 64);
                    if (frame == 10) Check(scene.Art.HasAnimatedProperties, "Launch did not animate saga: " + scene.Theme.Label);
                }
                Pump(60); root.UpdateLayout(); gif.Frames.Add(BitmapFrame.Create(Render(root)));
            }
            export.Invoke(null, new object[] { gif, Path.Combine(args[0], "Sagas-" + (page + 1) + ".gif") });
            foreach (var scene in scenes)
            {
                scene.Art.MotionEnabled = false; scene.Art.SetInteraction(true); scene.Art.Launch(scene.Focus.X + 40);
                Check(!scene.Art.HasAnimatedProperties, "Disabled saga restarted its clocks");
                foreach (var tile in scene.Tiles) SagaTileMotion.Stop(tile);
            }
        }
        window.Close(); app.Shutdown();
    }
}
