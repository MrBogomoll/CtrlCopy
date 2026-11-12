using System;
using System.Windows;
using System.Windows.Forms.Integration;
using System.Windows.Interop;
using System.Windows.Media;

namespace AutoCopy
{
    internal sealed class SmoothWindow : Window
    {
        internal bool SessionEnding { get; private set; }
        internal event Action ScaleChanged;
        internal IntPtr Handle { get { return new WindowInteropHelper(this).EnsureHandle(); } }
        internal SmoothWindow(bool showInTaskbar)
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = showInTaskbar;
            ElementHost.EnableModelessKeyboardInterop(this);
            SourceInitialized += (s, e) => HwndSource.FromHwnd(Handle).AddHook(WindowMessage);
        }
        private IntPtr WindowMessage(IntPtr hwnd, int message, IntPtr w, IntPtr l, ref bool handled)
        {
            if (message == 0x11 || message == 0x16) SessionEnding = true;
            if (message == 0x2E0) Dispatcher.BeginInvoke(new Action(() => { var action = ScaleChanged; if (action != null) action(); }));
            return IntPtr.Zero;
        }
        internal void ShowVisible()
        {
            Show();
            Native.ShowWindow(Handle, 5);
            Activate();
        }
    }
}
