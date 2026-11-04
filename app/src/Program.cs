using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace GamesDock
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            Displays.EnableDpiScaling();
            if (args.Length == 1 && args[0] == "--shutdown")
            {
                EventWaitHandle signal;
                if (EventWaitHandle.TryOpenExisting("GamesDock_Shutdown", out signal))
                    using (signal) signal.Set();
                return 0;
            }
            Paths.Ensure();
            if (args.Length == 2 && args[0] == "--configure-startup")
            {
                if (args[1] != "on" && args[1] != "off") return 2;
                var settings = Config.Load();
                bool enabled = args[1] == "on";
                if (!Autostart.Set(enabled)) return 1;
                settings.Autostart = enabled;
                settings.Save();
                return 0;
            }
            var startupWatch = Stopwatch.StartNew();
            Net.AsegurarTls();

            bool primero;
            using (var single = new Mutex(false, "GamesDock_Single_Instance", out primero))
            {
                if (!primero)
                {
                    Log.Write("Segunda instancia: cancelada.");
                    return 0;
                }

                Log.Write("Arranque PID " + Process.GetCurrentProcess().Id +
                          " (STA=" + Thread.CurrentThread.GetApartmentState() + ")");

                var cfg = Config.Load();
                // antes de construir ninguna ventana: los textos se traducen al crearse el control, no
                // se reevaluan despues
                Lang.Apply(cfg.Language);

                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                app.DispatcherUnhandledException += (s, e) =>
                {
                    Log.Write("EXCEPCION no manejada: " + e.Exception);
                    e.Handled = true;
                };

                try
                {
                    app.Resources.MergedDictionaries.Add(XamlAssets.LoadDictionary("styles"));
                    Log.Write("estilos globales cargados");
                }
                catch (Exception ex)
                {
                    Log.Write("FALLO al cargar estilos globales: " + ex.Message);
                    if (ex.InnerException != null) Log.Write("  interior: " + ex.InnerException.Message);
                }

                var win = XamlAssets.Load<Window>("window");
                win.Width = 1000;
                win.Height = cfg.IconSize + 26;

                var dock = new Dock(win, cfg);
                dock.Cat = new Catalog(cfg);
                dock.ApplyLayout();
                dock.WireEvents();
                win.ShowActivated = false;
                win.Show();
                // la fila se reconstruye DESPUES del primer pintado: el contorno se mide contra el
                // ActualWidth de la rejilla, y antes de Show eso es 0, asi que la pildora no nace
                dock.RebuildStrip(false);
                dock.PlayEntry();
                dock.AttachToDesktop();
                win.ContentRendered += delegate
                {
                    Log.Write("Primera imagen: " + startupWatch.ElapsedMilliseconds + " ms");
                };

                // el escaneo va en prioridad background: registry + manifiestos + recorrido del menu
                // Inicio puede tardar unos cientos de ms y no debe bloquear la primera pildora
                win.Dispatcher.BeginInvoke(new Action(delegate
                {
                    dock.Cat.Build();
                    dock.Refresh(true);
                }), DispatcherPriority.Background);

                // el fantasma se monta con el dock ya pintado: la primera vez que alguien levanta un
                // icono no puede coincidir con la primera vez que se crea la ventana, ese costo si se
                // nota en el gesto
                win.Dispatcher.BeginInvoke(new Action(dock.EnsureGhost), DispatcherPriority.ContextIdle);

                // igual que el fantasma: la ventana de la ficha se monta por adelantado, porque el primer
                // hover no puede pagar crear una HWND y decodificar un banner de golpe
                win.Dispatcher.BeginInvoke(new Action(dock.EnsureCard), DispatcherPriority.ContextIdle);

                dock.TrayIcon = new Tray(dock);
                dock.InstallHotkey();

                // lo que dejara una descarga cortada a medias no se hereda de una sesion a otra
                win.Dispatcher.BeginInvoke(new Action(delegate
                {
                    Autostart.Repair(cfg);
                    Update.LimpiarRestos();
                }), DispatcherPriority.ContextIdle);

                // la comprobacion de versiones unos segundos despues del primer pintado: al arrancar
                // Windows es cuando menos gracia hace que se monte una ventana encima del escritorio, y
                // ademas deja pasar delante el escaneo de juegos
                var upd = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
                upd.Tick += delegate { upd.Stop(); Settings.ComprobarAlArrancar(dock); };
                upd.Start();

                // red de seguridad, no un recorte periodico: solo suelta memoria si el proceso se ha
                // pasado del umbral, asi que en reposo no mueve ni un cuadro de animacion
                var idle = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
                idle.Tick += delegate
                {
                    if (dock.CanTrimMemory && Process.GetCurrentProcess().WorkingSet64 > 80L * 1024 * 1024) Memory.Trim();
                };
                idle.Start();

                using (var shutdown = new EventWaitHandle(false, EventResetMode.AutoReset, "GamesDock_Shutdown"))
                {
                    var wait = ThreadPool.RegisterWaitForSingleObject(shutdown, (state, timedOut) =>
                    {
                        app.Dispatcher.BeginInvoke(new Action(delegate { dock.Exiting = true; win.Close(); }));
                    }, null, Timeout.Infinite, true);
                    try { app.Run(win); }
                    finally { wait.Unregister(null); }
                }
                GC.KeepAlive(single);
                return 0;
            }
        }
    }
}
