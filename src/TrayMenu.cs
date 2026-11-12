using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace AutoCopy
{
    internal sealed class TrayMenu : IDisposable
    {
        private readonly SmoothWindow window;
        private readonly FrameworkElement view;
        private readonly ToggleButton toggle;
        private bool disposing;
        internal FrameworkElement View { get { return view; } }
        internal TrayMenu(Action onToggle, Action onSettings, Action onExit)
        {
            using(Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AutoCopy.TrayMenu.xaml")) view = (FrameworkElement)XamlReader.Load(stream);
            UiTheme.Apply(view, UiTheme.IsDark);
            window = new SmoothWindow(false) { Title = "CtrlCopy — Меню", Content = view, Topmost = true, WindowStartupLocation = WindowStartupLocation.Manual, SizeToContent = SizeToContent.WidthAndHeight };
            toggle = (ToggleButton)view.FindName("Enabled");
            var settings = (Button)view.FindName("Settings"); var exit = (Button)view.FindName("Exit");
            toggle.Click += (s,e) => { Hide(); onToggle(); };
            settings.Click += (s,e) => { Hide(); onSettings(); };
            exit.Click += (s,e) => { Hide(); onExit(); };
            window.Deactivated += (s,e) => Hide();
            window.Closing += (s,e) => { if(!disposing && !window.SessionEnding) { e.Cancel=true; Hide(); } };
            view.PreviewKeyDown += (s,e) =>
            {
                if(e.Key == Key.Escape) { Hide(); e.Handled=true; }
                else if(e.Key == Key.Up || e.Key == Key.Down)
                {
                    Control[] items = { toggle, settings, exit }; int at = Array.FindIndex(items,item=>item.IsKeyboardFocusWithin);
                    int next=at<0?(e.Key==Key.Down?0:items.Length-1):(at+(e.Key==Key.Down?1:items.Length-1))%items.Length;
                    items[next].Focus(); e.Handled=true;
                }
            };
        }
        internal void SetEnabled(bool enabled)
        {
            toggle.IsChecked = enabled;
            ((TextBlock)view.FindName("EnabledLabel")).Text = enabled ? "Автокопирование включено" : "Автокопирование на паузе";
            ((System.Windows.Shapes.Path)view.FindName("EnabledMark")).Visibility = enabled ? Visibility.Visible : Visibility.Hidden;
        }
        internal void ShowAt(System.Drawing.Point point, bool enabled)
        {
            SetEnabled(enabled); UiTheme.Apply(view, UiTheme.IsDark);
            var area = Forms.Screen.FromPoint(point).WorkingArea;
            view.Measure(new System.Windows.Size(294,double.PositiveInfinity));
            window.Opacity=0;
            try
            {
                window.ShowVisible();
                Native.SetWindowPos(window.Handle,new IntPtr(-1),point.X,point.Y,0,0,0x11);
                double scale=VisualTreeHelper.GetDpi(window).DpiScaleX;
                int width=(int)Math.Ceiling(view.DesiredSize.Width*scale),height=(int)Math.Ceiling(view.DesiredSize.Height*scale);
                int x=Math.Max(area.Left,Math.Min(point.X,area.Right-width)),y=Math.Max(area.Top,Math.Min(point.Y,area.Bottom-height));
                Native.SetWindowPos(window.Handle,new IntPtr(-1),x,y,width,height,0x10);
            }
            finally { window.Opacity=1; }
        }
        internal void Hide() { if(window.IsVisible) window.Hide(); }
        public void Dispose() { if(disposing)return; disposing=true; window.Close(); }
    }
}
