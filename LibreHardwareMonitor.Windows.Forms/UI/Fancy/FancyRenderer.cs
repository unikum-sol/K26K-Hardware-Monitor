// K26K Hardware Monitor - Fancy dashboard renderer (in the spirit of the "Bloom-Line" FUI scene).
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
using System.Globalization;
using System.Linq;
using LibreHardwareMonitor.Windows.Forms.Telemetry;
using LibreHardwareMonitor.Windows.Forms.UI.SciFi;
using SkiaSharp;

namespace LibreHardwareMonitor.Windows.Forms.UI.Fancy;

public sealed class FancyOptions
{
    public bool ReducedMotion { get; set; }
    /// <summary>Seconds since the dashboard was opened (drives the boot sequence).</summary>
    public double SinceOpen { get; set; } = 10;
    public bool ExitHover { get; set; }
}

/// <summary>
/// Draws the whole Fancy dashboard as a pure function of the model, the time and the size.
/// Used by the live control and by the snapshot renderer.
/// </summary>
public static class FancyRenderer
{
    public static readonly SKColor Bg = Fui.Hex("#04100C");
    public static readonly SKColor Panel = Fui.Hex("#0A1A14");
    public static readonly SKColor Text = Fui.Hex("#D8F0E2");
    public static readonly SKColor Dim = Fui.Hex("#6E9C8A");
    public static readonly SKColor Accent = Fui.Hex("#6BE8A4");
    public static readonly SKColor Data = Fui.Hex("#F0C060");
    public static readonly SKColor Cool = Fui.Hex("#7DD8C4");
    public static readonly SKColor Alarm = Fui.Hex("#F09BAA");
    public static readonly SKColor Rule = Fui.Hex("#1A3026");

    private const double BootSeconds = 2.6;

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Renders one frame. Returns the hit area of the exit button.</summary>
    public static SKRect Render(SKCanvas c, int width, int height, float dpiScale, DashboardModel m, double t, FancyOptions o)
    {
        float u = Math.Max(0.55f, Math.Min(2.6f, Math.Min(width / 1600f, height / 900f))) * Math.Max(1f, dpiScale * 0.85f);
        u = Math.Min(u, Math.Min(width / 1100f, height / 640f));
        double boot = o.ReducedMotion ? 1 : Math.Min(1, o.SinceOpen / BootSeconds);
        float ease = (float)(1 - Math.Pow(1 - boot, 3));

        c.Clear(Bg);
        DrawBackground(c, width, height, u, t, o);

        float top = 38 * u, bottom = height - 44 * u;
        float leftW = 300 * u, rightW = 340 * u;
        var leftCol = new SKRect(16 * u, top + 12 * u, 16 * u + leftW, bottom - 12 * u);
        var rightCol = new SKRect(width - 16 * u - rightW, top + 12 * u, width - 16 * u, bottom - 12 * u);
        var center = new SKRect(leftCol.Right + 20 * u, top + 8 * u, rightCol.Left - 20 * u, bottom - 8 * u);

        int layer = c.Save();
        if (ease < 1)
        {
            using SKPaint fade = new() { Color = SKColors.White.WithAlpha(ease) };
            c.SaveLayer(fade);
        }

        DrawOrbital(c, center, u, m, t, o, ease);
        DrawLeftColumn(c, leftCol, u, m, t);
        DrawRightColumn(c, rightCol, u, m, t);

        c.RestoreToCount(layer);

        DrawTopStrip(c, width, top, u, m, t);
        SKRect exit = DrawBottomStrip(c, width, height, bottom, u, m, o);

        if (boot < 1)
            DrawBoot(c, width, height, u, boot, t);

        DrawScanlines(c, width, height, u);
        return exit;
    }

    // ------------------------------------------------------------------ background

    private static void DrawBackground(SKCanvas c, int w, int h, float u, double t, FancyOptions o)
    {
        using (SKPaint grid = Fui.Stroke(Accent.WithAlpha(0.035f)))
        {
            float step = 32 * u;
            for (float x = 0; x < w; x += step)
                c.DrawLine(x, 0, x, h, grid);
            for (float y = 0; y < h; y += step)
                c.DrawLine(0, y, w, y, grid);
        }

        using var glow = new SKPaint
        {
            Shader = SKShader.CreateRadialGradient(new SKPoint(w / 2f, h / 2f), Math.Max(w, h) * 0.55f,
                                                   new[] { Accent.WithAlpha(0.10f), Accent.WithAlpha(0.02f), SKColors.Transparent },
                                                   new[] { 0f, 0.45f, 1f }, SKShaderTileMode.Clamp)
        };
        c.DrawRect(0, 0, w, h, glow);
    }

    private static void DrawScanlines(SKCanvas c, int w, int h, float u)
    {
        using SKPaint p = Fui.Stroke(SKColors.Black.WithAlpha(0.16f), Math.Max(1, u));
        for (float y = 0; y < h; y += 3 * Math.Max(1, u))
            c.DrawLine(0, y, w, y, p);

        using var vignette = new SKPaint
        {
            Shader = SKShader.CreateRadialGradient(new SKPoint(w / 2f, h / 2f), Math.Max(w, h) * 0.75f,
                                                   new[] { SKColors.Transparent, SKColors.Black.WithAlpha(0.45f) },
                                                   new[] { 0.6f, 1f }, SKShaderTileMode.Clamp)
        };
        c.DrawRect(0, 0, w, h, vignette);
    }

