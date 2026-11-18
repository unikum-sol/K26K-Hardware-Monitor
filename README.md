# K26K Hardware Monitor

K26K Hardware Monitor ist eine erweiterte Version von [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor).
Sie bringt eine **neue, für Smartphones optimierte Weboberfläche** mit: Alle Sensoren und Kurven lassen sich im Browser
auf dem Handy, Tablet oder einem anderen PC ansehen – mit denselben Einstellungen, die in der Desktop-App eingestellt sind.

## Was ist neu?

- **Mobile Weboberfläche** unter `http://<PC-IP>:<Port>/` (Standard-Port 8085)
  - Karten pro Hardware, Gruppen pro Sensortyp, große, gut lesbare Werte und Lastbalken
  - **Plot-Karte** mit genau den Kurven, die in der Desktop-App für den Plot ausgewählt sind
  - Antippen eines Sensors öffnet **seine eigene Kurve**; Antippen/Ziehen in einer Kurve zeigt die Werte zu diesem Zeitpunkt
  - Zeitraum der Kurven wählbar (1 min bis 24 h) oder wie im Desktop-Plot
  - Suche/Filter, Auf-/Zuklappen, lässt sich als App auf den Startbildschirm legen
- **Gespiegelte Desktop-Einstellungen** (live, ohne Neuladen):
  - Design (Light/Dark/Black/Auto) inkl. Plot-Farbpalette
  - ausgeblendete Sensoren und „Show Hidden Sensors“
  - umbenannte Sensoren und Hardware
  - Spalten Value/Min/Max
  - Plot an/aus, ausgewählte Plot-Sensoren und ihre Farben, „Stacked Axes“, sichtbarer Zeitraum, Linienstärke
  - Temperatureinheit °C/°F
  - Aufklapp-Zustand der Hardware und Sensorgruppen
  - Aktualisierungsintervall
- **Neue JSON-API** (nur lesend):
  - `GET /api/state` – Sensorbaum, aktuelle Werte und gespiegelte Einstellungen
  - `GET /api/history?ids=<id1>,<id2>&span=<Sekunden>&points=<Anzahl>` – Werteverlauf, auf `points` Punkte verdichtet
- Die bisherige Weboberfläche bleibt als `classic.html` erhalten; `data.json`, `/Sensor` und `/metrics` funktionieren unverändert.

## Benutzung

1. K26K Hardware Monitor **als Administrator** starten (wie LibreHardwareMonitor).
2. Unter **Options → Remote Web Server → Interface/Port** die IP-Adresse des PCs im Heimnetz wählen (nicht `127.0.0.1`) und den Port festlegen.
3. **Options → Remote Web Server → Run** aktivieren.
4. In der Windows-Firewall den Port (Standard 8085, TCP) für das private Netzwerk freigeben, z. B. in einer Administrator-PowerShell:
   ```powershell
   New-NetFirewallRule -DisplayName "K26K Hardware Monitor" -Direction Inbound -Protocol TCP -LocalPort 8085 -Action Allow -Profile Private
   ```
5. Auf dem Smartphone `http://<PC-IP>:8085/` öffnen.

Optional lässt sich unter **Options → Remote Web Server → Authentication** ein Benutzername/Passwort setzen.
Achtung: Die Verbindung ist unverschlüsseltes HTTP und nur für das eigene Netzwerk gedacht.

## Bauen

Benötigt das .NET SDK in der Version, die die Projektdateien verlangen (aktuell .NET 11, siehe `.github/workflows`).

```powershell
dotnet build LibreHardwareMonitor.Windows.Forms\LibreHardwareMonitor.Windows.Forms.csproj -c Release -p:Platform=x64
```

Die neue Weboberfläche liegt in `LibreHardwareMonitor.Windows.Forms/Resources/Web/` (`index.html`, `app.js`, `app.css`),
die API in `LibreHardwareMonitor.Windows.Forms/Utilities/WebApi.cs`.

## Updates von LibreHardwareMonitor übernehmen

Dieses Repository ist ein Fork. Änderungen des Originals lassen sich so übernehmen:

```powershell
git fetch upstream
git merge upstream/master
```

## Lizenz

- Das Gesamtwerk K26K Hardware Monitor wird unter der **GNU Affero General Public License v3.0** weitergegeben, siehe [LICENSE](LICENSE).
- Der Code von LibreHardwareMonitor steht unter der **Mozilla Public License 2.0**, siehe [LICENSE-MPL-2.0](LICENSE-MPL-2.0).
  Die MPL-Hinweise in diesen Dateien bleiben erhalten; die MPL 2.0 erlaubt nach Abschnitt 3.3 die Weitergabe als Teil eines
  größeren Werks unter der (A)GPL.
- Neue Dateien von K26K (`WebApi.cs`, `Resources/Web/index.html`, `app.js`, `app.css`, `manifest.webmanifest`, `icon.svg`)
  stehen unter der AGPL-3.0-or-later.
- Weitere Drittanbieter-Hinweise: [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).

Die Weboberfläche verlinkt den Quellcode (AGPL Abschnitt 13).

---

