using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;

namespace ParkLecture
{
    internal enum LectureTool
    {
        Select,
        Pen,
        Highlighter,
        Line,
        Arrow,
        Rectangle,
        Ellipse,
        Number,
        Text,
        Laser,
        Eraser
    }

    internal enum BoardMode
    {
        Desktop,
        Whiteboard,
        Blackboard
    }

    internal sealed class LaserSample
    {
        internal PointF Point;
        internal long TickMs;
    }

    internal sealed class LectureStroke
    {
        internal LectureTool Kind = LectureTool.Pen;
        internal Color Color = Style.Red;
        internal float Width = 4f;
        internal List<PointF> Points = new List<PointF>();
        internal PointF Start, End;
        internal int Number = 1;
        internal string Text = "";
        internal string FontName = AnnotationFonts.Default;
        internal float FontSize = 32f;

        internal void OffsetBy(float dx, float dy)
        {
            Start = new PointF(Start.X + dx, Start.Y + dy);
            End = new PointF(End.X + dx, End.Y + dy);
            for (int i = 0; i < Points.Count; i++)
                Points[i] = new PointF(Points[i].X + dx, Points[i].Y + dy);
        }

        internal LectureStroke Clone()
        {
            return new LectureStroke
            {
                Kind = Kind,
                Color = Color,
                Width = Width,
                Points = new List<PointF>(Points),
                Start = Start,
                End = End,
                Number = Number,
                Text = Text,
                FontName = FontName,
                FontSize = FontSize
            };
        }

        internal RectangleF Bounds()
        {
            if (Kind == LectureTool.Pen || Kind == LectureTool.Highlighter)
            {
                if (Points.Count == 0) return RectangleF.Empty;
                float minX = Points.Min(p => p.X), minY = Points.Min(p => p.Y);
                float maxX = Points.Max(p => p.X), maxY = Points.Max(p => p.Y);
                float pad = Width * 2 + 4;
                return RectangleF.FromLTRB(minX - pad, minY - pad, maxX + pad, maxY + pad);
            }
            if (Kind == LectureTool.Number)
            {
                float r = Math.Max(16f, FontSize * 0.65f);
                return new RectangleF(Start.X - r, Start.Y - r, r * 2, r * 2);
            }
            if (Kind == LectureTool.Text)
            {
                float w = Math.Max(60f, (Text ?? "").Length * FontSize * 0.85f);
                float h = Math.Max(30f, FontSize * 1.4f * Math.Max(1, (Text ?? "").Split('\n').Length));
                return new RectangleF(Start.X - 4, Start.Y - 4, w + 8, h + 8);
            }
            float x1 = Math.Min(Start.X, End.X), y1 = Math.Min(Start.Y, End.Y);
            float x2 = Math.Max(Start.X, End.X), y2 = Math.Max(Start.Y, End.Y);
            return RectangleF.FromLTRB(x1 - Width - 6, y1 - Width - 6, x2 + Width + 6, y2 + Width + 6);
        }

        internal bool HitTest(PointF worldPt, float tolerance)
        {
            RectangleF b = Bounds();
            b.Inflate(tolerance, tolerance);
            if (!b.Contains(worldPt)) return false;

            if (Kind == LectureTool.Pen || Kind == LectureTool.Highlighter)
            {
                float thresh = Math.Max(tolerance, Width * 1.5f);
                for (int i = 0; i < Points.Count; i++)
                {
                    float dx = Points[i].X - worldPt.X, dy = Points[i].Y - worldPt.Y;
                    if (dx * dx + dy * dy <= thresh * thresh) return true;
                    if (i > 0 && DistanceToSegment(worldPt, Points[i - 1], Points[i]) <= thresh) return true;
                }
                return false;
            }
            if (Kind == LectureTool.Line || Kind == LectureTool.Arrow)
                return DistanceToSegment(worldPt, Start, End) <= Math.Max(tolerance, Width * 2f);
            return true;
        }

        private static float DistanceToSegment(PointF p, PointF a, PointF b)
        {
            float dx = b.X - a.X, dy = b.Y - a.Y;
            if (Math.Abs(dx) < 0.001f && Math.Abs(dy) < 0.001f)
                return (float)Math.Sqrt((p.X - a.X) * (p.X - a.X) + (p.Y - a.Y) * (p.Y - a.Y));
            float t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / (dx * dx + dy * dy);
            t = Math.Max(0f, Math.Min(1f, t));
            float projX = a.X + t * dx, projY = a.Y + t * dy;
            return (float)Math.Sqrt((p.X - projX) * (p.X - projX) + (p.Y - projY) * (p.Y - projY));
        }