    // ------------------------------------------------------------------ orbital core

    private static void DrawOrbital(SKCanvas c, SKRect area, float u, DashboardModel m, double t, FancyOptions o, float ease)
    {
        var cp = new SKPoint(area.MidX, area.MidY + 6 * u);
        float maxR = Math.Min(area.Width, area.Height) / 2f - 34 * u;
        float coreR = Math.Max(52 * u, maxR * 0.24f);
        float[] orbits = { maxR * 0.55f, maxR * 0.80f, maxR };
        double rot = o.ReducedMotion ? 0 : t * 4;

        // orbit rings with ticks
        for (int i = 0; i < orbits.Length; i++)
        {
            float r = orbits[i];
            using SKPaint ring = Fui.Stroke(Accent.WithAlpha(i == 1 ? 0.22f : 0.12f), (i == 1 ? 1.2f : 1f) * u);
            if (i == 2)
                ring.PathEffect = SKPathEffect.CreateDash(new[] { 2 * u, 7 * u }, (float)(rot * u));
            DrawArc(c, cp, r, -90, 360 * ease, ring);

            int ticks = i == 0 ? 72 : 120;
            using SKPaint tick = Fui.Stroke(Accent.WithAlpha(0.18f), u);
            for (int k = 0; k < ticks * ease; k++)
            {
                double a = (k * 360.0 / ticks + (i == 1 ? rot : -rot * 0.5)) * Math.PI / 180;
                float len = (k % 6 == 0 ? 7 : 3) * u;
                c.DrawLine(cp.X + (float)Math.Cos(a) * r, cp.Y + (float)Math.Sin(a) * r,
                           cp.X + (float)Math.Cos(a) * (r - len), cp.Y + (float)Math.Sin(a) * (r - len), tick);
            }
        }

        // crosshair
        using (SKPaint cross = Fui.Stroke(Accent.WithAlpha(0.08f), u))
        {
            c.DrawLine(cp.X - orbits[2] - 18 * u, cp.Y, cp.X + orbits[2] + 18 * u, cp.Y, cross);
            c.DrawLine(cp.X, cp.Y - orbits[2] - 18 * u, cp.X, cp.Y + orbits[2] + 18 * u, cross);
        }

        // nodes
        List<HardwareTile> tiles = m.Tiles.Where(x => x.Kind != "power").ToList();
        int n = Math.Max(1, tiles.Count);
        double total = Math.Max(1, m.TotalPower);
        var positions = new List<(HardwareTile Tile, SKPoint P, float R, SKColor Color, double Angle)>();
        for (int i = 0; i < tiles.Count; i++)
        {
            HardwareTile tile = tiles[i];
            double angle = -90 + 360.0 * i / n + 18;
            float r = (i % 2 == 0 ? orbits[1] : orbits[2] * 0.93f) * (0.75f + 0.25f * ease);
            double rad = angle * Math.PI / 180;
            var p = new SKPoint(cp.X + (float)Math.Cos(rad) * r, cp.Y + (float)Math.Sin(rad) * r);
            float nodeR = (15 + (float)(Nz(tile.Load) / 100 * 13)) * u;
            positions.Add((tile, p, nodeR, HeatColor(tile), angle));
        }

        // petal links between neighbours (organic "bloom" lines)
        for (int i = 0; i < positions.Count && positions.Count > 2; i++)
        {
            var a = positions[i];
            var b = positions[(i + 1) % positions.Count];
            using var path = new SKPath();
            path.MoveTo(a.P);
            var mid = new SKPoint((a.P.X + b.P.X) / 2, (a.P.Y + b.P.Y) / 2);
            var outward = new SKPoint(mid.X + (mid.X - cp.X) * 0.22f, mid.Y + (mid.Y - cp.Y) * 0.22f);
            path.QuadTo(outward, b.P);
            using SKPaint petal = Fui.Stroke(Cool.WithAlpha(0.16f), u);
            c.DrawPath(path, petal);
        }

        // energy lines from the core to every node
        foreach (var node in positions)
        {
            double share = !double.IsNaN(node.Tile.Power) ? node.Tile.Power / total : 0.03;
            float thick = (1 + (float)Math.Min(1, share * 2.2) * 6) * u;
            using SKPath path = Bloom(cp, node.P, coreR, node.R, node.Angle);
            using (SKPaint halo = Fui.Stroke(node.Color.WithAlpha(0.22f), thick * 3))
            {
                halo.MaskFilter = Fui.Blur(4 * u);
                c.DrawPath(path, halo);
            }
            using (SKPaint line = Fui.Stroke(node.Color.WithAlpha(0.75f), thick))
            {
                line.StrokeCap = SKStrokeCap.Round;
                c.DrawPath(path, line);
            }

            // particles flowing outward, speed and count follow the activity
            if (!o.ReducedMotion)
            {
                using var measure = new SKPathMeasure(path);
                float len = measure.Length;
                int count = 2 + (int)(Nz(node.Tile.Load) / 25);
                double speed = 0.18 + Nz(node.Tile.Load) / 100 * 0.55;
                using SKPaint dot = Fui.Fill(SKColors.White.WithAlpha(0.9f));
                using SKPaint dotGlow = Fui.Fill(node.Color.WithAlpha(0.8f));
                dotGlow.MaskFilter = Fui.Blur(3 * u);
                for (int k = 0; k < count; k++)
                {
                    double f = (t * speed + k / (double)count + node.Angle / 360) % 1;
                    if (measure.GetPosition((float)(f * len), out SKPoint pos))
                    {
                        c.DrawCircle(pos, 3.2f * u, dotGlow);
                        c.DrawCircle(pos, 1.3f * u, dot);
                    }
                }
            }
        }

        // the core: stress index
        DrawCore(c, cp, coreR, u, m, t, o, ease);

        // node bodies and labels
        foreach (var node in positions)
            DrawNode(c, node.Tile, node.P, node.R, node.Color, node.Angle, cp, area, u, m, t, o);
    }

