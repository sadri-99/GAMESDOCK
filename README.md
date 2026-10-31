# GamesDock

Píldora translúcida al nivel del escritorio, para Windows, que sustituye a los accesos directos de
la mesa de trabajo: lanza videojuegos y cualquier aplicación, detecta sola los juegos que ya tienes
instalados y se aparta cuando no la usas.

El producto completo es **un único `.exe`** (`app/GamesDock.exe`, ≈ 432 KiB): sin instalador, sin
servicios, sin dependencias externas y sin usar la red.

## Qué hace

- **Detecta los juegos sin que muevas un dedo**: Steam (biblioteca y atajos), Epic Games (manifiestos
  de instalación), GOG, el menú Inicio —del usuario y de todos los usuarios— y los accesos del
  escritorio. Deduplica por destino y clasifica cada entrada como juego o aplicación.
- **Acepta lo que tú le des**: suelta un `.exe`, un `.lnk`, un `.url` o un enlace `steam://` sobre la
  barra y queda fijado, con su icono real extraído en alta resolución y cacheado en disco.
- **Magnificado estilo macOS** al pasar el ratón, con caída suave hacia los vecinos y la píldora
  creciendo al ritmo del icono agrandado.
- **Se comporta como parte del escritorio**: ventana `WS_EX_NOACTIVATE` colocada al fondo de la
  z-order, de modo que cualquier ventana —incluso a pantalla completa— la tapa y nunca le roba el foco
  a lo que estés usando.
- **Se oculta al lanzar** (y vuelve con el atajo de teclado), minimiza todo al lanzar si lo pides y
  deja un icono en la bandeja del sistema como acceso cuando está oculta.
- **Absorbe el escritorio**: un botón mueve los accesos de la mesa de trabajo a la barra y otro los
  devuelve exactamente a donde estaban.
- Instancia única por mutex nominal, estado en un único `config.json` y bitácora autolimitada.

## Cómo se maneja

| Gesto | Resultado |
| --- | --- |
| `Ctrl` + `Alt` + `D` | Mostrar / ocultar la barra |
| Doble clic en el icono de la bandeja | Mostrar / ocultar la barra |
| Clic derecho en el hueco libre de la píldora | Menú: Buscar…, Añadir app o juego…, Qué se ve, Tema, Restaurar ocultos, Reescanear, Ajustes, Ocultar, Salir |
| Clic derecho sobre un icono | Favorito, Lanzar ahora, Abrir carpeta del juego, Renombrar, Quitar/Ocultar del dock |
| Clic central sobre un icono | Marcar o desmarcar como favorito |
| Arrastrar un archivo hasta la píldora | Fijarlo en la barra |
| Arrastrar un icono dentro de la barra | Reordenar la fila |
| Soltar un icono fuera del cristal | Ocultarlo; vuelve desde «Restaurar ocultos» |

No hay cuadro de búsqueda, filtros ni engranajes a la vista: la barra es una píldora limpia y todas
esas opciones entran por el menú del clic derecho o por la ventana de Ajustes.

## Qué se puede personalizar

- **Posición**: abajo, arriba, izquierda, derecha o libre en cualquier punto de la pantalla.
- **Tamaño del icono**: 28 – 76 px. **Holgura de la píldora**: 2 – 26 px.
- **Opacidad en reposo**: 10 – 100 %, con la barra recuperando opacidad completa al hover.
- **Sección visible**: Favoritos, Juegos detectados, Aplicaciones o Todo.
- **Nueve temas**: Noche, Niebla, Onix, Neón, Ámbar, Bosque, Anime (blanco y rosa), Cyberpunk (rojo,
  negro y azul) y Ovni (verde y amarillo).
- **Idioma de la interfaz**: automático —el del sistema—, español o inglés. Se cambia en caliente
  desde los ajustes: menús, bandeja y ventana de ajustes se reescriben sin reiniciar.
- **Comportamiento**: magnificado, vivir solo en el escritorio, minimizar todo al lanzar, ocultar al
  lanzar y arrancar con Windows.

## Dónde guarda sus cosas

