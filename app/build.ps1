$ErrorActionPreference = 'Stop'

# El exe se compila con el Roslyn de VS 2026 BuildTools: en esta maquina no hay SDK de .NET ni
# targeting packs de Framework, asi que MSBuild con UseWPF no compila sin descargar nada. El csc
# que vive en Framework64\v4.0.30319 es el LEGACY (solo C# 5): no usarlo.
$csc = 'C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe'
$fw  = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'

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
) | Where-Object { Test-Path $_ }

$src = Get-ChildItem -Path "$PSScriptRoot\src" -Filter *.cs -Recurse | Select-Object -ExpandProperty FullName
$out = "$PSScriptRoot\GamesDock.exe"

$argv = @('-nologo', '-target:winexe', '-platform:x64', '-langversion:latest', '-optimize+',
          '-codepage:65001', "-out:$out", "-win32manifest:$PSScriptRoot\app.manifest",
          "-win32icon:$PSScriptRoot\icon.ico",
          "-resource:$PSScriptRoot\icon.ico,GamesDock.icon.ico",
          "-resource:$PSScriptRoot\xaml\styles.xaml,GamesDock.styles.xaml",
          "-resource:$PSScriptRoot\xaml\window.xaml,GamesDock.window.xaml")
foreach ($r in $refs) { $argv += "-r:$r" }
$argv += $src

& $csc $argv
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "OK -> $out ($((Get-Item $out).Length) bytes)" -ForegroundColor Green
