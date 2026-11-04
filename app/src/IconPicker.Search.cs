using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace GamesDock
{
    public partial class IconPicker
    {
        CancellationTokenSource BeginRequest()
        {
            if (_request != null) _request.Cancel();
            _request = new CancellationTokenSource(); _revision++;
            _fichas.Clear(); _rejilla.Children.Clear(); _scroll.ScrollToVerticalOffset(0);
            _empty.Visibility = Visibility.Collapsed;
            _status.Text = Lang.T("Buscando iconos...");
            _footer.Text = Lang.T("Puedes probar otro nombre mientras carga.");
            return _request;
        }

        bool Current(int revision, CancellationToken token) { return !_closed && !token.IsCancellationRequested && revision == _revision; }
        void EndRequest(CancellationTokenSource request)
        {
            if (ReferenceEquals(_request, request)) _request = null;
            request.Dispose();
        }

        void SetGames(List<SgdJuego> games, string fallback)
        {
            _settingGames = true;
            try
            {
                _games.Items.Clear();
                if (games != null)
                    foreach (var game in games)
                        _games.Items.Add(new ComboBoxItem { Content = game.Name, Tag = game, ToolTip = game.Name, Style = Res.Style("PickerGameItem") });
                if (_games.Items.Count == 0)
                    _games.Items.Add(new ComboBoxItem { Content = fallback, Style = Res.Style("PickerGameItem") });
                _games.SelectedIndex = 0; _games.IsEnabled = games != null && games.Count > 0;
                _games.ToolTip = _games.IsEnabled ? Lang.T("Elige otra coincidencia si no es el juego que buscas.") : fallback;
            }
            finally { _settingGames = false; }
        }

        async Task SearchAsync(bool initial)
        {
            if (_closed) return;
            string query = (_query.Text ?? string.Empty).Trim();
            if (query.Length == 0) { _status.Text = Lang.T("Escribe el nombre de un juego."); return; }
            var request = BeginRequest(); int revision = _revision; var token = request.Token;
            SetGames(null, Lang.T("Buscando juegos..."));
            try
            {
                if (initial && _seed != null)
                { await ShowIconsAsync(_seed, _title, revision, token); return; }
                string key = _dock.ClaveSgd();
                int steam;
                if (initial && _original != null && int.TryParse(_original.AppId, out steam) && steam > 0)
                {
                    var exact = await SteamGrid.IconosDeSteamAsync(steam, key, token);
                    if (!Current(revision, token)) return;
                    if (exact.EsOk)
                    { SetGames(null, _title); await ShowIconsAsync(exact.Datos, _title, revision, token); return; }
                    if (exact.Estado != SgdEstado.NoEncontrado) { ShowError(exact.Estado); return; }
                }
                var matches = await SteamGrid.BuscarJuegosAsync(initial ? SteamGrid.LimpiarNombre(query) : query, key, token);
                if (!Current(revision, token)) return;
                if (!matches.EsOk) { SetGames(null, Lang.T("Sin coincidencias")); ShowError(matches.Estado); return; }
                SetGames(matches.Datos, query);
                await LoadGameIconsAsync(matches.Datos[0], key, revision, token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { if (Current(revision, token)) { Log.Write("Selector de iconos: " + ex.Message); ShowError(SgdEstado.ApiError); } }
            finally { EndRequest(request); }
        }

        async Task SelectGameAsync(SgdJuego game)
        {
            if (_closed) return;
            var request = BeginRequest(); int revision = _revision; var token = request.Token;
            try { await LoadGameIconsAsync(game, _dock.ClaveSgd(), revision, token); }
            catch (OperationCanceledException) { }
            catch (Exception ex) { if (Current(revision, token)) { Log.Write("Selector de juego: " + ex.Message); ShowError(SgdEstado.ApiError); } }
            finally { EndRequest(request); }
        }

        async Task LoadGameIconsAsync(SgdJuego game, string key, int revision, CancellationToken token)
        {
            _status.Text = Lang.F("Cargando iconos de {0}...", game.Name);
            var response = await SteamGrid.IconosDeJuegoAsync(game.Id, key, token);
            if (!Current(revision, token)) return;
            if (!response.EsOk) { ShowError(response.Estado); return; }
            await ShowIconsAsync(response.Datos, game.Name, revision, token);
        }

        async Task ShowIconsAsync(List<SgdIcono> icons, string name, int revision, CancellationToken token)
        {
            if (!Current(revision, token)) return;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var icon in icons)
            {
                if (icon == null || string.IsNullOrEmpty(icon.Url) || !seen.Add(icon.Url)) continue;
                var card = FichaDe(icon); _fichas.Add(card); _rejilla.Children.Add(card.Btn);
                if (_fichas.Count == SteamGrid.TopeSelector) break;
            }
            if (_fichas.Count == 0) { ShowError(SgdEstado.NoEncontrado); return; }
            _empty.Visibility = Visibility.Collapsed;
            _status.Text = Lang.F("{0} iconos · {1}", _fichas.Count, name);
            _status.ToolTip = name;
            await LoadThumbnailsAsync(new List<Ficha>(_fichas), revision, token);
        }

        void ShowError(SgdEstado state)
        {
            _empty.Visibility = Visibility.Visible;
            _emptyTitle.Text = Lang.T(state == SgdEstado.NoEncontrado ? "No se han encontrado iconos" : "No se pudieron cargar los iconos");
            string hint;
            switch (state)
            {
                case SgdEstado.NoEncontrado: hint = "Elige otra coincidencia o prueba con otro nombre."; break;
                case SgdEstado.SinClave: hint = "Añade tu clave de SteamGridDB en Ajustes → Iconos de la comunidad."; break;
                case SgdEstado.ClaveInvalida: hint = "SteamGridDB no acepta la clave. Revísala en Ajustes → Iconos de la comunidad."; break;
                case SgdEstado.SinRed: hint = "No hay conexión con SteamGridDB. Puedes volver a buscar para reintentarlo."; break;
                default: hint = "SteamGridDB no pudo completar la búsqueda. Inténtalo de nuevo."; break;
            }
            _emptyHint.Text = Lang.T(hint); _status.Text = _emptyTitle.Text;
            _footer.Text = Lang.T("Elige un icono para aplicarlo · Esc para cerrar");
        }
    }
}
