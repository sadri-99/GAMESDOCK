#Requires -Version 5.0
[CmdletBinding()]
param(
    [switch]$SoloDeteccion
)

$ErrorActionPreference = 'Continue'
$ProgressPreference    = 'SilentlyContinue'

Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

if (-not ('GD32' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class GD32 {
    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("kernel32.dll")] public static extern bool SetProcessWorkingSetSize(IntPtr p, IntPtr min, IntPtr max);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int idx);
    [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr h, int idx, int val);
}
public static class GDIco {
    // ExtractAssociatedIcon solo sabe pedir 32x32. SHDefExtractIcon deja pedir el tamano real del
    // recurso (256 en casi cualquier exe moderno), que es lo que hace falta cuando el magnes agranda
    // el icono por encima de su medida de reposo. PrivateExtractIcons no esta exportada por nombre
    // en este shell32, y las importaciones por ordinal no las admite el CLR de PowerShell 5.1.
    [DllImport("shell32.dll", CharSet=CharSet.Unicode, EntryPoint="SHDefExtractIconW")]
    public static extern int Extract(string file, int index, uint flags, out IntPtr big, out IntPtr small, uint size);
    [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr h);
}
'@ -ErrorAction SilentlyContinue
}

# El contorno de la pildora se rehace en cada cuadro de la animacion. Muestreado y montado en
# PowerShell costaba ~17 ms por cuadro (el 90 % del presupuesto), lo que topaba el magnes a ~43 fps
# en un panel de 165 Hz y dejaba el hilo de UI al 100 % de un nucleo. Compilado, cuesta ~0,1 ms.
if (-not ('GDPill' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

public static class GDPill {
    const double PI = Math.PI;

    static double HalfAt(double m, double baseH, double uni, double amp, double ptr, double r, double peak) {
        if (amp <= 0.001) return baseH + uni;
        if (Math.Abs(m - ptr) >= r) return baseH + uni;
        double a = Math.Abs(ptr - m) / r;
        if (a >= 1.0) return baseH + uni;
        double c = Math.Cos(a * PI / 2.0);
        double s = 1.0 + (peak - 1.0) * c * c;
        return baseH + uni + amp * ((s - 1.0) / (peak - 1.0));
    }

    static void Add(List<double> ru, List<double> rv, double u, double v) {
        if (ru.Count == 0 || Math.Abs(ru[ru.Count - 1] - u) > 0.4 || Math.Abs(rv[rv.Count - 1] - v) > 0.4) {
            ru.Add(u); rv.Add(v);
        }
    }

    // spline cerrada de Catmull-Rom convertida a cubicas, con las tangentes ponderadas por longitud
    // de segmento para que el bulto del magnes no estire las tapas
    static void Spline(StreamGeometryContext ctx, Point[] p) {
        int n = p.Length;
        if (n < 3) return;
        double[] dx = new double[n], dy = new double[n], dl = new double[n];
        for (int i = 0; i < n; i++) {
            int j = i + 1; if (j >= n) j = 0;
            dx[i] = p[j].X - p[i].X; dy[i] = p[j].Y - p[i].Y;
            double l = Math.Sqrt(dx[i] * dx[i] + dy[i] * dy[i]);
            dl[i] = l < 1e-3 ? 1e-3 : l;
        }
        double[] tx = new double[n], ty = new double[n];
        for (int i = 0; i < n; i++) {
            int im = i - 1; if (im < 0) im = n - 1;
            double h0 = dl[im], h1 = dl[i];
            tx[i] = ((dx[im] / h0) * h1 + (dx[i] / h1) * h0) / (h0 + h1);
            ty[i] = ((dy[im] / h0) * h1 + (dy[i] / h1) * h0) / (h0 + h1);
        }
        ctx.BeginFigure(p[0], true, true);
        List<Point> bez = new List<Point>(n * 3);
        for (int i = 0; i < n; i++) {
            int ip = i + 1; if (ip >= n) ip = 0;
            bez.Add(new Point(p[i].X + tx[i] / 3.0, p[i].Y + ty[i] / 3.0));
            bez.Add(new Point(p[ip].X - tx[ip] / 3.0, p[ip].Y - ty[ip] / 3.0));
            bez.Add(new Point(p[ip].X, p[ip].Y));
        }
        ctx.PolyBezierTo(bez, true, true);
    }

    // overL/overR sacan las tapas hacia fuera, dentro de la banda transparente de la ventana: el
    // icono de un extremo, agrandado, pisa el ultimo tramo de la tapa y ninguna curva que cierre en
    // punta lo puede cubrir. Cada lado sale lo que entra su propio icono, para no dejar vidrio de
    // sobra en el otro. Nada de esto toca la medida de reposo (over=0 => la pildora de siempre).
    public static Geometry Build(double len, double cc, double baseH, double uni, double amp,
                                 double ptr, double pitch, double peak, bool vert,
                                 double overL, double overR) {
        double L = len + overL + overR;
        double p0 = ptr + overL;
        double r = pitch * 2.4;
        double hL = HalfAt(0.0, baseH, uni, amp, p0, r, peak);
        double hR = HalfAt(L, baseH, uni, amp, p0, r, peak);
        // Las tapas son semielipses de radio horizontal anclado al grosor de reposo y radio vertical
        // el grosor real. Con semicirculo puro (radio = grosor actual) la tapa se abre mas de lo que
        // sube su borde, y el icono del extremo agrandado la muerde por la esquina; al mantener el
        // radio horizontal la esquina se levanta con la pildora y el icono queda dentro.
        double eL = Math.Min(hL, baseH), eR = Math.Min(hR, baseH);
        double tL = HalfAt(eL, baseH, uni, amp, p0, r, peak);
        double tR = HalfAt(L - eR, baseH, uni, amp, p0, r, peak);
        double a0 = Math.Max(eL, p0 - r);
        double a1 = Math.Min(L - eR, p0 + r);
        if (amp <= 0.001) { a0 = eL; a1 = eL; }
        double paso = Math.Max(6.0, r / 16.0);

        List<double> us = new List<double>(), hs = new List<double>();
        us.Add(eL); hs.Add(tL);
        double m = a0;
        while (m < a1) {
            if (m > us[us.Count - 1] + 0.5) { us.Add(m); hs.Add(HalfAt(m, baseH, uni, amp, p0, r, peak)); }
            m += paso;
        }
        if (a1 > us[us.Count - 1] + 0.5) { us.Add(a1); hs.Add(HalfAt(a1, baseH, uni, amp, p0, r, peak)); }
        double top = L - eR;
        if (top > us[us.Count - 1] + 0.5) { us.Add(top); hs.Add(tR); }

        List<double> ru = new List<double>(), rv = new List<double>();
        for (int i = 0; i < us.Count; i++) Add(ru, rv, us[i], cc - hs[i]);
        for (int k = 1; k <= 13; k++) {
            double f = PI * (k / 13.0);
            Add(ru, rv, top + eR * Math.Sin(f), cc - tR * Math.Cos(f));
        }
        for (int i = us.Count - 1; i >= 0; i--) Add(ru, rv, us[i], cc + hs[i]);
        for (int k = 1; k <= 13; k++) {
            double f = PI * (k / 13.0);
            Add(ru, rv, eL - eL * Math.Sin(f), cc + tL * Math.Cos(f));
        }

        Point[] pts = new Point[ru.Count];
        for (int i = 0; i < ru.Count; i++) {
            double u = ru[i] - overL;
            pts[i] = vert ? new Point(rv[i], u) : new Point(u, rv[i]);
        }

        StreamGeometry geo = new StreamGeometry();
        using (StreamGeometryContext ctx = geo.Open()) { Spline(ctx, pts); }
        geo.Freeze();
        return geo;
    }
}
'@ -ReferencedAssemblies @([System.Windows.Media.Geometry].Assembly.Location, [System.Windows.Point].Assembly.Location)
}

# ------------------------------------------------------------------ rutas basicas
$script:Root    = Join-Path $env:APPDATA 'GamesDock'
$script:IconDir = Join-Path $script:Root 'icons'
$script:DeskDir = Join-Path $script:Root 'escritorio'
$script:CfgFile = Join-Path $script:Root 'config.json'
$script:LogFile = Join-Path $script:Root 'dock.log'
$null = New-Item -ItemType Directory -Force -Path $script:Root, $script:IconDir, $script:DeskDir

function Write-Log {
    param([string]$Message)
    try {
        if ((Test-Path -LiteralPath $script:LogFile) -and (Get-Item -LiteralPath $script:LogFile).Length -gt 262144) {
            Clear-Content -LiteralPath $script:LogFile
        }
        Add-Content -LiteralPath $script:LogFile -Value ("[{0}] {1}" -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $Message) -Encoding UTF8
    } catch { }
}

function Trim-Memory {
    $null = [GC]::Collect(); $null = [GC]::WaitForPendingFinalizers(); $null = [GC]::Collect()
    try {
        # -1 = volver a los limites por defecto: suelta las paginas en frio y devuelve RAM al sistema
        $null = [GD32]::SetProcessWorkingSetSize([IntPtr](-1), [IntPtr](-1), [IntPtr](-1))
    } catch { }
}

Write-Log ("Arranque PID " + $PID + " (STA=" + ([Threading.Thread]::CurrentThread.GetApartmentState()) + ")")

$script:Single = New-Object System.Threading.Mutex($false, 'GamesDock_Single_Instance')
if (-not $script:Single.WaitOne(0)) {
    Write-Log 'Segunda instancia: cancelada.'
    exit 0
}

# ------------------------------------------------------------------ configuracion
$script:Cfg = [ordered]@{
    version      = 1
    position     = 'bottom'
    left         = $null
    top          = $null
    iconSize     = 46
    pillPad      = 2
    theme        = 'noche'
    magnify      = $true
    idleOpacity  = 45
    minimizeAll  = $true
    hideOnLaunch = $true
    hotkey       = $true
    desktopLevel = $true
    section      = 'games'
    order        = @()
    pins         = @()
    desktopMoved = @{}
}
$script:Usage = @{}
$script:Pins  = @{}
$script:PosMap = @{ 'Abajo' = 'bottom'; 'Arriba' = 'top'; 'Izquierda' = 'left'; 'Derecha' = 'right'; 'Libre' = 'free' }

function Read-Config {
    if (-not (Test-Path -LiteralPath $script:CfgFile)) { return }
    try {
        $j = Get-Content -LiteralPath $script:CfgFile -Raw -Encoding UTF8 | ConvertFrom-Json
        foreach ($k in 'position', 'iconSize', 'pillPad', 'theme', 'magnify', 'idleOpacity', 'minimizeAll', 'hideOnLaunch', 'hotkey', 'desktopLevel', 'section', 'left', 'top') {
            $p = $j.PSObject.Properties[$k]
            if ($null -ne $p -and $null -ne $p.Value) { $script:Cfg[$k] = $p.Value }
        }
        if ($null -ne $j.order) {
            # el orden manual es una lista de claves: lo que no esta aqui se ordena por uso
            $script:Cfg['order'] = @($j.order | ForEach-Object { [string]$_ })
        }
        if ($null -ne $j.pins) {
            foreach ($p in @($j.pins)) {
                if ($null -ne $p -and $p.key) { $script:Pins[[string]$p.key] = $p }
            }
        }
        if ($null -ne $j.usage) {
            foreach ($pr in $j.usage.PSObject.Properties) {
                $script:Usage[$pr.Name] = @{ n = [int]$pr.Value.n; d = [string]$pr.Value.d }
            }
        }
        if ($null -ne $j.desktopMoved) {
            foreach ($pr in $j.desktopMoved.PSObject.Properties) { $script:Cfg['desktopMoved'][$pr.Name] = [string]$pr.Value }
        }
    } catch {
        Write-Log "Lectura de config fallida: $($_.Exception.Message)"
    }
    Write-Log ("Config leida: {0} fijados, {1} usos." -f $script:Pins.Count, $script:Usage.Count)
}

function Write-Config {
    try {
        $pins = @($script:Pins.Values | ForEach-Object {
            [ordered]@{
                key    = $_.key
                name   = $_.name
                fav    = [bool]$_.fav
                hidden = [bool]$_.hidden
                link   = $_.link
                target = $_.target
                args   = $_.args
                work   = $_.work
                origin = $_.origin
                kind   = $_.kind
                icon   = $_.icon
                added  = $_.added
            }
        })
        $o = [ordered]@{ usage = $script:Usage; pins = $pins; desktopMoved = $script:Cfg['desktopMoved'] }
        foreach ($k in $script:Cfg.Keys) {
            if ($k -notin 'desktopMoved', 'pins') { $o[$k] = $script:Cfg[$k] }
        }
        $json = $o | ConvertTo-Json -Depth 8
        $tmp = "$script:CfgFile.tmp"
        Set-Content -LiteralPath $tmp -Value $json -Encoding UTF8
        Move-Item -LiteralPath $tmp -Destination $script:CfgFile -Force
    } catch {
        Write-Log "Escritura de config fallida: $($_.Exception.Message)"
    }
}

# ------------------------------------------------------------------ utilidades
function Br([string]$Hex) {
    $c = [System.Windows.Media.ColorConverter]::ConvertFromString($Hex)
    $b = New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.Color]$c)
    $b.Freeze(); return $b
}

# misma brocha pero escalando el alfa: evita tener que inventarse un color nuevo por tema
function BrA([string]$Hex, [double]$Mult) {
    $c = [System.Windows.Media.Color][System.Windows.Media.ColorConverter]::ConvertFromString($Hex)
    $a = [int]([Math]::Max(0.0, [Math]::Min(255.0, ([double]$c.A * $Mult))))
    $b = New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.Color]::FromArgb($a, $c.R, $c.G, $c.B))
    $b.Freeze(); return $b
}

function New-Spec {
    param($Key, $Name, $Origin, $Kind, $Link, $Target, $ArgLine, $Work, $Icon, $IconHint, $AppId)
    [pscustomobject]@{
        Key = $Key; Name = $Name; Origin = $Origin; Kind = $Kind
        Link = $Link; Target = $Target; Arguments = $ArgLine; Work = $Work
        Icon = $Icon; IconHint = $IconHint; AppId = $AppId
    }
}

$script:Wsh = New-Object -ComObject WScript.Shell

function Resolve-Link([string]$Path) {
    try {
        $s = $script:Wsh.CreateShortcut($Path)
        $r = [pscustomobject]@{
            Target  = $s.TargetPath
            Args    = $s.Arguments
            Work    = $s.WorkingDirectory
            IconRaw = $s.IconLocation
        }
        $null = [System.Runtime.InteropServices.Marshal]::ReleaseComObject($s)
        $r
    } catch { $null }
}

$script:NoiseName = '(?i)uninstall|remove|desinstal|documentation|readme|licen|support|soporte|manual|prerequisit|prerrequi|directx|vulkan|visual c\+\+|redistribut|dotnet framework|\.net framework|anticheat|easy anti|battle eye|game controller|update tool|dedicated server|server edit'
$script:NoiseDir  = '(?i)\\accessories\\|\\accesorios\\|\\administrative tools\\|\\system tools\\|\\utilidades\\|\\mantenimiento\\|\\maintenance\\|\\windows powershell\\|\\startup\\|\\inicio\\|\\one drive\\|\\onenote\\|\\excel\\|\\word\\|\\powerpoint\\|\\teams\\|\\skype\\|\\navegadores\\|\\tools\\'
$script:GameDirRe = '(?i)\\steamapps\\common\\|\\epic games\\|\\gog\.com\\|gog galaxy\\games\\|\\jeux\\|\\juegos\\|\\games\\'
$script:Markers   = 'steam_api64.dll', 'steam_api.dll', 'EOSSDK-Win64-Shipping.dll', 'GalaxyPeer64.dll', 'UnityPlayer.dll', 'GameAssembly.dll', 'upc_rloader.dll', 'RGLConfig.ini'
$script:DirIsGame = @{}

function Test-IsGameDir([string]$ExePath) {
    if (-not $ExePath) { return $false }
    if ($ExePath -match $script:GameDirRe) { return $true }
    try { $dir = Split-Path -Parent $ExePath } catch { return $false }
    if (-not $dir) { return $false }
    if ($script:DirIsGame.ContainsKey($dir)) { return $script:DirIsGame[$dir] }
    $probe = @($dir)
    try { $parent = Split-Path -Parent $dir; if ($parent) { $probe += $parent } } catch { }
    $found = $false
    foreach ($p in $probe) {
        foreach ($m in $script:Markers) {
            if (Test-Path -LiteralPath (Join-Path $p $m)) { $found = $true; break }
        }
        if ($found) { break }
    }
    $script:DirIsGame[$dir] = $found
    return $found
}

function Clean-Name([string]$Raw) {
    $n = $Raw -replace '\.(lnk|exe|bat|cmd|url|appref-ms)$', '' -replace ' - (Acceso directo|Shortcut|Direct link)$', ''
    $n = $n -replace '\s*\[(GOG\.com|Steam|Epic Games)\]\s*$', '' -replace '^&', ''
    return $n.Trim()
}

# ------------------------------------------------------------------ escaner Steam
function Find-SteamGames {
    $out = @()
    try {
        $reg = Get-ItemProperty -Path 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue
        if (-not $reg -or -not $reg.SteamPath) { return $out }
        $root = (Get-Item -LiteralPath ($reg.SteamPath -replace '\\', '/')).FullName
    } catch { return $out }

    $libs = New-Object System.Collections.Generic.List[string]
    $libs.Add($root)
    $vdf = Join-Path $root 'config\libraryfolders.vdf'
    if (Test-Path -LiteralPath $vdf) {
        foreach ($line in (Get-Content -LiteralPath $vdf -ErrorAction SilentlyContinue)) {
            if ($line -match '"path"\s+"([^"]+)"') {
                $cand = $matches[1] -replace '\\\\', '\'
                if ((Test-Path -LiteralPath $cand -ErrorAction SilentlyContinue) -and -not ($libs -contains $cand)) { $libs.Add($cand) }
            }
        }
    }

    $cache = Join-Path $root 'appcache\librarycache'
    foreach ($lib in $libs) {
        $sa = Join-Path $lib 'steamapps'
        if (-not (Test-Path -LiteralPath $sa)) { continue }
        foreach ($f in (Get-ChildItem -LiteralPath $sa -Filter 'appmanifest_*.acf' -ErrorAction SilentlyContinue)) {
            $h = @{}
            foreach ($line in (Get-Content -LiteralPath $f.FullName -Encoding UTF8 -ErrorAction SilentlyContinue)) {
                if ($line -match '^\s*"([^"]+)"\s+"(.*)"\s*$') { $h[$matches[1].ToLower()] = $matches[2] }
            }
            $appid = $h['appid']; $name = $h['name']
            if (-not $appid -or -not $name) { continue }
            if ($name -match '(?i)dedicated server|source sdk|common redistributables|proton|beta') { continue }
            $icon = $null
            foreach ($c in 'library_600x900.jpg', 'header.jpg', 'logo.png') {
                $p = Join-Path (Join-Path $cache $appid) $c
                if (Test-Path -LiteralPath $p) { $icon = $p; break }
            }
            $out += New-Spec -Key "steam:$appid" -Name $name -Origin 'steam' -Kind 'game' `
                -Target (Join-Path $sa "common\$($h['installdir'])") -Icon $icon -IconHint (Join-Path $root 'steam.exe') -AppId $appid
        }
    }
    return $out
}

# ------------------------------------------------------------------ escaner Epic
function Find-EpicGames {
    $out  = @()
    $seen = @{}
    $content = Join-Path $env:ProgramData 'Epic\EpicGamesLauncher\Data\Content'

    foreach ($d in (Join-Path $env:ProgramData 'Epic\EpicGamesLauncher\Data\Manifests'),
                   (Join-Path $env:LOCALAPPDATA 'Epic\EpicGamesLauncher\Data\Manifests')) {
        foreach ($f in (Get-ChildItem -LiteralPath $d -Filter '*.item' -ErrorAction SilentlyContinue)) {
            try {
                $j = Get-Content -LiteralPath $f.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
            } catch { continue }
            $app = [string]$j.AppName
            if (-not $app -or $seen.ContainsKey($app)) { continue }
            if ($app -match '(?i)launcher|prerequisite|redistributable|directx|vulkan|eac|bec|unity|unreal|modding' -and -not $j.FriendlyName) { continue }
            $name = if ($j.FriendlyName) { [string]$j.FriendlyName } else { Clean-Name $app }
            $loc  = if ($j.InstallLocation) { [string]$j.InstallLocation } else { $null }
            $exe  = if ($j.LaunchExecutable) { [string]$j.LaunchExecutable } else { $null }
            $target = if ($loc -and $exe) { Join-Path $loc $exe } elseif ($loc) { $loc } else { $null }
            if (-not $target -or -not (Test-Path -LiteralPath $target)) { continue }
            $seen[$app] = $true
            $icon = (Get-ChildItem -LiteralPath $content -Filter "*${app}*Icon*.png" -ErrorAction SilentlyContinue | Select-Object -First 1).FullName
            $out += New-Spec -Key "epic:$app" -Name $name -Origin 'epic' -Kind 'game' `
                -Target $target -Work $loc -Icon $icon -IconHint $target
        }
    }

    # LauncherInstalled.dat cubre juegos registrados aunque no exista manifest .item
    foreach ($dat in (Join-Path $env:ProgramData 'Epic\UnrealEngineLauncher\LauncherInstalled.dat'),
                     (Join-Path $env:LOCALAPPDATA 'Epic\UnrealEngineLauncher\LauncherInstalled.dat')) {
        if (-not (Test-Path -LiteralPath $dat)) { continue }
        try {
            $j = Get-Content -LiteralPath $dat -Raw -Encoding UTF8 | ConvertFrom-Json
        } catch { continue }
        foreach ($e in @($j.InstallationList)) {
            if ($null -eq $e -or -not $e.AppName) { continue }
            $app = [string]$e.AppName
            if ($seen.ContainsKey($app)) { continue }
            $loc = if ($e.InstallLocation) { [string]$e.InstallLocation } else { $null }
            if (-not $loc -or -not (Test-Path -LiteralPath $loc)) { continue }
            $exe = Get-ChildItem -LiteralPath $loc -Filter '*.exe' -Recurse -Depth 2 -ErrorAction SilentlyContinue |
                Where-Object { $_.Length -gt 300000 -and $_.Name -notmatch '(?i)unins|setup|install|crash|redist' } |
                Sort-Object Length -Descending | Select-Object -First 1
            if (-not $exe) { continue }
            $name = if ($e.AppName -match '[a-z]') { Clean-Name $e.AppName } else { $e.AppName }
            $seen[$app] = $true
            $icon = (Get-ChildItem -LiteralPath $content -Filter "*${app}*Icon*.png" -ErrorAction SilentlyContinue | Select-Object -First 1).FullName
            $out += New-Spec -Key "epic:$app" -Name $name -Origin 'epic' -Kind 'game' `
                -Target $exe.FullName -Work $exe.DirectoryName -Icon $icon
        }
    }
    return $out
}

# ------------------------------------------------------------------ escaner GOG
function Find-GogGames {
    $out = @()
    foreach ($hive in 'HKLM:\SOFTWARE\WOW6432Node\GOG.com\Games', 'HKLM:\SOFTWARE\GOG.com\Games', 'HKCU:\SOFTWARE\GOG.com\Games') {
        if (-not (Test-Path -LiteralPath $hive)) { continue }
        foreach ($k in (Get-ChildItem -LiteralPath $hive -ErrorAction SilentlyContinue)) {
            $v = Get-ItemProperty -LiteralPath $k.PSPath -ErrorAction SilentlyContinue
            $name = if ($v.gameName) { [string]$v.gameName } elseif ($v.DisplayName) { [string]$v.DisplayName } else { $null }
            $exe  = if ($v.exe) { [string]$v.exe } else { $null }
            if (-not $exe -and $v.launchCommand) {
                $lc = [string]$v.launchCommand
                $exe = if ($lc -match '^"([^"]+)"') { $matches[1] } else { ($lc -split '\s+')[0] }
            }
            if (-not $name -or -not $exe) { continue }          # DLCs y componentes sin ejecutable propio
            if ($exe -notmatch '\\') {
                $base = if ($v.path) { [string]$v.path } else { $null }
                if ($base) { $exe = Join-Path $base $exe }
            }
            if (-not (Test-Path -LiteralPath $exe)) { continue }
            $work = if ($v.workingDir) { [string]$v.workingDir } elseif ($v.working_dir) { [string]$v.working_dir } else { Split-Path -Parent $exe }
            $out += New-Spec -Key ("gog:{0}" -f $k.PSChildName) -Name (Clean-Name $name) -Origin 'gog' -Kind 'game' `
                -Target $exe -Work $work -IconHint $exe
        }
    }
    return $out
}

