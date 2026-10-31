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

        public GlassPanel(string titleTxt, double width,
                          string subTxt = null)
        {
            // el valor por defecto no puede ser la cadena traducida: un parametro opcional ha de ser
            // constante en tiempo de compilacion
            if (subTxt == null) subTxt = Lang.T("clic derecho en la barra vacia - Esc para cerrar");
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
                ShowActivated = true
            };
            Win.KeyUp += (s, e) => { if (e.Key == System.Windows.Input.Key.Escape) try { Win.Close(); } catch { } };

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
                MaxHeight = 640,
                Content = Body
            };
            shell.Child = scroll;
            Win.Content = shell;
        }

        static void SetColumn(UIElement e, int c) { Grid.SetColumn(e, c); }

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

    public static class Settings
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

            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            var izq = new StackPanel { Width = 340 };
            var der = new StackPanel { Margin = new Thickness(22, 0, 0, 0), Width = 340 };
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
                    delegate { cfg.Position = id; cfg.Save(); dock.ApplyLayout(); }));
            }
            izq.Children.Add(Field(Lang.T("Posicion de la barra"), posRow, 13));

            izq.Children.Add(Field(Lang.T("Tamano del icono"),
                Stepper("iconSize", 28, 76, 2, cfg.IconSize, "px", v => { cfg.IconSize = v; cfg.Save(); dock.ApplyLayout(); dock.Refresh(false); }), 13));

            izq.Children.Add(Field(Lang.T("Holgura alto/ancho"),
                Stepper("pillPad", 2, 26, 2, cfg.PillPad, "px", v => { cfg.PillPad = v; cfg.Save(); dock.ApplyLayout(); }), 13));

            izq.Children.Add(Field(Lang.T("Opacidad en reposo"),
                Stepper("idleOpacity", 10, 100, 5, cfg.IdleOpacity, "%", v => { cfg.IdleOpacity = v; cfg.Save(); dock.SetWindowOpacity(v / 100.0, 120); }), 13));

            var temRow = new WrapPanel();
            foreach (var kv in Themes.All) temRow.Children.Add(ThemeChip(kv.Key, kv.Key == cfg.Theme, dock));
            izq.Children.Add(Field(Lang.T("Tema de la barra"), temRow, 4));

            // cada idioma se escribe en su propio idioma: es la convencion de los selectores y evita
            // que "Espanol" aparezca como "Spanish" cuando ya estas en ingles
            var langRow = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            foreach (var l in new[] { new[] { "auto", "Auto" }, new[] { "es", "Espanol" }, new[] { "en", "English" } })
            {
                string id = l[0];
                langRow.Children.Add(Chip(l[1], "idioma", id == (cfg.Language ?? "auto"),
                    delegate { SetLanguage(dock, cfg, id); }));
            }
            izq.Children.Add(Field(Lang.T("Idioma de la interfaz"), langRow, 4));

            // --------------------------------------------------------- columna derecha
            der.Children.Add(SecHead("Que se ve"));
            var secRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 15) };
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

            der.Children.Add(SecHead("Comportamiento"));
            der.Children.Add(Switch(Lang.T("Agrandar los iconos al pasar el raton (efecto Mac)"), cfg.Magnify,
                on => { cfg.Magnify = on; cfg.Save(); dock.ResetProximity(); dock.ApplyLayout(); }));
            der.Children.Add(Switch(Lang.T("Vivir solo en el escritorio (nunca por encima de otras ventanas)"), cfg.DesktopLevel,
                on => { cfg.DesktopLevel = on; cfg.Save(); dock.AttachToDesktop(); dock.ApplyLayout(); }));
            der.Children.Add(Switch(Lang.T("Minimizar todo al lanzar"), cfg.MinimizeAll,
                on => { cfg.MinimizeAll = on; cfg.Save(); }));
            der.Children.Add(Switch(Lang.T("Ocultar el dock al lanzar (Ctrl+Alt+D lo trae)"), cfg.HideOnLaunch,
                on => { cfg.HideOnLaunch = on; cfg.Save(); }));

            der.Children.Add(SecHead("Sistema"));
            var sysRow = new WrapPanel();
            Button b1 = null, b2 = null, b3 = null, b4 = null, b5 = null;
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
            der.Children.Add(sysRow);

            var note = Res.Label(Lang.T("Arrastra un .lnk, .exe o un enlace steam:// sobre la barra para fijarlo. " +
                                 "Arrastra un icono para ordenar la barra; soltarlo fuera del cristal lo quita y vuelve con " +
                                 "\"Restaurar ocultos\" del clic derecho. Clic derecho en un icono: fijar, renombrar u ocultar. " +
                                 "Clic central: favorito."), 10.5, "PanelSub");
            note.TextWrapping = TextWrapping.Wrap;
            note.Margin = new Thickness(0, 6, 0, 0);
            der.Children.Add(note);

            _open = w;
            w.Closed += delegate { _open = null; cfg.Save(); };
            par.PlayIn();
            // ShowDialog y no Show: el proceso del dock esta en segundo plano y Windows no le deja traer
            // una ventana modal-less al frente; el dialogo modal si (y el bucle anidado del dispatcher
            // no para las animaciones de la barra)
            w.ShowDialog();
        }

        static string AutostartLabel(Config cfg)
        {
            return Lang.T(cfg.Autostart ? "Arranca con Windows: si" : "Arranca con Windows: no");
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

        static StackPanel SecHead(string txt)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 11) };
            var bar = new Border
            {
                Width = 3,
                Margin = new Thickness(0, 2, 7, 1),
                CornerRadius = new CornerRadius(2)
            };
            bar.SetResourceReference(Border.BackgroundProperty, "MenuAccent");
            var t = Res.Label(Lang.T(txt).ToUpperInvariant(), 10.5, "PanelSub");
            t.FontWeight = FontWeights.SemiBold;
            row.Children.Add(bar);
            row.Children.Add(t);
            return row;
        }

        static StackPanel Field(string label, UIElement ctrl, double gap)
        {
            var s = new StackPanel { Margin = new Thickness(0, 0, 0, gap) };
            if (!string.IsNullOrEmpty(label))
            {
                var t = Res.Label(label, 11, "PanelSub");
                t.Margin = new Thickness(0, 0, 0, 6);
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
                Margin = new Thickness(0, 0, 6, 6)
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
                Margin = new Thickness(0, 0, 0, 9)
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
                Margin = new Thickness(0, 0, 11, 13)
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

        // ------------------------------------------------------------------ prompt de nombre
        public static string ReadNamePrompt(Dock dock, string current)
        {
            var par = new GlassPanel(Lang.T("Nombre en el dock"), 400, Lang.T("Enter para guardar - Esc para cancelar"));
            var w = par.Win;
            _promptValue = null;

            var etq = Res.Label(Lang.T("Nombre visible"), 11, "PanelSub");
            etq.Margin = new Thickness(0, 0, 0, 6);
            par.Body.Children.Add(etq);

            var box = new TextBox { Style = Res.Style("GlassText"), Text = current, Margin = new Thickness(0, 0, 0, 14) };
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
            par.PlayIn();
            w.ShowDialog();
            return _promptValue;
        }
    }

    // Vaciar el escritorio dentro de una carpeta del dock y devolverlo: los accesos siguen vivos, solo
    // cambian de sitio, y el mapa "desktopMoved" del config sabe a donde volver.
    public static class Desktop
    {
        static readonly string[] Ext = { ".lnk", ".exe", ".bat", ".cmd", ".url", ".appref-ms" };

        public static bool Import(Config cfg)
        {
            var r = MessageBox.Show(Lang.F("Los accesos directos del escritorio se moveran a\n{0}\nSe mantienen intactos y puedes devolverlos con un boton.\n\nContinuar?", Paths.DeskDir),
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
