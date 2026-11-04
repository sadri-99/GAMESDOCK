using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace GamesDock
{
    public sealed class DisplayInfo
    {
        public IntPtr Handle;
        public string Id, Device;
        public Rect Bounds, WorkArea; // Physical desktop pixels, including negative coordinates.
        public bool Primary;
    }

    public static class MonitorPlacement
    {
        static double Unit(double value) { return double.IsNaN(value) || double.IsInfinity(value) ? .5 : Math.Max(0, Math.Min(1, value)); }

        public static DisplayInfo Resolve(Config cfg, IList<DisplayInfo> displays)
        {
            if (displays == null || displays.Count == 0) throw new ArgumentException("No connected displays.");
            if (!string.IsNullOrEmpty(cfg.MonitorId))
                foreach (var display in displays)
                    if (string.Equals(display.Id, cfg.MonitorId, StringComparison.OrdinalIgnoreCase)) return display;
            // An interface identity takes precedence over display numbering, which Windows may change.
            if (string.IsNullOrEmpty(cfg.MonitorId) || cfg.MonitorId == cfg.MonitorDevice)
                foreach (var display in displays)
                    if (string.Equals(display.Device, cfg.MonitorDevice, StringComparison.OrdinalIgnoreCase)) return display;
            foreach (var display in displays) if (display.Primary) return display;
            return displays[0];
        }

        public static DisplayInfo AtPoint(Point point, IList<DisplayInfo> displays)
        {
            DisplayInfo nearest = null; double best = double.MaxValue;
            foreach (var display in displays)
            {
                var bounds = display.Bounds;
                if (point.X >= bounds.Left && point.X < bounds.Right && point.Y >= bounds.Top && point.Y < bounds.Bottom) return display;
                double dx = Math.Max(bounds.Left - point.X, Math.Max(0, point.X - bounds.Right));
                double dy = Math.Max(bounds.Top - point.Y, Math.Max(0, point.Y - bounds.Bottom));
                double distance = dx * dx + dy * dy;
                if (distance < best) { nearest = display; best = distance; }
            }
            return nearest;
        }

        public static Rect DockBounds(Config cfg, DisplayInfo display, Size logicalSize, double scale)
        {
            var area = display.WorkArea;
            double w = logicalSize.Width * scale, h = logicalSize.Height * scale;
            double m = Layout.Margin * scale, gap = 6 * scale;
            double left = area.Left + (area.Width - w) / 2, top = area.Bottom - h + m - gap;
            switch (cfg.Position)
            {
                case "top": top = area.Top - m + gap; break;
                case "left": left = area.Left - m + gap; top = area.Top + (area.Height - h) / 2; break;
                case "right": left = area.Right - w + m - gap; top = area.Top + (area.Height - h) / 2; break;
                case "free":
                    double minX = area.Left - m + gap, minY = area.Top - m + gap;
                    double spanX = Math.Max(0, area.Width - Math.Max(0, w - 2 * m) - 2 * gap);
                    double spanY = Math.Max(0, area.Height - Math.Max(0, h - 2 * m) - 2 * gap);
                    double x = cfg.MonitorFreeX ?? (cfg.Left.HasValue && spanX > 0 ? ((cfg.Left.Value - Layout.Margin) * scale - minX) / spanX : .5);
                    double y = cfg.MonitorFreeY ?? (cfg.Top.HasValue && spanY > 0 ? ((cfg.Top.Value - Layout.Margin) * scale - minY) / spanY : .5);
                    left = minX + Unit(x) * spanX; top = minY + Unit(y) * spanY;
                    break;
            }
            return new Rect(Math.Round(left), Math.Round(top), Math.Max(1, Math.Round(w)), Math.Max(1, Math.Round(h)));
        }

        public static void Remember(Config cfg, DisplayInfo display, Rect windowBounds, double scale)
        {
            cfg.MonitorId = display.Id; cfg.MonitorDevice = display.Device;
            double margin = Layout.Margin * scale, gap = 6 * scale;
            double spanX = display.WorkArea.Width - Math.Max(0, windowBounds.Width - 2 * margin) - 2 * gap;
            double spanY = display.WorkArea.Height - Math.Max(0, windowBounds.Height - 2 * margin) - 2 * gap;
            cfg.MonitorFreeX = spanX > 0 ? Unit((windowBounds.Left - display.WorkArea.Left + margin - gap) / spanX) : .5;
            cfg.MonitorFreeY = spanY > 0 ? Unit((windowBounds.Top - display.WorkArea.Top + margin - gap) / spanY) : .5;
        }

        public static Rect ClampPopup(Rect popup, Rect area, double gap)
        {
            double left = Math.Max(area.Left + gap, Math.Min(popup.Left, area.Right - popup.Width - gap));
            double top = Math.Max(area.Top + gap, Math.Min(popup.Top, area.Bottom - popup.Height - gap));
            return new Rect(left, top, popup.Width, popup.Height);
        }

        public static Rect PopupBounds(Rect anchor, Size size, Rect work, string position, double gap, double inset)
        {
            if (position == "free") position = anchor.Top + anchor.Height / 2 > work.Top + work.Height / 2 ? "bottom" : "top";
            double left = anchor.Left + (anchor.Width - size.Width) / 2, top = anchor.Top - gap - size.Height;
            switch (position)
            {
                case "top": top = anchor.Bottom + gap; break;
                case "left": left = anchor.Right + gap; top = anchor.Top + (anchor.Height - size.Height) / 2; break;
                case "right": left = anchor.Left - gap - size.Width; top = anchor.Top + (anchor.Height - size.Height) / 2; break;
            }
            return ClampPopup(new Rect(left, top, size.Width, size.Height), work, inset);
        }
    }

    public static class Displays
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct MonitorInfo
        {
            public int Size;
            public Native.RECT Bounds, Work;
            public uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device;
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct DisplayDevice
        {
            public int Size;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Name;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
            public uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Id;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Key;
        }
        delegate bool MonitorCallback(IntPtr monitor, IntPtr hdc, ref Native.RECT bounds, IntPtr state);
        [DllImport("user32.dll")] static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorCallback callback, IntPtr state);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetMonitorInfoW")]
        static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "EnumDisplayDevicesW")]
        static extern bool EnumDisplayDevices(string device, uint index, ref DisplayDevice display, uint flags);
        [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
        static List<DisplayInfo> _connected;

        public static void EnableDpiScaling()
        {
            AppContext.SetSwitch("Switch.System.Windows.DoNotScaleForDpiChanges", false);
        }

        public static void Invalidate() { _connected = null; }
        public static IList<DisplayInfo> Connected
        {
            get
            {
                if (_connected != null) return _connected;
                var result = new List<DisplayInfo>();
                EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, delegate(IntPtr monitor, IntPtr dc, ref Native.RECT rectangle, IntPtr state)
                {
                    var info = new MonitorInfo { Size = Marshal.SizeOf(typeof(MonitorInfo)) };
                    if (!GetMonitorInfo(monitor, ref info)) return true;
                    string identity = info.Device;
                    for (uint index = 0; ; index++)
                    {
                        var device = new DisplayDevice { Size = Marshal.SizeOf(typeof(DisplayDevice)) };
                        if (!EnumDisplayDevices(info.Device, index, ref device, 1)) break;
                        if ((device.Flags & 1) != 0 && !string.IsNullOrEmpty(device.Id)) { identity = device.Id; break; }
                    }
                    var bounds = RectOf(info.Bounds); var work = RectOf(info.Work);
                    if (bounds.Width > 0 && bounds.Height > 0 && work.Width > 0 && work.Height > 0)
                        result.Add(new DisplayInfo { Handle = monitor, Id = identity, Device = info.Device,
                            Primary = (info.Flags & 1) != 0, Bounds = bounds, WorkArea = work });
                    return true;
                }, IntPtr.Zero);
                if (result.Count == 0)
                    foreach (var screen in System.Windows.Forms.Screen.AllScreens)
                        result.Add(new DisplayInfo { Id = screen.DeviceName, Device = screen.DeviceName, Primary = screen.Primary,
                            Bounds = new Rect(screen.Bounds.X, screen.Bounds.Y, screen.Bounds.Width, screen.Bounds.Height),
                            WorkArea = new Rect(screen.WorkingArea.X, screen.WorkingArea.Y, screen.WorkingArea.Width, screen.WorkingArea.Height) });
                _connected = result; return _connected;
            }
        }

        public static Rect RectOf(Native.RECT rect) { return new Rect(rect.left, rect.top, Math.Max(0, rect.right - rect.left), Math.Max(0, rect.bottom - rect.top)); }
        public static Rect WindowBounds(Window window)
        {
            Native.RECT bounds;
            var handle = new WindowInteropHelper(window).Handle;
            if (handle != IntPtr.Zero && Native.GetWindowRect(handle, out bounds)) return RectOf(bounds);
            double scale = Dock.DipScale(window);
            return new Rect(double.IsNaN(window.Left) ? 0 : window.Left * scale, double.IsNaN(window.Top) ? 0 : window.Top * scale,
                Math.Max(1, window.Width * scale), Math.Max(1, double.IsNaN(window.Height) ? 1 : window.Height * scale));
        }

        public static DisplayInfo ForWindow(Window window)
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle != IntPtr.Zero)
            {
                var monitor = MonitorFromWindow(handle, 2);
                foreach (var display in Connected) if (display.Handle == monitor) return display;
            }
            var bounds = WindowBounds(window);
            return MonitorPlacement.AtPoint(new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2), Connected);
        }

        public static Rect WorkAreaDip(DisplayInfo display, double scale)
        {
            scale = scale > 0 ? scale : 1;
            var work = display.WorkArea;
            return new Rect(work.Left / scale, work.Top / scale, work.Width / scale, work.Height / scale);
        }

        public static void MoveWindow(Window window, Point pixels)
        {
            var handle = new WindowInteropHelper(window).EnsureHandle();
            Native.SetWindowPos(handle, IntPtr.Zero, (int)Math.Round(pixels.X), (int)Math.Round(pixels.Y), 0, 0, 0x0001 | 0x0004 | 0x0010);
        }

        public static void CenterWindow(Window window, DisplayInfo display)
        {
            var bounds = WindowBounds(window); var work = display.WorkArea;
            var target = MonitorPlacement.ClampPopup(new Rect(work.Left + (work.Width - bounds.Width) / 2,
                work.Top + (work.Height - bounds.Height) / 2, bounds.Width, bounds.Height), work, 6);
            MoveWindow(window, target.TopLeft);
        }
    }
}
