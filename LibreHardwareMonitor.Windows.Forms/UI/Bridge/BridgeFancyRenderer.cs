// K26K Hardware Monitor - the Bridge Fancy dashboard: the Fancy functions in the Bridge design.
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
using LibreHardwareMonitor.Windows.Forms.UI.Fancy;
using LibreHardwareMonitor.Windows.Forms.UI.SciFi;
using SkiaSharp;
using static LibreHardwareMonitor.Windows.Forms.UI.Bridge.BridgeDraw;

namespace LibreHardwareMonitor.Windows.Forms.UI.Bridge;

/// <summary>
/// Draws the Bridge Fancy dashboard as a pure function of the model, the time and the size, like
/// <see cref="FancyRenderer"/>: the same data and functions, in flat color blocks, elbows and pills.
/// The upper frame holds the title, the session and the window buttons; the lower frame holds the
/// hardware blocks in its arm and three columns: the stress gauge with the system flow, the core and
/// thermal view, and the telemetry with the power scope.
/// </summary>
public static partial class BridgeFancyRenderer
{
    public static readonly IDashboardRenderer Instance = new Renderer();

    private sealed class Renderer : IDashboardRenderer
    {
        public SKRect MotionArea(int width, int height)
        {
            // the stress field and the flow tracks move; both sit in the first column
            Layout l = Layout.Of(width, height);
            return new SKRect(Math.Min(l.Stress.Left, l.Flow.Left), l.Stress.Top, Math.Max(l.Stress.Right, l.Flow.Right), l.Flow.Bottom);
        }

        public SKRect Render(SKCanvas c, int width, int height, float dpiScale, DashboardModel m, double t, FancyOptions o) =>
            BridgeFancyRenderer.Render(c, width, height, m, t, o);
    }

    private const double BootSeconds = 2.6;

    // when each part starts to glide to the new values (seconds after the update, at a 1 s interval):
    // the stress gauge first, then the flow, the hardware blocks and the columns from left to right
    private const double GaugeDelay = 0.0, FlowDelay = 0.06, FlowStep = 0.05;
    private const double BlockDelay = 0.10, BlockStep = 0.05;
    private const double CoreCellDelay = 0.20, CoreCellStep = 0.012;
    private const double ThermalDelay = 0.30, ThermalStep = 0.05;
    private const double TelemetryDelay = 0.12, EnergyDelay = 0.20, CompositionDelay = 0.08, HeadroomDelay = 0.36;

    private static readonly CultureInfo Inv = CultureInfo.CurrentCulture;
    private static readonly SKColor Off = Fui.Hex("#3A332D");
    private static readonly SKColor OffText = Fui.Hex("#D2C6BA");
    private static readonly SKColor ReadoutBack = Fui.Hex("#15121A");
    private static readonly SKColor HotFill = Fui.Hex("#FF7A6B");

    // Type scale (Antonio is condensed, so it runs a bit larger than the monospaced Fancy text)
    private static float Micro(float u) => Math.Max(14f, 13.5f * u);
    private static float Small(float u) => Math.Max(15f, 14.5f * u);
    private static float Body(float u) => Math.Max(16f, 16f * u);
    private static float Big(float u) => Math.Max(22f, 24f * u);

    /// <summary>The fixed geometry of one size; it does not depend on the data, so the motion area is known in advance.</summary>
    private readonly struct Layout
    {
        public readonly float U, Margin, Arm, Gap;
        public readonly SKRect Upper, Lower, Col1, Col2, Col3, Flow, Gauge, Stress;
        public readonly float UpperBar, LowerTopBar, LowerBottomBar, TopElbowH, BottomElbowH;

        private Layout(int w, int h)
        {
            float u = Math.Max(0.8f, Math.Min(2.6f, Math.Min(w / 1600f, h / 900f)));
            U = u;
            Margin = 12 * u;
            Arm = 196 * u;
            Gap = 6 * u;
            UpperBar = 18 * u;
            LowerTopBar = 18 * u;
            LowerBottomBar = 30 * u;
            TopElbowH = 86 * u;
            BottomElbowH = 74 * u;

            Upper = new SKRect(Margin, Margin, w - Margin, Margin + 112 * u);
            Lower = new SKRect(Margin, Upper.Bottom + 8 * u, w - Margin, h - Margin);

            float left = Lower.Left + Arm + 30 * u, right = Lower.Right - 4 * u;
            float top = Lower.Top + LowerTopBar + 22 * u, bottom = Lower.Bottom - LowerBottomBar - 20 * u;
            float width = right - left, gap = 34 * u;
            float w1 = (width - 2 * gap) * 0.40f, w2 = (width - 2 * gap) * 0.28f;
            Col1 = new SKRect(left, top, left + w1, bottom);
            Col2 = new SKRect(Col1.Right + gap, top, Col1.Right + gap + w2, bottom);
            Col3 = new SKRect(Col2.Right + gap, top, right, bottom);

            float gaugeH = Math.Min(Col1.Width * 0.44f, Col1.Height * 0.33f);
            Gauge = new SKRect(Col1.Left, Col1.Top, Col1.Right, Col1.Top + gaugeH);
            // the composition (two rows of two parts, each with its meaning) and the formula sit below the gauge;
            // the flow tracks fill the rest of the column
            float micro = Math.Max(14f, 13.5f * u), small = Math.Max(15f, 14.5f * u);
            float flowTop = Gauge.Bottom + micro + 14 * u + 2 * CompositionRow(u, small, micro) + micro + 22 * u;
            Flow = new SKRect(Col1.Left - 4 * u, flowTop, Col1.Right + 4 * u, Col1.Bottom + 4 * u);
            // the stress panel (text column and stress field) fills the column above the flow
            // (at least 380 units high, at most 62 % of the column; the flow takes the rest)
            float stressBottom = Math.Min(Col1.Top + Col1.Height * 0.62f, Math.Max(flowTop - 16 * u, Col1.Top + 380 * u));
            Stress = new SKRect(Col1.Left, Col1.Top, Col1.Right, stressBottom);
            if (stressBottom + 16 * u > flowTop)
                Flow = new SKRect(Flow.Left, stressBottom + 16 * u, Flow.Right, Flow.Bottom);
        }

        public static Layout Of(int w, int h) => new(w, h);

        public static float CompositionRow(float u, float small, float micro) => small + micro + 20 * u;
    }

    /// <summary>Renders one frame. Returns the hit area of the exit button.</summary>
    public static SKRect Render(SKCanvas c, int width, int height, DashboardModel m, double t, FancyOptions o)
    {
        Layout l = Layout.Of(width, height);
        float u = l.U;
        double boot = o.ReducedMotion ? 1 : Math.Min(1, o.SinceOpen / BootSeconds);
        float ease = (float)(1 - Math.Pow(1 - boot, 3));
        var tw = new FancyTweenState(o, m);

        if (o.Layer == FancyLayer.Motion)
        {
            if (!o.ReducedMotion)
            {
                DrawStress(c, l, tw, t, o, 1, true);
                DrawFlow(c, l, tw, t, o, true);
            }
            return SKRect.Empty;
        }

        o.Regions.Clear();
        c.Clear(Bg);

        // the frame lights up block by block during the boot sequence
        DrawUpperFrame(c, l, width, m, o, boot);
        SKRect exit = DrawLowerFrame(c, l, m, o, boot);

        int layer = c.Save();
        if (ease < 1)
        {
            using SKPaint fade = new() { Color = SKColors.White.WithAlpha(ease) };
            c.SaveLayer(fade);
        }
        DrawBlocks(c, l, tw);
        DrawStress(c, l, tw, t, o, ease, false);
        DrawFlow(c, l, tw, t, o, false);
        DrawCoreColumn(c, l, tw);
        DrawTelemetryColumn(c, l, tw);
        c.RestoreToCount(layer);

        if (boot < 1)
            DrawBoot(c, width, height, u, boot);
        return exit;
    }

