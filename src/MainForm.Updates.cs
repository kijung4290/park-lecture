using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ParkLecture
{
    internal sealed partial class MainForm
    {
        private readonly System.Windows.Forms.Timer updateTimer = new System.Windows.Forms.Timer { Interval = 3 * 60 * 1000 };
        private bool checkingUpdate, updateInstalling, updateBalloon;
        private AvailableUpdate pendingUpdate;
        private string notifiedVersion;
        private bool UpdateBlocked { get { return overlayOpen || timerForm != null || !Enabled || quitting; } }

        private void InitializeUpdates()
        {
            if (Program.Demo) return;
            updateTimer.Tick += async delegate { if (settings.AutoUpdate) await CheckForUpdates(false); };
            updateTimer.Start();
            tray.BalloonTipClicked += async delegate
            {
                if (!updateBalloon || pendingUpdate == null || UpdateBlocked) return;
                ShowDashboard();
                await CheckForUpdates(true);
            };
            BeginInvoke(new Action(async delegate { await Task.Delay(3000); if (!IsDisposed && settings.AutoUpdate) await CheckForUpdates(false); }));
        }

        private void NotifyPendingUpdate()
        {
            if (IsDisposed || !settings.AutoUpdate || pendingUpdate == null || UpdateBlocked || updateInstalling) return;
            string version = pendingUpdate.Manifest.version;
            if (version == notifiedVersion) return;
            notifiedVersion = version;
            updateBalloon = true;
            tray.BalloonTipTitle = Words.T("박주임 강의도구 새 버전", "Park Lecture update available") + " v" + version;
            tray.BalloonTipText = Words.T("알림을 누르거나 트레이 메뉴에서 업데이트를 확인하세요.", "Click this notification or check for updates in the tray menu.");
            tray.ShowBalloonTip(5000);
        }

        private async Task CheckForUpdates(bool manual)
        {
            if (checkingUpdate || updateInstalling) return;
            if (manual && UpdateBlocked)
            {
                MessageBox.Show(this, Words.T("화면 확대, 판서 또는 타이머를 마친 뒤 업데이트를 확인해 주세요.", "Finish zooming, drawing, or using the timer before checking for updates."), Text);
                return;
            }
            checkingUpdate = true;
            string stage = null;
            try
            {
                AvailableUpdate update;
                using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(35))) update = await Updates.Check(timeout.Token);
                if (IsDisposed) return;
                pendingUpdate = update;
                if (!manual)
                {
                    NotifyPendingUpdate();
                    return;
                }
                if (update == null)
                {
                    MessageBox.Show(this, Words.T("최신 버전을 사용하고 있습니다. v", "You are up to date. v") + Updates.CurrentVersion, Words.T("업데이트 확인", "Check for updates"));
                    return;
                }
                if (UpdateBlocked) return;
                notifiedVersion = update.Manifest.version;
                updateInstalling = true;
                using (var dialog = new UpdateDialog(update))
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    stage = dialog.PreparedStage;
                }
                if (!SaveCurrentBoard()) return;
                Updates.Apply(stage);
                stage = null;
                quitting = true;
                Close();
                if (!IsDisposed) quitting = false;
            }
            catch (OperationCanceledException) { if (manual && !IsDisposed) MessageBox.Show(this, Words.T("업데이트 확인 시간이 초과되었습니다. 잠시 후 다시 시도해 주세요.", "The update check timed out. Try again later."), Text); }
            catch (Exception error)
            {
                if (manual && !IsDisposed) MessageBox.Show(this, Words.T("업데이트를 확인하지 못했습니다. 인터넷 연결을 확인해 주세요.\n", "Could not check for updates. Check your connection.\n") + error.Message, Text);
                try { File.AppendAllText(Path.Combine(Program.DataDir, "update.log"), DateTime.UtcNow.ToString("s") + " " + error.Message + Environment.NewLine); } catch { }
            }
            finally
            {
                if (stage != null) Updates.Discard(stage);
                checkingUpdate = false;
                updateInstalling = false;
            }
        }
    }

    internal sealed class UpdateDialog : Form
    {
        internal string PreparedStage;
        private CancellationTokenSource download;
        internal UpdateDialog(AvailableUpdate update)
        {
            Text = Words.T("새 버전이 준비되었습니다", "An update is ready"); Font = Style.Font(10); BackColor = Style.Sidebar;
            ClientSize = new Size(490, 240); StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; Icon = Branding.AppIcon;
            var heading = new Label { Text = Words.T("박주임 강의도구 v", "Park Lecture v") + update.Manifest.version, Font = Style.Font(17, FontStyle.Bold), Bounds = new Rectangle(24, 23, 442, 35) };
            var note = new Label { Text = Words.T("현재 v", "Current v") + Updates.CurrentVersion + Words.T("\n업데이트 후 앱이 다시 열립니다. 판서 보관함과 설정은 유지됩니다.", "\nThe app will restart. Your saved notes and settings are kept."), ForeColor = Style.Muted, Bounds = new Rectangle(24, 70, 442, 52) };
            var progress = new ProgressBar { Bounds = new Rectangle(24, 132, 442, 12), Visible = false };
            var status = new Label { Text = Words.T("서명과 파일 무결성을 확인한 뒤 적용합니다.", "Downloads are verified before installation."), Font = Style.Font(9), ForeColor = Style.Muted, Bounds = new Rectangle(24, 151, 442, 23) };
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 58, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(18, 3, 18, 8), WrapContents = false };
            var later = new SoftButton { Text = Words.T("나중에", "Later") };
            var install = new SoftButton { Text = Words.T("업데이트 후 재시작", "Update and restart"), Glyph = "save", Tone = ButtonTone.Primary };
            later.Click += delegate { DialogResult = DialogResult.Cancel; };
            install.Click += async delegate
            {
                install.Enabled = false; progress.Visible = true; later.Text = Words.T("취소", "Cancel"); status.Text = Words.T("업데이트를 내려받는 중…", "Downloading update…");
                using (var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(5)))
                {
                    download = cancellation;
                    try
                    {
                        string stage = await Updates.Prepare(update, cancellation.Token, new Progress<int>(delegate(int value) { if (!IsDisposed) progress.Value = Math.Max(0, Math.Min(100, value)); }));
                        if (IsDisposed || cancellation.IsCancellationRequested) Updates.Discard(stage);
                        else { PreparedStage = stage; DialogResult = DialogResult.OK; }
                    }
                    catch (OperationCanceledException) { if (!IsDisposed) DialogResult = DialogResult.Cancel; }
                    catch (Exception error) { if (!IsDisposed) { status.Text = Words.T("업데이트 실패: ", "Update failed: ") + error.Message; install.Enabled = true; } }
                    finally { download = null; }
                }
            };
            FormClosing += delegate { if (download != null) download.Cancel(); };
            buttons.Controls.AddRange(new Control[] { later, install });
            Controls.AddRange(new Control[] { heading, note, progress, status, buttons }); CancelButton = later;
        }
    }
}
