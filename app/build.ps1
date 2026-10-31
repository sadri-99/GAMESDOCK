$ErrorActionPreference = 'Stop'

# No hay proyecto MSBuild: el exe se compila invocando directamente el Roslyn de Visual Studio. En esta
# maquina no existe ni el SDK de .NET ni los targeting packs de Framework, asi que un .csproj con
# UseWPF no compila sin descargar nada.
#
# IMPORTANTE: nunca usar C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe. Ese es el compilador
# legacy (se detiene en C# 5) y rechaza gran parte del codigo de src/.
$cscRutas = @(
    'C:\Program Files\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe',
    'C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe',
    'C:\Program Files\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe',
    'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe',
    'C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\Roslyn\csc.exe',
    'C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\Roslyn\csc.exe'
)
$csc = $cscRutas | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $csc) {
    Write-Host 'No se encuentra el Roslyn de Visual Studio (MSBuild\Current\Bin\Roslyn\csc.exe).' -ForegroundColor Red
    Write-Host 'Instala las "Build Tools for Visual Studio" 2022 o superior y vuelve a intentarlo.' -ForegroundColor Red
    exit 1
}

# Las referencias salen del propio runtime de .NET Framework 4.8, que ya esta en Windows. Los ensamblajes
# de WPF viven en la subcarpeta WPF\ y System.Xaml en la raiz; no es una equivalencia tipografica.
$fw = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$refs = @(
    "$fw\WPF\WindowsBase.dll",
    "$fw\WPF\PresentationCore.dll",
    "$fw\WPF\PresentationFramework.dll",
    "$fw\System.Xaml.dll",
    "$fw\System.dll",
    "$fw\System.Core.dll",
    "$fw\System.Drawing.dll",
    "$fw\System.Windows.Forms.dll",
    "$fw\System.Runtime.Serialization.dll",
    "$fw\System.Xml.dll"
)
$faltan = @($refs | Where-Object { -not (Test-Path $_) })
if ($faltan.Count -gt 0) {
    Write-Host 'Faltan ensamblajes de .NET Framework:' -ForegroundColor Red
    $faltan | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    exit 1
}

# csc no puede escribir encima de un exe en ejecucion: mejor decirlo aqui que un CS0016 en la cara.
if (Get-Process -Name GamesDock -ErrorAction SilentlyContinue) {
    Write-Host 'GamesDock.exe esta en ejecucion. Cierralo desde la bandeja (Salir) o para el proceso antes de compilar.' -ForegroundColor Yellow
    exit 1
}

$src = @(Get-ChildItem -Path "$PSScriptRoot\src" -Filter *.cs -Recurse | Select-Object -ExpandProperty FullName)
if ($src.Count -eq 0) { Write-Host 'No hay fuentes en .\src' -ForegroundColor Red; exit 1 }
$out = "$PSScriptRoot\GamesDock.exe"

$cscArgs = @('-nologo', '-target:winexe', '-platform:x64', '-langversion:latest', '-optimize+',
             '-codepage:65001', "-out:$out", "-win32manifest:$PSScriptRoot\app.manifest",
             "-win32icon:$PSScriptRoot\icon.ico",
             "-resource:$PSScriptRoot\icon.ico,GamesDock.icon.ico",
             "-resource:$PSScriptRoot\xaml\styles.xaml,GamesDock.styles.xaml",
             "-resource:$PSScriptRoot\xaml\window.xaml,GamesDock.window.xaml")
foreach ($r in $refs) { $cscArgs += "-r:$r" }
$cscArgs += $src

& $csc $cscArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "OK -> $out ($((Get-Item $out).Length) bytes)" -ForegroundColor Green
