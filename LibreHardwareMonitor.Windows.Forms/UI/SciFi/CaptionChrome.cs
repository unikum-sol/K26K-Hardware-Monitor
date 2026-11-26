// K26K Hardware Monitor - own window buttons (minimize, maximize, close) for the Sci-Fi and Fancy designs.
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
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SkiaSharp;

namespace LibreHardwareMonitor.Windows.Forms.UI.SciFi;

public enum WindowButton
{
    Minimize,
    Maximize,
    Close
}

/// <summary>Colors of the window buttons in one design.</summary>
public sealed class CaptionPalette
{
    public SKColor Glyph { get; set; }
    public SKColor GlyphInactive { get; set; }
    public SKColor Accent { get; set; }
    public SKColor Alarm { get; set; }
    public SKColor Background { get; set; }
    public SKColor Rule { get; set; }

    /// <summary>Rounded pill buttons in flat colors (Bridge) instead of thin glyphs in cells.</summary>
    public bool Pills { get; set; }
    public SKColor PillMinimize { get; set; }
    public SKColor PillMaximize { get; set; }
    public SKColor PillClose { get; set; }
    public SKColor PillInactive { get; set; }

    /// <summary>The palette of the active design, used by the window buttons in the menu row.</summary>
    public static CaptionPalette Current { get; set; }

    public static readonly CaptionPalette SciFi = new()
    {
        Glyph = Fui.Hex("#00C8D0"),
        GlyphInactive = Fui.Hex("#6E8798"),
        Accent = Fui.Hex("#00E0E8"),
        Alarm = Fui.Hex("#FF40A0"),
        Background = Fui.Hex("#050810"),
        Rule = Fui.Hex("#2C4A5C")
    };

    static CaptionPalette()
    {
        Current = SciFi;
    }
}

/// <summary>Draws the window buttons and sends their commands to the window.</summary>
public static class CaptionChrome
{
    /// <summary>Size of one button at 96 DPI (the size of the Windows buttons).</summary>
    public const float ButtonWidth = 46, ButtonHeight = 30;

    public static string ToolTip(WindowButton kind, bool maximized) => kind switch
    {
        WindowButton.Minimize => "Minimize",
        WindowButton.Maximize => maximized ? "Restore Down" : "Maximize",
        _ => "Close"
    };

    /// <summary>Draws one button: a thin glyph, on hover a tinted cell with an accent line (close: the alarm color).</summary>
    public static void Draw(SKCanvas c, SKRect r, WindowButton kind, bool maximized, bool hot, bool pressed, bool active, CaptionPalette p, float k)
    {
        if (p.Pills)
        {
            DrawPill(c, r, kind, maximized, hot, pressed, active, p, k);
            return;
        }

        bool close = kind == WindowButton.Close;
        SKColor glyph = active ? p.Glyph : p.GlyphInactive;
        if (hot || pressed)
        {
            SKColor tone = close ? p.Alarm : p.Accent;
            using (SKPaint fill = Fui.Fill(close ? tone.WithAlpha(pressed ? 0.75f : 1f) : tone.WithAlpha(pressed ? 0.26f : 0.14f)))
                c.DrawRect(r, fill);
            if (!close)
            {
                using SKPaint line = Fui.Fill(tone);
                c.DrawRect(new SKRect(r.Left, r.Bottom - 2 * k, r.Right, r.Bottom), line);
            }
            glyph = close ? p.Background : p.Accent;
        }

        // crisp one pixel lines: centers on half pixels
        float cx = (float)Math.Floor(r.MidX) + 0.5f;
        float cy = (float)Math.Floor(r.MidY) + 0.5f;
        float s = (float)Math.Round(5 * k);
        float w = Math.Max(1, (float)Math.Round(k));
        using SKPaint pen = Fui.Stroke(glyph, w);
        pen.StrokeCap = SKStrokeCap.Square;

        switch (kind)
        {
            case WindowButton.Minimize:
                c.DrawLine(cx - s, cy, cx + s, cy, pen);
                break;
            case WindowButton.Maximize when maximized:
            {
                // restore: the front frame with corner brackets, the back frame as an offset edge
                float o = (float)Math.Round(2 * k);
                var front = new SKRect(cx - s, cy - s + o, cx + s - o, cy + s);
                using (SKPaint faint = Fui.Stroke(glyph.WithAlpha(0.55f), w))
                    c.DrawRect(front, faint);
                Fui.Brackets(c, front, 3 * k, glyph, w);
                c.DrawLine(cx - s + o, cy - s, cx + s, cy - s, pen);
                c.DrawLine(cx + s, cy - s, cx + s, cy + s - o, pen);
                break;
            }
            case WindowButton.Maximize:
            {
                // maximize: a frame with emphasized corner brackets, the motif of the panels
                var frame = new SKRect(cx - s, cy - s, cx + s, cy + s);
                using (SKPaint faint = Fui.Stroke(glyph.WithAlpha(0.55f), w))
                    c.DrawRect(frame, faint);
                Fui.Brackets(c, frame, 3.5f * k, glyph, w);
                break;
            }
            default:
                pen.StrokeCap = SKStrokeCap.Round;
                c.DrawLine(cx - s, cy - s, cx + s, cy + s, pen);
                c.DrawLine(cx - s, cy + s, cx + s, cy - s, pen);
                break;
        }
    }

