using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Forms;
using Microsoft.Win32;

namespace AutoCopy.Tests
{
    internal static class InterfaceTests
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string cls, string title);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr handle);
        [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr handle, uint message, IntPtr w, IntPtr l);
        private static int passed, failed;
        private static void Assert(bool result, string message) { if (!result) throw new Exception(message); }
        private static void Test(string label, Action action)
        {
            try { action(); Console.WriteLine("PASS " + label); passed++; }
            catch (Exception error) { Console.WriteLine("FAIL " + label + ": " + error.Message); failed++; }
        }
        private static void Wait(Func<bool> check, string message)
        {
            var timer = Stopwatch.StartNew();
            while (timer.ElapsedMilliseconds < 6000)
            {
                Application.DoEvents();
                // UIA callbacks save asynchronously; an atomic file replacement can briefly hold the file.
                try { if (check()) return; } catch (IOException) { }
                Thread.Sleep(40);
            }
            throw new Exception(message);
        }
        private static AutomationElement Element(IntPtr window, string name)
        {
            return AutomationElement.FromHandle(window).FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, name));
        }
        private static void Invoke(IntPtr window, string name)
        {
            var element = Element(window, name); Assert(element != null, "Missing control: " + name);
            ((InvokePattern)element.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        }
        private static void Toggle(IntPtr window, string name)
        {
            var element = Element(window, name); Assert(element != null, "Missing checkbox: " + name);
            ((TogglePattern)element.GetCurrentPattern(TogglePattern.Pattern)).Toggle();
        }
        private static object StartupValue()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) return key == null ? null : key.GetValue("CtrlCopy");
        }
        [STAThread]
        public static int Main()
        {
            if (Process.GetProcessesByName("CtrlCopy").Length > 0 || Process.GetProcessesByName("AutoCopy").Length > 0) { Console.WriteLine("Close AutoCopy before interface tests."); return 1; }
            string exe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AutoCopy.exe");
            string file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AutoCopy", "settings.json");
            byte[] backup = File.Exists(file) ? File.ReadAllBytes(file) : null;
            object startup = StartupValue();
            Process app = null, fixture = null; IntPtr window = IntPtr.Zero, dialog = IntPtr.Zero;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                var store = new SettingsStore(file); store.Save(new AppSettings());
                app = Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden });
                Thread.Sleep(500);
                Test("Tray startup has no visible settings", () => Assert(FindWindow(null, "CtrlCopy — Настройки") == IntPtr.Zero, "Settings opened at startup"));
                Test("Hidden launch can open visible compact settings", () =>
                {
                    using (var second = Process.Start(exe)) Assert(second.WaitForExit(5000), "Second instance did not exit");
                    Wait(() => (window = FindWindow(null, "CtrlCopy — Настройки")) != IntPtr.Zero && IsWindowVisible(window), "Settings not visible");
                    Wait(() => Element(window, "Автокопирование") != null, "Settings content not ready");
                });
                if (window == IntPtr.Zero) throw new Exception("Cannot continue without settings window");
                Test("Main switch pauses and persists", () => { Toggle(window, "Автокопирование"); Wait(() => !store.Load().Enabled, "Pause not saved"); Assert(Element(window, "Автокопирование на паузе") != null, "Status label did not update"); });
                Test("Main switch resumes and persists", () => { Toggle(window, "Автокопирование"); Wait(() => store.Load().Enabled, "Resume not saved"); });
                Test("Sound checkbox persists", () => { Toggle(window, "Звук при копировании"); Wait(() => store.Load().Sound, "Sound option not saved"); Toggle(window, "Звук при копировании"); });
                Test("Popup checkbox persists", () => { Toggle(window, "Показывать иконку при копировании"); Wait(() => !store.Load().Popup, "Popup option not saved"); Toggle(window, "Показывать иконку при копировании"); });
                Test("Automatic field copying can be enabled independently",()=>{Toggle(window,"Копировать при автовыделении в поле");Wait(()=>store.Load().CopyAutoSelectedFields,"Field option not saved");Toggle(window,"Копировать при автовыделении в поле");});
                Test("Keyboard copying can be disabled independently",()=>{Toggle(window,"Копировать при выделении клавишами");Wait(()=>!store.Load().CopyKeyboardSelections,"Keyboard option not saved");Toggle(window,"Копировать при выделении клавишами");});
                Test("Running application can be excluded from settings",()=>{
                    fixture=Process.Start(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"SelectionFixture.exe"));
                    Wait(()=>FindWindow(null,"AutoCopy Selection Fixture")!=IntPtr.Zero,"Fixture did not open");
                    Invoke(window,"Из запущенных приложений");
                    Wait(()=>Element(window,"Исключить selectionfixture.exe")!=null,"Running app missing from list");
                    Invoke(window,"Исключить selectionfixture.exe");
                    Wait(()=>store.Load().Exclusions.Contains("selectionfixture"),"Running app exclusion not saved");
                    Assert(Element(window,"selectionfixture.exe")!=null,"Exclusion not visible");
                    Invoke(window,"Убрать selectionfixture из исключений");
                    Wait(()=>!store.Load().Exclusions.Contains("selectionfixture"),"Running app removal not saved");
                });
                Test("Add application opens native picker", () => { Invoke(window, "Добавить приложение"); Wait(() => (dialog = FindWindow(null, "Выберите приложение для исключения")) != IntPtr.Zero, "File picker did not open"); });
                Test("Picker adds executable as inline exclusion", () =>
                {
                    Assert(dialog != IntPtr.Zero, "File picker missing");
                    AutomationElement edit = null;
                    Wait(() => (edit = AutomationElement.FromHandle(dialog).FindFirst(TreeScope.Descendants, new AndCondition(new PropertyCondition(AutomationElement.AutomationIdProperty, "1148"), new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit)))) != null, "Filename input missing");
                    ((ValuePattern)edit.GetCurrentPattern(ValuePattern.Pattern)).SetValue(exe);
                    Assert(((ValuePattern)edit.GetCurrentPattern(ValuePattern.Pattern)).Current.Value == exe, "Filename value did not change");
                    edit.SetFocus();
                    var open = AutomationElement.FromHandle(dialog).FindFirst(TreeScope.Descendants, new AndCondition(new PropertyCondition(AutomationElement.AutomationIdProperty, "1"), new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button)));
                    ((InvokePattern)open.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
                    Wait(() => store.Load().Exclusions.Contains("autocopy"), "Exclusion not saved");
                    Assert(Element(window, "autocopy.exe") != null, "Inline exclusion not visible"); dialog = IntPtr.Zero;
                });
                Test("Inline remove button persists deletion", () => { Invoke(window, "Убрать autocopy из исключений"); Wait(() => !store.Load().Exclusions.Contains("autocopy"), "Exclusion not removed"); });
                Test("Close button hides settings and preserves tray instance", () => { Invoke(window,"Закрыть настройки"); Wait(() => !IsWindowVisible(window), "Settings did not hide"); Assert(!app.HasExited, "Closing settings exited utility"); });
                Test("Windows autostart was not changed", () => Assert(object.Equals(startup, StartupValue()), "Autostart registry value changed"));
            }
            catch (Exception error) { failed++; Console.WriteLine("FAIL setup: " + error.Message); }
            finally
            {
                if(fixture!=null && !fixture.HasExited) { fixture.CloseMainWindow();if(!fixture.WaitForExit(1000)){fixture.Kill();fixture.WaitForExit();} }
                if (dialog != IntPtr.Zero) PostMessage(dialog, 0x10, IntPtr.Zero, IntPtr.Zero);
                if (app != null && !app.HasExited) { PostMessage(FindWindow(null, "AutoCopy.MessageWindow"), 0x8002, IntPtr.Zero, IntPtr.Zero); if (!app.WaitForExit(2000)) { app.Kill(); app.WaitForExit(); } }
                if (backup == null) { if (File.Exists(file)) File.Delete(file); } else File.WriteAllBytes(file, backup);
            }
            Console.WriteLine(passed + " passed, " + failed + " failed"); return failed == 0 ? 0 : 1;
        }
    }
}
