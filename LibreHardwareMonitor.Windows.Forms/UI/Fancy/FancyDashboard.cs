// K26K Hardware Monitor - Fancy dashboard control with its own animation loop.
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
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using LibreHardwareMonitor.Windows.Forms.Telemetry;
using LibreHardwareMonitor.Windows.Forms.UI.SciFi;
using SkiaSharp;

namespace LibreHardwareMonitor.Windows.Forms.UI.Fancy;

public sealed class FancyDashboard : SkiaView
{
    private readonly Timer _timer = new();
    private readonly Stopwatch _clock = new();
    private readonly FancyOptions _options = new();
    private DashboardModel _target = new();
    private DashboardModel _shown = new();
    private double _lastFrame;
    private double _openedAt;
    private SKRect _exit;
    private int _slowFrames;

    public event EventHandler ExitRequested;

    public FancyDashboard()
    {
        Dock = DockStyle.Fill;
        _timer.Interval = 33;
        _timer.Tick += (s, e) => Invalidate();
        _clock.Start();
    }

    public bool ReducedMotion
    {
        get => _options.ReducedMotion;
        set
        {
            _options.ReducedMotion = value;
            UpdateTimer();
            Invalidate();
        }
    }

    public DashboardModel Model
    {
        get => _target;
        set
        {
            _target = value ?? new DashboardModel();
            if (_shown.Tiles.Count != _target.Tiles.Count || ReducedMotion)
                _shown = Clone(_target);
            else
                CopyStatic(_target, _shown);
            if (ReducedMotion)
                Invalidate();
        }
    }

    /// <summary>Restarts the boot sequence; call when the dashboard is shown.</summary>
    public void Restart()
    {
        _openedAt = _clock.Elapsed.TotalSeconds;
        _shown = Clone(_target);
        UpdateTimer();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        UpdateTimer();
    }

    private void UpdateTimer()
    {
        // 2 fps is enough for the clock and values when motion is reduced
        _timer.Interval = ReducedMotion ? 500 : _slowFrames > 20 ? 66 : 33;
        _timer.Enabled = Visible && FindForm()?.WindowState != FormWindowState.Minimized;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        bool hover = _exit.Contains(e.X, e.Y);
        if (hover != _options.ExitHover)
        {
            _options.ExitHover = hover;
            Cursor = hover ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button == MouseButtons.Left && _exit.Contains(e.X, e.Y))
            ExitRequested?.Invoke(this, EventArgs.Empty);
    }

    protected override void Render(SKCanvas canvas, int width, int height, float scale)
    {
        double now = _clock.Elapsed.TotalSeconds;
        double dt = Math.Min(0.25, now - _lastFrame);
        _lastFrame = now;
        Ease(dt);

        _options.SinceOpen = now - _openedAt;
        long start = Stopwatch.GetTimestamp();
        _exit = FancyRenderer.Render(canvas, width, height, scale, _shown, now, _options);
        double ms = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;

        // drop to 15 fps if frames take too long (slow CPU, huge window)
        if (ms > 22)
            _slowFrames++;
        else if (_slowFrames > 0)
            _slowFrames--;
        if (_slowFrames == 21 || _slowFrames == 0)
            UpdateTimer();
    }

    // Values glide toward the latest sample instead of jumping once per second.
    private void Ease(double dt)
    {
        if (ReducedMotion)
            return;
        double f = 1 - Math.Exp(-dt * 5);
        _shown.Stress = Lerp(_shown.Stress, _target.Stress, f);
        for (int i = 0; i < _shown.Tiles.Count && i < _target.Tiles.Count; i++)
        {
            _shown.Tiles[i].Load = Lerp(_shown.Tiles[i].Load, _target.Tiles[i].Load, f);
            _shown.Tiles[i].Temp = Lerp(_shown.Tiles[i].Temp, _target.Tiles[i].Temp, f);
        }
        if (_shown.CoreLoads.Length == _target.CoreLoads.Length)
        {
            for (int i = 0; i < _shown.CoreLoads.Length; i++)
                _shown.CoreLoads[i] = Lerp(_shown.CoreLoads[i], _target.CoreLoads[i], f);
        }
        else
        {
            _shown.CoreLoads = (double[])_target.CoreLoads.Clone();
        }
    }

    private static double Lerp(double a, double b, double f)
    {
        if (double.IsNaN(a))
            return b;
        if (double.IsNaN(b))
            return b;
        return a + (b - a) * f;
    }

    private static DashboardModel Clone(DashboardModel m)
    {
        string json = System.Text.Json.JsonSerializer.Serialize(m, SnapshotJson.Options);
        return System.Text.Json.JsonSerializer.Deserialize<DashboardModel>(json, SnapshotJson.Options);
    }

    // Everything except the eased values is taken over directly.
    private static void CopyStatic(DashboardModel from, DashboardModel to)
    {
        double stress = to.Stress;
        double[] cores = to.CoreLoads;
        var loads = to.Tiles.Select(x => (x.Load, x.Temp)).ToArray();
        DashboardModel copy = Clone(from);
        foreach (var p in typeof(DashboardModel).GetProperties())
            p.SetValue(to, p.GetValue(copy));
        to.Stress = stress;
        to.CoreLoads = cores.Length == from.CoreLoads.Length ? cores : to.CoreLoads;
        for (int i = 0; i < to.Tiles.Count && i < loads.Length; i++)
        {
            to.Tiles[i].Load = loads[i].Load;
            to.Tiles[i].Temp = loads[i].Temp;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _timer.Dispose();
        base.Dispose(disposing);
    }
}