## Original-README von LibreHardwareMonitor
[![GitHub license](https://img.shields.io/github/license/LibreHardwareMonitor/LibreHardwareMonitor?label=License)](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/blob/master/LICENSE)

[![NuGet](https://img.shields.io/nuget/v/LibreHardwareMonitorLib?label=NuGet&logo=nuget&logoColor=white)](https://www.nuget.org/packages/LibreHardwareMonitorLib/)
[![NuGet (Prerelases)](https://img.shields.io/nuget/vpre/LibreHardwareMonitorLib?label=NuGet-Prerelease&logo=nuget&logoColor=white)](https://www.nuget.org/packages/LibreHardwareMonitorLib/)
[![Downloads](https://img.shields.io/nuget/dt/LibreHardwareMonitorLib?label=Downloads&logo=nuget&logoColor=white)](https://www.nuget.org/packages/LibreHardwareMonitorLib/)

Libre Hardware Monitor is free software that can monitor the temperature sensors, fan speeds, voltages, load and clock speeds of your computer. 

## What's included?
| Name| Build Status |
| --- | --- |
| **LibreHardwareMonitorLib** <br /> Library that allows you to use all features in your own application | [![Build status](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/actions/workflows/master.yml/badge.svg)](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/actions) |
| **LibreHardwareMonitor.Windows.Forms** <br /> Windows Forms based application that presents all data in a graphical interface | [![Build status](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/actions/workflows/master.yml/badge.svg)](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/actions) |
| **LibreHardwareMonitor.Avalonia** <br /> Avalonia based application that presents all data in a graphical interface <br/> _In development, not recommended yet_ | [![Build status](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/actions/workflows/master.yml/badge.svg)](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/actions) |

## What can it do?
You can read information from devices such as:
- Motherboards
- Intel and AMD processors
- NVIDIA, AMD and Intel graphics cards
- HDD, SSD and NVMe hard drives
- Network cards

## Where can I download it?
You can download the latest release [here](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/releases).

### Nightly builds
If you have a **GitHub** account, you can download nightly builds [here](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/actions).

Otherwise, you can download the *latest* nightly build
[here (Windows.Forms)](https://nightly.link/LibreHardwareMonitor/LibreHardwareMonitor/workflows/master/master/LibreHardwareMonitor.Windows.Forms.zip)
or [here (Avalonia)](https://nightly.link/LibreHardwareMonitor/LibreHardwareMonitor/workflows/master/master/LibreHardwareMonitor.Avalonia%20%28win-x64%29.zip).

## How can I help improve it?
The LibreHardwareMonitor team welcomes feedback and contributions!<br/>
You can check if it works properly on your motherboard. For many manufacturers, the way of reading data differs a bit, so if you notice any inaccuracies, please send us a pull request. If you have any suggestions or improvements, don't hesitate to create an issue.

### Which .NET versions are supported?

| Target | Library | Windows.Forms | Avalonia |
| --- | --- | --- | --- |
| .NET Framework 4.7.2 | ✅ | ✅ | ❌ |
| .NET Framework 4.8.1 | ✅ | ✅ | ❌ |
| .NET Standard 2.0 | ✅ | ✅ | ❌ |
| .NET 10 | ✅ | ✅ | ✅ |
| .NET 11 | ✅ | ❌ | ❌ |

## Developer information
**Integrate the library in own application**
1. Add the [LibreHardwareMonitorLib](https://www.nuget.org/packages/LibreHardwareMonitorLib/) NuGet package to your application.
2. Use the sample code below.

**Sample code**
```c#
Computer computer = new Computer
{
    IsCpuEnabled = true,
    IsGpuEnabled = true,
    IsMemoryEnabled = true,
    IsMotherboardEnabled = true,
    IsControllerEnabled = true,
    IsNetworkEnabled = true,
    IsStorageEnabled = true,
    IsPowerMonitorEnabled = true,
};

computer.Open();
computer.Accept(new UpdateVisitor());

foreach (IHardware hardware in computer.Hardware)
{
    Console.WriteLine("Hardware: {0}", hardware.Name);
    
    foreach (IHardware subhardware in hardware.SubHardware)
    {
        Console.WriteLine("\tSubhardware: {0}", subhardware.Name);
        
        foreach (ISensor sensor in subhardware.Sensors)
            Console.WriteLine("\t\tSensor: {0}, value: {1}", sensor.Name, sensor.Value);
    }

    foreach (ISensor sensor in hardware.Sensors)
        Console.WriteLine("\tSensor: {0}, value: {1}", sensor.Name, sensor.Value);
}

computer.Close();

public class UpdateVisitor : IVisitor
{
    public void VisitComputer(IComputer computer) => computer.Traverse(this);

    public void VisitHardware(IHardware hardware)
    {
        hardware.Update();
        foreach (IHardware subHardware in hardware.SubHardware)
            subHardware.Accept(this);
    }

    public void VisitSensor(ISensor sensor) { }

    public void VisitParameter(IParameter parameter) { }
}
```

**Administrator rights**

Some sensors require administrator privileges to access the data. Restart your IDE with admin privileges, or add an [app.manifest](https://learn.microsoft.com/en-us/windows/win32/sbscs/application-manifests) file to your project with requestedExecutionLevel on requireAdministrator.

## Warning

We're **not affiliated** with `librehardwaremonitor.com`.<br>
For your safety, please avoid using that site.

## Acknowledgements

Many thanks to all contributors listed below, and to JetBrains for providing tooling.

<a href="https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/graphs/contributors">
  <img src="https://contrib.rocks/image?repo=LibreHardwareMonitor/LibreHardwareMonitor" />
</a>

## License
LibreHardwareMonitor is free and open source software licensed under MPL 2.0. Some parts of LibreHardwareMonitor are licensed under different terms, see [THIRD-PARTY-LICENSES](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/blob/master/THIRD-PARTY-NOTICES.txt).
