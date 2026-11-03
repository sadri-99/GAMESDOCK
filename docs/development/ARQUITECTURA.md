# Arquitectura y validación

## Plataforma

C# y WPF sobre .NET Framework 4.8, proceso x64. Sin NuGet ni servicios. Roslyn compila directamente; XAML e icono se incrustan y cargan en ejecución.

| Ruta | Responsabilidad |
| --- | --- |
| app/build.ps1 | Localiza Roslyn, referencia runtime e incrusta recursos. |
| app/src/Program.cs | Inicio, instancia única y ciclo de vida. |
| app/src/AssemblyInfo.cs | Metadatos de producto 1.9.0. |
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

Guarda la versión anterior fuera del repositorio antes de sustituirla:

```powershell
./app/tests/validate.ps1 -Executable "$PWD/app/GamesDock.exe" `
  -Baseline 'C:/ruta/anterior/GamesDock.exe' `
  -OutputDirectory "$PWD/artifacts/validation"
```

Los programas de pruebas se compilan junto al ejecutable indicado. Esa carpeta debe permitir escritura. Las ventanas se colocan fuera de pantalla y se usan datos de muestra.

| Programa | Comprobaciones v1.9 |
| --- | ---:|
| ThemeValidation | 4.057 |
| MotionValidation | 206 |
| HoverRegression | 7.886 |
| ProMotionValidation | 1.615 |
| SettingsValidation | 330 |
| PerformanceRegression | 27 |
| Total | 14.121 |

ThemeValidation recorre 540 estados y 60 cambios de tema/posición. SettingsValidation verifica idiomas, controles dentro de ventana, ausencia de scroll, reutilización y parada. PerformanceRegression verifica inmutabilidad, aislamiento por hilo, asignaciones y aplazamiento de limpiezas.

PerformanceProbe y PreviewProbe son mediciones adicionales. Las pruebas fuera de pantalla no miden fotogramas presentados ni impacto en juegos. Informes incluyen condiciones y límites; no garantizan consumo en otros equipos.

## Compatibilidad y publicación

Se conserva el esquema de configuración y MIT. Opacidad heredada normalizada al 100 %; formas válidas conservadas. No publicar config.json, claves, cachés ni registros.

Versión actual: tag v1.9.5, título GamesDock v1.9.5. Se publican GamesDock.exe y GamesDock-Setup-1.9.5.exe por separado. Actualizador selecciona exclusivamente GamesDock.exe y compara tag numérico con versión del ensamblaje; tags históricos no numéricos conservan criterio de fechas con cinco minutos de margen. Instalador e inicio con Windows se describen en [INICIO-WINDOWS.md](../INICIO-WINDOWS.md).

SHA256SUMS.txt permite comprobar las descargas. No es una firma digital.
