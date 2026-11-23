// K26K Hardware Monitor - Sci-Fi status strip.
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
using SkiaSharp;

namespace LibreHardwareMonitor.Windows.Forms.UI.SciFi;

/// <summary>Bottom strip of the Sci-Fi main window: session, sensor count, web server, interval, energy and clock.</summary>
public sealed class HudStatusBar : SkiaView
{
    private DashboardModel _model = new();

    public HudStatusBar()
    {
        Dock = DockStyle.Bottom;
        Height = (int)(28 * UiScale);
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
        Height = (int)(28 * UiScale);
    }

    protected override void Render(SKCanvas c, int width, int height, float k) => Draw(c, width, height, k, _model);

    public static void Draw(SKCanvas c, int width, int height, float k, DashboardModel m)
    {
        c.Clear(HudHeader.Bg);
        using (SKPaint rule = Fui.Stroke(HudHeader.Rule))
            c.DrawLine(0, 0.5f, width, 0.5f, rule);

        float y = height / 2f + 4 * k;
        float x = 16 * k;
        var items = new (string Label, string Value, SKColor Color)[]
        {
            ("SESSION", Fui.Duration(m.SessionSeconds), HudHeader.Text),
            ("SENSORS", m.SensorCount.ToString(CultureInfo.CurrentCulture), HudHeader.Text),
            ("INTERVAL", (m.UpdateIntervalMs / 1000.0).ToString("0.##", CultureInfo.CurrentCulture) + " S", HudHeader.Text),
            ("ENERGY", m.SessionEnergyWh.ToString("0.0", CultureInfo.CurrentCulture) + " WH", HudHeader.Text),
            ("WEB", m.WebServerOnline ? m.WebServer : "OFFLINE", m.WebServerOnline ? HudHeader.Accent : HudHeader.Dim)
        };

        foreach (var item in items)
        {
            using (SKPaint dot = Fui.Fill(item.Label == "WEB" ? item.Color : HudHeader.Accent.WithAlpha(0.6f)))
                c.DrawRect(new SKRect(x, y - 5 * k, x + 3 * k, y - 2 * k), dot);
            x += 8 * k;
            x += Fui.Text(c, item.Label, x, y, 9.5f * k, HudHeader.Dim, "Bold", spacing: 0.8f * k) + 7 * k;
            x += Fui.Text(c, item.Value, x, y, 10.5f * k, item.Color, "Medium") + 22 * k;
            if (x > width - 180 * k)
                break;
        }

        string machine = "K26K // " + (string.IsNullOrEmpty(m.Machine) ? "LOCAL" : m.Machine.ToUpperInvariant());
        float machineW = Fui.Text(c, machine, width - 16 * k, y, 10 * k, HudHeader.Dim, "Medium", SKTextAlign.Right, 0.8f * k);

        // legend for the meter colors in the grid (only when there is room)
        var legend = new (string Label, SKColor Color)[] { ("< 70 %", Fui.Hex("#00E0E8")), ("≥ 70 %", Fui.Hex("#F0A030")), ("≥ 90 %", Fui.Hex("#FF40A0")) };
        float legendW = Fui.Measure("METER", 9.5f * k, "Bold", 0.8f * k) + 10 * k;
        foreach (var item in legend)
            legendW += 14 * k + Fui.Measure(item.Label, 10 * k, "Medium") + 14 * k;
        float lx = width - 16 * k - machineW - 34 * k - legendW;
        if (lx > x + 10 * k)
        {
            lx += Fui.Text(c, "METER", lx, y, 9.5f * k, HudHeader.Dim, "Bold", spacing: 0.8f * k) + 10 * k;
            foreach (var item in legend)
            {
                using (SKPaint sw = Fui.Fill(item.Color))
                    c.DrawRect(lx, y - 7 * k, 9 * k, 5 * k, sw);
                lx += 14 * k;
                lx += Fui.Text(c, item.Label, lx, y, 10 * k, HudHeader.Dim, "Medium") + 14 * k;
            }
        }
    }
}
