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
            Put(d, "CPU al animar · RAM según tema", "CPU while animating · RAM varies by theme");
            Put(d, "Referencia local de los 11 temas animados conservados, con ocho accesos de 64 px y 12 procesadores lógicos: al mover el cursor, aproximadamente 2,6–5,3 % de CPU total. Ventana fuera del área visible; no es un monitor en directo.", "Local reference for the 11 retained animated themes with eight 64 px shortcuts and 12 logical processors: cursor movement used about 2.6–5.3% total CPU. Window outside the visible desktop; this is not a live monitor.");
            Put(d, "La RAM varía con el tema y las cachés. En la medición anterior de los seis temas originales, activar efectos añadió aproximadamente 1–4 MB de memoria privada y hasta 1,2 MB de memoria física. Ese incremento no se ha medido por separado para los doce nuevos. El total también incluye Windows, iconos, catálogo y fichas.", "RAM varies with the theme and caches. In the earlier test of the six original themes, effects added about 1–4 MB private memory and up to 1.2 MB physical memory. That increment has not been measured separately for the twelve new themes. Total memory also includes Windows, icons, catalog and cards.");
            Put(d, "Cyberpunk 2077: circuitos, amarillo ácido, enlace cian y glitch de implantes.", "Cyberpunk 2077: circuits, acid yellow, cyan uplink and implant glitch.");
            Put(d, "Resident Evil: monitor clínico, pulso de estado, vial y marcas de cuarentena.", "Resident Evil: clinical monitor, status pulse, vial and quarantine marks.");


            Put(d, "Metal Gear Solid: enlace Codec, señal táctica, radar y alerta de infiltración.", "Metal Gear Solid: Codec link, tactical signal, radar and infiltration alert.");

            Put(d, "Final Fantasy: cristal facetado, flujo de éter y estela de meteoro.", "Final Fantasy: faceted crystal, aether flow and meteor trail.");
            Put(d, "Diablo: orbes de sangre, círculo de invocación, runas y brasas.", "Diablo: blood orbs, summoning circle, runes and embers.");

            Put(d, "Death Stranding: lluvia de declive, Odradek, enlace quiral y hilos de conexión.", "Death Stranding: timefall, Odradek, chiral link and connection strands.");

            Put(d, "Mostrar ficha del juego al pasar el ratón", "Show game card on hover");
            Put(d, "Mantener el dock al nivel del escritorio", "Keep dock at desktop level");
            Put(d, "Cambios al instante · Clic fuera o Esc para cerrar", "Changes apply instantly · Click outside or Esc to close");
            Put(d, "Clave gratuita: Perfil → Preferencias → API en SteamGridDB. Cambia el arte desde «Iconos de la comunidad» en el menú de cada icono.", "Free key: Profile → Preferences → API on SteamGridDB. Change artwork using Community icons in each icon's menu.");
            Put(d, "{0} aperturas desde GamesDock", "{0} launches from GamesDock");
            Put(d, "sin aperturas desde GamesDock", "no launches from GamesDock");
            Put(d, "Ficha un 15 % más grande", "15% larger card");
            Put(d, "Mostrar mi actividad de Steam", "Show my Steam activity");
            Put(d, "Horas jugadas y última partida de la cuenta activa, desde los datos locales de Steam.", "Playtime and last played for the active account, from local Steam data.");
            Put(d, "Tu actividad en Steam", "Your Steam activity");
            Put(d, "Consultando datos locales...", "Reading local data...");
            Put(d, "Sin datos locales para este juego.", "No local data for this game.");
            Put(d, "Tiempo jugado: {0}", "Playtime: {0}");
            Put(d, "Última partida: {0}", "Last played: {0}");
            Put(d, "Últimas 2 semanas: {0}", "Last 2 weeks: {0}");
            if (id != "en") return d;

            Put(d, "Páginas", "Pages");
            Put(d, "+ Añadir página", "+ Add page");
            Put(d, "Nueva página", "New page");
            Put(d, "Nombre de la página", "Page name");
            Put(d, "Renombrar página", "Rename page");
            Put(d, "Eliminar página", "Delete page");
            Put(d, "Añadir a página", "Add to page");
            Put(d, "Quitar de esta página", "Remove from this page");
            Put(d, "Puedes crear hasta 5 páginas personalizadas.", "You can create up to 5 custom pages.");
            Put(d, "Elige un nombre de hasta 40 caracteres.", "Choose a name up to 40 characters long.");
            Put(d, "Escribe un nombre distinto, de 1 a 40 caracteres.", "Enter a unique name between 1 and 40 characters.");
            Put(d, "¿Eliminar la página «{0}»? Las demás páginas conservarán sus accesos.", "Delete the page “{0}”? Other pages will keep their shortcuts.");
            Put(d, "Arrastra un .lnk, .exe o un enlace steam:// sobre la barra para fijarlo. Arrastra un icono para ordenar esta página; suéltalo fuera para quitarlo solo de aquí. Clic derecho: favoritos, renombrar, iconos de la comunidad o añadir a otra página. Clic central: favorito.",
                "Drop a .lnk, .exe or steam:// link on the bar to add it. Drag an icon to reorder this page; drop it outside to remove it only from here. Right-click for favorites, renaming, community icons or adding to another page. Middle click: favorite.");

            Put(d, "Prueba otro nombre y elige el juego correcto.", "Try another name and choose the right game.");
            Put(d, "Buscar por otro nombre en SteamGridDB", "Search SteamGridDB using another name");
            Put(d, "Buscar", "Search");
            Put(d, "Juego encontrado", "Matching game");
            Put(d, "Fondo claro", "Light background");
            Put(d, "Buscando iconos...", "Searching for icons...");
            Put(d, "Busca iconos para tu juego", "Find icons for your game");
            Put(d, "Prueba con el título original, otra edición o una abreviatura.", "Try the original title, another edition or an abbreviation.");
            Put(d, "Elige un icono para aplicarlo · Esc para cerrar", "Choose an icon to apply it · Esc to close");
            Put(d, "{0} × {1} · Puntuación de la comunidad: {2}", "{0} × {1} · Community score: {2}");
            Put(d, "Icono de {0} × {1}", "{0} × {1} icon");
            Put(d, "Puedes probar otro nombre mientras carga.", "You can try another name while it loads.");
            Put(d, "Elige otra coincidencia si no es el juego que buscas.", "Choose another match if this is not the game you want.");
            Put(d, "Escribe el nombre de un juego.", "Enter a game name.");
            Put(d, "Buscando juegos...", "Searching for games...");
            Put(d, "Sin coincidencias", "No matches");
            Put(d, "Cargando iconos de {0}...", "Loading icons for {0}...");
            Put(d, "{0} iconos · {1}", "{0} icons · {1}");
            Put(d, "No se han encontrado iconos", "No icons found");
            Put(d, "No se pudieron cargar los iconos", "Could not load icons");
            Put(d, "Elige otra coincidencia o prueba con otro nombre.", "Choose another match or try another name.");
            Put(d, "Añade tu clave de SteamGridDB en Ajustes → Iconos de la comunidad.", "Add your SteamGridDB key in Settings → Community icons.");
            Put(d, "SteamGridDB no acepta la clave. Revísala en Ajustes → Iconos de la comunidad.", "SteamGridDB did not accept the key. Check it in Settings → Community icons.");
            Put(d, "No hay conexión con SteamGridDB. Puedes volver a buscar para reintentarlo.", "Cannot connect to SteamGridDB. Search again to retry.");
            Put(d, "SteamGridDB no pudo completar la búsqueda. Inténtalo de nuevo.", "SteamGridDB could not complete the search. Please try again.");
            Put(d, "Vista previa no disponible. Vuelve a buscar para reintentar.", "Preview unavailable. Search again to retry.");
            Put(d, "Miniaturas: {0}/{1} · Esc para cerrar", "Previews: {0}/{1} · Esc to close");
            Put(d, "Algunas vistas previas no se cargaron. Vuelve a buscar para reintentar.", "Some previews could not load. Search again to retry.");

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
            Put(d, "Clásicos", "Classic");
            Put(d, "Animados", "Animated");
            Put(d, "Animaciones de los temas", "Theme animations");
            Put(d, "Alien: detector de movimiento, fósforo y secuencia de esclusa.",
                "Alien: motion tracker, phosphor persistence and airlock sequence.");

            Put(d, "Portal: carga azul/naranja, apertura y transferencia entre portales.",
                "Portal: blue/orange charge, aperture opening and portal transfer.");
            Put(d, "PlayStation: arranque por pasos, paquetes de memoria y LED de escritura.",
                "PlayStation: stepped boot sequence, memory packets and write LED.");
            Put(d, "Mass Effect: lente orbital, compresión gravitatoria y onda de campo.",
                "Mass Effect: orbital lens, gravitational compression and field wave.");
            Put(d, "Matrix: lluvia de código, decodificación y breve fallo de señal.",
                "Matrix: code rain, decoding and a brief signal glitch.");
            Put(d, "Santuario", "Sanctuary");
            Put(d, "Cámara de pruebas", "Test chamber");
            Put(d, "Horizonte", "Horizon");
            Put(d, "Terminal fantasma", "Ghost terminal");
            Put(d, "Restaurar ocultos", "Restore hidden");
            Put(d, "(nada oculto)", "(nothing hidden)");
            Put(d, "Bloquear posición e iconos", "Lock position and icons");
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

            // ------------------------------------------------------------ iconos de la comunidad (SteamGridDB)
            Put(d, "Iconos de la comunidad", "Community icons");
            Put(d, "Elige un icono para {0}", "Choose an icon for {0}");
            Put(d, "clic en el que quieras - Esc para cancelar", "click the one you want - Esc to cancel");
            Put(d, "cargando miniaturas...", "loading thumbnails...");
            Put(d, "Clave de SteamGridDB", "SteamGridDB key");
            Put(d, "Falta la clave de SteamGridDB.", "The SteamGridDB key is missing.");
            Put(d, "Sin clave no se pueden pedir iconos a SteamGridDB.\nCrea una cuenta gratis, saca la clave en Perfil > Preferencias > API y pégala en Ajustes > Iconos de la comunidad.",
                 "Without a key no icons can be requested from SteamGridDB.\nCreate a free account, get the key at Profile > Preferences > API and paste it in Settings > Community icons.");
            Put(d, "SteamGridDB no acepta esa clave.\nCompruébala o crea una nueva en Perfil > Preferencias > API y pégala en Ajustes > Iconos de la comunidad.",
                 "SteamGridDB does not accept that key.\nCheck it or create a new one at Profile > Preferences > API and paste it in Settings > Community icons.");
            Put(d, "SteamGridDB no tiene iconos para ese juego.", "SteamGridDB has no icons for that game.");
            Put(d, "Sin conexión con SteamGridDB.", "No connection to SteamGridDB.");
            Put(d, "Lo descargado no es una imagen válida.", "What was downloaded is not a valid image.");
            Put(d, "SteamGridDB ha respondido mal: {0}", "SteamGridDB replied badly: {0}");
            Put(d, "No se ha podido cambiar el icono.", "The icon could not be changed.");
            Put(d, "Abrir ajustes", "Open settings");
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
            Put(d, "Personaliza tu dock", "Make your dock yours");
            Put(d, "Elige un tema y combina movimiento, forma y tamaño. Los cambios se aplican al instante.",
                "Choose a theme and mix motion, shape and size. Changes apply instantly.");
            Put(d, "Pasa el ratón por la muestra y pulsa un icono para probar su animación.",
                "Hover over the preview and click an icon to try its animation.");
            Put(d, "Elige un tema", "Choose a theme");
            Put(d, "Prueba combinaciones", "Try combinations");
            Put(d, "Probar animación", "Try animation");
            Put(d, "Más ajustes", "More settings");
            Put(d, "Barra y contenido", "Dock and content");
            Put(d, "Posición y qué accesos aparecen.", "Position and which shortcuts appear.");
            Put(d, "Ficha de juegos y nivel de escritorio.", "Game cards and desktop placement.");
            Put(d, "Idioma", "Language");
            Put(d, "Español, inglés o el idioma del sistema.", "Spanish, English or your system language.");
            Put(d, "Tu clave y el arte de SteamGridDB.", "Your key and artwork from SteamGridDB.");
            Put(d, "Inicio con Windows, actualizaciones y archivos.", "Windows startup, updates and files.");
            Put(d, "Ayuda y proyecto", "Help and project");
            Put(d, "Gestos, accesos rápidos y GitHub.", "Gestures, shortcuts and GitHub.");
            Put(d, "CPU al animar · RAM extra baja en prueba", "CPU while animating · low extra RAM in test");
            Put(d, "Consumo", "Resource use");
            Put(d, "Prueba simplificada fuera del área visible, con ocho accesos de 64 px y 12 procesadores lógicos: al mover el cursor por los seis temas, aproximadamente 2–3,4 % de CPU total. Es una referencia, no un monitor en directo.",
                "Simplified off-screen test with eight 64 px shortcuts and 12 logical processors: moving the cursor across the six themes used approximately 2–3.4% total CPU. This is a reference, not a live monitor.");
            Put(d, "Las animaciones tienen un coste medible mientras se mueven: usan CPU y GPU. En reposo no mantienen un bucle de efectos.",
                "Animations have a measurable cost while moving: they use CPU and GPU. At rest, they do not keep an effects loop running.");
            Put(d, "En la prueba local, activar efectos con el cursor quieto añadió aproximadamente 1–4 MB de memoria privada y hasta 1,2 MB de memoria física. La memoria total de GamesDock también incluye Windows, iconos, catálogo y fichas; no corresponde solo a las animaciones.",
                "In the local test, enabling effects with a stationary cursor added approximately 1–4 MB of private memory and up to 1.2 MB of physical memory. Total GamesDock memory also includes Windows, icons, the catalog and game cards; it is not all animation memory.");
            Put(d, "El consumo cambia con el equipo, el tema, la resolución y la interacción. No se ha medido la GPU ni el impacto en los FPS de juegos. Puedes desactivar los efectos; el agrandado es independiente.",
                "Usage varies with hardware, theme, resolution and interaction. GPU usage and game FPS impact have not been measured. You can disable effects; magnification is independent.");
            Put(d, "Cuadrado", "Square");
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
            Put(d, "Agrandar los iconos al pasar el ratón", "Enlarge icons on hover");
            Put(d, "Ficha del juego al pasar el ratón (banner y descripción)", "Game card on hover (banner and description)");
            Put(d, "Vivir solo en el escritorio (nunca por encima de otras ventanas)",
                 "Stay on the desktop (never above other windows)");
            // las dos etiquetas en ingles tenian que ser cortas: eran las que hacian que la rejilla de
            // botones del sistema pidiera una fila mas que en castellano, y esa fila era justo la que
            // sacaba el pie del viewport del panel (la barra de scroll va oculta a proposito)
            Put(d, "Vaciar escritorio al dock", "Shortcuts into the dock");
            Put(d, "Devolver al escritorio", "Shortcuts to the desktop");
            Put(d, "Todo devuelto", "Everything back");
            Put(d, "Reescaneados: {0}", "Rescanned: {0}");
            Put(d, "No se ha podido cambiar el inicio con Windows. Consulta dock.log.", "Could not change Windows startup. Check dock.log.");
            Put(d, "Arranca con Windows: sí", "Start with Windows: yes");
            Put(d, "Arranca con Windows: no", "Start with Windows: no");
            Put(d, "Abrir carpeta de ajustes", "Open settings folder");
            Put(d, "Guardar la clave", "Save the key");
            Put(d, "Clave guardada", "Key saved");
            Put(d, "Clave vaciada", "Key cleared");
            Put(d, "Abrir la página de la clave", "Open the key page");
            Put(d, "Crea una cuenta gratis en steamgriddb.com y saca la clave en Perfil > Preferencias > API. Después, el clic derecho en un icono ofrece «Iconos de la comunidad»: el arte que sube la gente, en vez del icono oficial.",
                 "Create a free account at steamgriddb.com and get the key at Profile > Preferences > API. Right-clicking an icon then offers \"Community icons\": the artwork people upload, instead of the official icon.");
            Put(d, "Arrastra un .lnk, .exe o un enlace steam:// sobre la barra para fijarlo. Arrastra un icono para ordenar la barra; soltarlo fuera del cristal lo quita y vuelve con \"Restaurar ocultos\" del clic derecho. Clic derecho en un icono: fijar, renombrar u ocultar. Clic central: favorito.",
                 "Drop a .lnk, .exe or steam:// link on the bar to pin it. Drag an icon to reorder the bar; dropping it outside the glass hides it, and it comes back from \"Restore hidden\". Right-click an icon to pin, rename or hide it. Middle click: favorite.");
            Put(d, "¿Te gusta la barra? Apoya el proyecto con una estrella o un café.",
                 "Like the bar? Support the project with a star or a coffee.");
            Put(d, "Estrella en GitHub", "Star on GitHub");
            Put(d, "invítame a un café", "Buy me a coffee");

            // ------------------------------------------------------------ actualizaciones
            Put(d, "Buscar actualizaciones", "Check for updates");
            Put(d, "Comprobando...", "Checking...");
            Put(d, "Actualización disponible", "Update available");
            Put(d, "Enter para actualizar - Esc para dejarlo para otra vez",
                 "Enter to update - Esc to leave it for another time");
            Put(d, "Publicada el {0} - ocupa {1}", "Published on {0} - takes {1}");
            Put(d, "Lo que tienes ahora es la compilación del {0}.", "What you are running now is the build from {0}.");
            Put(d, "Se sustituye el programa en su propio sitio: no tienes que guardar ni mover ningún archivo. La barra se cierra un instante y vuelve sola con la versión nueva.",
                 "The program is replaced in its own folder: you do not have to save or move any file. The bar closes for a moment and comes back on its own with the new version.");
            Put(d, "Actualizar", "Update");
            Put(d, "Ahora no", "Not now");
            Put(d, "Descargando...", "Downloading...");
            Put(d, "Cambiando...", "Swapping...");
            Put(d, "No se ha podido bajar la nueva versión: {0}", "The new version could not be downloaded: {0}");
            Put(d, "La actualización no ha podido empezar: {0}", "The update could not start: {0}");
            Put(d, "No hay ninguna versión publicada más nueva que la que estás usando. La última es {0}.",
                 "No published version is newer than the one you are using. The last one is {0}.");
            Put(d, "Todavía no hay ninguna versión publicada en el repositorio.",
                 "There is no published release in the repository yet.");
            Put(d, "Sin conexión con GitHub para comprobar las versiones.",
                 "No connection to GitHub to check the versions.");
            Put(d, "GitHub ha cortado la comprobación por exceso de peticiones. Vuelve a intentarlo dentro de un rato.",
                 "GitHub cut the check short for too many requests. Try again in a while.");
            Put(d, "GitHub ha respondido mal: {0}", "GitHub replied badly: {0}");

            // ------------------------------------------------------------ prompt de nombre
            Put(d, "Nombre en el dock", "Name in the dock");
            Put(d, "Enter para guardar - Esc para cancelar", "Enter to save - Esc to cancel");
            Put(d, "Nombre visible", "Display name");
            Put(d, "Guardar", "Save");

            // ------------------------------------------------------------ avisos de cristal
            Put(d, "Aviso de GamesDock", "GamesDock notice");
            Put(d, "Enter o Esc para cerrar", "Enter or Esc to close");
            Put(d, "Vale", "OK");

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
