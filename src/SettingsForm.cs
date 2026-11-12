using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Linq;
using System.Windows.Markup;
using Microsoft.Win32;
using Wpf = System.Windows;
using WpfControls = System.Windows.Controls;
using Media = System.Windows.Media;
using Forms = System.Windows.Forms;

namespace AutoCopy
{
    internal sealed class SettingsForm : IDisposable, Forms.IWin32Window
    {
        private readonly SmoothWindow window;
        private readonly AppSettings settings;
        private readonly Action changed;
        private readonly Wpf.FrameworkElement view;
        private readonly WpfControls.ScrollViewer viewport;
        private readonly WpfControls.CheckBox enabled, startup, sound, popup, autoFields, keyboardSelections;
        private readonly WpfControls.TextBlock status, saveStatus;
        private readonly WpfControls.StackPanel exclusions;
        private bool initializing=true, disposing, disposed, dark;
        internal Wpf.FrameworkElement View { get { return view; } }
        public IntPtr Handle { get { return window.Handle; } }
        internal bool IsDisposed { get { return disposed; } }
        internal bool Visible { get { return window.IsVisible; } }
        internal Forms.FormWindowState WindowState { get { return (Forms.FormWindowState)(int)window.WindowState; } set { window.WindowState=(Wpf.WindowState)(int)value; } }
        internal Size Size { get { return new Size((int)window.Width,(int)window.Height); } set { window.Width=value.Width;window.Height=value.Height; } }
        internal event Forms.FormClosingEventHandler FormClosing;

