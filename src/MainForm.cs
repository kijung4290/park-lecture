using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.VisualBasic.FileIO;

namespace ParkLecture
{
    internal sealed partial class MainForm : Form
    {
        private Settings settings;
        private BoardEditor editor;
        private CaptureList library;
        private ImageList thumbnails;
        private Label status, title, detail, count;
        private ComboBox filter;
        private readonly Dictionary<LectureTool, SoftButton> toolButtons = new Dictionary<LectureTool, SoftButton>();
        private readonly NotifyIcon tray;
        private readonly Timer saveTimer = new Timer { Interval = 900 };
        private readonly ToolTip tips = new ToolTip { AutoPopDelay = 10000 };
        private bool quitting, loading, overlayOpen;
        private CursorHaloOverlay haloOverlay;
        private LectureTimerForm timerForm;
        internal string PreviewPath;

        internal MainForm()
        {
            settings = Settings.Load();
            Words.English = settings.Language == "en";
            if (Program.Demo)
            {
                settings.Folder = Path.Combine(Path.GetTempPath(), "ParkLecture-demo-clean");
                settings.CloseToTray = false;
                Directory.CreateDirectory(settings.Folder);
            }

            Text = Words.T("박주임 강의도구", "Park Lecture") + " · " + Updates.CurrentVersion;
            Font = Style.Font(9);
            BackColor = Style.Paper;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1380, 880);
            MinimumSize = new Size(1080, 720);
            AutoScaleMode = AutoScaleMode.Dpi;
            KeyPreview = true;
            Icon = Branding.AppIcon;

            BuildUI();

            var menu = new ContextMenuStrip();
            menu.Items.Add(Words.T("창 열기", "Open window"), null, delegate { ShowDashboard(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Words.T("화면 확대 시작", "Start screen zoom") + "  (" + ShortcutKeys.Format(settings.ZoomModifiers, settings.ZoomHotkey) + ")", null, async delegate { await LaunchOverlay(true, false, BoardMode.Desktop); });
            menu.Items.Add(Words.T("화면 필기 시작", "Start screen draw") + "  (" + ShortcutKeys.Format(settings.DrawModifiers, settings.DrawHotkey) + ")", null, async delegate { await LaunchOverlay(false, false, BoardMode.Desktop); });
            menu.Items.Add(Words.T("스포트라이트 시작", "Start spotlight"), null, async delegate { await LaunchOverlay(false, true, BoardMode.Desktop); });
            menu.Items.Add(Words.T("화이트보드 판서", "Whiteboard mode"), null, async delegate { await LaunchOverlay(false, false, BoardMode.Whiteboard); });
            menu.Items.Add(Words.T("칠판 판서", "Blackboard mode"), null, async delegate { await LaunchOverlay(false, false, BoardMode.Blackboard); });
            menu.Items.Add(Words.T("마우스 강조 켜기/끄기", "Toggle cursor highlight") + "  (" + ShortcutKeys.Format(settings.HaloModifiers, settings.HaloHotkey) + ")", null, delegate { ToggleCursorHalo(); });
            menu.Items.Add(Words.T("강의 타이머 열기", "Open lecture timer") + "  (" + ShortcutKeys.Format(settings.TimerModifiers, settings.TimerHotkey) + ")", null, delegate { LaunchTimer(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Words.T("도움말 및 단축키…", "Help & shortcuts…"), null, delegate { ShowHelp(); });
            menu.Items.Add(Words.T("설정…", "Settings…"), null, delegate { ShowSettings(); });
            menu.Items.Add(Words.T("업데이트 확인…", "Check for updates…"), null, async delegate { ShowDashboard(); await CheckForUpdates(true); });
            menu.Items.Add(Words.T("종료", "Quit"), null, delegate { Quit(); });

            tray = new NotifyIcon { Icon = Icon, Text = "박주임 강의도구 / Park Lecture", Visible = true, ContextMenuStrip = menu };
            tray.DoubleClick += delegate { ShowDashboard(); };
            saveTimer.Tick += delegate { saveTimer.Stop(); SaveCurrentBoard(); };

            Shown += delegate
            {
                RegisterAllHotkeys();
                InitializeUpdates();
                if (Program.Demo) CreateDemoBoard();
                LoadLibrary(null);
                if (Program.Demo && library.Items.Count > 0)
                {
                    library.Items[0].Selected = true;
                    OpenSavedFile((string)library.Items[0].Tag);
                }
                UpdateStatus();

                if (PreviewPath != null)
                {
                    var previewTimer = new Timer { Interval = 750 };
                    previewTimer.Tick += delegate
                    {
                        previewTimer.Stop();
                        previewTimer.Dispose();
                        try
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(PreviewPath)));
                            SavePreview(PreviewPath);
                            editor.CloseBoard();
                            UpdateStatus();
                            SavePreview(PreviewPath + ".empty.png");
                            using (var helpDlg = new HelpDialog(settings))
                            {
                                helpDlg.Show();
                                helpDlg.Refresh();
                                using (var hbmp = new Bitmap(helpDlg.Width, helpDlg.Height))
                                {
                                    helpDlg.DrawToBitmap(hbmp, new Rectangle(Point.Empty, helpDlg.Size));
                                    Storage.SavePng(hbmp, PreviewPath + ".help.png");
                                }
                                helpDlg.Close();
                            }
                            File.WriteAllText(PreviewPath + ".txt", "PASS UI rendered: " + Width + "x" + Height + "\r\nLibrary items: " + library.Items.Count);
                        }
                        catch (Exception ex) { File.WriteAllText(PreviewPath + ".txt", "FAIL " + ex); }
                        quitting = true;
                        Close();
                    };
                    previewTimer.Start();
                }
            };
            FormClosing += OnClosing;
        }

