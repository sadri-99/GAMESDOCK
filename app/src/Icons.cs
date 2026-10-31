using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace GamesDock
{
    // Espejo de Get-IconFile: el config guarda rutas que a veces son el png ya extraido y a veces un
    // .exe/.dll/.ico/.url. Todo lo que no sea imagen se convierte a png de 256 y se cachea en disco,
    // que es lo que mantiene nitido el icono cuando el magnes lo agranda por encima de su medida base.
    public static class Icons
    {
        static readonly Regex IsImageRe = new Regex(@"\.(png|jpg|jpeg|ico)$", RegexOptions.IgnoreCase);
        static readonly Regex IsBin   = new Regex(@"\.(exe|ico|dll)$", RegexOptions.IgnoreCase);

        public static bool IsImage(string path)
        {
            return !string.IsNullOrEmpty(path) && IsImageRe.IsMatch(path);
        }

        // un "icon" del config solo manda cuando es un archivo colocado por InstallCustom: los valores
        // heredados (el .url de Steam, un .lnk, el png del cache) son pistas del escaneo y hay que
        // volver a resolverlas en cada arranque, si no se quedan clavados aunque Steam mueva la cache
        public static bool IsCustom(string path)
        {
            if (!IsImage(path) || !File.Exists(path)) return false;
            string dir;
            try { dir = Path.GetDirectoryName(path); }
            catch { return false; }
            return string.Equals(dir, Paths.CustomDir, StringComparison.OrdinalIgnoreCase);
        }

        // Elige y guarda el icono propio de un fijado. La copia vive dentro de la carpeta de la app: si
        // el usuario borra o mueve el original, el tile no se queda sin nada.
        public static string InstallCustom(string key, string src)
        {
            string name = Md5Hex(key ?? string.Empty).Substring(0, 16);
            string ext = Path.GetExtension(src) ?? string.Empty;
            try
            {
                Directory.CreateDirectory(Paths.CustomDir);
                string dest = Path.Combine(Paths.CustomDir, name + ext);
                if (IsImage(dest))
                {
                    CleanCustom(name, ext);
                    File.Copy(src, dest, true);
                    return dest;
                }
                // binario (exe, dll, lnk): se guarda el png ya extraido, copiar un .exe entero seria
                // llenar el disco de copias que nadie mira
                string png = Path.Combine(Paths.CustomDir, name + ".png");
                CleanCustom(name, ".png");
                if (Extract256(src, png) || ExtractFallback(src, png)) return png;
                TryDelete(png);
            }
            catch (Exception ex)
            {
                Log.Write("icono personalizado fallido para " + src + ": " + ex.Message);
            }
            return null;
        }

        public static void ForgetCustom(string key)
        {
            string name = Md5Hex(key ?? string.Empty).Substring(0, 16);
            try { foreach (var f in Directory.GetFiles(Paths.CustomDir, name + ".*")) TryDelete(f); }
            catch { }
        }

        static void CleanCustom(string name, string keep)
        {
            foreach (var f in Directory.GetFiles(Paths.CustomDir, name + ".*"))
                if (!string.Equals(Path.GetExtension(f), keep, StringComparison.OrdinalIgnoreCase)) TryDelete(f);
        }

        static void TryDelete(string f)
        {
            try { File.Delete(f); } catch { }
        }

        static string Md5Hex(string s)
        {
            byte[] hash;
            using (var md5 = MD5.Create()) hash = md5.ComputeHash(Encoding.UTF8.GetBytes(s));
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }

        public static string Resolve(ItemSpec it)
        {
            if (IsImage(it.Icon) && File.Exists(it.Icon)) return it.Icon;

            string hint = it.IconHint;
            if (string.IsNullOrEmpty(hint) || !File.Exists(hint)) hint = it.Target;
            if (string.IsNullOrEmpty(hint) && !string.IsNullOrEmpty(it.Target) && IsBin.IsMatch(it.Target) && File.Exists(it.Target))
                hint = it.Target;
            if (string.IsNullOrEmpty(hint) || !File.Exists(hint)) return null;

            FileInfo fi;
            try { fi = new FileInfo(hint); }
            catch { return null; }

            string png = CachePathFor(hint, fi);
            if (File.Exists(png)) return png;

            if (Extract256(hint, png)) return png;
            if (ExtractFallback(hint, png)) return png;
            return png;   // puede no existir: el tile usara la inicial como reserva
        }

        static string CachePathFor(string hint, FileInfo fi)
        {
            string seed = hint.ToLowerInvariant() + "|" + fi.LastWriteTimeUtc.Ticks + "|256";
            return Path.Combine(Paths.IconDir, Md5Hex(seed).Substring(0, 24) + ".png");
        }

        static bool Extract256(string hint, string png)
        {
            IntPtr big = IntPtr.Zero, small = IntPtr.Zero;
            try
            {
                int rc;
                try { rc = IconExtract.Extract(hint, 0, 0u, out big, out small, 256u); }
                catch (Exception ex)
                {
                    Log.Write("extract 256 fallida para " + hint + ": " + ex.Message);
                    return false;
                }
                if (rc != 0 || big == IntPtr.Zero) return false;

                using (var ico = System.Drawing.Icon.FromHandle(big))
                using (var bmp = ico.ToBitmap())
                {
                    // un recurso de 32x32 no sirve: al agrandar se volveria borroso
                    if (bmp.Width < 48) return false;
                    bmp.Save(png, System.Drawing.Imaging.ImageFormat.Png);
                }
                return true;
            }
            finally
            {
                // FromHandle no es dueno del handle: si nadie lo cierra, cada icono extraido deja dos GDI
                if (big != IntPtr.Zero) IconExtract.DestroyIcon(big);
                if (small != IntPtr.Zero) IconExtract.DestroyIcon(small);
            }
        }

        static bool ExtractFallback(string hint, string png)
        {
            try
            {
                var ico = System.Drawing.Icon.ExtractAssociatedIcon(hint);
                if (ico == null) return false;
                using (ico)
                using (var bmp = ico.ToBitmap())
                {
                    bmp.Save(png, System.Drawing.Imaging.ImageFormat.Png);
                }
                return true;
            }
            catch (Exception ex)
            {
                Log.Write("Icono no extraible para " + hint + ": " + ex.Message);
                return false;
            }
        }
    }
}