Se queda íntegramente en el perfil de usuario. El registro solo se lee, nunca se escribe.

| Ruta | Contenido |
| --- | --- |
| `%APPDATA%\GamesDock\config.json` | Ajustes —incluido el idioma—, fijados, orden, favoritos, usos y mapa de accesos movidos |
| `%APPDATA%\GamesDock\dock.log` | Bitácora de arranque, detección y errores; se vacía al superar 256 KiB |
| `%APPDATA%\GamesDock\icons\` | Caché de iconos en PNG de 256 px |
| `%APPDATA%\GamesDock\escritorio\` | Copias de los accesos movidos desde el escritorio |
| `%APPDATA%\GamesDock\GamesDock.lanzador.vbs` | Solo si activas «Arranca con Windows» |
| `%STARTUP%\GamesDock.lnk` | El acceso de arranque, que ejecuta el `.vbs` anterior con la ventana oculta |

Borrar `%APPDATA%\GamesDock` deja la aplicación como recién instalada; lo único que puede quedar es
el acceso de `%STARTUP%` si activaste el arranque con Windows.

## Compilar desde el código

Requisitos: Windows 10 u 11 x64 con **.NET Framework 4.8** (ya viene con el sistema) y las **Build
Tools de Visual Studio 2022 o superior**, que son las que aportan el compilador Roslyn. No hace falta
el SDK de .NET ni los *targeting packs*.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File app\build.ps1
```

`build.ps1` invoca a `csc.exe` directamente —no hay proyecto MSBuild— y produce
`app/GamesDock.exe`. Detalles que conviene saber antes de tocar el build:

- Se referencian los ensamblajes de WPF del propio runtime
  (`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\*.dll`) y `System.Xaml` de la raíz de esa
  carpeta. Esa asimetría es real, no una errata.
- **Nunca** uses `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`: es el compilador *legacy*,
  se detiene en C# 5 y rechaza parte del código.
- Los dos ficheros XAML y el icono se empotran como recursos administrados del ensamblaje y se parsean
  en tiempo de ejecución, así que no hay BAML ni generador de marcado.
- El icono se incluye dos veces: como `RT_GROUP_ICON` del PE (`-win32icon`, para el explorador) y como
  recurso gestionado (para la bandeja, que pide una variante pequeña).
- La compilación sale con **cero avisos**.

## Estructura del repositorio

```
app/
  build.ps1          compilación de un solo fichero (la única herramienta necesaria)
  app.manifest       DPI PerMonitorV2 y asInvoker: se ejecuta sin elevación
  icon.ico           icono del programa, con todas sus variantes de tamaño
  src/               18 ficheros .cs, todos en el namespace GamesDock
    Program.cs       arranque, instancia única, cableado de la ventana principal
    Foundation.cs    rutas, bitácora y recorte de memoria
    Config.cs        modelo de configuración y su persistencia JSON
    Lang.cs          tabla de traducción es → en y selección de idioma
    Catalog.cs       detección e índice de juegos y aplicaciones
    Dock.cs          ventana píldora: tiles, temas y pintado
    Dock.Interact.cs hover, arrastre, menús contextuales y soltar archivos
    Springs.cs       motor de muelles sincronizado con el compositor
    Layout.cs        toda la geometría de la píldora
    PillGeometry.cs  construcción del contorno de la píldora
    Ghost.cs         icono flotante que sigue al cursor durante el arrastre
    Icons.cs         extracción y caché de iconos
    Themes.cs        definición de los nueve temas
    Tray.cs          bandeja del sistema y atajo global
    Settings.cs      ventana de ajustes y absorción del escritorio
    Shell.cs         lanzar, minimizar todo, accesos .lnk y arranque con Windows
    Native.cs        P/Invoke justos (hotkey y working set)
    XamlAssets.cs    carga de los XAML empotrados
  xaml/              diccionario de estilos y ventana, parseados en runtime
  GamesDock.exe      binario compilado (se versiona a propósito: es el producto)
LICENSE, README.md, .gitattributes, .gitignore
```

