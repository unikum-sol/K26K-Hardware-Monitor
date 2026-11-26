// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// Partial Copyright (C) Michael Möller <mmoeller@openhardwaremonitor.org> and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Windows.Forms.Utilities;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.WindowsForms;
using OxyPlot.Series;
using LibreHardwareMonitor.Windows.Forms.UI.Themes;

namespace LibreHardwareMonitor.Windows.Forms.UI;

public class PlotPanel : UserControl
{
    private readonly PersistentSettings _settings;
    private readonly UnitManager _unitManager;
    private readonly PlotView _plot;
    private readonly PlotModel _model;
    private readonly TimeSpanAxis _timeAxis = new TimeSpanAxis();
    private readonly SortedDictionary<SensorType, LinearAxis> _axes = new SortedDictionary<SensorType, LinearAxis>();
    private readonly Dictionary<SensorType, LineAnnotation> _annotations = new Dictionary<SensorType, LineAnnotation>();

    // K26K: value axes the user zoomed or panned. Only their ranges are saved; all others autoscale.
    private readonly HashSet<SensorType> _userZoomedAxes = new HashSet<SensorType>();

    // K26K: legend and horizontal axis heads ("TEMPERATURE // °C") used by the Sci-Fi design
    private Legend _legend;
    private readonly Dictionary<SensorType, TextAnnotation> _axisHeads = new Dictionary<SensorType, TextAnnotation>();

    // K26K: sensor behind every series (legend values) and the marker at its newest value
    private readonly Dictionary<LineSeries, ISensor> _seriesSensors = new Dictionary<LineSeries, ISensor>();
    private readonly Dictionary<LineSeries, PointAnnotation> _endMarkers = new Dictionary<LineSeries, PointAnnotation>();
    private double _strokeThickness = 1;

    private bool AxisHeadsActive => Theme.Current.PlotAxisHeads && StackedAxes;

    // K26K: limits that keep every axis range finite. Infinite ranges made GDI+ throw an OverflowException.
    private const double AxisLimit = 1e15;

    // K26K: version 2 only saves ranges of axes the user zoomed. Older versions saved every range,
    // which froze the axes and stored -Infinity for hidden ones, so those values are ignored once.
    private const int RangesVersion = 2;
    private UserOption _stackedAxes;
    private UserOption _showAxesLabels;
    private UserOption _timeAxisEnableZoom;
    private UserOption _yAxesEnableZoom;
    private DateTime _now;
    private float _dpiX;
    private float _dpiY;
    private double _dpiXScale = 1;
    private double _dpiYScale = 1;
    private Point _rightClickEnter;
    private bool _cancelContextMenu = false;

    public PlotPanel(PersistentSettings settings, UnitManager unitManager)
    {
        _settings = settings;
        _unitManager = unitManager;

        SetDpi();
        _model = CreatePlotModel();

        _plot = new PlotView { Dock = DockStyle.Fill, Model = _model, BackColor = Color.Black, ContextMenuStrip = CreateMenu() };
        _plot.MouseDown += (sender, e) =>
        {
            if (e.Button == MouseButtons.Right)
            {
                _rightClickEnter = e.Location;
            }
        };
        _plot.MouseMove += (sender, e) =>
        {
            if (e.Button == MouseButtons.Right)
            {
                if (!_cancelContextMenu && e.Location.DistanceTo(_rightClickEnter) > 10.0f)
                {
                    _cancelContextMenu = true;
                }
            }
        };

        UpdateAxesPosition();

        SuspendLayout();
        Controls.Add(_plot);
        ResumeLayout(true);
        _plot.ShowTracker(new TrackerHitResult());
        _plot.HideTracker();
        foreach (Control plotControl in _plot.Controls)
        {
            plotControl.BackColor = Theme.Current.PlotBackgroundColor;
            plotControl.ForeColor = Theme.Current.PlotTextColor;
        }
        ApplyTheme();
    }

