// K26K Hardware Monitor - Sci-Fi decorations for the sensor tree.
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
using System.Drawing;
using System.Drawing.Imaging;
using Aga.Controls.Tree;
using Aga.Controls.Tree.NodeControls;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Windows.Forms.UI.Themes;

namespace LibreHardwareMonitor.Windows.Forms.UI.SciFi;

/// <summary>Colors of the meters in the tree; each design (Sci-Fi, Bridge) has its own set.</summary>
public sealed class MeterPalette
{
    public Color Normal { get; set; }
    public Color Warm { get; set; }
    public Color Hot { get; set; }
    public Color Off { get; set; }
    /// <summary>Rounded cells (Bridge) instead of sharp ones.</summary>
    public bool Rounded { get; set; }

    public static readonly MeterPalette SciFi = new()
    {
        Normal = SciFiColors.Accent,
        Warm = SciFiColors.Cool,
        Hot = SciFiColors.Alarm,
        Off = ColorTranslator.FromHtml("#5A7B94")
    };
}

/// <summary>
/// Segmented meter in front of the value for load-like sensors (Load, Control, Level, Humidity).
/// Takes no space at all while the Sci-Fi design is off, so Classic is unchanged.
/// </summary>
public sealed class SciFiBarControl : NodeControl
{
    private const int Segments = 10;

    public static bool Active { get; set; }

    public static MeterPalette Colors { get; set; } = MeterPalette.SciFi;

    public int BarWidth { get; set; } = 62;

    public override Size MeasureSize(TreeNodeAdv node, DrawContext context)
    {
        if (!Active || !(node.Tag is SensorNode))
            return Size.Empty;
        return new Size(BarWidth, 8);
    }

    public override void Draw(TreeNodeAdv node, DrawContext context)
    {
        if (!Active || !(node.Tag is SensorNode sensorNode) || !IsPercent(sensorNode.Sensor.SensorType))
            return;

        float? value = sensorNode.Sensor.Value;
        Rectangle r = context.Bounds;
        int h = 8;
        int y = r.Top + (r.Height - h) / 2;
        int w = BarWidth - 8;
        int cell = (w - (Segments - 1)) / Segments;
        int lit = value.HasValue ? (int)Math.Round(Math.Max(0, Math.Min(100, value.Value)) / 100 * Segments) : 0;
        MeterPalette palette = Colors;
        Color on = value >= 90 ? palette.Hot : value >= 70 ? palette.Warm : palette.Normal;

        using var onBrush = new SolidBrush(on);
        using var offBrush = new SolidBrush(palette.Off);
        if (palette.Rounded)
        {
            DrawRounded(context.Graphics, r.Left, y, cell, h, lit, onBrush, offBrush);
            return;
        }
        for (int i = 0; i < Segments; i++)
            context.Graphics.FillRectangle(i < lit ? onBrush : offBrush, r.Left + i * (cell + 1), y, cell, h);
    }

    // Bridge: the cells as one pill-shaped track whose two ends are rounded
    private static void DrawRounded(Graphics g, int left, int y, int cell, int h, int lit, Brush on, Brush off)
    {
        System.Drawing.Drawing2D.SmoothingMode mode = g.SmoothingMode;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        for (int i = 0; i < Segments; i++)
        {
            Brush brush = i < lit ? on : off;
            int x = left + i * (cell + 1);
            if (i == 0 || i == Segments - 1)
            {
                using var path = new System.Drawing.Drawing2D.GraphicsPath();
                if (i == 0)
                {
                    path.AddArc(x, y, h, h, 90, 180);
                    path.AddLine(x + h / 2f, y, x + cell, y);
                    path.AddLine(x + cell, y + h, x + h / 2f, y + h);
                }
                else
                {
                    path.AddLine(x, y, x + cell - h / 2f, y);
                    path.AddArc(x + cell - h, y, h, h, 270, 180);
                    path.AddLine(x + cell - h / 2f, y + h, x, y + h);
                }
                path.CloseFigure();
                g.FillPath(brush, path);
            }
            else
                g.FillRectangle(brush, x, y, cell, h);
        }
        g.SmoothingMode = mode;
    }

    public static bool IsPercent(SensorType type) => type is SensorType.Load or SensorType.Control or SensorType.Level or SensorType.Humidity;
}

/// <summary>The color the node icons are tinted with, and the floor added to dark parts.</summary>
public sealed class IconTint
{
    public Color Color { get; set; }
    public float OffsetR { get; set; }
    public float OffsetG { get; set; }
    public float OffsetB { get; set; }

    public static readonly IconTint SciFi = new() { Color = SciFiColors.Accent, OffsetR = 0.02f, OffsetG = 0.06f, OffsetB = 0.07f };
}

/// <summary>Tints the colorful Classic node icons into single-color glyphs of a design and back.</summary>
public static class SciFiIcons
{
    private static readonly Dictionary<(Image, IconTint), Image> Tinted = new();
    private static readonly Dictionary<Image, Image> Original = new();

    public static void Apply(Node node, bool sciFi) => Apply(node, sciFi ? IconTint.SciFi : null);

    /// <summary>Tints all icons below the node; null restores the Classic icons.</summary>
    public static void Apply(Node node, IconTint tint)
    {
        if (node.Image != null)
        {
            Image source = Original.TryGetValue(node.Image, out Image original) ? original : node.Image;
            node.Image = tint == null ? source : Tint(source, tint);
        }

        foreach (Node child in node.Nodes)
            Apply(child, tint);
    }

    private static Image Tint(Image source, IconTint tint)
    {
        if (Tinted.TryGetValue((source, tint), out Image tinted))
            return tinted;

        var bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            // keep the shape and its brightness, but in shades of the accent color (dark parts stay dark)
            float ar = tint.Color.R / 255f * 1.5f, ag = tint.Color.G / 255f * 1.5f, ab = tint.Color.B / 255f * 1.5f;
            var matrix = new ColorMatrix(new[]
            {
                new[] { 0.30f * ar, 0.30f * ag, 0.30f * ab, 0f, 0f },
                new[] { 0.59f * ar, 0.59f * ag, 0.59f * ab, 0f, 0f },
                new[] { 0.11f * ar, 0.11f * ag, 0.11f * ab, 0f, 0f },
                new[] { 0f, 0f, 0f, 1f, 0f },
                new[] { tint.OffsetR, tint.OffsetG, tint.OffsetB, 0f, 1f }
            });
            using var attributes = new ImageAttributes();
            attributes.SetColorMatrix(matrix);
            g.DrawImage(source, new Rectangle(0, 0, source.Width, source.Height), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attributes);
        }

        Tinted[(source, tint)] = bitmap;
        Original[bitmap] = source;
        return bitmap;
    }
}
