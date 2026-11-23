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

/// <summary>All: everything (snapshots, boot). Static: everything that only changes with the data. Motion: the moving parts on top.</summary>
public enum FancyLayer
{
    All,
    Static,
    Motion
}

public sealed class FancyOptions
{
    public FancyLayer Layer { get; set; } = FancyLayer.All;
    public bool ReducedMotion { get; set; }
    /// <summary>Seconds since the dashboard was opened (drives the boot sequence).</summary>
    public double SinceOpen { get; set; } = 10;
    public bool ExitHover { get; set; }
    /// <summary>Soft glow around lines, nodes and text; off = calm, flat look for long viewing.</summary>
    public bool Glow { get; set; } = true;
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
    public static readonly SKColor Dim = Fui.Hex("#9CC4B1");
    public static readonly SKColor Accent = Fui.Hex("#6BE8A4");
    public static readonly SKColor Data = Fui.Hex("#E8B040");
    public static readonly SKColor Cool = Fui.Hex("#7DD8C4");
    public static readonly SKColor Alarm = Fui.Hex("#FF8FA3");
    public static readonly SKColor Rule = Fui.Hex("#1F3A2E");
    public static readonly SKColor Track = Fui.Hex("#2A4A3C");
    /// <summary>Track behind bars and rings that carry information (at least 3:1 against the panel).</summary>
    public static readonly SKColor TrackStrong = Fui.Hex("#4A7A64");

    private const double BootSeconds = 2.6;
    private static readonly CultureInfo Inv = CultureInfo.CurrentCulture;

    // Type scale: sizes grow with the window, but never below the legible minimum (in pixels).
    private static float Micro(float u) => Math.Max(12f, 10f * u);
    private static float Small(float u) => Math.Max(12f, 10.5f * u);
    private static float Body(float u) => Math.Max(13f, 11.5f * u);
    private static float Value(float u) => Math.Max(16f, 15f * u);

    /// <summary>The area in which things move (orbital view); animation frames only redraw this part.</summary>
    public static SKRect MotionArea(int width, int height)
    {
        float u = Math.Max(0.8f, Math.Min(2.6f, Math.Min(width / 1600f, height / 900f)));
        float top = 40 * u, bottom = height - 46 * u;
        return new SKRect(18 * u + 310 * u + 24 * u, top, width - 18 * u - 350 * u - 24 * u, bottom);
    }

    /// <summary>Renders one frame. Returns the hit area of the exit button.</summary>
    public static SKRect Render(SKCanvas c, int width, int height, float dpiScale, DashboardModel m, double t, FancyOptions o)
    {
        Fui.GlowEnabled = o.Glow;
        try
        {
            return RenderFrame(c, width, height, m, t, o);
        }
        finally
        {
            Fui.GlowEnabled = true;
        }
    }

