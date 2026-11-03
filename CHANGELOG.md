# Changelog

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
