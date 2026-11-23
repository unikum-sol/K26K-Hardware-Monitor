// K26K Hardware Monitor - drawing helpers for the futuristic (FUI) designs.
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
using LibreHardwareMonitor.Windows.Forms.UI.Design;
using SkiaSharp;

namespace LibreHardwareMonitor.Windows.Forms.UI.SciFi;

public static class Fui
{
    private static readonly Dictionary<(string, float), SKFont> Fonts = new();
    private static readonly Dictionary<float, SKMaskFilter> Blurs = new();

    public static SKColor Hex(string hex) => SKColor.Parse(hex);

    public static SKColor WithAlpha(this SKColor c, float alpha) => c.WithAlpha((byte)Math.Max(0, Math.Min(255, alpha * 255)));

    public static SKColor Mix(SKColor a, SKColor b, float t)
    {
        t = Math.Max(0, Math.Min(1, t));
        return new SKColor((byte)(a.Red + (b.Red - a.Red) * t), (byte)(a.Green + (b.Green - a.Green) * t),
                           (byte)(a.Blue + (b.Blue - a.Blue) * t), (byte)(a.Alpha + (b.Alpha - a.Alpha) * t));
    }

    public static SKFont Font(float size, string weight = "Regular")
    {
        size = (float)Math.Round(size * 2) / 2;
        if (!Fonts.TryGetValue((weight, size), out SKFont font))
        {
            font = new SKFont(FontManager.Typeface(weight), size) { Edging = SKFontEdging.SubpixelAntialias, Subpixel = true };
            Fonts[(weight, size)] = font;
        }
        return font;
    }

