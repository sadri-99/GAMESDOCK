using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Windows.Threading;
using Shape = System.Windows.Shapes.Path;

namespace GamesDock
{
    public partial class Dock
    {
        public readonly Window Win;
        public readonly Grid RootGrid;
        readonly Canvas PillLayer;
        readonly Shape Glass, Edge;
        readonly StackPanel Bar, Strip;
        readonly ScrollViewer StripScroll;
        readonly Border SearchPanel;
        readonly TextBox SearchBox;

        public readonly Config Cfg;
        public readonly Layout Lay;

        public readonly List<Button> Tiles = new List<Button>();
        readonly List<Spring> MagSpr = new List<Spring>();
        readonly Spring HoverSpr, PtrSpr, SlideSpr, FadeSpr;

        double _hoverT, _ptrLive, _magLive, _rowShift;
        double _ptrU = -1e9;
        bool _ptrInside;
        int _magTop = -1;
        string _shapeSig = string.Empty;
        string _barSig = string.Empty;
        ThemeDef _theme;
        Button _pressBtn;
        readonly Dictionary<string, ImageBrush> _brushes = new Dictionary<string, ImageBrush>();

        public bool DesktopLevel { get; private set; }
        public Catalog Cat;
        public IList<ItemSpec> Items = new List<ItemSpec>();
        public Tray TrayIcon;
        DispatcherTimer _searchTimer;

        public Dock(Window w, Config cfg)
        {
            Win = w;
            Cfg = cfg;
            Lay = new Layout(cfg);

            RootGrid = (Grid)Win.FindName("Root");
            PillLayer = (Canvas)Win.FindName("PillLayer");
            Glass = (Shape)Win.FindName("Glass");
            Edge = (Shape)Win.FindName("Edge");
            Bar = (StackPanel)Win.FindName("Bar");
            Strip = (StackPanel)Win.FindName("Strip");
            StripScroll = (ScrollViewer)Win.FindName("StripScroll");
            SearchPanel = (Border)Win.FindName("SearchPanel");
            SearchBox = (TextBox)Win.FindName("SearchBox");
            // el agua del .xaml esta escrita en castellano: se pinta en el idioma activo antes del
            // primer pintado, si no el cuadro arrancaria con el texto equivocado en ingles
            SearchBox.Text = Lang.Search;

            // la banda transparente que rodea la pildora va en el margen del Grid: el Padding de la
            // ventana no lo aplica su plantilla, y sin esa banda el cristal se estira hasta el borde y
            // no queda sitio por donde desborde el icono agrandado
            RootGrid.Margin = new Thickness(Layout.Margin);

            // muelles fijos del dock: la pildora, la posicion del cursor (suavizada) y la entrada/salida
            // k manda lo TARDA en llegar (omega = sqrt(k), el 2 % se asienta en ~5,8/omega s) y zeta
            // cuanto rebota. El del cursor va a k=3200: con 900 la fila llegaba al agrandado ~190 ms
            // despues, un retardo perceptible al pasar el ratón.
            HoverSpr = Spring.Make("pill", 340.0, 0.92, null);
            PtrSpr = Spring.Make("ptr", 3200.0, 1.0, null);
            SlideSpr = Spring.Make("slide", 260.0, 1.0, null);
            FadeSpr = Spring.Make("fade", 320.0, 1.0, null);

            HoverSpr.Apply = s => { _hoverT = s.X; Anim.ShapeDirty = true; };
            PtrSpr.Apply = s => { _ptrLive = s.X; SetTileTargets(); Anim.ShapeDirty = true; };
            SlideSpr.Apply = s =>
            {
                var tr = RootGrid.RenderTransform as TranslateTransform;
                if (tr != null) { if ((string)s.Owner == "x") tr.X = s.X; else tr.Y = s.X; }
                Anim.ShapeDirty = true;
            };
            FadeSpr.Apply = s => { RootGrid.Opacity = s.X; };

            Anim.Add(HoverSpr);
            Anim.Add(PtrSpr);
            Anim.Add(SlideSpr);
            Anim.Add(FadeSpr);
            Anim.RepaintShape = UpdatePillShape;

            ApplyTheme(Cfg.Theme);
        }

