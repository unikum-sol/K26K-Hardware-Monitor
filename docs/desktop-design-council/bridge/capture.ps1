<#
  Screenshots of the Bridge design for the design council (no administrator rights needed).

  - Renders the Bridge Fancy dashboard, header and status strip from the snapshots in ..\snapshots (idle, load)
    at 1920x1080, 1366x768, 150 % scaling, with reduced motion and during the boot sequence.
  - Starts K26KHardwareMonitor.exe without elevation in a temporary copy (Bridge and Bridge Fancy, once with
    real CPU load) and captures the real window with PrintWindow. Without elevation some sensors are missing;
    the renders show full data.

  Usage: .\capture.ps1 -Round 1 [-Build ..\..\..\bin\Release\net10.0-windows]
#>
param(
  [Parameter(Mandatory = $true)][int]$Round,
  [string]$Build = ""
)

$ErrorActionPreference = 'Stop'
# (Windows PowerShell has no $PSScriptRoot in parameter defaults)
if (-not $Build) { $Build = Join-Path $PSScriptRoot "..\..\..\bin\Release\net10.0-windows" }
$out = Join-Path $PSScriptRoot "round-$Round"
New-Item -ItemType Directory -Force $out | Out-Null
$exe = Join-Path (Resolve-Path $Build) "K26KHardwareMonitor.exe"
$snapshots = Join-Path $PSScriptRoot "..\snapshots"
$env:__COMPAT_LAYER = 'RunAsInvoker'

function Render-Snapshot($snapshot, $file, $size, $extra = @()) {
  $arguments = @("--render-snapshot", (Join-Path $snapshots "$snapshot.json"), (Join-Path $out $file)) + $extra + @("--size", $size, "--t", "7.4")
  $p = Start-Process $exe -ArgumentList $arguments -PassThru -WindowStyle Hidden
  $p.WaitForExit(60000) | Out-Null
}

Render-Snapshot "idle" "bridge-fancy-idle-1920.png" "1920x1080" @("--view", "bridge-fancy")
Render-Snapshot "load" "bridge-fancy-load-1920.png" "1920x1080" @("--view", "bridge-fancy")
Render-Snapshot "load" "bridge-fancy-load-1366.png" "1366x768" @("--view", "bridge-fancy")
Render-Snapshot "load" "bridge-fancy-load-reduced-motion-1920.png" "1920x1080" @("--view", "bridge-fancy", "--reduced-motion")
Render-Snapshot "load" "bridge-fancy-load-boot-1920.png" "1920x1080" @("--view", "bridge-fancy", "--t", "1.2")
# 150 % display scaling: the dashboard at 1920x1080 logical pixels, header and status with scale 1.5
Render-Snapshot "load" "bridge-fancy-load-150pct.png" "2880x1620" @("--view", "bridge-fancy")
Render-Snapshot "load" "bridge-header-load.png" "1500x84" @("--view", "bridge-header")
Render-Snapshot "load" "bridge-header-load-150pct.png" "2250x126" @("--view", "bridge-header", "--scale", "1.5")
Render-Snapshot "load" "bridge-status-load.png" "1500x32" @("--view", "bridge-status")

# ---------- live window captures ----------
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class BridgeWin {
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT r);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
"@

$runDir = Join-Path $env:TEMP "k26k-bridge-council-run"
if (Test-Path $runDir) { Get-ChildItem $runDir | Remove-Item -Recurse -Force }
New-Item -ItemType Directory -Force $runDir | Out-Null
Copy-Item (Join-Path (Resolve-Path $Build) "*") $runDir -Recurse -Force

function Capture($design, $file, $width, $height, [switch]$Load) {
  $config = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration><appSettings>
  <add key="mainForm.Location.X" value="10" /><add key="mainForm.Location.Y" value="10" />
  <add key="mainForm.Width" value="$width" /><add key="mainForm.Height" value="$height" />
  <add key="plotMenuItem" value="true" /><add key="plotLocation" value="2" />
  <add key="splitContainer.SplitterDistance" value="$([int]($width / 2))" />
  <add key="plotPanel.MinTimeSpan" value="0" /><add key="plotPanel.MaxTimeSpan" value="60" />
  <add key="minMenuItem" value="true" />
  <add key="fancyReducedMotion" value="false" />
  <add key="keepSensorHistory" value="false" />
  <add key="/amdcpu/0/Voltage/.expanded" value="false" /><add key="/amdcpu/0/Power/.expanded" value="false" />
  <add key="/amdcpu/0/Clock/.expanded" value="false" /><add key="/amdcpu/0/Factor/.expanded" value="false" />
  <add key="/lpc/it8689e/0/expanded" value="false" />
  <add key="/amdcpu/0/load/0/plot" value="true" />
  <add key="/gpu-nvidia/0/load/0/plot" value="true" />
  <add key="/ram/load/0/plot" value="true" />
  <add key="/gpu-nvidia/0/temperature/0/plot" value="true" />
</appSettings></configuration>
"@
  Set-Content -Path (Join-Path $runDir "K26KHardwareMonitor.config") -Value $config -Encoding UTF8
  $p = Start-Process (Join-Path $runDir "K26KHardwareMonitor.exe") -ArgumentList @("--design", $design) -PassThru
  $h = [IntPtr]::Zero
  for ($i = 0; $i -lt 60 -and $h -eq [IntPtr]::Zero; $i++) { Start-Sleep -Seconds 1; $p.Refresh(); $h = $p.MainWindowHandle }
  if ($Load) {
    # real CPU load for the meter colors: one busy loop per core, each ends by itself after 80 s
    $busy = "`$e = (Get-Date).AddSeconds(80); while ((Get-Date) -lt `$e) { }"
    1..[Environment]::ProcessorCount | ForEach-Object { Start-Process powershell -ArgumentList @("-NoProfile", "-Command", $busy) -WindowStyle Hidden | Out-Null }
  }
  Start-Sleep -Seconds 60
  $p.Refresh()
  $h = $p.MainWindowHandle
  if ($h -ne [IntPtr]::Zero) {
    $r = New-Object BridgeWin+RECT
    [BridgeWin]::GetWindowRect($h, [ref]$r) | Out-Null
    $bmp = New-Object System.Drawing.Bitmap ($r.Right - $r.Left), ($r.Bottom - $r.Top)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $hdc = $g.GetHdc()
    [BridgeWin]::PrintWindow($h, $hdc, 2) | Out-Null
    $g.ReleaseHdc($hdc); $g.Dispose()
    $bmp.Save((Join-Path $out $file)); $bmp.Dispose()
  }
  Stop-Process -Id $p.Id -Force
  Start-Sleep -Seconds 2
}

Capture "bridge" "window-bridge-load.png" 1500 900 -Load
Capture "bridge" "window-bridge.png" 1500 900
Capture "bridgefancy" "window-bridgefancy.png" 1600 900

Get-ChildItem $out | Select-Object Name, Length
