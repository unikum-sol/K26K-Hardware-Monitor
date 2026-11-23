// K26K Hardware Monitor - Sci-Fi theme (colors in the spirit of the "Patch-7" FUI scene).
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
    /// <summary>The Sci-Fi palette, shared by the theme and the Skia-drawn HUD.</summary>
    public static class SciFiColors
    {
        public static readonly Color Background = ColorTranslator.FromHtml("#050810");
        public static readonly Color Panel = ColorTranslator.FromHtml("#0A1218");
        public static readonly Color PanelRaised = ColorTranslator.FromHtml("#0E1A22");
        public static readonly Color Text = ColorTranslator.FromHtml("#D8E8F0");
        public static readonly Color Dim = ColorTranslator.FromHtml("#8CA3B3");
        public static readonly Color Muted = ColorTranslator.FromHtml("#6E8798");
        public static readonly Color Accent = ColorTranslator.FromHtml("#00E0E8");
        public static readonly Color Data = ColorTranslator.FromHtml("#F0E040");
        public static readonly Color Cool = ColorTranslator.FromHtml("#F0A030");
        public static readonly Color Alarm = ColorTranslator.FromHtml("#FF40A0");
        public static readonly Color Rule = ColorTranslator.FromHtml("#1C2C3A");
        public static readonly Color RuleStrong = ColorTranslator.FromHtml("#2C4A5C");
    }

    /// <summary>
    /// Theme used by the Sci-Fi design. It is not listed in the Classic theme menu;
    /// the design manager switches to it when the Sci-Fi design is active.
    /// </summary>
    public class SciFiTheme : Theme
    {
        private static readonly Color[] Palette =
        {
            SciFiColors.Accent,
            SciFiColors.Data,
            SciFiColors.Cool,
            SciFiColors.Alarm,
            ColorTranslator.FromHtml("#7AF0A8"),
            ColorTranslator.FromHtml("#8FA8FF"),
            ColorTranslator.FromHtml("#00A8B0"),
            ColorTranslator.FromHtml("#C8B830"),
            ColorTranslator.FromHtml("#D07AFF"),
            ColorTranslator.FromHtml("#FF8060"),
            ColorTranslator.FromHtml("#60D0FF"),
            ColorTranslator.FromHtml("#B0C8D0")
        };

        private static Font _headerFont;
        private static Font HeaderFont => _headerFont ??= FontManager.CreateFont(8.5f, FontStyle.Bold);

        public SciFiTheme() : base("scifi", "Sci-Fi") { }

        public override bool ListedInThemeMenu => false;
        public override string PlotFontFamily => "Consolas";
        public override double PlotStackGap => 0.045;
        public override string PlotEmptyHint => "SELECT SENSORS IN THE GRID TO PLOT THEM";
        public override bool PlotUppercaseTitles => true;
        public override string PlotTimeFormat => "h:mm:ss";
        public override bool PlotAxisHeads => true;
        public override bool PlotShowLegend => true;
        public override bool PlotDashedSeparators => true;
        public override double PlotFontSize => 12.5;
        public override double PlotMinStrokeThickness => 1.75;
        public override bool PlotVaryLineStyles => true;
        public override bool PlotEndMarkers => true;

        public override Color BackgroundColor => SciFiColors.Background;
        public override Color ForegroundColor => SciFiColors.Text;
        public override Color HyperlinkColor => SciFiColors.Accent;
        public override Color LineColor => SciFiColors.Rule;
        public override Color StrongLineColor => SciFiColors.RuleStrong;
        public override Color SelectedBackgroundColor => Color.FromArgb(38, 0, 224, 232);
        public override Color SelectedForegroundColor => SciFiColors.Text;

        public override Color MenuBackgroundColor => SciFiColors.Panel;
        public override Color MenuBorderColor => SciFiColors.RuleStrong;
        public override Color MenuSelectedBackgroundColor => Color.FromArgb(255, 9, 44, 52);
        public override Color MenuSelectedForegroundColor => SciFiColors.Accent;

        public override Color PlotBackgroundColor => SciFiColors.Panel;
        public override Color PlotBorderColor => SciFiColors.RuleStrong;
        public override Color[] PlotColorPalette => Palette;
        public override Color PlotGridMajorColor => ColorTranslator.FromHtml("#2E4A5C");
        public override Color PlotGridMinorColor => ColorTranslator.FromHtml("#16252F");
        public override Color PlotTextColor => ColorTranslator.FromHtml("#A8BCC8");

        public override Color ScrollbarBackground => SciFiColors.Background;
        public override Color ScrollbarTrack => SciFiColors.RuleStrong;

        public override Color SplitterColor => SciFiColors.Background;
        public override Color SplitterHoverColor => Color.FromArgb(255, 9, 44, 52);

        public override Color TreeBackgroundColor => SciFiColors.Panel;
        public override Color TreeOutlineColor => SciFiColors.Dim;
        public override Color TreeSelectedBackgroundColor => Color.FromArgb(255, 9, 40, 48);
        public override Color TreeTextColor => SciFiColors.Text;
        public override Color TreeSelectedTextColor => SciFiColors.Text;
        public override Color TreeRowSepearatorColor => SciFiColors.Rule;

        public override Color WindowTitlebarBackgroundColor => SciFiColors.Background;
        public override Color WindowTitlebarForegroundColor => SciFiColors.Text;
        public override bool WindowTitlebarFallbackToImmersiveDarkMode => true;

        protected override void InstallTreeRenderers()
        {
            // Chevron expander instead of the plus/minus box.
            TreeViewAdv.CustomPlusMinusRenderFunc = (g, rect, isExpanded) =>
            {
                int cx = rect.Left + 5;
                int cy = rect.Top + rect.Height / 2;
                using var pen = new Pen(isExpanded ? SciFiColors.Accent : SciFiColors.Dim, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                SmoothingMode old = g.SmoothingMode;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                if (isExpanded)
                    g.DrawLines(pen, new[] { new PointF(cx - 3.5f, cy - 1.5f), new PointF(cx, cy + 2f), new PointF(cx + 3.5f, cy - 1.5f) });
                else
                    g.DrawLines(pen, new[] { new PointF(cx - 1.5f, cy - 3.5f), new PointF(cx + 2f, cy), new PointF(cx - 1.5f, cy + 3.5f) });
                g.SmoothingMode = old;
            };

            // Square bracket checkbox with a filled core when the sensor is plotted.
            TreeViewAdv.CustomCheckRenderFunc = (g, rect, isChecked) =>
            {
                int x = rect.Left;
                int y = rect.Top + 1;
                const int size = 11;
                using (var back = new SolidBrush(SciFiColors.Panel))
                    g.FillRectangle(back, x - 1, y - 1, size + 3, size + 3);
                using (var pen = new Pen(isChecked ? SciFiColors.Accent : ColorTranslator.FromHtml("#5A7A8E")))
                    g.DrawRectangle(pen, x, y, size, size);
                if (isChecked)
                {
                    using var fill = new SolidBrush(SciFiColors.Accent);
                    g.FillRectangle(fill, x + 3, y + 3, size - 5, size - 5);
                }
            };

            TreeViewAdv.CustomColumnBackgroundRenderFunc = (g, rect, isPressed, isHot) =>
            {
                using (var brush = new SolidBrush(isHot ? SciFiColors.PanelRaised : SciFiColors.Background))
                    g.FillRectangle(brush, rect);
                using (var pen = new Pen(SciFiColors.RuleStrong))
                    g.DrawLine(pen, rect.Left, rect.Bottom - 1, rect.Right, rect.Bottom - 1);
                using (var tick = new Pen(SciFiColors.Accent))
                    g.DrawLine(tick, rect.Left, rect.Bottom - 1, rect.Left + 6, rect.Bottom - 1);
            };

            TreeViewAdv.CustomColumnTextRenderFunc = (g, rect, font, text) =>
            {
                Font header = HeaderFont;
                var r = new Rectangle(rect.Left + 4, rect.Top, rect.Width - 4, rect.Height);
                TextRenderer.DrawText(g, text?.ToUpperInvariant() ?? string.Empty, header, r, SciFiColors.Dim,
                                      TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            };

            TreeViewAdv.CustomHorizontalLinePen = new Pen(SciFiColors.Rule);
            TreeViewAdv.CustomSelectedRowBrush = new SolidBrush(TreeSelectedBackgroundColor);
            TreeViewAdv.CustomSelectedTextColor = SciFiColors.Text;
        }

    }
}
