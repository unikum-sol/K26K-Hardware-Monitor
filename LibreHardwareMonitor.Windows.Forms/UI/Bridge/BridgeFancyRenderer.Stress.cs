// K26K Hardware Monitor - the stress panel of the Bridge Fancy dashboard: index, parts and the stress field.
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
using LibreHardwareMonitor.Windows.Forms.Telemetry;
using LibreHardwareMonitor.Windows.Forms.UI.Fancy;
using LibreHardwareMonitor.Windows.Forms.UI.SciFi;
using SkiaSharp;
using static LibreHardwareMonitor.Windows.Forms.UI.Bridge.BridgeDraw;

namespace LibreHardwareMonitor.Windows.Forms.UI.Bridge;

/// <summary>
/// The stress panel: a text column with the index, the state and the four weighted parts, and beside it the
/// "stress field", a perspective floor with rings for the scale and a core that rises with the index. Every part
/// circles the core on a tilted orbit whose radius is the part's value; a labeled marker runs along it (faster with
/// a higher value), and fine streams flow from each part into the core, more and brighter with its share of the index.
/// </summary>
public static partial class BridgeFancyRenderer
{
    // the parts of the index: name, meaning, weight, the orbit's tilt and the direction it opens to
    private static readonly (string Name, string Meaning, double Weight, double Tilt, double Node, double Phase)[] Parts =
    {
        ("LOAD", "MAX OF CPU AND GPU", StressWeights.Load, 58, 20, 0.2),
        ("HEAT", "SHARE OF THE LIMIT", StressWeights.Heat, 32, 110, 3.6),
        ("MEMORY", "RAM IN USE", StressWeights.Memory, 72, 205, 1.4),
        ("FANS", "FAN EFFORT", StressWeights.Fans, 18, 295, 5.0)
    };

    private static readonly SKColor FieldTop = Fui.Hex("#060912");
    private static readonly SKColor FieldBottom = Fui.Hex("#0C0F1C");
    private static readonly SKColor FieldGlow = Fui.Hex("#18284A");

    // room for the scale bracket at the right edge of the field
    private static float ScaleWidth(SKRect field) => Math.Min(74, field.Width * 0.17f);

    private static double PartValue(DashboardModel m, int i) => Nz(i switch
    {
        0 => m.StressLoad,
        1 => m.StressHeat,
        2 => m.StressMemory,
        _ => m.StressFans
    });

    private static SKColor PartColor(int i) => i switch
    {
        0 => Blue,
        1 => Data,
        2 => Peach,
        _ => Lilac
    };

    /// <summary>The part of the panel that holds the field (inside the stress panel, right of the text column).</summary>
    private static SKRect FieldRect(Layout l)
    {
        float u = l.U;
        SKRect p = l.Stress;
        float top = p.Top + HeaderHeight(u);
        float textW = Math.Max(175 * u, p.Width * 0.35f);
        return new SKRect(p.Left + textW + 14 * u, top - 4 * u, p.Right, p.Bottom);
    }

    /// <summary>A simple camera: the floor is the x/z plane, y points up, the view looks down onto it at a low angle.</summary>
    private readonly struct Camera
    {
        private const double Elevation = 24 * Math.PI / 180, Distance = 3.4;
        private static readonly double Ce = Math.Cos(Elevation), Se = Math.Sin(Elevation);
        private readonly SKPoint _center;
        private readonly float _scale;

        public Camera(SKRect field)
        {
            float w = field.Width - ScaleWidth(field);
            _center = new SKPoint(field.Left + w / 2, field.Top + field.Height * 0.60f);
            _scale = Math.Min(w / 2.75f, field.Height / 1.8f);
        }

        public SKPoint P(double x, double y, double z)
        {
            double yr = y * Ce + z * Se, zr = z * Ce - y * Se;
            double f = Distance / (Distance + zr);
            return new SKPoint(_center.X + (float)(x * f * _scale), _center.Y - (float)(yr * f * _scale));
        }

        /// <summary>Positive behind the core, negative in front of it.</summary>
        public static double Depth(double y, double z) => z * Ce - y * Se;
    }

