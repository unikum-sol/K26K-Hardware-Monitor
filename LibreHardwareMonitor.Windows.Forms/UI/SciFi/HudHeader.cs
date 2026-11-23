// K26K Hardware Monitor - Sci-Fi header strip.
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
using System.Windows.Forms;
using LibreHardwareMonitor.Windows.Forms.Telemetry;
using SkiaSharp;

namespace LibreHardwareMonitor.Windows.Forms.UI.SciFi;

/// <summary>
/// Header of the Sci-Fi main window: title block, a ruler with the most important live values,
/// the system state and the button that opens the Fancy dashboard.
/// </summary>
public sealed class HudHeader : SkiaView
{
    public static readonly SKColor Bg = Fui.Hex("#050810");
    public static readonly SKColor Panel = Fui.Hex("#0A1218");
    public static readonly SKColor Text = Fui.Hex("#D8E8F0");
    public static readonly SKColor Dim = Fui.Hex("#6A8292");
    public static readonly SKColor Accent = Fui.Hex("#00E0E8");
    public static readonly SKColor Data = Fui.Hex("#F0E040");
    public static readonly SKColor Cool = Fui.Hex("#F0A030");
    public static readonly SKColor Alarm = Fui.Hex("#FF40A0");
    public static readonly SKColor Rule = Fui.Hex("#1C2C3A");

    private SKRect _button;
    private bool _hover;
    private DashboardModel _model = new();

    public event EventHandler FancyRequested;

    public HudHeader()
    {
        Dock = DockStyle.Top;
        Height = (int)(64 * UiScale);
    }

