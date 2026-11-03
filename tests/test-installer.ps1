$ErrorActionPreference = 'Stop'
function Assert($condition, [string]$message) { if (-not $condition) { throw $message } }
$setup = (Resolve-Path "$PSScriptRoot\..\dist\GamesDock-Setup-1.9.5.exe").Path
$installDir = Join-Path $env:LOCALAPPDATA 'Programs\GamesDock'
$log = Join-Path $env:TEMP 'GamesDock-setup-test.log'
$process = Start-Process $setup -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/TASKS=startup', "/LOG=$log" -Wait -PassThru -WindowStyle Hidden
Assert ($process.ExitCode -eq 0) "Installer failed: $($process.ExitCode). See $log"
$installed = Join-Path $installDir 'GamesDock.exe'
Assert (Test-Path $installed) 'Installed executable missing.'
Assert ((Get-FileHash $installed).Hash -eq (Get-FileHash "$PSScriptRoot\..\app\GamesDock.exe").Hash) 'Installed executable differs from release.'
$sid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
$task = Get-ScheduledTask -TaskName "GamesDock-$sid"
Assert ($task.Actions.Execute -eq $installed) 'Installer did not register stable installation path.'
# Launch the real task action. This also exercises --startup and a working directory unrelated to source.
Start-ScheduledTask -TaskName "GamesDock-$sid"
$running = $null
$rendered = $false
$dockLog = Join-Path $env:APPDATA 'GamesDock\dock.log'
for ($attempt = 0; $attempt -lt 30; $attempt++) {
    Start-Sleep -Milliseconds 500
    $running = Get-Process GamesDock -ErrorAction SilentlyContinue
    if ($running -and (Test-Path $dockLog)) {
        $logText = Get-Content $dockLog -Raw
        $position = $logText.LastIndexOf("Arranque PID $($running.Id) ")
        $rendered = $position -ge 0 -and $logText.Substring($position).Contains('Primera imagen:')
        if ($rendered) { break }
    }
}
Assert ($running -and $rendered) 'Task action did not show GamesDock.'
$shutdown = Start-Process $installed -ArgumentList '--shutdown' -Wait -PassThru -WindowStyle Hidden
Wait-Process -Id $running.Id -Timeout 15
$config = Join-Path $env:APPDATA 'GamesDock\config.json'
$preferences = Get-Content $config -Raw | ConvertFrom-Json
$preferences.language = 'en'
$preferences.iconSize = 72
$preferences.locked = $true
$preferences | ConvertTo-Json -Depth 50 -Compress | Set-Content -Encoding UTF8 $config
# Reinstall after moving the downloaded setup: installed location and startup must stay stable.
$movedSetup = Join-Path $env:TEMP 'GamesDock moved setup.exe'
Copy-Item $setup $movedSetup
$upgrade = Start-Process $movedSetup -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/TASKS=startup' -Wait -PassThru -WindowStyle Hidden
Assert ($upgrade.ExitCode -eq 0) 'Reinstall failed.'
Assert ((Get-ScheduledTask -TaskName "GamesDock-$sid").Actions.Execute -eq $installed) 'Moving setup changed startup location.'
$config = Join-Path $env:APPDATA 'GamesDock\config.json'
$retained = Get-Content $config -Raw | ConvertFrom-Json
Assert ($retained.language -eq 'en' -and $retained.iconSize -eq 72 -and $retained.locked) 'Reinstall changed user preferences.'
$uninstall = Start-Process (Join-Path $installDir 'unins000.exe') -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART' -Wait -PassThru -WindowStyle Hidden
Assert ($uninstall.ExitCode -eq 0) 'Uninstall failed.'
Assert (-not (Test-Path $installed)) 'Executable remains after uninstall.'
Assert (-not (Get-ScheduledTask -TaskName "GamesDock-$sid" -ErrorAction SilentlyContinue)) 'Startup task remains after uninstall.'
Assert (Test-Path $config) 'Uninstall deleted user settings.'
$retained = Get-Content $config -Raw | ConvertFrom-Json
Assert ($retained.language -eq 'en' -and $retained.iconSize -eq 72 -and $retained.locked) 'Uninstall changed user preferences.'
Assert (-not $retained.autostart) 'Uninstall did not disable startup preference.'
Write-Host 'PASS: install, stable location, task launch, graceful exit, upgrade, uninstall, preserved user settings.'
