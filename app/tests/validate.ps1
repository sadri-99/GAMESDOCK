param(
    [Parameter(Mandatory=$true)][string]$Executable,
    [Parameter(Mandatory=$true)][string]$Baseline,
    [Parameter(Mandatory=$true)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$compilerCandidates = foreach ($programRoot in @(${env:ProgramFiles}, ${env:ProgramFiles(x86)})) {
    if (-not $programRoot) { continue }
    foreach ($version in @('18', '2022')) {
        foreach ($edition in @('BuildTools', 'Community', 'Professional', 'Enterprise')) {
            Join-Path $programRoot "Microsoft Visual Studio/$version/$edition/MSBuild/Current/Bin/Roslyn/csc.exe"
        }
    }
}
$csc = $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $csc) { throw 'Roslyn not found. Install Visual Studio Build Tools 2022 or later.' }
$fw = 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319'
$harness = Join-Path (Split-Path $Executable) 'ThemeValidation.exe'
$arguments = @('-nologo','-target:exe','-platform:x64','-langversion:latest','-codepage:65001',"-out:$harness","-r:$Executable")
foreach ($dll in @('WPF/WindowsBase.dll','WPF/PresentationCore.dll','WPF/PresentationFramework.dll','System.Xaml.dll','System.dll','System.Core.dll')) {
    $arguments += "-r:$fw/$dll"
}
$arguments += Join-Path $PSScriptRoot 'ThemeValidation.cs'
& $csc $arguments
if ($LASTEXITCODE -ne 0) { throw 'Validation build failed.' }
& $harness $OutputDirectory $Baseline (Join-Path $PSScriptRoot '../../docs/screenshots/iconos-barra.png')
if ($LASTEXITCODE -ne 0) { throw 'Theme validation failed.' }
$motionHarness = Join-Path (Split-Path $Executable) 'MotionValidation.exe'
$motionArguments = @($arguments | Where-Object { $_ -notlike '-out:*' -and $_ -notlike '*ThemeValidation.cs' })
$motionArguments += "-out:$motionHarness"
$motionArguments += Join-Path $PSScriptRoot 'MotionValidation.cs'
& $csc $motionArguments
if ($LASTEXITCODE -ne 0) { throw 'Motion validation build failed.' }
& $motionHarness $OutputDirectory (Join-Path $PSScriptRoot '../../docs/screenshots/iconos-barra.png')
if ($LASTEXITCODE -ne 0) { throw 'Motion validation failed.' }
$hoverHarness = Join-Path (Split-Path $Executable) 'HoverRegression.exe'
$hoverArguments = @($arguments | Where-Object { $_ -notlike '-out:*' -and $_ -notlike '*ThemeValidation.cs' })
$hoverArguments += "-out:$hoverHarness"
$hoverArguments += Join-Path $PSScriptRoot 'HoverRegression.cs'
& $csc $hoverArguments
if ($LASTEXITCODE -ne 0) { throw 'Hover regression build failed.' }
& $hoverHarness
if ($LASTEXITCODE -ne 0) { throw 'Hover regression failed.' }
$proHarness = Join-Path (Split-Path $Executable) 'ProMotionValidation.exe'
$proArguments = @($arguments | Where-Object { $_ -notlike '-out:*' -and $_ -notlike '*ThemeValidation.cs' })
$proArguments += "-out:$proHarness"
$proArguments += Join-Path $PSScriptRoot 'ProMotionValidation.cs'
& $csc $proArguments
if ($LASTEXITCODE -ne 0) { throw 'Coordinated hover validation build failed.' }
& $proHarness $OutputDirectory (Join-Path $PSScriptRoot '../../docs/screenshots/iconos-barra.png')
if ($LASTEXITCODE -ne 0) { throw 'Coordinated hover validation failed.' }
$settingsHarness = Join-Path (Split-Path $Executable) 'SettingsValidation.exe'
$settingsArguments = @($arguments | Where-Object { $_ -notlike '-out:*' -and $_ -notlike '*ThemeValidation.cs' })
$settingsArguments += "-out:$settingsHarness"
$settingsArguments += Join-Path $PSScriptRoot 'SettingsValidation.cs'
& $csc $settingsArguments
if ($LASTEXITCODE -ne 0) { throw 'Settings validation build failed.' }
& $settingsHarness $OutputDirectory (Join-Path $PSScriptRoot '../../docs/screenshots/iconos-barra.png')
if ($LASTEXITCODE -ne 0) { throw 'Settings validation failed.' }
$performanceHarness = Join-Path (Split-Path $Executable) 'PerformanceRegression.exe'
$performanceArguments = @($arguments | Where-Object { $_ -notlike '-out:*' -and $_ -notlike '*ThemeValidation.cs' })
$performanceArguments += "-out:$performanceHarness"
$performanceArguments += Join-Path $PSScriptRoot 'PerformanceRegression.cs'
& $csc $performanceArguments
if ($LASTEXITCODE -ne 0) { throw 'Performance regression build failed.' }
& $performanceHarness
if ($LASTEXITCODE -ne 0) { throw 'Performance regression failed.' }