    public void ApplyTheme()
    {
        _model.Background = Theme.Current.PlotBackgroundColor.ToOxyColor();
        // K26K: designs can use their own plot font (OxyPlot's default is Segoe UI).
        _model.DefaultFont = Theme.Current.PlotFontFamily ?? "Segoe UI";
        _model.DefaultFontSize = double.IsNaN(Theme.Current.PlotFontSize) ? 12 : Theme.Current.PlotFontSize;
        _model.PlotAreaBorderColor = Theme.Current.PlotBorderColor.ToOxyColor();
        foreach (Axis axis in _model.Axes)
        {
            axis.AxislineColor = Theme.Current.PlotBorderColor.ToOxyColor();
            axis.MajorGridlineColor = Theme.Current.PlotGridMajorColor.ToOxyColor();
            axis.MinorGridlineColor = Theme.Current.PlotGridMinorColor.ToOxyColor();
            axis.TextColor = Theme.Current.PlotTextColor.ToOxyColor();
            axis.TitleColor = Theme.Current.PlotTextColor.ToOxyColor();
            axis.MinorTicklineColor = Theme.Current.PlotBorderColor.ToOxyColor();
            axis.TicklineColor = Theme.Current.PlotBorderColor.ToOxyColor();
        }
        ApplyAxisTitles();
        _model.TitleColor = Theme.Current.PlotTextColor.ToOxyColor();
        _timeAxis.StringFormat = Theme.Current.PlotTimeFormat ?? "h:mm";
        foreach (LineAnnotation annotation in _model.Annotations.Select(x => x as LineAnnotation).Where(x => x != null))
        {
            annotation.Color = Theme.Current.PlotDashedSeparators ? Theme.Current.PlotGridMajorColor.ToOxyColor() : Theme.Current.PlotBorderColor.ToOxyColor();
            annotation.LineStyle = Theme.Current.PlotDashedSeparators ? LineStyle.Dash : LineStyle.Solid;
            annotation.StrokeThickness = Theme.Current.PlotDashedSeparators ? 1 : 2;
        }

        // K26K: legend (which curve is which sensor) for designs that want one
        if (Theme.Current.PlotShowLegend)
        {
            // below the plot, so it never covers a curve
            // (the model already has a legend, invisible in Classic; it is configured here)
            if (_legend == null)
            {
                _legend = _model.Legends.OfType<Legend>().FirstOrDefault();
                if (_legend == null)
                {
                    _legend = new Legend();
                    _model.Legends.Add(_legend);
                }
                // one entry per line in axis order; more than four entries continue in a second column
                _legend.LegendPosition = LegendPosition.BottomLeft;
                _legend.LegendPlacement = LegendPlacement.Outside;
                _legend.LegendOrientation = LegendOrientation.Vertical;
                _legend.LegendMaxHeight = 4 * 19 * _dpiYScale + 14 * _dpiYScale;
                _legend.LegendLineSpacing = 3 * _dpiYScale;
                _legend.LegendItemAlignment = OxyPlot.HorizontalAlignment.Left;
                _legend.LegendSymbolLength = 22 * _dpiXScale;
                _legend.LegendFontSize = 12;
                _legend.LegendPadding = 6 * _dpiXScale;
                _legend.LegendItemSpacing = 18 * _dpiXScale;
                _legend.LegendColumnSpacing = 18 * _dpiXScale;
                _legend.LegendMargin = 6 * _dpiXScale;
            }
            _legend.LegendBackground = Theme.Current.PlotLegendBackgroundColor.ToOxyColor();
            _legend.LegendBorder = Theme.Current.PlotLegendBorder ? OxyColor.FromAColor(110, Theme.Current.HyperlinkColor.ToOxyColor()) : OxyColors.Undefined;
            _legend.LegendTextColor = Theme.Current.ForegroundColor.ToOxyColor();
            _model.IsLegendVisible = true;
        }
        else
        {
            _model.IsLegendVisible = false;
        }
        ApplySeriesStyle();
        UpdateAxisHeads();
    }

    // Rotated axis titles, unless the design shows horizontal heads (only possible with stacked axes).
    private void ApplyAxisTitles()
    {
        foreach (KeyValuePair<SensorType, LinearAxis> pair in _axes)
        {
            LinearAxis axis = pair.Value;
            axis.MinimumRange = Theme.Current.PlotMinimumSpans ? MinimumSpan(pair.Key) : 0;
            // a little room above the highest value, so a curve at 100 % is not hidden by the separator
            axis.MaximumPadding = Theme.Current.PlotMinimumSpans ? 0.05 : 0.01;
            if (string.IsNullOrEmpty(axis.Key))
                continue;
            axis.Title = AxisHeadsActive ? null : Theme.Current.PlotUppercaseTitles ? axis.Key.ToUpperInvariant() : axis.Key;
            // with horizontal heads the rotated title (including the unit) is not drawn at all
            axis.TitleFormatString = AxisHeadsActive ? " " : "{0} [{1}]";
        }
    }

