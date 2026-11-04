using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using GamesDock;

static class PagesValidation
{
    static int checks;
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Check(bool result, string reason) { if (!result) throw new Exception(reason); checks++; }
    static PageDto Page(Config cfg, string id) { return PageStore.Find(cfg, id); }
    static bool Has(Config cfg, string id, string key) { return Page(cfg, id).Members.Contains(key); }
    static void Pump()
    {
        var frame = new DispatcherFrame(); var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        timer.Tick += delegate { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame);
    }
    static void Save(FrameworkElement element, string path)
    {
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth + 30), (int)Math.Ceiling(element.ActualHeight + 30), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(path)) encoder.Save(stream);
    }
    static void Raise(MenuItem item) { item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, item)); }
    static IEnumerable<T> Find<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T) yield return (T)root;
        foreach (object child in LogicalTreeHelper.GetChildren(root))
            if (child is DependencyObject)
                foreach (T match in Find<T>((DependencyObject)child)) yield return match;
    }

    [STAThread]
    static int Main(string[] args)
    {
        try
        {
            string output = Path.GetFullPath(args[0]); Directory.CreateDirectory(output);
            Environment.SetEnvironmentVariable("GAMESDOCK_DATA_DIR", Path.Combine(output, "pages-profile"));
            Paths.Ensure(); Run(output); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    static void Run(string output)
    {
        // Load an actual old-schema document, then verify that migration preserves the old arrangement.
        File.WriteAllText(Paths.CfgFile, "{\"iconSize\":46,\"position\":\"bottom\",\"theme\":\"horizonte\",\"section\":\"all\",\"order\":[\"g2\",\"g1\"],\"pins\":[{\"key\":\"g1\",\"fav\":true},{\"key\":\"hidden\",\"hidden\":true}],\"groups\":[{\"id\":\"legacy\",\"members\":[\"g1\",\"g2\"]}]}");
        var cfg = Config.Load();
        var source = new List<ItemSpec> {
            new ItemSpec { Key = "g1", Name = "First", Kind = "game", Origin = "steam" },
            new ItemSpec { Key = "g2", Name = "Second", Kind = "game", Origin = "steam" },
            new ItemSpec { Key = "a1", Name = "Editor", Kind = "app", Origin = "start" },
            new ItemSpec { Key = "hidden", Name = "Hidden", Kind = "game", Origin = "steam" }
        };
        PageStore.Synchronize(cfg, source);
        Check(cfg.Section == "games" && cfg.Pages.Count == 3, "Old All selection was not normalized to the three fixed pages");
        Check(Has(cfg, "games", "g1") && Has(cfg, "games", "g2") && Has(cfg, "apps", "a1"), "Migration lost detected applications");
        Check(Has(cfg, "fav", "g1") && !Has(cfg, "fav", "g2"), "Migration changed manual favorites");
        Check(!Has(cfg, "games", "hidden"), "Migration restored an old hidden item unexpectedly");
        Check(Page(cfg, "games").Order[0] == "g2" && Page(cfg, "games").Groups.Count == 1, "Migration lost ordering or folders");
        Check(!ReferenceEquals(Page(cfg, "games").Groups[0], cfg.Groups[0]), "Migrated folders share mutable state");
        PageStore.Synchronize(cfg, source);
        Check(Page(cfg, "games").Groups.Count == 1, "Repeated scans duplicated folders");

        var custom = PageStore.Create(cfg, "Trabajo");
        Check(custom != null && custom.Members.Count == 0, "Custom page must start empty");
        PageStore.Add(cfg, custom.Id, "g1"); PageStore.Add(cfg, custom.Id, "g2");
        PageStore.Add(cfg, "games", "a1");
        Check(!Has(cfg, "fav", "a1"), "Adding an application to Games made it a favorite");
        custom.Order.Reverse();
        Check(Page(cfg, "games").Order[0] == "g2", "Ordering one page changed another");
        PageStore.Remove(cfg, "games", "g1");
        Check(Has(cfg, custom.Id, "g1") && Has(cfg, "fav", "g1"), "Removing from Games changed another page");
        Check(Page(cfg, "games").Groups.Count == 0 && cfg.Groups.Count == 1, "Page-local removal changed the legacy folder");
        PageStore.Synchronize(cfg, source);
        Check(!Has(cfg, "games", "g1"), "Rescanning resurrected a manually removed page member");
        PageStore.Remove(cfg, "fav", "g1");
        Check(Has(cfg, custom.Id, "g1") && !cfg.Pins[0].Fav, "Removing a favorite changed custom membership");
        for (int i = 1; i < 5; i++) Check(PageStore.Create(cfg, "Page " + i) != null, "Could not create five pages");
        Check(PageStore.Create(cfg, "Sixth") == null && PageStore.CustomCount(cfg) == 5, "Custom page limit was exceeded");
        Check(PageStore.Create(cfg, " ") == null && !PageStore.Rename(cfg, "fav", "Other"), "Invalid name or fixed-page rename was accepted");
        Check(!PageStore.Delete(cfg, "fav") && !PageStore.Delete(cfg, "games") && !PageStore.Delete(cfg, "apps"), "A fixed page was deletable");
        Check(PageStore.Rename(cfg, custom.Id, "Personal"), "Custom page could not be renamed");
        cfg.Section = custom.Id; cfg.Save();
        var loaded = Config.Load();
        Check(loaded.Section == custom.Id && Has(loaded, custom.Id, "g1"), "Pages did not persist across reload");
        Check(PageStore.CustomCount(loaded) == 5 && !Has(loaded, "games", "g1"), "Reload changed page membership or limits");
        var catalog = new Catalog(loaded) { Items = source };
        Check(catalog.Visible(custom.Id, null).Count == 2, "Custom page filter is incorrect");
        Check(catalog.Visible("fav", null).Count == 0, "Favorites is not manually controlled");
        loaded.Usage["g1"] = new UsageDto { N = 999 };
        Check(catalog.Visible(custom.Id, null)[0].Key == "g2", "Launching in another page changes the saved order");

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(XamlAssets.LoadDictionary("styles"));
        var owner = XamlAssets.Load<Window>("window"); owner.ShowActivated = false; owner.Opacity = 0;
        var dock = new GamesDock.Dock(owner, loaded) { Cat = catalog }; owner.Show(); owner.Left = owner.Top = -10000;
        dock.Refresh(false); Pump();
        dock.GroupInto(source[0], source[1]);
        Check(Page(loaded, custom.Id).Groups.Count == 1 && Page(loaded, "games").Groups.Count == 0, "Grouping leaked into another page");
        dock.UnGroup(Page(loaded, custom.Id).Groups[0]);

        // A file dropped on a custom page stays there, even when its kind is application.
        string file = Path.Combine(output, "sample-app.exe"); File.WriteAllText(file, "fixture only; never launched");
        dock.Batch = true;
        string key = dock.AddPinFromPath(file, null);
        Check(key != null && Has(loaded, custom.Id, key), "Drop did not add to the current page");
        Check(!Has(loaded, "fav", key) && !Has(loaded, "apps", key), "Drop leaked into favorites or Applications");
        Check(loaded.Section == custom.Id && dock.AddPinFromPath(file, null) == null, "Drop changed the page or duplicated an icon");
        dock.SetSection("games"); dock.AddPinFromPath(file, null);
        Check(Has(loaded, "games", key) && !Has(loaded, "fav", key), "Adding an app to Games changed Favorites");
        dock.SetSection("fav"); dock.AddPinFromPath(file, null);
        Check(Has(loaded, "fav", key) && Has(loaded, custom.Id, key), "Explicit favorite insertion affected another page");
        dock.Batch = false;
        int pageNumber = 1;
        foreach (var page in loaded.Pages)
            if (!PageStore.IsFixed(page.Id))
                Check(PageStore.Rename(loaded, page.Id, ("Página " + pageNumber++ + " para juegos y aplicaciones personales").Substring(0, 40)), "Long page name was rejected");

        foreach (string language in new[] { "es", "en" })
        {
            Lang.Apply(language); dock.InvalidateMenus();
            var menu = dock.DockMenu; menu.PlacementTarget = dock.RootGrid; menu.IsOpen = true; Pump();
            MenuItem pages = null, locked = null;
            foreach (object row in menu.Items)
            {
                var item = row as MenuItem; if (item == null) continue;
                string label = Convert.ToString(item.Header);
                Check(label != Lang.T("Buscar...") && label != Lang.T("Restaurar ocultos"), "Removed background menu action remains");
                if (label == Lang.T("Páginas")) pages = item;
                if (Convert.ToString(item.Tag) == "dock-lock") locked = item;
            }
            Check(pages != null && locked != null, "Pages or lock action is missing");
            int pageCount = 0; bool capped = false;
            foreach (object row in pages.Items)
            {
                var item = row as MenuItem; if (item == null) continue;
                if (item.Tag != null) { pageCount++; Check(Convert.ToString(item.Tag) != "all", "All page is still available"); }
                if (Convert.ToString(item.Header) == Lang.T("+ Añadir página")) capped = !item.IsEnabled;
            }
            Check(pageCount == 8 && capped, "Page menu does not show three fixed plus five capped custom pages");
            pages.IsSubmenuOpen = true; Pump();
            var popup = (System.Windows.Controls.Primitives.Popup)pages.Template.FindName("PART_Popup", pages);
            Save((FrameworkElement)popup.Child, Path.Combine(output, "Paginas-" + language + ".png"));
            pages.IsSubmenuOpen = false;
            locked.ApplyTemplate();
            var gutter = (Border)locked.Template.FindName("Chk", locked);
            Check(!locked.IsCheckable && gutter.Visibility == Visibility.Collapsed, "Lock still reserves a checkbox gutter");
            loaded.Locked = false; locked.IsChecked = false;
            Save(menu, Path.Combine(output, "Menu-" + language + "-unlocked.png"));
            Raise(locked); Pump();
            Check(loaded.Locked && locked.IsChecked && gutter.Visibility == Visibility.Collapsed, "Lock does not toggle without a tick");
            var surface = (Border)locked.Template.FindName("Bd", locked);
            Check(surface.Background != null && surface.BorderThickness.Left == 1, "Locked action is not highlighted");
            Save(menu, Path.Combine(output, "Menu-" + language + "-locked.png"));
            menu.IsOpen = false; dock.SetSection(custom.Id);
            dock.ShowItemMenu(source[0], dock.Tiles[0]); Pump();
            var itemMenu = (ContextMenu)typeof(GamesDock.Dock).GetField("_itemMenu", Private).GetValue(dock);
            bool addToPage = false;
            foreach (object row in itemMenu.Items)
            {
                var item = row as MenuItem; if (item == null) continue;
                string label = Convert.ToString(item.Header);
                Check(label != Lang.T("Ocultar del dock") && label != Lang.T("Cambiar icono...") && label != Lang.T("Lanzar ahora"), "Removed icon menu action remains");
                if (label == Lang.T("Añadir a página")) addToPage = true;
            }
            Check(addToPage, "No way to add existing icons to another page");
            Save(itemMenu, Path.Combine(output, "Menu-icono-" + language + ".png"));
            itemMenu.IsOpen = false;
            var build = typeof(Settings).GetMethod("BuildPanel", BindingFlags.Static | BindingFlags.NonPublic);
            var settings = (GlassPanel)build.Invoke(null, new object[] { dock });
            settings.Win.ShowActivated = false; settings.Win.Topmost = false; settings.Win.Opacity = 0;
            settings.Win.WindowStartupLocation = WindowStartupLocation.Manual; settings.Win.Left = settings.Win.Top = -10000;
            settings.Win.Show(); settings.Win.UpdateLayout(); Pump();
            int pageButtons = 0;
            foreach (var radio in Find<RadioButton>(settings.Body)) if (radio.GroupName == "seccion") pageButtons++;
            Check(pageButtons == 8 && settings.Win.ActualHeight <= SystemParameters.WorkArea.Height, "Eight pages do not fit in settings");
            foreach (var scroll in Find<ScrollViewer>(settings.Body)) Check(Convert.ToString(scroll.Tag) == "theme-gallery", "Scrolling escaped the theme gallery");
            foreach (var button in Find<System.Windows.Controls.Primitives.ButtonBase>(settings.Body))
            {
                var themeChip = button as RadioButton;
                if (themeChip != null && themeChip.GroupName == "tema") continue;
                var bounds = button.TransformToAncestor(settings.Win).TransformBounds(new Rect(button.RenderSize));
                Check(button.IsVisible && bounds.Bottom <= settings.Win.ActualHeight + 1 && bounds.Right <= settings.Win.ActualWidth + 1,
                    "A settings control is clipped with five long custom-page names");
            }
            Save((FrameworkElement)settings.Win.Content, Path.Combine(output, "Ajustes-8-paginas-" + language + ".png"));
            settings.Win.Close();
        }
        Check(PageStore.Delete(loaded, custom.Id) && Has(loaded, "games", key), "Deleting a custom page changes another page");
        Check(PageStore.Create(loaded, "Replacement") != null, "Deleting a page did not free a slot");
        owner.Close(); Anim.Stop(); app.Shutdown();
        Console.WriteLine("PASS: " + checks + " page checks; migration, persistence, isolated membership/order/folders, five-page limit and ES/EN menus.");
    }
}
