using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace GamesDock
{
    public partial class HoverCard
    {
        CancellationTokenSource _activityRequest;
        int _activityEpoch, _activityApp;
        bool _activityResolved;
        SteamActivityInfo _activityInfo;

        void ClipBanner()
        {
            double width = _cartel.ActualWidth, height = _cartel.ActualHeight;
            if (width <= 0 || height <= 0) return;
            double radius = Math.Min(Radio - 1, Math.Min(width / 2, height));
            var clip = new StreamGeometry();
            using (var context = clip.Open())
            {
                context.BeginFigure(new Point(0, radius), true, true);
                context.ArcTo(new Point(radius, 0), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
                context.LineTo(new Point(width - radius, 0), true, false);
                context.ArcTo(new Point(width, radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
                context.LineTo(new Point(width, height), true, false);
                context.LineTo(new Point(0, height), true, false);
            }
            clip.Freeze(); _cartel.Clip = clip;
        }

        void CancelActivity()
        {
            _activityEpoch++;
            if (_activityRequest != null) _activityRequest.Cancel();
            _activityRequest = null; _activityApp = 0; _activityInfo = null; _activityResolved = false;
            _personal.Text = string.Empty; _personalFrame.Visibility = Visibility.Collapsed;
        }

        void StartActivity(ItemSpec item)
        {
            CancelActivity();
            if (!_dock.Cfg.SteamPersonalData) return;
            int app = GameInfo.AppIdDe(item); if (app <= 0) return;
            _activityApp = app;
            var request = new CancellationTokenSource(); _activityRequest = request;
            _ = LoadActivityAsync(app, _activityEpoch, request);
        }

        async Task LoadActivityAsync(int app, int epoch, CancellationTokenSource request)
        {
            SteamActivityInfo info = null;
            try { info = await SteamActivity.ReadAsync(app, request.Token); }
            catch (OperationCanceledException) { return; }
            catch (Exception) { Log.Write("Actividad de Steam: datos locales no disponibles."); }
            finally
            {
                if (ReferenceEquals(_activityRequest, request)) _activityRequest = null;
                request.Dispose();
            }
            if (epoch != _activityEpoch || !_dock.Cfg.SteamPersonalData || _activityApp != app) return;
            _activityInfo = info; _activityResolved = true;
            if (_visible && _apuntero != null && GameInfo.Clave(_apuntero) == _clave)
            { Rellenar(_apuntero); _win.UpdateLayout(); Colocar(); }
        }

        static string Duration(long minutes)
        {
            return minutes < 60 ? minutes.ToString(CultureInfo.CurrentCulture) + " min"
                : (minutes / 60.0).ToString("0.#", CultureInfo.CurrentCulture) + " h";
        }

        void FillActivity(ItemSpec item)
        {
            int app = GameInfo.AppIdDe(item);
            _personalFrame.Visibility = _dock.Cfg.SteamPersonalData && app > 0 ? Visibility.Visible : Visibility.Collapsed;
            if (_personalFrame.Visibility != Visibility.Visible) return;
            _personalTitle.Text = Lang.T("Tu actividad en Steam");
            if (!_activityResolved || _activityApp != app) { _personal.Text = Lang.T("Consultando datos locales..."); return; }
            if (_activityInfo == null || !_activityInfo.HasData)
            { _personal.Text = Lang.T("Sin datos locales para este juego."); return; }
            var lines = new List<string>(); var info = _activityInfo;
            if (info.Minutes.HasValue) lines.Add(Lang.F("Tiempo jugado: {0}", Duration(info.Minutes.Value)));
            if (info.LastPlayedUtc.HasValue)
                lines.Add(Lang.F("Última partida: {0}", info.LastPlayedUtc.Value.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)));
            if (info.RecentMinutes.HasValue && info.LastPlayedUtc.HasValue && info.LastPlayedUtc.Value >= DateTime.UtcNow.AddDays(-14) &&
                DateTime.UtcNow - info.SnapshotUtc <= TimeSpan.FromDays(1) && info.SnapshotUtc <= DateTime.UtcNow.AddMinutes(5))
                lines.Add(Lang.F("Últimas 2 semanas: {0}", Duration(info.RecentMinutes.Value)));
            _personal.Text = string.Join("\n", lines);
        }

        void FitTextHeight()
        {
            _desc.MaxHeight = 90; _meta.MaxHeight = 49.5;
            _desc.Visibility = string.IsNullOrEmpty(_desc.Text) ? Visibility.Collapsed : Visibility.Visible;
            double available = _dock.MonitorWorkArea.Height - 12;
            _content.Measure(new Size(Ancho * _cardScale.ScaleX, double.PositiveInfinity));
            double excess = _content.DesiredSize.Height - available;
            if (excess > 0 && _desc.Visibility == Visibility.Visible)
            {
                double lines = Math.Floor(Math.Max(0, 90 - excess / _cardScale.ScaleY) / 18);
                _desc.MaxHeight = lines * 18;
                if (lines < 1) _desc.Visibility = Visibility.Collapsed;
                _content.Measure(new Size(Ancho * _cardScale.ScaleX, double.PositiveInfinity));
            }
            if (_content.DesiredSize.Height > available) _meta.MaxHeight = 33;
        }
    }
}