    // Smallest span an autoscaled axis shows, so a 0.4 K wobble does not fill the whole height.
    private double MinimumSpan(SensorType type) => type switch
    {
        SensorType.Temperature => _unitManager.TemperatureUnit == TemperatureUnit.Fahrenheit ? 18 : 10,
        SensorType.Load or SensorType.Control or SensorType.Level or SensorType.Humidity => 10,
        SensorType.Power => 10,
        SensorType.Clock => 200,
        SensorType.Fan => 300,
        SensorType.Voltage => 0.2,
        SensorType.Current => 1,
        SensorType.Flow => 50,
        SensorType.Data => 1,
        _ => 0
    };

    // K26K: line width, line style per series of one axis, legend titles and end markers.
    private void ApplySeriesStyle()
    {
        var perAxis = new Dictionary<string, int>();
        LineStyle[] styles = { LineStyle.Solid, LineStyle.Dash, LineStyle.Dot, LineStyle.DashDot, LineStyle.LongDash };
        foreach (LineSeries series in _model.Series.OfType<LineSeries>())
        {
            series.StrokeThickness = Math.Max(_strokeThickness, Theme.Current.PlotMinStrokeThickness);
            perAxis.TryGetValue(series.YAxisKey ?? "", out int index);
            perAxis[series.YAxisKey ?? ""] = index + 1;
            series.LineStyle = Theme.Current.PlotVaryLineStyles ? styles[index % styles.Length] : LineStyle.Automatic;
        }
        UpdateSeriesTitles();
        UpdateEndMarkers();
    }

    private static string TypeTag(SensorType type) => type switch
    {
        SensorType.Temperature => "TEMP",
        SensorType.Voltage => "VOLT",
        SensorType.Control => "CTRL",
        SensorType.Throughput => "RATE",
        _ => type.ToString().ToUpperInvariant()
    };

    // Classic: "hardware sensor". With a legend: "RTX 2070 SUPER GPU CORE // TEMP  42.0 °C".
    private void UpdateSeriesTitles()
    {
        foreach (KeyValuePair<LineSeries, ISensor> pair in _seriesSensors)
        {
            ISensor sensor = pair.Value;
            if (!Theme.Current.PlotShowLegend)
            {
                pair.Key.Title = sensor.Hardware.Name + " " + sensor.Name;
                continue;
            }

            // "Total Memory" + "Memory" is shown once
            string hardware = Telemetry.SystemMetrics.ShortHardwareName(sensor.Hardware.Name, 16);
            string name = hardware.EndsWith(sensor.Name, StringComparison.OrdinalIgnoreCase) ? hardware : hardware + " " + sensor.Name;
            string title = name.ToUpperInvariant() + " // " + TypeTag(sensor.SensorType);
            DataPoint? last = LastPoint(sensor);
            if (last.HasValue && !double.IsNaN(last.Value.Y))
            {
                string unit = _axes.TryGetValue(sensor.SensorType, out LinearAxis axis) ? axis.Unit : "";
                title += "  " + last.Value.Y.ToString(Math.Abs(last.Value.Y) >= 100 ? "0" : "0.0") + (string.IsNullOrEmpty(unit) ? "" : " " + unit);
            }
            pair.Key.Title = title;
        }
    }

    // A dot on the newest value of every curve, so the current reading is easy to find.
    private void UpdateEndMarkers()
    {
        foreach (KeyValuePair<LineSeries, PointAnnotation> pair in _endMarkers.ToList())
        {
            if (!Theme.Current.PlotEndMarkers || !_seriesSensors.ContainsKey(pair.Key))
            {
                _model.Annotations.Remove(pair.Value);
                _endMarkers.Remove(pair.Key);
            }
        }
        if (!Theme.Current.PlotEndMarkers)
            return;

        foreach (KeyValuePair<LineSeries, ISensor> pair in _seriesSensors)
        {
            DataPoint? last = LastPoint(pair.Value);
            if (!_endMarkers.TryGetValue(pair.Key, out PointAnnotation marker))
            {
                marker = new PointAnnotation { Shape = MarkerType.Circle, Size = 3.5, StrokeThickness = 1.5, Layer = AnnotationLayer.AboveSeries };
                _endMarkers[pair.Key] = marker;
                _model.Annotations.Add(marker);
            }
            marker.YAxisKey = pair.Key.YAxisKey;
            marker.Fill = pair.Key.Color;
            marker.Stroke = Theme.Current.PlotBackgroundColor.ToOxyColor();
            bool visible = last.HasValue && !double.IsNaN(last.Value.Y);
            marker.X = visible ? last.Value.X : double.NaN;
            marker.Y = visible ? last.Value.Y : double.NaN;
            marker.Size = visible ? 3.5 : 0;
        }
    }

