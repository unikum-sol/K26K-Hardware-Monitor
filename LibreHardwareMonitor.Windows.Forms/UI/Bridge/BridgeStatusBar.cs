// K26K Hardware Monitor - bottom strip of the Bridge main window.
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

/// <summary>Bottom strip of the Bridge main window: a row of colored blocks with session, sensors, interval, energy and web server.</summary>
public sealed class BridgeStatusBar : SkiaView
{
    public const int LogicalHeight = 32;

    private DashboardModel _model = new();

    public BridgeStatusBar()
    {
        Dock = DockStyle.Bottom;
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

    protected override void Render(SKCanvas c, int width, int height, float k) => Draw(c, width, height, k, _model);

    public static void Draw(SKCanvas c, int width, int height, float k, DashboardModel m)
    {
        c.Clear(Bg);
        CultureInfo culture = CultureInfo.CurrentCulture;
        float top = 4 * k, bottom = height - 5 * k, gap = 5 * k;
        float size = 14 * k, y = (top + bottom) / 2 + 5 * k;

        var items = new (string Label, string Value, SKColor Color)[]
        {
            ("SESSION", Fui.Duration(m.SessionSeconds), Peach),
            ("SENSORS", m.SensorCount.ToString(culture), Lilac),
            ("INTERVAL", (m.UpdateIntervalMs / 1000.0).ToString("0.##", culture) + " S", Tan),
            ("ENERGY", m.SessionEnergyWh.ToString("0.0", culture) + " WH", Blue),
            ("WEB", m.WebServerOnline ? m.WebServer : "OFFLINE", m.WebServerOnline ? Accent : Fui.Hex("#6A6058"))
        };

        // the machine name at the right end, on black, closed by a rounded cap
        string machine = "K26K // " + (string.IsNullOrEmpty(m.Machine) ? "LOCAL" : m.Machine.ToUpperInvariant());
        var cap = new SKRect(width - 10 * k - 26 * k, top, width - 10 * k, bottom);
        Block(c, cap, Accent, false, true);
        float machineW = Label(c, machine, cap.Left - 10 * k, y, size, Accent, SemiBold, SKTextAlign.Right, 0.8f * k);
        float limit = cap.Left - 10 * k - machineW - 16 * k;

        // legend for the meter colors in the grid
        var legend = new (string Label, SKColor Color)[] { ("< 70 %", Blue), ("≥ 70 %", Warm), ("≥ 90 %", Alarm) };
        float legendW = Measure("METER", size, SemiBold, 0.8f * k) + 10 * k;
        foreach (var item in legend)
            legendW += 22 * k + Measure(item.Label, size, Regular) + 12 * k;

        float x = 10 * k;
        for (int i = 0; i < items.Length; i++)
        {
            var item = items[i];
            string text = item.Label + "  " + item.Value;
            float w = Measure(text, size, SemiBold, 0.6f * k) + 26 * k;
            if (x + w > limit)
                break;
            var block = new SKRect(x, top, x + w, bottom);
            Block(c, block, item.Color, i == 0, false);
            Label(c, text, block.Right - 10 * k, y, size, Bg, SemiBold, SKTextAlign.Right, 0.6f * k);
            x += w + gap;
        }

        // a plain bar fills the gap up to the legend (or the machine name)
        float fillEnd = limit - (limit - x > legendW + 40 * k ? legendW + 16 * k : 0);
        if (fillEnd - x > 12 * k)
            Block(c, new SKRect(x, top, fillEnd, bottom), Fui.Hex("#5A4C40"));

        if (limit - x > legendW + 40 * k)
        {
            float lx = fillEnd + 16 * k;
            lx += Label(c, "METER", lx, y, size, Dim, SemiBold, spacing: 0.8f * k) + 10 * k;
            foreach (var item in legend)
            {
                Pill(c, new SKRect(lx, y - 9 * k, lx + 16 * k, y - 2 * k), item.Color);
                lx += 22 * k;
                lx += Label(c, item.Label, lx, y, size, Dim, Regular) + 12 * k;
            }
        }
    }
}