    // ------------------------------------------------------------------ frame

    // frame parts appear one after another while booting (index = order)
    private static SKColor Lit(SKColor color, int index, double boot) =>
        boot >= 1 ? color : color.WithAlpha((float)Math.Max(0, Math.Min(1, boot * 2.2 - index * 0.12)));

    private static void DrawUpperFrame(SKCanvas c, Layout l, int width, DashboardModel m, FancyOptions o, double boot)
    {
        float u = l.U;
        SKRect r = l.Upper;

        // elbow at the bottom left: the arm holds the K26K block, the bar runs right along the bottom
        float elbowRight = r.Left + l.Arm + 240 * u;
        FillElbow(c, new SKRect(r.Left, r.Top, elbowRight, r.Bottom), l.Arm, l.UpperBar, Corner.BottomLeft, Lit(Lilac, 0, boot), 46 * u, 18 * u);
        Label(c, "K26K", r.Left + l.Arm - 14 * u, r.Top + 42 * u, 34 * u, Bg.WithAlpha(Lit(Lilac, 0, boot).Alpha / 255f), Bold, SKTextAlign.Right, 1.5f * u);
        Label(c, Code(MachineName(m)), r.Left + l.Arm - 14 * u, r.Top + 64 * u, Micro(u), Bg.WithAlpha(Lit(Lilac, 0, boot).Alpha / 255f), Regular, SKTextAlign.Right);

        // the bar continues in segments and ends in a rounded cap; the long segment is a live strip:
        // the stress of the last 60 updates, one cell each, in the colors of the state scale
        float barTop = r.Bottom - l.UpperBar, x = elbowRight + l.Gap;
        float[] shares = { 0.18f, 0.44f, 0.22f };
        SKColor[] colors = { Peach, Tan, Blue };
        float room = r.Right - x - 40 * u - l.Gap * shares.Length;
        for (int i = 0; i < shares.Length; i++)
        {
            float w = room * shares[i] / shares.Sum();
            var seg = new SKRect(x, barTop, x + w, r.Bottom);
            if (i == 1 && boot >= 1)
                StressStrip(c, seg, m, u);
            else
                Block(c, seg, Lit(colors[i], i + 1, boot));
            x += w + l.Gap;
        }
        Block(c, new SKRect(x, barTop, r.Right, r.Bottom), Lit(Accent, 4, boot), false, true);

        // window buttons at the top right, the rest of the upper frame moves the window
        float right = r.Right;
        o.TopStripHeight = barTop;
        if (o.CaptionButtons)
        {
            float bw = Math.Max(CaptionChrome.ButtonWidth, 46 * u), bh = Math.Max(CaptionChrome.ButtonHeight, 32 * u);
            var kinds = o.CaptionMaximize
                ? new[] { WindowButton.Close, WindowButton.Maximize, WindowButton.Minimize }
                : new[] { WindowButton.Close, WindowButton.Minimize };
            var rects = new (WindowButton, SKRect)[kinds.Length];
            for (int i = 0; i < kinds.Length; i++)
            {
                var b = new SKRect(right - bw, r.Top - 4 * u, right, r.Top - 4 * u + bh);
                bool hot = o.CaptionHover == kinds[i];
                CaptionChrome.Draw(c, b, kinds[i], o.Maximized, hot, hot && o.CaptionPressed, o.WindowActive, Caption, Math.Max(1, u));
                rects[i] = (kinds[i], b);
                right -= bw;
            }
            o.CaptionRects = rects;
            right -= 14 * u;
        }
        else
        {
            o.CaptionRects = Array.Empty<(WindowButton, SKRect)>();
        }

        // title and the session line, right next to the arm
        float tx = r.Left + l.Arm + 30 * u;
        Label(c, "SYSTEM CORE", right, r.Top + 30 * u, 32 * u, Accent, Bold, SKTextAlign.Right, 1.8f * u);
        float y = r.Top + 26 * u;
        x = tx;
        foreach (var (label, value) in new[] { ("NODE", (string.IsNullOrEmpty(m.Machine) ? "LOCAL" : m.Machine).ToUpperInvariant()), ("SESSION", Fui.Duration(m.SessionSeconds)), ("SENSORS", m.SensorCount.ToString(Inv)) })
        {
            if (x > right - 330 * u)
                break;
            x += Label(c, label, x, y, Small(u), Lilac, SemiBold, spacing: 1 * u) + 8 * u;
            x += Label(c, value, x, y, Body(u), Text, SemiBold, spacing: 0.4f * u) + 26 * u;
        }

        // date and clock on the second line
        float y2 = barTop - 16 * u;
        float cw = Label(c, m.Clock, tx, y2, Big(u), Data, SemiBold, spacing: 1 * u);
        Label(c, m.Date, tx + cw + 12 * u, y2, Small(u), Dim, Regular, spacing: 0.6f * u);
    }

    private static void StressStrip(SKCanvas c, SKRect r, DashboardModel m, float u)
    {
        const int cells = 60;
        float gap = 2 * u, cw = (r.Width - gap * (cells - 1)) / cells;
        double[] history = m.StressHistory ?? Array.Empty<double>();
        for (int i = 0; i < cells; i++)
        {
            int k = history.Length - cells + i;
            SKColor color = Off;
            if (k >= 0)
            {
                // the colors of the state scale: nominal, high load, thermal limit
                double v = history[k];
                color = (v >= 85 ? Alarm : v >= 60 ? Warm : Blue).WithAlpha(0.88f);
            }
            float x = r.Left + i * (cw + gap);
            // the cell height follows the value above a floor, so the rail keeps its mass at idle
            float h = k >= 0 ? r.Height * (float)(0.35 + 0.65 * Math.Max(0, Math.Min(1, history[k] / 100))) : r.Height * 0.35f;
            using SKPaint p = Fui.Fill(color);
            c.DrawRect(new SKRect(x, r.Bottom - h, x + cw, r.Bottom), p);
        }
        Label(c, "STRESS  LAST " + cells.ToString(Inv) + " UPDATES  //  0 TO 100", r.Left, r.Top - 8 * u, Micro(u), Tan, SemiBold, spacing: 0.8f * u);
        Label(c, "NOW " + m.Stress.ToString("0", Inv), r.Right, r.Top - 8 * u, Micro(u), StateColor(m.State), Bold, SKTextAlign.Right, 0.8f * u);
    }

