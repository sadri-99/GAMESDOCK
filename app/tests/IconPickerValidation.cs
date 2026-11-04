using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using GamesDock;

static class IconPickerValidation
{
    static int checks;
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    static T Field<T>(object owner, string name) { return (T)owner.GetType().GetField(name, Private).GetValue(owner); }
    static void Pump(int milliseconds)
    {
        var frame = new DispatcherFrame(); var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += delegate { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame);
    }
    static void Wait(Func<bool> ready, string reason)
    {
        var watch = Stopwatch.StartNew();
        while (!ready() && watch.ElapsedMilliseconds < 10000) Pump(20);
        Check(ready(), reason);
    }
    static void Save(Window window, string path)
    {
        var visual = (FrameworkElement)window.Content;
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling((visual.ActualWidth + 30) * 2), (int)Math.Ceiling((visual.ActualHeight + 30) * 2), 192, 192, PixelFormats.Pbgra32);
        bitmap.Render(visual); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(path)) encoder.Save(stream);
    }
    static Task Search(object picker, string text)
    {
        Field<TextBox>(picker, "_query").Text = text;
        return (Task)typeof(IconPicker).GetMethod("SearchAsync", Private).Invoke(picker, new object[] { false });
    }
    static object Create(GamesDock.Dock dock, ItemSpec original)
    {
        var picker = Activator.CreateInstance(typeof(IconPicker), Private, null, new object[] { dock, original.Name, null, original }, null);
        var window = Field<GlassPanel>(picker, "_par").Win;
        window.ShowActivated = false; window.Topmost = false; window.Opacity = 0;
        window.WindowStartupLocation = WindowStartupLocation.Manual; window.Left = window.Top = -10000;
        window.Show(); window.UpdateLayout(); return picker;
    }
    static string CardUrl(object picker)
    {
        var cards = Field<IList>(picker, "_fichas");
        return cards.Count == 0 ? "" : (string)cards[0].GetType().GetField("Url").GetValue(cards[0]);
    }
    static Button CloseButton(object picker)
    {
        var window = Field<GlassPanel>(picker, "_par").Win;
        var layout = (Grid)((Border)window.Content).Child;
        return (Button)((Grid)layout.Children[0]).Children[1];
    }
    static void Close(object picker) { CloseButton(picker).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }

    [STAThread]
    static int Main(string[] args)
    {
        try
        {
            Displays.EnableDpiScaling();
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            string output = Path.GetFullPath(args[0]); Directory.CreateDirectory(output);
            Environment.SetEnvironmentVariable("GAMESDOCK_DATA_DIR", Path.Combine(output, "picker-profile")); Paths.Ensure();
            Environment.SetEnvironmentVariable("STEAMGRIDDB_KEY", "fixture-key");
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources.MergedDictionaries.Add(XamlAssets.LoadDictionary("styles"));
            var source = new BitmapImage(new Uri(Path.GetFullPath(args[1])));
            var pictures = new List<byte[]>();
            foreach (int left in new[] { 68, 152, 236, 404, 656 })
            {
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(new CroppedBitmap(source, new Int32Rect(left, 22, 62, 62))));
                using (var stream = new MemoryStream()) { encoder.Save(stream); pictures.Add(stream.ToArray()); }
            }
            using (var server = new FixtureServer(pictures))
            {
                Environment.SetEnvironmentVariable("GAMESDOCK_SGD_BASE", server.Url + "/api");
                Net.AsegurarTls();
                Run(output, server);
            }
            Anim.Stop(); app.Shutdown();
            Console.WriteLine("PASS: " + checks + " community icon checks; alternative names, match selection, fixed close/search on scroll, cancellation, bounded thumbnails and ES/EN UI.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    static void Run(string output, FixtureServer server)
    {
        var cfg = new Config { Theme = "horizonte", SteamGridKey = "fixture-key" };
        var owner = XamlAssets.Load<Window>("window"); owner.Opacity = 0; owner.ShowActivated = false;
        var dock = new GamesDock.Dock(owner, cfg); owner.Show(); owner.Left = owner.Top = -10000;
        var original = new ItemSpec { Key = "original", Name = "Nombre original irreconocible", Kind = "game" };
        foreach (string language in new[] { "es", "en" })
        {
            Lang.Apply(language);
            var picker = Create(dock, original); var window = Field<GlassPanel>(picker, "_par").Win;
            Wait(() => Field<TextBlock>(picker, "_emptyTitle").Text == Lang.T("No se han encontrado iconos"), "Automatic no-match prevents opening the search window");
            Check(window.IsVisible && Field<Button>(picker, "_search").IsEnabled, "Search cannot recover an initial no-match");
            Check(Convert.ToString(Field<Button>(picker, "_search").Content) == Lang.T("Buscar"), "Search button was not translated");
            Field<TextBox>(picker, "_query").Text = "Alternate";
            Field<Button>(picker, "_search").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Wait(() => Field<CancellationTokenSource>(picker, "_request") == null && Field<IList>(picker, "_fichas").Count == 60,
                "The search button did not load alternative results");
            var cards = Field<IList>(picker, "_fichas");
            Check(cards.Count == 60 && Field<ComboBox>(picker, "_games").Items.Count == 2, "Alternative search did not offer games and icons");
            Check(CardUrl(picker).Contains("/full/1/"), "Wrong game was displayed after alternate search");
            foreach (object card in cards)
            {
                var image = (Image)card.GetType().GetField("Image").GetValue(card);
                Check(image.Source != null && image.Source.IsFrozen && image.Stretch == Stretch.Uniform, "Preview is missing, mutable or cropped");
            }
            Check(server.MaximumThumbs <= 4, "Thumbnail load opens more than four simultaneous downloads");
            var scroll = Field<ScrollViewer>(picker, "_scroll"); var close = CloseButton(picker); var query = Field<TextBox>(picker, "_query");
            double closeY = close.TranslatePoint(new Point(), window).Y, searchY = query.TranslatePoint(new Point(), window).Y;
            Save(window, Path.Combine(output, "Iconos-comunidad-" + language + ".png"));
            scroll.ScrollToBottom(); Pump(150);
            Check(scroll.VerticalOffset > 0, "Fixture did not exercise a scrolled gallery");
            Check(Math.Abs(close.TranslatePoint(new Point(), window).Y - closeY) < .1 && Math.Abs(query.TranslatePoint(new Point(), window).Y - searchY) < .1,
                "Close button or search moves away when scrolling");
            Check(close.IsVisible && close.TranslatePoint(new Point(), window).Y >= 0 && close.TranslatePoint(new Point(), window).Y < 100,
                "Close button is inaccessible after scrolling");
            Save(window, Path.Combine(output, "Iconos-comunidad-scroll-" + language + ".png"));
            var first = cards[0]; var preview = (Border)first.GetType().GetField("Preview").GetValue(first); var background = preview.Background;
            int downloaded = server.ThumbnailRequests;
            Field<CheckBox>(picker, "_light").IsChecked = true;
            Check(!ReferenceEquals(background, preview.Background) && server.ThumbnailRequests == downloaded, "Preview background refetches images or does not change");
            var task = Search(picker, "Alternate"); Wait(() => task.IsCompleted, "Cached search did not finish"); task.GetAwaiter().GetResult();
            Check(server.ThumbnailRequests == downloaded, "Repeated search ignores the thumbnail cache");
            Check(scroll.VerticalOffset == 0, "A new search keeps the old scroll offset");
            var games = Field<ComboBox>(picker, "_games"); games.IsDropDownOpen = true; Pump(40);
            var dropdown = (System.Windows.Controls.Primitives.Popup)games.Template.FindName("PART_Popup", games);
            Check(dropdown.IsOpen && ((FrameworkElement)dropdown.Child).ActualWidth >= games.ActualWidth - 1,
                "Matching-game dropdown does not open or has a collapsed width");
            games.SelectedIndex = 1; games.IsDropDownOpen = false;
            Wait(() => Field<CancellationTokenSource>(picker, "_request") == null && Field<IList>(picker, "_fichas").Count == 4, "Choosing another game does not load its icons");
            Check(CardUrl(picker).Contains("/full/2/"), "Choosing an edition still shows icons from the first match");
            query.Text = "Alternate";
            query.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(query), Environment.TickCount, Key.Enter)
                { RoutedEvent = Keyboard.KeyDownEvent });
            Wait(() => Field<CancellationTokenSource>(picker, "_request") == null && Field<IList>(picker, "_fichas").Count == 60, "Enter does not submit the search");
            Check(original.Name == "Nombre original irreconocible" && cfg.Pins.Count == 0, "Searching renamed the dock entry or changed preferences");

            var slow = Search(picker, "Slow"); Pump(60); var fast = Search(picker, "Fast");
            Wait(() => slow.IsCompleted && fast.IsCompleted, "Overlapping searches do not settle"); Pump(650);
            Check(CardUrl(picker).Contains("/full/2/") && Convert.ToString(((ComboBoxItem)Field<ComboBox>(picker, "_games").SelectedItem).Content) == "Fast match",
                "A delayed search overwrites the newest results");
            task = Search(picker, "Invalid"); Wait(() => task.IsCompleted, "Invalid-key response does not finish");
            Check(Field<TextBlock>(picker, "_emptyHint").Text == Lang.T("SteamGridDB no acepta la clave. Revísala en Ajustes → Iconos de la comunidad."), "Invalid API key is not explained inline");
            task = Search(picker, "Broken"); Wait(() => task.IsCompleted, "Broken-image search did not finish");
            Check(Field<TextBlock>(picker, "_footer").Text == Lang.T("Algunas vistas previas no se cargaron. Vuelve a buscar para reintentar."), "Broken thumbnail has no retry explanation");
            Check(Field<IDictionary>(picker, "_imageCache").Count <= 64, "Decoded image cache is unbounded");
            scroll.ScrollToBottom(); Pump(50); Close(picker); Pump(30);
            Check(!window.IsVisible && Field<string>(picker, "_elegida") == null, "Closing from a scrolled gallery applies an icon");
        }
        var exact = new ItemSpec { Key = "exact", Name = "A name that does not match", AppId = "777", Kind = "game" };
        int searchesBefore = server.SearchRequests;
        var direct = Create(dock, exact);
        Wait(() => Field<CancellationTokenSource>(direct, "_request") == null && Field<IList>(direct, "_fichas").Count == 4, "Steam ID lookup no longer works");
        Check(server.SearchRequests == searchesBefore, "Exact Steam lookup unnecessarily searches by name");
        var alternate = Search(direct, "Alternate"); Wait(() => alternate.IsCompleted, "Manual search after Steam ID did not finish");
        Check(CardUrl(direct).Contains("/full/1/"), "Steam ID overrides a manual alternate-name search");
        string chosen = CardUrl(direct); var choice = Field<IList>(direct, "_fichas")[0];
        ((RadioButton)choice.GetType().GetField("Btn").GetValue(choice)).RaiseEvent(new RoutedEventArgs(RadioButton.ClickEvent));
        Check(Field<string>(direct, "_elegida") == chosen && chosen.Contains("/full/"), "Selection returns a thumbnail URL instead of the original image");
        Check(exact.Name == "A name that does not match", "Choosing an icon changed the entry name");
        var pending = Create(dock, original);
        Wait(() => Field<CancellationTokenSource>(pending, "_request") == null, "Initial request did not settle");
        var waiting = Search(pending, "Slow"); var source = Field<CancellationTokenSource>(pending, "_request"); var token = source.Token;
        Close(pending); Wait(() => waiting.IsCompleted, "Closing leaves a request running");
        Check(token.IsCancellationRequested && Field<IList>(pending, "_fichas").Count == 0, "Closing did not cancel the outstanding search");
        var downloading = Create(dock, original);
        Wait(() => Field<CancellationTokenSource>(downloading, "_request") == null, "No-match request did not settle before thumbnail cancellation test");
        var downloadingTask = Search(downloading, "Pending");
        Wait(() => Field<IList>(downloading, "_fichas").Count == 3, "Slow thumbnail fixture was not displayed");
        var downloadToken = Field<CancellationTokenSource>(downloading, "_request").Token;
        Close(downloading); Wait(() => downloadingTask.IsCompleted, "Closing does not stop thumbnail workers"); Pump(500);
        Check(downloadToken.IsCancellationRequested, "Thumbnail requests are not cancelled on close");
        foreach (object card in Field<IList>(downloading, "_fichas"))
            Check(((Image)card.GetType().GetField("Image").GetValue(card)).Source == null, "A closed gallery still receives thumbnail updates");
        Check(!server.ThumbnailAuthorization, "API credentials were sent with public thumbnail requests");
        owner.Close();
    }

    sealed class FixtureServer : IDisposable
    {
        readonly TcpListener listener;
        readonly List<byte[]> pictures;
        volatile bool stopped;
        int activeThumbs;
        public string Url;
        public int SearchRequests, ThumbnailRequests, MaximumThumbs;
        public bool ThumbnailAuthorization;
        public FixtureServer(List<byte[]> pictures)
        {
            this.pictures = pictures; listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            Url = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port;
            Task.Run(async delegate
            {
                while (!stopped)
                {
                    try { var client = await listener.AcceptTcpClientAsync(); _ = Task.Run(() => Handle(client)); }
                    catch (ObjectDisposedException) { break; } catch (SocketException) { if (stopped) break; }
                }
            });
        }
        void Handle(TcpClient client)
        {
            bool thumb = false;
            try
            {
                using (client)
                using (var stream = client.GetStream())
                using (var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true))
                {
                    string line = reader.ReadLine(); if (line == null) return;
                    string path = Uri.UnescapeDataString(line.Split(' ')[1]); bool authorization = false;
                    while (!string.IsNullOrEmpty(line = reader.ReadLine())) if (line.StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase)) authorization = true;
                    byte[] bytes; string mime = "application/json"; int status = 200;
                    if (path.StartsWith("/thumb/"))
                    {
                        thumb = true; int active = Interlocked.Increment(ref activeThumbs);
                        lock (this) { MaximumThumbs = Math.Max(MaximumThumbs, active); ThumbnailAuthorization |= authorization; }
                        Interlocked.Increment(ref ThumbnailRequests);
                        string[] parts = path.Split('/'); int index = int.Parse(parts[3].Split('.')[0]);
                        Thread.Sleep(parts[2] == "4" ? 450 : 12);
                        bytes = parts[2] == "3" && index == 0 ? new byte[100] : pictures[index % pictures.Count]; mime = "image/png";
                    }
                    else
                    {
                        string json = "{\"success\":true,\"data\":[]}";
                        if (path.StartsWith("/api/search/autocomplete/"))
                        {
                            Interlocked.Increment(ref SearchRequests); string query = path.Substring("/api/search/autocomplete/".Length).ToLowerInvariant();
                            if (query == "slow") { Thread.Sleep(550); json = Games("1", "Slow match"); }
                            else if (query == "fast") json = Games("2", "Fast match");
                            else if (query == "alternate") json = "{\"success\":true,\"data\":[{\"id\":1,\"name\":\"Juego alternativo\"},{\"id\":2,\"name\":\"Otra edición\"}]}";
                            else if (query == "broken") json = Games("3", "Broken preview fixture");
                            else if (query == "pending") json = Games("4", "Slow thumbnail fixture");
                            else if (query == "invalid") { status = 401; json = "{\"success\":false,\"errors\":[\"Invalid key\"]}"; }
                        }
                        else if (path.StartsWith("/api/icons/"))
                        {
                            int id = path.Contains("/steam/") ? 2 : int.Parse(path.Substring(path.LastIndexOf('/') + 1));
                            int count = id == 1 ? 60 : id == 2 ? 4 : 3;
                            var entries = new List<string>();
                            for (int i = 0; i < count; i++) entries.Add("{\"id\":" + i + ",\"url\":\"" + Url + "/full/" + id + "/" + i + ".png\",\"thumb\":\"" + Url + "/thumb/" + id + "/" + i + ".png\",\"width\":256,\"height\":256,\"score\":" + (count - i) + "}");
                            json = "{\"success\":true,\"data\":[" + string.Join(",", entries) + "]}";
                        }
                        bytes = Encoding.UTF8.GetBytes(json);
                    }
                    byte[] header = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + " OK\r\nContent-Type: " + mime + "\r\nContent-Length: " + bytes.Length + "\r\nConnection: close\r\n\r\n");
                    stream.Write(header, 0, header.Length); stream.Write(bytes, 0, bytes.Length);
                }
            }
            catch (IOException) { } catch (SocketException) { } catch (ObjectDisposedException) { }
            finally { if (thumb) Interlocked.Decrement(ref activeThumbs); }
        }
        static string Games(string id, string name) { return "{\"success\":true,\"data\":[{\"id\":" + id + ",\"name\":\"" + name + "\"}]}"; }
        public void Dispose() { stopped = true; listener.Stop(); }
    }
}
