using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Media;
using System.Windows.Forms;

namespace ParkLecture
{
    internal sealed class LectureTimerForm : Form
    {
        private readonly Settings settings;
        private readonly Timer tickTimer = new Timer { Interval = 250 };
        private int totalSeconds;
        private int remainingSeconds;
        private DateTime lastTickUtc;
        private bool running = true;
        private bool fullScreen;
        private Rectangle normalBounds;
        private string messageText;

        private readonly SoftButton toggleBtn, resetBtn, fullBtn;
        private readonly TextBox messageInput;
        private readonly Panel canvas;

        internal LectureTimerForm(Settings settings)
        {
            this.settings = settings;
            totalSeconds = Math.Max(60, Math.Min(180 * 60, settings.TimerMinutes * 60));
            remainingSeconds = totalSeconds;
            messageText = string.IsNullOrWhiteSpace(settings.TimerMessage) ? "쉬는 시간입니다 · 잠시 후 강의가 이어집니다" : settings.TimerMessage;

            Text = Words.T("박주임 강의 타이머", "Lecture Timer");
            Font = Style.Font(9.5f);
            BackColor = Style.Paper;
            ForeColor = Style.Ink;
            Size = new Size(920, 560);
            MinimumSize = new Size(720, 460);
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;
            TopMost = true;
            Icon = Branding.AppIcon;

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            Controls.Add(root);

            // Top bar: Brand + Message input + Presets
            var topBar = new Surface { Dock = DockStyle.Fill, BackColor = Color.White, BottomLine = true, Padding = new Padding(18, 10, 18, 10) };
            var topFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoSize = false };
            var mark = new BrandMark { Size = new Size(32, 32), Margin = new Padding(0, 4, 10, 0) };
            var titleLbl = new Label { Text = Words.T("안내 문구", "Message"), AutoSize = true, ForeColor = Style.Muted, Font = Style.Font(9f), Margin = new Padding(0, 11, 6, 0) };
            messageInput = new TextBox { Text = messageText, Width = 340, Font = Style.Font(9.5f), Margin = new Padding(0, 8, 12, 0) };
            messageInput.TextChanged += delegate
            {
                messageText = messageInput.Text;
                settings.TimerMessage = messageText;
                canvas.Invalidate();
            };

            topFlow.Controls.AddRange(new Control[] { mark, titleLbl, messageInput });
            foreach (int min in new[] { 5, 10, 15, 20, 30 })
            {
                int m = min;
                var pBtn = new SoftButton { Text = m + Words.T("분", "m"), Tone = ButtonTone.Quiet, MinimumSize = new Size(48, 34), Margin = new Padding(2, 3, 2, 0) };
                pBtn.Click += delegate { SetMinutes(m); };
                topFlow.Controls.Add(pBtn);
            }
            topBar.Controls.Add(topFlow);
            root.Controls.Add(topBar, 0, 0);

            // Center countdown display canvas
            canvas = new TimerCanvas(this) { Dock = DockStyle.Fill, BackColor = Style.Paper };
            canvas.DoubleClick += delegate { ToggleFullScreen(); };
            root.Controls.Add(canvas, 0, 1);

            // Bottom control bar
            var bottomBar = new Surface { Dock = DockStyle.Fill, BackColor = Color.White, TopLine = true, Padding = new Padding(18, 10, 18, 10) };
            var bottomLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var hintLbl = new Label
            {
                Text = Words.T("Space 일시정지/계속  ·  휠/방향키 ±1분  ·  R 초기화  ·  F 전체화면  ·  Esc 닫기", "Space Pause/Resume · Wheel/Up/Down ±1m · R Reset · F Fullscreen · Esc Close"),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Style.Muted,
                Font = Style.Font(8.5f)
            };
            bottomLayout.Controls.Add(hintLbl, 0, 0);

            var btnFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false, Margin = Padding.Empty };
            var minusBtn = new SoftButton { Text = "-1" + Words.T("분", "m"), Glyph = "minus", Tone = ButtonTone.Normal };
            minusBtn.Click += delegate { AdjustSeconds(-60); };
            var plusBtn = new SoftButton { Text = "+1" + Words.T("분", "m"), Glyph = "plus", Tone = ButtonTone.Normal };
            plusBtn.Click += delegate { AdjustSeconds(60); };
            toggleBtn = new SoftButton { Text = Words.T("일시정지", "Pause"), Glyph = "pause", Tone = ButtonTone.Primary };
            toggleBtn.Click += delegate { ToggleRunning(); };
            resetBtn = new SoftButton { Text = Words.T("초기화", "Reset"), Glyph = "undo", Tone = ButtonTone.Normal };
            resetBtn.Click += delegate { ResetTimer(); };
            fullBtn = new SoftButton { Text = Words.T("전체화면", "Fullscreen"), Glyph = "fit", Tone = ButtonTone.Quiet };
            fullBtn.Click += delegate { ToggleFullScreen(); };