        // ------------------------------------------------------------------ tema
        public void ApplyTheme(string id)
        {
            var t = Themes.Get(id);
            _theme = t;
            Cfg.Theme = id;

            Glass.Fill = Themes.Grad(t.C["glass"], t.C["glassBot"]);
            Edge.Stroke = Themes.Br(t.C["edge"]);
            Edge.Fill = Themes.Grad(t.C["sheenTop"], t.C["sheenMid"], t.C["sheenBot"]);

            SearchPanel.Background = Themes.Br(t.C["fieldBg"]);
            SearchPanel.BorderBrush = Themes.Br(t.C["edge"]);
            SearchBox.Foreground = Themes.Br(t.C["fieldFg"]);
            SearchBox.CaretBrush = Themes.Br(t.C["letter"]);

            // las plantillas leen estas claves con DynamicResource: cambian el aspecto sin reconstruir
            Win.Resources["TileHover"] = Themes.Glow(t.C["hover"]);
            Win.Resources["TilePress"] = Themes.Glow(t.C["press"]);

            // menus y ajustes viven en sus propias ventanas visuales: sus brochas tienen que estar en
            // el alcance de la aplicacion para que DynamicResource las encuentre desde cualquier parte
            var ar = Application.Current.Resources;
            ar["MenuBg"] = Themes.Br(t.C["tipBg"]);
            ar["MenuEdge"] = Themes.Br(t.C["tipEdge"]);
            ar["MenuFg"] = Themes.Br(t.C["tipFg"]);
            ar["MenuHiFg"] = Themes.Br(t.C["letter"]);
            ar["MenuHi"] = Themes.Br(t.C["hover"]);
            ar["MenuSep"] = Themes.BrA(t.C["edge"], 0.55);
            ar["MenuAccent"] = Themes.Br(t.C["accent"]);
            ar["CtlBg"] = Themes.BrA(t.C["fieldBg"], 0.55);
            ar["CtlHi"] = Themes.Br(t.C["hover"]);
            ar["CtlEdge"] = Themes.BrA(t.C["edge"], 0.85);
            ar["CtlSel"] = Themes.BrA(t.C["accent"], 0.45);
            ar["ChipOn"] = Themes.BrA(t.C["accent"], 0.30);
            ar["SwitchOff"] = Themes.BrA(t.C["fieldFg"], 0.40);
            ar["PanelBg"] = Themes.Br(t.C["tipBg"]);
            ar["PanelEdge"] = Themes.BrA(t.C["edge"], 0.90);
            ar["PanelFg"] = Themes.Br(t.C["letter"]);
            ar["PanelSub"] = Themes.Br(t.C["fieldFg"]);

            if (Tiles.Count > 0) RebuildStrip(false);
            Log.Write("tema aplicado: " + id);
        }

        // ------------------------------------------------------------------ fila
        // lo que entra en la barra: la seccion elegida mas lo que se este buscando, sin los ocultados
        public void ApplyFilter()
        {
            if (Cat == null) return;
            string q = SearchBox.Text;
            if (Lang.IsSearch(q)) q = null;
            Items = Cat.Visible(Cfg.Section, q);
        }

        public void Refresh(bool pop)
        {
            ApplyFilter();
            RebuildStrip(pop);
        }

        public void RebuildStrip(bool pop)
        {
            Strip.Children.Clear();
            Tiles.Clear();
            Anim.RemoveAll(s => s.Kind == "tile");
            MagSpr.Clear();

            for (int i = 0; i < Items.Count; i++)
            {
                var t = NewTile(Items[i]);
                Strip.Children.Add(t);
                Tiles.Add(t);
                // muelle de escala propio por tile. Con pop entran escalonados y con un asomo de
                // rebote, que es como se ve una app cara al abrirse; sin pop nacen ya asentados.
                // k=900 en lugar de 300: el tile es lo ultimo que queda entre el cursor y lo que se
                // ve, y a omega 17 la fila llegaba al agrandado ~340 ms despues de pasar el ratón.
                var sp = Spring.Make("tile", 900.0, 0.85, t);
                var box = (Border)t.Content;
                var st = (ScaleTransform)t.RenderTransform;
                sp.Apply = s => { st.ScaleX = s.X; st.ScaleY = s.X; };
                if (pop) { sp.X = 0.62; sp.Target = 1.0; sp.Wait = (3.0 + i * 1.6) / 60.0; }
                else { sp.X = 1.0; sp.Target = 1.0; st.ScaleX = 1.0; st.ScaleY = 1.0; }
                MagSpr.Add(sp);
                Anim.Add(sp);
            }

            if (pop && Items.Count > 0) Anim.Start();

            if (Items.Count == 0)
            {
                Strip.Children.Add(new TextBlock
                {
                    Text = Lang.T("Clic derecho en el hueco para añadir o configurar."),
                    Foreground = Themes.Br(_theme.C["fieldFg"]),
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 0, 0)
                });
            }

            _magTop = -1;
            FitPill();
            SetBarMargins();
            RefreshPill();
        }

