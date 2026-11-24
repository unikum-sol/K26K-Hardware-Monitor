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
using System.Windows.Forms;
using LibreHardwareMonitor.Windows.Forms.Telemetry;
using LibreHardwareMonitor.Windows.Forms.UI.SciFi;
using SkiaSharp;

namespace LibreHardwareMonitor.Windows.Forms.UI.Fancy;

/// <summary>
/// Shows the Fancy dashboard. Everything that only changes with the data is drawn once per update
/// into a cached bitmap; each animation frame copies that bitmap and draws only the moving parts
/// (particles and pulse rings) on top. This keeps the CPU load low.
/// </summary>
public sealed class FancyDashboard : SkiaView
{
    private const double BootSeconds = 2.6;

    private readonly Timer _timer = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly FancyOptions _options = new();
    private DashboardModel _model = new();
    private SKBitmap _cache;
    private bool _cacheDirty = true;
    private double _openedAt;
    private SKRect _exit;

    public event EventHandler ExitRequested;

    /// <summary>A window button in the top strip was clicked.</summary>
    public event EventHandler<WindowButton> CaptionCommand;

    public FancyDashboard()
    {
        Dock = DockStyle.Fill;
        _timer.Interval = 40;
        _timer.Tick += (s, e) =>
        {
            // during the boot sequence everything changes; afterwards only the orbital view moves
            if (_options.SinceOpen < BootSeconds + 0.2)
            {
                Invalidate();
                return;
            }
            SKRect area = FancyRenderer.MotionArea(ClientSize.Width, ClientSize.Height);
            Invalidate(System.Drawing.Rectangle.FromLTRB((int)area.Left, (int)area.Top, (int)Math.Ceiling(area.Right), (int)Math.Ceiling(area.Bottom)));
        };
    }

    public bool ReducedMotion
    {
        get => _options.ReducedMotion;
        set
        {
            _options.ReducedMotion = value;
            _cacheDirty = true;
            UpdateTimer();
            Invalidate();
        }
    }

    public bool Glow
    {
        get => _options.Glow;
        set
        {
            _options.Glow = value;
            _cacheDirty = true;
            Invalidate();
        }
    }

    public DashboardModel Model
    {
        get => _model;
        set
        {
            _model = value ?? new DashboardModel();
            _cacheDirty = true;
            Invalidate();
        }
    }

    /// <summary>Shows the window buttons in the top strip (fullscreen: without maximize).</summary>
    public void SetCaption(bool show, bool maximize, bool maximized, bool active)
    {
        if (_options.CaptionButtons == show && _options.CaptionMaximize == maximize && _options.Maximized == maximized && _options.WindowActive == active)
            return;
        _options.CaptionButtons = show;
        _options.CaptionMaximize = maximize;
        _options.Maximized = maximized;
        _options.WindowActive = active;
        _cacheDirty = true;
        Invalidate();
    }

    /// <summary>True on the top strip outside the window buttons: that part moves the window like a title bar.</summary>
    public bool IsCaptionArea(System.Drawing.Point p) => p.Y < _options.TopStripHeight && CaptionAt(p) == null;

    private WindowButton? CaptionAt(System.Drawing.Point p)
    {
        foreach (var (kind, rect) in _options.CaptionRects)
        {
            if (rect.Contains(p.X, p.Y))
                return kind;
        }
        return null;
    }

    private void SetCaptionHover(WindowButton? hover, bool pressed)
    {
        if (hover == _options.CaptionHover && pressed == _options.CaptionPressed)
            return;
        _options.CaptionHover = hover;
        _options.CaptionPressed = pressed;
        _cacheDirty = true;
        Invalidate(System.Drawing.Rectangle.FromLTRB(0, 0, ClientSize.Width, (int)Math.Ceiling(_options.TopStripHeight) + 1));
    }

    /// <summary>Restarts the boot sequence; call when the dashboard is shown.</summary>
    public void Restart()
    {
        _openedAt = _clock.Elapsed.TotalSeconds;
        _cacheDirty = true;
        UpdateTimer();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        UpdateTimer();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        _cacheDirty = true;
    }

    private void UpdateTimer()
    {
        // with reduced motion nothing moves, so the data updates alone redraw the view
        _timer.Enabled = Visible && !ReducedMotion && FindForm()?.WindowState != FormWindowState.Minimized;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        bool hover = _exit.Contains(e.X, e.Y);
        if (hover != _options.ExitHover)
        {
            _options.ExitHover = hover;
            Cursor = hover ? Cursors.Hand : Cursors.Default;
            _cacheDirty = true;
            Invalidate();
        }
        SetCaptionHover(CaptionAt(e.Location), _options.CaptionPressed && e.Button == MouseButtons.Left);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        SetCaptionHover(null, false);
        if (_options.ExitHover)
        {
            _options.ExitHover = false;
            _cacheDirty = true;
            Invalidate();
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
            SetCaptionHover(CaptionAt(e.Location), CaptionAt(e.Location) != null);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        WindowButton? at = CaptionAt(e.Location);
        bool clicked = e.Button == MouseButtons.Left && _options.CaptionPressed && at != null && at == _options.CaptionHover;
        SetCaptionHover(at, false);
        if (clicked)
        {
            SetCaptionHover(null, false);
            CaptionCommand?.Invoke(this, at.Value);
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
        _options.SinceOpen = now - _openedAt;

        // the boot sequence changes everything, so it is drawn completely each frame
        if (!ReducedMotion && _options.SinceOpen < BootSeconds)
        {
            _options.Layer = FancyLayer.All;
            _exit = FancyRenderer.Render(canvas, width, height, scale, _model, now, _options);
            _cacheDirty = true;
            return;
        }

        if (_cache == null || _cache.Width != width || _cache.Height != height)
        {
            _cache?.Dispose();
            _cache = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
            _cacheDirty = true;
        }

        if (_cacheDirty)
        {
            using var cacheCanvas = new SKCanvas(_cache);
            _options.Layer = FancyLayer.Static;
            _exit = FancyRenderer.Render(cacheCanvas, width, height, scale, _model, now, _options);
            _cacheDirty = false;
        }

        canvas.DrawBitmap(_cache, 0, 0);

        if (!ReducedMotion)
        {
            _options.Layer = FancyLayer.Motion;
            FancyRenderer.Render(canvas, width, height, scale, _model, now, _options);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _cache?.Dispose();
        }
        base.Dispose(disposing);
    }
}
