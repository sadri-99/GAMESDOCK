using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace GamesDock
{
    public class ShellLinkInfo
    {
        public string Target;
        public string Args;
        public string Work;
        public string IconRaw;
    }

    // Enlace .lnk leido por late binding sobre WScript.Shell: evita meter una referencia de interop
    // en un exe que se compila sin SDK. El objeto es COM y vive en el hilo STA del dispatcher.
    public static class ShellLink
    {
        static readonly Type WshType = Type.GetTypeFromProgID("WScript.Shell");

        public static ShellLinkInfo Resolve(string path)
        {
            if (WshType == null) return null;
            object wsh = null, link = null;
            try
            {
                wsh = Activator.CreateInstance(WshType);
                link = WshType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, wsh, new object[] { path });
                if (link == null) return null;
                Type t = link.GetType();
                return new ShellLinkInfo
                {
                    Target = Str(t, link, "TargetPath"),
                    Args = Str(t, link, "Arguments"),
                    Work = Str(t, link, "WorkingDirectory"),
                    IconRaw = Str(t, link, "IconLocation")
                };
            }
            catch { return null; }
            finally
            {
                if (link != null) { try { Marshal.ReleaseComObject(link); } catch { } }
                if (wsh != null) { try { Marshal.ReleaseComObject(wsh); } catch { } }
            }
        }

        public static bool Create(string lnkPath, string target, string args, string work, string description)
        {
            if (WshType == null) return false;
            object wsh = null, link = null;
            try
            {
                wsh = Activator.CreateInstance(WshType);
                link = WshType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, wsh, new object[] { lnkPath });
                Type t = link.GetType();
                t.InvokeMember("TargetPath", BindingFlags.SetProperty, null, link, new object[] { target });
                if (args != null) t.InvokeMember("Arguments", BindingFlags.SetProperty, null, link, new object[] { args });
                if (work != null) t.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, link, new object[] { work });
                if (description != null) t.InvokeMember("Description", BindingFlags.SetProperty, null, link, new object[] { description });
                t.InvokeMember("Save", BindingFlags.InvokeMethod, null, link, null);
                return true;
            }
            catch (Exception ex)
            {
                Log.Write("acceso directo no creado: " + ex.Message);
                return false;
            }
            finally
            {
                if (link != null) { try { Marshal.ReleaseComObject(link); } catch { } }
                if (wsh != null) { try { Marshal.ReleaseComObject(wsh); } catch { } }
            }
        }

        static string Str(Type t, object o, string name)
        {
            try { return Convert.ToString(t.InvokeMember(name, BindingFlags.GetProperty, null, o, null)); }
            catch { return string.Empty; }
        }
    }

    public static class Launcher
    {
        public static Process Start(ItemSpec it)
        {
            if (it.Origin == "steam" && !string.IsNullOrEmpty(it.AppId))
            {
                var psi = new ProcessStartInfo("steam://rungameid/" + it.AppId) { UseShellExecute = true };
                return Process.Start(psi);
            }

            string file = !string.IsNullOrEmpty(it.Link) && File.Exists(it.Link) ? it.Link : it.Target;
            if (string.IsNullOrEmpty(file) || !File.Exists(file)) throw new FileNotFoundException(Lang.F("No existe el destino: {0}", file));

            var info = new ProcessStartInfo(file)
            {
                UseShellExecute = true,
                WorkingDirectory = !string.IsNullOrEmpty(it.Work) && Directory.Exists(it.Work) ? it.Work : Catalog.DirOf(file)
            };
            if (!string.IsNullOrEmpty(it.Arguments)) info.Arguments = it.Arguments;
            return Process.Start(info);
        }

        public static void SelectInExplorer(string target)
        {
            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe") { Arguments = "/select,\"" + target + "\"", UseShellExecute = true });
            }
            catch { }
        }

        public static void OpenFolder(string dir)
        {
            try { Process.Start(new ProcessStartInfo("explorer.exe") { Arguments = "\"" + dir + "\"", UseShellExecute = true }); }
            catch { }
        }

        // Abrir una pagina en el navegador que el usuario ya tiene: UseShellExecute es lo unico que
        // funciona con URLs en .NET Framework (una ProcessStartInfo sin ella intenta ejecutar la url
        // como si fuera un executable y revienta con Win32Exception)
        public static void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception ex) { Log.Write("no se pudo abrir " + url + ": " + ex.Message); }
        }
    }

}
