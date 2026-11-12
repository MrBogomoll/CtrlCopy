using System;
using System.IO;
using System.Reflection;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("CtrlCopy")]
[assembly: AssemblyDescription("Автоматическое копирование выделенного текста")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace AutoCopy
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += (sender, error) => { Diagnostics.Write("UI exception " + error.Exception); MessageBox.Show("Ошибка CtrlCopy:\n" + error.Exception.Message, "CtrlCopy", MessageBoxButtons.OK, MessageBoxIcon.Error); };
            bool created;
            string user = WindowsIdentity.GetCurrent().User.Value;
            using (var mutex = new Mutex(true, "Local\\AutoCopy." + user, out created))
            {
                if (!created)
                {
                    for (int i = 0; i < 40; i++)
                    {
                        IntPtr window = Native.FindWindow(null, "AutoCopy.MessageWindow");
                        if (window != IntPtr.Zero)
                        {
                            uint process; Native.GetWindowThreadProcessId(window, out process);
                            Native.AllowSetForegroundWindow(process);
                            Native.PostMessage(window, Native.OpenSettingsMessage, IntPtr.Zero, IntPtr.Zero); return;
                        }
                        Thread.Sleep(50);
                    }
                    return;
                }
                try
                {
                    try { StartupRegistration.MigrateLegacy(Application.ExecutablePath); }
                    catch(Exception error) when(error is UnauthorizedAccessException || error is System.Security.SecurityException || error is IOException) { Diagnostics.Write("startup migration: "+error.GetType().Name); }
                    string file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AutoCopy", "settings.json");
                    using (var context = new TrayContext(new SettingsStore(file))) Application.Run(context);
                }
                catch (Exception ex) { MessageBox.Show("Не удалось запустить CtrlCopy:\n" + ex.Message, "CtrlCopy", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                finally { mutex.ReleaseMutex(); }
            }
        }
    }
}