    private static SKPath Bloom(SKPoint from, SKPoint to, float fromR, float toR, double angle)
    {
        double dx = to.X - from.X, dy = to.Y - from.Y, d = Math.Sqrt(dx * dx + dy * dy);
        if (d < 1)
            d = 1;
        float ux = (float)(dx / d), uy = (float)(dy / d);
        var a = new SKPoint(from.X + ux * fromR, from.Y + uy * fromR);
        var b = new SKPoint(to.X - ux * toR, to.Y - uy * toR);
        float bend = (float)(d * 0.22) * (Math.Sin(angle * Math.PI / 90) >= 0 ? 1 : -1);
        var c1 = new SKPoint(a.X + (b.X - a.X) * 0.33f - uy * bend, a.Y + (b.Y - a.Y) * 0.33f + ux * bend);
        var c2 = new SKPoint(a.X + (b.X - a.X) * 0.70f + uy * bend * 0.4f, a.Y + (b.Y - a.Y) * 0.70f - ux * bend * 0.4f);
        var path = new SKPath();
        path.MoveTo(a);
        path.CubicTo(c1, c2, b);
        return path;
    }

    private static void DrawCore(SKCanvas c, SKPoint cp, float r, float u, DashboardModel m, double t, FancyOptions o, float ease)
    {
        SKColor col = StressColor(m.Stress);
        double pulse = o.ReducedMotion ? 0.5 : 0.5 + 0.5 * Math.Sin(t * 2.2);

        using (var glow = new SKPaint { Shader = SKShader.CreateRadialGradient(cp, r * 1.9f, new[] { col.WithAlpha(0.25f + 0.08f * (float)pulse), SKColors.Transparent }, null, SKShaderTileMode.Clamp) })
            c.DrawCircle(cp, r * 1.9f, glow);

        using (SKPaint face = Fui.Fill(Panel.WithAlpha(0.92f)))
            c.DrawCircle(cp, r, face);
        using (SKPaint ring = Fui.Stroke(Rule, 6 * u))
            c.DrawCircle(cp, r - 7 * u, ring);

        float sweep = (float)(Math.Max(0, Math.Min(100, m.Stress)) / 100 * 360 * ease);
        using (SKPaint halo = Fui.Stroke(col.WithAlpha(0.6f), 8 * u))
        {
            halo.MaskFilter = Fui.Blur(5 * u);
            halo.StrokeCap = SKStrokeCap.Round;
            DrawArc(c, cp, r - 7 * u, -90, sweep, halo);
        }
        using (SKPaint arc = Fui.Stroke(col, 5 * u))
        {
            arc.StrokeCap = SKStrokeCap.Round;
            DrawArc(c, cp, r - 7 * u, -90, sweep, arc);
        }
        using (SKPaint outer = Fui.Stroke(Accent.WithAlpha(0.35f), u))
            c.DrawCircle(cp, r + 6 * u, outer);

        Fui.Text(c, "SYSTEM STRESS", cp.X, cp.Y - r * 0.36f, Math.Max(7, 8.5f * u), Dim, "Bold", SKTextAlign.Center, 1.6f * u);
        Fui.Text(c, Math.Round(m.Stress).ToString(Inv), cp.X, cp.Y + r * 0.22f, r * 0.62f, col, "Bold", SKTextAlign.Center, 0, 6 * u);
        string state = m.State == "THERMAL" ? "THERMAL LIMIT" : m.State == "LOAD" ? "HIGH LOAD" : "NOMINAL";
        Fui.Text(c, state, cp.X, cp.Y + r * 0.52f, Math.Max(7, 8.5f * u), col, "Bold", SKTextAlign.Center, 1.8f * u);
    }