# ------------------------------------------------------------------ accesos directos
function Find-LinkApps {
    param([string[]]$Roots, [string]$Origin, [switch]$Recurse)
    $exts = '.lnk', '.appref-ms', '.url', '.bat', '.cmd', '.exe'
    $out = @()
    foreach ($r in $Roots) {
        if (-not $r -or -not (Test-Path -LiteralPath $r)) { continue }
        $files = New-Object 'System.Collections.Generic.List[object]'
        foreach ($ext in $exts) {
            # -Filter evita materializar cada archivo del arbol: solo los que sirven
            if ($Recurse) {
                $files.AddRange(@(Get-ChildItem -LiteralPath $r -Filter "*$ext" -Recurse -File -ErrorAction SilentlyContinue))
            } else {
                $files.AddRange(@(Get-ChildItem -LiteralPath $r -Filter "*$ext" -File -ErrorAction SilentlyContinue))
            }
        }
        foreach ($f in $files) {
            try {
                $name = Clean-Name $f.Name
                if (-not $name) { continue }
                $rel = $f.FullName
                if ($rel -match $script:NoiseDir -or $name -match $script:NoiseName) { continue }

                $link = $null; $target = $null; $argLine = $null; $work = $null; $hint = $null; $kind = 'app'

                if ($f.Extension -eq '.lnk') {
                    $s = Resolve-Link $f.FullName
                    if (-not $s -or -not $s.Target) { continue }
                    $link = $f.FullName; $target = $s.Target; $argLine = $s.Args; $work = $s.Work
                    if ($target -match '(?i)^https?://|\.url$') { continue }
                    $hint = $target
                    $il = $s.IconRaw -split ','
                    if ($il[0] -and $il[0] -match '\.(ico|exe|dll)$' -and (Test-Path -LiteralPath $il[0])) { $hint = $il[0] }
                } elseif ($f.Extension -eq '.url') {
                    $raw = (Get-Content -LiteralPath $f.FullName -TotalCount 20 -Encoding UTF8 -ErrorAction SilentlyContinue |
                        Where-Object { $_ -match '^URL=(.+)$' } | Select-Object -First 1)
                    if (-not $raw) { continue }
                    $url = $raw -replace '^URL=', ''
                    if ($url -match '(?i)^steam://rungameid/(\d+)') {
                        $out += New-Spec -Key "steam:$($matches[1])" -Name $name -Origin 'steam' -Kind 'game' -Link $f.FullName -AppId $matches[1]
                        continue
                    }
                    $link = $url; $target = $f.FullName; $hint = $f.FullName
                } else {
                    $target = $f.FullName; $work = $f.DirectoryName; $hint = $f.FullName
                    if ($f.Extension -eq '.exe') { $link = $f.FullName }
                }

                if ($argLine -match '-appid\s*(\d+)') {
                    $kind = 'game'
                    $out += New-Spec -Key "steam:$($matches[1])" -Name $name -Origin 'steam' -Kind 'game' -Link $link -Target $target
                    continue
                }
                if ($link -and $target -match '(?i)\\steam\.exe$' -and $argLine -match '-exec') { continue }

                if ($Origin -eq 'start' -and $target -match '(?i)steam\.exe$') { continue }
                if (Test-IsGameDir $target) { $kind = 'game' }

                $key = ($target.ToLowerInvariant() + '|' + ($argLine -replace '\s+', ' ').Trim().ToLowerInvariant())
                $out += New-Spec -Key $key -Name $name -Origin $Origin -Kind $kind -Link $link -Target $target `
                    -ArgLine $argLine -Work $work -IconHint $hint
            } catch { }
        }
    }
    return $out
}

function Get-DesktopDirs {
    $d = New-Object System.Collections.Generic.List[string]
    try { $d.Add([Environment]::GetFolderPath('Desktop')) } catch { }
    try {
        $u = (Get-Item -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders').GetValue('Desktop')
        if ($u) {
            $u = [Environment]::ExpandEnvironmentVariables([string]$u)
            if (-not ($d -contains $u)) { $d.Add($u) }
        }
    } catch { }
    if (-not ($d -contains $script:DeskDir)) { $d.Add($script:DeskDir) }
    return @($d)
}

# ------------------------------------------------------------------ iconos
function Get-IconFile([object]$spec) {
    if ($spec.Icon -and $spec.Icon -match '(?i)\.(png|jpg|jpeg|ico)$' -and (Test-Path -LiteralPath $spec.Icon)) { return $spec.Icon }
    $hint = $spec.IconHint
    if (-not $hint -or -not (Test-Path -LiteralPath $hint)) { $hint = $spec.Target }
    if (-not $hint -and $spec.Target -and $spec.Target -match '\.(exe|ico|dll)$' -and (Test-Path -LiteralPath $spec.Target)) { $hint = $spec.Target }
    if ($hint) {
        try { $fi = Get-Item -LiteralPath $hint -ErrorAction Stop } catch { $fi = $null }
    } else { $fi = $null }
    if (-not $fi) { return $null }

    $seed = $hint.ToLowerInvariant() + '|' + $fi.LastWriteTimeUtc.Ticks + '|256'
    $md5  = [System.BitConverter]::ToString([System.Security.Cryptography.MD5]::Create().ComputeHash([System.Text.Encoding]::UTF8.GetBytes($seed)))
    $png  = Join-Path $script:IconDir (($md5 -replace '-', '').Substring(0, 24).ToLowerInvariant() + '.png')
    if (Test-Path -LiteralPath $png) { return $png }

    # primera intentona: el recurso grande del exe/dll/ico, con su canal alfa intacto
    $big = [IntPtr]::Zero; $small = [IntPtr]::Zero
    try {
        if ([GDIco]::Extract($hint, 0, 0, [ref]$big, [ref]$small, 256) -eq 0 -and $big -ne [IntPtr]::Zero) {
            $ico = [System.Drawing.Icon]::FromHandle($big)
            $bmp = $ico.ToBitmap()
            $ok = $bmp.Width -ge 48
            if ($ok) { $bmp.Save($png, [System.Drawing.Imaging.ImageFormat]::Png) }
            $bmp.Dispose()
            if ($ok) { return $png }
        }
    } catch { Write-Log "extract 256 fallida para ${hint}: $($_.Exception.Message)" }
    finally {
        # FromHandle no es dueño del handle: si nadie lo cierra, cada icono extraido deja dos GDI
        if ($big -ne [IntPtr]::Zero)   { $null = [GDIco]::DestroyIcon($big) }
        if ($small -ne [IntPtr]::Zero) { $null = [GDIco]::DestroyIcon($small) }
    }

    try {
        $ico = [System.Drawing.Icon]::ExtractAssociatedIcon($hint)
        if ($ico) {
            $bmp = $ico.ToBitmap()
            $bmp.Save($png, [System.Drawing.Imaging.ImageFormat]::Png)
            $bmp.Dispose(); $ico.Dispose()
            return $png
        }
    } catch {
        Write-Log "Icono no extraible para $hint"
    }
    return $png   # puede no existir: el tile usara la inicial como reserva
}

# ------------------------------------------------------------------ construccion del indice
function Build-Index {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $specs = @()
    $steam = Find-SteamGames
    $epic  = Find-EpicGames
    $gog   = Find-GogGames
    $start = Find-LinkApps -Recurse -Roots @("$env:APPDATA\Microsoft\Windows\Start Menu\Programs",
                                            "$env:ProgramData\Microsoft\Windows\Start Menu\Programs") -Origin 'start'
    $desk  = Find-LinkApps -Roots (Get-DesktopDirs) -Origin 'desktop'
    $specs = @($steam) + @($epic) + @($gog) + @($desk) + @($start)

    Write-Log ("Deteccion: steam={0} epic={1} gog={2} startmenu={3} escritorio={4} antes de dedupe={5}" -f `
        @($steam).Count, @($epic).Count, @($gog).Count, @($start).Count, @($desk).Count, @($specs).Count)

    $byKey = [ordered]@{}
    foreach ($s in $specs) {
        if (-not $s.Key) { continue }
        $k = [string]$s.Key
        if (-not $byKey.Contains($k)) { $byKey[$k] = $s }
    }

    $items = @()
    foreach ($k in $byKey.Keys) {
        $s = $byKey[$k]
        $p = $script:Pins[$k]
        if ($p -and [bool]$p.hidden) { continue }
        $name = if ($p -and $p.name) { [string]$p.name } else { $s.Name }
        $items += [pscustomobject]@{
            Key = $k; Name = $name; Origin = $s.Origin; Kind = $s.Kind
            Link = $s.Link; Target = $s.Target; Arguments = $s.Arguments; Work = $s.Work
            Icon = $s.Icon; IconHint = $s.IconHint; AppId = $s.AppId; Fav = [bool]($p -and $p.fav); Pin = $p
        }
    }

    $orden = 0
    foreach ($p in $script:Pins.Values) {
        if ($byKey.Contains([string]$p.key)) { continue }
        $orden++
        if ([bool]$p.hidden) { continue }
        $hasFile = ($p.link -and (Test-Path -LiteralPath $p.link)) -or ($p.target -and (Test-Path -LiteralPath $p.target))
        if (-not $hasFile) { continue }
        $hint = if ($p.icon) { $p.icon } elseif ($p.target) { $p.target } else { $p.link }
        $kind = if ($p.kind) { [string]$p.kind } else { 'game' }
        $items += [pscustomobject]@{
            Key = [string]$p.key; Name = [string]$p.name; Origin = 'custom'; Kind = $kind
            Link = $p.link; Target = $p.target; Arguments = $p.args; Work = $p.work
            Icon = $p.icon; IconHint = $hint; AppId = $null; Fav = [bool]$p.fav; Pin = $p; Orden = $orden
        }
    }

    $script:Items = $items
    $sw.Stop()
    $script:TrimArmed = $true
    Write-Log ("Indice construido: {0} elementos en {1} ms" -f $items.Count, $sw.ElapsedMilliseconds)
    if ($SoloDeteccion) {
        $items | Sort-Object Kind, Name | ForEach-Object {
            Write-Host ("[{0}/{1}] {2}  <-  {3}" -f $_.Kind, $_.Origin, $_.Name, $(if ($_.Target) { $_.Target } else { $_.Link }))
        }
    }
}

function Get-VisibleItems {
    $pool = switch ($script:Cfg['section']) {
        'fav'   { @($script:Items | Where-Object { $_.Fav }) }
        'games' { @($script:Items | Where-Object { $_.Kind -eq 'game' }) }
        'apps'  { @($script:Items | Where-Object { $_.Kind -eq 'app' }) }
        default { @($script:Items) }
    }
    $q = $script:SearchBox.Text
    if ($q -and $q -ne 'buscar...') {
        $pool = @($pool | Where-Object { $_.Name.IndexOf($q, [System.StringComparison]::OrdinalIgnoreCase) -ge 0 })
    }
    $pool = @($pool | Where-Object { $_ })
    # lo sacado del dock se quita aqui, no en el indice: rehacer el indice cuesta un escaneo de disco
    # entero, y al soltar fuera hace falta que el tile desaparezca al instante
    $pool = @($pool | Where-Object {
        $p = $script:Pins[[string]$_.Key]
        -not ($p -and [bool]$p.hidden)
    })

    # el orden que eligio el usuario arrastrando manda sobre el resto; lo que no esta en la lista
    # (recien detectados) se coloca detras, por numero de usos y nombre
    $script:OrderRank = @{}
    $oi = 0
    foreach ($k in @($script:Cfg['order'])) {
        $ks = [string]$k
        if ($ks -and -not $script:OrderRank.ContainsKey($ks)) { $script:OrderRank[$ks] = $oi; $oi++ }
    }
    $sortExp = {
        param($x)
        if ($x.Key -eq 'NO_MATCH') { return 'C|zzzz' }
        $ks = [string]$x.Key
        if ($script:OrderRank.ContainsKey($ks)) { return ('A|{0:d6}' -f $script:OrderRank[$ks]) }
        $u = $script:Usage[$ks]
        $n = if ($u) { $u.n } else { 0 }
        return ('B|{0:d6}|{1}' -f [int][Math]::Max(0, 999999 - $n), ([string]$x.Name).ToLowerInvariant())
    }
    return @($pool | Sort-Object -Property $sortExp)
}

# ------------------------------------------------------------------ temas
# Cada tema solo pinta brochas: nada de efectos de mapa de bits, la barra sigue costando lo mismo.
$script:Themes = [ordered]@{
    'noche' = @{
        label     = 'Noche'
        glass     = '#660A1018'; glassBot = '#7A060A10'
        edge      = '#42FFFFFF'
        sheenTop  = '#3AFFFFFF'; sheenMid = '#0AFFFFFF'; sheenBot = '#14FFFFFF'
        hover     = '#8CFFFFFF'; press = '#A6FFFFFF'
        badge     = '#2EFFFFFF'; letter = '#FFE6E9F2'
        tipBg     = '#F0131822'; tipFg = '#FFE6E9F2'; tipEdge = '#4DFFFFFF'
        fieldBg   = '#F0131822'; fieldFg = '#FF9AA3B4'
        accent    = '#FF5B8DEF'
    }
    'niebla' = @{
        label     = 'Niebla'
        glass     = '#8CF2F5FA'; glassBot = '#8CC6D0DE'
        edge      = '#B3FFFFFF'
        sheenTop  = '#CCFFFFFF'; sheenMid = '#14FFFFFF'; sheenBot = '#3399A6BC'
        hover     = '#881B2A3D'; press = '#A31B2A3D'
        badge     = '#331B2A3D'; letter = '#FF1E2430'
        tipBg     = '#F2F5F9FA'; tipFg = '#FF1E2430'; tipEdge = '#80FFFFFF'
        fieldBg   = '#F2F5F9FA'; fieldFg = '#FF5A6474'
        accent    = '#FF2F6FB8'
    }
    'onix' = @{
        label     = 'Onix'
        glass     = '#8C0C0C0F'; glassBot = '#99000000'
        edge      = '#33FFFFFF'
        sheenTop  = '#24FFFFFF'; sheenMid = '#05FFFFFF'; sheenBot = '#0FFFFFFF'
        hover     = '#80FFFFFF'; press = '#99FFFFFF'
        badge     = '#24FFFFFF'; letter = '#FFF2F2F5'
        tipBg     = '#F20D0D10'; tipFg = '#FFF2F2F5'; tipEdge = '#2EFFFFFF'
        fieldBg   = '#F20D0D10'; fieldFg = '#FF9DA2AE'
        accent    = '#FF8B7BF0'
    }
    'neon' = @{
        label     = 'Neon'
        glass     = '#73160A26'; glassBot = '#801B0630'
        edge      = '#4DFF59C7'
        sheenTop  = '#40FF7AE0'; sheenMid = '#0AFFFFFF'; sheenBot = '#3359FFF6'
        hover     = '#992FA8E8'; press = '#B338E0FF'
        badge     = '#3359FFF6'; letter = '#FFE8FBFF'
        tipBg     = '#F2160A28'; tipFg = '#FFD8F6FF'; tipEdge = '#6659FFF6'
        fieldBg   = '#F2160A28'; fieldFg = '#FF8FD8F0'
        accent    = '#FFFF4FC3'
    }
    'ambar' = @{
        label     = 'Ambar'
        glass     = '#731D1406'; glassBot = '#80120B02'
        edge      = '#4DE8A33A'
        sheenTop  = '#40FFD08A'; sheenMid = '#0AFFFFFF'; sheenBot = '#26E8A33A'
        hover     = '#8CE8A33A'; press = '#AAFFB347'
        badge     = '#33FFC46B'; letter = '#FFF6E8D0'
        tipBg     = '#F218120A'; tipFg = '#FFF3E4C8'; tipEdge = '#66E8A33A'
        fieldBg   = '#F218120A'; fieldFg = '#FFB9A184'
        accent    = '#FFF0A83C'
    }
    'bosque' = @{
        label     = 'Bosque'
        glass     = '#73081A11'; glassBot = '#8004120A'
        edge      = '#4D7CE8B4'
        sheenTop  = '#3AA8F0D0'; sheenMid = '#0AFFFFFF'; sheenBot = '#267CE8B4'
        hover     = '#8C2FAE7E'; press = '#A634C98F'
        badge     = '#337CE8B4'; letter = '#FFDCF5E8'
        tipBg     = '#F207170F'; tipFg = '#FFDCF5E8'; tipEdge = '#667CE8B4'
        fieldBg   = '#F207170F'; fieldFg = '#FF8FBFAC'
        accent    = '#FF3FC98F'
    }
}
$script:Theme = $script:Themes['noche']
$script:ThemeIds = @($script:Themes.Keys)

# ------------------------------------------------------------------ estilos globales
# Van en Application.Resources, no en los de la ventana: los menús y los ajustes viven en otras
# ventanas visuales (Popup / Window nuevos) y solo llegan a recursos de alcance de aplicación.
# Todo lo que pinta se resuelve con DynamicResource sobre las claves que empuja Apply-Theme, así que
# cambiar de tema repinta menús y ajustes sin reconstruir nada.
$GlobalXaml = @'
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

  <!-- .......................................... menu contextual: pastilla de cristal -->
  <Style TargetType="ContextMenu">
    <Setter Property="OverridesDefaultStyle" Value="True"/>
    <Setter Property="SnapsToDevicePixels" Value="True"/>
    <!-- ojo: PopupAnimation NO va aqui. Popup lo tiene, pero ContextMenu lo redeclara privado y el
         Setter no lo resuelve -> "El valor no puede ser nulo. Nombre del parámetro: property" -->
    <Setter Property="TextOptions.TextFormattingMode" Value="Display"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="ContextMenu">
          <!-- el popup recorta lo que sobresalga de su ventana: sin este margen la sombra salen
               cortada por los cuatro lados -->
          <Grid Margin="9">
            <Border x:Name="Cuerpo" CornerRadius="14" Padding="5,6"
                    Background="{DynamicResource MenuBg}"
                    BorderBrush="{DynamicResource MenuEdge}" BorderThickness="1"
                    RenderTransformOrigin="0,0" SnapsToDevicePixels="True">
              <Border.RenderTransform><ScaleTransform ScaleX="1" ScaleY="1"/></Border.RenderTransform>
              <Border.Effect>
                <DropShadowEffect Color="#FF04060B" BlurRadius="26" ShadowDepth="7" Opacity="0.5" Direction="270"/>
              </Border.Effect>
              <ItemsPresenter SnapsToDevicePixels="True"/>
            </Border>
          </Grid>
          <ControlTemplate.Triggers>
            <!-- entra cada vez que se abre: los Loaded de un popup reutilizado no son fiables -->
            <Trigger Property="IsOpen" Value="True">
              <Trigger.EnterActions>
                <BeginStoryboard Name="MenuPop">
                  <Storyboard>
                    <DoubleAnimation Storyboard.TargetName="Cuerpo" Storyboard.TargetProperty="Opacity"
                                     From="0" To="1" Duration="0:0:0.12"/>
                    <DoubleAnimation Storyboard.TargetName="Cuerpo"
                                     Storyboard.TargetProperty="(UIElement.RenderTransform).(ScaleTransform.ScaleX)"
                                     From="0.92" To="1" Duration="0:0:0.22">
                      <DoubleAnimation.EasingFunction><CubicEase EasingMode="EaseOut"/></DoubleAnimation.EasingFunction>
                    </DoubleAnimation>
                    <DoubleAnimation Storyboard.TargetName="Cuerpo"
                                     Storyboard.TargetProperty="(UIElement.RenderTransform).(ScaleTransform.ScaleY)"
                                     From="0.86" To="1" Duration="0:0:0.26">
                      <DoubleAnimation.EasingFunction><BackEase EasingMode="EaseOut" Amplitude="0.30"/></DoubleAnimation.EasingFunction>
                    </DoubleAnimation>
                  </Storyboard>
                </BeginStoryboard>
              </Trigger.EnterActions>
              <Trigger.ExitActions>
                <RemoveStoryboard BeginStoryboardName="MenuPop"/>
              </Trigger.ExitActions>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType="MenuItem">
    <Setter Property="OverridesDefaultStyle" Value="True"/>
    <Setter Property="Foreground" Value="{DynamicResource MenuFg}"/>
    <Setter Property="FontSize" Value="12.5"/>
    <Setter Property="SnapsToDevicePixels" Value="True"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="MenuItem">
          <Border x:Name="Bd" CornerRadius="9" Margin="0,1" Background="Transparent" SnapsToDevicePixels="True">
            <Grid>
              <Grid.ColumnDefinitions>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="Auto"/>
              </Grid.ColumnDefinitions>
              <Border x:Name="Chk" Grid.Column="0" Width="20" Visibility="Collapsed">
                <Path x:Name="Tick" Visibility="Hidden" Data="M 0,3.6 L 3,6.6 L 8.2,1"
                      Stroke="{DynamicResource MenuAccent}" StrokeThickness="1.8"
                      StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"
                      HorizontalAlignment="Center" VerticalAlignment="Center"/>
              </Border>
              <ContentPresenter x:Name="Hdr" Grid.Column="1" ContentSource="Header" RecognizesAccessKey="True"
                                VerticalAlignment="Center" Margin="0,6"
                                RenderTransformOrigin="0,0.5">
                <ContentPresenter.RenderTransform><TranslateTransform X="0"/></ContentPresenter.RenderTransform>
              </ContentPresenter>
              <Path x:Name="Arr" Grid.Column="2" Visibility="Collapsed" Data="M 0,0 L 4.5,4.5 L 0,9"
                    Stroke="{DynamicResource MenuFg}" StrokeThickness="1.6" Opacity="0.75"
                    StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"
                    Margin="8,0,3,0" VerticalAlignment="Center"/>
              <Popup x:Name="PART_Popup" Grid.Column="0" Grid.ColumnSpan="3" AllowsTransparency="True"
                     Placement="Right" HorizontalOffset="4" VerticalOffset="-12" PopupAnimation="Fade"
                     IsOpen="{TemplateBinding IsSubmenuOpen}" Focusable="False">
                <Grid Margin="9">
                  <Border CornerRadius="14" Padding="5,6" Background="{DynamicResource MenuBg}"
                          BorderBrush="{DynamicResource MenuEdge}" BorderThickness="1">
                    <Border.Effect>
                      <DropShadowEffect Color="#FF04060B" BlurRadius="26" ShadowDepth="7" Opacity="0.5" Direction="270"/>
                    </Border.Effect>
                    <ItemsPresenter/>
                  </Border>
                </Grid>
              </Popup>
            </Grid>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsCheckable" Value="True">
              <Setter TargetName="Chk" Property="Visibility" Value="Visible"/>
            </Trigger>
            <Trigger Property="IsChecked" Value="True">
              <Setter TargetName="Tick" Property="Visibility" Value="Visible"/>
            </Trigger>
            <Trigger Property="Role" Value="SubmenuHeader">
              <Setter TargetName="Arr" Property="Visibility" Value="Visible"/>
            </Trigger>
            <Trigger Property="IsHighlighted" Value="True">
              <Setter TargetName="Bd" Property="Background" Value="{DynamicResource MenuHi}"/>
              <Setter Property="Foreground" Value="{DynamicResource MenuHiFg}"/>
              <Trigger.EnterActions>
                <BeginStoryboard Name="Nudge">
                  <Storyboard>
                    <DoubleAnimation Storyboard.TargetName="Hdr"
                                     Storyboard.TargetProperty="(UIElement.RenderTransform).(TranslateTransform.X)"
                                     To="3" Duration="0:0:0.14">
                      <DoubleAnimation.EasingFunction><CubicEase EasingMode="EaseOut"/></DoubleAnimation.EasingFunction>
                    </DoubleAnimation>
                  </Storyboard>
                </BeginStoryboard>
              </Trigger.EnterActions>
              <Trigger.ExitActions>
                <BeginStoryboard>
                  <Storyboard>
                    <DoubleAnimation Storyboard.TargetName="Hdr" FillBehavior="Stop"
                                     Storyboard.TargetProperty="(UIElement.RenderTransform).(TranslateTransform.X)"
                                     To="0" Duration="0:0:0.20">
                      <DoubleAnimation.EasingFunction><CubicEase EasingMode="EaseOut"/></DoubleAnimation.EasingFunction>
                    </DoubleAnimation>
                  </Storyboard>
                </BeginStoryboard>
              </Trigger.ExitActions>
            </Trigger>
            <Trigger Property="IsEnabled" Value="False">
              <Setter Property="Opacity" Value="0.36"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType="Separator">
    <Setter Property="OverridesDefaultStyle" Value="True"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate>
          <Border Height="1" Margin="11,5" CornerRadius="1" Background="{DynamicResource MenuSep}"/>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- Separator no: MenuBase lo mete dentro de un MenuItem y la linea sale desplazada segun el
       hueco de la marca de check. Un MenuItem con plantilla de raya manda el ancho entero. -->
  <Style x:Key="SepItem" TargetType="MenuItem">
    <Setter Property="OverridesDefaultStyle" Value="True"/>
    <Setter Property="IsHitTestVisible" Value="False"/>
    <Setter Property="Focusable" Value="False"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="MenuItem">
          <Border Height="1" Margin="12,7" CornerRadius="1" Background="{DynamicResource MenuSep}"/>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- .......................................... controles de los ajustes -->
  <Style x:Key="PillBtn" TargetType="Button">
    <Setter Property="Foreground" Value="{DynamicResource MenuFg}"/>
    <Setter Property="Background" Value="{DynamicResource CtlBg}"/>
    <Setter Property="BorderBrush" Value="{DynamicResource CtlEdge}"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="FontSize" Value="12"/>
    <Setter Property="Padding" Value="13,7"/>
    <Setter Property="Cursor" Value="Hand"/>
    <Setter Property="SnapsToDevicePixels" Value="True"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="Button">
          <Border x:Name="Bd" CornerRadius="10" Padding="{TemplateBinding Padding}"
                  Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}"
                  BorderThickness="{TemplateBinding BorderThickness}" RenderTransformOrigin="0.5,0.5">
            <Border.RenderTransform><ScaleTransform ScaleX="1" ScaleY="1"/></Border.RenderTransform>
            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="Bd" Property="Background" Value="{DynamicResource CtlHi}"/>
              <Setter TargetName="Bd" Property="BorderBrush" Value="{DynamicResource MenuAccent}"/>
            </Trigger>
            <Trigger Property="IsPressed" Value="True">
              <Setter TargetName="Bd" Property="RenderTransform">
                <Setter.Value><ScaleTransform ScaleX="0.965" ScaleY="0.965"/></Setter.Value>
              </Setter>
            </Trigger>
            <Trigger Property="IsEnabled" Value="False">
              <Setter Property="Opacity" Value="0.4"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key="StepBtn" TargetType="Button">
    <Setter Property="Foreground" Value="{DynamicResource MenuFg}"/>
    <Setter Property="Background" Value="{DynamicResource CtlBg}"/>
    <Setter Property="BorderBrush" Value="{DynamicResource CtlEdge}"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="FontSize" Value="14"/>
    <Setter Property="FontWeight" Value="SemiBold"/>
    <Setter Property="Width" Value="28"/>
    <Setter Property="Height" Value="26"/>
    <Setter Property="Cursor" Value="Hand"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="Button">
          <Border x:Name="Bd" CornerRadius="8" Background="{TemplateBinding Background}"
                  BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}">
            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="Bd" Property="Background" Value="{DynamicResource CtlHi}"/>
              <Setter TargetName="Bd" Property="BorderBrush" Value="{DynamicResource MenuAccent}"/>
            </Trigger>
            <Trigger Property="IsPressed" Value="True">
              <Setter TargetName="Bd" Property="RenderTransform">
                <Setter.Value><ScaleTransform ScaleX="0.94" ScaleY="0.94"/></Setter.Value>
              </Setter>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key="GhostBtn" TargetType="Button">
    <Setter Property="Foreground" Value="{DynamicResource PanelSub}"/>
    <Setter Property="FontSize" Value="15"/>
    <Setter Property="Width" Value="30"/>
    <Setter Property="Height" Value="30"/>
    <Setter Property="Cursor" Value="Hand"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="Button">
          <Grid>
            <Ellipse x:Name="Hi" Fill="{DynamicResource MenuHi}" Opacity="0"/>
            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="Hi" Property="Opacity" Value="1"/>
              <Setter Property="Foreground" Value="{DynamicResource MenuHiFg}"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key="ChipRadio" TargetType="RadioButton">
    <Setter Property="Foreground" Value="{DynamicResource MenuFg}"/>
    <Setter Property="FontSize" Value="12"/>
    <Setter Property="Cursor" Value="Hand"/>
    <Setter Property="SnapsToDevicePixels" Value="True"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="RadioButton">
          <Grid Background="Transparent">
            <Border x:Name="Base" CornerRadius="9" Background="{DynamicResource CtlBg}"
                    BorderBrush="{DynamicResource CtlEdge}" BorderThickness="1"/>
            <Border x:Name="Fill" CornerRadius="9" Opacity="0" RenderTransformOrigin="0.5,0.5"
                    Background="{DynamicResource ChipOn}" BorderBrush="{DynamicResource MenuAccent}"
                    BorderThickness="1">
              <Border.RenderTransform><ScaleTransform ScaleX="1" ScaleY="1"/></Border.RenderTransform>
            </Border>
            <ContentPresenter x:Name="Txt" Margin="12,6" HorizontalAlignment="Center" VerticalAlignment="Center"/>
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="Base" Property="BorderBrush" Value="{DynamicResource MenuAccent}"/>
            </Trigger>
            <Trigger Property="IsChecked" Value="True">
              <Setter TargetName="Txt" Property="TextElement.Foreground" Value="{DynamicResource MenuHiFg}"/>
              <Trigger.EnterActions>
                <BeginStoryboard>
                  <Storyboard>
                    <DoubleAnimation Storyboard.TargetName="Fill" Storyboard.TargetProperty="Opacity"
                                     To="1" Duration="0:0:0.14"/>
                    <DoubleAnimation Storyboard.TargetName="Fill"
                                     Storyboard.TargetProperty="(UIElement.RenderTransform).(ScaleTransform.ScaleX)"
                                     From="0.90" To="1" Duration="0:0:0.24">
                      <DoubleAnimation.EasingFunction><BackEase EasingMode="EaseOut" Amplitude="0.35"/></DoubleAnimation.EasingFunction>
                    </DoubleAnimation>
                    <DoubleAnimation Storyboard.TargetName="Fill"
                                     Storyboard.TargetProperty="(UIElement.RenderTransform).(ScaleTransform.ScaleY)"
                                     From="0.90" To="1" Duration="0:0:0.24">
                      <DoubleAnimation.EasingFunction><BackEase EasingMode="EaseOut" Amplitude="0.35"/></DoubleAnimation.EasingFunction>
                    </DoubleAnimation>
                  </Storyboard>
                </BeginStoryboard>
              </Trigger.EnterActions>
              <Trigger.ExitActions>
                <BeginStoryboard>
                  <Storyboard>
                    <DoubleAnimation Storyboard.TargetName="Fill" Storyboard.TargetProperty="Opacity"
                                     To="0" Duration="0:0:0.14"/>
                  </Storyboard>
                </BeginStoryboard>
              </Trigger.ExitActions>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key="ThemeChip" TargetType="RadioButton">
    <Setter Property="Cursor" Value="Hand"/>
    <Setter Property="SnapsToDevicePixels" Value="True"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="RadioButton">
          <Grid Background="Transparent">
            <ContentPresenter/>
            <Border x:Name="Ring" CornerRadius="11" BorderBrush="{DynamicResource MenuAccent}"
                    BorderThickness="2" Opacity="0" Margin="-3" IsHitTestVisible="False"/>
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="Ring" Property="Opacity" Value="0.4"/>
            </Trigger>
            <Trigger Property="IsChecked" Value="True">
              <Setter TargetName="Ring" Property="Opacity" Value="1"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key="SwitchBox" TargetType="CheckBox">
    <Setter Property="Foreground" Value="{DynamicResource MenuFg}"/>
    <Setter Property="FontSize" Value="12"/>
    <Setter Property="Cursor" Value="Hand"/>
    <Setter Property="SnapsToDevicePixels" Value="True"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="CheckBox">
          <Grid Background="Transparent">
            <Grid.ColumnDefinitions>
              <ColumnDefinition Width="Auto"/>
              <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>
            <Grid Width="42" Height="24" VerticalAlignment="Center">
              <Border x:Name="Off" CornerRadius="12" Background="{DynamicResource SwitchOff}"/>
              <Border x:Name="On" CornerRadius="12" Background="{DynamicResource MenuAccent}" Opacity="0"/>
              <Border x:Name="Ring" CornerRadius="14" Margin="-2" Opacity="0" IsHitTestVisible="False"
                      BorderBrush="{DynamicResource MenuAccent}" BorderThickness="1.5"/>
              <Ellipse x:Name="Thumb" Width="18" Height="18" Fill="#FFFFFFFF"
                       HorizontalAlignment="Left" VerticalAlignment="Center" Margin="3,0,0,0">
                <Ellipse.RenderTransform><TranslateTransform x:Name="Tx" X="0"/></Ellipse.RenderTransform>
                <Ellipse.Effect>
                  <DropShadowEffect Color="#FF000000" BlurRadius="6" ShadowDepth="1.5" Opacity="0.45" Direction="270"/>
                </Ellipse.Effect>
              </Ellipse>
            </Grid>
            <ContentPresenter Grid.Column="1" VerticalAlignment="Center" Margin="10,0,0,0">
              <ContentPresenter.Resources>
                <Style TargetType="TextBlock">
                  <Setter Property="TextWrapping" Value="Wrap"/>
                </Style>
              </ContentPresenter.Resources>
            </ContentPresenter>
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property="IsChecked" Value="True">
              <Trigger.EnterActions>
                <BeginStoryboard>
                  <Storyboard>
                    <DoubleAnimation Storyboard.TargetName="On" Storyboard.TargetProperty="Opacity" To="1" Duration="0:0:0.16"/>
                    <DoubleAnimation Storyboard.TargetName="Off" Storyboard.TargetProperty="Opacity" To="0" Duration="0:0:0.16"/>
                    <DoubleAnimation Storyboard.TargetName="Tx" Storyboard.TargetProperty="X" To="18" Duration="0:0:0.26">
                      <DoubleAnimation.EasingFunction><BackEase EasingMode="EaseOut" Amplitude="0.40"/></DoubleAnimation.EasingFunction>
                    </DoubleAnimation>
                  </Storyboard>
                </BeginStoryboard>
              </Trigger.EnterActions>
              <Trigger.ExitActions>
                <BeginStoryboard>
                  <Storyboard>
                    <DoubleAnimation Storyboard.TargetName="On" Storyboard.TargetProperty="Opacity" To="0" Duration="0:0:0.16"/>
                    <DoubleAnimation Storyboard.TargetName="Off" Storyboard.TargetProperty="Opacity" To="1" Duration="0:0:0.16"/>
                    <DoubleAnimation Storyboard.TargetName="Tx" Storyboard.TargetProperty="X" To="0" Duration="0:0:0.22">
                      <DoubleAnimation.EasingFunction><CubicEase EasingMode="EaseOut"/></DoubleAnimation.EasingFunction>
                    </DoubleAnimation>
                  </Storyboard>
                </BeginStoryboard>
              </Trigger.ExitActions>
            </Trigger>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="Ring" Property="Opacity" Value="0.5"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key="GlassText" TargetType="TextBox">
    <Setter Property="Foreground" Value="{DynamicResource MenuFg}"/>
    <Setter Property="CaretBrush" Value="{DynamicResource MenuHiFg}"/>
    <Setter Property="SelectionBrush" Value="{DynamicResource CtlSel}"/>
    <Setter Property="Background" Value="{DynamicResource CtlBg}"/>
    <Setter Property="BorderBrush" Value="{DynamicResource CtlEdge}"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="FontSize" Value="13"/>
    <Setter Property="Padding" Value="9,6"/>
    <Setter Property="VerticalContentAlignment" Value="Center"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="TextBox">
          <Border CornerRadius="9" Background="{TemplateBinding Background}"
                  BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}">
            <ScrollViewer x:Name="PART_ContentHost" Margin="{TemplateBinding Padding}" VerticalAlignment="Center"/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsKeyboardFocused" Value="True">
              <Setter Property="BorderBrush" Value="{DynamicResource MenuAccent}"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

