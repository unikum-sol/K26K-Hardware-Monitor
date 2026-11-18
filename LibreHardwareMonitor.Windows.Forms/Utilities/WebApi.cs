// K26K Hardware Monitor - web API for the mobile web interface.
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
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Windows.Forms.UI;
using LibreHardwareMonitor.Windows.Forms.UI.Themes;

namespace LibreHardwareMonitor.Windows.Forms.Utilities;

/// <summary>
/// Read-only view of the desktop settings that the web interface mirrors.
/// The main form fills these delegates; they are read from the HTTP threads.
/// </summary>
public sealed class WebUiContext
{
    public Func<bool> ShowHiddenSensors { get; set; } = () => false;
    public Func<bool> ShowValueColumn { get; set; } = () => true;
    public Func<bool> ShowMinColumn { get; set; } = () => false;
    public Func<bool> ShowMaxColumn { get; set; } = () => true;
    public Func<bool> PlotVisible { get; set; } = () => false;
    public Func<bool> StackedAxes { get; set; } = () => true;
    public Func<double> PlotTimeSpanSeconds { get; set; } = () => 600;
    public Func<double> PlotStrokeThickness { get; set; } = () => 2;
    public Func<int> UpdateIntervalMs { get; set; } = () => 1000;
    public Func<IDictionary<ISensor, Color>> PlotColors { get; set; } = () => new Dictionary<ISensor, Color>();
    public UnitManager UnitManager { get; set; }
}

/// <summary>
/// JSON endpoints under /api/ used by the mobile web interface:
///   GET /api/state                          tree, current values and mirrored settings
///   GET /api/history?ids=a,b&amp;span=600&amp;points=300   value history for the given sensors
/// </summary>
public sealed class WebApi
{
    private const int MaxPoints = 2000;
    private const int MaxSpanSeconds = 24 * 60 * 60;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly Node _root;
    private readonly WebUiContext _context;
    private readonly Version _version = typeof(WebApi).Assembly.GetName().Version;

    public WebApi(Node root, WebUiContext context)
    {
        _root = root;
        _context = context;
    }

    public static bool IsApiRequest(string requestedFile)
    {
        return requestedFile.StartsWith("api/", StringComparison.OrdinalIgnoreCase);
    }

    public async Task HandleAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        string path = request.Url.AbsolutePath.TrimEnd('/').ToLowerInvariant();
        object result;
        int status = 200;

        try
        {
            switch (path)
            {
                case "/api/state":
                    result = Retry(BuildState);
                    break;
                case "/api/history":
                    result = Retry(() => BuildHistory(request));
                    break;
                default:
                    status = 404;
                    result = new Dictionary<string, object> { ["error"] = "Unknown endpoint" };
                    break;
            }
        }
        catch (Exception e)
        {
            status = 500;
            result = new Dictionary<string, object> { ["error"] = e.Message };
        }