    // a point on the orbit of part i at angle a (radians), around the core at height h
    private static (double X, double Y, double Z) Orbit(int i, double radius, double a, double h)
    {
        double tilt = Parts[i].Tilt * Math.PI / 180, node = Parts[i].Node * Math.PI / 180;
        double x = radius * Math.Cos(a), z0 = radius * Math.Sin(a);
        double y = -z0 * Math.Sin(tilt), z = z0 * Math.Cos(tilt);
        return (x * Math.Cos(node) + z * Math.Sin(node), y + h, -x * Math.Sin(node) + z * Math.Cos(node));
    }

    private static double OrbitRadius(double value) => 0.25 + Math.Max(0, Math.Min(100, value)) / 100 * 0.8;

    private static double RingRadius(double value) => Math.Max(0, Math.Min(100, value)) / 100 * 1.25;

    private static double CoreHeight(double stress) => 0.06 + Math.Max(0, Math.Min(100, stress)) / 100 * 0.42;

    private static void DrawStress(SKCanvas c, Layout l, FancyTweenState tw, double t, FancyOptions o, float ease, bool motion)
    {
        float u = l.U;
        DashboardModel m = tw.Model(GaugeDelay);
        SKRect field = FieldRect(l);
        var cam = new Camera(field);
        bool still = o.ReducedMotion;

        if (!motion)
        {
            Header(c, "SYSTEM STRESS", "INDEX 0 TO 100  //  FOUR WEIGHTED PARTS", l.Stress.Left, l.Stress.Right, l.Stress.Top, u, Accent, "INDEX 0 TO 100");
            DrawStressText(c, l, tw, m);
            DrawFieldStatic(c, field, cam, m, u, ease);
            tw.Region(new SKRect(l.Stress.Left - 2 * u, l.Stress.Top, l.Stress.Right + 2 * u, l.Stress.Bottom + 2 * u), GaugeDelay);
        }

        // markers, streams and the pulse move; with reduced motion the markers stand still in the static layer
        if (motion || still || o.Layer == FancyLayer.All)
            DrawFieldMotion(c, field, cam, m, u, still ? 0 : t, true, !still);
    }

    // ------------------------------------------------------------------ text column

