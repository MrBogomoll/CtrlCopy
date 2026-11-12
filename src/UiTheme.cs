using System;
using System.IO;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Media;

namespace AutoCopy
{
    internal static class UiTheme
    {
        internal static bool IsDark
        {
            get
            {
                try { using(var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")) return key != null && Convert.ToInt32(key.GetValue("AppsUseLightTheme", 1)) == 0; }
                catch(Exception ex) when(ex is System.Security.SecurityException || ex is UnauthorizedAccessException || ex is IOException || ex is FormatException) { return false; }
            }
        }
        internal static void Apply(FrameworkElement view, bool dark)
        {
            string[] keys = { "Bg", "Panel", "Text", "Muted", "Line", "Hover", "Accent" };
            string[] colors = dark ? new[] { "#202126", "#292A30", "#EFF0F5", "#B3B7C4", "#3C3F49", "#353740", "#5078E8" } : new[] { "#F6F7F9", "#FFFFFF", "#20232B", "#656B78", "#E1E4EB", "#EEF0F5", "#5078E8" };
            for(int i=0;i<keys.Length;i++) { var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[i])); brush.Freeze(); view.Resources[keys[i]] = brush; }
        }
    }
}
