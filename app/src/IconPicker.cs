using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GamesDock
{
    // El selector de iconos de SteamGridDB: una rejilla de miniaturas con scroll. Recibe la lista que
    // devolvio el servicio, las va bajando en cuanto la ventana existe y al pinchar una se cierra
    // devolviendo esa url. No escribe nada en disco ni toca el config: eso lo decide el dock, que es el
    // que sabe a que fijado pertenece la eleccion.
    public class IconPicker
    {
        // el tamano de una ficha en DIP. Con 84 caben cinco por fila en un panel de 560, que es lo que
        // se ve sin mover la cabeza
        const double Lado = 84.0;

        // cuatro descargas a la vez: sesenta conexiones simultaneas al CDN al abrir el panel retrasan
        // el primer pintado mas de lo que tardan cuatro tandas
        const int Carriles = 4;

        readonly GlassPanel _par;
        readonly WrapPanel _rejilla;
        readonly TextBlock _aviso;
        readonly Dock _dock;
        readonly List<Ficha> _fichas = new List<Ficha>();
        readonly CancellationTokenSource _alCerrar = new CancellationTokenSource();
        string _elegida;
        int _pendientes;

        // una miniatura: el boton, el marco que recibe la imagen y las dos urls (la grande para elegir,
        // la pequena para ensenar)
        class Ficha
        {
            public RadioButton Btn;
            public Border Marco;
            public string Url;
            public string Thumb;
        }

        // Abre el panel modal y devuelve la url del icono elegido, o null si se cancela (cruz, Esc o
        // clic fuera, que el velo lo convierte en "no pasa nada").
        public static string Seleccionar(Dock dock, string titulo, List<SgdIcono> iconos)
        {
            if (dock == null || iconos == null || iconos.Count == 0) return null;
            var p = new IconPicker(dock, titulo, iconos);
            p._par.ShowModal();
            return p._elegida;
        }

        IconPicker(Dock dock, string titulo, List<SgdIcono> iconos)
        {
            _dock = dock;
            _par = new GlassPanel(Lang.F("Elige un icono para {0}", titulo), 560,
                                  Lang.T("clic en el que quieras - Esc para cancelar"));

            _rejilla = new WrapPanel { Margin = new Thickness(0, 2, 0, 2) };
            _aviso = Res.Label(Lang.T("cargando miniaturas..."), 11, "PanelSub");
            _aviso.HorizontalAlignment = HorizontalAlignment.Center;
            _aviso.Margin = new Thickness(0, 6, 0, 0);

            _par.Body.Children.Add(_rejilla);
            _par.Body.Children.Add(_aviso);

            foreach (var ic in iconos) _rejilla.Children.Add(FichaDe(ic).Btn);
            _pendientes = _fichas.Count;

            // al cerrar se cortan las descargas que queden: si no, el panel seguiria llenando brochas
            // de una ventana que ya no existe
            _par.Win.Closed += delegate { _alCerrar.Cancel(); };
            // la carga no puede empezar antes de que haya ventana: los await vuelven al dispatcher del
            // dialogo, y sin mensaje de ventana no hay dispatcher al que volver
            _par.Win.ContentRendered += Cargar;
        }

        Ficha FichaDe(SgdIcono ic)
        {
            var marco = new Border
            {
                Width = Lado,
                Height = Lado,
                ClipToBounds = true,
                // la imagen va en el Background del Border, no como hijo: WPF recorta el fondo a su
                // CornerRadius pero al hijo no, y el recorte tiene que seguir la forma elegida en Ajustes
                CornerRadius = new CornerRadius(_dock.Lay.TileRadius(Lado))
            };
            marco.SetResourceReference(Border.BackgroundProperty, "CtlBg");
            marco.SetResourceReference(Border.BorderBrushProperty, "CtlEdge");
            marco.BorderThickness = new Thickness(1);

            var f = new Ficha { Url = ic.Url, Thumb = string.IsNullOrEmpty(ic.Thumb) ? ic.Url : ic.Thumb, Marco = marco };

            // ChipRadio ya tiene el realce al pasar el raton y el borde de acento del tema, que es justo
            // lo que hace falta para que se lea "esto es seleccionable" sin explicar nada
            f.Btn = new RadioButton
            {
                Style = Res.Style("ChipRadio"),
                Content = marco,
                Margin = new Thickness(6),
                ToolTip = Lang.F("{0} x {1}", ic.Width, ic.Height)
            };
            var btn = f.Btn;
            btn.Click += delegate
            {
                _elegida = f.Url;
                Cerrar();
            };
            _fichas.Add(f);
            return f;
        }

        async void Cargar(object sender, EventArgs e)
        {
            var ct = _alCerrar.Token;
            var sig = new int[1];
            try
            {
                var tareas = new List<Task>();
                for (int carril = 0; carril < Carriles; carril++) tareas.Add(CarrilAsync(sig, ct));
                await Task.WhenAll(tareas);
            }
            catch (Exception ex) { Log.Write("selector: " + ex.Message); }
        }

        async Task CarrilAsync(int[] sig, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                int i = sig[0]++;
                if (i >= _fichas.Count) return;
                await BajarAsync(_fichas[i], ct);
            }
        }

        async Task BajarAsync(Ficha f, CancellationToken ct)
        {
            try
            {
                var r = await SteamGrid.DescargarBytesAsync(f.Thumb, ct);
                // el await vuelve al hilo de UI, asi que aqui ya se puede pintar sin invocar
                if (r.EsOk && !ct.IsCancellationRequested)
                {
                    var img = DesdeBytes(r.Datos);
                    // la ficha que no decodea se queda como placeholder: mejor vacia que rota
                    if (img != null)
                    {
                        var brocha = new ImageBrush(img) { Stretch = Stretch.Uniform };
                        brocha.Freeze();   // congelada: el panel la pinta por hardware y no la revalida
                        f.Marco.Background = brocha;
                    }
                }
            }
            finally
            {
                // el contador baja tambien cuando una miniatura falla: si no, el aviso de "cargando" no
                // se iria nunca y daria a entender que el panel sigue trabajando
                _pendientes--;
                if (_pendientes <= 0) _aviso.Visibility = Visibility.Collapsed;
            }
        }

        static ImageSource DesdeBytes(byte[] b)
        {
            try
            {
                var bi = new BitmapImage();
                using (var ms = new MemoryStream(b))
                {
                    bi.BeginInit();
                    // OnLoad decodifica aqui mismo y suelta el stream: si no, la imagen dependeria de un
                    // MemoryStream que ya no existe
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.StreamSource = ms;
                    bi.EndInit();
                }
                bi.Freeze();
                return bi;
            }
            catch (Exception ex)
            {
                Log.Write("miniatura: " + ex.Message);
                return null;
            }
        }

        void Cerrar()
        {
            try { _par.Win.Close(); } catch { }
        }
    }
}
