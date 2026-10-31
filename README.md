<p align="center">
  <img src="docs/logo.png" width="148" height="148" alt="Logotipo de GamesDock: las letras D y G entrelazadas en negro y blanco, con un arco amarillo">
</p>

# GamesDock

Píldora translúcida al nivel del escritorio, para Windows, que sustituye a los accesos directos de
la mesa de trabajo: lanza videojuegos y cualquier aplicación, detecta sola los juegos que ya tienes
instalados y se aparta cuando no la usas.

El producto completo es **un único `.exe`** (`app/GamesDock.exe`, ≈ 456 KiB): sin instalador, sin
servicios, sin dependencias externas y sin usar la red.

![La píldora en reposo, al nivel del escritorio](docs/screenshots/dock-reposo.png)

## Capturas

| | |
| --- | --- |
| ![Magnificado al pasar el ratón, con el tooltip del juego](docs/screenshots/dock-hover.png) | **Magnificado estilo macOS.** El icono bajo el cursor crece y sus vecinos lo hacen de forma decreciente; la píldora se estira con él. El tooltip dice de dónde salió el acceso, cuántas veces se ha lanzado y dónde está. |
| ![Menú del clic derecho](docs/screenshots/dock-menu.png) | **Todo por el clic derecho.** Buscar, añadir, elegir qué se ve, el tema, restaurar ocultos, reescanear y ajustes —en las dos puntas vacías de la barra, no en los iconos—. Sin cuadros ni engranajes a la vista. |
| ![La carpeta en la barra: un mosaico con los iconos de dentro](docs/screenshots/dock-carpeta.png) | **Carpetas como en iOS.** Se suelta un icono encima de otro y en ese hueco queda un mosaico con lo que hay dentro —aquí, Cyberpunk 2077 y The Witcher 3—, no un dibujo de carpeta. |
| ![El contenedor abierto, con el título del grupo y el nombre de cada icono](docs/screenshots/carpeta-abierta.png) | **Al pincharla sale el contenedor**, pegado a la barra y centrado en su hueco: el título del grupo arriba —que solo se ve ahí—, los iconos con su nombre debajo, y un clic en cualquiera lo lanza y cierra el contenedor. |
| ![La misma fila de la barra con las tres formas de icono: cuadrada, iOS y esfera](docs/screenshots/dock-formas.png) | **La forma del icono.** La misma fila, la misma config y los mismos PNG recortados de tres maneras: cuadrada por defecto, cuadrado suavizado tipo iOS, o esfera. Se cambia en los ajustes y se aplica a todo, carpetas incluidas. |
| ![Ajustes en español](docs/screenshots/ajustes.png) | **Ajustes.** Posición, tamaño y forma del icono, opacidad en reposo, los nueve temas con su muestra de color y el idioma de la interfaz. |
| ![Ajustes en inglés](docs/screenshots/ajustes-en.png) | **Y en inglés**, cambiándolo en caliente: la ventana se reescribe sin reiniciar la barra. |

## Qué hace

- **Detecta los juegos sin que muevas un dedo**: Steam (biblioteca y atajos), Epic Games (manifiestos
  de instalación), GOG, el menú Inicio —del usuario y de todos los usuarios— y los accesos del
  escritorio. Deduplica por destino y clasifica cada entrada como juego o aplicación.
- **Pone la cara de cada juego**: el arte de Steam se busca en `appcache\librarycache`, y como el
  cliente nuevo lo guarda en subcarpetas con nombre-hash, la búsqueda recorre ese nivel y puntúa los
  ficheros por nombre en vez de exigir la ruta literal.
- **Acepta lo que tú le des**: suelta un `.exe`, un `.lnk`, un `.url` o un enlace `steam://` sobre la
  barra y queda fijado, con su icono real extraído en alta resolución y cacheado en disco. Y si el que
  trae no te gusta, «Cambiar icono…» coge cualquier PNG, JPG, ICO o EXE del disco.
