using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.Json;

namespace AvatarGifTool
{
    internal sealed class AppConfig
    {
        public string BaseWzPath { get; set; }
        public int WindowWidth { get; set; }
        public int WindowHeight { get; set; }
        public int DyeSaturationOffset { get; set; }
        public int DyeBrightnessOffset { get; set; }
        public int BackgroundColorArgb { get; set; } = Color.White.ToArgb();
        public string BackgroundImagePath { get; set; }
        public List<string> NormalExportActions { get; set; } = new List<string>();
        public string DyeExportAction { get; set; }
        public List<TemplateHistoryItem> TemplateHistory { get; set; } = new List<TemplateHistoryItem>();
    }

    internal sealed class TemplateHistoryItem
    {
        public int Skin { get; set; }
        public string SkinName { get; set; }
        public int Face { get; set; }
        public string FaceName { get; set; }
        public int Hair { get; set; }
        public string HairName { get; set; }
    }

    internal sealed class AppConfigStore
    {
        private readonly string configPath;

        public AppConfigStore(string configPath)
        {
            this.configPath = configPath;
        }

        public static AppConfigStore CreateDefault()
        {
            string appName = typeof(AppConfigStore).Assembly.GetName().Name ?? "AvatarGifTool";
            string path = Path.Combine(AppContext.BaseDirectory, appName + ".config");
            return new AppConfigStore(path);
        }

        public AppConfig Load()
        {
            if (!File.Exists(this.configPath))
            {
                return new AppConfig();
            }

            try
            {
                string json = File.ReadAllText(this.configPath);
                this.TryMarkHidden();
                return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            }
            catch
            {
                return new AppConfig();
            }
        }

        public void Save(AppConfig config)
        {
            try
            {
                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions
                {
                    WriteIndented = true,
                });
                File.WriteAllText(this.configPath, json);
                this.TryMarkHidden();
            }
            catch
            {
            }
        }

        private void TryMarkHidden()
        {
            try
            {
                if (!OperatingSystem.IsWindows() || !File.Exists(this.configPath))
                {
                    return;
                }

                FileAttributes attributes = File.GetAttributes(this.configPath);
                if ((attributes & FileAttributes.Hidden) == 0)
                {
                    File.SetAttributes(this.configPath, attributes | FileAttributes.Hidden);
                }
            }
            catch
            {
            }
        }
    }
}
