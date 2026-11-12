using System;
using System.IO;
using System.Linq;

namespace AutoCopy
{
    public enum SelectionKind { Drag, MultiClick, Keyboard, AutoSelectedField }

    public sealed class CopyRequest
    {
        public IntPtr Window;
        public int Generation;
        public uint ClipboardSequence;
        public int X, Y;
        public SelectionKind Kind;
        public bool TextCursor;
        public bool SelectAll;
        public bool FocusShortcut;
    }

    public static class SelectionPolicy
    {
        private static readonly string[] AlwaysExcluded = { "explorer", "shellexperiencehost", "startmenuexperiencehost", "searchhost", "searchapp", "textinputhost", "lockapp", "logonui", "windowsterminal", "windowsterminalpreview", "openconsole", "conhost", "cmd", "powershell", "pwsh", "mintty", "putty", "puttytel", "kitty", "conemu", "conemu64", "conemuc", "conemuc64", "wezterm", "wezterm-gui", "alacritty", "tabby", "hyper", "bash", "wsl", "wslhost", "ubuntu" };
        public static string NormalizeProcess(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            string name = value.Trim().Trim('"').Replace('/', '\\');
            name = name.Substring(name.LastIndexOf('\\') + 1);
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name.Substring(0, name.Length - 4);
            return name.ToLowerInvariant();
        }
        public static bool IsExcluded(string name, int processId, int ownId, AppSettings settings, string windowClass = null)
        {
            string normalized = NormalizeProcess(name);
            bool consoleOrShell = new[] { "ConsoleWindowClass", "CASCADIA_HOSTING_WINDOW_CLASS", "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd" }.Contains(windowClass, StringComparer.OrdinalIgnoreCase);
            return consoleOrShell || processId == ownId || normalized.Length == 0 || AlwaysExcluded.Contains(normalized) ||
                settings.Exclusions.Any(item => NormalizeProcess(item) == normalized);
        }
        public static bool IsSelectionGesture(int dx, int dy, int clickCount, bool shift)
        {
            return Math.Abs(dx) >= 4 || Math.Abs(dy) >= 4 || clickCount >= 2 || shift;
        }
        public static bool IsCurrent(CopyRequest request, int generation, IntPtr foreground, uint clipboard)
        {
            return request != null && request.Generation == generation && request.Window == foreground && request.ClipboardSequence == clipboard;
        }
        public static bool ShouldFallback(CopyRequest request, bool selectionAvailable, bool readFailed, bool textContext)
        {
            return request.Kind != SelectionKind.AutoSelectedField && !selectionAvailable && !readFailed &&
                ((request.Kind != SelectionKind.Keyboard && request.TextCursor) || (request.SelectAll && textContext));
        }
        public static bool ShouldRead(CopyRequest request) { return request.Kind == SelectionKind.Keyboard || request.TextCursor; }
        public static bool ShouldRead(CopyRequest request, AppSettings settings)
        {
            if (request.Kind == SelectionKind.AutoSelectedField) return settings.CopyAutoSelectedFields;
            if (request.Kind == SelectionKind.Keyboard && !settings.CopyKeyboardSelections) return false;
            return ShouldRead(request);
        }
        public static bool IsFieldFocusShortcut(int key, bool control, bool alt, bool shift, bool windows)
        {
            return !windows && !shift && ((control && !alt && key == 0x4C) || (alt && !control && key == 0x44) || (!control && !alt && key == 0x75));
        }
        public static bool IsSelectionShortcut(int key, bool shift, bool control, bool alt, bool windows)
        {
            if (alt || windows) return false;
            if (key == 0x41) return control;
            if (!shift || key < 0x21 || key > 0x28) return false;
            return !(control && (key == 0x21 || key == 0x22));
        }
    }
}