    private static void DrawStressText(SKCanvas c, Layout l, FancyTweenState tw, DashboardModel m)
    {
        float u = l.U;
        SKRect p = l.Stress;
        float left = p.Left, top = p.Top + HeaderHeight(u);
        float right = FieldRect(l).Left - 14 * u;
        SKColor col = StateColor(m.State);

        // the index, large, with its scale and the state
        Label(c, "STRESS INDEX", left, top + Micro(u), Micro(u), Dim, SemiBold, spacing: 1 * u);
        float size = Math.Min(70 * u, Math.Max(44, (right - left) * 0.36f));
        float ny = top + Micro(u) + 10 * u + size * 0.86f;
        float nw = Label(c, m.Stress.ToString("0", Inv), left - 2 * u, ny, size, col, Bold);
        Label(c, "/ 100", left + nw + 6 * u, ny, Small(u), Dim, SemiBold);
        string state = SystemMetrics.StateLabel(m.State);
        float sw = Measure(state, Small(u), Bold, 1.2f * u) + 30 * u;
        var pill = new SKRect(left, ny + 10 * u, left + sw, ny + 10 * u + Small(u) + 12 * u);
        Pill(c, pill, col);
        Label(c, state, pill.MidX, pill.Bottom - 6 * u - Small(u) * 0.12f, Small(u), Bg, Bold, SKTextAlign.Center, 1.2f * u);

        // the four parts: marker (the same as on its orbit), name, value, weight and meaning
        float y = pill.Bottom + 12 * u;
        float rowH = Small(u) + Micro(u) + 10 * u;
        for (int i = 0; i < Parts.Length; i++)
        {
            SKColor color = PartColor(i);
            double value = PartValue(m, i);
            float ly = y + Small(u);
            Marker(c, new SKPoint(left + 7 * u, ly - Small(u) * 0.34f), u, color, 1f);
            Label(c, Parts[i].Name, left + 20 * u, ly, Small(u), color, Bold, spacing: 1 * u);
            Label(c, value.ToString("0", Inv) + " / 100", right, ly, Small(u), Text, SemiBold, SKTextAlign.Right);
            // weight and meaning; in a narrow column the meaning gives way, the text never gets smaller
            string weight = "WEIGHT " + Parts[i].Weight.ToString("0.00", CultureInfo.InvariantCulture);
            string note = weight + "   " + Parts[i].Meaning;
            if (Measure(note, Micro(u), SemiBold, 0.3f * u) > right - left - 20 * u)
                note = weight;
            Label(c, note, left + 20 * u, ly + Micro(u) + 4 * u, Micro(u), Dim, SemiBold, spacing: 0.3f * u);
            y += rowH;
        }

        // the formula, wrapped to the width of the column, so it never runs into the field
        y += 2 * u;
        string formula = "INDEX = SUM OF WEIGHT × PART, AT LEAST " + (StressWeights.Floor * 100).ToString("0", Inv) + "% OF MAX(LOAD, HEAT)";
        var lines = Wrap(formula, Micro(u), right - left, 0.3f * u);
        float lineH = Micro(u) + 4 * u;
        // in a short panel the formula is shortened (the full one is in the README), never drawn over the flow
        if (y + lines.Count * lineH > p.Bottom)
            lines = Wrap("INDEX = SUM OF WEIGHT × PART", Micro(u), right - left, 0.3f * u);
        if (y + lines.Count * lineH > p.Bottom)
            lines.Clear();
        foreach (string line in lines)
        {
            Label(c, line, left, y + Micro(u), Micro(u), Dim, SemiBold, spacing: 0.3f * u);
            y += Micro(u) + 4 * u;
        }
        y += 12 * u;

        // the stress of the last updates as a mirrored waveform, when there is room
        float rest = p.Bottom - y;
        if (rest > 44 * u && m.StressHistory.Length > 1)
        {
            var box = new SKRect(left, y, right, p.Bottom);
            Label(c, "STRESS STREAM", box.Left, box.Top + Micro(u), Micro(u), Tan, SemiBold, spacing: 1 * u);
            var wave = new SKRect(box.Left, box.Top + Micro(u) + 8 * u, box.Right, box.Bottom);
            using (SKPaint tick = Fui.Stroke(Fui.Hex("#4A3E34"), Math.Max(1, u)))
            {
                for (float x = wave.Left, k = 0; x <= wave.Right; x += 8 * u, k++)
                    c.DrawLine(x, wave.Top, x, wave.Top + (k % 5 == 0 ? 6 : 3) * u, tick);
            }
            var bars = new SKRect(wave.Left, wave.Top + 8 * u, wave.Right, wave.Bottom);
            float mid = bars.MidY, half = bars.Height / 2;
            (double X, double V)[] points = FancyTween.Scroll(tw.PrevHistory(x => x.StressHistory), m.StressHistory, tw.Scroll);
            float barW = Math.Max(2 * u, bars.Width / Math.Max(30, points.Length) * 0.55f);
            int save = c.Save();
            c.ClipRect(bars);
            foreach (var (fx, v) in points)
            {
                float x = bars.Left + bars.Width * (float)fx;
                float h = half * (float)Math.Max(0.06, Math.Min(1, v / 100));
                SKColor bc = v >= 85 ? Alarm : v >= 60 ? Warm : Blue;
                using SKPaint bar = Fui.Fill(bc.WithAlpha(0.85f));
                c.DrawRect(new SKRect(x - barW / 2, mid - h, x + barW / 2, mid + h), bar);
            }
            c.RestoreToCount(save);
            tw.Scrolling(new SKRect(bars.Left - 2 * u, bars.Top - 2 * u, bars.Right + 2 * u, bars.Bottom + 2 * u));
        }
    }

