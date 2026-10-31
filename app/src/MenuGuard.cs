using System;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Threading;

namespace GamesDock
{
    // WPF cierra un ContextMenu cuando la ventana que lo hospeda pierde la activacion. La pildora es
    // WS_EX_NOACTIVATE y nunca llega a tenerla, asi que el desplegable se queda colgado hasta que se
    // vuelve a pinchar la barra. Este guard mira el raton con un hook global mientras hay un menu
    // abierto y lo cierra al primer clic fuera de el, igual que hace Windows con sus menus: el clic se
    // retira para que la ventana de atras no reciba una accion que nadie pidio. El hook vive solo lo
    // que dura el menu, asi que en reposo no cuesta ni un mensaje.
    static class MenuGuard
    {
        const int WH_MOUSE_LL = 14;
        const int GWL_EXSTYLE = -20;
        const int WS_EX_TOOLWINDOW = 0x00000080;
        const int WS_EX_TRANSPARENT = 0x00000020;

        const int WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202;
        const int WM_RBUTTONDOWN = 0x0204, WM_RBUTTONUP = 0x0205;
        const int WM_MBUTTONDOWN = 0x0207, WM_MBUTTONUP = 0x0208;

        static readonly uint MiPid = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;

        // el delegate tiene que vivir en estatico: si lo recoge el GC el hook queda apuntando a la
        // nada y el siguiente movimiento del raton tumba el proceso
        static Native.LowLevelMouseProc _proc;
        static IntPtr _hook;
        static ContextMenu _menu;
        static DispatcherTimer _retraer;

        // mensaje de la suelta que queda por comerse (la que acompana al clic que cerramos el menu),
        // 0 si no queda ninguna. Se guarda cual es porque si no un clic derecho justo despues de
        // cerrar perderia tambien su suelta y el menu no llegaria a abrirse.
        static int _pendiente;

        public static void Watch(ContextMenu menu)
        {
            menu.Opened += delegate { Instalar(menu); };
            menu.Closed += delegate { Apartar(); };
        }

        static void Instalar(ContextMenu menu)
        {
            _menu = menu;
            if (_retraer != null) _retraer.Stop();
            if (_hook != IntPtr.Zero) return;
            if (_proc == null) _proc = new Native.LowLevelMouseProc(Callback);
            _hook = Native.SetWindowsHookEx(WH_MOUSE_LL, _proc, Native.GetModuleHandle(null), 0);
        }

        static void Apartar()
        {
            _menu = null;
            if (_hook == IntPtr.Zero) return;
            if (_pendiente == 0) { Quitar(); return; }
            // al comernos la pulsacion, el sistema ni siquiera llega a entregar su suelta: la espera
            // tiene que caducar sola, si no el hook se quedaria instalado de por vida
            if (_retraer == null)
            {
                _retraer = new DispatcherTimer();
                _retraer.Interval = TimeSpan.FromMilliseconds(300);
                _retraer.Tick += delegate { _retraer.Stop(); Quitar(); };
            }
            _retraer.Stop();
            _retraer.Start();
        }

        static void Quitar()
        {
            if (_retraer != null) _retraer.Stop();
            _pendiente = 0;
            if (_hook == IntPtr.Zero) return;
            Native.UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }

        static IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode < 0) return Native.CallNextHookEx(_hook, nCode, wParam, lParam);

            int msg = (int)(long)wParam;
            bool suelta = msg == WM_LBUTTONUP || msg == WM_RBUTTONUP || msg == WM_MBUTTONUP;
            bool pulsa = msg == WM_LBUTTONDOWN || msg == WM_RBUTTONDOWN || msg == WM_MBUTTONDOWN;
            if (!suelta && !pulsa) return Native.CallNextHookEx(_hook, nCode, wParam, lParam);

            // la suelta que acompana al clic que cerramos: tambien se retira, si no la app de debajo
            // se quedaria con una pulsacion huerfana
            if (msg == _pendiente)
            {
                _pendiente = 0;
                if (_menu == null) Quitar();
                return (IntPtr)1;
            }

            ContextMenu menu = _menu;
            if (menu == null || !menu.IsOpen) return Native.CallNextHookEx(_hook, nCode, wParam, lParam);

            // el MSLLHOOKSTRUCT empieza por el POINT: x en el offset 0, y en el 4
            int cx = Marshal.ReadInt32(lParam), cy = Marshal.ReadInt32(lParam, 4);
            if (DentroDeMenu(cx, cy)) return Native.CallNextHookEx(_hook, nCode, wParam, lParam);

            // la suelta de un boton es su pulsacion + 1: 0x201->0x202, 0x204->0x205, 0x207->0x208
            if (pulsa) _pendiente = msg + 1;
            menu.Dispatcher.BeginInvoke(new Action(delegate { menu.IsOpen = false; }));
            return (IntPtr)1;
        }

        // los popups del menu (el principal y cada submenu abierto) son ventanas tool sin dueno del
        // propio proceso; la pildora no lo es, asi que pincharla tambien cierra. El fantasma del
        // arrastre si es tool, pero lleva WS_EX_TRANSPARENT y nunca tiene que contar.
        static bool DentroDeMenu(int x, int y)
        {
            bool dentro = false;
            Native.EnumWindows((h, lp) =>
            {
                uint pid;
                Native.GetWindowThreadProcessId(h, out pid);
                if (pid != MiPid || !Native.IsWindowVisible(h)) return true;
                int ex = Native.GetWindowLong(h, GWL_EXSTYLE);
                if ((ex & WS_EX_TOOLWINDOW) == 0 || (ex & WS_EX_TRANSPARENT) != 0) return true;
                Native.RECT r;
                if (!Native.GetWindowRect(h, out r)) return true;
                if (x >= r.left && x < r.right && y >= r.top && y < r.bottom) { dentro = true; return false; }
                return true;
            }, IntPtr.Zero);
            return dentro;
        }
    }
}
