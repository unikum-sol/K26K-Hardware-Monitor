// K26K Hardware Monitor - the Bridge design of the main window: frames, tree styling and chrome.
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
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Aga.Controls.Tree;
using Aga.Controls.Tree.NodeControls;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Windows.Forms.UI.Bridge;
using LibreHardwareMonitor.Windows.Forms.UI.Design;
using LibreHardwareMonitor.Windows.Forms.UI.SciFi;
using LibreHardwareMonitor.Windows.Forms.UI.Themes;

namespace LibreHardwareMonitor.Windows.Forms.UI;

public sealed partial class MainForm
{
    private BridgeHeader _bridgeHeader;
    private BridgeStatusBar _bridgeStatus;
    private Font _bridgeTreeFont;
    private Font _bridgeMenuFont;
    private Font _bridgeLabelFont;
    private Font _bridgeSmallFont;
    private Font _bridgeDeviceFont;
    private Font _bridgeValueFont;

    private static readonly MeterPalette BridgeMeters = new()
    {
        Normal = BridgeColors.Blue,
        Warm = BridgeColors.Accent,
        Hot = BridgeColors.Alarm,
        Off = ColorTranslator.FromHtml("#5E5248"),
        Rounded = true
    };

    private static readonly IconTint BridgeIcons = new() { Color = BridgeColors.Accent, OffsetR = 0.07f, OffsetG = 0.04f, OffsetB = 0.01f };

    private Font BridgeTreeFont => _bridgeTreeFont ??= FontManager.CreateBridgeFont(11.25f);
    private Font BridgeMenuFont => _bridgeMenuFont ??= FontManager.CreateBridgeFont(10.5f, FontStyle.Bold);
    private Font BridgeLabelFont => _bridgeLabelFont ??= FontManager.CreateBridgeFont(11f, FontStyle.Bold);
    private Font BridgeSmallFont => _bridgeSmallFont ??= FontManager.CreateBridgeFont(8f);
    private Font BridgeDeviceFont => _bridgeDeviceFont ??= FontManager.CreateBridgeFont(12f, FontStyle.Bold);
    private Font BridgeValueFont => _bridgeValueFont ??= FontManager.CreateBridgeFont(12f, FontStyle.Bold);
    private Font _bridgeMinMaxFont;
    private Font BridgeMinMaxFont => _bridgeMinMaxFont ??= FontManager.CreateBridgeFont(11.25f);

    // room for the color column on the left and the bars above and below the content
    private Padding BridgePanelPadding => Scale(new Padding(32, 30, 10, 20));

    private Padding Scale(Padding p)
    {
        float k = DeviceDpi / 96f;
        return new Padding((int)(p.Left * k), (int)(p.Top * k), (int)(p.Right * k), (int)(p.Bottom * k));
    }

    private void InitializeBridge()
    {
        nodeTextBoxText.DrawText += BridgeTree_DrawText;
        nodeTextBoxValue.DrawText += BridgeTree_DrawText;
        nodeTextBoxMin.DrawText += BridgeTree_DrawText;
        nodeTextBoxMax.DrawText += BridgeTree_DrawText;
        treeView.RowDraw += BridgeTree_RowDraw;
        splitContainer.Panel1.Paint += BridgePanel_Paint;
        splitContainer.Panel2.Paint += BridgePanel_Paint;
        splitContainer.Panel1.Resize += delegate { SnapBridgeTree(); };
        splitContainer.Panel2.Resize += delegate { SnapBridgeTree(); };
    }

    // The bottom padding of the tree's panel grows by the remainder, so the tree shows whole rows only.
    private void SnapBridgeTree()
    {
        if (_design != UiDesign.Bridge || _fancyActive)
            return;
        Panel panel = treeView.Parent as Panel;
        if (panel == null)
            return;
        Padding pad = BridgePanelPadding;
        int available = panel.ClientSize.Height - pad.Top - pad.Bottom;
        int header = treeView.UseColumns ? treeView.ColumnHeaderHeight : 0;
        int row = Math.Max(1, treeView.RowHeight);
        if (available <= header + row)
            return;
        int rest = (available - header) % row;
        var snapped = new Padding(pad.Left, pad.Top, pad.Right, pad.Bottom + rest);
        if (panel.Padding != snapped)
            panel.Padding = snapped;
    }

