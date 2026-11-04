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

static class ProMotionValidation
{
    static int _checks;
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly MethodInfo Step = typeof(Anim).GetMethod("Step", BindingFlags.Static | BindingFlags.NonPublic);
    static void Check(bool ok, string reason) { if (!ok) throw new Exception(reason); _checks++; }
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
    static bool Inside(Rect bounds, double width, double height)
    {
        return bounds.Left >= -1 && bounds.Top >= -1 && bounds.Right <= width + 1 && bounds.Bottom <= height + 1;
    }
    static GamesDock.Dock Create(string theme, int count)
    {
        var window = XamlAssets.Load<Window>("window"); window.Opacity = 0; window.ShowActivated = false;
        var dock = new GamesDock.Dock(window, new Config { Theme = theme, IconSize = 64, IconShape = "ios", IdleOpacity = 100,
            Magnify = true, ThemeMotion = true, HoverCard = false });
        window.Show();
        for (int i = 0; i < count; i++) dock.Items.Add(new ItemSpec { Key = theme + "-showcase-" + i, Name = "Game " + i, Kind = "game" });
        dock.RebuildStrip(false); dock.ApplyLayout(); window.UpdateLayout(); dock.RefreshPill();
        window.Left = -10000; window.Top = -10000; return dock;
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
        var dock = Create("nostromo", 3);
        var peaks = new HashSet<double>();
        foreach (var entry in Themes.InGroup("animated"))
        {
            var profile = ThemeMotionProfile.For(entry.Value.Material); peaks.Add(profile.Peak);
            Check(profile.Influence(0, 100) == 1 && profile.Influence(101, 100) == 0, "Invalid focus field endpoints");
            double previous = 1;
            for (int i = 1; i <= 100; i++)
            {
                double weight = profile.Influence(i, 100);
                Check(weight >= 0 && weight <= previous + 1e-9, "Focus field has a discontinuity or inverted neighbour");
                previous = weight;
            }
            foreach (string position in new[] { "bottom", "top", "left", "right" })
            foreach (int size in new[] { 32, 64, 96 })
            {
                dock.Cfg.Position = position; dock.Cfg.IconSize = size; dock.Cfg.Magnify = true;
                dock.ResetProximity(); dock.SetPillHover(false); Settle(dock);
                dock.ApplyTheme(entry.Key); dock.ApplyLayout(); dock.Win.UpdateLayout(); dock.RefreshPill();
                dock.Win.Left = -10000; dock.Win.Top = -10000;
                foreach (int selected in new[] { 0, 1, 2 })
                {
                    dock.UpdateProximity(Center(dock, selected)); dock.SetPillHover(true); Settle(dock);
                    var glass = (System.Windows.Shapes.Path)dock.Win.FindName("Glass");
                    Rect shell = glass.TransformToAncestor(dock.Win).TransformBounds(glass.Data.Bounds);
                    Check(Inside(shell, dock.Win.ActualWidth, dock.Win.ActualHeight),
                        "Expanded shell clips: " + entry.Key + "/" + position + "/" + size + "/" + selected + " " + shell);
                    if (profile.Locality == 0) Check(glass.Data is RectangleGeometry, "Square housing deforms into a capsule");
                    var tile = dock.Tiles[selected]; var content = (FrameworkElement)tile.Content;
                    Rect icon = content.TransformToAncestor(dock.Win).TransformBounds(new Rect(content.RenderSize));
                    Check(Inside(icon, dock.Win.ActualWidth, dock.Win.ActualHeight), "Enlarged artwork clips window");
                    if (selected == 0)
                    {
                        var original = new Layout(new Config { IconSize = size, Theme = "noche" }) { Scale = dock.Lay.Scale };
                        Check(Math.Abs(dock.Lay.RowInset - original.RowInset) < .001,
                            "Theme adds lateral padding: " + entry.Key + "/" + size + "/" + position);
                    }
                    Matrix before = content.RenderTransform.Value;
                    SagaTileMotion.Launch(tile, entry.Value.Material, dock.Lay.Vert, dock.Lay.Outward);
                    Matrix after = content.RenderTransform.Value;
                    Check(Math.Abs(before.M11 - after.M11) < .001 && Math.Abs(before.OffsetY - after.OffsetY) < .001,
                        "Click destroys the current hover pose");
                    SagaTileMotion.Stop(tile);
                    dock.Cfg.Magnify = false; dock.ResetProximity(); dock.ApplyLayout(); dock.RefreshPill();
                    Check(((ScaleTransform)tile.RenderTransform).ScaleX == 1 && content.RenderTransform.Value.IsIdentity,
                        "Disabling enlargement leaves residual pose");
                    dock.Cfg.Magnify = true; dock.ApplyLayout(); dock.RefreshPill();
                }
            }
        }
        Check(peaks.Count == 11, "All themes share the same magnification profile");
        dock.Win.Close(); Anim.Stop();

        // Actual docks, actual spring integrator, actual overlay and launch transforms; no separate mock renderer.
        var source = new BitmapImage(new Uri(Path.GetFullPath(args[1])));
        int[] crops = { 68, 152, 236, 320, 404, 488, 572, 656 };
        var demos = new List<GamesDock.Dock>();
        foreach (var entry in Themes.InGroup("animated"))
        {
            var demo = Create(entry.Key, 8);
            for (int i = 0; i < 8; i++)
            {
                var content = (Border)demo.Tiles[i].Content;
                content.Background = new ImageBrush(new CroppedBitmap(source, new Int32Rect(crops[i], 22, 62, 62)));
                content.Child = null; content.CornerRadius = new CornerRadius(13);
            }
            Art(demo).StopMotion(); demo.SetPillHover(true); demos.Add(demo);
        }
        Anim.Stop();
        var gif = new GifBitmapEncoder();
        for (int frame = 0; frame < 72; frame++)
        {
            foreach (var demo in demos)
            {
                if (frame < 27)
                {
                    double t = Math.Min(1, frame / 23.0); t = t * t * (3 - 2 * t);
                    Point from = Center(demo, 2), to = Center(demo, 4);
                    demo.UpdateProximity(new Point(from.X + (to.X - from.X) * t, from.Y));
                }
                if (frame == 27)
                {
                    SagaTileMotion.Launch(demo.Tiles[4], Themes.Get(demo.Cfg.Theme).Material, false);
                    Art(demo).Launch(Center(demo, 4).X);
                }
                if (frame == 56) { demo.ResetProximity(); demo.SetPillHover(false); }
                Anim.Stop(); Advance(demo); Anim.Stop();
            }
            Pump(70); Anim.Stop();
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(Themes.Br("#FF11171B"), null, new Rect(0, 0, 840, 1020));
                for (int i = 0; i < demos.Count; i++)
                {
                    var demo = demos[i]; demo.RefreshPill();
                    var label = new FormattedText(Themes.Get(demo.Cfg.Theme).Label + "  ·  " + (frame < 27 ? "Acercamiento y enfoque" : frame < 56 ? "Lanzamiento" : "Reposo"),
                        CultureInfo.GetCultureInfo("es"), FlowDirection.LeftToRight, new Typeface("Segoe UI"), 13, Themes.Br("#FFDDE5E6"), 1);
                    dc.DrawText(label, new Point(28, 12 + i * 168));
                    var brush = new VisualBrush(demo.RootGrid)
                    {
                        ViewboxUnits = BrushMappingMode.Absolute,
                        Viewbox = new Rect(-34, -34, demo.RootGrid.ActualWidth + 68, demo.RootGrid.ActualHeight + 68),
                        Stretch = Stretch.Uniform
                    };
                    dc.DrawRectangle(brush, null, new Rect(8, 29 + i * 168, 824, 137));
                }
            }
            var image = new RenderTargetBitmap(840, 1020, 96, 96, PixelFormats.Pbgra32); image.Render(visual); image.Freeze();
            gif.Frames.Add(BitmapFrame.Create(image));
            if (frame == 22 || frame == 35)
            {
                var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(image));
                using (var stream = File.Create(Path.Combine(args[0], frame == 22 ? "Enfoque-Pro.png" : "Lanzamiento-Pro.png"))) png.Save(stream);
            }
        }
        var exporter = Assembly.LoadFrom(Path.Combine(Path.GetDirectoryName(typeof(ProMotionValidation).Assembly.Location), "MotionValidation.exe"))
            .GetType("MotionValidation").GetMethod("SaveGif", BindingFlags.Static | BindingFlags.NonPublic);
        exporter.Invoke(null, new object[] { gif, Path.Combine(args[0], "Animaciones-Pro.gif") });
        foreach (var demo in demos) { Art(demo).StopMotion(); demo.Win.Close(); }
        Anim.Stop(); app.Shutdown();
        Console.WriteLine("PASS: " + _checks + " coordinated hover checks; 11 profiles, continuous falloff, corner clearance, launch composition and reset; real-dock preview exported.");
    }
}
