using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using GamesDock;

static class PerformanceRegression
{
    static int _checks;
    static void Check(bool value, string reason) { if (!value) throw new Exception(reason); _checks++; }
    [STAThread]
    static int Main()
    {
        try { Run(); return 0; } catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    static void Run()
    {
        AppDomain.MonitoringIsEnabled = true;
        var retained = new List<System.Windows.Media.Geometry>(); var expected = new List<string>();
        for (int i = 0; i < 20; i++)
        {
            var geometry = PillGeometry.Build(720, 64, 42, 6, 8, i * 32, 80, 1.55, i % 2 == 0, 4, 4);
            retained.Add(geometry); expected.Add(geometry.ToString(CultureInfo.InvariantCulture));
        }
        for (int i = 0; i < 1000; i++) PillGeometry.Build(720, 64, 42, 6, 8, i % 720, 80, 1.55, false, 4, 4);
        for (int i = 0; i < retained.Count; i++)
            Check(retained[i].IsFrozen && retained[i].ToString(CultureInfo.InvariantCulture) == expected[i], "Reused workspace mutated a retained frame");
        Exception failure = null;
        var threads = new Thread[2];
        for (int i = 0; i < 2; i++)
        {
            threads[i] = new Thread(delegate()
            {
                try
                {
                    for (int j = 0; j < 150; j++)
                    {
                        var frame = PillGeometry.Build(720, 64, 42, 6, 8, j, 80, 1.55, false, 4, 4);
                        string before = frame.ToString(CultureInfo.InvariantCulture);
                        PillGeometry.Build(380, 42, 28, 3, 5, j * 2, 60, 1.4, true, 3, 2);
                        if (frame.ToString(CultureInfo.InvariantCulture) != before) throw new Exception("Cross-frame workspace alias");
                    }
                }
                catch (Exception ex) { failure = ex; }
            });
            threads[i].SetApartmentState(ApartmentState.STA); threads[i].Start();
        }
        foreach (var thread in threads) thread.Join();
        Check(failure == null, "Per-thread geometry workspace is not isolated");
        long beforeBytes = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
        for (int i = 0; i < 3000; i++) PillGeometry.Build(720, 64, 42, 6, 8, i % 720, 80, 1.55, false, 4, 4);
        double bytes = (AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - beforeBytes) / 3000.0;
        Check(bytes < 14000, "Geometry allocation budget regressed: " + bytes);

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(XamlAssets.LoadDictionary("styles"));
        var window = XamlAssets.Load<Window>("window"); window.Opacity = 0; window.ShowActivated = false;
        var dock = new GamesDock.Dock(window, new Config { Theme = "terminal", IdleOpacity = 100, ThemeMotion = false }); window.Show();
        for (int i = 0; i < 5; i++) dock.Items.Add(new ItemSpec { Key = "perf-regression-" + i, Name = "Game " + i, Kind = "game" });
        dock.RebuildStrip(false); dock.ApplyLayout(); window.UpdateLayout(); dock.RefreshPill();
        var art = (MaterialArt)typeof(GamesDock.Dock).GetField("Material", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(dock);
        art.StopMotion(); Anim.Stop();
        Check(dock.CanTrimMemory, "Idle memory maintenance remains blocked");
        dock.UpdateProximity(new Point(dock.RootGrid.ActualWidth / 2, 20));
        Check(!dock.CanTrimMemory, "Memory trimming is allowed during interaction");
        dock.ResetProximity(); dock.SetPillHover(false); Anim.Stop(); art.StopMotion();
        dock.DragMoved = true; Check(!dock.CanTrimMemory, "Memory trimming is allowed while dragging"); dock.DragMoved = false;
        var open = typeof(Settings).GetField("_open", BindingFlags.Static | BindingFlags.NonPublic);
        open.SetValue(null, window);
        try { Check(!dock.CanTrimMemory, "Memory trimming is allowed with settings open"); }
        finally { open.SetValue(null, null); }
        dock.RefreshPill();
        beforeBytes = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
        for (int i = 0; i < 10000; i++) dock.UpdatePillShape();
        bytes = (AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - beforeBytes) / 10000.0;
        Check(bytes < 256, "Unchanged contour allocates formatted cache keys: " + bytes);
        window.Close(); Anim.Stop(); app.Shutdown();
        Console.WriteLine("PASS: " + _checks + " performance regressions; immutable retained frames, thread isolation, allocation budgets and safe memory maintenance.");
    }
}
