using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using GamesDock;

static class PreviewProbe
{
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Pump(int ms)
    {
        var frame = new DispatcherFrame(); var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        timer.Tick += delegate { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame);
    }
    [STAThread]
    static int Main(string[] args)
    {
        try { Run(args); return 0; } catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    static void Run(string[] args)
    {
        AppDomain.MonitoringIsEnabled = true;
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(XamlAssets.LoadDictionary("styles"));
        var owner = XamlAssets.Load<Window>("window"); owner.Opacity = 0; owner.ShowActivated = false;
        var dock = new GamesDock.Dock(owner, new Config { Theme = "terminal", IdleOpacity = 100, IconSize = 46 }); owner.Show();
        for (int i = 0; i < 5; i++) dock.Items.Add(new ItemSpec { Key = "preview-probe-" + i, Name = "Game " + i, Kind = "game" });
        dock.RebuildStrip(false); dock.ApplyLayout(); owner.Left = owner.Top = -10000;
        var sample = new DockPreview(dock);
        var window = new Window { Content = sample, Width = 700, Height = 170, Opacity = 0, ShowActivated = false,
            WindowStartupLocation = WindowStartupLocation.Manual, Left = -10000, Top = -10000 };
        window.Show(); window.UpdateLayout(); Pump(400);
        var tiles = typeof(DockPreview).GetField("_tiles", Private);
        var original = new List<Button>((List<Button>)tiles.GetValue(sample));
        long bytes = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < 200; i++) sample.Refresh();
        watch.Stop();
        long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - bytes;
        int preserved = 0;
        var current = (List<Button>)tiles.GetValue(sample);
        for (int i = 0; i < 5; i++) if (ReferenceEquals(original[i], current[i])) preserved++;
        var rows = new List<string> { "metric,value", "refresh_us," + N(watch.Elapsed.TotalMilliseconds * 1000 / 200),
            "refresh_bytes," + N(allocated / 200.0), "buttons_preserved," + preserved };
        var pointer = typeof(DockPreview).GetMethod("AnimatePointer", Private);
        var progress = typeof(DockPreview).GetMethod("AnimateProgress", Private);
        var inside = typeof(DockPreview).GetField("_inside", Private);
        var material = (MaterialArt)typeof(DockPreview).GetField("_material", Private).GetValue(sample);
        foreach (string theme in new[] { "terminal", "nostromo" })
        {
            dock.ApplyTheme(theme); sample.Refresh(); window.UpdateLayout(); Pump(400);
            inside.SetValue(sample, true); progress.Invoke(sample, new object[] { 1.0 }); material.SetInteraction(true);
            var elapsed = Stopwatch.StartNew();
            var movement = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(8) };
            movement.Tick += delegate { pointer.Invoke(sample, new object[] { 160 + Math.Sin(elapsed.Elapsed.TotalSeconds * 3) * 120 }); };
            movement.Start(); Pump(700);
            int updates = 0;
            var property = (DependencyProperty)typeof(DockPreview).GetField("PointerProperty", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var descriptor = DependencyPropertyDescriptor.FromProperty(property, typeof(DockPreview));
            EventHandler changed = delegate { updates++; }; descriptor.AddValueChanged(sample, changed);
            var process = Process.GetCurrentProcess(); process.Refresh(); double cpu = process.TotalProcessorTime.TotalMilliseconds;
            bytes = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize; watch.Restart(); Pump(3000); watch.Stop(); process.Refresh();
            rows.Add(theme + "_cpu_total," + N((process.TotalProcessorTime.TotalMilliseconds - cpu) / watch.Elapsed.TotalMilliseconds * 100 / Environment.ProcessorCount));
            rows.Add(theme + "_allocated_mib_s," + N((AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - bytes) / 1048576.0 / watch.Elapsed.TotalSeconds));
            rows.Add(theme + "_pointer_updates_s," + N(updates / watch.Elapsed.TotalSeconds)); descriptor.RemoveValueChanged(sample, changed);
            movement.Stop(); inside.SetValue(sample, false); progress.Invoke(sample, new object[] { 0.0 }); material.SetInteraction(false); Pump(500);
        }
        File.WriteAllLines(args[0], rows); foreach (string row in rows) Console.WriteLine(row);
        window.Close(); owner.Close(); Anim.Stop(); app.Shutdown();
    }
    static string N(double value) { return value.ToString("F4", CultureInfo.InvariantCulture); }
}
