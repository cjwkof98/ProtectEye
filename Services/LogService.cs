using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace ProtectEye.Services;

public enum LogEventType
{
    WorkStarted,
    RestStarted,
    RestSkipped,
    RestCompleted,
    WarningShown,
    PausedByUser,
    ResumedByUser,
    IdleReset,
    AppStarted,
    AppExited
}

public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public LogEventType EventType { get; set; }
    public string? Note { get; set; }

    public string EventTypeText => EventType switch
    {
        LogEventType.WorkStarted    => "开始工作",
        LogEventType.RestStarted    => "进入休息",
        LogEventType.RestSkipped    => "跳过休息",
        LogEventType.RestCompleted  => "休息完成",
        LogEventType.WarningShown   => "预警提示",
        LogEventType.PausedByUser   => "用户暂停",
        LogEventType.ResumedByUser  => "用户继续",
        LogEventType.IdleReset      => "空闲重置",
        LogEventType.AppStarted     => "程序启动",
        LogEventType.AppExited      => "程序退出",
        _                           => "未知事件"
    };
}

public static class LogService
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ProtectEye", "logs.json");

    private static List<LogEntry> _cache = new();

    public static void Log(LogEventType eventType, string? note = null)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            EventType = eventType,
            Note = note
        };
        _cache.Add(entry);
        Persist(entry);
    }

    public static List<LogEntry> LoadAll()
    {
        try
        {
            if (!File.Exists(LogPath)) return new List<LogEntry>();
            var json = File.ReadAllText(LogPath);
            return JsonSerializer.Deserialize<List<LogEntry>>(json) ?? new List<LogEntry>();
        }
        catch { return new List<LogEntry>(); }
    }

    private static void Persist(LogEntry entry)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            var all = LoadAll();
            all.Add(entry);
            // 自动清理过期日志
            int maxDays = ConfigManager.Current.MaxLogDays;
            if (maxDays > 0)
            {
                var limitDate = DateTime.Now.Date.AddDays(-maxDays);
                all.RemoveAll(x => x.Timestamp.Date < limitDate);
            }
            File.WriteAllText(LogPath, JsonSerializer.Serialize(all, new JsonSerializerOptions { WriteIndented = false }));
        }
        catch { }
    }

    public static void Clear()
    {
        try
        {
            if (File.Exists(LogPath)) File.Delete(LogPath);
            _cache.Clear();
        }
        catch { }
    }

    /// <summary>
    /// 根据日志计算今日统计摘要
    /// </summary>
    public static (TimeSpan TotalWork, TimeSpan TotalRest, int RestCount, int SkipCount, int Over1HourCount) GetTodaySummary()
    {
        var today = DateTime.Today;
        var logs = LoadAll().FindAll(e => e.Timestamp.Date == today);
        return GetDaySummary(logs);
    }

    public static (TimeSpan TotalWork, TimeSpan TotalRest, int RestCount, int SkipCount, int Over1HourCount) GetDaySummary(List<LogEntry> logs)
    {
        TimeSpan totalWork = TimeSpan.Zero;
        TimeSpan totalRest = TimeSpan.Zero;
        int restCount = 0, skipCount = 0;
        int over1HourCount = 0;

        DateTime? workStart = null;
        DateTime? restStart = null;

        void FinishWorkSession(DateTime endTime)
        {
            if (workStart.HasValue)
            {
                var diff = endTime - workStart.Value;
                totalWork += diff;
                if (diff.TotalHours >= 1.0) over1HourCount++;
                workStart = null;
            }
        }

        foreach (var e in logs)
        {
            switch (e.EventType)
            {
                case LogEventType.AppStarted:
                case LogEventType.WorkStarted:
                case LogEventType.ResumedByUser:
                    if (restStart.HasValue) { totalRest += e.Timestamp - restStart.Value; restStart = null; }
                    workStart = e.Timestamp;
                    break;
                case LogEventType.RestStarted:
                    FinishWorkSession(e.Timestamp);
                    restStart = e.Timestamp;
                    restCount++;
                    break;
                case LogEventType.RestSkipped:
                    skipCount++;
                    break;
                case LogEventType.RestCompleted:
                    if (restStart.HasValue) { totalRest += e.Timestamp - restStart.Value; restStart = null; }
                    workStart = e.Timestamp;
                    break;
                case LogEventType.PausedByUser:
                    FinishWorkSession(e.Timestamp);
                    break;
            }
        }

        if (workStart.HasValue)
        {
            var diff = DateTime.Now - workStart.Value;
            totalWork += diff;
            if (diff.TotalHours >= 1.0) over1HourCount++;
        }
        if (restStart.HasValue) totalRest += DateTime.Now - restStart.Value;

        return (totalWork, totalRest, restCount, skipCount, over1HourCount);
    }
}
