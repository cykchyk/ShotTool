using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace ShotTool
{
    /// <summary>设置项，落在 %APPDATA%\ShotTool\config.json（可用环境变量 SHOTTOOL_CONFIG_DIR 覆盖）。</summary>
    [DataContract]
    internal sealed class AppConfig
    {
        [DataMember(Name = "configVersion")] public int ConfigVersion { get; set; }
        [DataMember(Name = "folderName")] public string FolderName { get; set; }
        [DataMember(Name = "hotkeyModifiers")] public uint HotkeyModifiers { get; set; }
        [DataMember(Name = "hotkeyKey")] public uint HotkeyKey { get; set; }
        [DataMember(Name = "topMost")] public bool TopMost { get; set; }
        [DataMember(Name = "copyToClipboard")] public bool CopyToClipboard { get; set; }
        [DataMember(Name = "hideWindowOnCapture")] public bool HideWindowOnCapture { get; set; }
        [DataMember(Name = "windowX")] public int WindowX { get; set; }
        [DataMember(Name = "windowY")] public int WindowY { get; set; }

        public AppConfig()
        {
            SetDefaults();
        }

        [OnDeserializing]
        private void OnDeserializing(StreamingContext context)
        {
            SetDefaults();
        }

        private void SetDefaults()
        {
            ConfigVersion = 1;
            FolderName = "截图";
            HotkeyModifiers = Native.MOD_CONTROL | Native.MOD_ALT;
            HotkeyKey = 0x53; // S
            TopMost = true;
            CopyToClipboard = true;
            HideWindowOnCapture = true;
            WindowX = int.MinValue;
            WindowY = int.MinValue;
        }

        public static string ConfigDirectory
        {
            get
            {
                string overridden = Environment.GetEnvironmentVariable("SHOTTOOL_CONFIG_DIR");
                if (!string.IsNullOrEmpty(overridden))
                {
                    return overridden;
                }

                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ShotTool");
            }
        }

        public static string ConfigPath
        {
            get { return Path.Combine(ConfigDirectory, "config.json"); }
        }

        public static AppConfig Load()
        {
            try
            {
                string path = ConfigPath;
                if (!File.Exists(path))
                {
                    return new AppConfig();
                }

                using (FileStream stream = File.OpenRead(path))
                {
                    var serializer = new DataContractJsonSerializer(typeof(AppConfig));
                    var config = serializer.ReadObject(stream) as AppConfig;
                    return config ?? new AppConfig();
                }
            }
            catch
            {
                // 配置坏了就用默认值，不影响截图本身
                return new AppConfig();
            }
        }

        public void Save()
        {
            try
            {
                string directory = ConfigDirectory;
                Directory.CreateDirectory(directory);

                string path = ConfigPath;
                string temp = path + ".tmp";

                using (FileStream stream = File.Create(temp))
                {
                    var serializer = new DataContractJsonSerializer(typeof(AppConfig));
                    serializer.WriteObject(stream, this);
                }

                if (File.Exists(path))
                {
                    File.Replace(temp, path, null);
                }
                else
                {
                    File.Move(temp, path);
                }
            }
            catch
            {
                // 设置写不进去也继续运行
            }
        }
    }
}
