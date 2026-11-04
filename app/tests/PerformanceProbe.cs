using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using GamesDock;

static class PerformanceProbe
{
    static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Pump(int ms)
    {
        var frame = new DispatcherFrame(); var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        timer.Tick += delegate { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame);
    }
    static string N(double n) { return n.ToString("F4", Invariant); }
    static MaterialArt Art(GamesDock.Dock dock) { return (MaterialArt)typeof(GamesDock.Dock).GetField("Material", Private).GetValue(dock); }
    static long Allocated { get { return AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize; } }
    [STAThread]
    static int Main(string[] args)
    {
        try { AppDomain.MonitoringIsEnabled = true; Run(args); return 0; }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    static void Run(string[] args)
    {
        string directory = args[1]; Directory.CreateDirectory(directory);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(XamlAssets.LoadDictionary("styles"));
        if (args[0] == "parity") { Parity(directory); app.Shutdown(); return; }
        var micro = new List<string> { "metric,value" };
        for (int i = 0; i < 200; i++) PillGeometry.Build(720, 64, 42, 6, 8, i, 80, 1.55, false, 4, 4);
        long allocated = Allocated; var watch = Stopwatch.StartNew();
        for (int i = 0; i < 15000; i++) PillGeometry.Build(720, 64, 42, 6, 8, i % 720, 80, 1.55, false, 4, 4);
        watch.Stop();
        micro.Add("geometry_us_per_build," + N(watch.Elapsed.TotalMilliseconds * 1000 / 15000));
        micro.Add("geometry_bytes_per_build," + N((Allocated - allocated) / 15000.0));
        foreach (string line in micro) Console.WriteLine(line);
        if (args[0] == "micro") { File.WriteAllLines(Path.Combine(directory, "micro.csv"), micro); app.Shutdown(); return; }

        var win = XamlAssets.Load<Window>("window"); win.ShowActivated = false;
        var dock = new GamesDock.Dock(win, new Config { Theme = "terminal", IconSize = 64, IconShape = "ios", HoverCard = false, IdleOpacity = 100 });
        win.Show();
        for (int i = 0; i < 8; i++) dock.Items.Add(new ItemSpec { Key = "perf-" + i, Name = "Sample " + i, Kind = "game" });
        dock.RebuildStrip(false); dock.ApplyLayout(); win.Left = win.Top = -10000; Pump(2200);
        var build = typeof(Settings).GetMethod("BuildPanel", BindingFlags.Static | BindingFlags.NonPublic);
        long heapBefore = GC.GetTotalMemory(true); watch.Restart();
        var panel = (GlassPanel)build.Invoke(null, new object[] { dock }); watch.Stop();
        long heapAfter = GC.GetTotalMemory(true);
        micro.Add("settings_build_ms," + N(watch.Elapsed.TotalMilliseconds));
        micro.Add("settings_managed_added_mib," + N((heapAfter - heapBefore) / 1048576.0));
        GC.KeepAlive(panel); panel.Win.Close();
        File.WriteAllLines(Path.Combine(directory, "micro.csv"), micro);
        var rows = new List<string> { "theme,phase,seconds,cpu_one_core_percent,cpu_machine_percent,working_mib,private_mib,managed_mib,allocated_mib_per_second,gen0,gen1,gen2" };
        foreach (var theme in Themes.InGroup("animated"))
        {
            if (args.Length > 2 && theme.Key != args[2]) continue;
            dock.ResetProximity(); dock.SetPillHover(false); dock.ApplyTheme(theme.Key); dock.ApplyLayout();
            win.Left = win.Top = -10000; Pump(2100); Art(dock).StopMotion(); Anim.Stop();
            if (args[0] == "sweep")
            {
                dock.Cfg.ThemeMotion = true; Art(dock).MotionEnabled = true; dock.SetPillHover(true);
                var movement = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000.0 / 60) };
                var time = Stopwatch.StartNew();
                movement.Tick += delegate
                {
                    double fraction = .5 + Math.Sin(time.Elapsed.TotalSeconds * 2.5) * .38;
                    dock.UpdateProximity(new Point(dock.RootGrid.ActualWidth * fraction, dock.RootGrid.ActualHeight / 2));
                };
                movement.Start(); Pump(700); Measure(rows, theme.Key, "cursor_sweep", 3500); movement.Stop();
                File.WriteAllLines(Path.Combine(directory, "sweep.csv"), rows); continue;
            }
            Measure(rows, theme.Key, "idle", 1700);
            dock.Cfg.ThemeMotion = false; Art(dock).MotionEnabled = false;
            dock.SetPillHover(true); dock.UpdateProximity(new Point(dock.RootGrid.ActualWidth / 2, dock.RootGrid.ActualHeight / 2));
            Pump(800);
            Measure(rows, theme.Key, "magnified_no_effects", 1700);
            dock.Cfg.ThemeMotion = true; Art(dock).MotionEnabled = true; Art(dock).SetInteraction(true); Pump(2100);
            Measure(rows, theme.Key, "animated_hover", 1700);
            dock.ResetProximity(); dock.SetPillHover(false); Pump(700);
            Art(dock).Launch(dock.RootGrid.ActualWidth / 2);
            SagaTileMotion.Launch(dock.Tiles[4], theme.Value.Material, false);
            Measure(rows, theme.Key, "launch", 1800);
            File.WriteAllLines(Path.Combine(directory, "runtime.csv"), rows);
        }
        win.Close(); Anim.Stop(); app.Shutdown();
    }
    static void Measure(List<string> rows, string theme, string phase, int ms)
    {
        var process = Process.GetCurrentProcess(); process.Refresh();
        double cpu = process.TotalProcessorTime.TotalMilliseconds;
        long allocated = Allocated; int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);
        var watch = Stopwatch.StartNew(); Pump(ms); watch.Stop(); process.Refresh();
        double oneCore = (process.TotalProcessorTime.TotalMilliseconds - cpu) / watch.Elapsed.TotalMilliseconds * 100;
        string line = string.Join(",", theme, phase, N(watch.Elapsed.TotalSeconds), N(oneCore), N(oneCore / Environment.ProcessorCount),
            N(process.WorkingSet64 / 1048576.0), N(process.PrivateMemorySize64 / 1048576.0), N(GC.GetTotalMemory(false) / 1048576.0),
            N((Allocated - allocated) / 1048576.0 / watch.Elapsed.TotalSeconds), GC.CollectionCount(0) - g0, GC.CollectionCount(1) - g1, GC.CollectionCount(2) - g2);
        rows.Add(line); Console.WriteLine(line);
    }
    static void Parity(string directory)
    {
        var results = new List<string>();
        var window = new Window { Width = 800, Height = 800, Opacity = 0, ShowActivated = false, ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual, Left = -10000, Top = -10000 };
        var panel = new Canvas { Width = 780, Height = 780, Background = Themes.Br("#FF11171B") };
        window.Content = panel; window.Show();
        using (var hash = SHA256.Create())
        foreach (var entry in Themes.All)
        foreach (bool vertical in new[] { false, true })
        foreach (int size in new[] { 32, 64 })
        foreach (double phase in new[] { 0.0, .23, .68 })
        {
            panel.Children.Clear();
            var profile = ThemeMotionProfile.For(entry.Value.Material);
            double half = size / 2 + 8;
            var geometry = PillGeometry.Build(650, half + 20, half, 4, profile.Locality * 8, 325, 80,
                profile.Peak, vertical, 4, 4, entry.Value.CornerRatio, profile.Radius);
            panel.Children.Add(new System.Windows.Shapes.Path { Data = geometry, Fill = Themes.Grad(entry.Value.C["glass"], entry.Value.C["glassBot"]),
                Stroke = Themes.Br(entry.Value.C["edge"]), StrokeThickness = 1 });
            var art = new MaterialArt { MotionEnabled = true, IsHitTestVisible = false };
            panel.Children.Add(art); panel.Children.Add(art.Overlay);
            art.Update(entry.Value, geometry, 650, half + 20, half + 4, vertical, 325);
            art.StopMotion(); art.InteractionLevel = 1; art.EventPhase = phase; art.AmbientPhase = phase;
            art.SetFocus(vertical ? new Rect(22, 295, size, size) : new Rect(295, 22, size, size), 8, .7);
            panel.Measure(new Size(780, 780)); panel.Arrange(new Rect(0, 0, 780, 780)); panel.UpdateLayout();
            int width = vertical ? 160 : 670, height = vertical ? 670 : 160;
            var image = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); image.Render(panel);
            var pixels = new byte[width * height * 4]; image.CopyPixels(pixels, width * 4, 0);
            string key = entry.Key + "-" + vertical + "-" + size + "-" + phase.ToString(Invariant);
            results.Add(key + "\t" + BitConverter.ToString(hash.ComputeHash(pixels)).Replace("-", ""));
            art.StopMotion();
        }
        File.WriteAllLines(Path.Combine(directory, "pixels.tsv"), results);
        Console.WriteLine("PIXEL_FIXTURES=" + results.Count); window.Close(); Anim.Stop();
    }
}
