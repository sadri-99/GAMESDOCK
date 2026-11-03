param([string]$Compiler)
$ErrorActionPreference = 'Stop'
if (-not $Compiler) {
    $Compiler = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 7\ISCC.exe"
    ) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (-not $Compiler) { throw 'Instala Inno Setup 6.7 o superior, o indica -Compiler con la ruta de ISCC.exe.' }
& $Compiler "$PSScriptRoot\GamesDock.iss"
if ($LASTEXITCODE -ne 0) { throw "Error de Inno Setup: $LASTEXITCODE" }