    public static SKMaskFilter Blur(float sigma)
    {
        sigma = (float)Math.Round(sigma * 2) / 2;
        if (!Blurs.TryGetValue(sigma, out SKMaskFilter blur))
        {
            blur = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, sigma);
            Blurs[sigma] = blur;
        }
        return blur;
    }

    public static SKPaint Fill(SKColor color) => new() { Color = color, IsAntialias = true, Style = SKPaintStyle.Fill };

    public static SKPaint Stroke(SKColor color, float width = 1) => new() { Color = color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = width };

    public static float Measure(string text, float size, string weight = "Regular", float spacing = 0)
    {
        SKFont font = Font(size, weight);
        if (string.IsNullOrEmpty(text))
            return 0;
        return font.MeasureText(text) + spacing * Math.Max(0, text.Length - 1);
    }

    /// <summary>Text glow on or off (all drawing happens on the UI thread; the Fancy renderer sets it per frame).</summary>
    public static bool GlowEnabled { get; set; } = true;

    /// <summary>Draws text with optional letter spacing and glow. Returns the drawn width.</summary>
    public static float Text(SKCanvas canvas, string text, float x, float y, float size, SKColor color, string weight = "Regular",
                             SKTextAlign align = SKTextAlign.Left, float spacing = 0, float glow = 0)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        SKFont font = Font(size, weight);
        float width = Measure(text, size, weight, spacing);
        float start = align == SKTextAlign.Center ? x - width / 2 : align == SKTextAlign.Right ? x - width : x;

        if (glow > 0 && GlowEnabled)
        {
            using SKPaint halo = Fill(color.WithAlpha(0.55f));
            halo.MaskFilter = Blur(glow);
            DrawRun(canvas, text, start, y, font, halo, spacing);
        }

        using SKPaint paint = Fill(color);
        DrawRun(canvas, text, start, y, font, paint, spacing);
        return width;
    }

    private static void DrawRun(SKCanvas canvas, string text, float x, float y, SKFont font, SKPaint paint, float spacing)
    {
        if (spacing == 0)
        {
            canvas.DrawText(text, x, y, SKTextAlign.Left, font, paint);
            return;
        }

        foreach (char c in text)
        {
            string s = c.ToString();
            canvas.DrawText(s, x, y, SKTextAlign.Left, font, paint);
            x += font.MeasureText(s) + spacing;
        }
    }

    /// <summary>Corner brackets around a rectangle.</summary>
    public static void Brackets(SKCanvas canvas, SKRect r, float length, SKColor color, float width = 1)
    {
        using SKPaint p = Stroke(color, width);
        p.StrokeCap = SKStrokeCap.Square;
        canvas.DrawLine(r.Left, r.Top, r.Left + length, r.Top, p);
        canvas.DrawLine(r.Left, r.Top, r.Left, r.Top + length, p);
        canvas.DrawLine(r.Right, r.Top, r.Right - length, r.Top, p);
        canvas.DrawLine(r.Right, r.Top, r.Right, r.Top + length, p);
        canvas.DrawLine(r.Left, r.Bottom, r.Left + length, r.Bottom, p);
        canvas.DrawLine(r.Left, r.Bottom, r.Left, r.Bottom - length, p);
        canvas.DrawLine(r.Right, r.Bottom, r.Right - length, r.Bottom, p);
        canvas.DrawLine(r.Right, r.Bottom, r.Right, r.Bottom - length, p);
    }

    /// <summary>A frame with one cut corner (top right), typical for FUI panels.</summary>
    public static SKPath CutFrame(SKRect r, float cut)
    {
        var path = new SKPath();
        path.MoveTo(r.Left, r.Top);
        path.LineTo(r.Right - cut, r.Top);
        path.LineTo(r.Right, r.Top + cut);
        path.LineTo(r.Right, r.Bottom);
        path.LineTo(r.Left, r.Bottom);
        path.Close();
        return path;
    }

    public static void Bar(SKCanvas canvas, SKRect r, double fraction, SKColor color, SKColor track)
    {
        using (SKPaint t = Fill(track))
            canvas.DrawRect(r, t);
        if (double.IsNaN(fraction))
            return;
        float f = (float)Math.Max(0, Math.Min(1, fraction));
        using SKPaint p = Fill(color);
        canvas.DrawRect(new SKRect(r.Left, r.Top, r.Left + Math.Max(r.Height > 2 ? 2 : 1, r.Width * f), r.Bottom), p);
    }

    /// <summary>Segmented meter: <paramref name="count"/> cells, lit up to the fraction.</summary>
    public static void Segments(SKCanvas canvas, SKRect r, double fraction, int count, SKColor on, SKColor off, float gap = 1)
    {
        float cell = (r.Width - gap * (count - 1)) / count;
        int lit = double.IsNaN(fraction) ? 0 : (int)Math.Round(Math.Max(0, Math.Min(1, fraction)) * count);
        using SKPaint a = Fill(on);
        using SKPaint b = Fill(off);
        for (int i = 0; i < count; i++)
        {
            float x = r.Left + i * (cell + gap);
            canvas.DrawRect(new SKRect(x, r.Top, x + cell, r.Bottom), i < lit ? a : b);
        }
    }

    public static string Temp(double celsius, bool fahrenheit)
    {
        if (double.IsNaN(celsius))
            return "n/a";
        double v = fahrenheit ? celsius * 1.8 + 32 : celsius;
        return v.ToString("0", CultureInfo.InvariantCulture) + (fahrenheit ? "°F" : "°C");
    }

    public static string Pct(double value) => double.IsNaN(value) ? "n/a" : value.ToString("0", CultureInfo.InvariantCulture) + "%";

    public static string Watts(double value) => double.IsNaN(value) ? "n/a" : value.ToString(value < 10 ? "0.0" : "0", CultureInfo.InvariantCulture) + " W";

    public static string Duration(double seconds)
    {
        var t = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return ((int)t.TotalHours).ToString("00", CultureInfo.InvariantCulture) + ":" + t.Minutes.ToString("00", CultureInfo.InvariantCulture) + ":" + t.Seconds.ToString("00", CultureInfo.InvariantCulture);
    }
}
