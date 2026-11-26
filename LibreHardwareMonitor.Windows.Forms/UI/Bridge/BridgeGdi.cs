// K26K Hardware Monitor - the Bridge shapes for GDI+ (the tree and the panel frames).
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

namespace LibreHardwareMonitor.Windows.Forms.UI.Bridge;

/// <summary>The same elbows and blocks as <see cref="BridgeDraw"/>, as GDI+ paths.</summary>
public static class BridgeGdi
{
    /// <summary>An elbow in one corner of <paramref name="r"/>: an arm along the side and a bar along the top or bottom.</summary>
    public static GraphicsPath Elbow(RectangleF r, float arm, float bar, Corner corner, float outer, float inner)
    {
        arm = Math.Min(arm, r.Width);
        bar = Math.Min(bar, r.Height);
        float ro = Math.Max(1, Math.Min(outer, Math.Min(r.Width, r.Height)));
        float ri = Math.Max(0, Math.Min(inner, Math.Min(r.Width - arm, r.Height - bar)));
        float w = r.Width, h = r.Height;

        var p = new GraphicsPath();
        p.AddLine(0, h, 0, ro);
        p.AddArc(0, 0, 2 * ro, 2 * ro, 180, 90);
        p.AddLine(ro, 0, w, 0);
        p.AddLine(w, 0, w, bar);
        p.AddLine(w, bar, arm + ri, bar);
        if (ri > 0.5f)
            p.AddArc(arm, bar, 2 * ri, 2 * ri, 270, -90);
        p.AddLine(arm, bar + ri, arm, h);
        p.CloseFigure();

        bool right = corner is Corner.TopRight or Corner.BottomRight;
        bool bottom = corner is Corner.BottomLeft or Corner.BottomRight;
        using var m = new Matrix(right ? -1 : 1, 0, 0, bottom ? -1 : 1, right ? r.Right : r.Left, bottom ? r.Bottom : r.Top);
        p.Transform(m);
        return p;
    }

    /// <summary>A block with an optionally rounded left or right end.</summary>
    public static GraphicsPath Block(RectangleF r, bool roundLeft, bool roundRight)
    {
        var p = new GraphicsPath();
        float d = Math.Min(r.Height, r.Width);
        if (roundLeft)
            p.AddArc(r.Left, r.Top, d, d, 90, 180);
        else
            p.AddLine(r.Left, r.Bottom, r.Left, r.Top);
        if (roundRight)
            p.AddArc(r.Right - d, r.Top, d, d, 270, 180);
        else
        {
            p.AddLine(r.Right, r.Top, r.Right, r.Bottom);
        }
        p.CloseFigure();
        return p;
    }
}