</ResourceDictionary>
'@


# ------------------------------------------------------------------ interfaz
$WindowXaml = @'
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="GamesDock" WindowStyle="None" ResizeMode="NoResize" ShowInTaskbar="False"
        AllowsTransparency="True" Background="Transparent" Topmost="False"
        UseLayoutRounding="True" SnapsToDevicePixels="True" Width="1000" Height="70">
  <Window.Resources>
    <Style x:Key="Tile" TargetType="Button">
      <Setter Property="Background" Value="Transparent"/>
      <Setter Property="Foreground" Value="#FFF0F3FA"/>
      <Setter Property="FontWeight" Value="SemiBold"/>
      <Setter Property="BorderThickness" Value="0"/>
      <Setter Property="Padding" Value="0"/>
      <Setter Property="Cursor" Value="Hand"/>
      <Setter Property="SnapsToDevicePixels" Value="True"/>
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="Button">
            <Grid>
              <!-- halo circular al pasar el raton: en reposo es invisible, solo se ve el icono.
                   tiene que caber dentro del propio boton: el ScrollViewer recorta todo lo que sobresalga -->
              <Border x:Name="Glow" Background="Transparent" CornerRadius="999"/>
              <ContentPresenter x:Name="Art" Margin="{TemplateBinding Padding}"
                                HorizontalAlignment="Center" VerticalAlignment="Center"
                                RenderTransformOrigin="0.5,0.5"/>
            </Grid>
            <ControlTemplate.Triggers>
              <!-- el agrandado lo lleva el magnes de Update-Proximity sobre todo el boton: aqui no se
                   toca RenderTransform o los dos efectos se multiplicarian -->
              <Trigger Property="IsMouseOver" Value="True">
                <Setter TargetName="Glow" Property="Background" Value="{DynamicResource TileHover}"/>
              </Trigger>
              <Trigger Property="IsPressed" Value="True">
                <Setter TargetName="Glow" Property="Background" Value="{DynamicResource TilePress}"/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
    <Style x:Key="Tip" TargetType="ToolTip">
      <Setter Property="HasDropShadow" Value="False"/>
      <Setter Property="BorderThickness" Value="1"/>
      <Setter Property="Padding" Value="9,6"/>
      <Setter Property="FontSize" Value="12"/>
      <Setter Property="MaxWidth" Value="380"/>
    </Style>
  </Window.Resources>
  <!-- La pildora la dibuja una Geometry calculada (dos Path con el mismo contorno): un Border
       redondeado no puede hincharse solo en la zona donde el raton agranda los iconos. La fila va
       encima, fuera del relleno, para que el icono del magnes se desborde sin que recorte nadie.
       Los Path van dentro de un Canvas A PROPOSITO: WPF recorta un elemento al hueco que le da el
       layout cuando su DesiredSize lo desborda, y aqui ese hueco es el grosor de reposo de la pildora
       (Root lleva el margen de la banda transparente), asi que dentro de una Grid el cristal jamas
       llegaria a hincharse por fuera de ese grosor mientras los iconos si lo hacen. El Canvas coloca a
       (0,0) sin recortar: las coordenadas de la geometria son las mismas de siempre. -->
  <Grid x:Name="Root">
    <Canvas x:Name="PillLayer" Panel.ZIndex="0">
      <Path x:Name="Glass" Fill="#660A1018" Stretch="None" Canvas.Left="0" Canvas.Top="0"/>
      <Path x:Name="Edge" Stroke="#42FFFFFF" StrokeThickness="1" Stretch="None"
            Canvas.Left="0" Canvas.Top="0">
        <Path.Fill>
          <LinearGradientBrush StartPoint="0,0" EndPoint="0,1">
            <GradientStop Color="#3AFFFFFF" Offset="0"/>
            <GradientStop Color="#0AFFFFFF" Offset="0.5"/>
            <GradientStop Color="#14FFFFFF" Offset="1"/>
          </LinearGradientBrush>
        </Path.Fill>
      </Path>
    </Canvas>
    <StackPanel x:Name="Bar" Orientation="Horizontal" Panel.ZIndex="2">
      <ScrollViewer x:Name="StripScroll" HorizontalScrollBarVisibility="Hidden"
                    VerticalScrollBarVisibility="Disabled" Focusable="False">
        <StackPanel x:Name="Strip" Orientation="Horizontal"/>
      </ScrollViewer>
    </StackPanel>
    <Border x:Name="SearchPanel" Visibility="Collapsed" CornerRadius="15" Padding="8,4"
            Background="#F0131822" BorderBrush="#4DFFFFFF" BorderThickness="1"
            HorizontalAlignment="Center" VerticalAlignment="Center">
      <TextBox x:Name="SearchBox" Text="buscar..." Foreground="#FF9AA3B4" Background="Transparent"
               BorderThickness="0" CaretBrush="#FFE6E9F2" FontSize="13" Width="190"
               Padding="2,3" VerticalContentAlignment="Center"/>
    </Border>
  </Grid>
</Window>
'@

$script:App    = New-Object System.Windows.Application
$script:App.ShutdownMode = [System.Windows.ShutdownMode]::OnExplicitShutdown
$script:App.add_DispatcherUnhandledException({
    param($s, $e)
    Write-Log ("EXCEPCION no manejada: " + $e.Exception)
    $e.Handled = $true
})
try {
    $null = $script:App.Resources.MergedDictionaries.Add([System.Windows.Markup.XamlReader]::Parse($GlobalXaml))
    Write-Log 'estilos globales cargados'
} catch {
    Write-Log ("FALLO al cargar estilos globales: " + $_.Exception.Message)
    if ($_.Exception.InnerException) { Write-Log ("  interior: " + $_.Exception.InnerException.Message) }
}
$script:Window = [System.Windows.Markup.XamlReader]::Parse($WindowXaml)

$script:RootGrid   = $script:Window.FindName('Root')
$script:Glass      = $script:Window.FindName('Glass')
$script:Edge       = $script:Window.FindName('Edge')
$script:Bar        = $script:Window.FindName('Bar')
$script:Strip      = $script:Window.FindName('Strip')
$script:StripScroll= $script:Window.FindName('StripScroll')
$script:SearchPanel= $script:Window.FindName('SearchPanel')
$script:SearchBox  = $script:Window.FindName('SearchBox')

$script:Tiles   = @()
$script:MagTop  = -1
$script:Batch     = $false   # true mientras se fijan varios de golpe: evita rehacer todo cada vez
$script:BatchFail = @()
$script:OrderRank = @{}
$script:Brushes = @{}
# banda transparente que rodea la pildora: aqui cabe el icono agrandado del magnes desbordando el
# cristal. Si se queda corta, el ScrollViewer recorta la punta de arriba y el icono parece cortado.
$script:Margin  = 34
$script:MagPeak = 1.55   # cuanto estira el magnes al icono del centro de la fila
$script:HoverT  = 0.0    # 0 = pildora en reposo, 1 = pildora abierta del todo
$script:PtrU    = -1e9   # coordenada del raton a lo largo de la fila, en medida de la pildora
$script:PtrLive = 0.0    # la misma coordenada, pero seguida con muelle: el bulto no teletransporta
$script:MagLive = 0.0    # cuanto esta agrandando de verdad (0..1), para que la pildora y los
                         # iconos se hinchen y se deshinchen a la vez
$script:RowShift = 0.0   # cuanto se mueve la fila hacia dentro para que el borde exterior no baje
$script:PtrInside = $false  # el raton esta dentro de la ventana: fuera, el magnes no tiene pinta
$script:DragBtn = $null  # tile arrastrando para reordenar (arrastre propio, no de OLE)
$script:DragStart = $null
$script:DragMoved = $false
$script:DesktopLevel = $false   # Attach-ToDesktop la confirma contra la config
$script:TrimArmed = $false
$script:IconQueue = New-Object System.Collections.Generic.Queue[object]
$script:IconBusy  = $false

function New-Grad([string[]]$HexStops) {
    $g = New-Object System.Windows.Media.LinearGradientBrush
    $g.StartPoint = New-Object System.Windows.Point(0, 0)
    $g.EndPoint   = New-Object System.Windows.Point(0, 1)
    $last = $HexStops.Count - 1
    for ($i = 0; $i -le $last; $i++) {
        $stop = New-Object System.Windows.Media.GradientStop
        $stop.Color = [System.Windows.Media.Color][System.Windows.Media.ColorConverter]::ConvertFromString($HexStops[$i])
        $stop.Offset = if ($last -gt 0) { $i / $last } else { 0 }
        $null = $g.GradientStops.Add($stop)
    }
    $g.Freeze(); return $g
}

# halo de raton: degradado radial (vector, sin efectos de mapa de bits) que se apaga hacia el borde
function New-Glow([string]$Hex) {
    $col = [System.Windows.Media.Color][System.Windows.Media.ColorConverter]::ConvertFromString($Hex)
    $mid = [System.Windows.Media.Color]::FromArgb([int]($col.A * 0.55), $col.R, $col.G, $col.B)
    $out = [System.Windows.Media.Color]::FromArgb(0, $col.R, $col.G, $col.B)
    $g = New-Object System.Windows.Media.RadialGradientBrush
    $g.Center = New-Object System.Windows.Point(0.5, 0.5)
    $g.GradientOrigin = New-Object System.Windows.Point(0.5, 0.5)
    $g.RadiusX = 0.5; $g.RadiusY = 0.5
    $null = $g.GradientStops.Add((New-Object System.Windows.Media.GradientStop($col, 0.0)))
    $null = $g.GradientStops.Add((New-Object System.Windows.Media.GradientStop($mid, 0.60)))
    $null = $g.GradientStops.Add((New-Object System.Windows.Media.GradientStop($out, 1.0)))
    $g.Freeze(); return $g
}

