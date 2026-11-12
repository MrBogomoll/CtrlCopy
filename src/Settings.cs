using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Xml;

namespace AutoCopy
{
    [DataContract]
    public sealed class AppSettings
    {
        [DataMember] public bool Enabled = true;
        [DataMember] public bool Sound;
        [DataMember] public bool Popup = true;
        [DataMember] public bool CopyAutoSelectedFields;
        [DataMember] public bool CopyKeyboardSelections = true;
        [DataMember] public List<string> Exclusions = new List<string>();
        [OnDeserializing] private void Defaults(StreamingContext context)
        {
            Enabled = true; Popup = true; Sound = false; CopyAutoSelectedFields = false; CopyKeyboardSelections = true; Exclusions = new List<string>();
        }
    }

    public sealed class SettingsStore
    {
        private readonly string path;
        public SettingsStore(string path) { this.path = Path.GetFullPath(path); }
        public AppSettings Load()
        {
            if (!File.Exists(path)) return new AppSettings();
            try
            {
                using (var stream = File.OpenRead(path))
                {
                    if (stream.Length > 1024 * 1024) throw new SerializationException("Settings file too large");
                    var settings = new DataContractJsonSerializer(typeof(AppSettings)).ReadObject(stream) as AppSettings;
                    if (settings == null) throw new SerializationException("Empty settings");
                    settings.Exclusions = (settings.Exclusions ?? new List<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).Select(SelectionPolicy.NormalizeProcess).Distinct().Take(512).ToList();
                    return settings;
                }
            }
            catch (Exception ex) when (ex is SerializationException || ex is XmlException || ex is FormatException)
            {
                File.Move(path, path + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6));
                return new AppSettings();
            }
        }
        public void Save(AppSettings settings)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    new DataContractJsonSerializer(typeof(AppSettings)).WriteObject(stream, settings);
                    stream.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