        await SendJsonAsync(request, response, status, result);
    }

    // The sensor tree and the value lists are changed by the UI and update threads.
    // A read that collides with such a change is simply repeated.
    private static T Retry<T>(Func<T> build)
    {
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                return build();
            }
            catch (Exception e) when (attempt < 4 && (e is InvalidOperationException || e is ArgumentException || e is IndexOutOfRangeException))
            {
                System.Threading.Thread.Sleep(5);
            }
        }
    }

    private Dictionary<string, object> BuildState()
    {
        IDictionary<ISensor, Color> plotColors = _context.PlotColors() ?? new Dictionary<ISensor, Color>();
        bool fahrenheit = _context.UnitManager?.TemperatureUnit == TemperatureUnit.Fahrenheit;

        return new Dictionary<string, object>
        {
            ["app"] = "K26K Hardware Monitor",
            ["version"] = $"{_version.Major}.{_version.Minor}.{_version.Build}",
            ["machine"] = _root.Text,
            ["time"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ["settings"] = new Dictionary<string, object>
            {
                ["theme"] = BuildTheme(Theme.Current),
                ["temperatureUnit"] = fahrenheit ? "F" : "C",
                ["showHidden"] = _context.ShowHiddenSensors(),
                ["columns"] = new Dictionary<string, object>
                {
                    ["value"] = _context.ShowValueColumn(),
                    ["min"] = _context.ShowMinColumn(),
                    ["max"] = _context.ShowMaxColumn()
                },
                ["plot"] = new Dictionary<string, object>
                {
                    ["visible"] = _context.PlotVisible(),
                    ["stacked"] = _context.StackedAxes(),
                    ["timeSpan"] = Finite(_context.PlotTimeSpanSeconds()),
                    ["stroke"] = _context.PlotStrokeThickness()
                },
                ["updateInterval"] = _context.UpdateIntervalMs()
            },
            ["nodes"] = _root.Nodes.ToList().Select(n => BuildNode(n, plotColors, fahrenheit)).Where(n => n != null).ToList()
        };
    }

    private Dictionary<string, object> BuildNode(Node node, IDictionary<ISensor, Color> plotColors, bool fahrenheit)
    {
        var json = new Dictionary<string, object>
        {
            ["name"] = node.Text,
            ["visible"] = node.IsVisible
        };

        if (node is IExpandPersistNode expandable)
            json["expanded"] = expandable.Expanded;

        switch (node)
        {
            case SensorNode sensorNode:
                {
                    ISensor sensor = sensorNode.Sensor;
                    json["kind"] = "sensor";
                    json["id"] = sensor.Identifier.ToString();
                    json["sensorType"] = sensor.SensorType.ToString();
                    json["typeIndex"] = (int)sensor.SensorType;
                    json["unit"] = GetUnit(sensor.SensorType, fahrenheit);
                    json["value"] = Finite(ToDisplayNumber(sensor.SensorType, sensor.Value, fahrenheit));
                    json["min"] = Finite(ToDisplayNumber(sensor.SensorType, sensor.Min, fahrenheit));
                    json["max"] = Finite(ToDisplayNumber(sensor.SensorType, sensor.Max, fahrenheit));
                    json["text"] = new Dictionary<string, object>
                    {
                        ["value"] = sensorNode.Value,
                        ["min"] = sensorNode.Min,
                        ["max"] = sensorNode.Max
                    };
                    json["plot"] = sensorNode.Plot;
                    if (plotColors.TryGetValue(sensor, out Color color))
                        json["color"] = ToHex(color);
                    else if (sensorNode.PenColor.HasValue)
                        json["color"] = ToHex(sensorNode.PenColor.Value);

                    return json;
                }
            case HardwareNode hardwareNode:
                json["kind"] = "hardware";
                json["id"] = hardwareNode.Hardware.Identifier.ToString();
                json["hardwareType"] = hardwareNode.Hardware.HardwareType.ToString();
                break;
            case TypeNode typeNode:
                json["kind"] = "group";
                json["sensorType"] = typeNode.SensorType.ToString();
                break;
            default:
                json["kind"] = "node";
                break;
        }

        json["children"] = node.Nodes.ToList().Select(n => BuildNode(n, plotColors, fahrenheit)).Where(n => n != null).ToList();
        return json;
    }

    private Dictionary<string, object> BuildHistory(HttpListenerRequest request)
    {
        var query = HttpUtility.ParseQueryString(request.Url.Query);
        string[] ids = (query["ids"] ?? string.Empty).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        int span = ParseInt(query["span"], 600, 10, MaxSpanSeconds);
        int points = ParseInt(query["points"], 300, 10, MaxPoints);
        bool fahrenheit = _context.UnitManager?.TemperatureUnit == TemperatureUnit.Fahrenheit;

        Dictionary<string, SensorNode> sensors = new();
        CollectSensors(_root, sensors);

        DateTime now = DateTime.UtcNow;
        var series = new Dictionary<string, object>();

        foreach (string id in ids.Distinct().Take(64))
        {
            if (!sensors.TryGetValue(id, out SensorNode sensorNode))
                continue;

            ISensor sensor = sensorNode.Sensor;
            SensorValue[] values = sensor.Values.ToArray();
            series[id] = Downsample(values, sensor.SensorType, now, span, points, fahrenheit);
        }

        return new Dictionary<string, object>
        {
            ["time"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ["span"] = span,
            ["series"] = series
        };
    }

    private static void CollectSensors(Node node, Dictionary<string, SensorNode> sensors)
    {
        foreach (Node child in node.Nodes.ToList())
        {
            if (child is SensorNode sensorNode)
                sensors[sensorNode.Sensor.Identifier.ToString()] = sensorNode;
            else
                CollectSensors(child, sensors);
        }
    }

    // Returns [[secondsAgo, value], ...] from oldest to newest, averaged into at most `points` time buckets.
    private static List<double[]> Downsample(SensorValue[] values, SensorType type, DateTime now, int span, int points, bool fahrenheit)
    {
        var result = new List<double[]>();
        double bucketSize = (double)span / points;

        int bucket = -1;
        double sum = 0, timeSum = 0;
        int count = 0;

        void Flush()
        {
            if (count > 0)
                result.Add(new[] { Math.Round(timeSum / count, 2), Math.Round(sum / count, 4) });

            sum = timeSum = 0;
            count = 0;
        }

        float? before = null;

        foreach (SensorValue value in values)
        {
            double ago = (now - value.Time).TotalSeconds;
            float? display = ToDisplayNumber(type, value.Value, fahrenheit);
            if (!display.HasValue || float.IsNaN(display.Value) || float.IsInfinity(display.Value))
                continue;

            if (ago > span)
            {
                // remember the last value before the window, so the curve starts at the left edge
                before = display;
                continue;
            }

            if (before.HasValue)
            {
                result.Add(new[] { (double)span, Math.Round((double)before.Value, 4) });
                before = null;
            }

            int b = (int)((span - Math.Max(0, ago)) / bucketSize);
            if (b != bucket)
            {
                Flush();
                bucket = b;
            }

            sum += display.Value;
            timeSum += Math.Max(0, ago);
            count++;
        }

        Flush();
        return result;
    }

    private static float? ToDisplayNumber(SensorType type, float? value, bool fahrenheit)
    {
        if (!value.HasValue)
            return null;

        return type switch
        {
            SensorType.Temperature when fahrenheit => UnitManager.CelsiusToFahrenheit(value),
            SensorType.Data => UnitManager.BytesToGigaBytes(value),
            _ => value
        };
    }

    private static string GetUnit(SensorType type, bool fahrenheit)
    {
        return type switch
        {
            SensorType.Voltage => "V",
            SensorType.Current => "A",
            SensorType.Clock => "MHz",
            SensorType.Temperature => fahrenheit ? "°F" : "°C",
            SensorType.Load => "%",
            SensorType.Fan => "RPM",
            SensorType.Flow => "L/h",
            SensorType.Control => "%",
            SensorType.Level => "%",
            SensorType.Factor => "",
            SensorType.Power => "W",
            SensorType.Data => "GB",
            SensorType.Frequency => "Hz",
            SensorType.Throughput => "B/s",
            SensorType.TimeSpan => "s",
            SensorType.Timing => "ns",
            SensorType.Energy => "mWh",
            SensorType.Noise => "dBA",
            SensorType.Conductivity => "µS/cm",
            SensorType.Humidity => "%",
            _ => ""
        };
    }

    private static Dictionary<string, object> BuildTheme(Theme theme)
    {
        return new Dictionary<string, object>
        {
            ["id"] = theme.Id,
            ["name"] = theme.DisplayName,
            ["dark"] = theme.BackgroundColor.GetBrightness() < 0.5f,
            ["background"] = ToHex(theme.BackgroundColor),
            ["foreground"] = ToHex(theme.ForegroundColor),
            ["line"] = ToHex(theme.LineColor),
            ["strongLine"] = ToHex(theme.StrongLineColor),
            ["selectedBackground"] = ToHex(theme.SelectedBackgroundColor),
            ["hyperlink"] = ToHex(theme.HyperlinkColor),
            ["plotBackground"] = ToHex(theme.PlotBackgroundColor),
            ["plotText"] = ToHex(theme.PlotTextColor),
            ["plotBorder"] = ToHex(theme.PlotBorderColor),
            ["plotGridMajor"] = ToHex(theme.PlotGridMajorColor),
            ["plotGridMinor"] = ToHex(theme.PlotGridMinorColor),
            ["palette"] = theme.PlotColorPalette.Select(ToHex).ToList()
        };
    }

    private static string ToHex(Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private static object Finite(double? value)
    {
        if (!value.HasValue || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
            return null;

        return Math.Round(value.Value, 4);
    }

    private static object Finite(float? value)
    {
        return Finite(value.HasValue ? (double?)value.Value : null);
    }

    private static int ParseInt(string text, int fallback, int min, int max)
    {
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            value = fallback;

        return Math.Max(min, Math.Min(max, value));
    }

    private static async Task SendJsonAsync(HttpListenerRequest request, HttpListenerResponse response, int status, object content)
    {
        byte[] buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(content, JsonOptions));

        bool acceptGzip = request.Headers["Accept-Encoding"]?.IndexOf("gzip", StringComparison.OrdinalIgnoreCase) >= 0;

        response.StatusCode = status;
        response.ContentType = "application/json; charset=utf-8";
        response.AddHeader("Cache-Control", "no-store");

        try
        {
            if (acceptGzip)
            {
                response.AddHeader("Content-Encoding", "gzip");
                using var ms = new MemoryStream();
                using (var zip = new GZipStream(ms, CompressionLevel.Fastest, true))
                    await zip.WriteAsync(buffer, 0, buffer.Length);

                buffer = ms.ToArray();
            }

            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }
        catch (HttpListenerException)
        { }
        catch (InvalidOperationException)
        { }
    }
}