    public DashboardModel Model
    {
        get => _model;
        set
        {
            _model = value ?? new DashboardModel();
            Invalidate();
        }
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        Height = (int)(64 * UiScale);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        bool hover = _button.Contains(e.X, e.Y);
        if (hover != _hover)
        {
            _hover = hover;
            Cursor = hover ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hover)
        {
            _hover = false;
            Invalidate();
        }
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button == MouseButtons.Left && _button.Contains(e.X, e.Y))
            FancyRequested?.Invoke(this, EventArgs.Empty);
    }

    protected override void Render(SKCanvas c, int width, int height, float k)
    {
        Draw(c, width, height, k, _model, _hover, out _button);
    }

    /// <summary>Static so that the snapshot renderer can draw the header without a window.</summary>
    public static void Draw(SKCanvas c, int width, int height, float k, DashboardModel m, bool hover, out SKRect button)
    {
        c.Clear(Bg);

        // faint vertical grid
        using (SKPaint grid = Fui.Stroke(Accent.WithAlpha(0.045f)))
        {
            for (float x = 0; x < width; x += 24 * k)
                c.DrawLine(x, 0, x, height, grid);
        }

        using (SKPaint rule = Fui.Stroke(Rule))
            c.DrawLine(0, height - 0.5f, width, height - 0.5f, rule);
        using (SKPaint tick = Fui.Stroke(Accent, 2 * k))
            c.DrawLine(0, height - k, 64 * k, height - k, tick);

        // ---------- title block ----------
        float x0 = 18 * k;
        float midY = height / 2f;
        using (var diamond = new SKPath())
        {
            float d = 9 * k, cx = x0 + d, cy = midY - 4 * k;
            diamond.MoveTo(cx, cy - d);
            diamond.LineTo(cx + d, cy);
            diamond.LineTo(cx, cy + d);
            diamond.LineTo(cx - d, cy);
            diamond.Close();
            using SKPaint p = Fui.Stroke(Accent, 1.4f * k);
            c.DrawPath(diamond, p);
            using SKPaint core = Fui.Fill(Accent);
            c.DrawCircle(cx, cy, 2.2f * k, core);
        }
        float tx = x0 + 28 * k;
        Fui.Text(c, "K26K", tx, midY + 1 * k, 21 * k, Text, "Bold", spacing: 3 * k);
        Fui.Text(c, "HARDWARE MONITOR", tx, midY + 16 * k, 8 * k, Dim, "Medium", spacing: 1.6f * k);
        float titleRight = tx + Math.Max(Fui.Measure("K26K", 21 * k, "Bold", 3 * k), Fui.Measure("HARDWARE MONITOR", 8 * k, "Medium", 1.6f * k)) + 26 * k;

        // ---------- right block: state and fancy button ----------
        float bw = 112 * k, bh = 30 * k;
        button = new SKRect(width - 18 * k - bw, midY - bh / 2, width - 18 * k, midY + bh / 2);
        using (SKPaint fill = Fui.Fill(hover ? Accent : Accent.WithAlpha(0.08f)))
            c.DrawRect(button, fill);
        using (SKPaint border = Fui.Stroke(Accent, 1 * k))
            c.DrawRect(button, border);
        Fui.Brackets(c, SKRect.Inflate(button, 3 * k, 3 * k), 5 * k, Accent.WithAlpha(0.7f), 1 * k);
        Fui.Text(c, "FANCY  ▸", button.MidX, button.MidY + 4 * k, 10.5f * k, hover ? Bg : Accent, "Bold", SKTextAlign.Center, 1.8f * k);

        SKColor stateColor = m.State == "THERMAL" ? Alarm : m.State == "LOAD" ? Cool : Accent;
        string stateText = m.State == "THERMAL" ? "THERMAL ALERT" : m.State == "LOAD" ? "HIGH LOAD" : "SYSTEM NOMINAL";
        float sw = Fui.Measure(stateText, 9.5f * k, "Bold", 1.6f * k) + 30 * k;
        var state = new SKRect(button.Left - 16 * k - sw, midY - 14 * k, button.Left - 16 * k, midY + 2 * k);
        using (SKPaint p = Fui.Stroke(stateColor.WithAlpha(0.8f), 1 * k))
            c.DrawRect(state, p);
        using (SKPaint dot = Fui.Fill(stateColor))
            c.DrawCircle(state.Left + 10 * k, state.MidY, 3 * k, dot);
        Fui.Text(c, stateText, state.Left + 20 * k, state.MidY + 3.5f * k, 9.5f * k, stateColor, "Bold", spacing: 1.6f * k);
        Fui.Text(c, m.Date + "  " + m.Clock, state.Right, midY + 17 * k, 9 * k, Dim, "Medium", SKTextAlign.Right, 1.2f * k);

        // ---------- center ruler with live values ----------
        float left = titleRight + 8 * k, right = state.Left - 22 * k;
        if (right - left < 160 * k)
            return;

        using (SKPaint ruler = Fui.Stroke(Rule))
        {
            c.DrawLine(left, 11 * k, right, 11 * k, ruler);
            int i = 0;
            for (float x = left; x <= right; x += 8 * k, i++)
                c.DrawLine(x, 11 * k, x, (i % 5 == 0 ? 17 : 14) * k, ruler);
        }

        var cells = new (string Label, string Value, string Sub, double Fraction, bool Hot)[]
        {
            ("CPU", Fui.Pct(m.CpuLoad), Fui.Temp(m.CpuTemp, m.Fahrenheit), m.CpuLoad / 100, m.CpuTemp >= m.CpuTempLimit - 10),
            ("GPU", Fui.Pct(m.GpuLoad), Fui.Temp(m.GpuTemp, m.Fahrenheit), m.GpuLoad / 100, m.GpuTemp >= m.GpuTempLimit - 10),
            ("RAM", Fui.Pct(m.MemoryLoad), double.IsNaN(m.MemoryUsedGb) ? "" : m.MemoryUsedGb.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " GB", m.MemoryLoad / 100, m.MemoryLoad >= 90),
            ("PWR", Fui.Watts(m.TotalPower), m.OtherPowerEstimated ? "EST" : "MEAS", Math.Min(1, m.TotalPower / 600), false),
            ("NET", SystemMetrics.Rate(m.NetDown), "▲ " + SystemMetrics.Rate(m.NetUp), double.NaN, false),
            ("STRESS", m.Stress.ToString("0", System.Globalization.CultureInfo.InvariantCulture), m.State, m.Stress / 100, m.Stress >= 80)
        };

        float cellW = (right - left) / cells.Length;
        for (int i = 0; i < cells.Length; i++)
        {
            var cell = cells[i];
            float cx = left + i * cellW + 6 * k;
            float valueSize = 15 * k;
            Fui.Text(c, cell.Label, cx, 30 * k, 8 * k, Dim, "Bold", spacing: 1.5f * k);
            float vw = Fui.Text(c, cell.Value, cx, 48 * k, valueSize, cell.Hot ? Alarm : Data, "Medium");
            Fui.Text(c, cell.Sub, cx + vw + 6 * k, 48 * k, 9 * k, Dim, "Regular");
            if (!double.IsNaN(cell.Fraction))
                Fui.Segments(c, new SKRect(cx, 54 * k, cx + Math.Min(cellW - 16 * k, 92 * k), 56.5f * k), cell.Fraction, 12,
                             cell.Hot ? Alarm : Accent, Rule, 1.5f * k);
            if (i > 0)
            {
                using SKPaint sep = Fui.Stroke(Rule);
                c.DrawLine(left + i * cellW - 4 * k, 24 * k, left + i * cellW - 4 * k, 56 * k, sep);
            }
        }
    }
}
