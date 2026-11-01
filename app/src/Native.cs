using System;
using System.Runtime.InteropServices;

namespace GamesDock
{
    public static class Native
    {
        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("kernel32.dll")]
        public static extern bool SetProcessWorkingSetSize(IntPtr p, IntPtr min, IntPtr max);

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr h);

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr h, int idx);

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr h, int idx, int val);

        // ------------------------------------------------------------------------------------------
        // lo que necesita MenuGuard para ver el raton mientras un menu esta abierto
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint threadId);

        [DllImport("user32.dll")]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lp);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr h, out RECT r);

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT p);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr h);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowText(IntPtr h, System.Text.StringBuilder s, int max);

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetModuleHandle(string name);

        public delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
        public delegate bool EnumWindowsProc(IntPtr h, IntPtr lp);

        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }
    }

    public static class IconExtract
    {
        // ExtractAssociatedIcon solo sabe pedir 32x32. SHDefExtractIcon deja pedir el tamano real del
        // recurso (256 en casi cualquier exe moderno), que es lo que hace falta cuando el magnes
        // agranda el icono por encima de su medida de reposo.
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHDefExtractIconW")]
        public static extern int Extract(string file, int index, uint flags, out IntPtr big, out IntPtr small, uint size);

        [DllImport("user32.dll")]
        public static extern bool DestroyIcon(IntPtr h);
    }
}
