// K26K Hardware Monitor - saving dashboard snapshots and rendering them without hardware access.
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
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using LibreHardwareMonitor.Windows.Forms.UI.Fancy;
using LibreHardwareMonitor.Windows.Forms.UI.SciFi;
using SkiaSharp;

namespace LibreHardwareMonitor.Windows.Forms.Telemetry;

public static class SnapshotJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        WriteIndented = true
    };

    public static void Save(DashboardModel model, string path)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(model, Options));
    }

    public static DashboardModel Load(string path)
    {
        return JsonSerializer.Deserialize<DashboardModel>(File.ReadAllText(path), Options);
    }

    /// <summary>
    /// Command line: --render-snapshot &lt;in.json&gt; &lt;out.png&gt; [--view fancy|header|status] [--size 1920x1080] [--t 6] [--scale 1] [--reduced-motion]
    /// Renders a saved snapshot to a PNG file. No hardware access and no administrator rights are needed.
    /// </summary>
    public static int RenderFromCommandLine(string[] args)
    {
        string input = args.Length > 1 ? args[1] : null;
        string output = args.Length > 2 ? args[2] : null;
        if (input == null || output == null)
            return 2;

        string view = Option(args, "--view", "fancy");
        string[] size = Option(args, "--size", "1920x1080").Split('x');
        int width = int.Parse(size[0], CultureInfo.InvariantCulture);
        int height = int.Parse(size[1], CultureInfo.InvariantCulture);
        double t = double.Parse(Option(args, "--t", "6"), CultureInfo.InvariantCulture);
        float scale = float.Parse(Option(args, "--scale", "1"), CultureInfo.InvariantCulture);
        bool reduced = Array.IndexOf(args, "--reduced-motion") >= 0;

        DashboardModel model = Load(input);
        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        SKCanvas canvas = surface.Canvas;

        switch (view)
        {
            case "header":
                HudHeader.Draw(canvas, width, height, scale, model, false, out _);
                break;
            case "status":
                HudStatusBar.Draw(canvas, width, height, scale, model);
                break;
            default:
                FancyRenderer.Render(canvas, width, height, scale, model, t, new FancyOptions { ReducedMotion = reduced, SinceOpen = t });
                break;
        }

        canvas.Flush();
        using SKImage image = surface.Snapshot();
        using SKData png = image.Encode(SKEncodedImageFormat.Png, 100);
        using FileStream file = File.Create(output);
        png.SaveTo(file);
        return 0;
    }

    private static string Option(string[] args, string name, string fallback)
    {
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
    }
}
