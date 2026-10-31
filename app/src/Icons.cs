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
            byte[] hash;
            using (var md5 = MD5.Create()) hash = md5.ComputeHash(Encoding.UTF8.GetBytes(seed));
            string hex = BitConverter.ToString(hash).Replace("-", string.Empty);
            return Path.Combine(Paths.IconDir, hex.Substring(0, 24).ToLowerInvariant() + ".png");
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