    private static void DrawNode(SKCanvas c, HardwareTile tile, SKPoint p, float r, SKColor col, double angle, SKPoint cp, SKRect area, float u, DashboardModel m, double t, FancyOptions o)
    {
        double load = Nz(tile.Load);
        if (!o.ReducedMotion && load > 5)
        {
            double phase = (t * (0.4 + load / 120) + angle / 360) % 1;
            using SKPaint pulse = Fui.Stroke(col.WithAlpha((float)(0.55 * (1 - phase))), u);
            c.DrawCircle(p, r + (float)(phase * 16 * u), pulse);
        }

        using (SKPaint halo = Fui.Fill(col.WithAlpha(0.28f)))
        {
            halo.MaskFilter = Fui.Blur(7 * u);
            c.DrawCircle(p, r, halo);
        }
        using (SKPaint face = Fui.Fill(Panel))
            c.DrawCircle(p, r, face);
        using (SKPaint ring = Fui.Stroke(col.WithAlpha(0.35f), 2 * u))
            c.DrawCircle(p, r - 3 * u, ring);
        using (SKPaint arc = Fui.Stroke(col, 2.6f * u))
        {
            arc.StrokeCap = SKStrokeCap.Round;
            DrawArc(c, p, r - 3 * u, -90, (float)(load / 100 * 360), arc);
        }

        Fui.Text(c, tile.Label, p.X, p.Y + 3.5f * u, Math.Max(7, 9 * u), Text, "Bold", SKTextAlign.Center, 1.2f * u);

        // callout label, placed outward from the core
        bool rightSide = p.X >= cp.X;
        float lx = p.X + (rightSide ? 1 : -1) * (r + 26 * u);
        float ly = p.Y - 10 * u;
        using (SKPaint lead = Fui.Stroke(col.WithAlpha(0.6f), u))
        {
            c.DrawLine(p.X + (rightSide ? r : -r), p.Y, lx - (rightSide ? 6 : -6) * u, ly + 4 * u, lead);
            c.DrawLine(lx - (rightSide ? 6 : -6) * u, ly + 4 * u, lx, ly + 4 * u, lead);
        }

        SKTextAlign align = rightSide ? SKTextAlign.Left : SKTextAlign.Right;
        string value = tile.Kind is "net" or "disk" ? tile.Detail : Fui.Pct(tile.Load);
        var sub = new List<string>();
        if (!double.IsNaN(tile.Temp))
            sub.Add(Fui.Temp(tile.Temp, m.Fahrenheit));
        if (!double.IsNaN(tile.Power))
            sub.Add(Fui.Watts(tile.Power));
        if (tile.Kind is not ("net" or "disk") && !string.IsNullOrEmpty(tile.Detail))
            sub.Add(tile.Detail);
        string subText = string.Join("  ·  ", sub);
        string nameText = Shorten(tile.Name, 22).ToUpperInvariant();

        // keep the callout inside the orbital area
        float labelW = Math.Max(Fui.Measure(value, Math.Max(9, 15 * u), "Medium"), Math.Max(Fui.Measure(subText, Math.Max(7, 9 * u), "Medium", 0.6f * u), Fui.Measure(nameText, Math.Max(6.5f, 7.5f * u), "Bold", 1.2f * u)));
        if (rightSide && lx + labelW > area.Right)
            lx = area.Right - labelW;
        else if (!rightSide && lx - labelW < area.Left)
            lx = area.Left + labelW;

        Fui.Text(c, value, lx, ly + 1 * u, Math.Max(9, 15 * u), Data, "Medium", align, 0, 3 * u);
        Fui.Text(c, subText, lx, ly + 16 * u, Math.Max(7, 9 * u), Dim, "Medium", align, 0.6f * u);
        Fui.Text(c, nameText, lx, ly - 14 * u, Math.Max(6.5f, 7.5f * u), Cool.WithAlpha(0.85f), "Bold", align, 1.2f * u);
    }

    // ------------------------------------------------------------------ left column

    private static void DrawLeftColumn(SKCanvas c, SKRect col, float u, DashboardModel m, double t)
    {
        float y = col.Top;
        y = Header(c, "HARDWARE NODES", m.Tiles.Count.ToString("00", Inv), col.Left, col.Right, y, u);

        float gap = 8 * u;
        float tileW = (col.Width - gap) / 2, tileH = 74 * u;
        int i = 0;
        foreach (HardwareTile tile in m.Tiles)
        {
            float x = col.Left + (i % 2) * (tileW + gap);
            float ty = y + (i / 2) * (tileH + gap);
            if (ty + tileH > col.Bottom - 150 * u)
                break;
            DrawTile(c, new SKRect(x, ty, x + tileW, ty + tileH), tile, u, m);
            i++;
        }
        y += (int)Math.Ceiling(i / 2.0) * (tileH + gap) + 10 * u;

        // core matrix: one cell per CPU core
        y = Header(c, "CORE MATRIX", m.CoreLoads.Length.ToString("00", Inv), col.Left, col.Right, y, u);
        int cores = m.CoreLoads.Length;
        if (cores > 0)
        {
            int perRow = cores <= 8 ? cores : cores <= 16 ? 8 : 12;
            float cell = Math.Min(30 * u, (col.Width - (perRow - 1) * 4 * u) / perRow);
            for (int k = 0; k < cores; k++)
            {
                float cx = col.Left + (k % perRow) * (cell + 4 * u);
                float cy = y + (k / perRow) * (cell + 4 * u);
                if (cy + cell > col.Bottom - 46 * u)
                    break;
                double load = m.CoreLoads[k];
                SKColor color = LoadColor(load);
                using (SKPaint back = Fui.Fill(Panel))
                    c.DrawRect(cx, cy, cell, cell, back);
                using (SKPaint fill = Fui.Fill(color.WithAlpha(0.18f + 0.72f * (float)(load / 100))))
                    c.DrawRect(cx, cy + cell * (1 - (float)(load / 100)), cell, cell * (float)(load / 100), fill);
                using (SKPaint border = Fui.Stroke(color.WithAlpha(0.5f), u))
                    c.DrawRect(cx, cy, cell, cell, border);
                Fui.Text(c, (k + 1).ToString(Inv), cx + 3 * u, cy + 9 * u, Math.Max(6, 7 * u), Dim, "Medium");
            }
            y += (float)Math.Ceiling(cores / (double)perRow) * (cell + 4 * u) + 14 * u;
        }
        else
        {
            Fui.Text(c, "NO PER-CORE LOAD SENSORS", col.Left, y + 12 * u, Math.Max(7, 8 * u), Dim, "Medium", spacing: u);
            y += 26 * u;
        }

        // fan effort
        if (y < col.Bottom - 34 * u)
        {
            y = Header(c, "FAN EFFORT", Fui.Pct(m.FanEffort), col.Left, col.Right, y, u);
            Fui.Segments(c, new SKRect(col.Left, y, col.Right, y + 8 * u), m.FanEffort / 100, 30, Cool, Rule, 2 * u);
            Fui.Text(c, m.FanCount + " FANS  ·  AVG " + (double.IsNaN(m.FanAvgRpm) ? "--" : m.FanAvgRpm.ToString("0", Inv)) + " RPM", col.Left, y + 22 * u, Math.Max(7, 8 * u), Dim, "Medium", spacing: 0.6f * u);
        }
    }

