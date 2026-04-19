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

    private static readonly string[] _tips = new[]
    {
        "请将视线从屏幕移开，眺望 6 米远处",
        "请用力眨眼 5 次，保持眼球湿润",
        "请闭目养神，顺时针/逆时针转动眼球",
        "请起立伸个懒腰，稍微活动一下身体",
        "请深呼吸，放松紧绷的肩颈肌肉"
    };

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // 随机显示一条建议
        var random = new Random();
        TxtTip.Text = _tips[random.Next(_tips.Length)];

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
