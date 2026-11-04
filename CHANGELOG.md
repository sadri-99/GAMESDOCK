# Changelog

## v2.00 — 2026-11-04

Cambios desde la última subida, `e77f08b`, posterior a v1.9.5. Esta lista describe el resultado publicado; los diseños descartados durante las pruebas no forman parte de la versión.

### Organización y menús

- «Páginas» sustituye a «Qué se ve»: Favoritos, Juegos y Aplicaciones, más hasta cinco páginas personalizadas. Se retira Todo y «detectados» del nombre Juegos.
- Contenido, orden y carpetas independientes. Añadir accesos a una página no añade favoritos; Favoritos permanece fija y se edita manualmente. Crear, renombrar, eliminar y copiar accesos a otras páginas desde el menú.
- Migración compatible de favoritos, accesos, orden y carpetas. Reescanear respeta las bajas manuales de cada página.
- Menús de iconos sin Ocultar del dock, Cambiar icono ni Lanzar ahora; menú de fondo sin Buscar ni Restaurar ocultos. Bloqueo con resalte temático, sin tick ni hueco reservado.
- Márgenes interiores uniformes para los textos de menús contextuales y bandeja.

### Varias pantallas

- Arrastrar a otra pantalla guarda monitor y posición para el siguiente inicio.
- Anclaje, posición libre y límite de tamaño relativos a la pantalla elegida, con coordenadas negativas y escalas distintas.
- Recuperación visible al desconectar una pantalla, conservando la preferida para reconexión. Carpetas, fichas, ajustes y arrastre siguen al dock.
- Actualización por eventos de Windows, sin sondeo continuo.

### SteamGridDB y fichas

- Buscador de nombres alternativos en el selector de iconos de comunidad y elección de juego o edición coincidente.
- El selector abre aunque no haya resultado automático, muestra errores y permite reintentar. Cabecera, búsqueda, estado y cierre fijos; scroll solo en la galería.
- Tarjetas mayores, fondo para transparencias y resolución/puntuación en descripción emergente. Cancelación de búsquedas anteriores, respuestas tardías descartadas, cuatro trabajadores y caché de 64 miniaturas.
- Banner de ficha recortado realmente por arriba, texto más espaciado y márgenes naturales. Tamaño opcional del 115 %.
- Actividad personal Steam opcional, desactivada por defecto: horas jugadas, última partida y período reciente cuando está disponible localmente. Lectura asíncrona cancelable y caché; cuenta activa sin mezclar usuarios, sin clave adicional ni consultas personales por red.

### Ajustes

- Personalización prioritaria: tema, animación, aumento y muestra juntos. Resto distribuido en bloques compactos, sin pestañas, desplegables de sección ni scroll general.
- Scroll exclusivamente en las miniaturas de temas, alineadas y con selección visible. Controles en español e inglés.
- Clic fuera cierra y guarda igual que la cruz o Esc; respeta diálogos secundarios y consume el clic exterior.
- Muestra reutilizable y relojes detenidos al asentarse; se conserva calidad de dibujo y movimiento. El aviso de consumo explica las mediciones y sus límites.

### Temas y acabado

- **20 temas: nueve clásicos y 11 animados**. Alien, Portal, PlayStation, Mass Effect, Matrix, Cyberpunk 2077, Resident Evil, Metal Gear Solid, Final Fantasy, Diablo y Death Stranding.
- Superficies, paletas, contornos, enfoque, aumento y secuencias de lanzamiento propios. Emblemas vectoriales reconocibles en el selector; gestos de esclusa, cubo/portal, Memory Card, relé, descifrado, quickhack, puerta, caja, cristales, invocación y enlace quiral.
- Márgenes laterales compactos originales restaurados. Sin ampliar extremos ni añadir sellos o logotipos al pie del icono activo.
- PlayStation sin símbolos redundantes bajo el icono seleccionado. Cyberpunk al doble de duración: ciclo 7,6 s, entrada 3,7 s y lanzamiento 1,9 s.
- Diablo sin círculo de seguimiento ni runas estáticas centrales; conserva runas móviles, brasas y lanzamiento.
- Death Stranding sin indicadores fijos azules/ámbar de extremos; 16–34 gotas que se acumulan y resbalan con aceleración, estela y reflejo.
- Retirados Dark Souls, Dragon Ball, Hollow Knight, Unturned, Kingdom Hearts, GTA VI y Pokémon. Si uno estaba seleccionado se cambia a Noche; se conservan los demás ajustes.
- Matrix reutiliza columnas vectoriales congeladas: menor asignación sin recortar canales ni colas. Sin temporizadores nuevos ni animación permanente en reposo.

### Distribución y documentación

- Metadatos de aplicación, instalador y pruebas actualizados a v2.00; AppId y ubicación del instalador conservados para actualizar.
- README español/inglés renovados, guías de funciones, arquitectura final, metodología y resultados de validación.
- PNG a 192 DPI y GIF de los 11 temas desde WPF real. Ajustes con Horizonte después de las muestras animadas.
- Suite aislada de datos personales, regresiones de páginas, monitores, iconos, fichas, ajustes, geometría, movimiento y temas retirados. [Evidencia de v2.00](docs/development/VALIDACION-v2.00.md).

