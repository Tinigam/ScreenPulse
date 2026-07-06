using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;

namespace ScreenPulse.Models;

public class AppSettings
{
    public int CaptureIntervalMinutes { get; set; } = 5;
    public int IdleSkipThresholdMinutes { get; set; } = 2;
    public int RetentionDays { get; set; } = 14;
    public bool AutoStartEnabled { get; set; } = false;
    public bool MonitoringEnabled { get; set; } = true;
    public string StorageFolder { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScreenPulse");

    // 0.0~1.0, 越接近 1 表示要求画面越相似才判定为“未变化”
    public double DuplicateSimilarityThreshold { get; set; } = 0.985;

    // "zh" 或 "en"
    public string Language { get; set; } = "zh";

    public ObservableCollection<string> ExcludedProcesses { get; set; } = new();

    private static string ConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScreenPulse", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded is not null) return loaded;
            }
        }
        catch
        {
            // 配置文件损坏时退回默认设置
        }
        return new AppSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }
}
