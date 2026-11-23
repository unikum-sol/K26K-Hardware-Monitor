// K26K Hardware Monitor - computes the dashboard metrics from the sensors.
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using LibreHardwareMonitor.Hardware;

namespace LibreHardwareMonitor.Windows.Forms.Telemetry;

/// <summary>
/// Turns the raw sensors into a <see cref="DashboardModel"/>: picks the relevant sensors per device
/// and computes stress, energy distribution, thermal headroom, fan effort and session energy.
/// Call <see cref="Update"/> on the UI thread after a sensor update has finished.
/// </summary>
public sealed class SystemMetrics
{
    private const int HistoryLength = 300;

    private readonly Stopwatch _session = Stopwatch.StartNew();
    private readonly Queue<double> _power = new();
    private readonly Queue<double> _stress = new();
    private readonly Queue<double> _cpu = new();
    private readonly Queue<double> _gpu = new();
    private double _lastSeconds = double.NaN;
    private double _energyWh;
    private double _maxFanRpm;

    public DashboardModel Current { get; private set; } = new();

    public DashboardModel Update(IEnumerable<IHardware> hardware, string machine, bool fahrenheit, int intervalMs, string webServer, bool webOnline)
    {
        var m = new DashboardModel
        {
            Machine = machine,
            Clock = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            Date = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            SessionSeconds = _session.Elapsed.TotalSeconds,
            Fahrenheit = fahrenheit,
            UpdateIntervalMs = intervalMs,
            WebServer = webServer,
            WebServerOnline = webOnline
        };

        List<IHardware> all = Flatten(hardware).ToList();
        m.SensorCount = all.Sum(h => h.Sensors.Length);

        var thermal = new List<(string Name, double Temp, double Limit)>();
        int dimms = 0, drives = 0;

        // ---------- CPU ----------
        IHardware cpu = all.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
        if (cpu != null)
        {
            m.CpuName = cpu.Name;
            m.CpuLoad = Value(cpu, SensorType.Load, "CPU Total");
            m.CpuPower = First(Value(cpu, SensorType.Power, "Package"), Value(cpu, SensorType.Power, "CPU Package"), MaxOf(cpu, SensorType.Power));
            ISensor temp = Find(cpu, SensorType.Temperature, "Core (Tctl/Tdie)") ?? Find(cpu, SensorType.Temperature, "CPU Package") ??
                           Find(cpu, SensorType.Temperature, "Core Max") ?? Find(cpu, SensorType.Temperature, "Core Average") ??
                           Sensors(cpu, SensorType.Temperature).OrderByDescending(s => s.Value ?? 0).FirstOrDefault();
            m.CpuTemp = Read(temp);
            m.CpuTempLimit = Limit(temp, 95);
            m.CoreLoads = Sensors(cpu, SensorType.Load)
                          .Where(s => s.Name.StartsWith("CPU Core #", StringComparison.Ordinal))
                          .Select(s => Clamp(Read(s), 0, 100)).ToArray();
            double[] clocks = Sensors(cpu, SensorType.Clock).Where(s => s.Name.StartsWith("Core #", StringComparison.Ordinal) || s.Name.StartsWith("P-Core", StringComparison.Ordinal) || s.Name.StartsWith("E-Core", StringComparison.Ordinal))
                              .Select(Read).Where(v => !double.IsNaN(v) && v > 0).ToArray();
            m.CpuClock = clocks.Length > 0 ? clocks.Max() : double.NaN;
            if (!double.IsNaN(m.CpuTemp))
                thermal.Add(("CPU", m.CpuTemp, m.CpuTempLimit));
        }

        // ---------- GPU (the one that draws most power, i.e. the discrete card) ----------
        IHardware gpu = all.Where(h => h.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel)
                           .OrderByDescending(h => Nz(MaxOf(h, SensorType.Power)))
                           .ThenByDescending(h => Nz(Value(h, SensorType.Load, "GPU Core")))
                           .FirstOrDefault();
        if (gpu != null)
        {
            m.GpuName = gpu.Name;
            m.GpuLoad = Value(gpu, SensorType.Load, "GPU Core");
            m.GpuMemoryLoad = First(Value(gpu, SensorType.Load, "GPU Memory"), Value(gpu, SensorType.Load, "GPU Memory Controller"));
            m.GpuPower = MaxOf(gpu, SensorType.Power);
            ISensor temp = Find(gpu, SensorType.Temperature, "GPU Core") ?? Sensors(gpu, SensorType.Temperature).FirstOrDefault();
            m.GpuTemp = Read(temp);
            m.GpuTempLimit = Limit(temp, 90);
            if (!double.IsNaN(m.GpuTemp))
                thermal.Add(("GPU", m.GpuTemp, m.GpuTempLimit));
        }

        // ---------- Memory ----------
        IHardware memory = all.FirstOrDefault(h => h.HardwareType == HardwareType.Memory && Find(h, SensorType.Load, "Memory") != null);
        if (memory != null)
        {
            m.MemoryLoad = Value(memory, SensorType.Load, "Memory");
            double used = Value(memory, SensorType.Data, "Memory Used");
            double free = Value(memory, SensorType.Data, "Memory Available");
            // Data sensors report bytes.
            m.MemoryUsedGb = used / 1073741824.0;
            m.MemoryTotalGb = !double.IsNaN(used) && !double.IsNaN(free) ? (used + free) / 1073741824.0 : double.NaN;
        }
        dimms = all.Count(h => h.HardwareType == HardwareType.Memory && h.Name.IndexOf("Memory", StringComparison.OrdinalIgnoreCase) < 0);

        // ---------- Storage ----------
        double diskActivity = double.NaN, diskTemp = double.NaN;
        foreach (IHardware disk in all.Where(h => h.HardwareType == HardwareType.Storage && h.Sensors.Length > 0))
        {
            drives++;
            diskActivity = Max(diskActivity, Value(disk, SensorType.Load, "Total Activity"));
            m.DiskRead += Nz(Value(disk, SensorType.Throughput, "Read Rate"));
            m.DiskWrite += Nz(Value(disk, SensorType.Throughput, "Write Rate"));
            ISensor t = Sensors(disk, SensorType.Temperature).FirstOrDefault(s => !IsThreshold(s.Name) && Read(s) > 0);
            double temp = Read(t);
            if (!double.IsNaN(temp))
            {
                diskTemp = Max(diskTemp, temp);
                thermal.Add((disk.Name, temp, Limit(t, 75)));
            }
        }
        m.DiskCount = drives;
        m.DiskActivity = diskActivity;
        m.DiskMaxTemp = diskTemp;

        // ---------- Network (physical adapters, not the filter drivers) ----------
        foreach (IHardware nic in all.Where(h => h.HardwareType == HardwareType.Network && IsPhysicalAdapter(h.Name)))
        {
            m.NetDown += Nz(Value(nic, SensorType.Throughput, "Download Speed"));
            m.NetUp += Nz(Value(nic, SensorType.Throughput, "Upload Speed"));
        }

        // ---------- Fans ----------
        double[] rpms = all.SelectMany(h => Sensors(h, SensorType.Fan)).Select(Read).Where(v => v > 0).ToArray();
        double[] controls = all.Where(h => h.HardwareType is HardwareType.SuperIO or HardwareType.Motherboard or HardwareType.Cooler or HardwareType.EmbeddedController)
                               .SelectMany(h => Sensors(h, SensorType.Control)).Select(Read).Where(v => v > 0).ToArray();
        m.FanCount = rpms.Length;
        if (rpms.Length > 0)
        {
            m.FanAvgRpm = rpms.Average();
            _maxFanRpm = Math.Max(_maxFanRpm, rpms.Max());
        }
        m.FanEffort = controls.Length > 0 ? controls.Average() : rpms.Length > 0 && _maxFanRpm > 0 ? rpms.Average() / _maxFanRpm * 100 : double.NaN;

        // ---------- Board ----------
        IHardware[] board = all.Where(h => h.HardwareType is HardwareType.Motherboard or HardwareType.SuperIO or HardwareType.EmbeddedController).ToArray();
        double boardTemp = board.SelectMany(h => Sensors(h, SensorType.Temperature)).Select(Read).Where(v => v > 0 && v < 120).DefaultIfEmpty(double.NaN).Max();

        // ---------- Energy distribution ----------
        double cpuP = Nz(m.CpuPower), gpuP = Nz(m.GpuPower);
        IHardware psu = all.FirstOrDefault(h => h.HardwareType is HardwareType.Psu or HardwareType.PowerMonitor && Sensors(h, SensorType.Power).Any());
        double measuredTotal = psu != null ? MaxOf(psu, SensorType.Power) : double.NaN;
        if (!double.IsNaN(measuredTotal) && measuredTotal > cpuP + gpuP)
        {
            m.OtherPower = measuredTotal - cpuP - gpuP;
            m.OtherPowerEstimated = false;
        }
        else
        {
            // Rough estimate for board, chipset, memory, drives and fans.
            m.OtherPower = 25 + 3 * dimms + 4 * drives + 1.5 * m.FanCount;
            m.OtherPowerEstimated = true;
        }
        m.TotalPower = cpuP + gpuP + m.OtherPower;

        double seconds = _session.Elapsed.TotalSeconds;
        if (!double.IsNaN(_lastSeconds))
            _energyWh += m.TotalPower * (seconds - _lastSeconds) / 3600.0;
        _lastSeconds = seconds;
        m.SessionEnergyWh = _energyWh;

        // ---------- Thermal headroom ----------
        if (thermal.Count > 0)
        {
            var hottest = thermal.OrderByDescending(t => (t.Temp - 30) / Math.Max(1, t.Limit - 30)).First();
            m.ThermalRatio = Clamp((hottest.Temp - 30) / Math.Max(1, hottest.Limit - 30), 0, 1);
            m.ThermalHeadroom = thermal.Min(t => t.Limit - t.Temp);
            m.HottestName = hottest.Name;
            m.HottestTemp = hottest.Temp;
        }

        // ---------- Stress index: weighted mix, biased toward the worst component ----------
        double cpuL = Nz(m.CpuLoad), gpuL = Nz(m.GpuLoad), memL = Nz(m.MemoryLoad), fanL = Nz(m.FanEffort), thermL = m.ThermalRatio * 100;
        double weighted = 0.30 * cpuL + 0.25 * gpuL + 0.25 * thermL + 0.10 * memL + 0.10 * fanL;
        double worst = Math.Max(cpuL, Math.Max(gpuL, thermL));
        m.Stress = Clamp(0.5 * worst + 0.5 * weighted, 0, 100);
        m.State = m.ThermalRatio >= 0.85 ? "THERMAL" : m.Stress >= 60 ? "LOAD" : "NOMINAL";

        // ---------- History ----------
        m.PowerHistory = Push(_power, m.TotalPower);
        m.StressHistory = Push(_stress, m.Stress);
        m.CpuHistory = Push(_cpu, cpuL);
        m.GpuHistory = Push(_gpu, gpuL);

        // ---------- Tiles for the orbital view ----------
        if (cpu != null)
            m.Tiles.Add(new HardwareTile { Kind = "cpu", Label = "CPU", Name = cpu.Name, Load = m.CpuLoad, Temp = m.CpuTemp, TempLimit = m.CpuTempLimit, Power = m.CpuPower, Detail = double.IsNaN(m.CpuClock) ? "" : (m.CpuClock / 1000).ToString("0.00", CultureInfo.InvariantCulture) + " GHz" });
        if (gpu != null)
            m.Tiles.Add(new HardwareTile { Kind = "gpu", Label = "GPU", Name = gpu.Name, Load = m.GpuLoad, Temp = m.GpuTemp, TempLimit = m.GpuTempLimit, Power = m.GpuPower, Detail = double.IsNaN(m.GpuMemoryLoad) ? "" : "VRAM " + m.GpuMemoryLoad.ToString("0", CultureInfo.InvariantCulture) + " %" });
        if (memory != null)
            m.Tiles.Add(new HardwareTile { Kind = "ram", Label = "RAM", Name = "Memory", Load = m.MemoryLoad, Detail = double.IsNaN(m.MemoryUsedGb) ? "" : m.MemoryUsedGb.ToString("0.0", CultureInfo.InvariantCulture) + " / " + m.MemoryTotalGb.ToString("0", CultureInfo.InvariantCulture) + " GB" });
        if (board.Length > 0)
            m.Tiles.Add(new HardwareTile { Kind = "board", Label = "BOARD", Name = board[0].Name, Load = m.FanEffort, Temp = boardTemp, TempLimit = 80, Detail = double.IsNaN(m.FanAvgRpm) ? "" : m.FanAvgRpm.ToString("0", CultureInfo.InvariantCulture) + " RPM" });
        if (drives > 0)
            m.Tiles.Add(new HardwareTile { Kind = "disk", Label = "DISK", Name = drives + " drives", Load = m.DiskActivity, Temp = m.DiskMaxTemp, TempLimit = 75, Detail = Rate(m.DiskRead + m.DiskWrite) });
        if (all.Any(h => h.HardwareType == HardwareType.Network))
            m.Tiles.Add(new HardwareTile { Kind = "net", Label = "NET", Name = "Network", Load = Clamp(NetScale(m.NetDown + m.NetUp), 0, 100), Detail = "↓ " + Rate(m.NetDown) });
        if (psu != null)
            m.Tiles.Add(new HardwareTile { Kind = "power", Label = "PSU", Name = psu.Name, Power = measuredTotal, Load = double.NaN });

        Current = m;
        return m;
    }

