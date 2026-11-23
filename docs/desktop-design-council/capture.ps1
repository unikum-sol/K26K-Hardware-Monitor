<#
  Screenshots for the desktop design council (no administrator rights needed).

  - Renders the Fancy dashboard from the snapshots in .\snapshots (idle, load) at 1920x1080 and 1366x768.
  - Starts K26KHardwareMonitor.exe without elevation in a temporary copy (Sci-Fi and Fancy) and captures
    the real window with PrintWindow. Without elevation some sensors are missing; the renders show full data.

  Usage: .\capture.ps1 -Round 1 [-Build ..\..\bin\Release\net10.0-windows]
#>
param(
  [Parameter(Mandatory = $true)][int]$Round,
  [string]$Build = (Join-Path $PSScriptRoot "..\..\bin\Release\net10.0-windows")
)

$ErrorActionPreference = 'Stop'
$out = Join-Path $PSScriptRoot "round-$Round"
New-Item -ItemType Directory -Force $out | Out-Null
$exe = Join-Path (Resolve-Path $Build) "K26KHardwareMonitor.exe"
$env:__COMPAT_LAYER = 'RunAsInvoker'

function Render($snapshot, $file, $size, $extra = @()) {
  $args = @("--render-snapshot", (Join-Path $PSScriptRoot "snapshots\$snapshot.json"), (Join-Path $out $file), "--size", $size, "--t", "7.4") + $extra
  $p = Start-Process $exe -ArgumentList $args -PassThru -WindowStyle Hidden
  $p.WaitForExit(60000) | Out-Null
}

Render "idle" "fancy-idle-1920.png" "1920x1080"
Render "load" "fancy-load-1920.png" "1920x1080"
Render "load" "fancy-load-1366.png" "1366x768"
Render "load" "fancy-load-reduced-motion-1920.png" "1920x1080" @("--reduced-motion")
Render "load" "fancy-load-no-glow-1920.png" "1920x1080" @("--no-glow")
Render "load" "header-load.png" "1500x64" @("--view", "header")
# 150 % display scaling: the dashboard at 1920x1080 logical pixels and the header with scale 1.5
Render "load" "fancy-load-150pct.png" "2880x1620"
Render "load" "header-load-150pct.png" "2250x96" @("--view", "header", "--scale", "1.5")

# ---------- live window captures ----------
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class Win {
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT r);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
"@

$runDir = Join-Path $env:TEMP "k26k-council-run"
if (Test-Path $runDir) { Get-ChildItem $runDir | Remove-Item -Recurse -Force }
New-Item -ItemType Directory -Force $runDir | Out-Null
Copy-Item (Join-Path (Resolve-Path $Build) "*") $runDir -Recurse -Force

$config = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration><appSettings>
  <add key="mainForm.Location.X" value="40" /><add key="mainForm.Location.Y" value="40" />
  <add key="mainForm.Width" value="1500" /><add key="mainForm.Height" value="940" />
  <add key="plotMenuItem" value="true" /><add key="plotLocation" value="2" />
  <add key="splitContainer.SplitterDistance" value="760" />
  <add key="plotPanel.MinTimeSpan" value="0" /><add key="plotPanel.MaxTimeSpan" value="60" />
  <add key="minMenuItem" value="true" />
  <add key="/amdcpu/0/Voltage/.expanded" value="false" /><add key="/amdcpu/0/Power/.expanded" value="false" />
  <add key="/amdcpu/0/Clock/.expanded" value="false" /><add key="/amdcpu/0/Factor/.expanded" value="false" />
  <add key="/lpc/it8689e/0/expanded" value="false" />
  <add key="/amdcpu/0/load/0/plot" value="true" />
  <add key="/gpu-nvidia/0/load/0/plot" value="true" />
  <add key="/ram/load/0/plot" value="true" />
  <add key="/gpu-nvidia/0/temperature/0/plot" value="true" />
</appSettings></configuration>
"@

function Capture($design, $file) {
  Set-Content -Path (Join-Path $runDir "K26KHardwareMonitor.config") -Value $config -Encoding UTF8
  $p = Start-Process (Join-Path $runDir "K26KHardwareMonitor.exe") -ArgumentList @("--design", $design) -PassThru
  # wait until the main window exists, then give it time to collect data
  $h = [IntPtr]::Zero
  for ($i = 0; $i -lt 60 -and $h -eq [IntPtr]::Zero; $i++) { Start-Sleep -Seconds 1; $p.Refresh(); $h = $p.MainWindowHandle }
  Start-Sleep -Seconds 45
  $p.Refresh()
  $h = $p.MainWindowHandle
  if ($h -ne [IntPtr]::Zero) {
    $r = New-Object Win+RECT
    [Win]::GetWindowRect($h, [ref]$r) | Out-Null
    $bmp = New-Object System.Drawing.Bitmap ($r.Right - $r.Left), ($r.Bottom - $r.Top)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $hdc = $g.GetHdc()
    [Win]::PrintWindow($h, $hdc, 2) | Out-Null
    $g.ReleaseHdc($hdc); $g.Dispose()
    $bmp.Save((Join-Path $out $file)); $bmp.Dispose()
  }
  Stop-Process -Id $p.Id -Force
  Start-Sleep -Seconds 2
}

Capture "scifi" "window-scifi.png"
Capture "fancy" "window-fancy.png"
Capture "classic" "window-classic.png"

Get-ChildItem $out | Select-Object Name, Length
