using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace GamesDock
{
    public static class Paths
    {
        public static readonly string Root    = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GamesDock");
        public static readonly string IconDir = Path.Combine(Root, "icons");
        // los iconos elegidos por el usuario se copian aqui; la carpeta es el marcador de "personalizado",
        // porque el cache de extraccion vive justo al lado y no hay que confundirlo
        public static readonly string CustomDir = Path.Combine(Root, "icons", "custom");
        public static readonly string DeskDir = Path.Combine(Root, "escritorio");
        public static readonly string CfgFile = Path.Combine(Root, "config.json");
        public static readonly string LogFile = Path.Combine(Root, "dock.log");

        public static void Ensure()
        {
            Directory.CreateDirectory(Root);
            Directory.CreateDirectory(IconDir);
            Directory.CreateDirectory(CustomDir);
            Directory.CreateDirectory(DeskDir);
        }
    }

    public static class Log
    {
        const long MaxBytes = 262144;

        public static void Write(string message)
        {
            try
            {
                var f = new FileInfo(Paths.LogFile);
                if (f.Exists && f.Length > MaxBytes) File.WriteAllText(Paths.LogFile, string.Empty);
                File.AppendAllText(Paths.LogFile,
                    "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + message + Environment.NewLine,
                    new UTF8Encoding(false));
            }
            catch
            {
            }
        }
    }

    public static class Memory
    {
        public static void Trim()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            try
            {
                // -1 = volver a los limites por defecto: suelta las paginas en frio y devuelve RAM al sistema
                Native.SetProcessWorkingSetSize(new IntPtr(-1), new IntPtr(-1), new IntPtr(-1));
            }
            catch
            {
            }
        }
    }
}
