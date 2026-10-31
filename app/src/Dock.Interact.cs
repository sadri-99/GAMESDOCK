using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace GamesDock
{
    // Capa de interaccion del dock: cola de iconos, arrastre con fantasma, lanzado, fijados, menus,
    // buscador y entrada/salida. Va aparte de Dock.cs porque Dock.cs es geometria y muelles, y aqui lo
    // que hay es gestos y efectos sobre el sistema.
    public partial class Dock
    {
        // ------------------------------------------------------------------ cola de iconos
        struct IconJob { public Button Btn; public ItemSpec It; public string Path; }

        readonly Queue<IconJob> _iconQueue = new Queue<IconJob>();
        bool _iconBusy, _trimArmed;

        void EnqueueIcon(Button btn, ItemSpec it)
        {
            _iconQueue.Enqueue(new IconJob { Btn = btn, It = it, Path = it.Icon });
            Win.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(PumpIcons));
        }

        void PumpIcons()
        {
            if (_iconBusy) return;
            _iconBusy = true;
            int n = 0;
            while (_iconQueue.Count > 0 && n < 4)
            {
                var job = _iconQueue.Dequeue();
                // siempre pasa por Resolve: el config puede traer un .exe, un .lnk o un .url en "icon"
                // (los escribio la version PowerShell) y pintarlos tal cual deja el tile en negro
                if (job.It != null)
                {
                    string png = Icons.Resolve(job.It);
                    if (png != null) { job.Path = png; job.It.Icon = png; }
                }
                if (job.Btn != null) SetIconNow(job.Btn, job.Path);
                n++;
            }
            _iconBusy = false;
            if (_iconQueue.Count > 0)
                Win.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(PumpIcons));
            else if (_trimArmed) { _trimArmed = false; Memory.Trim(); }
        }

        public void ArmTrim() { _trimArmed = true; }

        // ------------------------------------------------------------------ arrastre del tile
        Point _dragStart;
        bool _dragHasStart;
        Ghost _ghost;
        Spring _ghostSpr, _ghostFade;

        public void EnsureGhost()
        {
            if (_ghost != null) return;
            _ghost = new Ghost(Cfg.IconSize);
            // arranque a mano: Move ignora el objetivo si ya era el de la ultima vez, y aqui lo que se
            // cambia es la POSICION del muelle, no su objetivo
            _ghostSpr = Spring.Make("ghost", 700.0, 0.68, null);
            _ghostFade = Spring.Make("ghostfade", 420.0, 1.0, null);
            _ghostSpr.Apply = s =>
            {
                var st = (ScaleTransform)_ghost.Box.RenderTransform;
                st.ScaleX = st.ScaleY = Math.Max(0.01, s.X);
            };
            _ghostFade.Apply = s => { _ghost.Win.Opacity = s.X; };
            Anim.Add(_ghostSpr);
            Anim.Add(_ghostFade);
        }

        static double DipScale(Visual v)
        {
            try
            {
                var ps = PresentationSource.FromVisual(v);
                if (ps != null) return ps.CompositionTarget.TransformToDevice.M11;
            }
            catch { }
            return 1.0;
        }

        void MoveGhost(Point p)
        {
            if (_ghost == null) return;
            _ghost.MoveTo(Win, p, Layout.Margin, DipScale(Win));
        }

        void StartGhost(Button btn, Point p)
        {
            EnsureGhost();
            _ghost.Resize(Cfg.IconSize);
            var bd = (Border)btn.Content;
            _ghost.Box.Background = bd.Background;
            _ghost.Box.CornerRadius = bd.CornerRadius;
            // la letra de respaldo vive dentro del borde del tile: se CLONA, un visual no puede tener
            // dos padres
            _ghost.Box.Child = null;
            var t = bd.Child as TextBlock;
            if (t != null)
            {
                _ghost.Box.Child = new TextBlock
                {
                    Text = t.Text,
                    FontSize = t.FontSize,
                    Foreground = t.Foreground,
                    FontFamily = t.FontFamily,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }
            MoveGhost(p);
            if (!_ghost.Win.IsVisible) _ghost.Win.Show();
            _ghostSpr.X = 0.42; _ghostSpr.V = 0.0; _ghostSpr.Target = 0.0; _ghostSpr.Settled = null;
            _ghostFade.X = 0.0; _ghostFade.V = 0.0; _ghostFade.Target = 0.0; _ghostFade.Settled = null;
            _ghost.Win.Opacity = 0.0;
            Anim.Move(_ghostSpr, 1.0);
            Anim.Move(_ghostFade, 1.0);
        }

        void EndGhost(Point p)
        {
            if (_ghost == null) return;
            MoveGhost(p);
            // se guarda, no se cierra: la ventanita cuesta montarla una vez y vaciarla por completo
            // cada vez que se suelta un icono es volver a pagarla en el siguiente arrastre
            _ghostFade.Settled = _ghost.Park;
            Anim.Move(_ghostSpr, 0.0);
            Anim.Move(_ghostFade, 0.0);
        }

        public void CloseGhost()
        {
            if (_ghost == null) return;
            try { _ghost.Win.Close(); } catch { }
            _ghost = null;
        }

        void TileDown(object s, MouseButtonEventArgs e)
        {
            var btn = (Button)s;
            DragBtn = btn;
            _dragStart = e.GetPosition(RootGrid);
            _dragHasStart = true;
            DragMoved = false;
            // el boton se hunde bajo el dedo antes de lanzar nada: es el detalle que hace que el dock
            // se sienta fisico en lugar de un mapa de bits que cambia de tamano
            _pressBtn = btn;
            SetTileTargets();
        }

        void TileMove(object s, MouseEventArgs e)
        {
            var btn = (Button)s;
            if (!ReferenceEquals(DragBtn, btn) || !_dragHasStart) return;
            if (e.LeftButton != MouseButtonState.Pressed) { DragBtn = null; return; }
            var p = e.GetPosition(RootGrid);
            if (!DragMoved)
            {
                double dx = p.X - _dragStart.X, dy = p.Y - _dragStart.Y;
                if (Math.Sqrt(dx * dx + dy * dy) < SystemParameters.MinimumHorizontalDragDistance) return;
                DragMoved = true;
                Panel.SetZIndex(btn, 30);
                // el hueco de origen se queda atenuado: es la referencia de donde salio el icono y de
                // donde vuelve a caer si se suelta dentro
                btn.Opacity = 0.3;
                StartGhost(btn, p);
                Log.Write("arrastrando: " + ((ItemSpec)btn.Tag).Name);
            }
            if (!btn.IsMouseCaptured) btn.CaptureMouse();
            MoveDraggedTile(p);
            MoveGhost(p);
        }

        void TileUp(object s, MouseButtonEventArgs e)
        {
            var btn = (Button)s;
            if (ReferenceEquals(_pressBtn, btn)) { _pressBtn = null; SetTileTargets(); }
            if (!DragMoved) return;
            e.Handled = true;          // soltar despues de arrastrar no tiene que lanzar nada
            EndTileDrag(e.GetPosition(RootGrid));
        }

        void TileLostCapture(object s, MouseEventArgs e)
        {
            var btn = (Button)s;
            if (ReferenceEquals(_pressBtn, btn)) { _pressBtn = null; SetTileTargets(); }
            // perdida de captura = arrastre cortado por fuera (Alt+Tab, menu del sistema, etc). Sin esto
            // el fantasma quedaria clavado en pantalla con el tile de origen apagado para siempre
            if (DragMoved && ReferenceEquals(DragBtn, btn))
            {
                btn.Opacity = 1.0;
                EndGhost(Mouse.GetPosition(RootGrid));
                DragBtn = null; DragMoved = false; _dragHasStart = false;
                Panel.SetZIndex(btn, 0);
            }
        }

        int SlotAt(Point p)
        {
            int n = Tiles.Count;
            if (n <= 0) return -1;
            double u = Lay.Vert ? p.Y : p.X;
            double off = Lay.Vert ? StripScroll.VerticalOffset : StripScroll.HorizontalOffset;
            int k = (int)Math.Floor((u - Lay.RowInset + off) / Lay.Pitch);
            return Math.Max(0, Math.Min(n - 1, k));
        }

        void MoveDraggedTile(Point p)
        {
            if (DragBtn == null) return;
            int cur = Tiles.IndexOf(DragBtn);
            int k = SlotAt(p);
            if (k < 0 || k == cur) { UpdateProximity(p); return; }
            Strip.Children.Remove(DragBtn);
            Strip.Children.Insert(k, DragBtn);
            Tiles.Clear();
            foreach (var c in Strip.Children) { var b = c as Button; if (b != null) Tiles.Add(b); }
            FitPill();
            UpdateProximity(p);
        }

        void EndTileDrag(Point p)
        {
            var b = DragBtn;
            DragBtn = null; DragMoved = false; _dragHasStart = false;
            if (b == null) return;
            b.Opacity = 1.0;
            EndGhost(p);
            try { b.ReleaseMouseCapture(); } catch { }
            Panel.SetZIndex(b, 0);
            double b2 = Layout.Margin + 6.0;
            bool fuera = p.X < -b2 || p.Y < -b2 || p.X > RootGrid.ActualWidth + b2 || p.Y > RootGrid.ActualHeight + b2;
            if (fuera)
            {
                DropFromDock((ItemSpec)b.Tag);
                return;
            }
            CommitOrder();
            ResetProximity();
        }

        void CommitOrder()
        {
            var keys = new List<string>();
            foreach (var t in Tiles) { var it = t.Tag as ItemSpec; if (it != null) keys.Add(it.Key ?? string.Empty); }
            if (keys.Count == 0) return;
            var rest = new List<string>();
            foreach (var k in Cfg.Order) if (!keys.Contains(k)) rest.Add(k);
            Cfg.Order = keys;
            foreach (var k in rest) Cfg.Order.Add(k);
            Log.Write("orden guardado: " + keys.Count);
            Cfg.Save();
        }

        // soltar un icono fuera de la barra lo oculta: se puede devolver desde el menu de la pildora
        void DropFromDock(ItemSpec it)
        {
            if (it == null) return;
            string k = it.Key ?? string.Empty;
            Cfg.Order.RemoveAll(x => k == x);
            Log.Write("sacado del dock: " + it.Name);
            HideFromDock(it);
        }

        // ------------------------------------------------------------------ fijados
        PinDto EnsurePin(ItemSpec it)
        {
            string k = it.Key ?? string.Empty;
            foreach (var p in Cfg.Pins) if (p.Key == k) { it.Pin = p; return p; }
            var pin = new PinDto
            {
                Key = k,
                Name = it.Name,
                Fav = false,
                Hidden = false,
                Link = it.Link,
                Target = it.Target,
                Args = it.Arguments,
                Work = it.Work,
                Origin = it.Origin,
                Kind = it.Kind,
                Icon = it.Icon,
                Added = DateTime.Now.ToString("o")
            };
            Cfg.Pins.Add(pin);
            it.Pin = pin;
            return pin;
        }

        void SetPinOverride(ItemSpec it, Action<PinDto> cambio)
        {
            var p = EnsurePin(it);
            cambio(p);
        }

        public void ToggleFav(ItemSpec it)
        {
            var p = EnsurePin(it);
            p.Fav = !p.Fav;
            it.Fav = p.Fav;
            Cfg.Save();
            Refresh(false);
            if (it.Fav) SetSection("fav");
        }

        public void HideFromDock(ItemSpec it)
        {
            var p = EnsurePin(it);
            p.Hidden = true;
            p.Fav = false;
            Cfg.Save();
            Refresh(false);
        }

        public void UnhidePin(string key)
        {
            foreach (var p in Cfg.Pins) if (p.Key == key) p.Hidden = false;
            Cfg.Save();
            Rebuild();
        }

        public void SetSection(string id)
        {
            Cfg.Section = id;
            Refresh(false);
            Cfg.Save();
        }

        public void RegisterUse(ItemSpec it)
        {
            string k = it.Key ?? string.Empty;
            UsageDto u;
            if (Cfg.Usage.TryGetValue(k, out u) && u != null) { u.N = u.N + 1; u.D = DateTime.Now.ToString("o"); }
            else Cfg.Usage[k] = new UsageDto { N = 1, D = DateTime.Now.ToString("o") };
            it.Fav = it.Pin != null && it.Pin.Fav;
            Cfg.Save();
        }

        // ------------------------------------------------------------------ lanzar
        public void StartEntry(ItemSpec it, string via)
        {
            if (it == null) return;
            Log.Write("Lanzando (" + via + "): " + it.Name);
            try
            {
                if (Cfg.MinimizeAll) Shell.MinimizeAll();
                var p = Launcher.Start(it);
                RegisterUse(it);
                WatchProcess(p);
                if (Cfg.HideOnLaunch) HideDock();
            }
            catch (Exception ex)
            {
                Log.Write("ERROR al lanzar " + it.Name + ": " + ex.Message);
                MessageBox.Show(Win, "No se pudo lanzar:\n" + it.Name + "\n\n" + ex.Message, "GamesDock",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                ShowDock();
            }
        }

        void WatchProcess(Process p)
        {
            try
            {
                if (p == null || p.HasExited) return;
                p.EnableRaisingEvents = true;
                p.Exited += delegate
                {
                    Win.Dispatcher.BeginInvoke(new Action(delegate { if (_hidden) ShowDock(); }));
                };
            }
            catch { }
        }

        // ------------------------------------------------------------------ anadir por arrastre
        // que se puede soltar encima de la barra: ficheros arrastrados (acceso .lnk, .exe, .url,
        // carpeta), la direccion de una pestana del navegador y texto que sea una ruta o un enlace
        // de verdad. Los formatos van por nombre crudo: DataFormats no tiene clave Uri.
        public static List<string> DropPaths(IDataObject data)
        {
            var outList = new List<string>();
            if (data == null) return outList;
            foreach (var f in new[] { "FileDrop", "UniformResourceLocatorW", "UniformResourceLocator" })
            {
                if (!data.GetDataPresent(f)) continue;
                var v = data.GetData(f);
                var arr = v as Array;
                if (arr != null && v is string[]) { foreach (var s in (string[])arr) outList.Add(s); return outList; }
                if (v != null && !(v is string)) { foreach (var s in (System.Collections.IEnumerable)v) outList.Add(Convert.ToString(s)); return outList; }
                if (v is string) { outList.Add((string)v); return outList; }
            }
            foreach (var f in new[] { "Text", "UnicodeText" })
            {
                if (!data.GetDataPresent(f)) continue;
                string txt = Convert.ToString(data.GetData(f));
                foreach (var line in txt.Split('\n'))
                {
                    string t = line.Trim();
                    if (t.Length > 0 && (RegexLooksLikeUrl(t) || PathExists(t))) outList.Add(t);
                }
                if (outList.Count > 0) return outList;
            }
            return outList;
        }

        static bool RegexLooksLikeUrl(string t)
        {
            return System.Text.RegularExpressions.Regex.IsMatch(t, @"^\w+://");
        }

        static bool PathExists(string p)
        {
            try { return File.Exists(p) || Directory.Exists(p); } catch { return false; }
        }

        public bool Batch;
        readonly List<string> _batchFail = new List<string>();

        public void AddPinsBatch(IEnumerable<string> rutas)
        {
            var lista = new List<string>();
            foreach (var r in rutas) if (!string.IsNullOrEmpty(r)) lista.Add(r.Trim());
            if (lista.Count == 0) return;
            Batch = true;
            _batchFail.Clear();
            try
            {
                foreach (var f in lista) AddPinFromPath(f, null);
            }
            finally
            {
                Batch = false;
                Rebuild();
            }
            if (_batchFail.Count > 0)
            {
                string cabeza = _batchFail.Count == 1 ? "No se pudo anadir:" : "No se pudieron anadir:";
                MessageBox.Show(Win, cabeza + "\n" + string.Join("\n", _batchFail.ToArray()), "GamesDock");
            }
            _batchFail.Clear();
        }

        // devuelve la clave del fijado nuevo, o null si ya estaba o fallo
        public string AddPinFromPath(string path, string nameHint)
        {
            try
            {
                string link = null, target = null, argLine = null, work = null, hint = null;
                string origin = "custom", key, name;
                // los accesos de Steam del escritorio son .url con URL=steam://rungameid/N: hay que
                // keyarlos igual que el indice, si no el tile nuevo no coincide con el juego detectado
                string url = Path.GetExtension(path).Equals(".url", StringComparison.OrdinalIgnoreCase)
                    ? Catalog.UrlShortcutTarget(path) : null;
                var sid = url != null ? System.Text.RegularExpressions.Regex.Match(url, @"(?i)^steam://rungameid/(\d+)") : null;

                if (System.Text.RegularExpressions.Regex.IsMatch(path, @"^\w+://") || path.StartsWith("steam:"))
                {
                    target = path;
                    key = "uri:" + path.ToLowerInvariant();
                    name = !string.IsNullOrEmpty(nameHint) ? nameHint : Path.GetFileName(path);
                }
                else if (Path.GetExtension(path).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    link = path;
                    var s = ShellLink.Resolve(path);
                    if (s == null || string.IsNullOrEmpty(s.Target)) throw new Exception("El acceso directo no tiene destino.");
                    target = s.Target; argLine = s.Args; work = s.Work; hint = s.Target;
                    key = target.ToLowerInvariant() + "|" +
                          System.Text.RegularExpressions.Regex.Replace(argLine ?? string.Empty, @"\s+", " ").Trim().ToLowerInvariant();
                    name = !string.IsNullOrEmpty(nameHint) ? nameHint : Catalog.CleanName(Path.GetFileName(path));
                }
                else if (sid != null && sid.Success)
                {
                    link = path; target = url; hint = path;
                    key = "steam:" + sid.Groups[1].Value;
                    name = !string.IsNullOrEmpty(nameHint) ? nameHint : Catalog.CleanName(Path.GetFileName(path));
                }
                else
                {
                    target = path; work = Catalog.DirOf(path); hint = path;
                    key = path.ToLowerInvariant() + "|";
                    name = !string.IsNullOrEmpty(nameHint) ? nameHint : Catalog.CleanName(Path.GetFileName(path));
                }

                PinDto cur = null;
                foreach (var p in Cfg.Pins) if (p.Key == key) { cur = p; break; }
                if (cur != null && cur.Fav)
                {
                    Log.Write("ya fijado: " + name);
                    return null;
                }

                string kind = (target.StartsWith("steam:") || Catalog.IsGameDir(target)) ? "game" : "app";
                if (cur == null)
                {
                    cur = new PinDto { Key = key };
                    Cfg.Pins.Add(cur);
                }
                // se reescribe la entrada existente: la clave ya estaba y estaba oculta, el arrastre
                // nuevo la reaviva en vez de dejar dos fijados para el mismo juego
                cur.Name = name;
                cur.Fav = true;
                cur.Hidden = false;
                cur.Link = link;
                cur.Target = target;
                cur.Args = argLine;
                cur.Work = work;
                cur.Origin = origin;
                cur.Kind = kind;
                cur.Icon = hint;
                cur.Added = DateTime.Now.ToString("o");
                // un fijado explicito tiene que verse: si la seccion activa lo filtra, salta a Favoritos,
                // la unica donde lo fijado a mano esta siempre
                if ((Cfg.Section == "games" && kind != "game") || (Cfg.Section == "apps" && kind != "app"))
                {
                    Cfg.Section = "fav";
                    Log.Write("seccion cambiada: fav");
                }
                Cfg.Save();
                if (!Batch) Rebuild();
                Log.Write("fijado: " + name + " (" + key + ") " + kind);
                return key;
            }
            catch (Exception ex)
            {
                Log.Write("fijar fallida " + path + ": " + ex.Message);
                if (Batch) _batchFail.Add(path);
                else MessageBox.Show(Win, "No se pudo anadir:\n" + path + "\n\n" + ex.Message, "GamesDock");
                return null;
            }
        }

        // reescanea y vuelve a montar la fila; es lo caro, asi que solo en cambios de indice
        public void Rebuild()
        {
            if (Cat != null) Cat.Build();
            if (Cat != null) _trimArmed = true;
            Refresh(false);
        }

        public void ShowAddDialog()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Elige el juego o aplicacion",
                Filter = "Programas y accesos|*.exe;*.lnk;*.bat;*.cmd;*.url|Todos|*.*",
                CheckFileExists = true,
                Multiselect = true
            };
            if (dlg.ShowDialog(Win) == true) AddPinsBatch(dlg.FileNames);
        }

        // ------------------------------------------------------------------ menus
        ContextMenu _dockMenu, _itemMenu;
        MenuItem _verMenu, _temaMenu, _ocultosMenu;

        public ContextMenu DockMenu
        {
            get
            {
                if (_dockMenu != null) return _dockMenu;
                _dockMenu = new ContextMenu();
                _dockMenu.Items.Add(MenuRow("Buscar...", delegate { OpenSearch(); }));
                _dockMenu.Items.Add(MenuRow("Anadir app o juego...", delegate { ShowAddDialog(); }));

                _verMenu = new MenuItem { Header = "Que se ve" };
                foreach (var s in new[]
                {
                    new[] { "fav", "Favoritos" }, new[] { "games", "Juegos detectados" },
                    new[] { "apps", "Aplicaciones" }, new[] { "all", "Todo" }
                })
                {
                    string id = s[0];
                    var mi = new MenuItem { Header = s[1], Tag = id, IsCheckable = true };
                    mi.Click += (a, e) => SetSection(id);
                    _verMenu.Items.Add(mi);
                }
                _dockMenu.Items.Add(_verMenu);

                _temaMenu = new MenuItem { Header = "Tema" };
                foreach (var kv in Themes.All)
                {
                    string id = kv.Key;
                    var mi = new MenuItem { Header = kv.Value.Label, Tag = id, IsCheckable = true };
                    mi.Click += (a, e) => SetTheme(id);
                    _temaMenu.Items.Add(mi);
                }
                _dockMenu.Items.Add(_temaMenu);

                // lo que se sacó arrastrando fuera (o con "quitar del dock") vuelve a entrar por aqui
                _ocultosMenu = new MenuItem { Header = "Restaurar ocultos" };
                _dockMenu.Items.Add(_ocultosMenu);

                _dockMenu.Items.Add(MenuRow("Reescanear", delegate { Rebuild(); }));
                _dockMenu.Items.Add(MenuRow("Ajustes", delegate { Settings.Show(this); }));
                _dockMenu.Items.Add(new Separator());
                _dockMenu.Items.Add(MenuRow("Ocultar (Ctrl+Alt+D)", delegate { HideDock(); }));
                _dockMenu.Items.Add(MenuRow("Salir", delegate { Exiting = true; Win.Close(); }));

                _dockMenu.Opened += (a, e) =>
                {
                    foreach (var o in _verMenu.Items)
                    {
                        var mi = (MenuItem)o;
                        mi.IsChecked = Cfg.Section == Convert.ToString(mi.Tag);
                    }
                    foreach (var o in _temaMenu.Items)
                    {
                        var mi = (MenuItem)o;
                        mi.IsChecked = Cfg.Theme == Convert.ToString(mi.Tag);
                    }
                    UpdateOcultosMenu();
                };
                return _dockMenu;
            }
        }

        MenuItem MenuRow(string header, Action accion)
        {
            var mi = new MenuItem { Header = header };
            mi.Click += delegate { accion(); };
            return mi;
        }

        void UpdateOcultosMenu()
        {
            _ocultosMenu.Items.Clear();
            var ocultos = new List<PinDto>();
            foreach (var p in Cfg.Pins) if (p.Hidden) ocultos.Add(p);
            if (ocultos.Count == 0)
            {
                _ocultosMenu.Items.Add(new MenuItem { Header = "(nada oculto)", IsEnabled = false });
                _ocultosMenu.IsEnabled = false;
                return;
            }
            _ocultosMenu.IsEnabled = true;
            ocultos.Sort((a, b) => string.Compare(a.Name ?? string.Empty, b.Name ?? string.Empty, StringComparison.OrdinalIgnoreCase));
            foreach (var p in ocultos)
            {
                string k = p.Key;
                var mi = new MenuItem { Header = p.Name, Tag = k };
                mi.Click += (a, e) => UnhidePin(k);
                _ocultosMenu.Items.Add(mi);
            }
        }

        public void ShowItemMenu(ItemSpec it, Button owner)
        {
            if (it == null) return;
            _itemMenu = new ContextMenu();
            var acciones = new List<MenuItem>
            {
                MenuRow(it.Fav ? "Quitar de favoritos" : "Fijar en favoritos", delegate { ToggleFav(it); }),
                MenuRow("Lanzar ahora", delegate { StartEntry(it, "menu"); }),
                MenuRow("Abrir carpeta del juego", delegate { OpenFolderOf(it); }),
                MenuRow("Renombrar", delegate { Rename(it); })
            };
            var destructiva = MenuRow(it.Origin == "custom" ? "Quitar del dock" : "Ocultar del dock",
                                      delegate { HideFromDock(it); });
            foreach (var a in acciones) _itemMenu.Items.Add(a);
            _itemMenu.Items.Add(new Separator());   // la accion destructiva, aparte
            _itemMenu.Items.Add(destructiva);
            _itemMenu.PlacementTarget = owner;
            _itemMenu.IsOpen = true;
        }

        void OpenFolderOf(ItemSpec it)
        {
            string t = it.Target;
            if (!string.IsNullOrEmpty(t) && !System.Text.RegularExpressions.Regex.IsMatch(t, @"^\w+://") && File.Exists(t))
                Launcher.SelectInExplorer(t);
            else
                MessageBox.Show(Win, "Ese juego se lanza por el cliente (Steam/Epic) y no tiene carpeta directa.", "GamesDock");
        }

        void Rename(ItemSpec it)
        {
            string n = Settings.ReadNamePrompt(this, it.Name);
            if (string.IsNullOrEmpty(n) || n == it.Name) return;
            SetPinOverride(it, p => p.Name = n);
            it.Name = n;
            Cfg.Save();
            Refresh(false);
        }

        public void SetTheme(string id)
        {
            Cfg.Theme = id;
            ApplyTheme(id);
            Cfg.Save();
        }

        // ------------------------------------------------------------------ buscador
        public void OpenSearch()
        {
            // el buscador necesita teclado: mientras esta abierto la barra sale momentaneamente del fondo
            SetDesktopLevel(false);
            SearchPanel.Visibility = Visibility.Visible;
            Win.Activate();
            SearchBox.Focus();
            SearchBox.SelectAll();
        }

        public void CloseSearch()
        {
            SearchPanel.Visibility = Visibility.Collapsed;
            AttachToDesktop();
            if (SearchBox.Text != "buscar...")
            {
                SearchBox.Foreground = Themes.Br(_theme.C["fieldFg"]);
                SearchBox.Text = "buscar...";
            }
            Refresh(false);
        }

        // ------------------------------------------------------------------ entrada y salida
        // el dock se desliza desde el borde que lo ancla y se enciende con un fundido corto, mientras
        // los iconos escalonan su pop. Al ocultarse hace la inversa y la ventana se apaga cuando el
        // muelle aterriza, no de golpe.
        bool _hidden;
        string _slideAxis = "y";
        double _slideFrom = 22.0;

        public bool Hidden { get { return _hidden; } }
        public bool Exiting;

        void ReadSlideOut()
        {
            _slideFrom = 22.0;
            switch (Cfg.Position)
            {
                case "top": _slideAxis = "y"; _slideFrom = -_slideFrom; break;
                case "left": _slideAxis = "x"; _slideFrom = -_slideFrom; break;
                case "right": _slideAxis = "x"; break;
                default: _slideAxis = "y"; break;
            }
        }

        public void PlayEntry()
        {
            ReadSlideOut();
            var tr = RootGrid.RenderTransform as TranslateTransform;
            if (tr == null) { tr = new TranslateTransform(); RootGrid.RenderTransform = tr; }
            if (_slideAxis == "x") { tr.X = _slideFrom; tr.Y = 0.0; } else { tr.Y = _slideFrom; tr.X = 0.0; }
            RootGrid.Opacity = 0.0;
            SlideSpr.Owner = _slideAxis;
            SlideSpr.X = _slideFrom; SlideSpr.V = 0.0; SlideSpr.Target = 0.0; SlideSpr.Settled = null;
            FadeSpr.X = 0.0; FadeSpr.V = 0.0; FadeSpr.Target = 1.0; FadeSpr.Settled = null;
            Anim.Start();
        }

        public void ShowDock()
        {
            _hidden = false;
            Win.Visibility = Visibility.Visible;
            if (DesktopLevel) SendToBottom();
            SetWindowOpacity((double)Cfg.IdleOpacity / 100.0, 160);
            PlayEntry();
        }

        public void HideDock()
        {
            if (_hidden) return;
            _hidden = true;
            ReadSlideOut();
            SlideSpr.Owner = _slideAxis;
            SlideSpr.Target = _slideFrom;
            FadeSpr.Target = 0.0;
            // la ventana se apaga cuando el muelle aterriza, no de golpe
            FadeSpr.Settled = delegate { if (_hidden) Win.Visibility = Visibility.Hidden; };
            Anim.Start();
        }

        public void ToggleDock()
        {
            if (_hidden || Win.Visibility != Visibility.Visible) ShowDock(); else HideDock();
        }
    }
}
