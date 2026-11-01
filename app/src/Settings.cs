using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace GamesDock
{
    // Los unicos estilos que construyen los paneles viven ya en el diccionario de aplicacion
    // (xaml/styles.xaml), aqui solo se buscan por clave.
    static class Res
    {
        public static Style Style(string key) { return (Style)Application.Current.Resources[key]; }

        public static TextBlock Label(string txt, double size, string resKey)
        {
            var t = new TextBlock { Text = txt, FontSize = size };
            TextOptions.SetTextFormattingMode(t, TextFormattingMode.Display);
            t.SetResourceReference(TextBlock.ForegroundProperty, resKey);
            return t;
        }
    }

    // Panel de cristal sin marco: borde redondeado, sombra propia y entrada animada. Una Window con
    // WindowStyle=None + AllowsTransparency de verdad deja ver el escritorio de atras, asi que el
    // margen del borde exterior es el sitio donde cabe la sombra.
    public class GlassPanel
    {
        public Window Win;
        public StackPanel Body;
        public TextBlock Tit;
        bool _veilo;

        public GlassPanel(string titleTxt, double width,
                          string subTxt = null)
        {
            // el valor por defecto no puede ser la cadena traducida: un parametro opcional ha de ser
            // constante en tiempo de compilacion
            if (subTxt == null) subTxt = Lang.T("clic derecho en la barra vacía - Esc para cerrar");
            Win = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                SizeToContent = SizeToContent.Height,
                Width = width,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Title = titleTxt,
                Background = Brushes.Transparent,
                UseLayoutRounding = true,
                ShowActivated = true,
                // es un emergente modal: mientras este abierto nada de atras se puede tocar, asi que
                // tampoco tiene sentido que una ventana normal lo tape
                Topmost = true
            };
            Win.KeyUp += (s, e) => { if (e.Key == System.Windows.Input.Key.Escape) try { Win.Close(); } catch { } };
            Win.Closed += delegate { if (_veilo) { _veilo = false; Veil.Pop(); } };

            var shell = new Border
            {
                Margin = new Thickness(14),
                CornerRadius = new CornerRadius(18),
                BorderThickness = new Thickness(1)
            };
            shell.SetResourceReference(Border.BackgroundProperty, "PanelBg");
            shell.SetResourceReference(Border.BorderBrushProperty, "PanelEdge");
            shell.Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(3, 5, 10),
                BlurRadius = 34,
                ShadowDepth = 12,
                Opacity = 0.55,
                Direction = 270
            };

            Body = new StackPanel { Margin = new Thickness(20, 16, 20, 18) };

            var hdr = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            var tit = Res.Label(titleTxt, 15, "PanelFg");
            tit.FontWeight = FontWeights.SemiBold;
            tit.VerticalAlignment = VerticalAlignment.Center;
            Tit = tit;
            var sub = Res.Label(subTxt, 10.5, "PanelSub");
            sub.Margin = new Thickness(0, 3, 0, 0);
            var headsp = new StackPanel();
            headsp.Children.Add(tit);
            headsp.Children.Add(sub);
            SetColumn(headsp, 0);

            var x = new Button
            {
                Style = Res.Style("GhostBtn"),
                Content = "" + (char)0x2715,
                VerticalAlignment = VerticalAlignment.Center
            };
            SetColumn(x, 1);
            x.Click += delegate { try { Window.GetWindow(x).Close(); } catch { } };
            hdr.Children.Add(headsp);
            hdr.Children.Add(x);

            // la cabecera sirve de barra de arrastre, salvo cuando se pulsa un boton: si no, el cierre
            // tambien iniciaria un DragMove y se comeria el clic
            hdr.Background = Brushes.Transparent;
            hdr.PreviewMouseLeftButtonDown += (s, e) =>
            {
                DependencyObject o = (DependencyObject)e.OriginalSource;
                while (o != null)
                {
                    if (o is ButtonBase) return;
                    try { o = VisualTreeHelper.GetParent(o); } catch { o = null; }
                }
                try { Window.GetWindow((Visual)s).DragMove(); } catch { }
            };
            Body.Children.Add(hdr);

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Focusable = false,
                // el techo era fijo en 640, y con la sección de la clave los ajustes ya no cabían ahí:
                // la última fila se cortaba sin barra a la vista. Se da tanto alto como deja el área de
                // trabajo, con un suelo para pantallas pequeñas y un techo para no estirar el panel de
                // un aviso. El ScrollViewer sigue ahí para lo que aun así no quepa.
                MaxHeight = BodyHeight(),
                Content = Body
            };
            shell.Child = scroll;
            Win.Content = shell;
        }

        static void SetColumn(UIElement e, int c) { Grid.SetColumn(e, c); }

        // Cuanto puede ocupar el cuerpo de un panel: lo que deja el area de trabajo menos la cabecera,
        // los margenes del cristal y un dedo de aire. Con un suelo para pantallas pequenas y un techo
        // para que un monitor grande no estire el aviso de dos lineas.
        //
        // El aire era de 110 y el cristal solo gasta 30 (medido: 736 de ventana para 706 de cuerpo), asi
        // que 80 px eran puro margen perdido. Con 70 el panel de ajustes entra entero en un 1080p al
        // 125 %: con 110 la ultima fila se quedaba detras del viewport, y la barra de scroll esta oculta
        // a proposito, o sea que esa fila era invisible e inalcanzable a la vista.
        static double BodyHeight()
        {
            double h = SystemParameters.WorkArea.Height - 70;
            if (h < 420) h = 420;
            if (h > 760) h = 760;
            return h;
        }

        // entrada y salida: el panel se hincha desde el 95,5 % mientras enciende, en el primer pintado
        public void PlayIn()
        {
            var el = (FrameworkElement)Win.Content;
            var rt = new ScaleTransform(0.955, 0.93);
            el.RenderTransformOrigin = new Point(0.5, 0.4);
            el.RenderTransform = rt;
            el.Opacity = 0.0;
            Win.ContentRendered += delegate
            {
                BringToTop(Win);
                var st = new Storyboard();
                Add(st, "Opacity", 0.0, 1.0, 170, el);
                Add(st, "(UIElement.RenderTransform).(ScaleTransform.ScaleX)", 0.955, 1.0, 300, el);
                Add(st, "(UIElement.RenderTransform).(ScaleTransform.ScaleY)", 0.93, 1.0, 360, el);
                st.Begin(el, true);   // Begin(FrameworkElement, bool esControllable)
            };
        }

        // el panel es modal de verdad: velo a pantalla completa mientras esta abierto, y el velo se
        // quita solo cuando se cierra (Guardar, Enter, Esc o la cruz)
        public void ShowModal()
        {
            _veilo = true;
            Veil.Push();
            PlayIn();
            Win.ShowDialog();
        }

        static void Add(Storyboard st, string path, double from, double to, int ms, DependencyObject target)
        {
            var a = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = new Duration(TimeSpan.FromMilliseconds(ms)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTargetProperty(a, new PropertyPath(path));
            Storyboard.SetTarget(a, target);
            st.Children.Add(a);
        }

        public static void BringToTop(Window w)
        {
            try
            {
                IntPtr h = new WindowInteropHelper(w).Handle;
                if (h == IntPtr.Zero) return;
                // HWND_TOP con SWP_NOMOVE|NOSIZE|NOACTIVATE: reordenar no pide permiso de primer plano,
                // y el proceso del dock vive en segundo plano (ShowDialog solo no saca la ventana de atras)
                Native.SetWindowPos(h, IntPtr.Zero, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
            }
            catch { }
        }
    }

    public class Stepper
    {
        public int Min, Max, StepValue, Value;
        public string Suffix;
        public TextBlock Label;
        public Border Fill;
        public double BarWidth = 130.0;
        public Action<int> OnSet;
    }

    // Las dos mitades del panel viven en este archivo y en UpdateUI.cs: aqui esta la rejilla de ajustes y
    // alla lo que se pregunta y se responde sobre las versiones.
    public static partial class Settings
    {
        static Window _open;
        static readonly Dictionary<string, Stepper> Steppers = new Dictionary<string, Stepper>();
        static string _promptValue;
        static TextBox _promptBox;

        public static void Show(Dock dock)
        {
            if (_open != null)
            {
                try { _open.Activate(); return; } catch { _open = null; }
            }
            var cfg = dock.Cfg;
            var par = new GlassPanel(Lang.T("Ajustes de GamesDock"), 790);
            var w = par.Win;
            Steppers.Clear();

            // Las dos columnas se reparten el ancho del cristal con un hueco fijo en medio. Antes cada
            // una llevaba 340 px contados: sobraban 48 px a la derecha y el panel se veia descompensado.
            var sp = new Grid();
            sp.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            sp.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
            sp.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            // dos filas: la de las columnas y, debajo de las dos, la del pie a lo ancho. El pie en una
            // sola columna eran cuatro lineas que empujaban la ultima fila de esa columna fuera del alto
            // de la pantalla (1080p al 125 %), y detras de una barra de scroll que ni se ve.
            sp.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            sp.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            var izq = new StackPanel();
            var der = new StackPanel();
            Grid.SetColumn(izq, 0);
            Grid.SetColumn(der, 2);
            sp.Children.Add(izq);
            sp.Children.Add(der);
            par.Body.Children.Add(sp);

            // --------------------------------------------------------- columna izquierda
            izq.Children.Add(SecHead("Aspecto"));

            var posRow = new WrapPanel();
            var posMap = new Dictionary<string, string>
            {
                { "Abajo", "bottom" }, { "Arriba", "top" }, { "Izquierda", "left" },
                { "Derecha", "right" }, { "Libre", "free" }
            };
            foreach (var k in new[] { "Abajo", "Arriba", "Izquierda", "Derecha", "Libre" })
            {
                string id = posMap[k];
                posRow.Children.Add(Chip(Lang.T(k), "posicion", id == cfg.Position,
                    delegate
                    {
                        // al pasar de un borde a Libre la barra se queda donde esta: sin este aviso
                        // PlaceDock usaria el ultimo punto libre que recuerda la config, que puede ser el
                        // de otra tarde y acabar a medias fuera de la pantalla
                        if (id == "free" && cfg.Position != "free") dock.AnotarPuestoActual();
                        cfg.Position = id;
                        cfg.Save();
                        dock.ApplyLayout();
                    }));
            }
            izq.Children.Add(Field(Lang.T("Posición de la barra"), posRow, 14));

            izq.Children.Add(Field(Lang.T("Tamaño del icono"),
                Stepper("iconSize", 28, 76, 2, cfg.IconSize, "px", v => { cfg.IconSize = v; cfg.Save(); dock.ApplyLayout(); dock.Refresh(false); }), 14));

            // la forma es el recorte, no el icono: las tres chips repintan la misma fila de pngs
            var formaRow = new WrapPanel();
            var formaMap = new[]
            {
                new[] { "Por defecto", "defecto" }, new[] { "iOS", "ios" }, new[] { "Esfera", "esfera" }
            };
            foreach (var f in formaMap)
            {
                string id = f[1];
                formaRow.Children.Add(Chip(Lang.T(f[0]), "forma", id == (cfg.IconShape ?? "defecto"),
                    delegate { cfg.IconShape = id; cfg.Save(); dock.Refresh(false); }));
            }
            izq.Children.Add(Field(Lang.T("Forma del icono"), formaRow, 14));

            izq.Children.Add(Field(Lang.T("Opacidad en reposo"),
                Stepper("idleOpacity", 10, 100, 5, cfg.IdleOpacity, "%", v => { cfg.IdleOpacity = v; cfg.Save(); dock.SetWindowOpacity(v / 100.0, 120); }), 14));

            var temRow = new WrapPanel();
            foreach (var kv in Themes.All) temRow.Children.Add(ThemeChip(kv.Key, kv.Key == cfg.Theme, dock));
            izq.Children.Add(Field(Lang.T("Tema de la barra"), temRow, 8));

            // cada idioma se escribe en su propio idioma: es la convención de los selectores y evita
            // que "Español" aparezca como "Spanish" cuando ya estás en inglés
            var langRow = new WrapPanel();
            foreach (var l in new[] { new[] { "auto", "Auto" }, new[] { "es", "Español" }, new[] { "en", "English" } })
            {
                string id = l[0];
                langRow.Children.Add(Chip(l[1], "idioma", id == (cfg.Language ?? "auto"),
                    delegate { SetLanguage(dock, cfg, id); }));
            }
            izq.Children.Add(Field(Lang.T("Idioma de la interfaz"), langRow, 8));

            // --------------------------------------------------------- columna derecha
            der.Children.Add(SecHead("Qué se ve"));
            var secRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 7) };
            var secMap = new[]
            {
                new[] { "Favoritos", "fav" }, new[] { "Juegos", "games" },
                new[] { "Apps", "apps" }, new[] { "Todo", "all" }
            };
            foreach (var s in secMap)
            {
                string id = s[1];
                secRow.Children.Add(Chip(Lang.T(s[0]), "seccion", id == cfg.Section, delegate { dock.SetSection(id); }));
            }
            der.Children.Add(secRow);

            // ------------------------------------------------------- iconos de la comunidad
            // El unico campo del programa que guarda una credencial. Se queda en el config.json del
            // usuario y nunca viaja dentro del exe: una clave metida en un binario publico se recupera
            // descompilando y quien la registró es quien paga el uso que otros hagan de ella.
            // Va el segundo en la columna, no al final: es la caja que busca quien acaba de leer el
            // aviso de "sin clave", y debajo de lo que cabe en pantalla no se ve a primera vista.
            der.Children.Add(SecHead("Iconos de la comunidad"));
            var clave = new TextBox
            {
                Style = Res.Style("GlassText"),
                Text = cfg.SteamGridKey,
                Margin = new Thickness(0, 0, 0, 7)
            };
            Button kBoton = null;
            Action kGuardar = delegate
            {
                cfg.SteamGridKey = (clave.Text ?? string.Empty).Trim();
                cfg.Save();
                bool hay = cfg.SteamGridKey.Length > 0;
                kBoton.Content = Lang.T(hay ? "Clave guardada" : "Clave vaciada");
                Log.Write(hay ? "SteamGridDB: clave guardada en el config" : "SteamGridDB: clave vaciada");
            };
            var kAyuda = Res.Label(Lang.T(
                "Crea una cuenta gratis en steamgriddb.com y saca la clave en Perfil > Preferencias > API. " +
                "Después, el clic derecho en un icono ofrece «Iconos de la comunidad»: el arte que sube la gente, " +
                "en vez del icono oficial."), 10.5, "PanelSub");
            kAyuda.TextWrapping = TextWrapping.Wrap;
            kAyuda.Margin = new Thickness(0, 0, 0, 10);
            var kFila = new WrapPanel();
            kFila.Children.Add(kBoton = Pill(Lang.T("Guardar la clave"), kGuardar));
            kFila.Children.Add(Pill(Lang.T("Abrir la página de la clave"), delegate { Launcher.OpenUrl(SteamGrid.PaginaClave); }));

            clave.KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter) { e.Handled = true; kGuardar(); }
            };
            der.Children.Add(Field(Lang.T("Clave de SteamGridDB"), clave, 0));
            der.Children.Add(kAyuda);
            der.Children.Add(kFila);

            der.Children.Add(SecHead("Comportamiento"));
            der.Children.Add(Switch(Lang.T("Agrandar los iconos al pasar el ratón (efecto Mac)"), cfg.Magnify,
                on => { cfg.Magnify = on; cfg.Save(); dock.ResetProximity(); dock.ApplyLayout(); }));
            der.Children.Add(Switch(Lang.T("Vivir solo en el escritorio (nunca por encima de otras ventanas)"), cfg.DesktopLevel,
                on => { cfg.DesktopLevel = on; cfg.Save(); dock.AttachToDesktop(); dock.ApplyLayout(); }));
            der.Children.Add(Switch(Lang.T("Minimizar todo al lanzar"), cfg.MinimizeAll,
                on => { cfg.MinimizeAll = on; cfg.Save(); }));
            der.Children.Add(Switch(Lang.T("Ocultar el dock al lanzar (Ctrl+Alt+D lo trae)"), cfg.HideOnLaunch,
                on => { cfg.HideOnLaunch = on; cfg.Save(); }));

            der.Children.Add(SecHead("Sistema"));
            var sysRow = new WrapPanel();
            Button b1 = null, b2 = null, b3 = null, b4 = null, b5 = null, b6 = null;
            sysRow.Children.Add(b1 = Pill(Lang.T("Reescanear juegos"), delegate { dock.Rebuild(); b1.Content = Lang.F("Reescaneados: {0}", dock.Cat.Items.Count); }));
            sysRow.Children.Add(b2 = Pill(Lang.T("Vaciar escritorio al dock"), delegate
            {
                if (Desktop.Import(dock.Cfg))
                {
                    dock.Rebuild();
                    b3.Content = Lang.T("Todo devuelto");
                }
            }));
            sysRow.Children.Add(b3 = Pill(Lang.T("Devolver al escritorio"), delegate
            {
                Desktop.Restore(dock.Cfg);
                dock.Rebuild();
                b2.Content = Lang.T("Vaciar escritorio al dock");
            }));
            sysRow.Children.Add(b4 = Pill(AutostartLabel(cfg), delegate
            {
                cfg.Autostart = !cfg.Autostart;
                cfg.Save();
                if (Autostart.Set(cfg.Autostart)) b4.Content = AutostartLabel(cfg);
                else { cfg.Autostart = false; cfg.Save(); b4.Content = Lang.T("Arranca con Windows: no"); }
            }));
            sysRow.Children.Add(b5 = Pill(Lang.T("Abrir carpeta de ajustes"), delegate { Launcher.OpenFolder(Paths.Root); }));
            // pregunta a GitHub cual es la ultima version publicada y cuenta el resultado, bueno o malo:
            // un actualizador que se queda callado no se distingue de uno roto
            sysRow.Children.Add(b6 = Pill(Lang.T("Buscar actualizaciones"), delegate { BuscarActualizaciones(dock, b6); }));
            der.Children.Add(sysRow);

            // la estrella cierra la columna izquierda: es lo unico del panel que no es un ajuste, y puesta
            // arriba se confundiria con el boton de guardar de algo. Son dos lineas y una ficha: metiendo
            // ademas el aviso de los gestos en esta columna, la ficha se salia del alto de la pantalla.
            var guya = Res.Label(Lang.T("¿Te gusta la barra? Deja una estrella: es nuestra única señal de que la usas."), 10.5, "PanelSub");
            guya.TextWrapping = TextWrapping.Wrap;
            guya.Margin = new Thickness(0, 6, 0, 0);
            izq.Children.Add(guya);
            var filaEstrella = new WrapPanel { Margin = new Thickness(0, 1, 0, 0) };
            filaEstrella.Children.Add(Pill(Lang.T("Estrella en GitHub"), delegate { Launcher.OpenUrl(Update.PaginaRepo); }));
            izq.Children.Add(filaEstrella);

            // el pie va debajo de las dos columnas, a lo ancho del cristal y separado por un hilo: son las
            // lineas que explican los gestos, y en media columna ocupaban cuatro renglones
            var pie = new StackPanel();
            Grid.SetRow(pie, 1);
            Grid.SetColumnSpan(pie, 3);
            var hilo = new Border { Height = 1, Margin = new Thickness(0, 12, 0, 0) };
            hilo.SetResourceReference(Border.BackgroundProperty, "MenuSep");
            pie.Children.Add(hilo);
            var note = Res.Label(Lang.T("Arrastra un .lnk, .exe o un enlace steam:// sobre la barra para fijarlo. " +
                                 "Arrastra un icono para ordenar la barra; soltarlo fuera del cristal lo quita y vuelve con " +
                                 "\"Restaurar ocultos\" del clic derecho. Clic derecho en un icono: fijar, renombrar u ocultar. " +
                                 "Clic central: favorito."), 10.5, "PanelSub");
            note.TextWrapping = TextWrapping.Wrap;
            note.Margin = new Thickness(0, 9, 0, 0);
            pie.Children.Add(note);
            sp.Children.Add(pie);

            _open = w;
            w.Closed += delegate { _open = null; cfg.Save(); };
            // ShowDialog y no Show: el proceso del dock esta en segundo plano y Windows no le deja traer
            // una ventana modal-less al frente; el dialogo modal si (y el bucle anidado del dispatcher
            // no para las animaciones de la barra)
            par.ShowModal();
        }

        static string AutostartLabel(Config cfg)
        {
            return Lang.T(cfg.Autostart ? "Arranca con Windows: sí" : "Arranca con Windows: no");
        }

        // cambiar de idioma en caliente: los textos se resuelven al crearse el control, asi que lo que
        // ya esta construido (menu de la pildora, bandeja, tiles con su tooltip) se tira y se remonta.
        // El panel abierto se cierra y se reabre desde el despachador, no desde dentro del dialogo que
        // se estaba cerrando, para no anidar un modal dentro de otro.
        static void SetLanguage(Dock dock, Config cfg, string id)
        {
            cfg.Language = id;
            cfg.Save();
            Lang.Apply(id);
            dock.InvalidateMenus();
            dock.SyncSearchWatermark();
            if (dock.TrayIcon != null) dock.TrayIcon.RebuildMenu();
            dock.Refresh(false);
            var w = _open;
            _open = null;
            if (w == null) { Show(dock); return; }
            w.Dispatcher.BeginInvoke(new Action(delegate { Show(dock); }), DispatcherPriority.Background);
            try { w.Close(); } catch { }
        }

        static Button Pill(string text, Action onClick)
        {
            var b = new Button { Style = Res.Style("PillBtn"), Content = text, Margin = new Thickness(0, 0, 7, 7) };
            b.Click += delegate { onClick(); };
            return b;
        }

        // Cabecera de bloque: raya de acento, texto en mayusculas y un hilo que estira hasta el borde de
        // la columna. El texto paso a PanelFg: antes usaba el mismo gris que las etiquetas de los campos
        // y los dos niveles se leian como uno solo.
        static FrameworkElement SecHead(string txt)
        {
            var row = new Grid { Margin = new Thickness(0, 6, 0, 10) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var bar = new Border
            {
                Width = 2,
                Margin = new Thickness(0, 2, 7, 2),
                CornerRadius = new CornerRadius(1)
            };
            bar.SetResourceReference(Border.BackgroundProperty, "MenuAccent");

            var t = Res.Label(Lang.T(txt).ToUpperInvariant(), 10.5, "PanelFg");
            t.FontWeight = FontWeights.SemiBold;
            t.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(t, 1);

            var hilo = new Border
            {
                Height = 1,
                Margin = new Thickness(11, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            hilo.SetResourceReference(Border.BackgroundProperty, "MenuSep");
            Grid.SetColumn(hilo, 2);

            row.Children.Add(bar);
            row.Children.Add(t);
            row.Children.Add(hilo);
            return row;
        }

        static StackPanel Field(string label, UIElement ctrl, double gap)
        {
            var s = new StackPanel { Margin = new Thickness(0, 0, 0, gap) };
            if (!string.IsNullOrEmpty(label))
            {
                var t = Res.Label(label, 11, "PanelSub");
                t.Margin = new Thickness(0, 0, 0, 7);
                s.Children.Add(t);
            }
            s.Children.Add(ctrl);
            return s;
        }

        static RadioButton Chip(string txt, string group, bool on, Action pick)
        {
            var rb = new RadioButton
            {
                Style = Res.Style("ChipRadio"),
                Content = txt,
                GroupName = group,
                Margin = new Thickness(0, 0, 7, 7)
            };
            rb.IsChecked = on;                       // antes de enganchar el evento: si no, arranca pulsando
            rb.Checked += delegate { pick(); };
            return rb;
        }

        static CheckBox Switch(string txt, bool on, Action<bool> changed)
        {
            var cb = new CheckBox
            {
                Style = Res.Style("SwitchBox"),
                Content = txt,
                Margin = new Thickness(0, 0, 0, 10)
            };
            cb.IsChecked = on;
            RoutedEventHandler h = delegate { changed(cb.IsChecked == true); };
            cb.Checked += h;
            cb.Unchecked += h;
            return cb;
        }

        static RadioButton ThemeChip(string id, bool on, Dock dock)
        {
            var t = Themes.Get(id);
            var rb = new RadioButton
            {
                Style = Res.Style("ThemeChip"),
                GroupName = "tema",
                Tag = id,
                Margin = new Thickness(0, 0, 12, 14)
            };
            rb.IsChecked = on;
            // el chip va sin texto accesible (el nombre va debajo, en otro bloque): sin esto es un boton
            // vacio para un lector de pantalla y para cualquier prueba automatizada
            rb.SetValue(AutomationProperties.NameProperty, Lang.T(t.Label));

            var sp = new StackPanel();
            var sw = new Border
            {
                Width = 50,
                Height = 31,
                CornerRadius = new CornerRadius(9),
                Background = Themes.Grad(t.C["glass"], t.C["glassBot"]),
                BorderBrush = Themes.Br(t.C["edge"]),
                BorderThickness = new Thickness(1)
            };
            var dots = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            for (int i = 0; i < 3; i++)
            {
                double d = i == 1 ? 9 : 6;
                dots.Children.Add(new Ellipse
                {
                    Width = d,
                    Height = d,
                    Fill = Themes.Br(t.C["accent"]),
                    Margin = new Thickness(2, 0, 2, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });
            }
            sw.Child = dots;
            sp.Children.Add(sw);

            var lb = Res.Label(Lang.T(t.Label), 10, "PanelSub");
            lb.Margin = new Thickness(0, 4, 0, 0);
            lb.HorizontalAlignment = HorizontalAlignment.Center;
            sp.Children.Add(lb);

            rb.Content = sp;
            rb.Checked += delegate { dock.SetTheme(id); };
            return rb;
        }

        static UIElement Stepper(string id, int min, int max, int step, int value, string suffix, Action<int> onSet)
        {
            var wrap = new StackPanel();
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            var menos = new Button { Style = Res.Style("StepBtn"), Content = "" + (char)0x2212, Tag = id };
            var lbl = Res.Label(value + " " + suffix, 12, "PanelFg");
            lbl.Width = 60;
            lbl.TextAlignment = TextAlignment.Center;
            lbl.VerticalAlignment = VerticalAlignment.Center;
            lbl.Margin = new Thickness(2, 0, 2, 0);
            var mas = new Button { Style = Res.Style("StepBtn"), Content = "+", Tag = id };
            menos.Click += delegate { MoveStep(id, -1); };
            mas.Click += delegate { MoveStep(id, 1); };
            row.Children.Add(menos);
            row.Children.Add(lbl);
            row.Children.Add(mas);

            var track = new Border
            {
                Height = 3,
                Width = 130,
                CornerRadius = new CornerRadius(2),
                Margin = new Thickness(0, 5, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            track.SetResourceReference(Border.BackgroundProperty, "SwitchOff");
            var fill = new Border
            {
                Height = 3,
                CornerRadius = new CornerRadius(2),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            fill.SetResourceReference(Border.BackgroundProperty, "MenuAccent");
            double fr = max > min ? (double)(value - min) / (max - min) : 0.0;
            fill.Width = Math.Max(8.0, fr * 130.0);
            var grid = new Grid();
            grid.Children.Add(track);
            grid.Children.Add(fill);

            wrap.Children.Add(row);
            wrap.Children.Add(grid);
            Steppers[id] = new Stepper
            {
                Min = min, Max = max, StepValue = step, Value = value, Suffix = suffix,
                Label = lbl, Fill = fill, OnSet = onSet
            };
            return wrap;
        }

        static void MoveStep(string id, int dir)
        {
            Stepper s;
            if (!Steppers.TryGetValue(id, out s) || s == null) return;
            s.Value = Math.Max(s.Min, Math.Min(s.Max, s.Value + dir * s.StepValue));
            s.Label.Text = s.Value + " " + s.Suffix;
            double f = s.Max > s.Min ? (double)(s.Value - s.Min) / (s.Max - s.Min) : 0.0;
            s.Fill.Width = Math.Max(8.0, f * s.BarWidth);
            if (s.OnSet != null) s.OnSet(s.Value);
        }

        // ------------------------------------------------------------------ prompt de una linea
        // El unico cuadro de texto suelto fuera de Ajustes: sirve para renombrar un tile o un grupo. Lo
        // que cambian son los tres textos, que pasan por argumentos porque un parametro opcional ha de
        // ser constante en tiempo de compilacion.
        public static string ReadPrompt(Dock dock, string titulo, string aviso, string etiqueta, string actual)
        {
            var par = new GlassPanel(titulo, 400, aviso);
            var w = par.Win;
            _promptValue = null;

            var etq = Res.Label(etiqueta, 11, "PanelSub");
            etq.Margin = new Thickness(0, 0, 0, 6);
            par.Body.Children.Add(etq);

            var box = new TextBox { Style = Res.Style("GlassText"), Text = actual, Margin = new Thickness(0, 0, 0, 14) };
            par.Body.Children.Add(box);

            var ok = new Button
            {
                Style = Res.Style("PillBtn"),
                Content = Lang.T("Guardar"),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            ok.Click += delegate { _promptValue = box.Text; try { Window.GetWindow(ok).Close(); } catch { } };
            par.Body.Children.Add(ok);

            _promptBox = box;
            box.KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter)
                {
                    _promptValue = box.Text;
                    try { Window.GetWindow(box).Close(); } catch { }
                }
            };
            box.SelectAll();
            // el foco solo se puede pedir con la ventana ya creada
            w.ContentRendered += delegate { box.Focus(); box.SelectAll(); };
            par.ShowModal();
            return _promptValue;
        }

        // renombrar un tile y renombrar un grupo son el mismo cuadro con los mismos tres textos
        public static string PromptNombre(Dock dock, string actual)
        {
            return ReadPrompt(dock, Lang.T("Nombre en el dock"),
                Lang.T("Enter para guardar - Esc para cancelar"), Lang.T("Nombre visible"), actual);
        }

        // ------------------------------------------------------------------ aviso de cristal
        // Un aviso del programa es un panel del propio programa, no un cuadro de Windows. El MessageBox
        // nativo se atiende con su propio bucle modal -que deja la barra sin moverse hasta que alguien lo
        // cierra- y debajo del velo casi no se ve. El panel de cristal usa el mismo ShowDialog que
        // Ajustes: se ve por encima del velo, se cierra con Enter, Esc, la cruz o el boton, y la barra
        // detras sigue animando.
        public static void Aviso(string texto)
        {
            Aviso(texto, null, null);
        }

        // El boton opcional evita el callejon sin salida: el aviso de "falta la clave" abre Ajustes al
        // pulsarlo. La accion se lanza DESPUES de que ShowDialog haya vuelto, con el velo ya quitado, para
        // no anidar un modal dentro del bucle modal del aviso.
        public static void Aviso(string texto, string boton, Action accion)
        {
            bool pedido = false;
            var par = new GlassPanel(Lang.T("Aviso de GamesDock"), 430, Lang.T("Enter o Esc para cerrar"));
            var w = par.Win;

            var cuerpo = Res.Label(texto, 12.5, "PanelFg");
            cuerpo.TextWrapping = TextWrapping.Wrap;
            cuerpo.Margin = new Thickness(0, 0, 0, 16);
            par.Body.Children.Add(cuerpo);

            var fila = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            fila.Children.Add(Pill(Lang.T("Vale"), delegate { Cerrar(w); }));
            if (!string.IsNullOrEmpty(boton))
                fila.Children.Add(Pill(boton, delegate { pedido = true; Cerrar(w); }));
            par.Body.Children.Add(fila);

            // Esc ya lo cierra el panel; Enter es lo que se espera de un aviso de un boton
            w.KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter) { e.Handled = true; Cerrar(w); }
            };

            par.ShowModal();
            if (pedido && accion != null) { try { accion(); } catch (Exception ex) { Log.Write("aviso: " + ex.Message); } }
        }

        static void Cerrar(Window w)
        {
            try { w.Close(); } catch { }
        }
    }

    // Vaciar el escritorio dentro de una carpeta del dock y devolverlo: los accesos siguen vivos, solo
    // cambian de sitio, y el mapa "desktopMoved" del config sabe a donde volver.
    public static class Desktop
    {
        static readonly string[] Ext = { ".lnk", ".exe", ".bat", ".cmd", ".url", ".appref-ms" };

        public static bool Import(Config cfg)
        {
            var r = Veil.Ask(null, Lang.F("Los accesos directos del escritorio se moverán a\n{0}\nSe mantienen intactos y puedes devolverlos con un botón.\n\nContinuar?", Paths.DeskDir),
                             Lang.T("Vaciar escritorio"), MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r != MessageBoxResult.Yes) return false;
            int movidos = 0;
            foreach (var d in Catalog.DesktopDirs())
            {
                if (d == Paths.DeskDir || !Directory.Exists(d)) continue;
                foreach (var f in Directory.GetFiles(d))
                {
                    string ext = System.IO.Path.GetExtension(f);
                    bool ok = false;
                    foreach (var e in Ext) if (e.Equals(ext, StringComparison.OrdinalIgnoreCase)) ok = true;
                    if (!ok) continue;
                    string dest = System.IO.Path.Combine(Paths.DeskDir, System.IO.Path.GetFileName(f));
                    int n = 1;
                    while (System.IO.File.Exists(dest))
                    {
                        dest = System.IO.Path.Combine(Paths.DeskDir,
                            System.IO.Path.GetFileNameWithoutExtension(f) + " (" + n + ")" + ext);
                        n++;
                    }
                    try
                    {
                        System.IO.File.Move(f, dest);
                        cfg.DesktopMoved[f.ToLowerInvariant()] = dest;
                        movidos++;
                    }
                    catch (Exception ex) { Log.Write("no movido " + System.IO.Path.GetFileName(f) + ": " + ex.Message); }
                }
            }
            cfg.Save();
            Log.Write("Escritorio vaciado: " + movidos + " archivos");
            return true;
        }

        public static void Restore(Config cfg)
        {
            int devueltos = 0;
            foreach (var k in new List<string>(cfg.DesktopMoved.Keys))
            {
                string from;
                if (!cfg.DesktopMoved.TryGetValue(k, out from) || !System.IO.File.Exists(from))
                {
                    cfg.DesktopMoved.Remove(k);
                    continue;
                }
                try
                {
                    string destDir = Catalog.DirOf(k);
                    if (!Directory.Exists(destDir)) throw new Exception(Lang.T("El escritorio original ya no existe."));
                    System.IO.File.Move(from, k);
                    cfg.DesktopMoved.Remove(k);
                    devueltos++;
                }
                catch (Exception ex) { Log.Write("no devuelto " + k + " : " + ex.Message); }
            }
            cfg.Save();
            Log.Write("Escritorio restaurado: " + devueltos + " archivos");
        }
    }
}
