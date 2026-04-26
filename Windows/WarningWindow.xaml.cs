using System;
using System.Windows;

namespace ProtectEye.Windows;

public partial class WarningWindow : Window
{
    private Action? _onSkip;
    private Action? _onRestNow;
    private Action<int>? _onPostpone;
    
    public WarningWindow(Action? onSkip, Action? onRestNow, Action<int>? onPostpone)
    {
        InitializeComponent();
        _onSkip = onSkip;
        _onRestNow = onRestNow;
        _onPostpone = onPostpone;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Reposition();
    }

    public void Reposition()
    {
        // 自动贴边右下角
        // 使用 SizeToContent 时，如果窗口从未显示过，ActualWidth 可能不准
        // 但在 Loaded 之后或 Show 之后调用通常是安全的
        if (this.ActualWidth == 0) this.UpdateLayout(); 
        
        var workArea = SystemParameters.WorkArea;
        double width = this.ActualWidth > 0 ? this.ActualWidth : 320; // 回退值
        double height = this.ActualHeight > 0 ? this.ActualHeight : 180;

        this.Left = workArea.Right - width - 20;
        this.Top = workArea.Bottom - height - 20;
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

    private void BtnPostpone3_Click(object sender, RoutedEventArgs e)
    {
        _onPostpone?.Invoke(3);
        this.Hide();
    }

    private void BtnPostpone5_Click(object sender, RoutedEventArgs e)
    {
        _onPostpone?.Invoke(5);
        this.Hide();
    }

    private void BtnPostpone10_Click(object sender, RoutedEventArgs e)
    {
        _onPostpone?.Invoke(10);
        this.Hide();
    }
}
