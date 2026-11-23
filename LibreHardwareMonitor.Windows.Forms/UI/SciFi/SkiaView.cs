// K26K Hardware Monitor - lightweight SkiaSharp control for WinForms.
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
using System.Drawing.Imaging;
using System.Windows.Forms;
using SkiaSharp;

namespace LibreHardwareMonitor.Windows.Forms.UI.SciFi;

/// <summary>
/// Renders with SkiaSharp into a GDI bitmap (CPU raster). No OpenGL, so it works over remote desktop,
/// on ARM64 and with window capture. Subclasses draw in <see cref="Render"/>.
/// </summary>
public abstract class SkiaView : Control
{
    private Bitmap _bitmap;

    protected SkiaView()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Opaque | ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.Selectable, false);
    }

    /// <summary>Scale factor for the current DPI (1.0 at 96 DPI).</summary>
    protected float UiScale => DeviceDpi / 96f;

    protected abstract void Render(SKCanvas canvas, int width, int height, float scale);

    protected override void OnPaint(PaintEventArgs e)
    {
        int w = ClientSize.Width, h = ClientSize.Height;
        if (w <= 0 || h <= 0)
            return;

        if (_bitmap == null || _bitmap.Width != w || _bitmap.Height != h)
        {
            _bitmap?.Dispose();
            _bitmap = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
        }

        // only the invalidated part is rendered and copied (animations invalidate a small area)
        Rectangle clip = Rectangle.Intersect(e.ClipRectangle, new Rectangle(0, 0, w, h));
        if (clip.Width <= 0 || clip.Height <= 0)
            return;

        BitmapData data = _bitmap.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadWrite, PixelFormat.Format32bppPArgb);
        try
        {
            var info = new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul);
            using SKSurface surface = SKSurface.Create(info, data.Scan0, data.Stride);
            surface.Canvas.ClipRect(new SKRect(clip.Left, clip.Top, clip.Right, clip.Bottom));
            Render(surface.Canvas, w, h, UiScale);
            surface.Canvas.Flush();
        }
        finally
        {
            _bitmap.UnlockBits(data);
        }

        e.Graphics.DrawImage(_bitmap, clip, clip, GraphicsUnit.Pixel);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _bitmap?.Dispose();
        base.Dispose(disposing);
    }
}
