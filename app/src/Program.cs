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
        static void Main(string[] args)
        {
            Paths.Ensure();
            Net.AsegurarTls();

            bool primero;
            using (var single = new Mutex(false, "GamesDock_Single_Instance", out primero))
            {
                if (!primero)
                {
                    Log.Write("Segunda instancia: cancelada.");
                    return;
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

                dock.TrayIcon = new Tray(dock);
                dock.InstallHotkey();

                // lo que dejara una descarga cortada a medias no se hereda de una sesion a otra
                Update.LimpiarRestos();

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
                    if (Process.GetCurrentProcess().WorkingSet64 > 80L * 1024 * 1024) Memory.Trim();
                };
                idle.Start();

                app.Run(win);
                GC.KeepAlive(single);
            }
        }
    }
}
