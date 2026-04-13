using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ProtectEye.Services;
using WpfColor = System.Windows.Media.Color;
using WpfColorConv = System.Windows.Media.ColorConverter;
using WpfFontFamily = System.Windows.Media.FontFamily;
using WpfSolidBrush = System.Windows.Media.SolidColorBrush;
using WpfBrushes = System.Windows.Media.Brushes;
using Rectangle = System.Windows.Shapes.Rectangle;
using Ellipse = System.Windows.Shapes.Ellipse;


namespace ProtectEye;

public partial class MainWindow : Window
{
    private bool _chartShowToday = true;

    public MainWindow()
    {
        InitializeComponent();
        MainTab.SelectedIndex = 0;
        RefreshHome();
    }

    // ─── Tab 切换 ─────────────────────────────────────
    private void MainTab_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PanelHome == null) return;
        int idx = MainTab.SelectedIndex;
        PanelHome.Visibility     = idx == 0 ? Visibility.Visible : Visibility.Collapsed;
        PanelLog.Visibility      = idx == 1 ? Visibility.Visible : Visibility.Collapsed;
        PanelSettings.Visibility = idx == 2 ? Visibility.Visible : Visibility.Collapsed;

        if (idx == 0) RefreshHome();
        if (idx == 1) RefreshLog();
        if (idx == 2) LoadSettings();
    }

    // ─── 首页 ─────────────────────────────────────────
    private void RefreshHome()
    {
        var (work, rest, restCnt, skipCnt, over1HourCount) = LogService.GetTodaySummary();
        HomeTxtWork.Text    = FormatSpan(work);
        HomeTxtRest.Text    = FormatSpan(rest);
        HomeTxtRestCnt.Text = restCnt.ToString();
        HomeTxtSkip.Text    = skipCnt.ToString();

        var cfg = ConfigManager.Current;
        HomeCfgWork.Text = cfg.WorkIntervalMinutes.ToString();
        HomeCfgRest.Text = cfg.RestDurationMinutes.ToString();
        HomeCfgWarn.Text = cfg.WarningDurationMinutes.ToString();
        HomeCfgIdle.Text = cfg.IdleResetThresholdMinutes.ToString();

        // 功能状态标签
        UpdateStatusTag(TagDnd, TagDndText, DotDnd, "免打扰", cfg.EnableDND);
        UpdateStatusTag(TagStartup, TagStartupText, DotStartup, "开机启动", StartupService.IsStartupEnabled());
    }

    private void UpdateStatusTag(Border border, TextBlock text, Ellipse dot, string label, bool isOn)
    {
        text.Text = $"{label} {(isOn ? "ON" : "OFF")}";
        if (isOn)
        {
            border.Background = (WpfSolidBrush)FindResource("Theme.ControlBg");
            border.BorderBrush = (WpfSolidBrush)FindResource("Theme.Primary");
            text.Foreground = (WpfSolidBrush)FindResource("Theme.Primary");
            dot.Fill = (WpfSolidBrush)FindResource("Theme.Primary");
            border.Opacity = 1.0;
        }
        else
        {
            border.Background = WpfBrushes.Transparent;
            border.BorderBrush = (WpfSolidBrush)FindResource("Theme.Border");
            text.Foreground = (WpfSolidBrush)FindResource("Theme.TextMuted");
            dot.Fill = (WpfSolidBrush)FindResource("Theme.TextMuted");
            border.Opacity = 0.7;
        }
    }

    // ─── 设置 ─────────────────────────────────────────
    private void LoadSettings()
    {
        var config = ConfigManager.Current;
        InpWorkInterval.Text    = config.WorkIntervalMinutes.ToString();
        InpRestDuration.Text    = config.RestDurationMinutes.ToString();
        InpWarningDuration.Text = config.WarningDurationMinutes.ToString();
        InpIdle.Text            = config.IdleResetThresholdMinutes.ToString();
        InpMaxLogDays.Text      = config.MaxLogDays.ToString();
        ChkDnd.IsChecked        = config.EnableDND;
        ChkMicroBreak.IsChecked = config.EnableMicroBreak;
        ChkHealthTips.IsChecked = config.ShowHealthTips;
        CmbBgStyle.SelectedIndex = config.BackgroundStyle;
        CmbAppTheme.SelectedIndex = config.AppTheme;
        ChkStartup.IsChecked    = StartupService.IsStartupEnabled();
        InpShortcut.Text        = config.ImmediateRestShortcut;
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        var config = ConfigManager.Current;
        if (int.TryParse(InpWorkInterval.Text,    out int work)) config.WorkIntervalMinutes    = work;
        if (int.TryParse(InpRestDuration.Text,    out int rest)) config.RestDurationMinutes    = rest;
        if (int.TryParse(InpWarningDuration.Text, out int warn)) config.WarningDurationMinutes = warn;
        if (int.TryParse(InpIdle.Text,            out int idle)) config.IdleResetThresholdMinutes = idle;
        if (int.TryParse(InpMaxLogDays.Text,      out int days)) config.MaxLogDays = days;
        config.EnableDND       = ChkDnd.IsChecked ?? true;
        config.EnableMicroBreak = ChMicroBreakIsChecked();
        config.ShowHealthTips  = ChHealthTipsIsChecked();
        config.BackgroundStyle = CmbBgStyle.SelectedIndex;
        config.AppTheme        = CmbAppTheme.SelectedIndex;
        config.ImmediateRestShortcut = InpShortcut.Text?.Trim() ?? string.Empty;
        ConfigManager.Save();
        StartupService.SetStartup(ChkStartup.IsChecked ?? false);

        if (System.Windows.Application.Current is App app)
        {
            app.RegisterGlobalHotKey();
            app.ChangeTheme(config.AppTheme);
            app.NotifyConfigChanged();
        }
        
        TxtSaveSuccess.Visibility = Visibility.Visible;
        await System.Threading.Tasks.Task.Delay(3000);
        TxtSaveSuccess.Visibility = Visibility.Collapsed;
    }

    private bool ChMicroBreakIsChecked() => ChkMicroBreak.IsChecked ?? true;
    private bool ChHealthTipsIsChecked() => ChkHealthTips.IsChecked ?? true;

    private void BtnRestNow_Click(object sender, RoutedEventArgs e)
    {
        if (System.Windows.Application.Current is App app) app.TriggerRestNow();
        Close();
    }

    // ─── 日志与图表 ───────────────────────────────────
    private void RefreshLog()
    {
        var logs = LogService.LoadAll();
        LogList.ItemsSource = logs.AsEnumerable().Reverse().Take(300).Reverse().ToList();
        Dispatcher.InvokeAsync(() => ScrollLog.ScrollToBottom(),
            System.Windows.Threading.DispatcherPriority.Loaded);

        var (work, rest, restCnt, skipCnt, over1HourCnt) = LogService.GetTodaySummary();
        TxtTotalWork.Text  = FormatSpan(work);
        TxtTotalRest.Text  = FormatSpan(rest);
        TxtRestCount.Text  = restCnt.ToString();
        TxtSkipCount.Text  = over1HourCnt.ToString(); // Reuse skip block for over1HourCount in log tab
        DrawChart();
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e) => RefreshLog();
    private void BtnOpenLogFolder_Click(object sender, RoutedEventArgs e)
    {
        string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ProtectEye");
        if (System.IO.Directory.Exists(dir))
        {
            System.Diagnostics.Process.Start("explorer.exe", dir);
        }
    }
    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        var r = System.Windows.MessageBox.Show("确定要清空所有运行日志吗？",
            "清空日志", System.Windows.MessageBoxButton.OKCancel,
            System.Windows.MessageBoxImage.Question);
        if (r == System.Windows.MessageBoxResult.OK) { LogService.Clear(); RefreshLog(); }
    }

    private WpfColor Hex(string h) => (WpfColor)WpfColorConv.ConvertFromString(h);

    private void BtnChartToday_Click(object sender, RoutedEventArgs e)
    {
        _chartShowToday = true;
        BtnToday.Background = new WpfSolidBrush(Hex("#0D9488")); BtnToday.Foreground = WpfBrushes.White;
        BtnWeek.Background  = new WpfSolidBrush(Hex("#1A1A1A")); BtnWeek.Foreground  = new WpfSolidBrush(Hex("#6B7280"));
        DrawChart();
    }
    private void BtnChartWeek_Click(object sender, RoutedEventArgs e)
    {
        _chartShowToday = false;
        BtnWeek.Background  = new WpfSolidBrush(Hex("#0D9488")); BtnWeek.Foreground  = WpfBrushes.White;
        BtnToday.Background = new WpfSolidBrush(Hex("#1A1A1A")); BtnToday.Foreground = new WpfSolidBrush(Hex("#6B7280"));
        DrawChart();
    }

    private void DrawChart()
    {
        ChartCanvas.Children.Clear(); ChartXAxis.Children.Clear();
        if (_chartShowToday) DrawTodayChart(); else DrawWeekChart();
    }

    private void DrawTodayChart()
    {
        var (work, rest, _, _, _) = LogService.GetTodaySummary();
        double max = Math.Max(work.TotalMinutes + rest.TotalMinutes, 60);
        double lW = 44, aW = 390;
        DrawBar(ChartCanvas, lW, 10,  aW, 36, work.TotalMinutes / max, "#374151", FormatSpan(work), "工作");
        DrawBar(ChartCanvas, lW, 60,  aW, 36, rest.TotalMinutes / max, "#0D9488", FormatSpan(rest), "休息");
    }

    private void DrawBar(System.Windows.Controls.Canvas cv, double x, double y,
        double mW, double h, double ratio, string col, string valLbl, string rowLbl)
    {
        ratio = Math.Max(0, Math.Min(1, ratio));
        double fw = mW * ratio;

        var track = new Rectangle { Width = mW, Height = h, Fill = new WpfSolidBrush(Hex("#1A1A1A")), RadiusX = 4, RadiusY = 4 };
        System.Windows.Controls.Canvas.SetLeft(track, x); System.Windows.Controls.Canvas.SetTop(track, y); cv.Children.Add(track);

        if (fw > 0)
        {
            var fill = new Rectangle { Width = Math.Max(fw, 8), Height = h, Fill = new WpfSolidBrush(Hex(col)), RadiusX = 4, RadiusY = 4 };
            System.Windows.Controls.Canvas.SetLeft(fill, x); System.Windows.Controls.Canvas.SetTop(fill, y); cv.Children.Add(fill);
        }

        var lbl = new TextBlock { Text = rowLbl, FontSize = 10, FontFamily = new WpfFontFamily("Segoe UI"),
            Foreground = new WpfSolidBrush(Hex("#6B7280")), Width = 40, TextAlignment = TextAlignment.Right };
        System.Windows.Controls.Canvas.SetLeft(lbl, x - 44); System.Windows.Controls.Canvas.SetTop(lbl, y + h / 2 - 7); cv.Children.Add(lbl);

        var vl = new TextBlock { Text = valLbl, FontSize = 10, FontFamily = new WpfFontFamily("Segoe UI"),
            Foreground = new WpfSolidBrush(Hex("#9CA3AF")) };
        System.Windows.Controls.Canvas.SetLeft(vl, x + fw + 6); System.Windows.Controls.Canvas.SetTop(vl, y + h / 2 - 7); cv.Children.Add(vl);
    }

    private void DrawWeekChart()
    {
        double cH = 130; double bW = 28; double gap = 36; double sx = 24; double mH = cH - 24;
        var today = DateTime.Today;
        int dfm = ((int)today.DayOfWeek + 6) % 7;
        var mon = today.AddDays(-dfm);
        var all = LogService.LoadAll();
        double gMax = 1;
        var data = new List<(TimeSpan w, TimeSpan r, string lbl)>();
        for (int i = 0; i < 7; i++)
        {
            var day = mon.AddDays(i);
            var dl = all.Where(e => e.Timestamp.Date == day).ToList();
            var (w, r, _, _, _) = LogService.GetDaySummary(dl);
            gMax = Math.Max(gMax, w.TotalMinutes + r.TotalMinutes);
            string[] n = { "一", "二", "三", "四", "五", "六", "日" };
            data.Add((w, r, day == today ? "今" : n[i]));
        }
        for (int i = 0; i < data.Count; i++)
        {
            var (w, r, lbl) = data[i];
            double x = sx + i * (bW + gap);
            double wH = mH * (w.TotalMinutes / gMax);
            double rH = mH * (r.TotalMinutes / gMax);
            double sh = wH + rH;
            if (wH > 0) { var rect = new Rectangle { Width = bW, Height = wH, Fill = new WpfSolidBrush(Hex("#374151")), RadiusX = 3, RadiusY = 3 }; System.Windows.Controls.Canvas.SetLeft(rect, x); System.Windows.Controls.Canvas.SetTop(rect, cH - 20 - wH); ChartCanvas.Children.Add(rect); }
            if (rH > 0) { var rect = new Rectangle { Width = bW, Height = rH, Fill = new WpfSolidBrush(Hex("#0D9488")), RadiusX = 3, RadiusY = 3 }; System.Windows.Controls.Canvas.SetLeft(rect, x); System.Windows.Controls.Canvas.SetTop(rect, cH - 20 - sh);  ChartCanvas.Children.Add(rect); }
            if (wH == 0 && rH == 0) { var rect = new Rectangle { Width = bW, Height = 3, Fill = new WpfSolidBrush(Hex("#1E1E1E")), RadiusX = 2, RadiusY = 2 }; System.Windows.Controls.Canvas.SetLeft(rect, x); System.Windows.Controls.Canvas.SetTop(rect, cH - 23); ChartCanvas.Children.Add(rect); }
            var t = new TextBlock { Text = lbl, FontSize = 10, FontFamily = new WpfFontFamily("Segoe UI"),
                Foreground = new WpfSolidBrush(Hex(lbl == "今" ? "#0D9488" : "#4B5563")) };
            System.Windows.Controls.Canvas.SetLeft(t, x + bW / 2 - 5); System.Windows.Controls.Canvas.SetTop(t, 0); ChartXAxis.Children.Add(t);
        }
    }

    private static string FormatSpan(TimeSpan ts)
    {
        if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}h{ts.Minutes}m";
        if (ts.TotalMinutes >= 1) return $"{ts.Minutes}m";
        return ts.TotalSeconds > 0 ? $"{(int)ts.TotalSeconds}s" : "—";
    }
}