        Button NewTile(ItemSpec it)
        {
            int sz = Cfg.IconSize;
            var btn = new Button
            {
                Style = (Style)Win.FindResource("Tile"),
                Tag = it,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new ScaleTransform()
            };
            btn.Padding = Lay.Vert
                ? new Thickness(Lay.RingCross, Lay.RingMain, Lay.RingCross, Lay.RingMain)
                : new Thickness(Lay.RingMain, Lay.RingCross, Lay.RingMain, Lay.RingCross);

            var tt = new ToolTip
            {
                Style = (Style)Win.FindResource("Tip"),
                Content = TooltipFor(it),
                Background = Themes.Br(_theme.C["tipBg"]),
                Foreground = Themes.Br(_theme.C["tipFg"]),
                BorderBrush = Themes.Br(_theme.C["tipEdge"])
            };
            btn.ToolTip = tt;

            // sin cuadro: el mosaico va transparente y solo se pinta el circulo de la letra cuando no hay icono
            var bd = new Border
            {
                Width = sz,
                Height = sz,
                CornerRadius = new CornerRadius(Lay.BadgeRadius(sz)),
                Background = Themes.Br(_theme.C["badge"]),
                ClipToBounds = true
            };
            bd.Child = Letra(it.Name, sz);
            btn.Content = bd;

            // el icono no se extrae aqui: un .exe sin cache cuesta decenas de ms y la fila entera se
            // congelaria. Se encola y baja de cuatro en cuatro cuando el dispatcher esta libre.
            EnqueueIcon(btn, it);

            btn.Click += (s, e) =>
            {
                var g = (ItemSpec)((Button)s).Tag;
                // una carpeta no se lanza: se abre
                if (Groups.IsGroup(g)) OpenGroup(g, (Button)s); else StartEntry(g, "click");
            };
            btn.MouseRightButtonUp += (s, e) => { ShowItemMenu((ItemSpec)((Button)s).Tag, (Button)s); e.Handled = true; };
            // WPF no tiene evento de boton central: se caza en PreviewMouseDown, que es lo que deja el
            // favorito de en medio funcionando
            btn.PreviewMouseDown += (s, e) =>
            {
                if (e.ChangedButton == MouseButton.Middle)
                {
                    ToggleFav((ItemSpec)((Button)s).Tag);
                    e.Handled = true;
                }
            };
            // reordenar arrastrando con un arrastre propio (no de OLE): solo asi se puede mover el hueco
            // en caliente mientras se pasa el raton por los vecinos y soltar fuera para sacarlo del dock
            btn.PreviewMouseLeftButtonDown += TileDown;
            btn.PreviewMouseMove += TileMove;
            btn.PreviewMouseLeftButtonUp += TileUp;
            btn.LostMouseCapture += TileLostCapture;
            return btn;
        }

