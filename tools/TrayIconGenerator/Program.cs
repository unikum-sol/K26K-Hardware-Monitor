// K26K Hardware Monitor - generator for the tray icon (Resources/smallicon.ico).
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
//
// Usage: dotnet run -c Release -- <output folder>
// Writes k26k.ico (16 to 256 px, each size drawn on its own), the single PNGs and preview.png;
// copy k26k.ico to LibreHardwareMonitor.Windows.Forms/Resources/smallicon.ico.

using SkiaSharp;

// K26K tray icon: the diamond with its core from the Sci-Fi header on a dark plate.
// Every size is drawn on its own (no scaling), so the small tray sizes stay sharp.
string outDir = args.Length > 0 ? args[0] : ".";
int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 256 };

SKColor Hex(string h) => SKColor.Parse(h);
SKColor cyan = Hex("#00E0E8"), mint = Hex("#6BE8A4"), ice = Hex("#E8FFFF");

var images = new List<(int Size, SKBitmap Bitmap)>();
foreach (int s in sizes)
{
    var bmp = new SKBitmap(new SKImageInfo(s, s, SKColorType.Bgra8888, SKAlphaType.Unpremul));
    using (var surface = SKSurface.Create(new SKImageInfo(s, s, SKColorType.Bgra8888, SKAlphaType.Premul)))
    {
        Draw(surface.Canvas, s);
        using SKImage img = surface.Snapshot();
        img.ReadPixels(bmp.Info, bmp.GetPixels(), bmp.RowBytes, 0, 0);
    }
    images.Add((s, bmp));
    using var png = SKImage.FromBitmap(bmp).Encode(SKEncodedImageFormat.Png, 100);
    File.WriteAllBytes(Path.Combine(outDir, $"k26k-{s}.png"), png.ToArray());
}
WriteIco(Path.Combine(outDir, "k26k.ico"), images);
Preview(Path.Combine(outDir, "preview.png"), images);
Console.WriteLine("ok");

void Draw(SKCanvas c, int s)
{
    c.Clear(SKColors.Transparent);
    float k = s / 32f;
    bool small = s <= 24;
    var plate = new SKRect(0.5f, 0.5f, s - 0.5f, s - 0.5f);
    float radius = Math.Max(2.5f, s * 0.2f);

    // plate: deep blue-black with a faint teal top
    using (var fill = new SKPaint { IsAntialias = true, Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(0, s), new[] { Hex("#0F2A36"), Hex("#060D14"), Hex("#03060A") }, new[] { 0f, 0.55f, 1f }, SKShaderTileMode.Clamp) })
        c.DrawRoundRect(plate, radius, radius, fill);
    using (var border = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = Math.Max(1f, k), Color = small ? Hex("#2C5A6C") : Hex("#24505F") })
        c.DrawRoundRect(SKRect.Inflate(plate, -border.StrokeWidth / 2 + 0.5f, -border.StrokeWidth / 2 + 0.5f), radius, radius, border);

    float cx = s / 2f, cy = s / 2f;
    float r = s * (small ? 0.37f : 0.33f);

    if (!small)
    {
        // soft glow behind the core
        using var glow = new SKPaint { IsAntialias = true, Shader = SKShader.CreateRadialGradient(new SKPoint(cx, cy), r * 1.25f, new[] { cyan.WithAlpha(110), cyan.WithAlpha(25), SKColors.Transparent }, new[] { 0f, 0.55f, 1f }, SKShaderTileMode.Clamp) };
        c.DrawCircle(cx, cy, r * 1.25f, glow);

        // orbit ring in four arcs with ticks, the motif of the Fancy dashboard
        float or = s * 0.42f;
        using var orbit = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = Math.Max(1f, 1.1f * k), Color = mint.WithAlpha(150), StrokeCap = SKStrokeCap.Round };
        var oval = new SKRect(cx - or, cy - or, cx + or, cy + or);
        for (int i = 0; i < 4; i++)
        {
            using var arc = new SKPath();
            arc.AddArc(oval, -90 + 22 + i * 90, 46);
            c.DrawPath(arc, orbit);
        }
        if (s >= 48)
        {
            using var tick = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = Math.Max(1f, k), Color = cyan.WithAlpha(170) };
            for (int i = 0; i < 4; i++)
            {
                double a = (45 + i * 90) * Math.PI / 180;
                c.DrawLine(cx + (float)Math.Cos(a) * (or - 2.5f * k), cy + (float)Math.Sin(a) * (or - 2.5f * k),
                           cx + (float)Math.Cos(a) * (or + 1.5f * k), cy + (float)Math.Sin(a) * (or + 1.5f * k), tick);
            }
        }
    }

    // the diamond: cyan to mint
    using (var diamond = new SKPath())
    {
        diamond.MoveTo(cx, cy - r);
        diamond.LineTo(cx + r, cy);
        diamond.LineTo(cx, cy + r);
        diamond.LineTo(cx - r, cy);
        diamond.Close();
        float w = small ? Math.Max(1.25f, s / 13f) : 2.2f * k;
        if (!small)
        {
            using var halo = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = w * 2.6f, Color = cyan.WithAlpha(90), MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 1.6f * k) };
            c.DrawPath(diamond, halo);
        }
        using var stroke = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = w, StrokeJoin = SKStrokeJoin.Miter,
            Shader = SKShader.CreateLinearGradient(new SKPoint(cx - r, cy - r), new SKPoint(cx + r, cy + r), new[] { cyan, mint }, null, SKShaderTileMode.Clamp) };
        c.DrawPath(diamond, stroke);
    }

    // the core: a small filled diamond with a bright center
    float cr = r * (small ? 0.42f : 0.36f);
    using (var core = new SKPath())
    {
        core.MoveTo(cx, cy - cr);
        core.LineTo(cx + cr, cy);
        core.LineTo(cx, cy + cr);
        core.LineTo(cx - cr, cy);
        core.Close();
        using var fill = new SKPaint { IsAntialias = true, Shader = SKShader.CreateRadialGradient(new SKPoint(cx, cy), cr, new[] { ice, cyan }, new[] { 0.15f, 1f }, SKShaderTileMode.Clamp) };
        c.DrawPath(core, fill);
    }
}

