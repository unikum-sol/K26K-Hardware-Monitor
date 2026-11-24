// K26K Hardware Monitor - data model for the Sci-Fi HUD and the Fancy dashboard.
// Copyright (C) 2026 K26K Hardware Monitor contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
//
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System.Collections.Generic;

namespace LibreHardwareMonitor.Windows.Forms.Telemetry;

/// <summary>
/// One snapshot of everything the HUD and the Fancy dashboard show. Temperatures are in °C;
/// renderers convert to °F when <see cref="Fahrenheit"/> is set. NaN means "no sensor".
/// The model is plain data, so it can be saved as JSON and rendered later without hardware access.
/// </summary>
public sealed class DashboardModel
{
    public string Machine { get; set; } = "";
    public string Clock { get; set; } = "";
    public string Date { get; set; } = "";
    public double SessionSeconds { get; set; }
    public int SensorCount { get; set; }
    public int UpdateIntervalMs { get; set; } = 1000;
    public bool Fahrenheit { get; set; }
    public string WebServer { get; set; } = "";
    public bool WebServerOnline { get; set; }

    // Computed metrics
    public double Stress { get; set; }
    /// <summary>The four parts of the stress index, each 0..100: load (max of CPU/GPU), heat (thermal ratio), memory and fans.</summary>
    public double StressLoad { get; set; }
    public double StressHeat { get; set; }
    public double StressMemory { get; set; }
    public double StressFans { get; set; }
    public string State { get; set; } = "NOMINAL";
    public double ThermalRatio { get; set; }
    public double ThermalHeadroom { get; set; } = double.NaN;
    public string HottestName { get; set; } = "";
    public double HottestTemp { get; set; } = double.NaN;
    public double TotalPower { get; set; }
    public double OtherPower { get; set; }
    public bool OtherPowerEstimated { get; set; }
    public double SessionEnergyWh { get; set; }
    public double FanEffort { get; set; } = double.NaN;
    public double FanAvgRpm { get; set; } = double.NaN;
    public int FanCount { get; set; }

    // CPU
    public string CpuName { get; set; } = "";
    public double CpuLoad { get; set; } = double.NaN;
    public double CpuTemp { get; set; } = double.NaN;
    public double CpuTempLimit { get; set; } = 95;
    public double CpuPower { get; set; } = double.NaN;
    public double CpuClock { get; set; } = double.NaN;
    public double[] CoreLoads { get; set; } = new double[0];

    // GPU
    public string GpuName { get; set; } = "";
    public double GpuLoad { get; set; } = double.NaN;
    public double GpuTemp { get; set; } = double.NaN;
    public double GpuTempLimit { get; set; } = 90;
    public double GpuPower { get; set; } = double.NaN;
    public double GpuMemoryLoad { get; set; } = double.NaN;

    // Memory
    public double MemoryLoad { get; set; } = double.NaN;
    public double MemoryUsedGb { get; set; } = double.NaN;
    public double MemoryTotalGb { get; set; } = double.NaN;

    // Storage and network
    public int DiskCount { get; set; }
    public double DiskActivity { get; set; } = double.NaN;
    public double DiskRead { get; set; }
    public double DiskWrite { get; set; }
    public double DiskMaxTemp { get; set; } = double.NaN;
    public double NetDown { get; set; }
    public double NetUp { get; set; }

    // History, oldest first, one value per update
    public double[] PowerHistory { get; set; } = new double[0];
    public double[] StressHistory { get; set; } = new double[0];
    public double[] CpuHistory { get; set; } = new double[0];
    public double[] GpuHistory { get; set; } = new double[0];

    /// <summary>Temperatures of the main components with their limits, hottest first.</summary>
    public List<ThermalEntry> Thermals { get; set; } = new();

    /// <summary>Hardware nodes for the orbital view and the tile column.</summary>
    public List<HardwareTile> Tiles { get; set; } = new();

    /// <summary>A copy that shares the lists and arrays (used for the transitions of the Fancy dashboard).</summary>
    public DashboardModel ShallowCopy() => (DashboardModel)MemberwiseClone();
}

public sealed class HardwareTile
{
    /// <summary>cpu, gpu, ram, board, disk, net or power.</summary>
    public string Kind { get; set; } = "";
    public string Label { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Activity 0..100 (load, utilisation or normalised throughput).</summary>
    public double Load { get; set; } = double.NaN;
    public double Temp { get; set; } = double.NaN;
    public double TempLimit { get; set; } = 90;
    public double Power { get; set; } = double.NaN;
    /// <summary>Short secondary value, e.g. "4.9 GHz" or "↓ 1.2 MB/s".</summary>
    public string Detail { get; set; } = "";

    public HardwareTile Copy() => (HardwareTile)MemberwiseClone();
}

public sealed class ThermalEntry
{
    public string Name { get; set; } = "";
    public double Temp { get; set; }
    public double Limit { get; set; }
}
