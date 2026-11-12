using System;
using System.IO;

namespace AutoCopy
{
    internal static class Diagnostics
    {
        private static readonly string path = Environment.GetEnvironmentVariable("AUTOCOPY_DIAGNOSTICS");
        private static readonly object gate = new object();
        internal static void Write(string message)
        {
            if (string.IsNullOrEmpty(path)) return;
            try { lock (gate) { if (!File.Exists(path) || new FileInfo(path).Length < 1024 * 1024) File.AppendAllText(path, DateTime.UtcNow.ToString("HH:mm:ss.fff ") + message + Environment.NewLine); } }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