    private void CreateBridgeChrome()
    {
        if (_bridgeHeader != null)
            return;
        _bridgeHeader = new BridgeHeader();
        _bridgeHeader.FancyRequested += delegate { EnterFancy(); };
        _bridgeStatus = new BridgeStatusBar();
        AttachFrameHitTests(_bridgeHeader, _bridgeHeader.IsOnButton, _bridgeStatus);
    }

    // ---------- tree ----------

    private void BridgeTree_DrawText(object sender, DrawEventArgs e)
    {
        if (_design != UiDesign.Bridge || !(e.Node.Tag is Node node) || !node.IsVisible)
            return;

        if (node is SensorNode sensorNode)
        {
            ISensor sensor = sensorNode.Sensor;
            float? v = sensor.Value;
            bool dead = !v.HasValue || v.Value == 0 && (sensor.Max ?? 0) == 0;
            if (sender == nodeTextBoxValue)
            {
                bool hot = sensor.SensorType == SensorType.Temperature && v >= 85 || SciFiBarControl.IsPercent(sensor.SensorType) && v >= 95;
                bool warm = sensor.SensorType == SensorType.Temperature && v >= 75 || SciFiBarControl.IsPercent(sensor.SensorType) && v >= 80;
                if (dead)
                {
                    e.TextColor = BridgeColors.Dim;
                    e.Text = "n/a";
                }
                else if (hot)
                {
                    // a lighter red for text: calmer when many rows are hot at once
                    e.TextColor = ColorTranslator.FromHtml("#FF7A6B");
                    e.Text = "▲ " + e.Text;
                }
                else if (warm)
                    e.TextColor = BridgeColors.Accent;
                else
                    e.TextColor = BridgeValueColor(sensor.SensorType);
                // live readings in bold, so the eye finds them first (the column is right aligned)
                e.Font = BridgeValueFont;
            }
            else if (sender == nodeTextBoxMin || sender == nodeTextBoxMax)
            {
                e.TextColor = dead ? BridgeColors.Dim : ColorTranslator.FromHtml("#D2C6BA");
                e.Font = BridgeMinMaxFont;
                if (dead)
                    e.Text = "-";
            }
            else if (sender == nodeTextBoxText && dead)
            {
                e.TextColor = BridgeColors.Dim;
            }
        }
        else if (node is HardwareNode && sender == nodeTextBoxText)
        {
            // devices: large orange caps on a dark band (the band and the count block come from the row painter)
            e.TextColor = BridgeColors.Accent;
            e.Font = BridgeDeviceFont;
            e.Text = e.Text.ToUpperInvariant();
        }
        else if (node is TypeNode && sender == nodeTextBoxText)
        {
            e.TextColor = BridgeColors.Lilac;
            e.Font = BridgeLabelFont;
            e.Text = e.Text.ToUpperInvariant();
        }
    }

    private static Color BridgeValueColor(SensorType type)
    {
        return type switch
        {
            SensorType.Temperature => BridgeColors.Data,
            SensorType.Load or SensorType.Control or SensorType.Level or SensorType.Humidity => BridgeColors.Blue,
            SensorType.Power or SensorType.Voltage or SensorType.Current or SensorType.Energy => BridgeColors.Peach,
            SensorType.Fan or SensorType.Flow => BridgeColors.Lilac,
            _ => BridgeColors.Text
        };
    }