        internal static string Quote(string value)
        {
            if (value.Contains("\"")) throw new ArgumentException("Invalid path.");
            return "\"" + value + "\"";
        }

        private void RegisterAllHotkeys()
        {
            for (int id = 1; id <= 4; id++) Native.UnregisterHotKey(Handle, id);
            bool ok1 = Native.RegisterHotKey(Handle, 1, settings.ZoomModifiers | 0x4000, (uint)settings.ZoomHotkey);
            bool ok2 = Native.RegisterHotKey(Handle, 2, settings.DrawModifiers | 0x4000, (uint)settings.DrawHotkey);
            bool ok3 = Native.RegisterHotKey(Handle, 3, settings.HaloModifiers | 0x4000, (uint)settings.HaloHotkey);
            bool ok4 = Native.RegisterHotKey(Handle, 4, settings.TimerModifiers | 0x4000, (uint)settings.TimerHotkey);
            if (!ok1 || !ok2 || !ok3 || !ok4)
                SetStatus(Words.T("일부 전역 단축키가 다른 프로그램에서 사용 중입니다. 설정에서 바꿔 주세요.", "Some global shortcuts are in use. Change them in Settings."));
        }

        private void ShowDashboard()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        internal async Task LaunchOverlay(bool startZoom, bool startSpotlight, BoardMode initialBoard)
        {
            if (overlayOpen || updateInstalling) return;
            SaveCurrentBoard();
            overlayOpen = true;
            bool wasVisible = Visible && WindowState != FormWindowState.Minimized;
            try
            {
                if (wasVisible)
                {
                    Hide();
                    await Task.Delay(190);
                }
                Screen activeScreen = Screen.FromPoint(Cursor.Position);
                using (var snap = Native.ScreenBitmap(activeScreen.Bounds))
                using (var overlay = new LectureOverlay(settings, snap, activeScreen.Bounds, startZoom, startSpotlight, initialBoard))
                {
                    overlay.CaptureSaved += delegate(string path)
                    {
                        if (!IsDisposed) BeginInvoke(new Action(delegate { LoadLibrary(path); OpenSavedFile(path); }));
                    };
                    overlay.ShowDialog();
                }
                settings.Save();
            }
            catch (Exception e) { Program.Report(e); }
            finally
            {
                overlayOpen = false;
                if (wasVisible && !quitting)
                {
                    ShowDashboard();
                    UpdateStatus();
                }
            }
        }

        internal void ToggleCursorHalo()
        {
            if (haloOverlay != null && !haloOverlay.IsDisposed)
            {
                haloOverlay.Close();
                haloOverlay.Dispose();
                haloOverlay = null;
                SetStatus(Words.T("마우스 강조가 꺼졌습니다.", "Cursor highlight OFF."));
            }
            else
            {
                haloOverlay = new CursorHaloOverlay(settings);
                haloOverlay.Show();
                SetStatus(Words.T("마우스 강조 켜짐 · 클릭 파동 작동 중 (" + ShortcutKeys.Format(settings.HaloModifiers, settings.HaloHotkey) + "로 끄기)", "Cursor highlight ON"));
            }
            UpdateStatus();
        }

        internal void LaunchTimer()
        {
            if (timerForm != null && !timerForm.IsDisposed)
            {
                timerForm.Activate();
                return;
            }
            timerForm = new LectureTimerForm(settings);
            timerForm.FormClosed += delegate { settings.Save(); timerForm = null; UpdateStatus(); };
            timerForm.Show();
            UpdateStatus();
        }