Todos los ficheros de `src/` comparten el namespace `GamesDock` y se compilan con
`-langversion:latest` contra .NET Framework 4.8.

## Cómo está hecho

- **Nada de animaciones por temporizador.** Los muelles se integran contra el reloj del compositor:
  el motor engancha `CompositionTarget.Rendering` mientras algo se está moviendo y lo desengancha al
  asentarse. Por eso en reposo el proceso no ejecuta ni un cuadro de animación.
- **Medido en la máquina de desarrollo** (1920 × 1080 al 125 %, 12 iconos fijados, índice de 68
  elementos): CPU en reposo entre 0,01 % y 0,2 % normalizado a 12 hilos, con el recuento de handles
  estable y sin crecimiento de memoria tras dejar la barra tranquila. El *working set* ronda los
  17 MB en reposo prolongado y sube a ~67 MB justo después de interactuar, cuando la caché de iconos
  está caliente. Durante el barrido de hover el consumo no pasa de ~2,3 %.
- **El índice se construye en segundo plano**, con prioridad `Background` del despachador: la píldora
  se pinta antes de que termine el recorrido del registro y del menú Inicio.
- **Nivel de escritorio, no hijo del `WorkerW`.** La barra es una ventana de nivel superior
  `WS_EX_TOOLWINDOW` + `WS_EX_NOACTIVATE`, colocada en `HWND_BOTTOM` tras cada `Show()`. La otra
  opción —re-parentarla al `WorkerW`/`Progman` del escritorio con `SetParent`— quedó probada y
  descartada: la ventana con `AllowsTransparency` dejaba de pintar y de recibir clics.
- **La config respeta el esquema de las versiones anteriores**, así que una instalación existente sigue
  funcionando sin migración; los campos ausentes conservan su valor por defecto.
- **El idioma es una tabla, no recursos satélites.** La literal en castellano es la clave de búsqueda y
  `Lang.cs` solo guarda las parejas al inglés, así que una cadena sin traducir se ve en castellano en
  vez de en blanco. Los textos se resuelven al construir el control; cambiar de idioma en caliente
  descarta los menús memorizados y reconstruye la fila.
- **Un tema son dieciséis brochas**, todas con alfa: cristal de arriba y abajo, contorno, tres brillos,
  hover, pulsado, insignia, letra, los cuatro colores del tooltip y los dos del campo de texto. Añadir
  un tema es añadir una entrada en `Themes.cs`; no hay código de pintado nuevo.

## Limitaciones conocidas

- Solo Windows; no hay versión para otros sistemas.
- El atajo está fijo en `Ctrl` + `Alt` + `D` y solo se puede encender o apagar, no reasignarlo.
- El acceso de arranque se lanza a través de `wscript.exe`, así que su icono en la carpeta de inicio
  es el de WScript, no el de GamesDock.
- Algunos accesos `.url` de Steam no exponen un icono decodificable: la entrada se fija y se lanza
  igual, y el fallo queda anotado en `dock.log`.
- No hay firma de código ni instalador: el binario se distribuye tal cual.

## Privacidad

GamesDock no abre conexiones de red, no envía telemetría y no analiza nada fuera del perfil del
usuario. Los iconos y la lista de juegos se quedan en tu carpeta de `%APPDATA%`.

## Licencia

MIT. Ver el fichero [`LICENSE`](LICENSE). Los iconos de los juegos y aplicaciones que la barra muestra
son propiedad de sus respectivos autores; GamesDock solo los lee del sistema para pintarlos.

## In English

GamesDock is a single small Windows `.exe` that puts a translucent macOS-style pill at desktop level —
behind your windows, never stealing focus. It auto-detects installed games (Steam, Epic, GOG, Start
Menu, desktop shortcuts), lets you pin anything by dropping it on the bar, magnifies on hover with
spring physics driven by the compositor clock, and idles at essentially zero CPU. The interface is
bilingual (Spanish and English, switching live) and there are nine color themes, including Anime,
Cyberpunk and UFO. No installer, no network, no telemetry: settings, icon cache and log live in
`%APPDATA%\GamesDock`. MIT licensed.
