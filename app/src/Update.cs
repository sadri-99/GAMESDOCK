using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GamesDock
{
    public enum UpdEstado { Actualizada, Disponible, SinReleases, SinRed, Rechazada, Error, DescargaFalla }

    // Lo que se sabe de la ultima version publicada. Viaja con el estado en lugar de lanzar excepciones:
    // "no hay novedad", "no hay red" y "GitHub te ha cortado las peticiones" se cuentan distinto en la
    // interfaz y las tres son respuestas normales a la misma pregunta.
    public class UpdInfo
    {
        public UpdEstado Estado = UpdEstado.Actualizada;
        public string Tag = "";              // "Release"
        public string Titulo = "";           // "GamesDock v.1.0.1"
        public string Url = "";              // descarga del .exe
        public long Bytes;
        public DateTime PublicadoUtc = DateTime.MinValue;
        public string Detalle = "";          // para los estados que hay que explicar

        public bool HayNovedad { get { return Estado == UpdEstado.Disponible; } }
        public string Mb { get { return Update.Tamano(Bytes); } }
    }

    // ----------------------------------------------------------------- lo que devuelve GitHub
    // Se leen cuatro campos y la lista de archivos; el resto del json (author, tarball_zip, discussion,
    // immutability...) no le sirve a la barra. Un campo que falte queda por defecto y no rompe nada.
    [DataContract]
    public class UpdAsset
    {
        [DataMember(Name = "name")] public string Name;
        [DataMember(Name = "size")] public long Size;
        [DataMember(Name = "browser_download_url")] public string Url;
    }

    [DataContract]
    public class UpdRelease
    {
        [DataMember(Name = "tag_name")] public string Tag;
        [DataMember(Name = "name")] public string Name;
        [DataMember(Name = "published_at")] public string Published;
        [DataMember(Name = "created_at")] public string Created;
        [DataMember(Name = "assets")] public List<UpdAsset> Assets;
    }

    // Cliente de la pagina de releases del repositorio. Dos unicas cosas sabe hacer: preguntar cual es
    // la ultima version publicada y traer el ejecutable de esa version. La decision de cambiar de
    // archivo la toma el usuario desde el panel de cristal; aqui no hay interfaz.
    public static class Update
    {
        const string RepoOficial = "https://api.github.com/repos/sadri-99/GAMESDOCK";

        // la pagina del repositorio vista por una persona: es a donde lleva el boton de estrella de
        // Ajustes, y el enlace que hay que abrir para dejarla. El API de arriba es el mismo repo, pero
        // en su cara de maquina.
        public const string PaginaRepo = "https://github.com/sadri-99/GAMESDOCK";

        // GAMESDOCK_UPD_BASE lo pisa unicamente el arnes (_dev/cs-upd.ps1 levanta un servidor local con
        // la misma forma de respuesta); en la barra normal siempre es el repositorio de verdad.
        static readonly string Base = BaseDelEntorno();

        static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        const string Agente = "GamesDock (https://github.com/sadri-99/GAMESDOCK)";

        // aire entre la compilacion local y la publicacion: si alguien compila y publica en el mismo
        // minuto, eso no es una version mas nueva que la que tiene delante
        static readonly TimeSpan Margen = TimeSpan.FromMinutes(5);

        static string _exe;

        static Update()
        {
            try { Http.DefaultRequestHeaders.UserAgent.ParseAdd(Agente); }
            catch (Exception ex) { Log.Write("actualizacion agente: " + ex.Message); }
        }

        static string BaseDelEntorno()
        {
            string b = Environment.GetEnvironmentVariable("GAMESDOCK_UPD_BASE");
            return string.IsNullOrEmpty(b) ? RepoOficial : b.TrimEnd('/');
        }

        // ------------------------------------------------------------------------ lo que se usa aqui
        public static string Exe
        {
            get
            {
                if (_exe != null) return _exe;
                string p = null;
                try { p = Process.GetCurrentProcess().MainModule.FileName; } catch { }
                if (string.IsNullOrEmpty(p))
                {
                    try { p = Assembly.GetExecutingAssembly().Location; } catch { }
                }
                _exe = p ?? string.Empty;
                return _exe;
            }
        }

        // Cuando se compilo el ejecutable que esta en marcha: su ultima escritura. Es lo unico que la
        // barra sabe de si misma, porque el build de desarrollo no lleva numero de version y nunca se le
        // pone uno. El numero solo lo tiene lo publicado, y sale del titulo del release.
        public static DateTime FechaLocalUtc()
        {
            try { return File.GetLastWriteTimeUtc(Exe); }
            catch { return DateTime.MinValue; }
        }

        // ------------------------------------------------------------------------ paso 1: preguntar
        public static async Task<UpdInfo> ComprobarAsync(CancellationToken ct)
        {
            var r = new UpdInfo();
            string json;
            HttpResponseMessage resp;
            try { resp = await Http.GetAsync(Base + "/releases/latest", ct); }
            catch (OperationCanceledException) { return Mal(r, UpdEstado.SinRed, "cancelado"); }
            catch (Exception ex)
            {
                // HttpClient envuelve lo de verdad en el interior: un TLS que no negocia dice solo
                // "Error al enviar la solicitud." y deja la bitacora sin ninguna pista
                string interior = ex.InnerException != null ? " / " + ex.InnerException.Message : string.Empty;
                Log.Write("actualizacion red: " + ex.Message + interior);
                return Mal(r, UpdEstado.SinRed, ex.Message);
            }

            using (resp)
            {
                // 404 es la respuesta de GitHub cuando el repositorio todavia no tiene ningun release
                if (resp.StatusCode == HttpStatusCode.NotFound) return Mal(r, UpdEstado.SinReleases, "404");
                int code = (int)resp.StatusCode;
                if (code == 403 || code == 429) return Mal(r, UpdEstado.Rechazada, resp.ReasonPhrase);
                if (!resp.IsSuccessStatusCode) return Mal(r, UpdEstado.Error, code + " " + resp.ReasonPhrase);
                try { json = await resp.Content.ReadAsStringAsync(); }
                catch (Exception ex) { return Mal(r, UpdEstado.SinRed, ex.Message); }
            }

            UpdRelease d;
            try { d = Leer<UpdRelease>(json); }
            catch (Exception ex) { return Mal(r, UpdEstado.Error, ex.Message); }
            if (d == null) return Mal(r, UpdEstado.SinReleases, "vacio");

            r.Tag = d.Tag ?? string.Empty;
            r.Titulo = string.IsNullOrEmpty(d.Name) ? r.Tag : d.Name;
            r.PublicadoUtc = FechaUtc(d.Published);
            if (r.PublicadoUtc == DateTime.MinValue) r.PublicadoUtc = FechaUtc(d.Created);

            UpdAsset a = Elegir(d);
            if (a == null || string.IsNullOrEmpty(a.Url)) return Mal(r, UpdEstado.SinReleases, "sin exe");
            if (!DescargaPermitida(a.Url)) return Mal(r, UpdEstado.Error, "la descarga apunta a otro sitio");
            if (r.PublicadoUtc == DateTime.MinValue) return Mal(r, UpdEstado.Error, "sin fecha de publicacion");

            r.Url = a.Url;
            r.Bytes = a.Size;
            r.Estado = MasNuevo(r.PublicadoUtc) ? UpdEstado.Disponible : UpdEstado.Actualizada;
            Log.Write("actualizacion: \"" + r.Titulo + "\" publicado " +
                      r.PublicadoUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) +
                      " UTC, " + r.Mb + " -> " + (r.HayNovedad ? "hay novedad" : "ya esta puesta"));
            return r;
        }

        // El release puede arrastrar el instalador, el codigo fuente y mil cosas mas: lo que interesa es
        // el ejecutable con el que trabaja la barra, y si no se llama igual que el nuestro no se cambia.
        static UpdAsset Elegir(UpdRelease d)
        {
            if (d.Assets == null || d.Assets.Count == 0) return null;
            foreach (var a in d.Assets)
                if (a != null && string.Equals(a.Name, "GamesDock.exe", StringComparison.OrdinalIgnoreCase)) return a;
            foreach (var a in d.Assets)
                if (a != null && !string.IsNullOrEmpty(a.Name) &&
                    a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return a;
            return null;
        }

        // Solo se baja de donde viene la respuesta: el host del que hemos leido el json y los dominios de
        // GitHub. Un json ajeno con una url a un servidor raro no hace que la barra vaya a buscar un
        // ejecutable a ciegas. Con el mismo host vale el http, que es como contesta el servidor de
        // pruebas; para cualquier otro sitio se exige https.
        static bool DescargaPermitida(string url)
        {
            Uri u;
            if (!Uri.TryCreate(url, UriKind.Absolute, out u)) return false;
            Uri b;
            if (Uri.TryCreate(Base, UriKind.Absolute, out b) &&
                string.Equals(u.Authority, b.Authority, StringComparison.OrdinalIgnoreCase)) return true;
            if (u.Scheme != Uri.UriSchemeHttps) return false;
            string h = u.Host.ToLowerInvariant();
            return h == "github.com" || h.EndsWith(".github.com") ||
                   h == "objects.githubusercontent.com" || h.EndsWith(".githubusercontent.com");
        }

        static bool MasNuevo(DateTime publicadoUtc)
        {
            DateTime local = FechaLocalUtc();
            if (local == DateTime.MinValue) return true;
            return publicadoUtc > local + Margen;
        }

        // ------------------------------------------------------------------------ paso 2: bajarlo
        // A la carpeta de actualizaciones, nunca encima del exe en marcha: ese archivo esta bloqueado
        // por Windows mientras el proceso vive y el cambio se hace despues, desde fuera.
        public static async Task<UpdInfo> DescargarAsync(UpdInfo info, CancellationToken ct)
        {
            byte[] b;
            try
            {
                var peticion = new HttpRequestMessage(HttpMethod.Get, info.Url);
                var resp = await Http.SendAsync(peticion, ct);
                using (resp)
                {
                    if (!resp.IsSuccessStatusCode)
                        return Mal(info, UpdEstado.DescargaFalla, (int)resp.StatusCode + " " + resp.ReasonPhrase);
                    try { b = await resp.Content.ReadAsByteArrayAsync(); }
                    catch (Exception ex) { return Mal(info, UpdEstado.DescargaFalla, ex.Message); }
                }
            }
            catch (OperationCanceledException) { return Mal(info, UpdEstado.DescargaFalla, "cancelado"); }
            catch (Exception ex)
            {
                Log.Write("actualizacion descarga: " + ex.Message);
                return Mal(info, UpdEstado.DescargaFalla, ex.Message);
            }

            if (b == null || b.Length < 4096) return Mal(info, UpdEstado.DescargaFalla, "respuesta corta");
            // MZ: lo primero que tiene que traer cualquier ejecutable de Windows. Un html de un proxy,
            // una pagina de aviso o un archivo truncado se descartan aqui y no en el proximo arranque.
            if (b[0] != 0x4D || b[1] != 0x5A) return Mal(info, UpdEstado.DescargaFalla, "lo bajado no es un ejecutable");
            if (info.Bytes > 0 && b.LongLength != info.Bytes)
                return Mal(info, UpdEstado.DescargaFalla, "el tamano no cuadra con el anunciado");

            try
            {
                Directory.CreateDirectory(Paths.UpdateDir);
                string nuevo = NuevoExe;
                string tmp = nuevo + ".bajando";
                File.WriteAllBytes(tmp, b);
                if (File.Exists(nuevo)) File.Delete(nuevo);
                File.Move(tmp, nuevo);
                Log.Write("actualizacion: " + b.Length + " bytes guardados en " + Paths.UpdateDir);
            }
            catch (Exception ex)
            {
                Log.Write("actualizacion guardado: " + ex.Message);
                return Mal(info, UpdEstado.DescargaFalla, ex.Message);
            }
            return info;
        }

        static string NuevoExe { get { return Path.Combine(Paths.UpdateDir, "GamesDock.exe"); } }

        // ------------------------------------------------------------------------ paso 3: cambiarlo
        // La barra no puede sustituir su propio exe: Windows no deja escribir encima de un archivo en
        // ejecucion. Lo que si puede es dejar montado el encargo y cerrarse: un .cmd en %TEMP% espera a
        // que el proceso muera, mueve el nuevo encima del viejo, borra la carpeta de descargas, vuelve a
        // arrancar y se borra a si mismo. El usuario no toca ningun archivo.
        //
        // Se escribe en %TEMP% y no en la carpeta del programa porque el .cmd tampoco podria borrarse
        // estando dentro de lo que hay que mover, y porque %TEMP% es lo unico que Windows limpia solo si
        // algo se tuerce.
        public static bool Preparar(out string error)
        {
            error = null;
            string exe = Exe;
            string nuevo = NuevoExe;
            string dir = Paths.UpdateDir;

            if (string.IsNullOrEmpty(exe) || !File.Exists(exe)) { error = "no se sabe que archivo hay que sustituir"; return false; }
            if (!File.Exists(nuevo)) { error = "falta el archivo descargado"; return false; }

            // un .cmd no admite comillas ni signos de porcentaje dentro de una ruta: mejor decirlo antes
            // de cerrar la barra que dejarla cerrada sin poder volver
            string carpeta = Path.GetDirectoryName(exe) ?? string.Empty;
            foreach (string p in new[] { exe, nuevo, dir, carpeta, Path.GetTempPath() })
                if (!RutaValida(p)) { error = "la carpeta de la barra tiene un nombre que el actualizador no sabe manejar"; return false; }

            if (!SePuedeEscribir(carpeta))
            {
                error = "la carpeta del programa no admite escritura (prueba a moverla fuera de Archivos de Programa)";
                return false;
            }

            try
            {
                string bat = Path.Combine(Path.GetTempPath(),
                    "GamesDock-actualizacion-" + Process.GetCurrentProcess().Id + ".cmd");
                File.WriteAllText(bat, Guion(exe, nuevo, dir), new UTF8Encoding(false));
                var psi = new ProcessStartInfo("cmd.exe", "/d /c \"\"" + bat + "\"\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(psi);
                Log.Write("actualizacion: encargo entregado a " + bat);
                return true;
            }
            catch (Exception ex)
            {
                Log.Write("actualizacion lanzador: " + ex.Message);
                error = ex.Message;
                return false;
            }
        }

        // CRLF a proposito: cmd.exe lee el archivo linea a linea y con saltos de linea sueltos se salta
        // etiquetas y deja de funcionar el reintento del move.
        static string Guion(string exe, string nuevo, string dir)
        {
            var sb = new StringBuilder();
            Linea(sb, "@echo off");
            Linea(sb, "setlocal");
            Linea(sb, "set \"NEW=" + nuevo + "\"");
            Linea(sb, "set \"OLD=" + exe + "\"");
            Linea(sb, "set \"DIR=" + dir + "\"");
            Linea(sb, "set \"N=0\"");
            Linea(sb, ":intento");
            Linea(sb, "move /y \"%NEW%\" \"%OLD%\" >nul 2>&1");
            Linea(sb, "if not errorlevel 1 goto limpio");
            Linea(sb, "set /a N=N+1");
            Linea(sb, "if %N% geq 25 goto limpio");
            Linea(sb, "ping -n 2 127.0.0.1 >nul");
            Linea(sb, "goto intento");
            Linea(sb, ":limpio");
            Linea(sb, "rd /s /q \"%DIR%\" >nul 2>&1");
            Linea(sb, "start \"\" \"%OLD%\"");
            Linea(sb, "del \"%~f0\" >nul 2>&1");
            return sb.ToString();
        }

        static void Linea(StringBuilder sb, string s) { sb.Append(s).Append("\r\n"); }

        static bool RutaValida(string p)
        {
            if (string.IsNullOrEmpty(p)) return false;
            return p.IndexOfAny(new[] { '"', '%', '&', '|', '<', '>', '^', '\r', '\n' }) < 0;
        }

        static bool SePuedeEscribir(string dir)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return false;
            string prueba = Path.Combine(dir, "GamesDock-permiso.tmp");
            try
            {
                using (var s = File.Create(prueba)) s.WriteByte(0);
                File.Delete(prueba);
                return true;
            }
            catch { return false; }
        }

        // ------------------------------------------------------------------------ restos de antes
        // Si una descarga se corto a media (un apagado, un cierre brusco), el archivo baja se queda en la
        // carpeta de actualizaciones. Al arrancar ya no hay ningun cambio en curso, asi que se barre:
        // la promesa del actualizador es que no deja rastro.
        public static void LimpiarRestos()
        {
            try
            {
                if (!Directory.Exists(Paths.UpdateDir)) return;
                Directory.Delete(Paths.UpdateDir, true);
                Log.Write("actualizacion: barridos los restos de una descarga anterior");
            }
            catch (Exception ex) { Log.Write("actualizacion limpieza: " + ex.Message); }
        }

        // ------------------------------------------------------------------------ utilidades
        static T Leer<T>(string json)
        {
            var dcs = new DataContractJsonSerializer(typeof(T));
            byte[] raw = new UTF8Encoding(false).GetBytes(json);
            using (var ms = new MemoryStream(raw)) return (T)dcs.ReadObject(ms);
        }

        // GitHub manda las fechas en ISO con Z final; DateTime.Parse las entiende tal cual y las trae a
        // hora universal. Si el campo no es una fecha, se queda en MinValue y el llamador decide.
        static DateTime FechaUtc(string s)
        {
            if (string.IsNullOrEmpty(s)) return DateTime.MinValue;
            try
            {
                return DateTime.Parse(s, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
            }
            catch { return DateTime.MinValue; }
        }

        public static string Tamano(long bytes)
        {
            if (bytes <= 0) return "? MB";
            double mb = bytes / 1048576.0;
            string f = mb >= 10 ? "0.#" : "0.00";
            return mb.ToString(f, CultureInfo.CurrentCulture) + " MB";
        }

        static UpdInfo Mal(UpdInfo r, UpdEstado e, string detalle)
        {
            r.Estado = e;
            r.Detalle = detalle ?? string.Empty;
            Log.Write("actualizacion: " + e + " (" + r.Detalle + ")");
            return r;
        }
    }
}
