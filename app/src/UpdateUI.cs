using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GamesDock
{
    // Lo que la barra dice sobre las actualizaciones. La red, la descarga y el cambio de archivo estan
    // en Update.cs; aqui solo se pregunta, se cuenta el resultado y se pide el si o el no.
    //
    // Dos entradas: el boton de Ajustes, que siempre contesta algo, y la comprobacion del arranque, que
    // solo se hace oir cuando hay una version nueva que no se haya rechazado ya.
    public static partial class Settings
    {
        static bool _comprobando;

        // ------------------------------------------------------------------ boton de Ajustes
        public static async void BuscarActualizaciones(Dock dock, Button boton)
        {
            if (_comprobando || dock == null) return;
            _comprobando = true;
            string normal = Lang.T("Buscar actualizaciones");
            if (boton != null) { boton.Content = Lang.T("Comprobando..."); boton.IsEnabled = false; }

            UpdInfo info;
            try { info = await Update.ComprobarAsync(CancellationToken.None); }
            catch (Exception ex)
            {
                Log.Write("actualizacion comprobacion: " + ex.Message);
                info = new UpdInfo { Estado = UpdEstado.Error, Detalle = ex.Message };
            }
            if (boton != null) { boton.Content = normal; boton.IsEnabled = true; }

            if (info.HayNovedad) Ofrecer(dock, info);
            else Aviso(Expresar(info));
            _comprobando = false;
        }

        // ------------------------------------------------------------------ arranque
        // Silenciosa salvo cuando trae algo nuevo. Un actualizador que saluda en cada arranque se
        // convierte en lo primero que se aprende a cerrar, asi que solo habla si hay que hablar.
        public static async void ComprobarAlArrancar(Dock dock)
        {
            if (_comprobando || dock == null) return;
            _comprobando = true;
            try
            {
                var info = await Update.ComprobarAsync(CancellationToken.None);
                if (!info.HayNovedad) return;
                if (string.Equals(dock.Cfg.UpdateIgnored, info.Titulo, StringComparison.OrdinalIgnoreCase))
                {
                    Log.Write("actualizacion: " + info.Titulo + " ya estaba rechazada");
                    return;
                }
                // con un emergente abierto (ajustes, renombrar, un aviso) no se monta otro encima
                if (Veil.HayVelo) { Log.Write("actualizacion: hay un emergente abierto, se deja estar"); return; }
                Ofrecer(dock, info);
            }
            catch (Exception ex) { Log.Write("actualizacion arranque: " + ex.Message); }
            finally { _comprobando = false; }
        }

        // ------------------------------------------------------------------ el si o el no
        // El panel hace de boton de descarga: al pulsar Actualizar se queda leyendo y la barra se cierra
        // cuando el archivo ya esta en disco. El aviso de un fallo se lanza DESPUES de que ShowDialog
        // haya vuelto, con el velo quitado, para no anidar un modal dentro del modal del propio panel.
        static void Ofrecer(Dock dock, UpdInfo info)
        {
            bool denegado = false, preparado = false;
            string fallo = null;

            var par = new GlassPanel(Lang.T("Actualización disponible"), 430,
                Lang.T("Enter para actualizar - Esc para dejarlo para otra vez"));
            var w = par.Win;

            var cab = Res.Label(info.Titulo, 14, "PanelFg");
            cab.FontWeight = FontWeights.SemiBold;
            cab.TextWrapping = TextWrapping.Wrap;
            cab.Margin = new Thickness(0, 0, 0, 3);
            par.Body.Children.Add(cab);

            var meta = Res.Label(Lang.F("Publicada el {0} - ocupa {1}",
                info.PublicadoUtc.ToLocalTime().ToShortDateString(), info.Mb), 11, "PanelSub");
            meta.TextWrapping = TextWrapping.Wrap;
            meta.Margin = new Thickness(0, 0, 0, 11);
            par.Body.Children.Add(meta);

            var local = Res.Label(Lang.F("Lo que tienes ahora es la compilación del {0}.",
                Update.FechaLocalUtc().ToLocalTime().ToShortDateString()), 11, "PanelSub");
            local.TextWrapping = TextWrapping.Wrap;
            local.Margin = new Thickness(0, 0, 0, 11);
            par.Body.Children.Add(local);

            var cuerpo = Res.Label(Lang.T("Se sustituye el programa en su propio sitio: no tienes que guardar ni mover ningún archivo. La barra se cierra un instante y vuelve sola con la versión nueva."), 12, "PanelFg");
            cuerpo.TextWrapping = TextWrapping.Wrap;
            cuerpo.Margin = new Thickness(0, 0, 0, 16);
            par.Body.Children.Add(cuerpo);

            var fila = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            var no = Pill(Lang.T("Ahora no"), delegate { denegado = true; Cerrar(w); });
            var si = new Button
            {
                Style = Res.Style("PillBtn"),
                Content = Lang.T("Actualizar"),
                Margin = new Thickness(0, 0, 7, 7),
                IsDefault = true
            };
            fila.Children.Add(no);
            fila.Children.Add(si);
            par.Body.Children.Add(fila);

            si.Click += async (s, e) =>
            {
                if (preparado || fallo != null) return;
                si.IsEnabled = false;
                no.IsEnabled = false;
                si.Content = Lang.T("Descargando...");

                var r = await Update.DescargarAsync(info, CancellationToken.None);
                if (r.Estado == UpdEstado.DescargaFalla)
                {
                    fallo = Lang.F("No se ha podido bajar la nueva versión: {0}", r.Detalle);
                    Cerrar(w);
                    return;
                }
                string err;
                if (!Update.Preparar(out err))
                {
                    fallo = Lang.F("La actualización no ha podido empezar: {0}", err);
                    Cerrar(w);
                    return;
                }
                preparado = true;
                si.Content = Lang.T("Cambiando...");
                Cerrar(w);
            };

            par.ShowModal();

            if (fallo != null) { Aviso(fallo); return; }
            if (denegado)
            {
                dock.Cfg.UpdateIgnored = info.Titulo;
                dock.Cfg.Save();
                Log.Write("actualizacion: " + info.Titulo + " rechazada por ahora");
                return;
            }
            // el encargo ya esta entregado: solo falta que este proceso se apague para que el .cmd
            // pueda mover el archivo
            if (preparado) Salir(dock);
        }

        // Salir por la puerta de siempre, la del menu de la bandeja: el config se guarda, se suelta el
        // fantasma, se quita el atajo y el dispatch se apaga solo cuando se cierra la ventana de la barra.
        static void Salir(Dock dock)
        {
            try
            {
                var ajustes = _open;
                _open = null;
                if (ajustes != null) ajustes.Close();
            }
            catch (Exception ex) { Log.Write("actualizacion cerrar ajustes: " + ex.Message); }
            try { dock.Exiting = true; dock.Win.Close(); }
            catch (Exception ex) { Log.Write("actualizacion salir: " + ex.Message); }
        }

        // ------------------------------------------------------------------ lo que se cuenta
        static string Expresar(UpdInfo info)
        {
            switch (info.Estado)
            {
                case UpdEstado.Actualizada:
                    return Lang.F("No hay ninguna versión publicada más nueva que la que estás usando. La última es {0}.", info.Titulo);
                case UpdEstado.SinReleases:
                    return Lang.T("Todavía no hay ninguna versión publicada en el repositorio.");
                case UpdEstado.SinRed:
                    return Lang.T("Sin conexión con GitHub para comprobar las versiones.");
                case UpdEstado.Rechazada:
                    return Lang.T("GitHub ha cortado la comprobación por exceso de peticiones. Vuelve a intentarlo dentro de un rato.");
                default:
                    return Lang.F("GitHub ha respondido mal: {0}", info.Detalle);
            }
        }
    }
}
