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
        private readonly System.Windows.Forms.Timer updateTimer = new System.Windows.Forms.Timer { Interval = 6 * 60 * 60 * 1000 };
        private bool checkingUpdate, updateInstalling;
        private AvailableUpdate pendingUpdate;

        private void InitializeUpdates()
        {
            if (Program.Demo) return;
            updateTimer.Tick += async delegate { if (settings.AutoUpdate) await CheckForUpdates(false); };
            updateTimer.Start();
            Activated += async delegate { if (pendingUpdate != null && !checkingUpdate && !updateInstalling) await CheckForUpdates(false); };
            BeginInvoke(new Action(async delegate { await Task.Delay(3000); if (!IsDisposed && settings.AutoUpdate) await CheckForUpdates(false); }));
        }

        private async Task CheckForUpdates(bool manual)
        {
            if (checkingUpdate || updateInstalling) return;
            if (overlayOpen)
            {
                if (manual) MessageBox.Show(this, Words.T("화면 확대나 필기를 마친 뒤 업데이트를 확인해 주세요.", "Finish zooming or drawing before checking for updates."), Text);
                return;
            }
            checkingUpdate = true;
            try
            {
                AvailableUpdate update = pendingUpdate;
                using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(35))) if (update == null) update = await Updates.Check(timeout.Token);
                if (IsDisposed) return;
                if (update == null)
                {
                    if (manual) MessageBox.Show(this, Words.T("최신 버전을 사용하고 있습니다. v", "You are up to date. v") + Updates.CurrentVersion, Words.T("업데이트 확인", "Check for updates"));
                    return;
                }
                pendingUpdate = update;
                if (overlayOpen || (!Visible && !manual)) return;
                updateInstalling = true;
                using (var dialog = new UpdateDialog(update))
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) { pendingUpdate = null; return; }
                    SaveCurrentBoard();
                    Updates.Apply(dialog.PreparedStage);
                    quitting = true; Close();
                    if (!IsDisposed) quitting = false;
                }
            }
            catch (OperationCanceledException) { if (manual && !IsDisposed) MessageBox.Show(this, Words.T("업데이트 확인 시간이 초과되었습니다. 잠시 후 다시 시도해 주세요.", "The update check timed out. Try again later."), Text); }
            catch (Exception error)
            {
                if (manual && !IsDisposed) MessageBox.Show(this, Words.T("업데이트를 확인하지 못했습니다. 인터넷 연결을 확인해 주세요.\n", "Could not check for updates. Check your connection.\n") + error.Message, Text);
                try { File.AppendAllText(Path.Combine(Program.DataDir, "update.log"), DateTime.UtcNow.ToString("s") + " " + error.Message + Environment.NewLine); } catch { }
            }
            finally { checkingUpdate = false; updateInstalling = false; }
        }
    }

    internal sealed class UpdateDialog : Form
    {
        internal string PreparedStage;
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(5));
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
            later.Click += delegate { cancellation.Cancel(); DialogResult = DialogResult.Cancel; };
            install.Click += async delegate
            {
                install.Enabled = false; progress.Visible = true; later.Text = Words.T("취소", "Cancel"); status.Text = Words.T("업데이트를 내려받는 중…", "Downloading update…");
                try
                {
                    PreparedStage = await Updates.Prepare(update, cancellation.Token, new Progress<int>(delegate(int value) { if (!IsDisposed) progress.Value = Math.Max(0, Math.Min(100, value)); }));
                    if (!IsDisposed && !cancellation.IsCancellationRequested) DialogResult = DialogResult.OK;
                }
                catch (OperationCanceledException) { if (!IsDisposed) DialogResult = DialogResult.Cancel; }
                catch (Exception error) { if (!IsDisposed) { status.Text = Words.T("업데이트 실패: ", "Update failed: ") + error.Message; install.Enabled = true; } }
            };
            FormClosing += delegate { cancellation.Cancel(); };
            buttons.Controls.AddRange(new Control[] { later, install });
            Controls.AddRange(new Control[] { heading, note, progress, status, buttons }); CancelButton = later;
        }
    }
}
