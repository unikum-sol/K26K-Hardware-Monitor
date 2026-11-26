// K26K Hardware Monitor - drawing helpers of the Bridge design (elbows, pills, blocks).
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
using LibreHardwareMonitor.Windows.Forms.UI.Design;
using LibreHardwareMonitor.Windows.Forms.UI.SciFi;
using SkiaSharp;

namespace LibreHardwareMonitor.Windows.Forms.UI.Bridge;

/// <summary>Which corner of a frame an elbow sits in.</summary>
public enum Corner
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight
}

/// <summary>
/// The shapes of the Bridge design: thick flat bars that turn the corner in a rounded elbow, pill buttons
/// and blocks with rounded ends. Colors as Skia colors, the lettering is Antonio.
/// </summary>
public static class BridgeDraw
{
    public static readonly SKColor Bg = Fui.Hex("#000000");
    public static readonly SKColor Panel = Fui.Hex("#0B0B12");
    public static readonly SKColor Text = Fui.Hex("#F2E6D8");
    public static readonly SKColor Dim = Fui.Hex("#B5AAA0");
    public static readonly SKColor Accent = Fui.Hex("#FF9C3A");
    public static readonly SKColor Peach = Fui.Hex("#FFC890");
    public static readonly SKColor Lilac = Fui.Hex("#C79BD6");
    public static readonly SKColor Blue = Fui.Hex("#8FA8FF");
    public static readonly SKColor Tan = Fui.Hex("#D9A877");
    public static readonly SKColor Data = Fui.Hex("#FFE08A");
    public static readonly SKColor Alarm = Fui.Hex("#FF5C4D");
    /// <summary>Red for text on black (lighter than the alarm fill, about 8:1).</summary>
    public static readonly SKColor AlarmText = Fui.Hex("#FF7A6B");
    public static readonly SKColor Warm = Fui.Hex("#FF9C3A");
    public static readonly SKColor Track = Fui.Hex("#2E2822");
    public static readonly SKColor TrackStrong = Fui.Hex("#5A4C40");

    public const string Regular = FontManager.BridgeRegular;
    public const string SemiBold = FontManager.BridgeSemiBold;
    public const string Bold = FontManager.BridgeBold;

    public static readonly CaptionPalette Caption = new()
    {
        Glyph = Text,
        GlyphInactive = Dim,
        Accent = Accent,
        Alarm = Alarm,
        Background = Bg,
        Rule = TrackStrong,
        Pills = true,
        PillMinimize = Lilac,
        PillMaximize = Peach,
        PillClose = Alarm,
        PillInactive = Fui.Hex("#6A6058")
    };

    /// <summary>
    /// An elbow: an arm of width <paramref name="arm"/> along one side of <paramref name="r"/> and a bar of height
    /// <paramref name="bar"/> along the top or bottom, joined by a large outer and a small inner rounding.
    /// </summary>
    public static SKPath Elbow(SKRect r, float arm, float bar, Corner corner, float outer = -1, float inner = -1)
    {
        arm = Math.Min(arm, r.Width);
        bar = Math.Min(bar, r.Height);
        float ro = outer >= 0 ? outer : Math.Min(arm, r.Height) * 0.9f;
        ro = Math.Min(ro, Math.Min(r.Width, r.Height));
        float ri = inner >= 0 ? inner : Math.Min(bar, arm) * 0.8f;
        ri = Math.Max(0, Math.Min(ri, Math.Min(r.Width - arm, r.Height - bar)));

        // drawn as top left in a local box, then mirrored into the wanted corner
        float w = r.Width, h = r.Height;
        var p = new SKPath();
        p.MoveTo(0, h);
        p.LineTo(0, ro);
        p.ArcTo(new SKRect(0, 0, 2 * ro, 2 * ro), 180, 90, false);
        p.LineTo(w, 0);
        p.LineTo(w, bar);
        p.LineTo(arm + ri, bar);
        if (ri > 0)
            p.ArcTo(new SKRect(arm, bar, arm + 2 * ri, bar + 2 * ri), 270, -90, false);
        p.LineTo(arm, h);
        p.Close();

        bool right = corner is Corner.TopRight or Corner.BottomRight;
        bool bottom = corner is Corner.BottomLeft or Corner.BottomRight;
        SKMatrix m = SKMatrix.CreateScale(right ? -1 : 1, bottom ? -1 : 1);
        m = m.PostConcat(SKMatrix.CreateTranslation(right ? r.Right : r.Left, bottom ? r.Bottom : r.Top));
        p.Transform(m);
        return p;
    }