    private static void DrawTile(SKCanvas c, SKRect r, HardwareTile tile, float u, DashboardModel m)
    {
        SKColor col = HeatColor(tile);
        using (SKPaint back = Fui.Fill(Panel.WithAlpha(0.85f)))
            c.DrawRect(r, back);
        using (SKPath frame = Fui.CutFrame(r, 9 * u))
        using (SKPaint border = Fui.Stroke(Rule, u))
            c.DrawPath(frame, border);
        using (SKPaint edge = Fui.Stroke(col, 2 * u))
            c.DrawLine(r.Left, r.Top + 6 * u, r.Left, r.Bottom - 6 * u, edge);

        float x = r.Left + 9 * u;
        Fui.Text(c, tile.Label, x, r.Top + 15 * u, Math.Max(7, 8.5f * u), Dim, "Bold", spacing: 1.4f * u);
        string value = tile.Kind == "power" ? Fui.Watts(tile.Power) : tile.Kind is "net" ? tile.Detail : Fui.Pct(tile.Load);
        Fui.Text(c, value, x, r.Top + 38 * u, Math.Max(10, (value.Length > 8 ? 14 : 19) * u), Text, "Medium", glow: 2 * u);
        Fui.Text(c, !double.IsNaN(tile.Temp) ? Fui.Temp(tile.Temp, m.Fahrenheit) : "", r.Right - 8 * u, r.Top + 15 * u, Math.Max(7, 8.5f * u), col, "Bold", SKTextAlign.Right);

        Fui.Bar(c, new SKRect(x, r.Bottom - 20 * u, r.Right - 9 * u, r.Bottom - 17 * u), Nz(tile.Load) / 100, Accent, Rule);
        double heat = double.IsNaN(tile.Temp) ? double.NaN : (tile.Temp - 25) / Math.Max(1, tile.TempLimit - 25);
        Fui.Bar(c, new SKRect(x, r.Bottom - 12 * u, r.Right - 9 * u, r.Bottom - 9 * u), heat, col, Rule);
    }

    // ------------------------------------------------------------------ right column

