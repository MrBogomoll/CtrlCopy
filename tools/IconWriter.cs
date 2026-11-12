using System.IO;
using AutoCopy;

internal static class IconWriter
{
    public static void Main(string[] args)
    {
        int[] sizes={16,20,24,32,48,64,128,256};
        var frames=new byte[sizes.Length][];
        for(int i=0;i<sizes.Length;i++)
        {
            using(var bitmap=AppIcon.CreateBitmap(sizes[i],true))
            using(var stream=new MemoryStream()) { bitmap.Save(stream,System.Drawing.Imaging.ImageFormat.Png);frames[i]=stream.ToArray(); }
        }
        using(var writer=new BinaryWriter(File.Create(args[0])))
        {
            writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)sizes.Length);
            int offset=6+16*sizes.Length;
            for(int i=0;i<sizes.Length;i++) {writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)0);writer.Write((byte)0);writer.Write((ushort)1);writer.Write((ushort)32);writer.Write(frames[i].Length);writer.Write(offset);offset+=frames[i].Length;}
            foreach(var frame in frames)writer.Write(frame);
        }
        File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(args[0]),"app-icon.png"),frames[3]);
    }
}
