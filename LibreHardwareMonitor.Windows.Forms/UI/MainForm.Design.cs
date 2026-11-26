// K26K Hardware Monitor - design switching (Classic, Sci-Fi, Bridge and their dashboards) for the main window.
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
using LibreHardwareMonitor.Windows.Forms.UI.Bridge;
using LibreHardwareMonitor.Windows.Forms.UI.Design;
using LibreHardwareMonitor.Windows.Forms.UI.Fancy;
using LibreHardwareMonitor.Windows.Forms.UI.SciFi;
using LibreHardwareMonitor.Windows.Forms.UI.Themes;

namespace LibreHardwareMonitor.Windows.Forms.UI;

public enum UiDesign
{
    Classic,
    SciFi,
    Bridge
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
    private ToolStripMenuItem _designBridgeItem;
    private ToolStripMenuItem _fancyItem;
    private ToolStripMenuItem _reducedMotionItem;
    private ToolStripMenuItem _fancyGlowItem;
    private ToolStripMenuItem _fancyFullscreenItem;
    private Font _classicTreeFont;
    private Font _classicMenuFont;
    private Font _sciFiTreeFont;
    private Font _sciFiMenuFont;
    private Font _sciFiLabelFont;
    private Font _sciFiValueFont;
    private Font _sciFiDeviceFont;

    /// <summary>Header and status strip of the active design (Sci-Fi or Bridge), null in Classic.</summary>
    private Control _activeHeader;
    private Control _activeStatus;

    // visible sensors below a device (sub-devices included)
    private static int CountSensors(Node node)
    {
        int count = 0;
        foreach (Node child in node.Nodes)
        {
            if (!child.IsVisible)
                continue;
            if (child is SensorNode)
                count++;
            else if (!(child is HardwareNode))
                count += CountSensors(child);
        }
        return count;
    }
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
        InitializeWindowFrame();

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
        InitializeBridge();

        // Sci-Fi is the default, except with Windows high contrast, where Classic follows the system colors
        string saved = _settings.GetValue("design", System.Windows.Forms.SystemInformation.HighContrast ? "classic" : "scifi");
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
        _fancyGlowItem.Checked = _settings.GetValue("fancyGlow", true);

        if (!_skiaAvailable || saved == "classic")
        {
            ApplyDesign(UiDesign.Classic, false);
        }
        else
        {
            bool bridge = saved == "bridge" || saved == "bridgefancy";
            ApplyDesign(bridge ? UiDesign.Bridge : UiDesign.SciFi, false);
            if (saved == "fancy" || saved == "bridgefancy")
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
        _designBridgeItem = new ToolStripMenuItem("Bridge");
        _fancyItem = new ToolStripMenuItem("Fancy Mode") { ShortcutKeyDisplayString = "F11" };
        _reducedMotionItem = new ToolStripMenuItem("Reduced Motion");
        _fancyFullscreenItem = new ToolStripMenuItem("Fancy Mode in Fullscreen");
        _fancyGlowItem = new ToolStripMenuItem("Glow Effects");
        _fancyGlowItem.Click += delegate
        {
            _fancyGlowItem.Checked = !_fancyGlowItem.Checked;
            _settings.SetValue("fancyGlow", _fancyGlowItem.Checked);
            if (_fancy != null)
                _fancy.Glow = _fancyGlowItem.Checked;
        };

        _designClassicItem.Click += delegate { ApplyDesign(UiDesign.Classic, true); };
        _designSciFiItem.Click += delegate { ApplyDesign(UiDesign.SciFi, true); };
        _designBridgeItem.Click += delegate { ApplyDesign(UiDesign.Bridge, true); };
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

        designMenu.DropDownItems.AddRange(new ToolStripItem[] { _designClassicItem, _designSciFiItem, _designBridgeItem, new ToolStripSeparator(), _fancyItem, _reducedMotionItem, _fancyGlowItem, _fancyFullscreenItem });
        viewMenuItem.DropDownItems.Add(new ToolStripSeparator());
        viewMenuItem.DropDownItems.Add(designMenu);

        if (!_skiaAvailable)
        {
            _designSciFiItem.Enabled = false;
            _designBridgeItem.Enabled = false;
            _fancyItem.Enabled = false;
            _designSciFiItem.ToolTipText = _designBridgeItem.ToolTipText = _fancyItem.ToolTipText = "SkiaSharp could not be loaded.";
        }
    }

