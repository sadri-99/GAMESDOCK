# Instalación e inicio con Windows — v1.9.5

## Instalación recomendada

Descarga `GamesDock-Setup-1.9.5.exe` de la release. Asistente disponible en español e inglés, sin permisos de administrador. Instala el programa en `%LOCALAPPDATA%\Programs\GamesDock`; esa ruta se reutiliza en las actualizaciones. Crea entrada en menú Inicio y permite elegir acceso al escritorio e inicio con Windows. En actualizaciones conserva la preferencia previa de inicio, que puedes cambiar en el asistente.

El archivo descargado es solo el instalador. Puedes moverlo o borrarlo después de instalar. No muevas manualmente el ejecutable instalado: usa reinstalación para reparar archivos. Los datos siguen en `%APPDATA%\GamesDock`; desinstalar conserva ajustes, iconos y copias de accesos del escritorio, y desactiva el inicio automático.

## Arranque

«Arranca con Windows» registra una tarea `GamesDock-<SID del usuario>` en el Programador de tareas. Se dispara al entrar ese usuario en sesión, ejecuta `GamesDock.exe --startup` directamente y fija su carpeta de trabajo. Usa sesión interactiva, privilegios normales y prioridad normal, sin contraseña, red, espera de inactividad, bloqueo por batería, límite de tres días ni demora de inicio añadida. El mutex evita duplicar la barra.

Windows decide cuándo programa cada proceso; no se garantiza arrancar antes de todas las aplicaciones ni antes de entrar en sesión. GamesDock necesita sesión de escritorio para mostrar su barra. No modifica retrasos globales de Windows ni eleva prioridad de CPU. Muestra barra antes del escaneo del catálogo; tarea de registro y limpieza de actualizaciones se aplazan hasta después del primer dibujo. `dock.log` registra `Primera imagen: N ms` desde la inicialización del programa, sin incluir tiempo previo que Windows tarde en lanzarlo.

Si Windows permite acceder al Programador pero rechaza crear la tarea, intenta entrada directa `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\GamesDock`, siempre que pueda retirar la tarea antigua. El registro de respaldo puede seguir el retraso de inicio impuesto por Windows. Si tampoco es posible, ajustes conserva estado anterior y muestra error. El motivo queda en `dock.log`.

## Migración desde versiones anteriores

Cierra copia antigua desde bandeja e instala v1.9.5. Conserva configuración existente, actualiza destino de inicio y elimina `%STARTUP%\GamesDock.lnk` y `%APPDATA%\GamesDock\GamesDock.lanzador.vbs` cuando el registro nuevo termina correctamente.

La ruta antigua se guardaba dentro de VBScript en ASCII. Cambiar la carpeta dejaba una ruta inexistente; acentos y caracteres no ASCII podían alterarse. Además, la cadena generada dejaba comillas desequilibradas. v1.9.5 elimina esa dependencia: el producto ya es una aplicación de Windows sin consola y no necesita WScript para ocultarla.

Para copia portable, mueve `GamesDock.exe` y ábrelo una vez desde ubicación nueva. Si inicio automático estaba activado, vuelve a registrar ruta. Windows no puede descubrir por sí solo un archivo movido sin volver a ejecutar GamesDock. Instalador evita depender de la ubicación de descarga.

## Compilar y verificar

```powershell
./app/build.ps1
./tests/test-startup.ps1
./installer/build.ps1
```

Compilación del instalador requiere Inno Setup 6.7 o superior. `installer/build.ps1 -Compiler 'C:\ruta\ISCC.exe'` permite indicar compilador. Resultado: `dist/GamesDock-Setup-1.9.5.exe`. Flujo GitHub Actions **Windows installer**, ejecutado manualmente, compila aplicación, prueba registro real de inicio y genera asistente; después instala, lanza tarea, reinstala desde otra ubicación y desinstala, comprobando conservación de ajustes. Publica artefacto con instalador, ejecutable y SHA256SUMS.

`tests/test-startup.ps1 -Integration` modifica registro de inicio y configuración del perfil que lo ejecuta: úsalo en usuario de pruebas. Verifica registro idempotente y retirada de tarea, además de migración de VBS/acceso antiguos. Prueba sin ese parámetro inspecciona XML, Unicode, permisos, condiciones, versión y selección de asset, sin modificar inicio del usuario.

Para publicar usa tag `v1.9.5` y título **GamesDock v1.9.5**, con instalador y `GamesDock.exe` como archivos separados. Actualizador integrado solo acepta asset exacto `GamesDock.exe`; jamás instala el setup como barra. Tags numéricos se comparan con versión de ensamblaje, evitando avisos repetidos tras instalar; tags históricos no numéricos conservan comparación por fechas.

Fuentes de implementación: [registro de tareas de Microsoft](https://learn.microsoft.com/en-us/windows/win32/taskschd/taskfolder-registertask), [demora de inicio de sesión](https://learn.microsoft.com/en-us/windows/win32/taskschd/taskschedulerschema-delay-logontriggertype-element) y [Inno Setup](https://jrsoftware.org/isdl.php).
