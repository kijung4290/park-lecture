using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace ParkLecture
{
    internal enum ButtonTone { Normal, Primary, Quiet, Tool, Segment, Swatch }

    internal sealed class SoftButton : Button
    {
        internal string Glyph = "";
        internal ButtonTone Tone;
        internal Color SwatchColor { get; set; }
        private bool hover, pressed, selected;
        internal bool Selected { get { return selected; } set { if (selected != value) { selected = value; Invalidate(); } } }
        private const TextFormatFlags TextFlags = TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.PreserveGraphicsClipping;

        internal SoftButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Opaque, true);
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; Cursor = Cursors.Hand;
            Font = Style.Font(9); BackColor = Color.White; ForeColor = Style.Ink;
            AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink; MinimumSize = new Size(36, 36); Margin = new Padding(3);
            UseVisualStyleBackColor = false;
        }
        private float ContentScale { get { return Math.Max(1f, Math.Max(DeviceDpi / 96f, Font.SizeInPoints / 9f)); } }
        private int Px(float value) { return Math.Max(1, (int)Math.Round(value * ContentScale)); }
        private Color SurfaceColor
        {
            get
            {
                Control parent = Parent;
                while (parent != null && parent.BackColor.A != 255) parent = parent.Parent;
                return parent == null ? BackColor : parent.BackColor;
            }
        }
        private Size MeasureLabel(Graphics graphics)
        {
            if (string.IsNullOrEmpty(Text)) return Size.Empty;
            Size proposed = new Size(int.MaxValue, int.MaxValue);
            return graphics == null ? TextRenderer.MeasureText(Text, Font, proposed, TextFlags) : TextRenderer.MeasureText(graphics, Text, Font, proposed, TextFlags);
        }
        public override Size GetPreferredSize(Size proposedSize)
        {
            if (!AutoSize) return Size;
            Size label;
            if (IsHandleCreated) using (var g = CreateGraphics()) label = MeasureLabel(g);
            else label = MeasureLabel(null);
            if (Tone == ButtonTone.Tool)
                return new Size(Math.Max(MinimumSize.Width, Math.Max(Px(60), label.Width + Px(16))), Math.Max(MinimumSize.Height, Math.Max(Px(60), Px(22 + 8 + 12) + label.Height)));
            int icon = string.IsNullOrEmpty(Glyph) ? 0 : Px(19), gap = icon > 0 && label.Width > 0 ? Px(8) : 0;
            return new Size(Math.Max(MinimumSize.Width, label.Width + icon + gap + Px(24)), Math.Max(MinimumSize.Height, Math.Max(label.Height, icon) + Px(16)));
        }
        internal void ContentBounds(Graphics graphics, out Rectangle icon, out Rectangle label)
        {
            Size text = MeasureLabel(graphics);
            int pad = Math.Min(Px(6), Math.Max(1, Math.Min(Width, Height) / 8));
            int availableHeight = Math.Max(0, Height - pad * 2);
            int textHeight = Math.Min(text.Height, availableHeight);
            bool hasIcon = !string.IsNullOrEmpty(Glyph);
            if (Tone == ButtonTone.Tool)
            {
                int gap = hasIcon && textHeight > 0 ? Px(8) : 0;
                int edge = hasIcon ? Math.Max(0, Math.Min(Px(22), Math.Min(Width - pad * 2, availableHeight - textHeight - gap))) : 0;
                if (edge == 0) gap = 0;
                int top = Math.Max(pad, (Height - edge - gap - textHeight) / 2);
                icon = new Rectangle((Width - edge) / 2, top, edge, edge);
                label = new Rectangle(pad, top + edge + gap, Math.Max(0, Width - pad * 2), textHeight);
                return;
            }
            int iconEdge = hasIcon ? Math.Max(0, Math.Min(Px(19), Math.Min(availableHeight, Width - pad * 2))) : 0;
            int space = iconEdge > 0 && text.Width > 0 ? Px(8) : 0;
            int textWidth = Math.Max(0, Math.Min(text.Width + Px(2), Width - pad * 2 - iconEdge - space));
            int left = Math.Max(pad, (Width - iconEdge - space - textWidth) / 2);
            icon = new Rectangle(left, (Height - iconEdge) / 2, iconEdge, iconEdge);
            label = new Rectangle(left + iconEdge + space, (Height - textHeight) / 2, textWidth, textHeight);
        }
        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); if (Parent != null) Parent.PerformLayout(); Invalidate(); }
        protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); if (Parent != null) Parent.PerformLayout(); Invalidate(); }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnPaintBackground(PaintEventArgs e) { using (var b = new SolidBrush(SurfaceColor)) e.Graphics.FillRectangle(b, ClientRectangle); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Color background = SurfaceColor;
            using (var brush = new SolidBrush(background)) g.FillRectangle(brush, ClientRectangle);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(1, 1, Math.Max(1, Width - 3), Math.Max(1, Height - 3));
            Color foreground = Enabled ? Style.Ink : Style.Disabled;
            Color fill = Color.Transparent, border = Color.Transparent;
            if (Tone == ButtonTone.Primary) { fill = Enabled ? (pressed ? Color.FromArgb(0, 87, 191) : hover ? Color.FromArgb(0, 112, 232) : Style.Blue) : Color.FromArgb(225, 227, 231); foreground = Enabled ? Color.White : Style.Muted; }
            else if (Tone == ButtonTone.Tool) { fill = selected && Enabled ? Style.BlueWash : hover && Enabled ? Style.Hover : Color.Transparent; foreground = selected && Enabled ? Style.Blue : foreground; }
            else if (Tone == ButtonTone.Segment) { fill = selected ? Color.White : hover ? Color.FromArgb(232, 232, 236) : Color.Transparent; if (selected) border = Color.FromArgb(225, 225, 230); }
            else if (Tone == ButtonTone.Normal) { fill = pressed ? Style.Hover : hover ? Color.FromArgb(245, 245, 247) : Color.White; border = Style.Line; }
            else if (hover || pressed || selected) fill = selected ? Style.BlueWash : Style.Hover;
            if (Tone == ButtonTone.Swatch)
            {
                float diameter = Math.Max(1, Math.Min(Width, Height) - Px(10));
                var circle = new RectangleF((Width - diameter) / 2, (Height - diameter) / 2, diameter, diameter);
                using (var brush = new SolidBrush(SwatchColor)) g.FillEllipse(brush, circle);
                using (var pen = new Pen(selected ? Style.Blue : Color.FromArgb(38, 0, 0, 0), selected ? 2 : 1)) { var outline = circle; outline.Inflate(selected ? Px(3) : 0, selected ? Px(3) : 0); g.DrawEllipse(pen, outline); }
                if (selected) Glyphs.Draw(g, "check", new RectangleF(circle.X + Px(4), circle.Y + Px(4), Math.Max(1, circle.Width - Px(8)), Math.Max(1, circle.Height - Px(8))), SwatchColor.GetBrightness() > .65 ? Style.Ink : Color.White);
                return;
            }
            using (var path = Glyphs.Round(r, Px(Tone == ButtonTone.Tool ? 10 : 9)))
            {
                if (fill.A > 0) using (var brush = new SolidBrush(fill)) g.FillPath(brush, path);
                if (border.A > 0) using (var pen = new Pen(border)) g.DrawPath(pen, path);
                if (Focused && ShowFocusCues) using (var pen = new Pen(Color.FromArgb(130, Style.Blue), 2)) g.DrawPath(pen, path);
            }
            Rectangle icon, label; ContentBounds(g, out icon, out label);
            Glyphs.Draw(g, Glyph, icon, foreground);
            if (!string.IsNullOrEmpty(Text) && label.Width > 0 && label.Height > 0)
                TextRenderer.DrawText(g, Text, Font, label, foreground, fill.A == 255 ? fill : background, TextFlags | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    internal sealed class Surface : Panel
    {
        internal int Radius;
        internal bool TopLine, BottomLine;
        internal Surface() { DoubleBuffered = true; SetStyle(ControlStyles.ResizeRedraw, true); }
        protected override void OnPaintBackground(PaintEventArgs e)
        { if (Radius > 0) e.Graphics.Clear(Parent == null ? BackColor : Parent.BackColor); else base.OnPaintBackground(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            if (Radius > 0) { g.SmoothingMode = SmoothingMode.AntiAlias; using (var path = Glyphs.Round(new RectangleF(0, 0, Width - 1, Height - 1), Radius)) using (var brush = new SolidBrush(BackColor)) g.FillPath(brush, path); }
            using (var p = new Pen(Style.Line)) { if (TopLine) g.DrawLine(p, 0, 0, Width, 0); if (BottomLine) g.DrawLine(p, 0, Height - 1, Width, Height - 1); }
            base.OnPaint(e);
        }
    }

    internal sealed class BrandMark : Control
    {
        internal BrandMark() { DoubleBuffered = true; Size = new Size(34, 34); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            if (Branding.Logo != null) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.DrawImage(Branding.Logo, ClientRectangle); return; }
            using (var path = Glyphs.Round(new RectangleF(1, 1, 32, 32), 9)) using (var b = new SolidBrush(Style.Blue)) g.FillPath(b, path);
            Glyphs.Draw(g, "zoom", new RectangleF(7, 7, 20, 20), Color.White);
        }
    }

    internal sealed class CaptureList : ListView
    {
        private readonly ImageList rows = new ImageList { ImageSize = new Size(1, 163), ColorDepth = ColorDepth.Depth32Bit };
        internal CaptureList()
        {
            DoubleBuffered = true; OwnerDraw = true; View = View.Details; HeaderStyle = ColumnHeaderStyle.None;
            FullRowSelect = true; BorderStyle = BorderStyle.None; BackColor = Style.Sidebar; ShowItemToolTips = true;
            Columns.Add("Capture", 220); SmallImageList = rows; rows.Images.Add(new Bitmap(1, 163));
        }
        protected override void OnResize(EventArgs e) { base.OnResize(e); if (Columns.Count > 0) Columns[0].Width = Math.Max(50, ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 2); }
        protected override void OnDrawColumnHeader(DrawListViewColumnHeaderEventArgs e) { e.DrawDefault = true; }
        protected override void OnDrawSubItem(DrawListViewSubItemEventArgs e) { }
        protected override void OnDrawItem(DrawListViewItemEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(e.Bounds.X + 2, e.Bounds.Y + 3, Math.Max(20, e.Bounds.Width - 5), e.Bounds.Height - 7);
            using (var b = new SolidBrush(BackColor)) g.FillRectangle(b, e.Bounds);
            using (var path = Glyphs.Round(r, 12))
            {
                using (var b = new SolidBrush(e.Item.Selected ? Style.BlueWash : Color.Transparent)) g.FillPath(b, path);
                if (e.Item.Selected) using (var p = new Pen(Color.FromArgb(185, 214, 250))) g.DrawPath(p, path);
            }
            var imageRect = new RectangleF(r.X + 9, r.Y + 9, r.Width - 18, 108);
            using (var imagePath = Glyphs.Round(imageRect, 7))
            {
                var saved = g.Save(); g.SetClip(imagePath);
                using (var b = new SolidBrush(Color.White)) g.FillRectangle(b, imageRect);
                if (LargeImageList != null && e.Item.ImageIndex >= 0 && e.Item.ImageIndex < LargeImageList.Images.Count)
                    g.DrawImage(LargeImageList.Images[e.Item.ImageIndex], imageRect);
                g.Restore(saved); using (var p = new Pen(Color.FromArgb(22, 0, 0, 0))) g.DrawPath(p, imagePath);
            }
            using (var f = Style.Font(8.5f)) TextRenderer.DrawText(g, e.Item.Text, f, new Rectangle((int)r.X + 11, (int)r.Y + 122, (int)r.Width - 44, 21), e.Item.Selected ? Style.Blue : Style.Ink, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            Glyphs.Draw(g, "image", new RectangleF(r.Right - 28, r.Y + 124, 15, 15), e.Item.Selected ? Style.Blue : Style.Muted);
        }
        protected override void Dispose(bool disposing) { if (disposing) rows.Dispose(); base.Dispose(disposing); }
    }

    internal static class Glyphs
    {
        internal static GraphicsPath Round(RectangleF r, float radius)
        {
            float d = Math.Max(1, Math.Min(radius * 2, Math.Min(r.Width, r.Height)));
            var path = new GraphicsPath(); path.AddArc(r.X, r.Y, d, d, 180, 90); path.AddArc(r.Right - d, r.Y, d, d, 270, 90); path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); path.AddArc(r.X, r.Bottom - d, d, d, 90, 90); path.CloseFigure(); return path;
        }
        internal static void Draw(Graphics g, string name, RectangleF rect, Color color)
        {
            if (string.IsNullOrEmpty(name) || rect.Width <= 0 || rect.Height <= 0) return;
            var saved = g.Save(); g.TranslateTransform(rect.X, rect.Y); g.ScaleTransform(rect.Width / 24f, rect.Height / 24f); g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var p = new Pen(color, 1.65f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round }) using (var b = new SolidBrush(color))
            {
                switch (name)
                {
                    case "select":
                        g.DrawPolygon(p, new[] { new PointF(5, 3), new PointF(5, 20), new PointF(10, 15), new PointF(14, 21), new PointF(17, 19), new PointF(13, 13), new PointF(20, 12) });
                        break;
                    case "help":
                        g.DrawEllipse(p, 3, 3, 18, 18);
                        g.DrawArc(p, 9, 7, 6, 5, 180, 250);
                        g.DrawLine(p, 12, 12, 12, 14.5f);
                        g.FillEllipse(b, 11, 16.8f, 2.2f, 2.2f);
                        break;
                    case "zoom":
                        g.DrawEllipse(p, 3, 3, 13, 13);
                        g.DrawLine(p, 14, 14, 21, 21);
                        g.DrawLine(p, 9.5f, 6.5f, 9.5f, 12.5f);
                        g.DrawLine(p, 6.5f, 9.5f, 12.5f, 9.5f);
                        break;
                    case "pen":
                        g.DrawPolygon(p, new[] { new PointF(4, 20), new PointF(8, 19), new PointF(19, 8), new PointF(16, 5), new PointF(5, 16) });
                        g.DrawLine(p, 13.5f, 7.5f, 16.5f, 10.5f);
                        break;
                    case "highlighter":
                        g.DrawPolygon(p, new[] { new PointF(4, 19), new PointF(9, 19), new PointF(20, 8), new PointF(16, 4), new PointF(5, 15) });
                        g.DrawLine(p, 3, 21, 12, 21);
                        break;
                    case "line":
                        g.DrawLine(p, 4, 19, 20, 5);
                        g.FillEllipse(b, 2.5f, 17.5f, 3, 3);
                        g.FillEllipse(b, 18.5f, 3.5f, 3, 3);
                        break;
                    case "arrow":
                        g.DrawLine(p, 4, 20, 20, 4);
                        g.DrawLines(p, new[] { new PointF(10, 4), new PointF(20, 4), new PointF(20, 14) });
                        break;
                    case "rectangle":
                        using (var path = Round(new RectangleF(3, 5, 18, 14), 2)) g.DrawPath(p, path);
                        break;
                    case "ellipse":
                        g.DrawEllipse(p, 3, 3, 18, 18);
                        break;
                    case "number":
                        g.DrawEllipse(p, 3, 3, 18, 18);
                        g.DrawLines(p, new[] { new PointF(10, 9), new PointF(12, 7), new PointF(12, 17) });
                        g.DrawLine(p, 9, 17, 15, 17);
                        break;
                    case "text":
                        g.DrawLine(p, 5, 5, 19, 5); g.DrawLine(p, 12, 5, 12, 20);
                        g.DrawLine(p, 8, 20, 16, 20); g.DrawLine(p, 5, 5, 5, 8); g.DrawLine(p, 19, 5, 19, 8);
                        break;
                    case "laser":
                        g.FillEllipse(b, 9, 9, 6, 6);
                        g.DrawEllipse(p, 5, 5, 14, 14);
                        g.DrawLine(p, 12, 1.5f, 12, 3.5f); g.DrawLine(p, 12, 20.5f, 12, 22.5f);
                        g.DrawLine(p, 1.5f, 12, 3.5f, 12); g.DrawLine(p, 20.5f, 12, 22.5f, 12);
                        break;
                    case "spotlight":
                        g.DrawEllipse(p, 5, 5, 14, 14);
                        using (var light = new SolidBrush(Color.FromArgb(65, color))) g.FillEllipse(light, 5, 5, 14, 14);
                        g.DrawLine(p, 3, 3, 6, 6); g.DrawLine(p, 21, 3, 18, 6);
                        break;
                    case "cursor":
                        g.DrawEllipse(p, 3, 3, 14, 14);
                        g.DrawPolygon(p, new[] { new PointF(10, 9), new PointF(10, 20), new PointF(13.5f, 16.5f), new PointF(16, 21), new PointF(18, 20), new PointF(15.5f, 15.5f), new PointF(20, 15) });
                        break;
                    case "whiteboard":
                        using (var path = Round(new RectangleF(3, 4, 18, 13), 2)) g.DrawPath(p, path);
                        g.DrawLine(p, 8, 17, 6, 21); g.DrawLine(p, 16, 17, 18, 21); g.DrawLine(p, 12, 17, 12, 21);
                        break;
                    case "blackboard":
                        using (var path = Round(new RectangleF(3, 4, 18, 14), 2)) { g.FillPath(b, path); }
                        using (var innerPen = new Pen(Color.White, 1.5f)) g.DrawLine(innerPen, 7, 14, 17, 8);
                        break;
                    case "timer":
                        g.DrawEllipse(p, 4, 5, 16, 16);
                        g.DrawLine(p, 9, 2, 15, 2); g.DrawLine(p, 12, 2, 12, 5);
                        g.DrawLines(p, new[] { new PointF(12, 9), new PointF(12, 13), new PointF(15.5f, 15) });
                        break;
                    case "eraser":
                        g.DrawPolygon(p, new[] { new PointF(4, 15), new PointF(13, 6), new PointF(20, 13), new PointF(13, 20), new PointF(7, 20) });
                        g.DrawLine(p, 8, 11, 15, 18);
                        g.DrawLine(p, 13, 20, 21, 20);
                        break;
                    case "copy":
                        using (var path = Round(new RectangleF(8, 7, 12, 14), 2)) g.DrawPath(p, path);
                        g.DrawLines(p, new[] { new PointF(5, 16), new PointF(3, 16), new PointF(3, 3), new PointF(15, 3), new PointF(15, 4) });
                        break;
                    case "trash":
                        g.DrawLine(p, 4, 6, 20, 6); g.DrawLines(p, new[] { new PointF(9, 6), new PointF(9, 3), new PointF(15, 3), new PointF(15, 6) });
                        g.DrawLines(p, new[] { new PointF(6, 9), new PointF(7, 21), new PointF(17, 21), new PointF(18, 9) });
                        g.DrawLine(p, 10, 10, 10, 17); g.DrawLine(p, 14, 10, 14, 17);
                        break;
                    case "save":
                        g.DrawLine(p, 12, 3, 12, 15); g.DrawLines(p, new[] { new PointF(7, 10), new PointF(12, 15), new PointF(17, 10) });
                        g.DrawLines(p, new[] { new PointF(4, 16), new PointF(4, 21), new PointF(20, 21), new PointF(20, 16) });
                        break;
                    case "play":
                        g.DrawPolygon(p, new[] { new PointF(7, 4), new PointF(20, 12), new PointF(7, 20) });
                        break;
                    case "pause":
                        g.DrawLine(p, 8, 5, 8, 19); g.DrawLine(p, 16, 5, 16, 19);
                        break;
                    case "image":
                        using (var path = Round(new RectangleF(3, 3, 18, 18), 3)) g.DrawPath(p, path);
                        g.DrawEllipse(p, 7, 7, 3, 3);
                        g.DrawLines(p, new[] { new PointF(4, 18), new PointF(10, 12), new PointF(14, 16), new PointF(17, 13), new PointF(21, 17) });
                        break;
                    case "folder":
                        g.DrawLines(p, new[] { new PointF(3, 8), new PointF(3, 5), new PointF(10, 5), new PointF(13, 8), new PointF(21, 8), new PointF(21, 20), new PointF(3, 20), new PointF(3, 8), new PointF(21, 8) });
                        break;
                    case "settings":
                        g.DrawEllipse(p, 5, 5, 14, 14); g.DrawEllipse(p, 9, 9, 6, 6);
                        for (int i = 0; i < 8; i++) { double a = Math.PI * i / 4; g.DrawLine(p, 12 + (float)Math.Cos(a) * 8, 12 + (float)Math.Sin(a) * 8, 12 + (float)Math.Cos(a) * 10, 12 + (float)Math.Sin(a) * 10); }
                        break;
                    case "more":
                        for (int i = 0; i < 3; i++) g.FillEllipse(b, 4 + i * 7, 10, 3, 3);
                        break;
                    case "undo":
                        g.DrawArc(p, 6, 7, 14, 13, 205, 260); g.DrawLines(p, new[] { new PointF(10, 3), new PointF(5, 8), new PointF(11, 10) });
                        break;
                    case "redo":
                        g.DrawArc(p, 4, 7, 14, 13, 75, 260); g.DrawLines(p, new[] { new PointF(14, 3), new PointF(19, 8), new PointF(13, 10) });
                        break;
                    case "minus":
                        g.DrawLine(p, 5, 12, 19, 12);
                        break;
                    case "plus":
                        g.DrawLine(p, 5, 12, 19, 12); g.DrawLine(p, 12, 5, 12, 19);
                        break;
                    case "fit":
                        g.DrawRectangle(p, 4, 5, 16, 14); g.DrawLine(p, 8, 9, 16, 9); g.DrawLine(p, 8, 15, 16, 15);
                        break;
                    case "search":
                        g.DrawEllipse(p, 4, 3, 12, 12); g.DrawLine(p, 14, 14, 21, 21);
                        break;
                    case "check":
                        g.DrawLines(p, new[] { new PointF(5, 12), new PointF(10, 17), new PointF(20, 6) });
                        break;
                    case "chevron":
                        g.DrawLines(p, new[] { new PointF(7, 10), new PointF(12, 15), new PointF(17, 10) });
                        break;
                }
            }
            g.Restore(saved);
        }
    }
}
