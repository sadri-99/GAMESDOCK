# Capturas animadas del dock

Los README español e inglés muestran los 15 temas en `dock-15-temas-hd.gif` (1920 × 540, 43,2 segundos). La guía de temas muestra los seis materiales animados en `temas-animados-hd.gif` (1680 × 2040, 7,68 segundos). Ambos archivos repiten su secuencia automáticamente.

Capturas de los controles reales de GamesDock v1.9.5, sus contornos, integrador de muelles y renderizadores vectoriales WPF. Se capturan a 192 dpi, doble de resolución lógica; no se amplía una captura pequeña. La vista conjunta permite comparar acercamiento, enfoque y lanzamiento. El recorrido incluye también los nueve temas clásicos. Se usa una línea de tiempo controlada para reproducir estados de interacción sin abrir juegos.

Los iconos se leen de las imágenes locales de juegos ya disponibles. Cuando no hay ocho imágenes de al menos 128 píxeles, se completa con el arte de la captura del repositorio. No se exporta configuración, claves, rutas personales ni datos del equipo. La captura no cambia preferencias ni requiere cerrar el dock que esté abierto.

GIF admite hasta 256 colores por paleta. Una paleta común por secuencia evita cambios de color entre cuadros. Exportación con repetición infinita, recorte de diferencias y fusión de cuadros idénticos. Dimensiones y duración verificadas tras codificar; revisados cuadros de inicio, interacción y final. Los archivos de alta resolución son grandes: aproximadamente 21,5 MiB y 11,5 MiB, respectivamente.

## Reproducir

Requiere Windows, Roslyn de Visual Studio y Python con Pillow. Desde raíz del repositorio:

```powershell
./app/tools/capture-themes.ps1 -Python 'C:/ruta/python.exe'
```

Las secuencias PNG, ejecutable auxiliar y GIF aparecen en `artifacts/theme-capture`. El script usa el ejecutable existente en `app/GamesDock.exe`. Para capturar otra compilación, actualízalo antes con `app/build.ps1`. Copiar solo los GIF revisados a `docs/screenshots/v1.9.5/`.