    public static void FillElbow(SKCanvas c, SKRect r, float arm, float bar, Corner corner, SKColor color, float outer = -1, float inner = -1)
    {
        using SKPath path = Elbow(r, arm, bar, corner, outer, inner);
        using SKPaint fill = Fui.Fill(color);
        c.DrawPath(path, fill);
    }

    /// <summary>A pill: a rectangle with fully rounded ends.</summary>
    public static void Pill(SKCanvas c, SKRect r, SKColor color)
    {
        float radius = Math.Min(r.Height, r.Width) / 2;
        using SKPaint fill = Fui.Fill(color);
        c.DrawRoundRect(r, radius, radius, fill);
    }

    /// <summary>A block with a rounded left and/or right end (the caps at the ends of the bars).</summary>
    public static void Block(SKCanvas c, SKRect r, SKColor color, bool roundLeft = false, bool roundRight = false)
    {
        using SKPaint fill = Fui.Fill(color);
        if (!roundLeft && !roundRight)
        {
            c.DrawRect(r, fill);
            return;
        }

        float d = Math.Min(r.Height, r.Width);
        using var path = new SKPath();
        if (roundLeft)
        {
            path.MoveTo(r.Left + d / 2, r.Bottom);
            path.ArcTo(new SKRect(r.Left, r.Top, r.Left + d, r.Bottom), 90, 180, false);
        }
        else
        {
            path.MoveTo(r.Left, r.Bottom);
            path.LineTo(r.Left, r.Top);
        }
        if (roundRight)
        {
            path.LineTo(r.Right - d / 2, r.Top);
            path.ArcTo(new SKRect(r.Right - d, r.Top, r.Right, r.Bottom), 270, 180, false);
        }
        else
        {
            path.LineTo(r.Right, r.Top);
            path.LineTo(r.Right, r.Bottom);
        }
        path.Close();
        c.DrawPath(path, fill);
    }

    /// <summary>A horizontal meter: a dark pill track and a lit part with a rounded end.</summary>
    public static void Meter(SKCanvas c, SKRect r, double fraction, SKColor color, SKColor track)
    {
        Pill(c, r, track);
        if (double.IsNaN(fraction))
            return;
        float f = (float)Math.Max(0, Math.Min(1, fraction));
        float w = Math.Max(r.Height, r.Width * f);
        if (f <= 0)
            return;
        Pill(c, new SKRect(r.Left, r.Top, r.Left + w, r.Bottom), color);
    }

    /// <summary>Lettering in Antonio (uppercase is up to the caller). Returns the drawn width.</summary>
    public static float Label(SKCanvas c, string text, float x, float y, float size, SKColor color, string weight = SemiBold,
                              SKTextAlign align = SKTextAlign.Left, float spacing = 0) =>
        Fui.Text(c, Plain(text), x, y, size, color, weight, align, spacing);

    public static float Measure(string text, float size, string weight = SemiBold, float spacing = 0) => Fui.Measure(Plain(text), size, weight, spacing);

    /// <summary>Antonio has no arrows and no symbols: they become words (the values of the model use them).</summary>
    public static string Plain(string text)
    {
        if (string.IsNullOrEmpty(text) || text.IndexOfAny(Symbols) < 0)
            return text;
        return text.Replace("↓ ", "DOWN ").Replace("↑ ", "UP ").Replace("↓", "DOWN ").Replace("↑", "UP ")
                   .Replace("▲ ", "").Replace("● ", "").Replace("▲", "").Replace("●", "");
    }

    private static readonly char[] Symbols = { '↓', '↑', '▲', '●' };

    /// <summary>
    /// A short code of digits for a block, like the numbers on console panels. Stable for one name,
    /// so a block keeps its code across restarts.
    /// </summary>
    public static string Code(string name, int salt = 0)
    {
        unchecked
        {
            int h = 17 + salt * 31;
            foreach (char ch in name ?? "")
                h = h * 31 + ch;
            h &= 0x7FFFFFFF;
            return (h % 90 + 10).ToString("00", System.Globalization.CultureInfo.InvariantCulture) + "-" +
                   (h / 90 % 9000 + 1000).ToString("0000", System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Color of a load-like value: blue, orange from 70 %, red from 90 %.</summary>
    public static SKColor LoadColor(double load) => load >= 90 ? Alarm : load >= 70 ? Warm : Blue;

    /// <summary>Color of a temperature ratio (0 = cool, 1 = at the limit).</summary>
    public static SKColor HeatColor(double ratio) => ratio >= 0.85 ? Alarm : ratio >= 0.6 ? Warm : Data;

    public static SKColor StateColor(string state) => state == "THERMAL" ? Alarm : state == "LOAD" ? Warm : Blue;
}