        internal SettingsForm(AppSettings settings, Action changed)
        {
            this.settings=settings; this.changed=changed;
            using(Stream resource=Assembly.GetExecutingAssembly().GetManifestResourceStream("AutoCopy.CompactSettings.xaml"))
            {
                if(resource==null) throw new InvalidOperationException("Отсутствует интерфейс настроек.");
                view=(Wpf.FrameworkElement)XamlReader.Load(resource);
            }
            viewport=new WpfControls.ScrollViewer { Content=view, VerticalScrollBarVisibility=WpfControls.ScrollBarVisibility.Auto, HorizontalScrollBarVisibility=WpfControls.ScrollBarVisibility.Disabled, CanContentScroll=false };
            viewport.Resources[typeof(WpfControls.Primitives.ScrollBar)]=view.Resources[typeof(WpfControls.Primitives.ScrollBar)];
            window=new SmoothWindow(true) { Title="CtrlCopy — Настройки", Content=viewport, WindowStartupLocation=Wpf.WindowStartupLocation.CenterScreen };
            using(var icon=AppIcon.Create(true)) window.Icon=System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(icon.Handle,Wpf.Int32Rect.Empty,System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
            enabled=Find<WpfControls.CheckBox>("Enabled");startup=Find<WpfControls.CheckBox>("Startup");sound=Find<WpfControls.CheckBox>("Sound");popup=Find<WpfControls.CheckBox>("Popup");
            autoFields=Find<WpfControls.CheckBox>("AutoFields");keyboardSelections=Find<WpfControls.CheckBox>("KeyboardSelections");
            autoFields.IsChecked=settings.CopyAutoSelectedFields;keyboardSelections.IsChecked=settings.CopyKeyboardSelections;
            Bind(autoFields,()=>{settings.CopyAutoSelectedFields=autoFields.IsChecked==true;changed();});
            Bind(keyboardSelections,()=>{settings.CopyKeyboardSelections=keyboardSelections.IsChecked==true;changed();});
            status=Find<WpfControls.TextBlock>("Status");saveStatus=Find<WpfControls.TextBlock>("SaveStatus");exclusions=Find<WpfControls.StackPanel>("Exclusions");
            Find<WpfControls.Grid>("TitleBar").MouseLeftButtonDown+=(s,e)=>window.DragMove();
            Find<WpfControls.Button>("CloseSettings").Click+=(s,e)=>{e.Handled=true;Close();};
            view.PreviewKeyDown+=(s,e)=>{if(e.Key==System.Windows.Input.Key.Escape){e.Handled=true;Close();}};
            enabled.IsChecked=settings.Enabled;sound.IsChecked=settings.Sound;popup.IsChecked=settings.Popup;startup.IsChecked=StartupRegistration.IsEnabled(Forms.Application.ExecutablePath);
            Bind(enabled,()=>{settings.Enabled=enabled.IsChecked==true;RefreshState();changed();});
            Bind(sound,()=>{settings.Sound=sound.IsChecked==true;changed();});Bind(popup,()=>{settings.Popup=popup.IsChecked==true;changed();});Bind(startup,ChangeStartup);
            Find<WpfControls.Button>("AddExclusion").Click+=(s,e)=>
            {
                using(var picker=new Forms.OpenFileDialog { Title="Выберите приложение для исключения",Filter="Приложения (*.exe)|*.exe",CheckFileExists=true })
                    if(picker.ShowDialog(this)==Forms.DialogResult.OK)AddExclusion(picker.FileName);
            };
            Find<WpfControls.Button>("RunningApps").Click+=(s,e)=>
            {
                var panel=Find<WpfControls.Border>("RunningPanel");
                panel.Visibility=panel.Visibility==Wpf.Visibility.Visible?Wpf.Visibility.Collapsed:Wpf.Visibility.Visible;
                if(panel.Visibility==Wpf.Visibility.Visible) RefreshRunningApps();
                SizeToContent();
            };
            Find<WpfControls.Button>("RefreshRunningApps").Click+=(s,e)=>RefreshRunningApps();
            ApplyTheme(UiTheme.IsDark);RefreshState();RefreshExclusions();initializing=false;
            window.Closing+=(s,e)=>
            {
                if(disposing)return;
                e.Cancel=true;
                var args=new Forms.FormClosingEventArgs(window.SessionEnding?Forms.CloseReason.WindowsShutDown:Forms.CloseReason.UserClosing,false);
                var handler=FormClosing;if(handler!=null)handler(this,args);e.Cancel=args.Cancel;
            };
            window.Closed+=(s,e)=>disposed=true;
            window.SourceInitialized+=(s,e)=>SizeToContent();window.ScaleChanged+=SizeToContent;
            SystemEvents.UserPreferenceChanged+=ThemeChanged;
        }
        private T Find<T>(string name) where T:class{return(T)view.FindName(name);}
        private void RefreshRunningApps()
        {
            var list=Find<WpfControls.StackPanel>("RunningList");list.Children.Clear();
            var apps=new System.Collections.Generic.SortedDictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            int own=Process.GetCurrentProcess().Id;
            foreach(var process in Process.GetProcesses())
            {
                using(process) try
                {
                    string name=SelectionPolicy.NormalizeProcess(process.ProcessName);
                    if(process.MainWindowHandle==IntPtr.Zero || string.IsNullOrWhiteSpace(process.MainWindowTitle) || SelectionPolicy.IsExcluded(name,process.Id,own,settings)) continue;
                    if(!apps.ContainsKey(name)) apps.Add(name,process.MainWindowTitle);
                }
                catch(Exception error) when(error is InvalidOperationException || error is System.ComponentModel.Win32Exception || error is NotSupportedException) { }
            }
            foreach(var app in apps)
            {
                string name=app.Key;
                var text=new WpfControls.TextBlock { Text=name+".exe — "+app.Value,TextTrimming=Wpf.TextTrimming.CharacterEllipsis,TextWrapping=Wpf.TextWrapping.NoWrap,ToolTip=app.Value };
                var button=new WpfControls.Button { Content=text,Style=(Wpf.Style)view.FindResource("SoftButton"),Margin=new Wpf.Thickness(0,3,0,0),HorizontalContentAlignment=Wpf.HorizontalAlignment.Stretch };
                System.Windows.Automation.AutomationProperties.SetName(button,"Исключить "+name+".exe");
                button.Click+=(s,e)=>{AddExclusion(name);Find<WpfControls.Border>("RunningPanel").Visibility=Wpf.Visibility.Collapsed;SizeToContent();};
                list.Children.Add(button);
            }
            if(apps.Count==0) { var text=new WpfControls.TextBlock { Text="Нет доступных приложений с открытыми окнами.",Margin=new Wpf.Thickness(4) };text.SetResourceReference(WpfControls.TextBlock.ForegroundProperty,"Muted");list.Children.Add(text); }
            SizeToContent();
        }
        private void Bind(WpfControls.CheckBox box,Action action){Wpf.RoutedEventHandler handler=(s,e)=>{if(!initializing)action();};box.Checked+=handler;box.Unchecked+=handler;}
        private void ChangeStartup()
        {
            try{StartupRegistration.SetEnabled(startup.IsChecked==true,Forms.Application.ExecutablePath);ShowSaveStatus(null);}
            catch(Exception error)when(error is System.Security.SecurityException||error is UnauthorizedAccessException||error is IOException)
            {
                initializing=true;startup.IsChecked=startup.IsChecked!=true;initializing=false;
                Forms.MessageBox.Show(this,"Не удалось изменить автозапуск:\n"+error.Message,"CtrlCopy",Forms.MessageBoxButtons.OK,Forms.MessageBoxIcon.Warning);
            }
        }
        private void ThemeChanged(object sender,UserPreferenceChangedEventArgs args)
        {
            if(disposed)return;window.Dispatcher.BeginInvoke(new Action(()=>{if(!disposed)ApplyTheme(UiTheme.IsDark);}));
        }
        internal void ApplyTheme(bool dark){this.dark=dark;UiTheme.Apply(viewport,dark);UiTheme.Apply(view,dark);RefreshState();}
        internal void RefreshState()
        {
            bool before=initializing;initializing=true;enabled.IsChecked=settings.Enabled;
            status.Text=settings.Enabled?"Автокопирование включено":"Автокопирование на паузе";
            Find<System.Windows.Shapes.Ellipse>("StateDot").SetResourceReference(System.Windows.Shapes.Shape.FillProperty,settings.Enabled?"Accent":"Muted");initializing=before;
        }
        internal void AddExclusion(string process)
        {
            string name=SelectionPolicy.NormalizeProcess(process);if(name.Length==0||settings.Exclusions.Contains(name))return;
            settings.Exclusions.Add(name);RefreshExclusions();changed();
        }
        internal void RefreshExclusions()
        {
            exclusions.Children.Clear();
            foreach(string process in settings.Exclusions)
            {
                var row=new WpfControls.Grid { Margin=new Wpf.Thickness(0,7,0,11) };row.ColumnDefinitions.Add(new WpfControls.ColumnDefinition());row.ColumnDefinitions.Add(new WpfControls.ColumnDefinition { Width=Wpf.GridLength.Auto });
                var name=new WpfControls.TextBlock { Text=process+".exe",FontSize=13,VerticalAlignment=Wpf.VerticalAlignment.Center };name.SetResourceReference(WpfControls.TextBlock.ForegroundProperty,"Muted");
                var remove=new WpfControls.Button { Content="Убрать",Style=(Wpf.Style)view.FindResource("SoftButton"),Padding=new Wpf.Thickness(8,3,8,3),Margin=new Wpf.Thickness(12,0,0,0) };
                System.Windows.Automation.AutomationProperties.SetName(remove,"Убрать "+process+" из исключений");
                string item=process;remove.Click+=(s,e)=>{settings.Exclusions.Remove(item);RefreshExclusions();changed();};row.Children.Add(name);row.Children.Add(remove);WpfControls.Grid.SetColumn(remove,1);exclusions.Children.Add(row);
            }
            SizeToContent();
        }
        internal void ShowSaveStatus(string error)
        {
            saveStatus.Text=error==null?"Сохранено":"Ошибка сохранения";saveStatus.ToolTip=error;
            if(error==null)saveStatus.SetResourceReference(WpfControls.TextBlock.ForegroundProperty,"Muted");
            else saveStatus.Foreground=new Media.SolidColorBrush(dark?Media.Color.FromRgb(255,153,153):Media.Color.FromRgb(176,38,38));
        }
        private void SizeToContent()
        {
            IntPtr handle=new System.Windows.Interop.WindowInteropHelper(window).Handle;
            var area=Forms.Screen.FromHandle(handle).WorkingArea;double scale=Media.VisualTreeHelper.GetDpi(window).DpiScaleX;
            double maxHeight=area.Height/scale,width=Math.Min(460,Math.Max(240,area.Width/scale-32));
            Find<WpfControls.ScrollViewer>("ExclusionScroll").MaxHeight=Math.Max(40,Math.Min(172,maxHeight-610));view.Measure(new Wpf.Size(width,double.PositiveInfinity));
            window.Width=width;window.Height=Math.Min(view.DesiredSize.Height,Math.Max(120,maxHeight-32));
            if(window.IsVisible){window.Left=Math.Max(area.Left/scale,Math.Min(window.Left,area.Right/scale-window.Width));window.Top=Math.Max(area.Top/scale,Math.Min(window.Top,area.Bottom/scale-window.Height));}
        }
        internal void Show(){window.ShowVisible();}
        internal void Hide(){window.Hide();}
        internal void Activate(){window.Activate();}
        internal void Close(){window.Close();}
        public void Dispose(){if(disposed)return;disposing=true;SystemEvents.UserPreferenceChanged-=ThemeChanged;window.Close();disposed=true;}
    }
}
