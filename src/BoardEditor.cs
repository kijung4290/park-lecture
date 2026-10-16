using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;

namespace ParkLecture
{
    internal sealed class BoardEditor : Control
    {
        private Bitmap baseImage;
        private readonly List<LectureStroke> strokes = new List<LectureStroke>();
        private readonly Stack<List<LectureStroke>> undoStack = new Stack<List<LectureStroke>>();
        private readonly Stack<List<LectureStroke>> redoStack = new Stack<List<LectureStroke>>();

        internal string PathName;
        internal bool Dirty;
        internal LectureTool Tool = LectureTool.Pen;
        internal Color MarkColor { get; set; }
        internal float LineWidth = 4f;
        internal float TextSize = 32f;
        internal string TextFontName = AnnotationFonts.Default;

        private float zoom = 1f;
        private PointF origin;
        private bool fit = true, panning, draggingSelected;
        private Point panAnchor;
        private PointF dragLastImgPt;
        private int selectedIndex = -1;
        private int nextNumber = 1;
        private LectureStroke draft;

        internal event EventHandler Changed, ViewChanged;

        internal bool HasBoard { get { return baseImage != null; } }
        internal Size BoardSize { get { return baseImage == null ? Size.Empty : baseImage.Size; } }
        internal float Zoom { get { return zoom; } }
        internal bool CanUndo { get { return undoStack.Count > 0; } }
        internal bool CanRedo { get { return redoStack.Count > 0; } }
        internal bool CanClear { get { return strokes.Count > 0; } }
        internal LectureStroke SelectedStroke { get { return selectedIndex >= 0 && selectedIndex < strokes.Count ? strokes[selectedIndex] : null; } }

        internal BoardEditor()
        {
            MarkColor = Style.Red;
            DoubleBuffered = true;
            BackColor = Style.Paper;
            TabStop = true;
            SetStyle(ControlStyles.Selectable | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            AccessibleName = "판서 캔버스 / Lecture board canvas";
        }

        internal void CloseBoard()
        {
            if (baseImage != null) { baseImage.Dispose(); baseImage = null; }
            strokes.Clear();
            undoStack.Clear();
            redoStack.Clear();
            selectedIndex = -1;
            draft = null;
            PathName = null;
            Dirty = false;
            Invalidate();
            NotifyView();
        }

        internal void LoadImageBoard(Bitmap image, string path)
        {
            if (baseImage != null) baseImage.Dispose();
            baseImage = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(baseImage)) g.DrawImage(image, 0, 0, image.Width, image.Height);
            strokes.Clear();
            undoStack.Clear();
            redoStack.Clear();
            selectedIndex = -1;
            nextNumber = 1;
            draft = null;
            PathName = path;
            Dirty = false;
            fit = true;
            Fit();
            NotifyChanged();
        }

        internal void CreateBlankBoard(BoardMode mode, string savePath)
        {
            var bmp = new Bitmap(1600, 900, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(mode == BoardMode.Blackboard ? Color.FromArgb(20, 42, 30) : Color.White);
                Color dot = mode == BoardMode.Blackboard ? Color.FromArgb(34, 64, 46) : Color.FromArgb(232, 234, 240);
                using (var b = new SolidBrush(dot))
                {
                    for (int y = 40; y < bmp.Height; y += 40)
                        for (int x = 40; x < bmp.Width; x += 40)
                            g.FillRectangle(b, x, y, 2, 2);
                }
            }
            using (bmp) LoadImageBoard(bmp, savePath);
            Dirty = true;
        }

        internal void SetTool(LectureTool value)
        {
            Tool = value;
            draft = null;
            if (value != LectureTool.Select) selectedIndex = -1;
            Cursor = value == LectureTool.Select ? Cursors.Default : Cursors.Cross;
            Invalidate();
            NotifyView();
        }

        internal void ApplyStyle()
        {
            var s = SelectedStroke;
            if (s == null) return;
            PushUndo();
            s.Color = MarkColor;
            s.Width = LineWidth;
            s.FontSize = TextSize;
            s.FontName = TextFontName;
            Dirty = true;
            NotifyChanged();
        }

