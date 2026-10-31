using System;
using System.Collections.Generic;
using System.Globalization;

namespace GamesDock
{
    // Traduccion al estilo gettext: la literal en castellano ES la clave. Aqui solo viven las parejas
    // al ingles, asi que una cadena que se quede sin traducir se ve en castellano en vez de en blanco,
    // y anadir un idioma tercero es otro diccionario y una rama mas en T().
    public static class Lang
    {
        public static string Id = "es";

        static readonly Dictionary<string, string> En = Build("en");

        // el agua de la caja de busqueda se compara en seis sitios: si cada uno escribiera su literal,
        // cambiar de idioma dejaria el cuadro sin reconocer como vacio
        public static string Search { get { return T("buscar..."); } }

        // las aguas de TODOS los idiomas: tras cambiar de idioma el cuadro puede seguir luciendo la del
        // anterior, y para la busqueda esta igual de vacio
        static readonly string[] SearchWords = { "buscar...", "search..." };

        public static bool IsSearch(string text)
        {
            if (string.IsNullOrEmpty(text)) return true;
            for (int i = 0; i < SearchWords.Length; i++) if (text == SearchWords[i]) return true;
            return false;
        }

        public static string T(string es)
        {
            if (es == null || Id != "en") return es;
            string v;
            return En.TryGetValue(es, out v) ? v : es;
        }

        public static string F(string es, params object[] args)
        {
            return string.Format(T(es), args);
        }

        // "es", "en" o cualquier otro valor = auto: se mira la cultura de interfaz del usuario
        public static void Apply(string pref)
        {
            if (pref == "es" || pref == "en") { Id = pref; return; }
            string ui;
            try { ui = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName; }
            catch { ui = "es"; }
            Id = ui == "en" ? "en" : "es";
        }