    /// <summary>A flat rounded pill in the button's own color with a dark glyph; brighter on hover, darker when pressed.</summary>
    private static void DrawPill(SKCanvas c, SKRect r, WindowButton kind, bool maximized, bool hot, bool pressed, bool active, CaptionPalette p, float k)
    {
        SKColor tone = !active && !hot ? p.PillInactive : kind switch
        {
            WindowButton.Minimize => p.PillMinimize,
            WindowButton.Maximize => p.PillMaximize,
            _ => p.PillClose
        };
        if (pressed)
            tone = Fui.Mix(tone, SKColors.Black, 0.25f);
        else if (hot)
            tone = Fui.Mix(tone, SKColors.White, 0.3f);

        var pill = new SKRect(r.Left + 3 * k, r.Top + 5 * k, r.Right - 3 * k, r.Bottom - 5 * k);
        float radius = pill.Height / 2;
        using (SKPaint fill = Fui.Fill(tone))
            c.DrawRoundRect(pill, radius, radius, fill);

        float cx = (float)Math.Floor(pill.MidX) + 0.5f;
        float cy = (float)Math.Floor(pill.MidY) + 0.5f;
        float s = (float)Math.Round(4 * k);
        using SKPaint pen = Fui.Stroke(p.Background, Math.Max(1.5f, (float)Math.Round(3 * k) / 2));
        pen.StrokeCap = SKStrokeCap.Square;
        switch (kind)
        {
            case WindowButton.Minimize:
                c.DrawLine(cx - s, cy + s * 0.5f, cx + s, cy + s * 0.5f, pen);
                break;
            case WindowButton.Maximize when maximized:
            {
                float o = (float)Math.Round(2 * k);
                c.DrawRect(new SKRect(cx - s, cy - s + o, cx + s - o, cy + s), pen);
                c.DrawLine(cx - s + o, cy - s, cx + s, cy - s, pen);
                c.DrawLine(cx + s, cy - s, cx + s, cy + s - o, pen);
                break;
            }
            case WindowButton.Maximize:
                c.DrawRect(new SKRect(cx - s, cy - s, cx + s, cy + s), pen);
                break;
            default:
                pen.StrokeCap = SKStrokeCap.Round;
                c.DrawLine(cx - s, cy - s, cx + s, cy + s, pen);
                c.DrawLine(cx - s, cy + s, cx + s, cy - s, pen);
                break;
        }
    }

    /// <summary>
    /// Runs the command like the Windows buttons do (WM_SYSCOMMAND), so minimize to tray
    /// and minimize on close keep working.
    /// </summary>
    public static void Execute(Form form, WindowButton kind)
    {
        if (form == null || !form.IsHandleCreated)
            return;

        const int WM_SYSCOMMAND = 0x0112;
        const int SC_MINIMIZE = 0xF020, SC_MAXIMIZE = 0xF030, SC_RESTORE = 0xF120, SC_CLOSE = 0xF060;
        int command = kind switch
        {
            WindowButton.Minimize => SC_MINIMIZE,
            WindowButton.Maximize => form.WindowState == FormWindowState.Maximized ? SC_RESTORE : SC_MAXIMIZE,
            _ => SC_CLOSE
        };
        PostMessage(form.Handle, WM_SYSCOMMAND, (IntPtr)command, IntPtr.Zero);
    }

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);
}

