# Los 11 temas animados

Catálogo final de v2.00. Se retiran Dark Souls, Dragon Ball, Hollow Knight, Unturned, Kingdom Hearts, GTA VI y Pokémon. Quedan **11 animados y 9 clásicos**, 20 temas en total. Una selección anterior de un tema retirado pasa a Noche.

En Ajustes → Animados, las miniaturas muestran el emblema de cada saga. Activa **Animaciones de los temas**; el aumento de iconos se puede activar por separado. Solo el selector de temas tiene scroll. Los cinco originales conservados se muestran como Alien, Portal, PlayStation, Mass Effect y Matrix; sus identificadores guardados permanecen iguales.

## Dock compacto y guiños contextuales

Restaurados exactamente el margen lateral, ancho y posiciones de los iconos de la distribución compacta original. No se añade ninguna franja para logotipos y tampoco se reduce el tamaño de los accesos.

En reposo, los laterales usan microdetalles dentro del espacio que ya existía. Se retira el sello de la esquina del icono activo; el foco y las secuencias no añaden ese logotipo. Se conserva el resto de la presentación compacta.

Las miniaturas de Ajustes conservan los emblemas completos. Las secuencias de cada saga siguen activas al lanzar. Los instrumentos grandes que antes ocupaban el extremo se integran ahora en la franja inferior o en el foco, sin placas laterales.

| Tema | Identidad contextual | Movimiento y lanzamiento |
| --- | --- | --- |
| Alien | Emblema W/Y, placa industrial y detector de movimiento | Barrido del detector, contacto luminoso y secuencia de cierre/apertura de esclusa. |
| Portal | Iris de Aperture, portales azul/naranja y cubo con corazón | Un cubo entra en un portal y sale por el otro; el acceso se comprime durante la transferencia. |
| PlayStation | Símbolo multicolor, botones geométricos y ranura de Memory Card | Arranque por colores, escritura segmentada y confirmación de memoria. Retirados los cuatro símbolos redundantes bajo el icono enfocado. |
| Mass Effect | Insignia N7, rojo de armadura y estructura de relé | Herramienta holográfica ámbar, nave y corredor de relé azul. |
| Matrix | Glifos verdes y lluvia de código con cabezas blancas | Columnas de signos, lectura por filas y confirmación de descifrado. Columnas vectoriales reutilizadas. |
| Cyberpunk 2077 | Máscara roja, 2077, placa amarilla y circuito cian | Quickhack y datos con tiempos al doble de duración: ciclo 7,6 s, lanzamiento 1,9 s y entrada 3,7 s. Se mantiene la tasa de fotogramas. |
| Resident Evil | Emblema rojo/blanco de Umbrella, ECG y vial | Doble latido y puerta de carga con panel, pomo, bisagra, oscuridad y revelado. Estado FINE decorativo, sin lectura de salud real. |
| Metal Gear Solid | Zorro/FOX, frecuencia Codec 140.85 y pantalla militar | Enlace de voz, alerta de exclamación y caja de infiltración sobre el acceso. |
| Final Fantasy | Cristal facetado y monograma FF | Fragmentos que convergen, brillo por facetas y estela de meteoro. |
| Diablo | D con cuernos, nombre, orbes de sangre/maná y piedra | Retirados el anillo de enfoque y las runas estáticas del centro inferior. Se conservan runas móviles, brasas y la invocación al lanzar. |
| Death Stranding | Cápsula BB ámbar, lluvia y Odradek | Extremos sin instrumentos azules/ámbar. Entre 16 y 34 gotas de agua con acumulación, aceleración, estela y reflejo al resbalar. Enlace y huellas al lanzar. |

## Secuencias e interacción

Los actos de cada lanzamiento son propios de su saga. No se resuelven todos con la misma onda, anillo o lluvia de partículas. Se comparten primitivas de dibujo, geometría de contorno y administración de relojes; el contenido y la composición de las secuencias son diferentes.

Las secuencias se dibujan en el dock y no alteran cómo ni cuándo se abre la aplicación. Clic, arrastre, páginas e iconos mantienen su función. El lanzamiento funciona también sin un icono previamente enfocado. Al desactivar animaciones, salir del dock u ocultarlo, los relojes se detienen según la política existente. No hay bucle de efectos en reposo.

## Muestras de los 11 temas

Arte de aplicaciones idéntico entre filas para comparar el tema. Los GIF capturan el renderer WPF de producción; tienen muestreo reducido para compartir y no miden los FPS de la aplicación.

![Alien, Portal, PlayStation, Mass Effect, Matrix y Cyberpunk](screenshots/v2.00/Sagas-1.gif)

![Resident Evil, Metal Gear Solid, Final Fantasy, Diablo y Death Stranding](screenshots/v2.00/Sagas-2.gif)


## Validación y rendimiento

La suite recorre los 20 temas y mantiene las regresiones de geometría, aumento, monitores y ajustes. CompactMarginValidation añade 720 comparaciones contra el ejecutable publicado: margen, ancho con ocho accesos y posiciones de los iconos de los extremos a varios tamaños y escalas.

[Medición de CPU y metodología](development/SAGAS-RENDIMIENTO.md). La memoria indicada corresponde al proceso completo, no al coste aislado de los efectos. GPU y FPS de juegos no medidos.

## Referencias

Los nombres identifican sus sagas y GamesDock no está afiliado a sus titulares. Referencias contrastadas previamente: [Codec en Konami](https://metalgear.konami.net/manual/mc2/mggb/ps5/en/page06.html), [Odradek y Timefall](https://kojimaproductions.jp/en/DSTips).


ThemeTweaksValidation verifica selección limpia de PlayStation y Diablo, extremos y gotas de Death Stranding, reloj lento de Cyberpunk y ausencia de temas retirados.
