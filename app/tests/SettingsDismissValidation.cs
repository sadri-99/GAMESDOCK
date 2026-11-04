using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using GamesDock;

static class SettingsDismissValidation
{
    static int checks;
    static Type veil = typeof(Settings).Assembly.GetType("GamesDock.Veil");
    static void Check(bool value, string reason) { if (!value) throw new Exception(reason); checks++; }
    static int Depth { get { return (int)veil.GetField("_depth", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null); } }
    static void VeilCall(string method, params object[] args) { veil.GetMethod(method, BindingFlags.Static | BindingFlags.Public).Invoke(null, args); }
    static void ClickOutside()
    {
        var window = (Window)veil.GetField("_win", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        var click = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseDownEvent };
        window.RaiseEvent(click); Check(click.Handled, "Outside click passed through");
    }
    [STAThread] static int Main()
    {
        try { Run(); Console.WriteLine("PASS: " + checks + " settings dismissal checks; outside close, nested scopes, save and reopen."); return 0; }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    static void Run()
    {
        System.IO.Directory.CreateDirectory(Paths.Root);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(XamlAssets.LoadDictionary("styles"));
        var cfg = new Config { Theme = "horizonte" };
        var owner = XamlAssets.Load<Window>("window"); owner.ShowActivated = false; owner.Opacity = 0;
        var dock = new GamesDock.Dock(owner, cfg); owner.Show();
        Exception failure = null;
        for (int pass = 0; pass < 3; pass++)
        {
            int cycle = pass;
            owner.Dispatcher.BeginInvoke(new Action(delegate
            {
                try
                {
                    Check(Settings.IsOpen && Depth == 1, "Settings modal scope not ready");
                    if (cycle == 0)
                    {
                        VeilCall("Push", new object[] { null });
                        ClickOutside();
                        Check(Settings.IsOpen && Depth == 2, "Nested prompt dismissed settings");
                        VeilCall("Pop");
                        Check(Depth == 1, "Nested prompt removed settings scope");
                    }
                    cfg.IconSize = 52 + cycle * 2;
                    ClickOutside();
                }
                catch (Exception ex)
                {
                    failure = ex;
                    var open = (Window)typeof(Settings).GetField("_open", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                    if (open != null) open.Close();
                }
            }), DispatcherPriority.Background);
            Settings.Show(dock);
            if (failure != null) throw failure;
            Check(!Settings.IsOpen && Depth == 0, "Close left settings or veil active");
            Check(Config.Load().IconSize == cfg.IconSize, "Outside close did not save through Closed");
        }
        var notice = new GlassPanel("Notice", 400);
        Check(!notice.CloseOnOutsideClick, "Other dialogs changed outside behavior"); notice.Win.Close();
        var build = typeof(Settings).GetMethod("BuildPanel", BindingFlags.Static | BindingFlags.NonPublic);
        var panel = (GlassPanel)build.Invoke(null, new object[] { dock });
        Check(panel.CloseOnOutsideClick, "Settings did not opt in"); panel.Win.Close();
        owner.Close(); Anim.Stop(); app.Shutdown();
    }
}