        static Dictionary<string, string> Build(string id)
        {
            var d = new Dictionary<string, string>();
            if (id != "en") return d;

            // ------------------------------------------------------------ bandeja y menus
            Put(d, "Mostrar / ocultar   (Ctrl+Alt+D)", "Show / hide   (Ctrl+Alt+D)");
            Put(d, "Reescanear juegos", "Rescan games");
            Put(d, "Reescanear", "Rescan");
            Put(d, "Ajustes", "Settings");
            Put(d, "Salir", "Quit");
            Put(d, "Buscar...", "Search...");
            Put(d, "Añadir app o juego...", "Add app or game...");
            Put(d, "Qué se ve", "What to show");
            Put(d, "Favoritos", "Favorites");
            Put(d, "Juegos detectados", "Detected games");
            Put(d, "Aplicaciones", "Applications");
            Put(d, "Todo", "Everything");
            Put(d, "Tema", "Theme");
            Put(d, "Restaurar ocultos", "Restore hidden");
            Put(d, "(nada oculto)", "(nothing hidden)");
            Put(d, "Ocultar (Ctrl+Alt+D)", "Hide (Ctrl+Alt+D)");
            Put(d, "Quitar de favoritos", "Remove from favorites");
            Put(d, "Fijar en favoritos", "Pin as favorite");
            Put(d, "Lanzar ahora", "Launch now");
            Put(d, "Abrir carpeta del juego", "Open game folder");
            Put(d, "Renombrar", "Rename");
            Put(d, "Cambiar icono...", "Change icon...");
            Put(d, "Quitar icono personalizado", "Remove custom icon");
            Put(d, "Elige el icono del acceso", "Choose the shortcut icon");
            Put(d, "Imagenes e iconos|*.png;*.jpg;*.jpeg;*.ico|Programas|*.exe;*.dll;*.lnk|Todos|*.*",
                 "Images and icons|*.png;*.jpg;*.jpeg;*.ico|Programs|*.exe;*.dll;*.lnk|All files|*.*");
            Put(d, "Ese archivo no tiene un icono que se pueda extraer:\n{0}",
                 "That file has no icon that can be extracted:\n{0}");
            Put(d, "Quitar del dock", "Remove from dock");
            Put(d, "Ocultar del dock", "Hide from dock");
            Put(d, "Ese juego se lanza por el cliente (Steam/Epic) y no tiene carpeta directa.",
                 "That game starts through the client (Steam/Epic) and has no folder of its own.");

            // ------------------------------------------------------------ avisos
            Put(d, "El acceso directo no tiene destino.", "The shortcut has no target.");
            Put(d, "No se pudo lanzar:\n{0}\n\n{1}", "Could not launch:\n{0}\n\n{1}");
            Put(d, "No se pudo añadir:", "Could not add:");
            Put(d, "No se pudieron añadir:", "Could not add any of these:");
            Put(d, "No se pudo añadir:\n{0}\n\n{1}", "Could not add:\n{0}\n\n{1}");
            Put(d, "No existe el destino: {0}", "Target does not exist: {0}");
            Put(d, "Elige el juego o aplicación", "Choose the game or app");
            Put(d, "Programas y accesos|*.exe;*.lnk;*.bat;*.cmd;*.url|Todos|*.*",
                 "Programs and shortcuts|*.exe;*.lnk;*.bat;*.cmd;*.url|All files|*.*");
            Put(d, "Dock de videojuegos", "Video game dock");

            // ------------------------------------------------------------ pildora
            Put(d, "Clic derecho en el hueco para añadir o configurar.",
                 "Right-click the empty space to add or configure.");
            Put(d, "buscar...", "search...");
            Put(d, "Escritorio", "Desktop");
            Put(d, "Menú Inicio", "Start Menu");
            Put(d, "Añadido a mano", "Added by hand");
            Put(d, "usado {0} veces", "used {0} times");
            Put(d, "sin usar todavía", "never used");

            // ------------------------------------------------------------ ajustes
            Put(d, "Ajustes de GamesDock", "GamesDock settings");
            Put(d, "clic derecho en la barra vacía - Esc para cerrar",
                 "right-click on the empty bar - Esc to close");
            Put(d, "Aspecto", "Appearance");
            Put(d, "Comportamiento", "Behavior");
            Put(d, "Sistema", "System");
            Put(d, "Abajo", "Bottom");
            Put(d, "Arriba", "Top");
            Put(d, "Izquierda", "Left");
            Put(d, "Derecha", "Right");
            Put(d, "Libre", "Free");
            Put(d, "Posición de la barra", "Bar position");
            Put(d, "Tamaño del icono", "Icon size");
            Put(d, "Forma del icono", "Icon shape");
            Put(d, "Por defecto", "Default");
            Put(d, "Esfera", "Sphere");
            Put(d, "Opacidad en reposo", "Idle opacity");
            Put(d, "Tema de la barra", "Bar theme");
            Put(d, "Idioma de la interfaz", "Interface language");
            Put(d, "Juegos", "Games");
            Put(d, "Apps", "Apps");
            Put(d, "Agrandar los iconos al pasar el ratón (efecto Mac)", "Enlarge icons on hover (Mac effect)");
            Put(d, "Vivir solo en el escritorio (nunca por encima de otras ventanas)",
                 "Stay on the desktop (never above other windows)");
            Put(d, "Minimizar todo al lanzar", "Minimize everything when launching");
            Put(d, "Ocultar el dock al lanzar (Ctrl+Alt+D lo trae)",
                 "Hide the dock when launching (Ctrl+Alt+D brings it back)");
            Put(d, "Vaciar escritorio al dock", "Move desktop shortcuts in");
            Put(d, "Devolver al escritorio", "Put them back on the desktop");
            Put(d, "Todo devuelto", "Everything back");
            Put(d, "Reescaneados: {0}", "Rescanned: {0}");
            Put(d, "Arranca con Windows: sí", "Start with Windows: yes");
            Put(d, "Arranca con Windows: no", "Start with Windows: no");
            Put(d, "Abrir carpeta de ajustes", "Open settings folder");
            Put(d, "Arrastra un .lnk, .exe o un enlace steam:// sobre la barra para fijarlo. Arrastra un icono para ordenar la barra; soltarlo fuera del cristal lo quita y vuelve con \"Restaurar ocultos\" del clic derecho. Clic derecho en un icono: fijar, renombrar u ocultar. Clic central: favorito.",
                 "Drop a .lnk, .exe or steam:// link on the bar to pin it. Drag an icon to reorder the bar; dropping it outside the glass hides it, and it comes back from \"Restore hidden\". Right-click an icon to pin, rename or hide it. Middle click: favorite.");

            // ------------------------------------------------------------ prompt de nombre
            Put(d, "Nombre en el dock", "Name in the dock");
            Put(d, "Enter para guardar - Esc para cancelar", "Enter to save - Esc to cancel");
            Put(d, "Nombre visible", "Display name");
            Put(d, "Guardar", "Save");

            // ------------------------------------------------------------ grupos (carpetas de iOS)
            Put(d, "Grupo", "Group");
            Put(d, "Grupo de {0} iconos", "Group of {0} icons");
            Put(d, "Clic para abrirlo", "Click to open it");
            Put(d, "clic en un icono para lanzarlo", "click an icon to launch it");
            Put(d, "Abrir el grupo", "Open group");
            Put(d, "Desagrupar", "Ungroup");
            Put(d, "Quitar del grupo", "Remove from group");

            // ------------------------------------------------------------ absorcion del escritorio
            Put(d, "Los accesos directos del escritorio se moverán a\n{0}\nSe mantienen intactos y puedes devolverlos con un botón.\n\nContinuar?",
                 "The desktop shortcuts will be moved to\n{0}\nThey stay untouched and one button puts them back.\n\nContinue?");
            Put(d, "Vaciar escritorio", "Empty the desktop");
            Put(d, "El escritorio original ya no existe.", "The original desktop no longer exists.");

            // ------------------------------------------------------------ nombres de tema
            Put(d, "Noche", "Night");
            Put(d, "Niebla", "Fog");
            Put(d, "Ónix", "Onyx");
            Put(d, "Neón", "Neon");
            Put(d, "Ámbar", "Amber");
            Put(d, "Bosque", "Forest");
            Put(d, "Anime", "Anime");
            Put(d, "Cyberpunk", "Cyberpunk");
            Put(d, "Ovni", "UFO");
            return d;
        }

        static void Put(Dictionary<string, string> d, string es, string en) { d[es] = en; }
    }
}