    private static SKRect DrawLowerFrame(SKCanvas c, Layout l, DashboardModel m, FancyOptions o, double boot)
    {
        float u = l.U;
        SKRect r = l.Lower;

        // top elbow: the arm runs down the left side, the bar along the top in segments
        float topElbowRight = r.Left + l.Arm + 120 * u;
        FillElbow(c, new SKRect(r.Left, r.Top, topElbowRight, r.Top + l.TopElbowH), l.Arm, l.LowerTopBar, Corner.TopLeft, Lit(Accent, 1, boot), 46 * u, 18 * u);
        float x = topElbowRight + l.Gap;
        SKColor[] topColors = { Lilac, Peach, Tan };
        float[] topShares = { 0.5f, 0.3f, 0.2f };
        float room = r.Right - x - 40 * u - l.Gap * topShares.Length;
        for (int i = 0; i < topShares.Length; i++)
        {
            float w = room * topShares[i];
            Block(c, new SKRect(x, r.Top, x + w, r.Top + l.LowerTopBar), Lit(topColors[i], i + 2, boot));
            x += w + l.Gap;
        }
        Block(c, new SKRect(x, r.Top, r.Right, r.Top + l.LowerTopBar), Lit(Lilac, 5, boot), false, true);
        if (boot >= 1)
        {
            // the arm below the elbow carries the heading of the device column
            float ax = r.Left + l.Arm - 12 * u;
            Label(c, "HARDWARE", ax, r.Top + l.TopElbowH - 30 * u, Body(u), Bg, Bold, SKTextAlign.Right, 1.2f * u);
            Label(c, m.Tiles.Count.ToString("00", Inv) + " DEVICES", ax, r.Top + l.TopElbowH - 10 * u, Micro(u), Bg, SemiBold, SKTextAlign.Right, 0.6f * u);
        }

        // bottom elbow and bar with the state, the interval and the exit button
        float bottomElbowRight = r.Left + l.Arm + 60 * u;
        float barTop = r.Bottom - l.LowerBottomBar;
        FillElbow(c, new SKRect(r.Left, r.Bottom - l.BottomElbowH, bottomElbowRight, r.Bottom), l.Arm, l.LowerBottomBar, Corner.BottomLeft, Lit(Tan, 2, boot), 40 * u, 16 * u);
        if (boot >= 1)
        {
            // what the two meters of every device mean
            float lx = r.Left + 14 * u, ly = r.Bottom - l.BottomElbowH + Micro(u) + 12 * u;
            Pill(c, new SKRect(lx, ly - 9 * u, lx + 18 * u, ly - 3 * u), Blue);
            Label(c, "LOAD", lx + 24 * u, ly, Micro(u), Bg, SemiBold, spacing: 0.6f * u);
            float hx = lx + 24 * u + Measure("LOAD", Micro(u), SemiBold, 0.6f * u) + 18 * u;
            Pill(c, new SKRect(hx, ly - 9 * u, hx + 18 * u, ly - 3 * u), Data);
            Label(c, "HEAT", hx + 24 * u, ly, Micro(u), Bg, SemiBold, spacing: 0.6f * u);
        }

        float ty = barTop + l.LowerBottomBar / 2 + Small(u) * 0.36f;
        x = bottomElbowRight + l.Gap;
        // the state as three blocks: the active one is lit in its color, the others stay dark
        foreach (string state in new[] { "NOMINAL", "LOAD", "THERMAL" })
        {
            bool active = m.State == state;
            string text = SystemMetrics.StateLabel(state);
            float w = Measure(text, Small(u), SemiBold, 1 * u) + 28 * u;
            Block(c, new SKRect(x, barTop, x + w, r.Bottom), Lit(active ? StateColor(state) : Off, 3, boot));
            Label(c, text, x + w - 12 * u, ty, Small(u), active ? Bg : OffText, SemiBold, SKTextAlign.Right, 1 * u);
            x += w + l.Gap;
        }

        string interval = "INTERVAL  " + (m.UpdateIntervalMs / 1000.0).ToString("0.0#", Inv) + " S";
        float iw = Measure(interval, Small(u), SemiBold, 1 * u) + 28 * u;
        Block(c, new SKRect(x, barTop, x + iw, r.Bottom), Lit(Peach, 4, boot));
        Label(c, interval, x + iw - 12 * u, ty, Small(u), Bg, SemiBold, SKTextAlign.Right, 1 * u);
        x += iw + l.Gap;
        if (o.ReducedMotion)
        {
            const string reduced = "REDUCED MOTION";
            float rw = Measure(reduced, Small(u), SemiBold, 1 * u) + 28 * u;
            Block(c, new SKRect(x, barTop, x + rw, r.Bottom), Lilac);
            Label(c, reduced, x + rw - 12 * u, ty, Small(u), Bg, SemiBold, SKTextAlign.Right, 1 * u);
            x += rw + l.Gap;
        }

        const string exitText = "EXIT FANCY   ESC";
        float ew = Measure(exitText, Small(u), Bold, 1.2f * u) + 44 * u;
        var exit = new SKRect(r.Right - ew, barTop, r.Right, r.Bottom);
        if (exit.Left - x > 12 * u)
            Block(c, new SKRect(x, barTop, exit.Left - l.Gap, r.Bottom), Lit(Fui.Hex("#5A4C40"), 5, boot));
        Block(c, exit, Lit(o.ExitHover ? Fui.Mix(Alarm, SKColors.White, 0.25f) : Alarm, 6, boot), false, true);
        Label(c, exitText, exit.Right - 22 * u, ty, Small(u), Bg, Bold, SKTextAlign.Right, 1.2f * u);
        return exit;
    }

    // ------------------------------------------------------------------ arm: hardware blocks

    private static SKColor KindColor(string kind) => kind switch
    {
        "cpu" => Peach,
        "gpu" => Lilac,
        "ram" => Blue,
        "board" or "fans" => Tan,
        "disk" => Peach,
        "net" => Blue,
        "power" => Data,
        _ => Tan
    };

    private static double Heat(HardwareTile tile) => double.IsNaN(tile.Temp) ? double.NaN : (tile.Temp - 30) / Math.Max(1, tile.TempLimit - 30);

    // the block takes its kind color; warm and hot parts turn orange and red
    private static SKColor BlockColor(HardwareTile tile)
    {
        double heat = Heat(tile);
        if (!double.IsNaN(heat))
            return heat >= 0.85 ? Alarm : heat >= 0.6 ? Warm : KindColor(tile.Kind);
        return tile.Kind != "fans" && tile.Load >= 90 ? Warm : KindColor(tile.Kind);
    }