        internal void Fit()
        {
            fit = true;
            if (baseImage == null) { Invalidate(); return; }
            zoom = Math.Max(0.05f, Math.Min(1f, Math.Min((ClientSize.Width - 70f) / baseImage.Width, (ClientSize.Height - 70f) / baseImage.Height)));
            CenterBoard();
        }

        private void CenterBoard()
        {
            if (baseImage == null) return;
            origin = new PointF((Width - baseImage.Width * zoom) / 2f, (Height - baseImage.Height * zoom) / 2f);
            Invalidate();
            NotifyView();
        }

        internal void ZoomBy(float factor)
        {
            if (baseImage == null) return;
            ZoomAt(factor, new Point(Width / 2, Height / 2));
        }

        private void ZoomAt(float factor, Point clientPt)
        {
            PointF before = ToImage(clientPt, false);
            zoom = Math.Max(0.1f, Math.Min(8f, zoom * factor));
            fit = false;
            origin = new PointF(clientPt.X - before.X * zoom, clientPt.Y - before.Y * zoom);
            Invalidate();
            NotifyView();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (fit && baseImage != null) Fit();
        }

        private void PushUndo()
        {
            undoStack.Push(strokes.Select(s => s.Clone()).ToList());
            redoStack.Clear();
        }

        internal void Undo()
        {
            if (undoStack.Count == 0) return;
            redoStack.Push(strokes.Select(s => s.Clone()).ToList());
            var prev = undoStack.Pop();
            strokes.Clear();
            strokes.AddRange(prev);
            selectedIndex = -1;
            Dirty = true;
            NotifyChanged();
        }

        internal void Redo()
        {
            if (redoStack.Count == 0) return;
            undoStack.Push(strokes.Select(s => s.Clone()).ToList());
            var next = redoStack.Pop();
            strokes.Clear();
            strokes.AddRange(next);
            selectedIndex = -1;
            Dirty = true;
            NotifyChanged();
        }

        internal void DeleteSelected()
        {
            if (SelectedStroke == null) return;
            PushUndo();
            strokes.RemoveAt(selectedIndex);
            selectedIndex = -1;
            Dirty = true;
            NotifyChanged();
        }

        internal void ClearStrokes()
        {
            if (strokes.Count == 0) return;
            PushUndo();
            strokes.Clear();
            selectedIndex = -1;
            nextNumber = 1;
            Dirty = true;
            NotifyChanged();
        }

        internal Bitmap RenderComposite()
        {
            if (baseImage == null) return null;
            var result = new Bitmap(baseImage.Width, baseImage.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(result))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawImage(baseImage, 0, 0, baseImage.Width, baseImage.Height);
                foreach (var s in strokes) s.Draw(g);
            }
            return result;
        }

        private PointF ToImage(Point p, bool clamp = true)
        {
            float x = (p.X - origin.X) / zoom, y = (p.Y - origin.Y) / zoom;
            if (clamp && baseImage != null)
            {
                x = Math.Max(0, Math.Min(baseImage.Width, x));
                y = Math.Max(0, Math.Min(baseImage.Height, y));
            }
            return new PointF(x, y);
        }

