// K26K Hardware Monitor - main window without the Windows title bar in the Sci-Fi and Fancy designs.
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
using System.Runtime.InteropServices;
using System.Windows.Forms;
using LibreHardwareMonitor.Windows.Forms.UI.SciFi;

namespace LibreHardwareMonitor.Windows.Forms.UI;

// The window keeps its normal styles (caption, sizing border, system menu), so Windows still handles
// moving, snapping, the system menu, minimize and maximize animations and the taskbar. Only the frame
// is removed: WM_NCCALCSIZE makes the whole window client area, and WM_NCHITTEST tells Windows where
// the (now invisible) caption and sizing edges are. The design draws its own window buttons.
public sealed partial class MainForm
{
    private const int WM_NCCALCSIZE = 0x0083;
    private const int WM_NCHITTEST = 0x0084;
    private const int HTCAPTION = 2, HTLEFT = 10, HTRIGHT = 11, HTTOP = 12, HTTOPLEFT = 13, HTTOPRIGHT = 14;
    private const int HTBOTTOM = 15, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17;

    private const int WM_WINDOWPOSCHANGING = 0x0046, WM_WINDOWPOSCHANGED = 0x0047;

    private bool _customFrame;
    private bool _leftNormalState;
    private Size? _restoredSize;
    private CaptionButtonItem[] _captionItems;
    private Padding _classicMenuPadding;

    /// <summary>True while the window is drawn without the Windows title bar and border.</summary>
    private bool CustomFrameActive => _customFrame && FormBorderStyle != FormBorderStyle.None;

    private bool IsMaximized => IsHandleCreated ? IsZoomed(Handle) : WindowState == FormWindowState.Maximized;

    private void InitializeWindowFrame()
    {
        _classicMenuPadding = mainMenu.Padding;

        // right aligned items: the first one added is the rightmost
        _captionItems = new[]
        {
            new CaptionButtonItem(WindowButton.Close),
            new CaptionButtonItem(WindowButton.Maximize),
            new CaptionButtonItem(WindowButton.Minimize)
        };
        foreach (CaptionButtonItem item in _captionItems)
        {
            item.Visible = false;
            item.SetScale(DeviceDpi);
            mainMenu.Items.Add(item);
        }
        mainMenu.ShowItemToolTips = true;

        // empty parts of the menu row and the Sci-Fi header move the window; the outer edges resize it
        HitTestPassThrough.Attach(mainMenu, p => CustomFrameActive && (InResizeBand(mainMenu, p) || mainMenu.GetItemAt(p) == null));
        HitTestPassThrough.Attach(splitContainer, p => CustomFrameActive && InResizeBand(splitContainer, p));
        HitTestPassThrough.Attach(splitContainer.Panel1, p => CustomFrameActive && InResizeBand(splitContainer.Panel1, p));
        HitTestPassThrough.Attach(splitContainer.Panel2, p => CustomFrameActive && InResizeBand(splitContainer.Panel2, p));

        HandleCreated += delegate { ApplyWindowFrame(); };
        Activated += delegate { UpdateCaptionState(); };
        Deactivate += delegate { UpdateCaptionState(); };
        Resize += delegate { UpdateCaptionState(); };
        DpiChanged += delegate
        {
            foreach (CaptionButtonItem item in _captionItems)
                item.SetScale(DeviceDpi);
        };
    }

    /// <summary>Attaches the drag and resize handling to the header and status strip of a design and to the dashboard.</summary>
    private void AttachFrameHitTests(Control header, Func<Point, bool> isOnButton, Control status)
    {
        HitTestPassThrough.Attach(header, p => CustomFrameActive && (InResizeBand(header, p) || !isOnButton(p)));
        HitTestPassThrough.Attach(status, p => CustomFrameActive && InResizeBand(status, p));
    }

    private void AttachFrameHitTests(Fancy.FancyDashboard fancy)
    {
        HitTestPassThrough.Attach(fancy, p => CustomFrameActive && (InResizeBand(fancy, p) || fancy.IsCaptionArea(p)));
        fancy.CaptionCommand += (s, kind) => CaptionChrome.Execute(this, kind);
    }

    /// <summary>Switches between the Windows frame (Classic) and the design's own frame.</summary>
    private void SetCustomFrame(bool on)
    {
        _customFrame = on;
        _captionItems ??= Array.Empty<CaptionButtonItem>();
        foreach (CaptionButtonItem item in _captionItems)
            item.Visible = on;
        // the menu row is the caption now: no gap above and to the right of the buttons
        mainMenu.Padding = on ? new Padding(_classicMenuPadding.Left, 0, 0, 0) : _classicMenuPadding;
        ApplyWindowFrame();
    }

