# Varias pantallas

## Mover y recordar el dock

1. Si está activo **Bloquear posición e iconos**, desactívalo desde el menú de clic derecho.
2. Arrastra una zona vacía del cristal del dock hasta la pantalla que quieras.
3. Suelta el botón. El dock guarda la pantalla y se ancla al borde más cercano de esa pantalla.
4. Para conservar una ubicación libre, elige **Libre** en Ajustes → Posición de la barra antes de arrastrar.

Al cerrar y volver a abrir GamesDock, también mediante el inicio con Windows, se recuperan la pantalla elegida y el borde o la posición libre guardados. No hace falta volver a configurar el inicio automático.

## Cambios de pantallas

- El tamaño y el desplazamiento de la fila se calculan con el área útil de la pantalla del dock y su escala.
- Se admiten pantallas situadas a la izquierda, derecha, encima o debajo, incluidas coordenadas negativas.
- La posición libre se guarda en relación con el espacio disponible: al cambiar resolución, escala o área útil se conserva la ubicación relativa y se evita que el dock quede fuera.
- Si falta la pantalla preferida, se usa una disponible sin borrar la preferencia. Cuando Windows notifica que vuelve a estar conectada, el dock recupera esa pantalla.
- Arrastrarlo expresamente a otra pantalla cambia la preferencia guardada.
- Las carpetas, fichas de juegos y ventanas de ajustes se colocan en la pantalla del dock. El icono que acompaña al arrastre utiliza coordenadas físicas del escritorio.

## Implementación

La configuración conserva `monitorId`, `monitorDevice`, `monitorFreeX` y `monitorFreeY`. Se intenta identificar el monitor por su interfaz de dispositivo; el nombre de pantalla se usa cuando el controlador no proporciona esa identidad. Las coordenadas anteriores `left` y `top` siguen siendo compatibles.

`Displays.cs` enumera y guarda en caché la topología. `MonitorPlacement` contiene los cálculos de selección y posición. `Dock.Displays.cs` escucha cambios de pantallas, área útil y DPI, y agrupa la actualización del diseño. No hay sondeo continuo ni temporizador de monitores. WPF procesa el cambio de DPI antes de recalcular el dock.

Referencias de Windows: [MonitorFromWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-monitorfromwindow), [EnumDisplayDevicesW](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-enumdisplaydevicesw), [WM_DPICHANGED](https://learn.microsoft.com/en-us/windows/win32/hidpi/wm-dpichanged).

## Validación

La suite suma **15.969 comprobaciones**. Las 1.626 comprobaciones de monitores incluyen 500 combinaciones de pantalla, posición y escala, guardado y lectura de configuración, cambio de numeración, desconexión/reconexión, ventanas auxiliares y creación de una nueva ventana con la preferencia guardada.

Se simulan cinco pantallas con escalas del 100, 125, 150 y 200 %. La integración nativa se ha probado con la única pantalla real disponible en esta sesión. No se ha realizado un arrastre físico entre dos monitores reales durante esta validación.