    private DataPoint? LastPoint(ISensor sensor)
    {
        SensorValue[] values;
        try
        {
            values = sensor.Values.ToArray();
        }
        catch (InvalidOperationException)
        {
            // the history changed while it was copied; the next update shows the value
            return null;
        }
        if (values.Length == 0)
            return null;
        return ToDataPoint(sensor.SensorType, values[values.Length - 1]);
    }

    private DataPoint ToDataPoint(SensorType type, SensorValue value)
    {
        float displayedValue;

        if (type == SensorType.Temperature && _unitManager.TemperatureUnit == TemperatureUnit.Fahrenheit)
        {
            displayedValue = UnitManager.CelsiusToFahrenheit(value.Value).Value;
        }
        else if (type == SensorType.Data)
        {
            displayedValue = UnitManager.BytesToGigaBytes(value.Value).Value;
        }
        else
        {
            displayedValue = value.Value;
        }

        // K26K: an infinite value would stretch the axis to infinity; draw it as a gap instead.
        if (float.IsInfinity(displayedValue))
            displayedValue = float.NaN;

        return new DataPoint((_now - value.Time).TotalSeconds, displayedValue);
    }

    public void SetCurrentSettings()
    {
        // K26K: save a value axis range only when the user zoomed or panned that axis;
        // otherwise remove it, so the axis autoscales again on the next start.
        foreach (KeyValuePair<SensorType, LinearAxis> pair in _axes)
        {
            LinearAxis axis = pair.Value;
            if (_userZoomedAxes.Contains(pair.Key) && axis.IsAxisVisible && IsValidRange(axis.ActualMinimum, axis.ActualMaximum))
            {
                _settings.SetValue("plotPanel.Min" + axis.Key, (float)axis.ActualMinimum);
                _settings.SetValue("plotPanel.Max" + axis.Key, (float)axis.ActualMaximum);
            }
            else
            {
                _settings.Remove("plotPanel.Min" + axis.Key);
                _settings.Remove("plotPanel.Max" + axis.Key);
            }
        }

        _settings.SetValue("plotPanel.RangesVersion", RangesVersion);

        if (IsValidRange(_timeAxis.ActualMinimum, _timeAxis.ActualMaximum))
        {
            _settings.SetValue("plotPanel.MinTimeSpan", (float)_timeAxis.ActualMinimum);
            _settings.SetValue("plotPanel.MaxTimeSpan", (float)_timeAxis.ActualMaximum);
        }
    }

    // K26K: a range OxyPlot and GDI+ can draw.
    private static bool IsValidRange(double min, double max)
    {
        return !double.IsNaN(min) && !double.IsNaN(max) &&
               !double.IsInfinity(min) && !double.IsInfinity(max) &&
               min < max && Math.Abs(min) < AxisLimit && Math.Abs(max) < AxisLimit;
    }

    // K26K: puts axes whose range became invalid back to autoscaling before they are drawn.
    private void ResetInvalidAxes()
    {
        foreach (KeyValuePair<SensorType, LinearAxis> pair in _axes)
        {
            LinearAxis axis = pair.Value;
            if (axis.IsAxisVisible && !IsValidRange(axis.ActualMinimum, axis.ActualMaximum))
            {
                axis.Reset();
                _userZoomedAxes.Remove(pair.Key);
            }
        }

        if (!IsValidRange(_timeAxis.ActualMinimum, _timeAxis.ActualMaximum))
            _timeAxis.Reset();
    }

