// K26K Hardware Monitor - Bridge theme: flat color blocks, rounded elbows and pills on black.
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

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Aga.Controls.Tree;
using LibreHardwareMonitor.Windows.Forms.UI.Design;

namespace LibreHardwareMonitor.Windows.Forms.UI.Themes
{
    /// <summary>The Bridge palette, shared by the theme, the tree and the Skia-drawn frame.</summary>
    public static class BridgeColors
    {
        public static readonly Color Background = ColorTranslator.FromHtml("#000000");
        public static readonly Color Panel = ColorTranslator.FromHtml("#0B0B12");
        public static readonly Color PanelRaised = ColorTranslator.FromHtml("#16141C");
        public static readonly Color Text = ColorTranslator.FromHtml("#F2E6D8");
        public static readonly Color Dim = ColorTranslator.FromHtml("#B5AAA0");
        public static readonly Color Accent = ColorTranslator.FromHtml("#FF9C3A");
        public static readonly Color Peach = ColorTranslator.FromHtml("#FFC890");
        public static readonly Color Lilac = ColorTranslator.FromHtml("#C79BD6");
        public static readonly Color Blue = ColorTranslator.FromHtml("#8FA8FF");
        public static readonly Color Tan = ColorTranslator.FromHtml("#D9A877");
        public static readonly Color Data = ColorTranslator.FromHtml("#FFE08A");
        public static readonly Color Alarm = ColorTranslator.FromHtml("#FF5C4D");
        public static readonly Color Rule = ColorTranslator.FromHtml("#2A2420");
        public static readonly Color RuleStrong = ColorTranslator.FromHtml("#4A3E34");

        /// <summary>The colors of the menu pills and frame blocks, in their order.</summary>
        public static readonly Color[] Blocks = { Peach, Lilac, Accent, Blue, Tan };
    }

    /// <summary>A theme that draws the menu items itself (the Bridge pills).</summary>
    public interface IMenuItemPainter
    {
        /// <summary>Paints the background of a menu item; false lets the default renderer do it.</summary>
        bool PaintMenuItemBackground(ToolStripItemRenderEventArgs e);

        /// <summary>Text color of a menu item, or null for the theme's default.</summary>
        Color? MenuItemTextColor(ToolStripItem item);
    }

    /// <summary>
    /// Theme used by the Bridge design. Like Sci-Fi it is not listed in the Classic theme menu;
    /// the design switch sets it when the Bridge design is active.
    /// </summary>
    public class BridgeTheme : Theme, IMenuItemPainter
    {
        private static readonly Color[] Palette =
        {
            BridgeColors.Accent,
            ColorTranslator.FromHtml("#E0B8EE"),
            BridgeColors.Blue,
            BridgeColors.Peach,
            BridgeColors.Data,
            BridgeColors.Tan,
            ColorTranslator.FromHtml("#FF7A9A"),
            ColorTranslator.FromHtml("#9EE0C8"),
            ColorTranslator.FromHtml("#E0B0FF"),
            ColorTranslator.FromHtml("#FFB060"),
            ColorTranslator.FromHtml("#A8C8FF"),
            ColorTranslator.FromHtml("#E8D8C8")
        };

        private static Font _headerFont;
        private static Font HeaderFont => _headerFont ??= FontManager.CreateBridgeFont(10.5f, FontStyle.Bold);

        public BridgeTheme() : base("bridge", "Bridge") { }

        public override bool ListedInThemeMenu => false;
        // OxyPlot draws with GDI+, which cannot use the embedded Antonio; Bahnschrift (part of Windows 10 1709
        // and later) is the closest condensed face, with Consolas as the fallback
        public override string PlotFontFamily => PlotFont.Value;

        private static readonly System.Lazy<string> PlotFont = new(() =>
        {
            try
            {
                using var family = new FontFamily("Bahnschrift SemiCondensed");
                return family.Name;
            }
            catch (System.ArgumentException)
            {
                return "Consolas";
            }
        });
        public override double PlotStackGap => 0.045;
        public override string PlotEmptyHint => "SELECT SENSORS IN THE GRID TO PLOT THEM";
        public override bool PlotUppercaseTitles => true;
        public override string PlotTimeFormat => "h:mm:ss";
        public override bool PlotAxisHeads => true;
        public override bool PlotShowLegend => true;
        public override bool PlotDashedSeparators => false;
        public override double PlotFontSize => 12.5;
        public override double PlotMinStrokeThickness => 2;
        public override bool PlotVaryLineStyles => true;
        public override bool PlotEndMarkers => true;
        public override bool PlotMinimumSpans => true;
        public override bool PlotLegendBorder => false;
        public override Color PlotLegendBackgroundColor => ColorTranslator.FromHtml("#1E1A17");

