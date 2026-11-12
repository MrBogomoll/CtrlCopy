using System;
using Microsoft.Win32;

namespace AutoCopy
{
    internal static class StartupRegistration
    {
        private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
        internal static bool IsEnabled(string executable)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(Key))
                return key != null && (string.Equals(key.GetValue("CtrlCopy") as string, "\"" + executable + "\"", StringComparison.OrdinalIgnoreCase) || string.Equals(key.GetValue("AutoCopy") as string, "\"" + executable + "\"", StringComparison.OrdinalIgnoreCase));
        }
        internal static void SetEnabled(bool enabled, string executable)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(Key))
            {
                if (enabled) key.SetValue("CtrlCopy", "\"" + executable + "\"");
                else key.DeleteValue("CtrlCopy", false);
                string old = key.GetValue("AutoCopy") as string;
                if (string.Equals(old,"\""+executable+"\"",StringComparison.OrdinalIgnoreCase) || string.Equals(old,LegacyCommand(executable),StringComparison.OrdinalIgnoreCase)) key.DeleteValue("AutoCopy",false);
            }
        }
        private static string LegacyCommand(string executable) { return "\""+System.IO.Path.Combine(System.IO.Path.GetDirectoryName(executable),"AutoCopy.exe")+"\""; }
        internal static void MigrateLegacy(string executable)
        {
            using(var key=Registry.CurrentUser.OpenSubKey(Key,true))
            {
                if(key==null || key.GetValue("CtrlCopy")!=null || !string.Equals(key.GetValue("AutoCopy") as string,LegacyCommand(executable),StringComparison.OrdinalIgnoreCase)) return;
                key.SetValue("CtrlCopy","\""+executable+"\"");key.DeleteValue("AutoCopy",false);
            }
        }
    }
}
