using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace AutoCopy
{
    internal sealed class SelectionMonitor : IDisposable
    {
        private readonly Native.HookProc mouseProc, keyProc;
        private IntPtr mouseHook, keyHook;
        private int generation;
        private Native.Point down, previousDown;
        private uint previousTime;
        private int clicks;
        private bool pressed, textCursor, selectionKey;
        private bool selectAll;
        private bool focusKey;
        internal event Action<CopyRequest> Requested;
        internal int Generation { get { return Volatile.Read(ref generation); } }
        internal void Cancel() { Interlocked.Increment(ref generation); }
        internal SelectionMonitor()
        {
            mouseProc = Mouse; keyProc = Keyboard;
            IntPtr module = Native.GetModuleHandle(null);
            mouseHook = Native.SetWindowsHookEx(14, mouseProc, module, 0);
            keyHook = Native.SetWindowsHookEx(13, keyProc, module, 0);
            if (mouseHook == IntPtr.Zero || keyHook == IntPtr.Zero) { int error = Marshal.GetLastWin32Error(); Dispose(); throw new Win32Exception(error, "Не удалось включить автокопирование."); }
        }
        private IntPtr Mouse(int code, IntPtr message, IntPtr pointer)
        {
            if (code >= 0)
            {
                int kind = message.ToInt32();
                if (kind == 0x201 || kind == 0x202)
                {
                    var data = (Native.MouseData)Marshal.PtrToStructure(pointer, typeof(Native.MouseData));
                    if (kind == 0x201)
                    {
                        Cancel(); pressed = true; down = data.Point; textCursor = Native.TextCursor();
                        bool nearby = Math.Abs(down.X - previousDown.X) <= SystemInformation.DoubleClickSize.Width / 2 && Math.Abs(down.Y - previousDown.Y) <= SystemInformation.DoubleClickSize.Height / 2;
                        clicks = nearby && unchecked(data.Time - previousTime) <= SystemInformation.DoubleClickTime ? clicks + 1 : 1;
                        previousDown = down; previousTime = data.Time;
                    }
                    else if (pressed)
                    {
                        pressed = false;
                        if (SelectionPolicy.IsSelectionGesture(data.Point.X - down.X, data.Point.Y - down.Y, clicks, Native.KeyDown(0x10)))
                            Request(data.Point, clicks >= 2 ? SelectionKind.MultiClick : SelectionKind.Drag, textCursor);
                        else Request(data.Point, SelectionKind.AutoSelectedField, false);
                    }
                }
                else if (kind == 0x204 || kind == 0x207 || kind == 0x20B || kind == 0x20A) Cancel();
            }
            return Native.CallNextHookEx(mouseHook, code, message, pointer);
        }
        private IntPtr Keyboard(int code, IntPtr message, IntPtr pointer)
        {
            if (code >= 0)
            {
                var data = (Native.KeyboardData)Marshal.PtrToStructure(pointer, typeof(Native.KeyboardData));
                if (data.Extra != Native.CopyMarker)
                {
                    int key = (int)data.Key;
                    bool modifier = key == 0x10 || key == 0x11 || key == 0x12 || (key >= 0xA0 && key <= 0xA5);
                    bool downMessage = message.ToInt32() == 0x100 || message.ToInt32() == 0x104;
                    if (downMessage && !modifier)
                    {
                        Cancel();
                        selectionKey = SelectionPolicy.IsSelectionShortcut(key, Native.KeyDown(0x10), Native.KeyDown(0x11), Native.KeyDown(0x12), Native.KeyDown(0x5B) || Native.KeyDown(0x5C));
                        selectAll = selectionKey && key == 0x41;
                        focusKey = SelectionPolicy.IsFieldFocusShortcut(key, Native.KeyDown(0x11), Native.KeyDown(0x12), Native.KeyDown(0x10), Native.KeyDown(0x5B) || Native.KeyDown(0x5C));
                    }
                    else if (!downMessage && !modifier && selectionKey)
                    {
                        selectionKey = false;
                        var point = Cursor.Position;
                        Request(new Native.Point { X = point.X, Y = point.Y }, SelectionKind.Keyboard, false);
                    }
                    else if (!downMessage && !modifier && focusKey)
                    {
                        focusKey = false;
                        var point = Cursor.Position;
                        Request(new Native.Point { X = point.X, Y = point.Y }, SelectionKind.AutoSelectedField, false, true);
                    }
                }
            }
            return Native.CallNextHookEx(keyHook, code, message, pointer);
        }
        private void Request(Native.Point point, SelectionKind kind, bool text, bool focusShortcut = false)
        {
            var handler = Requested;
            if (handler != null) handler(new CopyRequest { Window = Native.GetForegroundWindow(), Generation = Generation, ClipboardSequence = Native.GetClipboardSequenceNumber(), X = point.X, Y = point.Y, Kind = kind, TextCursor = text, SelectAll = kind == SelectionKind.Keyboard && selectAll, FocusShortcut = focusShortcut });
        }
        public void Dispose()
        {
            if (mouseHook != IntPtr.Zero) { Native.UnhookWindowsHookEx(mouseHook); mouseHook = IntPtr.Zero; }
            if (keyHook != IntPtr.Zero) { Native.UnhookWindowsHookEx(keyHook); keyHook = IntPtr.Zero; }
        }
    }
}