        /// <summary>Indent of the VALUE header, so it stands above the numbers and not above the meters.</summary>
        public static int ValueHeaderIndent { get; set; }

        public override Color BackgroundColor => BridgeColors.Background;
        public override Color ForegroundColor => BridgeColors.Text;
        public override Color HyperlinkColor => BridgeColors.Accent;
        public override Color LineColor => BridgeColors.Rule;
        public override Color StrongLineColor => BridgeColors.RuleStrong;
        public override Color SelectedBackgroundColor => Color.FromArgb(60, 255, 156, 58);
        public override Color SelectedForegroundColor => BridgeColors.Text;

        public override Color MenuBackgroundColor => BridgeColors.Background;
        public override Color MenuBorderColor => BridgeColors.Accent;
        public override Color MenuForegroundColor => BridgeColors.Text;
        public override Color MenuSelectedBackgroundColor => BridgeColors.Accent;
        public override Color MenuSelectedForegroundColor => BridgeColors.Background;

        public override Color PlotBackgroundColor => BridgeColors.Panel;
        public override Color PlotBorderColor => BridgeColors.RuleStrong;
        public override Color[] PlotColorPalette => Palette;
        public override Color PlotGridMajorColor => ColorTranslator.FromHtml("#3A3029");
        public override Color PlotGridMinorColor => ColorTranslator.FromHtml("#1A1714");
        public override Color PlotTextColor => ColorTranslator.FromHtml("#C8B8A8");

        // a dark groove along the whole tree, so the orange thumb reads as part of a scrollbar
        public override Color ScrollbarBackground => ColorTranslator.FromHtml("#2E2822");
        public override Color ScrollbarTrack => BridgeColors.Accent;
        public override int ScrollbarThumbInset => 1;
        public override int ScrollbarThumbMinLength => 36;

        public override Color SplitterColor => BridgeColors.Background;
        public override Color SplitterHoverColor => Color.FromArgb(255, 60, 40, 24);

        public override Color TreeBackgroundColor => BridgeColors.Panel;
        public override Color TreeOutlineColor => BridgeColors.Dim;
        public override Color TreeSelectedBackgroundColor => Color.FromArgb(255, 58, 38, 20);
        public override Color TreeTextColor => BridgeColors.Text;
        public override Color TreeSelectedTextColor => BridgeColors.Text;
        public override Color TreeRowSepearatorColor => BridgeColors.Rule;

        public override Color WindowTitlebarBackgroundColor => BridgeColors.Background;
        public override Color WindowTitlebarForegroundColor => BridgeColors.Text;
        public override bool WindowTitlebarFallbackToImmersiveDarkMode => true;

        // ---------- menu: the items of the menu row are colored pills ----------

        private static bool IsTopLevel(ToolStripItem item) => !item.IsOnDropDown && item.Owner is MenuStrip;

        private static Color PillColor(ToolStripItem item)
        {
            int index = item.Owner?.Items.IndexOf(item) ?? 0;
            return BridgeColors.Blocks[System.Math.Max(0, index) % BridgeColors.Blocks.Length];
        }

        public bool PaintMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!IsTopLevel(e.Item))
                return false;

            Graphics g = e.Graphics;
            var bounds = new Rectangle(Point.Empty, e.Item.Size);
            using (var back = new SolidBrush(BridgeColors.Background))
                g.FillRectangle(back, bounds);

            Color color = PillColor(e.Item);
            if (e.Item.Pressed)
                color = Mix(color, Color.White, 0.45f);
            else if (e.Item.Selected)
                color = Mix(color, Color.White, 0.25f);