    private static SKRect RenderFrame(SKCanvas c, int width, int height, DashboardModel m, double t, FancyOptions o)
    {
        float u = Math.Max(0.8f, Math.Min(2.6f, Math.Min(width / 1600f, height / 900f)));
        double boot = o.ReducedMotion ? 1 : Math.Min(1, o.SinceOpen / BootSeconds);
        float ease = (float)(1 - Math.Pow(1 - boot, 3));

        bool still = o.Layer != FancyLayer.Motion;
        if (still)
        {
            c.Clear(Bg);
            DrawBackground(c, width, height, u, o.Glow);
        }

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
        if (!still)
        {
            c.RestoreToCount(layer);
            return SKRect.Empty;
        }

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

    private static void DrawBackground(SKCanvas c, int w, int h, float u, bool glowOn)
    {
        using (SKPaint grid = Fui.Stroke(Accent.WithAlpha(0.03f)))
        {
            float step = 32 * u;
            for (float x = 0; x < w; x += step)
                c.DrawLine(x, 0, x, h, grid);
            for (float y = 0; y < h; y += step)
                c.DrawLine(0, y, w, y, grid);
        }

        if (!glowOn)
            return;
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
        // rings only turn in single renders (snapshots); the live view caches them
        double rot = o.ReducedMotion || o.Layer != FancyLayer.All ? 0 : t * 3;
        bool still = o.Layer != FancyLayer.Motion;
        bool motion = o.Layer != FancyLayer.Static && !o.ReducedMotion;

        // orbit rings with ticks and labels
        string[] ringNames = { "ORBIT A // COMPUTE", "ORBIT B // PERIPHERY", "ORBIT C // FIELD" };
        var ringLabels = new List<(string Text, SKPoint At)>();
        for (int i = 0; i < orbits.Length && still; i++)
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
                ringLabels.Add((ringNames[i], new SKPoint(cp.X + (float)Math.Cos(la) * (r + 6 * u), cp.Y + (float)Math.Sin(la) * (r + 6 * u))));
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
        for (int i = 0; i < nodes.Count && nodes.Count > 2 && still; i++)
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
            if (still)
            {
                if (o.Glow)
                {
                    using SKPaint halo = Fui.Stroke(node.Color.WithAlpha(0.14f), thick * 2.2f);
                    halo.MaskFilter = Fui.Blur(2.5f * u);
                    c.DrawPath(path, halo);
                }
                using SKPaint line = Fui.Stroke(node.Color.WithAlpha(0.85f), thick);
                line.StrokeCap = SKStrokeCap.Round;
                c.DrawPath(path, line);
            }

            // particles: count and speed follow the activity of the node
            if (motion)
            {
                using var measure = new SKPathMeasure(path);
                float len = measure.Length;
                int count = 2 + (int)(Nz(node.Tile.Load) / 25);
                double speed = 0.12 + Nz(node.Tile.Load) / 100 * 0.4;
                using SKPaint dot = Fui.Fill(SKColors.White.WithAlpha(0.85f));
                using SKPaint dotGlow = Fui.Fill(node.Color.WithAlpha(0.35f));
                for (int k = 0; k < count; k++)
                {
                    double f = (t * speed + k / (double)count + node.Angle / 360) % 1;
                    if (measure.GetPosition((float)(f * len), out SKPoint pos))
                    {
                        c.DrawCircle(pos, 3.4f * u, dotGlow);
                        c.DrawCircle(pos, 1.3f * u, dot);
                    }
                }
            }
        }

        if (still)
        {
            // ring labels on a dark plate above the energy lines, so no line runs through the text
            foreach (var (text, at) in ringLabels)
            {
                float lw = Fui.Measure(text, Small(u), "Medium", 0.6f * u);
                using (SKPaint plate = Fui.Fill(Bg.WithAlpha(0.72f)))
                    c.DrawRect(new SKRect(at.X - lw - 4 * u, at.Y - Small(u) - 1 * u, at.X + 4 * u, at.Y + 5 * u), plate);
                Fui.Text(c, text, at.X, at.Y, Small(u), Dim, "Medium", SKTextAlign.Right, 0.6f * u);
            }
            DrawCore(c, cp, coreR, u, m, t, o, ease);
        }

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
        // one color scale for the number, the ring and the state word: the state decides
        SKColor col = StateColor(m.State);
        double pulse = o.ReducedMotion || o.Layer != FancyLayer.All ? 0.5 : 0.5 + 0.5 * Math.Sin(t * 1.6);

        if (o.Glow)
        {
            using var glow = new SKPaint { Shader = SKShader.CreateRadialGradient(cp, r * 1.7f, new[] { col.WithAlpha(0.16f + 0.05f * (float)pulse), SKColors.Transparent }, null, SKShaderTileMode.Clamp) };
            c.DrawCircle(cp, r * 1.7f, glow);
        }

        using (SKPaint face = Fui.Fill(Panel))
            c.DrawCircle(cp, r, face);

        // outer ring: the stress index itself
        using (SKPaint track = Fui.Stroke(Track, 6 * u))
            c.DrawCircle(cp, r - 7 * u, track);
        float sweep = (float)(Math.Max(0, Math.Min(100, m.Stress)) / 100 * 360 * ease);
        if (o.Glow)
        {
            using SKPaint halo = Fui.Stroke(col.WithAlpha(0.35f), 8 * u);
            halo.MaskFilter = Fui.Blur(3 * u);
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

        // every text inside the core must stay clear of the inner ring: its width is limited by the chord at its height
        float inner = ir - 6 * u;
        float Chord(float dy) => 2 * (float)Math.Sqrt(Math.Max(0, inner * inner - dy * dy));

        string title = Fui.Measure("SYSTEM STRESS", Micro(u), "Bold", 0.8f * u) <= Chord(r * 0.34f + Micro(u)) ? "SYSTEM STRESS" : "STRESS";
        Fui.Text(c, title, cp.X, cp.Y - r * 0.34f, Micro(u), Dim, "Bold", SKTextAlign.Center, 0.8f * u);
        Fui.Text(c, m.Stress.ToString("0", Inv), cp.X, cp.Y + r * 0.2f, r * 0.6f, col, "Bold", SKTextAlign.Center, 0, 4 * u);

        // the same wording as the header and the state strip; smaller or shorter when the core is small
        string state = (m.State is "THERMAL" or "LOAD" ? "▲ " : "● ") + SystemMetrics.StateLabel(m.State);
        // the place is chosen per layout with the longest state, so the label never jumps when the state changes
        float room = Chord(r * 0.5f);
        float stateSize = Small(u), stateSpacing = 0.8f * u;
        if (Fui.Measure("▲ THERMAL LIMIT", stateSize, "Bold", stateSpacing) > room)
            stateSpacing = 0;
        if (Fui.Measure("▲ THERMAL LIMIT", stateSize, "Bold", stateSpacing) <= room)
        {
            Fui.Text(c, state, cp.X, cp.Y + r * 0.5f, stateSize, col, "Bold", SKTextAlign.Center, stateSpacing);
            return;
        }

        // too small a core: the full state goes on a plate right below the core, never shortened or squeezed;
        // a dark margin around the plate keeps the energy lines clear of the text
        float plateSize = Body(u);
        float sw = Fui.Measure(state, plateSize, "Bold", 0.6f * u) + 18 * u;
        float top = cp.Y + r + 12 * u;
        var plate = new SKRect(cp.X - sw / 2, top, cp.X + sw / 2, top + plateSize + 12 * u);
        using (SKPaint back = Fui.Fill(Bg))
            c.DrawRect(SKRect.Inflate(plate, 5 * u, 4 * u), back);
        using (SKPaint fill = Fui.Fill(col.WithAlpha(0.08f)))
            c.DrawRect(plate, fill);
        using (SKPaint frame = Fui.Stroke(col.WithAlpha(0.8f), u))
            c.DrawRect(plate, frame);
        Fui.Text(c, state, cp.X, plate.Bottom - 7 * u, plateSize, col, "Bold", SKTextAlign.Center, 0.6f * u);
    }

    private static void DrawNode(SKCanvas c, HardwareTile tile, SKPoint p, float r, SKColor col, double angle, SKRect area, float u, DashboardModel m, double t, FancyOptions o)
    {
        double load = Nz(tile.Load);
        if (!o.ReducedMotion && o.Layer != FancyLayer.Static && load > 5)
        {
            // one slow ring per second at most
            double phase = (t * (0.35 + load / 250) + angle / 360) % 1;
            using SKPaint pulse = Fui.Stroke(col.WithAlpha((float)(0.5 * (1 - phase))), u);
            c.DrawCircle(p, r + (float)(phase * 14 * u), pulse);
        }

        if (o.Layer == FancyLayer.Motion)
            return;

        if (o.Glow)
        {
            using SKPaint halo = Fui.Fill(col.WithAlpha(0.18f));
            halo.MaskFilter = Fui.Blur(4.5f * u);
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
            subParts.Add(TempPrefix(tile) + Fui.Temp(tile.Temp, m.Fahrenheit) + TempTag(tile));
        if (!double.IsNaN(tile.Power))
            subParts.Add(Fui.Watts(tile.Power));
        if (tile.Kind is not ("net" or "disk") && !string.IsNullOrEmpty(tile.Detail))
            subParts.Add(tile.Detail);
        string sub = string.Join("  ", subParts);
        string name = ShortName(tile).ToUpperInvariant();

        float fv = Value(u), fs = Small(u), fn = Micro(u);
        float w = Math.Max(Fui.Measure(value, fv, "Medium"), Math.Max(Fui.Measure(sub, fs, "Medium"), Fui.Measure(name, fn, "Bold", 0.6f * u)));
        double cos = Math.Cos(angle * Math.PI / 180), sin = Math.Sin(angle * Math.PI / 180);
        float gap = 22 * u;
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
            // a bit flatter in small layouts, so the charts further down keep their height
            float cellH = Math.Max(28, 34 * u);
            for (int k = 0; k < cores; k++)
            {
                float cx = col.Left + (k % perRow) * (cell + cellGap);
                float cy = y + (k / perRow) * (cellH + cellGap);
                double load = m.CoreLoads[k];
                SKColor color = LoadColor(load);
                float f = (float)(load / 100);
                // the fill starts below the core label, so the label always sits on the dark panel
                float labelH = Micro(u) + 4 * u;
                float fillH = (cellH - labelH) * f;
                using (SKPaint back = Fui.Fill(Panel))
                    c.DrawRect(cx, cy, cell, cellH, back);
                using (SKPaint fill = Fui.Fill(color.WithAlpha(0.22f + 0.33f * f)))
                    c.DrawRect(cx, cy + cellH - fillH, cell, fillH, fill);
                bool warm = load >= 60 && load < 85;
                using (SKPaint border = Fui.Stroke(color.WithAlpha(warm ? 1f : 0.75f), warm ? 1.75f * u : u))
                {
                    // warm cells get a dashed border, hot cells a triangle: the state does not rely on color
                    if (warm)
                        border.PathEffect = SKPathEffect.CreateDash(new[] { 4 * u, 2.5f * u }, 0);
                    c.DrawRect(cx, cy, cell, cellH, border);
                }
                Fui.Text(c, "C" + (k + 1).ToString(Inv), cx + 4 * u, cy + Micro(u), Micro(u), Text, "Medium");
                Fui.Text(c, (load >= 85 ? "▲" : "") + load.ToString("0", Inv), cx + cell - 4 * u, cy + cellH - 5 * u, Small(u), Text, "Bold", SKTextAlign.Right);
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
                Fui.Bar(c, new SKRect(col.Left, y + Micro(u) + 5 * u, col.Right, y + Micro(u) + 8 * u), ratio, colr, TrackStrong);
                y += rowH;
            }
        }

        // color legend for all heat colors, right below the thermals
        float ly = y + Small(u) + 2 * u;
        float lx = col.Left;
        foreach (var (label, color) in new[] { ("OK", Accent), ("WARM ≥60%", Data), ("▲ HOT ≥85% OF LIMIT", Alarm) })
        {
            using (SKPaint sw = Fui.Fill(color))
                c.DrawRect(lx, ly - 9 * u, 9 * u, 9 * u, sw);
            lx += 13 * u;
            lx += Fui.Text(c, label, lx, ly, Small(u), Dim, "Medium") + 14 * u;
        }
        y = ly + 20 * u;

        // activity history fills the rest of the column
        float rest = col.Bottom - y;
        if (rest > 110 * u && m.CpuHistory.Length > 1)
        {
            // the scale follows the data in three fixed steps (25, 50, 100 %), so idle curves do not stick
            // to the floor and two views stay comparable
            double peak = Math.Max(m.CpuHistory.Max(), m.GpuHistory.Length > 0 ? m.GpuHistory.Max() : 0);
            double scale = peak * 1.15 <= 25 ? 25 : peak * 1.15 <= 50 ? 50 : 100;
            string span = Math.Max(1, m.CpuHistory.Length * m.UpdateIntervalMs / 60000).ToString(Inv);
            // in short columns the legend moves into the heading line, so the chart keeps its height
            bool compact = rest < 175 * u;
            y = Header(c, "ACTIVITY // " + span + " MIN", compact ? "" : "SCALE 0 TO " + scale.ToString("0", Inv) + " %", col.Left, col.Right, y, u);

            // legend with line samples and the numbers behind the curves
            float ky = y + Micro(u);
            float kx = col.Left;
            if (compact)
            {
                ky = y - 10 * u - 7 * u;
                kx = col.Right - (26 * u + Fui.Measure("CPU", Micro(u), "Bold") + 18 * u) * 2;
            }
            foreach (var (label, values, color, dashed) in new[] { ("CPU", m.CpuHistory, Accent, false), ("GPU", m.GpuHistory, Data, true) })
            {
                if (values.Length == 0)
                    continue;
                if (compact)
                {
                    using (SKPaint sample = Fui.Stroke(color, 2 * u))
                    {
                        if (dashed)
                            sample.PathEffect = SKPathEffect.CreateDash(new[] { 5 * u, 3 * u }, 0);
                        c.DrawLine(kx, ky - 4 * u, kx + 20 * u, ky - 4 * u, sample);
                    }
                    kx += 26 * u;
                    kx += Fui.Text(c, label, kx, ky, Micro(u), Text, "Bold") + 18 * u;
                    continue;
                }
                string stats = "AVG " + values.Average().ToString("0", Inv) + "  MAX " + values.Max().ToString("0", Inv);
                float need = 26 * u + Fui.Measure(label, Micro(u), "Bold") + 8 * u + Fui.Measure(stats, Micro(u), "Medium");
                if (kx > col.Left && kx + need > col.Right)
                {
                    // no room left on this line: continue below
                    kx = col.Left;
                    ky += Micro(u) + 8 * u;
                }
                using (SKPaint sample = Fui.Stroke(color, 2 * u))
                {
                    if (dashed)
                        sample.PathEffect = SKPathEffect.CreateDash(new[] { 5 * u, 3 * u }, 0);
                    c.DrawLine(kx, ky - 4 * u, kx + 20 * u, ky - 4 * u, sample);
                }
                kx += 26 * u;
                kx += Fui.Text(c, label, kx, ky, Micro(u), Text, "Bold") + 8 * u;
                kx += Fui.Text(c, stats, kx, ky, Micro(u), Dim, "Medium") + 18 * u;
            }
            if (!compact)
                y = ky + 10 * u;

            // in short columns the scale stands left of the chart, like in POWER, and never covers a curve
            var box = new SKRect(col.Left + (compact ? 34 * u : 0), y, col.Right, col.Bottom);
            using (SKPaint back = Fui.Fill(Panel.WithAlpha(0.8f)))
                c.DrawRect(box, back);
            if (compact)
            {
                Fui.Text(c, scale.ToString("0", Inv) + "%", box.Left - 6 * u, box.Top + Micro(u), Micro(u), Dim, "Medium", SKTextAlign.Right);
                Fui.Text(c, "0", box.Left - 6 * u, box.Bottom, Micro(u), Dim, "Medium", SKTextAlign.Right);
            }
            using (SKPaint grid = Fui.Stroke(Rule, u))
            {
                for (int g = 1; g < 4; g++)
                    c.DrawLine(box.Left, box.Top + box.Height * g / 4, box.Right, box.Top + box.Height * g / 4, grid);
            }
            Fui.Brackets(c, box, 8 * u, Accent.WithAlpha(0.7f), u);
            Spark(c, box, m.CpuHistory, scale, Accent, u, false);
            Spark(c, box, m.GpuHistory, scale, Data, u, true);

            // a young session has only a few samples; say so instead of leaving the box looking empty
            double seconds = m.CpuHistory.Length * m.UpdateIntervalMs / 1000.0;
            if (seconds < 60)
            {
                // top right corner on its own plate, so it never sits on a curve
                string note = "COLLECTING " + seconds.ToString("0", Inv) + " S";
                float nw = Fui.Measure(note, Micro(u), "Medium") + 12 * u;
                var plate = new SKRect(box.Right - 6 * u - nw, box.Top + 6 * u, box.Right - 6 * u, box.Top + 10 * u + Micro(u) + 2 * u);
                using (SKPaint back = Fui.Fill(Bg))
                    c.DrawRect(plate, back);
                Fui.Text(c, note, plate.MidX, plate.Bottom - 5 * u, Micro(u), Dim, "Medium", SKTextAlign.Center);
            }
        }
    }

    private static void Spark(SKCanvas c, SKRect r, double[] values, double scale, SKColor color, float u, bool dashed)
    {
        if (values.Length < 2)
            return;
        using var path = new SKPath();
        for (int i = 0; i < values.Length; i++)
        {
            float x = r.Left + r.Width * i / (values.Length - 1);
            float y = r.Bottom - 3 * u - (float)(Math.Max(0, Math.Min(scale, values[i])) / scale) * (r.Height - 6 * u);
            if (i == 0)
                path.MoveTo(x, y);
            else
                path.LineTo(x, y);
        }
        using SKPaint p = Fui.Stroke(color, 1.8f * u);
        if (dashed)
            p.PathEffect = SKPathEffect.CreateDash(new[] { 6 * u, 3 * u }, 0);
        c.DrawPath(path, p);
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
        string temp = !double.IsNaN(tile.Temp) ? TempPrefix(tile) + Fui.Temp(tile.Temp, m.Fahrenheit) + TempTag(tile) : "";
        Fui.Text(c, temp, r.Right - 9 * u, r.Top + Micro(u) + 5 * u, Micro(u), col, "Bold", SKTextAlign.Right);

        string value = tile.Kind == "power" ? Fui.Watts(tile.Power) : tile.Kind is "net" ? tile.Detail.Replace("↓ ", "") : Fui.Pct(tile.Load);
        float vs = Math.Max(18, (value.Length > 7 ? 15 : 21) * u);
        Fui.Text(c, value, x, r.Top + Micro(u) + vs + 9 * u, vs, Text, "Medium");

        Fui.Bar(c, new SKRect(x, r.Bottom - 15 * u, r.Right - 10 * u, r.Bottom - 11 * u), Nz(tile.Load) / 100, Accent, TrackStrong);
        double heat = double.IsNaN(tile.Temp) ? double.NaN : (tile.Temp - 30) / Math.Max(1, tile.TempLimit - 30);
        Fui.Bar(c, new SKRect(x, r.Bottom - 8 * u, r.Right - 10 * u, r.Bottom - 5 * u), heat, col, TrackStrong);
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
        if (rows.Any(r => r.Value.StartsWith("n/a", StringComparison.Ordinal)))
        {
            y += Micro(u) + 4 * u;
            Fui.Text(c, "n/a = NO SENSOR OR NEEDS ADMIN RIGHTS", col.Left, y, Micro(u), Dim, "Medium");
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
            Fui.Text(c, "REST ≈ BOARD + RAM + DRIVES + FANS", col.Left, y, Micro(u), Dim, "Medium");
        }
        y += 20 * u;

        // stress composition: the four parts of the index
        y = Header(c, "STRESS COMPOSITION", m.Stress.ToString("0", Inv) + " / 100", col.Left, col.Right, y, u);
        var comp = new[]
        {
            ("LOAD  max CPU/GPU", StressWeights.Load, m.StressLoad, Accent),
            ("HEAT  share of limit", StressWeights.Heat, m.StressHeat, Data),
            ("MEMORY", StressWeights.Memory, m.StressMemory, Cool),
            ("FANS  effort", StressWeights.Fans, m.StressFans, Fui.Hex("#B8A0F0"))
        };
        foreach (var (label, weight, value, color) in comp)
        {
            float lw = Fui.Text(c, label.ToUpperInvariant(), col.Left, y + Micro(u), Micro(u), Dim, "Medium");
            Fui.Text(c, "×" + weight.ToString("0.00", Inv), col.Left + lw + 8 * u, y + Micro(u), Micro(u), Cool, "Bold");
            Fui.Text(c, value.ToString("0", Inv), col.Right, y + Micro(u), Small(u), Text, "Bold", SKTextAlign.Right);
            Fui.Bar(c, new SKRect(col.Left, y + Micro(u) + 5 * u, col.Right, y + Micro(u) + 9 * u), value / 100, color, TrackStrong);
            y += Math.Max(26, 25 * u);
        }
        // two short lines, so the explanation never runs past the column
        Fui.Text(c, "INDEX = SUM OF WEIGHT × PART", col.Left, y + Micro(u) - 2 * u, Micro(u), Dim, "Medium");
        y += Micro(u) + 4 * u;
        Fui.Text(c, "BUT AT LEAST " + (StressWeights.Floor * 100).ToString("0", Inv) + "% OF MAX(LOAD, HEAT)", col.Left, y + Micro(u) - 2 * u, Micro(u), Dim, "Medium");
        y += Micro(u) + 14 * u;

        // thermal headroom ring: distance of the hottest component to its limit
        // a smaller ring in short columns, so the power scope below keeps a usable height
        float ringR = Math.Min(42 * u, (col.Bottom - y) * 0.17f);
        if (ringR > 22)
        {
            y = Header(c, "THERMAL HEADROOM", string.IsNullOrEmpty(m.HottestName) ? "n/a" : Shorten(m.HottestName, 18).ToUpperInvariant(), col.Left, col.Right, y, u);
            var rc = new SKPoint(col.Left + ringR + 4 * u, y + ringR + 6 * u);
            // the ring shows the reserve that is left: full = plenty of room, empty = at the limit
            double reserve = 1 - m.ThermalRatio;
            // with a part missing (no CPU temperature) the ring is dashed: the reserve is not the whole picture
            bool partial = double.IsNaN(m.CpuTemp);
            using (SKPaint track = Fui.Stroke(TrackStrong, 7 * u))
                c.DrawCircle(rc, ringR, track);
            using (SKPaint arc = Fui.Stroke(HeatColor(m.ThermalRatio), 7 * u))
            {
                if (partial)
                    arc.PathEffect = SKPathEffect.CreateDash(new[] { 9 * u, 5 * u }, 0);
                else
                    arc.StrokeCap = SKStrokeCap.Round;
                DrawArc(c, rc, ringR, -90, (float)(reserve * 360), arc);
            }
            string k = double.IsNaN(m.ThermalHeadroom) ? "n/a" : m.ThermalHeadroom.ToString("0", Inv) + " K";
            Fui.Text(c, k, rc.X, rc.Y + 6 * u, Math.Max(15, 17 * u), Text, "Bold", SKTextAlign.Center, 0, 2 * u);

            float sx = rc.X + ringR + 18 * u;
            // without a CPU temperature the headroom only covers the other parts; say so
            Fui.Text(c, double.IsNaN(m.CpuTemp) ? "CPU TEMP n/a, NOT COUNTED" : "TO THE NEAREST LIMIT", sx, rc.Y - 10 * u, Micro(u), Dim, "Medium");
            Fui.Text(c, "HOTTEST  " + (double.IsNaN(m.HottestTemp) ? "n/a" : Fui.Temp(m.HottestTemp, m.Fahrenheit)), sx, rc.Y + 8 * u, Small(u), Text, "Medium");
            Fui.Text(c, "RING = RESERVE LEFT " + (reserve * 100).ToString("0", Inv) + "%", sx, rc.Y + 26 * u, Micro(u), Dim, "Medium");
            y = rc.Y + ringR + 18 * u;
        }

        // power oscilloscope with a scale
        float scopeH = Math.Min(170 * u, col.Bottom - y - 30 * u);
        if (scopeH > 60)
        {
            string span = Math.Max(1, m.PowerHistory.Length * m.UpdateIntervalMs / 60000).ToString(Inv);
            string minMax = m.PowerHistory.Length > 1 ? "MIN " + m.PowerHistory.Min().ToString("0", Inv) + "  MAX " + m.PowerHistory.Max().ToString("0", Inv) + " W" : est + Fui.Watts(m.TotalPower);
            y = Header(c, "POWER // " + span + " MIN", minMax, col.Left, col.Right, y, u);
            DrawScope(c, new SKRect(col.Left + 44 * u, y, col.Right, y + scopeH), m.PowerHistory, u, m.OtherPowerEstimated);
        }
    }

    private static void DrawScope(SKCanvas c, SKRect r, double[] values, float u, bool estimated)
    {
        using (SKPaint back = Fui.Fill(Panel.WithAlpha(0.8f)))
            c.DrawRect(r, back);
        Fui.Brackets(c, r, 8 * u, Accent.WithAlpha(0.7f), u);

        // the scale follows the data (with some margin), so the curve uses the full height
        double lo = values.Length > 0 ? values.Min() : 0, hi = values.Length > 0 ? values.Max() : 100;
        double margin = Math.Max(5, (hi - lo) * 0.2);
        // at most four scale lines, fewer when the scope is short (at least 30 px between labels)
        int lines = Math.Max(2, Math.Min(4, (int)(r.Height / (30 * u))));
        double step = NiceStep((hi - lo + 2 * margin) / lines);
        double min = Math.Max(0, Math.Floor((lo - margin) / step) * step);
        double max = Math.Max(min + step * 2, Math.Ceiling((hi + margin) / step) * step);
        using (SKPaint grid = Fui.Stroke(Rule, u))
        {
            // labels keep at least 28 px apart; the grid lines in between stay
            int every = 1;
            while (r.Height * step * every / (max - min) < 22 * u && every < 10)
                every++;
            int k = 0;
            for (double v = min; v <= max + step / 2; v += step, k++)
            {
                float gy = r.Bottom - (float)((v - min) / (max - min)) * r.Height;
                c.DrawLine(r.Left, gy, r.Right, gy, grid);
                if (k % every == 0)
                    Fui.Text(c, v.ToString("0", Inv), r.Left - 6 * u, gy + 4 * u, Micro(u), Dim, "Medium", SKTextAlign.Right);
            }
        }

        if (values.Length < 2)
            return;

        using var line = new SKPath();
        using var area = new SKPath();
        float py(double v) => r.Bottom - (float)((v - min) / (max - min)) * r.Height;
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

        // min and max markers (their values are in the header)
        int iMax = Array.IndexOf(values, values.Max()), iMin = Array.IndexOf(values, values.Min());
        foreach (int idx in new[] { iMax, iMin })
        {
            float x = r.Left + r.Width * idx / (values.Length - 1), y = py(values[idx]);
            using (SKPaint ring = Fui.Stroke(Text, 1.4f * u))
                c.DrawCircle(x, y, 3.5f * u, ring);
        }
    }

    private static double NiceStep(double raw)
    {
        if (raw <= 0 || double.IsNaN(raw))
            return 10;
        double mag = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        double norm = raw / mag;
        return (norm < 1.5 ? 1 : norm < 3 ? 2 : norm < 7 ? 5 : 10) * mag;
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
            string text = (active ? "● " : "○ ") + SystemMetrics.StateLabel(state);
            float tw = Fui.Text(c, text, x, y, Small(u), active ? col : Dim, "Bold", spacing: 0.6f * u);
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

    // the fans tile shows the board temperature; say so
    private static string TempPrefix(HardwareTile tile) => tile.Kind == "fans" ? "BOARD " : "";

    private static SKColor HeatColor(HardwareTile tile)
    {
        // fans working hard is not an alarm (they only weigh 0.10 in the stress index); only heat colors them
        if (double.IsNaN(tile.Temp))
            return tile.Kind == "fans" ? Cool : LoadColor(Nz(tile.Load));
        return HeatColor((tile.Temp - 30) / Math.Max(1, tile.TempLimit - 30));
    }

    private static SKColor HeatColor(double ratio) => ratio >= 0.85 ? Alarm : ratio >= 0.6 ? Data : Accent;

    private static string HeatTag(double ratio) => ratio >= 0.85 ? "  ▲ HOT" : ratio >= 0.6 ? "  WARM" : "";

    private static string TempTag(HardwareTile tile) => double.IsNaN(tile.Temp) ? "" : HeatTag((tile.Temp - 30) / Math.Max(1, tile.TempLimit - 30));

    private static SKColor LoadColor(double load) => load >= 85 ? Alarm : load >= 60 ? Data : Accent;

    private static SKColor StateColor(string state) => state == "THERMAL" ? Alarm : state == "LOAD" ? Data : Accent;

    private static string NA(string value) => value == "--" ? "n/a" : value;

    // "NVIDIA GeForce RTX 2070 SUPER" -> "RTX 2070 SUPER", long board names cut at a word boundary
    private static string ShortName(HardwareTile tile) => SystemMetrics.ShortHardwareName(tile.Name);

    private static string Shorten(string text, int max)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= max)
            return text ?? "";
        return text.Substring(0, max - 1) + "…";
    }

    private static double Nz(double v) => double.IsNaN(v) ? 0 : v;
}