            btnFlow.Controls.AddRange(new Control[] { minusBtn, plusBtn, toggleBtn, resetBtn, fullBtn });
            bottomLayout.Controls.Add(btnFlow, 1, 0);
            bottomBar.Controls.Add(bottomLayout);
            root.Controls.Add(bottomBar, 0, 2);

            lastTickUtc = DateTime.UtcNow;
            tickTimer.Tick += OnTick;
            tickTimer.Start();
        }

        private void SetMinutes(int minutes)
        {
            settings.TimerMinutes = minutes;
            totalSeconds = minutes * 60;
            remainingSeconds = totalSeconds;
            running = true;
            lastTickUtc = DateTime.UtcNow;
            UpdateToggleButton();
            canvas.Invalidate();
        }

        private void AdjustSeconds(int delta)
        {
            remainingSeconds = Math.Max(0, Math.Min(180 * 60, remainingSeconds + delta));
            totalSeconds = Math.Max(totalSeconds, remainingSeconds);
            lastTickUtc = DateTime.UtcNow;
            canvas.Invalidate();
        }

        private void ToggleRunning()
        {
            if (remainingSeconds <= 0)
            {
                remainingSeconds = totalSeconds;
                running = true;
            }
            else
            {
                running = !running;
            }
            lastTickUtc = DateTime.UtcNow;
            UpdateToggleButton();
            canvas.Invalidate();
        }

        private void ResetTimer()
        {
            remainingSeconds = totalSeconds;
            running = true;
            lastTickUtc = DateTime.UtcNow;
            UpdateToggleButton();
            canvas.Invalidate();
        }

        private void UpdateToggleButton()
        {
            toggleBtn.Text = running ? Words.T("일시정지", "Pause") : Words.T("계속", "Resume");
            toggleBtn.Glyph = running ? "pause" : "play";
        }

        private void ToggleFullScreen()
        {
            fullScreen = !fullScreen;
            if (fullScreen)
            {
                normalBounds = Bounds;
                FormBorderStyle = FormBorderStyle.None;
                Bounds = Screen.FromControl(this).Bounds;
                fullBtn.Text = Words.T("창 모드", "Windowed");
            }
            else
            {
                FormBorderStyle = FormBorderStyle.Sizable;
                Bounds = normalBounds;
                fullBtn.Text = Words.T("전체화면", "Fullscreen");
            }
            canvas.Invalidate();
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (!running) return;
            DateTime now = DateTime.UtcNow;
            double elapsed = (now - lastTickUtc).TotalSeconds;
            if (elapsed >= 1.0)
            {
                int steps = (int)elapsed;
                lastTickUtc = lastTickUtc.AddSeconds(steps);
                int prev = remainingSeconds;
                remainingSeconds = Math.Max(0, remainingSeconds - steps);
                if (prev > 0 && remainingSeconds == 0)
                {
                    running = false;
                    UpdateToggleButton();
                    try { SystemSounds.Asterisk.Play(); } catch { }
                }
                canvas.Invalidate();
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            AdjustSeconds(e.Delta > 0 ? 60 : -60);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (messageInput.Focused)
            {
                if (keyData == Keys.Enter || keyData == Keys.Escape)
                {
                    canvas.Focus();
                    return true;
                }
                return base.ProcessCmdKey(ref msg, keyData);
            }

            switch (keyData)
            {
                case Keys.Escape:
                    if (fullScreen) { ToggleFullScreen(); return true; }
                    Close();
                    return true;
                case Keys.Space:
                    ToggleRunning();
                    return true;
                case Keys.R:
                    ResetTimer();
                    return true;
                case Keys.F:
                case Keys.F11:
                    ToggleFullScreen();
                    return true;
                case Keys.Up:
                case Keys.Oemplus:
                case Keys.Add:
                    AdjustSeconds(60);
                    return true;
                case Keys.Down:
                case Keys.OemMinus:
                case Keys.Subtract:
                    AdjustSeconds(-60);
                    return true;
                case Keys.Right:
                    AdjustSeconds(10);
                    return true;
                case Keys.Left:
                    AdjustSeconds(-10);
                    return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        internal static string FormatClock(int seconds)
        {
            seconds = Math.Max(0, seconds);
            int mins = seconds / 60;
            int secs = seconds % 60;
            return mins.ToString("00") + ":" + secs.ToString("00");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                tickTimer.Stop();
                tickTimer.Dispose();
            }
            base.Dispose(disposing);
        }

        private sealed class TimerCanvas : Panel
        {
            private readonly LectureTimerForm parent;
            internal TimerCanvas(LectureTimerForm parent)
            {
                this.parent = parent;
                DoubleBuffered = true;
                SetStyle(ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Style.Paper);

                int cardW = Math.Min(Width - 48, 920);
                int cardH = Math.Min(Height - 40, 480);
                if (cardW < 200 || cardH < 180) return;

                var cardRect = new RectangleF((Width - cardW) / 2f, (Height - cardH) / 2f, cardW, cardH);
                using (var shadowPath = Glyphs.Round(new RectangleF(cardRect.X, cardRect.Y + 4, cardRect.Width, cardRect.Height), 20))
                using (var shadow = new SolidBrush(Color.FromArgb(18, 0, 0, 0)))
                    g.FillPath(shadow, shadowPath);

                using (var cardPath = Glyphs.Round(cardRect, 20))
                using (var bg = new SolidBrush(Color.White))
                using (var border = new Pen(Style.Line, 1.2f))
                {
                    g.FillPath(bg, cardPath);
                    g.DrawPath(border, cardPath);
                }

                // Message title
                float msgFontSize = Math.Max(18f, Math.Min(32f, cardH * 0.068f));
                using (var msgFont = AnnotationFonts.Create(AnnotationFonts.Default, msgFontSize, true))
                {
                    var msgRect = new Rectangle((int)cardRect.X + 28, (int)cardRect.Y + (int)(cardH * 0.10f), (int)cardRect.Width - 56, (int)(msgFontSize * 1.8f));
                    TextRenderer.DrawText(g, parent.messageText, msgFont, msgRect, Style.Ink, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                }

                // Large Countdown Digits
                string timeText = FormatClock(parent.remainingSeconds);
                Color clockColor = parent.remainingSeconds == 0 ? Style.Red : parent.remainingSeconds <= 60 ? Style.Orange : Style.Ink;
                float clockFontSize = Math.Max(64f, Math.Min(185f, cardH * 0.38f));
                using (var clockFont = AnnotationFonts.Create(AnnotationFonts.Default, clockFontSize, true))
                {
                    var clockRect = new Rectangle((int)cardRect.X + 20, (int)cardRect.Y + (int)(cardH * 0.25f), (int)cardRect.Width - 40, (int)(cardH * 0.44f));
                    TextRenderer.DrawText(g, timeText, clockFont, clockRect, clockColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                }

                // Progress bar
                float ratio = parent.totalSeconds > 0 ? Math.Max(0f, Math.Min(1f, parent.remainingSeconds / (float)parent.totalSeconds)) : 0f;
                int barW = (int)(cardRect.Width * 0.72f);
                int barH = 12;
                var barTrack = new RectangleF(cardRect.X + (cardRect.Width - barW) / 2f, cardRect.Y + cardH * 0.74f, barW, barH);
                using (var trackPath = Glyphs.Round(barTrack, 6))
                using (var trackBrush = new SolidBrush(Style.Paper))
                    g.FillPath(trackBrush, trackPath);

                if (ratio > 0.005f)
                {
                    var fillRect = new RectangleF(barTrack.X, barTrack.Y, Math.Max(12f, barTrack.Width * ratio), barH);
                    Color barColor = parent.remainingSeconds <= 60 ? Style.Orange : Style.Blue;
                    using (var fillPath = Glyphs.Round(fillRect, 6))
                    using (var fillBrush = new SolidBrush(barColor))
                        g.FillPath(fillBrush, fillPath);
                }

                // Expected resume time subtitle
                DateTime resumeTime = DateTime.Now.AddSeconds(parent.remainingSeconds);
                string subText = parent.remainingSeconds == 0
                    ? Words.T("시간이 종료되었습니다 · 강의를 다시 시작해 주세요!", "Time is up · Ready to resume the lecture!")
                    : (!parent.running ? Words.T("일시정지됨  ·  Space를 누르면 계속 진행합니다", "Paused  ·  Press Space to resume")
                    : Words.T("현재 시각 ", "Now ") + DateTime.Now.ToString("HH:mm") + Words.T("   ·   예상 시작 시각 ", "   ·   Resumes at ") + resumeTime.ToString("HH:mm"));

                using (var subFont = Style.Font(10.5f, parent.remainingSeconds == 0 ? FontStyle.Bold : FontStyle.Regular))
                {
                    var subRect = new Rectangle((int)cardRect.X + 24, (int)(cardRect.Y + cardH * 0.81f), (int)cardRect.Width - 48, 36);
                    TextRenderer.DrawText(g, subText, subFont, subRect, parent.remainingSeconds == 0 ? Style.Red : Style.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            }
        }
    }
}
