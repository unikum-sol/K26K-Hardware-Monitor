<#
  Libre Settings Importer for K26K Hardware Monitor
  Copyright (C) 2026 K26K Hardware Monitor contributors
  Licensed under the GNU Affero General Public License v3.0 or later, see LICENSE.

  Copies all settings and customizations of LibreHardwareMonitor to K26K Hardware Monitor:
  theme, hidden and renamed sensors, plot selection and colors, columns, web server,
  logging, hardware options, window layout and the stored value history.
  Both programs use the same settings format, so the file is copied as a whole.

  Usage:
    Double-click "Libre-Settings-Importer.cmd", or
    .\Import-LibreSettings.ps1 [-Source <LibreHardwareMonitor.config>] [-Target <folder of K26KHardwareMonitor.exe>]
#>
param(
  [string]$Source,
  [string]$Target,
  [switch]$Yes   # no questions: answers OK to everything and prints messages to the console
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms

function Show-Message([string]$text, [string]$icon = 'Information', [string]$buttons = 'OK') {
  if ($Yes) {
    Write-Host $text
    if ($buttons -eq 'RetryCancel') { return 'Cancel' }
    return 'OK'
  }
  return [System.Windows.Forms.MessageBox]::Show($text, 'Libre Settings Importer', $buttons, $icon)
}

function Test-SettingsFile([string]$path) {
  if (-not $path -or -not (Test-Path -LiteralPath $path)) { return $false }
  try {
    [xml]$xml = Get-Content -LiteralPath $path -Raw
    return @($xml.configuration.appSettings.add).Count -gt 0
  } catch { return $false }
}

# ---------- source: LibreHardwareMonitor.config ----------
if (-not $Source) {
  $candidates = @(
    "$env:ProgramFiles\LibreHardwareMonitor\LibreHardwareMonitor.config",
    "${env:ProgramFiles(x86)}\LibreHardwareMonitor\LibreHardwareMonitor.config",
    "$env:LOCALAPPDATA\LibreHardwareMonitor\LibreHardwareMonitor.config",
    "$env:USERPROFILE\Downloads\LibreHardwareMonitor\LibreHardwareMonitor.config"
  )
  $Source = $candidates | Where-Object { Test-SettingsFile $_ } | Select-Object -First 1
}
if (-not (Test-SettingsFile $Source)) {
  $dialog = New-Object System.Windows.Forms.OpenFileDialog
  $dialog.Title = 'Select LibreHardwareMonitor.config'
  $dialog.Filter = 'LibreHardwareMonitor settings (LibreHardwareMonitor.config)|LibreHardwareMonitor.config|Settings files (*.config)|*.config'
  if ($dialog.ShowDialog() -ne 'OK') { exit 1 }
  $Source = $dialog.FileName
  if (-not (Test-SettingsFile $Source)) { Show-Message "This is not a LibreHardwareMonitor settings file:`n$Source" 'Warning' | Out-Null; exit 1 }
}

# ---------- target: folder of K26KHardwareMonitor.exe ----------
if (-not $Target) {
  $here = Split-Path -Parent $MyInvocation.MyCommand.Path
  if (Test-Path -LiteralPath (Join-Path $here 'K26KHardwareMonitor.exe')) { $Target = $here }
}
if (-not $Target -or -not (Test-Path -LiteralPath (Join-Path $Target 'K26KHardwareMonitor.exe'))) {
  $dialog = New-Object System.Windows.Forms.OpenFileDialog
  $dialog.Title = 'Select K26KHardwareMonitor.exe'
  $dialog.Filter = 'K26K Hardware Monitor (K26KHardwareMonitor.exe)|K26KHardwareMonitor.exe'
  if ($dialog.ShowDialog() -ne 'OK') { exit 1 }
  $Target = Split-Path -Parent $dialog.FileName
}
$targetFile = Join-Path $Target 'K26KHardwareMonitor.config'

# ---------- K26K must not run, it would overwrite the settings when it exits ----------
while (Get-Process K26KHardwareMonitor -ErrorAction SilentlyContinue) {
  $answer = Show-Message "K26K Hardware Monitor is running.`nIt saves its settings when it exits and would overwrite the import.`n`nPlease exit it (tray icon -> Exit), then click Retry." 'Warning' 'RetryCancel'
  if ($answer -ne 'Retry') { exit 1 }
}

# ---------- summary + confirmation ----------
[xml]$xml = Get-Content -LiteralPath $Source -Raw
$keys = @($xml.configuration.appSettings.add)
$hidden = @($keys | Where-Object { $_.key -like '*/hidden' -and $_.value -eq 'true' }).Count
$plot = @($keys | Where-Object { $_.key -like '*/plot' -and $_.value -eq 'true' }).Count
$names = @($keys | Where-Object { $_.key -like '*/name' }).Count
$theme = ($keys | Where-Object { $_.key -eq 'theme' }).value

$summary = "From:`n$Source`n`nTo:`n$targetFile`n`n" +
  "$($keys.Count) settings, including:`n" +
  " - Theme: $(if ($theme) { $theme } else { 'default' })`n" +
  " - Hidden sensors: $hidden`n" +
  " - Sensors in the plot: $plot`n" +
  " - Sensor/hardware names: $names`n`n" +
  $(if (Test-Path -LiteralPath $targetFile) { "The current K26K settings are kept as K26KHardwareMonitor.config.bak.`n`n" } else { '' }) +
  'Import now?'
if ((Show-Message $summary 'Question' 'OKCancel') -ne 'OK') { exit 1 }

# ---------- copy (restart elevated if the target folder needs admin rights) ----------
try {
  if (Test-Path -LiteralPath $targetFile) { Copy-Item -LiteralPath $targetFile -Destination "$targetFile.bak" -Force }
  Copy-Item -LiteralPath $Source -Destination $targetFile -Force
} catch [System.UnauthorizedAccessException] {
  $isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
  if ($isAdmin) { throw }
  Start-Process powershell.exe -Verb RunAs -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$($MyInvocation.MyCommand.Path)`"", '-Source', "`"$Source`"", '-Target', "`"$Target`"")
  exit 0
}

Show-Message "Done. $($keys.Count) settings were imported.`n`nStart K26K Hardware Monitor to use them.`n`nNot included: autostart (enable it again under Options -> Run On Windows Startup).`nIf LibreHardwareMonitor keeps running with its web server, give one of them another port." | Out-Null