    private static void DrawBlocks(SKCanvas c, Layout l, FancyTweenState tw)
    {
        float u = l.U;
        DashboardModel m = tw.Model(BlockDelay, BlockStep);
        float left = l.Lower.Left, right = l.Lower.Left + l.Arm;
        float top = l.Lower.Top + l.TopElbowH + l.Gap, bottom = l.Lower.Bottom - l.BottomElbowH - l.Gap;
        int n = m.Tiles.Count;
        if (n == 0)
        {
            Block(c, new SKRect(left, top, right, bottom), Off);
            return;
        }

        // every device: a colored tab with its name (the device color) and a dark readout beside it, so the arm
        // stays a console column without large bright areas; heat has its own colors in the readout
        float h = (bottom - top - l.Gap * (n - 1)) / n;
        bool compact = h < 60 * u;
        float tabW = 62 * u;
        for (int i = 0; i < n; i++)
        {
            HardwareTile tile = m.Tiles[i];
            var r = new SKRect(left, top + i * (h + l.Gap), right, top + i * (h + l.Gap) + h);
            var tab = new SKRect(r.Left, r.Top, r.Left + tabW, r.Bottom);
            var readout = new SKRect(tab.Right + 3 * u, r.Top, r.Right, r.Bottom);
            Block(c, tab, KindColor(tile.Kind));
            using (SKPaint panel = Fui.Fill(ReadoutBack))
                c.DrawRect(readout, panel);
            Label(c, tile.Label, tab.Right - 8 * u, tab.Bottom - 8 * u, Body(u), Bg, Bold, SKTextAlign.Right, 0.8f * u);

            float pad = 10 * u;
            string value = tile.Kind == "power" ? Fui.Watts(tile.Power) : tile.Kind is "net" ? tile.Detail : Fui.Pct(tile.Load);
            double heat = Heat(tile);
            SKColor heatColor = double.IsNaN(heat) ? Dim : TextColor(HeatColor(heat));
            string temp = double.IsNaN(tile.Temp) ? "" : (tile.Kind == "fans" ? "BOARD " : "") + Fui.Temp(tile.Temp, m.Fahrenheit) + HeatTag(heat);
            if (compact)
            {
                float y = r.MidY + Body(u) * 0.36f;
                float tw2 = Measure(temp, Micro(u), SemiBold);
                Label(c, value, readout.Right - pad, y, Fit(value, Body(u), readout.Width - 2 * pad - tw2 - 8 * u), Data, SemiBold, SKTextAlign.Right);
                Label(c, temp, readout.Left + pad, y, Micro(u), heatColor, SemiBold);
            }
            else
            {
                // the value on the first line (shrunk to fit long rates), the temperature with its heat word below
                float vy = readout.Top + Big(u) + 4 * u;
                Label(c, value, readout.Right - pad, vy, Fit(value, Big(u), readout.Width - 2 * pad), Data, SemiBold, SKTextAlign.Right);
                Label(c, temp, readout.Right - pad, vy + Micro(u) + 8 * u, Micro(u), heatColor, SemiBold, SKTextAlign.Right);

                // load (blue) and heat (heat colors) as two short meters at the bottom of the readout
                float mx = readout.Left + pad, mw = readout.Right - pad - mx;
                Meter(c, new SKRect(mx, readout.Bottom - 16 * u, mx + mw, readout.Bottom - 12 * u), Nz(tile.Load) / 100, Blue, Track);
                Meter(c, new SKRect(mx, readout.Bottom - 8 * u, mx + mw, readout.Bottom - 4 * u), heat, heatColor, Track);
            }
        }
        tw.Region(new SKRect(left - 2 * u, top - 2 * u, right + 2 * u, bottom + 2 * u), BlockDelay, n, BlockStep);
    }

    // ------------------------------------------------------------------ column 1: stress gauge, composition, flow

    // One track per hardware node: the lit part is the load, the track gets thicker with the share of the power.
    // In the motion layer a short bright segment runs along the lit part, faster with more load.
    private static void DrawFlow(SKCanvas c, Layout l, FancyTweenState tw, double t, FancyOptions o, bool motion)
    {
        float u = l.U;
        SKRect area = l.Flow;
        DashboardModel m = tw.Model(FlowDelay, FlowStep);
        List<HardwareTile> tiles = m.Tiles.Where(x => x.Kind != "power").ToList();
        float left = area.Left + 4 * u, right = area.Right - 4 * u;

        // the motion pass must find the tracks exactly where the static pass drew them: every position below
        // depends on the size only, never on the text of the values
        float y = area.Top + HeaderHeight(u);
        if (!motion)
            Header(c, "SYSTEM FLOW", "FILL = LOAD  //  THICKNESS = SHARE OF POWER", left, right, area.Top, u, Lilac, "FILL = LOAD  //  WIDTH = POWER");

        if (tiles.Count == 0)
            return;
        double total = Math.Max(1, m.TotalPower);
        float footH = Micro(u) + 10 * u;
        float rowH = Math.Min(54 * u, (area.Bottom - y - footH) / tiles.Count);
        bool tight = rowH < 46 * u;
        // fixed columns right of the track: in short rows the share of the power and the value, otherwise the value
        // with temperature and watts below it (the share then sits under the track)
        float labelW = 70 * u, shareW = tight ? 78 * u : 0, valueW = tight ? 118 * u : 150 * u;
        float trackRight = right - valueW - shareW - 14 * u;
        var missing = new List<string>();
        for (int i = 0; i < tiles.Count; i++)
        {
            HardwareTile tile = tiles[i];
            float ry = y + i * rowH;
            float cy = ry + rowH * 0.42f;
            bool hasPower = !double.IsNaN(tile.Power);
            double share = hasPower ? tile.Power / total : 0;
            float thick = (6 + (float)Math.Min(1, share * 1.6) * 14) * u;
            var track = new SKRect(left + labelW, cy - thick / 2, trackRight, cy + thick / 2);
            double load = Nz(tile.Load) / 100;
            SKColor color = KindColor(tile.Kind);
            float litRight = track.Left + Math.Max(thick, track.Width * (float)Math.Max(0, Math.Min(1, load)));

            if (motion)
            {
                if (load <= 0.03)
                    continue;
                // the runner: speed from the load, clipped to the lit part
                double speed = 0.18 + load * 0.55;
                double f = (t * speed + i * 0.23) % 1;
                float len = Math.Max(14 * u, (litRight - track.Left) * 0.18f);
                float rx = track.Left + (float)f * (litRight - track.Left + len) - len;
                int save = c.Save();
                using (var clip = new SKPath())
                {
                    clip.AddRoundRect(new SKRect(track.Left, track.Top, litRight, track.Bottom), thick / 2, thick / 2);
                    c.ClipPath(clip, SKClipOperation.Intersect, true);
                }
                using (SKPaint glint = Fui.Fill(SKColors.White.WithAlpha(0.55f)))
                    c.DrawRoundRect(new SKRect(rx, track.Top, rx + len, track.Bottom), thick / 2, thick / 2, glint);
                c.RestoreToCount(save);
                continue;
            }

            if (!hasPower)
                missing.Add(tile.Label);
            Label(c, tile.Label, left, cy + Small(u) * 0.36f, Small(u), KindColor(tile.Kind), Bold, spacing: 1 * u);
            Pill(c, track, Track);
            if (load > 0)
                Pill(c, new SKRect(track.Left, track.Top, litRight, track.Bottom), color);

            string value = tile.Kind is "net" or "disk" ? tile.Detail : Fui.Pct(tile.Load);
            if (tight)
            {
                // one line: the share of the power in its own column, then the value (temperature and watts are in the device column)
                if (hasPower)
                    Label(c, (share * 100).ToString("0", Inv) + "% PWR", right - valueW, cy + Small(u) * 0.36f, Micro(u), OffText, SemiBold, SKTextAlign.Right);
                else
                    // no power data: a dim tick keeps the rhythm of the column (named in the footnote)
                    Pill(c, new SKRect(right - valueW - 22 * u, cy - 1.5f * u, right - valueW, cy + 1.5f * u), Off);
                Label(c, value, right, cy + Small(u) * 0.36f, Fit(value, Body(u), valueW - 8 * u), Data, SemiBold, SKTextAlign.Right);
                continue;
            }

            if (hasPower)
                Label(c, (share * 100).ToString("0", Inv) + "% OF TOTAL POWER", track.Left, cy + thick / 2 + Micro(u) + 2 * u, Micro(u), OffText, Regular, spacing: 0.4f * u);
            var sub = new List<string>();
            if (!double.IsNaN(tile.Temp))
                sub.Add(Fui.Temp(tile.Temp, m.Fahrenheit));
            if (hasPower)
                sub.Add(Fui.Watts(tile.Power));
            Label(c, value, right, cy + Small(u) * 0.2f, Fit(value, Body(u), valueW - 8 * u), Data, SemiBold, SKTextAlign.Right);
            Label(c, string.Join("  ", sub), right, cy + Small(u) * 0.2f + Micro(u) + 4 * u, Micro(u), Dim, Regular, SKTextAlign.Right);
        }
        // the devices without a power sensor are named once, not on every row
        if (!motion && missing.Count > 0)
            Label(c, "NO POWER SENSOR: " + string.Join(", ", missing) + "  (NOT PART OF THE SHARE)", left, y + tiles.Count * rowH + Micro(u) + 2 * u, Micro(u), Dim, SemiBold, spacing: 0.4f * u);
        if (!motion)
            tw.Region(area, FlowDelay, tiles.Count, FlowStep);
    }

