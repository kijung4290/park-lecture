using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ParkLecture
{
    internal sealed class CursorHaloOverlay : Form
    {
        private readonly Settings settings;
        private readonly Timer trackTimer = new Timer { Interval = 16 };
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly List<ClickRipple> ripples = new List<ClickRipple>();
        private bool prevLeftDown, prevRightDown;
        private const int CanvasSize = 180;

        private sealed class ClickRipple
        {
            internal long StartMs;
            internal Color Color;
        }

        internal CursorHaloOverlay(Settings settings)
        {
            this.settings = settings;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(CanvasSize, CanvasSize);
            ShowInTaskbar = false;
            TopMost = true;

            trackTimer.Tick += delegate { UpdateFrame(); };
            trackTimer.Start();
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                // WS_EX_LAYERED (0x80000) | WS_EX_TRANSPARENT (0x20) | WS_EX_TOPMOST (0x8) | WS_EX_TOOLWINDOW (0x80) | WS_EX_NOACTIVATE (0x08000000)
                cp.ExStyle |= 0x00080000 | 0x00000020 | 0x00000008 | 0x00000080 | 0x08000000;
                return cp;
            }
        }

        private void UpdateFrame()
        {
            if (!IsHandleCreated || IsDisposed) return;

            Point cursor = Cursor.Position;
            bool leftDown = (Native.GetAsyncKeyState(0x01) & 0x8000) != 0;
            bool rightDown = (Native.GetAsyncKeyState(0x02) & 0x8000) != 0;
            long now = clock.ElapsedMilliseconds;

            if (leftDown && !prevLeftDown)
                ripples.Add(new ClickRipple { StartMs = now, Color = Style.Red });
            if (rightDown && !prevRightDown)
                ripples.Add(new ClickRipple { StartMs = now, Color = Style.Blue });

            prevLeftDown = leftDown;
            prevRightDown = rightDown;
            ripples.RemoveAll(r => now - r.StartMs > 420);

            int radius = Math.Max(18, Math.Min(75, settings.CursorHaloSize));
            Color baseColor = Color.FromArgb(settings.CursorHaloColorArgb);
            if (baseColor.A == 0) baseColor = Style.Yellow;

            using (var bmp = new Bitmap(CanvasSize, CanvasSize, PixelFormat.Format32bppArgb))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                float cx = CanvasSize / 2f;
                float cy = CanvasSize / 2f;

                // Soft glowing halo
                var haloRect = new RectangleF(cx - radius, cy - radius, radius * 2, radius * 2);
                using (var fill = new SolidBrush(Color.FromArgb(leftDown ? 130 : 88, baseColor.R, baseColor.G, baseColor.B)))
                    g.FillEllipse(fill, haloRect);
                using (var border = new Pen(Color.FromArgb(195, baseColor.R, baseColor.G, baseColor.B), leftDown ? 3f : 2f))
                    g.DrawEllipse(border, haloRect);

                // Expanding click ripples
                foreach (var ripple in ripples)
                {
                    float t = Math.Min(1f, (now - ripple.StartMs) / 420f);
                    float r = radius * 0.5f + t * (radius * 1.45f);
                    int alpha = (int)(220 * (1f - t));
                    using (var rp = new Pen(Color.FromArgb(alpha, ripple.Color.R, ripple.Color.G, ripple.Color.B), 3f * (1f - t * 0.4f)))
                        g.DrawEllipse(rp, cx - r, cy - r, r * 2, r * 2);
                }

                ApplyLayeredBitmap(bmp, new Point(cursor.X - CanvasSize / 2, cursor.Y - CanvasSize / 2));
            }
        }

        private void ApplyLayeredBitmap(Bitmap bitmap, Point screenPos)
        {
            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memDc = CreateCompatibleDC(screenDc);
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr oldBitmap = IntPtr.Zero;
            try
            {
                hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
                oldBitmap = SelectObject(memDc, hBitmap);
                POINT topPos = new POINT { x = screenPos.X, y = screenPos.Y };
                SIZE size = new SIZE { cx = bitmap.Width, cy = bitmap.Height };
                POINT srcPos = new POINT { x = 0, y = 0 };
                BLENDFUNCTION blend = new BLENDFUNCTION { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = 255, AlphaFormat = 1 };
                UpdateLayeredWindow(Handle, screenDc, ref topPos, ref size, memDc, ref srcPos, 0, ref blend, 2);
            }
            finally
            {
                if (oldBitmap != IntPtr.Zero) SelectObject(memDc, oldBitmap);
                if (hBitmap != IntPtr.Zero) DeleteObject(hBitmap);
                DeleteDC(memDc);
                ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                trackTimer.Stop();
                trackTimer.Dispose();
            }
            base.Dispose(disposing);
        }

        [StructLayout(LayoutKind.Sequential)] private struct POINT { public int x, y; }
        [StructLayout(LayoutKind.Sequential)] private struct SIZE { public int cx, cy; }
        [StructLayout(LayoutKind.Sequential, Pack = 1)] private struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pprSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hDC);
        [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr hObject);
    }
}
