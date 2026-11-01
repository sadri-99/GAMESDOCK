using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GamesDock
{
    // Que "no ha salido bien" tiene muchos motivos y en la barra cada uno se avisa distinto: sin clave
    // hay que pedirla, con clave invalida hay que dirigir a la pagina del desarrollador, y sin red no
    // se le puede pedir nada al usuario. Por eso el estado viaja con la respuesta en lugar de ser una
    // excepcion que el llamador tendria que adivinar.
    public enum SgdEstado { Ok, SinClave, NoEncontrado, ClaveInvalida, SinRed, ImagenInvalida, ApiError }

    public class SgdRespuesta<T>
    {
        public SgdEstado Estado = SgdEstado.Ok;
        public T Datos;
        public string Error;
        public bool EsOk { get { return Estado == SgdEstado.Ok; } }

        internal static SgdRespuesta<T> Mal(SgdEstado e, string msg)
        {
            var r = new SgdRespuesta<T> { Estado = e, Error = msg };
            return r;
        }

        internal static SgdRespuesta<T> Bien(T datos)
        {
            var r = new SgdRespuesta<T> { Estado = SgdEstado.Ok, Datos = datos };
            return r;
        }
    }

    // ----------------------------------------------------------------- lo que devuelve la API
    // SteamGridDB envuelve siempre todo en {"success":true,"data":[...]} y en los fallos
    // {"success":false,"errors":["..."]}. Son los unicos campos que leen estas clases: lo demas del json
    // (author, tags, notes, language...) se ignora, y si algun dia falta, el miembro queda por defecto.
    [DataContract]
    public class SgdLista<T>
    {
        [DataMember(Name = "success")] public bool Success;
        [DataMember(Name = "data")] public List<T> Data;
    }

    [DataContract]
    public class SgdJuego
    {
        [DataMember(Name = "id")] public int Id;
        [DataMember(Name = "name")] public string Name;
    }

    [DataContract]
    public class SgdIcono
    {
        [DataMember(Name = "id")] public int Id;
        // url es el icono entero; thumb es la miniatura, que es lo unico que se carga en el selector
        [DataMember(Name = "url")] public string Url;
        [DataMember(Name = "thumb")] public string Thumb;
        [DataMember(Name = "width")] public int Width;
        [DataMember(Name = "height")] public int Height;
        [DataMember(Name = "score")] public int Score;
    }

    [DataContract]
    public class SgdErrores
    {
        [DataMember(Name = "errors")] public List<string> Errors;
    }

    // Cliente de SteamGridDB API v2. Tres pasos, cada uno su propio metodo async: buscar el juego,
    // listar los iconos de ese juego y descargar uno. Ninguno toca la UI ni conoce el dock, asi que se
    // pueden probar sueltos y los reutiliza cualquiera de los dos caminos (por nombre o por Steam).
    public static class SteamGrid
    {
        // LA base real del API v2. El subdominio api.steamgriddb.com que usaban las antiguas
        // documentaciones ya no resuelve: el API vive colgado del sitio, en /api/v2.
        const string BaseOficial = "https://www.steamgriddb.com/api/v2";

        // Donde se saca la clave: cuenta gratis, despues Perfil > Preferencias > API > Generate API key.
        // Es el unico enlace que la barra abre del servicio, y lo abre el boton del campo de Ajustes.
        public const string PaginaClave = "https://www.steamgriddb.com/profile/preferences/api";

        // GAMESDOCK_SGD_BASE lo pisa unicamente el harness de prueba (_dev/cs-sgd.ps1 levanta un servidor
        // local con el mismo formato de respuestas); en la barra normal siempre es la oficial.
        static readonly string Base = BaseDelEntorno();

        // un solo HttpClient para toda la vida del proceso: abrir uno por peticion deja los sockets en
        // espera (TIME_WAIT) y 20 iconos en el selector serian 20 conexiones de mas
        static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(TechoSegundos) };

        // Veinte segundos era justo, y SteamGridDB no siempre llega a tiempo: medido desde casa, la lista
        // de iconos de un juego contesto a los 21,5 s y la barra lo conto como "sin red" cuando el API
        // estaba respondiendo. El selector ya avisa de que la lista tarda, y mientras tanto la barra sigue
        // viva, asi que aqui aguantar mas cuesta poco y quedarse corto corta una funcion entera.
        public const int TechoSegundos = 45;

        // el agente de usuario lo pide Cloudflare delante de .NET Framework: sin el, alguna ruta responde
        // con una pagina de bloqueo en lugar del json
        const string Agente = "GamesDock/1.0 (+https://github.com/sadri-99/GAMESDOCK)";

        static string BaseDelEntorno()
        {
            string b = Environment.GetEnvironmentVariable("GAMESDOCK_SGD_BASE");
            return string.IsNullOrEmpty(b) ? BaseOficial : b.TrimEnd('/');
        }

        static SteamGrid()
        {
            Http.DefaultRequestHeaders.UserAgent.ParseAdd(Agente);
        }

        // ---------------------------------------------------------------- paso 1: nombre -> id
        // Recibe el nombre del juego tal y como lo muestra la barra, lo limpia de la basura que arrastra
        // un acceso de Windows y saca el primer "id" del json. El autocomplete de SteamGridDB ordena por
        // coincidencia, asi que el primero es el que quiere el usuario en casi todos los casos.
        public static async Task<SgdRespuesta<int>> BuscarJuegoIdAsync(string nombre, string clave, CancellationToken ct)
        {
            string limpio = LimpiarNombre(nombre);
            if (limpio.Length == 0) return SgdRespuesta<int>.Mal(SgdEstado.NoEncontrado, "nombre vacio");

            var bruto = await PedirAsync("/search/autocomplete/" + Uri.EscapeDataString(limpio), clave, ct);
            if (!bruto.EsOk) return SgdRespuesta<int>.Mal(bruto.Estado, bruto.Error);

            SgdLista<SgdJuego> lista;
            try { lista = Leer<SgdLista<SgdJuego>>(bruto.Datos); }
            catch (Exception ex) { return SgdRespuesta<int>.Mal(SgdEstado.ApiError, ex.Message); }

            if (lista == null || !lista.Success || lista.Data == null || lista.Data.Count == 0)
                return SgdRespuesta<int>.Mal(SgdEstado.NoEncontrado, limpio);

            int id = lista.Data[0].Id;
            if (id <= 0) return SgdRespuesta<int>.Mal(SgdEstado.NoEncontrado, limpio);
            Log.Write(string.Format("SteamGridDB: \"{0}\" -> juego {1} ({2})", limpio, id, lista.Data[0].Name));
            return SgdRespuesta<int>.Bien(id);
        }

        // ---------------------------------------------------------------- paso 2: id -> lista de iconos
        // Recibe el id del paso anterior y devuelve la lista de iconos disponibles. El campo "url" de
        // cada entrada es el icono entero que se puede descargar; "thumb" va con el para que el selector
        // pinte miniaturas sin bajarse 60 iconos a resolucion completa.
        public static Task<SgdRespuesta<List<SgdIcono>>> IconosDeJuegoAsync(int id, string clave, CancellationToken ct)
        {
            return IconosDe("/icons/game/" + id, clave, ct);
        }

        // El mismo paso 2 sin pasar por la busqueda: los juegos detectados en Steam ya traen su app id,
        // y por ahi la coincidencia es exacta (nada de "assassins creed" buscando contra un autocomplete).
        public static Task<SgdRespuesta<List<SgdIcono>>> IconosDeSteamAsync(int steamAppId, string clave, CancellationToken ct)
        {
            return IconosDe("/icons/steam/" + steamAppId, clave, ct);
        }

        static async Task<SgdRespuesta<List<SgdIcono>>> IconosDe(string ruta, string clave, CancellationToken ct)
        {
            var bruto = await PedirAsync(ruta, clave, ct);
            if (!bruto.EsOk) return SgdRespuesta<List<SgdIcono>>.Mal(bruto.Estado, bruto.Error);

            SgdLista<SgdIcono> lista;
            try { lista = Leer<SgdLista<SgdIcono>>(bruto.Datos); }
            catch (Exception ex) { return SgdRespuesta<List<SgdIcono>>.Mal(SgdEstado.ApiError, ex.Message); }

            if (lista == null || !lista.Success || lista.Data == null)
                return SgdRespuesta<List<SgdIcono>>.Mal(SgdEstado.NoEncontrado, ruta);

            // el API ya vienen sin un orden garantizado; la puntuacion de la comunidad es lo que pone
            // primero el icono "oficial", que es el que casi todo el mundo quiere en la barra
            var sal = new List<SgdIcono>();
            var vistas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var ic in lista.Data)
            {
                if (ic == null || string.IsNullOrEmpty(ic.Url)) continue;
                if (!vistas.Add(ic.Url)) continue;
                sal.Add(ic);
            }
            sal.Sort(delegate(SgdIcono a, SgdIcono b) { return b.Score.CompareTo(a.Score); });
            if (sal.Count == 0) return SgdRespuesta<List<SgdIcono>>.Mal(SgdEstado.NoEncontrado, ruta);
            if (sal.Count > TopeSelector) sal = sal.GetRange(0, TopeSelector);
            return SgdRespuesta<List<SgdIcono>>.Bien(sal);
        }

        // el selector no es una galeria infinita: 60 miniaturas ya son dos pantallas de scroll y cada
        // una es una peticion al CDN
        public const int TopeSelector = 60;

        // ---------------------------------------------------------------- paso 3: url -> archivo en disco
        // Descarga la imagen y la guarda en el destino. El destino lo decide Icons (la carpeta de la app,
        // creada al arrancar), asi que el usuario nunca ve un cuadro de guardado.
        // Se escribe primero en .tmp y se mueve al final: si la caida es a media descarga no se queda
        // el tile con un png truncado por delante del icono que si funcionaba.
        public static async Task<SgdRespuesta<string>> DescargarIconoAsync(string url, string destino, CancellationToken ct)
        {
            var bytes = await DescargarBytesAsync(url, ct);
            if (!bytes.EsOk) return SgdRespuesta<string>.Mal(bytes.Estado, bytes.Error);

            string ext = ExtencionReal(bytes.Datos);
            if (ext == null) return SgdRespuesta<string>.Mal(SgdEstado.ImagenInvalida, "no es png, jpg ni ico");

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destino));
                string tmp = destino + ".tmp";
                if (ext == ".png")
                {
                    File.WriteAllBytes(tmp, bytes.Datos);
                }
                else
                {
                    // el pipeline de la barra trabaja con png de alta resolucion; un jpg del CDN se
                    // reencodea aqui una vez, no en cada pintado
                    using (var ms = new MemoryStream(bytes.Datos))
                    using (var img = System.Drawing.Image.FromStream(ms))
                        img.Save(tmp, System.Drawing.Imaging.ImageFormat.Png);
                }
                if (File.Exists(destino)) File.Delete(destino);
                File.Move(tmp, destino);
                return SgdRespuesta<string>.Bien(destino);
            }
            catch (Exception ex)
            {
                Log.Write("SteamGridDB guardado: " + ex.Message);
                return SgdRespuesta<string>.Mal(SgdEstado.ImagenInvalida, ex.Message);
            }
        }

        // Las miniaturas y los iconos completos viven en el CDN, que es publico: la cabecera de la clave
        // SOLO se manda cuando la url es del propio API, para no regalar el secreto a otro host.
        public static async Task<SgdRespuesta<byte[]>> DescargarBytesAsync(string url, CancellationToken ct)
        {
            return await DescargarBytesAsync(url, null, ct);
        }

        static async Task<SgdRespuesta<byte[]>> DescargarBytesAsync(string url, string clave, CancellationToken ct)
        {
            var r = new SgdRespuesta<byte[]>();
            Uri u;
            if (!Uri.TryCreate(url, UriKind.Absolute, out u))
                return SgdRespuesta<byte[]>.Mal(SgdEstado.ImagenInvalida, "url mala: " + url);

            var peticion = new HttpRequestMessage(HttpMethod.Get, u);
            if (!string.IsNullOrEmpty(clave) && EsNuestra(u)) peticion.Headers.Authorization = Cabecera(clave);

            HttpResponseMessage resp;
            try { resp = await Http.SendAsync(peticion, ct); }
            catch (OperationCanceledException) { return SgdRespuesta<byte[]>.Mal(SgdEstado.SinRed, "cancelado"); }
            catch (Exception ex) { return SgdRespuesta<byte[]>.Mal(SgdEstado.SinRed, ex.Message); }

            using (resp)
            {
                if (!resp.IsSuccessStatusCode)
                    return SgdRespuesta<byte[]>.Mal(SgdEstado.ApiError, (int)resp.StatusCode + " " + resp.ReasonPhrase);
                try { r.Datos = await resp.Content.ReadAsByteArrayAsync(); }
                catch (Exception ex) { return SgdRespuesta<byte[]>.Mal(SgdEstado.SinRed, ex.Message); }
            }
            if (r.Datos == null || r.Datos.Length < 64)
                return SgdRespuesta<byte[]>.Mal(SgdEstado.ImagenInvalida, "respuesta vacia");
            r.Estado = SgdEstado.Ok;
            return r;
        }

        // png, jpg o ico por cabecera y, si no trae, por las firmas del propio contenido
        static string ExtencionReal(byte[] b)
        {
            if (b == null || b.Length < 4) return null;
            if (b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return ".png";
            if (b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return ".jpg";
            if (b[0] == 0x00 && b[1] == 0x00 && b[2] == 0x01 && b[3] == 0x00) return ".ico";
            return null;
        }

        // ------------------------------------------------------------------ transporte
        static async Task<SgdRespuesta<string>> PedirAsync(string ruta, string clave, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(clave)) return SgdRespuesta<string>.Mal(SgdEstado.SinClave, "sin clave");

            var peticion = new HttpRequestMessage(HttpMethod.Get, Base + ruta);
            peticion.Headers.Authorization = Cabecera(clave);

            HttpResponseMessage resp;
            try { resp = await Http.SendAsync(peticion, ct); }
            catch (OperationCanceledException) { return SgdRespuesta<string>.Mal(SgdEstado.SinRed, "cancelado"); }
            catch (Exception ex)
            {
                Log.Write("SteamGridDB red: " + ex.Message);
                return SgdRespuesta<string>.Mal(SgdEstado.SinRed, ex.Message);
            }

            using (resp)
            {
                string cuerpo = string.Empty;
                try { cuerpo = await resp.Content.ReadAsStringAsync(); } catch { }

                if (resp.StatusCode == HttpStatusCode.Unauthorized || resp.StatusCode == HttpStatusCode.Forbidden)
                    return SgdRespuesta<string>.Mal(SgdEstado.ClaveInvalida, TextoError(cuerpo));
                if (!resp.IsSuccessStatusCode)
                    return SgdRespuesta<string>.Mal(SgdEstado.ApiError, (int)resp.StatusCode + " " + TextoError(cuerpo));
                if (cuerpo.Length == 0 || cuerpo.TrimStart()[0] != '{')
                    return SgdRespuesta<string>.Mal(SgdEstado.ApiError, "respuesta que no es json");
                return SgdRespuesta<string>.Bien(cuerpo);
            }
        }

        static AuthenticationHeaderValue Cabecera(string clave)
        {
            return new AuthenticationHeaderValue("Bearer", clave.Trim());
        }

        // los mensajes de la API van en {"errors":["..."]}; si el cuerpo no es eso, se devuelve tal cual
        // para que el log tenga algo que contar
        static string TextoError(string json)
        {
            if (string.IsNullOrEmpty(json)) return string.Empty;
            try
            {
                var e = Leer<SgdErrores>(json);
                if (e != null && e.Errors != null && e.Errors.Count > 0) return string.Join(", ", e.Errors.ToArray());
            }
            catch { }
            return json.Length > 120 ? json.Substring(0, 120) : json;
        }

        static T Leer<T>(string json)
        {
            var dcs = new DataContractJsonSerializer(typeof(T));
            byte[] raw = new UTF8Encoding(false).GetBytes(json);
            using (var ms = new MemoryStream(raw)) return (T)dcs.ReadObject(ms);
        }

        static bool EsNuestra(Uri u)
        {
            Uri baseOficial;
            if (!Uri.TryCreate(Base, UriKind.Absolute, out baseOficial)) return false;
            return string.Equals(u.Host, baseOficial.Host, StringComparison.OrdinalIgnoreCase);
        }

        // Un acceso de Windows llega con " - Acceso directo", con el [Steam] del escaneo o con la
        // extension del archivo. Nada de eso ayuda a buscar y en el URL solo estorba.
        public static string LimpiarNombre(string crudo)
        {
            string n = (crudo ?? string.Empty).Trim();
            n = System.Text.RegularExpressions.Regex.Replace(n, @"\s+-\s+(Acceso directo|Shortcut|Direct link)$", "",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            n = System.Text.RegularExpressions.Regex.Replace(n, @"\s*\[(GOG\.com|Steam|Epic Games|Ubisoft Connect)\]\s*", " ",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            n = System.Text.RegularExpressions.Regex.Replace(n, @"\.(lnk|exe|bat|cmd|url|appref-ms)$", "",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            n = n.Replace("&", " ").Replace("  ", " ").Trim();
            // un nombre desproporcionado no es un juego: es una ruta o un texto pegado por error
            if (n.Length > 90) n = n.Substring(0, 90).Trim();
            return n;
        }
    }
}