        internal void Draw(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            switch (Kind)
            {
                case LectureTool.Pen:
                    if (Points.Count == 1)
                    {
                        using (var b = new SolidBrush(Color))
                            g.FillEllipse(b, Points[0].X - Width / 2f, Points[0].Y - Width / 2f, Width, Width);
                    }
                    else if (Points.Count > 1)
                    {
                        using (var pen = new Pen(Color, Width) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                            g.DrawLines(pen, Points.ToArray());
                    }
                    break;

                case LectureTool.Highlighter:
                    Color hiColor = Color.FromArgb(105, Color.R, Color.G, Color.B);
                    float hiWidth = Math.Max(16f, Width * 4.5f);
                    if (Points.Count == 1)
                    {
                        using (var b = new SolidBrush(hiColor))
                            g.FillEllipse(b, Points[0].X - hiWidth / 2f, Points[0].Y - hiWidth / 2f, hiWidth, hiWidth);
                    }
                    else if (Points.Count > 1)
                    {
                        using (var pen = new Pen(hiColor, hiWidth) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                            g.DrawLines(pen, Points.ToArray());
                    }
                    break;

                case LectureTool.Line:
                    using (var pen = new Pen(Color, Width) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                        g.DrawLine(pen, Start, End);
                    break;

                case LectureTool.Arrow:
                    DrawArrow(g, Start, End, Color, Width);
                    break;

                case LectureTool.Rectangle:
                    var rect = NormalizedRect(Start, End);
                    if (rect.Width > 1 && rect.Height > 1)
                    {
                        using (var path = Glyphs.Round(rect, Math.Min(8f, Math.Min(rect.Width, rect.Height) * 0.15f)))
                        using (var pen = new Pen(Color, Width) { LineJoin = LineJoin.Round })
                            g.DrawPath(pen, path);
                    }
                    break;

                case LectureTool.Ellipse:
                    var ell = NormalizedRect(Start, End);
                    if (ell.Width > 1 && ell.Height > 1)
                    {
                        using (var pen = new Pen(Color, Width))
                            g.DrawEllipse(pen, ell);
                    }
                    break;

                case LectureTool.Number:
                    float radius = Math.Max(15f, FontSize * 0.56f);
                    var circle = new RectangleF(Start.X - radius, Start.Y - radius, radius * 2, radius * 2);
                    using (var shadow = new SolidBrush(Color.FromArgb(55, 0, 0, 0)))
                        g.FillEllipse(shadow, circle.X + 1.5f, circle.Y + 2f, circle.Width, circle.Height);
                    using (var fill = new SolidBrush(Color))
                        g.FillEllipse(fill, circle);
                    using (var ring = new Pen(Color.White, 2.2f))
                        g.DrawEllipse(ring, circle);
                    using (var font = AnnotationFonts.Create(FontName, radius * 1.15f, true))
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    using (var textBrush = new SolidBrush(Color.GetBrightness() > 0.72f ? Style.Ink : Color.White))
                        g.DrawString(Number.ToString(), font, textBrush, circle, sf);
                    break;

                case LectureTool.Text:
                    if (string.IsNullOrEmpty(Text)) break;
                    using (var font = AnnotationFonts.Create(FontName, FontSize, true))
                    {
                        // Subtle contrast shadow for visibility on both light and dark slides
                        Color shadowColor = Color.GetBrightness() > 0.65f ? Color.FromArgb(160, 0, 0, 0) : Color.FromArgb(160, 255, 255, 255);
                        using (var sb = new SolidBrush(shadowColor))
                            g.DrawString(Text, font, sb, new PointF(Start.X + 1.5f, Start.Y + 1.5f));
                        using (var tb = new SolidBrush(Color))
                            g.DrawString(Text, font, tb, Start);
                    }
                    break;
            }
        }

        internal static RectangleF NormalizedRect(PointF a, PointF b)
        {
            return RectangleF.FromLTRB(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
        }

        internal static void DrawArrow(Graphics g, PointF start, PointF end, Color color, float width)
        {
            float dx = end.X - start.X, dy = end.Y - start.Y;
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len < 2f) return;
            float headLen = Math.Max(14f, width * 3.8f);
            float headHalfWidth = Math.Max(7f, width * 1.95f);
            float ux = dx / len, uy = dy / len;
            PointF basePt = len > headLen ? new PointF(end.X - ux * headLen * 0.75f, end.Y - uy * headLen * 0.75f) : start;
            using (var pen = new Pen(color, width) { StartCap = LineCap.Round, EndCap = LineCap.Flat })
                g.DrawLine(pen, start, basePt);

            PointF left = new PointF(end.X - ux * headLen - uy * headHalfWidth, end.Y - uy * headLen + ux * headHalfWidth);
            PointF right = new PointF(end.X - ux * headLen + uy * headHalfWidth, end.Y - uy * headLen - ux * headHalfWidth);
            using (var brush = new SolidBrush(color))
                g.FillPolygon(brush, new[] { end, left, right });
        }
    }

    internal static class ZoomViewport
    {
        internal static RectangleF ComputeSourceRect(Size screenSize, float zoom, PointF focusWorld)
        {
            zoom = Math.Max(1.0f, Math.Min(8.0f, zoom));
            float viewW = screenSize.Width / zoom;
            float viewH = screenSize.Height / zoom;
            float left = focusWorld.X - viewW / 2f;
            float top = focusWorld.Y - viewH / 2f;
            left = Math.Max(0f, Math.Min(screenSize.Width - viewW, left));
            top = Math.Max(0f, Math.Min(screenSize.Height - viewH, top));
            return new RectangleF(left, top, viewW, viewH);
        }

        internal static PointF ClientToWorld(Point clientPt, Size clientSize, RectangleF sourceRect)
        {
            if (clientSize.Width <= 0 || clientSize.Height <= 0) return PointF.Empty;
            float wx = sourceRect.X + (clientPt.X / (float)clientSize.Width) * sourceRect.Width;
            float wy = sourceRect.Y + (clientPt.Y / (float)clientSize.Height) * sourceRect.Height;
            return new PointF(wx, wy);
        }

        internal static PointF WorldToClient(PointF worldPt, Size clientSize, RectangleF sourceRect)
        {
            if (sourceRect.Width <= 0 || sourceRect.Height <= 0) return PointF.Empty;
            float cx = ((worldPt.X - sourceRect.X) / sourceRect.Width) * clientSize.Width;
            float cy = ((worldPt.Y - sourceRect.Y) / sourceRect.Height) * clientSize.Height;
            return new PointF(cx, cy);
        }
    }
}