    // ------------------------------------------------------------------ column 2: cores, thermals, activity

    private static void DrawCoreColumn(SKCanvas c, Layout l, FancyTweenState tw)
    {
        float u = l.U;
        SKRect col = l.Col2;
        DashboardModel m = tw.Model(CoreCellDelay, CoreCellStep);
        int cores = m.CoreLoads.Length;
        float y = Header(c, "CORE MATRIX", cores.ToString("00", Inv) + " CORES  //  LOAD %  //  ! = LOAD ≥ 90 %", col.Left, col.Right, col.Top, u, Peach, "LOAD %  //  ! = LOAD ≥ 90");
        float sectionTop = y;
        if (cores > 0)
        {
            int perRow = cores <= 6 ? cores : cores <= 12 ? 6 : 8;
            float gap = 6 * u;
            float cell = (col.Width - (perRow - 1) * gap) / perRow;
            float cellH = Math.Max(46, 52 * u);
            // one decision for the whole grid: without the "C" when the widest case does not fit
            bool shortNames = Measure("C" + cores.ToString(Inv), Micro(u), Regular) + Measure("!99", Small(u), Bold) + 10 * u + Math.Max(8, 6 * u) > cell;
            for (int k = 0; k < cores; k++)
            {
                float cx = col.Left + (k % perRow) * (cell + gap);
                float cy = y + (k / perRow) * (cellH + gap);
                double load = m.CoreLoads[k];
                bool hot = load >= 90;
                // hot cells use a lighter red, so they differ from orange in lightness, not only in hue
                SKColor color = hot ? HotFill : LoadColor(load);
                var r = new SKRect(cx, cy, cx + cell, cy + cellH);
                float radius = Math.Min(10 * u, cellH / 3);
                using (SKPaint back = Fui.Fill(Fui.Hex("#1E1A17")))
                    c.DrawRoundRect(r, radius, radius, back);
                // the label row stays dark; below it the fill rises with the load (clipped to the rounded shape)
                float labelH = Micro(u) + 12 * u;
                float fillH = (r.Height - labelH) * (float)Math.Max(0, Math.Min(1, load / 100));
                int save = c.Save();
                using (var clip = new SKPath())
                {
                    clip.AddRoundRect(r, radius, radius);
                    c.ClipPath(clip, SKClipOperation.Intersect, true);
                }
                using (SKPaint fill = Fui.Fill(color))
                    c.DrawRect(new SKRect(r.Left, r.Bottom - fillH, r.Right, r.Bottom), fill);
                c.RestoreToCount(save);
                // name and value share the dark label row; the fill below is only the level
                string name = "C" + (k + 1).ToString(Inv);
                string shown = (hot ? "!" : "") + load.ToString("0", Inv);
                if (shortNames)
                    name = (k + 1).ToString(Inv);
                float ty = r.Top + Micro(u) + 5 * u;
                Label(c, name, r.Left + 5 * u, ty, Micro(u), OffText, Regular);
                Label(c, shown, r.Right - 5 * u, ty, Small(u), hot ? HotFill : Text, hot ? Bold : SemiBold, SKTextAlign.Right);
                if (hot)
                {
                    // hot cells get a light outline and a "!": the state does not rely on color alone
                    using SKPaint outline = Fui.Stroke(Text, 3 * u);
                    c.DrawRoundRect(SKRect.Inflate(r, -1.5f * u, -1.5f * u), radius, radius, outline);
                }
            }
            y += (float)Math.Ceiling(cores / (double)perRow) * (cellH + gap) + 14 * u;
            tw.Region(new SKRect(col.Left - 2 * u, sectionTop, col.Right + 2 * u, y), CoreCellDelay, cores, CoreCellStep);
        }
        else
        {
            Label(c, "n/a  (NO PER-CORE LOAD SENSORS)", col.Left, y + 14 * u, Micro(u), Dim, Regular);
            y += 30 * u;
        }

        // thermals: every component against its own limit
        m = tw.Model(ThermalDelay, ThermalStep);
        if (y < col.Bottom - 80 * u && m.Thermals.Count > 0)
        {
            sectionTop = y;
            y = Header(c, "THERMALS", "OF LIMIT", col.Left, col.Right, y, u, Tan);
            float rowH = Math.Max(30, 30 * u);
            foreach (ThermalEntry entry in m.Thermals)
            {
                if (y + rowH > col.Bottom - 40 * u)
                    break;
                double ratio = (entry.Temp - 30) / Math.Max(1, entry.Limit - 30);
                SKColor color = HeatColor(ratio);
                Label(c, Shorten(entry.Name, 26).ToUpperInvariant(), col.Left, y + Small(u), Small(u), Text, Regular);
                Label(c, Fui.Temp(entry.Temp, m.Fahrenheit) + "  /  " + Fui.Temp(entry.Limit, m.Fahrenheit) + HeatTag(ratio), col.Right, y + Small(u), Small(u), TextColor(color), SemiBold, SKTextAlign.Right);
                Meter(c, new SKRect(col.Left, y + Small(u) + 6 * u, col.Right, y + Small(u) + 12 * u), ratio, color, Track);
                y += rowH;
            }
            tw.Region(new SKRect(col.Left - 2 * u, sectionTop, col.Right + 2 * u, y), ThermalDelay, m.Thermals.Count, ThermalStep);

            // the heat colors, right below the thermals
            float ly = y + Small(u) + 2 * u, lx = col.Left;
            foreach (var (label, color) in new[] { ("OK", Data), ("WARM 60%", Warm), ("HOT 85% OF THE TEMP LIMIT", Alarm) })
            {
                Pill(c, new SKRect(lx, ly - 9 * u, lx + 18 * u, ly - 2 * u), color);
                lx += 24 * u;
                lx += Label(c, label, lx, ly, Micro(u), Dim, Regular) + 16 * u;
            }
            y = ly + 22 * u;
        }

        // activity history fills the rest of the column
        float rest = col.Bottom - y;
        m = tw.Next;
        if (rest > 110 * u && m.CpuHistory.Length > 1)
        {
            double scale = ActivityScale(m);
            double shown = tw.Prev != null ? FancyTween.Lerp(ActivityScale(tw.Prev), scale, tw.P(0)) : scale;
            string span = Math.Max(1, m.CpuHistory.Length * m.UpdateIntervalMs / 60000).ToString(Inv);
            y = Header(c, "ACTIVITY  " + span + " MIN", "SCALE 0 TO " + scale.ToString("0", Inv) + " %", col.Left, col.Right, y, u, Blue);

            float ky = y + Micro(u), kx = col.Left;
            foreach (var (label, values, color, dashed) in new[] { ("CPU", m.CpuHistory, Peach, false), ("GPU", m.GpuHistory, Lilac, true) })
            {
                if (values.Length == 0)
                    continue;
                using (SKPaint sample = Fui.Stroke(color, 3 * u))
                {
                    if (dashed)
                        sample.PathEffect = SKPathEffect.CreateDash(new[] { 6 * u, 4 * u }, 0);
                    c.DrawLine(kx, ky - 5 * u, kx + 22 * u, ky - 5 * u, sample);
                }
                kx += 28 * u;
                kx += Label(c, label, kx, ky, Micro(u), color, SemiBold) + 8 * u;
                kx += Label(c, "AVG " + values.Average().ToString("0", Inv) + "  MAX " + values.Max().ToString("0", Inv), kx, ky, Micro(u), Dim, Regular) + 20 * u;
            }
            y = ky + 12 * u;

            var box = new SKRect(col.Left + 30 * u, y, col.Right, col.Bottom);
            using (SKPaint back = Fui.Fill(Panel))
                c.DrawRoundRect(box, 8 * u, 8 * u, back);
            using (SKPaint grid = Fui.Stroke(Fui.Hex("#2A2420"), u))
            {
                for (int i = 1; i < 4; i++)
                    c.DrawLine(box.Left, box.Top + box.Height * i / 4, box.Right, box.Top + box.Height * i / 4, grid);
            }
            Label(c, shown.ToString("0", Inv), box.Left - 6 * u, box.Top + Micro(u), Micro(u), Dim, Regular, SKTextAlign.Right);
            Label(c, "0", box.Left - 6 * u, box.Bottom, Micro(u), Dim, Regular, SKTextAlign.Right);
            tw.Scrolling(new SKRect(box.Left - 34 * u, box.Top - 2 * u, box.Right + 2 * u, box.Bottom + 2 * u));
            Spark(c, box, FancyTween.Scroll(tw.PrevHistory(x => x.CpuHistory), m.CpuHistory, tw.Scroll), shown, Peach, u, false);
            Spark(c, box, FancyTween.Scroll(tw.PrevHistory(x => x.GpuHistory), m.GpuHistory, tw.Scroll), shown, Lilac, u, true);

            double seconds = m.CpuHistory.Length * m.UpdateIntervalMs / 1000.0;
            if (seconds < 60)
            {
                string note = "COLLECTING " + seconds.ToString("0", Inv) + " S";
                float nw = Measure(note, Micro(u), Regular) + 20 * u;
                var plate = new SKRect(box.Right - 8 * u - nw, box.Top + 8 * u, box.Right - 8 * u, box.Top + 12 * u + Micro(u) + 4 * u);
                Pill(c, plate, Off);
                Label(c, note, plate.MidX, plate.Bottom - 6 * u, Micro(u), Text, Regular, SKTextAlign.Center);
            }
        }
    }