    private static void DrawRightColumn(SKCanvas c, SKRect col, float u, DashboardModel m, double t)
    {
        float y = Header(c, "TELEMETRY", m.Clock, col.Left, col.Right, col.Top, u);

        var rows = new List<(string Label, string Value, SKColor Color)>
        {
            ("TOTAL POWER", Fui.Watts(m.TotalPower) + (m.OtherPowerEstimated ? "  EST" : ""), Data),
            ("CPU PACKAGE", Fui.Watts(m.CpuPower), Text),
            ("GPU BOARD", Fui.Watts(m.GpuPower), Text),
            ("THERMAL HEADROOM", double.IsNaN(m.ThermalHeadroom) ? "--" : m.ThermalHeadroom.ToString("0", Inv) + " K", m.ThermalRatio >= 0.85 ? Alarm : Text),
            ("HOTTEST", string.IsNullOrEmpty(m.HottestName) ? "--" : Shorten(m.HottestName, 14).ToUpperInvariant() + "  " + Fui.Temp(m.HottestTemp, m.Fahrenheit), Text),
            ("MEMORY", Fui.Pct(m.MemoryLoad) + (double.IsNaN(m.MemoryUsedGb) ? "" : "  " + m.MemoryUsedGb.ToString("0.0", Inv) + " GB"), Text),
            ("DISK I/O", SystemMetrics.Rate(m.DiskRead + m.DiskWrite), Text),
            ("NETWORK", "↓ " + SystemMetrics.Rate(m.NetDown) + "  ↑ " + SystemMetrics.Rate(m.NetUp), Text),
            ("SESSION ENERGY", m.SessionEnergyWh.ToString("0.00", Inv) + " WH", Data)
        };

        float rowH = 19 * u;
        foreach (var row in rows)
        {
            Fui.Text(c, row.Label, col.Left, y + 12 * u, Math.Max(7, 8 * u), Dim, "Bold", spacing: 1.2f * u);
            Fui.Text(c, row.Value, col.Right, y + 12 * u, Math.Max(7.5f, 10 * u), row.Color, "Medium", SKTextAlign.Right);
            using (SKPaint sep = Fui.Stroke(Rule, u))
            {
                sep.PathEffect = SKPathEffect.CreateDash(new[] { 1 * u, 3 * u }, 0);
                c.DrawLine(col.Left, y + rowH - 2 * u, col.Right, y + rowH - 2 * u, sep);
            }
            y += rowH;
        }
        y += 12 * u;

        // energy distribution
        y = Header(c, "ENERGY DISTRIBUTION", m.OtherPowerEstimated ? "REST EST" : "MEASURED", col.Left, col.Right, y, u);
        double cpuP = Nz(m.CpuPower), gpuP = Nz(m.GpuPower), rest = Nz(m.OtherPower), sum = Math.Max(1, cpuP + gpuP + rest);
        var parts = new[] { ("CPU", cpuP, Accent), ("GPU", gpuP, Data), ("REST", rest, Cool) };
        float bx = col.Left;
        var bar = new SKRect(col.Left, y, col.Right, y + 10 * u);
        foreach (var (label, value, color) in parts)
        {
            float w = (float)(value / sum) * bar.Width;
            using SKPaint p = Fui.Fill(label == "REST" && m.OtherPowerEstimated ? color.WithAlpha(0.55f) : color);
            c.DrawRect(bx, bar.Top, Math.Max(0, w - 2 * u), bar.Height, p);
            bx += w;
        }
        y += 24 * u;
        float lx = col.Left;
        foreach (var (label, value, color) in parts)
        {
            using (SKPaint sw = Fui.Fill(color))
                c.DrawRect(lx, y - 7 * u, 7 * u, 7 * u, sw);
            lx += 11 * u;
            lx += Fui.Text(c, label + " " + (value / sum * 100).ToString("0", Inv) + "%", lx, y, Math.Max(7, 8.5f * u), Text, "Medium") + 14 * u;
        }
        y += 18 * u;

        // headroom ring + power scope side by side
        float ringR = Math.Min(48 * u, (col.Bottom - y) * 0.3f);
        if (ringR > 18 * u)
        {
            y = Header(c, "SYSTEM HEADROOM", "", col.Left, col.Right, y, u);
            var rc = new SKPoint(col.Left + ringR + 4 * u, y + ringR + 4 * u);
            double headroom = 100 - m.Stress;
            using (SKPaint track = Fui.Stroke(Rule, 7 * u))
                c.DrawCircle(rc, ringR, track);
            using (SKPaint arc = Fui.Stroke(StressColor(m.Stress), 7 * u))
            {
                arc.StrokeCap = SKStrokeCap.Round;
                DrawArc(c, rc, ringR, -90, (float)(headroom / 100 * 360), arc);
            }
            Fui.Text(c, headroom.ToString("0", Inv) + "%", rc.X, rc.Y + 6 * u, Math.Max(10, 17 * u), Text, "Bold", SKTextAlign.Center, 0, 3 * u);

            float sx = rc.X + ringR + 18 * u;
            Fui.Text(c, "HEADROOM = 100 - STRESS", sx, rc.Y - ringR * 0.45f, Math.Max(6.5f, 7.5f * u), Dim, "Medium", spacing: 0.6f * u);
            Fui.Text(c, "LOAD, HEAT, MEMORY AND", sx, rc.Y - ringR * 0.45f + 14 * u, Math.Max(6.5f, 7.5f * u), Dim, "Medium", spacing: 0.6f * u);
            Fui.Text(c, "FAN EFFORT COMBINED", sx, rc.Y - ringR * 0.45f + 28 * u, Math.Max(6.5f, 7.5f * u), Dim, "Medium", spacing: 0.6f * u);
            y = rc.Y + ringR + 18 * u;
        }

        // power oscilloscope
        if (col.Bottom - y > 60 * u)
        {
            y = Header(c, "POWER · " + Math.Max(1, m.PowerHistory.Length * m.UpdateIntervalMs / 60000) + " MIN", Fui.Watts(m.TotalPower), col.Left, col.Right, y, u);
            DrawScope(c, new SKRect(col.Left, y, col.Right, col.Bottom), m.PowerHistory, u, t);
        }
    }

