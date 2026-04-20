using System;
using System.Windows;
using ProtectEye.Services;
using ProtectEye.Windows;
using System.Drawing;

namespace ProtectEye;

public partial class App : System.Windows.Application
{
    private System.Windows.Forms.NotifyIcon? _notifyIcon;
    private TimerService? _timerService;
    public TimerService? TimerService => _timerService;

    private WarningWindow? _warningWindow;
    private RestWindow? _restWindow;
    private MicroBreakWindow? _microBreakWindow;
    private MainWindow? _settingsWindow;
    private static System.Threading.Mutex? _mutex;

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        const string mutexName = "Global\\ProtectEyeApplicationMutex";
        _mutex = new System.Threading.Mutex(true, mutexName, out bool createdNew);
        if (!createdNew)
        {
            System.Windows.MessageBox.Show("护眼卫士 ProtectEye 已在运行中。", "提示", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            Current.Shutdown();
            return;
        }

        // Create a hidden anchor window to serve as a stable MainWindow
        // This prevents crashes when TrayMenuWindow is closed and helps standardizing the UI context.
        var anchor = new Window { Width = 0, Height = 0, WindowStyle = WindowStyle.None, ShowInTaskbar = false, Visibility = Visibility.Hidden };
        anchor.Show();
        this.MainWindow = anchor;

        _timerService = new TimerService();
        _timerService.StateChanged += OnStateChanged;
        _timerService.Tick += OnTick;
        _timerService.MicroBreakTriggered += OnMicroBreakTriggered;
        
        System.Windows.Interop.ComponentDispatcher.ThreadPreprocessMessage += ComponentDispatcher_ThreadPreprocessMessage;
        RegisterGlobalHotKey();
        
        ChangeTheme(ConfigManager.Current.AppTheme);

        _notifyIcon = new System.Windows.Forms.NotifyIcon();
        var iconUri = new Uri("pack://application:,,,/app.ico");
        var iconStream = System.Windows.Application.GetResourceStream(iconUri)?.Stream;
        _notifyIcon.Icon = iconStream != null 
            ? new System.Drawing.Icon(iconStream) 
            : System.Drawing.SystemIcons.Shield; 
        _notifyIcon.Text = "护眼卫士 (ProtectEye)";
        _notifyIcon.Visible = true;
        _notifyIcon.DoubleClick += (s, args) => ShowSettings(tabIndex: 0);

        _notifyIcon.MouseClick += (s, args) =>
        {
            if (args.Button == System.Windows.Forms.MouseButtons.Right)
            {
                // Use BeginInvoke (Asynchronous) to avoid blocking the WinForms thread and prevent deadlocks
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    // Close existing menu window if it's already open
                    foreach (Window win in System.Windows.Application.Current.Windows)
                    {
                        if (win is TrayMenuWindow)
                        {
                            win.Close();
                            break;
                        }
                    }

                    if (_timerService != null)
                    {
                        var trayMenu = new TrayMenuWindow(_timerService, OnTrayMenuClick);
                        trayMenu.Show();
                    }
                }));
            }
        };
        
        // 记录启动事件
        LogService.Log(LogEventType.AppStarted);
        
        // 自动启动计时
        _timerService.StartWorking();
    }

    private void OnTick(string timeStr)
    {
        Dispatcher.Invoke(() =>
        {
            if (_timerService?.CurrentState == AppState.Working)
            {
                if (_notifyIcon != null) _notifyIcon.Text = $"护眼卫士 · 工作中 ({timeStr})";
            }
            
            _warningWindow?.UpdateTime(timeStr);
            _restWindow?.UpdateTime(timeStr);
        });
    }

    private void OnStateChanged(AppState oldState, AppState newState)
    {
        Dispatcher.Invoke(() =>
        {
            // 关闭所有浮窗重置状态
            if (_warningWindow != null)
            {
                _warningWindow.Hide();
            }
            if (_restWindow != null)
            {
                _restWindow.Close();
                _restWindow = null;
            }
            
            if (newState == AppState.Warning)
            {
                if (_warningWindow == null)
                {
                    _warningWindow = new WarningWindow(
                        () => _timerService?.SkipWarning(),
                        () => _timerService?.ForceRestNow()
                    );
                }
                _warningWindow.Show();
            }
            else if (newState == AppState.Resting)
            {
                if (_restWindow == null)
                {
                    _restWindow = new RestWindow(
                        () => _timerService?.SkipRest(),
                        () => _timerService?.ExtendRest(1),
                        (isPaused) => {
                            if (isPaused) _timerService?.Pause();
                            else _timerService?.Resume();
                        }
                    );
                }
                // 使用 Show 且 Activate
                _restWindow.Show();
                _restWindow.Activate();
            }
            
            if (_notifyIcon != null)
            {
                if (newState == AppState.Paused)
                    _notifyIcon.Text = "护眼卫士 - 已暂停";
                else if (newState == AppState.Resting)
                    _notifyIcon.Text = "护眼卫士 - 正在紧急休息！";
            }
        });
    }
    
    private void OnMicroBreakTriggered()
    {
        Dispatcher.Invoke(() =>
        {
            if (_microBreakWindow == null || !_microBreakWindow.IsLoaded)
            {
                _microBreakWindow = new MicroBreakWindow();
                _microBreakWindow.Closed += (s, e) => _microBreakWindow = null;
                _microBreakWindow.Show();
            }
        });
    }

    private void ShowSettings(int tabIndex = 0)
    {
        if (_settingsWindow == null)
        {
            _settingsWindow = new MainWindow();
            _settingsWindow.Closed += (s, e) => _settingsWindow = null;
        }
        _settingsWindow.Show();
        _settingsWindow.Activate();
        if (tabIndex == 1)
            _settingsWindow.MainTab.SelectedIndex = 1;
        else if (tabIndex == 2)
            _settingsWindow.MainTab.SelectedIndex = 2;
    }

    private void OnTrayMenuClick(string tag)
    {
        switch (tag)
        {
            case "Home": ShowSettings(0); break;
            case "Analysis": ShowSettings(1); break;
            case "Config": ShowSettings(2); break;
            case "RestNow": _timerService?.ForceRestNow(); break;
            case "PauseResume":
                if (_timerService?.CurrentState == AppState.Paused)
                    _timerService.Resume();
                else
                    _timerService?.Pause();
                break;
            case "About":
                System.Windows.MessageBox.Show(
                    $"护眼卫士 (ProtectEye)\n版本：{AppVersion.Version}\n\n定时提醒休息，保护眼部健康。",
                    "关于护眼卫士",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                break;
            case "Exit": Shutdown(); break;
        }
    }

    private void Application_Exit(object sender, ExitEventArgs e)
    {
        LogService.Log(LogEventType.AppExited);
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
    }
    
    public void TriggerRestNow()
    {
        _timerService?.ForceRestNow();
    }

    public void NotifyConfigChanged()
    {
        _timerService?.UpdateFromConfig();
    }

    private const int HOTKEY_ID = 9000;
    public void RegisterGlobalHotKey()
    {
        var shortcut = ConfigManager.Current.ImmediateRestShortcut;
        if (string.IsNullOrWhiteSpace(shortcut)) return;

        uint modifiers = 0;
        uint vk = 0;
        var parts = shortcut.Split('+');
        foreach (var part in parts)
        {
            var p = part.Trim().ToUpper();
            if (p == "CTRL") modifiers |= 0x0002;
            else if (p == "ALT") modifiers |= 0x0001;
            else if (p == "SHIFT") modifiers |= 0x0004;
            else if (p == "WIN") modifiers |= 0x0008;
            else
            {
                if (Enum.TryParse<System.Windows.Forms.Keys>(p, true, out var k))
                {
                    vk = (uint)k;
                }
            }
        }
        
        Win32Api.UnregisterHotKey(IntPtr.Zero, HOTKEY_ID);
        if (vk != 0)
        {
            Win32Api.RegisterHotKey(IntPtr.Zero, HOTKEY_ID, modifiers, vk);
        }
    }

    private void ComponentDispatcher_ThreadPreprocessMessage(ref System.Windows.Interop.MSG msg, ref bool handled)
    {
        const int WM_HOTKEY = 0x0312;
        if (msg.message == WM_HOTKEY && msg.wParam.ToInt32() == HOTKEY_ID)
        {
            TriggerRestNow();
            handled = true;
        }
    }

    public void ChangeTheme(int themeIndex)
    {
        string themeName = themeIndex switch
        {
            1 => "ThemeGray.xaml",
            2 => "ThemeBeige.xaml",
            _ => "ThemeDark.xaml"
        };
        var resDict = new ResourceDictionary 
        { 
            Source = new Uri($"pack://application:,,,/Themes/{themeName}") 
        };
        System.Windows.Application.Current.Resources.MergedDictionaries.Clear();
        System.Windows.Application.Current.Resources.MergedDictionaries.Add(resDict);
    }
}