    private void ApplyWindowFrame()
    {
        if (!IsHandleCreated)
            return;

        // recalculate the frame (WM_NCCALCSIZE) without moving the window
        const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOZORDER = 0x0004, SWP_NOACTIVATE = 0x0010, SWP_FRAMECHANGED = 0x0020, SWP_NOOWNERZORDER = 0x0200;
        SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED | SWP_NOOWNERZORDER);
        UpdateCaptionState();
    }

    private void UpdateCaptionState()
    {
        if (_captionItems == null)
            return;
        bool maximized = IsMaximized;
        bool active = ActiveForm == this;
        foreach (CaptionButtonItem item in _captionItems)
            item.SetWindowState(maximized, active);
        _fancy?.SetCaption(CustomFrameActive || _fullscreen, !_fullscreen, maximized, active);
    }

    private int ResizeBand => Math.Max(4, (int)Math.Round(6 * DeviceDpi / 96f));

    /// <summary>True if the point (client coordinates of the control) lies on a sizing edge of the window.</summary>
    private bool InResizeBand(Control control, Point point)
    {
        if (IsMaximized)
            return false;
        Point p = PointToClient(control.PointToScreen(point));
        int band = ResizeBand;
        return p.X < band || p.Y < band || p.X >= ClientSize.Width - band || p.Y >= ClientSize.Height - band;
    }

    /// <summary>Handles the frame messages; returns true if the message was handled.</summary>
    private bool HandleWindowFrameMessage(ref Message m)
    {
        if (!CustomFrameActive)
        {
            if (m.Msg == WM_WINDOWPOSCHANGED && IsHandleCreated)
                _leftNormalState = IsZoomed(Handle) || IsIconic(Handle);
            return false;
        }

        if (m.Msg == WM_NCCALCSIZE && m.WParam != IntPtr.Zero)
        {
            // the whole window is client area; maximized, it must stay inside the work area
            // (Windows places a maximized window with its border outside the screen)
            if (IsZoomed(Handle))
            {
                var proposed = Marshal.PtrToStructure<RECT>(m.LParam);
                IntPtr monitor = MonitorFromRect(ref proposed, 2 /* MONITOR_DEFAULTTONEAREST */);
                var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                if (GetMonitorInfo(monitor, ref info))
                {
                    RECT work = info.rcWork;
                    // an auto-hide taskbar needs one free pixel to show up
                    if (work.Equals(info.rcMonitor) && TaskbarAutoHides())
                        work.Bottom -= 1;
                    Marshal.StructureToPtr(work, m.LParam, false);
                }
            }
            m.Result = IntPtr.Zero;
            return true;
        }

        if (m.Msg == WM_WINDOWPOSCHANGED)
        {
            // On Windows 10 WinForms remembers the client size when the window is maximized or minimized
            // and adds the Windows frame to it when the window is restored. Without a frame that would
            // grow the window by the frame size on each restore, so the size Windows restored is kept.
            bool normal = !IsZoomed(Handle) && !IsIconic(Handle);
            if (normal && _leftNormalState && GetWindowRect(Handle, out RECT now))
                _restoredSize = new Size(now.Right - now.Left, now.Bottom - now.Top);
            _leftNormalState = !normal;
            try
            {
                base.WndProc(ref m);
            }
            finally
            {
                _restoredSize = null;
            }
            return true;
        }

        if (m.Msg == WM_WINDOWPOSCHANGING && _restoredSize is Size keep)
        {
            var pos = Marshal.PtrToStructure<WINDOWPOS>(m.LParam);
            const uint SWP_NOSIZE = 0x0001;
            if ((pos.flags & SWP_NOSIZE) == 0)
            {
                pos.cx = keep.Width;
                pos.cy = keep.Height;
                Marshal.StructureToPtr(pos, m.LParam, false);
            }
            return false;
        }

        if (m.Msg == WM_NCHITTEST)
        {
            // the form itself only sees points the child controls hand over: edges and caption areas
            long lp = m.LParam.ToInt64();
            Point p = PointToClient(new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF)));
            m.Result = (IntPtr)FrameHitTest(p);
            return true;
        }

        return false;
    }

    private int FrameHitTest(Point p)
    {
        if (!IsMaximized)
        {
            int band = ResizeBand, corner = band * 3;
            int w = ClientSize.Width, h = ClientSize.Height;
            bool left = p.X < band, right = p.X >= w - band, top = p.Y < band, bottom = p.Y >= h - band;
            bool nearLeft = p.X < corner, nearRight = p.X >= w - corner, nearTop = p.Y < corner, nearBottom = p.Y >= h - corner;

            if (top && nearLeft || left && nearTop)
                return HTTOPLEFT;
            if (top && nearRight || right && nearTop)
                return HTTOPRIGHT;
            if (bottom && nearLeft || left && nearBottom)
                return HTBOTTOMLEFT;
            if (bottom && nearRight || right && nearBottom)
                return HTBOTTOMRIGHT;
            if (left)
                return HTLEFT;
            if (right)
                return HTRIGHT;
            if (top)
                return HTTOP;
            if (bottom)
                return HTBOTTOM;
        }
        return HTCAPTION;
    }

    private static bool TaskbarAutoHides()
    {
        const int ABM_GETSTATE = 0x4, ABS_AUTOHIDE = 0x1;
        var data = new APPBARDATA { cbSize = Marshal.SizeOf<APPBARDATA>() };
        return ((int)SHAppBarMessage(ABM_GETSTATE, ref data) & ABS_AUTOHIDE) != 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOWPOS
    {
        public IntPtr hwnd;
        public IntPtr hwndInsertAfter;
        public int x, y, cx, cy;
        public uint flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct APPBARDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uCallbackMessage;
        public uint uEdge;
        public RECT rc;
        public IntPtr lParam;
    }

    [DllImport("user32.dll")]
    private static extern bool IsZoomed(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromRect(ref RECT rect, uint flags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("shell32.dll")]
    private static extern IntPtr SHAppBarMessage(int message, ref APPBARDATA data);
}
