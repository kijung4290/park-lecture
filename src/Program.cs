using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace ParkLecture
{
    internal static class Program
    {
        internal const string CurrentVersion = "1.0.1";
        internal static readonly string DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ParkLecture");
        internal static bool Demo;

        [STAThread]
        private static int Main(string[] args)
        {
            try { Native.SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { try { Native.SetProcessDPIAware(); } catch { } }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args.Length > 0 && args[0] == "--self-test")
                return SelfTests.Run(args.Length > 1 ? args[1] : "self-test.txt");

            bool preview = args.Length > 1 && args[0] == "--preview";
            Demo = preview || (args.Length > 0 && args[0] == "--demo");
            bool first;
            using (var mutex = new Mutex(true, Demo ? "Local\\ParkLectureDemo" : "Local\\ParkLecture", out first))
            {
                if (!first)
                {
                    MessageBox.Show("박주임 강의도구가 이미 실행 중입니다. 알림 영역의 아이콘을 눌러 주세요.", "박주임 강의도구");
                    return 0;
                }
                Directory.CreateDirectory(DataDir);
                Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e) { Report(e.Exception); };
                try
                {
                    var main = new MainForm();
                    if (preview) main.PreviewPath = args[1];
                    Application.Run(main);
                    return 0;
                }
                catch (Exception e) { Report(e); return 1; }
            }
        }

        internal static void Report(Exception e)
        {
            try { Directory.CreateDirectory(DataDir); File.AppendAllText(Path.Combine(DataDir, "error.log"), DateTime.Now + " " + e + Environment.NewLine); } catch { }
            MessageBox.Show(e.Message, "박주임 강의도구", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    public class Settings
    {
        public string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "박주임 강의도구");
        public string Language = "ko";

        // Global Hotkeys (Modifiers: 2 = Ctrl, 4 = Shift, 1 = Alt)
        public uint ZoomModifiers = 2;
        public int ZoomHotkey = (int)Keys.D1;

        public uint DrawModifiers = 2;
        public int DrawHotkey = (int)Keys.D2;

        public uint HaloModifiers = 2;
        public int HaloHotkey = (int)Keys.D3;

        public uint TimerModifiers = 2;
        public int TimerHotkey = (int)Keys.D4;

        // Default Lecture Parameters
        public float DefaultZoom = 2.0f;
        public int PenColorArgb = Color.FromArgb(255, 69, 58).ToArgb();
        public float PenWidth = 4f;
        public string FontName = "Pretendard";
        public float FontSize = 32f;
        public int CursorHaloSize = 38;
        public int CursorHaloColorArgb = Color.FromArgb(255, 214, 10).ToArgb();
        public int SpotlightRadius = 150;
        public int TimerMinutes = 10;
        public string TimerMessage = "쉬는 시간입니다 · 잠시 후 강의가 이어집니다";
        public bool CloseToTray = true;
        public bool AutoSaveCaptures = true;
        public bool ShowHudByDefault = true;
        public bool AutoUpdate = true;
        public bool StartWithWindows = false;

        public static Settings Load()
        {
            try { using (var f = File.OpenRead(Path.Combine(Program.DataDir, "settings.xml"))) return (Settings)new XmlSerializer(typeof(Settings)).Deserialize(f); }
            catch { return new Settings(); }
        }
        public void Save() { Storage.WriteXml(Path.Combine(Program.DataDir, "settings.xml"), this); }
    }

    internal static class Words
    {
        internal static bool English;
        internal static string T(string ko, string en) { return English ? en : ko; }
    }

    internal static class Style
    {
        internal static readonly Color Ink = Color.FromArgb(29, 29, 31), Blue = Color.FromArgb(0, 122, 255), Paper = Color.FromArgb(242, 242, 245), Line = Color.FromArgb(229, 229, 234), Red = Color.FromArgb(255, 69, 58);
        internal static readonly Color Sidebar = Color.FromArgb(248, 248, 250), Muted = Color.FromArgb(110, 110, 115), Disabled = Color.FromArgb(174, 174, 181), Hover = Color.FromArgb(236, 236, 240), BlueWash = Color.FromArgb(231, 241, 255);
        internal static readonly Color Green = Color.FromArgb(48, 179, 94), Orange = Color.FromArgb(255, 159, 10), Yellow = Color.FromArgb(255, 214, 10), Purple = Color.FromArgb(175, 82, 222);

        internal static readonly Color[] Palette = new[]
        {
            Red,
            Blue,
            Green,
            Yellow,
            Orange,
            Purple,
            Ink,
            Color.White
        };

        internal static Font Font(float size, FontStyle style = FontStyle.Regular) { return new Font("Malgun Gothic", size, style); }
        internal static Button Button(string text, EventHandler click, bool primary = false)
        {
            var b = new SoftButton { Text = text, Tone = primary ? ButtonTone.Primary : ButtonTone.Normal, AccessibleName = text };
            b.Click += click; return b;
        }
    }

    internal static class Storage
    {
        internal static void WriteXml<T>(string path, T value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + ".tmp";
            using (var f = File.Create(temp)) new XmlSerializer(typeof(T)).Serialize(f, value);
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
        }
        internal static string NewPath(string folder, string ext)
        {
            Directory.CreateDirectory(folder);
            string stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
            string path = Path.Combine(folder, "강의판서_" + stamp + ext);
            int seq = 2;
            while (File.Exists(path)) { path = Path.Combine(folder, "강의판서_" + stamp + "_" + seq + ext); seq++; }
            return path;
        }
        internal static void SavePng(Image image, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string temp = path + ".tmp";
            image.Save(temp, ImageFormat.Png);
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
        }
        internal static void Export(Image image, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            if (path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
            {
                using (var flat = new Bitmap(image.Width, image.Height, PixelFormat.Format24bppRgb))
                using (var g = Graphics.FromImage(flat))
                {
                    g.Clear(Color.White); g.DrawImage(image, 0, 0, image.Width, image.Height);
                    flat.Save(path, ImageFormat.Jpeg);
                }
            }
            else SavePng(image, path);
        }
        internal static void Copy(Bitmap image)
        {
            Clipboard.SetImage(image);
        }
    }

    internal static class Native
    {
        [DllImport("user32.dll")] internal static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] internal static extern bool SetProcessDpiAwarenessContext(IntPtr value);
        [DllImport("user32.dll", SetLastError = true)] internal static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr window, int id);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, string lParam);
        [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int vKey);
        [DllImport("user32.dll")] internal static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] internal static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        internal static Bitmap ScreenBitmap(Rectangle bounds)
        {
            var b = new Bitmap(Math.Max(1, bounds.Width), Math.Max(1, bounds.Height), PixelFormat.Format32bppArgb);
            b.SetResolution(96, 96);
            try
            {
                using (var g = Graphics.FromImage(b))
                    g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);
                return b;
            }
            catch
            {
                // Fallback synthetic canvas in headless/session-0 environments
                using (var g = Graphics.FromImage(b)) g.Clear(Color.White);
                return b;
            }
        }
    }
}