- **Carpetas como en iOS**: suelta un icono encima de otro y se convierten en una carpeta. En la barra
  es un mosaico con los iconos de dentro —se redibuja cada vez que cambia el contenido—, y al pincharla
  sale un contenedor con esos iconos y sus nombres. Su título solo se ve ahí dentro; en la barra manda
  el mosaico.
- **Magnificado estilo macOS** al pasar el ratón, con caída suave hacia los vecinos y la píldora
  creciendo al ritmo del icono agrandado. Los iconos de las puntas se agrandan enteros: el cristal se
  recorta en el momento, no por un margen fijo que los cortaba.
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
| Clic derecho sobre un icono | Favorito, Lanzar ahora, Abrir carpeta del juego, Renombrar, Cambiar icono…, Quitar/Ocultar del dock |
| Clic derecho sobre una carpeta | Abrir el grupo, Renombrar, Desagrupar, Ocultar del dock |
| Clic fuera del menú desplegado | Cierra el menú sin activar lo que haya debajo |
| Clic central sobre un icono | Marcar o desmarcar como favorito |
| Arrastrar un archivo hasta la píldora | Fijarlo en la barra |
| Arrastrar un icono dentro de la barra | Reordenar la fila |
| Soltar un icono encima de otro | Crece una carpeta con los dos dentro |
| Clic en la carpeta | Se abre el contenedor con los iconos de dentro y sus nombres |
| Clic o `Esc` fuera del contenedor | Lo cierra, sin lanzar nada |
| Clic en un icono del contenedor | Lo lanza y cierra el contenedor |
| Soltar un icono fuera del cristal | Ocultarlo; vuelve desde «Restaurar ocultos» |

No hay cuadro de búsqueda, filtros ni engranajes a la vista: la barra es una píldora limpia y todas
esas opciones entran por el menú del clic derecho o por la ventana de Ajustes.

El desplegable se cierra con un clic en cualquier punto de la pantalla que no sea el propio menú —el
escritorio, otro icono, otra ventana—, igual que cualquier menú de Windows. Y ese clic no llega a la
ventana que hay debajo: se retira para que no dispare nada por accidente.

Mientras hay una ventana de por medio —Ajustes, el cuadro de renombrar, un aviso— la pantalla se cubre
con un velo tenue (negro al 22 %) que se come todos los clics de fuera: imposible pinchar un icono o
otra ventana mientras el programa espera una respuesta. El velo no roba el foco —el cuadro de texto
sigue escribiendo— y se quita solo cuando se cierra el emergente, sea con Guardar, Enter, `Esc` o la
cruz. El contenedor de una carpeta no es modal: se cierra pinchando fuera o con `Esc`.

## Qué se puede personalizar

- **Posición**: abajo, arriba, izquierda, derecha o libre en cualquier punto de la pantalla.
- **Tamaño del icono**: 28 – 76 px.
- **Forma del icono**: por defecto (el cuadrado de siempre), iOS (cuadrado con la esquina suavizada)
  o esfera. Es solo el recorte con el que se pinta: el PNG no se sustituye ni se reescala, y las
  carpetas entran en el mismo molde que las apps.
- **Opacidad en reposo**: 10 – 100 %, con la barra recuperando opacidad completa al hover.
- **Sección visible**: Favoritos, Juegos detectados, Aplicaciones o Todo.
- **Icono propio**: cualquier fijado puede llevar un PNG, JPG, ICO o EXE que elijas, y se vuelve al
  original con «Quitar icono personalizado».
