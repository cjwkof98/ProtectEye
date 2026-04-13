using System;
using System.Windows;
using System.Windows.Threading;

namespace ProtectEye.Windows;

public partial class MicroBreakWindow : Window
{
    private DispatcherTimer _timer;
    private int _seconds = 20;

    public MicroBreakWindow()
    {
        InitializeComponent();
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += _timer_Tick;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // 居中靠上显示
        var workArea = SystemParameters.WorkArea;
        this.Left = workArea.Left + (workArea.Width - this.Width) / 2;
        this.Top = workArea.Top + 40; // 离顶端 40px

        _timer.Start();
    }

    private void _timer_Tick(object? sender, EventArgs e)
    {
        _seconds--;
        if (_seconds <= 0)
        {
            _timer.Stop();
            Close();
            return;
        }
        TxtSeconds.Text = _seconds.ToString();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _timer.Stop();
    }
}