    private static System.Collections.Generic.List<string> Wrap(string text, float size, float width, float spacing)
    {
        var lines = new System.Collections.Generic.List<string>();
        string line = "";
        foreach (string word in text.Split(' '))
        {
            string next = line.Length == 0 ? word : line + " " + word;
            if (line.Length > 0 && Measure(next, size, SemiBold, spacing) > width)
            {
                lines.Add(line);
                line = word;
            }
            else
                line = next;
        }
        if (line.Length > 0)
            lines.Add(line);
        return lines;
    }

    // ------------------------------------------------------------------ the field

    private static void DrawFieldStatic(SKCanvas c, SKRect field, Camera cam, DashboardModel m, float u, float ease)
    {
        float radius = 16 * u;
        using (var back = new SKPaint { IsAntialias = true, Shader = SKShader.CreateLinearGradient(new SKPoint(0, field.Top), new SKPoint(0, field.Bottom), new[] { FieldTop, FieldBottom }, null, SKShaderTileMode.Clamp) })
            c.DrawRoundRect(field, radius, radius, back);

        int save = c.Save();
        using (var clip = new SKPath())
        {
            clip.AddRoundRect(field, radius, radius);
            c.ClipPath(clip, SKClipOperation.Intersect, true);
        }

        // a faint glow under the core
        SKPoint floor = cam.P(0, 0, 0);
        using (var glow = new SKPaint { Shader = SKShader.CreateRadialGradient(floor, field.Width * 0.45f, new[] { FieldGlow.WithAlpha(0.55f), SKColors.Transparent }, null, SKShaderTileMode.Clamp) })
            c.DrawRect(field, glow);

        // the floor grid: lines fade into the distance
        const double extent = 1.7, step = 0.2125;
        for (double v = -extent; v <= extent + 1e-6; v += step)
        {
            GridLine(c, cam.P(v, 0, -extent), cam.P(v, 0, extent), u);
            GridLine(c, cam.P(-extent, 0, v), cam.P(extent, 0, v), u, v);
        }

        // rings of the scale (25, 50, 75, 100) and a bright ring at the current index
        foreach (int level in new[] { 25, 50, 75, 100 })
        {
            using SKPath ring = FloorCircle(cam, RingRadius(level));
            // (the numbers of the scale are on the bracket at the right edge)
            using SKPaint p = Fui.Stroke(Accent.WithAlpha(level == 100 ? 0.45f : 0.28f), Math.Max(1, 1.2f * u));
            c.DrawPath(ring, p);
        }
        SKColor state = StateColor(m.State);
        using (SKPath now = FloorCircle(cam, RingRadius(m.Stress * ease)))
        using (SKPaint p = Fui.Stroke(state, 2.6f * u))
            c.DrawPath(now, p);

        // the axis and the core, which rises with the index
        double h = CoreHeight(m.Stress * ease);
        using (SKPaint axis = Fui.Stroke(Blue.WithAlpha(0.7f), Math.Max(1, 1.2f * u)))
        {
            axis.PathEffect = SKPathEffect.CreateDash(new[] { 5 * u, 5 * u }, 0);
            c.DrawLine(cam.P(0, -0.25, 0), cam.P(0, 1.35, 0), axis);
        }
        SKPoint core = cam.P(0, h, 0);
        using (SKPaint stem = Fui.Stroke(state, 2 * u))
            c.DrawLine(floor, core, stem);

        // the orbits of the four parts: the half behind the core is dimmer than the half in front of it
        for (int i = 0; i < Parts.Length; i++)
            DrawOrbit(c, cam, i, OrbitRadius(PartValue(m, i)) * (0.6 + 0.4 * ease), h, u);

        // the streams as faint threads; their bright pulses are drawn in the motion layer
        for (int i = 0; i < Parts.Length; i++)
        {
            int n = StreamCount(m, i);
            float share = (float)Contribution(m, i) / 40f;
            using SKPaint thread = Fui.Stroke(PartColor(i).WithAlpha(0.16f + 0.28f * share), Math.Max(1, 0.9f * u));
            for (int k = 0; k < n; k++)
            {
                using SKPath path = Stream(cam, i, k, n, h);
                c.DrawPath(path, thread);
            }
        }

        // the core itself: a ring in the state color, a bright center and a small star
        using (SKPaint ring = Fui.Stroke(state, 2 * u))
            c.DrawCircle(core, 10 * u, ring);
        using (SKPaint dot = Fui.Fill(Text))
            c.DrawCircle(core, 4 * u, dot);
        using (SKPaint star = Fui.Stroke(Text.WithAlpha(0.85f), Math.Max(1, u)))
        {
            c.DrawLine(core.X - 18 * u, core.Y, core.X - 12 * u, core.Y, star);
            c.DrawLine(core.X + 12 * u, core.Y, core.X + 18 * u, core.Y, star);
            c.DrawLine(core.X, core.Y - 18 * u, core.X, core.Y - 12 * u, star);
            c.DrawLine(core.X, core.Y + 12 * u, core.X, core.Y + 18 * u, star);
        }
        c.RestoreToCount(save);

        DrawScale(c, field, m, u, ease);
    }