    private static double ActivityScale(DashboardModel m)
    {
        double peak = Math.Max(m.CpuHistory.Length > 0 ? m.CpuHistory.Max() : 0, m.GpuHistory.Length > 0 ? m.GpuHistory.Max() : 0);
        return peak * 1.15 <= 25 ? 25 : peak * 1.15 <= 50 ? 50 : 100;
    }

    private static void Spark(SKCanvas c, SKRect r, (double X, double V)[] points, double scale, SKColor color, float u, bool dashed)
    {
        if (points.Length < 2)
            return;
        using var path = new SKPath();
        for (int i = 0; i < points.Length; i++)
        {
            float x = r.Left + r.Width * (float)points[i].X;
            float y = r.Bottom - 4 * u - (float)(Math.Max(0, Math.Min(scale, points[i].V)) / scale) * (r.Height - 8 * u);
            if (i == 0)
                path.MoveTo(x, y);
            else
                path.LineTo(x, y);
        }
        using SKPaint p = Fui.Stroke(color, 2.4f * u);
        p.StrokeJoin = SKStrokeJoin.Round;
        if (dashed)
            p.PathEffect = SKPathEffect.CreateDash(new[] { 7 * u, 4 * u }, 0);
        int save = c.Save();
        c.ClipRect(r);
        c.DrawPath(path, p);
        c.RestoreToCount(save);
    }

    // ------------------------------------------------------------------ column 3: telemetry, energy, headroom, power

    private static void DrawTelemetryColumn(SKCanvas c, Layout l, FancyTweenState tw)
    {
        float u = l.U;
        SKRect col = l.Col3;
        DashboardModel m = tw.Model(TelemetryDelay);
        float y = Header(c, "TELEMETRY", "", col.Left, col.Right, col.Top, u, Blue);
        float sectionTop = y;

        string est = m.OtherPowerEstimated ? "≈ " : "";
        var rows = new List<(string Label, string Value, SKColor Color)>
        {
            ("TOTAL POWER", est + Fui.Watts(m.TotalPower) + (m.OtherPowerEstimated ? " EST" : ""), Data),
            ("CPU PACKAGE", NA(Fui.Watts(m.CpuPower)), Text),
            ("GPU BOARD", NA(Fui.Watts(m.GpuPower)), Text),
            ("MEMORY", NA(Fui.Pct(m.MemoryLoad)) + (double.IsNaN(m.MemoryUsedGb) ? "" : "   " + m.MemoryUsedGb.ToString("0.0", Inv) + " GB"), Text),
            ("DISK I/O", SystemMetrics.Rate(m.DiskRead + m.DiskWrite), Text),
            ("NETWORK", "DOWN " + SystemMetrics.Rate(m.NetDown) + "   UP " + SystemMetrics.Rate(m.NetUp), Text),
            ("SESSION ENERGY", est + m.SessionEnergyWh.ToString("0.0", Inv) + " WH", Data)
        };
        float rowH = Math.Max(24, 24 * u);
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            // a small colored tab in front of every row, like the buttons of a console
            Block(c, new SKRect(col.Left, y + 4 * u, col.Left + 10 * u, y + rowH - 4 * u), i % 2 == 0 ? Lilac : Peach, true, false);
            Label(c, row.Label, col.Left + 18 * u, y + rowH * 0.5f + Small(u) * 0.36f, Small(u), Lilac, SemiBold, spacing: 0.8f * u);
            Label(c, row.Value, col.Right, y + rowH * 0.5f + Small(u) * 0.36f, Body(u), row.Color, SemiBold, SKTextAlign.Right);
            y += rowH;
        }
        if (rows.Any(r => r.Value.StartsWith("n/a", StringComparison.Ordinal)))
        {
            y += Micro(u) + 2 * u;
            Label(c, "n/a = NO SENSOR OR NEEDS ADMIN RIGHTS", col.Left, y, Micro(u), Dim, Regular);
        }
        y += 14 * u;
        tw.Region(new SKRect(col.Left - 2 * u, sectionTop, col.Right + 2 * u, y), TelemetryDelay);

