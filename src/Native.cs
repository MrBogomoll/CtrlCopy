using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace AutoCopy
{
    internal static class Native
    {
        internal const uint OpenSettingsMessage = 0x8001, ExitMessage = 0x8002;
        internal static readonly UIntPtr CopyMarker = new UIntPtr(0xAC0C0DECu);
        internal delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);
        [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] internal struct MouseData { public Point Point; public uint MouseDataValue, Flags, Time; public UIntPtr Extra; }
        [StructLayout(LayoutKind.Sequential)] internal struct KeyboardData { public uint Key, Scan, Flags, Time; public UIntPtr Extra; }
        [StructLayout(LayoutKind.Sequential)] private struct CursorInfo { public int Size, Flags; public IntPtr Cursor; public Point Position; }
        [StructLayout(LayoutKind.Sequential)] private struct Input { public uint Type; public InputUnion Data; }
        [StructLayout(LayoutKind.Explicit)] private struct InputUnion { [FieldOffset(0)] public KeyboardInput Keyboard; [FieldOffset(0)] public MouseInput Mouse; }
        [StructLayout(LayoutKind.Sequential)] private struct KeyboardInput { public ushort Key, Scan; public uint Flags, Time; public UIntPtr Extra; }
        [StructLayout(LayoutKind.Sequential)] private struct MouseInput { public int X, Y; public uint Data, Flags, Time; public UIntPtr Extra; }
        [DllImport("user32.dll", SetLastError = true)] internal static extern IntPtr SetWindowsHookEx(int kind, HookProc callback, IntPtr module, uint thread);
        [DllImport("user32.dll")] internal static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] internal static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr GetModuleHandle(string module);
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")] internal static extern uint GetClipboardSequenceNumber();
        [DllImport("user32.dll")] internal static extern IntPtr WindowFromPoint(Point point);
        [DllImport("user32.dll")] internal static extern IntPtr GetAncestor(IntPtr window, uint kind);
        [DllImport("user32.dll")] private static extern bool GetCursorInfo(ref CursorInfo info);
        [DllImport("user32.dll")] private static extern IntPtr LoadCursor(IntPtr instance, IntPtr id);
        [DllImport("user32.dll")] internal static extern bool DestroyIcon(IntPtr icon);
        [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] input, int size);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr FindWindow(string cls, string title);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder name, int max);
        [DllImport("user32.dll")] internal static extern bool PostMessage(IntPtr window, uint message, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] internal static extern bool ShowWindow(IntPtr window, int command);
        [DllImport("user32.dll", SetLastError=true)] internal static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll")] internal static extern bool AllowSetForegroundWindow(uint processId);
        [DllImport("user32.dll")] internal static extern bool ReleaseCapture();
        [DllImport("user32.dll")] internal static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr w, IntPtr l);
        [DllImport("dwmapi.dll")] internal static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
        internal static bool KeyDown(int key) { return (GetAsyncKeyState(key) & 0x8000) != 0; }
        internal static bool InputBusy { get { return KeyDown(1) || KeyDown(2) || KeyDown(4) || KeyDown(5) || KeyDown(6) || KeyDown(0x10) || KeyDown(0x11) || KeyDown(0x12) || KeyDown(0x5B) || KeyDown(0x5C); } }
        internal static string WindowClass(IntPtr window) { var name = new StringBuilder(256); GetClassName(window, name, name.Capacity); return name.ToString(); }
        internal static bool TextCursor()
        {
            var cursor = new CursorInfo { Size = Marshal.SizeOf(typeof(CursorInfo)) };
            return GetCursorInfo(ref cursor) && cursor.Cursor == LoadCursor(IntPtr.Zero, new IntPtr(32513));
        }
        internal static string ProcessName(IntPtr window, out int id)
        {
            uint pid;
            GetWindowThreadProcessId(window, out pid);
            id = (int)pid;
            if (pid == 0) return "";
            try { using (var process = Process.GetProcessById(id)) return process.ProcessName; }
            catch (ArgumentException) { return ""; }
            catch (InvalidOperationException) { return ""; }
        }
        internal static bool SendCopy()
        {
            var inputs = new Input[4];
            ushort[] keys = { 0x11, 0x43, 0x43, 0x11 };
            for (int i = 0; i < inputs.Length; i++) inputs[i] = new Input { Type = 1, Data = new InputUnion { Keyboard = new KeyboardInput { Key = keys[i], Flags = i >= 2 ? 2u : 0u, Extra = CopyMarker } } };
            uint sent = SendInput(4, inputs, Marshal.SizeOf(typeof(Input)));
            if (sent != 4)
            {
                // Release our keys if UIPI or another input restriction interrupted the sequence.
                SendInput(2, new[] { inputs[2], inputs[3] }, Marshal.SizeOf(typeof(Input)));
                return false;
            }
            return true;
        }
    }
}