    private static void DrawScope(SKCanvas c, SKRect r, double[] values, float u, double t)
    {
        using (SKPaint back = Fui.Fill(Panel.WithAlpha(0.7f)))
            c.DrawRect(r, back);
        using (SKPaint grid = Fui.Stroke(Rule, u))
        {
            for (int i = 1; i < 4; i++)
                c.DrawLine(r.Left, r.Top + r.Height * i / 4, r.Right, r.Top + r.Height * i / 4, grid);
            for (int i = 1; i < 6; i++)
                c.DrawLine(r.Left + r.Width * i / 6, r.Top, r.Left + r.Width * i / 6, r.Bottom, grid);
        }
        Fui.Brackets(c, r, 8 * u, Accent.WithAlpha(0.6f), u);

        if (values.Length < 2)
            return;

        double max = Math.Max(10, values.Max() * 1.15), min = 0;
        using var line = new SKPath();
        using var area = new SKPath();
        for (int i = 0; i < values.Length; i++)
        {
            float x = r.Left + r.Width * i / (values.Length - 1);
            float y = r.Bottom - (float)((values[i] - min) / (max - min)) * (r.Height - 6 * u) - 3 * u;
            if (i == 0)
            {
                line.MoveTo(x, y);
                area.MoveTo(x, r.Bottom);
                area.LineTo(x, y);
            }
            else
            {
                line.LineTo(x, y);
                area.LineTo(x, y);
            }
        }
        area.LineTo(r.Right, r.Bottom);
        area.Close();

        using (var fill = new SKPaint { Shader = SKShader.CreateLinearGradient(new SKPoint(0, r.Top), new SKPoint(0, r.Bottom), new[] { Data.WithAlpha(0.28f), Data.WithAlpha(0.0f) }, null, SKShaderTileMode.Clamp) })
            c.DrawPath(area, fill);
        using (SKPaint glow = Fui.Stroke(Data.WithAlpha(0.5f), 4 * u))
        {
            glow.MaskFilter = Fui.Blur(3 * u);
            c.DrawPath(line, glow);
        }
        using (SKPaint p = Fui.Stroke(Data, 1.6f * u))
            c.DrawPath(line, p);

        Fui.Text(c, max.ToString("0", Inv) + " W", r.Left + 6 * u, r.Top + 12 * u, Math.Max(6.5f, 7.5f * u), Dim, "Medium");
    }

    // ------------------------------------------------------------------ strips

    private static void DrawTopStrip(SKCanvas c, int w, float h, float u, DashboardModel m, double t)
    {
        using (SKPaint back = Fui.Fill(SKColors.Black.WithAlpha(0.55f)))
            c.DrawRect(0, 0, w, h, back);
        using (SKPaint rule = Fui.Stroke(Rule, u))
            c.DrawLine(0, h, w, h, rule);

        float y = h / 2 + 4 * u;
        float x = 16 * u;
        x += Fui.Text(c, "K26K", x, y, Math.Max(9, 12 * u), Accent, "Bold", spacing: 2 * u, glow: 3 * u) + 10 * u;
        x += Fui.Text(c, "SYSTEM CORE", x, y, Math.Max(8, 10 * u), Text, "Bold", spacing: 2 * u) + 26 * u;
        foreach (var (label, value) in new[] { ("NODE", m.Machine.ToUpperInvariant()), ("SESSION", Fui.Duration(m.SessionSeconds)), ("SENSORS", m.SensorCount.ToString(Inv)) })
        {
            x += Fui.Text(c, label, x, y, Math.Max(7, 8 * u), Dim, "Bold", spacing: 1.4f * u) + 8 * u;
            x += Fui.Text(c, value, x, y, Math.Max(7.5f, 9.5f * u), Text, "Medium") + 22 * u;
        }

        Fui.Text(c, m.Clock, w - 16 * u, y + 1 * u, Math.Max(10, 15 * u), Data, "Bold", SKTextAlign.Right, 1 * u, 4 * u);
        float cw = Fui.Measure(m.Clock, Math.Max(10, 15 * u), "Bold", 1 * u);
        Fui.Text(c, m.Date, w - 28 * u - cw, y, Math.Max(7, 8.5f * u), Dim, "Medium", SKTextAlign.Right, 1 * u);
    }