    private void ApplyDesign(UiDesign mode, bool save)
    {
        _design = mode;
        _designClassicItem.Checked = mode == UiDesign.Classic;
        _designSciFiItem.Checked = mode == UiDesign.SciFi;
        _designBridgeItem.Checked = mode == UiDesign.Bridge;
        if (save)
            _settings.SetValue("design", DesignKey(mode, false));

        SuspendLayout();
        RemoveDesignChrome();
        if (mode != UiDesign.Classic)
        {
            bool bridge = mode == UiDesign.Bridge;
            _sciFiTreeFont ??= FontManager.CreateFont(9.75f);
            _sciFiMenuFont ??= FontManager.CreateFont(8.5f);
            _sciFiLabelFont ??= FontManager.CreateFont(8f, FontStyle.Bold);
            Font treeFont = bridge ? BridgeTreeFont : _sciFiTreeFont;
            Font menuFont = bridge ? BridgeMenuFont : _sciFiMenuFont;

            treeView.Font = treeFont;
            treeView.RowHeight = Math.Max(_classicRowHeight, treeFont.Height + 8);
            mainMenu.Font = menuFont;
            treeContextMenu.Font = menuFont;
            splitContainer.Panel1.Padding = bridge ? BridgePanelPadding : new Padding(10, 26, 10, 10);
            splitContainer.Panel2.Padding = bridge ? BridgePanelPadding : new Padding(10, 26, 10, 10);
            themeMenuItem.Enabled = false;
            SciFiBarControl.Active = true;
            SciFiBarControl.Colors = bridge ? BridgeMeters : MeterPalette.SciFi;
            CaptionPalette.Current = bridge ? BridgeDraw.Caption : CaptionPalette.SciFi;
            SciFiTheme.ValueHeaderIndent = _barControl.BarWidth + 2;
            BridgeTheme.ValueHeaderIndent = _barControl.BarWidth + 2;
            // narrower name column; the tree hands the freed width to the value column (room for the meter)
            if (!_valueColumnWidened)
            {
                treeView.Columns[0].Width -= MeterRoom;
                _valueColumnWidened = true;
            }
            SciFiIcons.Apply(_root, bridge ? BridgeIcons : IconTint.SciFi);
            foreach (ToolStripItem item in mainMenu.Items)
            {
                if (item is CaptionButtonItem)
                    continue;
                if (!_classicMenuTexts.ContainsKey(item))
                    _classicMenuTexts[item] = item.Text;
                item.Text = _classicMenuTexts[item].ToUpperInvariant();
            }

            if (bridge)
            {
                CreateBridgeChrome();
                _activeHeader = _bridgeHeader;
                _activeStatus = _bridgeStatus;
            }
            else
            {
                if (_hudHeader == null)
                {
                    _hudHeader = new HudHeader();
                    _hudHeader.FancyRequested += delegate { EnterFancy(); };
                    _hudStatus = new HudStatusBar();
                    AttachFrameHitTests(_hudHeader, _hudHeader.IsOnButton, _hudStatus);
                }
                _activeHeader = _hudHeader;
                _activeStatus = _hudStatus;
            }

            Controls.Add(_activeHeader);
            Controls.Add(_activeStatus);
            ArrangeDocking();
            Theme.Current = bridge ? new BridgeTheme() : new SciFiTheme();
            ApplyThemeToWindows();
            SetCustomFrame(true);
            UpdateDashboard();
        }
        else
        {
            treeView.Font = _classicTreeFont;
            treeView.RowHeight = _classicRowHeight;
            mainMenu.Font = _classicMenuFont;
            treeContextMenu.Font = _classicMenuFont;
            splitContainer.Panel1.Padding = _classicPanel1Padding;
            splitContainer.Panel2.Padding = _classicPanel2Padding;
            themeMenuItem.Enabled = true;
            SciFiBarControl.Active = false;
            SciFiBarControl.Colors = MeterPalette.SciFi;
            CaptionPalette.Current = CaptionPalette.SciFi;
            if (_valueColumnWidened)
            {
                treeView.Columns[0].Width += MeterRoom;
                _valueColumnWidened = false;
            }
            SciFiIcons.Apply(_root, (IconTint)null);
            foreach (var pair in _classicMenuTexts)
                pair.Key.Text = pair.Value;
            RestoreClassicTheme();
            ApplyThemeToWindows();
            SetCustomFrame(false);
        }
        ResumeLayout(true);

        PlotSelectionChanged(this, null);
        SnapBridgeTree();
        mainMenu.Invalidate();
        splitContainer.Panel1.Invalidate();
        splitContainer.Panel2.Invalidate();
        treeView.Invalidate();
    }

