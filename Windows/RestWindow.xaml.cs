using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using ProtectEye.Services;

namespace ProtectEye.Windows;

public partial class RestWindow : Window
{
    private Action _onSkip;
    private Action _onExtend;
    private Action<bool> _onPauseToggle;
    private bool _isPaused = false;
    private DispatcherTimer _clockTimer;
    private DispatcherTimer _poemTimer;
    
    public RestWindow(Action onSkip, Action onExtend, Action<bool> onPauseToggle)
    {
        InitializeComponent();
        _onSkip = onSkip;
        _onExtend = onExtend;
        _onPauseToggle = onPauseToggle;
        
        // 实时时钟
        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (s, e) => UpdateClock();
        _clockTimer.Start();

        // 诗词轮播时钟
        _poemTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _poemTimer.Tick += (s, e) => CyclePoem();
        _poemTimer.Start();

        this.Closed += (s, e) => 
        {
            _clockTimer.Stop();
            _poemTimer.Stop();
        };
        UpdateClock();
        
        // 加载用户界面配置
        try 
        {
            var config = ConfigManager.Current;
            
            if (config.BackgroundStyle == 0) // 极限护眼 (纯色)
            {
                MaskBorder.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, "Theme.RestBg");
                MaskBorder.Opacity = 1.0;
                BgImage.Visibility = Visibility.Collapsed;
            }
            else if (config.BackgroundStyle == 1) // 半透明
            {
                MaskBorder.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, "Theme.RestBg");
                MaskBorder.Opacity = 0.85;
                BgImage.Visibility = Visibility.Collapsed;
            }
            else // 背景模糊
            {
                MaskBorder.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, "Theme.RestBg");
                MaskBorder.Opacity = 0.6;
                BgImage.Visibility = Visibility.Visible;
            }
            
            // 随机显示一条诗词并开始轮播
            if (ProtectEye.Models.PoemsData.Quotes != null && ProtectEye.Models.PoemsData.Quotes.Length > 0)
            {
                var rand = new Random();
                TxtQuote.Text = ProtectEye.Models.PoemsData.Quotes[rand.Next(ProtectEye.Models.PoemsData.Quotes.Length)];
            }
            
            // 随机显示一条健康科普文字
            if (config.ShowHealthTips && config.HealthTips != null && config.HealthTips.Length > 0)
            {
                var rand = new Random();
                TxtHealthTip.Text = config.HealthTips[rand.Next(config.HealthTips.Length)];
                TxtHealthTip.Visibility = Visibility.Visible;
            }
            else
            {
                TxtHealthTip.Visibility = Visibility.Collapsed;
            }
        } 
        catch { }

        // 关键逻辑：获取所有存在的屏幕的“虚拟边框组合”，提前尺寸以实现截屏
        this.Left = SystemParameters.VirtualScreenLeft;
        this.Top = SystemParameters.VirtualScreenTop;
        this.Width = SystemParameters.VirtualScreenWidth;
        this.Height = SystemParameters.VirtualScreenHeight;
        
        // 前置抓取桌面截图用于模糊背景，消除 Window_Loaded 被阻塞导致的白屏闪烁
        CaptureAndSetBackground();
    }

    private void CyclePoem()
    {
        if (ProtectEye.Models.PoemsData.Quotes == null || ProtectEye.Models.PoemsData.Quotes.Length == 0) return;

        var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(0, TimeSpan.FromSeconds(0.5));
        fadeOut.Completed += (s, e) => 
        {
            var rand = new Random();
            TxtQuote.Text = ProtectEye.Models.PoemsData.Quotes[rand.Next(ProtectEye.Models.PoemsData.Quotes.Length)];
            var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(1, TimeSpan.FromSeconds(0.5));
            TxtQuote.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        };
        TxtQuote.BeginAnimation(UIElement.OpacityProperty, fadeOut);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // 背景已经在构造函数抓取
    }
    
    private void CaptureAndSetBackground()
    {
        try
        {
            var width = (int)this.Width;
            var height = (int)this.Height;
            var left = (int)this.Left;
            var top = (int)this.Top;

            if (width <= 0 || height <= 0) return;

            using var bmp = new System.Drawing.Bitmap(width, height);
            using var g = System.Drawing.Graphics.FromImage(bmp);
            g.CopyFromScreen(left, top, 0, 0, new System.Drawing.Size(width, height));
            
            var handle = bmp.GetHbitmap();
            try
            {
                var bmpSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    handle, IntPtr.Zero, Int32Rect.Empty, 
                    System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                BgImage.Source = bmpSource;
            }
            finally
            {
                Win32Api.DeleteObject(handle);
            }
        }
        catch { }
    }
    
    private void UpdateClock()
    {
        TxtCurrentTime.Text = DateTime.Now.ToString("HH:mm:ss");
    }
    
    public void UpdateTime(string timeStr)
    {
        TxtCountdown.Text = timeStr;
    }

    private void BtnSkip_Click(object sender, RoutedEventArgs e)
    {
        _onSkip?.Invoke();
        this.Hide();
    }

    private void BtnExtend_Click(object sender, RoutedEventArgs e)
    {
        _onExtend?.Invoke();
    }

    private void BtnPause_Click(object sender, RoutedEventArgs e)
    {
        _isPaused = !_isPaused;
        BtnPause.Content = _isPaused ? "继续倒计时" : "暂停倒计时";
        
        var colorOff = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#444444");
        var colorOn = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#B71C1C");
        
        BtnPause.Background = new SolidColorBrush(_isPaused ? colorOn : colorOff);
        
        _onPauseToggle?.Invoke(_isPaused);
    }
}
