using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using ParkLecture.UpdateSupport;

namespace ParkLecture.Updater
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            string target = null, stage = null; bool parentExited = false;
            try
            {
                if (args.Length != 4) throw new ArgumentException("This helper is started by Park Lecture.");
                stage = Path.GetFullPath(args[0]); target = Path.GetFullPath(args[1]);
                int parentId = int.Parse(args[2]); long parentStarted = long.Parse(args[3]);
                var manifest = UpdatePackage.Read(File.ReadAllBytes(Path.Combine(stage, "update.json")), Convert.FromBase64String(File.ReadAllText(Path.Combine(stage, "update.json.sig"))), UpdatePackage.PublicKey);
                string extracted = Path.Combine(stage, "verified");
                UpdatePackage.Extract(Path.Combine(stage, manifest.asset), extracted, manifest);
                try
                {
                    using (var parent = Process.GetProcessById(parentId))
                    {
                        if (parent.StartTime.ToUniversalTime().Ticks == parentStarted && !parent.WaitForExit(90000)) throw new IOException("앱이 종료되지 않아 업데이트를 적용하지 않았습니다.");
                    }
                }
                catch (ArgumentException) { }
                parentExited = true;
                string app = Path.Combine(target, "ParkLecture.exe");
                if (File.Exists(app) && UpdatePackage.ParseVersion(manifest.version) <= new Version(System.Reflection.AssemblyName.GetAssemblyName(app).Version.ToString(3))) throw new InvalidDataException("이미 설치된 버전보다 오래된 업데이트입니다.");
                UpdateInstaller.Install(extracted, target, Path.Combine(stage, "backup"));
                Start(target); return 0;
            }
            catch (Exception error)
            {
                try { if (stage != null) File.WriteAllText(Path.Combine(stage, "error.txt"), error.ToString()); } catch { }
                MessageBox.Show("업데이트를 완료하지 못했습니다.\n" + error.Message + "\n\n기존 파일 백업과 오류 기록: " + stage, "박주임 강의도구 업데이트", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                if (parentExited && target != null) try { Start(target); } catch { }
                return 1;
            }
        }
        private static void Start(string target)
        { Process.Start(new ProcessStartInfo(Path.Combine(target, "ParkLecture.exe")) { UseShellExecute = false, WorkingDirectory = target }); }
    }
}
