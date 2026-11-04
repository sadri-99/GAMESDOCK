# Referencia de los 11 temas conservados

4 de noviembre de 2026. PerformanceProbe, modo sweep, ocho accesos de 64 px y 12 procesadores lógicos. Ventana WPF fuera del área visible. Una pasada de unos 3,5 segundos por tema tras el calentamiento. La medición de Matrix se repitió después de cachear sus columnas vectoriales, conservando los 25 canales y seis glifos por cola. No mide GPU ni FPS de juegos; depende del equipo y del entorno.

| Tema | CPU total (%) | Memoria física del proceso (MiB) |
| --- | ---: | ---: |
| nostromo | 3.42 | 84.0 |
| camara | 2.64 | 87.5 |
| memory | 2.9 | 87.3 |
| horizonte | 2.98 | 88.0 |
| terminal | 5.25 | 84.2 |
| nightcity | 2.61 | 83.7 |
| raccoon | 4.46 | 87.7 |
| codec | 3.17 | 88.3 |
| crystal | 4.01 | 89.7 |
| inferno | 3.21 | 89.2 |
| strand | 4.24 | 84.9 |

La memoria de la tabla corresponde al proceso completo, no al incremento del efecto. El aviso de consumo mantiene separada la comparación incremental histórica de los seis primeros materiales.

Matrix: la asignación medida bajó de 33,29 a 5,29 MiB/s al reutilizar cuatro variantes de columna congeladas; las cifras son de pasadas breves separadas. No se redujeron canales ni longitud de las colas. Se guardan la pasada previa y la repetición específica en `sagas-cpu-sin-cache-matrix.csv` y `sagas-matrix-cache.csv`; `sagas-cpu.csv` reúne los valores de la revisión.

Los emblemas se dibujan una vez en DrawingGroup congelados. No hay descargas de logos, temporizadores adicionales ni bucles de efectos en reposo. Los estados de enfoque, lanzamiento y desactivación usan los relojes existentes.

Tras retirar cuatro temas, se muestran únicamente las filas de los 14 conservados. Los CSV de pasadas anteriores son históricos y no definen el catálogo de la aplicación.

Se repiten las mediciones de Cyberpunk y Death Stranding tras los cambios de tiempos y gotas. Las demás filas corresponden a la referencia anterior de los temas conservados.