- **Carpetas**: su título —el que escribas, o el de lo primero de dentro— solo se ve dentro del
  contenedor; el mosaico de la barra se compone solo con lo que haya dentro en cada momento.
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
| `%APPDATA%\GamesDock\config.json` | Ajustes —incluido el idioma—, fijados, orden, favoritos, usos, grupos y mapa de accesos movidos |
| `%APPDATA%\GamesDock\dock.log` | Bitácora de arranque, detección y errores; se vacía al superar 256 KiB |
| `%APPDATA%\GamesDock\icons\` | Caché de iconos en PNG de 256 px |
| `%APPDATA%\GamesDock\icons\custom\` | Copia del icono propio de cada fijado, con nombre-hash; si borras el original, el tile no se queda vacío |
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
  src/               22 ficheros .cs, todos en el namespace GamesDock
    Program.cs       arranque, instancia única, cableado de la ventana principal
    Foundation.cs    rutas, bitácora y recorte de memoria
    Config.cs        modelo de configuración y su persistencia JSON
    Lang.cs          tabla de traducción es → en y selección de idioma
    Catalog.cs       detección e índice de juegos y aplicaciones
    Dock.cs          ventana píldora: tiles, temas y pintado
    Dock.Interact.cs hover, arrastre, menús contextuales y soltar archivos
    Groups.cs        agrupar, fusionar, desagrupar y vaciar carpetas
    GroupPanel.cs    el contenedor que se abre al pinchar una carpeta
    MenuGuard.cs     que el desplegable se cierre al pinchar fuera
    Veil.cs          velo a pantalla completa mientras hay un emergente abierto
    Springs.cs       motor de muelles sincronizado con el compositor
    Layout.cs        toda la geometría de la píldora
    PillGeometry.cs  construcción del contorno de la píldora
    Ghost.cs         icono flotante que sigue al cursor durante el arrastre
    Icons.cs         extracción y caché de iconos, incluidos los propios
    Themes.cs        definición de los nueve temas
    Tray.cs          bandeja del sistema y atajo global
    Settings.cs      ventana de ajustes y absorción del escritorio
    Shell.cs         lanzar, minimizar todo, accesos .lnk y arranque con Windows
    Native.cs        P/Invoke justos (atajo global, working set, gancho de ratón y primer plano)
    XamlAssets.cs    carga de los XAML empotrados
  xaml/              diccionario de estilos y ventana, parseados en runtime
  GamesDock.exe      binario compilado (se versiona a propósito: es el producto)
docs/
  logo.png           el logotipo de arriba, cortado del fotograma PNG de 256 px que lleva icon.ico
  screenshots/       las capturas de arriba, tomadas de la barra real
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
  funcionando sin migración; los campos ausentes conservan su valor por defecto. Los grupos son una
  clave nueva y opcional: un `config.json` sin `groups` se comporta exactamente igual que antes.
- **Una carpeta no es un fijado nuevo: es un tile virtual.** En `order` entra la clave `group:<id>` —y
  si no está, la carpeta se sienta directamente en el hueco de su primer miembro—, mientras los
  miembros siguen siendo pins normales, cada uno con sus propios datos. Por eso desagrupar no pierde
  nada —los iconos vuelven a la fila donde estaban— y por eso el mosaico puede redibujarse en cada
  cambio sin tocar el catálogo. El mosaico se compone en `Groups.cs`, que es el único sitio donde se
  mutan los grupos; toda operación termina en `Save + Refresh`, así que lo que se ve en la barra es
  siempre lo que hay dentro en ese momento.
- **La forma del icono es un recorte, no un icono nuevo.** `Layout.cs` guarda las tres curvas
  (`TileRadius`, `BadgeRadius`, `FolderRadius`) a partir de `config.iconShape`, y el PNG se sigue
  cargando entero: cambiar de forma no toca ni un archivo de `icons\`. Con «esfera» el radio se manda
  a tope y WPF lo ajusta solo a media casilla. El detalle que no era evidente: WPF recorta el
  `Background` de un `Border` a su `CornerRadius`, pero **no recorta a su hijo** —por eso el mosaico de
  una carpeta se redondea celda a celda (`MosaicRadius`, que pone el radio en la esquina de fuera de
  cada celda) en vez de confiar en el borde contenedor, y por eso no se usó un `Clip` geométrico: un
  clip de geometría manda el tile a la vía lenta de render justo cuando el magnificado lo está moviendo
  cuadro a cuadro—.
- **Dos valores que dejaron de ser ajuste.** La holgura de la píldora queda fija en 4 px
  (`Layout.PadFix`): era el punto donde la fila se ve asentada sin que el cristal coma halo, y su
  efecto apenas se apreciaba. Y una config escrita por versiones anteriores puede traer `pillPad`: se
  ignora al leer y deja de escribirse en el primer guardado, sin migración. `iconShape` es opcional
  igual que `groups` —cualquier valor que no sea `ios` o `esfera` vale tanto como no tenerlo: el
  cuadrado de siempre—.
- **Agrupar y reordenar se distinguen por una banda de tres zonas** alrededor del icono vecino, medida
  sobre su centro real (`Dock.Interact.cs`): ±mitad del icono → se marca la carpeta; el hueco entre ese
  límite y el borde del slot → zona muerta, no hace nada, que es lo que antes convertía cualquier roce
  en un reordenamiento; pasado el centro → se mueve el slot. Las tres rutas están cubiertas por el
  arnés `_dev/cs-orden.ps1`.
- **El contenedor de la carpeta no es modal** —se cierra pinchando fuera o con `Esc`—, pero pide el
  primer plano al abrirse. El clic que lo saca recae en la píldora, que es `WS_EX_NOACTIVATE` y nunca
  pierde la activación, así que Windows no le soltaba el foco al panel y el `Esc` se quedaba en la
  ventana de atrás. Antes de cerrarse solo, mira si hay un menú o el velo del renombrar por medio.
- **El recorte del extremo se arregló con geometría, no con un margen mayor.** El `ScrollViewer` de la
  fila recorta justo en el borde del cristal, que es donde el icono del extremo más se agranda; la
  ventana del viewport sale `MagOverrun` hacia fuera y la `Strip` vuelve a entrar esa misma distancia,
  así que el primer icono sigue empezando a `RowInset` del borde pero puede desbordarse. Se hace solo
  cuando la fila cabe entera: si desborda y se puede desplazar, recortar en el borde es lo correcto.
- **El velo es una ventana `Topmost` + `WS_EX_NOACTIVATE` + `WS_EX_TOOLWINDOW`** que tapiza todo el
  escritorio virtual y se come el ratón en sus tres eventos de vista previa. No roba el foco —si lo
  hiciera, el cuadro de renombrar dejaría de escribir a media frase— y su `Push`/`Pop` es un contador
  de profundidad, porque los diálogos se anidan (el explorador que abre «Cambiar icono…» nace con el
  velo ya puesto). Como no sabemos qué ventanas nuestras caerán por debajo, un temporizador de 150 ms
  las sube ocho intentos y se apaga; con el velo quitado, cada una vuelve a su banda normal. Sin velo
  el proceso no tiene ni ventana de velo ni temporizador.
- **El icono propio se copia, no se referencia.** `Icons.InstallCustom` guarda un PNG de 256 px en
  `icons\custom` con nombre derivado de la clave del fijado, así que borrar o mover el archivo original
  no deja el tile vacío; si lo que se eligió era un binario, se guarda la extracción y no una copia del
  ejecutable entero.
- **El idioma es una tabla, no recursos satélites.** La literal en castellano es la clave de búsqueda y
  `Lang.cs` solo guarda las parejas al inglés, así que una cadena sin traducir se ve en castellano en
  vez de en blanco. Los textos se resuelven al construir el control; cambiar de idioma en caliente
  descarta los menús memorizados y reconstruye la fila.
- **Un tema son dieciséis brochas**, todas con alfa: cristal de arriba y abajo, contorno, tres brillos,
  hover, pulsado, insignia, letra, los cuatro colores del tooltip y los dos del campo de texto. Añadir
  un tema es añadir una entrada en `Themes.cs`; no hay código de pintado nuevo.
- **El arte de Steam se busca por nombre, no por ruta.** `appcache\librarycache\<appid>` tiene ahora
  las imágenes dentro de subcarpetas con nombre-hash y a veces con otro fichero —el 292030 no guarda
  `library_600x900.jpg`, sino `dd1c19c4…/library_capsule.jpg`—, así que se recorre un nivel de
  subcarpetas, se puntúa por orden de preferencia y, si nada es reconocible, se coge la imagen más
  pesada descartando el desenfoque del *hero*. Exigir la ruta literal daba media biblioteca en el logo
  de `steam.exe`.
- **El menú se cierra con un gancho que solo vive mientras el menú está abierto** (`MenuGuard.cs`).
  WPF despacha el desplegable al perder la activación la ventana dueña, y la píldora es
  `WS_EX_NOACTIVATE`: nunca la pierde, así que el menú se quedaba colgado hasta pincharla. El guard
  engancha `WH_MOUSE_LL` en el `Opened` y lo suelta en el `Closed`; la primera pulsación que cae fuera
  de todos los *popups* del proceso cierra el menú y se retira a sí misma. Detectar los *popups* es
  trivial: son las ventanas `WS_EX_TOOLWINDOW` visibles del propio proceso —criterio que descarta tanto
  la píldora como el fantasma del arrastre, que es *tool* pero lleva `WS_EX_TRANSPARENT`—. El coste en
  reposo es exactamente cero porque con el menú cerrado no queda ni gancho ni temporizador; el único
  reloj que hay (300 ms) solo lo armamos para el caso en que Windows no entregue la suelta que iba
  pareada a una pulsación que nos acabamos de comer.

## Limitaciones conocidas

- Solo Windows; no hay versión para otros sistemas.
- El atajo está fijo en `Ctrl` + `Alt` + `D` y solo se puede encender o apagar, no reasignarlo.
- Las carpetas se hacen arrastrando: no hay entrada de menú que agrupe dos iconos señalándolos uno por
  uno, y el mosaico de la barra enseña como mucho los cuatro primeros de dentro (el contenedor los
  enseña todos).
- El acceso de arranque se lanza a través de `wscript.exe`, así que su icono en la carpeta de inicio
  es el de WScript, no el de GamesDock.
- Algunos accesos `.url` de Steam no exponen un icono decodificable, y hay juegos cuya carátula no está
  todavía en `librarycache` porque el cliente no la ha descargado: la entrada se fija y se lanza igual
  —con la letra del nombre como respaldo—, y el fallo queda anotado en `dock.log`.
- No hay firma de código ni instalador: el binario se distribuye tal cual.

## Privacidad

GamesDock no abre conexiones de red, no envía telemetría y no analiza nada fuera del perfil del
usuario. Los iconos y la lista de juegos se quedan en tu carpeta de `%APPDATA%`.

La única vez que mira fuera de sí mismo es con el desplegable abierto: para saber si el clic ha caído
fuera instala un gancho de ratón global del que solo lee las coordenadas y el botón pulsado, y lo
desengancha en cuanto el menú se cierra. Con el menú cerrado no escucha nada.

## Licencia

MIT. Ver el fichero [`LICENSE`](LICENSE). Los iconos de los juegos y aplicaciones que la barra muestra
son propiedad de sus respectivos autores; GamesDock solo los lee del sistema para pintarlos.

## In English

GamesDock is a single small Windows `.exe` that puts a translucent macOS-style pill at desktop level —
behind your windows, never stealing focus. It auto-detects installed games (Steam, Epic, GOG, Start
Menu, desktop shortcuts), pulls each game's artwork out of Steam's `librarycache` even when the client
hides it inside a hashed subfolder, lets you pin anything by dropping it on the bar —with any icon you
like—, and magnifies on hover with spring physics driven by the compositor clock, idling at essentially
zero CPU. Dropping one tile on another builds an iOS-style folder: a mosaic on the bar, a container
with names on click, and a title that only shows inside. Every icon can be masked three ways —plain
square, iOS-rounded corners or full circle—, folder mosaics included; the mask is only how the tile is
clipped, never a replacement for the icon. The interface is bilingual (Spanish and
English, switching live) and there are nine color themes, including Anime, Cyberpunk and UFO.
Right-click menus close on any click outside them — like any other Windows menu — and that closing
click is swallowed, so nothing underneath reacts to it; while one of the app's own windows is open a
dim veil takes every outside click, so you can't launch a game by accident mid-dialog. No installer, no
network, no telemetry: settings, icon cache and log live in `%APPDATA%\GamesDock`. MIT licensed.
