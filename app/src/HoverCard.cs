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
    public class HoverCard
    {
        // 320 de ancho: con 12 de margen del cristal quedan 296 de texto, que es lo que ocupa una linea
        // de generos y una sinopsis corta sin partirse en cinco renglones.
        const double Ancho = 320;
        // los heroes de la comunidad son ~920x430 (2,14:1); a 296 de ancho eso son ~138 de alto
        const double BannerAlto = 140;
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
            _cartel.CornerRadius = new CornerRadius(Radio, Radio, 0, 0);

            _nom = Etiqueta(14, "PanelFg", FontWeights.SemiBold);
            _nom.TextTrimming = TextTrimming.CharacterEllipsis;
            _meta = Etiqueta(11, "PanelSub", FontWeights.Normal);
            _meta.TextWrapping = TextWrapping.Wrap;
            _desc = Etiqueta(11.5, "PanelFg", FontWeights.Normal);
            _desc.TextWrapping = TextWrapping.Wrap;
            // cinco renglones es lo que cabe sin que la ficha se coma media pantalla; la sinopsis de
            // Steam es larga a proposito y recortarla con puntos suspensivos es preferible a un scroll
            _desc.MaxHeight = 76;
            // WordEllipsis, no CharacterEllipsis: recortar por caracteres dejaba la ultima linea
            // terminando en "solitario par…", que parece un fallo antes que un resumen
            _desc.TextTrimming = TextTrimming.WordEllipsis;
            _pie = Etiqueta(10, "PanelSub", FontWeights.Normal);
            _pie.TextTrimming = TextTrimming.CharacterEllipsis;

            _texto = new StackPanel { Margin = new Thickness(14, 11, 14, 13) };
            _texto.Children.Add(_nom);
            _texto.Children.Add(_meta);
            _texto.Children.Add(_desc);
            _texto.Children.Add(_pie);

            var cuerpo = new StackPanel();
            cuerpo.Children.Add(_cartel);
            cuerpo.Children.Add(_texto);
            _shell.Child = cuerpo;
            _win.Content = _shell;

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
            return h > 2 ? h + 2 * Cristal : 240.0;
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

            _nom.Text = it.Name;
            string generos = f != null ? f.Generos : null;
            string autores = f != null ? f.Autores : null;

            if (f != null && f.TieneAlgo)
            {
                var linea = new List<string>();
                if (!string.IsNullOrEmpty(generos)) linea.Add(generos);
                if (!string.IsNullOrEmpty(autores)) linea.Add(autores);
                if (!string.IsNullOrEmpty(f.Lanzamiento)) linea.Add(f.Lanzamiento);
                _meta.Text = string.Join("  ·  ", linea.ToArray());
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
                _cartel.Visibility = Visibility.Visible;
            }
            else _cartel.Visibility = Visibility.Collapsed;

            return _nom.Text.Length > 0;
        }

        // la ficha cae al lado de la pildora que da al centro del escritorio, centrada en el tile, igual
        // que el panel de un grupo. Con la barra en "libre" no hay borde del que fiarse: se mira en que
        // mitad de la pantalla esta y se saca la ficha hacia donde hay sitio.
        void Colocar()
        {
            // El alto lo da el cristal, no la ventana: SizeToContent=Height cierra el tamano del HWND
            // DESPUES del arrange, asi que en el instante en que Soltar llama a Colocar, _win.ActualHeight
            // todavia lleva la medida anterior (la de la carta vacia, 24 DIP) y la carta sube colocada con
            // un alto falso. La ventana mide el cristal colocado mas sus dos margenes de sombra.
            double alto = Alto();
            double ancho = _win.Width;
            double left = _win.Left, top = _win.Top;

            var b = _tile;
            if (b != null && b.IsVisible)
            {
                double dip = Dock.DipScale(_win);
                Point a = b.PointToScreen(new Point(0, 0));
                Point c = b.PointToScreen(new Point(b.ActualWidth, b.ActualHeight));
                double i = a.X / dip, d = a.Y / dip, der = c.X / dip, ab = c.Y / dip;
                string pos = _dock.Cfg.Position;
                if (pos == "free")
                {
                    // sin borde elegido manda la mitad de la pantalla en la que cae el tile: se saca la
                    // ficha hacia donde hay sitio. Se juzga por el tile y no por la ventana de la pildora
                    // porque esta aun puede estar aparcada fuera de pantalla.
                    var wa = SystemParameters.WorkArea;
                    pos = ((d + ab) / 2.0 > wa.Top + wa.Height / 2.0) ? "bottom" : "top";
                    if (_dock.Lay.Vert) pos = ((i + der) / 2.0 > wa.Left + wa.Width / 2.0) ? "right" : "left";
                }
                switch (pos)
                {
                    case "top": left = (i + der) / 2 - ancho / 2; top = ab + Hueco; break;
                    case "left": left = der + Hueco; top = (d + ab) / 2 - alto / 2; break;
                    case "right": left = i - Hueco - ancho; top = (d + ab) / 2 - alto / 2; break;
                    default: left = (i + der) / 2 - ancho / 2; top = d - Hueco - alto; break;
                }
            }

            var area = SystemParameters.WorkArea;
            // el area de trabajo esta en DIP de la pantalla primaria; WPF mueve ventanas en DIP del
            // mismo sistema, asi que el recorte es directo
            _win.Left = Math.Max(area.Left + 6, Math.Min(left, area.Right - ancho - 6));
            _win.Top = Math.Max(area.Top + 6, Math.Min(top, area.Bottom - alto - 6));
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
                    // 700 de ancho: el hero_thumb son ~920 px y la carta mide 296 DIP; decodificar a
                    // la mitad de resolucion sale igual de nitido y cuesta la cuarta parte de memoria
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