        private void StartCanvasBoard(BoardMode mode)
        {
            SaveCurrentBoard();
            string path = Storage.NewPath(settings.Folder, ".png");
            editor.CreateBlankBoard(mode, path);
            if (mode == BoardMode.Blackboard && editor.MarkColor.GetBrightness() < 0.25f)
                editor.MarkColor = Style.Yellow;
            else if (mode == BoardMode.Whiteboard && editor.MarkColor.GetBrightness() > 0.9f)
                editor.MarkColor = Style.Ink;
            SaveCurrentBoard();
            LoadLibrary(path);
            title.Text = Path.GetFileName(path);
            if (editor.Tool == LectureTool.Select) ChooseTool(LectureTool.Pen);
            UpdateStatus();
        }

        private bool SaveCurrentBoard()
        {
            saveTimer.Stop();
            if (editor == null || !editor.HasBoard || !editor.Dirty || string.IsNullOrEmpty(editor.PathName)) return true;
            try
            {
                using (var rendered = editor.RenderComposite())
                {
                    Storage.SavePng(rendered, editor.PathName);
                    using (var thumb = MakeThumb(rendered))
                    {
                        foreach (ListViewItem item in library.Items)
                            if ((string)item.Tag == editor.PathName && item.ImageIndex >= 0 && item.ImageIndex < thumbnails.Images.Count)
                                thumbnails.Images[item.ImageIndex] = thumb;
                        library.Invalidate();
                    }
                }
                editor.Dirty = false;
                UpdateStatus();
                return true;
            }
            catch (Exception e) { Program.Report(e); return false; }
        }

        private void OpenSavedFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            if (editor.HasBoard && string.Equals(editor.PathName, path, StringComparison.OrdinalIgnoreCase) && !editor.Dirty) return;
            SaveCurrentBoard();
            try
            {
                using (var img = Image.FromFile(path))
                using (var bmp = new Bitmap(img))
                {
                    editor.LoadImageBoard(bmp, path);
                }
                title.Text = Path.GetFileName(path);
                UpdateStatus();
            }
            catch (Exception e) { Program.Report(e); }
        }

