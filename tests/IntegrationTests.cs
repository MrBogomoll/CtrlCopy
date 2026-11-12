using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Forms;

namespace AutoCopy.Tests
{
    internal static class IntegrationTests
    {
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
        [DllImport("user32.dll")] private static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string cls, string title);
        [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
        [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(Point point);
        [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr window, uint kind);
        private static int failed, passed;
        private static IntPtr inputTarget;
        private static readonly string Text = "AutoCopy проверка выделения.\r\nВторая строка: 12345.";
        private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        private static void Test(string name, Action action)
        {
            try { action(); passed++; Console.WriteLine("PASS " + name); }
            catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + ex.Message); }
        }
        private static void Wait(Func<bool> predicate, string message, int milliseconds = 4000)
        {
            var watch = Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < milliseconds) { Application.DoEvents(); if (predicate()) return; Thread.Sleep(40); }
            string actual = ClipboardText();
            throw new Exception(message + " (clipboardLength=" + actual.Length + ", expectedLength=" + Text.Length + ", CR=" + (actual.Length-actual.Replace("\r", "").Length) + ", LF=" + (actual.Length-actual.Replace("\n", "").Length) + ")");
        }
        private static string ClipboardText() { try { return Clipboard.ContainsText() ? Clipboard.GetText() : ""; } catch (ExternalException) { return ""; } }
        private static void Click(int x, int y)
        {
            SetForegroundWindow(inputTarget);
            Application.DoEvents(); Thread.Sleep(100);
            Check(GetAncestor(WindowFromPoint(new Point { X = x, Y = y }), 2) == inputTarget, "Target point is covered by another window; no input sent");
            SetCursorPos(x, y); Thread.Sleep(50); mouse_event(2, 0, 0, 0, UIntPtr.Zero); Thread.Sleep(40); mouse_event(4, 0, 0, 0, UIntPtr.Zero); Thread.Sleep(180);
            Check(GetForegroundWindow() == inputTarget, "Test click did not activate fixture; keyboard input will not be sent");
        }
        private static void CtrlA()
        {
            Check(GetForegroundWindow() == inputTarget, "Test window is not foreground; no input sent");
            keybd_event(0x11, 0, 0, UIntPtr.Zero); keybd_event(0x41, 0, 0, UIntPtr.Zero); Thread.Sleep(35); keybd_event(0x41, 0, 2, UIntPtr.Zero); keybd_event(0x11, 0, 2, UIntPtr.Zero);
        }
        private static AutomationElement Element(IntPtr hwnd, string name)
        {
            return AutomationElement.FromHandle(hwnd).FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, name));
        }
        private static void ToggleEnabled(IntPtr hwnd)
        {
            var toggle = Element(hwnd, "Автокопирование");
            Check(toggle != null, "Enabled checkbox not accessible");
            ((TogglePattern)toggle.GetCurrentPattern(TogglePattern.Pattern)).Toggle();
            Thread.Sleep(200);
        }
        [STAThread]
        public static int Main()
        {
            string bin = AppDomain.CurrentDomain.BaseDirectory;
            string exe = Path.Combine(bin, "AutoCopy.exe");
            Test("Portable executable exists", () => Check(File.Exists(exe), "AutoCopy.exe has not been implemented"));
            if (!File.Exists(exe)) return 1;
            if (Process.GetProcessesByName("AutoCopy").Length > 0 || Process.GetProcessesByName("CtrlCopy").Length > 0) { Console.WriteLine("FAIL Close an existing CtrlCopy instance before integration testing."); return 1; }
            string config = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AutoCopy", "settings.json");
            byte[] original = File.Exists(config) ? File.ReadAllBytes(config) : null;
            var backup = new DataObject();
            try { var data = Clipboard.GetDataObject(); if (data != null) foreach (string format in data.GetFormats(false)) { try { backup.SetData(format, false, data.GetData(format, false)); } catch { } } } catch { }
            Process app = null, fixture = null;
            string diagnostic = Path.Combine(bin, "integration-diagnostics.log");
            if (File.Exists(diagnostic)) File.Delete(diagnostic);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(config));
                File.WriteAllText(config, "{\"Enabled\":true,\"Popup\":false,\"Sound\":false,\"Exclusions\":[]}");
                var start = new ProcessStartInfo(exe) { UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden };
                start.EnvironmentVariables["AUTOCOPY_DIAGNOSTICS"] = diagnostic;
                app = Process.Start(start);
                fixture = Process.Start(Path.Combine(bin, "SelectionFixture.exe"));
                Wait(() => { fixture.Refresh(); return fixture.MainWindowHandle != IntPtr.Zero; }, "Fixture did not start");
                Thread.Sleep(700);
                IntPtr fixtureWindow = fixture.MainWindowHandle;
                inputTarget = fixtureWindow;
                Test("Startup remains in tray", () => Check(FindWindow(null, "CtrlCopy — Настройки") == IntPtr.Zero && !app.HasExited, "Settings unexpectedly opened on startup"));
                SetForegroundWindow(fixtureWindow); Thread.Sleep(250);
                var textElement = Element(fixtureWindow, "Selection text");
                var rectangle = textElement.Current.BoundingRectangle;
                int x = (int)rectangle.X + 12, y = (int)rectangle.Y + 16;
                Click(x, y);
                Test("Keyboard selection copies exact Unicode multiline text", () => {
                    Click(x, y); Clipboard.SetText("sentinel-keyboard"); CtrlA();
                    Console.WriteLine("DIAG fixtureForeground="+(GetForegroundWindow()==fixtureWindow)+" focused="+AutomationElement.FocusedElement.Current.Name);
                    object pattern; if(textElement.TryGetCurrentPattern(TextPattern.Pattern,out pattern)) { var ranges=((TextPattern)pattern).GetSelection(); Console.WriteLine("DIAG selectionLength="+(ranges.Length==0?0:ranges[0].GetText(-1).Length)); }
                    Wait(() => ClipboardText() == Text, "Selected text was not copied exactly");
                });
                Test("Ordinary click keeps clipboard", () => { Clipboard.SetText("sentinel-click"); Click(x, y); Thread.Sleep(400); Check(ClipboardText() == "sentinel-click", "Ordinary click overwrote clipboard"); });
                Test("Double click copies selected word", () => { Clipboard.SetText("sentinel-double"); Click(x, y); Thread.Sleep(50); Click(x, y); Wait(() => ClipboardText().Trim() == "AutoCopy", "Double-click word not copied"); });
                Test("Mouse drag copies selected text", () => {
                    Click(x, y); Clipboard.SetText("sentinel-drag"); SetCursorPos((int)rectangle.X + 3, y); mouse_event(2,0,0,0,UIntPtr.Zero);
                    for(int i=0;i<12;i++) { SetCursorPos((int)rectangle.X+3+i*10,y); Thread.Sleep(20); }
                    mouse_event(4,0,0,0,UIntPtr.Zero); Wait(() => ClipboardText().StartsWith("AutoCopy") && ClipboardText() != "sentinel-drag", "Drag selection not copied");
                });
                Test("New manual clipboard content cancels pending copy", () => {
                    Click(x,y); CtrlA(); Clipboard.SetText("sentinel-new-manual-copy");
                    var until=Stopwatch.StartNew(); while(until.ElapsedMilliseconds<500) { Application.DoEvents(); Thread.Sleep(20); }
                    Check(ClipboardText()=="sentinel-new-manual-copy", "Pending selection overwrote newer clipboard content");
                });
                Test("Password selection preserves clipboard", () => { var password = Element(fixtureWindow, "Password text").Current.BoundingRectangle; Click((int)password.X+20,(int)password.Y+15); Clipboard.SetText("sentinel-password"); CtrlA(); Thread.Sleep(700); Check(ClipboardText()=="sentinel-password", "Password field caused clipboard change"); });
                IntPtr settings = IntPtr.Zero;
                Test("Second launch opens existing settings", () => {
                    using(var second = Process.Start(exe)) { Check(second.WaitForExit(4000), "Second instance remained alive"); }
                    Wait(() => (settings=FindWindow(null,"CtrlCopy — Настройки")) != IntPtr.Zero && IsWindowVisible(settings), "Settings did not open");
                    Check(Process.GetProcessesByName("AutoCopy").Length==1, "Duplicate tray processes");
                    Check(IsWindowVisible(settings), "Settings stayed hidden after background launch");
                });
                Test("Pause prevents copying", () => { ToggleEnabled(settings); SetForegroundWindow(fixtureWindow); Thread.Sleep(200); Click(x,y); Clipboard.SetText("sentinel-paused"); CtrlA(); Thread.Sleep(600); Check(ClipboardText()=="sentinel-paused", "Paused app copied text"); });
                Test("Re-enable resumes copying", () => { SetForegroundWindow(settings); ToggleEnabled(settings); SetForegroundWindow(fixtureWindow); Thread.Sleep(200); Click(x,y); Clipboard.SetText("sentinel-resume"); CtrlA(); Wait(()=>ClipboardText()==Text,"Resume failed"); });
                Test("Closing settings leaves tray utility alive", () => { PostMessage(settings,0x10,IntPtr.Zero,IntPtr.Zero); Thread.Sleep(350); Check(!app.HasExited && !IsWindowVisible(settings),"Settings close exited app or stayed visible"); });
                Test("Saved exclusion prevents actual selection copying", () => {
                    PostMessage(FindWindow(null,"AutoCopy.MessageWindow"),0x8002,IntPtr.Zero,IntPtr.Zero);
                    Check(app.WaitForExit(3000),"Could not restart utility for exclusion check"); app.Dispose();
                    File.WriteAllText(config,"{\"Enabled\":true,\"Popup\":false,\"Sound\":false,\"Exclusions\":[\"selectionfixture.exe\"]}");
                    app=Process.Start(start); Thread.Sleep(500);
                    Click(x,y); Clipboard.SetText("sentinel-excluded"); CtrlA();
                    var until=Stopwatch.StartNew(); while(until.ElapsedMilliseconds<600) { Application.DoEvents(); Thread.Sleep(20); }
                    Check(ClipboardText()=="sentinel-excluded","Excluded app still copied text");
                });
            }
            catch(Exception ex) { failed++; Console.WriteLine("FAIL integration setup: "+ex.Message); }
            finally
            {
                if (fixture != null && !fixture.HasExited) { fixture.Kill(); fixture.WaitForExit(); }
                if (app != null && !app.HasExited) { PostMessage(FindWindow(null,"AutoCopy.MessageWindow"),0x8002,IntPtr.Zero,IntPtr.Zero); if(!app.WaitForExit(2000)) { app.Kill(); app.WaitForExit(); } }
                if (original == null) { if(File.Exists(config)) File.Delete(config); } else File.WriteAllBytes(config,original);
                try { Clipboard.SetDataObject(backup,true,5,50); } catch { }
            }
            Console.WriteLine(passed+" passed, "+failed+" failed");
            if(failed>0 && File.Exists(diagnostic)) Console.WriteLine(File.ReadAllText(diagnostic));
            return failed==0 ? 0 : 1;
        }
    }
}
