// K26K Hardware Monitor - keep the recorded sensor history after a restart (menu Options).
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
using System.Threading.Tasks;
using System.Windows.Forms;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Windows.Forms.Utilities;

namespace LibreHardwareMonitor.Windows.Forms.UI;

public sealed partial class MainForm
{
    private const string KeepHistoryKey = "keepSensorHistory";

    // besides the save on exit: a crash or a power cut costs at most this much of the history
    private static readonly TimeSpan HistoryAutosave = TimeSpan.FromMinutes(10);

    // the autosave copies this many sensors per update, so a full day of history never stalls the window
    private const int HistorySensorsPerUpdate = 40;

    private UserOption _keepHistory;
    private List<ISensor> _historyQueue;
    private int _historyNext;
    private DateTime _historyTime;
    private List<(string Key, SensorValue[] Values)> _historyCaptured;
    private DateTime _lastHistorySave = DateTime.UtcNow;
    private volatile bool _historyWriting;
    private readonly object _historyExitLock = new();
    private bool _historySavedOnExit;

    private static string HistoryFile => Path.ChangeExtension(Application.ExecutablePath, ".history");

    private bool KeepHistory => _keepHistory?.Value ?? _settings.GetValue(KeepHistoryKey, true);

    /// <summary>
    /// Right after the settings are loaded and before the sensors exist: every sensor reads its history from the
    /// settings when it is created, so the saved history is put there.
    /// </summary>
    private void RestoreSensorHistory()
    {
        if (_settings.GetValue(KeepHistoryKey, true))
            SensorHistoryStore.Restore(HistoryFile, _settings, DateTime.UtcNow);
        else
            SensorHistoryStore.RemoveFromSettings(_settings); // also histories older versions kept in the settings file
    }

    private void InitializeHistory()
    {
        var item = new ToolStripMenuItem("Keep History After Restart")
        {
            ToolTipText = "Keeps the recorded sensor values (up to 24 hours, see Sensor Values Time Window) when the program" + Environment.NewLine +
                          "closes or Windows restarts, and every 10 minutes. The last hour is kept as recorded, older values as" + Environment.NewLine +
                          "30 second averages. Off: the recording starts empty after each start."
        };
        int index = optionsMenuItem.DropDownItems.IndexOf(sensorValuesTimeWindowMenuItem);
        optionsMenuItem.DropDownItems.Insert(index + 1, item);

        _keepHistory = new UserOption(KeepHistoryKey, true, item, _settings);
        bool wasOn = _keepHistory.Value;
        // (the handler also runs once right now, when it is added)
        _keepHistory.Changed += delegate
        {
            if (!_keepHistory.Value)
                SensorHistoryStore.Delete(HistoryFile);
            else if (!wasOn)
                _lastHistorySave = DateTime.MinValue; // switched on: save soon
            wasOn = _keepHistory.Value;
        };

        backgroundUpdater.RunWorkerCompleted += delegate { AutosaveHistory(); };
    }

    // after a sensor update, on the UI thread: no update changes the value lists while they are copied
    private void AutosaveHistory()
    {
        if (_historyQueue == null)
        {
            DateTime now = DateTime.UtcNow;
            if (!KeepHistory || _historyWriting || now - _lastHistorySave < HistoryAutosave)
                return;
            _lastHistorySave = now;
            _historyTime = now;
            _historyQueue = SensorHistoryStore.Sensors(_computer);
            _historyNext = 0;
            _historyCaptured = new List<(string, SensorValue[])>();
        }

        int end = Math.Min(_historyQueue.Count, _historyNext + HistorySensorsPerUpdate);
        for (; _historyNext < end; _historyNext++)
            SensorHistoryStore.Capture(_historyQueue[_historyNext], _historyTime, _historyCaptured);
        if (_historyNext < _historyQueue.Count)
            return;

        var history = _historyCaptured;
        DateTime time = _historyTime;
        _historyQueue = null;
        _historyCaptured = null;
        string file = HistoryFile;
        _historyWriting = true;
        Task.Run(() =>
        {
            try
            {
                SensorHistoryStore.Write(file, history, time);
                if (!KeepHistory)
                    SensorHistoryStore.Delete(file);
            }
            catch (Exception)
            {
                // e.g. no write access: the history is simply not kept
            }
            finally
            {
                _historyWriting = false;
            }
        });
    }

    /// <summary>On exit and when Windows ends the session, before the hardware closes (the sensors still have their values).</summary>
    private void SaveSensorHistoryOnExit()
    {
        lock (_historyExitLock)
        {
            if (_historySavedOnExit || _computer == null)
                return;
            _historySavedOnExit = true;

            try
            {
                if (KeepHistory)
                {
                    DateTime now = DateTime.UtcNow;
                    SensorHistoryStore.Write(HistoryFile, SensorHistoryStore.Capture(_computer, now), now);
                }
                else
                {
                    SensorHistoryStore.Delete(HistoryFile);
                }
            }
            catch (Exception)
            {
                // never block closing or the Windows shutdown
            }
        }
    }
}
