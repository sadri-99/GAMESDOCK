using System;
using System.Collections.Generic;

namespace GamesDock
{
    // La clave del tile virtual que representa a un grupo dentro de la fila. No es la clave de un
    // fijado: los miembros siguen siendo pins normales, asi que desarmar la carpeta no pierde nada y un
    // config sin "groups" se comporta exactamente igual que antes.
    public static class Groups
    {
        public static string Key(string id) { return "group:" + (id ?? string.Empty); }

        public static bool IsGroup(ItemSpec it) { return it != null && it.Kind == "group"; }

        public static string NewId() { return Guid.NewGuid().ToString("N").Substring(0, 10); }
    }

    // Operaciones de agrupacion: crear, fusionar, desagrupar y vaciar. Todas terminan en
    // Save + Refresh, que es lo que reconstruye la fila con el mosaico nuevo.
    public partial class Dock
    {
        public GroupDto GroupById(string id)
        {
            if (string.IsNullOrEmpty(id) || Cfg.Groups == null) return null;
            foreach (var g in Cfg.Groups) if (g != null && g.Id == id) return g;
            return null;
        }

        public GroupDto GroupOfKey(string key)
        {
            if (string.IsNullOrEmpty(key) || Cfg.Groups == null) return null;
            foreach (var g in Cfg.Groups)
                if (g != null && g.Members != null && g.Members.Contains(key)) return g;
            return null;
        }

        GroupDto GroupOfSpec(ItemSpec it)
        {
            if (it == null) return null;
            return Groups.IsGroup(it) ? GroupById(it.GroupId) : GroupOfKey(it.Key);
        }

        // lo que hay dentro de un tile a efectos de agrupar: un icono es el mismo; una carpeta, todos
        // sus miembros, incluidos los que la seccion activa no deja ver
        List<string> KeysOf(ItemSpec it)
        {
            var l = new List<string>();
            if (it == null) return l;
            if (Groups.IsGroup(it))
            {
                var g = GroupById(it.GroupId);
                if (g != null && g.Members != null) l.AddRange(g.Members);
                else if (it.Members != null) foreach (var m in it.Members) l.Add(m.Key ?? string.Empty);
            }
            else if (!string.IsNullOrEmpty(it.Key)) l.Add(it.Key);
            return l;
        }

        // los miembros reales (specs), para el contenedor y el mosaico
        public List<ItemSpec> GroupMembers(ItemSpec it)
        {
            var l = new List<ItemSpec>();
            if (it == null) return l;
            var claves = KeysOf(it);
            foreach (var k in claves)
            {
                ItemSpec m = Cat != null ? Cat.Find(k) : null;
                if (m == null && it.Members != null)
                    foreach (var x in it.Members) if (x.Key == k) { m = x; break; }
                if (m != null && !l.Contains(m)) l.Add(m);
            }
            return l;
        }

        // soltar un icono encima de otro. Si el de debajo ya era una carpeta entra en ella; si eran dos
        // carpetas, una se come a la otra.
        public void GroupInto(ItemSpec a, ItemSpec b)
        {
            if (a == null || b == null || ReferenceEquals(a, b)) return;
            var ka = KeysOf(a);
            var kb = KeysOf(b);
            if (ka.Count == 0 || kb.Count == 0) return;

            var ga = GroupOfSpec(a);
            var gb = GroupOfSpec(b);
            var g = ga ?? gb;
            if (g == null)
            {
                g = new GroupDto { Id = Groups.NewId(), Members = new List<string>() };
                Cfg.Groups.Add(g);
            }
            else if (ga != null && gb != null && !ReferenceEquals(ga, gb))
            {
                foreach (var k in gb.Members) if (!g.Members.Contains(k)) g.Members.Add(k);
                Cfg.Groups.Remove(gb);
                Cfg.Order.Remove(Groups.Key(gb.Id));
            }

            // el orden del mosaico: primero lo que ya estaba en el hueco, luego lo que llega arrastrando
            foreach (var k in kb) Metro(g, k);
            foreach (var k in ka) Metro(g, k);

            // los miembros tienen que existir como fijado si no, al cambiar de seccion la carpeta se
            // queda vacia; una carpeta que se arrastra no es un fijado, sus miembros ya lo son
            if (!Groups.IsGroup(a)) EnsurePin(a);
            if (!Groups.IsGroup(b)) EnsurePin(b);
            Log.Write(string.Format("grupo {0}: {1} dentro", g.Id, g.Members.Count));
            Cfg.Save();
            Refresh(false);
            CommitOrder();
            GroupPanel.RefrescarAbierto();
        }

        static void Metro(GroupDto g, string k)
        {
            if (string.IsNullOrEmpty(k) || g.Members.Contains(k)) return;
            g.Members.Add(k);
        }

        public void UnGroup(GroupDto g)
        {
            if (g == null) return;
            Cfg.Groups.Remove(g);
            Cfg.Order.Remove(Groups.Key(g.Id));
            Log.Write("desagrupado: " + (g.Name ?? g.Id));
            Cfg.Save();
            Refresh(false);
            GroupPanel.RefrescarAbierto();
        }

        public void RemoveFromGroup(string key)
        {
            var g = GroupOfKey(key);
            if (g == null) return;
            g.Members.Remove(key);
            // con un solo icono dentro ya no es una carpeta: se disuelve sola y el icono vuelve a la fila
            if (g.Members.Count <= 1) { UnGroup(g); return; }
            Cfg.Save();
            Refresh(false);
            GroupPanel.RefrescarAbierto();
        }

        // sacar una carpeta del dock de un golpe es sacar todo lo que hay dentro
        public void HideGroup(ItemSpec it)
        {
            foreach (var m in GroupMembers(it))
            {
                var p = EnsurePin(m);
                p.Hidden = true;
                p.Fav = false;
            }
            var g = GroupOfSpec(it);
            if (g != null)
            {
                Cfg.Groups.Remove(g);
                Cfg.Order.Remove(Groups.Key(g.Id));
            }
            Cfg.Save();
            Refresh(false);
            GroupPanel.Cerrar();
        }

        public void RenameGroup(ItemSpec it)
        {
            var g = GroupOfSpec(it);
            if (g == null) return;
            string n = Settings.PromptNombre(this, string.IsNullOrEmpty(g.Name) ? it.Name : g.Name);
            if (string.IsNullOrEmpty(n) || n == g.Name) return;
            g.Name = n;
            Cfg.Save();
            Refresh(false);
            GroupPanel.RefrescarAbierto();
        }

        // el contenedor se abre "con lo puesto", sin la cola de goteo de la fila: lo de dentro suele
        // estar ya extraido, y pintar media rejilla con huecos vacíos se vería roto un momento
        public System.Windows.Media.Brush MemberBrush(ItemSpec it)
        {
            string png = Icons.Resolve(it);
            if (png == null) return null;
            it.Icon = png;
            return GetBrush(png);
        }

        public System.Windows.UIElement MemberLetra(ItemSpec it, double lado)
        {
            return Letra(it.Name, lado);
        }

        // nombre del grupo: el que escribio el usuario, o el de lo primero de dentro
        public string GroupTitle(ItemSpec it)
        {
            var g = GroupOfSpec(it);
            if (g != null && !string.IsNullOrEmpty(g.Name)) return g.Name;
            return it != null && !string.IsNullOrEmpty(it.Name) ? it.Name : Lang.T("Grupo");
        }
    }
}
