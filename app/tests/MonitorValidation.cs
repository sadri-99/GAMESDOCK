using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using GamesDock;

static class MonitorValidation
{
    static int checks;
    static void Check(bool result, string message) { if (!result) throw new Exception(message); checks++; }
    static DisplayInfo Display(string id, string device, Rect bounds, Rect work, bool primary)
    { return new DisplayInfo { Id = id, Device = device, Bounds = bounds, WorkArea = work, Primary = primary }; }
    static void Pump()
    {
        var frame = new DispatcherFrame(); var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        timer.Tick += delegate { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame);
    }
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [STAThread]
    static int Main(string[] args)
    {
        try
        {
            Displays.EnableDpiScaling();
            Environment.SetEnvironmentVariable("GAMESDOCK_DATA_DIR", Path.Combine(Path.GetFullPath(args[0]), "monitor-profile"));
            Paths.Ensure(); Run(); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    static void Run()
    {
        var screens = new List<DisplayInfo> {
            Display("panel-primary", "DISPLAY1", new Rect(0, 0, 1920, 1080), new Rect(0, 0, 1920, 1040), true),
            Display("panel-right", "DISPLAY2", new Rect(1920, 0, 2560, 1440), new Rect(1920, 0, 2560, 1400), false),
            Display("panel-left", "DISPLAY3", new Rect(-1080, -400, 1080, 1920), new Rect(-1040, -400, 1040, 1920), false),
            Display("panel-above", "DISPLAY4", new Rect(0, -1440, 2560, 1440), new Rect(0, -1400, 2560, 1400), false),
            Display("panel-below", "DISPLAY5", new Rect(0, 1440, 3840, 2160), new Rect(0, 1440, 3840, 2110), false)
        };
        foreach (var display in screens)
        {
            Check(MonitorPlacement.AtPoint(new Point(display.Bounds.Left + 10, display.Bounds.Top + 10), screens) == display,
                "Cannot select a screen using physical pointer coordinates");
            foreach (double scale in new[] { 1.0, 1.25, 1.5, 2.0 })
            foreach (string side in new[] { "top", "bottom", "left", "right", "free" })
            foreach (double fraction in new[] { 0.0, .2, .5, .95, 1.0 })
            {
                bool vertical = side == "left" || side == "right";
                var size = new Size(vertical ? 130 : Math.Min(800, display.WorkArea.Width / scale - 60),
                    vertical ? Math.Min(800, display.WorkArea.Height / scale - 60) : 130);
                var cfg = new Config { Position = side, MonitorId = display.Id, MonitorDevice = display.Device,
                    MonitorFreeX = fraction, MonitorFreeY = 1 - fraction };
                var placed = MonitorPlacement.DockBounds(cfg, display, size, scale);
                double margin = Layout.Margin * scale;
                var visible = new Rect(placed.Left + margin, placed.Top + margin,
                    Math.Max(0, placed.Width - 2 * margin), Math.Max(0, placed.Height - 2 * margin));
                Check(visible.Left >= display.WorkArea.Left - 1 && visible.Top >= display.WorkArea.Top - 1 &&
                    visible.Right <= display.WorkArea.Right + 1 && visible.Bottom <= display.WorkArea.Bottom + 1,
                    "Dock escapes the selected screen: " + display.Id + " / " + side + " / " + scale);
                Check(MonitorPlacement.Resolve(cfg, screens) == display, "Restored screen differs from selected screen");
                if (side == "free")
                {
                    MonitorPlacement.Remember(cfg, display, placed, scale);
                    var restored = MonitorPlacement.DockBounds(cfg, display, size, scale);
                    Check(Math.Abs(restored.Left - placed.Left) <= 1 && Math.Abs(restored.Top - placed.Top) <= 1,
                        "Free position does not survive save/restore");
                }
                var popup = MonitorPlacement.PopupBounds(visible, new Size(280 * scale, 200 * scale), display.WorkArea, side, 12 * scale, 6 * scale);
                Check(popup.Left >= display.WorkArea.Left && popup.Top >= display.WorkArea.Top &&
                    popup.Right <= display.WorkArea.Right && popup.Bottom <= display.WorkArea.Bottom,
                    "Companion window jumps outside the dock's screen");
            }
        }
        var saved = new Config { Position = "free", MonitorId = screens[1].Id, MonitorDevice = screens[1].Device,
            MonitorFreeX = .31, MonitorFreeY = .64 };
        var onlyPrimary = new List<DisplayInfo> { screens[0] };
        Check(MonitorPlacement.Resolve(saved, onlyPrimary) == screens[0], "Missing screen has no visible fallback");
        Check(saved.MonitorId == "panel-right", "Temporary fallback overwrites the preferred screen");
        Check(MonitorPlacement.Resolve(saved, screens) == screens[1], "Reconnected preferred screen is not restored");
        var renumbered = Display("panel-right", "DISPLAY9", new Rect(-2560, 0, 2560, 1440), new Rect(-2560, 0, 2560, 1400), true);
        Check(MonitorPlacement.Resolve(saved, new List<DisplayInfo> { screens[0], renumbered }) == renumbered,
            "Windows display renumbering loses hardware identity");
        saved.Save(); var reloaded = Config.Load();
        Check(reloaded.MonitorId == saved.MonitorId && reloaded.MonitorFreeX == saved.MonitorFreeX && reloaded.MonitorFreeY == saved.MonitorFreeY,
            "Monitor preference does not persist on disk");
        var old = new Config { Position = "free", Left = -20000, Top = 30000 };
        var safe = MonitorPlacement.DockBounds(old, screens[0], new Size(600, 120), 1.25);
        Check(safe.Left + Layout.Margin * 1.25 >= 0 && safe.Bottom - Layout.Margin * 1.25 <= 1040,
            "Old off-screen coordinates remain unreachable");

        // Actual native-window integration on every display available to this test session.
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(XamlAssets.LoadDictionary("styles"));
        foreach (var connected in Displays.Connected)
        {
            var cfg = new Config { MonitorId = connected.Id, MonitorDevice = connected.Device, Theme = "horizonte" };
            var window = XamlAssets.Load<Window>("window"); window.ShowActivated = false; window.Opacity = 0;
            var dock = new GamesDock.Dock(window, cfg);
            for (int i = 0; i < 8; i++) dock.Items.Add(new ItemSpec { Key = "monitor-" + i, Kind = "game", Name = "Game " + i });
            dock.RebuildStrip(false); dock.ApplyLayout(); window.Show(); Pump();
            foreach (string side in new[] { "top", "bottom", "left", "right", "free" })
            {
                if (side == "free") dock.AnotarPuestoActual();
                cfg.Position = side; dock.ApplyLayout(); Pump();
                Check(Displays.ForWindow(window).Id == connected.Id, "Live dock is not on the selected screen");
                var bounds = Displays.WindowBounds(window); double scale = GamesDock.Dock.DipScale(window), margin = Layout.Margin * scale;
                Check(bounds.Left + margin >= connected.WorkArea.Left - 2 && bounds.Top + margin >= connected.WorkArea.Top - 2 &&
                    bounds.Right - margin <= connected.WorkArea.Right + 2 && bounds.Bottom - margin <= connected.WorkArea.Bottom + 2,
                    "Live dock is clipped by the monitor work area");
            }
            cfg.Save();
            var again = Config.Load();
            Check(again.MonitorId == connected.Id && again.MonitorFreeX.HasValue, "Live position was not saved for restart");
            cfg.MonitorId = "temporarily-disconnected-monitor";
            PostMessage(new WindowInteropHelper(window).Handle, 0x007E, IntPtr.Zero, IntPtr.Zero); Pump();
            Check(cfg.MonitorId == "temporarily-disconnected-monitor", "Display change overwrote a disconnected preference");
            Check(dock.CurrentDisplay != null && Displays.ForWindow(window).Id == dock.CurrentDisplay.Id, "Display-change recovery left the dock off-screen");
            window.Close(); Pump();
            var restoredWindow = XamlAssets.Load<Window>("window"); restoredWindow.ShowActivated = false; restoredWindow.Opacity = 0;
            var restoredDock = new GamesDock.Dock(restoredWindow, again);
            for (int i = 0; i < 8; i++) restoredDock.Items.Add(new ItemSpec { Key = "monitor-" + i, Kind = "game", Name = "Game " + i });
            restoredDock.RebuildStrip(false); restoredDock.ApplyLayout(); restoredWindow.Show(); Pump();
            Check(restoredDock.CurrentDisplay.Id == connected.Id && Displays.ForWindow(restoredWindow).Id == connected.Id,
                "A fresh dock window does not restore the saved display");
            Check(again.Position == "free" && again.MonitorFreeX.HasValue && again.MonitorFreeY.HasValue,
                "A fresh dock window loses the free-position preference");
            restoredWindow.Close(); Pump();
        }
        Anim.Stop(); app.Shutdown();
        Console.WriteLine("PASS: " + checks + " monitor checks; negative coordinates, 5-screen layouts, 100–200% DPI, reconnect, persistence and " + Displays.Connected.Count + " live display(s).");
    }
}