function Apply-Theme([string]$Id) {
    $t = $script:Themes[$Id]
    if (-not $t) { $Id = 'noche'; $t = $script:Themes['noche'] }
    $script:Theme = $t
    $script:Cfg['theme'] = $Id

    $script:Glass.Fill = New-Grad @($t.glass, $t.glassBot)
    $script:Edge.Stroke = Br $t.edge
    $script:Edge.Fill  = New-Grad @($t.sheenTop, $t.sheenMid, $t.sheenBot)

    $script:SearchPanel.Background  = Br $t.fieldBg
    $script:SearchPanel.BorderBrush = Br $t.edge
    $script:SearchBox.Foreground = Br $t.fieldFg
    $script:SearchBox.CaretBrush = Br $t.letter

    # las plantillas leen estas claves con DynamicResource: cambian el aspecto sin reconstruir nada.
    # el cast es obligatorio: sin el, PowerShell deja dentro del diccionario el envoltorio PSObject
    # y al resolver el recurso la pila nativa lo rechaza ("no es un valor valido para Background")
    $script:Window.Resources['TileHover'] = [System.Windows.Media.Brush] (New-Glow $t.hover)
    $script:Window.Resources['TilePress'] = [System.Windows.Media.Brush] (New-Glow $t.press)

    # menús y ajustes viven en sus propias ventanas visuales: sus brochas tienen que estar en el
    # alcance de la aplicación para que DynamicResource las encuentre desde cualquier parte
    $ar = $script:App.Resources
    $ar['MenuBg']    = [System.Windows.Media.Brush] (Br $t.tipBg)
    $ar['MenuEdge']  = [System.Windows.Media.Brush] (Br $t.tipEdge)
    $ar['MenuFg']    = [System.Windows.Media.Brush] (Br $t.tipFg)
    $ar['MenuHiFg']  = [System.Windows.Media.Brush] (Br $t.letter)
    $ar['MenuHi']    = [System.Windows.Media.Brush] (Br $t.hover)
    $ar['MenuSep']   = [System.Windows.Media.Brush] (BrA $t.edge 0.55)
    $ar['MenuAccent']= [System.Windows.Media.Brush] (Br $t.accent)
    $ar['CtlBg']     = [System.Windows.Media.Brush] (BrA $t.fieldBg 0.55)
    $ar['CtlHi']     = [System.Windows.Media.Brush] (Br $t.hover)
    $ar['CtlEdge']   = [System.Windows.Media.Brush] (BrA $t.edge 0.85)
    $ar['CtlSel']    = [System.Windows.Media.Brush] (BrA $t.accent 0.45)
    $ar['ChipOn']    = [System.Windows.Media.Brush] (BrA $t.accent 0.30)
    $ar['SwitchOff'] = [System.Windows.Media.Brush] (BrA $t.fieldFg 0.40)
    $ar['PanelBg']   = [System.Windows.Media.Brush] (Br $t.tipBg)
    $ar['PanelEdge'] = [System.Windows.Media.Brush] (BrA $t.edge 0.90)
    $ar['PanelFg']   = [System.Windows.Media.Brush] (Br $t.letter)
    $ar['PanelSub']  = [System.Windows.Media.Brush] (Br $t.fieldFg)

    if ($script:Tiles.Count -gt 0) { Rebuild-Strip }   # Calcitos, letras y tooltips leen el tema al crearse
    Write-Log "tema aplicado: $Id"
}

function Set-Theme([string]$Id) {
    Apply-Theme $Id
    Write-Config
}

