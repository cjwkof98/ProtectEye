using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ProtectEye.Services;

namespace ProtectEye.Windows;

public partial class TrayMenuWindow : Window
{
    private readonly Action<string> _onItemClick;
    private readonly TimerService _timerService;
    private bool _isClosing = false;

    public TrayMenuWindow(TimerService timerService, Action<string> onItemClick)
    {
        InitializeComponent();
        _timerService = timerService;
        _onItemClick = onItemClick;
        
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        var greenBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(34, 197, 94)); // Tailwind Green 500
        var redBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68));   // Tailwind Red 500
        var whiteBrush = new SolidColorBrush(System.Windows.Media.Colors.White);

        if (_timerService.CurrentState == AppState.Paused)
        {
            StateText.Text = "已暂停运行";
            PauseResumeBtn.Content = "▶  继续运行";
            HeaderBanner.Background = redBrush;
            HeaderIcon.Fill = whiteBrush;
            StateText.Foreground = whiteBrush;
        }
        else if (_timerService.CurrentState == AppState.Resting)
        {
            StateText.Text = "正在休息中";
            PauseResumeBtn.Content = "⏸  暂停运行";
            HeaderBanner.Background = greenBrush;
            HeaderIcon.Fill = whiteBrush;
            StateText.Foreground = whiteBrush;
        }
        else
        {
            StateText.Text = "正在运行中";
            PauseResumeBtn.Content = "⏸  暂停运行";
            HeaderBanner.Background = greenBrush;
            HeaderIcon.Fill = whiteBrush;
            StateText.Foreground = whiteBrush;
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try 
        {
            // Get physical mouse cursor position
            var mousePos = Win32Api.GetCursorPosition();

            // Convert physical pixels to logical units (DPI awareness)
            var dpi = VisualTreeHelper.GetDpi(this);
            
            double logicalX = mousePos.X / dpi.DpiScaleX;
            double logicalY = mousePos.Y / dpi.DpiScaleY;

            // Positioning logic
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;

            double left = logicalX - Width / 2;
            double top = logicalY - Height - 5; // Offset slightly above cursor

            // Ensure window stays within screen bounds
            if (left + Width > screenWidth) left = screenWidth - Width - 5;
            if (left < 5) left = 5;
            if (top + Height > screenHeight) top = screenHeight - Height - 5;
            if (top < 5) top = 5;

            Left = left;
            Top = top;
        }
        catch (Exception)
        {
            // Fallback to screen corner if positioning fails for some reason
            Left = SystemParameters.PrimaryScreenWidth - Width - 10;
            Top = SystemParameters.PrimaryScreenHeight - Height - 50;
        }

        Activate();
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        if (!_isClosing) 
        {
            SafeClose();
        }
    }

    private void MenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string tag)
        {
            // Execute action asynchronously on the global dispatcher
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => 
            {
                _onItemClick?.Invoke(tag);
            }));

            // Close menu window after initiating the action
            SafeClose();
        }
    }

    private void SafeClose()
    {
        if (_isClosing) return;
        _isClosing = true;
        
        try 
        {
            this.Close();
        }
        catch (Exception)
        {
            // Fail-safe
        }
    }
}
