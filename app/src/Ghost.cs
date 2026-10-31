using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace GamesDock
{
    // Icono flotante del arrastre. La pildora vive al nivel del escritorio y no mide mas de lo que
    // mide: un icono que se saca del dock no puede pintarse dentro de su ventana, se cortaria contra
    // su borde. Se suelta en una ventanita propia, topmost, sin activacion y click-through, que va
    // pegada al cursor cuadro a cuadro.
    public class Ghost
    {
        public const int Pad = 34;

        readonly Window _win;
        readonly Border _box;

        public Window Win { get { return _win; } }

        public Ghost(int iconSize)
        {
            _win = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                ShowActivated = false,
                Topmost = true,
                Title = "GamesDockGhost",
                Background = Brushes.Transparent,
                SizeToContent = SizeToContent.Manual,
                Width = iconSize + 2 * Pad,
                Height = iconSize + 2 * Pad,
                WindowStartupLocation = WindowStartupLocation.Manual,
                IsHitTestVisible = false,
                Opacity = 0.0,
                // fuera del alcance hasta el primer arrastre: la ventana ya esta montada y el icono
                // aparece pegado al cursor en el mismo cuadro en que se levanta
                Left = -32000.0,
                Top = -32000.0
            };

            _box = new Border
            {
                Width = iconSize,
                Height = iconSize,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                ClipToBounds = true,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new ScaleTransform()
            };
            // Fant: el mismo filtro por hardware que el tile, si no el icono flotante se ve borroso
            // justo cuando es lo unico que mira el usuario
            RenderOptions.SetBitmapScalingMode(_box, BitmapScalingMode.Fant);
            _box.Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(2, 4, 9),
                BlurRadius = 26,
                ShadowDepth = 10,
                Opacity = 0.6,
                Direction = 270
            };

            var shell = new Grid { Margin = new Thickness(Pad) };
            shell.Children.Add(_box);
            _win.Content = shell;

            _win.SourceInitialized += delegate
            {
                try
                {
                    IntPtr h = new WindowInteropHelper(_win).Handle;
                    int ex = Native.GetWindowLong(h, -20);
                    // WS_EX_TRANSPARENT (no se come ni un click) | WS_EX_TOOLWINDOW (ni alt-tab) |
                    // WS_EX_NOACTIVATE (robar la activacion al arrastrar cortaria lo que hubiera debajo)
                    Native.SetWindowLong(h, -20, ex | 0x20 | 0x80 | unchecked((int)0x08000000));
                }
                catch (Exception ex2) { Log.Write("estilo del fantasma: " + ex2.Message); }
            };

            // se muestra una sola vez para que la HWND y el primer render queden pagados aqui, y se
            // vuelve a esconder: CompositionTarget.Rendering es un evento ESTATICO que dispara una vez
            // por target de composicion, asi que una segunda ventana visible (aunque este fuera de
            // pantalla y a opacidad 0) duplica los cuadros de la animacion y el trabajo del integrador
            _win.Show();
            _win.Hide();
        }

        public void Resize(int iconSize)
        {
            if ((int)_box.Width == iconSize) return;
            _box.Width = iconSize;
            _box.Height = iconSize;
            _win.Width = iconSize + 2 * Pad;
            _win.Height = iconSize + 2 * Pad;
        }

        // PointToScreen devuelve pixeles fisicos y Window.Left/Top quieren DIP
        public void MoveTo(Window owner, Point pInGrid, double margin, double dip)
        {
            var sp = owner.PointToScreen(new Point(pInGrid.X + margin, pInGrid.Y + margin));
            _win.Left = (sp.X / dip) - _win.Width / 2.0;
            _win.Top = (sp.Y / dip) - _win.Height / 2.0;
        }

        public Border Box { get { return _box; } }

        public void Park()
        {
            _win.Opacity = 0.0;
            _win.Left = -32000.0;
            _win.Top = -32000.0;
            // ocultar, no cerrar: deja de participar en el paso de composicion y la ventana sigue
            // viva para el siguiente arrastre
            if (_win.IsVisible) _win.Hide();
        }
    }
}
