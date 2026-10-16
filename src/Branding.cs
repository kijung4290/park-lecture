using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;

namespace ParkLecture
{
    internal static class Branding
    {
        internal const string BlogUrl = "https://blog.naver.com/parkjuim90";
        internal const string Email = "parkjuim90@naver.com";
        internal static readonly Image Logo = LoadLogo();
        internal static readonly Icon AppIcon = LoadIcon();
        private static Icon LoadIcon()
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ParkLecture.App.ico"))
            {
                if (stream == null) return SystemIcons.Application;
                using (var icon = new Icon(stream, 64, 64)) return (Icon)icon.Clone();
            }
        }
        private static Image LoadLogo()
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ParkLecture.Logo.png"))
            {
                if (stream == null) return null;
                using (var image = Image.FromStream(stream)) return new Bitmap(image);
            }
        }
        internal static void Open(string destination)
        {
            try { Process.Start(new ProcessStartInfo(destination) { UseShellExecute = true }); }
            catch (Exception e) { Program.Report(e); }
        }
    }
}