        // energy distribution as one stacked pill; the estimated rest is hatched
        m = tw.Model(EnergyDelay);
        sectionTop = y;
        y = Header(c, "ENERGY DISTRIBUTION", m.OtherPowerEstimated ? "REST ESTIMATED" : "MEASURED", col.Left, col.Right, y, u, Peach);
        double cpuP = Nz(m.CpuPower), gpuP = Nz(m.GpuPower), rest = Nz(m.OtherPower), sum = Math.Max(1, cpuP + gpuP + rest);
        var parts = new[] { ("CPU", cpuP, Peach, false, double.IsNaN(m.CpuPower)), ("GPU", gpuP, Lilac, false, double.IsNaN(m.GpuPower)), ("REST", rest, Tan, m.OtherPowerEstimated, false) };
        var bar = new SKRect(col.Left, y, col.Right, y + 16 * u);
        int save = c.Save();
        using (var clip = new SKPath())
        {
            clip.AddRoundRect(bar, bar.Height / 2, bar.Height / 2);
            c.ClipPath(clip, SKClipOperation.Intersect, true);
        }
        using (SKPaint back = Fui.Fill(Track))
            c.DrawRect(bar, back);
        float bx = bar.Left;
        foreach (var (label, value, color, hatched, missing) in parts)
        {
            if (missing)
                continue;
            float w = (float)(value / sum) * bar.Width;
            var seg = new SKRect(bx, bar.Top, bx + Math.Max(0, w - 3 * u), bar.Bottom);
            if (hatched)
                Hatch(c, seg, color, u);
            else
            {
                using SKPaint p = Fui.Fill(color);
                c.DrawRect(seg, p);
            }
            bx += w;
        }
        c.RestoreToCount(save);
        y += 16 * u + Small(u) + 10 * u;
        float lx = col.Left;
        foreach (var (label, value, color, hatched, missing) in parts)
        {
            var sw = new SKRect(lx, y - 10 * u, lx + 20 * u, y - 2 * u);
            if (hatched)
                Hatch(c, sw, color, u);
            else
                Pill(c, sw, color);
            lx += 26 * u;
            string share = missing ? "n/a" : (hatched ? "≈" : "") + (value / sum * 100).ToString("0", Inv) + "%";
            string name = label == "REST" && (double.IsNaN(m.CpuPower) || double.IsNaN(m.GpuPower))
                ? "REST + " + (double.IsNaN(m.CpuPower) ? "CPU" : "GPU")
                : label;
            lx += Label(c, name + " " + share, lx, y, Small(u), missing ? Dim : Text, SemiBold) + 20 * u;
        }
        if (m.OtherPowerEstimated)
        {
            y += Micro(u) + 8 * u;
            Label(c, "REST ≈ BOARD + RAM + DRIVES + FANS", col.Left, y, Micro(u), Dim, SemiBold);
        }
        if (double.IsNaN(m.CpuPower) || double.IsNaN(m.GpuPower))
        {
            y += Micro(u) + 6 * u;
            Label(c, (double.IsNaN(m.CpuPower) ? "CPU" : "GPU") + " POWER n/a: THE SPLIT IS INCOMPLETE", col.Left, y, Micro(u), Warm, SemiBold);
        }
        y += 20 * u;
        tw.Region(new SKRect(col.Left - 2 * u, sectionTop, col.Right + 2 * u, y), EnergyDelay);

        // thermal headroom: the reserve to the nearest limit as a long meter with the number in front
        m = tw.Model(HeadroomDelay);
        if (col.Bottom - y > 200 * u)
        {
            sectionTop = y;
            y = Header(c, "THERMAL HEADROOM", string.IsNullOrEmpty(m.HottestName) ? "n/a" : Shorten(m.HottestName, 22).ToUpperInvariant(), col.Left, col.Right, y, u, Tan);
            double reserve = 1 - m.ThermalRatio;
            bool partial = double.IsNaN(m.CpuTemp);
            string k = double.IsNaN(m.ThermalHeadroom) ? "n/a" : (m.Fahrenheit ? m.ThermalHeadroom * 1.8 : m.ThermalHeadroom).ToString("0", Inv) + (m.Fahrenheit ? " °F" : " °C");
            float kw = Label(c, k, col.Left, y + Big(u), Big(u) * 1.2f, TextColor(HeatColor(m.ThermalRatio)), SemiBold);
            float mx = col.Left + Math.Max(kw, 70 * u) + 18 * u;
            var meter = new SKRect(mx, y + Big(u) * 0.55f, col.Right, y + Big(u) * 0.55f + 12 * u);
            Meter(c, meter, reserve, HeatColor(m.ThermalRatio), Track);
            if (partial)
            {
                // incomplete like the estimated rest of the energy split: the lit part is hatched
                float lit = meter.Left + Math.Max(meter.Height, meter.Width * (float)Math.Max(0, Math.Min(1, reserve)));
                int keep = c.Save();
                using (var clip = new SKPath())
                {
                    clip.AddRoundRect(new SKRect(meter.Left, meter.Top, lit, meter.Bottom), meter.Height / 2, meter.Height / 2);
                    c.ClipPath(clip, SKClipOperation.Intersect, true);
                }
                using (SKPaint dark = Fui.Fill(Track))
                    c.DrawRect(meter, dark);
                Hatch(c, new SKRect(meter.Left, meter.Top, lit, meter.Bottom), HeatColor(m.ThermalRatio), u);
                c.RestoreToCount(keep);
            }
            y += Big(u) * 1.2f + 10 * u;
            // without a CPU temperature the reserve is not the whole picture: said in the warning color
            if (partial)
                Label(c, "TO THE NEAREST LIMIT, CPU TEMP n/a: NOT COUNTED", col.Left, y, Micro(u), Warm, SemiBold);
            else
                Label(c, "TO THE NEAREST LIMIT, RESERVE " + (reserve * 100).ToString("0", Inv) + "%", col.Left, y, Micro(u), Dim, Regular);
            Label(c, "HOTTEST  " + (double.IsNaN(m.HottestTemp) ? "n/a" : Fui.Temp(m.HottestTemp, m.Fahrenheit)), col.Right, y, Small(u), Text, SemiBold, SKTextAlign.Right);
            y += 22 * u;
            tw.Region(new SKRect(col.Left - 2 * u, sectionTop, col.Right + 2 * u, y), HeadroomDelay);
        }