    public static string Rate(double bytesPerSecond)
    {
        if (double.IsNaN(bytesPerSecond))
            return "-";
        if (bytesPerSecond >= 1073741824)
            return (bytesPerSecond / 1073741824).ToString("0.0", CultureInfo.InvariantCulture) + " GB/s";
        if (bytesPerSecond >= 1048576)
            return (bytesPerSecond / 1048576).ToString("0.0", CultureInfo.InvariantCulture) + " MB/s";
        if (bytesPerSecond >= 1024)
            return (bytesPerSecond / 1024).ToString("0", CultureInfo.InvariantCulture) + " KB/s";
        return bytesPerSecond.ToString("0", CultureInfo.InvariantCulture) + " B/s";
    }

    // 1 KB/s = 0 %, 125 MB/s (1 Gbit/s) = 100 %, logarithmic in between.
    private static double NetScale(double bytesPerSecond)
    {
        if (bytesPerSecond <= 1024)
            return 0;
        return (Math.Log10(bytesPerSecond) - 3) / (Math.Log10(125_000_000) - 3) * 100;
    }

    private static bool IsThreshold(string name)
    {
        return name.IndexOf("Warning", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Critical", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Limit", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Threshold", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static double[] Push(Queue<double> queue, double value)
    {
        queue.Enqueue(double.IsNaN(value) ? 0 : value);
        while (queue.Count > HistoryLength)
            queue.Dequeue();
        return queue.ToArray();
    }

    private static bool IsPhysicalAdapter(string name)
    {
        return name.IndexOf("Filter", StringComparison.OrdinalIgnoreCase) < 0 &&
               name.IndexOf("Scheduler", StringComparison.OrdinalIgnoreCase) < 0 &&
               name.IndexOf("-0000", StringComparison.Ordinal) < 0 &&
               name.IndexOf("Bluetooth", StringComparison.OrdinalIgnoreCase) < 0;
    }

    private static IEnumerable<IHardware> Flatten(IEnumerable<IHardware> hardware)
    {
        foreach (IHardware h in hardware)
        {
            yield return h;
            foreach (IHardware sub in Flatten(h.SubHardware))
                yield return sub;
        }
    }

    private static IEnumerable<ISensor> Sensors(IHardware hardware, SensorType type)
    {
        return hardware.Sensors.Where(s => s.SensorType == type);
    }

    private static ISensor Find(IHardware hardware, SensorType type, string name)
    {
        return hardware.Sensors.FirstOrDefault(s => s.SensorType == type && string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private static double Value(IHardware hardware, SensorType type, string name)
    {
        return Read(Find(hardware, type, name));
    }

    private static double MaxOf(IHardware hardware, SensorType type)
    {
        return Sensors(hardware, type).Select(Read).Where(v => !double.IsNaN(v)).DefaultIfEmpty(double.NaN).Max();
    }

    private static double Read(ISensor sensor)
    {
        float? v = sensor?.Value;
        if (!v.HasValue || float.IsNaN(v.Value) || float.IsInfinity(v.Value))
            return double.NaN;

        // Without driver access some sensors report 0; treat that as "no reading" for temperature and power.
        if (sensor.SensorType is SensorType.Temperature && (v.Value <= 0 || v.Value >= 150))
            return double.NaN;
        if (sensor.SensorType is SensorType.Power && v.Value <= 0.05f)
            return double.NaN;
        return v.Value;
    }

    private static double Limit(ISensor sensor, double fallback)
    {
        float? limit = (sensor as ICriticalSensorLimits)?.CriticalHighLimit ?? (sensor as ISensorLimits)?.HighLimit;
        return limit.HasValue && limit.Value > 40 && limit.Value < 150 ? limit.Value : fallback;
    }

    private static double First(params double[] values)
    {
        foreach (double v in values)
        {
            if (!double.IsNaN(v))
                return v;
        }
        return double.NaN;
    }

    private static double Max(double a, double b) => double.IsNaN(a) ? b : double.IsNaN(b) ? a : Math.Max(a, b);
    private static double Nz(double v) => double.IsNaN(v) ? 0 : v;
    private static double Clamp(double v, double min, double max) => double.IsNaN(v) ? min : Math.Max(min, Math.Min(max, v));
}
