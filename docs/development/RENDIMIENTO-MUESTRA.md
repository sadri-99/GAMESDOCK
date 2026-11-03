# Rendimiento de la muestra de Ajustes

Medición local del 3 de noviembre de 2026. Comparación con la edición inmediatamente anterior.

## Cambios

- Cinco controles persistentes; no se reconstruyen al actualizar preferencias.
- Reutilización de imágenes, plantillas y geometría cuando sus parámetros no cambian.
- Movimiento del cursor y crecimiento agrupados por fotograma. Objetivo de 60 Hz, sin crear un reloj por evento del ratón.
- Reloj local detenido al estabilizarse y al descargar la muestra. Los efectos ambientales del tema conservan su funcionamiento durante la interacción.
- Quince temas y seis tarjetas de ajustes abiertos. Sin desplazamiento. Adaptación proporcional a áreas de trabajo menores.

## Actualización de configuración

200 llamadas consecutivas a Refresh, con cinco accesos de prueba. Estas cifras miden la actualización de la muestra, no el tiempo total de pintar la ventana ni cambiar de tema.

| Medida | Antes | Después |
|---|---:|---:|
| Memoria temporal por actualización | 69.920 bytes | 1.966 bytes |
| Tiempo medio por actualización | 345 µs | 14 µs |
| Controles originales conservados | 0 de 5 | 5 de 5 |

Reducción de memoria temporal: **97,2 %**. Menos asignaciones y creación de controles; no implica esa reducción de RAM total.

## Cursor en movimiento

Prueba WPF simplificada fuera del área visible, cinco accesos con iniciales de 46 px. Entrada del cursor solicitada cada 8 ms, calentamiento de 700 ms y medición de unos tres segundos por tema. CPU normalizada al total de 12 procesadores lógicos. Las actualizaciones son cambios de la propiedad del cursor, no FPS presentados en pantalla.

| Tema | CPU antes | CPU después | Memoria temporal antes | Después | Actualizaciones/s antes | Después |
|---|---:|---:|---:|---:|---:|---:|
| Terminal fantasma | 0,174 % | 0,305 % | 1,521 MiB/s | 1,262 MiB/s | 27,1 | 53,2 |
| Santuario | 0,390 % | 0,649 % | 1,533 MiB/s | 1,642 MiB/s | 27,0 | 55,5 |

La respuesta actualiza aproximadamente el doble durante este recorrido. El coste de CPU aumenta en esta tanda; no se afirma una reducción universal de CPU. El trabajo temporal por actualización baja, pero el mayor número de actualizaciones puede aumentar el total por segundo. Windows, cachés y otros procesos introducen variación.

No se midieron GPU, batería ni FPS de juegos. La prueba fuera de pantalla no sustituye una medición de presentación real. Los efectos y el objetivo de fluidez se mantienen; no se reduce su calidad para conseguir estas cifras.

## Validación

14.121 comprobaciones superadas. Incluyen geometría, animaciones, agrandado, contornos, estados y controles de Ajustes en español e inglés. La prueba de Ajustes comprueba ausencia de ScrollViewer y grupos plegados, controles dentro de la ventana, identidad de los cinco botones y estabilización/parada del movimiento. Capturas verificadas visualmente en ambas traducciones.

Fuentes reproducibles: app/tests/PreviewProbe.cs y app/tests/SettingsValidation.cs en el repositorio. Datos de esta medición: preview-data.zip.
