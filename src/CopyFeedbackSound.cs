using System;
using System.IO;
using System.Media;
using System.Reflection;

namespace AutoCopy
{
    internal sealed class CopyFeedbackSound : IDisposable
    {
        private readonly Stream stream;
        private readonly SoundPlayer player;
        internal CopyFeedbackSound()
        {
            stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AutoCopy.CopyClick.wav");
            if(stream == null) throw new InvalidOperationException("Отсутствует звук копирования.");
            player = new SoundPlayer(stream); player.Load();
        }
        internal void Play()
        {
            try { player.Play(); }
            catch(Exception error) when(error is InvalidOperationException || error is TimeoutException) { Diagnostics.Write("copy sound unavailable: "+error.GetType().Name); }
        }
        public void Dispose() { player.Stop(); player.Dispose(); stream.Dispose(); }
    }
}
