using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ParkLecture
{
    internal static class StartupRegistration
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        internal const string ValueName = "ParkLecture";

        internal static string Command(string executablePath)
        {
            if (string.IsNullOrWhiteSpace(executablePath) || !Path.IsPathRooted(executablePath) || executablePath.IndexOf('"') >= 0)
                throw new ArgumentException(Words.T("자동 실행할 프로그램 경로를 확인해 주세요.", "Check the startup executable path."));
            string command = "\"" + Path.GetFullPath(executablePath) + "\"";
            if (command.Length > 260)
                throw new ArgumentException(Words.T("자동 실행을 등록하려면 프로그램을 더 짧은 경로의 폴더로 옮겨 주세요.", "Move the app to a shorter path before enabling startup."));
            return command;
        }

        internal static void Save(Settings settings)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
                Apply(key, settings.StartWithWindows, Application.ExecutablePath, settings.Save);
        }

        // Tests use a separate, non-startup registry key. Never run a test against Run.
        internal static void Apply(RegistryKey key, bool enabled, string executablePath, Action saveSettings)
        {
            object previous = key.GetValue(ValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            RegistryValueKind previousKind = previous == null ? RegistryValueKind.String : key.GetValueKind(ValueName);
            if (enabled) key.SetValue(ValueName, Command(executablePath), RegistryValueKind.String);
            else key.DeleteValue(ValueName, false);
            try { saveSettings(); }
            catch (Exception saveError)
            {
                try
                {
                    if (previous == null) key.DeleteValue(ValueName, false);
                    else key.SetValue(ValueName, previous, previousKind);
                }
                catch (Exception restoreError) { throw new AggregateException(saveError, restoreError); }
                throw;
            }
        }
    }
}