    // Device rows: a dark band with a rounded orange cap on the left and a block with the channel count on the right.
    // Group rows: a lilac pill with the number of sensors at the right end. Rows inside a device get a tan rail.
    private void BridgeTree_RowDraw(object sender, TreeViewRowDrawEventArgs e)
    {
        if (_design != UiDesign.Bridge)
            return;

        Graphics g = e.Graphics;
        var r = new Rectangle(0, e.RowRect.Top, treeView.ClientSize.Width + Math.Abs(treeView.OffsetX), e.RowRect.Height);
        int right = treeView.ClientSize.Width - 6;
        SmoothingMode mode = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        try
        {
            if (e.Node.Tag is HardwareNode hardware)
            {
                using (var band = new SolidBrush(ColorTranslator.FromHtml("#22160C")))
                    g.FillRectangle(band, r.Left, r.Top + 1, r.Width, r.Height - 2);
                using (GraphicsPath cap = BridgeGdi.Block(new RectangleF(r.Left + 1, r.Top + 3, 14, r.Height - 6), true, false))
                using (var accent = new SolidBrush(BridgeColors.Accent))
                    g.FillPath(accent, cap);

                int channels = CountSensors(hardware);
                if (channels > 0)
                {
                    string text = channels.ToString(CultureInfo.InvariantCulture) + " CH";
                    Size size = TextRenderer.MeasureText(text, BridgeLabelFont, Size.Empty, TextFormatFlags.NoPadding);
                    var block = new RectangleF(right - size.Width - 22, r.Top + 3, size.Width + 22, r.Height - 6);
                    using (GraphicsPath path = BridgeGdi.Block(block, false, true))
                    using (var fill = new SolidBrush(BridgeColors.Peach))
                        g.FillPath(fill, path);
                    TextRenderer.DrawText(g, text, BridgeLabelFont, Rectangle.Round(new RectangleF(block.Left, block.Top, block.Width - 10, block.Height)),
                                          BridgeColors.Background, TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | RowTextFlags);
                }
                return;
            }

            bool insideDevice = false;
            for (TreeNodeAdv p = e.Node.Parent; p != null; p = p.Parent)
            {
                if (p.Tag is HardwareNode)
                {
                    insideDevice = true;
                    break;
                }
            }
            if (!insideDevice)
                return;

            using (var rail = new SolidBrush(BridgeColors.Tan))
                g.FillRectangle(rail, r.Left + 1, r.Top, 4, r.Height);

            if (e.Node.Tag is TypeNode type)
            {
                int count = type.Nodes.Count(n => n.IsVisible);
                string text = count.ToString("00", CultureInfo.InvariantCulture);
                Size size = TextRenderer.MeasureText(text, BridgeLabelFont, Size.Empty, TextFormatFlags.NoPadding);
                var pill = new RectangleF(right - size.Width - 18, r.Top + 4, size.Width + 18, r.Height - 8);
                using (GraphicsPath path = BridgeTheme.Pill(pill))
                using (var fill = new SolidBrush(BridgeColors.Lilac))
                    g.FillPath(fill, path);
                TextRenderer.DrawText(g, text, BridgeLabelFont, Rectangle.Round(pill), BridgeColors.Background,
                                      TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | RowTextFlags);
                // a thin lilac line from the label to the pill
                using var line = new Pen(Color.FromArgb(110, BridgeColors.Lilac));
                g.DrawLine(line, e.RowRect.Left + 8, r.Bottom - 2, pill.Left - 6, r.Bottom - 2);
            }
        }
        finally
        {
            g.SmoothingMode = mode;
        }
    }

    // the tree paints rows with a translated Graphics; GDI text keeps that offset and the clip only with these flags
    private const TextFormatFlags RowTextFlags = TextFormatFlags.PreserveGraphicsTranslateTransform | TextFormatFlags.PreserveGraphicsClipping;

    // ---------- frames around the tree and the plot ----------