function Get-Brush([string]$Path) {
    if ($script:Brushes.ContainsKey($Path)) { return $script:Brushes[$Path] }
    $b = $null
    try {
        $bmp = New-Object System.Windows.Media.Imaging.BitmapImage
        $bmp.BeginInit()
        $bmp.CacheOption = [System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad
        if ((Get-Item -LiteralPath $Path).Length -gt 8000) { $bmp.DecodePixelWidth = 256 }
        $bmp.UriSource = New-Object System.Uri($Path, [System.UriKind]::Absolute)
        $bmp.EndInit()
        $b = New-Object System.Windows.Media.ImageBrush($bmp)
        $b.Stretch = [System.Windows.Media.Stretch]::UniformToFill
        # una capsula de Steam es 2:3 con el titulo arriba: el recorte cuadrado centrado le comia
        # la cabecera y dejaba media ilustracion. AlignmentY solo admite Top/Center/Bottom, asi que
        # la ventana se coloca a mano con un Viewbox absoluto
        if ($bmp.PixelHeight -gt $bmp.PixelWidth) {
            $w = [double]$bmp.PixelWidth; $h = [double]$bmp.PixelHeight
            $b.ViewboxUnits = [System.Windows.Media.BrushMappingMode]::Absolute
            $b.Viewbox = New-Object System.Windows.Rect(0.0, (($h - $w) * 0.34), $w, $w)
        }
        $b.Freeze()
    } catch { Write-Log "decode fallo ${Path}: $($_.Exception.Message)"; $b = $null }
    $script:Brushes[$Path] = $b
    return $b
}

function Set-IconNow($btn, [string]$Path) {
    $bd = $btn.Content
    if ($Path -and (Test-Path -LiteralPath $Path)) {
        $brush = Get-Brush $Path
        if ($brush) {
            # la imagen pisa el circulo de respaldo: sin recorte redondeado, solo el icono en si
            $bd.CornerRadius = New-Object System.Windows.CornerRadius(0)
            $bd.Background = $brush
            $bd.Child = $null
            # Fant: el filtro bueno que WPF puede hacer POR HARDWARE. HighQualityCubic esta documentado
            # como solo-software y manda el tile a la via lenta: con 11 iconos reescalando cada cuadro
            # eso es exactamente el corte que se nota al pasar el raton
            [System.Windows.Media.RenderOptions]::SetBitmapScalingMode($bd, [System.Windows.Media.BitmapScalingMode]::Fant)
            return $true
        }
    }
    return $false
}

function Pump-Icons {
    if ($script:IconBusy) { return }
    $script:IconBusy = $true
    $n = 0
    while ($script:IconQueue.Count -gt 0 -and $n -lt 4) {
        $job = $script:IconQueue.Dequeue()
        # un .exe/.lnk no se puede pintar tal cual: hay que extraer antes su png al cache
        if ($job.it -and ($job.path -notmatch '(?i)\.(png|jpg|jpeg|ico)$' -or -not (Test-Path -LiteralPath $job.path))) {
            $job.path = Get-IconFile $job.it
            if ($job.path) { $job.it.Icon = $job.path }
        }
        $null = Set-IconNow $job.btn $job.path
        $n++
    }
    $script:IconBusy = $false
    if ($script:IconQueue.Count -gt 0) {
        $script:Window.Dispatcher.BeginInvoke([System.Action] { Pump-Icons },
            [System.Windows.Threading.DispatcherPriority]::ContextIdle) | Out-Null
    } elseif ($script:TrimArmed) {
        $script:TrimArmed = $false
        Trim-Memory
    }
}

function Get-TooltipFor($it) {
    $u = $script:Usage[$it.Key]
    $origen = switch ($it.Origin) {
        'steam' { 'Steam' } 'epic' { 'Epic Games' } 'gog' { 'GOG' }
        'desktop' { 'Escritorio' } 'start' { 'Menu Inicio' } default { 'Anadido a mano' }
    }
    $uso = if ($u) { "usado $($u.n) veces" } else { 'sin usar todavia' }
    $ruta = if ($it.Target) { $it.Target } elseif ($it.Link) { $it.Link } else { '' }
    return ("{0}`n{1} · {2}`n{3}" -f $it.Name, $origen, $uso, $ruta)
}

function New-Tile($it) {
    $sz = [int]$script:Cfg['iconSize']
    $vert = $script:Cfg['position'] -in 'left', 'right'
    $btn = New-Object System.Windows.Controls.Button
    $btn.Style = $script:Window.FindResource('Tile')
    $btn.Tag = $it
    $rm = Get-RingMain; $rc = Get-RingCross
    if ($vert) {
        $btn.Padding = New-Object System.Windows.Thickness($rc, $rm, $rc, $rm)
    } else {
        $btn.Padding = New-Object System.Windows.Thickness($rm, $rc, $rm, $rc)
    }
    # el magnes estira el boton entero (halo + icono) alrededor de su centro: una transformacion
    # visual propia por tile, reutilizada en cada movimiento del raton
    $btn.RenderTransformOrigin = New-Object System.Windows.Point(0.5, 0.5)
    $btn.RenderTransform = New-Object System.Windows.Media.ScaleTransform
    $btn.VerticalAlignment = [System.Windows.VerticalAlignment]::Center
    $btn.HorizontalAlignment = [System.Windows.HorizontalAlignment]::Center

    $tt = New-Object System.Windows.Controls.ToolTip
    $tt.Style = $script:Window.FindResource('Tip')
    $tt.Content = Get-TooltipFor $it
    $tt.Background = Br $script:Theme.tipBg
    $tt.Foreground = Br $script:Theme.tipFg
    $tt.BorderBrush = Br $script:Theme.tipEdge
    $btn.ToolTip = $tt

    # sin cuadro: el mosaico va transparente y solo se pinta el circulo de la letra cuando no hay icono
    $bd = New-Object System.Windows.Controls.Border
    $bd.Width = $sz; $bd.Height = $sz
    $bd.CornerRadius = New-Object System.Windows.CornerRadius(999)
    $bd.Background = Br $script:Theme.badge
    $bd.ClipToBounds = $true

    $txt = New-Object System.Windows.Controls.TextBlock
    $txt.Text = if ($it.Name) { $it.Name.Substring(0, 1).ToUpperInvariant() } else { '?' }
    $txt.FontSize = $sz * 0.44
    $txt.Foreground = Br $script:Theme.letter
    $txt.FontFamily = New-Object System.Windows.Media.FontFamily('Segoe UI')
    $txt.HorizontalAlignment = [System.Windows.HorizontalAlignment]::Center
    $txt.VerticalAlignment = [System.Windows.VerticalAlignment]::Center
    $bd.Child = $txt
    $btn.Content = $bd

    $script:IconQueue.Enqueue(@{ btn = $btn; it = $it; path = $it.Icon })
    $script:Window.Dispatcher.BeginInvoke([System.Action] { Pump-Icons },
        [System.Windows.Threading.DispatcherPriority]::ContextIdle) | Out-Null

    $btn.Add_Click({ Start-Entry $this.Tag 'click' })
    $btn.Add_MouseRightButtonUp({ Show-ItemMenu $this.Tag $this })
    # WPF no tiene Add_MouseMiddleButtonDown (el evento de boton del raton solo existe como attached
    # event); enmascararlo con PreviewMouseDown es lo que deja el favorito de en medio funcionando.
    # Con el nombre equivocado PS solo escribe el error en el flujo de errores y el gesto no existe.
    $btn.Add_PreviewMouseDown({
        param($s, $e)
        if ($e.ChangedButton -eq [System.Windows.Input.MouseButton]::Middle) {
            Toggle-Fav $s.Tag
            $e.Handled = $true
        }
    })

    # reordenar arrastrando con un arrastre propio (no de OLE): solo asi se puede mover el hueco en
    # caliente mientras se pasa el raton por los vecinos y soltar fuera para sacarlo del dock
    $btn.Add_PreviewMouseLeftButtonDown({ param($s, $e) Tile-Down $s $e })
    $btn.Add_PreviewMouseMove({ param($s, $e) Tile-Move $s $e })
    $btn.Add_PreviewMouseLeftButtonUp({ param($s, $e) Tile-Up $s $e })
    $btn.Add_LostMouseCapture({ param($s, $e) Tile-LostCapture $s })
    return $btn
}

# El arrastre vive en funciones y los handlers son delegados de una linea: comparten estado (DragBtn /
# DragStart / DragMoved / PressBtn) entre cuatro eventos y asi esa logica esta en un solo sitio en lugar
# de repetida dentro de cada closure del boton.
function Tile-Down($s, $e) {
    $script:DragBtn = $s
    $script:DragStart = $e.GetPosition($script:RootGrid)
    $script:DragMoved = $false
    # el boton se hunde bajo el dedo antes de lanzar nada: es el detalle que hace que el dock
    # se sienta fisico en lugar de un mapa de bits que cambia de tamano
    $script:PressBtn = $s
    Set-TileTargets
}

function Tile-Move($s, $e) {
    if ($script:DragBtn -ne $s -or -not $script:DragStart) { return }
    if ($e.LeftButton -ne [System.Windows.Input.MouseButtonState]::Pressed) { $script:DragBtn = $null; return }
    $p = $e.GetPosition($script:RootGrid)
    if (-not $script:DragMoved) {
        # raiz de la suma de cuadros a mano: Math.Hypot no esta en el .NET Framework de PS 5.1 y
        # un metodo que no existe deja la variable en null, con lo que la comparacion de abajo
        # siempre se cumple y el arrastre no empieza nunca
        $dx = [double]($p.X - $script:DragStart.X)
        $dy = [double]($p.Y - $script:DragStart.Y)
        $d = [Math]::Sqrt($dx * $dx + $dy * $dy)
        if ($d -lt [System.Windows.SystemParameters]::MinimumHorizontalDragDistance) { return }
        $script:DragMoved = $true
        [System.Windows.Controls.Panel]::SetZIndex($s, 30)
        # el hueco de origen se queda atenuado: es la referencia de donde salio el icono y de
        # donde vuelve a caer si se suelta dentro
        $s.Opacity = 0.3
        Start-Ghost $s $p
        Write-Log "arrastrando: $($s.Tag.Name)"
    }
    if (-not $s.IsMouseCaptured) { $null = $s.CaptureMouse() }
    Move-DraggedTile $p
    Move-Ghost $p
}

function Tile-Up($s, $e) {
    if ($script:PressBtn -eq $s) {
        $script:PressBtn = $null
        Set-TileTargets
    }
    if (-not $script:DragMoved) { return }
    $e.Handled = $true          # soltar despues de arrastrar no tiene que lanzar nada
    End-TileDrag ($e.GetPosition($script:RootGrid))
}

function Tile-LostCapture($s) {
    if ($script:PressBtn -eq $s) { $script:PressBtn = $null; Set-TileTargets }
    # perdida de captura = arrastre cortado por fuera (Alt+Tab, menu del sistema, etc). Sin esto
    # el fantasma quedaria clavado en pantalla con el tile de origen apagado para siempre
    if ($script:DragMoved -and $script:DragBtn -eq $s) {
        $s.Opacity = 1.0
        End-Ghost ([System.Windows.Input.Mouse]::GetPosition($script:RootGrid))
        $script:DragBtn = $null; $script:DragMoved = $false; $script:DragStart = $null
        [System.Windows.Controls.Panel]::SetZIndex($s, 0)
    }
}

function Get-StripTiles {
    $out = @()
    foreach ($c in $script:Strip.Children) {
        if ($c -is [System.Windows.Controls.Button]) { $out += $c }
    }
    return $out
}

# en que hueco de la fila cae un punto: la fila empieza en el hueco de entrada y se desplaza al hacer
# scroll, asi que hay que quitar las dos cosas
function Get-SlotAt([System.Windows.Point]$p) {
    $n = $script:Tiles.Count
    if ($n -le 0) { return -1 }
    $vert = $script:Cfg['position'] -in 'left', 'right'
    $u = if ($vert) { [double]$p.Y } else { [double]$p.X }
    $off = if ($vert) { [double]$script:StripScroll.VerticalOffset } else { [double]$script:StripScroll.HorizontalOffset }
    $k = [int][Math]::Floor(($u - (Get-RowInset) + $off) / (Get-Pitch))
    return [Math]::Max(0, [Math]::Min($n - 1, $k))
}

function Move-DraggedTile([System.Windows.Point]$p) {
    $b = $script:DragBtn
    if (-not $b) { return }
    $cur = [Array]::IndexOf($script:Tiles, $b)
    $k = Get-SlotAt $p
    if ($k -lt 0 -or $k -eq $cur) { Update-Proximity $p; return }
    $null = $script:Strip.Children.Remove($b)
    $script:Strip.Children.Insert($k, $b)
    $script:Tiles = Get-StripTiles
    Fit-Pill
    Update-Proximity $p
}

function End-TileDrag([System.Windows.Point]$p) {
    $b = $script:DragBtn
    $script:DragBtn = $null; $script:DragMoved = $false; $script:DragStart = $null
    if (-not $b) { return }
    $b.Opacity = 1.0
    End-Ghost $p
    try { $null = $b.ReleaseMouseCapture() } catch { }
    [System.Windows.Controls.Panel]::SetZIndex($b, 0)
    if (Test-OutsidePill $p) {
        Drop-FromDock $b.Tag
        return
    }
    Commit-Order
    Reset-Proximity
}

# fuera de la pildora (con un dedo de holgura) es "sacar del dock"; la banda transparente que
# rodea la pildora sigue contando como dentro, que ahi el icono todavia se ve
function Test-OutsidePill([System.Windows.Point]$p) {
    $w = [double]$script:RootGrid.ActualWidth
    $h = [double]$script:RootGrid.ActualHeight
    $b = [double]$script:Margin + 6.0
    return ($p.X -lt -$b -or $p.Y -lt -$b -or $p.X -gt ($w + $b) -or $p.Y -gt ($h + $b))
}

# ------------------------------------------------------------------ icono flotante del arrastre
# La pildora vive al nivel del escritorio y no mide mas de lo que mide: un icono que se saca del dock
# no puede pintarse dentro de su ventana, se cortaria contra su borde. Se suelta en una ventanita
# propia, topmost, sin activacion y click-through, que va pegada al cursor cuadro a cuadro. El tile de
# origen se queda en la fila como hueco atenuado, que es lo que deja ver a donde caera.
$script:GhostWin = $null
$script:GhostBox = $null
$script:GhostPad = 34

function Get-DipScale {
    try {
        $ps = [System.Windows.PresentationSource]::FromVisual($script:Window)
        if ($ps) { return [double]$ps.CompositionTarget.TransformToDevice.M11 }
    } catch { }
    return 1.0
}

function New-GhostWindow {
    $sz = [int]$script:Cfg['iconSize']
    # la sombra necesita sitio fuera del cuadro: si no, la recorta el borde de la propia ventana
    $pad = $script:GhostPad
    $w = New-Object System.Windows.Window
    $w.WindowStyle = [System.Windows.WindowStyle]::None
    $w.AllowsTransparency = $true
    $w.ResizeMode = [System.Windows.ResizeMode]::NoResize
    $w.ShowInTaskbar = $false
    $w.ShowActivated = $false
    $w.Topmost = $true
    $w.Title = 'GamesDockGhost'
    $w.Background = [System.Windows.Media.Brushes]::Transparent
    $w.SizeToContent = [System.Windows.SizeToContent]::Manual
    $w.Width = $sz + 2 * $pad
    $w.Height = $sz + 2 * $pad
    $w.WindowStartupLocation = [System.Windows.WindowStartupLocation]::Manual
    $w.IsHitTestVisible = $false
    $w.Opacity = 0.0
    # fuera del alcance hasta el primer arrastre: la ventana ya esta montada y el icono aparece pegado
    # al cursor en el mismo cuadro en que se levanta, sin el paron de crear la HWND a mitad del gesto
    $w.Left = -32000.0; $w.Top = -32000.0

    $box = New-Object System.Windows.Controls.Border
    $box.Width = $sz; $box.Height = $sz
    $box.HorizontalAlignment = [System.Windows.HorizontalAlignment]::Center
    $box.VerticalAlignment = [System.Windows.VerticalAlignment]::Center
    $box.ClipToBounds = $true
    $box.RenderTransformOrigin = New-Object System.Windows.Point(0.5, 0.5)
    $box.RenderTransform = New-Object System.Windows.Media.ScaleTransform
    # Fant: el mismo filtro por hardware que el tile, si no el icono flotante se ve borroso justo
    # cuando es lo unico que mira el usuario
    [System.Windows.Media.RenderOptions]::SetBitmapScalingMode($box, [System.Windows.Media.BitmapScalingMode]::Fant)
    $som = New-Object System.Windows.Media.Effects.DropShadowEffect
    $som.Color = [System.Windows.Media.Color]::FromArgb(255, 2, 4, 9)
    $som.BlurRadius = 26; $som.ShadowDepth = 10; $som.Opacity = 0.6; $som.Direction = 270
    $box.Effect = $som

    $shell = New-Object System.Windows.Controls.Grid
    $shell.Margin = New-Object System.Windows.Thickness($pad)
    $null = $shell.Children.Add($box)
    $w.Content = $shell

    $w.Add_SourceInitialized({
        param($s, $e)
        try {
            $h = (New-Object System.Windows.Interop.WindowInteropHelper($s)).Handle
            $ex = [GD32]::GetWindowLong($h, -20)
            # WS_EX_TRANSPARENT (no se come ni un click) | WS_EX_TOOLWINDOW (ni alt-tab) |
            # WS_EX_NOACTIVATE (robar la activacion al arrastrar cortaria lo que hubiera debajo)
            $null = [GD32]::SetWindowLong($h, -20, $ex -bor 0x20 -bor 0x80 -bor 0x08000000)
        } catch { Write-Log ("estilo del fantasma: " + $_.Exception.Message) }
    })
    $script:GhostWin = $w
    $script:GhostBox = $box
    # se muestra una sola vez para que la HWND y el primer render queden pagados aqui, y se vuelve
    # a esconder: CompositionTarget.Rendering es un evento ESTATICO que dispara una vez por target de
    # composicion, asi que una segunda ventana visible (aunque este fuera de pantalla y a opacidad 0)
    # duplica los cuadros de la animacion y el trabajo del integrador
    $w.Show()
    $w.Hide()
    return $w
}

function Move-Ghost([System.Windows.Point]$p) {
    if (-not $script:GhostWin) { return }
    # PointToScreen devuelve pixeles fisicos y Window.Left/Top quieren DIP: RootGrid esta ademas
    # desplazada el margen de la banda transparente
    $sc = Get-DipScale
    $sp = $script:Window.PointToScreen((New-Object System.Windows.Point(([double]$p.X + $script:Margin), ([double]$p.Y + $script:Margin))))
    $script:GhostWin.Left = ($sp.X / $sc) - ([double]$script:GhostWin.Width / 2.0)
    $script:GhostWin.Top  = ($sp.Y / $sc) - ([double]$script:GhostWin.Height / 2.0)
}

function Start-Ghost($btn, [System.Windows.Point]$p) {
    if (-not $script:GhostWin) { $null = New-GhostWindow }
    $sz = [int]$script:Cfg['iconSize']
    # la ventanita es de un tamano fijo pero el icono se puede ajustar en los ajustes: se redimensiona
    # al levantar, que es lo barato, en lugar de recrearla
    if ([int]$script:GhostBox.Width -ne $sz) {
        $script:GhostBox.Width = $sz; $script:GhostBox.Height = $sz
        $script:GhostWin.Width = $sz + 2 * $script:GhostPad
        $script:GhostWin.Height = $sz + 2 * $script:GhostPad
    }
    $bd = $btn.Content
    $box = $script:GhostBox
    $box.Background = $bd.Background
    $box.CornerRadius = $bd.CornerRadius
    # la letra de respaldo vive dentro del borde del tile: se CLONA, un visual no puede tener dos padres
    $box.Child = $null
    if ($bd.Child -is [System.Windows.Controls.TextBlock]) {
        $t = $bd.Child
        $cl = New-Object System.Windows.Controls.TextBlock
        $cl.Text = $t.Text
        $cl.FontSize = $t.FontSize
        $cl.Foreground = $t.Foreground
        $cl.FontFamily = $t.FontFamily
        $cl.HorizontalAlignment = [System.Windows.HorizontalAlignment]::Center
        $cl.VerticalAlignment = [System.Windows.VerticalAlignment]::Center
        $box.Child = $cl
    }
    Move-Ghost $p
    if (-not $script:GhostWin.IsVisible) { $script:GhostWin.Show() }
    # arranque a mano: Move-Spring ignora el objetivo si ya era el de la ultima vez, y aqui lo que se
    # cambia es la POSICION del muelle, no su objetivo
    foreach ($g in @($script:GhostSpr, $script:GhostFade)) { $g.done = $null }
    $script:GhostSpr.x = 0.42; $script:GhostSpr.v = 0.0; $script:GhostSpr.t = 0.0
    $script:GhostFade.x = 0.0; $script:GhostFade.v = 0.0; $script:GhostFade.t = 0.0
    $script:GhostWin.Opacity = 0.0
    Move-Spring $script:GhostSpr 1.0
    Move-Spring $script:GhostFade 1.0
}

function End-Ghost([System.Windows.Point]$p) {
    if (-not $script:GhostWin) { return }
    Move-Ghost $p
    # se guarda, no se cierra: la ventanita cuesta montarla una vez y vaciarla por completo cada vez
    # que se suelta un icono es volver a pagarla en el siguiente arrastre
    $script:GhostFade.done = { Park-Ghost }
    Move-Spring $script:GhostSpr 0.0
    Move-Spring $script:GhostFade 0.0
}

function Park-Ghost {
    if (-not $script:GhostWin) { return }
    $script:GhostWin.Opacity = 0.0
    $script:GhostWin.Left = -32000.0; $script:GhostWin.Top = -32000.0
    # ocultar, no cerrar: deja de participar en el paso de composicion (vease New-GhostWindow) y la
    # ventana sigue viva para el siguiente arrastre
    if ($script:GhostWin.IsVisible) { $script:GhostWin.Hide() }
}

function Close-Ghost {
    if (-not $script:GhostWin) { return }
    try { $script:GhostWin.Close() } catch { }
    $script:GhostWin = $null
    $script:GhostBox = $null
}

function Commit-Order {
    $keys = @()
    foreach ($t in (Get-StripTiles)) { if ($t.Tag) { $keys += [string]$t.Tag.Key } }
    if ($keys.Count -eq 0) { return }
    $rest = @($script:Cfg['order'] | Where-Object { $keys -notcontains [string]$_ })
    $script:Cfg['order'] = @($keys) + $rest
    Write-Log "orden guardado: $($keys.Count)"
    Write-Config
}

# soltar un icono fuera de la barra lo oculta: se puede devolver desde el menu de la píldora
function Drop-FromDock($it) {
    if (-not $it) { return }
    $k = [string]$it.Key
    $script:Cfg['order'] = @(@($script:Cfg['order']) | Where-Object { $k -ne [string]$_ })
    Write-Log "sacado del dock: $($it.Name)"
    Hide-FromDock $it
}

function Unhide-Pin([string]$Key) {
    Set-PinOverride ([pscustomobject]@{ Key = $Key }) @{ hidden = $false }
    Write-Config
    Build-Index
    Rebuild-Strip
}

function Rebuild-Strip {
    param([switch]$Pop)
    $script:Strip.Children.Clear()
    $items = Get-VisibleItems
    $script:Tiles = @()
    # los muelles de los tiles viejos apuntan a botones que ya no estan en el arbol: fuera o se
    # acumulan en cada reconstruccion
    $null = $script:Springs.RemoveAll({ param($s) $s.kind -eq 'tile' })
    $script:MagSpr = New-Object 'System.Collections.Generic.List[object]'
    $i = 0
    foreach ($it in $items) {
        $t = New-Tile $it
        $script:Strip.Children.Add($t) | Out-Null
        $script:Tiles += $t
        # muelle de escala propio por tile. Con -Pop entran escalonados y con un asomo de rebote,
        # que es como se ve una app cara al abrirse; sin -Pop nacen ya asentados.
        # k=900 en lugar de 300: el tile es lo ultimo que queda entre el cursor y lo que se ve, y a
        # omega 17 la fila llegaba al agrandado ~340 ms despues de pasar el raton por encima.
        $sp = New-Spring 'tile' 900.0 0.85 $t
        if ($Pop) { $sp.x = 0.62; $sp.t = 1.0; $sp.wait = (3.0 + $i * 1.6) / 60.0 } else { $sp.x = 1.0; $sp.t = 1.0 }
        $null = $script:MagSpr.Add($sp)
        $null = $script:Springs.Add($sp)
        $i++
    }
    if ($Pop -and $i -gt 0) { Start-Anim }
    if ($items.Count -eq 0) {
        $hint = New-Object System.Windows.Controls.TextBlock
        $hint.Text = 'Clic derecho en el hueco para anadir o configurar.'
        $hint.Foreground = Br $script:Theme.fieldFg
        $hint.FontSize = 12
        $hint.VerticalAlignment = [System.Windows.VerticalAlignment]::Center
        $hint.Margin = New-Object System.Windows.Thickness(8, 0, 0, 0)
        $script:Strip.Children.Add($hint) | Out-Null
    }
    $script:MagTop = -1
    Fit-Pill
    Set-BarMargins
    Refresh-Pill
}

function Set-Section([string]$id) {
    $script:Cfg['section'] = $id
    Rebuild-Strip
    Write-Config
}

# holgura interior de la píldora: la vertical manda y la horizontal la sigue (proporcion 18/12)
function Get-PillPadding {
    $v = [Math]::Max(2, [int]$script:Cfg['pillPad'])
    return @{ h = $v + 6; v = $v }
}

# el hueco del tile tiene dos medidas: a lo largo de la fila separa iconos y da sitio al halo,
# y en transversal es lo que manda en el grosor de la pildora, asi que va apretado
function Get-RingMain  { [Math]::Max(7, [int]([int]$script:Cfg['iconSize'] * 0.18)) }
function Get-RingCross { [Math]::Max(4, [int]([int]$script:Cfg['iconSize'] * 0.09)) }
function Get-TileBox   { [double]([int]$script:Cfg['iconSize'] + 2 * (Get-RingCross)) }
function Get-Pitch     { [double]([int]$script:Cfg['iconSize'] + 2 * (Get-RingMain)) }

# separacion de la fila respecto al borde de la pildora a lo largo de la fila: es lo que evita que
# el primer icono se salga por la izquierda, y define el ancho junto con la cantidad de iconos.
# El colchon extra es el "espacio vacio" donde dar click derecho para abrir el menu del dock: la
# banda que hay fuera de la pildora es click-through, asi que sin hueco de vidrio no habria donde.
function Get-RowInset { [double]((Get-PillPadding).h + 10) }

# medio grosor de la pildora en reposo y con el raton encima: el maximo deja dentro el icono
# agrandado del magnes, con su margen de halo
function Get-PillHalfIdle { [double]((Get-TileBox) + 2 * (Get-PillPadding).v) / 2.0 }
# el maximo deja el tile entero agrandado (con su halo) dentro del cristal, con un dedo de aire para
# que el borde animado nunca le muerda la punta
function Get-PillHalfMax {
    $need = ([double](Get-TileBox) * $script:MagPeak) / 2.0 + (Get-PillPadding).v + 4.0
    [double][Math]::Max($need, (Get-PillHalfIdle) + 7.0)
}

# cuanto desborda la fila por los lados del eje transversal: lo que pide el icono en su maximo
# agrandado, holgura incluida. Se mete en la banda transparente que la ventana deja libre
# alrededor de la pildora; si no cabe aqui, el ScrollViewer recorta el icono.
function Get-MagOverrun {
    if (-not [bool]$script:Cfg['magnify']) { return 6.0 }
    # lo que desborda la fila por el eje transversal: la punta del icono en su maximo agrandado,
    # contada desde el eje EN REPOSO, mas lo que la fila entera se desplaza hacia dentro cuando la
    # pildora se hincha (RowShift). Sin esa segunda cuenta el ScrollViewer recorta la punta de arriba
    # justo cuando el magnes esta a tope, que es cuando mas se nota.
    $delta = (Get-PillHalfMax) - (Get-PillHalfIdle)
    $need = ([double](Get-TileBox) * $script:MagPeak) / 2.0 - (Get-PillHalfIdle) + 4.0 + $delta * 0.45
    return [Math]::Max(6.0, [Math]::Min($need, [double]([int]$script:Margin - 2)))
}

function Get-MagScale([double]$d, [double]$r) {
    if ($r -le 0) { return 1.0 }
    $a = [Math]::Abs($d) / $r
    if ($a -ge 1) { return 1.0 }
    $c = [Math]::Cos($a * [Math]::PI / 2.0)
    return [double](1.0 + ($script:MagPeak - 1.0) * $c * $c)
}

# la fila se desplaza hacia dentro lo que la pildora gana por fuera: asi el borde que da al borde
# de la pantalla no se mueve y el dock parece que crece hacia el centro del escritorio
$script:BarSig = ''
function Set-BarMargins {
    $inset = Get-RowInset
    $ovr = [double][Math]::Ceiling((Get-MagOverrun))
    $sig = '{0}|{1}|{2}' -f $script:Cfg['position'], $inset, $ovr
    if ($sig -eq $script:BarSig) { return }
    $script:BarSig = $sig
    # el margen solo abre el hueco ESTATICO para que el icono agrandado desborde el cristal. Lo que se
    # mueve cuadro a cuadro va por Set-RowShift: escribir Margin en cada cuadro costaba una pasada de
    # layout completa y UseLayoutRounding cuantizaba la fila a pixel entero mientras el cristal
    # (geometria, sin redondear) se deslizaba subpixel - eso era el microcorte
    $gap = -$ovr
    if ($script:Cfg['position'] -in 'left', 'right') {
        $script:Bar.Margin = New-Object System.Windows.Thickness($gap, $inset, $gap, $inset)
    } else {
        $script:Bar.Margin = New-Object System.Windows.Thickness($inset, $gap, $inset, $gap)
    }
}

# el desplazamiento de la fila hacia dentro, en el paso de render: subpixel y sin layout
function Set-RowShift([double]$s) {
    $out = switch ($script:Cfg['position']) { 'bottom' { 1.0 } 'right' { 1.0 } 'top' { -1.0 } 'left' { -1.0 } default { 1.0 } }
    $tr = $script:Bar.RenderTransform
    if (-not ($tr -is [System.Windows.Media.TranslateTransform])) {
        $tr = New-Object System.Windows.Media.TranslateTransform
        $script:Bar.RenderTransform = $tr
    }
    $d = 0.0 - $out * $s
    if ($script:Cfg['position'] -in 'left', 'right') {
        if ([Math]::Abs($tr.X - $d) -gt 0.005) { $tr.X = $d }
    } else {
        if ([Math]::Abs($tr.Y - $d) -gt 0.005) { $tr.Y = $d }
    }
}

# contorno de la pildora: rectangulo redondeado que se hincha donde el raton agranda los iconos.
# Se muestrea solo la ventana del magnes (el resto es recto) y las tapas llevan semicirculo propio.
# El muestreo y la spline viven en GDPill (C# compilado, ver arriba): ver Update-PillShape.
function Update-PillShape {
    $gw = [double]$script:RootGrid.ActualWidth
    $gh = [double]$script:RootGrid.ActualHeight
    if ($gw -lt 2 -or $gh -lt 2) { return }
    $vert = $script:Cfg['position'] -in 'left', 'right'
    $base = Get-PillHalfIdle
    $fracc = if ([bool]$script:Cfg['magnify']) { 0.55 } else { 0.0 }
    # cuanto esta agrandado de verdad el tile mas grande, sacado de los muelles y no del cursor: es lo
    # que ata el bulto del cristal al tamano real de los iconos. Los dos extremos se anotan aparte,
    # que son los que pisan la tapa y deciden cuanto estira la pildora por cada lado.
    $mx = 1.0; $eL = 1.0; $eR = 1.0
    $n = $script:MagSpr.Count
    for ($i = 0; $i -lt $n; $i++) {
        $x = [double]$script:MagSpr[$i].x
        if ($x -gt $mx) { $mx = $x }
        if ($i -eq 0) { $eL = $x } elseif ($i -eq ($n - 1)) { $eR = $x }
    }
    $script:MagLive = [Math]::Max(0.0, [Math]::Min(1.0, ($mx - 1.0) / ($script:MagPeak - 1.0)))
    $sig = ('{0}|{1}|{2}x{3}|{4}|{5}|{6}|{7}|{8}' -f $script:Cfg['position'], [bool]$script:Cfg['magnify'],
            [int][Math]::Round($gw), [int][Math]::Round($gh),
            [Math]::Round($script:HoverT, 3), [Math]::Round($script:PtrLive, 1),
            [Math]::Round($script:MagLive, 3), [Math]::Round($eL, 3), [Math]::Round($eR, 3))
    if ($sig -eq $script:ShapeSig) { return }
    $script:ShapeSig = $sig
    # manda lo que este MAS abierto de los dos: el hover o el agrandado real de los tiles. Si sólo
    # mandara HoverT el cristal se adelanta (vidrio de sobra con los iconos chicos) y si el cristal
    # se quedara atras son los iconos los que muerden el borde de arriba, que era el corte
    $grow = [Math]::Max([double]$script:HoverT, [double]$script:MagLive)
    $delta = ((Get-PillHalfMax) - $base) * $grow
    $uni = $delta * (1.0 - $fracc)
    # el bulto local sigue el agrandado animado de los iconos: si no, la pildora se deshincharia
    # de golpe al salir el raton mientras los tiles todavia estan grandes
    $amp = $delta * $fracc * [double]$script:MagLive
    $out = switch ($script:Cfg['position']) { 'bottom' { 1.0 } 'right' { 1.0 } 'top' { -1.0 } 'left' { -1.0 } default { 1.0 } }
    # la pildora se corre hacia dentro lo que gana por fuera: el borde que da al borde de la pantalla
    # no se hunde debajo de la barra de tareas y los iconos siguen en el eje del cristal, que es
    # simetrico alrededor de ese mismo eje. Solo cuenta el grosor uniforme: el bulto local NO mueve la
    # fila, porque si lo hace el borde exterior ya no queda clavado y la excursion hacia arriba come
    # la banda transparente, que es donde el ScrollViewer recorta la punta de los iconos
    $script:RowShift = $uni
    $cc = if ($vert) { $gw / 2.0 } else { $gh / 2.0 }
    $cc = $cc - $out * $script:RowShift
    $len = if ($vert) { $gh } else { $gw }
    # el icono de un extremo, al agrandarse, se sale por la punta de la tapa: ese lado estira lo que
    # el tile suyo entra, y solo hasta donde aguanta la banda transparente de la ventana
    $estir = 0.5 * (Get-Pitch) + 10.0
    $tope  = [double]$script:Margin - 12.0
    $overL = [Math]::Min($tope, [Math]::Max(0.0, ($eL - 1.0) * $estir))
    $overR = [Math]::Min($tope, [Math]::Max(0.0, ($eR - 1.0) * $estir))
    Set-BarMargins
    Set-RowShift ([double]$script:RowShift)

    $geo = [GDPill]::Build($len, $cc, $base, $uni, $amp, [double]$script:PtrLive, (Get-Pitch),
                           [double]$script:MagPeak, $vert, $overL, $overR)
    $script:Glass.Data = $geo
    $script:Edge.Data  = $geo
}

# ------------------------------------------------------------------ motor de muelles
# Un solo bucle que solo vive mientras hay transiciones y se desconoce al asentar: en reposo no queda
# nada programado, que es lo que pide el dock. Todo lo que se mueve (la hinchazon de la pildora, el
# agrandado de los iconos, la entrada y la salida) pasa por aqui, así que comparte inercia y caracter
# en lugar de solaparse con animaciones sueltas.
$script:Springs  = New-Object 'System.Collections.Generic.List[object]'
$script:ShapeSig = ''
$script:ShapeDirty = $false
# el reloj lo da el propio compositor: Rendering llama una vez por cuadro presentado, así que el
# muelle se integra justo donde se pinta. Un DispatcherTimer de 16 ms deriva con el timer del sistema
# (ráfagas y huecos) y en un panel de 120 Hz solo muele la animación a 62 cuadros, que se lee como
# microcortes. El evento se desconecta al asentarse: eso es lo que deja el reposo en 0 % de CPU.
$script:AnimOn    = $false
$script:LastFrame = 0.0
$script:Clock     = New-Object System.Diagnostics.Stopwatch
$script:RenderHdl = [System.EventHandler] { param($s, $e) Invoke-Frame }

function Start-Anim {
    if ($script:AnimOn) { return }
    $script:AnimOn = $true
    $script:Clock = [System.Diagnostics.Stopwatch]::StartNew()
    $script:LastFrame = $script:Clock.Elapsed.TotalSeconds
    [System.Windows.Media.CompositionTarget]::add_Rendering($script:RenderHdl)
}

function Stop-Anim {
    if (-not $script:AnimOn) { return }
    $script:AnimOn = $false
    [System.Windows.Media.CompositionTarget]::remove_Rendering($script:RenderHdl)
    $script:Clock.Stop()
}

# dt real entre cuadros: con el mismo k/zeta la animación tarda lo mismo a 60, 120 o 144 Hz
function Invoke-Frame {
    # raiz del "va fluido pero a saltos": RenderingTime es un TimeSpan privativo de cada MediaContext y
    # CompositionTarget.Rendering es un evento ESTATICO, asi que desde que el fantasma tiene ventana
    # propia hay dos relojes alternandose. El unico fiable es un cronometro del proceso.
    # Antes se castreaba a DateTime, lo que reventaba cada cuadro (una excepcion por cuadro cuesta mas
    # que toda la fisica junto) y, con $dt null, la animacion se integraba siempre a 1/60 fijo: a 60 Hz
    # se hacia creible y en un panel de 120 Hz iría un 80 % rapida.
    $now = $script:Clock.Elapsed.TotalSeconds
    $dt = $now - $script:LastFrame
    # el mismo vsync puede disparar el evento por las dos ventanas: ese segundo golpe no es un cuadro
    if ($dt -lt 0.002) { return }
    $script:LastFrame = $now
    # un corte largo (ventana tapada, cambio de tema) no debe teletransportar los muelles
    if ($dt -gt 0.0333) { $dt = 0.0333 }
    # subpasos de 4 ms: con la rigidez que pide la respuesta al cursor, un cuadro de 16 ms integrado
    # de golpe se pasa del objetivo y el muelle se lee "goma" en lugar de "pegado al dedo". Integrando
    # en trozos fijos el recorrido es AUN IGUAL a 60, 120 y 165 Hz y el coste es aritmetica suelta
    # (11 muelles x 4 subpasos por cuadro), no otra pasada de layout.
    $n = [int][Math]::Ceiling($dt / 0.004)
    if ($n -lt 1) { $n = 1 } elseif ($n -gt 8) { $n = 8 }
    $h = $dt / $n
    $vivo = $false
    for ($i = 0; $i -lt $n; $i++) { if (Step-AllSprings $h) { $vivo = $true } }
    # un solo repintado del contorno por cuadro: los muelles 'pill', 'ptr' y 'slide' lo piden cada uno
    # y, sin esta cola, el segundo se comeria el trabajo del primero dentro del mismo frame
    if ($script:ShapeDirty) { $script:ShapeDirty = $false; Update-PillShape }
    if (-not $vivo) { Stop-Anim }
}

function Step-AllSprings([double]$dt) {
    $vivo = $false
    foreach ($s in $script:Springs) { if (Step-Spring $s $dt) { $vivo = $true } }
    return $vivo
}

# k = rigidez (cuanto antes quiere llegar), zeta = amortiguacion (1 = sin rebote; por debajo, un
# asomo de rebote que es lo que hace que se lea caro en vez de mecanico). wait = segundos de retardo
# antes de arrancar, para escalonar las entradas.
function New-Spring([string]$Kind, [double]$K, [double]$Zeta, $Owner) {
    # cuando esta "asentado" depende de las unidades: 'ptr' y 'slide' van en DIP (valores de cientos)
    # y el resto van normalizados de 0 a 1. Con la tolerancia fina, el muelle del cursor seguia
    # moviendo cuadros para recorrer 1/mil de pixel despues de cada pasada del raton.
    $dip = $Kind -in 'ptr', 'slide', 'ghostx', 'ghosty'
    return @{ kind = $Kind; o = $Owner; x = 0.0; v = 0.0; t = 0.0; wait = 0.0; done = $null
              k = $K; c = (2.0 * [Math]::Sqrt($K) * $Zeta)
              eps = $(if ($dip) { 0.02 } else { 0.0008 })
              veps = $(if ($dip) { 0.8 } else { 0.006 }) }
}

function Move-Spring($s, [double]$to) {
    if ($null -eq $s) { return }
    if ([Math]::Abs([double]$s.t - $to) -lt 0.0005) { return }
    $s.t = $to
    $s.wait = 0.0   # si el cursor pide algo durante la entrada, ese tile deja de esperar
    Start-Anim
}

function Apply-Spring($s) {
    switch ($s.kind) {
        'pill'  { $script:HoverT = [double]$s.x; $script:ShapeDirty = $true }
        'ptr'   { $script:PtrLive = [double]$s.x; Set-TileTargets; $script:ShapeDirty = $true }
        'tile'  {
            $b = $s.o
            if ($b) {
                $tf = $b.RenderTransform
                if ($tf -is [System.Windows.Media.ScaleTransform]) {
                    $tf.ScaleX = [double]$s.x; $tf.ScaleY = [double]$s.x
                }
            }
        }
        'slide' {
            $g = $script:RootGrid
            if ($g) {
                $tr = $g.RenderTransform
                if ($tr -is [System.Windows.Media.TranslateTransform]) {
                    if ([string]$s.o -eq 'x') { $tr.X = [double]$s.x } else { $tr.Y = [double]$s.x }
                }
            }
            $script:ShapeDirty = $true
        }
        'fade'  { if ($script:RootGrid) { $script:RootGrid.Opacity = [double]$s.x } }
        # el fantasma arranca en 0.42 y crece hasta 1: el icono se hincha al despegarse de la fila.
        # 1.16 de tope: algo mas grande que el tile, que es lo que vende que esta por encima del cristal
        'ghost' {
            $b = $script:GhostBox
            if ($b) {
                $tf = $b.RenderTransform
                if ($tf -is [System.Windows.Media.ScaleTransform]) {
                    $tf.ScaleX = 1.16 * [double]$s.x; $tf.ScaleY = 1.16 * [double]$s.x
                }
            }
        }
        'ghostfade' { if ($script:GhostWin) { $script:GhostWin.Opacity = [double]$s.x } }
    }
}

# un paso de integracion: aceleracion = rigidez * error - amortiguacion * velocidad
function Step-Spring($s, [double]$dt) {
    if ([double]$s.wait -gt 0.0) { $s.wait = [double]$s.wait - $dt; return $true }
    $d = [double]$s.t - [double]$s.x
    if ([Math]::Abs($d) -lt [double]$s.eps -and [Math]::Abs([double]$s.v) -lt [double]$s.veps) {
        if ([double]$s.x -ne [double]$s.t) { $s.x = $s.t; $s.v = 0.0; Apply-Spring $s }
        if ($s.done) { $c = $s.done; $s.done = $null; & $c }
        return $false
    }
    $s.v = [double]$s.v + ($s.k * $d - $s.c * [double]$s.v) * $dt
    $s.x = [double]$s.x + [double]$s.v * $dt
    Apply-Spring $s
    return $true
}

# muelles fijos del dock: la pildora, la posicion del cursor (suavizada) y la entrada/salida
# k manda lo TARDA en llegar (omega = sqrt(k), el 2 % se asienta en ~5,8/omega segundos) y zeta cuanto
# rebota: 1 = sin rebote. Antes el cursor se suavizaba con k=900 (omega 30), unos 190 ms de retardo
# perceptible: la fila llegaba tarde al paso del raton. Con k=3200 son ~100 ms y, como va pegada al
# mismo muelle que hincha el cristal, las dos cosas se mueven juntas.
$script:HoverSpr = New-Spring 'pill'  340.0 0.92 $null   # con un pelin de rebote: la pildora "respira"
$script:PtrSpr   = New-Spring 'ptr'  3200.0 1.0  $null   # pegada al cursor, pero sin saltos
$script:SlideSpr = New-Spring 'slide' 260.0 1.0  $null
$script:FadeSpr  = New-Spring 'fade'  320.0 1.0  $null
# el fantasma del arrastre: escala con un dedo de rebote (despegar algo de un cristal se nota con
# ese tiny overshoot) y opacidad seca, porque lo que cierra la ventanita es el 'done' del fade
$script:GhostSpr  = New-Spring 'ghost'     700.0 0.68 $null
$script:GhostFade = New-Spring 'ghostfade' 420.0 1.0  $null
foreach ($s in @($script:HoverSpr, $script:PtrSpr, $script:SlideSpr, $script:FadeSpr,
                 $script:GhostSpr, $script:GhostFade)) {
    $null = $script:Springs.Add($s)
}
$script:MagSpr   = New-Object 'System.Collections.Generic.List[object]'   # uno por tile, en Rebuild-Strip
$script:PressBtn = $null

# el agrandado pedido por el cursor, traducido a objetivos de los muelles de cada tile. Se recalcula
# desde la posicion suavizada, no de la cruda: asi el bulto barre la fila en lugar de teletransportarse
function Set-TileTargets {
    if (-not [bool]$script:Cfg['magnify']) { return }
    # fuera de la pildora el magnes no existe: sin esto, el ultimo paso del muelle del cursor al
    # salir re-magnificaba la fila y se quedaba agrandada para siempre con la pildora ya cerrada
    if (-not $script:PtrInside) { return }
    $tiles = $script:Tiles
    if ($null -eq $tiles -or $tiles.Count -ne $script:MagSpr.Count) { return }
    $vert  = $script:Cfg['position'] -in 'left', 'right'
    $pitch = Get-Pitch
    $off   = if ($vert) { [double]$script:StripScroll.VerticalOffset } else { [double]$script:StripScroll.HorizontalOffset }
    $inset = Get-RowInset
    $r     = $pitch * 2.4
    $best  = 1.0; $bestIx = -1
    for ($i = 0; $i -lt $tiles.Count; $i++) {
        $centro = $inset + ($i + 0.5) * $pitch - $off
        $s = Get-MagScale ($script:PtrLive - $centro) $r
        # el tile arrastrado ya no se agranda: ahora el que flota junto al cursor es el fantasma y la
        # fila tiene que leerse como un hueco libre, no como un icono que compite con el que se lleva
        if ($tiles[$i] -eq $script:DragBtn) { $s = 0.86 }
        elseif ($tiles[$i] -eq $script:PressBtn) { $s = $s * 0.9 }
        Move-Spring $script:MagSpr[$i] $s
        if ($s -gt $best) { $best = $s; $bestIx = $i }
    }
    # el del centro va por delante: si no, el vecino agrandado le pisa un trozo
    if ($bestIx -ne $script:MagTop) {
        if ($script:MagTop -ge 0 -and $script:MagTop -lt $tiles.Count) {
            [System.Windows.Controls.Panel]::SetZIndex($tiles[$script:MagTop], 0)
        }
        if ($bestIx -ge 0) { [System.Windows.Controls.Panel]::SetZIndex($tiles[$bestIx], 10) }
        $script:MagTop = $bestIx
    }
}

# magnes tipo mac: el icono bajo el cursor estira al maximo y los de al lado una fraccion
# proporcional a su distancia. Solo RenderTransform, nada de layout; lo que se escribe aqui son
# objetivos del muelle, nunca la escala final, que es lo que quita el efecto "telémetro".
function Update-Proximity([System.Windows.Point]$p) {
    $vert = $script:Cfg['position'] -in 'left', 'right'
    $veniaDeFuera = $script:PtrU -lt -1e8
    $script:PtrInside = $true
    $script:PtrU = if ($vert) { [double]$p.Y } else { [double]$p.X }
    if ($veniaDeFuera) {
        # primera pasada despues de entrar: el bulto nace bajo el cursor, no barre desde la posicion
        # que tenia cuando salio la ultima vez
        $script:PtrLive = [double]$script:PtrU
        $script:PtrSpr.x = $script:PtrLive; $script:PtrSpr.t = $script:PtrLive; $script:PtrSpr.v = 0.0
        # y nace ya aplicado: el muelle del cursor esta en su sitio, asi que no va a moverse y sin
        # esta pasada los iconos se quedarian pequenos hasta que el raton volviera a moverse, con la
        # pildora ya hinchada (vidrio de sobra y los tiles chicos dentro)
        Set-TileTargets
        Start-Anim
    }
    # los parentesis NO son de adorno: en modo argumento PowerShell no interpreta "[double]$x" como un
    # cast sino como dos valores (el tipo System.Double y la variable), el parametro [double]$to no
    # acepta un Type y la llamada muere antes de entrar en la funcion. El muelle del cursor se quedaba
    # por tanto sin objetivo: ni magnes ni bulto en la pildora.
    Move-Spring $script:PtrSpr ([double]$script:PtrU)
}

function Reset-Proximity {
    $script:MagTop = -1
    $script:PtrU = -1e9
    $script:PtrInside = $false
    # el cursor se queda donde estaba y solo sueltan los muelles: la pildora se deshincha en su sitio
    # en vez de desplazar el bulto al extremo de la fila
    foreach ($sp in $script:MagSpr) { Move-Spring $sp 1.0 }
    if ($null -ne $script:Tiles) {
        foreach ($t in $script:Tiles) { [System.Windows.Controls.Panel]::SetZIndex($t, 0) }
    }
}

function Set-PillHover([bool]$on) {
    Move-Spring $script:HoverSpr $(if ($on) { 1.0 } else { 0.0 })
}

function Set-Magnify([bool]$on) {
    $script:Cfg['magnify'] = $on
    Write-Config
    Reset-Proximity
    Apply-Layout
}

# La ventana mide lo que mide la fila de iconos + margen para la sombra y el agrandado al pasar el raton.
function Fit-Pill {
    $wa = [System.Windows.SystemParameters]::WorkArea
    $m  = [int]$script:Margin
    $pad = Get-PillPadding
    $vert = $script:Cfg['position'] -in 'left', 'right'
    $script:Window.UpdateLayout()
    $d = $script:Strip.DesiredSize
    # el eje cruzado no puede salir del DesiredSize: el ScrollViewer lo mide contra el viewport que
    # estamos calculando (circular). Lo manda la caja del tile, que es lo que tiene que caber.
    $box = Get-TileBox
    # el largo del eje principal lo manda la cantidad de iconos, no el DesiredSize: al quitar o mover
    # un tile la medida vieja sigue ahi hasta el siguiente arrange y la pildora no se encoge. El pitch
    # es exacto porque cada boton lleva el halo como padding y el estilo no le anade margen.
    $n = @($script:Tiles).Count
    if ($n -gt 0) {
        $mainLen = [double]$n * (Get-Pitch)
    } elseif ($vert) {
        $mainLen = [double]$d.Height
    } else {
        $mainLen = [double]$d.Width
    }

    if ($vert) {
        $maxH = $wa.Height - 2 * $m - 30
        $script:StripScroll.MaxHeight = [Math]::Max(40, $maxH)
        $script:StripScroll.MaxWidth  = 6000
        $pillW = $box + 2 * $pad.v
        $pillH = [Math]::Min($mainLen, $maxH) + 2 * (Get-RowInset)
    } else {
        $maxW = $wa.Width - 2 * $m - 30
        $script:StripScroll.MaxWidth  = [Math]::Max(80, $maxW)
        $script:StripScroll.MaxHeight = 6000
        $pillW = [Math]::Min($mainLen, $maxW) + 2 * (Get-RowInset)
        $pillH = $box + 2 * $pad.v
    }

    $script:Window.Width  = [int][Math]::Ceiling($pillW + 2 * $m)
    $script:Window.Height = [int][Math]::Ceiling($pillH + 2 * $m)
    Place-Dock
}

function Apply-Layout {
    $sz = [int]$script:Cfg['iconSize']
    $vert = $script:Cfg['position'] -in 'left', 'right'

    $orient = if ($vert) { 'Vertical' } else { 'Horizontal' }
    $script:Bar.Orientation = $orient
    $script:Strip.Orientation = $orient
    $script:SearchBox.Width = if ($vert) { $sz + 140 } else { 190 }

    if ($vert) {
        $script:StripScroll.HorizontalScrollBarVisibility = 'Disabled'
        $script:StripScroll.VerticalScrollBarVisibility = 'Hidden'
    } else {
        $script:StripScroll.HorizontalScrollBarVisibility = 'Hidden'
        $script:StripScroll.VerticalScrollBarVisibility = 'Disabled'
    }

    Fit-Pill
    # la fila entra con su hueco a lo largo de la fila y con la banda transversal para el magnes
    $script:RowShift = 0.0
    Set-BarMargins
    # al cambiar de lado el transform vive en otro eje (X pasa a Y): sin reiniciarlo la fila se queda
    # desplazada con el valor de la posicion anterior
    Set-RowShift 0.0
    Refresh-Pill
    Set-WindowOpacity ([double]$script:Cfg['idleOpacity'] / 100.0) 0
}

# la pildora se dibuja con la medida real de la rejilla: hay que forzar el layout antes de calcularla
function Refresh-Pill {
    $script:ShapeSig = ''
    $script:Window.UpdateLayout()
    Update-PillShape
}

function Place-Dock {
    $wa = [System.Windows.SystemParameters]::WorkArea
    $m  = [int]$script:Margin
    $w = [double]$script:Window.Width; $h = [double]$script:Window.Height
    switch ($script:Cfg['position']) {
        'top'   { $l = $wa.X + ($wa.Width - $w) / 2; $t = $wa.Y - $m + 6 }
        'left'  { $l = $wa.X - $m + 6;               $t = $wa.Y + ($wa.Height - $h) / 2 }
        'right' { $l = $wa.Right - $w + $m - 6;      $t = $wa.Y + ($wa.Height - $h) / 2 }
        'free'  {
            if ($null -ne $script:Cfg['left'] -and $null -ne $script:Cfg['top']) {
                $l = [double]$script:Cfg['left'] - $m; $t = [double]$script:Cfg['top'] - $m
            } else { $script:Cfg['position'] = 'bottom'; Place-Dock; return }
        }
        default { $l = $wa.X + ($wa.Width - $w) / 2; $t = $wa.Bottom - $h + $m - 6 }
    }
    Set-DockPosition $l $t
}

function Set-DockPosition([double]$l, [double]$t) {
    $script:Window.Left = $l; $script:Window.Top = $t
}

# nivel de escritorio: ventana sin activacion (los clics no la suben) + colocada al fondo del
# z-order, por encima del fondo y por debajo de cualquier aplicacion
function Send-ToBottom {
    try {
        $hwnd = (New-Object System.Windows.Interop.WindowInteropHelper($script:Window)).Handle
        if ($hwnd -eq [IntPtr]::Zero) { return }
        # SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE
        $null = [GD32]::SetWindowPos($hwnd, [IntPtr]1, 0, 0, 0, 0, 0x0001 -bor 0x0002 -bor 0x0010)
    } catch { }
}

function Set-DesktopLevel([bool]$on) {
    try {
        $hwnd = (New-Object System.Windows.Interop.WindowInteropHelper($script:Window)).EnsureHandle()
        if ($hwnd -eq [IntPtr]::Zero) { return }
        $script:DesktopLevel = $on
        $ex = [GD32]::GetWindowLong($hwnd, -20)   # GWL_EXSTYLE
        if ($on) { $null = [GD32]::SetWindowLong($hwnd, -20, $ex -bor 0x08000000) }   # WS_EX_NOACTIVATE
        else     { $null = [GD32]::SetWindowLong($hwnd, -20, $ex -band (-bnot 0x08000000)) }
        if ($on) { Send-ToBottom }
        Write-Log "modo escritorio $(if ($on) { 'activado' } else { 'desactivado' }) (hwnd=$hwnd)"
    } catch { Write-Log "fallo al aplicar el modo escritorio: $($_.Exception.Message)" }
}

function Attach-ToDesktop { Set-DesktopLevel ([bool]$script:Cfg['desktopLevel']) }

function Set-WindowOpacity([double]$to, [int]$ms) {
    if ($ms -le 0) { $script:Window.Opacity = $to; return }
    $a = New-Object System.Windows.Media.Animation.DoubleAnimation
    $a.To = $to
    $a.Duration = New-Object System.Windows.Duration([TimeSpan]::FromMilliseconds($ms))
    $a.EasingFunction = New-Object System.Windows.Media.Animation.CubicEase
    $script:Window.BeginAnimation([System.Windows.Window]::OpacityProperty, $a)
}

# ------------------------------------------------------------------ acciones
function Start-Entry($it, [string]$via) {
    if (-not $it) { return }
    Write-Log "Lanzando ($via): $($it.Name)"
    try {
        if ($script:Cfg['minimizeAll']) {
            try { (New-Object -ComObject Shell.Application).MinimizeAll() } catch { }
        }
        if ($it.Origin -eq 'steam' -and $it.AppId) {
            $null = Start-Process -FilePath ("steam://rungameid/{0}" -f $it.AppId)
        } else {
            $file = if ($it.Link -and (Test-Path -LiteralPath $it.Link)) { $it.Link } else { $it.Target }
            if (-not $file -or -not (Test-Path -LiteralPath $file)) { throw "No existe el destino: $file" }
            $wd = if ($it.Work -and (Test-Path -LiteralPath $it.Work)) { $it.Work } else { $null }
            if ($it.Arguments) {
                $p = Start-Process -FilePath $file -ArgumentList $it.Arguments -WorkingDirectory $wd -PassThru
            } else {
                $p = Start-Process -FilePath $file -WorkingDirectory $wd -PassThru
            }
            Watch-Process $p
        }
        Register-Use $it
        if ($script:Cfg['hideOnLaunch']) { Hide-Dock }
    } catch {
        Write-Log "ERROR al lanzar $($it.Name): $($_.Exception.Message)"
        [System.Windows.MessageBox]::Show("No se pudo lanzar:`n$($it.Name)`n`n$($_.Exception.Message)",
            'GamesDock', 'OK', 'Error') | Out-Null
        Show-Dock
    }
}

function Watch-Process($p) {
    try {
        if (-not $p -or $p.HasExited) { return }
        $p.EnableRaisingEvents = $true
        $p.add_Exited({
            $script:Window.Dispatcher.BeginInvoke([System.Action] {
                if ($script:Hidden) { Show-Dock }
            }) | Out-Null
        })
    } catch { }
}

function Register-Use($it) {
    $k = [string]$it.Key
    $u = $script:Usage[$k]
    if ($u) { $u.n = [int]$u.n + 1; $u.d = (Get-Date).ToString('o') }
    else { $script:Usage[$k] = @{ n = 1; d = (Get-Date).ToString('o') } }
    $it.Fav = [bool]($script:Pins[$k] -and $script:Pins[$k].fav)
    Write-Config
}

function Ensure-Pin($it) {
    $k = [string]$it.Key
    $p = $script:Pins[$k]
    if (-not $p) {
        $p = [pscustomobject]@{
            key = $k; name = $it.Name; fav = $false; hidden = $false
            link = $it.Link; target = $it.Target; args = $it.Arguments; work = $it.Work
            origin = $it.Origin; kind = $it.Kind; icon = $it.Icon; added = (Get-Date).ToString('o')
        }
        $script:Pins[$k] = $p
    }
    return $p
}

function Toggle-Fav($it) {
    $p = Ensure-Pin $it
    Set-PinOverride $it @{ fav = -not [bool]$p.fav }
    $it.Fav = [bool]$script:Pins[[string]$p.key].fav
    Write-Config
    Rebuild-Strip
    if ($it.Fav) { Set-Section 'fav' }
}

function Hide-FromDock($it) {
    $null = Ensure-Pin $it
    Set-PinOverride $it @{ hidden = $true; fav = $false }
    Write-Config
    Rebuild-Strip
}

function Add-PinFromPath([string]$Path, [string]$NameHint) {
    try {
        $link = $null; $target = $null; $argLine = $null; $work = $null; $hint = $null; $origin = 'custom'
        if ($Path -match '^\w+://' -or $Path -match '^steam:') {
            $target = $Path; $key = "uri:$($Path.ToLowerInvariant())"; $name = if ($NameHint) { $NameHint } else { Split-Path -Leaf $Path }
        } elseif ((Get-Item -LiteralPath $Path -ErrorAction SilentlyContinue).Extension -eq '.lnk') {
            $link = $Path
            $s = Resolve-Link $Path
            if (-not $s -or -not $s.Target) { throw 'El acceso directo no tiene destino.' }
            $target = $s.Target; $argLine = $s.Args; $work = $s.Work; $hint = $s.Target
            $key = ($target.ToLowerInvariant() + '|' + ($argLine -replace '\s+', ' ').Trim().ToLowerInvariant())
            $name = if ($NameHint) { $NameHint } else { Clean-Name (Split-Path -Leaf $Path) }
            $origin = if (Test-IsGameDir $target) { 'custom' } else { 'custom' }
        } else {
            $target = $Path; $work = (Split-Path -Parent $Path); $hint = $Path
            $key = ($Path.ToLowerInvariant() + '|')
            $name = if ($NameHint) { $NameHint } else { Clean-Name (Split-Path -Leaf $Path) }
        }
        if ($script:Pins.ContainsKey($key) -and [bool]$script:Pins[$key].fav) {
            Write-Log "ya fijado: $name"; return $null
        }
        $kind = if ($target -match '^steam:' -or (Test-IsGameDir $target)) { 'game' } else { 'app' }
        $pin = [pscustomobject]@{
            key = $key; name = $name; fav = $true; hidden = $false
            link = $link; target = $target; args = $argLine; work = $work
            origin = $origin; kind = $kind; icon = $hint; added = (Get-Date).ToString('o')
        }
        $script:Pins[$key] = $pin
        if (-not $script:Batch) { Build-Index; Write-Config; Rebuild-Strip }
        Write-Log "fijado: $name ($key)"
        return $pin
    } catch {
        Write-Log "fijar fallida ${Path}: $($_.Exception.Message)"
        if ($script:Batch) { $script:BatchFail += $Path }
        else {
            [System.Windows.MessageBox]::Show("No se pudo anadir:`n$Path`n`n$($_.Exception.Message)", 'GamesDock', 'OK', 'Error') | Out-Null
        }
        return $null
    }
}

# que se puede soltar encima de la barra: ficheros arrastrados (acceso .lnk, .exe, .url, carpeta),
# la direccion de una pestana del navegador y texto que sea una ruta o un enlace de verdad.
# Los formatos van por nombre crudo: DataFormats (WPF y WinForms) no tiene clave Uri.
function Get-DropPaths($data) {
    if ($null -eq $data) { return @() }
    foreach ($f in 'FileDrop', 'UniformResourceLocatorW', 'UniformResourceLocator') {
        if ($data.GetDataPresent($f)) {
            $v = $data.GetData($f)
            if ($v -isnot [string]) { return @($v | ForEach-Object { [string]$_ }) }
            return @([string]$v)
        }
    }
    foreach ($f in 'Text', 'UnicodeText') {
        if (-not $data.GetDataPresent($f)) { continue }
        $txt = [string]$data.GetData($f)
        $ok = @($txt -split "\r?\n" | Where-Object {
            $t = $_.Trim(); $t -and ($t -match '^\w+://' -or (Test-Path -LiteralPath $t))
        } | ForEach-Object { [string]$_ })
        if ($ok.Count -gt 0) { return $ok }
    }
    return @()
}

# toda la tanda de un golpe: un reescaneado, un guardado de config y un solo aviso de error
function Add-PinsBatch([string[]]$Rutas) {
    if ($null -eq $Rutas -or $Rutas.Count -eq 0) { return }
    $script:Batch = $true; $script:BatchFail = @()
    try {
        foreach ($f in $Rutas) { $null = Add-PinFromPath ([string]$f).Trim() $null }
    } finally {
        $script:Batch = $false
        Build-Index
        Write-Config
        Rebuild-Strip
    }
    if ($script:BatchFail.Count -gt 0) {
        $cabeza = if ($script:BatchFail.Count -eq 1) { 'No se pudo anadir:' } else { 'No se pudieron anadir:' }
        [System.Windows.MessageBox]::Show("$cabeza`n$($script:BatchFail -join "`n")", 'GamesDock', 'OK', 'Error') | Out-Null
    }
    $script:BatchFail = @()
}

function Show-AddDialog {
    $dlg = New-Object Microsoft.Win32.OpenFileDialog
    $dlg.Title = 'Elige el juego o aplicacion'
    $dlg.Filter = 'Programas y accesos|*.exe;*.lnk;*.bat;*.cmd;*.url|Todos|*.*'
    $dlg.CheckFileExists = $true
    $dlg.Multiselect = $true
    if ($dlg.ShowDialog($script:Window)) {
        Add-PinsBatch @($dlg.FileNames)
    }
}

function Show-ItemMenu($it, $owner) {
    $m = New-Object System.Windows.Controls.ContextMenu
    $acciones = @(
        @{ h = $(if ($it.Fav) { 'Quitar de favoritos' } else { 'Fijar en favoritos' }); a = { Toggle-Fav $this.Tag } },
        @{ h = 'Lanzar ahora'; a = { Start-Entry $this.Tag 'menu' } },
        @{ h = 'Abrir carpeta del juego'; a = {
                $t = [string]$this.Tag.Target
                if ($t -and $t -notmatch '^\w+://' -and (Test-Path -LiteralPath $t)) {
                    try { Start-Process explorer.exe -ArgumentList "/select,`"$t`"" } catch { }
                } else {
                    [System.Windows.MessageBox]::Show('Ese juego se lanza por el cliente (Steam/Epic) y no tiene carpeta directa.', 'GamesDock') | Out-Null
                }
            } },
        @{ h = 'Renombrar'; a = {
                $n = Read-NamePrompt $this.Tag.Name
                if ($n -and $n -ne $this.Tag.Name) {
                    Set-PinOverride $this.Tag @{ name = $n }
                    $this.Tag.Name = $n
                    Write-Config; Rebuild-Strip
                }
            } },
        @{ h = $(if ($it.Origin -eq 'custom') { 'Quitar del dock' } else { 'Ocultar del dock' }); a = { Hide-FromDock $this.Tag } }
    )
    $idx = 0
    foreach ($a in $acciones) {
        if ($idx -eq 4) { $null = $m.Items.Add((New-MenuSep)) }   # la accion destructiva, aparte
        $item = New-Object System.Windows.Controls.MenuItem
        $item.Header = $a.h
        $item.Tag = $it
        $item.Add_Click($a.a)
        $null = $m.Items.Add($item)
        $idx++
    }
    $m.PlacementTarget = $owner
    $m.IsOpen = $true
}

