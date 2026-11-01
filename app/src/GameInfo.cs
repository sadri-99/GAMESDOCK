using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GamesDock
{
    // Lo que la barra sabe de un juego para la ficha de hover. Es un registro pequeno y voluntario: si
    // no hay ficha el tile se comporta como antes (tooltip de texto), por eso viaja junto a la marca de
    // "ya lo busque y no estaba" (Vacia), que es lo que evita que el cursor pida la red en cada pasada.
    [DataContract]
    public class Ficha
    {
        [DataMember(Name = "appid")] public int AppId;
        [DataMember(Name = "nombre")] public string Nombre;
        [DataMember(Name = "sinopsis")] public string Sinopsis;
        [DataMember(Name = "generos")] public string Generos;
        [DataMember(Name = "autores")] public string Autores;
        [DataMember(Name = "lanzamiento")] public string Lanzamiento;
        // urls de banner en orden de preferencia: primero los de la comunidad (850 px de ancho) y al
        // final la carula de la tienda, que es la que siempre existe para un juego de Steam
        [DataMember(Name = "carulas")] public List<string> Carulas;
        // buscado y no habia nada: se guarda igual para no volver a preguntar
        [DataMember(Name = "vacia")] public bool Vacia;

        public Ficha() { Carulas = new List<string>(); }

        public bool TieneAlgo { get { return !Vacia && (!string.IsNullOrEmpty(Nombre) || Carulas.Count > 0); } }
    }

    // ------------------------------------------------------------------ lo que devuelve la tienda
    // appdetails envuelve el resultado bajo la clave del propio appid ({"730":{"success":true,"data":{}}}),
    // que es una clave que cambia en cada juego: se pela el sobre antes de deserializar (Interior). Se pide
    // SIN "filters": con filtros la tienda deja de traer generos y fecha, que son justo la parte que se
    // ensena. Son ~29 KB por juego, una vez en la vida.
    [DataContract]
    public class SteamSobre
    {
        [DataMember(Name = "success")] public bool Success;
        [DataMember(Name = "data")] public SteamDatos Data;
    }

    [DataContract]
    public class SteamDatos
    {
        [DataMember(Name = "name")] public string Name;
        [DataMember(Name = "short_description")] public string Corta;
        [DataMember(Name = "developers")] public List<string> Devs;
        [DataMember(Name = "publishers")] public List<string> Pubs;
        [DataMember(Name = "genres")] public List<SteamGenero> Generos;
        [DataMember(Name = "release_date")] public SteamFecha Fecha;
        [DataMember(Name = "header_image")] public string Cabecera;
    }

    [DataContract]
    public class SteamGenero { [DataMember(Name = "description")] public string Desc; }

    // la tienda ya la devuelve escrita en el idioma pedido ("12 ago, 2015"), asi que no se formatea
    [DataContract]
    public class SteamFecha { [DataMember(Name = "date")] public string Fecha; }

    [DataContract]
    public class SteamBusqueda { [DataMember(Name = "items")] public List<SteamResultado> Items; }

    [DataContract]
    public class SteamResultado
    {
        [DataMember(Name = "id")] public int Id;
        [DataMember(Name = "name")] public string Name;
    }

    // --------------------------------------------------------------------------- el cliente
    // Dos rutas de red, ninguna con cuenta: la tienda de Steam (appid y datos del juego) y el CDN de
    // SteamGridDB (los banners, que solo piden clave en el paso de listar, no al bajar la imagen).
    // Todo lo que sale se guarda en %APPDATA%\GamesDock\cards, asi que un juego se pide una vez y de ahi
    // en adelante la ficha sale sin red y sin disco apenas.
    public static class GameInfo
    {
        const string Tienda = "https://store.steampowered.com/api/";
        const string Agente = "GamesDock/1.0 (+https://github.com/sadri-99/GAMESDOCK)";

        // Doce segundos: la ficha se pinta al pasar el raton por encima, y una tienda que tarde mas que
        // eso ya no es informacion util, es un bloqueo. SteamGrid se paga sus 45 s en el selector.
        const int TechoSegundos = 12;

        static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(TechoSegundos) };
        static readonly Dictionary<string, Ficha> Memo = new Dictionary<string, Ficha>(StringComparer.OrdinalIgnoreCase);
        static readonly HashSet<string> Pidiendo = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        static readonly Random Dado = new Random();

        // Tope de banners en disco: cada uno son ~120 KB y se van eligiendo al azar, asi que la cola de
        // archivos viejos se poda para que la funcion no se coma el cache de los iconos.
        const int TopoBanners = 60;

        public static readonly string Dir = Path.Combine(Paths.Root, "cards");

        // avisa con la clave de la ficha cuando llega datos nuevos (o cuando aterriza un banner). Se
        // lanza en el hilo de la UI porque el unico oyente es la ventana de la ficha.
        public static event Action<string> Listo;

        static GameInfo()
        {
            Http.DefaultRequestHeaders.UserAgent.ParseAdd(Agente);
        }

        // ------------------------------------------------------------------ como se identifica
        public static int AppIdDe(ItemSpec it)
        {
            if (it == null || string.IsNullOrEmpty(it.AppId)) return 0;
            int n;
            return int.TryParse(it.AppId, out n) && n > 0 ? n : 0;
        }

        // El appid cuando la barra ya lo tiene (los detectados en Steam lo traen) y si no el nombre
        // limpio: dos tiles que apuntan al mismo juego comparten ficha y banners.
        public static string Clave(ItemSpec it)
        {
            if (it == null) return null;
            int a = AppIdDe(it);
            if (a > 0) return "steam-" + a;
            string n = SteamGrid.LimpiarNombre(it.Name);
            return n.Length == 0 ? null : "n-" + Md5Hex(n.ToLowerInvariant());
        }

        static string ClaveDeApp(int appid, string nombreLimpio)
        {
            if (appid > 0) return "steam-" + appid;
            return nombreLimpio.Length == 0 ? null : "n-" + Md5Hex(nombreLimpio.ToLowerInvariant());
        }

        // ------------------------------------------------------------------ lectura desde la UI
        // Primero memoria, luego disco. El json de una ficha son unos cientos de bytes leidos una sola
        // vez por juego y sesion, asi que no hace falta diferirlo: el hover tiene que tener algo que
        // pintar en el mismo momento en que aparece.
        public static Ficha Buscar(ItemSpec it)
        {
            string k = Clave(it);
            if (k == null) return null;
            lock (Memo)
            {
                Ficha f;
                if (Memo.TryGetValue(k, out f)) return f;
            }
            Ficha g = Leer(k);
            if (g != null) lock (Memo) Memo[k] = g;
            return g;
        }

        public static Ficha BuscarClave(string clave)
        {
            if (string.IsNullOrEmpty(clave)) return null;
            lock (Memo)
            {
                Ficha f;
                if (Memo.TryGetValue(clave, out f)) return f;
            }
            return Leer(clave);
        }

        // ------------------------------------------------------------------ recogida
        // Fire and forget a proposito: el hover no puede esperar a la red. Si la ficha llega cuando el
        // cursor ya se fue, se guarda igualmente y a la próxima pasada sale al instante.
        public static async void Pedir(ItemSpec it)
        {
            string k = Clave(it);
            if (k == null || Groups.IsGroup(it)) { Log.Write("ficha sin clave: " + it.Name); return; }
            Log.Write("ficha pedir " + k + " (" + it.Name + ")");
            lock (Pidiendo)
            {
                if (Pidiendo.Contains(k)) return;
                Pidiendo.Add(k);
            }
            try
            {
                Ficha f = await RellenarAsync(it, k).ConfigureAwait(false);
                Guardar(k, f);
                lock (Memo) Memo[k] = f;
                Log.Write("ficha ok " + k + " carulas=" + f.Carulas.Count + " vacia=" + f.Vacia);
                Avisar(k);
            }
            catch (Exception ex)
            {
                Log.Write("ficha " + it.Name + ": " + ex.Message);
            }
            finally
            {
                lock (Pidiendo) Pidiendo.Remove(k);
            }
        }

        static async Task<Ficha> RellenarAsync(ItemSpec it, string clave)
        {
            var f = new Ficha();
            string nombre = SteamGrid.LimpiarNombre(it.Name);
            int appid = AppIdDe(it);
            if (appid <= 0) appid = await BuscarAppIdAsync(nombre);

            f.AppId = appid;
            if (appid > 0)
            {
                var s = await PedirDetalleAsync(appid);
                if (s != null && s.Success && s.Data != null)
                {
                    var dd = s.Data;
                    f.Nombre = Texto(dd.Name);
                    f.Sinopsis = Texto(dd.Corta);
                    f.Generos = JuntarGeneros(dd.Generos);
                    f.Autores = JuntarAutores(dd.Devs, dd.Pubs);
                    f.Lanzamiento = dd.Fecha != null ? Texto(dd.Fecha.Fecha) : string.Empty;
                    if (!string.IsNullOrEmpty(dd.Cabecera)) f.Carulas.Add(dd.Cabecera);
                }
            }

            // los banners de la comunidad van delante: son mas grandes y los elige la gente. Sin clave no
            // se pueden ni listar, y entonces queda la carula de la tienda, que también es un banner.
            var carulas = await BannersAsync(appid, nombre);
            if (carulas != null && carulas.Count > 0) f.Carulas.InsertRange(0, carulas);

            f.Vacia = !f.TieneAlgo;
            // el nombre del acceso manda sobre el de la tienda: si el usuario lo retoco, es lo que quiere ver
            if (string.IsNullOrEmpty(f.Nombre)) f.Nombre = it.Name;
            return f;
        }

        // nombre -> appid. storesearch es el unico buscador de la tienda que no pide token y responde con
        // el id; el primero de la lista es el que quiere cualquiera en casi todos los casos.
        // Ojo con el "l": en storesearch SI que cambia el idioma del resultado (con l=es "Counter-Strike
        // 2" sale como "Contragolpe 2"), pero appdetails ignora ese parametro y devuelve la descripcion
        // en el idioma de la cuenta o en ingles. Asi que se busca SIN l, con el nombre tal cual lo muestra
        // la barra, y la ficha se pide en el idioma de la interfaz.
        static async Task<int> BuscarAppIdAsync(string nombre)
        {
            if (nombre.Length == 0) return 0;
            var r = await PedirJsonAsync<SteamBusqueda>(
                Tienda + "storesearch/?term=" + Uri.EscapeDataString(nombre) + "&cc=ES");
            if (r == null || r.Items == null || r.Items.Count == 0) return 0;
            Log.Write(string.Format("ficha: \"{0}\" -> app {1} ({2})", nombre, r.Items[0].Id, r.Items[0].Name));
            return r.Items[0].Id;
        }

        static async Task<List<string>> BannersAsync(int appid, string nombre)
        {
            string clave = ClaveSgd();
            if (clave.Length == 0) return null;

            List<SgdIcono> h = null;
            if (appid > 0)
            {
                var r = await SteamGrid.HeroesDeSteamAsync(appid, clave, CancellationToken.None);
                if (r.EsOk) h = r.Datos;
            }
            if (h == null)
            {
                var b = await SteamGrid.BuscarJuegoIdAsync(nombre, clave, CancellationToken.None);
                if (!b.EsOk) return null;
                var r2 = await SteamGrid.HeroesDeJuegoAsync(b.Datos, clave, CancellationToken.None);
                if (!r2.EsOk) return null;
                h = r2.Datos;
            }

            var sal = new List<string>();
            foreach (var x in h)
            {
                string u = !string.IsNullOrEmpty(x.Thumb) ? x.Thumb : x.Url;
                if (!string.IsNullOrEmpty(u) && !sal.Contains(u)) sal.Add(u);
                if (sal.Count >= TopoBanners) break;
            }
            return sal;
        }

        // La clave de SteamGridDB puede venir del campo de Ajustes o del entorno de prueba; el codigo
        // que decide cual de las dos fuentes manda vive en la barra, asi que se le pregunta alli.
        static string ClaveSgd()
        {
            var d = Dock.Activa;
            string k = d != null ? d.ClaveSgd() : Environment.GetEnvironmentVariable("STEAMGRIDDB_KEY");
            return (k ?? string.Empty).Trim();
        }

        // ------------------------------------------------------------------ banners
        // Se elige uno al azar PERO SOLO entre los que ya estan en disco: si el azar cayera sobre uno sin
        // bajar, la ficha parpadeaba vacia y encima habria que esperar a la red para volver a sortear.
        // La primera pasada de un juego aun no tiene nada bajado, y entonces se lanza una descarga y se
        // devuelve null: la ficha sale con los textos y el dibujo entra en la siguiente pasada.
        public static string ElegirBanner(Ficha f, string clave)
        {
            if (f == null || f.Carulas == null || f.Carulas.Count == 0) return null;

            var listos = new List<string>();
            foreach (string u in f.Carulas)
            {
                if (!IsImageFile(u)) continue;
                string p = Archivo(u);
                if (File.Exists(p)) listos.Add(p);
            }
            lock (Dado)
            {
                if (listos.Count > 0) return listos[Dado.Next(listos.Count)];
                string url = f.Carulas[Dado.Next(f.Carulas.Count)];
                if (IsImageFile(url)) Bajar(url, Archivo(url), clave);
            }
            return null;
        }

        // la lista de carulas puede traer una ruta local de una version anterior; solo se sortean urls
        static bool IsImageFile(string s)
        {
            return !string.IsNullOrEmpty(s) && (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
        }

        static async void Bajar(string url, string destino, string clave)
        {
            try
            {
                var r = await SteamGrid.DescargarBytesAsync(url, CancellationToken.None);
                if (!r.EsOk || r.Datos == null || r.Datos.Length < 64) return;
                Directory.CreateDirectory(Dir);
                string tmp = destino + ".tmp";
                File.WriteAllBytes(tmp, r.Datos);
                if (File.Exists(destino)) File.Delete(destino);
                File.Move(tmp, destino);
                Poda();
                Avisar(clave);
            }
            catch (Exception ex)
            {
                Log.Write("banner: " + ex.Message);
            }
        }

        static string Archivo(string url)
        {
            return Path.Combine(Dir, "b-" + Md5Hex(url) + ".img");
        }

        // el azar hace que la coleccion crezca sin limite; se dejan los ultimos TopoBanners
        static void Poda()
        {
            try
            {
                var fs = new DirectoryInfo(Dir).GetFiles("b-*.img");
                if (fs.Length <= TopoBanners) return;
                Array.Sort(fs, delegate(FileInfo a, FileInfo b) { return a.LastWriteTimeUtc.CompareTo(b.LastWriteTimeUtc); });
                for (int i = 0; i < fs.Length - TopoBanners; i++) fs[i].Delete();
            }
            catch { }
        }

        // ------------------------------------------------------------------ cache en disco
        static string Ruta(string clave) { return Path.Combine(Dir, clave + ".json"); }

        static Ficha Leer(string clave)
        {
            try
            {
                string p = Ruta(clave);
                if (!File.Exists(p)) return null;
                var dcs = new DataContractJsonSerializer(typeof(Ficha));
                using (var fs = File.OpenRead(p)) return (Ficha)dcs.ReadObject(fs);
            }
            catch (Exception ex)
            {
                Log.Write("ficha lectura: " + ex.Message);
                return null;
            }
        }

        static void Guardar(string clave, Ficha f)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                var dcs = new DataContractJsonSerializer(typeof(Ficha));
                using (var ms = new MemoryStream())
                {
                    dcs.WriteObject(ms, f);
                    File.WriteAllBytes(Ruta(clave), ms.ToArray());
                }
            }
            catch (Exception ex)
            {
                Log.Write("ficha guardado: " + ex.Message);
            }
        }

        // ------------------------------------------------------------------ transporte
        static async Task<string> CadenaAsync(string url)
        {
            try { return await Http.GetStringAsync(url).ConfigureAwait(false); }
            catch (Exception ex) { Log.Write("tienda: " + ex.Message); return null; }
        }

        static async Task<T> PedirJsonAsync<T>(string url)
        {
            string cuerpo = await CadenaAsync(url).ConfigureAwait(false);
            return DeJson<T>(cuerpo);
        }

        static T DeJson<T>(string cuerpo)
        {
            try
            {
                if (string.IsNullOrEmpty(cuerpo)) return default(T);
                char primero = cuerpo.TrimStart()[0];
                if (primero != '{' && primero != '[') return default(T);
                var dcs = new DataContractJsonSerializer(typeof(T));
                byte[] raw = new UTF8Encoding(false).GetBytes(cuerpo);
                using (var ms = new MemoryStream(raw)) return (T)dcs.ReadObject(ms);
            }
            catch (Exception ex)
            {
                Log.Write("json " + typeof(T).Name + ": " + ex.Message);
                return default(T);
            }
        }

        // appdetails envuelve el resultado bajo la clave del propio appid ({"221100":{...}}) y esa clave
        // es distinta en cada juego, asi que no se puede declarar como contrato. El serializador de
        // contratos NO sabe leer ese objeto como Dictionary<string,...>: medido en _dev/cs-ficha-json.ps1
        // devuelve el diccionario VACIO y sin avisar, que es como la ficha se quedaba sin generos ni
        // sinopsis. Se pela el sobre a mano (un solo miembro, siempre) y el contenido ya es un contrato
        // normal y corriente.
        static async Task<SteamSobre> PedirDetalleAsync(int appid)
        {
            string cuerpo = await CadenaAsync(Tienda + "appdetails?appids=" + appid + "&l=" + LangSteam())
                .ConfigureAwait(false);
            return DeJson<SteamSobre>(Interior(cuerpo));
        }

        static string Interior(string cuerpo)
        {
            if (string.IsNullOrEmpty(cuerpo)) return null;
            string s = cuerpo.Trim();
            if (s.Length < 4 || s[0] != '{' || s[s.Length - 1] != '}') return null;
            s = s.Substring(1, s.Length - 2);
            int dp = s.IndexOf(':');
            return dp < 0 ? null : s.Substring(dp + 1).Trim();
        }

        static void Avisar(string clave)
        {
            var h = Listo;
            if (h == null) return;
            var app = System.Windows.Application.Current;
            if (app == null) { h(clave); return; }
            app.Dispatcher.BeginInvoke(new Action(delegate { h(clave); }));
        }

        // ------------------------------------------------------------------ detalle util
        static string LangSteam()
        {
            // appdetails NO acepta el codigo de dos letras: medido con _dev/cs-ficha-net.ps1, con l=es
            // sigue dando "Action / 21 Aug, 2012" y con l=spanish da "Acci6n / 21 AGO 2012". Quiere el
            // nombre del idioma tal y como lo llama la propia tienda.
            return Lang.Id == "en" ? "english" : "spanish";
        }

        // los textos de la tienda llegan con entidades (&trade;, &quot;) y a veces con etiquetas sueltas
        static string Texto(string crudo)
        {
            if (string.IsNullOrEmpty(crudo)) return string.Empty;
            string s = WebUtility.HtmlDecode(crudo);
            s = s.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
            while (s.IndexOf("  ") >= 0) s = s.Replace("  ", " ");
            return s.Trim();
        }

        static string JuntarGeneros(List<SteamGenero> gs)
        {
            if (gs == null || gs.Count == 0) return string.Empty;
            var sal = new List<string>();
            foreach (var g in gs)
            {
                if (g == null || string.IsNullOrEmpty(g.Desc)) continue;
                string d = Texto(g.Desc);
                if (d.Length > 0 && !sal.Contains(d)) sal.Add(d);
                if (sal.Count >= 3) break;
            }
            return string.Join(", ", sal.ToArray());
        }

        static string JuntarAutores(List<string> devs, List<string> pubs)
        {
            if (devs != null && devs.Count > 0) return Texto(devs[0]);
            if (pubs != null && pubs.Count > 0) return Texto(pubs[0]);
            return string.Empty;
        }

        static string Md5Hex(string s)
        {
            byte[] hash;
            using (var md5 = MD5.Create()) hash = md5.ComputeHash(Encoding.UTF8.GetBytes(s ?? string.Empty));
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
