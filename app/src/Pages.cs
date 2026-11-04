using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace GamesDock
{
    [DataContract]
    public sealed class PageDto
    {
        [DataMember(Name = "id")] public string Id;
        [DataMember(Name = "name")] public string Name;
        [DataMember(Name = "members")] public List<string> Members = new List<string>();
        [DataMember(Name = "order")] public List<string> Order = new List<string>();
        [DataMember(Name = "groups")] public List<GroupDto> Groups = new List<GroupDto>();
    }

    // Membership, arrangement and folders belong to a page. Pins describe the application itself.
    public static class PageStore
    {
        public const int MaxCustomPages = 5;
        public static bool IsFixed(string id) { return id == "fav" || id == "games" || id == "apps"; }
        public static string Label(PageDto page) { return IsFixed(page.Id) ? Lang.T(page.Name) : page.Name; }

        public static void Normalize(Config cfg)
        {
            if (cfg.Pages == null) cfg.Pages = new List<PageDto>();
            if (cfg.KnownPageItems == null) cfg.KnownPageItems = new List<string>();
            var existing = new Dictionary<string, PageDto>(StringComparer.Ordinal);
            foreach (var page in cfg.Pages)
            {
                if (page == null || string.IsNullOrEmpty(page.Id) || existing.ContainsKey(page.Id)) continue;
                if (page.Members == null) page.Members = new List<string>();
                if (page.Order == null) page.Order = new List<string>();
                if (page.Groups == null) page.Groups = new List<GroupDto>();
                existing.Add(page.Id, page);
            }
            var pages = new List<PageDto>();
            foreach (var entry in new[] { new[] { "fav", "Favoritos" }, new[] { "games", "Juegos" }, new[] { "apps", "Aplicaciones" } })
            {
                PageDto page;
                if (!existing.TryGetValue(entry[0], out page)) page = new PageDto { Id = entry[0] };
                page.Name = entry[1]; pages.Add(page); existing.Remove(entry[0]);
            }
            foreach (var page in cfg.Pages)
                if (page != null && !string.IsNullOrEmpty(page.Id) && existing.ContainsKey(page.Id) && page.Id.StartsWith("page:", StringComparison.Ordinal))
                { pages.Add(page); existing.Remove(page.Id); }
            cfg.Pages = pages;
            if (Find(cfg, cfg.Section) == null) cfg.Section = "games";
        }

        public static PageDto Find(Config cfg, string id)
        {
            if (cfg.Pages != null) foreach (var page in cfg.Pages) if (page != null && page.Id == id) return page;
            return null;
        }

        public static PageDto Active(Config cfg)
        {
            var page = Find(cfg, cfg.Section);
            if (page == null) { Normalize(cfg); page = Find(cfg, cfg.Section); }
            return page;
        }

        public static int CustomCount(Config cfg)
        {
            Normalize(cfg); return cfg.Pages.Count - 3;
        }

        public static PageDto Create(Config cfg, string name)
        {
            Normalize(cfg);
            name = (name ?? string.Empty).Trim();
            if (name.Length == 0 || name.Length > 40 || CustomCount(cfg) >= MaxCustomPages) return null;
            foreach (var page in cfg.Pages)
                if (string.Equals(Label(page), name, StringComparison.OrdinalIgnoreCase)) return null;
            var added = new PageDto { Id = "page:" + Guid.NewGuid().ToString("N"), Name = name };
            cfg.Pages.Add(added); return added;
        }

        public static bool Rename(Config cfg, string id, string name)
        {
            var page = Find(cfg, id); name = (name ?? string.Empty).Trim();
            if (page == null || IsFixed(id) || name.Length == 0 || name.Length > 40) return false;
            foreach (var other in cfg.Pages)
                if (other != page && string.Equals(Label(other), name, StringComparison.OrdinalIgnoreCase)) return false;
            page.Name = name; return true;
        }

        public static bool Delete(Config cfg, string id)
        {
            var page = Find(cfg, id);
            if (page == null || IsFixed(id)) return false;
            cfg.Pages.Remove(page);
            if (cfg.Section == id) cfg.Section = "games";
            return true;
        }

        public static bool Add(Config cfg, string pageId, string key)
        {
            Normalize(cfg); var page = Find(cfg, pageId);
            if (page == null || string.IsNullOrEmpty(key)) return false;
            if (!cfg.KnownPageItems.Contains(key)) cfg.KnownPageItems.Add(key);
            if (page.Members.Contains(key)) return false;
            page.Members.Add(key);
            if (!page.Order.Contains(key)) page.Order.Add(key);
            if (pageId == "fav") foreach (var pin in cfg.Pins) if (pin.Key == key) pin.Fav = true;
            return true;
        }

        public static bool Remove(Config cfg, string pageId, string key)
        {
            var page = Find(cfg, pageId);
            if (page == null || !page.Members.Remove(key)) return false;
            page.Order.RemoveAll(k => k == key);
            for (int i = page.Groups.Count - 1; i >= 0; i--)
            {
                var group = page.Groups[i];
                if (group == null || group.Members == null) { page.Groups.RemoveAt(i); continue; }
                group.Members.RemoveAll(k => k == key);
                if (group.Members.Count < 2) { page.Order.Remove(Groups.Key(group.Id)); page.Groups.RemoveAt(i); }
            }
            if (pageId == "fav") foreach (var pin in cfg.Pins) if (pin.Key == key) pin.Fav = false;
            return true;
        }

        public static bool IsFavorite(Config cfg, string key)
        {
            var favorites = Find(cfg, "fav"); return favorites != null && favorites.Members.Contains(key);
        }

        public static bool Synchronize(Config cfg, IEnumerable<ItemSpec> items)
        {
            Normalize(cfg);
            bool initial = !cfg.PagesInitialized, changed = initial;
            var known = new HashSet<string>(cfg.KnownPageItems, StringComparer.Ordinal);
            var names = new Dictionary<string, string>(StringComparer.Ordinal);
            var pins = new Dictionary<string, PinDto>(StringComparer.Ordinal);
            foreach (var pin in cfg.Pins) if (pin != null && !string.IsNullOrEmpty(pin.Key)) pins[pin.Key] = pin;
            if (initial)
                foreach (var pin in cfg.Pins)
                    if (pin != null && pin.Fav && !pin.Hidden && !string.IsNullOrEmpty(pin.Key))
                    {
                        var favorites = Find(cfg, "fav");
                        if (!favorites.Members.Contains(pin.Key)) favorites.Members.Add(pin.Key);
                    }
            foreach (var item in items)
            {
                if (item == null || string.IsNullOrEmpty(item.Key)) continue;
                names[item.Key] = item.Name ?? string.Empty;
                if (!known.Add(item.Key)) continue;
                cfg.KnownPageItems.Add(item.Key); changed = true;
                PinDto pin; bool hidden = pins.TryGetValue(item.Key, out pin) && pin.Hidden;
                if (hidden || (!initial && item.Origin == "custom")) continue;
                var target = Find(cfg, item.Kind == "game" ? "games" : "apps");
                if (!target.Members.Contains(item.Key)) target.Members.Add(item.Key);
                if (!initial && !target.Order.Contains(item.Key)) target.Order.Add(item.Key);
            }
            if (initial)
            {
                foreach (var page in cfg.Pages)
                {
                    page.Order = cfg.Order == null ? new List<string>() : new List<string>(cfg.Order);
                    var remaining = page.Members.FindAll(key => !page.Order.Contains(key));
                    remaining.Sort(delegate(string a, string b)
                    {
                        UsageDto ua, ub;
                        int na = cfg.Usage.TryGetValue(a, out ua) && ua != null ? ua.N : 0;
                        int nb = cfg.Usage.TryGetValue(b, out ub) && ub != null ? ub.N : 0;
                        if (na != nb) return nb.CompareTo(na);
                        string nameA, nameB;
                        names.TryGetValue(a, out nameA); names.TryGetValue(b, out nameB);
                        return string.Compare(nameA ?? a, nameB ?? b, StringComparison.OrdinalIgnoreCase);
                    });
                    page.Order.AddRange(remaining);
                    if (cfg.Groups == null) continue;
                    foreach (var group in cfg.Groups)
                    {
                        if (group == null || group.Members == null) continue;
                        var members = group.Members.FindAll(page.Members.Contains);
                        if (members.Count >= 2) page.Groups.Add(new GroupDto { Id = group.Id, Name = group.Name, Members = members });
                    }
                }
                cfg.PagesInitialized = true;
            }
            return changed;
        }
    }
}