    // A bracket with a scale from 000 to 100 at the right edge of the field: the ranges of the states as colored
    // segments in the bar and an oval marker at the current index.
    private static void DrawScale(SKCanvas c, SKRect field, DashboardModel m, float u, float ease)
    {
        float sw = ScaleWidth(field);
        float barW = 7 * u, hook = 16 * u;
        float barX = field.Right - 14 * u - barW;
        float top = field.Top + 22 * u, bottom = field.Bottom - 22 * u;
        float y0 = top + hook * 0.9f, y1 = bottom - hook * 0.9f;
        float Y(double v) => y1 - (float)(Math.Max(0, Math.Min(100, v)) / 100) * (y1 - y0);

        // the bracket: one thick stroke with rounded corners, open to the left
        using (var bracket = new SKPath())
        {
            float cx = barX + barW / 2, r = hook;
            bracket.MoveTo(barX - hook, top + barW / 2);
            bracket.LineTo(cx - r, top + barW / 2);
            bracket.ArcTo(new SKRect(cx - 2 * r, top + barW / 2, cx, top + barW / 2 + 2 * r), 270, 90, false);
            bracket.LineTo(cx, bottom - barW / 2 - r);
            bracket.ArcTo(new SKRect(cx - 2 * r, bottom - barW / 2 - 2 * r, cx, bottom - barW / 2), 0, 90, false);
            bracket.LineTo(barX - hook, bottom - barW / 2);
            using SKPaint stroke = Fui.Stroke(Peach, barW);
            c.DrawPath(bracket, stroke);
        }

        // the ranges of the states inside the bar (nominal, high load, thermal)
        foreach (var (from, to, color) in new[] { (0.0, 60.0, Blue), (60.0, 85.0, Warm), (85.0, 100.0, Alarm) })
        {
            using SKPaint seg = Fui.Fill(color);
            c.DrawRect(new SKRect(barX + 1.5f * u, Y(to) + 1 * u, barX + barW - 1.5f * u, Y(from) - 1 * u), seg);
        }

        // ticks and numbers to the left of the bar
        using (SKPaint minor = Fui.Stroke(Accent.WithAlpha(0.75f), Math.Max(1, u)))
        using (SKPaint major = Fui.Stroke(Accent, Math.Max(1.5f, 1.6f * u)))
        {
            for (int v = 0; v <= 100; v += 5)
            {
                bool big = v % 20 == 0;
                float y = Y(v);
                c.DrawLine(barX - (big ? 10 : 5) * u, y, barX, y, big ? major : minor);
                if (big)
                    Label(c, v.ToString("000", CultureInfo.InvariantCulture), barX - 13 * u, y + Micro(u) * 0.36f, Micro(u) * 0.9f, v % 40 == 0 ? Blue : Accent, SemiBold, SKTextAlign.Right);
            }
        }

        // the oval marker at the current index, left of the numbers
        float my = Y(m.Stress * ease);
        float ox = barX - 13 * u - Measure("000", Micro(u) * 0.9f, SemiBold) - 8 * u;
        var oval = new SKRect(ox - 22 * u, my - 7 * u, ox, my + 7 * u);
        using (SKPaint fill = Fui.Fill(Fui.Hex("#F4F0D8")))
            c.DrawOval(oval, fill);
        using (SKPaint line = Fui.Stroke(StateColor(m.State).WithAlpha(0.8f), Math.Max(1, u)))
            c.DrawLine(ox, my, barX - 1 * u, my, line);
    }

