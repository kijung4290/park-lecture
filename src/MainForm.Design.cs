using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ParkLecture
{
    internal sealed partial class MainForm
    {
        private Label toolHint, zoomLabel, widthLabel, fontLabel, fontFamilyLabel;
        private SoftButton copyButton, exportButton, helpButton, deleteMarkButton, clearBoardButton, undoButton, redoButton, customColor;
        private SoftButton sidebarHaloBtn, emptyZoomDraw, emptyWhiteboard, emptyImport;
        private FlowLayoutPanel styleOptions, emptyActions;
        private NumericUpDown strokeInput, fontInput;
        private ComboBox fontFamilyInput;
        private TextBox search;
        private readonly List<SoftButton> filterButtons = new List<SoftButton>(), swatches = new List<SoftButton>();
        private bool updatingStyle;

        private SoftButton ActionButton(string text, string glyph, EventHandler action, ButtonTone tone = ButtonTone.Normal)
        {
            var button = new SoftButton { Text = text, Glyph = glyph, Tone = tone, AccessibleName = text.Length > 0 ? text : glyph };
            button.Click += action;
            return button;
        }

        private static TableLayoutPanel Grid(int rows)
        {
            var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = rows, Margin = Padding.Empty, Padding = Padding.Empty };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            return table;
        }

        private static Label Caption(string text, float size = 9, bool bold = false)
        {
            return new Label
            {
                Text = text,
                Font = Style.Font(size, bold ? FontStyle.Bold : FontStyle.Regular),
                ForeColor = Style.Ink,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Margin = Padding.Empty,
                Dock = DockStyle.Fill
            };
        }

        private void BuildUI()
        {
            var root = Grid(3);
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            Controls.Add(root);
            root.Controls.Add(BuildBrandFooter(), 0, 2);

            // Header
            var header = new Surface { Dock = DockStyle.Fill, BackColor = Color.White, BottomLine = true, Margin = Padding.Empty, Padding = new Padding(18, 10, 18, 10) };
            var headerGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
            headerGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            headerGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 252));
            headerGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            headerGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var brand = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
            var mark = new BrandMark { Location = new Point(0, 10) };
            brand.Controls.Add(mark);
            var name = Caption(Words.T("박주임 강의도구", "Park Lecture"), 14, true);
            name.Dock = DockStyle.None;
            name.Bounds = new Rectangle(46, 6, 200, 42);
            brand.Controls.Add(name);
            headerGrid.Controls.Add(brand, 0, 0);

            var documentHeading = Grid(2);
            documentHeading.Padding = new Padding(10, 4, 8, 4);
            documentHeading.RowStyles.Add(new RowStyle(SizeType.Percent, 54));
            documentHeading.RowStyles.Add(new RowStyle(SizeType.Percent, 46));
            title = Caption(Words.T("강의를 시작해 보세요", "Ready when you are"), 10, true);
            detail = Caption(Words.T("화면을 확대하고 필요한 곳에 바로 필기하세요.", "Zoom in on your screen and annotate clearly."), 8.5f);
            detail.ForeColor = Style.Muted;
            documentHeading.Controls.Add(title, 0, 0);
            documentHeading.Controls.Add(detail, 0, 1);
            headerGrid.Controls.Add(documentHeading, 1, 0);

            var headerActions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false, Padding = new Padding(0, 7, 0, 0), Margin = Padding.Empty };
            copyButton = ActionButton(Words.T("복사", "Copy"), "copy", delegate { CopyCurrentImage(); });
            tips.SetToolTip(copyButton, Words.T("이미지를 클립보드에 복사 · Ctrl+C", "Copy image to clipboard · Ctrl+C"));
            exportButton = ActionButton(Words.T("이미지 저장", "Save image"), "save", delegate { ExportCurrentImage(); }, ButtonTone.Primary);
            tips.SetToolTip(exportButton, Words.T("판서한 이미지를 PNG 또는 JPG로 저장", "Save annotated image as PNG or JPEG"));
            helpButton = ActionButton(Words.T("도움말", "Help"), "help", delegate { ShowHelp(); });
            tips.SetToolTip(helpButton, Words.T("단축키 안내 및 사용 방법 보기 · F1", "View keyboard shortcuts and usage guide · F1"));
            var more = ActionButton("", "more", delegate { }, ButtonTone.Quiet);
            tips.SetToolTip(more, Words.T("파일 및 도구 메뉴", "File and tools menu"));
            more.AccessibleName = Words.T("파일 메뉴", "File menu");

            var fileMenu = new ContextMenuStrip();
            fileMenu.Items.Add(Words.T("새 화이트보드 만들기", "New whiteboard"), null, delegate { StartCanvasBoard(BoardMode.Whiteboard); });
            fileMenu.Items.Add(Words.T("새 칠판 만들기", "New blackboard"), null, delegate { StartCanvasBoard(BoardMode.Blackboard); });
            fileMenu.Items.Add(new ToolStripSeparator());
            fileMenu.Items.Add(Words.T("이미지 가져오기…", "Import image…"), null, delegate { ImportImage(); });
            fileMenu.Items.Add(Words.T("클립보드에서 가져오기  Ctrl+V", "Paste image  Ctrl+V"), null, delegate { PasteImage(); });
            fileMenu.Items.Add(Words.T("선택한 파일 위치 열기", "Show selected file in folder"), null, delegate { if (editor.PathName != null) Process.Start("explorer.exe", "/select," + Quote(editor.PathName)); });
            fileMenu.Items.Add(new ToolStripSeparator());
            fileMenu.Items.Add(Words.T("휴지통으로 이동…", "Move to Recycle Bin…"), null, delegate { RecycleSelected(); });
            fileMenu.Items.Add(Words.T("단축키 및 도움말…  F1", "Shortcuts & Help…  F1"), null, delegate { ShowHelp(); });
            fileMenu.Items.Add(Words.T("업데이트 확인…", "Check for updates…"), null, async delegate { await CheckForUpdates(true); });
            more.Click += delegate { fileMenu.Show(more, new Point(0, more.Height)); };

            headerActions.Controls.AddRange(new Control[] { copyButton, exportButton, helpButton, more });
            headerGrid.Controls.Add(headerActions, 2, 0);
            header.Controls.Add(headerGrid);
            root.Controls.Add(header, 0, 0);

            // Split Workspace
            var split = new SplitContainer
            {
                Size = new Size(1300, 700),
                Dock = DockStyle.Fill,
                FixedPanel = FixedPanel.Panel1,
                SplitterDistance = 266,
                Panel1MinSize = 250,
                SplitterWidth = 1,
                IsSplitterFixed = true,
                Margin = Padding.Empty,
                BackColor = Style.Line
            };
            root.Controls.Add(split, 0, 1);
            split.Panel1.BackColor = Style.Sidebar;
            split.Panel2.BackColor = Style.Paper;

            BuildSidebar(split.Panel1);

            var workspace = Grid(4);
            workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
            workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            workspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            split.Panel2.Controls.Add(workspace);

            // Top Toolbar: Direct Drawing Tools + Quick Board/Spotlight Actions
            var toolSurface = new Surface { Dock = DockStyle.Fill, BackColor = Color.White, BottomLine = true, Margin = Padding.Empty };
            var tools = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(14, 5, 10, 4), Margin = Padding.Empty, WrapContents = true };
            bool sizingTools = false;
            tools.Layout += delegate
            {
                if (sizingTools || !tools.Visible) return;
                int bottom = tools.Padding.Top;
                foreach (Control child in tools.Controls) if (child.Visible) bottom = Math.Max(bottom, child.Bottom + child.Margin.Bottom);
                int height = Math.Max(74, bottom + tools.Padding.Bottom + 1);
                if (Math.Abs(workspace.RowStyles[0].Height - height) < 1) return;
                sizingTools = true; workspace.RowStyles[0].Height = height; sizingTools = false;
            };
            Shown += delegate { tools.PerformLayout(); };

            AddTool(tools, LectureTool.Select, "선택", "Select", "select", "요소를 클릭해 이동하거나 삭제합니다.");
            AddTool(tools, LectureTool.Pen, "펜", "Pen", "pen", "드래그해서 자유롭게 선을 그립니다.");
            AddTool(tools, LectureTool.Highlighter, "형광펜", "Marker", "highlighter", "중요한 문장이나 영역에 반투명 형광펜을 칠합니다.");
            AddTool(tools, LectureTool.Line, "직선", "Line", "line", "드래그해서 반듯한 직선을 그립니다.");
            AddTool(tools, LectureTool.Arrow, "화살표", "Arrow", "arrow", "드래그해서 가리킬 곳에 화살표를 표시합니다.");
            AddTool(tools, LectureTool.Rectangle, "사각형", "Box", "rectangle", "드래그해서 강조 사각형을 그립니다.");
            AddTool(tools, LectureTool.Ellipse, "원", "Ellipse", "ellipse", "드래그해서 원을 그립니다.");
            AddTool(tools, LectureTool.Number, "번호", "Number", "number", "클릭할 때마다 1, 2, 3 순서대로 번호를 붙입니다.");
            AddTool(tools, LectureTool.Text, "글자", "Text", "text", "원하는 위치를 클릭해 글자를 넣습니다.");
            AddTool(tools, LectureTool.Eraser, "지우개", "Eraser", "eraser", "그려진 선을 클릭하거나 드래그해서 지웁니다.");
            Separator(tools);

            var wbBtn = ActionButton(Words.T("화이트보드", "Whiteboard"), "whiteboard", delegate { StartCanvasBoard(BoardMode.Whiteboard); }, ButtonTone.Tool);
            wbBtn.Font = Style.Font(8.5f); wbBtn.MinimumSize = new Size(64, 60); wbBtn.Margin = Padding.Empty;
            tips.SetToolTip(wbBtn, Words.T("새 흰색 칠판을 열어 바로 판서합니다.", "Open a clean whiteboard."));
            tools.Controls.Add(wbBtn);

            var bbBtn = ActionButton(Words.T("칠판", "Blackboard"), "blackboard", delegate { StartCanvasBoard(BoardMode.Blackboard); }, ButtonTone.Tool);
            bbBtn.Font = Style.Font(8.5f); bbBtn.MinimumSize = new Size(60, 60); bbBtn.Margin = Padding.Empty;
            tips.SetToolTip(bbBtn, Words.T("새 진녹색 칠판을 열어 바로 판서합니다.", "Open a clean blackboard."));
            tools.Controls.Add(bbBtn);

            var spotBtn = ActionButton(Words.T("스포트라이트", "Spotlight"), "spotlight", async delegate { await LaunchOverlay(false, true, BoardMode.Desktop); }, ButtonTone.Tool);
            spotBtn.Font = Style.Font(8.5f); spotBtn.MinimumSize = new Size(68, 60); spotBtn.Margin = Padding.Empty;
            tips.SetToolTip(spotBtn, Words.T("현재 화면을 어둡게 하고 마우스 주변만 집중 조명합니다.", "Spotlight the area around the cursor."));
            tools.Controls.Add(spotBtn);

            toolSurface.Controls.Add(tools);
            workspace.Controls.Add(toolSurface, 0, 0);
            workspace.Controls.Add(BuildContextBar(), 0, 1);

            editor = new BoardEditor
            {
                Dock = DockStyle.Fill,
                AllowDrop = true,
                Margin = Padding.Empty,
                MarkColor = Color.FromArgb(settings.PenColorArgb),
                LineWidth = settings.PenWidth,
                TextSize = settings.FontSize,
                TextFontName = settings.FontName
            };
            editor.Changed += delegate { saveTimer.Stop(); saveTimer.Start(); UpdateStatus(); };
            editor.ViewChanged += delegate { UpdateStatus(); };
            editor.DragEnter += delegate(object sender, DragEventArgs e) { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
            editor.DragDrop += delegate(object sender, DragEventArgs e) { var paths = e.Data.GetData(DataFormats.FileDrop) as string[]; if (paths != null && paths.Length > 0) ImportPath(paths[0]); };
            workspace.Controls.Add(editor, 0, 2);

            emptyActions = new FlowLayoutPanel { BackColor = Style.Paper, Size = new Size(460, 48), WrapContents = false, FlowDirection = FlowDirection.LeftToRight };
            emptyZoomDraw = ActionButton(Words.T("화면 확대 · 필기", "Screen Zoom & Draw"), "zoom", async delegate { await LaunchOverlay(false, false, BoardMode.Desktop); }, ButtonTone.Primary);
            emptyWhiteboard = ActionButton(Words.T("새 화이트보드", "New Whiteboard"), "whiteboard", delegate { StartCanvasBoard(BoardMode.Whiteboard); });
            emptyImport = ActionButton(Words.T("이미지 가져오기", "Import Image"), "image", delegate { ImportImage(); });
            emptyActions.Controls.AddRange(new Control[] { emptyZoomDraw, emptyWhiteboard, emptyImport });
            editor.Controls.Add(emptyActions);
            editor.Resize += delegate { PositionEmptyActions(); };
            PositionEmptyActions();

            workspace.Controls.Add(BuildFooter(), 0, 3);
            ChooseTool(LectureTool.Pen);
            UpdateStatus();
        }

        private void AddTool(FlowLayoutPanel panel, LectureTool tool, string ko, string en, string glyph, string hint)
        {
            var button = ActionButton(Words.T(ko, en), glyph, delegate { ChooseTool(tool); }, ButtonTone.Tool);
            button.Font = Style.Font(8.5f);
            button.MinimumSize = new Size(60, 60);
            button.Margin = Padding.Empty;
            tips.SetToolTip(button, hint);
            toolButtons[tool] = button;
            panel.Controls.Add(button);
        }

        private static void Separator(FlowLayoutPanel panel)
        {
            panel.Controls.Add(new Panel { Width = 1, Height = 30, BackColor = Style.Line, Margin = new Padding(5, 14, 5, 0) });
        }

        private void ChooseTool(LectureTool tool)
        {
            if (editor != null) editor.SetTool(tool);
            foreach (var entry in toolButtons) entry.Value.Selected = entry.Key == tool;
            UpdateStatus();
        }

        private void BuildSidebar(Control parent)
        {
            var sidebar = Grid(7);
            sidebar.BackColor = Style.Sidebar;
            sidebar.Padding = new Padding(14, 14, 14, 8);
            foreach (float height in new[] { 54f, 40f, 48f, 38f, 44f })
                sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            sidebar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            parent.Controls.Add(sidebar);

            var startBtn = ActionButton(Words.T("화면 확대 · 필기", "Screen Zoom & Draw"), "plus", async delegate { await LaunchOverlay(false, false, BoardMode.Desktop); }, ButtonTone.Primary);
            startBtn.AutoSize = false;
            startBtn.Dock = DockStyle.Fill;
            startBtn.Margin = new Padding(2, 0, 2, 10);
            startBtn.Font = Style.Font(10);
            tips.SetToolTip(startBtn, Words.T("현재 화면을 고정하고 바로 확대·필기합니다 · ", "Annotate or zoom current screen · ") + ShortcutKeys.Format(settings.DrawModifiers, settings.DrawHotkey));
            sidebar.Controls.Add(startBtn, 0, 0);

            var quick = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
            quick.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            quick.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            quick.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));

            var zoomOnlyBtn = ActionButton(Words.T("확대", "Zoom"), "zoom", async delegate { await LaunchOverlay(true, false, BoardMode.Desktop); }, ButtonTone.Quiet);
            zoomOnlyBtn.AutoSize = false; zoomOnlyBtn.Dock = DockStyle.Fill; zoomOnlyBtn.Margin = new Padding(1);
            tips.SetToolTip(zoomOnlyBtn, Words.T("실시간 화면 확대 · ", "Live screen zoom · ") + ShortcutKeys.Format(settings.ZoomModifiers, settings.ZoomHotkey));

            sidebarHaloBtn = ActionButton(Words.T("마우스", "Cursor"), "cursor", delegate { ToggleCursorHalo(); }, ButtonTone.Quiet);
            sidebarHaloBtn.AutoSize = false; sidebarHaloBtn.Dock = DockStyle.Fill; sidebarHaloBtn.Margin = new Padding(1);
            tips.SetToolTip(sidebarHaloBtn, Words.T("투과형 마우스 형광 원 & 클릭 파동 토글 · ", "Toggle cursor highlight · ") + ShortcutKeys.Format(settings.HaloModifiers, settings.HaloHotkey));

            var timerBtn = ActionButton(Words.T("타이머", "Timer"), "timer", delegate { LaunchTimer(); }, ButtonTone.Quiet);
            timerBtn.AutoSize = false; timerBtn.Dock = DockStyle.Fill; timerBtn.Margin = new Padding(1);
            tips.SetToolTip(timerBtn, Words.T("강의 · 쉬는 시간 타이머 · ", "Lecture countdown timer · ") + ShortcutKeys.Format(settings.TimerModifiers, settings.TimerHotkey));

            quick.Controls.Add(zoomOnlyBtn, 0, 0);
            quick.Controls.Add(sidebarHaloBtn, 1, 0);
            quick.Controls.Add(timerBtn, 2, 0);
            sidebar.Controls.Add(quick, 0, 1);

            count = Caption(Words.T("보관함", "Library"), 10, true);
            count.Padding = new Padding(6, 12, 0, 0);
            sidebar.Controls.Add(count, 0, 2);

            filter = new ComboBox { Visible = false, DropDownStyle = ComboBoxStyle.DropDownList };
            filter.Items.AddRange(new object[] { Words.T("전체", "All"), Words.T("오늘", "Today"), Words.T("최근 7일", "7 Days") });
            filter.SelectedIndex = 0;
            filter.SelectedIndexChanged += delegate
            {
                for (int i = 0; i < filterButtons.Count; i++) filterButtons[i].Selected = i == filter.SelectedIndex;
                if (!loading) { SaveCurrentBoard(); LoadLibrary(editor == null ? null : editor.PathName); }
            };
            var segmentSurface = new Surface { Dock = DockStyle.Fill, BackColor = Style.Hover, Radius = 9, Padding = new Padding(2), Margin = new Padding(2, 1, 2, 3) };
            var segments = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty, BackColor = Style.Hover };
            for (int i = 0; i < 3; i++)
            {
                segments.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
                int value = i;
                var b = ActionButton((string)filter.Items[i], "", delegate { filter.SelectedIndex = value; }, ButtonTone.Segment);
                b.AutoSize = false; b.Dock = DockStyle.Fill; b.MinimumSize = new Size(30, 24); b.Margin = Padding.Empty; b.Selected = i == 0;
                filterButtons.Add(b);
                segments.Controls.Add(b, i, 0);
            }
            segmentSurface.Controls.Add(segments);
            sidebar.Controls.Add(segmentSurface, 0, 3);
            parent.Controls.Add(filter);

            var searchSurface = new Surface { Dock = DockStyle.Fill, BackColor = Color.White, Radius = 8, Margin = new Padding(2, 7, 2, 8), Padding = new Padding(30, 4, 6, 2) };
            searchSurface.Paint += delegate(object sender, PaintEventArgs e) { Glyphs.Draw(e.Graphics, "search", new RectangleF(8, 6, 15, 15), Style.Muted); };
            search = new TextBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, BackColor = Color.White, Font = Style.Font(9), AccessibleName = "판서 검색" };
            search.HandleCreated += delegate { Native.SendMessage(search.Handle, 0x1501, new IntPtr(1), Words.T("캡처·판서 검색", "Search notes")); };
            search.TextChanged += delegate { if (!loading) LoadLibrary(editor == null ? null : editor.PathName); };
            searchSurface.Controls.Add(search);
            sidebar.Controls.Add(searchSurface, 0, 4);

            thumbnails = new ImageList { ImageSize = new Size(206, 114), ColorDepth = ColorDepth.Depth32Bit };
            library = new CaptureList { Dock = DockStyle.Fill, LargeImageList = thumbnails, MultiSelect = true, HideSelection = false, Margin = Padding.Empty };
            library.SelectedIndexChanged += delegate
            {
                if (loading || library.SelectedItems.Count == 0) return;
                OpenSavedFile((string)library.SelectedItems[0].Tag);
            };
            var context = new ContextMenuStrip();
            context.Items.Add(Words.T("이미지 복사", "Copy image"), null, delegate { CopyCurrentImage(); });
            context.Items.Add(Words.T("이미지 저장…", "Save image…"), null, delegate { ExportCurrentImage(); });
            context.Items.Add(Words.T("파일 위치 열기", "Show in folder"), null, delegate { if (editor.PathName != null) Process.Start("explorer.exe", "/select," + Quote(editor.PathName)); });
            context.Items.Add(new ToolStripSeparator());
            context.Items.Add(Words.T("휴지통으로 이동…", "Move to Recycle Bin…"), null, delegate { RecycleSelected(); });
            library.ContextMenuStrip = context;
            sidebar.Controls.Add(library, 0, 5);

            var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 7, 0, 0), Margin = Padding.Empty };
            footer.Controls.Add(ActionButton(Words.T("저장 폴더", "Folder"), "folder", delegate { Directory.CreateDirectory(settings.Folder); Process.Start("explorer.exe", Quote(settings.Folder)); }, ButtonTone.Quiet));
            footer.Controls.Add(ActionButton(Words.T("설정", "Settings"), "settings", delegate { ShowSettings(); }, ButtonTone.Quiet));
            sidebar.Controls.Add(footer, 0, 6);
        }

        private Control BuildContextBar()
        {
            var surface = new Surface { Dock = DockStyle.Fill, BackColor = Color.White, BottomLine = true, Margin = Padding.Empty, Padding = new Padding(18, 4, 14, 4) };
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, Margin = Padding.Empty };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            toolHint = Caption("", 8.5f);
            toolHint.ForeColor = Style.Muted;
            layout.Controls.Add(toolHint, 0, 0);

            styleOptions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false, Margin = Padding.Empty, Padding = new Padding(0, 4, 0, 0) };
            foreach (Color c in new[] { Style.Red, Style.Blue, Style.Green, Style.Yellow, Style.Orange, Style.Purple, Style.Ink })
            {
                Color color = c;
                var swatch = new SoftButton { Tone = ButtonTone.Swatch, SwatchColor = c, AutoSize = false, Size = new Size(28, 28), MinimumSize = new Size(28, 28), Margin = new Padding(1, 0, 1, 0) };
                swatch.Click += delegate
                {
                    editor.MarkColor = color;
                    settings.PenColorArgb = color.ToArgb();
                    editor.ApplyStyle();
                    UpdateStatus();
                };
                swatches.Add(swatch);
                styleOptions.Controls.Add(swatch);
            }
            customColor = ActionButton("", "more", delegate
            {
                using (var d = new ColorDialog { Color = editor.MarkColor, FullOpen = true })
                    if (d.ShowDialog(this) == DialogResult.OK)
                    {
                        editor.MarkColor = d.Color;
                        settings.PenColorArgb = d.Color.ToArgb();
                        editor.ApplyStyle();
                        UpdateStatus();
                    }
            }, ButtonTone.Quiet);
            customColor.AutoSize = false; customColor.Size = new Size(28, 28); customColor.MinimumSize = new Size(28, 28); customColor.Margin = Padding.Empty;
            styleOptions.Controls.Add(customColor);

            widthLabel = new Label { Text = Words.T("굵기", "Stroke"), AutoSize = true, Margin = new Padding(10, 7, 5, 0), ForeColor = Style.Muted, Font = Style.Font(8.5f) };
            strokeInput = new NumericUpDown { Minimum = 1, Maximum = 24, Value = (decimal)Math.Max(1, Math.Min(24, settings.PenWidth)), Width = 44, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(0, 3, 0, 0) };
            strokeInput.ValueChanged += delegate
            {
                if (!updatingStyle)
                {
                    editor.LineWidth = (float)strokeInput.Value;
                    settings.PenWidth = editor.LineWidth;
                    editor.ApplyStyle();
                }
            };

            fontFamilyLabel = new Label { Text = Words.T("글꼴", "Font"), AutoSize = true, Margin = new Padding(10, 7, 5, 0), ForeColor = Style.Muted, Font = Style.Font(8.5f) };
            fontFamilyInput = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 136, DropDownWidth = 240, Font = Style.Font(8.5f), Margin = new Padding(0, 2, 0, 0) };
            foreach (var choice in AnnotationFonts.Choices()) fontFamilyInput.Items.Add(choice);
            fontFamilyInput.SelectedIndex = 0;
            fontFamilyInput.SelectedIndexChanged += delegate
            {
                if (!updatingStyle && fontFamilyInput.SelectedItem != null)
                {
                    editor.TextFontName = ((FontChoice)fontFamilyInput.SelectedItem).Name;
                    settings.FontName = editor.TextFontName;
                    editor.ApplyStyle();
                }
            };

            fontLabel = new Label { Text = Words.T("크기", "Size"), AutoSize = true, Margin = new Padding(10, 7, 5, 0), ForeColor = Style.Muted, Font = Style.Font(8.5f) };
            fontInput = new NumericUpDown { Minimum = 10, Maximum = 256, Value = (decimal)Math.Max(10, Math.Min(256, settings.FontSize)), Width = 48, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(0, 3, 0, 0) };
            fontInput.ValueChanged += delegate
            {
                if (!updatingStyle)
                {
                    editor.TextSize = (float)fontInput.Value;
                    settings.FontSize = editor.TextSize;
                    editor.ApplyStyle();
                }
            };

            styleOptions.Controls.AddRange(new Control[] { widthLabel, strokeInput, fontFamilyLabel, fontFamilyInput, fontLabel, fontInput });
            layout.Controls.Add(styleOptions, 1, 0);

            deleteMarkButton = ActionButton(Words.T("요소 삭제", "Delete"), "trash", delegate { editor.DeleteSelected(); });
            deleteMarkButton.Margin = new Padding(8, 3, 0, 0);
            layout.Controls.Add(deleteMarkButton, 2, 0);

            clearBoardButton = ActionButton(Words.T("모두 지우기", "Clear all"), "eraser", delegate { editor.ClearStrokes(); }, ButtonTone.Quiet);
            clearBoardButton.Margin = new Padding(6, 3, 0, 0);
            layout.Controls.Add(clearBoardButton, 3, 0);

            surface.Controls.Add(layout);
            return surface;
        }

        private Control BuildFooter()
        {
            var surface = new Surface { Dock = DockStyle.Fill, BackColor = Color.White, TopLine = true, Margin = Padding.Empty, Padding = new Padding(12, 4, 12, 4) };
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var history = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false, Margin = Padding.Empty };
            undoButton = ActionButton("", "undo", delegate { editor.Undo(); }, ButtonTone.Quiet);
            redoButton = ActionButton("", "redo", delegate { editor.Redo(); }, ButtonTone.Quiet);
            tips.SetToolTip(undoButton, Words.T("실행 취소 · Ctrl+Z", "Undo · Ctrl+Z"));
            tips.SetToolTip(redoButton, Words.T("다시 실행 · Ctrl+Y", "Redo · Ctrl+Y"));
            foreach (var b in new[] { undoButton, redoButton }) { b.MinimumSize = new Size(32, 32); b.Margin = new Padding(1); history.Controls.Add(b); }
            layout.Controls.Add(history, 0, 0);

            status = Caption("", 8.5f);
            status.Padding = new Padding(10, 0, 0, 0);
            status.ForeColor = Style.Muted;
            layout.Controls.Add(status, 1, 0);

            var zoom = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false, Margin = Padding.Empty };
            var minus = ActionButton("", "minus", delegate { editor.ZoomBy(1f / 1.2f); }, ButtonTone.Quiet);
            var plus = ActionButton("", "plus", delegate { editor.ZoomBy(1.2f); }, ButtonTone.Quiet);
            zoomLabel = new Label { Text = "—", Width = 48, Height = 34, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 9), ForeColor = Style.Muted, Margin = Padding.Empty };
            var fit = ActionButton(Words.T("맞춤", "Fit"), "fit", delegate { editor.Fit(); }, ButtonTone.Quiet);
            foreach (var b in new[] { minus, plus, fit }) { b.MinimumSize = new Size(30, 32); b.Margin = new Padding(1); }
            zoom.Controls.AddRange(new Control[] { minus, zoomLabel, plus, fit });
            layout.Controls.Add(zoom, 2, 0);

            surface.Controls.Add(layout);
            return surface;
        }

        private Control BuildBrandFooter()
        {
            var surface = new Surface { Dock = DockStyle.Fill, BackColor = Color.White, TopLine = true, Margin = Padding.Empty, Padding = new Padding(16, 0, 16, 0) };
            var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty, Padding = new Padding(0, 2, 0, 0) };
            flow.Controls.Add(new Label { Text = Words.T("만든 사람 · 박주임", "Made by Parkjuim"), AutoSize = true, Font = Style.Font(8), ForeColor = Style.Muted, Margin = new Padding(0, 6, 22, 0) });
            flow.Controls.Add(BrandLink("blog.naver.com/parkjuim90", Branding.BlogUrl));
            flow.Controls.Add(new Label { Text = "·", AutoSize = true, ForeColor = Style.Disabled, Margin = new Padding(12, 5, 12, 0) });
            flow.Controls.Add(BrandLink(Branding.Email, "mailto:" + Branding.Email));
            surface.Controls.Add(flow);
            return surface;
        }

        private LinkLabel BrandLink(string caption, string destination)
        {
            var link = new LinkLabel
            {
                Text = caption,
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5f),
                LinkBehavior = LinkBehavior.HoverUnderline,
                LinkColor = Style.Muted,
                ActiveLinkColor = Style.Blue,
                VisitedLinkColor = Style.Muted,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 5, 0, 0),
                AccessibleName = caption
            };
            link.Links.Clear();
            link.Links.Add(0, caption.Length, destination);
            link.LinkClicked += delegate { Branding.Open(destination); };
            tips.SetToolTip(link, destination);
            return link;
        }

        private void PositionEmptyActions()
        {
            if (emptyActions == null || editor == null) return;
            int w = 0;
            foreach (Control c in emptyActions.Controls)
                if (c.Visible) w += c.GetPreferredSize(Size.Empty).Width + c.Margin.Horizontal;
            emptyActions.Width = Math.Max(260, w + 8);
            emptyActions.Location = new Point((editor.Width - emptyActions.Width) / 2, editor.Height / 2 + 78);
        }

        private void SetStatus(string text)
        {
            if (status != null) status.Text = text;
        }

        private void UpdateStatus()
        {
            if (status == null || editor == null) return;
            bool hasBoard = editor.HasBoard;
            bool haloOn = haloOverlay != null && !haloOverlay.IsDisposed;
            if (sidebarHaloBtn != null) sidebarHaloBtn.Selected = haloOn;

            copyButton.Enabled = hasBoard;
            exportButton.Enabled = hasBoard;
            undoButton.Enabled = hasBoard && editor.CanUndo;
            redoButton.Enabled = hasBoard && editor.CanRedo;
            emptyActions.Visible = !hasBoard;
            PositionEmptyActions();

            zoomLabel.Text = hasBoard ? Math.Round(editor.Zoom * 100) + "%" : "—";
            foreach (var sw in swatches)
                sw.Selected = sw.SwatchColor.ToArgb() == editor.MarkColor.ToArgb();

            LectureStroke sel = editor.SelectedStroke;
            LectureTool active = sel != null ? sel.Kind : editor.Tool;
            bool isText = active == LectureTool.Text || active == LectureTool.Number;
            updatingStyle = true;
            try
            {
                decimal swVal = Math.Max(strokeInput.Minimum, Math.Min(strokeInput.Maximum, (decimal)Math.Round(sel != null ? sel.Width : editor.LineWidth)));
                strokeInput.Value = swVal;
                decimal fsVal = Math.Max(fontInput.Minimum, Math.Min(fontInput.Maximum, (decimal)Math.Round(sel != null ? sel.FontSize : editor.TextSize)));
                fontInput.Value = fsVal;
                string fam = (sel != null ? sel.FontName : editor.TextFontName) ?? AnnotationFonts.Default;
                for (int i = 0; i < fontFamilyInput.Items.Count; i++)
                {
                    var fc = fontFamilyInput.Items[i] as FontChoice;
                    if (fc != null && string.Equals(fc.Name, fam, StringComparison.OrdinalIgnoreCase))
                    {
                        if (fontFamilyInput.SelectedIndex != i) fontFamilyInput.SelectedIndex = i;
                        break;
                    }
                }
            }
            finally
            {
                updatingStyle = false;
            }
            fontFamilyLabel.Visible = fontFamilyInput.Visible = fontLabel.Visible = fontInput.Visible = isText;
            widthLabel.Visible = strokeInput.Visible = !isText && active != LectureTool.Select;
            deleteMarkButton.Visible = hasBoard && sel != null;
            clearBoardButton.Visible = hasBoard && editor.CanClear;

            string hkSummary =
                ShortcutKeys.Format(settings.ZoomModifiers, settings.ZoomHotkey) + Words.T(" 확대  ·  ", " Zoom  ·  ") +
                ShortcutKeys.Format(settings.DrawModifiers, settings.DrawHotkey) + Words.T(" 필기  ·  ", " Draw  ·  ") +
                ShortcutKeys.Format(settings.HaloModifiers, settings.HaloHotkey) + Words.T(" 마우스 강조  ·  ", " Halo  ·  ") +
                ShortcutKeys.Format(settings.TimerModifiers, settings.TimerHotkey) + Words.T(" 타이머", " Timer");

            if (!hasBoard)
            {
                title.Text = Words.T("강의를 시작해 보세요", "Ready when you are");
                detail.Text = Words.T("화면을 확대하고 필요한 곳에 바로 필기하세요.", "Zoom in on your screen and annotate clearly.");
                toolHint.Text = ToolInstruction(editor.Tool);
                SetStatus(hkSummary);
                editor.Invalidate();
                return;
            }

            toolHint.Text = sel != null
                ? Words.T("선택됨 · 드래그하여 이동 · Delete 삭제", "Selected · Drag to move · Delete to remove")
                : ToolInstruction(editor.Tool);
            detail.Text = editor.Dirty
                ? Words.T("변경 내용 자동 저장 중…", "Saving changes…")
                : Words.T("자동 저장됨 · 판서 보관", "Saved automatically");
            SetStatus(editor.BoardSize.Width + " × " + editor.BoardSize.Height + " px   ·   " + hkSummary);
        }

        private static string ToolInstruction(LectureTool tool)
        {
            switch (tool)
            {
                case LectureTool.Select: return Words.T("그려진 요소를 클릭해 이동하거나 삭제하세요.", "Click an element to move or delete it.");
                case LectureTool.Pen: return Words.T("드래그해서 자유롭게 선을 그립니다.", "Drag to draw freehand lines.");
                case LectureTool.Highlighter: return Words.T("드래그해서 반투명 형광펜으로 강조합니다.", "Drag to highlight with a translucent marker.");
                case LectureTool.Line: return Words.T("드래그해서 반듯한 직선을 그립니다.", "Drag to draw a straight line.");
                case LectureTool.Arrow: return Words.T("드래그해서 가리킬 곳에 화살표를 표시합니다.", "Drag to draw an arrow.");
                case LectureTool.Rectangle: return Words.T("드래그해서 사각형을 그립니다.", "Drag to draw a rectangle.");
                case LectureTool.Ellipse: return Words.T("드래그해서 원을 그립니다.", "Drag to draw an ellipse.");
                case LectureTool.Number: return Words.T("클릭할 때마다 1, 2, 3 순서대로 번호가 매겨집니다.", "Click to add sequential numbers 1, 2, 3…");
                case LectureTool.Text: return Words.T("글자를 넣을 위치를 클릭하세요.", "Click where you want to add text.");
                case LectureTool.Eraser: return Words.T("지울 선 위를 클릭하거나 드래그하세요.", "Click or drag over strokes to erase.");
                default: return "";
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F1) { ShowHelp(); return true; }
            if (keyData == (Keys.Control | Keys.Z)) { editor.Undo(); return true; }
            if (keyData == (Keys.Control | Keys.Y) || keyData == (Keys.Control | Keys.Shift | Keys.Z)) { editor.Redo(); return true; }
            if (keyData == (Keys.Control | Keys.C)) { CopyCurrentImage(); return true; }
            if (keyData == (Keys.Control | Keys.S)) { SaveCurrentBoard(); return true; }
            if (keyData == (Keys.Control | Keys.O)) { ImportImage(); return true; }
            if (keyData == (Keys.Control | Keys.V)) { PasteImage(); return true; }
            if (keyData == (Keys.Control | Keys.D0)) { editor.Fit(); return true; }
            if (editor.Focused && (keyData == Keys.Delete || keyData == Keys.Back)) { editor.DeleteSelected(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void SavePreview(string path)
        {
            using (var bitmap = new Bitmap(Width, Height))
            {
                DrawToBitmap(bitmap, new Rectangle(Point.Empty, Size));
                Storage.SavePng(bitmap, path);
            }
        }
    }

    internal sealed class SettingsDialog : Form
    {
        internal Settings Result;

        private sealed class HotkeyRow
        {
            internal CheckBox Ctrl, Shift, Alt;
            internal ComboBox KeyBox;
            internal uint Modifiers { get { return (uint)((Ctrl.Checked ? 2 : 0) | (Shift.Checked ? 4 : 0) | (Alt.Checked ? 1 : 0)); } }
            internal int KeyValue { get { return (int)((ShortcutKeyChoice)KeyBox.SelectedItem).Key; } }
        }

        internal SettingsDialog(Settings current)
        {
            Text = Words.T("설정", "Settings");
            ClientSize = new Size(620, 520);
            Font = Style.Font(9.5f);
            BackColor = Style.Sidebar;
            ForeColor = Style.Ink;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Icon = Branding.AppIcon;

            var folder = new TextBox { Text = current.Folder, Bounds = new Rectangle(24, 44, 468, 28) };
            var browse = Style.Button(Words.T("찾기", "Browse"), delegate
            {
                using (var d = new FolderBrowserDialog { SelectedPath = folder.Text })
                    if (d.ShowDialog(this) == DialogResult.OK) folder.Text = d.SelectedPath;
            });
            browse.Location = new Point(504, 40);

            Controls.Add(MakeLabel(Words.T("저장 폴더", "Save folder"), 24, 18, true));
            Controls.AddRange(new Control[] { folder, browse });

            Controls.Add(MakeLabel(Words.T("강의 전역 단축키", "Global Lecture Shortcuts"), 24, 86, true));
            var zoomRow = AddHotkeyRow(Words.T("화면 확대 (Zoom)", "Screen Zoom"), 116, current.ZoomModifiers, current.ZoomHotkey);
            var drawRow = AddHotkeyRow(Words.T("화면 필기 (Draw)", "Screen Draw"), 154, current.DrawModifiers, current.DrawHotkey);
            var haloRow = AddHotkeyRow(Words.T("마우스 강조 토글", "Cursor Highlight"), 192, current.HaloModifiers, current.HaloHotkey);
            var timerRow = AddHotkeyRow(Words.T("강의 타이머 열기", "Lecture Timer"), 230, current.TimerModifiers, current.TimerHotkey);

            Controls.Add(MakeLabel(Words.T("초기 확대 배율(%)", "Default Zoom(%)"), 24, 280, false));
            var zoomNum = new NumericUpDown { Minimum = 125, Maximum = 600, Increment = 25, Value = (decimal)Math.Max(125, Math.Min(600, Math.Round(current.DefaultZoom * 100))), Bounds = new Rectangle(150, 277, 68, 26) };

            Controls.Add(MakeLabel(Words.T("마우스 원 크기(px)", "Cursor Halo(px)"), 248, 280, false));
            var haloNum = new NumericUpDown { Minimum = 18, Maximum = 75, Value = Math.Max(18, Math.Min(75, current.CursorHaloSize)), Bounds = new Rectangle(374, 277, 60, 26) };

            Controls.Add(MakeLabel(Words.T("타이머(분)", "Timer(m)"), 456, 280, false));
            var timerNum = new NumericUpDown { Minimum = 1, Maximum = 180, Value = Math.Max(1, Math.Min(180, current.TimerMinutes)), Bounds = new Rectangle(528, 277, 60, 26) };

            var trayChk = new CheckBox
            {
                Text = Words.T("창을 닫으면 알림 영역(트레이)에서 계속 실행", "Keep running in system tray when window closes"),
                Checked = current.CloseToTray,
                Bounds = new Rectangle(24, 322, 540, 26)
            };
            var autoSaveChk = new CheckBox
            {
                Text = Words.T("전체화면 필기 중 Ctrl+C 복사 시 보관함에도 자동 저장", "Save to library automatically when copying with Ctrl+C"),
                Checked = current.AutoSaveCaptures,
                Bounds = new Rectangle(24, 354, 540, 26)
            };
            var showHudChk = new CheckBox
            {
                Text = Words.T("확대·필기 시작 시 하단 도구 안내 바 표시 (F1로 숨김 가능)", "Show bottom HUD bar in overlay by default (F1 toggles)"),
                Checked = current.ShowHudByDefault,
                Bounds = new Rectangle(24, 386, 540, 26)
            };
            var autoUpdateChk = new CheckBox
            {
                Text = Words.T("새 버전 자동 확인 (시작 시 · 이후 6시간마다)", "Check for updates automatically"),
                Checked = current.AutoUpdate,
                Bounds = new Rectangle(24, 418, 540, 26)
            };

            Controls.AddRange(new Control[] { zoomNum, haloNum, timerNum, trayChk, autoSaveChk, showHudChk, autoUpdateChk });

            var save = Style.Button(Words.T("저장", "Save"), delegate
            {
                try
                {
                    foreach (var r in new[] { zoomRow, drawRow, haloRow, timerRow })
                        if (r.Modifiers == 0) throw new ArgumentException(Words.T("단축키에 Ctrl, Shift, Alt 중 하나 이상을 포함해 주세요.", "Include Ctrl, Shift, or Alt in every shortcut."));

                    string full = Path.GetFullPath(folder.Text.Trim());
                    Directory.CreateDirectory(full);

                    Result = new Settings
                    {
                        Folder = full,
                        Language = current.Language,
                        ZoomModifiers = zoomRow.Modifiers,
                        ZoomHotkey = zoomRow.KeyValue,
                        DrawModifiers = drawRow.Modifiers,
                        DrawHotkey = drawRow.KeyValue,
                        HaloModifiers = haloRow.Modifiers,
                        HaloHotkey = haloRow.KeyValue,
                        TimerModifiers = timerRow.Modifiers,
                        TimerHotkey = timerRow.KeyValue,
                        DefaultZoom = (float)zoomNum.Value / 100f,
                        PenColorArgb = current.PenColorArgb,
                        PenWidth = current.PenWidth,
                        FontName = current.FontName,
                        FontSize = current.FontSize,
                        CursorHaloSize = (int)haloNum.Value,
                        CursorHaloColorArgb = current.CursorHaloColorArgb,
                        SpotlightRadius = current.SpotlightRadius,
                        TimerMinutes = (int)timerNum.Value,
                        TimerMessage = current.TimerMessage,
                        CloseToTray = trayChk.Checked,
                        AutoSaveCaptures = autoSaveChk.Checked,
                        ShowHudByDefault = showHudChk.Checked,
                        AutoUpdate = autoUpdateChk.Checked
                    };
                    DialogResult = DialogResult.OK;
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, Text); }
            }, true);
            save.Location = new Point(410, 462);

            var cancel = Style.Button(Words.T("취소", "Cancel"), delegate { DialogResult = DialogResult.Cancel; });
            cancel.Location = new Point(506, 462);
            CancelButton = cancel;
            Controls.AddRange(new Control[] { save, cancel });
        }

        private HotkeyRow AddHotkeyRow(string labelText, int y, uint modifiers, int keyVal)
        {
            var lbl = MakeLabel(labelText, 24, y + 4, false);
            var ctrl = new CheckBox { Text = "Ctrl", Checked = (modifiers & 2) != 0, Bounds = new Rectangle(195, y, 60, 28) };
            var shift = new CheckBox { Text = "Shift", Checked = (modifiers & 4) != 0, Bounds = new Rectangle(260, y, 64, 28) };
            var alt = new CheckBox { Text = "Alt", Checked = (modifiers & 1) != 0, Bounds = new Rectangle(328, y, 56, 28) };
            var key = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Bounds = new Rectangle(392, y, 196, 28), DropDownWidth = 220 };
            foreach (var choice in ShortcutKeys.Choices())
            {
                key.Items.Add(choice);
                if ((int)choice.Key == keyVal) key.SelectedItem = choice;
            }
            if (key.SelectedIndex < 0) key.SelectedIndex = 0;
            Controls.AddRange(new Control[] { lbl, ctrl, shift, alt, key });
            return new HotkeyRow { Ctrl = ctrl, Shift = shift, Alt = alt, KeyBox = key };
        }

        private static Label MakeLabel(string text, int x, int y, bool bold)
        {
            return new Label { Text = text, Location = new Point(x, y), AutoSize = true, Font = Style.Font(9.5f, bold ? FontStyle.Bold : FontStyle.Regular), ForeColor = Style.Ink };
        }
    }
}
