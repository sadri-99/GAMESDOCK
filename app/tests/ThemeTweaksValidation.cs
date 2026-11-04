using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using GamesDock;

static class ThemeTweaksValidation
{
    static int checks;
    static void Check(bool ok, string reason) { if (!ok) throw new Exception(reason); checks++; }
    static byte[] Pixels(Visual visual, int width, int height)
    {
        var b = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); b.Render(visual);
        var bytes = new byte[width * height * 4]; b.CopyPixels(bytes, width * 4, 0); return bytes;
    }
    static MaterialArt Art(string id, Geometry clip, bool vertical, double uniform = 0)
    {
        var art = new MaterialArt { MotionEnabled = true };
        art.Update(Themes.Get(id), clip, 640, 65, 35 + uniform, vertical, 40);
        art.Measure(new Size(700, 700)); art.Arrange(new Rect(art.DesiredSize));
        art.Overlay.Measure(new Size(700, 700)); art.Overlay.Arrange(new Rect(art.Overlay.DesiredSize));
        return art;
    }
    static void Pump(int ms)
    {
        var frame = new DispatcherFrame(); var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        timer.Tick += delegate { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame);
    }
    [STAThread] static int Main()
    {
        try { Run(); Console.WriteLine("PASS: " + checks + " theme-tweak checks; cleaned focus, water and Cyberpunk timing."); return 0; }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    static void Run()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var geometry = PillGeometry.Build(640, 65, 35, 0, 0, 320, 80, 1.37, false, 0, 0, 1);
        var ps = Art("memory", geometry, false); ps.SetFocus(new Rect(288, 33, 64, 64), 12, 1);
        ps.InteractionLevel = 1; ps.AmbientPhase = .4; ps.UpdateLayout(); ps.Overlay.UpdateLayout();
        byte[] footer = Pixels(ps.Overlay, 640, 140); int colored = 0;
        for (int y = 97; y < 103; y++) for (int x = 288; x < 352; x++)
        {
            int i = (y * 640 + x) * 4, a = footer[i + 3]; if (a < 10) continue;
            int min = Math.Min(footer[i], Math.Min(footer[i + 1], footer[i + 2]));
            int max = Math.Max(footer[i], Math.Max(footer[i + 1], footer[i + 2]));
            if ((max - min) * 255 / a > 60) colored++;
        }
        Check(colored == 0, "Redundant PlayStation glyphs remain below selected icon");
        var diablo = Art("inferno", geometry, false); diablo.SetFocus(new Rect(288, 33, 64, 64), 12, 1);
        diablo.InteractionLevel = 1; diablo.AmbientPhase = .4; diablo.UpdateLayout(); diablo.Overlay.UpdateLayout();
        byte[] focus = Pixels(diablo.Overlay, 640, 140); int alpha = 0;
        for (int i = 3; i < focus.Length; i += 4) alpha += focus[i];
        Check(alpha == 0, "Diablo still draws a cursor-following focus ring");
        var strand = Art("strand", geometry, false); strand.UpdateLayout();
        byte[] ends = Pixels(strand, 640, 140); int marks = 0;
        for (int y = 55; y < 75; y++) for (int x = 0; x < 20; x++)
        { marks += ends[(y * 640 + x) * 4 + 3]; marks += ends[(y * 640 + 639 - x) * 4 + 3]; }
        Check(marks == 0, "Death Stranding retains colored end instruments");
        strand.InteractionLevel = 1; strand.AmbientPhase = .1; strand.UpdateLayout(); byte[] wetA = Pixels(strand, 640, 140);
        strand.AmbientPhase = .37; strand.UpdateLayout(); byte[] wetB = Pixels(strand, 640, 140); int changed = 0;
        for (int i = 0; i < wetA.Length; i++) if (wetA[i] != wetB[i]) changed++;
        Check(changed > 100, "Water droplets do not slide");
        Check(!Themes.All.ContainsKey("vice") && !Themes.All.ContainsKey("hearts") && !Themes.All.ContainsKey("pokeball") && Themes.All.Count == 20, "Removed themes remain registered");
        Check(ThemeMotionProfile.For("cyberware").Cycle == 7.6 && ThemeMotionProfile.For("cyberware").LaunchTime == 1.9, "Cyberpunk duration was not doubled");
        var cyber = Art("nightcity", geometry, false);
        var window = new Window { Content = cyber, ShowActivated = false, ShowInTaskbar = false, Opacity = 0, Left = -10000, Top = -10000 };
        window.Show(); window.UpdateLayout(); cyber.StopMotion(); cyber.SetInteraction(true); Pump(350);
        Check(cyber.AmbientPhase > .025 && cyber.AmbientPhase < .075, "Cyberpunk live clock still runs at the old speed");
        cyber.StopMotion(); window.Close(); app.Shutdown();
    }
}