    // markers on the orbits, pulses along the streams and a wave that runs out to the current index
    private static void DrawFieldMotion(SKCanvas c, SKRect field, Camera cam, DashboardModel m, float u, double t, bool markers, bool flowing)
    {
        float radius = 16 * u;
        int save = c.Save();
        using (var clip = new SKPath())
        {
            clip.AddRoundRect(field, radius, radius);
            c.ClipPath(clip, SKClipOperation.Intersect, true);
        }

        double h = CoreHeight(m.Stress);
        SKColor state = StateColor(m.State);
        c.ClipRect(new SKRect(field.Left, field.Top, field.Right - ScaleWidth(field), field.Bottom));

        if (flowing)
        {
            // a wave on the floor, from the core out to the index ring, about every 2.5 seconds
            double phase = t / 2.5 % 1;
            using (SKPath wave = FloorCircle(cam, RingRadius(m.Stress) * phase))
            using (SKPaint p = Fui.Stroke(state.WithAlpha((float)(0.55 * (1 - phase))), 1.6f * u))
                c.DrawPath(wave, p);

            // bright pulses flow along the streams into the core; faster with a larger share
            for (int i = 0; i < Parts.Length; i++)
            {
                int n = StreamCount(m, i);
                double share = Contribution(m, i) / 40;
                using SKPaint pulse = Fui.Stroke(PartColor(i).WithAlpha(0.55f + 0.35f * (float)share), Math.Max(1.2f, 1.4f * u));
                pulse.StrokeCap = SKStrokeCap.Round;
                float speed = (float)(40 + 90 * share) * u;
                for (int k = 0; k < n; k++)
                {
                    using SKPath path = Stream(cam, i, k, n, h);
                    pulse.PathEffect = SKPathEffect.CreateDash(new[] { 10 * u, 34 * u }, -(float)(t * speed + k * 11 * u) % (44 * u));
                    c.DrawPath(path, pulse);
                }
            }
        }

        if (markers)
        {
            for (int i = 0; i < Parts.Length; i++)
            {
                double value = PartValue(m, i);
                double r = OrbitRadius(value);
                double a = Parts[i].Phase + t * (0.10 + value / 100 * 0.40);
                var (x, y, z) = Orbit(i, r, a, h);
                SKPoint at = cam.P(x, y, z);
                bool behind = Camera.Depth(y, z) > 0;
                Marker(c, at, u, PartColor(i), behind ? 0.55f : 1f);
                // the value beside the marker, on the side away from the core (the name is in the list on the left)
                SKPoint core = cam.P(0, h, 0);
                bool outRight = at.X >= core.X;
                if (outRight && at.X > field.Right - 40 * u)
                    outRight = false;
                if (!outRight && at.X < field.Left + 40 * u)
                    outRight = true;
                Label(c, value.ToString("0", Inv), at.X + (outRight ? 12 : -12) * u, at.Y + Micro(u) * 0.36f, Small(u), PartColor(i).WithAlpha(behind ? 0.6f : 1f), Bold,
                      outRight ? SKTextAlign.Left : SKTextAlign.Right);
            }
        }
        c.RestoreToCount(save);
    }

    // ------------------------------------------------------------------ geometry

