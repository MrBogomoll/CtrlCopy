using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Forms = System.Windows.Forms;

namespace AutoCopy
{
    internal sealed class CopyToast : IDisposable
    {
        private readonly SmoothWindow window;
        private readonly Forms.Timer timer;
        internal FrameworkElement View { get; private set; }
        [DllImport("user32.dll",EntryPoint="GetWindowLongW")] private static extern int GetWindowLong(IntPtr hwnd,int index);
        [DllImport("user32.dll",EntryPoint="SetWindowLongW")] private static extern int SetWindowLong(IntPtr hwnd,int index,int value);
        internal CopyToast()
        {
            var glyph=new Path { Data=Geometry.Parse("M8,8 H20 A2,2 0 0 1 22,10 V20 A2,2 0 0 1 20,22 H10 A2,2 0 0 1 8,20 Z M4,16 C2.9,16 2,15.1 2,14 V4 C2,2.9 2.9,2 4,2 H14 C15.1,2 16,2.9 16,4"),Stroke=new SolidColorBrush(Color.FromRgb(80,120,232)),StrokeThickness=2,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round };
            var canvas=new Canvas { Width=24,Height=24 };canvas.Children.Add(glyph);
            View=new Border { Width=24,Height=24,Padding=new Thickness(3),Child=new Viewbox { Child=canvas } };
            window=new SmoothWindow(false) { Content=View,Width=24,Height=24,Topmost=true,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Title="CtrlCopy — Индикатор копирования",IsHitTestVisible=false };
            window.SourceInitialized+=(s,e)=>SetWindowLong(window.Handle,-20,GetWindowLong(window.Handle,-20)|0x08000020);
            timer=new Forms.Timer { Interval=850 };timer.Tick+=(s,e)=>{timer.Stop();window.Hide();};
        }
        internal void Display()
        {
            var cursor=Forms.Cursor.Position;var area=Forms.Screen.FromPoint(cursor).WorkingArea;
            window.Opacity=0;
            try
            {
                window.Show();
                Native.SetWindowPos(window.Handle,new IntPtr(-1),cursor.X,cursor.Y,0,0,0x11);
                double scale=VisualTreeHelper.GetDpi(window).DpiScaleX;int size=(int)Math.Ceiling(24*scale);
                int x=Math.Max(area.Left,Math.Min(cursor.X+(int)(15*scale),area.Right-size));
                int y=Math.Max(area.Top,Math.Min(cursor.Y+(int)(22*scale),area.Bottom-size));
                Native.SetWindowPos(window.Handle,new IntPtr(-1),x,y,size,size,0x10);
            }
            finally { window.Opacity=1; }
            timer.Stop();timer.Start();
        }
        public void Dispose() { timer.Dispose();window.Close(); }
    }
}