function Set-PinOverride($it, [hashtable]$cambios) {
    $p = Ensure-Pin $it
    $nuevo = @{}
    foreach ($k in 'key', 'name', 'fav', 'hidden', 'link', 'target', 'args', 'work', 'origin', 'kind', 'icon', 'added') {
        $nuevo[$k] = $p.$k
    }
    foreach ($k in $cambios.Keys) { $nuevo[$k] = $cambios[$k] }
    $script:Pins[[string]$p.key] = [pscustomobject]$nuevo
}

function Read-NamePrompt([string]$current) {
    $par = New-GlassWindow 'Nombre en el dock' 400 'Enter para guardar - Esc para cancelar'
    $w = $par.win
    $script:PromptValue = $null

    $etq = New-TextLabel 'Nombre visible' 11 'PanelSub'
    $etq.Margin = New-Object System.Windows.Thickness(0, 0, 0, 6)
    $null = $par.body.Children.Add($etq)

    $box = New-Object System.Windows.Controls.TextBox
    $box.Style = Get-Style 'GlassText'
    $box.Text = $current
    $box.Margin = New-Object System.Windows.Thickness(0, 0, 0, 14)
    $null = $par.body.Children.Add($box)

    $ok = New-Object System.Windows.Controls.Button
    $ok.Style = Get-Style 'PillBtn'
    $ok.Content = 'Guardar'
    $ok.HorizontalAlignment = [System.Windows.HorizontalAlignment]::Right
    $ok.Add_Click({
        $script:PromptValue = $script:PromptBox.Text
        try { ([System.Windows.Window]::GetWindow($this)).Close() } catch { }
    })
    $null = $par.body.Children.Add($ok)

    $script:PromptBox = $box
    $box.Add_KeyDown({
        if ($_.Key -eq 'Enter') {
            $script:PromptValue = $script:PromptBox.Text
            try { ([System.Windows.Window]::GetWindow($this)).Close() } catch { }
        }
    })
    $box.SelectAll()
    # el foco solo se puede pedir con la ventana ya creada
    $w.Add_ContentRendered({ $null = $script:PromptBox.Focus(); $script:PromptBox.SelectAll() })
    Play-GlassIn $w $w.Content
    $null = $w.ShowDialog()
    return $script:PromptValue
}