        private void ImportImage()
        {
            using (var dialog = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif", Title = Words.T("이미지 열기", "Open image") })
                if (dialog.ShowDialog(this) == DialogResult.OK) ImportPath(dialog.FileName);
        }

        private void ImportPath(string path)
        {
            if (!File.Exists(path) || !SaveCurrentBoard()) return;
            try
            {
                using (var img = Image.FromFile(path))
                using (var bmp = new Bitmap(img))
                {
                    string target = Storage.NewPath(settings.Folder, ".png");
                    Storage.SavePng(bmp, target);
                    LoadLibrary(target);
                    OpenSavedFile(target);
                }
            }
            catch (Exception e) { Program.Report(e); }
        }

        private void PasteImage()
        {
            try
            {
                if (Clipboard.ContainsImage() && SaveCurrentBoard())
                {
                    using (var image = Clipboard.GetImage())
                    using (var bmp = new Bitmap(image))
                    {
                        string target = Storage.NewPath(settings.Folder, ".png");
                        Storage.SavePng(bmp, target);
                        LoadLibrary(target);
                        OpenSavedFile(target);
                    }
                }
                else SetStatus(Words.T("복사된 이미지가 없습니다. 먼저 이미지를 복사해 주세요.", "No image on the clipboard."));
            }
            catch (Exception e) { Program.Report(e); }
        }

        private void LoadLibrary(string selectPath)
        {
            if (library == null) return;
            try
            {
                Directory.CreateDirectory(settings.Folder);
                loading = true;
                library.BeginUpdate();
                library.Items.Clear();
                library.Groups.Clear();
                thumbnails.Images.Clear();

                string[] groupNames = { Words.T("오늘", "Today"), Words.T("어제", "Yesterday"), Words.T("최근 7일", "Last 7 days"), Words.T("이전", "Earlier") };
                foreach (string g in groupNames) library.Groups.Add(new ListViewGroup(g));

                var files = new DirectoryInfo(settings.Folder).EnumerateFiles()
                    .Where(f => new[] { ".png", ".jpg", ".jpeg", ".bmp" }.Contains(f.Extension.ToLowerInvariant()))
                    .OrderByDescending(f => f.LastWriteTime)
                    .ToList();

                int shown = 0;
                foreach (var f in files)
                {
                    int days = (DateTime.Today - f.LastWriteTime.Date).Days;
                    if (filter != null && filter.SelectedIndex == 1 && days > 0) continue;
                    if (filter != null && filter.SelectedIndex == 2 && days >= 7) continue;

                    string query = search == null ? "" : search.Text.Trim();
                    if (query.Length > 0 && f.Name.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) < 0 && f.LastWriteTime.ToString("yyyy.MM.dd HH:mm:ss").IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    if (shown >= 300) break;
                    using (var thumb = LoadThumb(f.FullName)) thumbnails.Images.Add(thumb);
                    var item = new ListViewItem(f.LastWriteTime.ToString("MM.dd  HH:mm:ss"), shown)
                    {
                        Tag = f.FullName,
                        ToolTipText = f.Name,
                        Group = library.Groups[days <= 0 ? 0 : days == 1 ? 1 : days < 7 ? 2 : 3]
                    };
                    library.Items.Add(item);
                    if (string.Equals(f.FullName, selectPath, StringComparison.OrdinalIgnoreCase))
                        item.Selected = true;
                    shown++;
                }
                count.Text = Words.T("보관함", "Library") + "  " + shown;
                if (library.SelectedItems.Count > 0)
                    library.SelectedItems[0].EnsureVisible();
            }
            catch (Exception e) { Program.Report(e); }
            finally { library.EndUpdate(); loading = false; }
        }

        private static Bitmap LoadThumb(string path)
        {
            try { using (var image = Image.FromFile(path)) return MakeThumb(image); }
            catch
            {
                var b = new Bitmap(206, 114);
                using (var g = Graphics.FromImage(b)) g.Clear(Style.Paper);
                return b;
            }
        }

        internal static Bitmap MakeThumb(Image image)
        {
            var b = new Bitmap(206, 114);
            using (var g = Graphics.FromImage(b))
            {
                g.Clear(Color.FromArgb(238, 241, 247));
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                float scale = Math.Min(198f / image.Width, 106f / image.Height);
                int w = Math.Max(1, (int)(image.Width * scale)), h = Math.Max(1, (int)(image.Height * scale));
                g.DrawImage(image, new Rectangle((206 - w) / 2, (114 - h) / 2, w, h));
            }
            return b;
        }

        private void CopyCurrentImage()
        {
            if (!editor.HasBoard) return;
            try
            {
                SaveCurrentBoard();
                using (var bmp = editor.RenderComposite()) Storage.Copy(bmp);
                SetStatus(Words.T("현재 판서 이미지를 클립보드에 복사했습니다.", "Copied board image to clipboard."));
            }
            catch (Exception e) { Program.Report(e); }
        }

        private void ExportCurrentImage()
        {
            if (!editor.HasBoard) return;
            SaveCurrentBoard();
            using (var dialog = new SaveFileDialog
            {
                Filter = "PNG image|*.png|JPEG image|*.jpg",
                FileName = (editor.PathName != null ? Path.GetFileNameWithoutExtension(editor.PathName) : "강의판서") + "_saved.png",
                AddExtension = true,
                DefaultExt = "png",
                OverwritePrompt = true
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    using (var bmp = editor.RenderComposite()) Storage.Export(bmp, dialog.FileName);
                    SetStatus(Words.T("내보내기 완료: ", "Exported: ") + dialog.FileName);
                }
                catch (Exception e) { Program.Report(e); }
            }
        }

        private void RecycleSelected()
        {
            SaveCurrentBoard();
            var paths = library.SelectedItems.Cast<ListViewItem>().Select(i => (string)i.Tag).ToList();
            if (paths.Count == 0 && editor.PathName != null) paths.Add(editor.PathName);
            if (paths.Count == 0) return;
            if (MessageBox.Show(this, Words.T("선택한 파일을 휴지통으로 옮길까요?", "Move selected file(s) to the Recycle Bin?"), Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            foreach (string p in paths)
            {
                try
                {
                    if (string.Equals(editor.PathName, p, StringComparison.OrdinalIgnoreCase)) editor.CloseBoard();
                    FileSystem.DeleteFile(p, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                }
                catch (Exception e) { Program.Report(e); }
            }
            LoadLibrary(null);
            UpdateStatus();
        }

        private void ShowHelp()
        {
            using (var dlg = new HelpDialog(settings)) dlg.ShowDialog(this);
        }

        private void ShowSettings()
        {
            SaveCurrentBoard();
            using (var dlg = new SettingsDialog(settings))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                Settings next = dlg.Result;
                try
                {
                    if (Program.Demo) next.Save();
                    else StartupRegistration.Save(next);
                }
                catch (Exception e)
                {
                    MessageBox.Show(this, Words.T("설정을 저장하지 못했습니다. 기존 설정을 유지합니다.\n\n", "Could not save settings. Previous settings are kept.\n\n") + e.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                settings = next;
                RegisterAllHotkeys();
                UpdateStatus();
            }
        }

        private void CreateDemoBoard()
        {
            string demoPath = Path.Combine(settings.Folder, "강의 안내 예시.png");
            if (File.Exists(demoPath)) return;
            using (var bmp = new Bitmap(1040, 640))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.White);
                using (var f = new Font("Segoe UI", 11, FontStyle.Bold)) using (var b = new SolidBrush(Style.Blue))
                    g.DrawString("LECTURE SLIDE", f, b, 46, 32);
                using (var f = Style.Font(26, FontStyle.Bold)) using (var b = new SolidBrush(Style.Ink))
                    g.DrawString("한눈에 들어오는 핵심 강의 화면", f, b, 44, 70);
                using (var f = Style.Font(13)) using (var b = new SolidBrush(Style.Muted))
                    g.DrawString("슬라이드를 확대하고 형광펜과 화살표로 중요한 곳을 짚어 주세요.", f, b, 46, 132);
                using (var p = new Pen(Style.Line)) g.DrawLine(p, 46, 190, 994, 190);

                string[] items =
                {
                    "Ctrl+1을 눌러 필요한 화면 영역을 부드럽게 확대하세요",
                    "확대 상태에서 클릭하거나 Ctrl+2를 눌러 바로 필기하세요",
                    "Ctrl+3으로 마우스 포인터 강조와 클릭 파동을 켜세요",
                    "Ctrl+4로 쉬는 시간과 실습 타이머를 띄우세요"
                };
                for (int i = 0; i < 4; i++)
                {
                    using (var b = new SolidBrush(i == 0 ? Style.BlueWash : Style.Sidebar))
                    using (var cardPath = Glyphs.Round(new RectangleF(44, 224 + i * 82, 950, 62), 10))
                        g.FillPath(b, cardPath);
                    using (var f = Style.Font(13)) using (var b = new SolidBrush(Style.Ink))
                        g.DrawString(items[i], f, b, 68, 241 + i * 82);
                }

                var hi = new LectureStroke { Kind = LectureTool.Highlighter, Color = Style.Yellow, Width = 5, Points = new List<PointF> { new PointF(68, 342), new PointF(560, 342) } };
                hi.Draw(g);
                var arr = new LectureStroke { Kind = LectureTool.Arrow, Color = Style.Red, Width = 4, Start = new PointF(820, 195), End = new PointF(910, 248) };
                arr.Draw(g);
                var num = new LectureStroke { Kind = LectureTool.Number, Color = Style.Red, Start = new PointF(942, 255), Number = 1, FontSize = 32 };
                num.Draw(g);

                Storage.SavePng(bmp, demoPath);
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0312)
            {
                int id = m.WParam.ToInt32();
                if (id == 1) BeginInvoke(new Action(async delegate { await LaunchOverlay(true, false, BoardMode.Desktop); }));
                else if (id == 2) BeginInvoke(new Action(async delegate { await LaunchOverlay(false, false, BoardMode.Desktop); }));
                else if (id == 3) BeginInvoke(new Action(delegate { ToggleCursorHalo(); }));
                else if (id == 4) BeginInvoke(new Action(delegate { LaunchTimer(); }));
            }
            base.WndProc(ref m);
        }

        private void OnClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.WindowsShutDown) { SaveCurrentBoard(); settings.Save(); return; }
            if (!SaveCurrentBoard()) { e.Cancel = true; return; }
            settings.Save();
            if (settings.CloseToTray && !quitting)
            {
                e.Cancel = true;
                Hide();
                tray.BalloonTipTitle = Words.T("박주임 강의도구", "Park Lecture");
                tray.BalloonTipText = Words.T("알림 영역에서 실행 중입니다. 단축키로 언제든 확대·필기하세요.", "Running in tray. Use shortcuts anytime.");
                tray.ShowBalloonTip(2000);
            }
        }

        private void Quit()
        {
            quitting = true;
            if (haloOverlay != null && !haloOverlay.IsDisposed) haloOverlay.Close();
            if (timerForm != null && !timerForm.IsDisposed) timerForm.Close();
            Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                for (int id = 1; id <= 4; id++) Native.UnregisterHotKey(Handle, id);
                saveTimer.Dispose();
                updateTimer.Dispose();
                if (haloOverlay != null) haloOverlay.Dispose();
                if (timerForm != null) timerForm.Dispose();
                tips.Dispose();
                tray.Visible = false;
                tray.Dispose();
                if (thumbnails != null) thumbnails.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
