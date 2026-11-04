using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace GamesDock
{
    // La ficha que sale al dejar el cursor sobre un icono: un banner del juego y sus datos basicos.
    // Sustituye al globo de texto de toda la vida cuando la funcion esta encendida en Ajustes.
    //
    // Va en su propia ventana por el mismo motivo que el fantasma del arrastre: la pildora vive pegada
    // al escritorio y solo mide lo que mide su cristal, asi que cualquier cosa que sobresalga de su
    // rectangulo se corta contra su borde. Es NOACTIVATE y click-through (WS_EX_TRANSPARENT): el cursor
    // puede pasar por encima sin dejar de estar sobre el tile, y al salir del tile la ficha se apaga
    // sola. No es un panel interactivo, es un aviso.
    public partial class HoverCard
    {
        // 320 de ancho: con 12 de margen del cristal quedan 296 de texto, que es lo que ocupa una linea
        // de generos y una sinopsis corta sin partirse en cinco renglones.
        const double Ancho = 320;
        // El alto del banner lo manda el dibujo, no la carta. Los heroes de la comunidad salen a
        // 850x274 (3,10:1) y la carula de la tienda a 460x215 (2,14:1): con un cuadro fijo de 140 el
        // UniformToFill llenaba la altura y se comia un tercio del ancho de los heroes, que es justo lo
        // que se ve cortado en Counter-Strike 2 o PRAGMATA. BannerAlto queda como valor de descanso
        // (la carta ya abierta que espera a que aterrice su dibujo) y las dos franjas sujetan los
        // extremos: un cuadro no puede ocupar media pantalla y un 21:9 no puede ser una tira de 40 px.
        const double BannerAlto = 140;
        const double BannerMin = 84;
        const double BannerMax = 176;
        const double Hueco = 12;          // aire entre la pildora y la ficha
        const double Radio = 14;

        // 380 ms: el globo clasico tarda ~500 en salir y la ficha es mas grande, pero por debajo de
        // 300 aparece sola al menor descuido del raton mientras se barre la fila. Con la ficha ya
        // abierta, cambiar de icono usa 140: el usuario ya esta mirando, no hay que volver a ganarse
        // la atencion.
        const int Espera = 380;
        const int EsperaCambio = 140;

        // margen del cristal: es el hueco donde cabe la sombra, y tambien lo que hay que sumar al alto
        // del contenido para saber cuanto mide la ventana
        const double Cristal = 12;

        // decodificados en RAM: una ficha abierta son ~1,4 MB de bitmap, y con 8 en cache el proceso
        // no pasa de 12 MB por muy larga que sea la barra
        const int TopoCache = 8;
        static readonly Dictionary<string, ImageSource> Bmps = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);
        static readonly List<string> BmpsOrden = new List<string>();

        readonly Dock _dock;
        readonly Window _win;
        readonly Border _shell;
        readonly Border _cartel;
        readonly Image _img;
        readonly TextBlock _nom, _meta, _desc, _pie;
        readonly StackPanel _texto;
        readonly Grid _content;
        readonly ScaleTransform _cardScale;
        readonly Border _personalFrame;
        readonly TextBlock _personalTitle, _personal;

        DispatcherTimer _dwell;
        ItemSpec _apuntero;     // el tile que el cursor esta mirando ahora mismo
        Button _tile;
        string _clave;          // la clave de la ficha que esta en pantalla
        bool _visible;

        public HoverCard(Dock dock)
        {
            _dock = dock;

            _win = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                ShowActivated = false,
                Topmost = true,
                Title = "GamesDockFicha",
                Background = Brushes.Transparent,
                SizeToContent = SizeToContent.Height,
                Width = Ancho,
                WindowStartupLocation = WindowStartupLocation.Manual,
                IsHitTestVisible = false,
                UseLayoutRounding = true,
                SnapsToDevicePixels = true,
                Left = -32000.0,
                Top = -32000.0
            };

            _shell = new Border
            {
                Margin = new Thickness(Cristal),
                CornerRadius = new CornerRadius(Radio),
                BorderThickness = new Thickness(1),
                ClipToBounds = true
            };
            _shell.SetResourceReference(Border.BackgroundProperty, "PanelBg");
            _shell.SetResourceReference(Border.BorderBrushProperty, "PanelEdge");
            _shell.Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(3, 5, 10),
                BlurRadius = 26,
                ShadowDepth = 9,
                Opacity = 0.5,
                Direction = 270
            };

            // el banner va pegado a los cantos de arriba: si fuera un cuadro con margen, la ficha
            // pareceria una tarjeta de presentacion en vez de lo que es, un fotograma del juego
            _img = new Image { Stretch = Stretch.UniformToFill };
            RenderOptions.SetBitmapScalingMode(_img, BitmapScalingMode.Fant);
            _cartel = new Border
            {
                Height = BannerAlto,
                Visibility = Visibility.Collapsed,
                ClipToBounds = true,
                Child = _img
            };
            // esquinas redondeadas solo arriba: el borde inferior del banner tiene que morir contra el
            // texto, no flotar
            _cartel.CornerRadius = new CornerRadius(Radio - 1, Radio - 1, 0, 0);
            _cartel.SizeChanged += delegate { ClipBanner(); };

            _nom = Etiqueta(16, "PanelFg", FontWeights.SemiBold);
            _nom.TextWrapping = TextWrapping.Wrap; _nom.TextTrimming = TextTrimming.WordEllipsis;
            _nom.LineHeight = 22; _nom.MaxHeight = 44;
            _meta = Etiqueta(11.5, "PanelSub", FontWeights.Normal);
            _meta.TextWrapping = TextWrapping.Wrap; _meta.TextTrimming = TextTrimming.WordEllipsis;
            _meta.LineHeight = 16.5; _meta.MaxHeight = 49.5; _meta.Margin = new Thickness(0, 8, 0, 0);
            _desc = Etiqueta(12, "PanelFg", FontWeights.Normal);
            _desc.TextWrapping = TextWrapping.Wrap; _desc.TextTrimming = TextTrimming.WordEllipsis;
            _desc.LineHeight = 18; _desc.MaxHeight = 90; _desc.Margin = new Thickness(0, 10, 0, 0);
            _pie = Etiqueta(10.5, "PanelSub", FontWeights.Normal);
            _pie.TextWrapping = TextWrapping.Wrap; _pie.TextTrimming = TextTrimming.WordEllipsis;
            _pie.LineHeight = 15; _pie.MaxHeight = 30; _pie.Margin = new Thickness(0, 12, 0, 0);
            _personalTitle = Etiqueta(11.5, "MenuAccent", FontWeights.SemiBold);
            _personalTitle.Text = Lang.T("Tu actividad en Steam");
            _personal = Etiqueta(11.5, "PanelFg", FontWeights.Normal);
            _personal.TextWrapping = TextWrapping.Wrap; _personal.LineHeight = 18; _personal.Margin = new Thickness(0, 5, 0, 0);
            var activity = new StackPanel(); activity.Children.Add(_personalTitle); activity.Children.Add(_personal);
            _personalFrame = new Border { Margin = new Thickness(0, 12, 0, 0), Padding = new Thickness(0, 10, 0, 0),
                BorderThickness = new Thickness(0, 1, 0, 0), Visibility = Visibility.Collapsed, Child = activity };
            _personalFrame.SetResourceReference(Border.BorderBrushProperty, "CtlEdge");
            _texto = new StackPanel { Margin = new Thickness(16, 14, 16, 16) };
            _texto.Children.Add(_nom); _texto.Children.Add(_meta); _texto.Children.Add(_personalFrame);
            _texto.Children.Add(_desc); _texto.Children.Add(_pie);
            var body = new StackPanel(); body.Children.Add(_cartel); body.Children.Add(_texto); _shell.Child = body;
            _cardScale = new ScaleTransform(1, 1);
            _content = new Grid { Width = Ancho, LayoutTransform = _cardScale };
            _content.Children.Add(_shell); _win.Content = _content;
            _win.Closed += delegate { CancelActivity(); };

            // cada refresco cambia el alto (llega la ficha, aterriza un banner) y la carta va anclada por
            // su borde inferior cuando la barra esta abajo: si no se recoloca al cambiar de tamano, crece
            // hacia abajo y se monta sobre el icono que la ha llamado. Se mira el tamano de la VENTANA y no
            // el del cristal porque SizeToContent cierra el alto despues del arrange y el evento de la
            // ventana es el unico que llega con el numero ya definitivo.
            _win.SizeChanged += delegate
            {
                if (_visible) Colocar();
            };

            _win.SourceInitialized += delegate
            {
                try
                {
                    IntPtr h = new WindowInteropHelper(_win).Handle;
                    int ex = Native.GetWindowLong(h, -20);
                    // TRANSPARENT (click-through) | TOOLWINDOW (ni alt-tab) | NOACTIVATE (la pildora ya
                    // nunca se activa; una ficha que robara el foco cortaria lo que se estuviera escribiendo)
                    Native.SetWindowLong(h, -20, ex | 0x20 | 0x80 | unchecked((int)0x08000000));
                }
                catch (Exception ex2) { Log.Write("estilo de la ficha: " + ex2.Message); }
            };

            _dwell = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Espera) };
            _dwell.Tick += delegate { _dwell.Stop(); Soltar(); };

            // se avisa con la clave cuando llega la ficha o cuando aterriza un banner: la carta que hay
            // en pantalla se vuelve a pintar si esa clave es la suya. Y se recoloca, porque la ficha nueva
            // es mas alta que el nombre solo: sin esto creceria hacia abajo y se montaria sobre el tile.
            GameInfo.Listo += delegate(string k)
            {
                if (!_visible || k != _clave || _apuntero == null) return;
                if (Rellenar(_apuntero)) { _win.UpdateLayout(); Colocar(); }
            };

            // la ventana se muestra una vez para pagar aqui su creacion, igual que el fantasma: el
            // primer hover no puede coincidir con la primera vez que Windows monta una HWND
            _win.Show();
            _win.Hide();
        }

        static TextBlock Etiqueta(double size, string clave, FontWeight peso)
        {
            var t = Res.Label(string.Empty, size, clave);
            t.FontWeight = peso;
            return t;
        }

        // ------------------------------------------------------------------ el gesto
        // Entra el cursor en un tile. La red se dispara YA, antes de que venza la espera: si el usuario
        // decide quedarse, la ficha estara lista cuando la ventana se abra.
        public void Sobreviene(ItemSpec it, Button tile)
        {
            if (it == null || tile == null) return;
            if (!_dock.UsaFicha(it)) return;
            if (ReferenceEquals(it, _apuntero) && (_visible || (_dwell != null && _dwell.IsEnabled))) return;

            _apuntero = it;
            _tile = tile;
            StartActivity(it);
            _dwell.Interval = TimeSpan.FromMilliseconds(_visible ? EsperaCambio : Espera);
            _dwell.Stop();
            _dwell.Start();

            if (GameInfo.Buscar(it) == null) GameInfo.Pedir(it);
        }

        // Sale el cursor del tile (o de la barra entera, o se pone a arrastrar un icono).
        public void Salir()
        {
            _dwell.Stop();
            _apuntero = null;
            _tile = null;
            CancelActivity();
            Ocultar();
        }

        public void Ocultar()
        {
            if (!_visible) return;
            _visible = false;
            _clave = null;
            // ocultar y aparcar, no cerrar: la ventana tiene que seguir viva para el siguiente hover.
            // Aqui NO se baja la opacidad de la ventana: lo hace el fundido de entrada sobre el cristal,
            // y un Window.Opacity=0 no se repone solo al volver a mostrar -> la carta quedaria invisible
            // para siempre despues del primer cierre.
            if (_win.IsVisible) _win.Hide();
            _win.Left = -32000.0;
            _win.Top = -32000.0;
        }

        // ------------------------------------------------------------------ pintar
        double Alto()
        {
            double h = _shell.ActualHeight;
            return (h > 2 ? h + 2 * Cristal : 240.0) * _cardScale.ScaleY;
        }

        // El marco del banner, a la medida del dibujo. El ancho utile es la carta menos los dos margenes
        // de sombra y menos el borde del cristal (1 DIP por cada lado), y con eso UniformToFill ya no
        // recorta nada mientras la proporcion caiga dentro de las dos franjas: fuera de ellas recorta por
        // igual arriba y abajo, que es donde un hero pierde menos cosas que en los cantos.
        static double AltoBanner(ImageSource b)
        {
            var bs = b as BitmapSource;
            if (bs == null || bs.PixelWidth <= 0 || bs.PixelHeight <= 0) return BannerAlto;
            double anchoUtil = Ancho - 2 * Cristal - 2;
            double h = anchoUtil * (double)bs.PixelHeight / bs.PixelWidth;
            return Math.Round(Math.Max(BannerMin, Math.Min(BannerMax, h)));
        }

        void Soltar()
        {
            var it = _apuntero;
            if (it == null) return;
            if (!Rellenar(it)) return;

            _visible = true;
            if (!_win.IsVisible) _win.Show();
            _win.UpdateLayout();
            Colocar();
            Fundido();
        }

        // devuelve false si no hay nada que ensenar, para no abrir una ventana en blanco
        bool Rellenar(ItemSpec it)
        {
            var f = GameInfo.Buscar(it);
            _clave = GameInfo.Clave(it);

            double factor = _dock.Cfg.HoverCardLarge ? 1.15 : 1;
            _cardScale.ScaleX = _cardScale.ScaleY = factor; _win.Width = Ancho * factor;
            _nom.Text = it.Name;
            string generos = f != null ? f.Generos : null;
            string autores = f != null ? f.Autores : null;

            if (f != null && f.TieneAlgo)
            {
                var linea = new List<string>();
                if (!string.IsNullOrEmpty(generos)) linea.Add(generos);
                if (!string.IsNullOrEmpty(autores)) linea.Add(autores);
                if (!string.IsNullOrEmpty(f.Lanzamiento)) linea.Add(f.Lanzamiento);
                _meta.Text = string.Join(" · ", linea.ToArray());
                _desc.Text = f.Sinopsis ?? string.Empty;
                _meta.Visibility = _meta.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                // sin ficha: la carta se queda con lo que decia el globo de texto, para que no se
                // pierda informacion al encender la funcion
                _meta.Text = _dock.RutaCorta(it);
                _meta.Visibility = _meta.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
                _desc.Text = string.Empty;
            }
            _desc.Visibility = _desc.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            _pie.Text = _dock.PieFicha(it);
            _pie.Visibility = _pie.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

            string ruta = GameInfo.ElegirBanner(f, _clave);
            var bmp = ruta != null ? Cargar(ruta) : null;
            if (bmp != null)
            {
                _img.Source = bmp;
                _cartel.Height = AltoBanner(bmp);
                _cartel.Visibility = Visibility.Visible;
            }
            else _cartel.Visibility = Visibility.Collapsed;
            FillActivity(it);
            FitTextHeight();
            return _nom.Text.Length > 0;
        }

        // la ficha cae al lado de la pildora que da al centro del escritorio, centrada en el tile, igual
        // que el panel de un grupo. Con la barra en "libre" no hay borde del que fiarse: se mira en que
        // mitad de la pantalla esta y se saca la ficha hacia donde hay sitio.
        void Colocar()
        {
            double scale = Dock.DipScale(_dock.Win);
            Rect anchor = Displays.WindowBounds(_dock.Win);
            if (_tile != null && _tile.IsVisible)
            {
                var a = _tile.PointToScreen(new Point());
                var b = _tile.PointToScreen(new Point(_tile.ActualWidth, _tile.ActualHeight));
                anchor = new Rect(a, b);
            }
            var bounds = MonitorPlacement.PopupBounds(anchor, new Size(_win.Width * scale, Alto() * scale),
                _dock.CurrentDisplay.WorkArea, _dock.Cfg.Position, Hueco * scale, 6 * scale);
            Displays.MoveWindow(_win, bounds.TopLeft);
        }

        void Fundido()
        {
            // opacidad del cristal, no de la ventana: sobre una ventana transparentemente recortada,
            // bajar Window.Opacity es lo mismo que bajar la del contenido, y aqui no hay nada detras
            _shell.Opacity = 0.0;
            var a = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            _shell.BeginAnimation(UIElement.OpacityProperty, a);
        }

        // ------------------------------------------------------------------ imagen
        // Los banners se guardan con extension neutra (.img) porque pueden ser png o jpeg segun lo que
        // respondiera el CDN, asi que se decodifica desde el stream y WPF mira la firma del contenido.
        static ImageSource Cargar(string ruta)
        {
            ImageSource viejo;
            if (Bmps.TryGetValue(ruta, out viejo)) return viejo;
            try
            {
                var bi = new BitmapImage();
                using (var fs = File.OpenRead(ruta))
                using (var ms = new MemoryStream())
                {
                    fs.CopyTo(ms);
                    ms.Position = 0;
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    // 700 de ancho: el hero_thumb de la comunidad mide 850 px y la carta 296 DIP, asi que
                    // decodificar por debajo del original sale igual de nitido y guarda menos bitmap
                    bi.DecodePixelWidth = 700;
                    bi.StreamSource = ms;
                    bi.EndInit();
                }
                bi.Freeze();
                Anadir(ruta, bi);
                return bi;
            }
            catch (Exception ex)
            {
                Log.Write("banner decodificado: " + ex.Message);
                return null;
            }
        }

        static void Anadir(string ruta, ImageSource s)
        {
            if (Bmps.Count >= TopoCache)
            {
                string fuera = BmpsOrden[0];
                BmpsOrden.RemoveAt(0);
                Bmps.Remove(fuera);
            }
            Bmps[ruta] = s;
            BmpsOrden.Add(ruta);
        }
    }
}
