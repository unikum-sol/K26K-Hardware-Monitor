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
    public static readonly SKColor Dim = Fui.Hex("#8FBBA8");
    public static readonly SKColor Accent = Fui.Hex("#6BE8A4");
    public static readonly SKColor Data = Fui.Hex("#E8B040");
    public static readonly SKColor Cool = Fui.Hex("#7DD8C4");
    public static readonly SKColor Alarm = Fui.Hex("#FF8FA3");
    public static readonly SKColor Rule = Fui.Hex("#1F3A2E");
    public static readonly SKColor Track = Fui.Hex("#2A4A3C");

    private const double BootSeconds = 2.6;
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // Type scale: sizes grow with the window, but never below the legible minimum (in pixels).
    private static float Micro(float u) => Math.Max(11f, 9.5f * u);
    private static float Small(float u) => Math.Max(12f, 10.5f * u);
    private static float Body(float u) => Math.Max(13f, 11.5f * u);
    private static float Value(float u) => Math.Max(16f, 15f * u);

    /// <summary>Renders one frame. Returns the hit area of the exit button.</summary>
    public static SKRect Render(SKCanvas c, int width, int height, float dpiScale, DashboardModel m, double t, FancyOptions o)
    {
        float u = Math.Max(0.8f, Math.Min(2.6f, Math.Min(width / 1600f, height / 900f)));
        double boot = o.ReducedMotion ? 1 : Math.Min(1, o.SinceOpen / BootSeconds);
        float ease = (float)(1 - Math.Pow(1 - boot, 3));

        c.Clear(Bg);
        DrawBackground(c, width, height, u);

        float top = 40 * u, bottom = height - 46 * u;
        float leftW = 310 * u, rightW = 350 * u;
        var leftCol = new SKRect(18 * u, top + 14 * u, 18 * u + leftW, bottom - 12 * u);
        var rightCol = new SKRect(width - 18 * u - rightW, top + 14 * u, width - 18 * u, bottom - 12 * u);
        var center = new SKRect(leftCol.Right + 24 * u, top + 8 * u, rightCol.Left - 24 * u, bottom - 8 * u);

        int layer = c.Save();
        if (ease < 1)
        {
            using SKPaint fade = new() { Color = SKColors.White.WithAlpha(ease) };
            c.SaveLayer(fade);
        }

        DrawOrbital(c, center, u, m, t, o, ease);
        DrawLeftColumn(c, leftCol, u, m);
        DrawRightColumn(c, rightCol, u, m);
        c.RestoreToCount(layer);

        DrawTopStrip(c, width, top, u, m);
        SKRect exit = DrawBottomStrip(c, width, height, bottom, u, m, o);

        if (boot < 1)
            DrawBoot(c, width, height, u, boot);

        DrawScanlines(c, width, height, u);
        return exit;
    }

    // ------------------------------------------------------------------ background

    private static void DrawBackground(SKCanvas c, int w, int h, float u)
    {
        using (SKPaint grid = Fui.Stroke(Accent.WithAlpha(0.03f)))
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
                                                   new[] { Accent.WithAlpha(0.09f), Accent.WithAlpha(0.02f), SKColors.Transparent },
                                                   new[] { 0f, 0.45f, 1f }, SKShaderTileMode.Clamp)
        };
        c.DrawRect(0, 0, w, h, glow);
    }

    // very light scanlines and a soft vignette; strong enough for the look, too weak to flicker
    private static void DrawScanlines(SKCanvas c, int w, int h, float u)
    {
        using SKPaint p = Fui.Stroke(SKColors.Black.WithAlpha(0.05f), 1);
        float step = Math.Max(3, 4 * u);
        for (float y = 0; y < h; y += step)
            c.DrawLine(0, y, w, y, p);

        using var vignette = new SKPaint
        {
            Shader = SKShader.CreateRadialGradient(new SKPoint(w / 2f, h / 2f), Math.Max(w, h) * 0.78f,
                                                   new[] { SKColors.Transparent, SKColors.Black.WithAlpha(0.35f) },
                                                   new[] { 0.62f, 1f }, SKShaderTileMode.Clamp)
        };
        c.DrawRect(0, 0, w, h, vignette);
    }

    // ------------------------------------------------------------------ orbital core

    private static void DrawOrbital(SKCanvas c, SKRect area, float u, DashboardModel m, double t, FancyOptions o, float ease)
    {
        var cp = new SKPoint(area.MidX, area.MidY + 4 * u);
        float maxR = Math.Min(area.Width / 2f - 150 * u, area.Height / 2f - 60 * u);
        maxR = Math.Max(120 * u, maxR);
        float coreR = Math.Max(66, Math.Max(58 * u, maxR * 0.26f));
        float[] orbits = { maxR * 0.56f, maxR * 0.80f, maxR };
        double rot = o.ReducedMotion ? 0 : t * 3;

        // orbit rings with ticks and labels
        string[] ringNames = { "ORBIT A // COMPUTE", "ORBIT B // PERIPHERY", "ORBIT C // FIELD" };
        for (int i = 0; i < orbits.Length; i++)
        {
            float r = orbits[i];
            using SKPaint ring = Fui.Stroke(Accent.WithAlpha(i == 1 ? 0.36f : 0.24f), (i == 1 ? 1.3f : 1f) * u);
            if (i == 2)
                ring.PathEffect = SKPathEffect.CreateDash(new[] { 2 * u, 6 * u }, (float)(rot * u));
            DrawArc(c, cp, r, -90, 360 * ease, ring);

            int ticks = i == 0 ? 72 : 120;
            using SKPaint tick = Fui.Stroke(Accent.WithAlpha(0.3f), u);
            for (int k = 0; k < ticks * ease; k++)
            {
                double a = (k * 360.0 / ticks + (i == 1 ? rot : -rot * 0.5)) * Math.PI / 180;
                float len = (k % 6 == 0 ? 8 : 3.5f) * u;
                c.DrawLine(cp.X + (float)Math.Cos(a) * r, cp.Y + (float)Math.Sin(a) * r,
                           cp.X + (float)Math.Cos(a) * (r - len), cp.Y + (float)Math.Sin(a) * (r - len), tick);
            }

            // ring label just outside the ring, left of the top, where no node sits (only when there is room)
            double la = -97 * Math.PI / 180;
            if (maxR >= 270)
                Fui.Text(c, ringNames[i], cp.X + (float)Math.Cos(la) * (r + 6 * u), cp.Y + (float)Math.Sin(la) * (r + 6 * u), Micro(u), Dim.WithAlpha(0.85f), "Medium", SKTextAlign.Right, 0.6f * u);
        }

        // nodes
        List<HardwareTile> tiles = m.Tiles.Where(x => x.Kind != "power").ToList();
        int n = Math.Max(1, tiles.Count);
        double total = Math.Max(1, m.TotalPower);
        var nodes = new List<(HardwareTile Tile, SKPoint P, float R, SKColor Color, double Angle)>();
        for (int i = 0; i < tiles.Count; i++)
        {
            HardwareTile tile = tiles[i];
            double angle = -90 + 360.0 * i / n + 18;
            float r = (i % 2 == 0 ? orbits[1] : orbits[2] * 0.94f) * (0.75f + 0.25f * ease);
            double rad = angle * Math.PI / 180;
            var p = new SKPoint(cp.X + (float)Math.Cos(rad) * r, cp.Y + (float)Math.Sin(rad) * r);
            float nodeR = (17 + (float)(Nz(tile.Load) / 100 * 12)) * u;
            nodes.Add((tile, p, nodeR, HeatColor(tile), angle));
        }

        // petal links between neighbours (organic "bloom" lines)
        for (int i = 0; i < nodes.Count && nodes.Count > 2; i++)
        {
            var a = nodes[i];
            var b = nodes[(i + 1) % nodes.Count];
            using var path = new SKPath();
            path.MoveTo(a.P);
            var mid = new SKPoint((a.P.X + b.P.X) / 2, (a.P.Y + b.P.Y) / 2);
            path.QuadTo(new SKPoint(mid.X + (mid.X - cp.X) * 0.22f, mid.Y + (mid.Y - cp.Y) * 0.22f), b.P);
            using SKPaint petal = Fui.Stroke(Cool.WithAlpha(0.28f), u);
            c.DrawPath(path, petal);
        }

        // energy lines from the core to every node; thickness = share of the total power
        foreach (var node in nodes)
        {
            double share = !double.IsNaN(node.Tile.Power) ? node.Tile.Power / total : 0.03;
            float thick = (1.2f + (float)Math.Min(1, share * 2.2) * 6) * u;
            using SKPath path = Bloom(cp, node.P, coreR + 8 * u, node.R, node.Angle);
            using (SKPaint halo = Fui.Stroke(node.Color.WithAlpha(0.18f), thick * 2.6f))
            {
                halo.MaskFilter = Fui.Blur(3 * u);
                c.DrawPath(path, halo);
            }
            using (SKPaint line = Fui.Stroke(node.Color.WithAlpha(0.85f), thick))
            {
                line.StrokeCap = SKStrokeCap.Round;
                c.DrawPath(path, line);
            }

            // particles: count and speed follow the activity of the node
            if (!o.ReducedMotion)
            {
                using var measure = new SKPathMeasure(path);
                float len = measure.Length;
                int count = 2 + (int)(Nz(node.Tile.Load) / 25);
                double speed = 0.12 + Nz(node.Tile.Load) / 100 * 0.4;
                using SKPaint dot = Fui.Fill(SKColors.White.WithAlpha(0.85f));
                using SKPaint dotGlow = Fui.Fill(node.Color.WithAlpha(0.7f));
                dotGlow.MaskFilter = Fui.Blur(2.5f * u);
                for (int k = 0; k < count; k++)
                {
                    double f = (t * speed + k / (double)count + node.Angle / 360) % 1;
                    if (measure.GetPosition((float)(f * len), out SKPoint pos))
                    {
                        c.DrawCircle(pos, 3f * u, dotGlow);
                        c.DrawCircle(pos, 1.3f * u, dot);
                    }
                }
            }
        }

        DrawCore(c, cp, coreR, u, m, t, o, ease);

        foreach (var node in nodes)
            DrawNode(c, node.Tile, node.P, node.R, node.Color, node.Angle, area, u, m, t, o);
    }

    private static SKPath Bloom(SKPoint from, SKPoint to, float fromR, float toR, double angle)
    {
        double dx = to.X - from.X, dy = to.Y - from.Y, d = Math.Max(1, Math.Sqrt(dx * dx + dy * dy));
        float ux = (float)(dx / d), uy = (float)(dy / d);
        var a = new SKPoint(from.X + ux * fromR, from.Y + uy * fromR);
        var b = new SKPoint(to.X - ux * toR, to.Y - uy * toR);
        float bend = (float)(d * 0.2) * (Math.Sin(angle * Math.PI / 90) >= 0 ? 1 : -1);
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
        double pulse = o.ReducedMotion ? 0.5 : 0.5 + 0.5 * Math.Sin(t * 1.6);

        using (var glow = new SKPaint { Shader = SKShader.CreateRadialGradient(cp, r * 1.9f, new[] { col.WithAlpha(0.2f + 0.06f * (float)pulse), SKColors.Transparent }, null, SKShaderTileMode.Clamp) })
            c.DrawCircle(cp, r * 1.9f, glow);

        using (SKPaint face = Fui.Fill(Panel))
            c.DrawCircle(cp, r, face);

        // outer ring: the stress index itself
        using (SKPaint track = Fui.Stroke(Track, 6 * u))
            c.DrawCircle(cp, r - 7 * u, track);
        float sweep = (float)(Math.Max(0, Math.Min(100, m.Stress)) / 100 * 360 * ease);
        using (SKPaint halo = Fui.Stroke(col.WithAlpha(0.45f), 8 * u))
        {
            halo.MaskFilter = Fui.Blur(4 * u);
            halo.StrokeCap = SKStrokeCap.Round;
            DrawArc(c, cp, r - 7 * u, -90, sweep, halo);
        }
        using (SKPaint arc = Fui.Stroke(col, 5 * u))
        {
            arc.StrokeCap = SKStrokeCap.Round;
            DrawArc(c, cp, r - 7 * u, -90, sweep, arc);
        }

        // inner ring: what the stress is made of (load, heat, memory, fans), each part a quarter
        var parts = new[] { (m.StressLoad, Accent), (m.StressHeat, Data), (m.StressMemory, Cool), (m.StressFans, Fui.Hex("#B8A0F0")) };
        float ir = r - 17 * u;
        for (int i = 0; i < parts.Length; i++)
        {
            float start = -90 + i * 90 + 3;
            using (SKPaint bg = Fui.Stroke(Track, 3 * u))
                DrawArc(c, cp, ir, start, 84, bg);
            using SKPaint fg = Fui.Stroke(parts[i].Item2, 3 * u);
            DrawArc(c, cp, ir, start, (float)(84 * Math.Max(0, Math.Min(100, parts[i].Item1)) / 100 * ease), fg);
        }

        using (SKPaint outer = Fui.Stroke(Accent.WithAlpha(0.45f), u))
            c.DrawCircle(cp, r + 7 * u, outer);

        Fui.Text(c, r >= 90 ? "SYSTEM STRESS" : "STRESS", cp.X, cp.Y - r * 0.34f, Micro(u), Dim, "Bold", SKTextAlign.Center, 0.8f * u);
        Fui.Text(c, Math.Round(m.Stress).ToString(Inv), cp.X, cp.Y + r * 0.2f, r * 0.6f, col, "Bold", SKTextAlign.Center, 0, 5 * u);
        string state = m.State == "THERMAL" ? (r >= 90 ? "▲ THERMAL LIMIT" : "▲ THERMAL") : m.State == "LOAD" ? (r >= 90 ? "▲ HIGH LOAD" : "▲ LOAD") : "● NOMINAL";
        Fui.Text(c, state, cp.X, cp.Y + r * 0.5f, Small(u), col, "Bold", SKTextAlign.Center, 0.8f * u);
    }

    private static void DrawNode(SKCanvas c, HardwareTile tile, SKPoint p, float r, SKColor col, double angle, SKRect area, float u, DashboardModel m, double t, FancyOptions o)
    {
        double load = Nz(tile.Load);
        if (!o.ReducedMotion && load > 5)
        {
            // one slow ring per second at most
            double phase = (t * (0.35 + load / 250) + angle / 360) % 1;
            using SKPaint pulse = Fui.Stroke(col.WithAlpha((float)(0.5 * (1 - phase))), u);
            c.DrawCircle(p, r + (float)(phase * 14 * u), pulse);
        }

        using (SKPaint halo = Fui.Fill(col.WithAlpha(0.22f)))
        {
            halo.MaskFilter = Fui.Blur(6 * u);
            c.DrawCircle(p, r, halo);
        }
        using (SKPaint face = Fui.Fill(Panel))
            c.DrawCircle(p, r, face);
        using (SKPaint ring = Fui.Stroke(Track, 2.4f * u))
            c.DrawCircle(p, r - 3 * u, ring);
        using (SKPaint arc = Fui.Stroke(col, 2.8f * u))
        {
            arc.StrokeCap = SKStrokeCap.Round;
            DrawArc(c, p, r - 3 * u, -90, (float)(load / 100 * 360), arc);
        }
        Fui.Text(c, tile.Label, p.X, p.Y + 4 * u, Small(u), Text, "Bold", SKTextAlign.Center, 0.6f * u);

        // ---- callout: beside the node on the left/right, above/below it near the top/bottom
        string value = tile.Kind is "net" or "disk" ? tile.Detail : Fui.Pct(tile.Load);
        var subParts = new List<string>();
        if (!double.IsNaN(tile.Temp))
            subParts.Add(Fui.Temp(tile.Temp, m.Fahrenheit) + TempTag(tile));
        if (!double.IsNaN(tile.Power))
            subParts.Add(Fui.Watts(tile.Power));
        if (tile.Kind is not ("net" or "disk") && !string.IsNullOrEmpty(tile.Detail))
            subParts.Add(tile.Detail);
        string sub = string.Join("  ", subParts);
        string name = ShortName(tile).ToUpperInvariant();

        float fv = Value(u), fs = Small(u), fn = Micro(u);
        float w = Math.Max(Fui.Measure(value, fv, "Medium"), Math.Max(Fui.Measure(sub, fs, "Medium"), Fui.Measure(name, fn, "Bold", 0.6f * u)));
        double cos = Math.Cos(angle * Math.PI / 180), sin = Math.Sin(angle * Math.PI / 180);
        float gap = 16 * u;
        float x, yTop;
        SKTextAlign align;
        SKPoint lineFrom, lineTo;

        bool side = Math.Abs(cos) >= 0.4;
        if (side)
        {
            bool right = cos > 0;
            x = right ? p.X + r + gap : p.X - r - gap;
            if (right && x + w > area.Right)
                side = false;
            else if (!right && x - w < area.Left)
                side = false;
            align = right ? SKTextAlign.Left : SKTextAlign.Right;
            yTop = p.Y - 20 * u;
            lineFrom = new SKPoint(p.X + (right ? r : -r), p.Y);
            lineTo = new SKPoint(x - (right ? 6 : -6) * u, p.Y);
        }
        else
        {
            x = 0; yTop = 0; align = SKTextAlign.Center; lineFrom = lineTo = p;
        }

        if (!side)
        {
            align = SKTextAlign.Center;
            x = Math.Max(area.Left + w / 2, Math.Min(area.Right - w / 2, p.X));
            bool above = sin < 0;
            yTop = above ? p.Y - r - gap - 46 * u : p.Y + r + gap;
            lineFrom = new SKPoint(p.X, p.Y + (above ? -r : r));
            lineTo = new SKPoint(p.X, above ? yTop + 46 * u : yTop - 4 * u);
        }

        using (SKPaint lead = Fui.Stroke(col.WithAlpha(0.7f), u))
            c.DrawLine(lineFrom, lineTo, lead);

        Fui.Text(c, name, x, yTop + fn * 0.9f, fn, Cool, "Bold", align, 0.6f * u);
        Fui.Text(c, value, x, yTop + fn + fv + 2 * u, fv, Data, "Medium", align, 0, 2 * u);
        Fui.Text(c, sub, x, yTop + fn + fv + fs + 7 * u, fs, Dim, "Medium", align);
    }

    // ------------------------------------------------------------------ left column

    private static void DrawLeftColumn(SKCanvas c, SKRect col, float u, DashboardModel m)
    {
        float y = Header(c, "HARDWARE NODES", m.Tiles.Count.ToString("00", Inv), col.Left, col.Right, col.Top, u);

        float gap = 8 * u;
        float tileW = (col.Width - gap) / 2, tileH = Math.Max(78, 76 * u);
        int i = 0;
        foreach (HardwareTile tile in m.Tiles)
        {
            float x = col.Left + (i % 2) * (tileW + gap);
            float ty = y + (i / 2) * (tileH + gap);
            DrawTile(c, new SKRect(x, ty, x + tileW, ty + tileH), tile, u, m);
            i++;
        }
        y += (float)Math.Ceiling(i / 2.0) * (tileH + gap) + 12 * u;

        // core matrix: one cell per CPU core with its load in %
        int cores = m.CoreLoads.Length;
        y = Header(c, "CORE MATRIX // LOAD %", cores.ToString("00", Inv), col.Left, col.Right, y, u);
        if (cores > 0)
        {
            int perRow = cores <= 6 ? cores : cores <= 12 ? 6 : 8;
            float cellGap = 5 * u;
            float cell = (col.Width - (perRow - 1) * cellGap) / perRow;
            float cellH = Math.Max(34, 34 * u);
            for (int k = 0; k < cores; k++)
            {
                float cx = col.Left + (k % perRow) * (cell + cellGap);
                float cy = y + (k / perRow) * (cellH + cellGap);
                double load = m.CoreLoads[k];
                SKColor color = LoadColor(load);
                float f = (float)(load / 100);
                using (SKPaint back = Fui.Fill(Panel))
                    c.DrawRect(cx, cy, cell, cellH, back);
                using (SKPaint fill = Fui.Fill(color.WithAlpha(0.16f + 0.38f * f)))
                    c.DrawRect(cx, cy + cellH * (1 - f), cell, cellH * f, fill);
                using (SKPaint border = Fui.Stroke(color.WithAlpha(0.6f), u))
                    c.DrawRect(cx, cy, cell, cellH, border);
                Fui.Text(c, "C" + (k + 1).ToString(Inv), cx + 4 * u, cy + Micro(u), Micro(u), Text.WithAlpha(0.8f), "Medium");
                Fui.Text(c, load.ToString("0", Inv), cx + cell - 4 * u, cy + cellH - 5 * u, Small(u), Text, "Bold", SKTextAlign.Right);
            }
            y += (float)Math.Ceiling(cores / (double)perRow) * (cellH + cellGap) + 12 * u;
        }
        else
        {
            Fui.Text(c, "n/a  (NO PER-CORE LOAD SENSORS)", col.Left, y + 14 * u, Micro(u), Dim, "Medium");
            y += 28 * u;
        }

        // thermals: every component against its own limit
        if (y < col.Bottom - 60 * u && m.Thermals.Count > 0)
        {
            y = Header(c, "THERMALS // OF LIMIT", m.Thermals.Count.ToString("00", Inv), col.Left, col.Right, y, u);
            float rowH = Math.Max(26, 24 * u);
            foreach (ThermalEntry entry in m.Thermals)
            {
                if (y + rowH > col.Bottom - 26 * u)
                    break;
                double ratio = (entry.Temp - 30) / Math.Max(1, entry.Limit - 30);
                SKColor colr = HeatColor(ratio);
                Fui.Text(c, Shorten(entry.Name, 22).ToUpperInvariant(), col.Left, y + Micro(u), Micro(u), Dim, "Medium");
                Fui.Text(c, Fui.Temp(entry.Temp, m.Fahrenheit) + " / " + Fui.Temp(entry.Limit, m.Fahrenheit) + HeatTag(ratio), col.Right, y + Micro(u), Micro(u), colr, "Bold", SKTextAlign.Right);
                Fui.Bar(c, new SKRect(col.Left, y + Micro(u) + 5 * u, col.Right, y + Micro(u) + 8 * u), ratio, colr, Track);
                y += rowH;
            }
        }

        // color legend for all heat colors
        float ly = col.Bottom - 2 * u;
        float lx = col.Left;
        foreach (var (label, color) in new[] { ("OK", Accent), ("WARM ≥60%", Data), ("HOT ≥85% OF LIMIT", Alarm) })
        {
            using (SKPaint sw = Fui.Fill(color))
                c.DrawRect(lx, ly - 8 * u, 8 * u, 8 * u, sw);
            lx += 12 * u;
            lx += Fui.Text(c, label, lx, ly, Micro(u), Dim, "Medium") + 14 * u;
        }
    }

    private static void DrawTile(SKCanvas c, SKRect r, HardwareTile tile, float u, DashboardModel m)
    {
        SKColor col = HeatColor(tile);
        using (SKPaint back = Fui.Fill(Panel.WithAlpha(0.9f)))
            c.DrawRect(r, back);
        using (SKPath frame = Fui.CutFrame(r, 9 * u))
        using (SKPaint border = Fui.Stroke(Track, u))
            c.DrawPath(frame, border);
        using (SKPaint edge = Fui.Stroke(col, 2.4f * u))
            c.DrawLine(r.Left, r.Top + 6 * u, r.Left, r.Bottom - 6 * u, edge);

        float x = r.Left + 10 * u;
        Fui.Text(c, tile.Label, x, r.Top + Micro(u) + 5 * u, Micro(u), Dim, "Bold", spacing: 0.8f * u);
        string temp = !double.IsNaN(tile.Temp) ? Fui.Temp(tile.Temp, m.Fahrenheit) + TempTag(tile) : "";
        Fui.Text(c, temp, r.Right - 9 * u, r.Top + Micro(u) + 5 * u, Micro(u), col, "Bold", SKTextAlign.Right);

        string value = tile.Kind == "power" ? Fui.Watts(tile.Power) : tile.Kind is "net" ? tile.Detail.Replace("↓ ", "") : Fui.Pct(tile.Load);
        float vs = Math.Max(18, (value.Length > 7 ? 15 : 21) * u);
        Fui.Text(c, value, x, r.Top + Micro(u) + vs + 9 * u, vs, Text, "Medium");

        Fui.Bar(c, new SKRect(x, r.Bottom - 15 * u, r.Right - 10 * u, r.Bottom - 11 * u), Nz(tile.Load) / 100, Accent, Track);
        double heat = double.IsNaN(tile.Temp) ? double.NaN : (tile.Temp - 30) / Math.Max(1, tile.TempLimit - 30);
        Fui.Bar(c, new SKRect(x, r.Bottom - 8 * u, r.Right - 10 * u, r.Bottom - 5 * u), heat, col, Track);
    }

    // ------------------------------------------------------------------ right column

    private static void DrawRightColumn(SKCanvas c, SKRect col, float u, DashboardModel m)
    {
        float y = Header(c, "TELEMETRY", "", col.Left, col.Right, col.Top, u);

        string est = m.OtherPowerEstimated ? "≈ " : "";
        var rows = new List<(string Label, string Value, SKColor Color)>
        {
            ("TOTAL POWER", est + Fui.Watts(m.TotalPower) + (m.OtherPowerEstimated ? " EST" : ""), Data),
            ("CPU PACKAGE", NA(Fui.Watts(m.CpuPower)), Text),
            ("GPU BOARD", NA(Fui.Watts(m.GpuPower)), Text),
            ("MEMORY", NA(Fui.Pct(m.MemoryLoad)) + (double.IsNaN(m.MemoryUsedGb) ? "" : "  " + m.MemoryUsedGb.ToString("0.0", Inv) + " GB"), Text),
            ("DISK I/O", SystemMetrics.Rate(m.DiskRead + m.DiskWrite), Text),
            ("NETWORK", "↓ " + SystemMetrics.Rate(m.NetDown) + "  ↑ " + SystemMetrics.Rate(m.NetUp), Text),
            ("SESSION ENERGY", est + m.SessionEnergyWh.ToString("0.0", Inv) + " WH", Data)
        };

        float rowH = Math.Max(22, 21 * u);
        foreach (var row in rows)
        {
            Fui.Text(c, row.Label, col.Left, y + Small(u), Micro(u), Dim, "Bold", spacing: 0.6f * u);
            Fui.Text(c, row.Value, col.Right, y + Small(u), Small(u), row.Color, "Medium", SKTextAlign.Right);
            using (SKPaint sep = Fui.Stroke(Rule, u))
                c.DrawLine(col.Left, y + rowH - 1 * u, col.Right, y + rowH - 1 * u, sep);
            y += rowH;
        }
        y += 12 * u;

        // energy distribution; the estimated rest is hatched, not solid
        y = Header(c, "ENERGY DISTRIBUTION", m.OtherPowerEstimated ? "REST ESTIMATED" : "MEASURED", col.Left, col.Right, y, u);
        double cpuP = Nz(m.CpuPower), gpuP = Nz(m.GpuPower), rest = Nz(m.OtherPower), sum = Math.Max(1, cpuP + gpuP + rest);
        var parts = new[] { ("CPU", cpuP, Accent, false), ("GPU", gpuP, Data, false), ("REST", rest, Cool, m.OtherPowerEstimated) };
        float bx = col.Left;
        var bar = new SKRect(col.Left, y, col.Right, y + 12 * u);
        foreach (var (label, value, color, hatched) in parts)
        {
            float w = (float)(value / sum) * bar.Width;
            var seg = new SKRect(bx, bar.Top, bx + Math.Max(0, w - 2 * u), bar.Bottom);
            if (hatched)
                Hatch(c, seg, color, u);
            else
            {
                using SKPaint p = Fui.Fill(color);
                c.DrawRect(seg, p);
            }
            bx += w;
        }
        y += 28 * u;
        float lx = col.Left;
        foreach (var (label, value, color, hatched) in parts)
        {
            if (hatched)
                Hatch(c, new SKRect(lx, y - 9 * u, lx + 9 * u, y), color, u);
            else
            {
                using SKPaint sw = Fui.Fill(color);
                c.DrawRect(lx, y - 9 * u, 9 * u, 9 * u, sw);
            }
            lx += 13 * u;
            lx += Fui.Text(c, label + " " + (hatched ? "≈" : "") + (value / sum * 100).ToString("0", Inv) + "%", lx, y, Small(u), Text, "Medium") + 16 * u;
        }
        if (m.OtherPowerEstimated)
        {
            y += Micro(u) + 8 * u;
            Fui.Text(c, "REST = BOARD + RAM + DRIVES + FANS (ESTIMATE)", col.Left, y, Micro(u), Dim, "Medium");
        }
        y += 20 * u;

        // stress composition: the four parts of the index
        y = Header(c, "STRESS COMPOSITION", Math.Round(m.Stress).ToString(Inv) + " / 100", col.Left, col.Right, y, u);
        var comp = new[] { ("LOAD  max CPU/GPU", m.StressLoad, Accent), ("HEAT  of limit", m.StressHeat, Data), ("MEMORY", m.StressMemory, Cool), ("FANS  effort", m.StressFans, Fui.Hex("#B8A0F0")) };
        foreach (var (label, value, color) in comp)
        {
            Fui.Text(c, label.ToUpperInvariant(), col.Left, y + Micro(u), Micro(u), Dim, "Medium");
            Fui.Text(c, value.ToString("0", Inv), col.Right, y + Micro(u), Small(u), Text, "Bold", SKTextAlign.Right);
            Fui.Bar(c, new SKRect(col.Left, y + Micro(u) + 5 * u, col.Right, y + Micro(u) + 9 * u), value / 100, color, Track);
            y += Math.Max(26, 25 * u);
        }
        y += 8 * u;

        // thermal headroom ring: distance of the hottest component to its limit
        float ringR = Math.Min(42 * u, (col.Bottom - y) * 0.22f);
        if (ringR > 22)
        {
            y = Header(c, "THERMAL HEADROOM", string.IsNullOrEmpty(m.HottestName) ? "n/a" : Shorten(m.HottestName, 18).ToUpperInvariant(), col.Left, col.Right, y, u);
            var rc = new SKPoint(col.Left + ringR + 4 * u, y + ringR + 6 * u);
            double used = m.ThermalRatio;
            using (SKPaint track = Fui.Stroke(Track, 7 * u))
                c.DrawCircle(rc, ringR, track);
            using (SKPaint arc = Fui.Stroke(HeatColor(used), 7 * u))
            {
                arc.StrokeCap = SKStrokeCap.Round;
                DrawArc(c, rc, ringR, -90, (float)(used * 360), arc);
            }
            string k = double.IsNaN(m.ThermalHeadroom) ? "n/a" : m.ThermalHeadroom.ToString("0", Inv) + " K";
            Fui.Text(c, k, rc.X, rc.Y + 6 * u, Math.Max(15, 17 * u), Text, "Bold", SKTextAlign.Center, 0, 2 * u);

            float sx = rc.X + ringR + 18 * u;
            Fui.Text(c, "TO THE NEAREST LIMIT", sx, rc.Y - 10 * u, Micro(u), Dim, "Medium");
            Fui.Text(c, "HOTTEST  " + (double.IsNaN(m.HottestTemp) ? "n/a" : Fui.Temp(m.HottestTemp, m.Fahrenheit)), sx, rc.Y + 8 * u, Small(u), Text, "Medium");
            Fui.Text(c, "RING = SHARE OF LIMIT USED", sx, rc.Y + 26 * u, Micro(u), Dim, "Medium");
            y = rc.Y + ringR + 18 * u;
        }

        // power oscilloscope with a scale
        float scopeH = Math.Min(170 * u, col.Bottom - y - 30 * u);
        if (scopeH > 60)
        {
            string span = Math.Max(1, m.PowerHistory.Length * m.UpdateIntervalMs / 60000).ToString(Inv);
            y = Header(c, "POWER // " + span + " MIN", est + Fui.Watts(m.TotalPower), col.Left, col.Right, y, u);
            DrawScope(c, new SKRect(col.Left + 44 * u, y, col.Right, y + scopeH), m.PowerHistory, u, m.OtherPowerEstimated);
        }
    }

    private static void DrawScope(SKCanvas c, SKRect r, double[] values, float u, bool estimated)
    {
        using (SKPaint back = Fui.Fill(Panel.WithAlpha(0.8f)))
            c.DrawRect(r, back);
        Fui.Brackets(c, r, 8 * u, Accent.WithAlpha(0.7f), u);

        double max = values.Length > 0 ? Math.Max(10, values.Max() * 1.15) : 100;
        max = Math.Ceiling(max / 50) * 50;
        using (SKPaint grid = Fui.Stroke(Rule, u))
        {
            for (int i = 0; i <= 4; i++)
            {
                float gy = r.Bottom - r.Height * i / 4;
                c.DrawLine(r.Left, gy, r.Right, gy, grid);
                Fui.Text(c, (max * i / 4).ToString("0", Inv), r.Left - 6 * u, gy + 4 * u, Micro(u), Dim, "Medium", SKTextAlign.Right);
            }
        }

        if (values.Length < 2)
            return;

        using var line = new SKPath();
        using var area = new SKPath();
        float py(double v) => r.Bottom - (float)(v / max) * r.Height;
        for (int i = 0; i < values.Length; i++)
        {
            float x = r.Left + r.Width * i / (values.Length - 1);
            if (i == 0)
            {
                line.MoveTo(x, py(values[i]));
                area.MoveTo(x, r.Bottom);
            }
            else
            {
                line.LineTo(x, py(values[i]));
            }
            area.LineTo(x, py(values[i]));
        }
        area.LineTo(r.Right, r.Bottom);
        area.Close();

        using (var fill = new SKPaint { Shader = SKShader.CreateLinearGradient(new SKPoint(0, r.Top), new SKPoint(0, r.Bottom), new[] { Data.WithAlpha(0.22f), Data.WithAlpha(0f) }, null, SKShaderTileMode.Clamp) })
            c.DrawPath(area, fill);
        using (SKPaint p = Fui.Stroke(Data, 2 * u))
            c.DrawPath(line, p);

        // min and max markers
        int iMax = Array.IndexOf(values, values.Max()), iMin = Array.IndexOf(values, values.Min());
        foreach (var (idx, label) in new[] { (iMax, "MAX "), (iMin, "MIN ") })
        {
            float x = r.Left + r.Width * idx / (values.Length - 1), y = py(values[idx]);
            using (SKPaint dot = Fui.Fill(Text))
                c.DrawCircle(x, y, 3 * u, dot);
            SKTextAlign align = x > r.MidX ? SKTextAlign.Right : SKTextAlign.Left;
            Fui.Text(c, label + values[idx].ToString("0", Inv) + " W", x + (align == SKTextAlign.Left ? 6 : -6) * u, y + (label == "MAX " ? -6 : 14) * u, Micro(u), Text, "Medium", align);
        }
    }

    // ------------------------------------------------------------------ strips

    private static void DrawTopStrip(SKCanvas c, int w, float h, float u, DashboardModel m)
    {
        using (SKPaint back = Fui.Fill(SKColors.Black.WithAlpha(0.55f)))
            c.DrawRect(0, 0, w, h, back);
        using (SKPaint rule = Fui.Stroke(Rule, u))
            c.DrawLine(0, h, w, h, rule);

        float y = h / 2 + 5 * u;
        float x = 18 * u;
        x += Fui.Text(c, "K26K", x, y, Body(u) + 2, Accent, "Bold", spacing: 1.6f * u, glow: 2.5f * u) + 10 * u;
        x += Fui.Text(c, "SYSTEM CORE", x, y, Body(u), Text, "Bold", spacing: 1.2f * u) + 28 * u;
        foreach (var (label, value) in new[] { ("NODE", m.Machine.ToUpperInvariant()), ("SESSION", Fui.Duration(m.SessionSeconds)), ("SENSORS", m.SensorCount.ToString(Inv)) })
        {
            x += Fui.Text(c, label, x, y, Micro(u), Dim, "Bold", spacing: 0.8f * u) + 8 * u;
            x += Fui.Text(c, value, x, y, Small(u), Text, "Medium") + 24 * u;
        }

        Fui.Text(c, m.Clock, w - 18 * u, y + 1 * u, Math.Max(16, 16 * u), Data, "Bold", SKTextAlign.Right, 0.6f * u, 3 * u);
        float cw = Fui.Measure(m.Clock, Math.Max(16, 16 * u), "Bold", 0.6f * u);
        Fui.Text(c, m.Date, w - 30 * u - cw, y, Small(u), Dim, "Medium", SKTextAlign.Right);
    }

    private static SKRect DrawBottomStrip(SKCanvas c, int w, int h, float top, float u, DashboardModel m, FancyOptions o)
    {
        using (SKPaint back = Fui.Fill(SKColors.Black.WithAlpha(0.55f)))
            c.DrawRect(0, top, w, h - top, back);
        using (SKPaint rule = Fui.Stroke(Rule, u))
            c.DrawLine(0, top, w, top, rule);

        float y = top + (h - top) / 2 + 5 * u;
        float x = 18 * u;

        // state indicator (read only): the active state is lit and underlined
        x += Fui.Text(c, "SYSTEM STATE", x, y, Micro(u), Dim, "Bold", spacing: 0.8f * u) + 14 * u;
        foreach (string state in new[] { "NOMINAL", "LOAD", "THERMAL" })
        {
            bool active = m.State == state;
            SKColor col = state == "THERMAL" ? Alarm : state == "LOAD" ? Data : Accent;
            string text = (active ? "● " : "○ ") + state;
            float tw = Fui.Text(c, text, x, y, Small(u), active ? col : Dim.WithAlpha(0.7f), "Bold", spacing: 0.6f * u);
            if (active)
            {
                using SKPaint underline = Fui.Stroke(col, 2 * u);
                c.DrawLine(x, y + 6 * u, x + tw, y + 6 * u, underline);
            }
            x += tw + 18 * u;
        }

        x += 16 * u;
        x += Fui.Text(c, "INTERVAL", x, y, Micro(u), Dim, "Bold", spacing: 0.8f * u) + 8 * u;
        x += Fui.Text(c, (m.UpdateIntervalMs / 1000.0).ToString("0.0#", Inv) + " S", x, y, Small(u), Text, "Medium") + 24 * u;
        if (o.ReducedMotion)
            Fui.Text(c, "REDUCED MOTION", x, y, Small(u), Cool, "Bold", spacing: 0.6f * u);

        string exitText = "EXIT FANCY  [ESC]";
        float ew = Fui.Measure(exitText, Small(u), "Bold", 0.8f * u) + 28 * u;
        var exit = new SKRect(w - 18 * u - ew, y - 17 * u, w - 18 * u, y + 8 * u);
        using (SKPaint f = Fui.Fill(o.ExitHover ? Accent : Accent.WithAlpha(0.1f)))
            c.DrawRect(exit, f);
        using (SKPaint b = Fui.Stroke(Accent, u))
            c.DrawRect(exit, b);
        Fui.Text(c, exitText, exit.MidX, y, Small(u), o.ExitHover ? Bg : Accent, "Bold", SKTextAlign.Center, 0.8f * u);
        return exit;
    }

    private static void DrawBoot(SKCanvas c, int w, int h, float u, double boot)
    {
        float alpha = (float)Math.Max(0, 1 - Math.Max(0, boot - 0.75) / 0.25);
        using (SKPaint veil = Fui.Fill(Bg.WithAlpha(0.6f * alpha)))
            c.DrawRect(0, 0, w, h, veil);
        Fui.Text(c, "INITIALIZING SENSOR GRID", w / 2f, h / 2f - 8 * u, Math.Max(16, 17 * u), Accent.WithAlpha(alpha), "Bold", SKTextAlign.Center, 3 * u, 3 * u);
        var bar = new SKRect(w / 2f - 180 * u, h / 2f + 10 * u, w / 2f + 180 * u, h / 2f + 14 * u);
        Fui.Bar(c, bar, boot, Accent.WithAlpha(alpha), Track.WithAlpha(alpha));
        Fui.Text(c, (boot * 100).ToString("000", Inv) + " %", w / 2f, h / 2f + 36 * u, Small(u), Dim.WithAlpha(alpha), "Medium", SKTextAlign.Center, 1.5f * u);
    }

    // ------------------------------------------------------------------ helpers

    private static float Header(SKCanvas c, string title, string meta, float left, float right, float y, float u)
    {
        Fui.Text(c, title, left, y + Small(u), Small(u), Accent, "Bold", spacing: 0.8f * u);
        if (!string.IsNullOrEmpty(meta))
            Fui.Text(c, meta, right, y + Small(u), Micro(u), Dim, "Medium", SKTextAlign.Right);
        float ly = y + Small(u) + 7 * u;
        using (SKPaint rule = Fui.Stroke(Track, u))
            c.DrawLine(left, ly, right, ly, rule);
        using (SKPaint tick = Fui.Stroke(Accent, 2 * u))
            c.DrawLine(left, ly, left + 20 * u, ly, tick);
        return ly + 10 * u;
    }

    private static void Hatch(SKCanvas c, SKRect r, SKColor color, float u)
    {
        using (SKPaint border = Fui.Stroke(color, u))
            c.DrawRect(r, border);
        c.Save();
        c.ClipRect(r);
        using SKPaint p = Fui.Stroke(color, 1.4f * u);
        for (float x = r.Left - r.Height; x < r.Right; x += 5 * u)
            c.DrawLine(x, r.Bottom, x + r.Height, r.Top, p);
        c.Restore();
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
        return HeatColor((tile.Temp - 30) / Math.Max(1, tile.TempLimit - 30));
    }

    private static SKColor HeatColor(double ratio) => ratio >= 0.85 ? Alarm : ratio >= 0.6 ? Data : Accent;

    private static string HeatTag(double ratio) => ratio >= 0.85 ? "  ▲ HOT" : ratio >= 0.6 ? "  WARM" : "";

    private static string TempTag(HardwareTile tile) => double.IsNaN(tile.Temp) ? "" : HeatTag((tile.Temp - 30) / Math.Max(1, tile.TempLimit - 30));

    private static SKColor LoadColor(double load) => load >= 85 ? Alarm : load >= 60 ? Data : Accent;

    private static SKColor StressColor(double stress) => stress >= 80 ? Alarm : stress >= 50 ? Data : Accent;

    private static string NA(string value) => value == "--" ? "n/a" : value;

    // "NVIDIA GeForce RTX 2070 SUPER" -> "RTX 2070 SUPER", long board names cut at a word boundary
    private static string ShortName(HardwareTile tile)
    {
        string name = tile.Name ?? "";
        foreach (string prefix in new[] { "NVIDIA GeForce ", "NVIDIA ", "AMD Radeon ", "AMD Ryzen ", "Intel(R) Core(TM) ", "Intel ", "AMD " })
        {
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                name = (prefix.StartsWith("AMD Ryzen") ? "Ryzen " : "") + name.Substring(prefix.Length);
                break;
            }
        }
        if (name.Length <= 18)
            return name;
        int cut = name.LastIndexOf(' ', 18);
        return cut > 6 ? name.Substring(0, cut) : name.Substring(0, 18);
    }

    private static string Shorten(string text, int max)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= max)
            return text ?? "";
        return text.Substring(0, max - 1) + "…";
    }

    private static double Nz(double v) => double.IsNaN(v) ? 0 : v;
}
