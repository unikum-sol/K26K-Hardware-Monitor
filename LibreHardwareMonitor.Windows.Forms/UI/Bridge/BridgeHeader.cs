// K26K Hardware Monitor - header of the Bridge main window.
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
using System.Globalization;
using System.Windows.Forms;
using LibreHardwareMonitor.Windows.Forms.Telemetry;
using LibreHardwareMonitor.Windows.Forms.UI.SciFi;
using SkiaSharp;
using static LibreHardwareMonitor.Windows.Forms.UI.Bridge.BridgeDraw;

namespace LibreHardwareMonitor.Windows.Forms.UI.Bridge;

/// <summary>
/// Header of the Bridge main window: an elbow with the machine block on the left, the top bar in colored
/// segments with the title at its end, the live values and the state and FANCY pills.
/// </summary>
public sealed class BridgeHeader : SkiaView
{
    public const int LogicalHeight = 84;

    private SKRect _button;
    private bool _hover;
    private DashboardModel _model = new();

    public event EventHandler FancyRequested;

    /// <summary>True on the FANCY pill; the rest of the header moves the window like a title bar.</summary>
    public bool IsOnButton(System.Drawing.Point p) => _button.Contains(p.X, p.Y);

    public BridgeHeader()
    {
        Dock = DockStyle.Top;
        Height = (int)(LogicalHeight * UiScale);
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
        Height = (int)(LogicalHeight * UiScale);
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

    protected override void Render(SKCanvas c, int width, int height, float k) => Draw(c, width, height, k, _model, _hover, out _button);

    /// <summary>Static so that the snapshot renderer can draw the header without a window.</summary>
    public static void Draw(SKCanvas c, int width, int height, float k, DashboardModel m, bool hover, out SKRect button)
    {
        c.Clear(Bg);
        CultureInfo culture = CultureInfo.CurrentCulture;

        float left = 10 * k, top = 4 * k;
        float arm = 150 * k, bar = 16 * k, gap = 5 * k;

        // ---------- title at the right end of the top bar ----------
        const string title = "K26K HARDWARE MONITOR";
        float titleSize = 30 * k;
        float titleW = Measure(title, titleSize, Bold, 1.5f * k);
        float titleRight = width - 14 * k;
        float titleLeft = titleRight - titleW;
        Label(c, title, titleRight, top + 25 * k, titleSize, Accent, Bold, SKTextAlign.Right, 1.5f * k);

        // ---------- the elbow: machine block on the left, the top bar running right ----------
        float barEnd = titleLeft - 14 * k;
        float elbowRight = Math.Max(left + arm + 40 * k, Math.Min(barEnd, left + arm + (barEnd - left - arm) * 0.42f));
        FillElbow(c, new SKRect(left, top, elbowRight, height), arm, bar, Corner.TopLeft, Accent, 34 * k, 14 * k);

        // the rest of the bar in segments of their own colors, each with a gap
        SKColor[] colors = { Lilac, Blue, Peach };
        float x = elbowRight + gap;
        float seg = (barEnd - x - gap * (colors.Length - 1)) / colors.Length;
        for (int i = 0; i < colors.Length && seg > 8 * k; i++)
        {
            Block(c, new SKRect(x, top, x + seg, top + bar), colors[i], false, i == colors.Length - 1);
            x += seg + gap;
        }

        // the machine block: name and code in black, at the bottom right of the arm
        string machine = string.IsNullOrEmpty(m.Machine) ? "LOCAL" : m.Machine.ToUpperInvariant();
        float armRight = left + arm;
        float nameSize = 13 * k;
        while (nameSize > 9 * k && Measure(machine, nameSize, SemiBold) > arm - 18 * k)
            nameSize -= 0.5f * k;
        Label(c, "K26K", armRight - 9 * k, height - 26 * k, 22 * k, Bg, Bold, SKTextAlign.Right, 1 * k);
        Label(c, machine, armRight - 9 * k, height - 9 * k, nameSize, Bg, SemiBold, SKTextAlign.Right);
        Label(c, Code(machine), left + 10 * k, height - 9 * k, 11 * k, Bg, Regular);

        // ---------- right block below the title: clock, state and FANCY ----------
        float rowY = top + bar + 10 * k, rowH = 28 * k;
        float bw = 108 * k;
        button = new SKRect(titleRight - bw, rowY + 14 * k, titleRight, rowY + 14 * k + rowH);
        SKColor fancy = hover ? Fui.Mix(Accent, SKColors.White, 0.3f) : Accent;
        Block(c, button, fancy, true, true);
        // (Antonio has no arrow glyphs: the triangle is drawn)
        float tri = 5 * k, tx = button.Right - 16 * k;
        using (var arrow = new SKPath())
        {
            arrow.MoveTo(tx - tri, button.MidY - tri);
            arrow.LineTo(tx + tri * 0.4f, button.MidY);
            arrow.LineTo(tx - tri, button.MidY + tri);
            arrow.Close();
            using SKPaint fill = Fui.Fill(Bg);
            c.DrawPath(arrow, fill);
        }
        Label(c, "FANCY", tx - tri - 8 * k, button.MidY + 6 * k, 16 * k, Bg, Bold, SKTextAlign.Right, 1 * k);

        SKColor stateColor = StateColor(m.State);
        string stateText = m.State == "THERMAL" || m.State == "LOAD" ? SystemMetrics.StateLabel(m.State) : "SYSTEM NOMINAL";
        float sw = Measure(stateText, 15 * k, Bold, 1 * k) + 34 * k;
        var state = new SKRect(button.Left - gap - sw, button.Top, button.Left - gap, button.Bottom);
        Block(c, state, stateColor, true, false);
        Label(c, stateText, state.Right - 12 * k, state.MidY + 5.5f * k, 15 * k, Bg, Bold, SKTextAlign.Right, 1 * k);
        Label(c, m.Date + "  " + m.Clock, state.Left - 10 * k, state.MidY + 5.5f * k, 15 * k, Peach, SemiBold, SKTextAlign.Right, 0.5f * k);
        float clockLeft = state.Left - 10 * k - Measure(m.Date + "  " + m.Clock, 15 * k, SemiBold, 0.5f * k);

        // ---------- live values between the arm and the right block ----------
        float cellsLeft = armRight + 22 * k, cellsRight = clockLeft - 24 * k;
        var cells = new (string Label, string Value, string Sub, double Fraction, SKColor Color)[]
        {
            ("CPU", Fui.Pct(m.CpuLoad), Fui.Temp(m.CpuTemp, m.Fahrenheit), m.CpuLoad / 100, Hot(m.CpuLoad, m.CpuTemp, m.CpuTempLimit)),
            ("GPU", Fui.Pct(m.GpuLoad), Fui.Temp(m.GpuTemp, m.Fahrenheit), m.GpuLoad / 100, Hot(m.GpuLoad, m.GpuTemp, m.GpuTempLimit)),
            ("MEMORY", Fui.Pct(m.MemoryLoad), double.IsNaN(m.MemoryUsedGb) ? "" : m.MemoryUsedGb.ToString("0.0", culture) + " GB", m.MemoryLoad / 100, LoadColor(m.MemoryLoad)),
            ("POWER", Fui.Watts(m.TotalPower), m.OtherPowerEstimated ? "EST" : "MEAS", Math.Min(1, m.TotalPower / 600), Peach),
            ("NETWORK", SystemMetrics.Rate(m.NetDown), "UP " + SystemMetrics.Rate(m.NetUp), double.NaN, Lilac),
            ("STRESS", m.Stress.ToString("0", culture), "/ 100", m.Stress / 100, StateColor(m.State))
        };

        int count = cells.Length;
        // fewer cells when the window is narrow; each needs about 104 px
        while (count > 0 && (cellsRight - cellsLeft) / count < 104 * k)
            count--;
        if (count == 0)
            return;

        float cellW = (cellsRight - cellsLeft) / count;
        float labelY = top + bar + 20 * k, valueY = labelY + 27 * k, meterY = valueY + 8 * k;
        for (int i = 0; i < count; i++)
        {
            var cell = cells[i];
            float cx = cellsLeft + i * cellW;
            float w = cellW - 14 * k;
            Label(c, cell.Label, cx, labelY, 12.5f * k, i % 2 == 0 ? Lilac : Peach, SemiBold, spacing: 1.2f * k);
            SKColor valueColor = cell.Label == "POWER" || cell.Label == "NETWORK" ? Data : cell.Color == Blue ? Data : cell.Color == Alarm ? AlarmText : cell.Color;
            float vw = Label(c, cell.Value, cx, valueY, 25 * k, valueColor, SemiBold);
            Label(c, cell.Sub, cx + vw + 6 * k, valueY, 13 * k, Dim, Regular);
            if (!double.IsNaN(cell.Fraction))
                Meter(c, new SKRect(cx, meterY, cx + Math.Min(w, 120 * k), meterY + 6 * k), cell.Fraction, cell.Color, Track);
        }
    }

    // a CPU or GPU cell turns orange or red with its load, red when it runs within 10 degrees of its limit
    private static SKColor Hot(double load, double temp, double limit) => temp >= limit - 10 ? Alarm : LoadColor(load);
}
