using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using GamesDock;

static class HoverRegression
{
    static int _checks;
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); _checks++; }
    static void Pump(int milliseconds)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += delegate { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame);
    }
    static Spring SpringOf(GamesDock.Dock dock, string name)
    {
        return (Spring)typeof(GamesDock.Dock).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(dock);
    }
    static void Fit(Window window, GamesDock.Dock dock)
    {
        dock.ApplyLayout();
        window.UpdateLayout(); dock.RefreshPill();
    }
    static string Contour(Window window)
    {
        return ((System.Windows.Shapes.Path)window.FindName("Glass")).Data.ToString(CultureInfo.InvariantCulture);
    }
    static void AssertInsideWindow(Window window, string context)
    {
        var path = (System.Windows.Shapes.Path)window.FindName("Glass");
        Rect bounds = path.TransformToAncestor(window).TransformBounds(path.Data.Bounds);
        Check(bounds.Left >= -1 && bounds.Top >= -1 && bounds.Right <= window.ActualWidth + 1 && bounds.Bottom <= window.ActualHeight + 1,
            context + ": contour clips window: " + bounds + "; window " + window.ActualWidth + "x" + window.ActualHeight);
    }
    [STAThread]
    static int Main()
    {
        try { Run(); return 0; } catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    static void Run()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(XamlAssets.LoadDictionary("styles"));
        var config = new Config { IconSize = 64, Magnify = false, ThemeMotion = false, IdleOpacity = 100, HoverCard = false };
        var window = XamlAssets.Load<Window>("window"); window.Opacity = 0; window.ShowActivated = false;
        var dock = new GamesDock.Dock(window, config); window.Show();
        for (int i = 0; i < 8; i++) dock.Items.Add(new ItemSpec { Key = "hover-test-" + i, Name = "Game " + i, Kind = "game" });
        dock.RebuildStrip(false); Fit(window, dock);
        string before = Contour(window);
        dock.SetPillHover(true); dock.UpdateProximity(new Point(dock.RootGrid.ActualWidth / 2, dock.RootGrid.ActualHeight / 2));
        Pump(900); dock.RefreshPill();
        AssertInsideWindow(window, "magnification disabled, pointer enters");
        Check(Contour(window) == before, "Disabled magnification still expands the dock on pointer entry");
        foreach (var position in new[] { "bottom", "top", "left", "right" })
        foreach (var theme in Themes.All)
        foreach (int size in new[] { 32, 64, 96 })
        {
            config.Position = position; config.IconSize = size; config.Magnify = false;
            dock.ResetProximity(); dock.SetPillHover(false);
            dock.ApplyTheme(theme.Key); Fit(window, dock);
            var hover = SpringOf(dock, "HoverSpr");
            string idle = Contour(window);
            // Drive a fully settled entry through the same spring callback used by the real frame loop.
            dock.SetPillHover(true);
            hover.X = hover.Target; hover.V = 0; hover.Apply(hover);
            dock.RefreshPill();
            Check(hover.Target == 0, "Disabled hover still requests shell growth");
            Check(Contour(window) == idle, "Disabled shell changes on entry");
            foreach (double fraction in new[] { 0.01, 0.5, 0.99 })
            {
                double length = dock.Lay.Vert ? dock.RootGrid.ActualHeight : dock.RootGrid.ActualWidth;
                dock.UpdateProximity(dock.Lay.Vert ? new Point(dock.RootGrid.ActualWidth / 2, length * fraction)
                    : new Point(length * fraction, dock.RootGrid.ActualHeight / 2));
                var pointer = SpringOf(dock, "PtrSpr"); pointer.X = pointer.Target; pointer.V = 0; pointer.Apply(pointer);
                dock.RefreshPill();
                Check(Contour(window) == idle, "Disabled contour changes with cursor: " + theme.Key + "/" + position);
                AssertInsideWindow(window, theme.Key + "/" + position + "/" + size);
                foreach (var tile in dock.Tiles)
                {
                    var scale = (ScaleTransform)tile.RenderTransform;
                    Check(scale.ScaleX == 1 && scale.ScaleY == 1, "Disabled tile scale changed");
                }
            }
            // Match the settings callback while the cursor's magnification is already fully open.
            config.Magnify = true; Fit(window, dock); dock.SetPillHover(true);
            double main = dock.Lay.Vert ? dock.RootGrid.ActualHeight : dock.RootGrid.ActualWidth;
            dock.UpdateProximity(dock.Lay.Vert ? new Point(dock.RootGrid.ActualWidth / 2, main / 2)
                : new Point(main / 2, dock.RootGrid.ActualHeight / 2));
            hover.X = hover.Target; hover.V = 0; hover.Apply(hover);
            var pointerSpring = SpringOf(dock, "PtrSpr");
            pointerSpring.X = pointerSpring.Target; pointerSpring.V = 0; pointerSpring.Apply(pointerSpring);
            var tileSprings = (List<Spring>)typeof(GamesDock.Dock).GetField("MagSpr", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(dock);
            bool enlarged = false;
            foreach (var spring in tileSprings)
            {
                spring.X = spring.Target; spring.V = 0; spring.Apply(spring);
                if (spring.X > 1.01) enlarged = true;
            }
            Check(enlarged, "Enabled magnification stopped working");
            config.Magnify = false; dock.ResetProximity(); Fit(window, dock);
            Check(hover.X == 0 && hover.Target == 0 && hover.V == 0, "Toggling off leaves a hover spring active");
            foreach (var spring in tileSprings)
                Check(spring.X == 1 && spring.Target == 1 && spring.V == 0 && spring.Wait == 0, "Toggling off leaves a tile enlarged");
            Check(Contour(window) == idle, "Toggling off fails to restore idle contour immediately");
            if (theme.Value.Material != null)
            {
                var material = (MaterialArt)typeof(GamesDock.Dock).GetField("Material", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(dock);
                material.MotionEnabled = true;
                dock.SetPillHover(true); dock.RefreshPill();
                Check(material.HasAnimatedProperties, "Magnification toggle incorrectly disables saga animations");
                Check(Contour(window) == idle, "Saga animation changes disabled shell contour");
                material.MotionEnabled = false;
            }
            dock.ResetProximity(); dock.SetPillHover(false);
        }
        window.Close(); app.Shutdown();
        Console.WriteLine("PASS: " + _checks + " hover regression checks; disabled contour remains rounded and within the window across 15 themes, 4 positions and 3 sizes.");
    }
}
