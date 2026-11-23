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

/// <summary>
/// Segmented meter in front of the value for load-like sensors (Load, Control, Level, Humidity).
/// Takes no space at all while the Sci-Fi design is off, so Classic is unchanged.
/// </summary>
public sealed class SciFiBarControl : NodeControl
{
    private const int Segments = 10;

    public static bool Active { get; set; }

    public int BarWidth { get; set; } = 46;

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
        int h = 6;
        int y = r.Top + (r.Height - h) / 2;
        int w = BarWidth - 8;
        int cell = (w - (Segments - 1)) / Segments;
        int lit = value.HasValue ? (int)Math.Round(Math.Max(0, Math.Min(100, value.Value)) / 100 * Segments) : 0;
        Color on = value >= 90 ? SciFiColors.Alarm : value >= 70 ? SciFiColors.Cool : SciFiColors.Accent;

        using var onBrush = new SolidBrush(on);
        using var offBrush = new SolidBrush(ColorTranslator.FromHtml("#33536A"));
        for (int i = 0; i < Segments; i++)
            context.Graphics.FillRectangle(i < lit ? onBrush : offBrush, r.Left + i * (cell + 1), y, cell, h);
    }

    public static bool IsPercent(SensorType type) => type is SensorType.Load or SensorType.Control or SensorType.Level or SensorType.Humidity;
}

/// <summary>Tints the colorful Classic node icons into single-color Sci-Fi glyphs and back.</summary>
public static class SciFiIcons
{
    private static readonly Dictionary<Image, Image> Tinted = new();
    private static readonly Dictionary<Image, Image> Original = new();

    public static void Apply(Node node, bool sciFi)
    {
        if (node.Image != null)
        {
            if (sciFi && !Original.ContainsKey(node.Image))
                node.Image = Tint(node.Image);
            else if (!sciFi && Original.TryGetValue(node.Image, out Image original))
                node.Image = original;
        }

        foreach (Node child in node.Nodes)
            Apply(child, sciFi);
    }

    private static Image Tint(Image source)
    {
        if (Tinted.TryGetValue(source, out Image tinted))
            return tinted;

        var bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            // keep the shape and its brightness, but in shades of the accent color (dark parts stay dark)
            float ar = SciFiColors.Accent.R / 255f * 1.5f, ag = SciFiColors.Accent.G / 255f * 1.5f, ab = SciFiColors.Accent.B / 255f * 1.5f;
            var matrix = new ColorMatrix(new[]
            {
                new[] { 0.30f * ar, 0.30f * ag, 0.30f * ab, 0f, 0f },
                new[] { 0.59f * ar, 0.59f * ag, 0.59f * ab, 0f, 0f },
                new[] { 0.11f * ar, 0.11f * ag, 0.11f * ab, 0f, 0f },
                new[] { 0f, 0f, 0f, 1f, 0f },
                new[] { 0.02f, 0.06f, 0.07f, 0f, 1f }
            });
            using var attributes = new ImageAttributes();
            attributes.SetColorMatrix(matrix);
            g.DrawImage(source, new Rectangle(0, 0, source.Width, source.Height), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attributes);
        }

        Tinted[source] = bitmap;
        Original[bitmap] = source;
        return bitmap;
    }
}
