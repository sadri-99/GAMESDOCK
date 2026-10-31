using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace GamesDock
{
    public class ItemSpec
    {
        public string Key;
        public string Name;
        public string Origin;
        public string Kind;
        public string Link;
        public string Target;
        public string Arguments;
        public string Work;
        public string Icon;
        public string IconHint;
        public string AppId;

        // el pin solo existe cuando el usuario tocó algo sobre este elemento (favorito, oculto,
        // renombrado o anadido a mano); el indice en bruto no lo tiene
        public PinDto Pin;
        public bool Fav;
        // solo para los fijados que no salieron del escaneo: manda su posicion de insercion
        public int Orden;

        public string Path => !string.IsNullOrEmpty(Target) ? Target : (Link ?? string.Empty);

        public static ItemSpec FromPin(PinDto p)
        {
            return new ItemSpec
            {
                Key = p.Key,
                Name = p.Name,
                Origin = string.IsNullOrEmpty(p.Origin) ? "custom" : p.Origin,
                Kind = string.IsNullOrEmpty(p.Kind) ? "game" : p.Kind,
                Link = p.Link,
                Target = p.Target,
                Arguments = p.Args,
                Work = p.Work,
                Icon = p.Icon,
                IconHint = !string.IsNullOrEmpty(p.Icon) ? p.Icon
                         : (!string.IsNullOrEmpty(p.Target) ? p.Target : p.Link),
                Pin = p,
                Fav = p.Fav
            };
        }
    }

    // Espejo de Build-Index / Get-VisibleItems: detecta, deduplica por clave, mezcla con los pins y
    // ordena. El escaneo completo cuesta unos 550 ms de disco, asi que solo se relanza al arrancar,
    // al reescanear o al fijar algo nuevo; lo demas (ocultar, reordenar, buscar) trabaja sobre la
    // lista ya construida.
    public class Catalog
    {
        readonly Config _cfg;
        public Config Cfg { get { return _cfg; } }
        public List<ItemSpec> Items = new List<ItemSpec>();

        static readonly Regex NoiseName = new Regex(
            @"(?i)uninstall|remove|desinstal|documentation|readme|licen|support|soporte|manual|prerequisit|prerrequi|directx|vulkan|visual c\+\+|redistribut|dotnet framework|\.net framework|anticheat|easy anti|battle eye|game controller|update tool|dedicated server|server edit");
        static readonly Regex NoiseDir = new Regex(
            @"(?i)\\accessories\\|\\accesorios\\|\\administrative tools\\|\\system tools\\|\\utilidades\\|\\mantenimiento\\|\\windows powershell\\|\\startup\\|\\inicio\\|\\one drive\\|\\onenote\\|\\excel\\|\\word\\|\\powerpoint\\|\\teams\\|\\skype\\|\\navegadores\\|\\tools\\");
        static readonly Regex GameDirRe = new Regex(
            @"(?i)\\steamapps\\common\\|\\epic games\\|\\gog\.com\\|gog galaxy\\games\\|\\jeux\\|\\juegos\\|\\games\\");
        static readonly string[] Markers =
        {
            "steam_api64.dll", "steam_api.dll", "EOSSDK-Win64-Shipping.dll", "GalaxyPeer64.dll",
            "UnityPlayer.dll", "GameAssembly.dll", "upc_rloader.dll", "RGLConfig.ini"
        };
        static readonly Dictionary<string, bool> DirIsGame = new Dictionary<string, bool>();

        public Catalog(Config cfg) { _cfg = cfg; }

        public void Build()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            var steam = FindSteamGames();
            var epic = FindEpicGames();
            var gog = FindGogGames();
            var start = FindLinkApps(new[]
            {
                Environment.ExpandEnvironmentVariables("%APPDATA%\\Microsoft\\Windows\\Start Menu\\Programs"),
                Environment.ExpandEnvironmentVariables("%ProgramData%\\Microsoft\\Windows\\Start Menu\\Programs")
            }, "start", true);
            var desk = FindLinkApps(DesktopDirs(), "desktop", false);

            var specs = new List<ItemSpec>();
            specs.AddRange(steam);
            specs.AddRange(epic);
            specs.AddRange(gog);
            specs.AddRange(desk);
            specs.AddRange(start);

            Log.Write(string.Format("Deteccion: steam={0} epic={1} gog={2} startmenu={3} escritorio={4} antes de dedupe={5}",
                steam.Count, epic.Count, gog.Count, start.Count, desk.Count, specs.Count));

            var byKey = new Dictionary<string, ItemSpec>();
            var keyOrder = new List<string>();
            foreach (var s in specs)
            {
                if (string.IsNullOrEmpty(s.Key)) continue;
                if (!byKey.ContainsKey(s.Key)) { byKey[s.Key] = s; keyOrder.Add(s.Key); }
            }

            var pins = PinIndex();
            var items = new List<ItemSpec>();
            foreach (var k in keyOrder)
            {
                var s = byKey[k];
                PinDto p;
                bool hay = pins.TryGetValue(k, out p);
                if (hay && p.Hidden) continue;
                s.Pin = hay ? p : null;
                s.Fav = hay && p.Fav;
                if (hay && !string.IsNullOrEmpty(p.Name)) s.Name = p.Name;
                items.Add(s);
            }

            int orden = 0;
            foreach (var p in _cfg.Pins)
            {
                if (string.IsNullOrEmpty(p.Key) || byKey.ContainsKey(p.Key)) continue;
                orden++;
                if (p.Hidden) continue;
                bool hayArchivo = (!string.IsNullOrEmpty(p.Link) && File.Exists(p.Link))
                               || (!string.IsNullOrEmpty(p.Target) && File.Exists(p.Target));
                if (!hayArchivo) continue;
                var it = ItemSpec.FromPin(p);
                it.Orden = orden;
                items.Add(it);
            }

            Items = items;
            sw.Stop();
            Log.Write(string.Format("Indice construido: {0} elementos en {1} ms", items.Count, sw.ElapsedMilliseconds));
        }

        public Dictionary<string, PinDto> PinIndex()
        {
            var d = new Dictionary<string, PinDto>();
            foreach (var p in _cfg.Pins) if (!string.IsNullOrEmpty(p.Key) && !d.ContainsKey(p.Key)) d[p.Key] = p;
            return d;
        }

        // lo sacado del dock se quita aqui, no en el indice: rehacer el indice cuesta un escaneo de
        // disco entero, y al soltar fuera hace falta que el tile desaparezca al instante
        public List<ItemSpec> Visible(string section, string query)
        {
            var pool = new List<ItemSpec>();
            var pins = PinIndex();
            foreach (var it in Items)
            {
                bool oculto;
                PinDto p;
                if (pins.TryGetValue(it.Key ?? string.Empty, out p)) { it.Pin = p; it.Fav = p.Fav; oculto = p.Hidden; }
                else oculto = false;
                if (oculto) continue;

                bool pasa;
                switch (section)
                {
                    case "fav": pasa = it.Fav; break;
                    case "games": pasa = it.Kind == "game"; break;
                    case "apps": pasa = it.Kind == "app"; break;
                    default: pasa = true; break;
                }
                if (!pasa) continue;
                if (!string.IsNullOrEmpty(query) &&
                    (it.Name ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;
                pool.Add(it);
            }

            var rank = new Dictionary<string, int>();
            int oi = 0;
            foreach (var k in _cfg.Order)
            {
                if (string.IsNullOrEmpty(k) || !rank.ContainsKey(k)) { rank[k] = oi; oi++; }
            }

            // el orden que eligio el usuario arrastrando manda sobre el resto; lo que no esta en la
            // lista (recien detectados) se coloca detras, por numero de usos y nombre
            pool.Sort((a, b) => OrderKey(a, rank).CompareTo(OrderKey(b, rank)));
            return pool;
        }

        string OrderKey(ItemSpec x, Dictionary<string, int> rank)
        {
            if (x.Key == "NO_MATCH") return "C|zzzz";
            string ks = x.Key ?? string.Empty;
            int r;
            if (rank.TryGetValue(ks, out r)) return "A|" + r.ToString("D6");
            UsageDto u;
            int n = _cfg.Usage.TryGetValue(ks, out u) && u != null ? u.N : 0;
            long inv = Math.Max(0, 999999 - n);
            return "B|" + inv.ToString("D6") + "|" + (x.Name ?? string.Empty).ToLowerInvariant();
        }

        // ------------------------------------------------------------------ Steam
        List<ItemSpec> FindSteamGames()
        {
            var outList = new List<ItemSpec>();
            string root;
            try
            {
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                {
                    if (k == null) return outList;
                    root = k.GetValue("SteamPath") as string;
                }
                if (string.IsNullOrEmpty(root)) return outList;
                root = root.Replace('/', '\\');
                root = new DirectoryInfo(root).FullName;
            }
            catch { return outList; }

            var libs = new List<string> { root };
            string vdf = Path.Combine(root, @"config\libraryfolders.vdf");
            if (File.Exists(vdf))
            {
                foreach (var line in SafeReadLines(vdf))
                {
                    var m = Regex.Match(line, @"""path""\s+""([^""]+)""");
                    if (!m.Success) continue;
                    string cand = m.Groups[1].Value.Replace("\\\\", "\\");
                    if (Directory.Exists(cand) && !libs.Contains(cand)) libs.Add(cand);
                }
            }

            string cache = Path.Combine(root, @"appcache\librarycache");
            foreach (var lib in libs)
            {
                string sa = Path.Combine(lib, "steamapps");
                if (!Directory.Exists(sa)) continue;
                foreach (var f in SafeFiles(sa, "appmanifest_*.acf"))
                {
                    var h = new Dictionary<string, string>();
                    foreach (var line in SafeReadLines(f))
                    {
                        var m = Regex.Match(line, @"^\s*""([^""]+)""\s+""(.*)""\s*$");
                        if (m.Success) h[m.Groups[1].Value.ToLowerInvariant()] = m.Groups[2].Value;
                    }
                    string appid, name, installdir;
                    h.TryGetValue("appid", out appid);
                    h.TryGetValue("name", out name);
                    h.TryGetValue("installdir", out installdir);
                    if (string.IsNullOrEmpty(appid) || string.IsNullOrEmpty(name)) continue;
                    if (Regex.IsMatch(name, @"(?i)dedicated server|source sdk|common redistributables|proton|beta")) continue;

                    string icon = null;
                    foreach (var c in new[] { "library_600x900.jpg", "header.jpg", "logo.png" })
                    {
                        string p = Path.Combine(Path.Combine(cache, appid), c);
                        if (File.Exists(p)) { icon = p; break; }
                    }
                    outList.Add(new ItemSpec
                    {
                        Key = "steam:" + appid,
                        Name = name,
                        Origin = "steam",
                        Kind = "game",
                        Target = Path.Combine(Path.Combine(sa, "common"), installdir ?? string.Empty),
                        Icon = icon,
                        IconHint = Path.Combine(root, "steam.exe"),
                        AppId = appid
                    });
                }
            }
            return outList;
        }

        // ------------------------------------------------------------------ Epic
        // DataContractJsonSerializer rellena estos campos por reflexion al deserializar el manifest de
        // Epic; csc no lo ve y los declararia "nunca asignados" (CS0649).
#pragma warning disable 0649
        [DataContract] class EpicItem
        {
            [DataMember(Name = "AppName")] public string AppName;
            [DataMember(Name = "FriendlyName")] public string FriendlyName;
            [DataMember(Name = "InstallLocation")] public string InstallLocation;
            [DataMember(Name = "LaunchExecutable")] public string LaunchExecutable;
        }

        [DataContract] class EpicEntry
        {
            [DataMember(Name = "AppName")] public string AppName;
            [DataMember(Name = "InstallLocation")] public string InstallLocation;
        }

        [DataContract] class EpicList
        {
            [DataMember(Name = "InstallationList")] public List<EpicEntry> InstallationList;
        }
#pragma warning restore 0649

        List<ItemSpec> FindEpicGames()
        {
            var outList = new List<ItemSpec>();
            var seen = new HashSet<string>();
            string content = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                                          @"Epic\EpicGamesLauncher\Data\Content");

            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            foreach (var d in new[]
            {
                Path.Combine(programData, @"Epic\EpicGamesLauncher\Data\Manifests"),
                Path.Combine(local, @"Epic\EpicGamesLauncher\Data\Manifests")
            })
            {
                foreach (var f in SafeFiles(d, "*.item"))
                {
                    EpicItem j;
                    try { j = FromJson<EpicItem>(File.ReadAllText(f)); }
                    catch { continue; }
                    if (j == null) continue;
                    string app = j.AppName;
                    if (string.IsNullOrEmpty(app) || seen.Contains(app)) continue;
                    if (string.IsNullOrEmpty(j.FriendlyName) &&
                        Regex.IsMatch(app, @"(?i)launcher|prerequisite|redistributable|directx|vulkan|eac|bec|unity|unreal|modding")) continue;
                    string name = !string.IsNullOrEmpty(j.FriendlyName) ? j.FriendlyName : CleanName(app);
                    string loc = string.IsNullOrEmpty(j.InstallLocation) ? null : j.InstallLocation;
                    string exe = string.IsNullOrEmpty(j.LaunchExecutable) ? null : j.LaunchExecutable;
                    string target = loc != null && exe != null ? Path.Combine(loc, exe) : loc;
                    if (string.IsNullOrEmpty(target) || !PathExists(target)) continue;
                    seen.Add(app);
                    outList.Add(new ItemSpec
                    {
                        Key = "epic:" + app,
                        Name = name,
                        Origin = "epic",
                        Kind = "game",
                        Target = target,
                        Work = loc,
                        Icon = FirstIconIn(content, app),
                        IconHint = target
                    });
                }
            }

            foreach (var dat in new[]
            {
                Path.Combine(programData, @"Epic\UnrealEngineLauncher\LauncherInstalled.dat"),
                Path.Combine(local, @"Epic\UnrealEngineLauncher\LauncherInstalled.dat")
            })
            {
                if (!File.Exists(dat)) continue;
                EpicList j;
                try { j = FromJson<EpicList>(File.ReadAllText(dat)); }
                catch { continue; }
                if (j == null || j.InstallationList == null) continue;
                foreach (var e in j.InstallationList)
                {
                    if (e == null || string.IsNullOrEmpty(e.AppName)) continue;
                    string app = e.AppName;
                    if (seen.Contains(app)) continue;
                    string loc = string.IsNullOrEmpty(e.InstallLocation) ? null : e.InstallLocation;
                    if (loc == null || !Directory.Exists(loc)) continue;
                    var exe = BiggestGameExe(loc);
                    if (exe == null) continue;
                    string name = Regex.IsMatch(app, "[a-z]") ? CleanName(app) : app;
                    seen.Add(app);
                    outList.Add(new ItemSpec
                    {
                        Key = "epic:" + app,
                        Name = name,
                        Origin = "epic",
                        Kind = "game",
                        Target = exe.FullName,
                        Work = exe.DirectoryName,
                        Icon = FirstIconIn(content, app),
                        IconHint = exe.FullName
                    });
                }
            }
            return outList;
        }

        static FileInfo BiggestGameExe(string dir)
        {
            FileInfo best = null;
            foreach (var f in SafeFilesRagged(dir, "*.exe", 2))
            {
                if (f.Length <= 300000) continue;
                if (Regex.IsMatch(f.Name, @"(?i)unins|setup|install|crash|redist")) continue;
                if (best == null || f.Length > best.Length) best = f;
            }
            return best;
        }

        static string FirstIconIn(string contentDir, string app)
        {
            if (!Directory.Exists(contentDir)) return null;
            foreach (var f in SafeFiles(contentDir, "*" + app + "*Icon*.png")) return f;
            return null;
        }

        // ------------------------------------------------------------------ GOG
        List<ItemSpec> FindGogGames()
        {
            var outList = new List<ItemSpec>();
            var hives = new[]
            {
                new { Root = Microsoft.Win32.Registry.LocalMachine, Path = @"SOFTWARE\WOW6432Node\GOG.com\Games" },
                new { Root = Microsoft.Win32.Registry.LocalMachine, Path = @"SOFTWARE\GOG.com\Games" },
                new { Root = Microsoft.Win32.Registry.CurrentUser,  Path = @"SOFTWARE\GOG.com\Games" }
            };
            foreach (var hive in hives)
            {
                using (var top = hive.Root.OpenSubKey(hive.Path))
                {
                    if (top == null) continue;
                    foreach (var sub in top.GetSubKeyNames())
                    {
                        try
                        {
                            using (var k = top.OpenSubKey(sub))
                            {
                                if (k == null) continue;
                                string name = FirstOf(k, "gameName", "DisplayName");
                                string exe = k.GetValue("exe") as string;
                                if (string.IsNullOrEmpty(exe))
                                {
                                    string lc = k.GetValue("launchCommand") as string;
                                    if (!string.IsNullOrEmpty(lc))
                                    {
                                        var m = Regex.Match(lc, "^\"([^\"]+)\"");
                                        exe = m.Success ? m.Groups[1].Value : lc.Split(' ')[0];
                                    }
                                }
                                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(exe)) continue;   // DLC sin ejecutable propio
                                if (exe.IndexOf('\\') < 0)
                                {
                                    string baseDir = k.GetValue("path") as string;
                                    if (!string.IsNullOrEmpty(baseDir)) exe = Path.Combine(baseDir, exe);
                                }
                                if (!File.Exists(exe)) continue;
                                string work = FirstOf(k, "workingDir", "working_dir");
                                if (string.IsNullOrEmpty(work)) work = DirOf(exe);
                                outList.Add(new ItemSpec
                                {
                                    Key = "gog:" + sub,
                                    Name = CleanName(name),
                                    Origin = "gog",
                                    Kind = "game",
                                    Target = exe,
                                    Work = work,
                                    IconHint = exe
                                });
                            }
                        }
                        catch { }
                    }
                }
            }
            return outList;
        }

        static string FirstOf(Microsoft.Win32.RegistryKey k, params string[] names)
        {
            foreach (var n in names)
            {
                var v = k.GetValue(n) as string;
                if (!string.IsNullOrEmpty(v)) return v;
            }
            return null;
        }

        // ------------------------------------------------------------------ accesos directos
        List<ItemSpec> FindLinkApps(string[] roots, string origin, bool recurse)
        {
            var outList = new List<ItemSpec>();
            var exts = new[] { ".lnk", ".appref-ms", ".url", ".bat", ".cmd", ".exe" };
            foreach (var r in roots)
            {
                if (string.IsNullOrEmpty(r) || !Directory.Exists(r)) continue;
                foreach (var ext in exts)
                {
                    foreach (var f in recurse ? SafeFilesRagged(r, "*" + ext, -1) : SafeFilesInfo(r, "*" + ext))
                    {
                        try
                        {
                            string name = CleanName(f.Name);
                            if (string.IsNullOrEmpty(name)) continue;
                            if (NoiseDir.IsMatch(f.FullName) || NoiseName.IsMatch(name)) continue;

                            string link = null, target = null, argLine = null, work = null, hint = null;
                            string kind = "app";

                            if (f.Extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase))
                            {
                                var s = ShellLink.Resolve(f.FullName);
                                if (s == null || string.IsNullOrEmpty(s.Target)) continue;
                                link = f.FullName; target = s.Target; argLine = s.Args; work = s.Work;
                                if (Regex.IsMatch(target, @"(?i)^https?://|\.url$")) continue;
                                hint = target;
                                var il = (s.IconRaw ?? string.Empty).Split(',');
                                if (il.Length > 0 && Regex.IsMatch(il[0], @"\.(ico|exe|dll)$", RegexOptions.IgnoreCase) && File.Exists(il[0]))
                                    hint = il[0];
                            }
                            else if (f.Extension.Equals(".url", StringComparison.OrdinalIgnoreCase))
                            {
                                string url = UrlShortcutTarget(f.FullName);
                                if (url == null) continue;
                                var sm = Regex.Match(url, @"(?i)^steam://rungameid/(\d+)");
                                if (sm.Success)
                                {
                                    outList.Add(new ItemSpec
                                    {
                                        Key = "steam:" + sm.Groups[1].Value,
                                        Name = name,
                                        Origin = "steam",
                                        Kind = "game",
                                        Link = f.FullName,
                                        AppId = sm.Groups[1].Value
                                    });
                                    continue;
                                }
                                link = url; target = f.FullName; hint = f.FullName;
                            }
                            else
                            {
                                target = f.FullName; work = f.DirectoryName; hint = f.FullName;
                                if (f.Extension.Equals(".exe", StringComparison.OrdinalIgnoreCase)) link = f.FullName;
                            }

                            var am = argLine != null ? Regex.Match(argLine, @"-appid\s*(\d+)") : null;
                            if (am != null && am.Success)
                            {
                                outList.Add(new ItemSpec
                                {
                                    Key = "steam:" + am.Groups[1].Value,
                                    Name = name,
                                    Origin = "steam",
                                    Kind = "game",
                                    Link = link,
                                    Target = target
                                });
                                continue;
                            }
                            if (link != null && Regex.IsMatch(target, @"(?i)\\steam\.exe$") && argLine != null && argLine.Contains("-exec")) continue;
                            if (origin == "start" && Regex.IsMatch(target, @"(?i)steam\.exe$")) continue;
                            if (IsGameDir(target)) kind = "game";

                            string key = target.ToLowerInvariant() + "|" +
                                         Regex.Replace(argLine ?? string.Empty, @"\s+", " ").Trim().ToLowerInvariant();
                            outList.Add(new ItemSpec
                            {
                                Key = key,
                                Name = name,
                                Origin = origin,
                                Kind = kind,
                                Link = link,
                                Target = target,
                                Arguments = argLine,
                                Work = work,
                                IconHint = hint
                            });
                        }
                        catch { }
                    }
                }
            }
            return outList;
        }

        public static string[] DesktopDirs()
        {
            var d = new List<string>();
            try { Add(d, Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)); } catch { }
            try
            {
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders"))
                {
                    if (k != null) Add(d, Environment.ExpandEnvironmentVariables(k.GetValue("Desktop") as string));
                }
            }
            catch { }
            Add(d, Paths.DeskDir);
            return d.ToArray();
        }

        static void Add(List<string> list, string p)
        {
            if (!string.IsNullOrEmpty(p) && !list.Contains(p)) list.Add(p);
        }

        public static bool IsGameDir(string exePath)
        {
            if (string.IsNullOrEmpty(exePath)) return false;
            if (GameDirRe.IsMatch(exePath)) return true;
            string dir;
            try { dir = DirOf(exePath); } catch { return false; }
            if (string.IsNullOrEmpty(dir)) return false;
            bool cached;
            if (DirIsGame.TryGetValue(dir, out cached)) return cached;

            var probes = new List<string> { dir };
            try
            {
                string parent = DirOf(dir);
                if (!string.IsNullOrEmpty(parent)) probes.Add(parent);
            }
            catch { }
            bool found = false;
            foreach (var p in probes)
            {
                foreach (var m in Markers)
                    if (File.Exists(Path.Combine(p, m))) { found = true; break; }
                if (found) break;
            }
            DirIsGame[dir] = found;
            return found;
        }

        public static string CleanName(string raw)
        {
            string n = Regex.Replace(raw ?? string.Empty, @"\.(lnk|exe|bat|cmd|url|appref-ms)$", string.Empty, RegexOptions.IgnoreCase);
            n = Regex.Replace(n, @" - (Acceso directo|Shortcut|Direct link)$", string.Empty, RegexOptions.IgnoreCase);
            n = Regex.Replace(n, @"\s*\[(GOG\.com|Steam|Epic Games)\]\s*$", string.Empty, RegexOptions.IgnoreCase);
            n = n.Replace("&", string.Empty);
            return n.Trim();
        }

        // ------------------------------------------------------------------ helpers
        static T FromJson<T>(string json)
        {
            var utf = new UTF8Encoding(false);
            byte[] bytes = utf.GetBytes(json);
            var dcs = new DataContractJsonSerializer(typeof(T));
            using (var ms = new MemoryStream(bytes)) return (T)dcs.ReadObject(ms);
        }

        static bool PathExists(string p)
        {
            try { return File.Exists(p) || Directory.Exists(p); } catch { return false; }
        }

        public static string DirOf(string p)
        {
            return System.IO.Path.GetDirectoryName(p);
        }

        // el destino de un acceso .url: la linea URL= del InternetShortcut
        public static string UrlShortcutTarget(string path)
        {
            foreach (var line in SafeReadLines(path))
            {
                var m = Regex.Match(line.Trim(), "^URL=(.+)$", RegexOptions.IgnoreCase);
                if (m.Success) return m.Groups[1].Value;
            }
            return null;
        }

        static IEnumerable<string> SafeReadLines(string f)
        {
            try { return File.ReadAllLines(f); } catch { return new string[0]; }
        }

        static IEnumerable<string> SafeFiles(string dir, string pattern)
        {
            try { return Directory.GetFiles(dir, pattern); } catch { return new string[0]; }
        }

        static IEnumerable<FileInfo> SafeFilesInfo(string dir, string pattern)
        {
            var outList = new List<FileInfo>();
            try { foreach (var f in Directory.GetFiles(dir, pattern)) outList.Add(new FileInfo(f)); }
            catch { }
            return outList;
        }

        // depth < 0 = sin tope: el menu Inicio se recorre entero y el arbol de Epic se corta a dos niveles
        static IEnumerable<FileInfo> SafeFilesRagged(string dir, string pattern, int depth)
        {
            var outList = new List<FileInfo>();
            Walk(dir, pattern, depth, 0, outList);
            return outList;
        }

        static void Walk(string dir, string pattern, int maxDepth, int level, List<FileInfo> acc)
        {
            try
            {
                foreach (var f in Directory.GetFiles(dir, pattern))
                {
                    try { acc.Add(new FileInfo(f)); } catch { }
                }
                if (maxDepth >= 0 && level >= maxDepth) return;
                foreach (var sub in Directory.GetDirectories(dir)) Walk(sub, pattern, maxDepth, level + 1, acc);
            }
            catch { }
        }
    }
}
