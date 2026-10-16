using System;
using System.Collections.Generic;
using System.IO;

namespace ParkLecture.UpdateSupport
{
    internal static class UpdateInstaller
    {
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
