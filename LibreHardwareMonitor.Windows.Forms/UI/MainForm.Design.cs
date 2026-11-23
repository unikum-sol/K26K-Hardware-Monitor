// K26K Hardware Monitor - design switching (Classic, Sci-Fi, Fancy) for the main window.
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
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using Aga.Controls.Tree.NodeControls;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Windows.Forms.Telemetry;
using LibreHardwareMonitor.Windows.Forms.UI.Design;
using LibreHardwareMonitor.Windows.Forms.UI.Fancy;
using LibreHardwareMonitor.Windows.Forms.UI.SciFi;
using LibreHardwareMonitor.Windows.Forms.UI.Themes;

namespace LibreHardwareMonitor.Windows.Forms.UI;

public enum UiDesign
{
    Classic,
    SciFi
}

public sealed partial class MainForm
{
    private UiDesign _design = UiDesign.Classic;
    private bool _fancyActive;
    private bool _skiaAvailable;
    private SystemMetrics _metrics;
    private HudHeader _hudHeader;
    private HudStatusBar _hudStatus;
    private FancyDashboard _fancy;
    private ToolStripMenuItem _designClassicItem;
    private ToolStripMenuItem _designSciFiItem;
    private ToolStripMenuItem _fancyItem;
    private ToolStripMenuItem _reducedMotionItem;
    private ToolStripMenuItem _fancyFullscreenItem;
    private Font _classicTreeFont;
    private Font _classicMenuFont;
    private Font _sciFiTreeFont;
    private Font _sciFiMenuFont;
    private Font _sciFiLabelFont;
    private int _classicRowHeight;
    private Padding _classicPanel1Padding;
    private Padding _classicPanel2Padding;
    private FormBorderStyle _savedBorder;
    private FormWindowState _savedState;
    private bool _fullscreen;
    private int _dumpAfter = -1;
    private string _dumpPath;
    private int _updates;
    private SciFiBarControl _barControl;
    private bool _valueColumnWidened;
    private int MeterRoom => _barControl.BarWidth + 6;
    private readonly System.Collections.Generic.Dictionary<ToolStripItem, string> _classicMenuTexts = new();

    /// <summary>The theme belongs to a design, so the Windows light/dark switch must not replace it.</summary>
    private bool DesignOwnsTheme => _design != UiDesign.Classic;

    private void InitializeDesign()
    {
        _metrics = new SystemMetrics();
        _classicTreeFont = treeView.Font;
        _classicMenuFont = mainMenu.Font;
        _classicRowHeight = treeView.RowHeight;
        _classicPanel1Padding = splitContainer.Panel1.Padding;
        _classicPanel2Padding = splitContainer.Panel2.Padding;
        _skiaAvailable = SkiaWorks();

        BuildDesignMenu();

        // segmented meters in front of the values (only visible in Sci-Fi)
        _barControl = new SciFiBarControl { ParentColumn = value, LeftMargin = 3 };
        treeView.NodeControls.Insert(treeView.NodeControls.IndexOf(nodeTextBoxValue), _barControl);

        nodeTextBoxText.DrawText += SciFiTree_DrawText;
        nodeTextBoxValue.DrawText += SciFiTree_DrawText;
        nodeTextBoxMin.DrawText += SciFiTree_DrawText;
        nodeTextBoxMax.DrawText += SciFiTree_DrawText;
        treeView.RowDraw += SciFiTree_RowDraw;
        backgroundUpdater.RunWorkerCompleted += delegate { UpdateDashboard(); };
        splitContainer.Panel1.Paint += SciFiPanel_Paint;
        splitContainer.Panel2.Paint += SciFiPanel_Paint;

        string saved = _settings.GetValue("design", "scifi");
        string[] args = Environment.GetCommandLineArgs();
        int designArg = Array.IndexOf(args, "--design");
        if (designArg >= 0 && designArg + 1 < args.Length)
            saved = args[designArg + 1];

        int dumpArg = Array.IndexOf(args, "--dump-snapshot");
        if (dumpArg >= 0 && dumpArg + 1 < args.Length)
        {
            _dumpPath = args[dumpArg + 1];
            int afterArg = Array.IndexOf(args, "--after");
            _dumpAfter = afterArg >= 0 && afterArg + 1 < args.Length && int.TryParse(args[afterArg + 1], out int n) ? n : 15;
        }

        _reducedMotionItem.Checked = _settings.GetValue("fancyReducedMotion", !ClientAreaAnimationsEnabled());
        _fancyFullscreenItem.Checked = _settings.GetValue("fancyFullscreen", false);

        if (!_skiaAvailable || saved == "classic")
        {
            ApplyDesign(UiDesign.Classic, false);
        }
        else
        {
            ApplyDesign(UiDesign.SciFi, false);
            if (saved == "fancy")
                EnterFancy();
        }
    }