# entrada y salida: el dock se desliza desde el borde que lo ancla y se enciende con un fundido
# corto, mientras los iconos escalonan su pop (Rebuild-Strip -Pop). Al ocultarse hace la inversa y
# la ventana se apaga cuando el muelle aterriza, no de golpe.
function Get-SlideOut {
    $d = 22.0
    switch ([string]$script:Cfg['position']) {
        'top'   { return @{ axis = 'y'; from = (-1.0 * $d) } }
        'left'  { return @{ axis = 'x'; from = (-1.0 * $d) } }
        'right' { return @{ axis = 'x'; from = $d } }
        default { return @{ axis = 'y'; from = $d } }
    }
}

function Play-Entry {
    $t = Get-SlideOut
    $tr = $script:RootGrid.RenderTransform
    if (-not ($tr -is [System.Windows.Media.TranslateTransform])) {
        $tr = New-Object System.Windows.Media.TranslateTransform
        $script:RootGrid.RenderTransform = $tr
    }
    if ($t.axis -eq 'x') { $tr.X = $t.from; $tr.Y = 0.0 } else { $tr.Y = $t.from; $tr.X = 0.0 }
    $script:RootGrid.Opacity = 0.0
    $script:SlideSpr.o = $t.axis
    $script:SlideSpr.x = $t.from;  $script:SlideSpr.v = 0.0; $script:SlideSpr.t = 0.0
    $script:FadeSpr.x = 0.0;       $script:FadeSpr.v = 0.0;  $script:FadeSpr.t = 1.0
    $script:SlideSpr.done = $null
    Start-Anim
}

function Show-Dock {
    $script:Hidden = $false
    $script:Window.Visibility = 'Visible'
    if ($script:DesktopLevel) { Send-ToBottom }
    Set-WindowOpacity ([double]$script:Cfg['idleOpacity'] / 100.0) 160
    Play-Entry
}

function Hide-Dock {
    if ([bool]$script:Hidden) { return }
    $script:Hidden = $true
    $t = Get-SlideOut
    $script:SlideSpr.o = $t.axis
    $script:SlideSpr.t = $t.from
    $script:FadeSpr.t = 0.0
    $script:SlideSpr.done = { if ([bool]$script:Hidden) { $script:Window.Visibility = 'Hidden' } }
    Start-Anim
}

function Toggle-Dock {
    if ($script:Hidden -or $script:Window.Visibility -ne 'Visible') { Show-Dock } else { Hide-Dock }
}

# ------------------------------------------------------------------ ajustes
# Panel de cristal sin marco: borde redondeado, sombra propia y entrada animada. Una Window con
# WindowStyle=None + AllowsTransparency de verdad deja ver el escritorio de atras, asi que el
# margen del borde exterior es el sitio donde cabe la sombra.
$script:Steppers    = @{}
$script:SettingsWin = $null

function Get-Style([string]$Key) { return [System.Windows.Style] $script:App.Resources[$Key] }

function Bind-Res($obj, [System.Windows.DependencyProperty]$Prop, [string]$Key) {
    $null = $obj.SetResourceReference($Prop, $Key)
    return $obj
}

function New-TextLabel([string]$Txt, [double]$Size, [string]$ResKey) {
    $t = New-Object System.Windows.Controls.TextBlock
    $t.Text = $Txt; $t.FontSize = $Size
    $t.TextOptions.TextFormattingMode = [System.Windows.Media.TextFormattingMode]::Display
    $null = Bind-Res $t ([System.Windows.Controls.TextBlock]::ForegroundProperty) $ResKey
    return $t
}

function New-SecHead([string]$Txt) {
    $row = New-Object System.Windows.Controls.StackPanel
    $row.Orientation = 'Horizontal'
    $row.Margin = New-Object System.Windows.Thickness(0, 0, 0, 11)
    $bar = New-Object System.Windows.Controls.Border
    $bar.Width = 3; $bar.Margin = New-Object System.Windows.Thickness(0, 2, 7, 1)
    $bar.CornerRadius = New-Object System.Windows.CornerRadius(2)
    $null = Bind-Res $bar ([System.Windows.Controls.Border]::BackgroundProperty) 'MenuAccent'
    $t = New-TextLabel $Txt.ToUpperInvariant() 10.5 'PanelSub'
    $t.FontWeight = [System.Windows.FontWeights]::SemiBold
    $null = $row.Children.Add($bar); $null = $row.Children.Add($t)
    return $row
}

function New-Field($Label, $ctrl, [double]$Gap = 13) {
    $sp = New-Object System.Windows.Controls.StackPanel
    $sp.Margin = New-Object System.Windows.Thickness(0, 0, 0, $Gap)
    if ($Label) {
        $t = New-TextLabel $Label 11 'PanelSub'
        $t.Margin = New-Object System.Windows.Thickness(0, 0, 0, 6)
        $null = $sp.Children.Add($t)
    }
    $null = $sp.Children.Add($ctrl)
    return $sp
}

function New-Chip([string]$Txt, [string]$Group, [bool]$On, [scriptblock]$Pick) {
    $rb = New-Object System.Windows.Controls.RadioButton
    $rb.Style = Get-Style 'ChipRadio'
    $rb.Content = $Txt; $rb.GroupName = $Group
    $rb.Margin = New-Object System.Windows.Thickness(0, 0, 6, 6)
    $rb.IsChecked = $On                       # antes de enganchar el evento: si no, arranca pulsando
    $rb.Add_Checked($Pick)
    return $rb
}

function New-Switch([string]$Txt, [bool]$On, [scriptblock]$Changed) {
    $cb = New-Object System.Windows.Controls.CheckBox
    $cb.Style = Get-Style 'SwitchBox'
    $cb.Content = $Txt
    $cb.Margin = New-Object System.Windows.Thickness(0, 0, 0, 9)
    $cb.IsChecked = $On
    $cb.Add_Checked($Changed); $cb.Add_Unchecked($Changed)
    return $cb
}

function New-ThemeChip([string]$Id, [bool]$On) {
    $t = $script:Themes[$Id]
    $rb = New-Object System.Windows.Controls.RadioButton
    $rb.Style = Get-Style 'ThemeChip'
    $rb.GroupName = 'tema'; $rb.Tag = $Id
    $rb.Margin = New-Object System.Windows.Thickness(0, 0, 11, 13)
    $rb.IsChecked = $On
    # el chip va sin texto accesible (el nombre va debajo, en otro bloque): sin esto es un boton
    # vacio para un lector de pantalla y para cualquier prueba automatizada
    $rb.SetValue([System.Windows.Automation.AutomationProperties]::NameProperty, [string]$t.label)

    $sp = New-Object System.Windows.Controls.StackPanel
    $sw = New-Object System.Windows.Controls.Border
    $sw.Width = 50; $sw.Height = 31
    $sw.CornerRadius = New-Object System.Windows.CornerRadius(9)
    $sw.Background = New-Grad @($t.glass, $t.glassBot)
    $sw.BorderBrush = Br $t.edge; $sw.BorderThickness = New-Object System.Windows.Thickness(1)

    $dots = New-Object System.Windows.Controls.StackPanel
    $dots.Orientation = 'Horizontal'
    $dots.HorizontalAlignment = [System.Windows.HorizontalAlignment]::Center
    $dots.VerticalAlignment = [System.Windows.VerticalAlignment]::Center
    foreach ($d in @('a', 'b', 'c')) {
        $el = New-Object System.Windows.Shapes.Ellipse
        $el.Width = if ($d -eq 'b') { 9 } else { 6 }
        $el.Height = $el.Width
        $el.Fill = Br $t.accent
        $el.Margin = New-Object System.Windows.Thickness(2, 0, 2, 0)
        $el.VerticalAlignment = [System.Windows.VerticalAlignment]::Center
        $null = $dots.Children.Add($el)
    }
    $sw.Child = $dots
    $null = $sp.Children.Add($sw)

    $lb = New-TextLabel $t.label 10 'PanelSub'
    $lb.Margin = New-Object System.Windows.Thickness(0, 4, 0, 0)
    $lb.HorizontalAlignment = [System.Windows.HorizontalAlignment]::Center
    $null = $sp.Children.Add($lb)

    $rb.Content = $sp
    $rb.Add_Checked({ Set-Theme ([string]$this.Tag) })
    return $rb
}

function Move-Step([string]$Id, [int]$dir) {
    $s = $script:Steppers[$Id]
    if (-not $s) { return }
    $s.v = [Math]::Max($s.min, [Math]::Min($s.max, $s.v + $dir * $s.step))
    $s.lbl.Text = "$($s.v) $($s.suffix)"
    $f = 0.0
    if ($s.max -gt $s.min) { $f = [double]($s.v - $s.min) / [double]($s.max - $s.min) }
    $s.fill.Width = [Math]::Max(8.0, $f * $s.barw)
    if ($s.onset) { & $s.onset $s.v }
}

function New-Stepper([string]$Id, [int]$Min, [int]$Max, [int]$Step, [int]$Value, [string]$Suffix, [scriptblock]$OnSet) {
    $wrap = New-Object System.Windows.Controls.StackPanel
    $row  = New-Object System.Windows.Controls.StackPanel
    $row.Orientation = 'Horizontal'
    $menos = New-Object System.Windows.Controls.Button
    $menos.Style = Get-Style 'StepBtn'
    $menos.Content = [string][char]0x2212
    $menos.Tag = $Id
    $lbl = New-TextLabel "$Value $Suffix" 12 'PanelFg'
    $lbl.Width = 60
    $lbl.TextAlignment = [System.Windows.TextAlignment]::Center
    $lbl.VerticalAlignment = [System.Windows.VerticalAlignment]::Center
    $lbl.Margin = New-Object System.Windows.Thickness(2, 0, 2, 0)
    $mas = New-Object System.Windows.Controls.Button
    $mas.Style = Get-Style 'StepBtn'
    $mas.Content = '+'
    $mas.Tag = $Id
    $menos.Add_Click({ Move-Step ([string]$this.Tag) -1 })
    $mas.Add_Click({ Move-Step ([string]$this.Tag) 1 })
    $null = $row.Children.Add($menos); $null = $row.Children.Add($lbl); $null = $row.Children.Add($mas)

    $barw = 130.0
    $track = New-Object System.Windows.Controls.Border
    $track.Height = 3; $track.Width = $barw
    $track.CornerRadius = New-Object System.Windows.CornerRadius(2)
    $track.Margin = New-Object System.Windows.Thickness(0, 5, 0, 0)
    $track.HorizontalAlignment = [System.Windows.HorizontalAlignment]::Left
    $null = Bind-Res $track ([System.Windows.Controls.Border]::BackgroundProperty) 'SwitchOff'
    $fill = New-Object System.Windows.Controls.Border
    $fill.Height = 3; $fill.CornerRadius = New-Object System.Windows.CornerRadius(2)
    $fill.HorizontalAlignment = [System.Windows.HorizontalAlignment]::Left
    $null = Bind-Res $fill ([System.Windows.Controls.Border]::BackgroundProperty) 'MenuAccent'
    $f = 0.0
    if ($Max -gt $Min) { $f = [double]($Value - $Min) / [double]($Max - $Min) }
    $fill.Width = [Math]::Max(8.0, $f * $barw)
    $grid = New-Object System.Windows.Controls.Grid
    $null = $grid.Children.Add($track); $null = $grid.Children.Add($fill)

    $null = $wrap.Children.Add($row); $null = $wrap.Children.Add($grid)
    $script:Steppers[$Id] = @{
        min = $Min; max = $Max; step = $Step; v = $Value; suffix = $Suffix
        lbl = $lbl; onset = $OnSet; fill = $fill; barw = $barw
    }
    return $wrap
}

function New-GlassWindow([string]$TitleTxt, [double]$Width, [string]$SubTxt = 'clic derecho en la barra vacia - Esc para cerrar') {
    $w = New-Object System.Windows.Window
    $w.WindowStyle = [System.Windows.WindowStyle]::None
    $w.AllowsTransparency = $true
    $w.ResizeMode = [System.Windows.ResizeMode]::NoResize
    $w.ShowInTaskbar = $false
    $w.SizeToContent = [System.Windows.SizeToContent]::Height
    $w.Width = $Width
    $w.WindowStartupLocation = [System.Windows.WindowStartupLocation]::CenterScreen
    $w.Title = $TitleTxt
    $w.Background = [System.Windows.Media.Brushes]::Transparent
    $w.UseLayoutRounding = $true
    $w.ShowActivated = $true
    $w.Add_KeyUp({ param($s, $e) if ($e.Key -eq 'Escape') { try { $s.Close() } catch { } } })

    $shell = New-Object System.Windows.Controls.Border
    $shell.Margin = New-Object System.Windows.Thickness(14)
    $shell.CornerRadius = New-Object System.Windows.CornerRadius(18)
    $shell.BorderThickness = New-Object System.Windows.Thickness(1)
    $null = Bind-Res $shell ([System.Windows.Controls.Border]::BackgroundProperty) 'PanelBg'
    $null = Bind-Res $shell ([System.Windows.Controls.Border]::BorderBrushProperty) 'PanelEdge'
    $som = New-Object System.Windows.Media.Effects.DropShadowEffect
    $som.Color = [System.Windows.Media.Color]::FromArgb(255, 3, 5, 10)
    $som.BlurRadius = 34; $som.ShadowDepth = 12; $som.Opacity = 0.55; $som.Direction = 270
    $shell.Effect = $som

    $body = New-Object System.Windows.Controls.StackPanel
    $body.Margin = New-Object System.Windows.Thickness(20, 16, 20, 18)

    $hdr = New-Object System.Windows.Controls.Grid
    $hdr.Margin = New-Object System.Windows.Thickness(0, 0, 0, 14)
    $c1 = New-Object System.Windows.Controls.ColumnDefinition
    $c1.Width = New-Object System.Windows.GridLength(1, [System.Windows.GridUnitType]::Star)
    $c2 = New-Object System.Windows.Controls.ColumnDefinition
    $c2.Width = New-Object System.Windows.GridLength([System.Windows.GridUnitType]::Auto)
    $null = $hdr.ColumnDefinitions.Add($c1); $null = $hdr.ColumnDefinitions.Add($c2)
    $tit = New-TextLabel $TitleTxt 15 'PanelFg'
    $tit.FontWeight = [System.Windows.FontWeights]::SemiBold
    $tit.VerticalAlignment = [System.Windows.VerticalAlignment]::Center
    $sub = New-TextLabel $SubTxt 10.5 'PanelSub'
    $sub.Margin = New-Object System.Windows.Thickness(0, 3, 0, 0)
    $headsp = New-Object System.Windows.Controls.StackPanel
    $null = $headsp.Children.Add($tit); $null = $headsp.Children.Add($sub)
    [System.Windows.Controls.Grid]::SetColumn($headsp, 0)
    $x = New-Object System.Windows.Controls.Button
    $x.Style = Get-Style 'GhostBtn'
    $x.Content = [string][char]0x2715
    $x.VerticalAlignment = [System.Windows.VerticalAlignment]::Center
    [System.Windows.Controls.Grid]::SetColumn($x, 1)
    $x.Add_Click({ try { ([System.Windows.Window]::GetWindow($this)).Close() } catch { } })
    $null = $hdr.Children.Add($headsp); $null = $hdr.Children.Add($x)
    # la cabecera sirve de barra de arrastre, salvo cuando se pulsa un boton: si no, el cierre
    # tambien iniciaria un DragMove y se comeria el clic
    $hdr.Background = [System.Windows.Media.Brushes]::Transparent
    $hdr.Add_PreviewMouseLeftButtonDown({
        param($s, $e)
        $o = $e.OriginalSource
        while ($o) {
            if ($o -is [System.Windows.Controls.Primitives.ButtonBase]) { return }
            try { $o = [System.Windows.Media.VisualTreeHelper]::GetParent($o) } catch { break }
        }
        try { $null = ([System.Windows.Window]::GetWindow($s)).DragMove() } catch { }
    })
    $null = $body.Children.Add($hdr)

    $scroll = New-Object System.Windows.Controls.ScrollViewer
    # ScrollBarVisibility esta en Controls, no en Controls.Primitives: con el nombre mal PowerShell no
    # resuelve el tipo, el error no es terminante y el ScrollViewer se queda con su Visible por defecto
    # (barra gris de 17px robándole sitio al panel)
    $scroll.VerticalScrollBarVisibility = 'Hidden'
    $scroll.HorizontalScrollBarVisibility = 'Disabled'
    $scroll.Focusable = $false
    $scroll.MaxHeight = 640
    $scroll.Content = $body

    $shell.Child = $scroll
    $w.Content = $shell
    return @{ win = $w; body = $body }
}

function Bring-ToTop($w) {
    try {
        $h = (New-Object System.Windows.Interop.WindowInteropHelper($w)).Handle
        if ($h -eq [IntPtr]::Zero) { return }
        # HWND_TOP con SWP_NOMOVE|NOSIZE|NOACTIVATE: reordenar no pide permiso de primer plano, y el
        # proceso del dock vive en segundo plano (ShowDialog solo no saca la ventana de detras)
        $null = [GD32]::SetWindowPos($h, [IntPtr]0, 0, 0, 0, 0, 0x0001 -bor 0x0002 -bor 0x0010)
    } catch { }
}

function Play-GlassIn($w, $el) {
    # el estado de reposo se pone antes de mostrar; la animacion arranca en el primer pintado, si no
    # el storyboard corre sobre un elemento todavia sin ventana y el usuario lo ve ya terminado
    $rt = New-Object System.Windows.Media.ScaleTransform(0.955, 0.93)
    $el.RenderTransformOrigin = New-Object System.Windows.Point(0.5, 0.4)
    $el.RenderTransform = $rt
    $el.Opacity = 0.0
    $script:GlassIn = @{ win = $w; el = $el }
    # OJO: un scriptblock de evento lanzado por WPF NO ve las locales de la funcion que lo creo
    # (se ejecuta en el alcance global del script), por eso el par se saca de $script:
    $w.Add_ContentRendered({
        $g = $script:GlassIn
        if (-not $g) { return }
        Bring-ToTop $g.win
        $el = $g.el
        $st = New-Object System.Windows.Media.Animation.Storyboard
        foreach ($sp in @(
            @{ p = 'Opacity';                                            f = 0.0;   t = 1.0; ms = 170 },
            @{ p = '(UIElement.RenderTransform).(ScaleTransform.ScaleX)'; f = 0.955; t = 1.0; ms = 300 },
            @{ p = '(UIElement.RenderTransform).(ScaleTransform.ScaleY)'; f = 0.93;  t = 1.0; ms = 360 }
        )) {
            # DoubleAnimation esta en Media.Animation, no en Media: el nombre corto falla en silencio
            # y deja el panel con Opacity 0 (ventana ahi, pero invisible)
            $a = New-Object System.Windows.Media.Animation.DoubleAnimation
            $a.From = [double]$sp.f; $a.To = [double]$sp.t
            $a.Duration = [System.Windows.Duration]::new([TimeSpan]::FromMilliseconds([int]$sp.ms))
            $ease = New-Object System.Windows.Media.CubicEase
            $ease.EasingMode = [System.Windows.Media.EasingMode]::EaseOut
            $a.EasingFunction = $ease
            [System.Windows.Media.Animation.Storyboard]::SetTargetProperty($a, [System.Windows.PropertyPath]::new([string]$sp.p))
            [System.Windows.Media.Animation.Storyboard]::SetTarget($a, $el)
            $null = $st.Children.Add($a)
        }
        $null = $st.Begin($el, $true)
    })
}

