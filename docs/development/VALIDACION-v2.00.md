# Validación de GamesDock v2.00

4 de noviembre de 2026. Compilación Roslyn x64 optimizada, .NET Framework 4.8 y WPF. Ensamblado `2.0.0.0`, versión informativa `2.00`. Referencia: ejecutable publicado v1.9.5. Último commit remoto previo: `e77f08b`.

## Suite local

Todos los programas terminaron correctamente. Perfil aislado con `GAMESDOCK_DATA_DIR`, datos ficticios y ventanas fuera de pantalla. No se alteraron claves, iconos ni configuración habitual.

| Programa | Comprobaciones |
| --- | ---: |
| ThemeValidation | 5.697 |
| MotionValidation | 326 |
| HoverRegression | 10.586 |
| ProMotionValidation | 3.044 |
| SettingsValidation | 427 |
| PerformanceRegression | 27 |
| PagesValidation | 190 |
| MonitorValidation | 1.626 |
| IconPickerValidation | 191 |
| CardValidation | 75 |
| SettingsDismissValidation | 17 |
| SagaValidation | 132 |
| CompactMarginValidation | 720 |
| ThemeTweaksValidation | 7 |
| **Total** | **23.065** |

También pasa `tests/test-startup.ps1`: XML de inicio, rutas Unicode, prioridad, batería/sin red, selección exclusiva del asset `GamesDock.exe` y versión. SettingsValidation se repitió para generar capturas con Horizonte (427 comprobaciones) y el selector para capturas a doble densidad (191). No se suman otra vez.

## Alcance

- Catálogo de 20 temas y traducciones. Migración a Noche de los siete temas retirados, incluido Pokémon.
- Aumento desactivado, esquinas, contornos, límites y posiciones de iconos, varios tamaños y cuatro orientaciones. Márgenes compactos comparados contra v1.9.5.
- Movimiento real WPF, finalización de relojes, restablecimiento de iconos, secuencias distintas y controles desactivados.
- Páginas independientes, migración, persistencia, límite de cinco personalizadas, orden y carpetas.
- Cinco pantallas simuladas, coordenadas negativas, escalas 100–200 %, desconexión/reconexión y persistencia. Integración nativa con una pantalla física disponible.
- SteamGridDB mediante servidor local: nombres alternativos, errores, cancelación, respuestas tardías, cuatro trabajadores, caché y cabecera/cierre fijos. No sustituye una prueba de disponibilidad de la API externa.
- Fichas, recorte, tamaño, cuenta Steam activa, caché, cancelación y preferencia de privacidad con datos ficticios.
- Ajustes en español/inglés, controles visibles y scroll local; cierre exterior, cuadros secundarios y reapertura.

## Imágenes

Las láminas, ajustes y selector de comunidad se renderizan desde WPF a 192 DPI. Las capturas auxiliares de páginas y menús se conservan a escala 100 %. Láminas de temas a 1680 × 2080 y 1680 × 1740; ajustes a 2484 × 1598. Los iconos de muestra son recortes del fixture del repositorio; los vectores y textos se dibujan a la resolución de salida. Selector de comunidad con servidor y contenido de muestra, sin claves ni datos personales.

GIF generados desde el renderer real con muestreo reducido. Compresión comprobada comparando todos los fotogramas RGB decodificados y duración total contra el GIF original; no se cambia color ni temporización. PNG conserva color completo.

## Límites

Las ventanas fuera de pantalla no miden FPS presentados, impacto en juegos, GPU ni batería. Solo se dispuso de una pantalla física; los demás escenarios se simulan. Las cifras de CPU y memoria son referencias locales breves de los temas conservados, con metodología en [SAGAS-RENDIMIENTO.md](SAGAS-RENDIMIENTO.md). No se repitieron todas las mediciones históricas al eliminar Pokémon.

## Instalador y binarios de distribución

[Windows installer, ejecución 41772453895](https://github.com/sadri-99/GAMESDOCK/actions/runs/41772453895), commit de código `987f3134446ccdfd4c8882931d211a64fbad7cb2`: **correcto**. Entorno Windows limpio. Pasa compilación, registro real de tarea de inicio, migración del acceso/VBScript antiguo, activación/desactivación repetida y selección del asset de actualización. El smoke test instala en ruta estable, lanza la tarea, cierra normalmente, reinstala desde otra ubicación y desinstala, conservando preferencias.

Se distribuyen el ejecutable y el instalador producidos por ese flujo, con SHA-256 verificado al descargar. El binario portable del repositorio se sincroniza con el mismo artefacto. Los cambios posteriores del commit de publicación son documentación, imágenes y ese binario; no cambian fuentes de producción.

La suite completa se aplica también al ejecutable final del artefacto, usando el binario de `e77f08b:app/GamesDock.exe` como referencia exacta. Los totales anteriores corresponden al resultado final; no se suman las pasadas de preparación.

| Archivo | SHA-256 |
| --- | --- |
| GamesDock.exe | `8697fec46d6fa56babb60c9e9e79fd77e583933c9c34a61da378f8374f3de5a5` |
| GamesDock-Setup-2.00.exe | `22f688f17175e7f1b6549c39342183cc332803fcd9f42d0a84d8aa78528f988c` |
