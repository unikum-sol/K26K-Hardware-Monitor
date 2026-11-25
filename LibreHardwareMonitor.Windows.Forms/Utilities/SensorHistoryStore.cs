// K26K Hardware Monitor - keeps the recorded sensor history across restarts.
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
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using LibreHardwareMonitor.Hardware;

namespace LibreHardwareMonitor.Windows.Forms.Utilities;

/// <summary>
/// Saves the recorded values of all sensors in a file of its own (not in the settings) and puts them
/// back into the settings at the next start, where every sensor reads its history when it is created.
/// The last hour is kept as recorded, older values as 30 second averages; at most 24 hours.
/// </summary>
public static class SensorHistoryStore
{
    public static readonly TimeSpan MaxAge = TimeSpan.FromHours(24);
    public static readonly TimeSpan FullResolution = TimeSpan.FromHours(1);
    public static readonly TimeSpan Bucket = TimeSpan.FromSeconds(30);

    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("K26KHIST");
    private const int FormatVersion = 1;
    private const string ValuesSuffix = "/values";
    private static readonly object FileLock = new();

    /// <summary>
    /// Copies the history of all sensors, already thinned out. Call it while no sensor update runs
    /// (on the UI thread after an update), it reads the value lists of the sensors.
    /// </summary>
    public static List<(string Key, SensorValue[] Values)> Capture(IComputer computer, DateTime nowUtc)
    {
        var result = new List<(string, SensorValue[])>();
        foreach (ISensor sensor in Sensors(computer))
            Capture(sensor, nowUtc, result);
        return result;
    }

    public static List<ISensor> Sensors(IComputer computer)
    {
        var sensors = new List<ISensor>();
        computer.Accept(new SensorVisitor(sensors.Add));
        return sensors;
    }

    /// <summary>Copies the thinned history of one sensor (the autosave goes through the sensors in small steps).</summary>
    public static void Capture(ISensor sensor, DateTime nowUtc, List<(string Key, SensorValue[] Values)> result)
    {
        SensorValue[] values = Retry(() => Thin(sensor.Values, nowUtc, sensor.ValuesTimeWindow));
        if (values != null && values.Length > 0)
            result.Add((new Identifier(sensor.Identifier, "values").ToString(), values));
    }

    /// <summary>Writes the history file; first into a temporary file, so an interrupted save never destroys the last one.</summary>
    public static void Write(string fileName, List<(string Key, SensorValue[] Values)> history, DateTime nowUtc)
    {
        // encoding and compressing happen outside the lock; only the file operations are serialized
        var entries = history.Select(h => (h.Key, Data: Encode(h.Values))).ToList();

        lock (FileLock)
        {
            string temp = fileName + ".tmp";
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write))
            using (var writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                writer.Write(Magic);
                writer.Write(FormatVersion);
                writer.Write(nowUtc.ToBinary());
                writer.Write(entries.Count);
                foreach (var (key, data) in entries)
                {
                    writer.Write(key);
                    writer.Write(data.Length);
                    writer.Write(data);
                }
            }