## v1.9.5 — 2026-11-03

- Reemplazado VBScript de inicio por tarea de sesión por usuario, ejecutable directo, sin elevación, sin demora añadida y compatible con batería y sin red.
- Migración del acceso y VBS antiguos; reparación automática de ruta al abrir una copia portable movida.
- Instalador Inno Setup con asistente español/inglés, ruta estable por usuario, accesos directos y desinstalación que conserva datos personales.
- Ajustes conserva estado anterior y muestra error si Windows rechaza cambio de inicio.
- Actualizador acepta exclusivamente asset GamesDock.exe y compara tags de versión; mantiene fechas para tags antiguos.
- Pruebas de regresión de registro real de tareas, Unicode/XML, migración, instalación, actualización y desinstalación.

## [1.9.1] — 2026-11-03

- Botón «invítame a un café» junto a «Estrella en GitHub» en Ajustes, con traducción «Buy me a coffee».
- Enlace directo a https://www.paypal.com/paypalme/sadriprods; se abre en el navegador.
- Apartado de donaciones en README español e inglés y enlace de patrocinio en .github/FUNDING.yml.
- Capturas actualizadas de Ajustes en ambos idiomas y metadatos de producto 1.9.1.
- 332 comprobaciones de Ajustes superadas; ambos botones visibles en la misma fila, sin scroll.

## [1.9] — 2026-11-03

### Temas y animaciones

- Nueve temas originales en Clásicos / Classic y seis nuevos en Animados / Animated.
- Nostromo, Santuario, Cámara de pruebas, Memory Card, Horizonte y Terminal fantasma, con traducciones al inglés.
- Materiales vectoriales: radar, brasas, portales, memoria, campos orbitales y código.
- Presentación, interacción y lanzamiento diferenciados por tema; relojes liberados al terminar, salir, ocultar el dock o desactivar efectos.
- Seis perfiles de agrandado coordinan intensidad, vecinos, elevación, enfoque y crecimiento del contorno.
- Contorno rectangular en Nostromo, Memory Card y Terminal; adaptación a las cuatro posiciones.
- Lanzamiento compuesto con el enfoque, sin salto inicial.

### Ajustes

- Personalización destacada con muestra interactiva sin lanzamientos reales.
- Agrandado, animaciones, forma y tamaño junto a los temas.
- Quince miniaturas visibles, con ancho uniforme y nombres completos debajo.
- Seis tarjetas abiertas: barra, comportamiento, idioma, comunidad, sistema y ayuda.
- Ventana sin scroll, con adaptación proporcional a áreas de trabajo menores.
- Eliminado el control de opacidad; ventana al 100 %, incluida la configuración heredada. Se conserva la transparencia del material.
- Forma iOS en instalaciones nuevas; elecciones anteriores conservadas.
- Aviso de CPU y memoria traducido, junto a animaciones y en el acceso a Ajustes.

### Correcciones

- Entrar con el agrandado desactivado ya no expande el contorno ni recorta las esquinas superiores.
- Desactivar el agrandado durante la transición restablece iconos y contorno de inmediato.
- Animación y agrandado permanecen independientes.

### Rendimiento

- Espacio geométrico reutilizado, claves numéricas para invalidación y caché orbital estática.
- Limpiezas manuales aplazadas durante interacción, arrastre, animaciones y Ajustes abiertos.
- Cinco controles persistentes en la muestra, reutilizando imágenes, plantillas y geometría.
- Actualizaciones agrupadas por fotograma; integración compartida con el dock y objetivo de 60 Hz.
- Memoria temporal por actualización de configuración de la muestra: 69.920 → 1.966 bytes en la prueba local (97,2 % menos). No representa la RAM total.
- Efectos y resolución conservados. CPU no disminuye en todos los casos; informes y datos en docs/development.

### Proyecto y validación

- Documentación reorganizada: inicio español e inglés, guía de uso, temas, arquitectura, mediciones y publicación.
- Capturas actuales en ambos idiomas y galería de temas/enfoque.
- Exclusión de ejecutables de pruebas, cachés, configuración personal y temporales. app/GamesDock.exe sigue siendo la distribución directa.
- Metadatos de producto 1.9.0; el actualizador conserva su comparación por fecha.
- 14.121 comprobaciones: 4.057 de temas, 206 de movimiento, 7.886 de agrandado, 1.615 de enfoque, 330 de Ajustes y 27 de rendimiento.
- Comparación adicional de 180 escenas: mismos píxeles tras optimizar el renderizador.

[1.9]: https://github.com/sadri-99/GAMESDOCK/releases/tag/v1.9

[1.9.1]: https://github.com/sadri-99/GAMESDOCK/releases/tag/v1.9.1
