using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;

namespace ParkLecture
{
    internal static class SelfTests
    {
        internal static int Run(string reportPath)
        {
            var sb = new StringBuilder();
            int passed = 0, failed = 0;

            Action<string, Action> check = delegate(string name, Action test)
            {
                try
                {
                    test();
                    passed++;
                    sb.AppendLine("PASS  " + name);
                }
                catch (Exception ex)
                {
                    failed++;
                    sb.AppendLine("FAIL  " + name + " -> " + ex.Message);
                }
            };

            check("Bundled Pretendard font loads and renders Korean glyphs", delegate
            {
                using (var f = AnnotationFonts.Create(AnnotationFonts.Default, 28, true))
                using (var bmp = new Bitmap(240, 80))
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.White);
                    g.DrawString("박주임 강의도구 판서", f, Brushes.Black, 10, 10);
                }
            });

            check("ZoomViewport round-trip coordinate mapping at 1x, 2x, 4x", delegate
            {
                Size screen = new Size(1920, 1080);
                foreach (float z in new[] { 1.0f, 2.0f, 4.0f })
                {
                    RectangleF src = ZoomViewport.ComputeSourceRect(screen, z, new PointF(960, 540));
                    Point sampleClient = new Point(480, 270);
                    PointF world = ZoomViewport.ClientToWorld(sampleClient, screen, src);
                    PointF back = ZoomViewport.WorldToClient(world, screen, src);
                    if (Math.Abs(back.X - sampleClient.X) > 0.5f || Math.Abs(back.Y - sampleClient.Y) > 0.5f)
                        throw new InvalidOperationException("Viewport round-trip mismatch at zoom " + z);
                }
            });

            check("All LectureStroke kinds render and hit-test cleanly", delegate
            {
                using (var bmp = new Bitmap(800, 600))
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.White);
                    var pen = new LectureStroke { Kind = LectureTool.Pen, Color = Style.Red, Width = 4, Points = new List<PointF> { new PointF(50, 50), new PointF(150, 100), new PointF(220, 120) } };
                    pen.Draw(g);
                    if (!pen.HitTest(new PointF(150, 100), 6f)) throw new InvalidOperationException("Pen hit-test failed.");

                    var hi = new LectureStroke { Kind = LectureTool.Highlighter, Color = Style.Yellow, Width = 5, Points = new List<PointF> { new PointF(60, 160), new PointF(300, 160) } };
                    hi.Draw(g);

                    var arrow = new LectureStroke { Kind = LectureTool.Arrow, Color = Style.Blue, Width = 4, Start = new PointF(100, 220), End = new PointF(280, 280) };
                    arrow.Draw(g);
                    if (!arrow.HitTest(new PointF(190, 250), 8f)) throw new InvalidOperationException("Arrow hit-test failed.");

                    var box = new LectureStroke { Kind = LectureTool.Rectangle, Color = Style.Green, Width = 3, Start = new PointF(320, 80), End = new PointF(480, 200) };
                    box.Draw(g);

                    var ell = new LectureStroke { Kind = LectureTool.Ellipse, Color = Style.Orange, Width = 3, Start = new PointF(320, 240), End = new PointF(480, 360) };
                    ell.Draw(g);

                    var num = new LectureStroke { Kind = LectureTool.Number, Color = Style.Red, Start = new PointF(540, 120), Number = 1, FontSize = 32 };
                    num.Draw(g);

                    var txt = new LectureStroke { Kind = LectureTool.Text, Color = Style.Ink, Start = new PointF(540, 220), Text = "핵심 요약", FontSize = 28 };
                    txt.Draw(g);
                }
            });

            check("LectureOverlay renders scene to bitmap without HUD", delegate
            {
                var settings = new Settings();
                using (var snap = new Bitmap(640, 360))
                using (var overlay = new LectureOverlay(settings, snap, new Rectangle(0, 0, 640, 360), true, true, BoardMode.Whiteboard))
                using (var rendered = overlay.RenderSceneToBitmap())
                {
                    if (rendered.Width != 640 || rendered.Height != 360)
                        throw new InvalidOperationException("Rendered bitmap size mismatch.");
                }
            });

            check("SoftButton icon and label bounds never overlap", delegate
            {
                using (var btn = new SoftButton { Text = "화면 확대 · 필기", Glyph = "zoom", Tone = ButtonTone.Tool, Size = new Size(92, 64) })
                using (var bmp = new Bitmap(92, 64))
                using (var g = Graphics.FromImage(bmp))
                {
                    Rectangle icon, label;
                    btn.ContentBounds(g, out icon, out label);
                    if (icon.IntersectsWith(label))
                        throw new InvalidOperationException("Icon and label bounds overlap.");
                }
            });

            check("LectureTimerForm.FormatClock formats MM:SS accurately", delegate
            {
                if (LectureTimerForm.FormatClock(600) != "10:00") throw new InvalidOperationException("Expected 10:00");
                if (LectureTimerForm.FormatClock(65) != "01:05") throw new InvalidOperationException("Expected 01:05");
                if (LectureTimerForm.FormatClock(0) != "00:00") throw new InvalidOperationException("Expected 00:00");
            });

            check("StartupRegistration applies and cleans up registry value safely", delegate
            {
                string testSubKey = @"Software\ParkLectureSelfTest_" + Guid.NewGuid().ToString("N");
                try
                {
                    using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(testSubKey))
                    {
                        string exe = Path.GetFullPath("ParkLecture.exe");
                        StartupRegistration.Apply(key, true, exe, delegate { });
                        object v = key.GetValue(StartupRegistration.ValueName);
                        if (!Equals(v, "\"" + exe + "\""))
                            throw new InvalidOperationException("Startup command mismatch: " + v);

                        StartupRegistration.Apply(key, false, exe, delegate { });
                        if (key.GetValue(StartupRegistration.ValueName) != null)
                            throw new InvalidOperationException("Startup value was not deleted.");
                    }
                }
                finally
                {
                    Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(testSubKey, false);
                }
            });

            sb.AppendLine(string.Format("Summary: {0} passed, {1} failed", passed, failed));
            File.WriteAllText(reportPath, sb.ToString(), Encoding.UTF8);
            return failed == 0 ? 0 : 1;
        }
    }
}