    // Dock order: menu at the very top, the design's header below it, status strip at the bottom, content fills the rest.
    private void ArrangeDocking()
    {
        Controls.SetChildIndex(splitContainer, 0);
        if (_activeHeader != null && Controls.Contains(_activeHeader))
        {
            Controls.SetChildIndex(_activeHeader, 1);
            Controls.SetChildIndex(_activeStatus, 2);
        }
        Controls.SetChildIndex(mainMenu, Controls.Count - 1);
    }

    private void RemoveDesignChrome()
    {
        if (_activeHeader != null)
        {
            Controls.Remove(_activeHeader);
            Controls.Remove(_activeStatus);
        }
        _activeHeader = null;
        _activeStatus = null;
    }

    /// <summary>The value of the settings key "design".</summary>
    private static string DesignKey(UiDesign design, bool fancy) => design switch
    {
        UiDesign.Bridge => fancy ? "bridgefancy" : "bridge",
        UiDesign.SciFi => fancy ? "fancy" : "scifi",
        _ => fancy ? "fancy" : "classic"
    };

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

        // Classic has no dashboard of its own: the Fancy dashboard comes with the Sci-Fi design
        if (_design == UiDesign.Classic)
            ApplyDesign(UiDesign.SciFi, false);

        _fancyActive = true;
        _fancyItem.Checked = true;
        _settings.SetValue("design", DesignKey(_design, true));

        if (_fancy == null)
        {
            _fancy = new FancyDashboard();
            _fancy.ExitRequested += delegate { ExitFancy(); };
            AttachFrameHitTests(_fancy);
        }
        _fancy.ReducedMotion = _reducedMotionItem.Checked;
        _fancy.Glow = _fancyGlowItem.Checked;
        // Bridge Fancy is flat: no glow to switch
        _fancyGlowItem.Enabled = _design != UiDesign.Bridge;
        _fancy.Renderer = _design == UiDesign.Bridge ? BridgeFancyRenderer.Instance : FancyRenderer.Instance;

        SuspendLayout();
        mainMenu.Visible = false;
        splitContainer.Visible = false;
        _activeHeader.Visible = false;
        _activeStatus.Visible = false;
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
        UpdateCaptionState();

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
        _fancyGlowItem.Enabled = true;
        _settings.SetValue("design", DesignKey(_design, false));

        if (_fullscreen)
        {
            _fullscreen = false;
            FormBorderStyle = _savedBorder;
            WindowState = _savedState;
        }
        UpdateCaptionState();

        SuspendLayout();
        _fancy.Visible = false;
        mainMenu.Visible = true;
        splitContainer.Visible = true;
        _activeHeader.Visible = true;
        _activeStatus.Visible = true;
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

        bool needed = _design != UiDesign.Classic || _fancyActive || _dumpPath != null;
        if (!needed)
            return;

        // hardware can appear later (e.g. a USB device); give its icons the design's look too
        if (_design != UiDesign.Classic)
            SciFiIcons.Apply(_root, _design == UiDesign.Bridge ? BridgeIcons : IconTint.SciFi);

        string ip = Server.ListenerIp;
        string web = "http://" + (ip == "+" || ip == "?" || string.IsNullOrEmpty(ip) ? "*" : ip) + ":" + Server.ListenerPort;
        DashboardModel model = _metrics.Update(_computer.Hardware, _root.Text, _unitManager.TemperatureUnit == TemperatureUnit.Fahrenheit,
                                               timer.Interval, web, _runWebServer?.Value == true);

