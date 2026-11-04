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

static class MotionValidation
{
    static int _checks;
    static void Check(bool value, string message) { if (!value) throw new Exception(message); _checks++; }
    static void Pump(int milliseconds)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += delegate { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame);
    }
    static RenderTargetBitmap Render(Visual visual, int width, int height)
    {
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual); bitmap.Freeze(); return bitmap;
    }
    static byte[] Pixels(BitmapSource bitmap)
    {
        var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
        bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0); return pixels;
    }
    static int Difference(byte[] left, byte[] right)
    {
        int count = 0; for (int i = 0; i < left.Length; i++) if (left[i] != right[i]) count++; return count;
    }
    // WIC writes every image but discards per-frame delay metadata. Repair the GIF container,
    // keeping WPF's encoded image data untouched, so browser and GDI decoders both animate it.
    static void SaveGif(GifBitmapEncoder encoder, string path)
    {
        byte[] data;
        using (var stream = new MemoryStream()) { encoder.Save(stream); data = stream.ToArray(); }
        int offset = 13;
        if ((data[10] & 128) != 0) offset += 3 * (1 << ((data[10] & 7) + 1));
        int insertAt = offset, frames = 0;
        while (offset < data.Length)
        {
            byte block = data[offset++];
            if (block == 0x3B) break;
            if (block == 0x21)
            {
                byte label = data[offset++];
                if (label == 0xF9 && data[offset] == 4)
                {
                    data[offset + 1] = (byte)((data[offset + 1] & 0xE3) | 8);
                    data[offset + 2] = 8; data[offset + 3] = 0;
                }
            }
            else if (block == 0x2C)
            {
                byte packed = data[offset + 8]; offset += 9;
                if ((packed & 128) != 0) offset += 3 * (1 << ((packed & 7) + 1));
                offset++; frames++;
            }
            else throw new InvalidDataException("Unexpected GIF block");
            while (offset < data.Length)
            {
                int size = data[offset++];
                if (size == 0) break;
                offset += size;
            }
        }
        Check(frames == encoder.Frames.Count, "GIF contains all live frames");
        using (var stream = File.Create(path))
        {
            stream.Write(data, 0, insertAt);
            byte[] loop = { 0x21, 0xFF, 0x0B, 78, 69, 84, 83, 67, 65, 80, 69, 50, 46, 48, 3, 1, 0, 0, 0 };
            stream.Write(loop, 0, loop.Length);
            stream.Write(data, insertAt, data.Length - insertAt);
        }
    }
    static void Png(BitmapSource image, string path)
    {
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(image));
        using (var stream = File.Create(path)) png.Save(stream);
    }
    [STAThread]
    static int Main(string[] args)
    {
        try { Run(args); return 0; } catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    static void Run(string[] args)
    {
        string output = args[0]; Directory.CreateDirectory(output);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var root = new Canvas { Width = 840, Height = 870, Background = Themes.Br("#FF11171B") };
        var window = new Window { Content = root, Width = 840, Height = 870, WindowStyle = WindowStyle.None,
            ShowActivated = false, ShowInTaskbar = false, Opacity = 0, Left = -2000, Top = -2000 };
        window.Show();
        var arts = new List<MaterialArt>(); var tiles = new List<Button>();
        var source = new BitmapImage(new Uri(Path.GetFullPath(args[1])));
        int[] cropX = { 68, 152, 236, 320, 404, 488, 572, 656 };
        var hints = new[] { "Alien · detector de movimiento / esclusa", "Portal · aperturas / transferencia",
            "PlayStation · arranque / memoria", "Mass Effect · campo gravitatorio", "Matrix · código / decodificación", "Cyberpunk · quickhack" };
        int row = 0;
        foreach (var entry in Themes.InGroup("animated"))
        {
            if (row == 6) break; // Existing six-theme animation reel; new sagas have their own coverage.
            var label = new TextBlock { Text = entry.Value.Label + "   —   " + hints[row], FontSize = 13,
                Foreground = Themes.Br("#FFDDE5E6"), FontFamily = new FontFamily("Segoe UI") };
            Canvas.SetLeft(label, 30); Canvas.SetTop(label, 17 + row * 144); root.Children.Add(label);
            var panel = new Canvas { Width = 780, Height = 102 };
            Canvas.SetLeft(panel, 30); Canvas.SetTop(panel, 43 + row * 144); root.Children.Add(panel);
            var geometry = PillGeometry.Build(780, 49, 45, 0, 0, 390, 90, 1.55, false, 0, 0, entry.Value.CornerRatio);
            panel.Children.Add(new System.Windows.Shapes.Path { Data = geometry,
                Fill = Themes.Grad(entry.Value.C["glass"], entry.Value.C["glassBot"]),
                Stroke = Themes.Br(entry.Value.C["edge"]), StrokeThickness = 1 });
            var art = new MaterialArt { IsHitTestVisible = false, MotionEnabled = true };
            panel.Children.Add(art); art.Update(entry.Value, geometry, 780, 49, 45, false, 406);
            arts.Add(art);
            for (int i = 0; i < cropX.Length; i++)
            {
                var bitmap = new CroppedBitmap(source, new Int32Rect(cropX[i], 22, 62, 62));
                var content = new Border { Width = 62, Height = 62, CornerRadius = new CornerRadius(13),
                    Background = new ImageBrush(bitmap) { Stretch = Stretch.Fill } };
                var tile = new Button { Content = content, Width = 62, Height = 62,
                    Padding = new Thickness(0), BorderThickness = new Thickness(0), Background = Brushes.Transparent,
                    Template = new ControlTemplate(typeof(Button)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) },
                    RenderTransform = new ScaleTransform(1, 1) };
                Canvas.SetLeft(tile, 50 + i * 93); Canvas.SetTop(tile, 18); panel.Children.Add(tile);
                if (i == 4) tiles.Add(tile);
            }
            row++;
        }
        root.UpdateLayout(); Pump(80);
        Check(arts.Count == 6 && tiles.Count == 6, "Six independent saga renderers");
        // Deterministic rendering: every saga must visibly change at distinct phase values and both orientations.
        foreach (var theme in Themes.InGroup("animated"))
        foreach (bool vertical in new[] { false, true })
        foreach (int size in new[] { 32, 64, 96 })
        {
            double half = size / 2 + 8;
            var geo = PillGeometry.Build(650, half, half, 0, 0, 325, 90, 1.55, vertical, 0, 0, theme.Value.CornerRatio);
            var art = new MaterialArt(); art.Update(theme.Value, geo, 650, half, half, vertical, 325);
            art.InteractionLevel = 1;
            byte[] previous = null;
            foreach (double phase in new[] { 0.12, 0.38, 0.64, 0.89 })
            {
                art.EventPhase = phase; art.AmbientPhase = phase;
                art.Measure(new Size(800, 800)); art.Arrange(new Rect(art.DesiredSize)); art.UpdateLayout();
                var bitmap = Render(art, vertical ? (int)(half * 2 + 2) : 650, vertical ? 650 : (int)(half * 2 + 2));
                byte[] current = Pixels(bitmap);
                if (previous != null) Check(Difference(previous, current) > 20, "Static saga animation: " + theme.Key);
                previous = current;
            }
            Check(!art.HasAnimatedProperties, "Static thumbnail started animation clocks");
        }
        foreach (var art in arts) art.StopMotion();
        foreach (var art in arts) Check(!art.HasAnimatedProperties, "Stop releases animation clocks");
        // Live clocks are exercised even when Windows disables native control animations.
        {
            foreach (var art in arts) art.SetInteraction(true);
            Pump(150);
            foreach (var art in arts) Check(art.AmbientPhase > 0 && art.InteractionLevel > 0, "Hover animation advances");
            foreach (var art in arts) art.SetInteraction(false);
            // Off-screen WPF composition may delay the first fade tick; assert the terminal state,
            // with a deadline, instead of assuming 350 ms of wall time equals animation-clock time.
            var stopDeadline = System.Diagnostics.Stopwatch.StartNew();
            while (!arts.TrueForAll(art => !art.HasAnimatedProperties && art.InteractionLevel == 0)
                && stopDeadline.ElapsedMilliseconds < 1500) Pump(25);
            foreach (var art in arts) Check(!art.HasAnimatedProperties && art.InteractionLevel == 0, "Hover clocks stop on leave");
            for (int i = 0; i < arts.Count; i++)
            {
                object transform = tiles[i].RenderTransform;
                arts[i].Launch(406);
                SagaTileMotion.Launch(tiles[i], Themes.Get(ThemesIds[i]).Material, false);
                Check(ReferenceEquals(transform, tiles[i].RenderTransform), "Launch overwrote magnification spring");
            }
            Pump(120);
            foreach (var art in arts) Check(art.EventPhase > 0, "Launch animation advances");
            double longestLaunch = 0;
            foreach (string id in ThemesIds) longestLaunch = Math.Max(longestLaunch, ThemeMotionProfile.For(Themes.Get(id).Material).LaunchTime);
            var launchDeadline = System.Diagnostics.Stopwatch.StartNew();
            while (!arts.TrueForAll(art => !art.HasAnimatedProperties && art.EventPhase < 0)
                && launchDeadline.ElapsedMilliseconds < longestLaunch * 1000 + 500) Pump(25);
            Pump(40);
            foreach (var art in arts) Check(!art.HasAnimatedProperties && art.EventPhase < 0, "Launch clocks finish");
            foreach (var tile in tiles)
            {
                var content = (FrameworkElement)tile.Content;
                Check(Math.Abs(content.Opacity - 1) < 0.001, "Artwork remained faded");
                var group = (TransformGroup)content.RenderTransform;
                Check(group.Value.IsIdentity, "Artwork transform did not settle");
            }
            foreach (var art in arts) { art.SetInteraction(true); art.Launch(406); }
            root.Visibility = Visibility.Collapsed; Pump(60);
            foreach (var art in arts) Check(!art.HasAnimatedProperties, "Hidden dock keeps rendering");
            root.Visibility = Visibility.Visible; Pump(30);
        }
        // Switching to Classic must cancel both hover and launch clocks immediately.
        var probe = arts[0]; probe.SetInteraction(true); probe.Launch(300);
        var classic = Themes.Get("noche");
        probe.Update(classic, PillGeometry.Build(780, 49, 45, 0, 0, 390, 90, 1.55, false, 0, 0), 780, 49, 45, false, 300);
        Check(!probe.HasAnimatedProperties, "Classic inherited saga motion");
        probe.Launch(300); probe.SetInteraction(true);
        Check(!probe.HasAnimatedProperties, "Classic started saga motion");
        Check(new Config().Motion, "Existing configuration defaults to requested theme animations");
        Check(!new Config { ThemeMotion = false }.Motion, "Theme animation preference disables effects");
        probe.Update(Themes.Get("nostromo"), PillGeometry.Build(780, 49, 45, 0, 0, 390, 90, 1.55, false, 0, 0, 0.32),
            780, 49, 45, false, 406);
        probe.SetInteraction(true); probe.Launch(300); probe.MotionEnabled = false;
        Check(!probe.HasAnimatedProperties, "Disabling theme motion cancels active clocks");
        probe.SetInteraction(true); probe.Launch(300);
        Check(!probe.HasAnimatedProperties, "Disabled theme motion cannot restart clocks");
        probe.MotionEnabled = true;
        foreach (var art in arts) { art.StopMotion(); art.SetInteraction(true); }
        // Live WPF frames include the production artwork animations, not a separate simulation.
        var gif = new GifBitmapEncoder();
        for (int frame = 0; frame < 42; frame++)
        {
            if (frame == 6)
                for (int i = 0; i < arts.Count; i++)
                {
                    arts[i].Launch(437);
                    SagaTileMotion.Launch(tiles[i], Themes.Get(ThemesIds[i]).Material, false);
                }
            if (frame == 30) foreach (var art in arts) art.SetInteraction(false);
            Pump(80);
            root.UpdateLayout();
            var bitmap = Render(root, 840, 870);
            var metadata = new BitmapMetadata("gif");
            metadata.SetQuery("/grctlext/Delay", (ushort)8);
            metadata.SetQuery("/grctlext/Disposal", (byte)2);
            if (frame == 0)
            {
                metadata.SetQuery("/appext/Application", System.Text.Encoding.ASCII.GetBytes("NETSCAPE2.0"));
                metadata.SetQuery("/appext/Data", new byte[] { 3, 1, 0, 0, 0 });
            }
            gif.Frames.Add(BitmapFrame.Create(bitmap, null, metadata, null));
            if (frame == 12) Png(bitmap, Path.Combine(output, "Animaciones-GamesDock.png"));
        }
        SaveGif(gif, Path.Combine(output, "Animaciones-GamesDock.gif"));
        foreach (var art in arts) art.StopMotion();
        window.Close(); app.Shutdown();
        Console.WriteLine("PASS: " + _checks + " motion checks; distinct animated frames, clock lifecycle, Classic isolation, icon recovery; live WPF GIF exported.");
    }
    static readonly string[] ThemesIds = { "nostromo", "camara", "memory", "horizonte", "terminal", "nightcity" };
}
