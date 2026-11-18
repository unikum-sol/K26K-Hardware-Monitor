@echo off
rem Local tool: copies all LibreHardwareMonitor settings to K26K Hardware Monitor.
powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "%~dp0Import-LibreSettings.ps1" %*
