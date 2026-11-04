using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace GamesDock
{
    public partial class Dock
    {
        DisplayInfo _display;
        HwndSource _displaySource;
        HwndSourceHook _displayHook;
        bool _movingDock, _displayLayoutPending;
        public DisplayInfo CurrentDisplay { get { return _display; } }
        public Rect MonitorWorkArea { get { return Displays.WorkAreaDip(_display, Lay.Scale); } }

        void InitializeDisplays()
        {
            Displays.EnableDpiScaling();
            _display = MonitorPlacement.Resolve(Cfg, Displays.Connected);
            if (string.IsNullOrEmpty(Cfg.MonitorId))
            {
                // Recover an old free-position configuration when it already points at another screen.
                if (Cfg.Position == "free" && Cfg.Left.HasValue && Cfg.Top.HasValue)
                {
                    var primary = _display.Bounds;
                    double scale = SystemParameters.PrimaryScreenWidth > 0 ? primary.Width / SystemParameters.PrimaryScreenWidth : 1;
                    _display = MonitorPlacement.AtPoint(new Point(Cfg.Left.Value * scale, Cfg.Top.Value * scale), Displays.Connected);
                }
                Cfg.MonitorId = _display.Id; Cfg.MonitorDevice = _display.Device;
            }
            Win.SourceInitialized += delegate { AttachDisplayTracking(); };
            Win.Closed += delegate
            {
                try { if (_displaySource != null && _displayHook != null) _displaySource.RemoveHook(_displayHook); }
                catch (ObjectDisposedException) { }
                _displaySource = null; _displayHook = null;
            };
            if (new WindowInteropHelper(Win).Handle != IntPtr.Zero) AttachDisplayTracking();
        }

        void AttachDisplayTracking()
        {
            if (_displaySource != null) return;
            _displaySource = HwndSource.FromHwnd(new WindowInteropHelper(Win).Handle);
            if (_displaySource == null) return;
            _displayHook = delegate(IntPtr handle, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
            {
                if (message == 0x007E || message == 0x001A)
                { Displays.Invalidate(); QueueDisplayLayout(); }
                else if (message == 0x02E0) QueueDisplayLayout();
                return IntPtr.Zero; // Let WPF apply WM_DPICHANGED before the deferred layout.
            };
            _displaySource.AddHook(_displayHook);
        }

        void QueueDisplayLayout()
        {
            if (_displayLayoutPending || Win.Dispatcher.HasShutdownStarted) return;
            _displayLayoutPending = true;
            Win.Dispatcher.BeginInvoke(new Action(delegate
            {
                _displayLayoutPending = false;
                if (_movingDock || _displaySource == null) return;
                _display = MonitorPlacement.Resolve(Cfg, Displays.Connected);
                ApplyLayout();
            }), DispatcherPriority.ContextIdle);
        }

        void RememberMonitorPosition()
        {
            MonitorPlacement.Remember(Cfg, _display, Displays.WindowBounds(Win), DipScale(Win));
            double margin = Lay.Snap(Layout.Margin);
            Cfg.Left = Win.Left + margin; Cfg.Top = Win.Top + margin;
        }
    }
}