function Show-Settings {
    if ($script:SettingsWin) {
        try { $null = $script:SettingsWin.Activate(); return } catch { $script:SettingsWin = $null }
    }
    $par = New-GlassWindow 'Ajustes de GamesDock' 790
    $w = $par.win
    $sp = New-Object System.Windows.Controls.StackPanel
    $sp.Orientation = 'Horizontal'
    $izq = New-Object System.Windows.Controls.StackPanel
    $izq.Width = 340
    $der = New-Object System.Windows.Controls.StackPanel
    $der.Margin = New-Object System.Windows.Thickness(22, 0, 0, 0)
    $der.Width = 340
    $null = $sp.Children.Add($izq); $null = $sp.Children.Add($der)
    $null = $par.body.Children.Add($sp)

    # --------------------------------------------------------- columna izquierda
    $null = $izq.Children.Add((New-SecHead 'Aspecto'))

    $posRow = New-Object System.Windows.Controls.WrapPanel
    foreach ($k in 'Abajo', 'Arriba', 'Izquierda', 'Derecha', 'Libre') {
        $null = $posRow.Children.Add((New-Chip $k 'posicion' ($script:PosMap[$k] -eq [string]$script:Cfg['position']) {
            $script:Cfg['position'] = $script:PosMap[[string]$this.Content]
            Write-Config; Apply-Layout
        }))
    }
    $null = $izq.Children.Add((New-Field 'Posicion de la barra' $posRow))

    $null = $izq.Children.Add((New-Field 'Tamano del icono' (New-Stepper 'iconSize' 28 76 2 ([int]$script:Cfg['iconSize']) 'px' {
        param($v)
        $script:Cfg['iconSize'] = $v
        Write-Config; Apply-Layout; Rebuild-Strip
    })))

    $null = $izq.Children.Add((New-Field 'Holgura alto/ancho' (New-Stepper 'pillPad' 2 26 2 ([int]$script:Cfg['pillPad']) 'px' {
        param($v)
        $script:Cfg['pillPad'] = $v
        Write-Config; Apply-Layout
    })))

    $null = $izq.Children.Add((New-Field 'Opacidad en reposo' (New-Stepper 'idleOpacity' 10 100 5 ([int]$script:Cfg['idleOpacity']) '%' {
        param($v)
        $script:Cfg['idleOpacity'] = $v
        Write-Config; Set-WindowOpacity ([double]$v / 100.0) 120
    })))

    $temRow = New-Object System.Windows.Controls.WrapPanel
    foreach ($k in $script:ThemeIds) {
        $null = $temRow.Children.Add((New-ThemeChip $k ([string]$script:Cfg['theme'] -eq [string]$k)))
    }
    $null = $izq.Children.Add((New-Field 'Tema de la barra' $temRow 4))

    # --------------------------------------------------------- columna derecha
    $null = $der.Children.Add((New-SecHead 'Que se ve'))
    $secRow = New-Object System.Windows.Controls.WrapPanel
    $secRow.Margin = New-Object System.Windows.Thickness(0, 0, 0, 15)
    $script:SecMap = [ordered]@{ 'Favoritos' = 'fav'; 'Juegos' = 'games'; 'Apps' = 'apps'; 'Todo' = 'all' }
    foreach ($k in $script:SecMap.Keys) {
        $null = $secRow.Children.Add((New-Chip $k 'seccion' ($script:SecMap[$k] -eq [string]$script:Cfg['section']) {
            Set-Section ([string]$script:SecMap[[string]$this.Content])
        }))
    }
    $null = $der.Children.Add($secRow)

    $null = $der.Children.Add((New-SecHead 'Comportamiento'))
    $null = $der.Children.Add((New-Switch 'Agrandar los iconos al pasar el raton (efecto Mac)' ([bool]$script:Cfg['magnify']) {
        Set-Magnify ([bool]$this.IsChecked)
    }))
    $null = $der.Children.Add((New-Switch 'Vivir solo en el escritorio (nunca por encima de otras ventanas)' ([bool]$script:Cfg['desktopLevel']) {
        $script:Cfg['desktopLevel'] = [bool]$this.IsChecked
        Write-Config; Attach-ToDesktop; Apply-Layout
    }))
    $null = $der.Children.Add((New-Switch 'Minimizar todo al lanzar' ([bool]$script:Cfg['minimizeAll']) {
        $script:Cfg['minimizeAll'] = [bool]$this.IsChecked; Write-Config
    }))
    $null = $der.Children.Add((New-Switch 'Ocultar el dock al lanzar (Ctrl+Alt+D lo trae)' ([bool]$script:Cfg['hideOnLaunch']) {
        $script:Cfg['hideOnLaunch'] = [bool]$this.IsChecked; Write-Config
    }))

    $null = $der.Children.Add((New-SecHead 'Sistema'))
    $sysRow = New-Object System.Windows.Controls.WrapPanel
    $b1 = New-Object System.Windows.Controls.Button
    $b1.Style = Get-Style 'PillBtn'
    $b1.Content = 'Reescanear juegos'
    $b1.Margin = New-Object System.Windows.Thickness(0, 0, 7, 7)
    $b1.Add_Click({
        Build-Index; Rebuild-Strip
        $this.Content = "Reescaneados: $($script:Items.Count)"
    })
    $null = $sysRow.Children.Add($b1)
    $b2 = New-Object System.Windows.Controls.Button
    $b2.Style = Get-Style 'PillBtn'
    $b2.Content = 'Vaciar escritorio al dock'
    $b2.Margin = New-Object System.Windows.Thickness(0, 0, 7, 7)
    $b2.Add_Click({
        Import-Desktop
        # los botones se guardan en alcance de script: dentro de un handler de evento WPF las
        # locales de Show-Settings ya no existen
        $script:BtnDevolver.Content = 'Todo devuelto'
    })
    $null = $sysRow.Children.Add($b2)
    $b3 = New-Object System.Windows.Controls.Button
    $b3.Style = Get-Style 'PillBtn'
    $b3.Content = 'Devolver al escritorio'
    $b3.Margin = New-Object System.Windows.Thickness(0, 0, 7, 7)
    $b3.Add_Click({
        Restore-Desktop
        $script:BtnVaciar.Content = 'Vaciar escritorio al dock'
    })
    $null = $sysRow.Children.Add($b3)
    $script:BtnVaciar   = $b2
    $script:BtnDevolver = $b3
    $b4 = New-Object System.Windows.Controls.Button
    $b4.Style = Get-Style 'PillBtn'
    $b4.Content = if ([bool]$script:Cfg['autostart']) { 'Arranca con Windows: si' } else { 'Arranca con Windows: no' }
    $b4.Margin = New-Object System.Windows.Thickness(0, 0, 7, 7)
    $b4.Add_Click({
        $script:Cfg['autostart'] = -not [bool]$script:Cfg['autostart']
        Write-Config
        if (Set-Autostart ([bool]$script:Cfg['autostart'])) {
            $this.Content = if ([bool]$script:Cfg['autostart']) { 'Arranca con Windows: si' } else { 'Arranca con Windows: no' }
        } else {
            $script:Cfg['autostart'] = $false; Write-Config
            $this.Content = 'Arranca con Windows: no'
        }
    })
    $null = $sysRow.Children.Add($b4)
    $b5 = New-Object System.Windows.Controls.Button
    $b5.Style = Get-Style 'PillBtn'
    $b5.Content = 'Abrir carpeta de ajustes'
    $b5.Margin = New-Object System.Windows.Thickness(0, 0, 7, 7)
    $b5.Add_Click({ try { Start-Process explorer.exe -ArgumentList "`"$script:Root`"" } catch { } })
    $null = $sysRow.Children.Add($b5)
    $null = $der.Children.Add($sysRow)

    $note = New-TextLabel ('Arrastra un .lnk, .exe o un enlace steam:// sobre la barra para fijarlo. ' +
        'Arrastra un icono para ordenar la barra; soltarlo fuera del cristal lo quita y vuelve con ' +
        '"Restaurar ocultos" del clic derecho. Clic derecho en un icono: fijar, renombrar u ocultar. ' +
        'Clic central: favorito.') 10.5 'PanelSub'
    $note.TextWrapping = [System.Windows.TextWrapping]::Wrap
    $note.Margin = New-Object System.Windows.Thickness(0, 6, 0, 0)
    $null = $der.Children.Add($note)

    $script:SettingsWin = $w
    $w.Add_Closed({ $script:SettingsWin = $null; Write-Config })
    Play-GlassIn $w $w.Content
    # ShowDialog y no Show: el proceso del dock esta en segundo plano y Windows no le deja traer una
    # ventana modal-less al frente; el dialogo modal si (y el bucle anidado del dispatcher no para
    # las animaciones de la barra).
    $null = $w.ShowDialog()
}

# ------------------------------------------------------------------ escritorio / arranque
function Import-Desktop {
    $r = [System.Windows.MessageBox]::Show(
        'Los accesos directos del escritorio se moveran a' + "`n$script:DeskDir`n" + 'Se mantienen intactos y puedes devolverlos con un boton.' + "`n`n`xbf继续?" -replace '\xbf继续?', 'Continuar?',
        'Vaciar escritorio', 'YesNo', 'Question')
    if ($r -ne 'Yes') { return }
    $movidos = 0
    foreach ($d in Get-DesktopDirs) {
        if ($d -eq $script:DeskDir -or -not (Test-Path -LiteralPath $d)) { continue }
        foreach ($f in (Get-ChildItem -LiteralPath $d -File -ErrorAction SilentlyContinue |
                    Where-Object { $_.Extension -in '.lnk', '.exe', '.bat', '.cmd', '.url', '.appref-ms' })) {
            $dest = Join-Path $script:DeskDir $f.Name
            $n = 1
            while (Test-Path -LiteralPath $dest) {
                $dest = Join-Path $script:DeskDir ("{0} ({1}){2}" -f [IO.Path]::GetFileNameWithoutExtension($f.Name), $n, $f.Extension)
                $n++
            }
            try {
                Move-Item -LiteralPath $f.FullName -Destination $dest
                $script:Cfg['desktopMoved'][$f.FullName.ToLowerInvariant()] = $dest
                $movidos++
            } catch { Write-Log "no movido $($f.Name): $($_.Exception.Message)" }
        }
    }
    Write-Config
    Build-Index; Rebuild-Strip
    Write-Log "Escritorio vaciado: $movidos archivos"
}

function Restore-Desktop {
    $devueltos = 0
    foreach ($k in @($script:Cfg['desktopMoved'].Keys)) {
        $from = [string]$script:Cfg['desktopMoved'][$k]
        if (-not (Test-Path -LiteralPath $from)) { $null = $script:Cfg['desktopMoved'].Remove($k); continue }
        $to = $k
        try {
            $destDir = Split-Path -Parent $to
            if (-not (Test-Path -LiteralPath $destDir)) { throw 'El escritorio original ya no existe.' }
            Move-Item -LiteralPath $from -Destination $to
            $null = $script:Cfg['desktopMoved'].Remove($k)
            $devueltos++
        } catch { Write-Log "no devuelto $to : $($_.Exception.Message)" }
    }
    Write-Config
    Build-Index; Rebuild-Strip
    Write-Log "Escritorio restaurado: $devueltos archivos"
}

function Get-LauncherVbs { Join-Path $script:Root 'GamesDock.lanzador.vbs' }

function Write-LauncherVbs {
    $self = $PSCommandPath
    if (-not $self) { $self = (Get-ChildItem -Path (Get-Location) -Filter GamesDock.ps1 | Select-Object -First 1).FullName }
    $vbs = @"
Set sh = CreateObject("WScript.Shell")
sh.Run "powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File ""$self""", 0, False
"@
    Set-Content -LiteralPath (Get-LauncherVbs) -Value $vbs -Encoding ASCII
    return (Get-LauncherVbs)
}

function Set-Autostart([bool]$on) {
    try {
        $startup = [Environment]::GetFolderPath('Startup')
        $lnkPath = Join-Path $startup 'GamesDock.lnk'
        if ($on) {
            $vbs = Write-LauncherVbs
            $s = $script:Wsh.CreateShortcut($lnkPath)
            $s.TargetPath = "$env:SystemRoot\System32\wscript.exe"
            $s.Arguments = "`"$vbs`""
            $s.Description = 'Dock de videojuegos'
            $s.Save()
        } elseif (Test-Path -LiteralPath $lnkPath) {
            Remove-Item -LiteralPath $lnkPath -Force
        }
        return $true
    } catch {
        Write-Log "autostart fallida: $($_.Exception.Message)"
        return $false
    }
}

# ------------------------------------------------------------------ bandeja y hotkey
function New-Tray {
    $ni = New-Object System.Windows.Forms.NotifyIcon
    $ni.Icon = [System.Drawing.SystemIcons]::Application
    $ni.Visible = $true
    $ni.Text = 'GamesDock'
    $menu = New-Object System.Windows.Forms.ContextMenuStrip
    $mi1 = $menu.Items.Add('Mostrar / ocultar   (Ctrl+Alt+D)')
    $mi1.add_Click({ $script:Window.Dispatcher.BeginInvoke([System.Action] { Toggle-Dock }) | Out-Null })
    $mi2 = $menu.Items.Add('Reescanear juegos')
    $mi2.add_Click({ $script:Window.Dispatcher.BeginInvoke([System.Action] { Build-Index; Rebuild-Strip }) | Out-Null })
    $mi3 = $menu.Items.Add('Ajustes')
    $mi3.add_Click({ $script:Window.Dispatcher.BeginInvoke([System.Action] { Show-Settings }) | Out-Null })
    $null = $menu.Items.Add('-')
    $mi4 = $menu.Items.Add('Salir')
    $mi4.add_Click({ $script:Exiting = $true; $script:Window.Dispatcher.BeginInvoke([System.Action] { $script:Window.Close() }) | Out-Null })
    $ni.ContextMenuStrip = $menu
    $ni.add_MouseDoubleClick({ $script:Window.Dispatcher.BeginInvoke([System.Action] { Toggle-Dock }) | Out-Null })
    return @{ Notify = $ni; Menu = $menu }
}

function Install-Hotkey {
    if (-not $script:Cfg['hotkey']) { return }
    try {
        $hwnd = (New-Object System.Windows.Interop.WindowInteropHelper($script:Window)).Handle
        # MOD_ALT=0x0001 y MOD_CONTROL=0x0002; con 0x0008 (Win) Windows se queda la combinacion y la
        # grabal teclas no la admite nunca, aunque RegisterHotKey devuelva sin error aparente
        $ok = [GD32]::RegisterHotKey($hwnd, 1, 0x0001 -bor 0x0002, 0x44)   # Ctrl+Alt+D
        $src = [System.Windows.Interop.HwndSource]::FromHwnd($hwnd)
        $src.AddHook({
            param($h, $msg, $w, $l, $handled)
            if ($msg -eq 0x0312 -and [int]$w -eq 1) {
                Toggle-Dock
                $handled.Value = $true
            }
            [IntPtr]::Zero
        })
        if ($ok) { Write-Log 'Hotkey Ctrl+Alt+D registrada.' }
        else { Write-Log 'Hotkey Ctrl+Alt+D rechazada por el sistema.' }
    } catch { Write-Log "hotkey no registrada: $($_.Exception.Message)" }
}

# ------------------------------------------------------------------ ensamblaje
Read-Config

if ($SoloDeteccion) {
    Build-Index
    exit 0
}

$null = New-Object System.Windows.Interop.WindowInteropHelper($script:Window)
$script:Window.Width = 1000; $script:Window.Height = ([int]$script:Cfg['iconSize'] + 26)
$script:Window.Add_MouseEnter({
    Set-WindowOpacity 1.0 130
    Set-PillHover $true
    # el bulto tiene que nacer bajo el cursor ya: si no, la pildora se hincha con los tiles todavia
    # chicos y los iconos quedan dentro de un cristal de sobra hasta que el raton se mueve
    Update-Proximity ([System.Windows.Input.Mouse]::GetPosition($script:RootGrid))
})
$script:Window.Add_MouseLeave({
    Reset-Proximity
    Set-WindowOpacity ([double]$script:Cfg['idleOpacity'] / 100.0) 260
    Set-PillHover $false
})
# el magnes y el bulto de la pildora trabajan solo con el raton encima: posicion -> escalas y
# contorno, sin temporizador
$script:RootGrid.Add_MouseMove({
    param($s, $e)
    if ($script:DragMoved) { return }
    Update-Proximity ($e.GetPosition($script:RootGrid))
})

$script:RootGrid.AllowDrop = $true
$script:RootGrid.Add_DragEnter({ Set-WindowOpacity 1.0 130 })
$script:RootGrid.Add_DragLeave({ Set-WindowOpacity ([double]$script:Cfg['idleOpacity'] / 100.0) 260 })
$script:RootGrid.Add_DragOver({
    param($s, $e)
    if (@(Get-DropPaths $e.Data).Count -gt 0) {
        $e.Effects = [System.Windows.DragDropEffects]::Copy
    } else { $e.Effects = [System.Windows.DragDropEffects]::None }
    $e.Handled = $true
})
$script:RootGrid.Add_Drop({
    param($s, $e)
    $e.Handled = $true
    Add-PinsBatch (Get-DropPaths $e.Data)
})

$script:Window.Add_MouseLeftButtonDown({
    param($s, $e)
    $o = $e.OriginalSource; $dentro = $false
    while ($o) {
        if ($o -is [System.Windows.Controls.Button] -or $o -is [System.Windows.Controls.Primitives.ToggleButton] -or
            $o -is [System.Windows.Controls.TextBox]) { $dentro = $true; break }
        try { $o = [System.Windows.Media.VisualTreeHelper]::GetParent($o) } catch { break }
    }
    if ($dentro) { return }
    try {
        $script:Window.DragMove()
        Save-DockAfterDrag
    } catch { }
})

function Save-DockAfterDrag {
    $wa = [System.Windows.SystemParameters]::WorkArea
    $m  = [int]$script:Margin
    $l = [double]$script:Window.Left + $m; $t = [double]$script:Window.Top + $m
    $w = [double]$script:Window.Width - 2 * $m; $h = [double]$script:Window.Height - 2 * $m
    if ($t -le ($wa.Y + $m)) { $script:Cfg['position'] = 'top' }
    elseif (($t + $h) -ge ($wa.Bottom - $m)) { $script:Cfg['position'] = 'bottom' }
    elseif ($l -le ($wa.X + $m)) { $script:Cfg['position'] = 'left' }
    elseif (($l + $w) -ge ($wa.Right - $m)) { $script:Cfg['position'] = 'right' }
    else {
        $script:Cfg['position'] = 'free'
        $script:Cfg['left'] = [int]$l; $script:Cfg['top'] = [int]$t
    }
    Write-Config
    Apply-Layout
}

$script:SearchTimer = New-Object System.Windows.Threading.DispatcherTimer
$script:SearchTimer.Interval = [TimeSpan]::FromMilliseconds(140)
$script:SearchTimer.Add_Tick({
    $script:SearchTimer.Stop()
    Rebuild-Strip
})
$script:SearchBox.Add_TextChanged({
    if ($script:SearchBox.Text -ne 'buscar...') {
        $script:SearchTimer.Stop(); $script:SearchTimer.Start()
    }
})
$script:SearchBox.Add_GotKeyboardFocus({
    if ($script:SearchBox.Text -eq 'buscar...') {
        $script:SearchBox.Foreground = Br $script:Theme.letter
        $script:SearchBox.Text = ''
    }
})
$script:SearchBox.Add_LostKeyboardFocus({
    if (-not $script:SearchBox.Text) {
        $script:SearchBox.Foreground = Br $script:Theme.fieldFg
        $script:SearchBox.Text = 'buscar...'
    }
})
$script:SearchBox.Add_KeyDown({
    param($s, $e)
    if ($e.Key -eq [System.Windows.Input.Key]::Enter) {
        if ($script:Tiles.Count -gt 0) { Start-Entry $script:Tiles[0].Tag 'enter' }
        Close-Search
        $e.Handled = $true
    } elseif ($e.Key -eq [System.Windows.Input.Key]::Escape) {
        Close-Search
        $e.Handled = $true
    }
})

function Open-Search {
    # el buscador necesita teclado: mientras esta abierto la barra sale momentaneamente del fondo
    Set-DesktopLevel $false
    $script:SearchPanel.Visibility = [System.Windows.Visibility]::Visible
    $script:Window.Activate()
    $null = $script:SearchBox.Focus()
    $script:SearchBox.SelectAll()
}

function Close-Search {
    $script:SearchPanel.Visibility = [System.Windows.Visibility]::Collapsed
    Attach-ToDesktop
    if ($script:SearchBox.Text -ne 'buscar...') {
        $script:SearchBox.Foreground = Br $script:Theme.fieldFg
        $script:SearchBox.Text = 'buscar...'
    }
}

$script:StripScroll.Add_PreviewMouseWheel({
    param($s, $e)
    $vert = $script:Cfg['position'] -in 'left', 'right'
    if ($vert) {
        $off = $script:StripScroll.VerticalOffset - $e.Delta
        $script:StripScroll.ScrollToVerticalOffset([Math]::Max(0, $off))
    } else {
        $off = $script:StripScroll.HorizontalOffset - $e.Delta
        $script:StripScroll.ScrollToHorizontalOffset([Math]::Max(0, $off))
    }
    $e.Handled = $true
})

$script:DockMenu = New-Object System.Windows.Controls.ContextMenu
function Add-MenuRow([string]$header, [scriptblock]$action) {
    $i = New-Object System.Windows.Controls.MenuItem
    $i.Header = $header
    $i.Add_Click($action)
    $null = $script:DockMenu.Items.Add($i)
    return $i
}
function New-MenuSep {
    $s = New-Object System.Windows.Controls.MenuItem
    $s.Style = Get-Style 'SepItem'
    return $s
}
$null = Add-MenuRow 'Buscar...'            { Open-Search }
$null = Add-MenuRow 'Anadir app o juego...' { Show-AddDialog }

$verMenu = New-Object System.Windows.Controls.MenuItem
$verMenu.Header = 'Que se ve'
foreach ($s in @(@{ id = 'fav'; h = 'Favoritos' }, @{ id = 'games'; h = 'Juegos detectados' },
                 @{ id = 'apps'; h = 'Aplicaciones' }, @{ id = 'all'; h = 'Todo' })) {
    $mi = New-Object System.Windows.Controls.MenuItem
    $mi.Header = $s.h; $mi.Tag = $s.id; $mi.IsCheckable = $true
    $mi.IsChecked = ($script:Cfg['section'] -eq $s.id)
    $mi.Add_Click({ Set-Section ([string]$this.Tag) })
    $null = $verMenu.Items.Add($mi)
}
$null = $script:DockMenu.Items.Add($verMenu)

$temaMenu = New-Object System.Windows.Controls.MenuItem
$temaMenu.Header = 'Tema'
foreach ($k in $script:Themes.Keys) {
    $mi = New-Object System.Windows.Controls.MenuItem
    $mi.Header = $script:Themes[$k].label; $mi.Tag = $k; $mi.IsCheckable = $true
    $mi.IsChecked = ([string]$script:Cfg['theme'] -eq [string]$k)
    $mi.Add_Click({ Set-Theme ([string]$this.Tag) })
    $null = $temaMenu.Items.Add($mi)
}
$null = $script:DockMenu.Items.Add($temaMenu)

# lo que se sacó arrastrando fuera (o con "quitar del dock") vuelve a entrar por aqui
$ocultosMenu = New-Object System.Windows.Controls.MenuItem
$ocultosMenu.Header = 'Restaurar ocultos'
$null = $script:DockMenu.Items.Add($ocultosMenu)
function Update-OcultosMenu {
    $ocultosMenu.Items.Clear()
    $ocultos = @($script:Pins.Values | Where-Object { [bool]$_.hidden })
    if ($ocultos.Count -eq 0) {
        $none = New-Object System.Windows.Controls.MenuItem
        $none.Header = '(nada oculto)'
        $none.IsEnabled = $false
        $null = $ocultosMenu.Items.Add($none)
        $ocultosMenu.IsEnabled = $false
        return
    }
    $ocultosMenu.IsEnabled = $true
    foreach ($p in ($ocultos | Sort-Object name)) {
        $mi = New-Object System.Windows.Controls.MenuItem
        $mi.Header = [string]$p.name; $mi.Tag = [string]$p.key
        $mi.Add_Click({ Unhide-Pin ([string]$this.Tag) })
        $null = $ocultosMenu.Items.Add($mi)
    }
}

$null = Add-MenuRow 'Reescanear' {
    $null = $script:Window.Dispatcher.BeginInvoke([System.Action] { Build-Index; Rebuild-Strip })
}
$null = Add-MenuRow 'Ajustes' { Show-Settings }
$null = $script:DockMenu.Items.Add((New-MenuSep))
$null = Add-MenuRow 'Ocultar (Ctrl+Alt+D)' { Hide-Dock }
$null = Add-MenuRow 'Salir' { $script:Exiting = $true; $script:Window.Close() }

$script:DockMenu.Add_Opened({
    foreach ($o in $verMenu.Items) { $o.IsChecked = ($script:Cfg['section'] -eq [string]$o.Tag) }
    foreach ($o in $temaMenu.Items) { $o.IsChecked = ([string]$script:Cfg['theme'] -eq [string]$o.Tag) }
    Update-OcultosMenu
})

$script:RootGrid.Add_MouseRightButtonUp({
    param($s, $e)
    $o = $e.OriginalSource; $dentro = $false
    while ($o) { if ($o -is [System.Windows.Controls.Button]) { $dentro = $true; break }; try { $o = [System.Windows.Media.VisualTreeHelper]::GetParent($o) } catch { break } }
    if (-not $dentro) { $script:DockMenu.PlacementTarget = $script:Glass; $script:DockMenu.IsOpen = $true }
})

$script:Window.Add_Closed({
    Write-Config
    Close-Ghost
    try {
        $hwnd = (New-Object System.Windows.Interop.WindowInteropHelper($script:Window)).Handle
        $null = [GD32]::UnregisterHotKey($hwnd, 1)
    } catch { }
    try { $script:Tray.Notify.Visible = $false; $script:Tray.Notify.Dispose() } catch { }
    try { $script:App.Shutdown() } catch { }
})

$script:Tray = New-Tray
$null = Write-LauncherVbs

# devolviendo la RAM al sistema cuando la barra lleva un rato sin tocarse
$script:IdleTimer = New-Object System.Windows.Threading.DispatcherTimer
$script:IdleTimer.Interval = [TimeSpan]::FromSeconds(60)
$script:IdleTimer.Add_Tick({
    if ($script:Window.IsMouseOver -or $script:IconQueue.Count -gt 0) { return }
    if ((Get-Process -Id $PID).WorkingSet64 -lt 80MB) { return }
    Trim-Memory
})
$script:IdleTimer.Start()

# la banda transparente que rodea la pildora va en el margen del Grid: el Padding de Window no lo
# aplica su plantilla, y sin esa banda la pildora se estiraba hasta el borde de la ventana dejando
# un rabo vacio a la derecha y sin sitio por donde desbordar el icono agrandado
$script:RootGrid.Margin = New-Object System.Windows.Thickness([int]$script:Margin)
Attach-ToDesktop
Apply-Theme ([string]$script:Cfg['theme'])
Apply-Layout
$script:Window.ShowActivated = $false
$script:Window.Show()
Send-ToBottom
Rebuild-Strip
Play-Entry

# primer pintado ya hecho: el escaneo va en prioridad background para no bloquear la barra
$null = $script:Window.Dispatcher.BeginInvoke([System.Action] {
    Build-Index
    Rebuild-Strip -Pop
}, [System.Windows.Threading.DispatcherPriority]::Background)

# el fantasma se monta aqui, con el dock ya pintado: la primera vez que alguien levanta un icono no
# puede coincidir con la primera vez que se crea la ventana, que ese costo si se nota en el gesto
$null = $script:Window.Dispatcher.BeginInvoke([System.Action] {
    if (-not $script:GhostWin) { $null = New-GhostWindow }
}, [System.Windows.Threading.DispatcherPriority]::ContextIdle)

Install-Hotkey

$script:App.Run()
Write-Log "Cerrado. Exiting=$($script:Exiting)"
$script:Single.ReleaseMutex()
$script:Single.Dispose()
