using System.Text.Json.Serialization;

namespace ProtectEye.Models;

public class AppConfig
{
    // 间隔提醒时间 (分钟), 默认 45
    public int WorkIntervalMinutes { get; set; } = 45;
    
    // 休息倒计时时间 (分钟), 默认 5
    public int RestDurationMinutes { get; set; } = 5;
    
    // 预警时间 (分钟), 默认 2
    public int WarningDurationMinutes { get; set; } = 2;
    
    // 用户闲置判定时间 (分钟), 超过该时间没有键鼠动作，重置全天/间隔计时器
    public int IdleResetThresholdMinutes { get; set; } = 5;
    
    // 背景风格：0=极致黑(纯黑), 1=半透明黑, 2=桌面毛玻璃模糊
    public int BackgroundStyle { get; set; } = 0;
    
    // 主题风格：0=极致黑, 1=极简灰, 2=米黄护眼
    public int AppTheme { get; set; } = 0;
    
    // 休息提示语音/提示音 (预留开关)
    public bool EnableSound { get; set; } = true;
    
    // 智能免打扰 (如果在全屏应用中，则推迟提醒)
    public bool EnableDND { get; set; } = true;

    // 日志保留天数 (超过天数的日志将被自动清理)，默认保留 30 天
    public int MaxLogDays { get; set; } = 30;
    
    // 激励语料
    public string[] MotivationalQuotes { get; set; } = new string[]
    {
        "长风破浪会有时\n\n直挂云帆济沧海",
        "会当凌绝顶\n\n一览众山小",
        "千磨万击还坚劲\n\n任尔东西南北风",
        "非淡泊无以明志\n\n非宁静无以致远",
        "天生我材必有用\n\n千金散尽还复来",
        "宝剑锋从磨砺出\n\n梅花香自苦寒来",
        "博观而约取\n\n厚积而薄发"
    };

    // 开启 20-20-20 微休息提醒: 每连续工作20分钟，弹出20秒远眺提醒
    public bool EnableMicroBreak { get; set; } = true;

    // 是否在大休息界面显示健康护眼提示
    public bool ShowHealthTips { get; set; } = true;

    // 健康评分低于阈值时弹出改善习惯提醒
    public bool EnableHealthScoreAlert { get; set; } = true;
    public int HealthScoreAlertThreshold { get; set; } = 60;

    // 全局快捷键 - 立即休息 (例如 Ctrl+Alt+R)
    public string ImmediateRestShortcut { get; set; } = "Ctrl+Alt+R";

    // 健康护眼贴士语料
    public string[] HealthTips { get; set; } = new string[]
    {
        "💡 请检查坐姿：背部挺直，双脚平放于地面",
        "💡 防止屏幕眩光：显示器顶部应与眼睛齐平或略低",
        "💡 不要忘记眨眼！注视屏幕时人类眨眼频率会下降 60%",
        "💡 伸个懒腰，喝口水，让颈椎也得到休息",
        "💡 屏幕距离眼睛需保持 50-70 厘米，约一臂的距离",
        "💡 适度使用人工泪液或加湿器，能有效缓解眼部干涩",
        "💡 经常转动眼球，有助于放松紧张的眼部肌肉"
    };
}