    private ContextMenuStrip CreateMenu()
    {
        ContextMenuStrip menu = new ContextMenuStrip();
        menu.Renderer = new ThemedToolStripRenderer();
        menu.Opening += (sender, e) =>
        {
            if (_cancelContextMenu)
            {
                e.Cancel = true;
                _cancelContextMenu = false;
            }
        };

        ToolStripMenuItem stackedAxesMenuItem = new ToolStripMenuItem("Stacked Axes");
        _stackedAxes = new UserOption("stackedAxes", true, stackedAxesMenuItem, _settings);
        _stackedAxes.Changed += (sender, e) =>
        {
            UpdateAxesPosition();
            InvalidatePlot();
        };
        menu.Items.Add(stackedAxesMenuItem);

        ToolStripMenuItem showAxesLabelsMenuItem = new ToolStripMenuItem("Show Axes Labels");
        _showAxesLabels = new UserOption("showAxesLabels", true, showAxesLabelsMenuItem, _settings);
        _showAxesLabels.Changed += (sender, e) =>
        {
            if (_showAxesLabels.Value)
                _model.PlotMargins = new OxyThickness(double.NaN);
            else
                _model.PlotMargins = new OxyThickness(0);
        };
        menu.Items.Add(showAxesLabelsMenuItem);

        ToolStripMenuItem timeAxisMenuItem = new ToolStripMenuItem("Time Axis");
        ToolStripMenuItem[] timeAxisMenuItems =
        { new ToolStripMenuItem("Enable Zoom"),
            new ToolStripMenuItem("Auto", null, (s, e) => { TimeAxisZoom(0, double.NaN); }),
            new ToolStripMenuItem("5 min", null, (s, e) => { TimeAxisZoom(0, 5 * 60); }),
            new ToolStripMenuItem("10 min", null, (s, e) => { TimeAxisZoom(0, 10 * 60); }),
            new ToolStripMenuItem("20 min", null, (s, e) => { TimeAxisZoom(0, 20 * 60); }),
            new ToolStripMenuItem("30 min", null, (s, e) => { TimeAxisZoom(0, 30 * 60); }),
            new ToolStripMenuItem("45 min", null, (s, e) => { TimeAxisZoom(0, 45 * 60); }),
            new ToolStripMenuItem("1 h", null, (s, e) => { TimeAxisZoom(0, 60 * 60); }),
            new ToolStripMenuItem("1.5 h", null, (s, e) => { TimeAxisZoom(0, 1.5 * 60 * 60); }),
            new ToolStripMenuItem("2 h", null, (s, e) => { TimeAxisZoom(0, 2 * 60 * 60); }),
            new ToolStripMenuItem("3 h", null, (s, e) => { TimeAxisZoom(0, 3 * 60 * 60); }),
            new ToolStripMenuItem("6 h", null, (s, e) => { TimeAxisZoom(0, 6 * 60 * 60); }),
            new ToolStripMenuItem("12 h", null, (s, e) => { TimeAxisZoom(0, 12 * 60 * 60); }),
            new ToolStripMenuItem("24 h", null, (s, e) => { TimeAxisZoom(0, 24 * 60 * 60); }) };

        foreach (ToolStripItem mi in timeAxisMenuItems)
            timeAxisMenuItem.DropDownItems.Add(mi);
        menu.Items.Add(timeAxisMenuItem);

        _timeAxisEnableZoom = new UserOption("timeAxisEnableZoom", true, timeAxisMenuItems[0], _settings);
        _timeAxisEnableZoom.Changed += (sender, e) =>
        {
            _timeAxis.IsZoomEnabled = _timeAxisEnableZoom.Value;
        };

        ToolStripMenuItem yAxesMenuItem = new ToolStripMenuItem("Value Axes");
        ToolStripMenuItem[] yAxesMenuItems =
        { new ToolStripMenuItem("Enable Zoom"),
            new ToolStripMenuItem("Autoscale All", null, (s, e) => { AutoscaleAllYAxes(); }) };

        foreach (ToolStripItem mi in yAxesMenuItems)
            yAxesMenuItem.DropDownItems.Add(mi);
        menu.Items.Add(yAxesMenuItem);

        _yAxesEnableZoom = new UserOption("yAxesEnableZoom", true, yAxesMenuItems[0], _settings);
        _yAxesEnableZoom.Changed += (sender, e) => UpdateAxesInteraction();

        return menu;
    }

