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
        Height = (int)(26 * UiScale);
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
        Height = (int)(26 * UiScale);
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
            ("SENSORS", m.SensorCount.ToString(CultureInfo.InvariantCulture), HudHeader.Text),
            ("INTERVAL", (m.UpdateIntervalMs / 1000.0).ToString("0.##", CultureInfo.InvariantCulture) + " S", HudHeader.Text),
            ("ENERGY", m.SessionEnergyWh.ToString("0.0", CultureInfo.InvariantCulture) + " WH", HudHeader.Text),
            ("WEB", m.WebServerOnline ? m.WebServer : "OFFLINE", m.WebServerOnline ? HudHeader.Accent : HudHeader.Dim)
        };

        foreach (var item in items)
        {
            using (SKPaint dot = Fui.Fill(item.Label == "WEB" ? item.Color : HudHeader.Accent.WithAlpha(0.6f)))
                c.DrawRect(new SKRect(x, y - 5 * k, x + 3 * k, y - 2 * k), dot);
            x += 8 * k;
            x += Fui.Text(c, item.Label, x, y, 8 * k, HudHeader.Dim, "Bold", spacing: 1.4f * k) + 7 * k;
            x += Fui.Text(c, item.Value, x, y, 9 * k, item.Color, "Medium", spacing: 0.4f * k) + 20 * k;
            if (x > width - 180 * k)
                break;
        }

        Fui.Text(c, "K26K // " + (string.IsNullOrEmpty(m.Machine) ? "LOCAL" : m.Machine.ToUpperInvariant()), width - 16 * k, y, 8.5f * k, HudHeader.Dim, "Medium", SKTextAlign.Right, 1.4f * k);
    }
}
