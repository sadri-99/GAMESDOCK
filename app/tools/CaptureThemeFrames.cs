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

static class CaptureThemeFrames
{
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly MethodInfo Step = typeof(Anim).GetMethod("Step", BindingFlags.Static | BindingFlags.NonPublic);
    static Spring Spring(GamesDock.Dock dock, string name) { return (Spring)typeof(GamesDock.Dock).GetField(name, Private).GetValue(dock); }
    static List<Spring> Tiles(GamesDock.Dock dock) { return (List<Spring>)typeof(GamesDock.Dock).GetField("MagSpr", Private).GetValue(dock); }
    static MaterialArt Art(GamesDock.Dock dock) { return (MaterialArt)typeof(GamesDock.Dock).GetField("Material", Private).GetValue(dock); }
    static void Pump(int ms)
    {
        var frame = new DispatcherFrame(); var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        timer.Tick += delegate { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame);
    }
    static void Settle(GamesDock.Dock dock)
    {
        foreach (string name in new[] { "PtrSpr", "HoverSpr" })
        {
            var spring = Spring(dock, name); spring.X = spring.Target; spring.V = 0; spring.Apply(spring);
        }
        foreach (var spring in Tiles(dock)) { spring.X = spring.Target; spring.V = 0; spring.Apply(spring); }
        dock.RefreshPill();
    }
    static void Advance(GamesDock.Dock dock)
    {
        for (int i = 0; i < 18; i++)
        {
            Step.Invoke(null, new object[] { Spring(dock, "PtrSpr"), .004 });
            Step.Invoke(null, new object[] { Spring(dock, "HoverSpr"), .004 });
            foreach (var spring in Tiles(dock)) Step.Invoke(null, new object[] { spring, .004 });
        }
        dock.RefreshPill();
    }
    static Point Center(GamesDock.Dock dock, int index)
    {
        var tile = dock.Tiles[index];
        return tile.TranslatePoint(new Point(tile.ActualWidth / 2, tile.ActualHeight / 2), dock.RootGrid);
    }
    static List<BitmapSource> LoadArtwork(string fallback)
    {
        var images = new List<BitmapSource>();
        // Read existing game art only; never save configuration or include profile paths in output.
        var config = Config.Load();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pin in config.Pins)
        {
            if (pin.Hidden || pin.Kind == "app") continue;
            try
            {
                string path = Icons.Resolve(ItemSpec.FromPin(pin));
                if (string.IsNullOrEmpty(path) || !File.Exists(path) || !seen.Add(path)) continue;
                var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(Path.GetFullPath(path)); image.EndInit(); image.Freeze();
                if (image.PixelWidth >= 128 && image.PixelHeight >= 128) images.Add(image);
                if (images.Count == 8) return images;
            }
            catch { }
        }
        var source = new BitmapImage(new Uri(Path.GetFullPath(fallback)));
        int[] crops = { 68, 152, 236, 320, 404, 488, 572, 656 };
        for (int i = images.Count; i < 8; i++) images.Add(new CroppedBitmap(source, new Int32Rect(crops[i], 22, 62, 62)));
        return images;
    }

    static GamesDock.Dock Create(string theme, List<BitmapSource> images)
    {
        var window = XamlAssets.Load<Window>("window");
        window.Opacity = 0; window.ShowActivated = false;
        window.Left = -10000; window.Top = -10000;
        var config = new Config { Theme = theme, IconSize = 64, IconShape = "ios", IdleOpacity = 100,
            Magnify = true, ThemeMotion = true, HoverCard = false };
        var dock = new GamesDock.Dock(window, config);
        window.Show();
        for (int i = 0; i < images.Count; i++)
            dock.Items.Add(new ItemSpec { Key = "capture-" + i, Name = "Game " + i, Kind = "game" });
        dock.RebuildStrip(false); dock.ApplyLayout(); window.UpdateLayout(); dock.RefreshPill();
        for (int i = 0; i < images.Count; i++)
        {
            var content = (Border)dock.Tiles[i].Content;
            content.Background = new ImageBrush(images[i]) { Stretch = Stretch.UniformToFill };
            content.Child = null; content.CornerRadius = new CornerRadius(13);
        }
        Art(dock).StopMotion(); Anim.Stop(); return dock;
    }

    static void Pose(GamesDock.Dock dock, int frame)
    {
        // Capture the production spring integrator and vector renderers at a repeatable timeline.
        var art = Art(dock);
        if (frame < 54)
        {
            double t = Math.Min(1, frame / 42.0); t = t * t * (3 - 2 * t);
            Point from = Center(dock, 1), to = Center(dock, 5);
            dock.SetPillHover(true);
            dock.UpdateProximity(new Point(from.X + (to.X - from.X) * t, from.Y));
        }
        if (frame == 54)
        {
            SagaTileMotion.Launch(dock.Tiles[5], Themes.Get(dock.Cfg.Theme).Material, false);
            art.Launch(Center(dock, 5).X);
            art.BeginAnimation(MaterialArt.EventPhaseProperty, null);
        }
        if (frame == 80) { dock.ResetProximity(); dock.SetPillHover(false); }
        Anim.Stop(); Advance(dock); Anim.Stop();
        art.BeginAnimation(MaterialArt.AmbientPhaseProperty, null);
        art.BeginAnimation(MaterialArt.InteractionLevelProperty, null);
        art.AmbientPhase = frame * .08 / ThemeMotionProfile.For(Themes.Get(dock.Cfg.Theme).Material).Cycle % 1;
        art.InteractionLevel = frame < 80 ? 1 : Math.Max(0, 1 - (frame - 80) / 10.0);
        art.EventPhase = frame >= 54 && frame < 76 ? (frame - 54) / 22.0 : -1;
        dock.RefreshPill();
    }

    static void Text(DrawingContext dc, string value, double x, double y, double size, string color)
    {
        var text = new FormattedText(value, CultureInfo.GetCultureInfo("es"), FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), size, Themes.Br(color), 2);
        dc.DrawText(text, new Point(x, y));
    }
    static void DrawDock(DrawingContext dc, GamesDock.Dock dock, Rect destination)
    {
        var brush = new VisualBrush(dock.RootGrid)
        {
            ViewboxUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(-34, -34, dock.RootGrid.ActualWidth + 68, dock.RootGrid.ActualHeight + 68),
            Stretch = Stretch.Uniform
        };
        dc.DrawRectangle(brush, null, destination);
    }
    static void Save(DrawingVisual visual, int width, int height, string path)
    {
        // 192 dpi rerenders WPF text and vectors at 2x. No enlargement of a low-resolution screenshot.
        var image = new RenderTargetBitmap(width * 2, height * 2, 192, 192, PixelFormats.Pbgra32);
        image.Render(visual);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(image));
        using (var file = File.Create(path)) png.Save(file);
    }
    [STAThread]
    static int Main(string[] args)
    {
        try
        {
            Directory.CreateDirectory(args[0]);
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources.MergedDictionaries.Add(XamlAssets.LoadDictionary("styles"));
            Lang.Apply("es");
            var images = LoadArtwork(args[1]);
            var demos = new List<GamesDock.Dock>();
            foreach (var entry in Themes.InGroup("animated")) demos.Add(Create(entry.Key, images));
            string board = Path.Combine(args[0], "animated"); Directory.CreateDirectory(board);
            for (int frame = 0; frame < 96; frame++)
            {
                foreach (var demo in demos) Pose(demo, frame);
                Pump(1); Anim.Stop();
                var visual = new DrawingVisual();
                using (var dc = visual.RenderOpen())
                {
                    dc.DrawRectangle(Themes.Br("#FF11171B"), null, new Rect(0, 0, 840, 1020));
                    for (int i = 0; i < demos.Count; i++)
                    {
                        var demo = demos[i];
                        Text(dc, Themes.Get(demo.Cfg.Theme).Label, 28, 10 + i * 168, 16, "#FFE6ECEB");
                        DrawDock(dc, demo, new Rect(8, 34 + i * 168, 824, 130));
                    }
                }
                Save(visual, 840, 1020, Path.Combine(board, frame.ToString("D4") + ".png"));
            }
            foreach (var demo in demos) { Art(demo).StopMotion(); demo.Win.Close(); }
            string carousel = Path.Combine(args[0], "all-themes"); Directory.CreateDirectory(carousel);
            int number = 0;
            foreach (var entry in Themes.All)
            {
                var demo = Create(entry.Key, images);
                for (int frame = 0; frame < 24; frame++)
                {
                    Pose(demo, frame * 4); Pump(1); Anim.Stop();
                    var visual = new DrawingVisual();
                    using (var dc = visual.RenderOpen())
                    {
                        dc.DrawRectangle(Themes.Br("#FF11171B"), null, new Rect(0, 0, 960, 270));
                        Text(dc, "GAMESDOCK", 40, 22, 12, "#FF91A5A9");
                        Text(dc, entry.Value.Label, 40, 47, 26, "#FFE6ECEB");
                        Text(dc, (number / 24 + 1).ToString("D2") + " / 15", 850, 49, 16, "#FF91A5A9");
                        DrawDock(dc, demo, new Rect(32, 100, 896, 155));
                    }
                    Save(visual, 960, 270, Path.Combine(carousel, (number++).ToString("D4") + ".png"));
                }
                Art(demo).StopMotion(); demo.Win.Close();
            }
            Anim.Stop(); app.Shutdown();
            Console.WriteLine("Captured 96 frames at 1680x2040 and 360 frames at 1920x540 from production WPF docks.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
