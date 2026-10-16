using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace ParkLecture
{
    internal sealed class LectureOverlay : Form
    {
        private readonly Settings settings;
        private readonly Bitmap desktopSnap;
        private readonly List<LectureStroke> strokes = new List<LectureStroke>();
        private readonly Stack<List<LectureStroke>> undoStack = new Stack<List<LectureStroke>>();
        private readonly Stack<List<LectureStroke>> redoStack = new Stack<List<LectureStroke>>();
        private readonly List<LaserSample> laserTrail = new List<LaserSample>();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly Timer animTimer = new Timer { Interval = 25 };

        private bool zoomPanning;
        private float zoom = 1.0f;
        private PointF focusWorld;
        private Point mouseClient;
        private bool spacePanning;
        private Point panDragStartClient;
        private PointF panDragStartFocus;

        private LectureTool currentTool = LectureTool.Pen;
        private BoardMode boardMode = BoardMode.Desktop;
        private bool spotlightActive;
        private int spotlightRadius;
        private Color penColor;
        private float penWidth;
        private int nextNumber = 1;
        private bool showHud;

        private LectureStroke activeStroke;
        private TextBox inlineTextBox;
        private PointF inlineTextWorldPos;
        private string toastMessage = "";
        private long toastUntilMs;

        private readonly List<RectangleHit> hudHits = new List<RectangleHit>();
        private Rectangle hudBounds = Rectangle.Empty;

        internal event Action<string> CaptureSaved;

        private struct RectangleHit
        {
            internal Rectangle Bounds;
            internal Action OnClick;
        }

        internal LectureOverlay(Settings settings, Bitmap screenshot, Rectangle screenBounds, bool startZoom, bool startSpotlight = false, BoardMode initialBoard = BoardMode.Desktop)
        {
            this.settings = settings;
            desktopSnap = screenshot;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Opaque, true);
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = screenBounds;
            TopMost = true;
            KeyPreview = true;
            ShowInTaskbar = false;
            DoubleBuffered = true;

            penColor = Color.FromArgb(settings.PenColorArgb);
            if (penColor.A == 0) penColor = Style.Red;
            penWidth = Math.Max(1f, Math.Min(30f, settings.PenWidth));
            spotlightRadius = Math.Max(60, Math.Min(500, settings.SpotlightRadius));
            spotlightActive = startSpotlight;
            boardMode = initialBoard;
            showHud = settings.ShowHudByDefault;

            Point cursorScreen = Cursor.Position;
            Point initialClient = new Point(
                Math.Max(0, Math.Min(screenBounds.Width - 1, cursorScreen.X - screenBounds.Left)),
                Math.Max(0, Math.Min(screenBounds.Height - 1, cursorScreen.Y - screenBounds.Top)));
            mouseClient = initialClient;
            focusWorld = new PointF(initialClient.X, initialClient.Y);

            if (startZoom)
            {
                zoomPanning = true;
                zoom = Math.Max(1.25f, Math.Min(8.0f, settings.DefaultZoom));
            }
            else
            {
                zoomPanning = false;
                zoom = 1.0f;
                focusWorld = new PointF(screenBounds.Width / 2f, screenBounds.Height / 2f);
            }

            if (boardMode == BoardMode.Blackboard && penColor.GetBrightness() < 0.25f)
                penColor = Style.Yellow;
            else if (boardMode == BoardMode.Whiteboard && penColor.GetBrightness() > 0.9f)
                penColor = Style.Red;

            Cursor = Cursors.Cross;

            animTimer.Tick += delegate
            {
                long now = clock.ElapsedMilliseconds;
                bool dirty = false;
                if (laserTrail.Count > 0)
                {
                    laserTrail.RemoveAll(s => now - s.TickMs > 550);
                    dirty = true;
                }
                if (toastUntilMs > 0 && now >= toastUntilMs)
                {
                    toastUntilMs = 0;
                    toastMessage = "";
                    dirty = true;
                }
                if (dirty) Invalidate();
            };
            animTimer.Start();
        }

        private RectangleF CurrentSourceRect()
        {
            return ZoomViewport.ComputeSourceRect(desktopSnap.Size, zoom, focusWorld);
        }

        private void PushUndo()
        {
            undoStack.Push(strokes.Select(s => s.Clone()).ToList());
            redoStack.Clear();
        }

        private void Undo()
        {
            CommitInlineText();
            if (undoStack.Count == 0) return;
            redoStack.Push(strokes.Select(s => s.Clone()).ToList());
            var prev = undoStack.Pop();
            strokes.Clear();
            strokes.AddRange(prev);
            Invalidate();
        }

        private void Redo()
        {
            CommitInlineText();
            if (redoStack.Count == 0) return;
            undoStack.Push(strokes.Select(s => s.Clone()).ToList());
            var next = redoStack.Pop();
            strokes.Clear();
            strokes.AddRange(next);
            Invalidate();
        }

        private void ClearAllStrokes()
        {
            CommitInlineText();
            if (strokes.Count == 0) return;
            PushUndo();
            strokes.Clear();
            nextNumber = 1;
            ShowToast(Words.T("판서를 모두 지웠습니다 (Ctrl+Z 복구)", "Cleared all annotations (Ctrl+Z to undo)"));
            Invalidate();
        }

        private void ShowToast(string text, int durationMs = 1800)
        {
            toastMessage = text;
            toastUntilMs = clock.ElapsedMilliseconds + durationMs;
            Invalidate();
        }

        private LectureTool EffectiveDragTool()
        {
            if (currentTool != LectureTool.Pen) return currentTool;
            bool ctrl = (ModifierKeys & Keys.Control) != 0;
            bool shift = (ModifierKeys & Keys.Shift) != 0;
            bool tab = (Native.GetAsyncKeyState(0x09) & 0x8000) != 0;
            if (ctrl && shift) return LectureTool.Arrow;
            if (tab) return LectureTool.Ellipse;
            if (ctrl) return LectureTool.Rectangle;
            if (shift) return LectureTool.Line;
            return LectureTool.Pen;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            mouseClient = e.Location;

            if (e.Button == MouseButtons.Right)
            {
                if (inlineTextBox != null) { CommitInlineText(); return; }
                if (activeStroke != null) { activeStroke = null; Invalidate(); return; }
                Close();
                return;
            }

            if (e.Button == MouseButtons.Middle || (e.Button == MouseButtons.Left && (Native.GetAsyncKeyState(0x20) & 0x8000) != 0))
            {
                spacePanning = true;
                panDragStartClient = e.Location;
                panDragStartFocus = focusWorld;
                Cursor = Cursors.SizeAll;
                return;
            }

            if (e.Button != MouseButtons.Left) return;

            if (showHud && hudBounds.Contains(e.Location))
            {
                foreach (var hit in hudHits)
                {
                    if (hit.Bounds.Contains(e.Location))
                    {
                        hit.OnClick();
                        Invalidate();
                        return;
                    }
                }
                return;
            }

            if (zoomPanning)
            {
                zoomPanning = false;
                ShowToast(Words.T("확대 고정됨 · 이제 화면에 바로 필기하세요", "Zoom locked · Draw directly on screen"));
                Invalidate();
                return;
            }

            if (inlineTextBox != null)
            {
                CommitInlineText();
                return;
            }

            RectangleF srcRect = CurrentSourceRect();
            PointF worldPt = ZoomViewport.ClientToWorld(e.Location, ClientSize, srcRect);

            LectureTool tool = EffectiveDragTool();

            if (tool == LectureTool.Text)
            {
                BeginInlineText(e.Location, worldPt);
                return;
            }

            if (tool == LectureTool.Number)
            {
                PushUndo();
                strokes.Add(new LectureStroke
                {
                    Kind = LectureTool.Number,
                    Color = penColor,
                    Width = penWidth,
                    Start = worldPt,
                    End = worldPt,
                    Number = nextNumber++,
                    FontName = settings.FontName,
                    FontSize = settings.FontSize
                });
                Invalidate();
                return;
            }

            if (tool == LectureTool.Laser)
            {
                laserTrail.Add(new LaserSample { Point = worldPt, TickMs = clock.ElapsedMilliseconds });
                Invalidate();
                return;
            }

            if (tool == LectureTool.Eraser)
            {
                EraseAt(worldPt);
                return;
            }

            PushUndo();
            activeStroke = new LectureStroke
            {
                Kind = tool,
                Color = penColor,
                Width = penWidth,
                Start = worldPt,
                End = worldPt,
                FontName = settings.FontName,
                FontSize = settings.FontSize
            };
            if (tool == LectureTool.Pen || tool == LectureTool.Highlighter)
                activeStroke.Points.Add(worldPt);
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            mouseClient = e.Location;

            if (zoomPanning)
            {
                focusWorld = new PointF(
                    (e.X / (float)Math.Max(1, ClientSize.Width)) * desktopSnap.Width,
                    (e.Y / (float)Math.Max(1, ClientSize.Height)) * desktopSnap.Height);
                Invalidate();
                return;
            }

            if (spacePanning)
            {
                float dx = (e.X - panDragStartClient.X) / zoom;
                float dy = (e.Y - panDragStartClient.Y) / zoom;
                focusWorld = new PointF(
                    Math.Max(0, Math.Min(desktopSnap.Width, panDragStartFocus.X - dx)),
                    Math.Max(0, Math.Min(desktopSnap.Height, panDragStartFocus.Y - dy)));
                Invalidate();
                return;
            }

            RectangleF srcRect = CurrentSourceRect();
            PointF worldPt = ZoomViewport.ClientToWorld(e.Location, ClientSize, srcRect);

            if (currentTool == LectureTool.Laser)
            {
                laserTrail.Add(new LaserSample { Point = worldPt, TickMs = clock.ElapsedMilliseconds });
                Invalidate();
                return;
            }

            if (e.Button == MouseButtons.Left && currentTool == LectureTool.Eraser)
            {
                EraseAt(worldPt);
                return;
            }

            if (activeStroke != null && e.Button == MouseButtons.Left)
            {
                if (activeStroke.Kind == LectureTool.Pen || activeStroke.Kind == LectureTool.Highlighter)
                {
                    PointF last = activeStroke.Points[activeStroke.Points.Count - 1];
                    if (Math.Abs(worldPt.X - last.X) > 0.5f || Math.Abs(worldPt.Y - last.Y) > 0.5f)
                        activeStroke.Points.Add(worldPt);
                }
                else
                {
                    activeStroke.End = worldPt;
                }
            }
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (spacePanning)
            {
                spacePanning = false;
                Cursor = Cursors.Cross;
                Invalidate();
                return;
            }
            if (activeStroke != null && e.Button == MouseButtons.Left)
            {
                strokes.Add(activeStroke);
                activeStroke = null;
                Invalidate();
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            bool ctrl = (ModifierKeys & Keys.Control) != 0;
            bool shift = (ModifierKeys & Keys.Shift) != 0;
            bool alt = (ModifierKeys & Keys.Alt) != 0;

            if (ctrl)
            {
                penWidth = Math.Max(1f, Math.Min(30f, penWidth + (e.Delta > 0 ? 1f : -1f)));
                settings.PenWidth = penWidth;
                ShowToast(Words.T("펜 굵기: ", "Pen thickness: ") + (int)penWidth + "px", 1000);
                Invalidate();
                return;
            }

            if (shift && spotlightActive)
            {
                spotlightRadius = Math.Max(50, Math.Min(500, spotlightRadius + (e.Delta > 0 ? 15 : -15)));
                settings.SpotlightRadius = spotlightRadius;
                Invalidate();
                return;
            }

            if (zoomPanning || alt || zoom > 1.01f)
            {
                AdjustZoom(e.Delta > 0 ? 0.25f : -0.25f, e.Location);
            }
            else
            {
                AdjustZoom(e.Delta > 0 ? 0.25f : -0.25f, e.Location);
            }
        }

        private void AdjustZoom(float delta, Point anchorClient)
        {
            RectangleF beforeRect = CurrentSourceRect();
            PointF anchorWorld = ZoomViewport.ClientToWorld(anchorClient, ClientSize, beforeRect);
            float nextZoom = Math.Max(1.0f, Math.Min(8.0f, (float)Math.Round((zoom + delta) * 4f) / 4f));
            if (Math.Abs(nextZoom - zoom) < 0.001f) return;
            zoom = nextZoom;
            if (zoom <= 1.01f)
            {
                zoom = 1.0f;
                focusWorld = new PointF(desktopSnap.Width / 2f, desktopSnap.Height / 2f);
            }
            else
            {
                float viewW = desktopSnap.Width / zoom;
                float viewH = desktopSnap.Height / zoom;
                float rx = anchorClient.X / (float)Math.Max(1, ClientSize.Width);
                float ry = anchorClient.Y / (float)Math.Max(1, ClientSize.Height);
                float left = anchorWorld.X - rx * viewW;
                float top = anchorWorld.Y - ry * viewH;
                focusWorld = new PointF(left + viewW / 2f, top + viewH / 2f);
            }
            Invalidate();
        }

        private void EraseAt(PointF worldPt)
        {
            for (int i = strokes.Count - 1; i >= 0; i--)
            {
                if (strokes[i].HitTest(worldPt, 10f / zoom))
                {
                    PushUndo();
                    strokes.RemoveAt(i);
                    Invalidate();
                    return;
                }
            }
        }

        private void BeginInlineText(Point clientPt, PointF worldPt)
        {
            CommitInlineText();
            inlineTextWorldPos = worldPt;
            float scaledFont = Math.Max(12f, settings.FontSize * zoom);
            inlineTextBox = new TextBox
            {
                Location = new Point(Math.Max(10, Math.Min(ClientSize.Width - 260, clientPt.X)), Math.Max(10, Math.Min(ClientSize.Height - 50, clientPt.Y))),
                Width = 320,
                Font = AnnotationFonts.Create(settings.FontName, scaledFont, true),
                ForeColor = penColor.GetBrightness() > 0.85f && boardMode != BoardMode.Blackboard ? Style.Ink : penColor,
                BackColor = boardMode == BoardMode.Blackboard ? Color.FromArgb(30, 42, 34) : Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            inlineTextBox.KeyDown += delegate(object sender, KeyEventArgs ke)
            {
                if (ke.KeyCode == Keys.Enter)
                {
                    ke.SuppressKeyPress = true;
                    CommitInlineText();
                }
                else if (ke.KeyCode == Keys.Escape)
                {
                    ke.SuppressKeyPress = true;
                    DiscardInlineText();
                }
            };
            Controls.Add(inlineTextBox);
            inlineTextBox.Focus();
        }

        private void CommitInlineText()
        {
            if (inlineTextBox == null) return;
            string text = inlineTextBox.Text.Trim();
            var box = inlineTextBox;
            inlineTextBox = null;
            Controls.Remove(box);
            box.Dispose();
            if (!string.IsNullOrEmpty(text))
            {
                PushUndo();
                strokes.Add(new LectureStroke
                {
                    Kind = LectureTool.Text,
                    Color = penColor,
                    Start = inlineTextWorldPos,
                    Text = text,
                    FontName = settings.FontName,
                    FontSize = settings.FontSize
                });
            }
            Focus();
            Invalidate();
        }

        private void DiscardInlineText()
        {
            if (inlineTextBox == null) return;
            var box = inlineTextBox;
            inlineTextBox = null;
            Controls.Remove(box);
            box.Dispose();
            Focus();
            Invalidate();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (inlineTextBox != null && inlineTextBox.Focused)
                return base.ProcessCmdKey(ref msg, keyData);

            if (keyData == Keys.Tab)
            {
                // Keep Tab from moving focus so Tab+Drag works for Ellipse
                return true;
            }

            if (keyData == Keys.Escape)
            {
                Close();
                return true;
            }
            if (keyData == (Keys.Control | Keys.Z)) { Undo(); return true; }
            if (keyData == (Keys.Control | Keys.Y) || keyData == (Keys.Control | Keys.Shift | Keys.Z)) { Redo(); return true; }
            if (keyData == (Keys.Control | Keys.C)) { CopyOrSaveCurrentScreen(false); return true; }
            if (keyData == (Keys.Control | Keys.S)) { CopyOrSaveCurrentScreen(true); return true; }

            Keys key = keyData & Keys.KeyCode;
            bool noMod = (keyData & (Keys.Control | Keys.Alt)) == 0;

            if (noMod)
            {
                switch (key)
                {
                    case Keys.F1:
                        showHud = !showHud;
                        Invalidate();
                        return true;
                    case Keys.Z:
                        zoomPanning = !zoomPanning;
                        if (zoomPanning && zoom <= 1.01f) zoom = Math.Max(1.5f, settings.DefaultZoom);
                        ShowToast(zoomPanning ? Words.T("확대 이동 모드 · 클릭하면 그리기", "Zoom pan mode · Click to draw") : Words.T("그리기 모드", "Drawing mode"), 1200);
                        Invalidate();
                        return true;
                    case Keys.P:
                        currentTool = LectureTool.Pen; zoomPanning = false; ShowToast(Words.T("펜 도구 (Shift: 직선, Ctrl: 사각형, Tab: 원)", "Pen tool"), 1100); Invalidate(); return true;
                    case Keys.H:
                        currentTool = LectureTool.Highlighter; zoomPanning = false; ShowToast(Words.T("형광펜 도구", "Highlighter"), 1100); Invalidate(); return true;
                    case Keys.L:
                        currentTool = LectureTool.Line; zoomPanning = false; ShowToast(Words.T("직선 도구", "Line tool"), 1100); Invalidate(); return true;
                    case Keys.A:
                        currentTool = LectureTool.Arrow; zoomPanning = false; ShowToast(Words.T("화살표 도구", "Arrow tool"), 1100); Invalidate(); return true;
                    case Keys.U:
                        currentTool = LectureTool.Rectangle; zoomPanning = false; ShowToast(Words.T("사각형 도구", "Rectangle tool"), 1100); Invalidate(); return true;
                    case Keys.C:
                        currentTool = LectureTool.Ellipse; zoomPanning = false; ShowToast(Words.T("원/타원 도구", "Ellipse tool"), 1100); Invalidate(); return true;
                    case Keys.N:
                        currentTool = LectureTool.Number; zoomPanning = false; ShowToast(Words.T("번호 스탬프 도구 (클릭마다 1, 2, 3…)", "Number stamp tool"), 1100); Invalidate(); return true;
                    case Keys.T:
                        currentTool = LectureTool.Text; zoomPanning = false; ShowToast(Words.T("텍스트 도구 · 화면을 클릭해 입력", "Text tool · Click to type"), 1100); Invalidate(); return true;
                    case Keys.Q:
                        currentTool = LectureTool.Laser; zoomPanning = false; ShowToast(Words.T("레이저 포인터", "Laser pointer"), 1100); Invalidate(); return true;
                    case Keys.X:
                        currentTool = LectureTool.Eraser; zoomPanning = false; ShowToast(Words.T("부분 지우개 · 드래그하여 선 삭제", "Eraser · Drag over strokes"), 1100); Invalidate(); return true;
                    case Keys.E:
                        ClearAllStrokes();
                        return true;
                    case Keys.Back:
                    case Keys.Delete:
                        Undo();
                        return true;
                    case Keys.S:
                        spotlightActive = !spotlightActive;
                        ShowToast(spotlightActive ? Words.T("스포트라이트 켜짐 (Shift+휠: 크기 조절)", "Spotlight ON (Shift+Wheel: size)") : Words.T("스포트라이트 꺼짐", "Spotlight OFF"), 1200);
                        Invalidate();
                        return true;
                    case Keys.W:
                        boardMode = boardMode == BoardMode.Whiteboard ? BoardMode.Desktop : BoardMode.Whiteboard;
                        if (boardMode == BoardMode.Whiteboard && penColor.GetBrightness() > 0.9f) penColor = Style.Ink;
                        ShowToast(boardMode == BoardMode.Whiteboard ? Words.T("화이트보드 모드 (W: 원래 화면)", "Whiteboard mode (W: return)") : Words.T("원본 강의 화면", "Desktop screen"), 1200);
                        Invalidate();
                        return true;
                    case Keys.K:
                        boardMode = boardMode == BoardMode.Blackboard ? BoardMode.Desktop : BoardMode.Blackboard;
                        if (boardMode == BoardMode.Blackboard && penColor.GetBrightness() < 0.25f) penColor = Style.Yellow;
                        ShowToast(boardMode == BoardMode.Blackboard ? Words.T("칠판 모드 (K: 원래 화면)", "Blackboard mode (K: return)") : Words.T("원본 강의 화면", "Desktop screen"), 1200);
                        Invalidate();
                        return true;
                    case Keys.D:
                        boardMode = BoardMode.Desktop;
                        Invalidate();
                        return true;
                    case Keys.R:
                        SetPenColor(Style.Red, "빨강"); return true;
                    case Keys.G:
                        SetPenColor(Style.Green, "초록"); return true;
                    case Keys.B:
                        SetPenColor(Style.Blue, "파랑"); return true;
                    case Keys.Y:
                        SetPenColor(Style.Yellow, "노랑"); return true;
                    case Keys.O:
                        SetPenColor(Style.Orange, "주황"); return true;
                    case Keys.M:
                        SetPenColor(Style.Purple, "보라"); return true;
                    case Keys.D1:
                    case Keys.NumPad1:
                        SetPenColor(Style.Palette[0], "빨강"); return true;
                    case Keys.D2:
                    case Keys.NumPad2:
                        SetPenColor(Style.Palette[1], "파랑"); return true;
                    case Keys.D3:
                    case Keys.NumPad3:
                        SetPenColor(Style.Palette[2], "초록"); return true;
                    case Keys.D4:
                    case Keys.NumPad4:
                        SetPenColor(Style.Palette[3], "노랑"); return true;
                    case Keys.D5:
                    case Keys.NumPad5:
                        SetPenColor(Style.Palette[4], "주황"); return true;
                    case Keys.D6:
                    case Keys.NumPad6:
                        SetPenColor(Style.Palette[5], "보라"); return true;
                    case Keys.D7:
                    case Keys.NumPad7:
                        SetPenColor(Style.Palette[6], "검정"); return true;
                    case Keys.D8:
                    case Keys.NumPad8:
                        SetPenColor(Style.Palette[7], "흰색"); return true;
                    case Keys.Oemplus:
                    case Keys.Add:
                    case Keys.Up:
                        AdjustZoom(0.25f, mouseClient); return true;
                    case Keys.OemMinus:
                    case Keys.Subtract:
                    case Keys.Down:
                        AdjustZoom(-0.25f, mouseClient); return true;
                    case Keys.OemOpenBrackets:
                        penWidth = Math.Max(1f, penWidth - 1f); settings.PenWidth = penWidth; Invalidate(); return true;
                    case Keys.OemCloseBrackets:
                        penWidth = Math.Min(30f, penWidth + 1f); settings.PenWidth = penWidth; Invalidate(); return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void SetPenColor(Color c, string koName)
        {
            penColor = c;
            settings.PenColorArgb = c.ToArgb();
            ShowToast(Words.T("색상: " + koName, "Color changed"), 900);
            Invalidate();
        }

        internal Bitmap RenderSceneToBitmap()
        {
            var bmp = new Bitmap(ClientSize.Width, ClientSize.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                PaintScene(g, false);
            }
            return bmp;
        }

        private void CopyOrSaveCurrentScreen(bool explicitSaveNotice)
        {
            CommitInlineText();
            try
            {
                using (var rendered = RenderSceneToBitmap())
                {
                    Storage.Copy(rendered);
                    string savedPath = null;
                    if (settings.AutoSaveCaptures || explicitSaveNotice)
                    {
                        savedPath = Storage.NewPath(settings.Folder, ".png");
                        Storage.SavePng(rendered, savedPath);
                        if (CaptureSaved != null) CaptureSaved(savedPath);
                    }
                    ShowToast(savedPath != null
                        ? Words.T("판서 화면을 클립보드에 복사하고 보관함에 저장했습니다", "Copied to clipboard and saved to library")
                        : Words.T("판서 화면을 클립보드에 복사했습니다", "Copied annotated screen to clipboard"), 2200);
                }
            }
            catch (Exception ex)
            {
                ShowToast(Words.T("저장 실패: ", "Save failed: ") + ex.Message, 2500);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            PaintScene(e.Graphics, true);
        }

        private void PaintScene(Graphics g, bool includeInteractiveHud)
        {
            RectangleF srcRect = CurrentSourceRect();
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 1. Background (Desktop screenshot, Whiteboard, or Blackboard)
            if (boardMode == BoardMode.Whiteboard)
            {
                g.Clear(Color.FromArgb(252, 252, 254));
                DrawBoardGrid(g, Color.FromArgb(230, 232, 238));
            }
            else if (boardMode == BoardMode.Blackboard)
            {
                g.Clear(Color.FromArgb(20, 42, 30));
                DrawBoardGrid(g, Color.FromArgb(32, 60, 44));
            }
            else
            {
                g.InterpolationMode = zoom > 1.05f ? InterpolationMode.HighQualityBicubic : InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(desktopSnap, new RectangleF(0, 0, ClientSize.Width, ClientSize.Height), srcRect, GraphicsUnit.Pixel);
            }

            // 2. Spotlight dimming layer (if active)
            if (spotlightActive)
            {
                using (var dimPath = new GraphicsPath())
                {
                    dimPath.AddRectangle(ClientRectangle);
                    float r = spotlightRadius;
                    var spotRect = new RectangleF(mouseClient.X - r, mouseClient.Y - r, r * 2, r * 2);
                    dimPath.AddEllipse(spotRect);
                    using (var dimBrush = new SolidBrush(Color.FromArgb(175, 10, 12, 16)))
                        g.FillPath(dimBrush, dimPath);
                    using (var ringPen = new Pen(Color.FromArgb(140, 255, 214, 10), 2.5f))
                        g.DrawEllipse(ringPen, spotRect);
                }
            }

            // 3. Transform world strokes into current zoomed viewport
            var state = g.Save();
            float scaleX = ClientSize.Width / Math.Max(1f, srcRect.Width);
            float scaleY = ClientSize.Height / Math.Max(1f, srcRect.Height);
            g.ScaleTransform(scaleX, scaleY);
            g.TranslateTransform(-srcRect.X, -srcRect.Y);

            foreach (var stroke in strokes)
                stroke.Draw(g);
            if (activeStroke != null)
                activeStroke.Draw(g);

            // Laser trail
            if (laserTrail.Count > 1)
            {
                long now = clock.ElapsedMilliseconds;
                for (int i = 1; i < laserTrail.Count; i++)
                {
                    long age = now - laserTrail[i].TickMs;
                    if (age > 550) continue;
                    int alpha = (int)Math.Max(10, 240 * (1.0 - age / 550.0));
                    float w = Math.Max(2f, 7f * (1f - age / 650f));
                    using (var lp = new Pen(Color.FromArgb(alpha, 255, 45, 45), w) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                        g.DrawLine(lp, laserTrail[i - 1].Point, laserTrail[i].Point);
                }
            }

            g.Restore(state);

            if (!includeInteractiveHud) return;

            // 4. Live cursor indicator (Laser dot or Pen thickness ring)
            if (!zoomPanning && (!showHud || !hudBounds.Contains(mouseClient)))
            {
                if (currentTool == LectureTool.Laser)
                {
                    using (var outer = new SolidBrush(Color.FromArgb(85, 255, 59, 48)))
                        g.FillEllipse(outer, mouseClient.X - 16, mouseClient.Y - 16, 32, 32);
                    using (var inner = new SolidBrush(Color.FromArgb(245, 255, 45, 45)))
                        g.FillEllipse(inner, mouseClient.X - 6, mouseClient.Y - 6, 12, 12);
                    using (var core = new SolidBrush(Color.White))
                        g.FillEllipse(core, mouseClient.X - 2.5f, mouseClient.Y - 2.5f, 5, 5);
                }
                else
                {
                    float ringDia = Math.Max(8f, (currentTool == LectureTool.Highlighter ? penWidth * 4.2f : penWidth) * zoom);
                    var ringRect = new RectangleF(mouseClient.X - ringDia / 2f, mouseClient.Y - ringDia / 2f, ringDia, ringDia);
                    using (var whitePen = new Pen(Color.FromArgb(190, Color.White), 2.2f))
                        g.DrawEllipse(whitePen, ringRect);
                    using (var colorPen = new Pen(penColor, 1.5f))
                        g.DrawEllipse(colorPen, ringRect);
                }
            }

            // 5. Zoom frame border when magnified
            if (zoom > 1.01f)
            {
                using (var borderPen = new Pen(zoomPanning ? Style.Blue : Color.FromArgb(160, Style.Red), 3f))
                    g.DrawRectangle(borderPen, 1, 1, ClientSize.Width - 3, ClientSize.Height - 3);
            }

            // 6. Toast notification
            if (!string.IsNullOrEmpty(toastMessage) && clock.ElapsedMilliseconds < toastUntilMs)
            {
                using (var font = Style.Font(10.5f, FontStyle.Bold))
                {
                    Size textSize = TextRenderer.MeasureText(g, toastMessage, font);
                    int tw = textSize.Width + 36, th = 40;
                    var toastRect = new RectangleF((ClientSize.Width - tw) / 2f, 24, tw, th);
                    using (var path = Glyphs.Round(toastRect, 12))
                    using (var fill = new SolidBrush(Color.FromArgb(230, 29, 29, 31)))
                    {
                        g.FillPath(fill, path);
                        TextRenderer.DrawText(g, toastMessage, font, Rectangle.Round(toastRect), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                    }
                }
            }

            // 7. Interactive Bottom HUD Bar (ParkCapture clean light floating bar)
            if (showHud)
                DrawHudBar(g);
        }

        private void DrawBoardGrid(Graphics g, Color dotColor)
        {
            using (var b = new SolidBrush(dotColor))
            {
                for (int y = 36; y < ClientSize.Height; y += 36)
                    for (int x = 36; x < ClientSize.Width; x += 36)
                        g.FillRectangle(b, x, y, 2, 2);
            }
        }

        private void DrawHudBar(Graphics g)
        {
            hudHits.Clear();
            int barW = Math.Min(ClientSize.Width - 40, 1080);
            int barH = 54;
            int barX = (ClientSize.Width - barW) / 2;
            int barY = ClientSize.Height - barH - 18;
            hudBounds = new Rectangle(barX, barY, barW, barH);

            using (var shadowPath = Glyphs.Round(new RectangleF(barX, barY + 3, barW, barH), 14))
            using (var shadowBrush = new SolidBrush(Color.FromArgb(42, 0, 0, 0)))
                g.FillPath(shadowBrush, shadowPath);

            using (var bgPath = Glyphs.Round(new RectangleF(barX, barY, barW, barH), 14))
            using (var bgBrush = new SolidBrush(Color.FromArgb(248, 255, 255, 255)))
            using (var borderPen = new Pen(Style.Line, 1.2f))
            {
                g.FillPath(bgBrush, bgPath);
                g.DrawPath(borderPen, bgPath);
            }

            int x = barX + 14;
            int cy = barY + barH / 2;

            // Mode badge (Zoom vs Draw)
            string modeLabel = zoomPanning
                ? Words.T("확대 이동 " + Math.Round(zoom * 100) + "%", "Zoom " + Math.Round(zoom * 100) + "%")
                : Words.T("필기 " + Math.Round(zoom * 100) + "%", "Draw " + Math.Round(zoom * 100) + "%");
            var modeRect = new Rectangle(x, cy - 16, 102, 32);
            using (var path = Glyphs.Round(modeRect, 8))
            using (var fill = new SolidBrush(zoomPanning ? Style.Blue : Style.BlueWash))
                g.FillPath(fill, path);
            using (var f = Style.Font(8.5f, FontStyle.Bold))
                TextRenderer.DrawText(g, modeLabel, f, modeRect, zoomPanning ? Color.White : Style.Blue, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            hudHits.Add(new RectangleHit { Bounds = modeRect, OnClick = delegate { zoomPanning = !zoomPanning; if (zoomPanning && zoom <= 1.01f) zoom = Math.Max(1.5f, settings.DefaultZoom); } });
            x += 110;

            // Tool pills
            var tools = new[]
            {
                new { Tool = LectureTool.Pen, Glyph = "pen", Label = "펜(P)" },
                new { Tool = LectureTool.Highlighter, Glyph = "highlighter", Label = "형광(H)" },
                new { Tool = LectureTool.Line, Glyph = "line", Label = "직선(L)" },
                new { Tool = LectureTool.Arrow, Glyph = "arrow", Label = "화살표(A)" },
                new { Tool = LectureTool.Rectangle, Glyph = "rectangle", Label = "사각(U)" },
                new { Tool = LectureTool.Ellipse, Glyph = "ellipse", Label = "원(C)" },
                new { Tool = LectureTool.Number, Glyph = "number", Label = "번호(N)" },
                new { Tool = LectureTool.Text, Glyph = "text", Label = "글자(T)" },
                new { Tool = LectureTool.Laser, Glyph = "laser", Label = "레이저(Q)" },
                new { Tool = LectureTool.Eraser, Glyph = "eraser", Label = "지우개(X)" }
            };

            using (var f = Style.Font(8f))
            {
                foreach (var item in tools)
                {
                    LectureTool t = item.Tool;
                    bool sel = !zoomPanning && currentTool == t;
                    var r = new Rectangle(x, cy - 17, 56, 34);
                    if (sel)
                    {
                        using (var p = Glyphs.Round(r, 8))
                        using (var b = new SolidBrush(Style.BlueWash))
                            g.FillPath(b, p);
                    }
                    Glyphs.Draw(g, item.Glyph, new RectangleF(r.X + (r.Width - 15) / 2f, r.Y + 3, 15, 15), sel ? Style.Blue : Style.Ink);
                    TextRenderer.DrawText(g, item.Label, f, new Rectangle(r.X, r.Y + 18, r.Width, 14), sel ? Style.Blue : Style.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                    hudHits.Add(new RectangleHit { Bounds = r, OnClick = delegate { currentTool = t; zoomPanning = false; } });
                    x += 57;
                }
            }

            // Divider
            using (var p = new Pen(Style.Line)) g.DrawLine(p, x + 3, barY + 10, x + 3, barY + barH - 10);
            x += 10;

            // Color swatches (1~6)
            for (int i = 0; i < 6; i++)
            {
                Color c = Style.Palette[i];
                var swRect = new Rectangle(x, cy - 10, 20, 20);
                using (var b = new SolidBrush(c)) g.FillEllipse(b, swRect);
                bool selectedColor = c.ToArgb() == penColor.ToArgb();
                using (var p = new Pen(selectedColor ? Style.Ink : Color.FromArgb(45, 0, 0, 0), selectedColor ? 2f : 1f))
                    g.DrawEllipse(p, swRect);
                hudHits.Add(new RectangleHit { Bounds = new Rectangle(x - 2, cy - 14, 24, 28), OnClick = delegate { penColor = c; settings.PenColorArgb = c.ToArgb(); } });
                x += 24;
            }

            // Divider
            using (var p = new Pen(Style.Line)) g.DrawLine(p, x + 3, barY + 10, x + 3, barY + barH - 10);
            x += 10;

            // Spotlight (S), Whiteboard (W), Blackboard (K), Clear (E), Copy (Ctrl+C), Close (Esc)
            var actions = new[]
            {
                new { Label = "집중(S)", Active = spotlightActive, Act = (Action)(() => spotlightActive = !spotlightActive) },
                new { Label = "칠판(W/K)", Active = boardMode != BoardMode.Desktop, Act = (Action)(() => boardMode = boardMode == BoardMode.Desktop ? BoardMode.Whiteboard : boardMode == BoardMode.Whiteboard ? BoardMode.Blackboard : BoardMode.Desktop) },
                new { Label = "지우기(E)", Active = false, Act = (Action)(() => ClearAllStrokes()) },
                new { Label = "복사", Active = false, Act = (Action)(() => CopyOrSaveCurrentScreen(false)) },
                new { Label = "종료(Esc)", Active = false, Act = (Action)(() => Close()) }
            };

            using (var f = Style.Font(8f, FontStyle.Bold))
            {
                foreach (var act in actions)
                {
                    if (x + 54 > barX + barW - 6) break;
                    var r = new Rectangle(x, cy - 15, 52, 30);
                    using (var p = Glyphs.Round(r, 7))
                    using (var b = new SolidBrush(act.Active ? Style.Blue : Style.Hover))
                        g.FillPath(b, p);
                    TextRenderer.DrawText(g, act.Label, f, r, act.Active ? Color.White : Style.Ink, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                    Action click = act.Act;
                    hudHits.Add(new RectangleHit { Bounds = r, OnClick = click });
                    x += 55;
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                animTimer.Stop();
                animTimer.Dispose();
                if (inlineTextBox != null) inlineTextBox.Dispose();
                if (desktopSnap != null) desktopSnap.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