            int inset = System.Math.Max(4, bounds.Height / 6);
            var pill = new Rectangle(bounds.Left + 2, bounds.Top + inset, bounds.Width - 4, bounds.Height - 2 * inset);
            SmoothingMode old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = Pill(pill))
            using (var brush = new SolidBrush(color))
                g.FillPath(brush, path);
            g.SmoothingMode = old;
            return true;
        }

        public Color? MenuItemTextColor(ToolStripItem item) => IsTopLevel(item) ? BridgeColors.Background : null;

        internal static GraphicsPath Pill(RectangleF r)
        {
            var path = new GraphicsPath();
            float d = System.Math.Min(r.Height, r.Width);
            path.AddArc(r.Left, r.Top, d, d, 90, 180);
            path.AddArc(r.Right - d, r.Top, d, d, 270, 180);
            path.CloseFigure();
            return path;
        }

        internal static Color Mix(Color a, Color b, float t) =>
            Color.FromArgb(a.A, (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));

        // ---------- tree ----------

        protected override void InstallTreeRenderers()
        {
            // a small rounded block that points right (closed) or down (open)
            TreeViewAdv.CustomPlusMinusRenderFunc = (g, rect, isExpanded) =>
            {
                int cx = rect.Left + 5;
                int cy = rect.Top + rect.Height / 2;
                SmoothingMode old = g.SmoothingMode;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using var brush = new SolidBrush(isExpanded ? BridgeColors.Accent : BridgeColors.Tan);
                PointF[] shape = isExpanded
                    ? new[] { new PointF(cx - 4.5f, cy - 2.5f), new PointF(cx + 4.5f, cy - 2.5f), new PointF(cx, cy + 3f) }
                    : new[] { new PointF(cx - 2f, cy - 4.5f), new PointF(cx + 3.5f, cy), new PointF(cx - 2f, cy + 4.5f) };
                g.FillPolygon(brush, shape);
                g.SmoothingMode = old;
            };

            // plotted sensors: a filled pill; others: an outlined pill
            TreeViewAdv.CustomCheckRenderFunc = (g, rect, isChecked) =>
            {
                var r = new RectangleF(rect.Left - 1, rect.Top + 2, 15, 10);
                using (var back = new SolidBrush(BridgeColors.Panel))
                    g.FillRectangle(back, r.Left - 1, r.Top - 2, r.Width + 3, r.Height + 4);
                SmoothingMode old = g.SmoothingMode;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = Pill(r))
                {
                    if (isChecked)
                    {
                        using var fill = new SolidBrush(BridgeColors.Accent);
                        g.FillPath(fill, path);
                    }
                    else
                    {
                        using var pen = new Pen(ColorTranslator.FromHtml("#8A7A6C"), 1.2f);
                        g.DrawPath(pen, path);
                    }
                }
                g.SmoothingMode = old;
            };

            TreeViewAdv.CustomColumnBackgroundRenderFunc = (g, rect, isPressed, isHot) =>
            {
                using (var back = new SolidBrush(BridgeColors.Background))
                    g.FillRectangle(back, rect);
                // every column head is a block of its own with a gap to the next one
                var block = new Rectangle(rect.Left, rect.Top + 2, rect.Width - 3, rect.Height - 4);
                Color color = isHot ? Mix(BridgeColors.Lilac, Color.White, 0.2f) : BridgeColors.Lilac;
                using (var fill = new SolidBrush(color))
                    g.FillRectangle(fill, block);
            };

            TreeViewAdv.CustomColumnTextRenderFunc = (g, rect, font, text) =>
            {
                Font header = HeaderFont;
                int indent = string.Equals(text, "Value", System.StringComparison.OrdinalIgnoreCase) ? ValueHeaderIndent : 0;
                // the name column is labeled at its left, the number columns at their right end (above the numbers)
                bool names = string.Equals(text, "Sensor", System.StringComparison.OrdinalIgnoreCase);
                var r = new Rectangle(rect.Left + (names ? 10 : 4 + indent), rect.Top, rect.Width - 10 - (names ? 0 : indent), rect.Height);
                TextRenderer.DrawText(g, text?.ToUpperInvariant() ?? string.Empty, header, r, BridgeColors.Background,
                                      (names ? TextFormatFlags.Left : TextFormatFlags.Right) | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            };

            TreeViewAdv.CustomHorizontalLinePen = new Pen(BridgeColors.Rule);
            TreeViewAdv.CustomSelectedRowBrush = new SolidBrush(TreeSelectedBackgroundColor);
            TreeViewAdv.CustomSelectedTextColor = BridgeColors.Text;
        }
    }
}
