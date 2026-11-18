# Libre Settings Importer

A small tool that copies all settings and customizations of an installed **LibreHardwareMonitor**
to **K26K Hardware Monitor**, so you don't have to set everything up again.

## What is imported

Everything stored in LibreHardwareMonitor's settings file, for example:

- Theme (Light/Dark/Black/Auto) and temperature unit
- Hidden sensors and renamed sensors and hardware
- Plot selection, plot colors, plot position, stacked axes and line thickness
- Visible columns (Value/Min/Max) and expanded/collapsed state of the tree
- Remote web server settings (interface, port, authentication)
- Logging, update interval, hardware options (CPU, GPU, storage …)
- Window size and position
- The stored value history of the sensors

Not imported, because it is not part of the settings file:

- **Autostart**: enable it again in K26K under *Options → Run On Windows Startup*.
  K26K uses its own autostart entry, so the autostart of LibreHardwareMonitor stays untouched.

Fixed plot axis ranges saved by LibreHardwareMonitor are ignored by K26K v1.1 and later,
so the autoscaling bug of the original is not carried over.

## Usage

1. Exit K26K Hardware Monitor if it is running (tray icon → *Exit*).
   K26K saves its settings when it exits and would otherwise overwrite the import.
2. Copy both files of this folder next to `K26KHardwareMonitor.exe` (recommended), or keep them anywhere.
3. Double-click **`Libre-Settings-Importer.cmd`**.
4. If the tool cannot find a file by itself, it asks for it:
   - `LibreHardwareMonitor.config` of your LibreHardwareMonitor installation
   - `K26KHardwareMonitor.exe` of your K26K installation
5. Check the summary (number of settings, theme, hidden sensors, plotted sensors) and click **OK**.
6. Start K26K Hardware Monitor.

If LibreHardwareMonitor keeps running with its web server enabled, both programs now use the same port.
Change the port of one of them under *Options → Remote Web Server → Interface/Port*.

### Command line

```powershell
powershell -ExecutionPolicy Bypass -File .\Import-LibreSettings.ps1 [-Source <path>] [-Target <folder>] [-Yes]
```

| Parameter | Meaning |
| --- | --- |
| `-Source` | Path of `LibreHardwareMonitor.config`. Default: found automatically. |
| `-Target` | Folder that contains `K26KHardwareMonitor.exe`. Default: the folder of the script. |
| `-Yes` | No dialogs: answers every question with OK and prints the messages to the console. Stops if K26K is running. |

## How it works

K26K Hardware Monitor is based on LibreHardwareMonitor and uses exactly the same settings format and keys.
Both programs store their settings in an XML file next to the executable:

| Program | Settings file |
| --- | --- |
| LibreHardwareMonitor | `LibreHardwareMonitor.config` |
| K26K Hardware Monitor | `K26KHardwareMonitor.config` |

The importer therefore copies the file as a whole:

1. **Find the source.** It looks for `LibreHardwareMonitor.config` in
   `%ProgramFiles%\LibreHardwareMonitor`, `%ProgramFiles(x86)%\LibreHardwareMonitor`,
   `%LOCALAPPDATA%\LibreHardwareMonitor` and `%USERPROFILE%\Downloads\LibreHardwareMonitor`,
   otherwise it opens a file dialog.
2. **Validate it.** The file must be XML with at least one `configuration/appSettings/add` entry.
3. **Find the target.** It uses the script's own folder if `K26KHardwareMonitor.exe` is there, otherwise it opens a file dialog.
4. **Make sure K26K is not running.** If it is, the tool asks you to exit it and try again.
5. **Show a summary** and ask for confirmation.
6. **Back up and copy.** An existing `K26KHardwareMonitor.config` is kept as `K26KHardwareMonitor.config.bak`,
   then `LibreHardwareMonitor.config` is copied to `K26KHardwareMonitor.config`.
7. **Admin rights only if needed.** If the target folder is write-protected (for example under `Program Files`),
   the tool restarts itself with administrator rights.

LibreHardwareMonitor's own settings file is only read, never changed.

### Undo

Delete `K26KHardwareMonitor.config` and rename `K26KHardwareMonitor.config.bak` back to `K26KHardwareMonitor.config`
(while K26K is not running).

## License

AGPL-3.0-or-later, like K26K Hardware Monitor. See [LICENSE](../LICENSE).
