using System;
using System.Diagnostics;
using System.IO;
using System.Net;
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
        // donde cae el ejecutable descargado mientras se espera a poder sustituir el viejo. NO se crea al
        // arrancar: solo existe durante una actualizacion, y el lanzador la borra entera al terminar, asi
        // que en una barra sin actualizar no queda ni la carpeta
        public static readonly string UpdateDir = Path.Combine(Root, "update");
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

    // .NET Framework arranca con TLS 1.0 como techo y ni GitHub ni Cloudflare negocian por debajo de 1.2:
    // sin esto el HttpClient de la actualizacion y el de los iconos fallan antes de salir de casa. El exe
    // no lleva .config, asi que el ajuste tiene que hacerse en codigo. Se lista 1.2 delante y se dejan
    // detras 1.1 y 1.0 para no cerrar la puerta a ningun servidor; SystemDefault queda fuera porque en
    // este runtime pide TLS 1.3 al sistema y el runtime no lo conoce: la conexion se corta en el handshake.
    public static class Net
    {
        public static void AsegurarTls()
        {
            try
            {
                ServicePointManager.SecurityProtocol =
                    SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            }
            catch (Exception ex) { Log.Write("tls: " + ex.Message); }
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
