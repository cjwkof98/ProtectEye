using System;
using System.Windows;

namespace ProtectEye.Windows;

public partial class WarningWindow : Window
{
    private Action _onSkip;
    private Action _onRestNow;
    
    public WarningWindow(Action onSkip, Action onRestNow)
    {
        InitializeComponent();
        _onSkip = onSkip;
        _onRestNow = onRestNow;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // 自动贴边右下角
        var workArea = SystemParameters.WorkArea;
        this.Left = workArea.Right - this.Width - 20;
        this.Top = workArea.Bottom - this.Height - 20;
    }
    
    public void UpdateTime(string timeStr)
    {
        TxtCountdown.Text = $"距离休息还有 {timeStr}";
    }

    private void BtnSkip_Click(object sender, RoutedEventArgs e)
    {
        _onSkip?.Invoke();
        this.Hide();
    }

    private void BtnRestNow_Click(object sender, RoutedEventArgs e)
    {
        _onRestNow?.Invoke();
        this.Hide();
    }
}