    private PlotModel CreatePlotModel()
    {
        _timeAxis.Position = AxisPosition.Bottom;
        _timeAxis.MajorGridlineStyle = LineStyle.Solid;
        _timeAxis.MajorGridlineThickness = 1;
        _timeAxis.MajorGridlineColor = OxyColor.FromRgb(192, 192, 192);
        _timeAxis.MinorGridlineStyle = LineStyle.Solid;
        _timeAxis.MinorGridlineThickness = 1;
        _timeAxis.MinorGridlineColor = OxyColor.FromRgb(232, 232, 232);
        _timeAxis.StartPosition = 1;
        _timeAxis.EndPosition = 0;
        _timeAxis.MinimumPadding = 0;
        _timeAxis.MaximumPadding = 0;
        _timeAxis.AbsoluteMinimum = 0;
        _timeAxis.Minimum = 0;
        _timeAxis.AbsoluteMaximum = 24 * 60 * 60;
        _timeAxis.Zoom(
                       _settings.GetValue("plotPanel.MinTimeSpan", 0.0f),
                       _settings.GetValue("plotPanel.MaxTimeSpan", 10.0f * 60));
        _timeAxis.StringFormat = "h:mm";

        var units = new Dictionary<SensorType, string>
        {
            { SensorType.Voltage, "V" },
            { SensorType.Current, "A" },
            { SensorType.Clock, "MHz" },
            { SensorType.Temperature, "°C" },
            { SensorType.Load, "%" },
            { SensorType.Fan, "RPM" },
            { SensorType.Flow, "L/h" },
            { SensorType.Control, "%" },
            { SensorType.Level, "%" },
            { SensorType.Factor, "1" },
            { SensorType.Power, "W" },
            { SensorType.Data, "GB" },
            { SensorType.Frequency, "Hz" },
            { SensorType.Energy, "mWh" },
            { SensorType.Noise, "dBA" },
            { SensorType.Conductivity, "µS/cm" },
            { SensorType.Humidity, "%" }
        };

        foreach (SensorType type in Enum.GetValues(typeof(SensorType)))
        {
            string typeName = type.ToString();
            var axis = new LinearAxis
            {
                Position = AxisPosition.Left,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineThickness = 1,
                MajorGridlineColor = _timeAxis.MajorGridlineColor,
                MinorGridlineStyle = LineStyle.Solid,
                MinorGridlineThickness = 1,
                MinorGridlineColor = _timeAxis.MinorGridlineColor,
                AxislineStyle = LineStyle.Solid,
                Title = typeName,
                Key = typeName,
                AbsoluteMinimum = -AxisLimit,
                AbsoluteMaximum = AxisLimit,
            };

            var annotation = new LineAnnotation
            {
                Type = LineAnnotationType.Horizontal,
                ClipByXAxis = false,
                ClipByYAxis = false,
                LineStyle = LineStyle.Solid,
                Color = Theme.Current.PlotBorderColor.ToOxyColor(),
                YAxisKey = typeName,
                StrokeThickness = 2,
            };

#pragma warning disable CS0618 //obsolete warning

            axis.AxisChanged += (sender, args) =>
            {
                annotation.Y = axis.ActualMinimum;

                // K26K: remember whether the user zoomed or panned this axis, or put it back to autoscaling.
                if (args.ChangeType == AxisChangeTypes.Reset)
                    _userZoomedAxes.Remove(type);
                else
                    _userZoomedAxes.Add(type);
            };
            axis.TransformChanged += (sender, args) => annotation.Y = axis.ActualMinimum;

#pragma warning restore CS0618 //obsolete warning

            // K26K: ranges saved by older versions froze the axes, so they are only used from version 2 on.
            if (_settings.GetValue("plotPanel.RangesVersion", 0) >= RangesVersion)
            {
                float min = _settings.GetValue("plotPanel.Min" + axis.Key, float.NaN);
                float max = _settings.GetValue("plotPanel.Max" + axis.Key, float.NaN);

                // Skip unset and broken ranges (e.g. Infinity saved after zooming out too far), as OxyPlot can't draw them.
                if (IsValidRange(min, max))
                    axis.Zoom(min, max);
            }

            if (units.ContainsKey(type))
                axis.Unit = units[type];

            _axes.Add(type, axis);
            _annotations.Add(type, annotation);
        }

        var model = new ScaledPlotModel(_dpiXScale, _dpiYScale);
        model.Axes.Add(_timeAxis);
        foreach (LinearAxis axis in _axes.Values)
            model.Axes.Add(axis);
        model.IsLegendVisible = false;

        return model;
    }

