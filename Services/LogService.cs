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

    public string IconColorHex => EventType switch
    {
        LogEventType.WorkStarted    => "#0D9488",
        LogEventType.RestStarted    => "#EAB308",
        LogEventType.RestSkipped    => "#EF4444",
        LogEventType.RestCompleted  => "#22C55E",
        LogEventType.WarningShown   => "#F59E0B",
        LogEventType.PausedByUser   => "#9CA3AF",
        LogEventType.ResumedByUser  => "#0D9488",
        LogEventType.IdleReset      => "#3B82F6",
        LogEventType.AppStarted     => "#A855F7",
        LogEventType.AppExited      => "#6B7280",
        _                           => "#9CA3AF"
    };

    public string IconPathData => EventType switch
    {
        LogEventType.WorkStarted    => "M12 21.5C5.37 21.5 1.5 14.93 1.5 12s3.87-9.5 10.5-9.5 10.5 6.57 10.5 9.5-3.87 9.5-10.5 9.5z M12 16.5a4.5 4.5 0 100-9 4.5 4.5 0 000 9z M12 14a2 2 0 100-4 2 2 0 000 4z",
        LogEventType.RestStarted    => "M12 22C6.477 22 2 17.523 2 12S6.477 2 12 2s10 4.477 10 10-4.477 10-10 10zM12 7v5l3 3",
        LogEventType.RestSkipped    => "M12 22C6.477 22 2 17.523 2 12S6.477 2 12 2s10 4.477 10 10-4.477 10-10 10zm-3-13l6 6m0-6l-6 6",
        LogEventType.RestCompleted  => "M12 22C6.477 22 2 17.523 2 12S6.477 2 12 2s10 4.477 10 10-4.477 10-10 10zm-5-10l3 3 7-7",
        LogEventType.WarningShown   => "M12 22C6.477 22 2 17.523 2 12S6.477 2 12 2s10 4.477 10 10-4.477 10-10 10zm0-15v6m0 4h.01",
        LogEventType.PausedByUser   => "M6 4h4v16H6z M14 4h4v16h-4z",
        LogEventType.ResumedByUser  => "M8 5v14l11-7z",
        LogEventType.IdleReset      => "M22 12c0 5.52-4.48 10-10 10S2 17.52 2 12 6.48 2 12 2c2.4 0 4.6.85 6.32 2.27L20 6h-5.5",
        LogEventType.AppStarted     => "M13 3v7h6l-8 11v-7H5l8-11z",
        LogEventType.AppExited      => "M10.09 15.59L11.5 17l5-5-5-5-1.41 1.41L12.67 11H3v2h9.67l-2.58 2.59zM19 3H5c-1.11 0-2 .9-2 2v4h2V5h14v14H5v-4H3v4c0 1.1.89 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2z",
        _                           => "M12 22C6.477 22 2 17.523 2 12S6.477 2 12 2s10 4.477 10 10-4.477 10-10 10z"
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

    public static (TimeSpan TotalWork, TimeSpan TotalRest, int RestCount, int SkipCount, int Over1HourCount) GetDaySummary(List<LogEntry> logs, DateTime? targetDate = null)
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
                    if (!workStart.HasValue) workStart = e.Timestamp; // 防止被意外覆盖
                    break;
                case LogEventType.RestStarted:
                    FinishWorkSession(e.Timestamp);
                    restStart = e.Timestamp;
                    restCount++;
                    break;
                case LogEventType.RestSkipped:
                    skipCount++;
                    if (restStart.HasValue)
                    {
                        if (restCount > 0) restCount--;
                        restStart = null;
                    }
                    if (!workStart.HasValue) workStart = e.Timestamp;
                    break;
                case LogEventType.RestCompleted:
                    if (restStart.HasValue) { totalRest += e.Timestamp - restStart.Value; restStart = null; }
                    if (!workStart.HasValue) workStart = e.Timestamp;
                    break;
                case LogEventType.PausedByUser:
                case LogEventType.AppExited:
                case LogEventType.IdleReset:
                    FinishWorkSession(e.Timestamp);
                    if (restStart.HasValue) { totalRest += e.Timestamp - restStart.Value; restStart = null; }
                    break;
            }
        }

        DateTime nowOrEnd = DateTime.Now;
        if (targetDate.HasValue && targetDate.Value.Date < DateTime.Today)
        {
            nowOrEnd = targetDate.Value.Date.AddDays(1).AddTicks(-1);
        }

        if (workStart.HasValue)
        {
            var diff = nowOrEnd - workStart.Value;
            if (diff.TotalSeconds > 0)
            {
                totalWork += diff;
                if (diff.TotalHours >= 1.0) over1HourCount++;
            }
        }
        if (restStart.HasValue) 
        {
            var diff = nowOrEnd - restStart.Value;
            if (diff.TotalSeconds > 0) totalRest += diff;
        }

        return (totalWork, totalRest, restCount, skipCount, over1HourCount);
    }

    public static (TimeSpan TotalWork, TimeSpan TotalRest, int RestCount, int SkipCount, int Over1HourCount) GetWeekSummary(DateTime weekMonday)
    {
        var all = LoadAll();
        TimeSpan tw = TimeSpan.Zero;
        TimeSpan tr = TimeSpan.Zero;
        int rc = 0, sc = 0, ohc = 0;
        for (int i = 0; i < 7; i++)
        {
            var day = weekMonday.Date.AddDays(i);
            var logs = all.Where(e => e.Timestamp.Date == day).ToList();
            var (w, r, c, s, o) = GetDaySummary(logs, day);
            tw += w;
            tr += r;
            rc += c;
            sc += s;
            ohc += o;
        }
        return (tw, tr, rc, sc, ohc);
    }
}