        // power scope with a scale
        m = tw.Next;
        float scopeH = col.Bottom - y - Small(u) - 30 * u;
        if (scopeH > 60)
        {
            string span = Math.Max(1, m.PowerHistory.Length * m.UpdateIntervalMs / 60000).ToString(Inv);
            string minMax = m.PowerHistory.Length > 1 ? "MIN " + m.PowerHistory.Min().ToString("0", Inv) + "   MAX " + m.PowerHistory.Max().ToString("0", Inv) + " W" : est + Fui.Watts(m.TotalPower);
            y = Header(c, "POWER  " + span + " MIN", minMax, col.Left, col.Right, y, u, Accent);
            var scope = new SKRect(col.Left + 40 * u, y, col.Right, col.Bottom);
            tw.Scrolling(new SKRect(col.Left - 2 * u, scope.Top - 10 * u, col.Right + 2 * u, scope.Bottom + 10 * u));
            DrawScope(c, scope, tw.PrevHistory(x => x.PowerHistory), m.PowerHistory, tw.Scroll, tw.P(0), u);
        }
    }

    private static void DrawScope(SKCanvas c, SKRect r, double[] previous, double[] values, double scroll, double ease, float u)
    {
        using (SKPaint back = Fui.Fill(Panel))
            c.DrawRoundRect(r, 8 * u, 8 * u, back);

        double lo = values.Length > 0 ? values.Min() : 0, hi = values.Length > 0 ? values.Max() : 100;
        if (previous != null && previous.Length > 0 && values.Length > 0)
        {
            lo = FancyTween.Lerp(previous.Min(), lo, ease);
            hi = FancyTween.Lerp(previous.Max(), hi, ease);
        }
        double margin = Math.Max(5, (hi - lo) * 0.2);
        int lines = Math.Max(3, Math.Min(4, (int)(r.Height / (26 * u))));
        double step = NiceStep((hi - lo + 2 * margin) / lines);
        double min = Math.Max(0, Math.Floor((lo - margin) / step) * step);
        double max = Math.Max(min + step * 2, Math.Ceiling((hi + margin) / step) * step);
        using (SKPaint grid = Fui.Stroke(Fui.Hex("#2A2420"), u))
        {
            int every = 1;
            while (r.Height * step * every / (max - min) < 24 * u && every < 10)
                every++;
            int k = 0;
            for (double v = min; v <= max + step / 2; v += step, k++)
            {
                float gy = r.Bottom - (float)((v - min) / (max - min)) * r.Height;
                c.DrawLine(r.Left, gy, r.Right, gy, grid);
                if (k % every == 0)
                    Label(c, v.ToString("0", Inv), r.Left - 6 * u, gy + 5 * u, Micro(u), Dim, Regular, SKTextAlign.Right);
            }
        }
        if (values.Length < 2)
            return;

        (double X, double V)[] points = FancyTween.Scroll(previous, values, scroll);
        using var line = new SKPath();
        using var area = new SKPath();
        float py(double v) => r.Bottom - (float)((Math.Max(min, Math.Min(max, v)) - min) / (max - min)) * r.Height;
        float px(double f) => r.Left + r.Width * (float)f;
        for (int i = 0; i < points.Length; i++)
        {
            float x = px(points[i].X);
            if (i == 0)
            {
                line.MoveTo(x, py(points[i].V));
                area.MoveTo(x, r.Bottom);
            }
            else
                line.LineTo(x, py(points[i].V));
            area.LineTo(x, py(points[i].V));
        }
        area.LineTo(px(points[points.Length - 1].X), r.Bottom);
        area.Close();

        int save = c.Save();
        c.ClipRect(r);
        using (SKPaint fill = Fui.Fill(Accent.WithAlpha(0.22f)))
            c.DrawPath(area, fill);
        using (SKPaint p = Fui.Stroke(Accent, 2.4f * u))
        {
            p.StrokeJoin = SKStrokeJoin.Round;
            c.DrawPath(line, p);
        }
        c.RestoreToCount(save);

        var visible = points.Where(q => q.X >= 0 && q.X <= 1).ToArray();
        if (visible.Length == 0)
            return;
        double vMax = visible.Max(q => q.V), vMin = visible.Min(q => q.V);
        foreach (var q in new[] { visible.First(q => q.V == vMax), visible.First(q => q.V == vMin) })
        {
            using SKPaint dot = Fui.Fill(Text);
            c.DrawCircle(px(q.X), py(q.V), 4 * u, dot);
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

    // ------------------------------------------------------------------ boot

    private static void DrawBoot(SKCanvas c, int w, int h, float u, double boot)
    {
        float alpha = (float)Math.Max(0, 1 - Math.Max(0, boot - 0.75) / 0.25);
        using (SKPaint veil = Fui.Fill(Bg.WithAlpha(0.97f * alpha)))
            c.DrawRect(0, 0, w, h, veil);
        Label(c, "INITIALIZING SENSOR GRID", w / 2f, h / 2f - 14 * u, Math.Max(22, 30 * u), Accent.WithAlpha(alpha), Bold, SKTextAlign.Center, 3 * u);

        // a row of blocks that light up one after another
        const int count = 12;
        float bw = 30 * u, gap = 6 * u, total = count * bw + (count - 1) * gap;
        float x = w / 2f - total / 2, y = h / 2f + 4 * u;
        SKColor[] colors = { Peach, Lilac, Accent, Blue, Tan };
        for (int i = 0; i < count; i++)
        {
            bool on = boot * count > i;
            Block(c, new SKRect(x, y, x + bw, y + 14 * u), (on ? colors[i % colors.Length] : Off).WithAlpha(alpha), i == 0, i == count - 1);
            x += bw + gap;
        }
        var plate = new SKRect(w / 2f - 60 * u, y + 24 * u, w / 2f + 60 * u, y + 52 * u);
        Pill(c, plate, Off.WithAlpha(alpha));
        Label(c, (boot * 100).ToString("000", CultureInfo.InvariantCulture) + " %", w / 2f, plate.MidY + Small(u) * 0.36f, Small(u), Text.WithAlpha(alpha), SemiBold, SKTextAlign.Center, 1.5f * u);
    }

    // ------------------------------------------------------------------ helpers

    // section heading: a small rounded tab, the title in its color and a thin bar to the meta text
    private static float HeaderHeight(float u) => Body(u) + 4 * u + 12 * u;

    private static float Header(SKCanvas c, string title, string meta, float left, float right, float y, float u, SKColor color, string shortMeta = null)
    {
        float h = Body(u) + 4 * u;
        Block(c, new SKRect(left, y, left + 18 * u, y + h), color, true, false);
        float tw = Label(c, title, left + 26 * u, y + h * 0.5f + Body(u) * 0.36f, Body(u), color, Bold, spacing: 1.2f * u);
        if (!string.IsNullOrEmpty(meta) && left + 26 * u + tw + 24 * u + Measure(meta, Micro(u), Regular, 0.6f * u) > right)
            meta = shortMeta ?? "";
        float mw = string.IsNullOrEmpty(meta) ? 0 : Label(c, meta, right, y + h * 0.5f + Micro(u) * 0.36f, Micro(u), Dim, Regular, SKTextAlign.Right, 0.6f * u);
        float from = left + 26 * u + tw + 12 * u, to = right - (mw > 0 ? mw + 12 * u : 0);
        if (to - from > 12 * u)
            Block(c, new SKRect(from, y + h * 0.5f - 2 * u, to, y + h * 0.5f + 2 * u), color.WithAlpha(0.45f));
        return y + h + 12 * u;
    }

    private static void Hatch(SKCanvas c, SKRect r, SKColor color, float u)
    {
        int save = c.Save();
        c.ClipRect(r);
        using (SKPaint back = Fui.Fill(color.WithAlpha(0.25f)))
            c.DrawRect(r, back);
        using SKPaint p = Fui.Stroke(color, 2 * u);
        for (float x = r.Left - r.Height; x < r.Right; x += 6 * u)
            c.DrawLine(x, r.Bottom, x + r.Height, r.Top, p);
        c.RestoreToCount(save);
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

    private static string HeatTag(double ratio) => double.IsNaN(ratio) ? "" : ratio >= 0.85 ? "  HOT" : ratio >= 0.6 ? "  WARM" : "";

    private static string NA(string value) => value == "--" ? "n/a" : value;

    // the largest size (down to 60 %) at which the text fits the width
    private static float Fit(string text, float size, float width)
    {
        float s = size;
        while (s > size * 0.6f && Measure(text, s, SemiBold) > width)
            s -= 0.5f;
        return s;
    }

    // red as text uses the lighter tone (about 8:1 on black); the alarm red stays for fills
    private static SKColor TextColor(SKColor color) => color == Alarm ? AlarmText : color;

    private static string MachineName(DashboardModel m) => string.IsNullOrEmpty(m.Machine) ? "LOCAL" : m.Machine.ToUpperInvariant();

    private static string Shorten(string text, int max)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= max)
            return text ?? "";
        return text.Substring(0, max - 1) + "…";
    }

    private static double Nz(double v) => double.IsNaN(v) ? 0 : v;
}