    private void SetDpi()
    {
        // https://msdn.microsoft.com/en-us/library/windows/desktop/dn469266(v=vs.85).aspx
        const int defaultDpi = 96;
        Graphics g = CreateGraphics();

        try
        {
            _dpiX = g.DpiX;
            _dpiY = g.DpiY;
        }
        finally
        {
            g.Dispose();
        }

        if (_dpiX > 0)
            _dpiXScale = _dpiX / defaultDpi;
        if (_dpiY > 0)
            _dpiYScale = _dpiY / defaultDpi;
    }

    public void SetSensors(List<ISensor> sensors, IDictionary<ISensor, Color> colors, double strokeThickness)
    {
        _model.Series.Clear();
        _seriesSensors.Clear();
        _strokeThickness = strokeThickness;
        var types = new HashSet<SensorType>();

        // with a legend the series are grouped by axis, so the legend reads axis by axis
        IEnumerable<ISensor> ordered = Theme.Current.PlotShowLegend ? sensors.OrderBy(s => s.SensorType) : sensors;
        foreach (ISensor sensor in ordered)
        {
            var series = new LineSeries
            {
                ItemsSource = sensor.Values.Select(value => ToDataPoint(sensor.SensorType, value)),
                Color = colors[sensor].ToOxyColor(),
                StrokeThickness = strokeThickness,
                YAxisKey = _axes[sensor.SensorType].Key,
                Title = sensor.Hardware.Name + " " + sensor.Name
            };

            _model.Series.Add(series);
            _seriesSensors[series] = sensor;

            types.Add(sensor.SensorType);
        }
        ApplySeriesStyle();

        foreach (KeyValuePair<SensorType, LinearAxis> pair in _axes.Reverse())
        {
            LinearAxis axis = pair.Value;
            SensorType type = pair.Key;
            axis.IsAxisVisible = types.Contains(type);
        }

        // K26K: a hint while nothing is plotted (only designs that define one)
        _model.Title = sensors.Count == 0 ? Theme.Current.PlotEmptyHint : null;
        _model.TitleFontSize = 12;
        _model.TitleFontWeight = OxyPlot.FontWeights.Normal;

        UpdateAxesInteraction();
        UpdateAxesPosition();
        InvalidatePlot();
    }

    // K26K: hidden axes have no height on screen. Zooming or panning them divided by zero and set
    // their range to -Infinity, which crashed the plot as soon as such an axis became visible.
    // So only visible axes can be zoomed or panned.
    private void UpdateAxesInteraction()
    {
        foreach (LinearAxis axis in _axes.Values)
        {
            axis.IsZoomEnabled = axis.IsAxisVisible && (_yAxesEnableZoom?.Value ?? true);
            axis.IsPanEnabled = axis.IsAxisVisible;
        }
    }

    // K26K: read by the web interface to mirror the desktop plot.
    public bool StackedAxes => _stackedAxes?.Value ?? true;

    // K26K: visible time span of the plot in seconds (NaN while the plot has not been drawn yet).
    public double VisibleTimeSpanSeconds => _timeAxis.ActualMaximum - Math.Max(0, _timeAxis.ActualMinimum);

    public void UpdateStrokeThickness(double strokeThickness)
    {
        _strokeThickness = strokeThickness;
        foreach (LineSeries series in _model.Series)
        {
            series.StrokeThickness = Math.Max(strokeThickness, Theme.Current.PlotMinStrokeThickness);
        }
        InvalidatePlot();
    }

    private void UpdateAxesPosition()
    {
        if (_stackedAxes.Value)
        {
            int count = _axes.Values.Count(axis => axis.IsAxisVisible);
            double start = 0.0;
            foreach (KeyValuePair<SensorType, LinearAxis> pair in _axes.Reverse())
            {
                LinearAxis axis = pair.Value;
                axis.StartPosition = start;
                double delta = axis.IsAxisVisible ? 1.0 / count : 0;
                start += delta;
                axis.EndPosition = start;
                // K26K: designs can leave a gap between stacked axes so their labels do not touch
                // (the gap sits above every axis, where its head is drawn)
                double gap = Theme.Current.PlotStackGap;
                if (axis.IsAxisVisible && gap > 0 && delta > gap * 2)
                    axis.EndPosition -= gap;
                axis.PositionTier = 0;
                axis.MajorGridlineStyle = LineStyle.Solid;
                axis.MinorGridlineStyle = LineStyle.Solid;
                LineAnnotation annotation = _annotations[pair.Key];
                annotation.Y = axis.ActualMinimum;
                if (!_model.Annotations.Contains(annotation)) 
                    _model.Annotations.Add(annotation);
            }
        }
        else
        {
            int tier = 0;

            foreach (KeyValuePair<SensorType, LinearAxis> pair in _axes.Reverse())
            {
                LinearAxis axis = pair.Value;

                if (axis.IsAxisVisible)
                {
                    axis.StartPosition = 0;
                    axis.EndPosition = 1;
                    axis.PositionTier = tier;
                    tier++;
                }
                else
                {
                    axis.StartPosition = 0;
                    axis.EndPosition = 0;
                    axis.PositionTier = 0;
                }
                axis.MajorGridlineStyle = LineStyle.None;
                axis.MinorGridlineStyle = LineStyle.None;
                LineAnnotation annotation = _annotations[pair.Key];
                if (_model.Annotations.Contains(annotation)) 
                    _model.Annotations.Remove(_annotations[pair.Key]);
            }
        }
    }

