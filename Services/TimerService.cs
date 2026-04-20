using System;
using System.Windows.Threading;
using ProtectEye.Services;

namespace ProtectEye.Services;

public enum AppState
{
    Working,    // 正常工作期间
    Warning,    // 预警期间（准备休息前1-2分钟）
    Resting,    // 休息期间
    Paused      // 暂停状态
}

public class TimerService
{
    private DispatcherTimer _timer;
    private int _secondsRemaining;
    public AppState CurrentState { get; private set; } = AppState.Working;

    public int SecondsRemaining => Math.Max(0, _secondsRemaining);
    public int SecondsToNextMicroBreak => ConfigManager.Current.EnableMicroBreak ? Math.Max(0, 20 * 60 - _secondsSinceLastMicroBreak) : -1;

    public event Action<string>? Tick; 
    public event Action<AppState, AppState>? StateChanged;
    public event Action? MicroBreakTriggered;
    
    // To restore from pause
    private AppState _stateBeforePause = AppState.Working;
    private int _secondsSinceLastMicroBreak = 0;
    private DateTime _phaseStartTime;
    private bool _wasIdle = false;

    public TimerService()
    {
        _timer = new DispatcherTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += OnTick;
    }

    public void StartWorking()
    {
        LogService.Log(LogEventType.WorkStarted);
        _secondsSinceLastMicroBreak = 0;
        _wasIdle = false;
        SetState(AppState.Working, ConfigManager.Current.WorkIntervalMinutes * 60);
        _timer.Start();
    }
    
    public void StartResting()
    {
        LogService.Log(LogEventType.RestStarted);
        _secondsSinceLastMicroBreak = 0;
        SetState(AppState.Resting, ConfigManager.Current.RestDurationMinutes * 60);
        if (!_timer.IsEnabled) _timer.Start();
    }

    public void Pause()
    {
        if (CurrentState == AppState.Paused) return;
        _timer.Stop();
        _stateBeforePause = CurrentState;
        LogService.Log(LogEventType.PausedByUser);
        ChangeState(AppState.Paused);
    }
    
    public void Resume()
    {
        if (CurrentState == AppState.Paused)
        {
            LogService.Log(LogEventType.ResumedByUser);
            ChangeState(_stateBeforePause);
            _timer.Start();
        }
    }

    private void SetState(AppState newState, int totalSeconds)
    {
        _phaseStartTime = DateTime.Now;
        _secondsRemaining = totalSeconds;
        ChangeState(newState);
    }

    private void ChangeState(AppState newState)
    {
        var oldState = CurrentState;
        CurrentState = newState;
        StateChanged?.Invoke(oldState, newState);
        InvokeTick();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        // 1. Check Idle Time
        if (CurrentState == AppState.Working || CurrentState == AppState.Warning)
        {
            var idleSeconds = Win32Api.GetIdleTimeSeconds();
            if (idleSeconds > ConfigManager.Current.IdleResetThresholdMinutes * 60)
            {
                _wasIdle = true;
                return; // Pause the timer and wait for user to return
            }
            else if (_wasIdle)
            {
                // User just returned from being idle, reset timer
                _wasIdle = false;
                StartWorking();
                return;
            }
        }

        _secondsRemaining--;

        if (CurrentState == AppState.Working && ConfigManager.Current.EnableMicroBreak)
        {
            _secondsSinceLastMicroBreak++;
            if (_secondsSinceLastMicroBreak >= 20 * 60)
            {
                // 不要紧贴着预警和大休息时报微休息（如果大休息只剩下不足2分钟，不触发）
                if (_secondsRemaining > 120)
                {
                    MicroBreakTriggered?.Invoke();
                }
                _secondsSinceLastMicroBreak = 0;
            }
        }

        if (_secondsRemaining <= 0)
        {
            HandlePhaseTransition();
        }
        else if (CurrentState == AppState.Working && _secondsRemaining == ConfigManager.Current.WarningDurationMinutes * 60)
        {
            // trigger warning phase
            LogService.Log(LogEventType.WarningShown);
            ChangeState(AppState.Warning);
        }
        
        InvokeTick();
    }
    
    private void InvokeTick()
    {
        var timeSpan = TimeSpan.FromSeconds(Math.Max(0, _secondsRemaining));
        Tick?.Invoke(timeSpan.ToString(@"mm\:ss"));
    }

    private void HandlePhaseTransition()
    {
        switch (CurrentState)
        {
            case AppState.Working:
            case AppState.Warning:
                if (ConfigManager.Current.EnableDND && Win32Api.IsForegroundFullScreen())
                {
                    LogService.Log(LogEventType.WorkStarted, "全屏应用检测中，推迟休息 5 分钟");
                    SetState(AppState.Working, 5 * 60);
                }
                else
                {
                    StartResting();
                }
                break;
            case AppState.Resting:
                LogService.Log(LogEventType.RestCompleted);
                StartWorking();
                break;
        }
    }
    
    public void SkipWarning()
    {
        if (CurrentState == AppState.Warning)
        {
            LogService.Log(LogEventType.RestSkipped, "跳过预警");
            StartWorking();
        }
    }
    
    public void SkipRest()
    {
        LogService.Log(LogEventType.RestSkipped);
        StartWorking();
    }
    
    public void ForceRestNow()
    {
        LogService.Log(LogEventType.RestStarted, "用户手动触发");
        StartResting();
    }
    
    public void ExtendRest(int minutes)
    {
        if (CurrentState == AppState.Resting)
        {
            _secondsRemaining += minutes * 60;
            InvokeTick();
        }
    }
    public void UpdateFromConfig()
    {
        var config = ConfigManager.Current;
        double elapsed = (DateTime.Now - _phaseStartTime).TotalSeconds;

        if (CurrentState == AppState.Working)
        {
            _secondsRemaining = (config.WorkIntervalMinutes * 60) - (int)elapsed;
            // 如果新时间已经到了，则进入预警或结束
            if (_secondsRemaining <= config.WarningDurationMinutes * 60)
            {
                // 强制触发一次 OnTick 逻辑由计时器在下一秒处理，或者这里直接处理
            }
        }
        else if (CurrentState == AppState.Resting)
        {
            _secondsRemaining = (config.RestDurationMinutes * 60) - (int)elapsed;
        }
        
        InvokeTick();
    }
}
