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
    private DashboardModel _previous;
    private bool _hasModel;
    private double _arrivedAt;
    private double _period;

    // a part gets a few more frames after its transition, so it surely ends on the exact new value
    private const double SettleSeconds = 0.08;

    public event EventHandler ExitRequested;

    /// <summary>A window button in the top strip was clicked.</summary>
    public event EventHandler<WindowButton> CaptionCommand;

    public FancyDashboard()
    {
        Dock = DockStyle.Fill;
        _timer.Interval = 30;
        _timer.Tick += (s, e) =>
        {
            // during the boot sequence everything changes; afterwards only the orbital view moves
            if (_options.SinceOpen < BootSeconds + 0.2)
            {
                Invalidate();
                return;
            }
            Invalidate(ToRectangle(FancyRenderer.MotionArea(ClientSize.Width, ClientSize.Height)));
            // the parts that are gliding to new values or scrolling right now
            double since = _clock.Elapsed.TotalSeconds - _arrivedAt;
            foreach (FancyRegion region in _options.Regions)
            {
                if (IsActive(region, since))
                    Invalidate(ToRectangle(region.Rect));
            }
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
            // the sensors still update once per interval; the old model is kept, so the dashboard can glide
            // from the old to the new values until the next update
            double now = _clock.Elapsed.TotalSeconds;
            DashboardModel next = value ?? new DashboardModel();
            double interval = Math.Max(0.1, next.UpdateIntervalMs / 1000.0);
            double gap = now - _arrivedAt;
            // measured time between updates (it runs a little longer than the interval); the charts scroll in that time
            _period = _hasModel && gap > interval * 0.5 && gap < interval * 2 ? (_period > 0 ? _period * 0.7 + gap * 0.3 : gap) : interval;
            _previous = _hasModel ? _model : null;
            _model = next;
            _hasModel = true;
            _arrivedAt = now;
            _cacheDirty = true;
            Invalidate();
        }
    }

    private static bool IsActive(FancyRegion region, double since) => since >= region.Start && since <= region.End + SettleSeconds;

    private static System.Drawing.Rectangle ToRectangle(SKRect r) =>
        System.Drawing.Rectangle.FromLTRB((int)Math.Floor(r.Left), (int)Math.Floor(r.Top), (int)Math.Ceiling(r.Right), (int)Math.Ceiling(r.Bottom));

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
        _options.Previous = _previous;
        _options.SinceUpdate = now - _arrivedAt;
        _options.ScrollPeriod = _period > 0 ? _period : 1;

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
        else if (!ReducedMotion)
        {
            // only the parts in transition are drawn again (clipped); the rest of the cached frame stays
            using var clip = new SKPath();
            foreach (FancyRegion region in _options.Regions)
            {
                if (IsActive(region, _options.SinceUpdate))
                    clip.AddRect(region.Rect);
            }
            if (!clip.IsEmpty)
            {
                using var cacheCanvas = new SKCanvas(_cache);
                cacheCanvas.ClipPath(clip);
                _options.Layer = FancyLayer.Static;
                _exit = FancyRenderer.Render(cacheCanvas, width, height, scale, _model, now, _options);
            }
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