        // la letra de respaldo: lo que se ve cuando un icono no se puede extraer
        TextBlock Letra(string nombre, double lado)
        {
            return new TextBlock
            {
                Text = string.IsNullOrEmpty(nombre) ? "?" : nombre.Substring(0, 1).ToUpperInvariant(),
                FontSize = lado * 0.44,
                Foreground = Themes.Br(_theme.C["letter"]),
                FontFamily = new FontFamily("Segoe UI"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        string TooltipFor(ItemSpec it)
        {
            if (Groups.IsGroup(it))
            {
                int n = it.Members != null ? it.Members.Count : 0;
                return it.Name + "\n" + Lang.F("Grupo de {0} iconos", n) + "\n" + Lang.T("Clic para abrirlo");
            }
            UsageDto u;
            Cfg.Usage.TryGetValue(it.Key ?? string.Empty, out u);
            string origen;
            switch (it.Origin)
            {
                case "steam": origen = "Steam"; break;
                case "epic": origen = "Epic Games"; break;
                case "gog": origen = "GOG"; break;
                case "desktop": origen = Lang.T("Escritorio"); break;
                case "start": origen = Lang.T("Menú Inicio"); break;
                default: origen = Lang.T("Añadido a mano"); break;
            }
            string uso = u != null ? Lang.F("usado {0} veces", u.N) : Lang.T("sin usar todavía");
            string ruta = !string.IsNullOrEmpty(it.Target) ? it.Target : (it.Link ?? string.Empty);
            return it.Name + "\n" + origen + " · " + uso + "\n" + ruta;
        }

        // el icono propio de un fijado se sobrescribe SIEMPRE en la misma ruta (Icons.RutaPropia parte
        // de la clave del pin), asi que cambiarlo dos veces no mueve la clave de esta caché: sin este
        // quite el tile seguiria pintando el primer png elegido aunque en disco esté el segundo.
        void OlvidarPincel(string path)
        {
            if (!string.IsNullOrEmpty(path)) _brushes.Remove(path);
        }

        ImageBrush GetBrush(string path)
        {
            ImageBrush cached;
            if (_brushes.TryGetValue(path, out cached)) return cached;
            ImageBrush b = null;
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                // WPF cachea el decoded por URI y no se entera de que el archivo cambio por debajo: un
                // icono de la comunidad re-elegido se sobrescribe en LA MISMA ruta, y sin este flag el
                // segundo y tercer png elegidos vuelven a pintar el primero que se abrio en la sesion.
                bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                if (new FileInfo(path).Length > 8000) bmp.DecodePixelWidth = 256;
                bmp.UriSource = new Uri(path, UriKind.Absolute);
                bmp.EndInit();
                b = new ImageBrush(bmp) { Stretch = Stretch.UniformToFill };
                // una capsula de Steam es 2:3 con el titulo arriba: el recorte cuadrado centrado le
                // comia la cabecera y dejaba media ilustracion
                if (bmp.PixelHeight > bmp.PixelWidth)
                {
                    double w = bmp.PixelWidth, h = bmp.PixelHeight;
                    b.ViewboxUnits = BrushMappingMode.Absolute;
                    b.Viewbox = new Rect(0.0, (h - w) * 0.34, w, w);
                }
                b.Freeze();
            }
            catch (Exception ex)
            {
                Log.Write("decode fallo " + path + ": " + ex.Message);
                b = null;
            }
            _brushes[path] = b;
            return b;
        }

        bool SetIconNow(Button btn, string path)
        {
            var bd = (Border)btn.Content;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
            var brush = GetBrush(path);
            if (brush == null) return false;
            // la imagen pisa el circulo de respaldo y se recorta con la forma de Ajustes: en "defecto"
            // radio 0, o sea el cuadrado entero de siempre
            bd.CornerRadius = new CornerRadius(Lay.TileRadius(bd.Width));
            bd.Background = brush;
            bd.Child = null;
            // Fant: el filtro bueno que WPF puede hacer POR HARDWARE. HighQualityCubic esta documentado
            // como solo-software y manda el tile a la via lenta: con 11 iconos reescalando cada cuadro
            // eso es exactamente el corte que se nota al pasar el ratón
            RenderOptions.SetBitmapScalingMode(bd, BitmapScalingMode.Fant);
            return true;
        }

        // Mosaico de la carpeta: las iconos que hay DENTRO en este momento, en 1/2/4 celdas. Es lo que
        // hace reconocible un grupo sin abrirlo, y por eso se rehace cada vez que cambia la fila.
        UniformGrid BuildMosaic(ItemSpec it)
        {
            var ms = it.Members;
            if (ms == null || ms.Count == 0) return null;
            int n = Math.Min(4, ms.Count);
            int cols = n <= 1 ? 1 : 2;
            int filas = n <= 2 ? 1 : 2;
            var g = new UniformGrid { Columns = cols, Rows = filas };
            double lado = Cfg.IconSize / Math.Max(1, g.Columns);
            double r = Lay.MosaicRadius(Cfg.IconSize, 1.2);
            for (int i = 0; i < n; i++)
            {
                var m = ms[i];
                int col = i % cols, fila = i / cols;
                var c = new Border
                {
                    Margin = new Thickness(1.2),
                    ClipToBounds = true,
                    // solo la esquina de fuera del tile: las juntas entre celdas tienen que seguir rectas
                    CornerRadius = new CornerRadius(
                        fila == 0 && col == 0 ? r : 0.0,
                        fila == 0 && col == cols - 1 ? r : 0.0,
                        fila == filas - 1 && col == cols - 1 ? r : 0.0,
                        fila == filas - 1 && col == 0 ? r : 0.0)
                };
                string png = Icons.Resolve(m);
                if (png != null) m.Icon = png;
                var br = png != null ? GetBrush(png) : null;
                if (br != null)
                {
                    c.Background = br;
                    RenderOptions.SetBitmapScalingMode(c, BitmapScalingMode.Fant);
                }
                else c.Child = Letra(m.Name, lado);
                g.Children.Add(c);
            }
            return g;
        }

        bool SetMosaicNow(Button btn, ItemSpec it)
        {
            var bd = (Border)btn.Content;
            var g = BuildMosaic(it);
            if (g == null) return false;
            // en "defecto" conserva su esquina suavizada de siempre; con forma, la carpeta se recorta
            // igual que las apps para que la fila se lea entera en el mismo molde
            bd.CornerRadius = new CornerRadius(Lay.FolderRadius(bd.Width));
            bd.Background = Themes.Br(_theme.C["badge"]);
            bd.Child = g;
            RenderOptions.SetBitmapScalingMode(bd, BitmapScalingMode.Fant);
            return true;
        }

        // ------------------------------------------------------------------ magnes
        public void SetTileTargets()
        {
            if (!Cfg.Magnify) return;
            // fuera de la pildora el magnes no existe: sin esto, el ultimo paso del muelle del cursor
            // al salir re-magnificaba la fila y se quedaba agrandada para siempre
            if (!_ptrInside) return;
            if (Tiles.Count != MagSpr.Count) return;

            double pitch = Lay.Pitch;
            double off = Lay.Vert ? StripScroll.VerticalOffset : StripScroll.HorizontalOffset;
            double inset = Lay.RowInset;
            double r = Lay.MagRadius;
            double best = 1.0;
            int bestIx = -1;

            for (int i = 0; i < Tiles.Count; i++)
            {
                double centro = inset + (i + 0.5) * pitch - off;
                double s = Lay.MagScale(_ptrLive - centro, r);
                if (Tiles[i] == DragBtn) s = 0.86;
                else if (Tiles[i] == _pressBtn) s *= 0.9;
                Anim.Move(MagSpr[i], s);
                if (s > best) { best = s; bestIx = i; }
            }

            // el del centro va por delante: si no, el vecino agrandado le pisa un trozo
            if (bestIx != _magTop)
            {
                if (_magTop >= 0 && _magTop < Tiles.Count) Panel.SetZIndex(Tiles[_magTop], 0);
                if (bestIx >= 0) Panel.SetZIndex(Tiles[bestIx], 10);
                _magTop = bestIx;
            }
        }

        public void UpdateProximity(Point p)
        {
            bool veniaDeFuera = _ptrU < -1e8;
            _ptrInside = true;
            _ptrU = Lay.Vert ? p.Y : p.X;
            if (veniaDeFuera)
            {
                // primera pasada despues de entrar: el bulto nace bajo el cursor, no barre desde la
                // posicion que tenia cuando salio la ultima vez
                _ptrLive = _ptrU;
                PtrSpr.X = _ptrLive;
                PtrSpr.Target = _ptrLive;
                PtrSpr.V = 0.0;
                // y nace ya aplicado: el muelle del cursor esta en su sitio, asi que no va a moverse y
                // sin esta pasada los iconos se quedarian pequenos hasta que el raton volviera a moverse
                SetTileTargets();
                Anim.Start();
            }
            Anim.Move(PtrSpr, _ptrU);
        }

        public void ResetProximity()
        {
            _magTop = -1;
            _ptrU = -1e9;
            _ptrInside = false;
            // el cursor se queda donde estaba y solo sueltan los muelles: la pildora se deshincha en su
            // sitio en vez de desplazar el bulto al extremo de la fila
            for (int i = 0; i < MagSpr.Count; i++) Anim.Move(MagSpr[i], 1.0);
            for (int i = 0; i < Tiles.Count; i++) Panel.SetZIndex(Tiles[i], 0);
        }

        public void SetPillHover(bool on) { Anim.Move(HoverSpr, on ? 1.0 : 0.0); }

        // el tile arrastrado y si el arrastre ya despego los modifica la capa de interaccion
        public Button DragBtn;
        public bool DragMoved;

        // ------------------------------------------------------------------ contorno
        public void UpdatePillShape()
        {
            double gw = RootGrid.ActualWidth, gh = RootGrid.ActualHeight;
            if (gw < 2 || gh < 2) return;

            bool vert = Lay.Vert;
            double baseH = Lay.HalfIdle;
            double fracc = Cfg.Magnify ? 0.55 : 0.0;

            // cuanto esta agrandado de verdad el tile mas grande, sacado de los muelles y no del cursor:
            // es lo que ata el bulto del cristal al tamano real de los iconos
            double mx = 1.0, eL = 1.0, eR = 1.0;
            for (int i = 0; i < MagSpr.Count; i++)
            {
                double x = MagSpr[i].X;
                if (x > mx) mx = x;
                if (i == 0) eL = x;
                else if (i == MagSpr.Count - 1) eR = x;
            }
            _magLive = Math.Max(0.0, Math.Min(1.0, (mx - 1.0) / (Layout.MagPeak - 1.0)));

            string sig = string.Format("{0}|{1}|{2}x{3}|{4}|{5}|{6}|{7}|{8}",
                Cfg.Position, Cfg.Magnify,
                Math.Round(gw), Math.Round(gh),
                Math.Round(_hoverT, 3), Math.Round(_ptrLive, 1),
                Math.Round(_magLive, 3), Math.Round(eL, 3), Math.Round(eR, 3));
            if (sig == _shapeSig) return;
            _shapeSig = sig;

            // manda lo que este MAS abierto de los dos: el hover o el agrandado real de los tiles. Si
            // solo mandara el hover el cristal se adelanta y si se quedara atras son los iconos los que
            // muerden el borde de arriba
            double grow = Math.Max(_hoverT, _magLive);
            double delta = (Lay.HalfMax - baseH) * grow;
            double uni = delta * (1.0 - fracc);
            // el bulto local sigue el agrandado animado de los iconos: si no, la pildora se deshincharia
            // de golpe al salir el raton mientras los tiles todavia estan grandes
            double amp = delta * fracc * _magLive;

            // la pildora se corre hacia dentro lo que gana por fuera: el borde que da al borde de la
            // pantalla no se hunde debajo de la barra de tareas. Solo cuenta el grosor uniforme: el
            // bulto local NO mueve la fila
            _rowShift = uni;
            double cc = (vert ? gw : gh) / 2.0 - Lay.Outward * _rowShift;
            double len = vert ? gh : gw;

            // el icono de un extremo, al agrandarse, se sale por la punta de la tapa: ese lado estira
            // lo que el tile suyo entra, y solo hasta donde aguanta la banda transparente
            double estir = 0.5 * Lay.Pitch + 10.0;
            double tope = Layout.Margin - 12.0;
            double overL = Math.Min(tope, Math.Max(0.0, (eL - 1.0) * estir));
            double overR = Math.Min(tope, Math.Max(0.0, (eR - 1.0) * estir));

            SetBarMargins();
            SetRowShift(_rowShift);

            var geo = PillGeometry.Build(len, cc, baseH, uni, amp, _ptrLive, Lay.Pitch,
                                         Layout.MagPeak, vert, overL, overR);
            Glass.Data = geo;
            Edge.Data = geo;
        }

        // la fila se desplaza hacia dentro lo que la pildora gana por fuera: asi el borde que da al
        // borde de la pantalla no se mueve y el dock parece que crece hacia el centro del escritorio
        void SetBarMargins()
        {
            double inset = Lay.RowInset;
            double ovr = Math.Ceiling(Lay.MagOverrun);
            bool fits = RowFits();
            string sig = string.Format("{0}|{1}|{2}|{3}", Cfg.Position, inset, ovr, fits);
            if (sig == _barSig) return;
            _barSig = sig;
            // el margen solo abre el hueco ESTATICO para que el icono agrandado desborde el cristal;
            // lo que se mueve cuadro a cuadro va por SetRowShift
            double gap = -ovr;
            // por el eje de la fila hace falta el mismo truco: si no, el recorte del ScrollViewer cae
            // justo sobre el borde del icono del extremo y lo corta en seco cuando mas se agranda. La
            // ventana del viewport sale ovr hacia fuera y Strip vuelve a entrar el mismo ovr, asi que
            // el icono sigue empezando a inset del borde de la pildora pero puede desbordarse.
            // Solo cuando la fila cabe entera: si desborda y se puede desplazar, recortar en el borde
            // del cristal es lo correcto, abrirlo dejaria trozos de icono flotando fuera de la pildora.
            double ext = fits ? ovr : 0.0;
            Bar.Margin = Lay.Vert
                ? new Thickness(gap, inset - ext, gap, inset - ext)
                : new Thickness(inset - ext, gap, inset - ext, gap);
            Strip.Margin = Lay.Vert
                ? new Thickness(0, ext, 0, ext)
                : new Thickness(ext, 0, ext, 0);
        }

        // cuantos iconos caben en la pildora sin necesidad de desplazar la fila: el mismo limite que
        // usa FitPill para el eje principal
        bool RowFits()
        {
            var wa = SystemParameters.WorkArea;
            double m = Layout.Margin;
            double avail = (Lay.Vert ? wa.Height : wa.Width) - 2 * m - 30;
            return Tiles.Count * Lay.Pitch <= avail;
        }

        void SetRowShift(double s)
        {
            var tr = Bar.RenderTransform as TranslateTransform;
            if (tr == null)
            {
                tr = new TranslateTransform();
                Bar.RenderTransform = tr;
            }
            double d = 0.0 - Lay.Outward * s;
            if (Lay.Vert) { if (Math.Abs(tr.X - d) > 0.005) tr.X = d; }
            else { if (Math.Abs(tr.Y - d) > 0.005) tr.Y = d; }
        }

        // ------------------------------------------------------------------ medida y sitio
        // La ventana mide lo que mide la fila de iconos + margen para la sombra y el agrandado.
        public void FitPill()
        {
            var wa = SystemParameters.WorkArea;
            double m = Layout.Margin;
            bool vert = Lay.Vert;

            Win.UpdateLayout();
            var d = Strip.DesiredSize;
            double box = Lay.TileBox;

            // el largo del eje principal lo manda la cantidad de iconos, no el DesiredSize: al quitar
            // o mover un tile la medida vieja sigue ahi hasta el siguiente arrange
            double mainLen;
            if (Tiles.Count > 0) mainLen = Tiles.Count * Lay.Pitch;
            else if (vert) mainLen = d.Height;
            else mainLen = d.Width;

            double pillW, pillH;
            if (vert)
            {
                double maxH = wa.Height - 2 * m - 30;
                StripScroll.MaxHeight = Math.Max(40, maxH);
                StripScroll.MaxWidth = 6000;
                pillW = box + 2 * Lay.PadV;
                pillH = Math.Min(mainLen, maxH) + 2 * Lay.RowInset;
            }
            else
            {
                double maxW = wa.Width - 2 * m - 30;
                StripScroll.MaxWidth = Math.Max(80, maxW);
                StripScroll.MaxHeight = 6000;
                pillW = Math.Min(mainLen, maxW) + 2 * Lay.RowInset;
                pillH = box + 2 * Lay.PadV;
            }

            Win.Width = Math.Ceiling(pillW + 2 * m);
            Win.Height = Math.Ceiling(pillH + 2 * m);
            PlaceDock();
        }

        public void ApplyLayout()
        {
            bool vert = Lay.Vert;
            Bar.Orientation = vert ? Orientation.Vertical : Orientation.Horizontal;
            Strip.Orientation = vert ? Orientation.Vertical : Orientation.Horizontal;
            SearchBox.Width = vert ? Cfg.IconSize + 140 : 190;

            if (vert)
            {
                StripScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                StripScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            }
            else
            {
                StripScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;
                StripScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
            }

            FitPill();
            _rowShift = 0.0;
            SetBarMargins();
            // al cambiar de lado el transform vive en otro eje (X pasa a Y): sin reiniciarlo la fila se
            // queda desplazada con el valor de la posicion anterior
            SetRowShift(0.0);
            RefreshPill();
            SetWindowOpacity((double)Cfg.IdleOpacity / 100.0, 0);
        }

        // la pildora se dibuja con la medida real de la rejilla: hay que forzar el layout antes
        public void RefreshPill()
        {
            _shapeSig = string.Empty;
            Win.UpdateLayout();
            UpdatePillShape();
        }

        // acordarse del rectangulo que se ocupa ahora, en las mismas coordenadas de contenido que lee la
        // posicion "free": elegir Libre en Ajustes tiene que ser quedarse quieto, no saltar al ultimo punto
        // que guardo la config (el de otra tarde, a media pantalla o fuera de ella).
        public void AnotarPuestoActual()
        {
            double m = Layout.Margin;
            Cfg.Left = (int)Math.Round(Win.Left + m);
            Cfg.Top = (int)Math.Round(Win.Top + m);
        }

        public void PlaceDock()
        {
            var wa = SystemParameters.WorkArea;
            double m = Layout.Margin;
            double w = Win.Width, h = Win.Height;
            double l, t;
            switch (Cfg.Position)
            {
                case "top":
                    l = wa.X + (wa.Width - w) / 2;
                    t = wa.Y - m + 6;
                    break;
                case "left":
                    l = wa.X - m + 6;
                    t = wa.Y + (wa.Height - h) / 2;
                    break;
                case "right":
                    l = wa.Right - w + m - 6;
                    t = wa.Y + (wa.Height - h) / 2;
                    break;
                case "free":
                    if (Cfg.Left.HasValue && Cfg.Top.HasValue)
                    {
                        l = Cfg.Left.Value - m;
                        t = Cfg.Top.Value - m;
                    }
                    else
                    {
                        Cfg.Position = "bottom";
                        PlaceDock();
                        return;
                    }
                    break;
                default:
                    l = wa.X + (wa.Width - w) / 2;
                    t = wa.Bottom - h + m - 6;
                    break;
            }
            Win.Left = l;
            Win.Top = t;
        }

        // ------------------------------------------------------------------ nivel de escritorio
        // ventana sin activacion (los clics no la suben) + colocada al fondo del z-order, por encima
        // del fondo y por debajo de cualquier aplicacion
        public void SendToBottom()
        {
            var hwnd = new WindowInteropHelper(Win).Handle;
            if (hwnd == IntPtr.Zero) return;
            // SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE
            Native.SetWindowPos(hwnd, new IntPtr(1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
        }

        public void SetDesktopLevel(bool on)
        {
            try
            {
                var hwnd = new WindowInteropHelper(Win).EnsureHandle();
                if (hwnd == IntPtr.Zero) return;
                Veil.Pildora = hwnd;   // el velo tiene que saber a que ventana jamas sube
                DesktopLevel = on;
                int ex = Native.GetWindowLong(hwnd, -20);   // GWL_EXSTYLE
                if (on) Native.SetWindowLong(hwnd, -20, ex | unchecked((int)0x08000000));   // WS_EX_NOACTIVATE
                else Native.SetWindowLong(hwnd, -20, ex & ~unchecked((int)0x08000000));
                if (on) SendToBottom();
                Log.Write("modo escritorio " + (on ? "activado" : "desactivado") + " (hwnd=" + hwnd + ")");
            }
            catch (Exception ex)
            {
                Log.Write("fallo al aplicar el modo escritorio: " + ex.Message);
            }
        }

        public void AttachToDesktop() { SetDesktopLevel(Cfg.DesktopLevel); }

        public void SetWindowOpacity(double to, int ms)
        {
            if (ms <= 0) { Win.Opacity = to; return; }
            var a = new DoubleAnimation
            {
                To = to,
                Duration = new Duration(TimeSpan.FromMilliseconds(ms)),
                EasingFunction = new CubicEase()
            };
            Win.BeginAnimation(Window.OpacityProperty, a);
        }

        // ------------------------------------------------------------------ senales
        public void WireEvents()
        {
            Win.MouseEnter += delegate
            {
                SetWindowOpacity(1.0, 130);
                SetPillHover(true);
                // el bulto tiene que nacer bajo el cursor ya: si no, la pildora se hincha con los tiles
                // todavia chicos y los iconos quedan dentro de un cristal de sobra hasta que se mueva
                UpdateProximity(Mouse.GetPosition(RootGrid));
            };
            Win.MouseLeave += delegate
            {
                ResetProximity();
                SetWindowOpacity((double)Cfg.IdleOpacity / 100.0, 260);
                SetPillHover(false);
            };
            // el magnes y el bulto trabajan solo con el raton encima: posicion -> escalas y contorno
            RootGrid.MouseMove += (s, e) =>
            {
                if (DragMoved) return;
                UpdateProximity(e.GetPosition(RootGrid));
            };
            // arrastrar la barra entera solo si el gesto no cae sobre un control: si no, mover un icono
            // se llevaria el dock puesto
            Win.MouseLeftButtonDown += (s, e) =>
            {
                DependencyObject o = (DependencyObject)e.OriginalSource;
                while (o != null)
                {
                    if (o is Button || o is System.Windows.Controls.Primitives.ToggleButton || o is TextBox) return;
                    try { o = VisualTreeHelper.GetParent(o); } catch { break; }
                }
                try
                {
                    // la posición de Ajustes se apunta ANTES de arrastrar: SaveDockAfterDrag la reescribe
                    // con el imán, y la excepción de "libre" tiene que juzgarse por lo que había elegido
                    _posAntesDeArrastrar = Cfg.Position;
                    Win.DragMove();
                    SaveDockAfterDrag();
                }
                catch
                {
                }
            };

            // el buscador reescribe la fila: sin retardo cada tecla tiraria los tiles y el magnes, que
            // es justo el corte que se nota mientras se escribe
            _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(140) };
            _searchTimer.Tick += delegate { _searchTimer.Stop(); Refresh(false); };
            SearchBox.TextChanged += delegate
            {
                if (!Lang.IsSearch(SearchBox.Text))
                {
                    _searchTimer.Stop();
                    _searchTimer.Start();
                }
            };
            SearchBox.GotKeyboardFocus += delegate
            {
                if (Lang.IsSearch(SearchBox.Text))
                {
                    SearchBox.Foreground = Themes.Br(_theme.C["letter"]);
                    SearchBox.Text = string.Empty;
                }
            };
            SearchBox.LostKeyboardFocus += delegate
            {
                if (string.IsNullOrEmpty(SearchBox.Text))
                {
                    SearchBox.Foreground = Themes.Br(_theme.C["fieldFg"]);
                    SearchBox.Text = Lang.Search;
                }
            };
            SearchBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    if (Tiles.Count > 0) StartEntry((ItemSpec)Tiles[0].Tag, "enter");
                    CloseSearch();
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    CloseSearch();
                    e.Handled = true;
                }
            };

            // la rueda no baja la pagina: mueve la fila a lo largo de su propio eje
            StripScroll.PreviewMouseWheel += (s, e) =>
            {
                if (Lay.Vert)
                {
                    double off = StripScroll.VerticalOffset - e.Delta;
                    StripScroll.ScrollToVerticalOffset(Math.Max(0, off));
                }
                else
                {
                    double off = StripScroll.HorizontalOffset - e.Delta;
                    StripScroll.ScrollToHorizontalOffset(Math.Max(0, off));
                }
                e.Handled = true;
            };

            // soltar archivos encima los fija. La capa de interaccion sigue en el fondo del escritorio,
            // asi que el cristal solo tiene que encenderse para que se vea lo que entra
            RootGrid.AllowDrop = true;
            RootGrid.DragEnter += delegate { SetWindowOpacity(1.0, 130); };
            RootGrid.DragLeave += delegate { SetWindowOpacity((double)Cfg.IdleOpacity / 100.0, 260); };
            RootGrid.DragOver += (s, e) =>
            {
                e.Effects = DropPaths(e.Data).Count > 0 ? DragDropEffects.Copy : DragDropEffects.None;
                e.Handled = true;
            };
            RootGrid.Drop += (s, e) =>
            {
                e.Handled = true;
                AddPinsBatch(DropPaths(e.Data));
            };

            // clic derecho en el hueco: el unico sitio sin icono es el que abre el menu del dock
            RootGrid.MouseRightButtonUp += (s, e) =>
            {
                DependencyObject o = (DependencyObject)e.OriginalSource;
                while (o != null)
                {
                    if (o is Button) return;
                    try { o = VisualTreeHelper.GetParent(o); } catch { break; }
                }
                DockMenu.PlacementTarget = Glass;
                DockMenu.IsOpen = true;
            };

            Win.Closed += delegate
            {
                Cfg.Save();
                CloseGhost();
                RemoveHotkey();
                if (TrayIcon != null) TrayIcon.Dispose();
                try { Application.Current.Shutdown(); } catch { }
            };
        }

        // el imán de los bordes. Al soltar la barra se mira qué borde le queda más cerca y se le asigna
        // esa posición, así que aterriza en el mismo sitio que si se hubiera elegido en Ajustes: centrada
        // en el eje transversal y con su hueco de tarea. La excepción es la posición "libre": ahí manda lo
        // que decidió el usuario con la mano, y la barra se queda exactamente donde la soltó.
        string _posAntesDeArrastrar = "bottom";

        public void SaveDockAfterDrag()
        {
            var wa = SystemParameters.WorkArea;
            double m = Layout.Margin;
            double l = Win.Left + m, t = Win.Top + m;

            // las coordenadas del último soltado se guardan siempre: si no, elegir "Libre" en Ajustes
            // después de arrastrar llevaba la barra a la posición de otra tarde en vez de a donde está
            Cfg.Left = (int)l;
            Cfg.Top = (int)t;

            // la posición en la que estaba el mando antes de este arrastre, no la que sale de él: el
            // propio imán la reescribe, y "libre" tiene que juzgarse por lo que puso el usuario
            if (_posAntesDeArrastrar == "free") { Cfg.Save(); return; }

            // el punto donde se levantó el botón, en DIP de pantalla: esa es la intención de la mano, y
            // el imán juzga por ahí. Se pregunta al sistema y no a WPF porque al salir de DragMove el
            // ratón de WPF ya no sabe donde está (devuelve 0,0, que es la esquina de arriba a la izquierda
            // de la ventana: juzgar por ahí anclaría la barra al borde que tocara por accidente).
            double px, py;
            if (!PuntoDeSueltado(out px, out py))
            {
                px = Win.Left + Win.Width / 2.0;
                py = Win.Top + Win.Height / 2.0;
            }

            double arriba, abajo, izquierda, derecha;
            string mejor = EdgeSnap.BordeMasCercano(px, py, wa.X, wa.Y, wa.Width, wa.Height,
                out arriba, out abajo, out izquierda, out derecha);

            Log.Write("borde mas cercano: " + Cfg.Position + " -> " + mejor +
                      " (arriba " + (int)arriba + ", abajo " + (int)abajo +
                      ", izq " + (int)izquierda + ", der " + (int)derecha + ")");
            Cfg.Position = mejor;
            Cfg.Save();
            ApplyLayout();
        }

        // el cursor en DIP de pantalla: GetCursorPos devuelve pixeles fisicos absolutos y TransformFromDevice
        // los pasa a las mismas unidades con las que Win.Left y SystemParameters.WorkArea ya hablan.
        bool PuntoDeSueltado(out double px, out double py)
        {
            px = py = 0.0;
            Native.POINT p;
            if (!Native.GetCursorPos(out p)) return false;
            var src = PresentationSource.FromVisual(Win);
            if (src == null || src.CompositionTarget == null) return false;
            var d = src.CompositionTarget.TransformFromDevice.Transform(new Point(p.X, p.Y));
            px = d.X;
            py = d.Y;
            return true;
        }
    }
}
