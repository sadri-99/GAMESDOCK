# Arquitectura y validación

## Plataforma

C# y WPF sobre .NET Framework 4.8, proceso x64. Sin NuGet ni servicios. Roslyn compila directamente; XAML e icono se incrustan y cargan en ejecución.

| Ruta | Responsabilidad |
| --- | --- |
| app/build.ps1 | Localiza Roslyn, referencia runtime e incrusta recursos. |
| app/src/Displays.cs, Dock.Displays.cs | Identidad y área útil de monitores, colocación física, DPI y eventos de topología. |
| app/src/Program.cs | Inicio, instancia única y ciclo de vida. |
| app/src/AssemblyInfo.cs | Metadatos de producto 2.00. |
| app/src/Pages.cs, Dock.Pages.cs | Páginas persistentes, migración y menús; orden, miembros y carpetas por página. |
| app/src/Config.cs | Preferencias y normalización compatible. |
| app/src/Dock*.cs, Layout.cs | Barra, interacción, invalidación y distribución. |
| app/src/PillGeometry.cs, Springs.cs | Contorno e integración del movimiento. |
| app/src/Themes.cs, ThemeMotionProfile.cs | Familias, colores y perfiles. |
| app/src/MaterialArt.cs, MaterialMotion.cs, MaterialFocus.cs | Materiales, movimiento y enfoque. |
| app/src/SagaTileMotion.cs | Enfoque y lanzamiento de iconos. |
| app/src/Settings*.cs, DockPreview.cs | Ajustes y muestra reutilizable. |
| app/src/PerformanceInfo.cs | Aviso de consumo sin monitor permanente. |
| app/src/Lang.cs | Traducción español/inglés. |
| app/src/Update.cs | Consulta, descarga y sustitución confirmada. |
| app/xaml | Estilos y ventana. |
| app/tests | Validación y mediciones. |
| docs/screenshots | Capturas seleccionadas. |

## Compilar

Windows x64, .NET Framework 4.8 y Build Tools de Visual Studio 2022 o posterior. Usa Roslyn, no el compilador C# 5 del directorio de Framework. No requiere SDK de .NET ni targeting packs.

```powershell
./app/build.ps1
./app/build.ps1 -OutputPath "$PWD/artifacts/GamesDock.exe"
```

La carpeta de salida se crea si hace falta. Se rechaza sobrescribir un ejecutable abierto. app/GamesDock.exe se versiona como distribución directa; otros ejecutables y resultados quedan excluidos.

## Validar

La suite usa un perfil aislado mediante `GAMESDOCK_DATA_DIR`; no modifica la configuración habitual. Guarda el binario anterior fuera del repositorio antes de sustituirlo:

```powershell
./app/tests/validate.ps1 -Executable "$PWD/app/GamesDock.exe" `
  -Baseline 'C:/ruta/anterior/GamesDock.exe' `
  -OutputDirectory "$PWD/artifacts/validation"
./tests/test-startup.ps1
```

La referencia oficial de v2.00 es v1.9.5. Los harness se compilan junto al ejecutable candidato. La carpeta debe permitir escritura. Las ventanas se dibujan fuera de pantalla con datos de muestra; las capturas PNG se renderizan a 192 DPI. GIF usa muestreo temporal reducido y no representa una medición de FPS.

[Resultado y alcance de validación de v2.00](VALIDACION-v2.00.md). PerformanceProbe y PreviewProbe permiten mediciones adicionales. Las pruebas fuera de pantalla no miden fotogramas presentados ni impacto en juegos.

## Páginas y compatibilidad

`Pages.cs` normaliza Favoritos, Juegos y Aplicaciones, más un máximo de cinco páginas personalizadas. Cada página guarda miembros, orden y carpetas. `Dock.Pages.cs` genera menús y ejecuta las acciones sobre la página activa. La migración es única e idempotente: conserva accesos y favoritos previos. Añadir un archivo no lo convierte en favorito. Reescanear no repone miembros retirados manualmente de una página.

Se conserva el esquema anterior, añadiendo campos opcionales. Opacidad heredada normalizada al 100 %; formas válidas conservadas. Un tema retirado o desconocido pasa a Noche. Las configuraciones nuevas usan iconos iOS. No publicar config.json, claves, cachés ni registros.

## Monitores

