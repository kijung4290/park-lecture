using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ParkLecture
{
    internal sealed class HelpDialog : Form
    {
        internal HelpDialog(Settings settings)
        {
            Text = Words.T("도움말 및 단축키 안내", "Help & Keyboard Shortcuts");
            ClientSize = new Size(760, 610);
            Font = Style.Font(9.5f);
            BackColor = Style.Sidebar;
            ForeColor = Style.Ink;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Icon = Branding.AppIcon;

            var canvas = new HelpPanel(settings) { Dock = DockStyle.Fill };
            var bottom = new Surface { Dock = DockStyle.Bottom, Height = 56, BackColor = Color.White, TopLine = true, Padding = new Padding(18, 8, 18, 8) };
            var closeBtn = Style.Button(Words.T("닫기", "Close"), delegate { DialogResult = DialogResult.OK; }, true);
            closeBtn.Location = new Point(760 - 110, 10);
            bottom.Controls.Add(closeBtn);
            CancelButton = closeBtn;
            AcceptButton = closeBtn;

            Controls.Add(canvas);
            Controls.Add(bottom);
        }

        private sealed class HelpPanel : Panel
        {
            private readonly Settings settings;
            internal HelpPanel(Settings settings)
            {
                this.settings = settings;
                DoubleBuffered = true;
                BackColor = Style.Sidebar;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Style.Sidebar);

                int x = 24, y = 20, w = Width - 48;

                using (var titleFont = Style.Font(13f, FontStyle.Bold))
                    TextRenderer.DrawText(g, Words.T("전역 단축키 (PPT 발표 · 강의 중 언제든 실행)", "Global Shortcuts (Available anytime during presentations)"), titleFont, new Rectangle(x, y, w, 28), Style.Ink, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                y += 34;

                var globalCard = new RectangleF(x, y, w, 148);
                DrawCard(g, globalCard);

                string[,] globals =
                {
                    { ShortcutKeys.Format(settings.ZoomModifiers, settings.ZoomHotkey), "화면 확대 (Zoom) — 마우스 이동으로 탐색, 휠로 배율 조절, 좌클릭 시 즉시 필기 전환" },
                    { ShortcutKeys.Format(settings.DrawModifiers, settings.DrawHotkey), "화면 필기 (Draw) — 현재 강의 화면 위에 바로 펜·형광펜·화살표·도형·글자 판서" },
                    { ShortcutKeys.Format(settings.HaloModifiers, settings.HaloHotkey), "마우스 강조 토글 — 슬라이드 클릭이 가능한 투과형 형광 원 + 클릭 파동 켜기/끄기" },
                    { ShortcutKeys.Format(settings.TimerModifiers, settings.TimerHotkey), "강의 타이머 — 쉬는 시간·실습 시간 대형 카운트다운 시계 및 예상 시작 시각 표시" }
                };

                DrawRows(g, globals, (int)globalCard.X + 18, (int)globalCard.Y + 12, (int)globalCard.Width - 36, 31, 120, true);
                y += 168;

                using (var titleFont = Style.Font(13f, FontStyle.Bold))
                    TextRenderer.DrawText(g, Words.T("화면 확대 · 필기 중 단축키 (ZoomIt 호환 + 확장)", "In-Overlay Drawing & Zoom Shortcuts"), titleFont, new Rectangle(x, y, w, 28), Style.Ink, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                y += 34;

                var overlayCard = new RectangleF(x, y, w, 304);
                DrawCard(g, overlayCard);

                string[,] overlayKeys =
                {
                    { "Shift / Ctrl / Tab + 드래그", "펜 모드에서 직선 / 사각형 / 원을 즉시 그리기" },
                    { "Ctrl + Shift + 드래그", "펜 모드에서 화살표를 즉시 그리기" },
                    { "P · H · L · A · U · C", "펜 · 형광펜 · 직선 · 화살표 · 사각형 · 원 도구 전환" },
                    { "N · T · Q · X", "순서 번호(1,2,3) · 텍스트 입력 · 레이저 포인터 · 부분 지우개" },
                    { "1 ~ 8 또는 R·G·B·Y·O·M", "빨강 · 파랑 · 초록 · 노랑 · 주황 · 보라 · 검정 · 흰색 색상 변경" },
                    { "Ctrl + 마우스 휠  (또는 [ / ])", "펜 굵기 조절 · 마우스 휠(또는 +/-): 화면 확대 배율 조절" },
                    { "W / K / D", "화이트보드(흰 배경) / 칠판(진녹색 배경) / 원본 강의 화면 전환" },
                    { "S  (Shift + 휠로 크기 조절)", "스포트라이트 조명 켜기/끄기 (마우스 주변만 밝게 비추기)" },
                    { "E  /  Ctrl+Z  /  Ctrl+C", "전체 지우기 / 실행 취소 / 판서 화면 클립보드 복사 및 보관함 저장" },
                    { "마우스 우클릭 또는 Esc", "확대·필기 종료 후 발표 화면으로 즉시 복귀 (F1: 하단 도구바 숨김)" }
                };

                DrawRows(g, overlayKeys, (int)overlayCard.X + 18, (int)overlayCard.Y + 12, (int)overlayCard.Width - 36, 28, 190, false);
            }

            private static void DrawCard(Graphics g, RectangleF rect)
            {
                using (var path = Glyphs.Round(rect, 12))
                using (var fill = new SolidBrush(Color.White))
                using (var pen = new Pen(Style.Line))
                {
                    g.FillPath(fill, path);
                    g.DrawPath(pen, path);
                }
            }

            private static void DrawRows(Graphics g, string[,] rows, int x, int y, int width, int rowH, int badgeWidth, bool highlightBadge)
            {
                using (var keyFont = new Font("Segoe UI", 8.5f, FontStyle.Bold))
                using (var descFont = Style.Font(8.8f))
                {
                    for (int i = 0; i < rows.GetLength(0); i++)
                    {
                        var badge = new Rectangle(x, y + 2, badgeWidth, rowH - 5);
                        using (var p = Glyphs.Round(badge, 6))
                        using (var b = new SolidBrush(highlightBadge ? Style.BlueWash : Style.Sidebar))
                            g.FillPath(b, p);
                        TextRenderer.DrawText(g, rows[i, 0], keyFont, badge, highlightBadge ? Style.Blue : Style.Ink, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                        TextRenderer.DrawText(g, rows[i, 1], descFont, new Rectangle(badge.Right + 14, y, width - badgeWidth - 14, rowH), Style.Ink, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
                        y += rowH;
                    }
                }
            }
        }
    }
}
