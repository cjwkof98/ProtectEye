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
    private bool _chartShowWork = true;
    private bool _hasShownHealthAlert = false;

    public MainWindow()
    {
        InitializeComponent();
        MainTab.SelectedIndex = 0;
        RefreshHome();

        if (System.Windows.Application.Current is App app && app.TimerService != null)
        {
            app.TimerService.Tick += OnTimerTick;
            app.TimerService.StateChanged += OnTimerStateChanged;
            UpdatePauseState(app.TimerService.CurrentState);
        }
        this.Closed += MainWindow_Closed;
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        if (System.Windows.Application.Current is App app && app.TimerService != null)
        {
            app.TimerService.Tick -= OnTimerTick;
            app.TimerService.StateChanged -= OnTimerStateChanged;
        }
    }

    private void OnTimerTick(string timeStr)
    {
        if (System.Windows.Application.Current is App app && app.TimerService != null)
        {
            Dispatcher.Invoke(() =>
            {
                var ts = app.TimerService;
                if (ts.CurrentState == AppState.Working)
                {
                    int macroSec = ts.SecondsRemaining;
                    int microSec = ts.SecondsToNextMicroBreak;

                    TxtNextMacro.Text = TimeSpan.FromSeconds(macroSec).ToString(@"mm\:ss");
                    TxtNextMicro.Text = microSec >= 0 ? TimeSpan.FromSeconds(microSec).ToString(@"mm\:ss") : "—:—";
                }
                else
                {
                    TxtNextMacro.Text = "—:—";
                    TxtNextMicro.Text = "—:—";
                }
            });
        }
    }

    private void OnTimerStateChanged(AppState oldState, AppState newState)
    {
        Dispatcher.Invoke(() => UpdatePauseState(newState));
    }

    private void UpdatePauseState(AppState state)
    {
        if (state == AppState.Paused)
        {
            TxtProtectStatus.Text = "保护已暂停";
            TxtProtectStatus.Foreground = new WpfSolidBrush(Hex("#9CA3AF")); // Gray
            StatusDot.Fill = new WpfSolidBrush(Hex("#9CA3AF"));
            StatusRing.Stroke = new WpfSolidBrush(Hex("#9CA3AF"));
            StatusDot.Visibility = Visibility.Visible;
            TxtBtnPause.Text = "恢复保护";
            IconPause.Data = System.Windows.Media.Geometry.Parse("M8 5v14l11-7z"); // Play icon
            TxtNextMacro.Text = "已暂停";
            TxtNextMicro.Text = "已暂停";
            // 已暂停 — 恢复是好事，用普通样式即可
            BtnTogglePause.Style = (Style)FindResource("GhostBtn");
            BtnTogglePause.ToolTip = "点击恢复护眼保护";
        }
        else
        {
            TxtProtectStatus.Text = "您的眼睛正在被保护";
            TxtProtectStatus.SetResourceReference(TextBlock.ForegroundProperty, "Theme.Primary");
            StatusDot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "Theme.Primary");
            StatusRing.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "Theme.Primary");
            StatusDot.Visibility = Visibility.Visible;
            TxtBtnPause.Text = "暂停保护";
            IconPause.Data = System.Windows.Media.Geometry.Parse("M6 4h4v16H6z M14 4h4v16h-4z"); // Pause icon
            // 保护中 — hover 变红警示，即将关闭保护
            BtnTogglePause.Style = (Style)FindResource("PauseDangerBtn");
            BtnTogglePause.ToolTip = "暂停后将停止所有护眼提醒，请谨慎使用";
        }
    }

    // ─── Tab 切换 ─────────────────────────────────────
    private void MainTab_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PanelHome == null) return;
        int idx = MainTab.SelectedIndex;
        PanelHome.Visibility     = idx == 0 ? Visibility.Visible : Visibility.Collapsed;
        PanelLog.Visibility      = idx == 1 ? Visibility.Visible : Visibility.Collapsed;
        PanelRunLog.Visibility   = idx == 2 ? Visibility.Visible : Visibility.Collapsed;
        PanelSettings.Visibility = idx == 3 ? Visibility.Visible : Visibility.Collapsed;

        if (idx == 0) RefreshHome();
        if (idx == 1) RefreshAnalysis();
        if (idx == 2) RefreshRunLog();
        if (idx == 3) LoadSettings();
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
        ChkHealthAlert.IsChecked = config.EnableHealthScoreAlert;
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
        config.EnableHealthScoreAlert = ChkHealthAlert.IsChecked ?? true;
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

    private void BtnTogglePause_Click(object sender, RoutedEventArgs e)
    {
        if (System.Windows.Application.Current is App app && app.TimerService != null)
        {
            if (app.TimerService.CurrentState == AppState.Paused)
                app.TimerService.Resume();
            else
                app.TimerService.Pause();
        }
    }

    // ─── 日志与图表 ───────────────────────────────────
    private void RefreshAnalysis()
    {
        DateTime today = DateTime.Today;
        int diffMon = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
        DateTime thisWeekMon = today.AddDays(-diffMon);
        DateTime lastWeekMon = thisWeekMon.AddDays(-7);

        var thisWeek = LogService.GetWeekSummary(thisWeekMon);
        var lastWeek = LogService.GetWeekSummary(lastWeekMon);

        TxtWeekWork.Text = FormatSpan(thisWeek.TotalWork);
        TxtLastWeekWork.Text = FormatSpan(lastWeek.TotalWork);
        TxtWeekRest.Text = FormatSpan(thisWeek.TotalRest);
        TxtLastWeekRest.Text = FormatSpan(lastWeek.TotalRest);
        DrawChart();

        CalculateHealthScore(thisWeek);
    }

    private void CalculateHealthScore((TimeSpan TotalWork, TimeSpan TotalRest, int RestCount, int SkipCount, int Over1HourCount) weekData)
    {
        int score = 100;
        if (weekData.TotalWork.TotalSeconds == 0)
        {
            TxtHealthScore.Text = "—";
            TxtHealthStatus.Text = "暂无数据";
            TxtHealthStatus.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
            TxtHealthScore.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
            TxtHealthAdvice.Text = "本周尚无足够的数据进行用眼健康评估。";
            TxtOver1Hour.Text = "0 次";
            TxtSkipCount.Text = "0 次";
            return;
        }

        score -= weekData.Over1HourCount * 15;
        score -= weekData.SkipCount * 8;
        score += weekData.RestCount * 2; 
        score = Math.Clamp(score, 0, 100);

        TxtHealthScore.Text = score.ToString();
        TxtOver1Hour.Text = $"{weekData.Over1HourCount} 次";
        TxtSkipCount.Text = $"{weekData.SkipCount} 次";

        if (score >= 85)
        {
            TxtHealthStatus.Text = "状态极佳 (低风险)";
            TxtHealthStatus.SetResourceReference(TextBlock.ForegroundProperty, "Theme.Primary");
            TxtHealthScore.SetResourceReference(TextBlock.ForegroundProperty, "Theme.Primary");
            TxtHealthAdvice.Text = "优秀的用眼习惯！这显著降低了数字视疲劳 (DES) 发病率。建议继续保持目前的微休息与定时远眺频率。";
        }
        else if (score >= 60)
        {
            TxtHealthStatus.Text = "轻度疲劳 (需注意)";
            TxtHealthStatus.Foreground = new WpfSolidBrush(Hex("#F59E0B"));
            TxtHealthScore.Foreground = new WpfSolidBrush(Hex("#F59E0B"));
            TxtHealthAdvice.Text = "您的用眼习惯存在一定瑕疵，跳过休息或超长连续工作的次数偏多。医学研究表明这会增加睫状肌痉挛与干眼症风险。";
        }
        else
        {
            TxtHealthStatus.Text = "高危疲劳 (请立即改善)";
            TxtHealthStatus.Foreground = new WpfSolidBrush(Hex("#EF4444"));
            TxtHealthScore.Foreground = new WpfSolidBrush(Hex("#EF4444"));
            TxtHealthAdvice.Text = "检测到严重的超负荷用眼行为！持续无节制面对屏幕将对眼底与视力造成不可逆负担，请务必遵从应用弹窗建议。";
        }

        if (ConfigManager.Current.EnableHealthScoreAlert && score < ConfigManager.Current.HealthScoreAlertThreshold && !_hasShownHealthAlert)
        {
            _hasShownHealthAlert = true;
            Dispatcher.InvokeAsync(() => {
                System.Windows.MessageBox.Show(
                    "您的用眼健康指数已跌破预警阈值！\n\n长期超负荷、无视休息预警会对眼底视神经与泪膜造成极大伤害，极易引发严重干眼症与睫状肌痉挛。\n\n请您：\n1. 务必遵从休息弹窗，给眼睛放松的时间。\n2. 严格执行 20-20-20 微休息原则。\n\n保护眼睛从现在做起！\n(您可以在设置中关闭此强提醒)",
                    "用眼高危预警", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            });
        }
    }

    private void RefreshRunLog()
    {
        var logs = LogService.LoadAll();
        LogList.ItemsSource = logs.AsEnumerable().Reverse().Take(300).Reverse().ToList();
        Dispatcher.InvokeAsync(() => ScrollLog.ScrollToBottom(),
            System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e) => RefreshRunLog();
    private void BtnHealthAlertInfo_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show(
            "当系统计算您的“用眼健康指数”低于预警阈值 (默认60分) 时，在您打开应用面板或启动应用时，将会强制弹出一个警示窗口。\n\n该预警旨在强力纠正您的危险用眼习惯（如无视休息、长时间不闭眼等），预防视力严重损伤。",
            "健康评分强提醒说明", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }

    private void BtnCvsInfo_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show(
            "CVS (Computer Vision Syndrome, 计算机视觉综合征) 和 DES (Digital Eye Strain, 数字视疲劳) 是由于长时间面对电子屏幕导致的一系列眼部和视觉问题。\n\n本评估模型基于医学结论，核心惩罚机制为：\n1. 单次连续工作超过 1 小时未休息 (高危，大幅扣分)\n2. 预警要求休息时，强行跳过休息 (风险，中幅扣分)\n\n评分越高，说明您的用眼习惯越好，患干眼症或导致视力下降的概率越低。",
            "CVS/DES 评估模型说明", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }

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
        if (r == System.Windows.MessageBoxResult.OK) { LogService.Clear(); RefreshRunLog(); }
    }

    private WpfColor Hex(string h) => (WpfColor)WpfColorConv.ConvertFromString(h);

    private void BtnChartWork_Click(object sender, RoutedEventArgs e)
    {
        _chartShowWork = true;
        BtnChartWork.Background = new WpfSolidBrush(Hex("#0D9488")); BtnChartWork.Foreground = WpfBrushes.White;
        BtnChartRest.Background  = new WpfSolidBrush(Hex("#1A1A1A")); BtnChartRest.Foreground  = new WpfSolidBrush(Hex("#6B7280"));
        DrawChart();
    }
    private void BtnChartRest_Click(object sender, RoutedEventArgs e)
    {
        _chartShowWork = false;
        BtnChartRest.Background  = new WpfSolidBrush(Hex("#0D9488")); BtnChartRest.Foreground  = WpfBrushes.White;
        BtnChartWork.Background = new WpfSolidBrush(Hex("#1A1A1A")); BtnChartWork.Foreground = new WpfSolidBrush(Hex("#6B7280"));
        DrawChart();
    }

    private void DrawChart()
    {
        ChartCanvas.Children.Clear(); ChartXAxis.Children.Clear();
        DrawComparisonChart();
    }

    private void DrawComparisonChart()
    {
        double cH = 90; double gap = 55; double sx = 30; double mH = cH - 20;
        
        DateTime today = DateTime.Today;
        int diffMon = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
        DateTime thisWeekMon = today.AddDays(-diffMon);
        DateTime lastWeekMon = thisWeekMon.AddDays(-7);
        
        var all = LogService.LoadAll();
        var thisWeekData = new List<double>();
        var lastWeekData = new List<double>();
        double maxVal = 1;
        
        for(int i = 0; i < 7; i++)
        {
            var day1 = thisWeekMon.AddDays(i);
            var (w1, r1, _, _, _) = LogService.GetDaySummary(all.Where(e => e.Timestamp.Date == day1).ToList(), day1);
            double val1 = _chartShowWork ? w1.TotalMinutes : r1.TotalMinutes;
            thisWeekData.Add(val1);
            maxVal = Math.Max(maxVal, val1);
            
            var day2 = lastWeekMon.AddDays(i);
            var (w2, r2, _, _, _) = LogService.GetDaySummary(all.Where(e => e.Timestamp.Date == day2).ToList(), day2);
            double val2 = _chartShowWork ? w2.TotalMinutes : r2.TotalMinutes;
            lastWeekData.Add(val2);
            maxVal = Math.Max(maxVal, val2);
            
            string[] n = { "一", "二", "三", "四", "五", "六", "日" };
            string lbl = (day1 == today) ? "今" : n[i];
            
            double x = sx + i * gap;
            var t = new TextBlock { Text = lbl, FontSize = 10, FontFamily = new WpfFontFamily("Segoe UI"),
                Foreground = new WpfSolidBrush(Hex(lbl == "今" ? "#0D9488" : "#4B5563")) };
            System.Windows.Controls.Canvas.SetLeft(t, x - 5); System.Windows.Controls.Canvas.SetTop(t, 0); ChartXAxis.Children.Add(t);
        }
        
        maxVal = Math.Max(maxVal, 60); // At least scale to 60 mins
        maxVal *= 1.1; // Headroom

        // Last week line (dashed grey)
        DrawLineChart(lastWeekData, maxVal, sx, gap, cH, mH, "#4B5563", true);
        
        // This week line (solid primary)
        DrawLineChart(thisWeekData, maxVal, sx, gap, cH, mH, "#0D9488", false);
    }
    
    private void DrawLineChart(List<double> data, double maxVal, double sx, double gap, double cH, double mH, string hexColor, bool isDashed)
    {
        var pts = new System.Windows.Media.PointCollection();
        for (int i = 0; i < data.Count; i++)
        {
            double x = sx + i * gap;
            double y = cH - 15 - (mH * (data[i] / maxVal));
            pts.Add(new System.Windows.Point(x, y));
            
            var dot = new Ellipse { Width = 6, Height = 6, Fill = new WpfSolidBrush(Hex(hexColor)) };
            System.Windows.Controls.Canvas.SetLeft(dot, x - 3);
            System.Windows.Controls.Canvas.SetTop(dot, y - 3);
            ChartCanvas.Children.Add(dot);
        }
        
        var line = new System.Windows.Shapes.Polyline
        {
            Points = pts,
            Stroke = new WpfSolidBrush(Hex(hexColor)),
            StrokeThickness = 2
        };
        
        if (isDashed)
        {
            line.StrokeDashArray = new System.Windows.Media.DoubleCollection(new double[] { 3, 3 });
        }
        
        ChartCanvas.Children.Add(line);
    }

    private static string FormatSpan(TimeSpan ts)
    {
        if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}h{ts.Minutes}m";
        if (ts.TotalMinutes >= 1) return $"{ts.Minutes}m";
        return ts.TotalSeconds > 0 ? $"{(int)ts.TotalSeconds}s" : "—";
    }

    private void BtnPreviewWarning_Click(object sender, RoutedEventArgs e)
    {
        var win = new Windows.WarningWindow(null, null);
        win.UpdateTime("1:00");
        win.Show();
    }

    private void BtnPreviewMicro_Click(object sender, RoutedEventArgs e)
    {
        var win = new Windows.MicroBreakWindow();
        win.Show();
    }


}