using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Media;
using Wpf = System.Windows;
using WpfControls = System.Windows.Controls;

namespace AutoCopy.Tests
{
    internal static class DesktopPolishTests
    {
        [DllImport("user32.dll", EntryPoint="GetWindowLongW")] private static extern int GetWindowLong(IntPtr window, int index);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateRectRgn(int x1, int y1, int x2, int y2);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr value);
        [DllImport("user32.dll")] private static extern int GetWindowRgn(IntPtr window, IntPtr region);
        private static int passed, failed;
        private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
        private static void Test(string label, Action action)
        {
            try { action(); passed++; Console.WriteLine("PASS " + label); }
            catch(Exception ex) { failed++; Console.WriteLine("FAIL " + label + ": " + ex.Message); }
        }
        private static IEnumerable<WpfControls.Primitives.ButtonBase> Buttons(DependencyObjectView root)
        {
            foreach (var child in root.Children())
            {
                var button=child.Value as WpfControls.Primitives.ButtonBase; if(button!=null) yield return button;
                foreach(var nested in Buttons(child)) yield return nested;
            }
        }
        private sealed class DependencyObjectView
        {
            internal readonly Wpf.DependencyObject Value;
            internal DependencyObjectView(Wpf.DependencyObject value) { Value=value; }
            internal IEnumerable<DependencyObjectView> Children()
            {
                int count=VisualTreeHelper.GetChildrenCount(Value);
                for(int i=0;i<count;i++) yield return new DependencyObjectView(VisualTreeHelper.GetChild(Value,i));
            }
        }
        [STAThread]
        public static int Main()
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            string dir=Path.Combine(Path.GetTempPath(),"AutoCopy-polish-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
            try
            {
                var store=new SettingsStore(Path.Combine(dir,"settings.json")); store.Save(new AppSettings { Enabled=false });
                using(var context=new TrayContext(store))
                {
                    object menu=typeof(TrayContext).GetField("menu",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(context);
                    Test("Tray contains only pause, settings and exit",()=>{
                        string[] labels;
                        var native=menu as ContextMenuStrip;
                        if(native!=null) labels=native.Items.OfType<ToolStripMenuItem>().Select(item=>item.Text).ToArray();
                        else {
                            var view=(Wpf.FrameworkElement)menu.GetType().GetProperty("View",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).GetValue(menu);
                            view.Measure(new Wpf.Size(294,double.PositiveInfinity)); view.Arrange(new Wpf.Rect(0,0,294,view.DesiredSize.Height)); view.UpdateLayout();
                            labels=Buttons(new DependencyObjectView(view)).Select(button=>System.Windows.Automation.AutomationProperties.GetName(button)).ToArray();
                        }
                        Assert(labels.Length==3 && labels.Any(s=>s.Contains("Автокопирование")) && labels.Contains("Настройки") && labels.Contains("Выход"),"Unexpected tray actions: "+string.Join(", ",labels));
                        Assert(!labels.Any(s=>s.Contains("Исключ")),"Exclusion still present in tray");
                    });
                }
                Test("Closing tray menu allows it to reopen",()=>{
                    using(var menu=new TrayMenu(()=>{},()=>{},()=>{}))
                    {
                        var window=(Wpf.Window)typeof(TrayMenu).GetField("window",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(menu);
                        var area=Screen.PrimaryScreen.WorkingArea;
                        var point=new System.Drawing.Point(area.Right-1,area.Bottom-1);
                        menu.ShowAt(point,true);
                        Assert(window.IsVisible,"Tray popup did not open");
                        window.Close();
                        Assert(!window.IsVisible,"Closing tray popup did not hide it");
                        menu.ShowAt(point,true);
                        Assert(window.IsVisible,"Tray popup could not reopen after closing");
                        menu.Hide();
                    }
                });
                using(var form=new SettingsForm(new AppSettings(),()=>{}))
                {
                    Test("Settings corners use alpha composition without hard region",()=>{
                        IntPtr region=CreateRectRgn(0,0,0,0);
                        try { Assert((GetWindowLong(form.Handle,-20)&0x80000)!=0 && GetWindowRgn(form.Handle,region)==0,"Window still uses hard clipping rather than alpha composition"); }
                        finally { DeleteObject(region); }
                    });
                }
                Test("Copy icon preserves focus and disappears automatically",()=>{
                    using(var toast=new CopyToast())
                    {
                        IntPtr before=Native.GetForegroundWindow();toast.Display();Application.DoEvents();
                        var indicator=Native.FindWindow(null,"CtrlCopy — Индикатор копирования");
                        Assert(indicator!=IntPtr.Zero,"Indicator did not open");
                        Assert(Native.GetForegroundWindow()==before,"Indicator stole keyboard focus");
                        Assert((GetWindowLong(indicator,-20)&0x08000020)==0x08000020,"Indicator could intercept input");
                        var timer=System.Diagnostics.Stopwatch.StartNew();
                        while(timer.ElapsedMilliseconds<1000){Application.DoEvents();System.Threading.Thread.Sleep(10);}
                        var window=(Wpf.Window)typeof(CopyToast).GetField("window",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(toast);
                        Assert(!window.IsVisible,"Indicator did not hide");
                    }
                });
                Test("Copy feedback is a short quiet PCM click",()=>{
                    using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("AutoCopy.CopyClick.wav"))
                    {
                        Assert(stream!=null,"Dedicated copy click is missing");
                        using(var reader=new BinaryReader(stream))
                        {
                            Assert(new string(reader.ReadChars(4))=="RIFF","Invalid wave");reader.ReadInt32();Assert(new string(reader.ReadChars(4))=="WAVE","Invalid wave format");
                            reader.ReadBytes(8); Assert(reader.ReadInt16()==1,"Expected PCM");Assert(reader.ReadInt16()==1,"Expected mono");int rate=reader.ReadInt32();reader.ReadBytes(6);Assert(reader.ReadInt16()==16,"Expected 16 bit PCM");reader.ReadBytes(4);int bytes=reader.ReadInt32();
                            double duration=bytes/(rate*2.0),peak=0,sum=0;short last=0;int count=bytes/2;
                            for(int i=0;i<count;i++){last=reader.ReadInt16();double value=last/32768.0;peak=Math.Max(peak,Math.Abs(value));sum+=value*value;}
                            Assert(duration>=0.02 && duration<=0.1,"Feedback is not a short click");Assert(peak>0.002 && peak<=0.10 && Math.Sqrt(sum/count)<=0.04,"Feedback is too loud or silent");Assert(Math.Abs(last)<=16,"Wave ends abruptly");
                            Console.WriteLine("Click: "+(duration*1000).ToString("F0")+"ms, peak "+(20*Math.Log10(peak)).ToString("F1")+" dBFS");
                        }
                    }
                });
            }
            finally { Directory.Delete(dir,true); }
            Console.WriteLine(passed+" passed, "+failed+" failed"); return failed==0?0:1;
        }
    }
}
