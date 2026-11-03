param([switch]$Integration)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -eq 'Core') {
    $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $PSCommandPath)
    if ($Integration) { $arguments += '-Integration' }
    & "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" @arguments
    if ($LASTEXITCODE -ne 0) { throw 'Startup regression tests failed.' }
    exit 0
}
function Assert($condition, [string]$message) { if (-not $condition) { throw $message } }
$exe = Join-Path $PSScriptRoot '..\app\GamesDock.exe'
$assembly = [Reflection.Assembly]::LoadFile((Resolve-Path $exe).Path)
$flags = [Reflection.BindingFlags]'NonPublic,Static'
$autostart = $assembly.GetType('GamesDock.Autostart')
$definition = $autostart.GetMethod('Definition', $flags)
$testPath = 'C:\Pruebas con espacio\Juegos á & 漢\GamesDock.exe'
$testPath = $testPath.Replace('\u00e1', [string][char]0x00e1).Replace('\u6f22', [string][char]0x6f22)
[xml]$xml = $definition.Invoke($null, @($testPath, 'S-1-5-21-1234'))
Assert ($xml.Task.Actions.Exec.Command -eq $testPath) 'Task must preserve Unicode and XML characters.'
Assert ($xml.Task.Actions.Exec.WorkingDirectory -eq [IO.Path]::GetDirectoryName($testPath)) 'Wrong working directory.'
Assert ($xml.Task.Actions.Exec.Arguments -eq '--startup') 'Wrong startup arguments.'
Assert ($null -eq $xml.Task.Triggers.LogonTrigger.Delay) 'Logon must have no artificial delay.'
Assert ($xml.Task.Principals.Principal.LogonType -eq 'InteractiveToken') 'Task must use interactive session.'
Assert ($xml.Task.Principals.Principal.RunLevel -eq 'LeastPrivilege') 'Task must not elevate.'
Assert ($xml.Task.Settings.Priority -eq '6') 'Use normal CPU priority.'
Assert ($xml.Task.Settings.DisallowStartIfOnBatteries -eq 'false') 'Startup must work on battery.'
Assert ($xml.Task.Settings.RunOnlyIfNetworkAvailable -eq 'false') 'Startup must work offline.'
Assert ($xml.Task.Settings.ExecutionTimeLimit -eq 'PT0S') 'Dock must not be killed after default task time limit.'
$releaseType = $assembly.GetType('GamesDock.UpdRelease')
$assetType = $assembly.GetType('GamesDock.UpdAsset')
$release = [Activator]::CreateInstance($releaseType)
$listType = [System.Collections.Generic.List``1].MakeGenericType($assetType)
$release.Assets = [Activator]::CreateInstance($listType)
$setup = [Activator]::CreateInstance($assetType)
$setup.Name = 'GamesDock-Setup-1.9.5.exe'
$release.Assets.Add($setup)
$choose = $assembly.GetType('GamesDock.Update').GetMethod('Elegir', $flags)
Assert ($null -eq $choose.Invoke($null, @($release))) 'Updater must never replace dock with setup wizard.'
$binary = [Activator]::CreateInstance($assetType)
$binary.Name = 'GamesDock.exe'
$release.Assets.Add($binary)
Assert ([object]::ReferenceEquals($binary, $choose.Invoke($null, @($release)))) 'Updater must select exact GamesDock.exe asset.'
Assert ($assembly.GetName().Version.ToString() -eq '1.9.5.0') 'Incorrect assembly version.'
if ($Integration) {
    function Configure([string]$mode) {
        $process = Start-Process -FilePath $exe -ArgumentList '--configure-startup', $mode -WindowStyle Hidden -Wait -PassThru
        Assert ($process.ExitCode -eq 0) "Startup command failed: $mode"
    }
    $sid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    $service = New-Object -ComObject Schedule.Service
    $service.Connect()
    $folder = $service.GetFolder('\')
    $name = "GamesDock-$sid"
    $startupDir = [Environment]::GetFolderPath('Startup')
    $dataDir = Join-Path ([Environment]::GetFolderPath('ApplicationData')) 'GamesDock'
    New-Item -ItemType Directory -Force $dataDir, $startupDir | Out-Null
    $legacy = Join-Path $startupDir 'GamesDock.lnk'
    $vbs = Join-Path $dataDir 'GamesDock.lanzador.vbs'
    Set-Content $legacy 'old broken shortcut'
    Set-Content $vbs 'old broken VBScript'
    try {
        Configure on
        [xml]$registered = $folder.GetTask($name).Xml
        Assert ($registered.Task.Actions.Exec.Command -eq (Resolve-Path $exe).Path) 'Actual registered task points to wrong executable.'
        Assert (-not (Test-Path $legacy)) 'Legacy shortcut remains.'
        Assert (-not (Test-Path $vbs)) 'Legacy VBScript remains.'
        Configure on
        Assert (($folder.GetTask($name)).Enabled) 'Repeated registration must remain enabled.'
        Configure off
        $missing = $false
        try { $null = $folder.GetTask($name) } catch { $missing = $true }
        Assert $missing 'Task remains after disabling startup.'
        Configure off
    } finally {
        try { $folder.DeleteTask($name, 0) } catch { }
    }
}
Write-Host 'PASS: startup XML, Unicode paths, no logon delay, normal priority, battery/offline support, updater asset selection, version.'
if ($Integration) { Write-Host 'PASS: real task registration, legacy migration, repeated enable/disable.' }
