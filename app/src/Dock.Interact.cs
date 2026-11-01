using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
                // una carpeta no tiene icono propio: se pinta el mosaico de lo que hay dentro, y eso
                // tambien pasa por la cola porque extraer los iconos de los miembros cuesta disco
                if (job.It != null && Groups.IsGroup(job.It))
                {
                    if (job.Btn != null) SetMosaicNow(job.Btn, job.It);
                    n++;
                    continue;
                }
                // siempre pasa por Resolve: el config puede traer un .exe, un .lnk o un .url en "icon"
                // (heredados de versiones anteriores) y pintarlos tal cual deja el tile en negro
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

        public static double DipScale(Visual v)
        {
            try
            {
                var ps = PresentationSource.FromVisual(v);
                if (ps != null) return ps.CompositionTarget.TransformToDevice.M11;
            }
            catch { }
            return 1.0;
        }

        // ------------------------------------------------------------------ ficha de hover
        HoverCard _card;

        public void EnsureCard()
        {
            if (_card != null) return;
            _card = new HoverCard(this);
        }

        public void OcultarFicha()
        {
            if (_card != null) _card.Salir();
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
            else
            {
                // arrastrar una carpeta lleva el mosaico detras: un cuadro vacio no dice que se esta
                // moviendo un grupo
                var g = bd.Child as UniformGrid;
                if (g != null) _ghost.Box.Child = BuildMosaic((ItemSpec)btn.Tag);
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
            // pulsar es intentar algo (lanzar o arrastrar), no quedarse leyendo: la ficha estorba
            OcultarFicha();
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
                if (_grupoBtn != null) { _grupoBtn.Opacity = 1.0; Panel.SetZIndex(_grupoBtn, 0); _grupoBtn = null; }
                EndGhost(Mouse.GetPosition(RootGrid));
                DragBtn = null; DragMoved = false; _dragHasStart = false;
                Panel.SetZIndex(btn, 0);
            }
        }

        // posicion del cursor en unidades de hueco: 0 es el borde interior de la pildora y cada unidad
        // vale un icono, asi que el centro del hueco j cae en j + 0.5
        double SlotU(Point p)
        {
            double u = Lay.Vert ? p.Y : p.X;
            double off = Lay.Vert ? StripScroll.VerticalOffset : StripScroll.HorizontalOffset;
            return (u - Lay.RowInset + off) / Lay.Pitch;
        }

        // vecino marcado durante el arrastre: soltar el tile de encima mete uno dentro del otro
        Button _grupoBtn;

        void MarcarGrupo(Button b)
        {
            if (ReferenceEquals(b, _grupoBtn)) return;
            if (_grupoBtn != null) _grupoBtn.Opacity = 1.0;
            _grupoBtn = b;
            if (b == null) return;
            // la misma senal de fundido que el tile que se arrastra: los dos se apagan
            b.Opacity = 0.55;
            Panel.SetZIndex(b, 20);
        }

        void MoveDraggedTile(Point p)
        {
            if (DragBtn == null) return;
            int n = Tiles.Count;
            if (n <= 0) return;
            int cur = Tiles.IndexOf(DragBtn);
            double x = SlotU(p);
            int j = Math.Max(0, Math.Min(n - 1, (int)Math.Floor(x)));
            Button vecino = j != cur ? Tiles[j] : null;

            // distancia con signo al centro del hueco del vecino: positiva cuando el cursor ya lo dejo
            // atras en la direccion del arrastre
            double pasado = (j > cur ? 1.0 : -1.0) * (x - (j + 0.5)) * Lay.Pitch;

            // el cambio de sitio se pide al DESPEGARSE del icono del vecino, no al cruzar la raya de su
            // hueco: la franja que ocupa ese icono, centrada en su hueco, es la de agrupar. Mientras el
            // cursor esta encima, el vecino se apaga y soltar mete un icono dentro del otro; en cuanto
            // lo suelta por el lado del arrastre, los dos cambian de puesto como siempre.
            if (vecino != null)
            {
                if (Math.Abs(pasado) <= 0.5 * Cfg.IconSize)
                {
                    MarcarGrupo(vecino);
                    UpdateProximity(p);
                    return;
                }
                MarcarGrupo(null);
                // zona muerta: entre el borde del hueco y el borde del icono, por el lado del que viene
                if (pasado < 0.0) { UpdateProximity(p); return; }
            }
            else { MarcarGrupo(null); }

            if (vecino == null) { UpdateProximity(p); return; }
            Strip.Children.Remove(DragBtn);
            Strip.Children.Insert(j, DragBtn);
            Tiles.Clear();
            foreach (var c in Strip.Children) { var b = c as Button; if (b != null) Tiles.Add(b); }
            FitPill();
            UpdateProximity(p);
        }

        void EndTileDrag(Point p)
        {
            var b = DragBtn;
            var objetivo = _grupoBtn;
            DragBtn = null; DragMoved = false; _dragHasStart = false; _grupoBtn = null;
            if (b == null) return;
            b.Opacity = 1.0;
            if (objetivo != null) { objetivo.Opacity = 1.0; Panel.SetZIndex(objetivo, 0); }
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
            if (objetivo != null && !ReferenceEquals(objetivo, b))
            {
                var destino = objetivo.Tag as ItemSpec;
                if (destino != null)
                {
                    GroupInto((ItemSpec)b.Tag, destino);
                    ResetProximity();
                    return;
                }
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
            // soltar una carpeta fuera saca de la barra todo lo que hay dentro
            if (Groups.IsGroup(it)) { HideGroup(it); return; }
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
            // una carpeta no se fija ni se des fija: lo que cuenta son sus miembros
            if (Groups.IsGroup(it)) return;
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
                Settings.Aviso(Lang.F("No se pudo lanzar:\n{0}\n\n{1}", it.Name, ex.Message));
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
                string cabeza = _batchFail.Count == 1 ? Lang.T("No se pudo añadir:") : Lang.T("No se pudieron añadir:");
                Settings.Aviso(cabeza + "\n" + string.Join("\n", _batchFail.ToArray()));
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
                    if (s == null || string.IsNullOrEmpty(s.Target)) throw new Exception(Lang.T("El acceso directo no tiene destino."));
                    target = s.Target; argLine = s.Args; work = s.Work; hint = s.Target;
                    key = target.ToLowerInvariant() + "|" +
                          System.Text.RegularExpressions.Regex.Replace(argLine ?? string.Empty, @"\s+", " ").Trim().ToLowerInvariant();
                    name = !string.IsNullOrEmpty(nameHint) ? nameHint : Catalog.CleanName(Path.GetFileName(path));
                }
                else if (sid != null && sid.Success)
                {
                    // el .url no extrae icono ninguno: se guarda directamente el arte de la caché de Steam
                    link = path; target = url; hint = Catalog.SteamArt(sid.Groups[1].Value);
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
                else Settings.Aviso(Lang.F("No se pudo añadir:\n{0}\n\n{1}", path, ex.Message));
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
                Title = Lang.T("Elige el juego o aplicación"),
                Filter = Lang.T("Programas y accesos|*.exe;*.lnk;*.bat;*.cmd;*.url|Todos|*.*"),
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
                _dockMenu.Items.Add(MenuRow(Lang.T("Buscar..."), delegate { OpenSearch(); }));
                _dockMenu.Items.Add(MenuRow(Lang.T("Añadir app o juego..."), delegate { ShowAddDialog(); }));

                _verMenu = new MenuItem { Header = Lang.T("Qué se ve") };
                foreach (var s in new[]
                {
                    new[] { "fav", "Favoritos" }, new[] { "games", "Juegos detectados" },
                    new[] { "apps", "Aplicaciones" }, new[] { "all", "Todo" }
                })
                {
                    string id = s[0];
                    var mi = new MenuItem { Header = Lang.T(s[1]), Tag = id, IsCheckable = true };
                    mi.Click += (a, e) => SetSection(id);
                    _verMenu.Items.Add(mi);
                }
                _dockMenu.Items.Add(_verMenu);

                _temaMenu = new MenuItem { Header = Lang.T("Tema") };
                foreach (var kv in Themes.All)
                {
                    string id = kv.Key;
                    var mi = new MenuItem { Header = Lang.T(kv.Value.Label), Tag = id, IsCheckable = true };
                    mi.Click += (a, e) => SetTheme(id);
                    _temaMenu.Items.Add(mi);
                }
                _dockMenu.Items.Add(_temaMenu);

                // lo que se sacó arrastrando fuera (o con "quitar del dock") vuelve a entrar por aqui
                _ocultosMenu = new MenuItem { Header = Lang.T("Restaurar ocultos") };
                _dockMenu.Items.Add(_ocultosMenu);

                _dockMenu.Items.Add(MenuRow(Lang.T("Reescanear"), delegate { Rebuild(); }));
                _dockMenu.Items.Add(MenuRow(Lang.T("Ajustes"), delegate { Settings.Show(this); }));
                _dockMenu.Items.Add(new Separator());
                _dockMenu.Items.Add(MenuRow(Lang.T("Ocultar (Ctrl+Alt+D)"), delegate { HideDock(); }));
                _dockMenu.Items.Add(MenuRow(Lang.T("Salir"), delegate { Exiting = true; Win.Close(); }));

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
                MenuGuard.Watch(_dockMenu);
                return _dockMenu;
            }
        }

        // el menu de la pildora se construye una vez y se guarda: al cambiar de idioma hay que tirar el
        // cache, si no la bandeja se pone en ingles y el clic derecho sigue en castellano
        public void InvalidateMenus()
        {
            _dockMenu = null;
            _verMenu = null;
            _temaMenu = null;
            _ocultosMenu = null;
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
                _ocultosMenu.Items.Add(new MenuItem { Header = Lang.T("(nada oculto)"), IsEnabled = false });
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
            // el menu y la ficha no van juntos: los dos salen encima del mismo icono y se taparian
            OcultarFicha();
            if (Groups.IsGroup(it)) { GrupoMenu(it, owner); return; }
            _itemMenu = new ContextMenu();
            var acciones = new List<MenuItem>
            {
                MenuRow(it.Fav ? Lang.T("Quitar de favoritos") : Lang.T("Fijar en favoritos"), delegate { ToggleFav(it); }),
                MenuRow(Lang.T("Lanzar ahora"), delegate { StartEntry(it, "menu"); }),
                MenuRow(Lang.T("Abrir carpeta del juego"), delegate { OpenFolderOf(it); }),
                MenuRow(Lang.T("Renombrar"), delegate { Rename(it); }),
                MenuRow(Lang.T("Cambiar icono..."), delegate { ChangeIcon(it); }),
                MenuRow(Lang.T("Iconos de la comunidad"), delegate { IconosComunidad(it); })
            };
            // la opcion de deshacer solo tiene sentido mientras el fijado guarde un icono propio
            if (it.Pin != null && Icons.IsCustom(it.Pin.Icon))
                acciones.Add(MenuRow(Lang.T("Quitar icono personalizado"), delegate { RestoreIcon(it); }));
            var destructiva = MenuRow(it.Origin == "custom" ? Lang.T("Quitar del dock") : Lang.T("Ocultar del dock"),
                                      delegate { HideFromDock(it); });
            foreach (var a in acciones) _itemMenu.Items.Add(a);
            _itemMenu.Items.Add(new Separator());   // la accion destructiva, aparte
            _itemMenu.Items.Add(destructiva);
            _itemMenu.PlacementTarget = owner;
            MenuGuard.Watch(_itemMenu);
            _itemMenu.IsOpen = true;
        }

        // el menu de una carpeta: lo de dentro no se ve desde la barra, asi que aqui no hay lanzar ni
        // icono propio; se abre, se le cambia el titulo, se desarma o se saca del dock entera
        void GrupoMenu(ItemSpec it, Button owner)
        {
            _itemMenu = new ContextMenu();
            _itemMenu.Items.Add(MenuRow(Lang.T("Abrir el grupo"), delegate { OpenGroup(it, owner); }));
            _itemMenu.Items.Add(MenuRow(Lang.T("Renombrar"), delegate { RenameGroup(it); }));
            _itemMenu.Items.Add(MenuRow(Lang.T("Desagrupar"), delegate { UnGroup(GroupOfSpec(it)); }));
            _itemMenu.Items.Add(new Separator());
            _itemMenu.Items.Add(MenuRow(Lang.T("Ocultar del dock"), delegate { HideGroup(it); }));
            _itemMenu.PlacementTarget = owner;
            MenuGuard.Watch(_itemMenu);
            _itemMenu.IsOpen = true;
        }

        void OpenFolderOf(ItemSpec it)
        {
            string t = it.Target;
            if (!string.IsNullOrEmpty(t) && !System.Text.RegularExpressions.Regex.IsMatch(t, @"^\w+://") && File.Exists(t))
                Launcher.SelectInExplorer(t);
            else
                Settings.Aviso(Lang.T("Ese juego se lanza por el cliente (Steam/Epic) y no tiene carpeta directa."));
        }

        public void Rename(ItemSpec it)
        {
            string n = Settings.PromptNombre(this, it.Name);
            if (string.IsNullOrEmpty(n) || n == it.Name) return;
            SetPinOverride(it, p => p.Name = n);
            it.Name = n;
            Cfg.Save();
            Refresh(false);
        }

        // ------------------------------------------------------------------ icono propio
        void ChangeIcon(ItemSpec it)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = Lang.T("Elige el icono del acceso"),
                Filter = Lang.T("Imagenes e iconos|*.png;*.jpg;*.jpeg;*.ico|Programas|*.exe;*.dll;*.lnk|Todos|*.*"),
                CheckFileExists = true
            };
            // el dialogo de elegir archivo es del sistema y no es topmost: el velo lo taparia, asi que
            // Veil lo remonta el solo en cuanto existe
            bool? elegido = null;
            Veil.Around(delegate { elegido = dlg.ShowDialog(Win); });
            if (elegido != true) return;
            string dest = Icons.InstallCustom(it.Key, dlg.FileName);
            if (dest == null)
            {
                Settings.Aviso(Lang.F("Ese archivo no tiene un icono que se pueda extraer:\n{0}", dlg.FileName));
                return;
            }
            SetPinOverride(it, p => p.Icon = dest);
            OlvidarPincel(dest);
            it.Icon = dest;
            Cfg.Save();
            Refresh(false);
            Log.Write("icono propio: " + it.Name + " <- " + dest);
        }

        void RestoreIcon(ItemSpec it)
        {
            string viejo = it.Icon;
            SetPinOverride(it, p => p.Icon = null);
            Icons.ForgetCustom(it.Key);
            OlvidarPincel(viejo);
            it.Icon = it.AutoIcon;
            it.IconHint = it.AutoIconHint;
            Cfg.Save();
            Refresh(false);
        }

        // ------------------------------------------------------------------ iconos de la comunidad (SteamGridDB)
        // mientras una busqueda esta en vuelo no se puede lanzar otra: dos selectores abiertos sobre el
        // mismo tile escribirian en el mismo png
        bool _sgdOcupada;

        // El boton 100 % automatico. La barra ya sabe como se llama el juego -y si salio de Steam, su
        // app id-, asi que el usuario no escribe nada: se pulsa, salen las miniaturas, se elige una y el
        // tile cambia. Nada de cuadros de guardado: el png va a la carpeta de la app, que ya existe
        // desde el primer arranque (%APPDATA%\GamesDock\icons\custom). Lo unico que se pide una vez es
        // la clave, y se pone en Ajustes.
        async void IconosComunidad(ItemSpec it)
        {
            if (it == null || _sgdOcupada) return;
            _sgdOcupada = true;
            try
            {
                string clave = ClaveSgd();
                if (string.IsNullOrEmpty(clave)) { AvisoSinClave(); return; }

                var lista = await IconosDeSgdAsync(it, clave);
                if (!lista.EsOk) { AvisoSgd(lista.Estado, lista.Error); return; }

                string url = IconPicker.Seleccionar(this, it.Name, lista.Datos);
                if (string.IsNullOrEmpty(url)) return;   // cerrado sin elegir: el icono de siempre sigue igual

                string destino = Icons.RutaPropia(it.Key, ".png");
                var d = await SteamGrid.DescargarIconoAsync(url, destino, CancellationToken.None);
                if (!d.EsOk) { AvisoSgd(d.Estado, d.Error); return; }

                Icons.OlvidarOtrasExtensiones(destino);
                OlvidarPincel(destino);
                SetPinOverride(it, p => p.Icon = destino);
                it.Icon = destino;
                Cfg.Save();
                Refresh(false);
                Log.Write("iconos comunidad: " + it.Name + " <- " + url);
            }
            catch (Exception ex)
            {
                Log.Write("iconos comunidad: " + ex.Message);
                Settings.Aviso(Lang.T("No se ha podido cambiar el icono."));
            }
            finally { _sgdOcupada = false; }
        }

        // Los dos caminos posibles: si el fijado tiene app id de Steam se pide directo (coincidencia
        // exacta, sin adivinar por el nombre) y si no, o si SteamGridDB no conoce ese juego, se busca el
        // nombre del tile en el autocomplete.
        async Task<SgdRespuesta<List<SgdIcono>>> IconosDeSgdAsync(ItemSpec it, string clave)
        {
            var ct = CancellationToken.None;
            int steam = AppIdSteam(it);
            if (steam > 0)
            {
                var r = await SteamGrid.IconosDeSteamAsync(steam, clave, ct);
                // "no esta ese juego" no es un fallo: es el motivo de continuar con la busqueda por nombre
                if (r.EsOk || r.Estado != SgdEstado.NoEncontrado) return r;
            }
            var b = await SteamGrid.BuscarJuegoIdAsync(it.Name, clave, ct);
            if (!b.EsOk) return SgdRespuesta<List<SgdIcono>>.Mal(b.Estado, b.Error);
            return await SteamGrid.IconosDeJuegoAsync(b.Datos, clave, ct);
        }

        // el app id solo vale si es numero: es lo que SteamGridDB entiende por id de Steam
        static int AppIdSteam(ItemSpec it)
        {
            if (it == null || string.IsNullOrEmpty(it.AppId)) return 0;
            int n;
            return int.TryParse(it.AppId, out n) && n > 0 ? n : 0;
        }

        // La clave no va escrita dentro del programa: la pega el usuario en Ajustes y se queda en su
        // config.json. La variable de entorno la usa unicamente el harness, que levanta un servidor local
        // con el mismo formato de respuestas.
        public string ClaveSgd()
        {
            string k = (Environment.GetEnvironmentVariable("STEAMGRIDDB_KEY") ?? string.Empty).Trim();
            if (k.Length == 0) k = (Cfg.SteamGridKey ?? string.Empty).Trim();
            return k;
        }

        // Sin clave la API responde 401 seguro, asi que se avisa antes de llamar. Y el aviso no puede ser
        // un callejon sin salida: su boton lleva al panel donde se pega la clave.
        void AvisoSinClave()
        {
            Settings.Aviso(Lang.T("Sin clave no se pueden pedir iconos a SteamGridDB.\nCrea una cuenta gratis, saca la clave en Perfil > Preferencias > API y pégala en Ajustes > Iconos de la comunidad."),
                           Lang.T("Abrir ajustes"), delegate { Settings.Show(this); });
        }

        // cada motivo de fallo se explica distinto: sin clave hay que pedirla, con clave caducada hay
        // que dirigir a la pagina del desarrollador y sin red no se puede hacer nada
        void AvisoSgd(SgdEstado e, string detalle)
        {
            string msg;
            switch (e)
            {
                case SgdEstado.SinClave: msg = Lang.T("Falta la clave de SteamGridDB."); break;
                case SgdEstado.ClaveInvalida: msg = Lang.T("SteamGridDB no acepta esa clave.\nCompruébala o crea una nueva en Perfil > Preferencias > API y pégala en Ajustes > Iconos de la comunidad."); break;
                case SgdEstado.NoEncontrado: msg = Lang.T("SteamGridDB no tiene iconos para ese juego."); break;
                case SgdEstado.SinRed: msg = Lang.T("Sin conexión con SteamGridDB."); break;
                case SgdEstado.ImagenInvalida: msg = Lang.T("Lo descargado no es una imagen válida."); break;
                default: msg = Lang.F("SteamGridDB ha respondido mal: {0}", detalle); break;
            }
            Log.Write("SteamGridDB " + e + ": " + (detalle ?? string.Empty));
            Settings.Aviso(msg);
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
            if (Lang.IsSearch(SearchBox.Text))
            {
                SearchBox.Foreground = Themes.Br(_theme.C["fieldFg"]);
                SearchBox.Text = Lang.Search;
            }
            Refresh(false);
        }

        // el agua pintada es la del idioma con el que se escribio, asi que al cambiar de idioma en
        // caliente hay que reescribirla: si no, ApplyFilter la leeria como busqueda y vaciaria la barra
        public void SyncSearchWatermark()
        {
            if (Lang.IsSearch(SearchBox.Text)) SearchBox.Text = Lang.Search;
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
            // la carpeta abierta vive pegada a la barra: si la barra se va, se va con ella
            GroupPanel.Cerrar();
            OcultarFicha();
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
