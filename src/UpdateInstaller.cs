using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace ParkLecture.UpdateSupport
{
    internal static class UpdateInstaller
    {
        internal const string InstalledAppMutex = @"Local\ParkLectureInstalled";
        internal static bool IsInstalled(string directory)
        {
            string installed = Registry.GetValue(@"HKEY_CURRENT_USER\Software\ParkLecture\Install", "Path", null) as string;
            return !string.IsNullOrEmpty(installed) && string.Equals(new DirectoryInfo(installed).FullName.TrimEnd(Path.DirectorySeparatorChar),
                new DirectoryInfo(directory).FullName.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
        }

        internal static void RunSetup(string stage, string target, UpdateAsset package)
        {
            string setup = Path.Combine(stage, package.asset);
            string args = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /RESTARTEXITCODE=9 /NOCANCEL /DIR=\"" + target + "\" /LOG=\"" + Path.Combine(stage, "setup.log") + "\"";
            using (var stream = File.OpenRead(setup))
            {
                UpdatePackage.Verify(stream, package);
                using (var process = Process.Start(new ProcessStartInfo(setup, args) { UseShellExecute = false, WorkingDirectory = stage }))
                {
                    process.WaitForExit();
                    if (process.ExitCode != 0) throw new IOException("Windows installer exit code: " + process.ExitCode);
                }
            }
        }

        internal static void CheckWritable(string directory)
        {
            string probe = Path.Combine(directory, ".parklecture-write-" + Guid.NewGuid().ToString("N"));
            try { using (File.Open(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { } }
            finally { if (File.Exists(probe)) File.Delete(probe); }
        }
        internal static void Install(string source, string target, string backup)
        {
            CheckWritable(target); Directory.CreateDirectory(backup);
            var installed = new List<string>(); var existed = new HashSet<string>();
            foreach (string file in UpdatePackage.Files)
            {
                if (File.Exists(Path.Combine(target, file))) { File.Copy(Path.Combine(target, file), Path.Combine(backup, file)); existed.Add(file); }
            }
            try
            {
                foreach (string file in UpdatePackage.Files)
                {
                    Replace(Path.Combine(source, file), Path.Combine(target, file)); installed.Add(file);
                }
            }
            catch (Exception error)
            {
                var failures = new List<Exception>(); failures.Add(error);
                for (int i = installed.Count - 1; i >= 0; i--)
                {
                    string file = installed[i];
                    try { if (existed.Contains(file)) Replace(Path.Combine(backup, file), Path.Combine(target, file)); else File.Delete(Path.Combine(target, file)); }
                    catch (Exception rollbackError) { failures.Add(rollbackError); }
                }
                if (failures.Count > 1) throw new AggregateException("Update and rollback failed. Backups: " + backup, failures);
                throw;
            }
        }
        private static void Replace(string source, string target)
        {
            string temporary = Path.Combine(Path.GetDirectoryName(target), ".parklecture-update-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.Copy(source, temporary);
                if (File.Exists(target)) File.Replace(temporary, target, null); else File.Move(temporary, target);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
