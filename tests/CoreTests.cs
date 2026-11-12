using System;
using System.IO;
using AutoCopy;

namespace AutoCopy.Tests
{
    internal static class CoreTests
    {
        private static int failed, passed;
        private static void Test(string name, Action action)
        {
            try { action(); passed++; Console.WriteLine("PASS " + name); }
            catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + ex.Message); }
        }
        private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        public static int Main()
        {
            var settings = new AppSettings();
            settings.Exclusions.Add("Telegram.EXE");
            Test("Process paths normalize case and extension", () => Check(SelectionPolicy.NormalizeProcess(@"C:\Apps\Telegram.EXE") == "telegram", "Expected normalized process name"));
            Test("Dotted executable names keep their full process name", () => Check(SelectionPolicy.NormalizeProcess("editor.beta") == "editor.beta" && SelectionPolicy.NormalizeProcess(@"C:\Apps\editor.beta.exe") == "editor.beta", "Dotted app exclusion is inconsistent"));
            Test("Shell remains excluded", () => Check(SelectionPolicy.IsExcluded("explorer", 12, 13, settings), "Explorer could be copied"));
            Test("Terminal never receives synthetic copy", () => Check(SelectionPolicy.IsExcluded("WindowsTerminal.exe", 12, 13, settings), "Terminal is not excluded"));
            Test("Third-party terminals are excluded from copy shortcuts", () => { foreach (string terminal in new[] { "mintty", "putty", "wezterm-gui", "alacritty", "ConEmu64", "tabby" }) Check(SelectionPolicy.IsExcluded(terminal, 12, 13, settings), "Unsafe terminal: " + terminal); });
            Test("Arbitrary console host cannot receive copy shortcuts", () => Check(SelectionPolicy.IsExcluded("MyConsoleApp", 12, 13, settings, "ConsoleWindowClass"), "Unknown console executable could receive Ctrl+C"));
            Test("Own process is excluded", () => Check(SelectionPolicy.IsExcluded("anything", 13, 13, settings), "Own UI could be copied"));
            Test("User exclusions ignore case and .exe", () => Check(SelectionPolicy.IsExcluded("TELEGRAM", 12, 13, settings), "User exclusion failed"));
            Test("Ordinary editor remains enabled", () => Check(!SelectionPolicy.IsExcluded("notepad", 12, 13, settings), "Editor incorrectly excluded"));
            Test("Click is not a selection", () => Check(!SelectionPolicy.IsSelectionGesture(1, 1, 1, false), "Click recognized as selection"));
            Test("Drag selects", () => Check(SelectionPolicy.IsSelectionGesture(22, 0, 1, false), "Drag was ignored"));
            Test("Double click selects", () => Check(SelectionPolicy.IsSelectionGesture(0, 0, 2, false), "Double-click was ignored"));
            Test("Shift click selects", () => Check(SelectionPolicy.IsSelectionGesture(0, 0, 1, true), "Shift-click was ignored"));
            var request = new CopyRequest { Window = new IntPtr(25), Generation = 8, ClipboardSequence = 90 };
            Test("Current request accepted", () => Check(SelectionPolicy.IsCurrent(request, 8, new IntPtr(25), 90), "Valid selection rejected"));
            Test("Foreground change cancels pending copy", () => Check(!SelectionPolicy.IsCurrent(request, 8, new IntPtr(26), 90), "Stale foreground accepted"));
            Test("New input cancels pending copy", () => Check(!SelectionPolicy.IsCurrent(request, 9, new IntPtr(25), 90), "Stale input accepted"));
            Test("Manual clipboard change cancels pending copy", () => Check(!SelectionPolicy.IsCurrent(request, 8, new IntPtr(25), 91), "Manual clipboard overwritten"));
            var drag = new CopyRequest { Kind = SelectionKind.Drag, TextCursor = true };
            Test("Automatic field selection is opt-in",()=>Check(!SelectionPolicy.ShouldRead(new CopyRequest { Kind=SelectionKind.AutoSelectedField },new AppSettings()),"Auto fields enabled by default"));
            Test("Automatic fields and keyboard are independent",()=>{
                var s=new AppSettings { CopyAutoSelectedFields=true,CopyKeyboardSelections=false };
                Check(SelectionPolicy.ShouldRead(new CopyRequest { Kind=SelectionKind.AutoSelectedField },s),"Auto fields blocked");
                Check(!SelectionPolicy.ShouldRead(new CopyRequest { Kind=SelectionKind.Keyboard,SelectAll=true },s),"Ctrl+A not disabled");
                Check(SelectionPolicy.ShouldRead(drag,s),"Mouse selection affected");
            });
            Test("Automatic fields never send synthetic copy",()=>Check(!SelectionPolicy.ShouldFallback(new CopyRequest { Kind=SelectionKind.AutoSelectedField,TextCursor=true },false,false,true),"Automatic field fallback unsafe"));
            Test("Address focus shortcuts are distinct from selection",()=>Check(SelectionPolicy.IsFieldFocusShortcut(0x4C,true,false,false,false) && SelectionPolicy.IsFieldFocusShortcut(0x44,false,true,false,false) && !SelectionPolicy.IsSelectionShortcut(0x4C,false,true,false,false),"Focus shortcuts misclassified"));
            Test("Confirmed empty selection never triggers whole-line copy", () => Check(!SelectionPolicy.ShouldFallback(drag, true, false, true), "Empty selection would send Ctrl+C"));
            Test("UIA failure cannot bypass password validation", () => Check(!SelectionPolicy.ShouldFallback(drag, false, true, true), "Failed read would send Ctrl+C"));
            Test("Unsupported real text drag can use compatibility copy", () => Check(SelectionPolicy.ShouldFallback(drag, false, false, true), "Text drag fallback unavailable"));
            Test("Shift selection boundary never triggers fallback", () => Check(!SelectionPolicy.ShouldFallback(new CopyRequest { Kind = SelectionKind.Keyboard }, false, false, true), "Empty Shift selection could copy full line"));
            Test("Titlebar or object drag never uses focused editor fallback", () => Check(!SelectionPolicy.ShouldFallback(new CopyRequest { Kind = SelectionKind.Drag }, false, false, true), "Object drag could copy old editor selection"));
            Test("Select-all in unsupported text editor can copy", () => Check(SelectionPolicy.ShouldFallback(new CopyRequest { Kind = SelectionKind.Keyboard, SelectAll = true }, false, false, true), "Select-all fallback unavailable"));
            Test("Moving a window cannot recopy its old text selection", () => Check(!SelectionPolicy.ShouldRead(new CopyRequest { Kind = SelectionKind.Drag, TextCursor = false }), "Nontext drag can read stale editor selection"));
            Test("Real text gesture can read selection", () => Check(SelectionPolicy.ShouldRead(drag), "Real text drag blocked"));
            Test("Keyboard selection does not depend on mouse cursor", () => Check(SelectionPolicy.ShouldRead(new CopyRequest { Kind = SelectionKind.Keyboard }), "Keyboard selection blocked by mouse cursor"));
            Test("Windows move-window shortcut never recopies text", () => Check(!SelectionPolicy.IsSelectionShortcut(0x25, true, false, false, true), "Win+Shift+Left treated as text selection"));
            Test("Alt navigation never recopies text", () => Check(!SelectionPolicy.IsSelectionShortcut(0x25, true, false, true, false), "Alt+Shift+Left treated as text selection"));
            Test("Browser tab movement never recopies text", () => Check(!SelectionPolicy.IsSelectionShortcut(0x21, true, true, false, false) && !SelectionPolicy.IsSelectionShortcut(0x22, true, true, false, false), "Ctrl+Shift+Page treated as text selection"));
            Test("Word selection remains supported", () => Check(SelectionPolicy.IsSelectionShortcut(0x25, true, true, false, false), "Ctrl+Shift+Left blocked"));
            Test("Windows modified select-all is not text selection", () => Check(!SelectionPolicy.IsSelectionShortcut(0x41, false, true, false, true), "Win+Ctrl+A treated as text selection"));
            string temp = Path.Combine(Path.GetTempPath(), "AutoCopy-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try
            {
                string file = Path.Combine(temp, "settings.json");
                var store = new SettingsStore(file);
                Test("Older preferences retain keyboard behavior",()=>{File.WriteAllText(file,"{\"Enabled\":true}");var s=store.Load();Check(s.CopyKeyboardSelections && !s.CopyAutoSelectedFields,"Migration defaults incorrect");File.Delete(file);});
                Test("Selection options round-trip",()=>{store.Save(new AppSettings {CopyKeyboardSelections=false,CopyAutoSelectedFields=true});var s=store.Load();Check(!s.CopyKeyboardSelections && s.CopyAutoSelectedFields,"Selection options lost");File.Delete(file);});
                Test("Missing settings use safe defaults", () => { var s = store.Load(); Check(s.Enabled && s.Popup && !s.Sound && s.Exclusions.Count == 0, "Unexpected defaults"); });
                Test("Preferences round-trip to actual disk", () => { store.Save(new AppSettings { Enabled = false, Sound = true, Popup = false, Exclusions = new System.Collections.Generic.List<string> { "telegram", "notepad" } }); var s = new SettingsStore(file).Load(); Check(!s.Enabled && s.Sound && !s.Popup && s.Exclusions.Count == 2, "Settings were lost"); });
                Test("Corrupt settings preserved before defaults", () => { File.WriteAllText(file, "{ invalid JSON"); var s = store.Load(); Check(s.Enabled && Directory.GetFiles(temp, "settings.json.corrupt-*").Length == 1, "Corrupt original not preserved"); });
                Test("Failed save preserves existing preferences", () => { store.Save(new AppSettings { Sound = true }); using (var locked = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.None)) { bool threw = false; try { store.Save(new AppSettings { Sound = false }); } catch (IOException) { threw = true; } Check(threw, "Save failure was hidden"); } Check(store.Load().Sound, "Previous settings destroyed"); });
                Test("Invalid null exclusion list normalized", () => { File.WriteAllText(file, "{\"Exclusions\":null,\"Enabled\":true,\"Popup\":true}"); Check(store.Load().Exclusions != null, "Null exclusions could crash runtime"); });
            }
            finally { Directory.Delete(temp, true); }
            Console.WriteLine(passed + " passed, " + failed + " failed");
            return failed == 0 ? 0 : 1;
        }
    }
}