    // Windows setting "Show animations in Windows"; Reduced Motion follows it until the user picks otherwise.
    private static bool ClientAreaAnimationsEnabled()
    {
        const uint SPI_GETCLIENTAREAANIMATION = 0x1042;
        bool enabled = true;
        try
        {
            SystemParametersInfo(SPI_GETCLIENTAREAANIMATION, 0, ref enabled, 0);
        }
        catch (Exception)
        {
            // keep animations on if the setting cannot be read
        }
        return enabled;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SystemParametersInfo(uint action, uint param, ref bool value, uint winIni);

    // SkiaSharp lives in its own method, so a missing native library is caught here and not at JIT time.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool SkiaWorks()
    {
        try
        {
            using var surface = SkiaSharp.SKSurface.Create(new SkiaSharp.SKImageInfo(4, 4));
            return surface != null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private void BuildDesignMenu()
    {
        var designMenu = new ToolStripMenuItem("Design");
        _designClassicItem = new ToolStripMenuItem("Classic");
        _designSciFiItem = new ToolStripMenuItem("Sci-Fi");
        _fancyItem = new ToolStripMenuItem("Fancy Mode") { ShortcutKeyDisplayString = "F11" };
        _reducedMotionItem = new ToolStripMenuItem("Reduced Motion");
        _fancyFullscreenItem = new ToolStripMenuItem("Fancy Mode in Fullscreen");

        _designClassicItem.Click += delegate { ApplyDesign(UiDesign.Classic, true); };
        _designSciFiItem.Click += delegate { ApplyDesign(UiDesign.SciFi, true); };
        _fancyItem.Click += delegate { ToggleFancy(); };
        _reducedMotionItem.Click += delegate
        {
            _reducedMotionItem.Checked = !_reducedMotionItem.Checked;
            _settings.SetValue("fancyReducedMotion", _reducedMotionItem.Checked);
            if (_fancy != null)
                _fancy.ReducedMotion = _reducedMotionItem.Checked;
        };
        _fancyFullscreenItem.Click += delegate
        {
            _fancyFullscreenItem.Checked = !_fancyFullscreenItem.Checked;
            _settings.SetValue("fancyFullscreen", _fancyFullscreenItem.Checked);
        };

        designMenu.DropDownItems.AddRange(new ToolStripItem[] { _designClassicItem, _designSciFiItem, new ToolStripSeparator(), _fancyItem, _reducedMotionItem, _fancyFullscreenItem });
        viewMenuItem.DropDownItems.Add(new ToolStripSeparator());
        viewMenuItem.DropDownItems.Add(designMenu);

        if (!_skiaAvailable)
        {
            _designSciFiItem.Enabled = false;
            _fancyItem.Enabled = false;
            _designSciFiItem.ToolTipText = _fancyItem.ToolTipText = "SkiaSharp could not be loaded.";
        }
    }

    private void ApplyDesign(UiDesign mode, bool save)
    {
        _design = mode;
        _designClassicItem.Checked = mode == UiDesign.Classic;
        _designSciFiItem.Checked = mode == UiDesign.SciFi;
        if (save)
            _settings.SetValue("design", mode == UiDesign.SciFi ? "scifi" : "classic");

        SuspendLayout();
        if (mode == UiDesign.SciFi)
        {
            _sciFiTreeFont ??= FontManager.CreateFont(9f);
            _sciFiMenuFont ??= FontManager.CreateFont(8.5f);
            _sciFiLabelFont ??= FontManager.CreateFont(8f, FontStyle.Bold);

            treeView.Font = _sciFiTreeFont;
            treeView.RowHeight = Math.Max(_classicRowHeight, _sciFiTreeFont.Height + 8);
            mainMenu.Font = _sciFiMenuFont;
            treeContextMenu.Font = _sciFiMenuFont;
            splitContainer.Panel1.Padding = new Padding(10, 26, 10, 10);
            splitContainer.Panel2.Padding = new Padding(10, 26, 10, 10);
            themeMenuItem.Enabled = false;
            SciFiBarControl.Active = true;
            // narrower name column; the tree hands the freed width to the value column (room for the meter)
            if (!_valueColumnWidened)
            {
                treeView.Columns[0].Width -= MeterRoom;
                _valueColumnWidened = true;
            }
            SciFiIcons.Apply(_root, true);
            foreach (ToolStripItem item in mainMenu.Items)
            {
                if (!_classicMenuTexts.ContainsKey(item))
                    _classicMenuTexts[item] = item.Text;
                item.Text = _classicMenuTexts[item].ToUpperInvariant();
            }

            if (_hudHeader == null)
            {
                _hudHeader = new HudHeader();
                _hudHeader.FancyRequested += delegate { EnterFancy(); };
                _hudStatus = new HudStatusBar();
            }

            if (!Controls.Contains(_hudHeader))
            {
                Controls.Add(_hudHeader);
                Controls.Add(_hudStatus);
            }
            ArrangeDocking();
            Theme.Current = new SciFiTheme();
            ApplyThemeToWindows();
            UpdateDashboard();
        }
        else
        {
            if (_hudHeader != null)
            {
                Controls.Remove(_hudHeader);
                Controls.Remove(_hudStatus);
            }

            treeView.Font = _classicTreeFont;
            treeView.RowHeight = _classicRowHeight;
            mainMenu.Font = _classicMenuFont;
            treeContextMenu.Font = _classicMenuFont;
            splitContainer.Panel1.Padding = _classicPanel1Padding;
            splitContainer.Panel2.Padding = _classicPanel2Padding;
            themeMenuItem.Enabled = true;
            SciFiBarControl.Active = false;
            if (_valueColumnWidened)
            {
                treeView.Columns[0].Width += MeterRoom;
                _valueColumnWidened = false;
            }
            SciFiIcons.Apply(_root, false);
            foreach (var pair in _classicMenuTexts)
                pair.Key.Text = pair.Value;
            RestoreClassicTheme();
            ApplyThemeToWindows();
        }
        ResumeLayout(true);

        PlotSelectionChanged(this, null);
        splitContainer.Panel1.Invalidate();
        splitContainer.Panel2.Invalidate();
        treeView.Invalidate();
    }

    // Dock order: menu at the very top, Sci-Fi header below it, status strip at the bottom, content fills the rest.
    private void ArrangeDocking()
    {
        Controls.SetChildIndex(splitContainer, 0);
        if (_hudHeader != null && Controls.Contains(_hudHeader))
        {
            Controls.SetChildIndex(_hudHeader, 1);
            Controls.SetChildIndex(_hudStatus, 2);
        }
        Controls.SetChildIndex(mainMenu, Controls.Count - 1);
    }

    // During start-up the main window is not in Application.OpenForms yet, so the theme is applied explicitly.
    private void ApplyThemeToWindows()
    {
        Theme.Current.Apply(this);
        if (_plotForm != null)
            Theme.Current.Apply(_plotForm);
    }

    private void RestoreClassicTheme()
    {
        string id = _settings.GetValue("theme", "auto");
        Theme theme = Theme.All.FirstOrDefault(x => x.Id == id);
        if (theme != null)
            Theme.Current = theme;
        else if (Theme.SupportsAutoThemeSwitching())
            Theme.SetAutoTheme();
        else
            Theme.Current = new LightTheme();
    }

    private void ToggleFancy()
    {
        if (_fancyActive)
            ExitFancy();
        else
            EnterFancy();
    }

    private void EnterFancy()
    {
        if (!_skiaAvailable || _fancyActive)
            return;

        if (_design != UiDesign.SciFi)
            ApplyDesign(UiDesign.SciFi, false);

        _fancyActive = true;
        _fancyItem.Checked = true;
        _settings.SetValue("design", "fancy");

        if (_fancy == null)
        {
            _fancy = new FancyDashboard();
            _fancy.ExitRequested += delegate { ExitFancy(); };
        }
        _fancy.ReducedMotion = _reducedMotionItem.Checked;

        SuspendLayout();
        mainMenu.Visible = false;
        splitContainer.Visible = false;
        _hudHeader.Visible = false;
        _hudStatus.Visible = false;
        if (!Controls.Contains(_fancy))
            Controls.Add(_fancy);
        _fancy.Visible = true;
        _fancy.BringToFront();
        ResumeLayout(true);

        if (_fancyFullscreenItem.Checked)
        {
            _savedBorder = FormBorderStyle;
            _savedState = WindowState;
            _fullscreen = true;
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Normal;
            WindowState = FormWindowState.Maximized;
        }

        UpdateDashboard();
        _fancy.Restart();
        _fancy.Focus();
    }

    private void ExitFancy()
    {
        if (!_fancyActive)
            return;

        _fancyActive = false;
        _fancyItem.Checked = false;
        _settings.SetValue("design", _design == UiDesign.SciFi ? "scifi" : "classic");

        if (_fullscreen)
        {
            _fullscreen = false;
            FormBorderStyle = _savedBorder;
            WindowState = _savedState;
        }

        SuspendLayout();
        _fancy.Visible = false;
        mainMenu.Visible = true;
        splitContainer.Visible = true;
        _hudHeader.Visible = true;
        _hudStatus.Visible = true;
        ArrangeDocking();
        ResumeLayout(true);
        treeView.Focus();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.F11 && _skiaAvailable)
        {
            ToggleFancy();
            return true;
        }

        if (keyData == Keys.Escape && _fancyActive)
        {
            ExitFancy();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void UpdateDashboard()
    {
        if (IsDisposed || _metrics == null)
            return;

        bool needed = _design == UiDesign.SciFi || _fancyActive || _dumpPath != null;
        if (!needed)
            return;

        // hardware can appear later (e.g. a USB device); give its icons the Sci-Fi look too
        if (_design == UiDesign.SciFi)
            SciFiIcons.Apply(_root, true);

        string ip = Server.ListenerIp;
        string web = "http://" + (ip == "+" || ip == "?" || string.IsNullOrEmpty(ip) ? "*" : ip) + ":" + Server.ListenerPort;
        DashboardModel model = _metrics.Update(_computer.Hardware, _root.Text, _unitManager.TemperatureUnit == TemperatureUnit.Fahrenheit,
                                               timer.Interval, web, _runWebServer?.Value == true);

        if (_hudHeader != null && _hudHeader.Visible)
        {
            _hudHeader.Model = model;
            _hudStatus.Model = model;
        }

        if (_fancyActive)
            _fancy.Model = model;

        _updates++;
        if (_dumpPath != null && _updates == _dumpAfter)
        {
            try
            {
                SnapshotJson.Save(model, _dumpPath);
            }
            catch (Exception)
            {
                // a failed dump must not disturb the application
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--exit-after-dump") >= 0)
                BeginInvoke(new Action(CloseApplication));
        }
    }

    // ---------- Sci-Fi tree: data colors and section frames ----------

    private void SciFiTree_DrawText(object sender, DrawEventArgs e)
    {
        if (_design != UiDesign.SciFi || !(e.Node.Tag is Node node) || !node.IsVisible)
            return;

        if (node is SensorNode sensorNode)
        {
            ISensor sensor = sensorNode.Sensor;
            float? v = sensor.Value;
            // a sensor that never reported anything (e.g. no driver access) is shown dimmed
            bool dead = !v.HasValue || v.Value == 0 && (sensor.Max ?? 0) == 0;
            if (sender == nodeTextBoxValue)
            {
                bool hot = sensor.SensorType == SensorType.Temperature && v >= 85 || SciFiBarControl.IsPercent(sensor.SensorType) && v >= 95;
                bool warm = sensor.SensorType == SensorType.Temperature && v >= 75 || SciFiBarControl.IsPercent(sensor.SensorType) && v >= 80;
                if (dead)
                {
                    e.TextColor = SciFiColors.Muted;
                    e.Text = "n/a";
                }
                else if (hot)
                {
                    e.TextColor = SciFiColors.Alarm;
                    e.Text = "▲ " + e.Text;
                }
                else if (warm)
                    e.TextColor = SciFiColors.Cool;
                else
                    e.TextColor = ValueColor(sensor.SensorType);
            }
            else if (sender == nodeTextBoxMin || sender == nodeTextBoxMax)
            {
                e.TextColor = dead ? SciFiColors.Muted : SciFiColors.Dim;
                if (dead)
                    e.Text = "-";
            }
            else if (sender == nodeTextBoxText && dead)
            {
                e.TextColor = SciFiColors.Dim;
            }
        }
        else if (node is HardwareNode && sender == nodeTextBoxText)
        {
            e.TextColor = SciFiColors.Accent;
            e.Text = e.Text.ToUpperInvariant();
        }
        else if (node is TypeNode && sender == nodeTextBoxText)
        {
            e.TextColor = SciFiColors.Dim;
            e.Text = e.Text.ToUpperInvariant() + "  //  " + node.Nodes.Count(n => n.IsVisible).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    // The value column is wider in Sci-Fi (room for the meter); the settings keep the Classic width.
    private int ColumnWidthForSave(Aga.Controls.Tree.TreeColumn column)
    {
        if (_valueColumnWidened && column == treeView.Columns[0])
            return column.Width + MeterRoom;
        if (_valueColumnWidened && column == treeView.Columns[1])
            return Math.Max(20, column.Width - MeterRoom);
        return column.Width;
    }

    // Hardware rows get a tinted band with an accent bar, so the devices stand out from their sensors.
    private void SciFiTree_RowDraw(object sender, Aga.Controls.Tree.TreeViewRowDrawEventArgs e)
    {
        if (_design != UiDesign.SciFi || !(e.Node.Tag is HardwareNode))
            return;

        Rectangle r = new Rectangle(0, e.RowRect.Top, treeView.ClientSize.Width + Math.Abs(treeView.OffsetX), e.RowRect.Height);
        using (var band = new System.Drawing.Drawing2D.LinearGradientBrush(r, Color.FromArgb(255, 12, 38, 46), SciFiColors.Panel, System.Drawing.Drawing2D.LinearGradientMode.Horizontal))
            e.Graphics.FillRectangle(band, r);
        using (var bar = new SolidBrush(SciFiColors.Accent))
            e.Graphics.FillRectangle(bar, r.Left, r.Top + 3, 3, r.Height - 6);
    }

    // Each kind of value gets its own color, so the grid reads like an instrument panel.
    private static Color ValueColor(SensorType type)
    {
        return type switch
        {
            SensorType.Temperature => SciFiColors.Data,
            SensorType.Load or SensorType.Control or SensorType.Level or SensorType.Humidity => SciFiColors.Accent,
            SensorType.Power or SensorType.Voltage or SensorType.Current or SensorType.Energy => Color.FromArgb(255, 240, 200, 120),
            SensorType.Fan or SensorType.Flow => Color.FromArgb(255, 122, 240, 168),
            _ => SciFiColors.Text
        };
    }

    private void SciFiPanel_Paint(object sender, PaintEventArgs e)
    {
        if (_design != UiDesign.SciFi || !(sender is Panel panel) || panel.Controls.Count == 0)
            return;

        Control content = panel.Controls[0];
        Rectangle r = Rectangle.Inflate(content.Bounds, 4, 4);
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.None;

        using (var frame = new Pen(SciFiColors.Rule))
            g.DrawRectangle(frame, r);

        const int len = 10;
        using (var bracket = new Pen(SciFiColors.Accent, 2))
        {
            g.DrawLine(bracket, r.Left, r.Top, r.Left + len, r.Top);
            g.DrawLine(bracket, r.Left, r.Top, r.Left, r.Top + len);
            g.DrawLine(bracket, r.Right, r.Top, r.Right - len, r.Top);
            g.DrawLine(bracket, r.Right, r.Top, r.Right, r.Top + len);
            g.DrawLine(bracket, r.Left, r.Bottom, r.Left + len, r.Bottom);
            g.DrawLine(bracket, r.Left, r.Bottom, r.Left, r.Bottom - len);
            g.DrawLine(bracket, r.Right, r.Bottom, r.Right - len, r.Bottom);
            g.DrawLine(bracket, r.Right, r.Bottom, r.Right, r.Bottom - len);
        }

        string label = content == treeView ? "SENSOR GRID" : "TELEMETRY PLOT";
        string meta = content == treeView ? "LIVE // " + _metrics.Current.SensorCount + " CH" : "STACKED // " + (_plotPanel.StackedAxes ? "ON" : "OFF");
        TextRenderer.DrawText(g, label, _sciFiLabelFont, new Point(r.Left, r.Top - 18), SciFiColors.Accent, TextFormatFlags.NoPadding);
        Size ms = TextRenderer.MeasureText(meta, _sciFiLabelFont, Size.Empty, TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, meta, _sciFiLabelFont, new Point(r.Right - ms.Width, r.Top - 18), SciFiColors.Dim, TextFormatFlags.NoPadding);
        using (var tick = new Pen(SciFiColors.Accent))
            g.DrawLine(tick, r.Left, r.Top - 4, r.Left + 28, r.Top - 4);
    }


}
