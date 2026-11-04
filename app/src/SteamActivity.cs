using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace GamesDock
{
    public sealed class SteamActivityInfo
    {
        public long? Minutes, RecentMinutes;
        public DateTime? LastPlayedUtc;
        public DateTime SnapshotUtc;
        public bool HasData { get { return Minutes.HasValue || LastPlayedUtc.HasValue; } }
    }

    // No Web API, account credentials or persistent copy of personal statistics.
    // Work is requested on hover and serialized; a file snapshot is reused while it is fresh.
    public static class SteamActivity
    {
        static readonly SemaphoreSlim ReaderGate = new SemaphoreSlim(1, 1);
        static readonly object Gate = new object();
        static Dictionary<int, SteamActivityInfo> _cache;
        static string _file;
        static DateTime _stamp, _readAt;
        static int _generation;
        const long SteamIdBase = 76561197960265728L;

        public static void Clear()
        {
            lock (Gate) { _generation++; _cache = null; _file = null; }
        }

        public static Task<SteamActivityInfo> ReadAsync(int appId, CancellationToken token)
        {
            return Task.Run(async delegate
            {
                if (appId <= 0) return null;
                token.ThrowIfCancellationRequested();
                string root = SteamRoot();
                if (string.IsNullOrEmpty(root)) return null;
                uint? active = ActiveUser();
                bool running = false;
                if (!active.HasValue || active.Value == 0)
                {
                    var processes = Process.GetProcessesByName("steam");
                    running = processes.Length > 0;
                    foreach (var process in processes) process.Dispose();
                }
                var account = SelectAccount(root, active, running);
                if (!account.HasValue) return null;
                string file = Path.Combine(root, "userdata", account.Value.ToString(CultureInfo.InvariantCulture), "config", "localconfig.vdf");
                await ReaderGate.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    token.ThrowIfCancellationRequested();
                    var info = new FileInfo(file);
                    if (!info.Exists || info.Length > 16 * 1024 * 1024) return null;
                    if (ActiveUser() != active) return null;
                    DateTime stamp = info.LastWriteTimeUtc; int generation;
                    lock (Gate)
                    {
                        generation = _generation;
                        if (_cache != null && _file == file && _stamp == stamp && DateTime.UtcNow - _readAt < TimeSpan.FromSeconds(30))
                        {
                            SteamActivityInfo cached;
                            return _cache.TryGetValue(appId, out cached) ? cached : null;
                        }
                    }
                    string text = ReadShared(file);
                    var snapshot = ParseLocal(text, stamp, token);
                    token.ThrowIfCancellationRequested();
                    // Do not publish a result for an account which switched during the read.
                    uint? after = ActiveUser();
                    if (after != active) return null;
                    lock (Gate)
                    {
                        if (generation != _generation) return null;
                        _cache = snapshot; _file = file; _stamp = stamp; _readAt = DateTime.UtcNow;
                    }
                    SteamActivityInfo result;
                    return snapshot.TryGetValue(appId, out result) ? result : null;
                }
                catch (IOException) { return null; }
                catch (UnauthorizedAccessException) { return null; }
                finally { ReaderGate.Release(); }
            }, token);
        }

        static string SteamRoot()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                    return key == null ? null : key.GetValue("SteamPath") as string;
            }
            catch { return null; }
        }

        static uint? ActiveUser()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam\ActiveProcess"))
                {
                    object value = key == null ? null : key.GetValue("ActiveUser");
                    if (value is int) return unchecked((uint)(int)value);
                    if (value is long && (long)value >= 0 && (long)value <= uint.MaxValue) return (uint)(long)value;
                }
            }
            catch { }
            return null;
        }

        public static uint? SelectAccount(string root, uint? active, bool steamRunning)
        {
            if (active.HasValue && active.Value > 0)
                return Directory.Exists(Path.Combine(root, "userdata", active.Value.ToString(CultureInfo.InvariantCulture))) ? active : null;
            if (steamRunning) return null;
            string login = Path.Combine(root, "config", "loginusers.vdf");
            try
            {
                var file = new FileInfo(login);
                if (!file.Exists || file.Length > 1024 * 1024) return null;
                var accounts = new HashSet<uint>();
                ReadValues(ReadShared(login), delegate(IList<string> path, string key, string value)
                {
                    ulong id;
                    if (path.Count != 2 || !path[0].Equals("users", StringComparison.OrdinalIgnoreCase) ||
                        !key.Equals("MostRecent", StringComparison.OrdinalIgnoreCase) || value != "1" ||
                        !ulong.TryParse(path[1], out id) || id <= (ulong)SteamIdBase || id > (ulong)SteamIdBase + uint.MaxValue) return;
                    uint account = (uint)(id - (ulong)SteamIdBase);
                    if (Directory.Exists(Path.Combine(root, "userdata", account.ToString(CultureInfo.InvariantCulture)))) accounts.Add(account);
                }, CancellationToken.None);
                if (accounts.Count == 1) foreach (uint account in accounts) return account;
            }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
            return null;
        }

        static string ReadShared(string file)
        {
            using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream, Encoding.UTF8, true)) return reader.ReadToEnd();
        }

        public static Dictionary<int, SteamActivityInfo> ParseLocal(string text, DateTime snapshot, CancellationToken token)
        {
            var games = new Dictionary<int, SteamActivityInfo>();
            ReadValues(text, delegate(IList<string> path, string key, string value)
            {
                int id; long number;
                if (path.Count != 6 || !path[0].Equals("UserLocalConfigStore", StringComparison.OrdinalIgnoreCase) ||
                    !path[1].Equals("Software", StringComparison.OrdinalIgnoreCase) || !path[2].Equals("Valve", StringComparison.OrdinalIgnoreCase) ||
                    !path[3].Equals("Steam", StringComparison.OrdinalIgnoreCase) || !path[4].Equals("apps", StringComparison.OrdinalIgnoreCase) ||
                    !int.TryParse(path[5], out id) || id <= 0 ||
                    !long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out number) || number < 0) return;
                if (!key.Equals("Playtime", StringComparison.OrdinalIgnoreCase) && !key.Equals("Playtime2w", StringComparison.OrdinalIgnoreCase) &&
                    !key.Equals("LastPlayed", StringComparison.OrdinalIgnoreCase)) return;
                SteamActivityInfo game;
                if (!games.TryGetValue(id, out game)) games[id] = game = new SteamActivityInfo { SnapshotUtc = snapshot };
                if (key.Equals("Playtime", StringComparison.OrdinalIgnoreCase) && number <= int.MaxValue) game.Minutes = number;
                else if (key.Equals("Playtime2w", StringComparison.OrdinalIgnoreCase) && number <= int.MaxValue) game.RecentMinutes = number;
                else if (key.Equals("LastPlayed", StringComparison.OrdinalIgnoreCase) && number > 0 && number <= 253402300799)
                {
                    var date = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(number);
                    if (date <= DateTime.UtcNow.AddDays(1)) game.LastPlayedUtc = date;
                }
            }, token);
            return games;
        }

        // KeyValues text is nested. Only the three selected scalar fields are retained, not a copy
        // of the full local configuration (which contains unrelated account preferences).
        static void ReadValues(string text, Action<IList<string>, string, string> scalar, CancellationToken token)
        {
            int offset = 0, count = 0;
            var path = new List<string>();
            while (true)
            {
                if ((count++ & 127) == 0) token.ThrowIfCancellationRequested();
                bool keyQuoted, valueQuoted;
                string key = NextToken(text, ref offset, out keyQuoted);
                if (key == null) break;
                if (!keyQuoted && key == "}") { if (path.Count > 0) path.RemoveAt(path.Count - 1); continue; }
                if (!keyQuoted && key.StartsWith("[", StringComparison.Ordinal)) continue;
                string value = NextToken(text, ref offset, out valueQuoted);
                if (value == null) break;
                if (!valueQuoted && value == "{") path.Add(key);
                else if (valueQuoted || value != "}") scalar(path, key, value);
            }
        }

        static string NextToken(string text, ref int offset, out bool quoted)
        {
            quoted = false;
            while (offset < text.Length)
            {
                char c = text[offset];
                if (char.IsWhiteSpace(c) || c == '\uFEFF') { offset++; continue; }
                if (c == '/' && offset + 1 < text.Length && text[offset + 1] == '/')
                { while (offset < text.Length && text[offset] != '\n') offset++; continue; }
                break;
            }
            if (offset >= text.Length) return null;
            char first = text[offset++];
            if (first == '{' || first == '}') return first.ToString();
            var token = new StringBuilder();
            if (first == '"')
            {
                quoted = true;
                while (offset < text.Length)
                {
                    char c = text[offset++];
                    if (c == '"') break;
                    if (c == '\\' && offset < text.Length)
                    {
                        char escaped = text[offset++];
                        token.Append(escaped == 'n' ? '\n' : escaped == 't' ? '\t' : escaped);
                    }
                    else token.Append(c);
                }
            }
            else
            {
                token.Append(first);
                while (offset < text.Length && !char.IsWhiteSpace(text[offset]) && text[offset] != '{' && text[offset] != '}') token.Append(text[offset++]);
            }
            return token.ToString();
        }
    }
}