        private bool InImage(Point p)
        {
            if (baseImage == null) return false;
            var v = ToImage(p, false);
            return v.X >= 0 && v.Y >= 0 && v.X <= baseImage.Width && v.Y <= baseImage.Height;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (baseImage != null) ZoomAt(e.Delta > 0 ? 1.15f : 1f / 1.15f, e.Location);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (baseImage == null) return;

            if (e.Button == MouseButtons.Middle || (e.Button == MouseButtons.Left && (ModifierKeys & Keys.Space) != 0))
            {
                panning = true;
                panAnchor = e.Location;
                Cursor = Cursors.SizeAll;
                return;
            }
            if (e.Button != MouseButtons.Left) return;

            PointF pt = ToImage(e.Location);

            if (Tool == LectureTool.Select)
            {
                selectedIndex = HitStroke(pt);
                if (selectedIndex >= 0)
                {
                    var s = strokes[selectedIndex];
                    MarkColor = s.Color;
                    LineWidth = s.Width;
                    TextSize = s.FontSize;
                    TextFontName = s.FontName;
                    PushUndo();
                    draggingSelected = true;
                    dragLastImgPt = pt;
                }
                Invalidate();
                NotifyView();
                return;
            }

            if (!InImage(e.Location)) return;

            if (Tool == LectureTool.Eraser)
            {
                EraseAt(pt);
                return;
            }

            if (Tool == LectureTool.Number)
            {
                PushUndo();
                strokes.Add(new LectureStroke
                {
                    Kind = LectureTool.Number,
                    Color = MarkColor,
                    Width = LineWidth,
                    Start = pt,
                    End = pt,
                    Number = nextNumber++,
                    FontName = TextFontName,
                    FontSize = TextSize
                });
                Dirty = true;
                NotifyChanged();
                return;
            }

            if (Tool == LectureTool.Text)
            {
                string txt = PromptText(this, Words.T("글자 입력", "Add Text"), "", TextFontName, TextSize);
                if (!string.IsNullOrWhiteSpace(txt))
                {
                    PushUndo();
                    strokes.Add(new LectureStroke
                    {
                        Kind = LectureTool.Text,
                        Color = MarkColor,
                        Start = pt,
                        Text = txt.Trim(),
                        FontName = TextFontName,
                        FontSize = TextSize
                    });
                    Dirty = true;
                    NotifyChanged();
                }
                return;
            }

            draft = new LectureStroke
            {
                Kind = Tool,
                Color = MarkColor,
                Width = LineWidth,
                Start = pt,
                End = pt,
                FontName = TextFontName,
                FontSize = TextSize
            };
            if (Tool == LectureTool.Pen || Tool == LectureTool.Highlighter)
                draft.Points.Add(pt);
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (baseImage == null) return;

            if (panning)
            {
                origin = new PointF(origin.X + e.X - panAnchor.X, origin.Y + e.Y - panAnchor.Y);
                panAnchor = e.Location;
                fit = false;
                Invalidate();
                return;
            }

            PointF pt = ToImage(e.Location);

            if (draggingSelected && selectedIndex >= 0 && selectedIndex < strokes.Count)
            {
                strokes[selectedIndex].OffsetBy(pt.X - dragLastImgPt.X, pt.Y - dragLastImgPt.Y);
                dragLastImgPt = pt;
                Dirty = true;
                Invalidate();
                return;
            }

            if (e.Button == MouseButtons.Left && Tool == LectureTool.Eraser)
            {
                EraseAt(pt);
                return;
            }

            if (draft != null && e.Button == MouseButtons.Left)
            {
                if (draft.Kind == LectureTool.Pen || draft.Kind == LectureTool.Highlighter)
                    draft.Points.Add(pt);
                else
                    draft.End = pt;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (panning)
            {
                panning = false;
                Cursor = Tool == LectureTool.Select ? Cursors.Default : Cursors.Cross;
                return;
            }
            if (draggingSelected)
            {
                draggingSelected = false;
                NotifyChanged();
                return;
            }
            if (draft != null && e.Button == MouseButtons.Left)
            {
                PushUndo();
                strokes.Add(draft);
                draft = null;
                Dirty = true;
                NotifyChanged();
            }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (SelectedStroke != null && SelectedStroke.Kind == LectureTool.Text)
            {
                string updated = PromptText(this, Words.T("내용 수정", "Edit Text"), SelectedStroke.Text, SelectedStroke.FontName, SelectedStroke.FontSize);
                if (updated != null && !string.IsNullOrWhiteSpace(updated))
                {
                    PushUndo();
                    SelectedStroke.Text = updated.Trim();
                    Dirty = true;
                    NotifyChanged();
                }
            }
        }

        private int HitStroke(PointF pt)
        {
            float tol = 8f / zoom;
            for (int i = strokes.Count - 1; i >= 0; i--)
                if (strokes[i].HitTest(pt, tol)) return i;
            return -1;
        }

        private void EraseAt(PointF pt)
        {
            int idx = HitStroke(pt);
            if (idx >= 0)
            {
                PushUndo();
                strokes.RemoveAt(idx);
                Dirty = true;
                NotifyChanged();
            }
        }

        private void NotifyChanged()
        {
            Invalidate();
            if (Changed != null) Changed(this, EventArgs.Empty);
            NotifyView();
        }

        private void NotifyView()
        {
            if (ViewChanged != null) ViewChanged(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (baseImage == null)
            {
                var icon = new RectangleF(Width / 2f - 38, Height / 2f - 124, 76, 76);
                using (var path = Glyphs.Round(icon, 22))
                using (var b = new SolidBrush(Color.White))
                    g.FillPath(b, path);
                Glyphs.Draw(g, "zoom", new RectangleF(icon.X + 20, icon.Y + 20, 36, 36), Style.Blue);

                using (var f = Style.Font(20, FontStyle.Bold))
                using (var b = new SolidBrush(Style.Ink))
                using (var format = new StringFormat { Alignment = StringAlignment.Center })
                    g.DrawString(Words.T("화면을 확대하고, 바로 판서하세요", "Zoom in and annotate with ease"), f, b, new RectangleF(20, Height / 2f - 20, Width - 40, 55), format);

                using (var f = Style.Font(10))
                using (var b = new SolidBrush(Style.Muted))
                using (var format = new StringFormat { Alignment = StringAlignment.Center })
                    g.DrawString(Words.T("발표 화면 위에서 바로 그리거나 새 칠판을 열어 시작하세요.", "Annotate over any screen or open a clean whiteboard."), f, b, new RectangleF(20, Height / 2f + 31, Width - 40, 35), format);
                return;
            }

            var state = g.Save();
            g.TranslateTransform(origin.X, origin.Y);
            g.ScaleTransform(zoom, zoom);

            for (int i = 10; i >= 2; i -= 2)
            {
                using (var shadow = new SolidBrush(Color.FromArgb(4, 0, 0, 0)))
                using (var path = Glyphs.Round(new RectangleF(-i / zoom, (3 - i) / zoom, baseImage.Width + 2 * i / zoom, baseImage.Height + 2 * i / zoom), (i + 2) / zoom))
                    g.FillPath(shadow, path);
            }

            g.InterpolationMode = zoom >= 1f ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBicubic;
            g.DrawImage(baseImage, new Rectangle(0, 0, baseImage.Width, baseImage.Height));
            g.SetClip(new Rectangle(0, 0, baseImage.Width, baseImage.Height));

            foreach (var s in strokes) s.Draw(g);
            if (draft != null) draft.Draw(g);

            if (selectedIndex >= 0 && selectedIndex < strokes.Count)
            {
                var r = strokes[selectedIndex].Bounds();
                using (var p = new Pen(Style.Blue, 1.5f / zoom) { DashStyle = DashStyle.Dash })
                    g.DrawRectangle(p, r.X, r.Y, r.Width, r.Height);
            }
            g.Restore(state);
        }

        private static string PromptText(IWin32Window owner, string title, string initial, string fontName, float fontSize)
        {
            using (var form = new Form { Text = title, StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(480, 240), FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false, Font = Style.Font(10), BackColor = Style.Sidebar })
            {
                var input = new TextBox { Multiline = true, Text = initial, Location = new Point(18, 18), Size = new Size(444, 144), AcceptsReturn = true, BorderStyle = BorderStyle.FixedSingle, Font = AnnotationFonts.Create(fontName, Math.Min(24, fontSize), true) };
                var hint = new Label { Text = Words.T("Ctrl+Enter 저장 · Esc 취소", "Ctrl+Enter to save · Esc to cancel"), ForeColor = Style.Muted, Font = Style.Font(8.5f), Bounds = new Rectangle(18, 170, 260, 20) };
                var ok = Style.Button(Words.T("확인", "OK"), delegate { form.DialogResult = DialogResult.OK; }, true); ok.Location = new Point(286, 188);
                var cancel = Style.Button(Words.T("취소", "Cancel"), delegate { form.DialogResult = DialogResult.Cancel; }); cancel.Location = new Point(378, 188);
                input.KeyDown += delegate(object sender, KeyEventArgs ke) { if (ke.Control && ke.KeyCode == Keys.Enter) { ke.SuppressKeyPress = true; form.DialogResult = DialogResult.OK; } };
                form.Controls.AddRange(new Control[] { input, hint, ok, cancel });
                form.CancelButton = cancel;
                form.Shown += delegate { input.Focus(); input.SelectAll(); };
                return form.ShowDialog(owner) == DialogResult.OK ? input.Text : null;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && baseImage != null) baseImage.Dispose();
            base.Dispose(disposing);
        }
    }
}