/// <summary>A window button at the right end of the Sci-Fi menu row.</summary>
internal sealed class CaptionButtonItem : ToolStripItem
{
    private bool _hot;
    private bool _pressed;
    private bool _maximized;
    private bool _active = true;

    public CaptionButtonItem(WindowButton kind)
    {
        Kind = kind;
        Alignment = ToolStripItemAlignment.Right;
        AutoSize = false;
        Overflow = ToolStripItemOverflow.Never;
        Margin = Padding.Empty;
        Padding = Padding.Empty;
        AccessibleName = CaptionChrome.ToolTip(kind, false);
        ToolTipText = AccessibleName;
        Name = "caption" + kind;
    }

    public WindowButton Kind { get; }

    public override bool CanSelect => false;

    public void SetWindowState(bool maximized, bool active)
    {
        if (maximized == _maximized && active == _active)
            return;
        _maximized = maximized;
        _active = active;
        ToolTipText = AccessibleName = CaptionChrome.ToolTip(Kind, maximized);
        Invalidate();
    }

    public void SetScale(int dpi)
    {
        Size = new Size((int)Math.Round(CaptionChrome.ButtonWidth * dpi / 96f), (int)Math.Round(CaptionChrome.ButtonHeight * dpi / 96f));
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hot = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hot = false;
        _pressed = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            _pressed = true;
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _pressed = false;
        Invalidate();
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        _hot = false;
        CaptionChrome.Execute(Owner?.FindForm(), Kind);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        int w = Width, h = Height;
        if (w <= 0 || h <= 0)
            return;

        using var bitmap = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
        BitmapData data = bitmap.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadWrite, PixelFormat.Format32bppPArgb);
        try
        {
            using SKSurface surface = SKSurface.Create(new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul), data.Scan0, data.Stride);
            SKCanvas c = surface.Canvas;
            Color back = LibreHardwareMonitor.Windows.Forms.UI.Themes.Theme.Current.MenuBackgroundColor;
            c.Clear(new SKColor(back.R, back.G, back.B));
            float k = (Owner?.DeviceDpi ?? 96) / 96f;
            CaptionPalette palette = CaptionPalette.Current;
            CaptionChrome.Draw(c, new SKRect(0, 0, w, h), Kind, _maximized, _hot, _pressed, _active, palette, k);
            // a short rule in front of the group separates it from the menu
            if (Kind == WindowButton.Minimize && !_hot && !_pressed && !palette.Pills)
            {
                using SKPaint sep = Fui.Stroke(palette.Rule, Math.Max(1, (float)Math.Round(k)));
                c.DrawLine(0.5f, h * 0.25f, 0.5f, h * 0.75f, sep);
            }
            c.Flush();
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
        e.Graphics.DrawImageUnscaled(bitmap, 0, 0);
    }
}

/// <summary>
/// Lets a child control hand mouse hit tests to the main window (HTTRANSPARENT), so the window
/// can answer with "caption" (drag, double click, snap) or a resize edge.
/// </summary>
internal sealed class HitTestPassThrough : NativeWindow
{
    private const int WM_NCHITTEST = 0x0084;
    private const int HTTRANSPARENT = -1;

    private readonly Control _control;
    private readonly Func<Point, bool> _passThrough;

    private HitTestPassThrough(Control control, Func<Point, bool> passThrough)
    {
        _control = control;
        _passThrough = passThrough;
        if (control.IsHandleCreated)
            AssignHandle(control.Handle);
        control.HandleCreated += (s, e) => AssignHandle(_control.Handle);
        control.HandleDestroyed += (s, e) => ReleaseHandle();
    }

    /// <param name="passThrough">Gets a point in client coordinates of the control; true hands the hit test to the window below.</param>
    public static void Attach(Control control, Func<Point, bool> passThrough) => new HitTestPassThrough(control, passThrough);

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_NCHITTEST)
        {
            long lp = m.LParam.ToInt64();
            var screen = new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF));
            if (_passThrough(_control.PointToClient(screen)))
            {
                m.Result = (IntPtr)HTTRANSPARENT;
                return;
            }
        }
        base.WndProc(ref m);
    }
}
