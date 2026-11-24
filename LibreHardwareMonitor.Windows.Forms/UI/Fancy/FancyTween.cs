// K26K Hardware Monitor - smooth transitions between two data updates of the Fancy dashboard.
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
using System.Linq;
using System.Reflection;
using LibreHardwareMonitor.Windows.Forms.Telemetry;

namespace LibreHardwareMonitor.Windows.Forms.UI.Fancy;

/// <summary>
/// The sensors still update once per interval; the dashboard only shows the way from the old
/// to the new value: every number, bar and ring glides there with an ease in and out curve.
/// </summary>
public static class FancyTween
{
    /// <summary>Length of one transition in seconds (at a 1 s update interval).</summary>
    public const double Duration = 0.45;

    private static readonly PropertyInfo[] Numbers = typeof(DashboardModel).GetProperties()
        .Where(p => p.PropertyType == typeof(double) && p.CanWrite && p.Name != nameof(DashboardModel.SessionSeconds))
        .ToArray();

    /// <summary>Ease in and out (cubic): calm start, calm arrival.</summary>
    public static double Ease(double x)
    {
        if (x <= 0)
            return 0;
        if (x >= 1)
            return 1;
        return x < 0.5 ? 4 * x * x * x : 1 - Math.Pow(-2 * x + 2, 3) / 2;
    }

    /// <summary>A missing value (NaN) does not glide: it shows the new state at once.</summary>
    public static double Lerp(double a, double b, double p)
    {
        if (p >= 1 || double.IsNaN(a) || double.IsNaN(b) || double.IsInfinity(a) || double.IsInfinity(b))
            return b;
        return a + (b - a) * p;
    }

    /// <summary>
    /// The model on its way from <paramref name="a"/> to <paramref name="b"/>. <paramref name="progress"/> gets -1 for
    /// the single values and the index for list items (tiles, cores, thermals), so items can start one after another.
    /// Lists that changed their length or order take the new state at once. Histories are not blended
    /// (the charts scroll instead).
    /// </summary>
    public static DashboardModel Blend(DashboardModel a, DashboardModel b, Func<int, double> progress)
    {
        if (a == null)
            return b;
        double p = progress(-1);

        DashboardModel m = b.ShallowCopy();
        foreach (PropertyInfo prop in Numbers)
            prop.SetValue(m, Lerp((double)prop.GetValue(a), (double)prop.GetValue(b), p));
        // words and colors switch halfway, together with the numbers
        if (p < 0.5)
            m.State = a.State;

        if (a.CoreLoads.Length == b.CoreLoads.Length)
            m.CoreLoads = b.CoreLoads.Select((v, i) => Lerp(a.CoreLoads[i], v, progress(i))).ToArray();

        if (a.Tiles.Count == b.Tiles.Count && a.Tiles.Select(t => t.Kind).SequenceEqual(b.Tiles.Select(t => t.Kind)))
        {
            m.Tiles = new List<HardwareTile>(b.Tiles.Count);
            for (int i = 0; i < b.Tiles.Count; i++)
            {
                HardwareTile from = a.Tiles[i], to = b.Tiles[i];
                double q = progress(i);
                HardwareTile tile = to.Copy();
                tile.Load = Lerp(from.Load, to.Load, q);
                tile.Temp = Lerp(from.Temp, to.Temp, q);
                tile.Power = Lerp(from.Power, to.Power, q);
                if (q < 0.5)
                    tile.Detail = from.Detail;
                m.Tiles.Add(tile);
            }
        }

        if (a.Thermals.Count == b.Thermals.Count && a.Thermals.Select(t => t.Name).SequenceEqual(b.Thermals.Select(t => t.Name)))
        {
            m.Thermals = b.Thermals.Select((t, i) => new ThermalEntry { Name = t.Name, Limit = t.Limit, Temp = Lerp(a.Thermals[i].Temp, t.Temp, progress(i)) }).ToList();
        }

        return m;
    }

    /// <summary>
    /// Positions (0 = left edge, 1 = right edge) of a history that scrolls continuously: the newest value
    /// slides in from the right during one update interval instead of the whole curve jumping by one step.
    /// <paramref name="scroll"/> runs from 0 (just updated) to 1 (the next update is due).
    /// </summary>
    public static (double X, double V)[] Scroll(double[] previous, double[] next, double scroll)
    {
        int n = next.Length;
        if (n < 2)
            return next.Select(v => (1.0, v)).ToArray();

        if (previous != null && scroll < 1 && previous.Length >= 2)
        {
            // still filling up: one more point, the spacing narrows while it comes in
            if (n == previous.Length + 1 && next[0] == previous[0])
            {
                double d = n - 2 + scroll;
                return next.Select((v, i) => (i / d, v)).ToArray();
            }

            // full: everything moves left by one step; the dropped oldest point leaves on the left
            if (n == previous.Length && next[0] == previous[1] && next[n - 2] == previous[n - 1])
            {
                var points = new (double, double)[n + 1];
                points[0] = (-scroll / (n - 1), previous[0]);
                for (int i = 0; i < n; i++)
                    points[i + 1] = ((i + 1 - scroll) / (n - 1), next[i]);
                return points;
            }
        }

        return next.Select((v, i) => (i / (double)(n - 1), v)).ToArray();
    }
}
