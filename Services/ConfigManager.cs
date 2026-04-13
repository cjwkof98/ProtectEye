using System;
using System.IO;
using System.Text.Json;
using ProtectEye.Models;

namespace ProtectEye.Services;

public class ConfigManager
{
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
        "ProtectEye", 
        "config.json");

    private static AppConfig? _current;

    public static AppConfig Current
    {
        get
        {
            if (_current == null)
            {
                Load();
            }
            return _current!;
        }
    }

    public static void Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json);
                if (config != null)
                {
                    _current = config;
                    return;
                }
            }
        }
        catch (Exception)
        {
            // 如果读取失败，也使用默认设置
        }

        _current = new AppConfig();
        Save();
    }

    public static void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(ConfigPath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir!);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(_current, options);
            File.WriteAllText(ConfigPath, json);
        }
        catch (Exception)
        {
            // 记录日志，暂略
        }
    }
}
