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
        public static extern int GetWindowLong(IntPtr h, int idx);

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr h, int idx, int val);
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
