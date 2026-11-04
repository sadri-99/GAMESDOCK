using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using GamesDock;

static class CardValidation
{
    static int checks;
    static void Check(bool ok, string reason) { if (!ok) throw new Exception(reason); checks++; }
    static object Field(object owner, string name) { return owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner); }
    static void Set(object owner, string name, object value) { owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value); }
    static object Call(object owner, string name, params object[] args) { return owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, args); }
    static string Vdf(string apps) { return "UserLocalConfigStore { Software { Valve { Steam { apps { " + apps + " } } } } }"; }
    static void Save(FrameworkElement element, string file)
    {
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth * 2), (int)Math.Ceiling(element.ActualHeight * 2), 192, 192, PixelFormats.Pbgra32);
        bitmap.Render(element); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(file)) encoder.Save(stream);
    }
    [STAThread] static int Main(string[] args)
    {
        try { Run(args[0]); Console.WriteLine("Cards: " + checks + " checks passed."); return 0; }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    static void Run(string output)
    {
        Directory.CreateDirectory(output);
        var now = DateTime.UtcNow;
        var parsed = SteamActivity.ParseLocal(Vdf("480 { Playtime 125 LastPlayed 1700000000 Playtime2w 15 } 481 { Playtime 0 LastPlayed 0 }"), now, CancellationToken.None);
        Check(parsed[480].Minutes == 125 && parsed[480].RecentMinutes == 15, "Minute fields");
        Check(parsed[480].LastPlayedUtc.Value.Kind == DateTimeKind.Utc && parsed[480].SnapshotUtc == now, "UTC and snapshot");
        Check(parsed[481].Minutes == 0 && !parsed[481].LastPlayedUtc.HasValue, "Zero is distinct from missing");
        Check(SteamActivity.ParseLocal(Vdf("480 { Playtime -1 LastPlayed -2 }"), now, CancellationToken.None).Count == 0, "Reject negatives");
        var invalid = SteamActivity.ParseLocal(Vdf("480 { Playtime 999999999999 LastPlayed 253402300799 }"), now, CancellationToken.None);
        Check(!invalid[480].HasData, "Reject impossible values");
        Check(SteamActivity.ParseLocal("apps { 480 { Playtime 10 } }", now, CancellationToken.None).Count == 0, "Exact hierarchy");
        Check(SteamActivity.ParseLocal(Vdf("480 { nested { Playtime 10 } }"), now, CancellationToken.None).Count == 0, "Ignore unrelated nesting");
        Check(SteamActivity.ParseLocal(Vdf("480 { // comment\n \"pLaYtImE\" \"12\" }"), now, CancellationToken.None)[480].Minutes == 12, "Comments, quoted values, case");
        Check(SteamActivity.ParseLocal("Root { value \"{\" } " + Vdf("480 { Playtime 9 }"), now, CancellationToken.None)[480].Minutes == 9, "Quoted brace stays scalar");
        Check(SteamActivity.ParseLocal(Vdf("x { Playtime 5 } 0 { Playtime 5 }"), now, CancellationToken.None).Count == 0, "Valid app identity only");
        var cancel = new CancellationTokenSource(); cancel.Cancel(); bool cancelled = false;
        try { SteamActivity.ParseLocal(Vdf(""), now, cancel.Token); } catch (OperationCanceledException) { cancelled = true; }
        Check(cancelled, "Parser cancellation"); cancel.Dispose();
        string root = Path.Combine(output, "synthetic-steam"); Directory.CreateDirectory(Path.Combine(root, "userdata", "42")); Directory.CreateDirectory(Path.Combine(root, "config"));
        Check(SteamActivity.SelectAccount(root, 42, true) == 42, "Active account");
        Check(SteamActivity.SelectAccount(root, 999, true) == null, "No wrong-account fallback");
        File.WriteAllText(Path.Combine(root, "config", "loginusers.vdf"), "users { 76561197960265770 { MostRecent 1 } }");
        Check(SteamActivity.SelectAccount(root, null, false) == 42, "Offline most recent");
        Check(SteamActivity.SelectAccount(root, 0, true) == null, "Running without logged-in user");
        Directory.CreateDirectory(Path.Combine(root, "userdata", "43"));
        File.WriteAllText(Path.Combine(root, "config", "loginusers.vdf"), "users { 76561197960265770 { MostRecent 1 } 76561197960265771 { MostRecent 1 } }");
        Check(SteamActivity.SelectAccount(root, null, false) == null, "Ambiguous offline account");
        Check(!new Config().SteamPersonalData && !new Config().HoverCardLarge, "Opt-in defaults");
        Check(GameInfo.AppIdDe(new ItemSpec { Key = "steam:480" }) == 480, "Exact key identity");
        Check(GameInfo.AppIdDe(new ItemSpec { Target = "steam://rungameid/480" }) == 480, "Exact shortcut identity");
        Check(GameInfo.AppIdDe(new ItemSpec { Name = "Spacewar" }) == 0, "No guessed personal identity");

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        app.Resources.MergedDictionaries.Add(XamlAssets.LoadDictionary("styles"));
        var cfg = new Config { Theme = "horizonte", HoverCard = true };
        var window = XamlAssets.Load<Window>("window"); window.ShowActivated = false; window.Opacity = 0;
        var dock = new GamesDock.Dock(window, cfg); window.Show();
        var item = new ItemSpec { Key = "steam:480", AppId = "480", Origin = "steam", Name = "Exploradores del horizonte", Kind = "game" };
        string url = "https://example.invalid/card-fixture.png";
        Directory.CreateDirectory(GameInfo.Dir);
        string path = (string)typeof(GameInfo).GetMethod("Archivo", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { url });
        var art = new DrawingVisual();
        using (var c = art.RenderOpen())
        {
            c.DrawRectangle(new LinearGradientBrush(Color.FromRgb(20, 92, 102), Color.FromRgb(228, 152, 95), 20), null, new Rect(0, 0, 900, 420));
            c.DrawEllipse(new SolidColorBrush(Color.FromRgb(255, 219, 161)), null, new Point(670, 130), 64, 64);
            var ridge = new StreamGeometry(); using (var g = ridge.Open()) { g.BeginFigure(new Point(0, 420), true, true); g.LineTo(new Point(250, 180), true, false); g.LineTo(new Point(510, 360), true, false); g.LineTo(new Point(760, 230), true, false); g.LineTo(new Point(900, 420), true, false); }
            c.DrawGeometry(new SolidColorBrush(Color.FromRgb(13, 41, 49)), null, ridge);
        }
        var banner = new RenderTargetBitmap(900, 420, 96, 96, PixelFormats.Pbgra32); banner.Render(art);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(banner)); using (var stream = File.Create(path)) encoder.Save(stream);
        var memo = (IDictionary)typeof(GameInfo).GetField("Memo", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        memo[GameInfo.Clave(item)] = new Ficha { Nombre = item.Name, Generos = "Aventura · Exploración", Autores = "Estudio de ejemplo", Lanzamiento = "2026", Sinopsis = "Cruza paisajes desconocidos, descubre antiguas señales y encuentra tu propio camino entre las montañas.", Carulas = new System.Collections.Generic.List<string> { url } };
        dock.EnsureCard(); var card = (HoverCard)Field(dock, "_card"); var win = (Window)Field(card, "_win"); win.Topmost = false;
        foreach (bool large in new[] { false, true })
        foreach (bool personal in new[] { false, true })
        foreach (string language in new[] { "es", "en" })
        {
            Lang.Apply(language); cfg.HoverCardLarge = large; cfg.SteamPersonalData = personal;
            Set(card, "_activityApp", 480); Set(card, "_activityResolved", true);
            Set(card, "_activityInfo", new SteamActivityInfo { Minutes = 1530, LastPlayedUtc = now.AddDays(-1), RecentMinutes = 120, SnapshotUtc = now });
            Call(card, "Rellenar", item); win.Show(); win.UpdateLayout();
            Check(Math.Abs(win.Width - (large ? 368 : 320)) < .01, "Whole card scale");
            var frame = (Border)Field(card, "_personalFrame");
            Check((frame.Visibility == Visibility.Visible) == personal, "Personal toggle");
            Check(win.ActualHeight <= dock.MonitorWorkArea.Height, "Card stays within monitor height");
            var clip = ((Border)Field(card, "_cartel")).Clip;
            Check(clip != null && clip.IsFrozen && !clip.FillContains(new Point(0, 0)), "Clipped upper corner");
            Check(clip.FillContains(new Point(100, 1)) && clip.FillContains(new Point(1, ((Border)Field(card, "_cartel")).ActualHeight - 1)), "Banner edges");
            Check(((StackPanel)Field(card, "_texto")).Margin.Left == 16, "Natural text margins");
            if (personal) Check(((TextBlock)Field(card, "_personal")).Text.Contains("25.5") || ((TextBlock)Field(card, "_personal")).Text.Contains("25,5"), "Personal hours");
            Save(win, Path.Combine(output, "card-" + language + (large ? "-115" : "-100") + (personal ? "-personal" : "") + ".png"));
        }
        Set(card, "_activityInfo", null); Call(card, "Rellenar", item);
        Check(((TextBlock)Field(card, "_personal")).Text == Lang.T("Sin datos locales para este juego."), "Missing data honest");
        cfg.SteamPersonalData = false; card.Salir();
        Check(Field(card, "_activityInfo") == null && Field(card, "_activityRequest") == null, "Disable clears personal state");
        cfg.HoverCardLarge = true; cfg.Save(); var loaded = Config.Load();
        Check(loaded.HoverCardLarge && !loaded.SteamPersonalData, "Settings persistence");
        win.Close(); window.Close(); app.Shutdown();
    }
}
