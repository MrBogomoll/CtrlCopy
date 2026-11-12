using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Reflection;
using System.Windows.Media.Imaging;

namespace AutoCopy.Tests
{
    internal static class RenderSettings
    {
        [STAThread]
        public static void Main(string[] args)
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            var settings = new AppSettings();
            using(var toast=new CopyToast())
            {
                toast.View.Measure(new System.Windows.Size(24,24));toast.View.Arrange(new System.Windows.Rect(0,0,24,24));toast.View.UpdateLayout();
                var image=new RenderTargetBitmap(24,24,96,96,System.Windows.Media.PixelFormats.Pbgra32);image.Render(toast.View);
                var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));
                using(var stream=System.IO.File.Create(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(args[0]),"copy-indicator.png")))encoder.Save(stream);
            }
            using (var form = new SettingsForm(settings, () => { }))
            {
                foreach (bool dark in new[] { false, true })
                {
                    form.ApplyTheme(dark);
                    var view = form.View;
                    view.Measure(new System.Windows.Size(460, double.PositiveInfinity));
                    view.Arrange(new System.Windows.Rect(0, 0, 460, view.DesiredSize.Height)); view.UpdateLayout();
                    var image = new RenderTargetBitmap(460, (int)Math.Ceiling(view.ActualHeight), 96, 96, System.Windows.Media.PixelFormats.Pbgra32); image.Render(view);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
                    string output = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(args[0]), dark ? "settings-dark.png" : "settings-light.png");
                    using (var stream = System.IO.File.Create(output)) encoder.Save(stream);
                    Console.WriteLine("Rendered " + (dark ? "dark" : "light") + " compact settings 460x" + view.ActualHeight);
                }
                var viewport = (System.Windows.Controls.ScrollViewer)typeof(SettingsForm).GetField("viewport", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
                var handle = form.Handle; form.Size = new Size(320, 480);
                viewport.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.ContextIdle, new Action(() => { }));
                viewport.Measure(new System.Windows.Size(320, 480)); viewport.Arrange(new System.Windows.Rect(0, 0, 320, 480)); viewport.UpdateLayout();
                foreach (bool bottom in new[] { false, true })
                {
                    if (bottom) { viewport.ScrollToBottom(); viewport.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.ContextIdle, new Action(() => { })); viewport.UpdateLayout(); }
                    var small = new RenderTargetBitmap(320, 480, 96, 96, System.Windows.Media.PixelFormats.Pbgra32); small.Render(viewport);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(small));
                    using (var stream = System.IO.File.Create(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(args[0]), bottom ? "settings-small-bottom.png" : "settings-small-top.png"))) encoder.Save(stream);
                }
                Console.WriteLine("Small viewport: 320x480; extent=" + viewport.ExtentHeight + "; bottom offset=" + viewport.VerticalOffset);
                if (viewport.ExtentHeight > 480 && viewport.VerticalOffset <= 0) throw new Exception("Small viewport cannot scroll to bottom");
            }
            using(var menu = new TrayMenu(()=>{},()=>{},()=>{}))
            {
                menu.SetEnabled(true);
                foreach(bool dark in new[]{false,true})
                {
                    UiTheme.Apply(menu.View,dark);menu.View.Measure(new System.Windows.Size(294,double.PositiveInfinity));menu.View.Arrange(new System.Windows.Rect(0,0,294,menu.View.DesiredSize.Height));menu.View.UpdateLayout();
                    var image=new RenderTargetBitmap(294,(int)Math.Ceiling(menu.View.ActualHeight),96,96,System.Windows.Media.PixelFormats.Pbgra32);image.Render(menu.View);
                    var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));
                    using(var stream=System.IO.File.Create(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(args[0]),dark?"tray-menu-dark.png":"tray-menu-light.png")))encoder.Save(stream);
                }
            }
        }
    }
}
