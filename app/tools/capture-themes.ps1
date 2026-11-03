param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '../../artifacts/theme-capture'),
    [string]$Python = 'python'
)
$ErrorActionPreference = 'Stop'
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$compilerCandidates = foreach ($programRoot in @($env:ProgramFiles, ${env:ProgramFiles(x86)})) {
    foreach ($version in @('18', '2022')) {
        foreach ($edition in @('BuildTools', 'Community', 'Professional', 'Enterprise')) {
            Join-Path $programRoot "Microsoft Visual Studio/$version/$edition/MSBuild/Current/Bin/Roslyn/csc.exe"
        }
    }
}
$compiler = $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $compiler) { throw 'Roslyn not found. Install Visual Studio Build Tools 2022 or later.' }
Copy-Item "$repository/app/GamesDock.exe" "$OutputDirectory/GamesDock.exe" -Force
$framework = "$env:SystemRoot/Microsoft.NET/Framework64/v4.0.30319"
$arguments = @('-nologo', '-target:exe', '-platform:x64', '-langversion:latest', '-codepage:65001',
    "-out:$OutputDirectory/CaptureThemeFrames.exe", "-r:$OutputDirectory/GamesDock.exe")
foreach ($assembly in @('WPF/WindowsBase.dll', 'WPF/PresentationCore.dll', 'WPF/PresentationFramework.dll',
    'System.Xaml.dll', 'System.dll', 'System.Core.dll')) {
    $arguments += "-r:$framework/$assembly"
}
$arguments += "$PSScriptRoot/CaptureThemeFrames.cs"
& $compiler $arguments
if ($LASTEXITCODE -ne 0) { throw 'Capture compilation failed.' }
& "$OutputDirectory/CaptureThemeFrames.exe" "$OutputDirectory/frames" "$repository/docs/screenshots/iconos-barra.png"
if ($LASTEXITCODE -ne 0) { throw 'WPF capture failed.' }
& $Python "$PSScriptRoot/encode-theme-gifs.py" "$OutputDirectory/frames" "$OutputDirectory/gifs"
if ($LASTEXITCODE -ne 0) { throw 'GIF export failed. Python requires Pillow.' }