    private static void GridLine(SKCanvas c, SKPoint a, SKPoint b, float u, double depth = double.NaN)
    {
        using var p = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = Math.Max(1, 0.8f * u) };
        if (double.IsNaN(depth))
        {
            // lines into the distance: bright in front, fading towards the back
            p.Shader = SKShader.CreateLinearGradient(a, b, new[] { Tan.WithAlpha(0.32f), Tan.WithAlpha(0.04f) }, null, SKShaderTileMode.Clamp);
        }
        else
        {
            float near = (float)(1 - (depth + 1.7) / 3.4);
            p.Color = Tan.WithAlpha(0.04f + 0.26f * near);
        }
        c.DrawLine(a, b, p);
    }

    private static SKPath FloorCircle(Camera cam, double r)
    {
        var path = new SKPath();
        const int steps = 72;
        for (int s = 0; s <= steps; s++)
        {
            double a = s * 2 * Math.PI / steps;
            SKPoint p = cam.P(r * Math.Cos(a), 0, r * Math.Sin(a));
            if (s == 0)
                path.MoveTo(p);
            else
                path.LineTo(p);
        }
        return path;
    }

    private static void DrawOrbit(SKCanvas c, Camera cam, int i, double r, double h, float u)
    {
        SKColor color = Fui.Mix(PartColor(i), SKColors.White, 0.3f);
        using var front = new SKPath();
        using var back = new SKPath();
        const int steps = 96;
        SKPoint last = default;
        bool lastBehind = false;
        for (int s = 0; s <= steps; s++)
        {
            double a = s * 2 * Math.PI / steps;
            var (x, y, z) = Orbit(i, r, a, h);
            SKPoint p = cam.P(x, y, z);
            bool behind = Camera.Depth(y, z) > 0;
            if (s > 0)
            {
                SKPath target = behind && lastBehind ? back : front;
                target.MoveTo(last);
                target.LineTo(p);
            }
            last = p;
            lastBehind = behind;
        }
        using (SKPaint pb = Fui.Stroke(color.WithAlpha(0.32f), Math.Max(1, 1.1f * u)))
            c.DrawPath(back, pb);
        using (SKPaint pf = Fui.Stroke(color.WithAlpha(0.92f), Math.Max(1.2f, 1.7f * u)))
            c.DrawPath(front, pf);
    }

    // the share of the index that comes from part i (weight × part, 0 to 40)
    private static double Contribution(DashboardModel m, int i) => Parts[i].Weight * PartValue(m, i);

    private static int StreamCount(DashboardModel m, int i) => 2 + (int)Math.Round(Contribution(m, i) * 0.32);

    // one stream of part i: from the edge of the floor, in the direction of the part's orbit, curving into the core
    private static SKPath Stream(Camera cam, int i, int k, int n, double h)
    {
        double node = Parts[i].Node * Math.PI / 180;
        // the streams of one part start side by side on the edge and sweep in a common arc into the core
        double spread = (n > 1 ? k / (double)(n - 1) - 0.5 : 0) * 0.9;
        double a = node + spread;
        double lift = 0.04 * Math.Sin(k * 1.7 + i);
        var start = cam.P(1.7 * Math.Cos(a), lift, 1.7 * Math.Sin(a));
        var c1 = cam.P(1.2 * Math.Cos(a + 0.7), 0.1 + lift, 1.2 * Math.Sin(a + 0.7));
        var c2 = cam.P(0.5 * Math.Cos(node + 1.4), h * 0.7, 0.5 * Math.Sin(node + 1.4));
        var end = cam.P(0, h, 0);
        var path = new SKPath();
        path.MoveTo(start);
        path.CubicTo(c1, c2, end);
        return path;
    }

    // a marker as in the text column and on the orbits: a light ring with a dot in the part's color
    private static void Marker(SKCanvas c, SKPoint at, float u, SKColor color, float alpha)
    {
        using (SKPaint back = Fui.Fill(Bg.WithAlpha(0.7f * alpha)))
            c.DrawCircle(at, 8 * u, back);
        using (SKPaint ring = Fui.Stroke(Text.WithAlpha(alpha), 2 * u))
            c.DrawCircle(at, 7 * u, ring);
        using (SKPaint dot = Fui.Fill(color.WithAlpha(alpha)))
            c.DrawCircle(at, 3.2f * u, dot);
    }
}