        if (_activeHeader == _hudHeader && _hudHeader != null && _hudHeader.Visible)
        {
            _hudHeader.Model = model;
            _hudStatus.Model = model;
        }
        if (_activeHeader == _bridgeHeader && _bridgeHeader != null && _bridgeHeader.Visible)
        {
            _bridgeHeader.Model = model;
            _bridgeStatus.Model = model;
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
                    e.TextColor = SciFiColors.Dim;
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
                // live readings in bold, so the eye finds them first; missing ones stay regular and dim
                if (!dead)
                    e.Font = _sciFiValueFont ??= FontManager.CreateFont(9.75f, FontStyle.Bold);
            }
            else if (sender == nodeTextBoxMin || sender == nodeTextBoxMax)
            {
                // min and max one step brighter than the dim color (still clearly below the live value)
                e.TextColor = dead ? SciFiColors.Dim : ColorTranslator.FromHtml("#A8BCC8");
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
            // devices are the top level of the hierarchy: larger bold accent text, the sensor count after a slash
            e.TextColor = SciFiColors.Accent;
            e.Font = _sciFiDeviceFont ??= FontManager.CreateFont(10.5f, FontStyle.Bold);
            int channels = CountSensors(node);
            // no "0 CH": a board whose sensors need admin rights would only lose the end of its (long) name
            e.Text = e.Text.ToUpperInvariant() + (channels > 0 ? "   // " + channels.ToString(System.Globalization.CultureInfo.InvariantCulture) + " CH" : "");
        }
        else if (node is TypeNode && sender == nodeTextBoxText)
        {
            // group heads read as section labels: bold, brighter, the count in a bracket chip
            e.TextColor = ColorTranslator.FromHtml("#B4C8D4");
            e.Font = _sciFiValueFont ??= FontManager.CreateFont(9.75f, FontStyle.Bold);
            e.Text = e.Text.ToUpperInvariant() + "  [" + node.Nodes.Count(n => n.IsVisible).ToString("00", System.Globalization.CultureInfo.InvariantCulture) + "]";
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
        if (_design != UiDesign.SciFi)
            return;

        Rectangle r = new Rectangle(0, e.RowRect.Top, treeView.ClientSize.Width + Math.Abs(treeView.OffsetX), e.RowRect.Height);
        if (!(e.Node.Tag is HardwareNode))
        {
            // rows inside a device get a thin rail on the left edge, so every device reads as one framed block
            bool insideDevice = false;
            for (Aga.Controls.Tree.TreeNodeAdv p = e.Node.Parent; p != null; p = p.Parent)
            {
                if (p.Tag is HardwareNode)
                {
                    insideDevice = true;
                    break;
                }
            }
            if (!insideDevice)
                return;

            using (var rail = new Pen(Color.FromArgb(110, SciFiColors.Accent)))
                e.Graphics.DrawLine(rail, r.Left + 1, r.Top, r.Left + 1, r.Bottom);
            if (e.Node.Tag is TypeNode)
            {
                // a faint band of its own, fading out to the right, makes every group head a section
                using (var band = new System.Drawing.Drawing2D.LinearGradientBrush(r, Color.FromArgb(255, 10, 30, 38), SciFiColors.Panel, System.Drawing.Drawing2D.LinearGradientMode.Horizontal))
                    e.Graphics.FillRectangle(band, r.Left + 2, r.Top, r.Width - 2, r.Height - 1);
                using var tick = new Pen(Color.FromArgb(170, SciFiColors.Accent));
                e.Graphics.DrawLine(tick, r.Left + 1, r.Top + r.Height / 2, r.Left + 7, r.Top + r.Height / 2);

                // group rows end in a segmented scale line, so the HUD language continues inside the grid
                int from = e.RowRect.Left + 8;
                using var segment = new Pen(Color.FromArgb(70, SciFiColors.Accent));
                using var major = new Pen(Color.FromArgb(150, SciFiColors.Accent));
                int y = r.Bottom - 2, k = 0;
                for (int x = from; x < r.Right - 8; x += 8, k++)
                {
                    e.Graphics.DrawLine(k % 6 == 0 ? major : segment, x, y, x + 4, y);
                    // every sixth segment carries a short tick: structure without extra brightness
                    if (k % 6 == 0)
                        e.Graphics.DrawLine(major, x, y - 3, x, y);
                }
            }
            return;
        }

        using (var band = new System.Drawing.Drawing2D.LinearGradientBrush(r, Color.FromArgb(255, 12, 38, 46), SciFiColors.Panel, System.Drawing.Drawing2D.LinearGradientMode.Horizontal))
            e.Graphics.FillRectangle(band, r);
        using (var bar = new SolidBrush(SciFiColors.Accent))
            e.Graphics.FillRectangle(bar, r.Left, r.Top + 3, 3, r.Height - 6);

        // device rows have edges of their own: an accent line on top that fades out, a fine line below,
        // and a notch at the right end, so the hierarchy (device, group, sensor) shows at a glance
        using (var top = new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(r.Left, r.Top, Math.Max(1, r.Width), 1), Color.FromArgb(220, SciFiColors.Accent), Color.FromArgb(0, SciFiColors.Accent), System.Drawing.Drawing2D.LinearGradientMode.Horizontal))
            e.Graphics.FillRectangle(top, r.Left, r.Top, r.Width, 1);
        using (var bottom = new Pen(Color.FromArgb(90, SciFiColors.Accent)))
            e.Graphics.DrawLine(bottom, r.Left, r.Bottom - 1, r.Right, r.Bottom - 1);
        int right = treeView.ClientSize.Width - 6;
        using (var notch = new SolidBrush(Color.FromArgb(160, SciFiColors.Accent)))
            e.Graphics.FillRectangle(notch, right - 18, r.Top + r.Height / 2 - 1, 18, 2);