            if (File.Exists(fileName))
            {
                try
                {
                    File.Replace(temp, fileName, null);
                    return;
                }
                catch (Exception)
                {
                    File.Delete(fileName);
                }
            }
            File.Move(temp, fileName);
        }
    }

    /// <summary>
    /// Reads the history file and hands every sensor history to the settings, where the sensors read it
    /// when they are created. Returns the number of sensors with history.
    /// </summary>
    public static int Restore(string fileName, PersistentSettings settings, DateTime nowUtc)
    {
        lock (FileLock)
        {
            if (!File.Exists(fileName))
                return 0;

            try
            {
                using var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
                using var reader = new BinaryReader(stream, Encoding.UTF8);
                if (!reader.ReadBytes(Magic.Length).SequenceEqual(Magic) || reader.ReadInt32() != FormatVersion)
                    return 0;

                DateTime saved = DateTime.FromBinary(reader.ReadInt64());
                if (nowUtc - saved > MaxAge)
                    return 0;

                int count = reader.ReadInt32();
                int restored = 0;
                for (int i = 0; i < count; i++)
                {
                    string key = reader.ReadString();
                    byte[] data = reader.ReadBytes(reader.ReadInt32());
                    if (!key.EndsWith(ValuesSuffix, StringComparison.Ordinal))
                        continue;
                    settings.SetValue(key, Convert.ToBase64String(data));
                    restored++;
                }
                return restored;
            }
            catch (Exception)
            {
                // a damaged file only costs the history, never the start
                return 0;
            }
        }
    }

    public static void Delete(string fileName)
    {
        lock (FileLock)
        {
            try
            {
                File.Delete(fileName);
                File.Delete(fileName + ".tmp");
            }
            catch (Exception)
            {
                // nothing to do
            }
        }
    }

    /// <summary>
    /// Removes the histories from the settings, so the settings file stays small. (The sensors also write
    /// their history into the settings when they close; it is kept in the history file instead.)
    /// </summary>
    public static void RemoveFromSettings(PersistentSettings settings)
    {
        foreach (string key in settings.Keys.Where(k => k.EndsWith(ValuesSuffix, StringComparison.Ordinal)).ToList())
            settings.Remove(key);
    }

    /// <summary>The last hour as recorded, older values as 30 second averages, nothing older than the time window.</summary>
    private static SensorValue[] Thin(IEnumerable<SensorValue> values, DateTime nowUtc, TimeSpan window)
    {
        TimeSpan age = window > TimeSpan.Zero && window < MaxAge ? window : MaxAge;
        DateTime oldest = nowUtc - age, full = nowUtc - FullResolution;
        var result = new List<SensorValue>();

        long bucket = long.MinValue;
        double sum = 0;
        int count = 0;
        DateTime last = default, gap = default;
        bool gapFirst = false, hasGap = false;

        void Flush()
        {
            // a gap (NaN, e.g. the program was not running) stays a gap, before or after the average
            if (hasGap && gapFirst)
                result.Add(new SensorValue(float.NaN, gap));
            if (count > 0)
                result.Add(new SensorValue((float)(sum / count), last));
            if (hasGap && !gapFirst)
                result.Add(new SensorValue(float.NaN, gap));
            sum = 0;
            count = 0;
            hasGap = false;
        }

        foreach (SensorValue value in values)
        {
            if (value.Time < oldest || value.Time > nowUtc)
                continue;

            if (value.Time >= full)
            {
                Flush();
                bucket = long.MinValue;
                result.Add(value);
                continue;
            }

            long b = value.Time.Ticks / Bucket.Ticks;
            if (b != bucket)
            {
                Flush();
                bucket = b;
            }

            if (float.IsNaN(value.Value))
            {
                if (!hasGap)
                {
                    hasGap = true;
                    gapFirst = count == 0;
                    gap = value.Time;
                }
            }
            else
            {
                sum += value.Value;
                count++;
                last = value.Time;
            }
        }
        Flush();
        return result.ToArray();
    }

    /// <summary>The format the sensors read from the settings: gzip of (time difference, value) pairs.</summary>
    private static byte[] Encode(SensorValue[] values)
    {
        using var memory = new MemoryStream();
        using (var gzip = new GZipStream(memory, CompressionMode.Compress))
        using (var buffered = new BufferedStream(gzip, 65536))
        using (var writer = new BinaryWriter(buffered))
        {
            long t = 0;
            foreach (SensorValue value in values)
            {
                long v = value.Time.ToBinary();
                writer.Write(v - t);
                t = v;
                writer.Write(value.Value);
            }
        }
        return memory.ToArray();
    }

    // the sensor update may change a value list at the same moment (e.g. during shutdown)
    private static T Retry<T>(Func<T> read) where T : class
    {
        for (int i = 0; i < 5; i++)
        {
            try
            {
                return read();
            }
            catch (InvalidOperationException)
            {
                System.Threading.Thread.Sleep(5);
            }
            catch (ArgumentException)
            {
                System.Threading.Thread.Sleep(5);
            }
        }
        return null;
    }
}