void WriteIco(string file, List<(int Size, SKBitmap Bitmap)> list)
{
    var blobs = new List<byte[]>();
    foreach (var (s, bmp) in list)
    {
        if (s >= 256)
        {
            using var png = SKImage.FromBitmap(bmp).Encode(SKEncodedImageFormat.Png, 100);
            blobs.Add(png.ToArray());
            continue;
        }
        // classic DIB entry: 32 bpp bottom-up BGRA plus a 1 bpp AND mask
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write(40); w.Write(s); w.Write(s * 2); w.Write((short)1); w.Write((short)32); w.Write(0);
        int maskStride = ((s + 31) / 32) * 4;
        w.Write(s * s * 4 + maskStride * s); w.Write(0); w.Write(0); w.Write(0); w.Write(0);
        for (int y = s - 1; y >= 0; y--)
            for (int x = 0; x < s; x++)
            {
                SKColor p = bmp.GetPixel(x, y);
                w.Write(p.Blue); w.Write(p.Green); w.Write(p.Red); w.Write(p.Alpha);
            }
        for (int y = s - 1; y >= 0; y--)
        {
            var row = new byte[maskStride];
            for (int x = 0; x < s; x++)
                if (bmp.GetPixel(x, y).Alpha == 0)
                    row[x / 8] |= (byte)(0x80 >> (x % 8));
            w.Write(row);
        }
        w.Flush();
        blobs.Add(ms.ToArray());
    }

    using var f = new BinaryWriter(File.Create(file));
    f.Write((short)0); f.Write((short)1); f.Write((short)list.Count);
    int offset = 6 + 16 * list.Count;
    for (int i = 0; i < list.Count; i++)
    {
        int s = list[i].Size;
        f.Write((byte)(s >= 256 ? 0 : s)); f.Write((byte)(s >= 256 ? 0 : s)); f.Write((byte)0); f.Write((byte)0);
        f.Write((short)1); f.Write((short)32); f.Write(blobs[i].Length); f.Write(offset);
        offset += blobs[i].Length;
    }
    foreach (byte[] b in blobs)
        f.Write(b);
}

void Preview(string file, List<(int Size, SKBitmap Bitmap)> list)
{
    int width = 900, height = 430;
    using var surface = SKSurface.Create(new SKImageInfo(width, height));
    SKCanvas c = surface.Canvas;
    c.Clear(Hex("#1F1F1F"));
    using (var light = new SKPaint { Color = Hex("#EDEDED") })
        c.DrawRect(0, 70, width, 70, light);
    using var label = new SKPaint { Color = SKColors.Gray, IsAntialias = true };
    using var font = new SKFont(SKTypeface.Default, 12);
    float x = 12;
    foreach (var (s, bmp) in list.Where(l => l.Size <= 64))
    {
        c.DrawBitmap(bmp, x, 35 - s / 2f);
        c.DrawBitmap(bmp, x, 105 - s / 2f);
        c.DrawText(s + " px", x, 160, font, label);
        x += Math.Max(s, 40) + 24;
    }
    // zoomed: 16, 24, 32 at 8x / 6x / 4x without smoothing
    float zx = 12;
    foreach (var (s, zoom) in new[] { (16, 8), (24, 6), (32, 4) })
    {
        SKBitmap bmp = list.First(l => l.Size == s).Bitmap;
        using var zimg = SKImage.FromBitmap(bmp);
        c.DrawImage(zimg, new SKRect(zx, 180, zx + s * zoom, 180 + s * zoom), new SKSamplingOptions(SKFilterMode.Nearest));
        zx += s * zoom + 30;
    }
    c.DrawBitmap(list.First(l => l.Size == 256).Bitmap, new SKRect(width - 250, 175, width - 10, 415));
    using var img = surface.Snapshot();
    using var png = img.Encode(SKEncodedImageFormat.Png, 100);
    File.WriteAllBytes(file, png.ToArray());
}
