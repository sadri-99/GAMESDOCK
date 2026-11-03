# GamesDock: rendimiento y coste de las animaciones

Medición local del 3 de noviembre de 2026. Los datos son orientativos y no sustituyen una prueba de FPS en un juego.

## Qué mejora sin cambiar el aspecto

- Memoria temporal por generación de contorno: **22,63 → 8,40 KiB**, un **62,9 % menos**.
- Tiempo mediano de ese cálculo: **15,52 → 15,58 microsegundos**. En estas muestras cortas no hay una mejora clara de tiempo; el beneficio consistente es reducir asignaciones temporales y presión del recolector.
- En el recorrido del cursor, asignaciones administradas medias del proceso de prueba: **4,85 → 4,09 MiB/s**.
- Comparación visual: **180 de 180 escenas con los mismos píxeles**, incluyendo contorno, material y enfoque. Se conservan curvas, efectos, resolución y objetivo de 60 actualizaciones por segundo.
- Las limpiezas manuales de memoria se posponen durante interacción, arrastre, animaciones o Ajustes abiertos.
- Actualización de Ajustes: ahora los seis paneles secundarios están siempre abiertos para mostrar todas las opciones sin scroll. La muestra interactiva reutiliza sus controles. Su medición independiente figura en [rendimiento de la muestra](RENDIMIENTO-MUESTRA.md); las tablas siguientes corresponden al dock principal de la optimización anterior.

Reducir asignaciones temporales no equivale a reducir en ese mismo porcentaje la RAM total de la aplicación.

## Coste mientras se usa el dock

Ventana de prueba WPF fuera del área visible, ocho accesos con iniciales, tamaño 64 px y 12 procesadores lógicos. Sin escaneo de catálogo, descarga de carátulas, fichas web ni actualizador. La versión anterior y la optimizada usan el mismo programa de medida y los mismos recorridos. La CPU se normaliza al total de los 12 procesadores lógicos.

| Tema, cursor en movimiento | CPU total antes | CPU total después |
|---|---:|---:|
| Nostromo | 2,79 % | 3,35 % |
| Santuario | 3,13 % | 2,01 % |
| Cámara de pruebas | 3,20 % | 2,94 % |
| Memory Card | 2,38 % | 2,68 % |
| Horizonte | 3,01 % | 2,23 % |
| Terminal fantasma | 3,35 % | 3,20 % |
| Media de seis temas | 2,98 % | 2,73 % |

Cada recorrido se midió durante unos 3,5 segundos por tema. La media baja en esta tanda, pero algunos temas suben; no se presenta como una reducción universal ni estadísticamente estable de CPU. La planificación de Windows, compilación inicial y cachés introducen variación.

Con el cursor quieto encima y los efectos activos, la versión optimizada registró **0,38–1,52 % de CPU total** en intervalos de unos 1,7 segundos. En los intervalos de reposo: **0,00–0,22 %**. Esto no significa consumo cero permanente del proceso; los efectos no mantienen un bucle de animación en reposo.

En instantáneas después de activar efectos, frente al acercamiento sin efectos:

- Diferencia de memoria física: aproximadamente **0,02–1,15 MiB**.
- Diferencia de memoria privada comprometida: aproximadamente **0,88–3,95 MiB**.

Son diferencias observadas en una secuencia de prueba; las cachés y el momento de la recolección también influyen. La memoria privada comprometida no es lo mismo que RAM física residente. La memoria total incluye el entorno WPF, iconos, ventanas y, en uso real, catálogo y fichas.

Como referencia separada, la copia real del usuario registró antes del cambio 0 % de un procesador lógico durante una muestra de cinco segundos, 74,8 MiB de memoria física y 215,6 MiB de memoria privada. Esa instantánea no permite atribuir todo el consumo a las animaciones ni compararlo directamente con el proceso simplificado de prueba.

## Qué dice la interfaz

La etiqueta junto a «Animaciones de los temas» avisa de CPU activa y RAM adicional baja en esta prueba. Al pulsarla se abre la explicación y sus límites. El acceso «Ajustes» también lleva una indicación de consumo. Español e inglés incluidos. Es información de referencia, sin un monitor permanente añadido al proceso.

**No se midieron GPU, VRAM, batería ni FPS de juegos.** Las animaciones usan CPU/GPU al moverse; no se afirma que sean gratuitas ni que no puedan afectar a equipos con pocos recursos. Apagarlas sigue siendo opcional e independiente del agrandado.

## Datos reproducibles

El archivo «dock-data.zip» contiene resultados antes/después, tres tandas del cálculo geométrico y manifiestos de píxeles. El código fuente incluye «PerformanceProbe.cs» y «PerformanceRegression.cs». Las pruebas corresponden a estas versiones y este equipo; no deben interpretarse como requisitos mínimos o una garantía para otros ordenadores.