`Displays.cs` encapsula identidad del dispositivo, área útil, conversión de coordenadas físicas/DIP y selección del monitor. `Dock.Displays.cs` conserva monitor preferido y posición relativa, reacciona a cambios de Windows y recupera una posición visible si falta la pantalla. No usa sondeo continuo. Carpetas, fichas, ajustes y arrastre usan el monitor del dock. Pruebas incluyen coordenadas negativas y escalas 100–200 %; la comprobación nativa local solo cubre una pantalla física. [Detalle](../MULTIPANTALLA.md).

## Selector de SteamGridDB

`IconPicker.cs` contiene cabecera fija y tarjetas; `IconPicker.Search.cs` coordina nombres alternativos, coincidencias y cancelación por generación; `IconPicker.Images.cs` mantiene cuatro trabajadores y una caché acotada a 64 miniaturas. Solo la galería se desplaza. Errores, búsqueda sin resultados y reintentos se resuelven dentro de la ventana. La clave no se envía a las URL de miniaturas. IconPickerValidation usa un servidor local y clave ficticia, sin depender de disponibilidad externa.

## Fichas y actividad

`HoverCard.Personal.cs` contiene recorte real del banner, carga cancelable y presentación de actividad. `HoverCardLarge` escala la ficha al 115 % y la colocación tiene en cuenta ese tamaño. `SteamActivity.cs` lee `localconfig.vdf` de la cuenta local activa: horas, última partida y período reciente cuando consta. No solicita datos personales por red ni mezcla cuentas. Caché temporal y lectura asíncrona; opción desactivada por defecto. Los lanzamientos de GamesDock se distinguen del tiempo registrado por Steam.

## Ajustes y cierre

Personalización agrupa tema, movimiento y aumento junto a la muestra. Barra, comportamiento, comunidad, idioma, sistema y ayuda siguen visibles, sin pestañas ni desplegables de sección. Solo la galería de temas se desplaza. El tema activo se lleva a la vista. `DockPreview` reutiliza cinco controles; actualiza disposición y suspende su reloj al asentarse. Clic fuera usa el cierre normal y guarda cambios, respetando diálogos secundarios. `Veil` consume el clic para no transmitirlo a otra aplicación.

## Materiales y movimiento

11 perfiles animados y nueve clásicos. `MaterialSagas.Surface.cs` define superficies propias; `.Motion.cs` separa ambiente y foco; `MaterialStories.cs` compone lanzamiento por saga. `MaterialLegacySagas.cs` aporta secuencias de los cinco materiales anteriores conservados. `SagaTileMotion` separa transformaciones de foco y lanzamiento y las restablece al finalizar.

`MaterialIdentity` conserva emblemas vectoriales congelados para el selector y microdetalles de reposo. No existe sello al pie del acceso activo ni reserva adicional para logos. `Layout.RowInset`, límites de scroll y muestra usan márgenes compactos originales. CompactMarginValidation compara geometría y extremos contra v1.9.5.

Cyberpunk: ciclo 7,6 s, lanzamiento 1,9 s, entrada 3,7 s. PlayStation no dibuja botones redundantes bajo el foco. Diablo conserva runas móviles y lanzamiento, sin círculo de seguimiento ni runas estáticas centrales. Death Stranding elimina instrumentos fijos de extremos y comparte 16–34 gotas entre reposo y movimiento: acumulación, aceleración, estela y reflejo.

Brochas, plumas, geometrías y emblemas congelados reutilizados. Matrix cachea variantes vectoriales de columna sin reducir canales o longitud. Los temas usan los relojes existentes: no añaden temporizadores ni bucles de efectos en reposo. Al desactivar animación, ocultar o terminar la interacción se detienen según el ciclo existente. El aviso de consumo es informativo; no mantiene un monitor permanente. [Mediciones y límites](SAGAS-RENDIMIENTO.md).

## Publicación

Tag `v2.00`, título **GamesDock v2.00**, ensamblado y versión de archivo `2.0.0.0`, versión informativa `2.00`. Instalador `GamesDock-Setup-2.00.exe`, portable `GamesDock.exe`. El actualizador solo acepta el asset exacto `GamesDock.exe`; no confunde el instalador con la aplicación. El asistente mantiene AppId y ruta de instalación para actualizar instalaciones anteriores. [Instalación e inicio](../INICIO-WINDOWS.md).