    public void InvalidatePlot()
    {
        _now = DateTime.UtcNow;

        if (_axes != null)
        {
            foreach (KeyValuePair<SensorType, LinearAxis> pair in _axes)
            {
                LinearAxis axis = pair.Value;
                SensorType type = pair.Key;
                if (type == SensorType.Temperature)
                    axis.Unit = _unitManager.TemperatureUnit == TemperatureUnit.Celsius ? "°C" : "°F";
                    
                if (!_stackedAxes.Value) 
                    continue;

                var annotation = _annotations[pair.Key];
                annotation.Y = axis.ActualMaximum;
            }

            ResetInvalidAxes();
            ApplyAxisTitles();
            UpdateAxisHeads();
            UpdateSeriesTitles();
            UpdateEndMarkers();
        }

        _plot?.InvalidatePlot(true);
    }

    // A head at the top left of every visible axis area; the time axis runs right to left, so its maximum is the left edge.
    private void UpdateAxisHeads()
    {
        foreach (KeyValuePair<SensorType, LinearAxis> pair in _axes)
        {
            LinearAxis axis = pair.Value;
            bool show = AxisHeadsActive && axis.IsAxisVisible && !double.IsNaN(axis.ActualMaximum) && !double.IsNaN(_timeAxis.ActualMaximum);
            _axisHeads.TryGetValue(pair.Key, out TextAnnotation head);
            if (!show)
            {
                if (head != null && _model.Annotations.Contains(head))
                    _model.Annotations.Remove(head);
                continue;
            }

            if (head == null)
            {
                head = new TextAnnotation
                {
                    YAxisKey = axis.Key,
                    TextHorizontalAlignment = OxyPlot.HorizontalAlignment.Left,
                    // sits in the gap above the axis, on the dashed separator, not on a grid line
                    TextVerticalAlignment = OxyPlot.VerticalAlignment.Bottom,
                    Stroke = OxyColors.Transparent,
                    FontWeight = FontWeights.Bold,
                    FontSize = 12,
                    Padding = new OxyThickness(6, 2, 6, 3),
                    ClipByYAxis = false,
                    Layer = AnnotationLayer.AboveSeries
                };
                _axisHeads[pair.Key] = head;
            }

            if (!_model.Annotations.Contains(head))
                _model.Annotations.Add(head);
            head.Text = axis.Key.ToUpperInvariant() + (string.IsNullOrEmpty(axis.Unit) ? "" : "  //  " + axis.Unit);
            head.TextColor = Theme.Current.HyperlinkColor.ToOxyColor();
            head.Background = OxyColor.FromAColor(200, Theme.Current.PlotBackgroundColor.ToOxyColor());
            head.TextPosition = new DataPoint(_timeAxis.ActualMaximum, axis.ActualMaximum);
        }
    }

    public void TimeAxisZoom(double min, double max)
    {
        bool timeAxisIsZoomEnabled = _timeAxis.IsZoomEnabled;

        _timeAxis.IsZoomEnabled = true;
        _timeAxis.Zoom(min, max);
        InvalidatePlot();
        _timeAxis.IsZoomEnabled = timeAxisIsZoomEnabled;
    }

    public void AutoscaleAllYAxes()
    {
        // K26K: Zoom() is ignored while "Enable Zoom" is off, so "Autoscale All" did nothing then.
        // Reset() always returns an axis to autoscaling.
        foreach (LinearAxis axis in _axes.Values)
            axis.Reset();

        _userZoomedAxes.Clear();
        InvalidatePlot();
    }
}