        // instrument details: a segment bar before the notch and corner brackets at both ends of the head
        using (var seg = new SolidBrush(Color.FromArgb(110, SciFiColors.Accent)))
        {
            for (int i = 0; i < 6; i++)
                e.Graphics.FillRectangle(seg, right - 30 - i * 7, r.Top + r.Height / 2 - 2, 4, 4);
        }
        using (var corner = new Pen(SciFiColors.Accent))
        {
            e.Graphics.DrawLine(corner, right, r.Top + 2, right, r.Top + 6);
            e.Graphics.DrawLine(corner, right - 4, r.Top + 2, right, r.Top + 2);
            e.Graphics.DrawLine(corner, right, r.Bottom - 3, right, r.Bottom - 7);
            e.Graphics.DrawLine(corner, right - 4, r.Bottom - 3, right, r.Bottom - 3);
        }
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

        // corner brackets (outer strong, inner fine), like the frames of a HUD instrument
        const int len = 18;
        using (var bracket = new Pen(SciFiColors.Accent, 2))
        using (var inner = new Pen(Color.FromArgb(140, SciFiColors.Accent)))
        {
            foreach ((int x, int y, int dx, int dy) in new[] { (r.Left, r.Top, 1, 1), (r.Right, r.Top, -1, 1), (r.Left, r.Bottom, 1, -1), (r.Right, r.Bottom, -1, -1) })
            {
                g.DrawLine(bracket, x, y, x + dx * len, y);
                g.DrawLine(bracket, x, y, x, y + dy * len);
                g.DrawLine(inner, x + dx * 4, y + dy * 4, x + dx * 10, y + dy * 4);
                g.DrawLine(inner, x + dx * 4, y + dy * 4, x + dx * 4, y + dy * 10);
            }
        }

        // ports in the middle of every edge
        using (var port = new Pen(SciFiColors.Accent, 2))
        {
            int mx = (r.Left + r.Right) / 2, my = (r.Top + r.Bottom) / 2;
            g.DrawLine(port, mx - 14, r.Bottom, mx + 14, r.Bottom);
            g.DrawLine(port, r.Left, my - 14, r.Left, my + 14);
            g.DrawLine(port, r.Right, my - 14, r.Right, my + 14);
        }

        string label = content == treeView ? "SENSOR GRID" : "TELEMETRY PLOT";
        string meta = content == treeView ? "LIVE // " + _metrics.Current.SensorCount + " CH" : "AXES // " + (_plotPanel.StackedAxes ? "STACKED" : "OVERLAID");
        TextRenderer.DrawText(g, label, _sciFiLabelFont, new Point(r.Left, r.Top - 18), SciFiColors.Accent, TextFormatFlags.NoPadding);
        Size ls = TextRenderer.MeasureText(label, _sciFiLabelFont, Size.Empty, TextFormatFlags.NoPadding);
        Size ms = TextRenderer.MeasureText(meta, _sciFiLabelFont, Size.Empty, TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, meta, _sciFiLabelFont, new Point(r.Right - ms.Width, r.Top - 18), SciFiColors.Dim, TextFormatFlags.NoPadding);
        using (var tick = new Pen(SciFiColors.Accent))
            g.DrawLine(tick, r.Left, r.Top - 4, r.Left + 28, r.Top - 4);

        // instrument ruler between the label and the meta text, like the header ruler
        using (var minor = new Pen(SciFiColors.RuleStrong))
        using (var major = new Pen(Color.FromArgb(150, SciFiColors.Accent)))
        {
            int from = r.Left + ls.Width + 16, to = r.Right - ms.Width - 16;
            for (int x = from, k = 0; x < to; x += 8, k++)
                g.DrawLine(k % 5 == 0 ? major : minor, x, r.Top - (k % 5 == 0 ? 11 : 8), x, r.Top - 5);
        }

        // scale marks on the left edge and a dim scan edge along the bottom
        using (var mark = new Pen(SciFiColors.RuleStrong))
        {
            for (int y = r.Top + 24; y < r.Bottom - 24; y += 24)
                g.DrawLine(mark, r.Left - 4, y, r.Left, y);
        }
        using (var scan = new Pen(Color.FromArgb(90, SciFiColors.Accent)))
            g.DrawLine(scan, r.Left + len + 6, r.Bottom, r.Right - len - 6, r.Bottom);
    }


}
