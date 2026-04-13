using System;
using System.Linq;
using System.Windows;
using ProtectEye.Services;

namespace ProtectEye.Windows;

public partial class LogWindow : Window
{
    public LogWindow()
    {
        InitializeComponent();
        Refresh();
    }

    private void Refresh()
    {
        var logs = LogService.LoadAll();

        // 填充日志列表（最新的在最下面）
        LogList.ItemsSource = logs;
        TxtLogCount.Text = $"共 {logs.Count} 条记录";
        TxtDateRange.Text = logs.Count > 0
            ? $"{logs.First().Timestamp:yyyy-MM-dd} ~ {logs.Last().Timestamp:MM-dd}  共 {logs.Count} 条"
            : "暂无记录";

        // 滚动到底部
        Dispatcher.InvokeAsync(() => ScrollLog.ScrollToBottom(),
            System.Windows.Threading.DispatcherPriority.Loaded);

        // 今日统计
        var (work, rest, restCnt, skipCnt, over1HourCnt) = LogService.GetTodaySummary();
        TxtTotalWork.Text = FormatSpan(work);
        TxtTotalRest.Text = FormatSpan(rest);
        TxtRestCount.Text = restCnt.ToString();
        TxtSkipCount.Text = skipCnt.ToString(); // Or over1HourCnt depending on UI, leaving as skipCnt for LogWindow
    }

    private static string FormatSpan(TimeSpan ts)
    {
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        if (ts.TotalMinutes >= 1)
            return $"{ts.Minutes}m {ts.Seconds}s";
        return $"{ts.Seconds}s";
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e) => Refresh();

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        var result = System.Windows.MessageBox.Show(
            "确定要清空所有运行日志吗？此操作不可撤销。",
            "清空日志",
            System.Windows.MessageBoxButton.OKCancel,
            System.Windows.MessageBoxImage.Question);

        if (result == System.Windows.MessageBoxResult.OK)
        {
            LogService.Clear();
            Refresh();
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}
