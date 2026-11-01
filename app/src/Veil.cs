using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace GamesDock
{
    // Velo a pantalla completa mientras hay un emergente del programa abierto (ajustes, renombrar,
    // avisos, elegir icono). La pildora vive al nivel del escritorio y sus dialogos son ventanas
    // normales, asi que sin el velo se puede seguir pinchando fuera -o cambiar de ventana- mientras
    // el programa espera una respuesta. El velo se come todos los clics de fuera y se quita solo,
    // cuando el emergente se cierra (Guardar, Enter, Esc o la cruz).
    static class Veil
    {
        const int GWL_EXSTYLE = -20;
        const int WS_EX_NOACTIVATE = unchecked((int)0x08000000);
        const int WS_EX_TOOLWINDOW = 0x00000080;
        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOACTIVATE = 0x0010;

        // las unicas ventanas nuestras que NUNCA tienen que quedar por encima del velo: la propia
        // pildora, el icono que sigue al cursor al arrastrar y el velo mismo
        const string TituloFantasma = "GamesDockGhost";
        const string TituloVelo = "GamesDockVeil";

        // La pildora se reconoce por su handle, nunca por su titulo: un aviso puede llevar el mismo
        // titulo en la barra (el de la propia app) y entonces el velo lo dejaba debajo, con el hilo de
        // UI parado dentro de un cuadro modal que nadie podia cerrar.
        public static IntPtr Pildora;

        static readonly uint MiPid = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;

        static Window _win;
        static IntPtr _hwnd;
        static int _depth;
        static DispatcherTimer _remonte;
        static int _intentos;
        static readonly List<IntPtr> Subidas = new List<IntPtr>();

        // negro al 22 %: lo justo para notar que la barra esta en pausa sin perder de vista el fondo
        static readonly SolidColorBrush Oscuro = new SolidColorBrush(Color.FromArgb(0x38, 0, 0, 0));

        // hay un emergente esperando respuesta: lo mira el contenedor de un grupo antes de cerrarse el
        // solo, porque al abrir el cuadro de renombrar la ventana del contenedor pierde la activacion
        public static bool HayVelo { get { return _depth > 0; } }

        public static void Push()
        {
            _depth++;
            try
            {
                if (_win == null) Crear();
                Tapizar();
                if (_win.Visibility != Visibility.Visible) _win.Show();
                // los dialogos del sistema (el de archivo, un aviso) nacen DESPUES de este Show y caen
                // encima solos; como no sabemos cuando existiran se intenta unas cuantas veces. Cada
                // clic en el velo lo comprueba otra vez, por si acaso.
                _intentos = 0;
                if (_remonte == null)
                {
                    _remonte = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
                    _remonte.Tick += delegate
                    {
                        Remontar();
                        if (++_intentos >= 8) _remonte.Stop();
                    };
                }
                _remonte.Stop();
                _remonte.Start();
            }
            catch (Exception ex) { Log.Write("velo: " + ex.Message); }
        }

        public static void Pop()
        {
            if (_depth > 0) _depth--;
            if (_depth > 0) return;
            try
            {
                if (_remonte != null) _remonte.Stop();
                if (_win != null && _win.Visibility == Visibility.Visible) _win.Hide();
                // lo que subimos a la fuerza vuelve a su banda normal: si no, el panel de ajustes
                // se quedaria por encima de todo el rato que este abierto
                foreach (var h in Subidas)
                {
                    try { Native.SetWindowPos(h, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE); }
                    catch { }
                }
                Subidas.Clear();
            }
            catch (Exception ex) { Log.Write("velo al quitar: " + ex.Message); }
        }

        // ejecuta la accion (un ShowDialog o un MessageBox, que bloquean) con el velo puesto
        public static void Around(Action mostrar)
        {
            Push();
            try { mostrar(); }
            finally { Pop(); }
        }

        // Los avisos de texto ya no pasan por aqui: viven en Settings.Notice, que es un panel del propio
        // programa. Queda el cuadro de si/no, que es la unica vez que se le pide a Windows una decision.
        public static MessageBoxResult Ask(Window owner, string text, string cap, MessageBoxButton btn, MessageBoxImage icono)
        {
            MessageBoxResult r = MessageBoxResult.None;
            Around(delegate { r = MessageBox.Show(owner, text, cap, btn, icono); });
            return r;
        }

        static void Crear()
        {
            Oscuro.Freeze();
            _win = new Window
            {
                Title = TituloVelo,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                ShowActivated = false,
                Focusable = false,
                IsTabStop = false,
                Topmost = true,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Background = Oscuro
            };
            _win.SourceInitialized += delegate
            {
                _hwnd = new WindowInteropHelper(_win).Handle;
                try
                {
                    // sin activacion: si el velo robara el foco, el cuadro de texto del renombrado
                    // dejaria de escribir a media frase
                    Native.SetWindowLong(_hwnd, GWL_EXSTYLE,
                        Native.GetWindowLong(_hwnd, GWL_EXSTYLE) | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
                }
                catch (Exception ex) { Log.Write("velo estilo: " + ex.Message); }
            };
            // pinchar el velo no hace nada: ni pasa el clic a lo de atras ni cierra el emergente. Si
            // aun asi el dialogo se quedo debajo, aqui es donde se vuelve a subir.
            _win.PreviewMouseDown += (s, e) => { e.Handled = true; Remontar(); };
            _win.PreviewMouseUp += (s, e) => { e.Handled = true; };
            _win.PreviewMouseWheel += (s, e) => { e.Handled = true; };
        }

        // cubre todo el escritorio virtual, con las varias pantallas y la barra de tareas dentro
        static void Tapizar()
        {
            _win.Left = SystemParameters.VirtualScreenLeft;
            _win.Top = SystemParameters.VirtualScreenTop;
            _win.Width = Math.Max(200, SystemParameters.VirtualScreenWidth);
            _win.Height = Math.Max(200, SystemParameters.VirtualScreenHeight);
        }

        static readonly StringBuilder Buf = new StringBuilder(256);

        static void Remontar()
        {
            try
            {
                Native.EnumWindows((h, lp) =>
                {
                    if (h == _hwnd || !Native.IsWindowVisible(h)) return true;
                    if (Pildora != IntPtr.Zero && h == Pildora) return true;
                    uint pid;
                    Native.GetWindowThreadProcessId(h, out pid);
                    if (pid != MiPid) return true;
                    Buf.Length = 0;
                    int n = Native.GetWindowText(h, Buf, Buf.Capacity);
                    if (n > 0)
                    {
                        string t = Buf.ToString();
                        if (t == TituloFantasma || t == TituloVelo) return true;
                    }
                    if (!Subidas.Contains(h)) Subidas.Add(h);
                    Native.SetWindowPos(h, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
                    return true;
                }, IntPtr.Zero);
            }
            catch (Exception ex) { Log.Write("velo remontar: " + ex.Message); }
        }
    }
}
