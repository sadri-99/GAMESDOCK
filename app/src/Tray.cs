using System;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace GamesDock
{
    // La bandeja es el unico acceso que queda cuando el dock esta oculto o tapado por una ventana a
    // pantalla completa. WPF no tiene NotifyIcon, asi que se toma de Windows.Forms; los click llegan
    // en su propio hilo de mensajes y por eso todo se reenvia al despachador de la barra.
    public sealed class Tray
    {
        readonly Forms.NotifyIcon _ni;
        readonly Dock _dock;

        public Tray(Dock dock)
        {
            _dock = dock;
            _ni = new Forms.NotifyIcon
            {
                Icon = AppTrayIcon(),
                Text = "GamesDock",
                Visible = true
            };

            var menu = new Forms.ContextMenuStrip();
            Row(menu, "Mostrar / ocultar   (Ctrl+Alt+D)", () => _dock.ToggleDock());
            Row(menu, "Reescanear juegos", () => _dock.Rebuild());
            Row(menu, "Ajustes", () => Settings.Show(_dock));
            menu.Items.Add("-");
            Row(menu, "Salir", () => { _dock.Exiting = true; _dock.Win.Close(); });
            _ni.ContextMenuStrip = menu;

            _ni.MouseDoubleClick += delegate { OnUi(() => _dock.ToggleDock()); };
        }

        // El icono del exe no vale aqui: el shell solo extrae de el el tamano grande y la bandeja pide
        // 16 px (24 con el DPI agrandado), que es la variante con placa del mismo .ico empotrado.
        static Drawing.Icon AppTrayIcon()
        {
            var sz = Forms.SystemInformation.SmallIconSize;
            using (Stream s = typeof(Tray).Assembly.GetManifestResourceStream("GamesDock.icon.ico"))
            {
                try { return new Drawing.Icon(s, sz.Width, sz.Height); }
                catch (Exception ex) { Log.Write("icono de la bandeja: " + ex.Message); return Drawing.SystemIcons.Application; }
            }
        }

        void Row(Forms.ContextMenuStrip menu, string text, Action accion)
        {
            var item = menu.Items.Add(text);
            item.Click += delegate { OnUi(accion); };
        }

        void OnUi(Action accion)
        {
            _dock.Win.Dispatcher.BeginInvoke(accion);
        }

        public void Dispose()
        {
            try
            {
                _ni.Visible = false;
                _ni.Dispose();
            }
            catch (Exception ex) { Log.Write("bandeja: " + ex.Message); }
        }
    }

    public partial class Dock
    {
        HwndSource _hotkeySrc;
        HwndSourceHook _hotkeyProc;

        // MOD_ALT=0x01 y MOD_CONTROL=0x02. Con 0x08 (tecla Win) Windows se queda la combinacion y
        // RegisterHotKey devuelve si mismo, pero el atajo nunca llega: la grabal teclas no la admite.
        public void InstallHotkey()
        {
            if (!Cfg.Hotkey) return;
            try
            {
                var src = (HwndSource)PresentationSource.FromVisual(Win);
                if (src == null) { Log.Write("hotkey: la ventana todavia no tiene HWND"); return; }
                bool ok = Native.RegisterHotKey(src.Handle, 1, 0x0001 | 0x0002, 0x44);   // Ctrl+Alt+D
                _hotkeySrc = src;
                // el delegate tiene que vivir en un campo: AddHook lo guarda como nativo pero si el GC
                // lo recoge el manejador se apaga en silencio
                _hotkeyProc = delegate(IntPtr h, int msg, IntPtr w, IntPtr l, ref bool handled)
                {
                    if (msg == 0x0312 && w.ToInt32() == 1)
                    {
                        ToggleDock();
                        handled = true;
                    }
                    return IntPtr.Zero;
                };
                src.AddHook(_hotkeyProc);
                Log.Write(ok ? "Hotkey Ctrl+Alt+D registrada." : "Hotkey Ctrl+Alt+D rechazada por el sistema.");
            }
            catch (Exception ex) { Log.Write("hotkey no registrada: " + ex.Message); }
        }

        public void RemoveHotkey()
        {
            try
            {
                if (_hotkeySrc == null) return;
                if (_hotkeyProc != null) _hotkeySrc.RemoveHook(_hotkeyProc);
                Native.UnregisterHotKey(_hotkeySrc.Handle, 1);
            }
            catch (Exception ex) { Log.Write("hotkey: " + ex.Message); }
            _hotkeySrc = null;
            _hotkeyProc = null;
        }
    }
}
