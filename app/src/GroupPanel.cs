using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace GamesDock
{
    // La carpeta abierta. Es el unico sitio donde el grupo tiene titulo: en la barra es un mosaico, y
    // al pincharlo salen los iconos de dentro con su nombre debajo. No es modal -pinchar fuera la
    // cierra, como en iOS- pero si sube la barra al nivel de las ventanas mientras dura, porque la
    // pildora vive pegada al escritorio y el panel iria por delante de ella.
    public class GroupPanel
    {
        static GroupPanel _abierto;

        readonly Dock _dock;
        readonly GlassPanel _par;
        readonly UniformGrid _rejilla;
        readonly ItemSpec _grupo;
        readonly Button _origen;
        readonly double _lado;
        DispatcherTimer _porCerrar;
        bool _activa = true;

        public static void Show(Dock dock, ItemSpec g, Button origen)
        {
            if (dock == null || g == null) return;
            Cerrar();
            var p = new GroupPanel(dock, g, origen);
            _abierto = p;
            p.Abrir();
        }

        public static void Cerrar()
        {
            var p = _abierto;
            _abierto = null;
            if (p == null) return;
            try { p._par.Win.Close(); } catch { }
        }

        // la barra manda sobre el contenido del grupo: si se ha renamed, desagrado o vaciado mientras
        // el contenedor estaba abierto, aqui se entera. Recargar cierra solo cuando ya no hay carpeta.
        public static void RefrescarAbierto()
        {
            var p = _abierto;
            if (p != null) p.Recargar();
        }

        GroupPanel(Dock dock, ItemSpec g, Button origen)
        {
            _dock = dock;
            _grupo = g;
            _origen = origen;
            _lado = dock.Cfg.IconSize + 8;

            int n = Math.Max(1, dock.GroupMembers(g).Count);
            int cols = n <= 1 ? 1 : Math.Min(4, (int)Math.Ceiling(Math.Sqrt(n)));
            double ancho = 68 + cols * (_lado + 22);

            var par = new GlassPanel(dock.GroupTitle(g), ancho, Lang.T("clic en un icono para lanzarlo"));
            par.Win.WindowStartupLocation = WindowStartupLocation.Manual;
            _par = par;
            _rejilla = new UniformGrid { Columns = cols, Margin = new Thickness(0, 2, 0, 4) };
            par.Body.Children.Add(_rejilla);

            par.Win.Closed += delegate
            {
                if (ReferenceEquals(_abierto, this)) _abierto = null;
                if (_porCerrar != null) _porCerrar.Stop();
                _dock.AttachToDesktop();
            };
            // se cierra al pinchar fuera, pero no cuando lo que ha pasado es que se ha abierto su menu
            // o el cuadro de renombrar (que se lleva la activacion y el velo)
            par.Win.Activated += delegate { _activa = true; if (_porCerrar != null) _porCerrar.Stop(); };
            par.Win.Deactivated += delegate
            {
                _activa = false;
                if (_porCerrar == null)
                {
                    _porCerrar = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(240) };
                    _porCerrar.Tick += delegate
                    {
                        _porCerrar.Stop();
                        if (_activa || MenuGuard.Abierto || Veil.HayVelo) return;
                        Cerrar();
                    };
                }
                _porCerrar.Stop();
                _porCerrar.Start();
            };
            Rellenar();
        }

        void Abrir()
        {
            _dock.SetDesktopLevel(false);
            _par.PlayIn();
            _par.Win.ContentRendered += delegate { Colocar(); };
            Colocar();
            _par.Win.Show();
            PedirPrimerPlano();
        }

        // el clic que abre la carpeta recae en la pildora, que nunca se activa, asi que Windows no le
        // suelta el primer plano al panel y el Esc se quedaba en la ventana de atras. Aqui se pide al
        // abrir; si al usuario no le interesa el teclado, pinchar fuera lo cierra igual.
        void PedirPrimerPlano()
        {
            try
            {
                _par.Win.Activate();
                if (!_par.Win.IsActive)
                {
                    var hwnd = new WindowInteropHelper(_par.Win).EnsureHandle();
                    if (hwnd != IntPtr.Zero) Native.SetForegroundWindow(hwnd);
                }
                _par.Win.Focus();
            }
            catch { }
        }

        // el panel aparece pegado al lado de la pildora que da al centro del escritorio, centrado en el
        // tile del grupo
        void Colocar()
        {
            var w = _par.Win;
            double alto = w.ActualHeight > 2 ? w.ActualHeight : 260;
            double ancho = w.Width;
            double left = w.Left, top = w.Top;
            if (_origen != null && _origen.IsVisible)
            {
                double dip = Escala(w);
                Point a = _origen.PointToScreen(new Point(0, 0));
                Point b = _origen.PointToScreen(new Point(_origen.ActualWidth, _origen.ActualHeight));
                double i = a.X / dip, d = a.Y / dip, c = b.X / dip, p = b.Y / dip;
                switch (_dock.Cfg.Position)
                {
                    case "top": left = (i + c) / 2 - ancho / 2; top = d + 12; break;
                    case "left": left = c + 12; top = (d + p) / 2 - alto / 2; break;
                    case "right": left = i - 12 - ancho; top = (d + p) / 2 - alto / 2; break;
                    default: left = (i + c) / 2 - ancho / 2; top = d - 12 - alto; break;
                }
            }
            var wa = SystemParameters.WorkArea;
            w.Left = Math.Max(wa.Left + 6, Math.Min(left, wa.Right - ancho - 6));
            w.Top = Math.Max(wa.Top + 6, Math.Min(top, wa.Bottom - alto - 6));
        }

        static double Escala(Visual v)
        {
            try
            {
                var ps = PresentationSource.FromVisual(v);
                if (ps != null) return ps.CompositionTarget.TransformToDevice.M11;
            }
            catch { }
            return 1.0;
        }

        void Rellenar()
        {
            _rejilla.Children.Clear();
            foreach (var m in _dock.GroupMembers(_grupo)) _rejilla.Children.Add(Celda(m));
        }

        void Recargar()
        {
            // el config manda: si el grupo se ha disuelto o vaciado mientras el panel estaba abierto, se
            // cierra solo en vez de quedarse enseñando una carpeta que ya no existe
            var g = _dock.GroupById(_grupo.GroupId);
            if (g == null || g.Members.Count == 0) { Cerrar(); return; }
            _par.Tit.Text = _dock.GroupTitle(_grupo);
            Rellenar();
        }

        UIElement Celda(ItemSpec m)
        {
            var bd = new Border
            {
                Width = _lado,
                Height = _lado,
                CornerRadius = new CornerRadius(_lado * 0.22),
                ClipToBounds = true
            };
            var br = _dock.MemberBrush(m);
            if (br != null) bd.Background = br;
            else
            {
                bd.SetResourceReference(Border.BackgroundProperty, "ChipOn");
                bd.Child = _dock.MemberLetra(m, _lado);
            }

            var btn = new Button
            {
                Style = Res.Style("GhostBtn"),
                Content = bd,
                Width = _lado + 18,
                Height = _lado + 18,
                HorizontalAlignment = HorizontalAlignment.Center,
                ToolTip = m.Name
            };
            RenderOptions.SetBitmapScalingMode(bd, BitmapScalingMode.Fant);
            btn.Click += delegate
            {
                var vivo = _dock.Cat != null ? _dock.Cat.Find(m.Key) : null;
                Cerrar();
                _dock.StartEntry(vivo ?? m, "grupo");
            };
            btn.MouseRightButtonUp += (s, e) => { MenuMiembro(m, btn); e.Handled = true; };

            var t = Res.Label(m.Name, 11, "PanelFg");
            t.TextTrimming = TextTrimming.CharacterEllipsis;
            t.TextAlignment = TextAlignment.Center;
            t.MaxWidth = _lado + 26;
            t.Margin = new Thickness(0, 1, 0, 0);

            var sp = new StackPanel { Margin = new Thickness(2, 5, 2, 7) };
            sp.Children.Add(btn);
            sp.Children.Add(t);
            return sp;
        }

        void MenuMiembro(ItemSpec m, Button owner)
        {
            var cm = new ContextMenu { PlacementTarget = owner, Placement = PlacementMode.Bottom };
            cm.Items.Add(Fila(Lang.T("Lanzar ahora"), delegate
            {
                Cerrar();
                _dock.StartEntry(m, "grupo menu");
            }));
            cm.Items.Add(Fila(Lang.T("Renombrar"), delegate { _dock.Rename(m); Recargar(); }));
            cm.Items.Add(new Separator());
            cm.Items.Add(Fila(Lang.T("Quitar del grupo"), delegate { _dock.RemoveFromGroup(m.Key); Recargar(); }));
            MenuGuard.Watch(cm);
            cm.IsOpen = true;
        }

        static MenuItem Fila(string header, Action accion)
        {
            var mi = new MenuItem { Header = header };
            mi.Click += delegate { accion(); };
            return mi;
        }
    }

    public partial class Dock
    {
        public void OpenGroup(ItemSpec it, Button origen) { GroupPanel.Show(this, it, origen); }
    }
}
