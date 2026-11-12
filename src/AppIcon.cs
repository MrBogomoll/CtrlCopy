using System.Drawing;
using System.Drawing.Drawing2D;

namespace AutoCopy
{
    internal static class AppIcon
    {
        internal static Icon Create(bool enabled)
        {
            using (var bitmap = CreateBitmap(32,enabled))
            {
                System.IntPtr handle = bitmap.GetHicon();
                try { using (var icon = Icon.FromHandle(handle)) return (Icon)icon.Clone(); }
                finally { Native.DestroyIcon(handle); }
            }
        }
        internal static Bitmap CreateBitmap(int size,bool enabled)
        {
            var bitmap=new Bitmap(size,size,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using(var graphics=Graphics.FromImage(bitmap))
            using(var pen=new Pen(enabled?Color.FromArgb(80,120,232):Color.FromArgb(119,125,139),2))
            using(var front=new GraphicsPath())
            using(var back=new GraphicsPath())
            {
                graphics.SmoothingMode=SmoothingMode.AntiAlias;graphics.ScaleTransform(size/24f,size/24f);
                pen.StartCap=pen.EndCap=LineCap.Round;pen.LineJoin=LineJoin.Round;
                front.AddArc(8,8,4,4,180,90);front.AddArc(18,8,4,4,270,90);front.AddArc(18,18,4,4,0,90);front.AddArc(8,18,4,4,90,90);front.CloseFigure();
                back.AddBezier(4,16,2.9f,16,2,15.1f,2,14);back.AddLine(2,14,2,4);back.AddBezier(2,4,2,2.9f,2.9f,2,4,2);back.AddLine(4,2,14,2);back.AddBezier(14,2,15.1f,2,16,2.9f,16,4);
                graphics.DrawPath(pen,front);graphics.DrawPath(pen,back);
            }
            return bitmap;
        }
    }
}