    private void BridgePanel_Paint(object sender, PaintEventArgs e)
    {
        if (_design != UiDesign.Bridge || !(sender is Panel panel) || panel.Controls.Count == 0)
            return;

        Control content = panel.Controls[0];
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float k = DeviceDpi / 96f;

        float left = 4 * k, top = 5 * k, bottom = panel.ClientSize.Height - 4 * k, right = panel.ClientSize.Width - 4 * k;
        float arm = 20 * k, bar = 11 * k, gap = 4 * k;
        bool tree = content == treeView;

        // top elbow with the section label in a gap of the bar
        var topElbow = new RectangleF(left, top, 96 * k, 48 * k);
        Fill(g, BridgeGdi.Elbow(topElbow, arm, bar, Corner.TopLeft, 24 * k, 9 * k), tree ? BridgeColors.Accent : BridgeColors.Lilac);

        string label = tree ? "SENSOR GRID" : "TELEMETRY PLOT";
        string meta = tree ? _metrics.Current.SensorCount.ToString(CultureInfo.InvariantCulture) + " CHANNELS LIVE" : (_plotPanel.StackedAxes ? "STACKED AXES" : "OVERLAID AXES");
        Size ls = TextRenderer.MeasureText(label, BridgeLabelFont, Size.Empty, TextFormatFlags.NoPadding);
        Size ms = TextRenderer.MeasureText(meta, BridgeLabelFont, Size.Empty, TextFormatFlags.NoPadding);
        float textY = top + bar / 2 - ls.Height / 2f;
        float labelX = topElbow.Right + 8 * k;
        TextRenderer.DrawText(g, label, BridgeLabelFont, new Point((int)labelX, (int)textY), tree ? BridgeColors.Accent : BridgeColors.Lilac, TextFormatFlags.NoPadding);

        float capW = 22 * k;
        float metaRight = right - capW - 8 * k;
        float metaX = metaRight - ms.Width;
        float barFrom = labelX + ls.Width + 8 * k, barTo = metaX - 8 * k;
        if (barTo - barFrom > 10 * k)
            Fill(g, BridgeGdi.Block(new RectangleF(barFrom, top, barTo - barFrom, bar), false, false), tree ? BridgeColors.Peach : BridgeColors.Blue);
        if (metaX > labelX + ls.Width + 8 * k)
            TextRenderer.DrawText(g, meta, BridgeLabelFont, new Point((int)metaX, (int)textY), BridgeColors.Peach, TextFormatFlags.NoPadding);
        Fill(g, BridgeGdi.Block(new RectangleF(right - capW, top, capW, bar), false, true), tree ? BridgeColors.Accent : BridgeColors.Lilac);

        // the frame ends in a bottom elbow only, so the bottom edge stays calm above the status strip
        float bottomBar = 7 * k;
        var bottomElbow = new RectangleF(left, bottom - 34 * k, 76 * k, 34 * k);
        Fill(g, BridgeGdi.Elbow(bottomElbow, arm, bottomBar, Corner.BottomLeft, 18 * k, 7 * k), BridgeColors.Tan);

        // the column between the elbows: stacked blocks with small codes
        float colTop = topElbow.Bottom + gap, colBottom = bottomElbow.Top - gap;
        if (colBottom - colTop > 30 * k)
        {
            float[] shares = { 0.46f, 0.18f, 0.36f };
            Color[] colors = tree ? new[] { BridgeColors.Tan, BridgeColors.Blue, BridgeColors.Peach } : new[] { BridgeColors.Peach, BridgeColors.Accent, BridgeColors.Tan };
            float y = colTop, h = colBottom - colTop - gap * (shares.Length - 1);
            for (int i = 0; i < shares.Length; i++)
            {
                float bh = h * shares[i];
                var block = new RectangleF(left, y, arm, bh);
                using (var brush = new SolidBrush(colors[i]))
                    g.FillRectangle(brush, block);
                y += bh + gap;
            }
        }
    }

    private static void Fill(Graphics g, GraphicsPath path, Color color)
    {
        using (path)
        using (var brush = new SolidBrush(color))
            g.FillPath(brush, path);
    }
}
