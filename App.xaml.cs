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

        var contextMenu = new System.Windows.Forms.ContextMenuStrip();
        
        var menuItemHome = contextMenu.Items.Add("🏠 首页");
        menuItemHome.Click += (s, args) => ShowSettings(tabIndex: 0);

        var menuItemLog = contextMenu.Items.Add("📊 用眼分析");
        menuItemLog.Click += (s, args) => ShowSettings(tabIndex: 1);

        var menuItemSettings = contextMenu.Items.Add("⚙ 设置管理");
        menuItemSettings.Click += (s, args) => ShowSettings(tabIndex: 2);
        
        contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        
        var menuItemRestNow = contextMenu.Items.Add("☕ 立即休息");
        menuItemRestNow.Click += (s, args) => _timerService?.ForceRestNow();
        
        var menuItemPause = contextMenu.Items.Add("⏸ 暂停 / 继续");
        menuItemPause.Click += (s, args) => 
        {
            if (_timerService.CurrentState == AppState.Paused)
                _timerService.Resume();
            else
                _timerService.Pause();
        };

        contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        var menuAbout = contextMenu.Items.Add($"ℹ 关于护眼卫士 {AppVersion.Version}");
        menuAbout.Click += (s, args) => System.Windows.MessageBox.Show(
            $"护眼卫士 (ProtectEye)\n版本：{AppVersion.Version}\n\n定时提醒休息，保护眼部健康。",
            "关于护眼卫士",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
        
        contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        
        var menuItemExit = contextMenu.Items.Add("❌ 彻底退出");
        menuItemExit.Click += (s, args) => Shutdown();

        _notifyIcon.ContextMenuStrip = contextMenu;
        
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