    private static SKRect DrawBottomStrip(SKCanvas c, int w, int h, float top, float u, DashboardModel m, FancyOptions o)
    {
        using (SKPaint back = Fui.Fill(SKColors.Black.WithAlpha(0.55f)))
            c.DrawRect(0, top, w, h - top, back);
        using (SKPaint rule = Fui.Stroke(Rule, u))
            c.DrawLine(0, top, w, top, rule);

        float y = top + (h - top) / 2 + 4 * u;
        float x = 16 * u;
        foreach (string state in new[] { "NOMINAL", "LOAD", "THERMAL" })
        {
            bool active = m.State == state;
            SKColor col = state == "THERMAL" ? Alarm : state == "LOAD" ? Data : Accent;
            float tw = Fui.Measure(state, Math.Max(7, 8.5f * u), "Bold", 1.4f * u) + 18 * u;
            var r = new SKRect(x, y - 13 * u, x + tw, y + 5 * u);
            if (active)
            {
                using SKPaint f = Fui.Fill(col.WithAlpha(0.18f));
                c.DrawRect(r, f);
            }
            using (SKPaint b = Fui.Stroke(active ? col : Rule, u))
                c.DrawRect(r, b);
            Fui.Text(c, state, r.MidX, y, Math.Max(7, 8.5f * u), active ? col : Dim, "Bold", SKTextAlign.Center, 1.4f * u);
            x += tw + 8 * u;
        }

        x += 18 * u;
        x += Fui.Text(c, "INTERVAL", x, y, Math.Max(7, 8 * u), Dim, "Bold", spacing: 1.4f * u) + 8 * u;
        x += Fui.Text(c, (m.UpdateIntervalMs / 1000.0).ToString("0.0#", Inv) + " S", x, y, Math.Max(7.5f, 9 * u), Text, "Medium") + 22 * u;
        if (o.ReducedMotion)
            Fui.Text(c, "REDUCED MOTION", x, y, Math.Max(7, 8 * u), Cool, "Bold", spacing: 1.4f * u);

        string exitText = "EXIT FANCY  [ESC]";
        float ew = Fui.Measure(exitText, Math.Max(7.5f, 9 * u), "Bold", 1.4f * u) + 26 * u;
        var exit = new SKRect(w - 16 * u - ew, y - 15 * u, w - 16 * u, y + 7 * u);
        using (SKPaint f = Fui.Fill(o.ExitHover ? Accent : Accent.WithAlpha(0.08f)))
            c.DrawRect(exit, f);
        using (SKPaint b = Fui.Stroke(Accent, u))
            c.DrawRect(exit, b);
        Fui.Text(c, exitText, exit.MidX, y, Math.Max(7.5f, 9 * u), o.ExitHover ? Bg : Accent, "Bold", SKTextAlign.Center, 1.4f * u);
        return exit;
    }

    private static void DrawBoot(SKCanvas c, int w, int h, float u, double boot, double t)
    {
        float alpha = (float)Math.Max(0, 1 - Math.Max(0, boot - 0.75) / 0.25);
        using (SKPaint veil = Fui.Fill(Bg.WithAlpha(0.6f * alpha)))
            c.DrawRect(0, 0, w, h, veil);
        string text = "INITIALIZING SENSOR GRID";
        Fui.Text(c, text, w / 2f, h / 2f - 8 * u, Math.Max(10, 16 * u), Accent.WithAlpha(alpha), "Bold", SKTextAlign.Center, 4 * u, 4 * u);
        var bar = new SKRect(w / 2f - 180 * u, h / 2f + 10 * u, w / 2f + 180 * u, h / 2f + 14 * u);
        Fui.Bar(c, bar, boot, Accent.WithAlpha(alpha), Rule.WithAlpha(alpha));
        Fui.Text(c, (boot * 100).ToString("000", Inv) + " %", w / 2f, h / 2f + 34 * u, Math.Max(8, 10 * u), Dim.WithAlpha(alpha), "Medium", SKTextAlign.Center, 2 * u);
    }

    // ------------------------------------------------------------------ helpers

    private static float Header(SKCanvas c, string title, string meta, float left, float right, float y, float u)
    {
        Fui.Text(c, title, left, y + 10 * u, Math.Max(7, 8.5f * u), Accent, "Bold", spacing: 1.8f * u);
        if (!string.IsNullOrEmpty(meta))
            Fui.Text(c, meta, right, y + 10 * u, Math.Max(7, 8.5f * u), Dim, "Medium", SKTextAlign.Right, 1 * u);
        using (SKPaint rule = Fui.Stroke(Rule, u))
            c.DrawLine(left, y + 16 * u, right, y + 16 * u, rule);
        using (SKPaint tick = Fui.Stroke(Accent, 2 * u))
            c.DrawLine(left, y + 16 * u, left + 18 * u, y + 16 * u, tick);
        return y + 24 * u;
    }

    private static void DrawArc(SKCanvas c, SKPoint center, float r, float start, float sweep, SKPaint paint)
    {
        if (sweep <= 0.1f)
            return;
        var oval = new SKRect(center.X - r, center.Y - r, center.X + r, center.Y + r);
        using var path = new SKPath();
        path.AddArc(oval, start, Math.Min(359.9f, sweep));
        c.DrawPath(path, paint);
    }

    private static SKColor HeatColor(HardwareTile tile)
    {
        if (double.IsNaN(tile.Temp))
            return LoadColor(Nz(tile.Load));
        double ratio = (tile.Temp - 30) / Math.Max(1, tile.TempLimit - 30);
        return ratio >= 0.85 ? Alarm : ratio >= 0.6 ? Fui.Mix(Data, Alarm, (float)((ratio - 0.6) / 0.25)) : Fui.Mix(Accent, Data, (float)Math.Max(0, ratio / 0.6));
    }

    private static SKColor LoadColor(double load)
    {
        return load >= 85 ? Alarm : load >= 50 ? Fui.Mix(Data, Alarm, (float)((load - 50) / 35)) : Fui.Mix(Accent, Data, (float)(load / 50));
    }

    private static SKColor StressColor(double stress)
    {
        return stress >= 80 ? Alarm : stress >= 50 ? Fui.Mix(Data, Alarm, (float)((stress - 50) / 30)) : Fui.Mix(Accent, Data, (float)(stress / 50));
    }

    private static string Shorten(string text, int max)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= max)
            return text ?? "";
        return text.Substring(0, max - 1) + "…";
    }

    private static double Nz(double v) => double.IsNaN(v) ? 0 : v;
}
